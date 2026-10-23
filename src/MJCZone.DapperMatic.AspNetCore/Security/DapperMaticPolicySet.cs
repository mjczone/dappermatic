// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Security;

/// <summary>
/// The policies in force: those of <see cref="DapperMaticOptions.Policies"/>, given in code or configuration, with
/// <see cref="DapperMaticOptions.RequireRole"/> and <see cref="DapperMaticOptions.ReadOnlyRole"/> as policies of their
/// own. Every policy is validated when the set is built, at startup.
/// </summary>
public sealed partial class DapperMaticPolicySet
{
    // Resources whose writes are not changes to a schema, and those that are not in one.
    private static readonly string[] NotSchemaChanges = ["datatypes", "unknown", string.Empty];
    private static readonly string[] NotInSchema = ["datasources", "datatypes", "policies", "tenants", "webhooks", "schemas", "functions", "unknown", string.Empty];

    private readonly IReadOnlyList<DmPolicy> _configured;
    private readonly IReadOnlyList<DmPolicy> _held;
    private readonly DapperMaticStoredAccess? _stored;
    private readonly DapperMaticTenancyOptions _tenancy;
    private Combined _combined;

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticPolicySet"/> class.
    /// </summary>
    /// <param name="options">The options holding the policies.</param>
    /// <param name="stored">The policies kept by DapperMatic, edited through its access endpoints; null for none.</param>
    /// <exception cref="InvalidOperationException">A policy cannot work; the message says which and why.</exception>
    public DapperMaticPolicySet(IOptions<DapperMaticOptions> options, DapperMaticStoredAccess? stored = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        var settings = options.Value;
        var policies = new List<DmPolicy>();
        foreach (var policy in settings.Policies)
        {
            policy.Source ??= "configuration";
            policies.Add(policy);
        }

        if (!string.IsNullOrWhiteSpace(settings.RequireRole))
        {
            policies.Add(Shorthand(DmPolicy.Allow(DmAction.All, settings.RequireRole), nameof(DapperMaticOptions.RequireRole)));
        }

        if (!string.IsNullOrWhiteSpace(settings.ReadOnlyRole))
        {
            policies.Add(Shorthand(DmPolicy.Allow(DmAction.Read | DmAction.ReadSchema, settings.ReadOnlyRole), nameof(DapperMaticOptions.ReadOnlyRole)));
        }

        foreach (var policy in policies)
        {
            try
            {
                DmPolicies.Validate(policy);
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException($"DapperMatic: {ex.Message}", ex);
            }
        }

        var tenancy = settings.Tenancy;
        foreach (var column in new[] { tenancy.RowsColumn }.Concat(tenancy.RowsColumns.Values).OfType<string>())
        {
            if (!Identifier().IsMatch(column))
            {
                throw new InvalidOperationException($"DapperMatic: '{column}' is not a column name that can mark a tenant's rows.");
            }
        }

        if (tenancy.TenantRulesReach is { } reach && !ValuePath().IsMatch(reach))
        {
            throw new InvalidOperationException($"DapperMatic: '{reach}' is not a value of the request, such as @tenant.below.");
        }

        _configured = policies;
        _stored = stored;
        _tenancy = tenancy;
        _held = [.. policies.SelectMany(Hold)];
        _combined = new Combined([], _held);
    }

    /// <summary>Gets the policies in force: those of code and configuration, then those kept by DapperMatic.</summary>
    public IReadOnlyList<DmPolicy> Policies
    {
        get
        {
            if (_stored is null)
            {
                return _held;
            }

            var stored = _stored.Policies;
            var combined = Volatile.Read(ref _combined);
            if (!ReferenceEquals(combined.Stored, stored))
            {
                combined = new Combined(stored, [.. _held, .. stored.SelectMany(Hold)]);
                Volatile.Write(ref _combined, combined);
            }

            return combined.All;
        }
    }

    /// <summary>Gets the policies of code and configuration, which the access endpoints cannot change.</summary>
    public IReadOnlyList<DmPolicy> Configured => _configured;

    /// <summary>Gets a version of the policies and tenants kept by DapperMatic: it changes when they do.</summary>
    internal string StoredVersion => _stored?.Version ?? string.Empty;

    /// <summary>
    /// Works out what the policies let a caller do to a resource.
    /// </summary>
    /// <param name="context">The caller, and the request's tenant and values.</param>
    /// <param name="resource">What the request acts on.</param>
    /// <param name="action">The action.</param>
    /// <returns>The rules.</returns>
    public DmAccessRules Evaluate(DmAccessContext context, DmResource resource, DmAction action) =>
        DmPolicies.Evaluate(Policies, context, resource, action);

    /// <summary>
    /// The action an operation performs: <c>tables/query</c> reads rows, <c>tables/insert-rows</c> creates them,
    /// <c>tables/get</c> reads a definition, <c>tables/post</c> changes the schema, <c>datasources/post</c> manages
    /// datasources. An operation DapperMatic does not know, or none (a context the request's endpoint did not fill), is
    /// <see cref="DmAction.None"/>, which no policy allows.
    /// </summary>
    /// <param name="operation">The operation, as <see cref="DapperMaticOperation.Operation"/> names it.</param>
    /// <returns>The action.</returns>
    public static DmAction ActionOf(string? operation)
    {
        var (resource, verb) = Split(operation);
        return verb switch
        {
            "query" => DmAction.Read,
            "insert-rows" or "save-rows" => DmAction.Create,
            "update-rows" => DmAction.Update,
            "delete-rows" => DmAction.Delete,
            "effective" => DmAction.ReadSchema,
            // The caller's own API: as the row endpoints, let through, each table narrowed to what the caller may do.
            _ when resource == "openapi" => DmAction.Read,
            _ when resource == "functions" => DmAction.Execute,
            _ when resource is "policies" or "tenants" or "webhooks" or "access" => DmAction.ManageAccess,
            "get" or "list" or "exists" => DmAction.ReadSchema,
            "post" or "put" or "patch" or "delete" or "test" when resource == "datasources" => DmAction.ManageDatasources,
            "post" or "put" or "patch" or "delete" when !NotSchemaChanges.Contains(resource) => DmAction.ChangeSchema,
            _ => DmAction.None,
        };
    }

    /// <summary>
    /// What an operation acts on: a listing of datasources or schemas is about every one of them, and is narrowed
    /// item by item; any other operation is about the datasource, schema and table or view its route names, the schema
    /// being the tenant's when the route names none.
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <returns>The resource.</returns>
    public static DmResource ResourceOf(DapperMaticOperation context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var (resource, verb) = Split(context.Operation);
        var datasource = context.DatasourceId is { Length: > 0 } id ? id : DmResource.Any;
        if (resource == "datasources" && verb == "post" && datasource == DmResource.Any)
        {
            // A datasource added without an id gets one no policy's list of datasources names.
            datasource = string.Empty;
        }

        if (resource == "functions")
        {
            // A function is the datasource's, in no schema: the policies covering it name it, or no schema or table.
            var function = context.RouteValues?.FirstOrDefault(v => string.Equals(v.Key, "functionName", StringComparison.OrdinalIgnoreCase)).Value;
            return new DmResource(datasource, DmResource.Any, null) { FunctionName = function ?? string.Empty };
        }

        if (resource is "datasources" or "datatypes" or "policies" or "tenants" or "webhooks" || (resource == "access" && verb != "effective"))
        {
            return new DmResource(verb == "list" && resource == "datasources" ? DmResource.Any : datasource, DmResource.Any, null);
        }

        var schema = context.SchemaName is { Length: > 0 } s && s != "_" ? s : context.Access?.Tenant?.SchemaName;
        if (resource == "schemas")
        {
            return new DmResource(datasource, verb == "list" ? DmResource.Any : schema, null);
        }

        return new DmResource(datasource, schema, verb == "list" && resource is "tables" or "views" ? null : context.TableName ?? context.ViewName);
    }

    /// <summary>
    /// Whether an operation acts in a schema (a table, view or their parts, or rows), rather than on a datasource as a
    /// whole, its listings, or access.
    /// </summary>
    /// <param name="operation">The operation, as <see cref="DapperMaticOperation.Operation"/> names it.</param>
    /// <returns>True if it does.</returns>
    internal static bool ActsInSchema(string? operation)
    {
        var (resource, verb) = Split(operation);
        return !NotInSchema.Contains(resource) && (resource != "access" || verb == "effective");
    }

    /// <summary>
    /// Tells whether a policy's filter on a table limits its rows to the tenant's only, as holding a tenant's policy
    /// does, or not at all: what a tenant's webhook, which hears only its tenant's rows, may report every row of.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="filter">The policy's filter, as held.</param>
    /// <returns>True if it does no more than that.</returns>
    internal bool OnlyFences(string table, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        if ((_tenancy.RowsColumns.GetValueOrDefault(table) ?? _tenancy.RowsColumn) is not { } column)
        {
            return false;
        }

        var fence = _tenancy.TenantRulesReach is { } reach ? $"{column} in {reach}" : $"{column} eq @tenant.id";
        return string.Equals(filter, fence, StringComparison.Ordinal);
    }

    // A tenant's policy, held to the tenant's rows: a copy per table, whose filter and check are ANDed with the column
    // that marks the table's rows compared with the tenant, or the tenants its administrators reach. One naming no
    // table cannot be held so: it keeps only access management (the access endpoints keep no other).
    private IEnumerable<DmPolicy> Hold(DmPolicy policy)
    {
        if (policy.Tenant is null)
        {
            yield return policy;
            yield break;
        }

        var marked = _tenancy.RowsColumn is not null || _tenancy.RowsColumns.Count > 0;
        if (!marked)
        {
            // The tenant's datasource or schema holds it, set when it was kept.
            yield return policy;
            yield break;
        }

        if (policy.Tables.Count == 0)
        {
            var managing = policy.Copy();
            managing.Actions &= DmAction.ManageAccess;
            if (managing.Actions != DmAction.None)
            {
                yield return managing;
            }

            yield break;
        }

        foreach (var table in policy.Tables)
        {
            var held = policy.Copy();
            held.Tables.Clear();
            held.Tables.Add(table);
            if ((_tenancy.RowsColumns.GetValueOrDefault(table) ?? _tenancy.RowsColumn) is { } column)
            {
                var fence = _tenancy.TenantRulesReach is { } reach ? $"{column} in {reach}" : $"{column} eq @tenant.id";
                held.Filter = string.IsNullOrWhiteSpace(policy.Filter) ? fence : $"({policy.Filter}) and {fence}";
                held.Check = string.IsNullOrWhiteSpace(policy.Check) ? null : $"({policy.Check}) and {fence}";

                // A row written is the tenant's: the column is set to it, where the policy reaches one tenant.
                if (_tenancy.TenantRulesReach is null)
                {
                    held.Presets[column] = "@tenant.id";
                }
            }

            yield return held;
        }
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex Identifier();

    [GeneratedRegex(@"^@[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)+$")]
    private static partial Regex ValuePath();

    private static DmPolicy Shorthand(DmPolicy policy, string option)
    {
        policy.Name = $"{option}: {policy.Roles[0]}";
        policy.Source = "options";
        return policy;
    }

    private static (string Resource, string Verb) Split(string? operation)
    {
        var text = (operation ?? string.Empty).ToLowerInvariant();
        var slash = text.LastIndexOf('/');
        return slash < 0 ? (text, string.Empty) : (text[..slash], text[(slash + 1)..]);
    }

    // The policies in force, for the stored ones they were combined with.
    private sealed class Combined(IReadOnlyList<DmPolicy> stored, IReadOnlyList<DmPolicy> all)
    {
        public IReadOnlyList<DmPolicy> Stored { get; } = stored;

        public IReadOnlyList<DmPolicy> All { get; } = all;
    }
}
