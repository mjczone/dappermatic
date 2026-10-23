// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Numerics;
using GraphQL;
using GraphQL.Execution;
using GraphQL.Types;
using GraphQLParser.AST;
using MJCZone.DapperMatic.AspNetCore.Services;
using MJCZone.DapperMatic.Dml.Rest;
using MJCZone.DapperMatic.GraphQL.Schema;
using GraphQLExecutionContext = GraphQL.Execution.ExecutionContext;

namespace MJCZone.DapperMatic.GraphQL.Execution;

/// <summary>
/// Turns a root field into one query of DapperMatic's engine: its arguments into the condition, order and page; its
/// whole selection (aliases, fragments, <c>@skip</c>/<c>@include</c>) into the columns read and the relations expanded,
/// each with its own arguments. Inputs of writes go back to the database's names.
/// </summary>
internal static class GraphQLQueries
{
    private static readonly FieldCollector Collector = new();

    /// <summary>
    /// Makes the query of a table's rows: the field's arguments and selection.
    /// </summary>
    /// <param name="context">The field's context.</param>
    /// <param name="model">The schema.</param>
    /// <param name="table">The table.</param>
    /// <returns>The query.</returns>
    public static RestQuery Rows(IResolveFieldContext context, GraphQLModel model, GraphQLTable table) =>
        Rows(Execution(context), model, table, context.Arguments, context.FieldAst.SelectionSet);

    /// <summary>
    /// Makes the query of the row a global id names: the selection as it applies to the row's type.
    /// </summary>
    /// <param name="context">The <c>node</c> field's context.</param>
    /// <param name="model">The schema.</param>
    /// <param name="table">The row's table.</param>
    /// <returns>The query.</returns>
    public static RestQuery Node(IResolveFieldContext context, GraphQLModel model, GraphQLTable table) =>
        Rows(Execution(context), model, table, null, context.FieldAst.SelectionSet);

    /// <summary>
    /// Makes the query of a page of a connection: the field's arguments, and the selection of its edges' nodes.
    /// </summary>
    /// <param name="context">The field's context.</param>
    /// <param name="model">The schema.</param>
    /// <param name="table">The table.</param>
    /// <returns>The query, which reads the rows' cursors.</returns>
    public static RestQuery Connection(IResolveFieldContext context, GraphQLModel model, GraphQLTable table)
    {
        var execution = Execution(context);
        var query = Arguments(context.Arguments, table);
        var count = false;
        var nodes = new List<(GraphQLField Field, FieldType Definition)>();
        foreach (var (_, (field, definition)) in Collect(execution, context.FieldDefinition.ResolvedType!, context.FieldAst.SelectionSet))
        {
            switch (KindOf(definition))
            {
                case GraphQLFieldKind.TotalCount:
                    count = true;
                    break;
                case GraphQLFieldKind.Edges:
                    nodes.AddRange(Nodes(execution, definition, field));
                    break;
            }
        }

        var (select, expand) = Selection(execution, model, table, nodes);
        return query with { Select = select, Expand = expand, Count = count, Cursors = true, Probe = true };
    }

    /// <summary>
    /// Makes the query rows written are read back with: the selection of a payload's rows, row and edges.
    /// </summary>
    /// <param name="context">The mutation field's context.</param>
    /// <param name="model">The schema.</param>
    /// <param name="table">The table.</param>
    /// <returns>The query, and whether the payload reads rows at all, and their cursors.</returns>
    public static (RestQuery Query, bool Read, bool Cursors) Payload(IResolveFieldContext context, GraphQLModel model, GraphQLTable table)
    {
        var execution = Execution(context);
        var rows = new List<(GraphQLField Field, FieldType Definition)>();
        var cursors = false;
        foreach (var (_, (field, definition)) in Collect(execution, context.FieldDefinition.ResolvedType!, context.FieldAst.SelectionSet))
        {
            switch (KindOf(definition))
            {
                case GraphQLFieldKind.Rows or GraphQLFieldKind.Row:
                    rows.Add((field, definition));
                    break;
                case GraphQLFieldKind.Edges:
                    cursors = true;
                    rows.AddRange(Nodes(execution, definition, field));
                    break;
            }
        }

        var (select, expand) = Selection(execution, model, table, rows);
        return (new RestQuery { Select = select, Expand = expand, Cursors = cursors }, rows.Count > 0, cursors);
    }

    /// <summary>
    /// Makes the condition a field's <c>where</c> and <c>filter</c> say, ANDed; null when they say nothing.
    /// </summary>
    /// <param name="arguments">The field's arguments.</param>
    /// <param name="table">The table.</param>
    /// <returns>The condition.</returns>
    public static RestNode? Condition(IDictionary<string, ArgumentValue>? arguments, GraphQLTable table)
    {
        var where = Value(arguments, "where") is { } input ? Where(input, table, [], new Counter()) : null;
        var filter = Value(arguments, "filter") is string text ? RestParser.ParseFilter(text) : null;
        return And(where, filter);
    }

    /// <summary>
    /// Makes the condition that matches a row by its key: the key's arguments, or its global id.
    /// </summary>
    /// <param name="arguments">The field's arguments.</param>
    /// <param name="table">The table.</param>
    /// <returns>The condition.</returns>
    /// <exception cref="ArgumentException">Neither the whole key nor a global id of the table is given, or both are.</exception>
    public static RestNode ByKey(IDictionary<string, ArgumentValue>? arguments, GraphQLTable table)
    {
        var keys = table.Table.Keys;
        var given = keys.Select(k => Value(arguments, table.Fields[k])).ToList();
        if (Value(arguments, "nodeId") is string id)
        {
            if (given.Any(v => v is not null))
            {
                throw new ArgumentException("Give a row's key or its nodeId, not both.");
            }

            if (GraphQLNodeIds.Decode(id) is not { } decoded
                || !string.Equals(decoded.Table, table.Table.Name, StringComparison.Ordinal)
                || decoded.Keys.Length != keys.Count)
            {
                throw new ArgumentException($"'{id}' is not the nodeId of a row of {table.Table.Name}.");
            }

            given = [.. decoded.Keys.Select(RestJson.Scalar)];
        }

        if (given.Any(v => v is null))
        {
            throw new ArgumentException($"A row of {table.Table.Name} is named by its key ({string.Join(", ", keys.Select(k => table.Fields[k]))}) or its nodeId.");
        }

        return keys.Select((k, i) => (RestNode)new RestBinary("eq", new RestPath([k]), new RestLiteral(Literal(given[i])))).Aggregate((a, b) => new RestBinary("and", a, b));
    }

    /// <summary>
    /// Gives an input object's values their database names: columns, and related rows under their relations' names.
    /// </summary>
    /// <param name="value">The input object's values, by GraphQL name.</param>
    /// <param name="type">The input object's type.</param>
    /// <returns>The values, by database name.</returns>
    public static Dictionary<string, object?> Input(object? value, IGraphType type)
    {
        var input = (InputObjectGraphType)Unwrap(type);
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, item) in (IDictionary<string, object?>)value!)
        {
            var field = input.GetField(name) ?? throw new ArgumentException($"{input.Name} has no field {name}.");
            if (field.GetMetadata<DapperMaticCallerRelation>(GraphQLBinding.Relation) is { } relation)
            {
                // Related rows as the engine takes them: a row, or a list of rows.
                values[relation.Name] = item is IEnumerable<object?> rows && item is not IDictionary<string, object?>
                    ? rows.Select(r => (IReadOnlyDictionary<string, object?>)Input(r, field.ResolvedType!)).ToList()
                    : item is null ? null : Input(item, field.ResolvedType!);
            }
            else
            {
                values[field.GetMetadata<string>(GraphQLBinding.Column)!] = Literal(item);
            }
        }

        return values;
    }

    /// <summary>Gets an argument's value, or null.</summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument.</param>
    /// <returns>The value.</returns>
    public static object? Value(IDictionary<string, ArgumentValue>? arguments, string name) =>
        arguments is not null && arguments.TryGetValue(name, out var value) ? value.Value : null;

    /// <summary>Gets the type under a list or non-null type.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The named type.</returns>
    public static IGraphType Unwrap(IGraphType type) =>
        type switch
        {
            NonNullGraphType nonNull => Unwrap(nonNull.ResolvedType!),
            ListGraphType list => Unwrap(list.ResolvedType!),
            _ => type,
        };

    // A value as the engine takes it: what a scalar parsed, with integers too big for .NET's own types as text.
    private static object? Literal(object? value) => value is BigInteger big ? big.ToString(CultureInfo.InvariantCulture) : value;

    private static GraphQLExecutionContext Execution(IResolveFieldContext context) => (GraphQLExecutionContext)context.ExecutionContext;

    private static GraphQLFieldKind? KindOf(FieldType definition) =>
        definition.Metadata.TryGetValue(GraphQLBinding.Kind, out var kind) && kind is GraphQLFieldKind known ? known : null;

    private static Dictionary<string, (GraphQLField Field, FieldType Definition)> Collect(GraphQLExecutionContext execution, IGraphType type, GraphQLSelectionSet? selection) =>
        selection is null
            ? []
            : Collector.Collect(execution, type, selection).ToDictionary(p => p.Key, p => (p.Value.field, p.Value.fieldType));

    // The nodes an edges field selects.
    private static IEnumerable<(GraphQLField Field, FieldType Definition)> Nodes(GraphQLExecutionContext execution, FieldType edges, GraphQLField field) =>
        Collect(execution, edges.ResolvedType!, field.SelectionSet).Values.Where(f => KindOf(f.Definition) == GraphQLFieldKind.EdgeNode);

    // The query of a table's rows under a field: its arguments and selection.
    private static RestQuery Rows(GraphQLExecutionContext execution, GraphQLModel model, GraphQLTable table, IDictionary<string, ArgumentValue>? arguments, GraphQLSelectionSet? selection)
    {
        var query = Arguments(arguments, table);
        var fields = selection is null ? [] : Collect(execution, table.Type!, selection).Select(p => (p.Key, p.Value.Field, p.Value.Definition)).ToList();
        var (select, expand) = Columns(execution, model, table, fields);
        return query with { Select = select, Expand = expand };
    }

    // The columns and relations several selections of one table's rows read, merged.
    private static (List<string> Select, List<RestExpand> Expand) Selection(
        GraphQLExecutionContext execution,
        GraphQLModel model,
        GraphQLTable table,
        IEnumerable<(GraphQLField Field, FieldType Definition)> selections
    )
    {
        var fields = new List<(string Key, GraphQLField Field, FieldType Definition)>();
        foreach (var (field, definition) in selections)
        {
            fields.AddRange(Collect(execution, Unwrap(definition.ResolvedType!), field.SelectionSet).Select(p => (p.Key, p.Value.Field, p.Value.Definition)));
        }

        return Columns(execution, model, table, fields);
    }

    // The columns read and the relations expanded, each under the field's response key.
    private static (List<string> Select, List<RestExpand> Expand) Columns(
        GraphQLExecutionContext execution,
        GraphQLModel model,
        GraphQLTable table,
        IEnumerable<(string Key, GraphQLField Field, FieldType Definition)> fields
    )
    {
        var select = new List<string>();
        var expand = new List<RestExpand>();
        foreach (var (key, field, definition) in fields)
        {
            switch (KindOf(definition))
            {
                case GraphQLFieldKind.Column:
                    select.Add(definition.GetMetadata<string>(GraphQLBinding.Column)!);
                    break;
                case GraphQLFieldKind.NodeId:
                    select.AddRange(table.Table.Keys);
                    break;
                case GraphQLFieldKind.Relation:
                    // Each relation field's rows go under a key of its own, which no column has and no other field
                    // took: two nodes of a connection may each give a field of one name other arguments, and an alias
                    // may be a column's name. The field's resolver finds its rows by it.
                    var expandKey = ExpandKeyOf(execution, table, field, key);
                    if (expand.Exists(e => e.As == expandKey))
                    {
                        break;
                    }

                    var relation = definition.GetMetadata<DapperMaticCallerRelation>(GraphQLBinding.Relation)!;
                    var other = model.Tables[relation.Other.Table.Name];
                    expand.Add(new RestExpand(relation.Name, Rows(execution, model, other, execution.GetArguments(definition, field), field.SelectionSet), expandKey));
                    break;
            }
        }

        // Rows selected for their relations or type only still read their key, rather than every column; a table
        // whose key the caller may not read (not a node) reads the columns they may.
        if (select.Count == 0 && table.IsNode)
        {
            select.AddRange(table.Table.Keys);
        }

        return ([.. select.Distinct(StringComparer.OrdinalIgnoreCase)], expand);
    }

    // The key a relation field's rows are put under: its response key, unless a column of its table or another field
    // has that name.
    private static string ExpandKeyOf(GraphQLExecutionContext execution, GraphQLTable table, GraphQLField field, string responseKey)
    {
        var keys = ((DapperMaticGraphQLContext)execution.UserContext).ExpandKeys;
        lock (keys)
        {
            if (keys.TryGetValue(field, out var known))
            {
                return known;
            }

            var taken = keys.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var chosen = responseKey;
            for (var n = 2; taken.Contains(chosen) || table.Table.Columns.Any(c => string.Equals(c.Name, chosen, StringComparison.OrdinalIgnoreCase)); n++)
            {
                chosen = $"{responseKey}#{n}";
            }

            keys[field] = chosen;
            return chosen;
        }
    }

    // A list's arguments: its condition, order, page and search.
    private static RestQuery Arguments(IDictionary<string, ArgumentValue>? arguments, GraphQLTable table) =>
        new()
        {
            Filter = Condition(arguments, table),
            OrderBy = Value(arguments, "orderBy") is IEnumerable<object?> orders ? [.. orders.Select(o => Order(o, table, []))] : [],
            Top = Count(arguments, "first"),
            Skip = Count(arguments, "skip"),
            After = Value(arguments, "after") as string,
            Search = Value(arguments, "search") as string,
        };

    // A count of rows an argument gives: none or more, never fewer (a negative LIMIT is no limit to SQLite).
    private static int? Count(IDictionary<string, ArgumentValue>? arguments, string name) =>
        Value(arguments, name) is int count
            ? count >= 0 ? count : throw new ArgumentException($"{name} is a count of rows: 0 or more.")
            : null;

    // One item of an order: one column, or a related row's order.
    private static RestOrder Order(object? item, GraphQLTable table, IReadOnlyList<string> path)
    {
        if (item is not IDictionary<string, object?> { Count: 1 } one)
        {
            throw new ArgumentException($"Each item of orderBy names one column or relation of {table.Table.Name}.");
        }

        var (name, value) = one.First();
        var field = table.OrderBy!.GetField(name) ?? throw new ArgumentException($"{table.OrderBy.Name} has no field {name}.");
        if (field.GetMetadata<DapperMaticCallerRelation>(GraphQLBinding.Relation) is { } relation)
        {
            return Order(value, table, [.. path, relation.Name]);
        }

        return new RestOrder(new RestPath([.. path, field.GetMetadata<string>(GraphQLBinding.Column)!]), string.Equals(value as string, "DESC", StringComparison.Ordinal));
    }

    // A where input: _and, _or, _not, a comparison per column, a related row's or rows' conditions.
    private static RestNode? Where(object? value, GraphQLTable table, IReadOnlyList<string> path, Counter variables)
    {
        if (value is not IDictionary<string, object?> input)
        {
            return null;
        }

        RestNode? condition = null;
        foreach (var (name, item) in input)
        {
            if (item is null)
            {
                continue;
            }

            RestNode? part = name switch
            {
                "_and" => ((IEnumerable<object?>)item).Select(i => Where(i, table, path, variables)).Aggregate((RestNode?)null, And),
                "_or" => ((IEnumerable<object?>)item).Select(i => Where(i, table, path, variables)).OfType<RestNode>().Aggregate((RestNode?)null, (a, b) => a is null ? b : new RestBinary("or", a, b)),
                "_not" => Where(item, table, path, variables) is { } inner ? new RestNot(inner) : null,
                _ => Field(name, item, table, path, variables),
            };
            condition = And(condition, part);
        }

        return condition;
    }

    private static RestNode? Field(string name, object item, GraphQLTable table, IReadOnlyList<string> path, Counter variables)
    {
        var field = table.Where!.GetField(name) ?? throw new ArgumentException($"{table.Where.Name} has no field {name}.");
        if (field.GetMetadata<string>(GraphQLBinding.Column) is { } column)
        {
            return Comparisons(new RestPath([.. path, column]), (IDictionary<string, object?>)item);
        }

        var relation = field.GetMetadata<DapperMaticCallerRelation>(GraphQLBinding.Relation)!;
        var other = (GraphQLTable)Unwrap(relation.Many ? ((InputObjectGraphType)field.ResolvedType!).GetField("any")!.ResolvedType! : field.ResolvedType!).GetMetadata<GraphQLTable>(GraphQLBinding.Table)!;
        if (!relation.Many)
        {
            return Where(item, other, [.. path, relation.Name], variables);
        }

        // Related rows: some, every, or none meets the conditions.
        RestNode? condition = null;
        foreach (var (kind, inner) in (IDictionary<string, object?>)item)
        {
            if (inner is null)
            {
                continue;
            }

            var variable = $"dm{variables.Next()}";
            var predicate = Where(inner, other, [variable], variables) ?? new RestLiteral(true);
            var lambda = new RestLambda(kind == "all" ? "all" : "any", new RestPath([.. path, relation.Name]), variable, predicate);
            condition = And(condition, kind == "none" ? new RestNot(lambda) : lambda);
        }

        return condition;
    }

    // A column's comparisons, ANDed.
    private static RestNode? Comparisons(RestPath column, IDictionary<string, object?> comparisons)
    {
        RestNode? condition = null;
        foreach (var (op, value) in comparisons)
        {
            RestNode? part = op switch
            {
                "isNull" when value is bool isNull => new RestBinary(isNull ? "eq" : "ne", column, new RestLiteral(null)),
                "eq" or "ne" or "gt" or "lt" when value is not null => new RestBinary(op, column, new RestLiteral(Literal(value))),
                "gte" when value is not null => new RestBinary("ge", column, new RestLiteral(Literal(value))),
                "lte" when value is not null => new RestBinary("le", column, new RestLiteral(Literal(value))),
                "in" when value is IEnumerable<object?> values => new RestIn(column, [.. values.Select(v => new RestLiteral(Literal(v)))]),
                "nin" when value is IEnumerable<object?> values => new RestNot(new RestIn(column, [.. values.Select(v => new RestLiteral(Literal(v)))])),
                "contains" or "startsWith" or "endsWith" when value is not null => new RestCall(op.ToLowerInvariant(), [column, new RestLiteral(value)]),
                _ => null,
            };
            condition = And(condition, part);
        }

        return condition;
    }

    private static RestNode? And(RestNode? left, RestNode? right) =>
        left is null ? right : right is null ? left : new RestBinary("and", left, right);

    // Gives lambdas' variables names of their own within one condition.
    private sealed class Counter
    {
        private int _next;

        public int Next() => _next++;
    }

    // Collects a selection's fields as GraphQL executes them: aliases, fragments, @skip and @include.
    private sealed class FieldCollector : SerialExecutionStrategy
    {
        public Dictionary<string, (GraphQLField field, FieldType fieldType)> Collect(GraphQLExecutionContext context, IGraphType type, GraphQLSelectionSet selection) =>
            CollectFieldsFrom(context, type, selection, null);
    }
}
