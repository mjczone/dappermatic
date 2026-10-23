// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using MJCZone.DapperMatic.Dml.Rest;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.Dml.Internal;

/// <summary>
/// The caller a scope's statements run as: who they are, the policies they are held to, the datasource the
/// connection is, and the request's values the policies read, resolved once.
/// </summary>
internal sealed class DmlCaller
{
    private static readonly ConcurrentDictionary<string, RestNode> Conditions = new(StringComparer.Ordinal);

    private readonly IReadOnlyDictionary<string, object?> _values;
    private readonly ConcurrentDictionary<(string? Schema, string Table, string Columns, DmAction Action), DmAccessRules> _rules = new();

    private DmlCaller(DmAccessContext access, IReadOnlyList<DmPolicy> policies, string datasourceId, IReadOnlyDictionary<string, object?> values)
    {
        Access = access;
        Policies = policies;
        DatasourceId = datasourceId;
        _values = values;
    }

    /// <summary>Gets the caller, with their tenant and values.</summary>
    public DmAccessContext Access { get; }

    /// <summary>Gets the policies the caller is held to.</summary>
    public IReadOnlyList<DmPolicy> Policies { get; }

    /// <summary>Gets the datasource the policies know the connection by.</summary>
    public string DatasourceId { get; }

    /// <summary>
    /// Gets or sets what is told of each table a statement is about to write to, by schema and name: before the write,
    /// which may not be committed yet.
    /// </summary>
    public Action<string?, string>? Writing { get; set; }

    /// <summary>
    /// Gets or sets what hears the changes the caller's writes make, in their transactions, or null for nothing.
    /// </summary>
    public IDmChangeSink? Changes { get; set; }

    /// <summary>
    /// Makes a caller, resolving every value of the request its policies read: a value read through SQL or code is
    /// read now, so that statements are written without waiting.
    /// </summary>
    /// <param name="access">Who the caller is, with their tenant and values.</param>
    /// <param name="policies">The policies.</param>
    /// <param name="datasourceId">The datasource the policies know the connection by.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The caller, with its values read.</returns>
    /// <exception cref="ArgumentException">A policy cannot work; the message says which and why.</exception>
    public static async Task<DmlCaller> CreateAsync(
        DmAccessContext access,
        IEnumerable<DmPolicy> policies,
        string datasourceId,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentException.ThrowIfNullOrWhiteSpace(datasourceId);
        access = access.ForDatasource(datasourceId);
        var list = policies.ToList();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var policy in list)
        {
            DmPolicies.Validate(policy);
            foreach (var text in new[] { policy.Filter, policy.Check })
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    paths.UnionWith(RestValues.Parameters(Parse(text)));
                }
            }

            paths.UnionWith(policy.Presets.Values.OfType<string>().Where(v => v.Length > 1 && v[0] == '@').Select(v => v[1..]));
        }

        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            values[path] = await access.GetValueAsync(path, cancellationToken).ConfigureAwait(false);
        }

        return new DmlCaller(access, list, datasourceId, values);
    }

    /// <summary>
    /// Works out what the policies let the caller do to a table.
    /// </summary>
    /// <param name="schema">The table's schema, or null on a provider without schemas.</param>
    /// <param name="table">The table.</param>
    /// <param name="columns">The table's columns, as the model maps them.</param>
    /// <param name="action">The action.</param>
    /// <returns>The rules.</returns>
    public DmAccessRules Rules(string? schema, string table, IReadOnlyCollection<string> columns, DmAction action) =>
        _rules.GetOrAdd(
            (schema?.ToLowerInvariant(), table.ToLowerInvariant(), string.Join('\u001f', columns), action),
            key => DmPolicies.Evaluate(Policies, Access, new DmResource(DatasourceId, schema, table, columns), action)
        );

    /// <summary>
    /// Reads a policy's condition, with the request's values in place.
    /// </summary>
    /// <param name="text">The condition in the query syntax, or null for none.</param>
    /// <returns>The condition, or null for none.</returns>
    public RestNode? Condition(string? text) => string.IsNullOrWhiteSpace(text) ? null : RestValues.Substitute(Parse(text), _values);

    /// <summary>
    /// Reads the value a policy writes in a column.
    /// </summary>
    /// <param name="table">The table, for the message.</param>
    /// <param name="column">The column.</param>
    /// <param name="preset">The preset: a path such as <c>@tenant.id</c>, or a fixed text.</param>
    /// <returns>The value.</returns>
    /// <exception cref="UnauthorizedAccessException">The request does not have the value the preset names.</exception>
    public object? Preset(string table, string column, string? preset)
    {
        if (preset is not { Length: > 1 } path || path[0] != '@')
        {
            return preset;
        }

        // A value the request does not have is not written as NULL: a row with no tenant is everyone's.
        return _values.GetValueOrDefault(path[1..]) is { } value and not DBNull
            ? value
            : throw new UnauthorizedAccessException($"The policies set the column {column} of {table} to {path}, which this caller does not have.");
    }

    private static RestNode Parse(string text) => Conditions.GetOrAdd(text, RestParser.ParseFilter);
}
