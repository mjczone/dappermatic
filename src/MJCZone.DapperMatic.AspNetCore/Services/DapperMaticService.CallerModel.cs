// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

#pragma warning disable SA1402 // The caller's model and its parts belong together.
#pragma warning disable SA1649

using System.Security.Cryptography;
using System.Text;
using MJCZone.DapperMatic.AspNetCore.Caching;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.Dml.Rest;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Services;

/// <summary>
/// What one caller may use of a datasource: the shape every description of it is built from (the caller's OpenAPI
/// document, their GraphQL schema).
/// </summary>
public partial class DapperMaticService
{
    /// <summary>
    /// Describes what a caller may use of a datasource's schema: each table and view it may do anything to, the actions
    /// it may take there and the columns it may read and write, the declared relations between them, and the functions
    /// it may run.
    /// </summary>
    /// <param name="context">The operation context, whose caller is described.</param>
    /// <param name="datasourceId">The datasource.</param>
    /// <param name="schemaName">The schema, or null for the default or the tenant's.</param>
    /// <param name="withFunctions">Whether to include the functions the caller may run.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The model.</returns>
    internal async Task<DapperMaticCallerModel> DescribeForCallerAsync(
        DapperMaticOperation context,
        string datasourceId,
        string? schemaName,
        bool withFunctions,
        CancellationToken cancellationToken
    )
    {
        schemaName = NormalizeSchemaName(schemaName);
        var key = await CallerModelKeyAsync(context, datasourceId, schemaName, withFunctions).ConfigureAwait(false);
        if (key is not null && _callerModels!.TryGet(key, out var kept))
        {
            return kept!;
        }

        var tables = new List<DapperMaticCallerTable>();
        DbProviderType provider;
        string? resolvedSchema;
        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            provider = connection.GetDbProviderType();
            resolvedSchema = SchemaOf(connection, schemaName);
            foreach (var name in (await connection.GetTableNamesAsync(schemaName, null, null, cancellationToken).ConfigureAwait(false)).Where(n => !IsPrivate(n)))
            {
                var table = await DescribeTableAsync(datasourceId, connection, schemaName, name, cancellationToken).ConfigureAwait(false);
                var rules = await AllowedAsync(context, connection, datasourceId, schemaName, table, TableActions).ConfigureAwait(false);
                if (rules.Count > 0)
                {
                    tables.Add(new DapperMaticCallerTable(table, View: false, rules));
                }
            }

            foreach (var name in await connection.GetViewNamesAsync(schemaName, null, null, cancellationToken).ConfigureAwait(false))
            {
                var qualified = connection.GetSchemaQualifiedTableName(schemaName, name);
                var view = await DescribeViewAsync(datasourceId, connection, qualified, schemaName, name).ConfigureAwait(false);
                var rules = await AllowedAsync(context, connection, datasourceId, schemaName, view, [("views/query", DmAction.Read)]).ConfigureAwait(false);
                if (rules.Count > 0)
                {
                    tables.Add(new DapperMaticCallerTable(view, View: true, rules));
                }
            }
        }

        // The declared relations between tables the caller may use, each from a table to another of the model.
        foreach (var table in tables.Where(t => !t.View))
        {
            foreach (var relation in _options.Relations.Where(r =>
                (r.DatasourceId is null || string.Equals(r.DatasourceId, datasourceId, StringComparison.OrdinalIgnoreCase))
                && (r.SchemaName is null || string.Equals(r.SchemaName, table.Table.SchemaName, StringComparison.OrdinalIgnoreCase))
                && string.Equals(r.TableName, table.Table.Name, StringComparison.OrdinalIgnoreCase)
            ))
            {
                if (tables.Find(t => !t.View && string.Equals(t.Table.Name, relation.OtherTableName, StringComparison.OrdinalIgnoreCase)) is { } other)
                {
                    table.Relations.Add(new DapperMaticCallerRelation(relation.Name, other, [.. relation.Columns.Zip(relation.OtherColumns)], relation.Many));
                }
            }
        }

        var functions = new List<DapperMaticFunction>();
        if (withFunctions)
        {
            foreach (var function in _options.Functions.Where(f => f.DatasourceId is null || string.Equals(f.DatasourceId, datasourceId, StringComparison.OrdinalIgnoreCase)))
            {
                if (FunctionOf(datasourceId, function.Name) == function && await MayExecuteAsync(context, function.Name).ConfigureAwait(false))
                {
                    functions.Add(function);
                }
            }
        }

        var model = new DapperMaticCallerModel(datasourceId, schemaName, resolvedSchema, provider, tables, functions);
        if (key is not null)
        {
            _callerModels!.Set(key, model, _schemaCache.Duration);
        }

        return model;
    }

    // What decides a caller's model: the datasource and schema; whether the caller is signed in, their roles (each
    // identity's own role claim type), and their values of every claim type a policy names; their tenant; and the
    // versions of the policies, tenants and descriptions it was built from. Null when the model is not kept: code
    // decides (a policy's predicate, the application's own permissions) and the application gives no key for it.
    private async Task<string?> CallerModelKeyAsync(DapperMaticOperation context, string datasourceId, string? schemaName, bool withFunctions)
    {
        if (_callerModels is null || _schemaCache.Duration <= TimeSpan.Zero)
        {
            return null;
        }

        var policies = _policies.Policies;
        string? decided = null;
        if (_permissions is not DefaultDapperMaticPermissions || policies.Any(p => p.Predicate is not null))
        {
            if (_options.CallerModelCacheKey?.Invoke(context) is not { } part)
            {
                return null;
            }

            decided = part;
        }

        var access = AccessOf(context);
        var text = new StringBuilder();
        void Part(object? value) => text.Append(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)?.ToLowerInvariant()).Append('\u001f');
        Part(datasourceId);
        Part(schemaName ?? await DefaultSchemaOfAsync(datasourceId).ConfigureAwait(false));
        Part(withFunctions);
        Part(access.IsAuthenticated);
        Part(string.Join('\u001e', access.User.Identities.SelectMany(i => i.FindAll(i.RoleClaimType)).Select(c => c.Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)));
        foreach (var type in policies.SelectMany(p => p.Claims.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            text.Append(type).Append('=');
            Part(string.Join('\u001e', access.User.FindAll(type).Select(c => c.Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)));
        }

        // An API key's limits: two keys of one owner may see different models.
        Part(access.Limits is { Count: > 0 } limits ? Convert.ToHexStringLower(SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(limits))) : null);
        Part(access.Tenant?.Id);
        Part(access.Tenant?.DatasourceId);
        Part(access.Tenant?.SchemaName);
        Part(_policies.StoredVersion);
        Part(_schemaCache.GenerationOf(datasourceId));
        Part(decided);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }
}

/// <summary>
/// What one caller may use of a datasource's schema.
/// </summary>
/// <param name="DatasourceId">The datasource.</param>
/// <param name="SchemaName">The schema asked for, or null for the default or the tenant's.</param>
/// <param name="ResolvedSchema">The schema the tables are in, as resolved (the provider's default when none was asked for).</param>
/// <param name="Provider">The datasource's database.</param>
/// <param name="Tables">The tables and views the caller may do anything to.</param>
/// <param name="Functions">The functions the caller may run.</param>
#pragma warning disable SA1313 // Positional record parameters are properties.
internal sealed record DapperMaticCallerModel(
    string DatasourceId,
    string? SchemaName,
    string? ResolvedSchema,
    DbProviderType Provider,
    IReadOnlyList<DapperMaticCallerTable> Tables,
    IReadOnlyList<DapperMaticFunction> Functions
)
#pragma warning restore SA1313
{
    /// <summary>
    /// Gets a fingerprint of the model's shape: callers whose models have the same one are described alike, so a
    /// description built for one serves the others.
    /// </summary>
    public string Fingerprint { get; } = Compute(Tables, Functions);

    private static string Compute(IReadOnlyList<DapperMaticCallerTable> tables, IReadOnlyList<DapperMaticFunction> functions)
    {
        var text = new StringBuilder();
        foreach (var table in tables.OrderBy(t => t.Table.Name, StringComparer.Ordinal))
        {
            text.Append("T|").Append(table.Table.Name).Append('|').Append(table.View).Append('|').Append(table.Table.Generated).Append('|')
                .Append(string.Join(',', table.Table.Keys)).Append('|').Append(table.Table.Searchable is not null).Append('\n');
            foreach (var column in table.Table.Columns)
            {
                text.Append("C|").Append(column.Name).Append('|').Append(column.Type).Append('|').Append(column.Nullable).Append('|').Append(column.Filled).Append('\n');
            }

            foreach (var (action, rules) in table.Rules.OrderBy(r => r.Key))
            {
                text.Append("A|").Append(action).Append('|').Append(rules.Columns is null ? "*" : string.Join(',', rules.Columns.Order(StringComparer.Ordinal)))
                    .Append('|').Append(string.Join(',', rules.Presets.Keys.Order(StringComparer.Ordinal))).Append('\n');
            }

            foreach (var relation in table.Relations)
            {
                text.Append("R|").Append(relation.Name).Append('|').Append(relation.Other.Table.Name).Append('|').Append(relation.Many).Append('|')
                    .Append(string.Join(',', relation.Keys.Select(k => k.This + "=" + k.Other))).Append('\n');
            }
        }

        foreach (var function in functions.OrderBy(f => f.Name, StringComparer.Ordinal))
        {
            text.Append("F|").Append(function.Name).Append('|').Append(function.Writes.Length > 0).Append('|')
                .Append(string.Join(',', function.Parameters.Where(p => p.Value is null).Select(p => $"{p.Name}:{p.Type}:{p.Nullable}:{p.Default is null}"))).Append('\n');
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }
}

/// <summary>
/// A table or view a caller may use, and what it may do there.
/// </summary>
/// <param name="Table">The table or view, as the database describes it.</param>
/// <param name="View">Whether it is a view.</param>
/// <param name="Rules">What the policies let the caller do, by action: only the actions allowed.</param>
#pragma warning disable SA1313
internal sealed record DapperMaticCallerTable(RestTable Table, bool View, IReadOnlyDictionary<DmAction, DmAccessRules> Rules)
#pragma warning restore SA1313
{
    /// <summary>Gets the declared relations from this table to others the caller may use.</summary>
    public List<DapperMaticCallerRelation> Relations { get; } = [];

    /// <summary>
    /// Gets the columns the caller may use for an action: read for reads, written (the policies' presets left out)
    /// for writes; none when the action is not allowed.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <returns>The columns.</returns>
    public IEnumerable<RestColumn> Columns(DmAction action) =>
        Rules.TryGetValue(action, out var rules)
            ? Table.Columns.Where(c => (rules.Columns is not { } allowed || allowed.Contains(c.Name)) && (action == DmAction.Read || !rules.Presets.ContainsKey(c.Name)))
            : [];

    /// <summary>Tells whether the caller may take an action.</summary>
    /// <param name="action">The action.</param>
    /// <returns>True if it may.</returns>
    public bool May(DmAction action) => Rules.ContainsKey(action);
}

/// <summary>
/// A declared relation between two tables of a caller's model.
/// </summary>
/// <param name="Name">The relation's name.</param>
/// <param name="Other">The related table.</param>
/// <param name="Keys">The key pairs: this table's column, the other's.</param>
/// <param name="Many">Whether a row relates to many rows.</param>
#pragma warning disable SA1313
internal sealed record DapperMaticCallerRelation(string Name, DapperMaticCallerTable Other, IReadOnlyList<(string This, string Other)> Keys, bool Many);
#pragma warning restore SA1313
