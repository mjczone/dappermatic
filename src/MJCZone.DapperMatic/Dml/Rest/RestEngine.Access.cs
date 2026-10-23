// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.Dml.Rest;

/// <summary>
/// What the policies allow on each table, and the values their conditions read.
/// </summary>
internal sealed class RestAccess
{
    /// <summary>Gets the rules for an action on a table.</summary>
    public required Func<RestTable, DmAction, DmAccessRules> Rules { get; init; }

    /// <summary>Gets a value of the request's context by its path, such as <c>tenant.below</c>.</summary>
    public required Func<string, CancellationToken, ValueTask<object?>> Value { get; init; }
}

/// <summary>
/// The policies' part of running a request: each table's rules for the action, applied to every table the request
/// reads, with the values their conditions read resolved once.
/// </summary>
internal sealed partial class RestEngine
{
    private static readonly ConcurrentDictionary<string, RestNode> PolicyConditions = new(StringComparer.Ordinal);

    private readonly Dictionary<(RestTable Table, DmAction Action), DmAccessRules> _rules = [];
    private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<RestTable, RestNode?> _readFilters = new(ReferenceEqualityComparer.Instance);

    // The rules for an action on a table: everything allowed when the engine runs without policies.
    private DmAccessRules Rules(RestTable table, DmAction action)
    {
        if (_access is null)
        {
            return DmAccessRules.Unrestricted;
        }

        if (!_rules.TryGetValue((table, action), out var rules))
        {
            rules = _access.Rules(table, action);
            _rules[(table, action)] = rules;
        }

        return rules;
    }

    private DmAccessRules Require(RestTable table, DmAction action)
    {
        var rules = Rules(table, action);
        var verb = action switch
        {
            DmAction.Read => "read",
            DmAction.Create => "insert into",
            DmAction.Update => "update",
            _ => "delete from",
        };
        return rules.Allowed ? rules : throw new UnauthorizedAccessException($"No policy lets this caller {verb} {table.Name}.");
    }

    // A policy condition, parsed once for all requests.
    private static RestNode? Condition(string? text) => text is null ? null : PolicyConditions.GetOrAdd(text, RestParser.ParseFilter);

    // A policy's condition, kept out of the request's limits.
    private static RestNode? Trusted(RestNode? node) => node is null ? null : new RestTrusted(node);

    private static RestNode? And(RestNode? left, RestNode? right) =>
        (left, right) switch
        {
            (null, _) => right,
            (_, null) => left,
            _ => new RestBinary("and", left, right),
        };

    // The read filter of a table the request reads, with its values in place; resolved before translation.
    private RestNode? ReadFilter(RestTable table) => _readFilters.GetValueOrDefault(table);

    private async Task PrepareReadFiltersAsync(IEnumerable<RestTable> tables, CancellationToken cancellationToken)
    {
        foreach (var table in tables.Distinct(ReferenceEqualityComparer.Instance).Cast<RestTable>())
        {
            if (!_readFilters.ContainsKey(table))
            {
                _readFilters[table] = Trusted(await BindAsync(Condition(Require(table, DmAction.Read).Filter), cancellationToken).ConfigureAwait(false));
            }
        }
    }

    // Resolves the values a condition reads, then puts them in place.
    private async Task<RestNode?> BindAsync(RestNode? node, CancellationToken cancellationToken)
    {
        if (node is null)
        {
            return null;
        }

        foreach (var path in RestValues.Parameters(node))
        {
            if (!_values.ContainsKey(path))
            {
                _values[path] = _access is null ? null : await _access.Value(path, cancellationToken).ConfigureAwait(false);
            }
        }

        return RestValues.Substitute(node, _values);
    }

    // A request names values of the context only through policies: they are not the caller's to read.
    private static void RefuseValues(RestQuery query)
    {
        var nodes = new Stack<RestQuery>([query]);
        while (nodes.TryPop(out var q))
        {
            foreach (var node in new[] { q.Filter }.Concat(q.OrderBy.Select(o => o.Key)).OfType<RestNode>())
            {
                if (RestValues.Parameters(node).FirstOrDefault() is { } path)
                {
                    throw new ArgumentException($"@{path} is a value policies read; a request compares columns with values it gives.");
                }
            }

            q.Expand.ToList().ForEach(e => nodes.Push(e.Query));
        }
    }

    private static void RefuseValues(RestNode? filter) => RefuseValues(new RestQuery { Filter = filter });

    // The columns a caller may read of a table, or null for all.
    private IReadOnlySet<string>? Readable(RestTable table) => Rules(table, DmAction.Read).Columns;

    // A query selecting, where it selects nothing in particular, the columns the caller may read; its expands too.
    private RestQuery Effective(
        RestQuery query,
        RestTable table,
        IReadOnlyDictionary<(RestTable Table, string Name), RestRelation> relations
    )
    {
        var select = query.Select;
        if (select.Count == 0 && Readable(table) is { } allowed)
        {
            select = [.. table.Columns.Select(c => c.Name).Where(allowed.Contains)];
            if (select.Count == 0)
            {
                throw new UnauthorizedAccessException($"No column of {table.Name} is one this caller may read.");
            }
        }

        return query with
        {
            Select = select,
            Expand = [.. query.Expand.Select(e => e with { Query = Effective(e.Query, relations[(table, e.Relation)].Other, relations) })],
        };
    }

    // A request reads only columns it may: those it selects, orders by and compares, on every table it reaches, and
    // the keys of every relation it follows, which the join compares. A write's condition is held to the same: a count
    // of the rows changed would otherwise tell what reading would not.
    private void RequireReadable(
        RestQuery query,
        RestTable table,
        IReadOnlyDictionary<(RestTable Table, string Name), RestRelation> relations
    )
    {
        if (_access is null)
        {
            return;
        }

        void Column(RestTable owner, string column)
        {
            if (Readable(owner) is { } allowed && !allowed.Contains(column))
            {
                throw new UnauthorizedAccessException($"The column {column} of {owner.Name} is not one this caller may read.");
            }
        }

        RestTable Follow(RestTable from, string name)
        {
            var relation = relations.TryGetValue((from, name), out var found) ? found : throw new ArgumentException($"'{name}' is not a relation of {from.Name}.");
            foreach (var (mine, other) in relation.Keys)
            {
                Column(from, from.Column(mine, name).Name);
                Column(relation.Other, relation.Other.Column(other, name).Name);
            }

            return relation.Other;
        }

        void Node(RestNode? node, RestTable it, Dictionary<string, RestTable> variables)
        {
            switch (node)
            {
                case RestPath path:
                    {
                        var (current, start) = variables.TryGetValue(path.Segments[0], out var bound) ? (bound, 1) : (it, 0);
                        for (var i = start; i < path.Segments.Count - 1; i++)
                        {
                            current = Follow(current, path.Segments[i]);
                        }

                        if (start < path.Segments.Count)
                        {
                            Column(current, path.Segments[^1]);
                        }

                        break;
                    }

                case RestLambda lambda:
                    {
                        var segments = lambda.Collection.Segments;
                        var (current, start) = variables.TryGetValue(segments[0], out var bound) ? (bound, 1) : (it, 0);
                        for (var i = start; i < segments.Count; i++)
                        {
                            current = Follow(current, segments[i]);
                        }

                        var inner = new Dictionary<string, RestTable>(variables, StringComparer.Ordinal);
                        if (lambda.Variable is { } variable)
                        {
                            inner[variable] = current;
                        }

                        Node(lambda.Predicate, it, inner);
                        break;
                    }

                case RestSearch:
                    // A search reads the searchable columns: their values tell whether a row matches.
                    foreach (var column in it.Searchable?.Columns ?? [])
                    {
                        Column(it, it.Column(column, "$search").Name);
                    }

                    break;
                case RestBinary binary:
                    Node(binary.Left, it, variables);
                    Node(binary.Right, it, variables);
                    break;
                case RestNot not:
                    Node(not.Operand, it, variables);
                    break;
                case RestIn @in:
                    Node(@in.Operand, it, variables);
                    break;
                case RestCall call:
                    call.Arguments.ToList().ForEach(a => Node(a, it, variables));
                    break;
            }
        }

        foreach (var column in query.Select)
        {
            Column(table, table.Column(column, "$select").Name);
        }

        var none = new Dictionary<string, RestTable>(StringComparer.Ordinal);
        Node(query.Filter, table, none);
        query.OrderBy.ToList().ForEach(o => Node(o.Key, table, none));
        foreach (var expand in query.Expand)
        {
            RequireReadable(expand.Query, Follow(table, expand.Relation), relations);
        }
    }

    // The columns a write sets must be ones the caller may write; the policies' presets are set whatever it sends.
    private async Task<Dictionary<string, object?>> WritableAsync(
        RestTable table,
        DmAccessRules rules,
        IReadOnlyDictionary<string, object?> values,
        CancellationToken cancellationToken
    )
    {
        var written = new Dictionary<string, object?>(values, StringComparer.OrdinalIgnoreCase);
        if (rules.Columns is { } allowed && written.Keys.FirstOrDefault(c => !allowed.Contains(c) && !rules.Presets.ContainsKey(c)) is { } refused)
        {
            throw new UnauthorizedAccessException($"The column {refused} of {table.Name} is not one this caller may write.");
        }

        foreach (var (column, preset) in rules.Presets)
        {
            if (preset is { Length: > 1 } path && path[0] == '@')
            {
                // A value the request does not have is not written as NULL: a row with no tenant is everyone's.
                var value = _access is null ? null : await _access.Value(path[1..], cancellationToken).ConfigureAwait(false);
                written[column] = value is null or DBNull
                    ? throw new UnauthorizedAccessException($"The policies set the column {column} of {table.Name} to {path}, which this request does not have.")
                    : value;
            }
            else
            {
                written[column] = preset;
            }
        }

        return written;
    }
}
