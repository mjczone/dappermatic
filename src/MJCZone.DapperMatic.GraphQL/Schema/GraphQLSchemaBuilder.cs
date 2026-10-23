// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using GraphQL;
using GraphQL.Resolvers;
using GraphQL.Types;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Services;
using MJCZone.DapperMatic.Dml.Rest;
using MJCZone.DapperMatic.GraphQL.Execution;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.GraphQL.Schema;

/// <summary>
/// Builds a caller's GraphQL schema from their model: for each table and view they may use, the types, conditions,
/// orders, connections, inputs and payloads of what they may do there, and the root fields that do it. Names are the
/// database's, made GraphQL names; a name already given gets <c>_2</c>, <c>_3</c> after it.
/// </summary>
internal sealed class GraphQLSchemaBuilder
{
    // GraphQL's own names and those every schema may have: never a table's.
    private static readonly string[] Shared =
    [
        "Query", "Mutation", "Subscription", "Node", "PageInfo", "order_by", "Int", "Float", "String", "Boolean", "ID", "Long",
        "Decimal", "BigInt", "BigDecimal", "UUID", "DateTime", "LocalDateTime", "Date", "LocalTime", "TimeSpan", "ByteArray", "Any",
    ];

    private readonly DapperMaticCallerModel _model;
    private readonly DapperMaticGraphQLOptions _options;
    private readonly Func<RestTable, bool> _subscribable;
    private readonly DapperMaticScalars _scalars;
    private readonly GraphQLNameScope _types;
    private readonly GraphQLModel _built;
    private readonly Dictionary<string, InputObjectGraphType> _comparisons = new(StringComparer.Ordinal);
    private readonly List<GraphQLTable> _tables = [];
    private EnumerationGraphType? _orderBy;
    private ObjectGraphType? _pageInfo;
    private InterfaceGraphType? _node;

    private GraphQLSchemaBuilder(DapperMaticCallerModel model, DapperMaticGraphQLOptions options, Func<RestTable, bool> subscribable)
    {
        _model = model;
        _options = options;
        _subscribable = subscribable;
        _scalars = new DapperMaticScalars(options.BigNumbersAsStrings);
        _types = new GraphQLNameScope(Shared.Concat(Shared.Select(s => s + "_comparison")));
        _built = new GraphQLModel { Schema = new global::GraphQL.Types.Schema() };
    }

    /// <summary>
    /// Builds a caller's schema.
    /// </summary>
    /// <param name="model">What the caller may use.</param>
    /// <param name="options">The GraphQL options.</param>
    /// <param name="subscribable">Whether callers may subscribe to a table's changes.</param>
    /// <returns>The schema and what its types stand for.</returns>
    public static GraphQLModel Build(DapperMaticCallerModel model, DapperMaticGraphQLOptions options, Func<RestTable, bool> subscribable) =>
        new GraphQLSchemaBuilder(model, options, subscribable).Build();

    private static bool May(GraphQLTable table, DmAction action) => table.Model.May(action);

    private static FieldType Field(string name, IGraphType type, IFieldResolver? resolver = null, QueryArguments? arguments = null, string? description = null) =>
        new() { Name = name, ResolvedType = type, Resolver = resolver, Arguments = arguments, Description = description };

    private static IGraphType NonNull(IGraphType type) => new NonNullGraphType(type);

    private static IGraphType ListOf(IGraphType type) => new NonNullGraphType(new ListGraphType(new NonNullGraphType(type)));

    private static QueryArgument Argument(string name, IGraphType type, string? description = null) => new(type) { Name = name, Description = description };

    private GraphQLModel Build()
    {
        // Every table's type keeps its own name first; the names made from it come after, in the same order.
        foreach (var model in _model.Tables.OrderBy(t => t.Table.Name, StringComparer.Ordinal).ThenBy(t => t.View))
        {
            var readable = model.Columns(DmAction.Read).Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var table = new GraphQLTable
            {
                Model = model,
                TypeName = _types.Take(GraphQLNames.Of(model.Table.Name)),
                IsNode = !model.View && model.Table.Keys.Count > 0 && model.Table.Keys.All(readable.Contains) && model.May(DmAction.Read),
            };
            _tables.Add(table);
            _built.Tables.TryAdd(model.Table.Name, table);
        }

        foreach (var table in _tables.Where(t => May(t, DmAction.Read)))
        {
            table.Type = new ObjectGraphType { Name = table.TypeName, Description = Describe(table) };
            table.Type.Metadata[GraphQLBinding.Table] = table;
            table.Where = new InputObjectGraphType { Name = _types.Take(table.TypeName + "_where"), Description = $"Conditions on rows of {table.Table.Name}." };
            table.OrderBy = new InputObjectGraphType { Name = _types.Take(table.TypeName + "_order_by"), Description = $"One way to order rows of {table.Table.Name}." };
            table.Where.Metadata[GraphQLBinding.Table] = table;
            table.OrderBy.Metadata[GraphQLBinding.Table] = table;
        }

        foreach (var table in _tables.Where(t => t.Type is not null))
        {
            FillType(table);
            FillWhere(table);
            FillOrderBy(table);
        }

        var query = new ObjectGraphType { Name = "Query" };
        var mutation = new ObjectGraphType { Name = "Mutation" };
        var subscription = new ObjectGraphType { Name = "Subscription" };
        var queries = new GraphQLNameScope(["__schema", "__type", "__typename"]);
        var mutations = new GraphQLNameScope(["__typename"]);
        var subscriptions = new GraphQLNameScope(["__typename"]);
        if (_tables.Any(t => t.IsNode))
        {
            query.AddField(NodeField(queries.Take("node")));
        }

        foreach (var table in _tables)
        {
            AddQueries(table, query, queries);
            AddMutations(table, mutation, mutations);
            if (table.Type is not null && _subscribable(table.Table))
            {
                var field = ListField(table, subscriptions.Take(table.TypeName), GraphQLFieldKind.List, paged: false);
                field.Resolver = GraphQLResolvers.Event;
                field.StreamResolver = GraphQLResolvers.Live(table);
                subscription.AddField(field);
            }
        }

        foreach (var function in _model.Functions.OrderBy(f => f.Name, StringComparer.Ordinal))
        {
            var writes = function.Writes.Length > 0;
            var field = Field(
                (writes ? mutations : queries).Take(GraphQLNames.Of(function.Name)),
                ListOf(_scalars.For(typeof(object))),
                GraphQLResolvers.Function(function),
                new QueryArguments(function.Parameters.Where(p => p.Value is null).Select(p => Argument(
                    GraphQLNames.Of(p.Name),
                    p.Nullable || p.Default is not null ? _scalars.For(p.Type) : NonNull(_scalars.For(p.Type))
                ))),
                function.Description
            );
            field.Metadata[GraphQLBinding.Kind] = GraphQLFieldKind.Function;
            field.Metadata[GraphQLBinding.Function] = function;
            (writes ? mutation : query).AddField(field);
        }

        if (query.Fields.Count == 0)
        {
            // A schema has a query type with a field: a caller who may use nothing gets one saying so.
            query.AddField(Field("_empty", _scalars.Named("Boolean"), new FuncFieldResolver<object?>(_ => null), description: "Nothing here: the caller may use no table."));
        }

        var schema = (global::GraphQL.Types.Schema)_built.Schema;
        schema.Query = query;
        schema.Mutation = mutation.Fields.Count > 0 ? mutation : null;
        schema.Subscription = subscription.Fields.Count > 0 ? subscription : null;
        foreach (var table in _tables.Where(t => t.Type is not null))
        {
            schema.RegisterType(table.Type!);
        }

        _built.Renamed.AddRange(_types.Renamed);
        _built.Renamed.AddRange(queries.Renamed);
        _built.Renamed.AddRange(mutations.Renamed);
        _built.Renamed.AddRange(subscriptions.Renamed);
        return _built;
    }

    private string Describe(GraphQLTable table) =>
        table.Model.View ? $"A row of the view {table.Table.Name}." : $"A row of {table.Table.Name}.";

    // A readable table's fields: its global id when its rows are nodes, its readable columns, and its relations to
    // tables the caller may read.
    private void FillType(GraphQLTable table)
    {
        var fields = new GraphQLNameScope(table.IsNode ? ["nodeId", "__typename"] : ["__typename"]);
        if (table.IsNode)
        {
            var nodeId = Field("nodeId", NonNull(_scalars.Named("ID")), GraphQLResolvers.NodeId, description: "The row's global id.");
            nodeId.Metadata[GraphQLBinding.Kind] = GraphQLFieldKind.NodeId;
            table.Type!.AddField(nodeId);
            table.Type.AddResolvedInterface(Node());
            Node().AddPossibleType(table.Type);
        }

        foreach (var column in table.Model.Columns(DmAction.Read))
        {
            var name = fields.Take(GraphQLNames.Of(column.Name));
            table.Fields[column.Name] = name;
            var scalar = _scalars.For(column.Type);
            var field = Field(name, column.Nullable ? scalar : NonNull(scalar), GraphQLResolvers.Column(column));
            field.Metadata[GraphQLBinding.Kind] = GraphQLFieldKind.Column;
            field.Metadata[GraphQLBinding.Column] = column.Name;
            table.Type!.AddField(field);
        }

        foreach (var relation in table.Model.Relations)
        {
            if (Find(relation.Other) is not { Type: { } other } related)
            {
                continue;
            }

            var name = fields.Take(GraphQLNames.Of(relation.Name));
            var field = relation.Many
                ? Field(name, ListOf(other), GraphQLResolvers.Related(related, many: true), Arguments(related, paged: true, after: false))
                : Field(name, other, GraphQLResolvers.Related(related, many: false));
            field.Metadata[GraphQLBinding.Kind] = GraphQLFieldKind.Relation;
            field.Metadata[GraphQLBinding.Relation] = relation;
            table.Type!.AddField(field);
        }
    }

    // _and, _or, _not; a comparison per readable column; a related table's conditions per relation.
    private void FillWhere(GraphQLTable table)
    {
        var where = table.Where!;
        var fields = new GraphQLNameScope(["_and", "_or", "_not"]);
        where.AddField(Field("_and", new ListGraphType(new NonNullGraphType(where)), description: "Every condition holds."));
        where.AddField(Field("_or", new ListGraphType(new NonNullGraphType(where)), description: "Any condition holds."));
        where.AddField(Field("_not", where, description: "The condition does not hold."));
        foreach (var column in table.Model.Columns(DmAction.Read))
        {
            var field = Field(fields.Take(GraphQLNames.Of(column.Name)), Comparison(_scalars.For(column.Type)));
            field.Metadata[GraphQLBinding.Column] = column.Name;
            where.AddField(field);
        }

        foreach (var relation in table.Model.Relations)
        {
            if (Find(relation.Other) is not { Where: { } other } related)
            {
                continue;
            }

            var field = Field(fields.Take(GraphQLNames.Of(relation.Name)), relation.Many ? ManyWhere(related) : other);
            field.Metadata[GraphQLBinding.Relation] = relation;
            where.AddField(field);
        }
    }

    // One readable column (ascending or descending), or a related row's order.
    private void FillOrderBy(GraphQLTable table)
    {
        var fields = new GraphQLNameScope([]);
        foreach (var column in table.Model.Columns(DmAction.Read))
        {
            var field = Field(fields.Take(GraphQLNames.Of(column.Name)), OrderDirection());
            field.Metadata[GraphQLBinding.Column] = column.Name;
            table.OrderBy!.AddField(field);
        }

        foreach (var relation in table.Model.Relations.Where(r => !r.Many))
        {
            if (Find(relation.Other) is not { OrderBy: { } other })
            {
                continue;
            }

            var field = Field(fields.Take(GraphQLNames.Of(relation.Name)), other);
            field.Metadata[GraphQLBinding.Relation] = relation;
            table.OrderBy!.AddField(field);
        }
    }

    private void AddQueries(GraphQLTable table, ObjectGraphType query, GraphQLNameScope names)
    {
        if (table.Type is null)
        {
            return;
        }

        query.AddField(ListField(table, names.Take(table.TypeName), GraphQLFieldKind.List, paged: true));
        if (!table.IsNode)
        {
            return;
        }

        var connection = Field(
            names.Take(table.TypeName + "_connection"),
            NonNull(Connection(table)),
            GraphQLResolvers.Connection(table),
            ConnectionArguments(table),
            $"Rows of {table.Table.Name}, a page at a time (a Relay connection)."
        );
        connection.Metadata[GraphQLBinding.Kind] = GraphQLFieldKind.Connection;
        connection.Metadata[GraphQLBinding.Table] = table;
        query.AddField(connection);

        var byKey = Field(names.Take(table.TypeName + "_by_key"), table.Type, GraphQLResolvers.ByKey(table), KeyArguments(table, required: true), $"A row of {table.Table.Name} by its key.");
        byKey.Metadata[GraphQLBinding.Kind] = GraphQLFieldKind.ByKey;
        byKey.Metadata[GraphQLBinding.Table] = table;
        query.AddField(byKey);
    }

    private void AddMutations(GraphQLTable table, ObjectGraphType mutation, GraphQLNameScope names)
    {
        if (table.Model.View)
        {
            return;
        }

        var name = table.TypeName;
        var insert = May(table, DmAction.Create) ? Insert(table) : null;
        if (insert is not null)
        {
            mutation.AddField(Write(table, names.Take("insert_" + name), GraphQLFieldKind.Insert, RowsPayload(table, edges: true), new QueryArguments(Argument("rows", ListOf(insert))), $"Inserts rows into {table.Table.Name}, all or none."));
            if (May(table, DmAction.Update) && table.Table.Keys.Count > 0)
            {
                mutation.AddField(Write(table, names.Take("save_" + name), GraphQLFieldKind.Save, RowsPayload(table, edges: true), new QueryArguments(Argument("rows", ListOf(insert))), $"Inserts rows into {table.Table.Name}, or updates those whose key is there already."));
            }
        }

        if (May(table, DmAction.Update) && Set(table) is { } set)
        {
            var arguments = Condition(table);
            arguments.Add(Argument("set", NonNull(set)));
            mutation.AddField(Write(table, names.Take("update_" + name), GraphQLFieldKind.Update, RowsPayload(table, edges: false), arguments, $"Updates the rows of {table.Table.Name} that match a condition (where, filter, or both); filter: \"true\" for every row."));
            if (table.IsNode)
            {
                var byKey = KeyArguments(table, required: false);
                byKey.Add(Argument("set", NonNull(set)));
                mutation.AddField(Write(table, names.Take("update_" + name + "_by_key"), GraphQLFieldKind.UpdateByKey, RowPayload(table), byKey, $"Updates a row of {table.Table.Name} by its key or nodeId."));
            }
        }

        if (May(table, DmAction.Delete))
        {
            mutation.AddField(Write(table, names.Take("delete_" + name), GraphQLFieldKind.Delete, RowsPayload(table, edges: false), Condition(table), $"Deletes the rows of {table.Table.Name} that match a condition (where, filter, or both), returning them as they were; filter: \"true\" for every row."));
            if (table.IsNode)
            {
                mutation.AddField(Write(table, names.Take("delete_" + name + "_by_key"), GraphQLFieldKind.DeleteByKey, RowPayload(table), KeyArguments(table, required: false), $"Deletes a row of {table.Table.Name} by its key or nodeId, returning it as it was."));
            }
        }
    }

    private FieldType Write(GraphQLTable table, string name, GraphQLFieldKind kind, IGraphType payload, QueryArguments arguments, string description)
    {
        var field = Field(name, NonNull(payload), GraphQLResolvers.Mutation(table, kind), arguments, description);
        field.Metadata[GraphQLBinding.Kind] = kind;
        field.Metadata[GraphQLBinding.Table] = table;
        return field;
    }

    private FieldType ListField(GraphQLTable table, string name, GraphQLFieldKind kind, bool paged)
    {
        var field = Field(name, ListOf(table.Type!), GraphQLResolvers.List(table), Arguments(table, paged, after: paged && table.IsNode), $"Rows of {table.Table.Name}.");
        field.Metadata[GraphQLBinding.Kind] = kind;
        field.Metadata[GraphQLBinding.Table] = table;
        return field;
    }

    // The arguments of a list: conditions, order, page, search.
    private QueryArguments Arguments(GraphQLTable table, bool paged, bool after)
    {
        var arguments = new QueryArguments(
            Argument("where", table.Where!, "Conditions the rows meet."),
            Argument("filter", _scalars.Named("String"), "A condition as OData text, as the REST API takes it: status eq 'open'. ANDed with where."),
            Argument("orderBy", new ListGraphType(new NonNullGraphType(table.OrderBy!)), "The order, each item naming one column or relation.")
        );
        if (paged)
        {
            arguments.Add(Argument("first", _scalars.Named("Int"), "The most rows to read."));
            arguments.Add(Argument("skip", _scalars.Named("Int"), "The rows to skip."));
        }

        if (after)
        {
            arguments.Add(Argument("after", _scalars.Named("String"), "Reads the rows after a cursor (an edge's, or a connection's endCursor)."));
        }

        if (table.Table.Searchable is not null)
        {
            arguments.Add(Argument("search", _scalars.Named("String"), "Words every row contains, in the table's searchable columns."));
        }

        return arguments;
    }

    private QueryArguments ConnectionArguments(GraphQLTable table)
    {
        var arguments = new QueryArguments(
            Argument("where", table.Where!, "Conditions the rows meet."),
            Argument("filter", _scalars.Named("String"), "A condition as OData text: status eq 'open'. ANDed with where."),
            Argument("orderBy", new ListGraphType(new NonNullGraphType(ConnectionOrderBy(table))), "The order, by the table's own columns; the key orders rows alike."),
            Argument("first", _scalars.Named("Int"), "The most rows in the page."),
            Argument("after", _scalars.Named("String"), "Reads the rows after a cursor.")
        );
        if (table.Table.Searchable is not null)
        {
            arguments.Add(Argument("search", _scalars.Named("String"), "Words every row contains, in the table's searchable columns."));
        }

        return arguments;
    }

    // A condition for an update or delete by condition: where (when the caller may read), filter.
    private QueryArguments Condition(GraphQLTable table)
    {
        var arguments = new QueryArguments(Argument("filter", _scalars.Named("String"), "A condition as OData text: status eq 'open'; true for every row."));
        if (table.Where is not null)
        {
            arguments.Add(Argument("where", table.Where, "Conditions the rows meet."));
        }

        return arguments;
    }

    // A row's key: each key column, and its global id in their place.
    private QueryArguments KeyArguments(GraphQLTable table, bool required)
    {
        var arguments = new QueryArguments();
        foreach (var key in table.Table.Keys)
        {
            var column = table.Table.Column(key, "the key");
            var scalar = _scalars.For(column.Type);
            var argument = Argument(table.Fields[key], required ? NonNull(scalar) : scalar);
            argument.Metadata[GraphQLBinding.Column] = key;
            arguments.Add(argument);
        }

        if (!required)
        {
            arguments.Add(Argument("nodeId", _scalars.Named("ID"), "The row's global id, in place of its key."));
        }

        return arguments;
    }

    private FieldType NodeField(string name)
    {
        var field = Field(name, Node(), GraphQLResolvers.Node, new QueryArguments(Argument("id", NonNull(_scalars.Named("ID")))), "A row by its global id.");
        field.Metadata[GraphQLBinding.Kind] = GraphQLFieldKind.Node;
        return field;
    }

    private InterfaceGraphType Node() =>
        _node ??= CreateNode();

    private InterfaceGraphType CreateNode()
    {
        var node = new InterfaceGraphType { Name = "Node", Description = "A row with a global id." };
        node.AddField(Field("nodeId", NonNull(_scalars.Named("ID")), description: "The row's global id."));
        node.ResolveType = value => value is GraphQLRow row ? row.Table.Type : null;
        return node;
    }

    private EnumerationGraphType OrderDirection()
    {
        if (_orderBy is null)
        {
            _orderBy = new EnumerationGraphType { Name = "order_by", Description = "Ascending or descending." };
            _orderBy.Add("ASC", "ASC", "Ascending.");
            _orderBy.Add("DESC", "DESC", "Descending.");
        }

        return _orderBy;
    }

    private ObjectGraphType PageInfo()
    {
        if (_pageInfo is null)
        {
            _pageInfo = new ObjectGraphType { Name = "PageInfo", Description = "Where a page is among the rows (Relay)." };
            _pageInfo.AddField(Field("hasNextPage", NonNull(_scalars.Named("Boolean")), GraphQLResolvers.PageInfo("hasNextPage")));
            _pageInfo.AddField(Field("hasPreviousPage", NonNull(_scalars.Named("Boolean")), GraphQLResolvers.PageInfo("hasPreviousPage")));
            _pageInfo.AddField(Field("startCursor", _scalars.Named("String"), GraphQLResolvers.PageInfo("startCursor")));
            _pageInfo.AddField(Field("endCursor", _scalars.Named("String"), GraphQLResolvers.PageInfo("endCursor")));
        }

        return _pageInfo;
    }

    private ObjectGraphType Edge(GraphQLTable table)
    {
        if (table.Edge is null)
        {
            table.Edge = new ObjectGraphType { Name = _types.Take(table.TypeName + "_edge"), Description = $"A row of {table.Table.Name} in a page, with its cursor." };
            var cursor = Field("cursor", NonNull(_scalars.Named("String")), GraphQLResolvers.Cursor);
            cursor.Metadata[GraphQLBinding.Kind] = GraphQLFieldKind.Cursor;
            var node = Field("node", NonNull(table.Type!), GraphQLResolvers.EdgeNode);
            node.Metadata[GraphQLBinding.Kind] = GraphQLFieldKind.EdgeNode;
            table.Edge.AddField(cursor);
            table.Edge.AddField(node);
        }

        return table.Edge;
    }

    private ObjectGraphType Connection(GraphQLTable table)
    {
        var connection = new ObjectGraphType { Name = _types.Take(table.TypeName + "_connection"), Description = $"A page of rows of {table.Table.Name} (a Relay connection)." };
        var total = Field("totalCount", NonNull(_scalars.Named("Int")), GraphQLResolvers.TotalCount, description: "How many rows match, on every page.");
        total.Metadata[GraphQLBinding.Kind] = GraphQLFieldKind.TotalCount;
        var page = Field("pageInfo", NonNull(PageInfo()), GraphQLResolvers.PageInfoOf);
        page.Metadata[GraphQLBinding.Kind] = GraphQLFieldKind.PageInfo;
        var edges = Field("edges", ListOf(Edge(table)), GraphQLResolvers.Edges);
        edges.Metadata[GraphQLBinding.Kind] = GraphQLFieldKind.Edges;
        connection.AddField(total);
        connection.AddField(page);
        connection.AddField(edges);
        return connection;
    }

    // A connection's order: the table's own readable columns only, which cursors are made of.
    private InputObjectGraphType ConnectionOrderBy(GraphQLTable table)
    {
        var order = new InputObjectGraphType { Name = _types.Take(table.TypeName + "_connection_order_by"), Description = $"One column to order a page of {table.Table.Name} by." };
        order.Metadata[GraphQLBinding.Table] = table;
        foreach (var field in table.OrderBy!.Fields.Where(f => f.GetMetadata<string>(GraphQLBinding.Column) is not null))
        {
            var copy = Field(field.Name, OrderDirection());
            copy.Metadata[GraphQLBinding.Column] = field.GetMetadata<string>(GraphQLBinding.Column);
            order.AddField(copy);
        }

        return order;
    }

    private InputObjectGraphType ManyWhere(GraphQLTable related)
    {
        var name = _types.Take(related.TypeName + "_many_where");
        var many = new InputObjectGraphType { Name = name, Description = $"Conditions on related rows of {related.Table.Name}." };
        many.AddField(Field("any", related.Where!, description: "Some related row meets the conditions."));
        many.AddField(Field("all", related.Where!, description: "Every related row meets the conditions."));
        many.AddField(Field("none", related.Where!, description: "No related row meets the conditions."));
        return many;
    }

    private InputObjectGraphType Comparison(ScalarGraphType scalar)
    {
        if (_comparisons.TryGetValue(scalar.Name, out var comparison))
        {
            return comparison;
        }

        comparison = new InputObjectGraphType { Name = scalar.Name + "_comparison", Description = $"Comparisons of {scalar.Name} values." };
        comparison.AddField(Field("isNull", _scalars.Named("Boolean"), description: "Whether the value is null."));
        if (scalar.Name != "Any")
        {
            foreach (var op in new[] { "eq", "ne", "gt", "gte", "lt", "lte" })
            {
                comparison.AddField(Field(op, scalar));
            }

            comparison.AddField(Field("in", new ListGraphType(new NonNullGraphType(scalar)), description: "The value is one of these."));
            comparison.AddField(Field("nin", new ListGraphType(new NonNullGraphType(scalar)), description: "The value is none of these."));
        }

        if (scalar.Name == "String")
        {
            comparison.AddField(Field("contains", scalar, description: "The text holds this, as the database compares text."));
            comparison.AddField(Field("startsWith", scalar, description: "The text starts with this."));
            comparison.AddField(Field("endsWith", scalar, description: "The text ends with this."));
        }

        _comparisons[scalar.Name] = comparison;
        return comparison;
    }

    // The columns a row may be inserted with (presets left out), each required when the database fills none; related
    // rows the caller may insert, under their relations.
    private InputObjectGraphType? Insert(GraphQLTable table, HashSet<GraphQLTable>? seen = null)
    {
        if (table.Type?.GetMetadata<InputObjectGraphType>("dm:insert") is { } made)
        {
            return made;
        }

        var columns = table.Model.Columns(DmAction.Create).ToList();
        if (columns.Count == 0)
        {
            return null;
        }

        var insert = new InputObjectGraphType { Name = _types.Take(table.TypeName + "_insert"), Description = $"A row to insert into {table.Table.Name}." };
        insert.Metadata[GraphQLBinding.Table] = table;
        table.Type?.Metadata.TryAdd("dm:insert", insert);
        var fields = new GraphQLNameScope([]);
        foreach (var column in columns)
        {
            var scalar = _scalars.For(column.Type);
            var required = !column.Nullable && !column.Filled && !string.Equals(column.Name, table.Table.Generated, StringComparison.OrdinalIgnoreCase);
            var field = Field(fields.Take(table.Fields.GetValueOrDefault(column.Name) ?? GraphQLNames.Of(column.Name)), required ? NonNull(scalar) : scalar);
            field.Metadata[GraphQLBinding.Column] = column.Name;
            insert.AddField(field);
        }

        seen ??= [];
        seen.Add(table);
        foreach (var relation in table.Model.Relations)
        {
            if (Find(relation.Other) is not { } related || !May(related, DmAction.Create) || seen.Contains(related))
            {
                continue;
            }

            if (Insert(related, seen) is { } other)
            {
                var field = Field(fields.Take(GraphQLNames.Of(relation.Name)), relation.Many ? new ListGraphType(new NonNullGraphType(other)) : other);
                field.Metadata[GraphQLBinding.Relation] = relation;
                insert.AddField(field);
            }
        }

        seen.Remove(table);
        return insert;
    }

    // The columns an update may set, each optional.
    private InputObjectGraphType? Set(GraphQLTable table)
    {
        var columns = table.Model.Columns(DmAction.Update).ToList();
        if (columns.Count == 0)
        {
            return null;
        }

        var set = new InputObjectGraphType { Name = _types.Take(table.TypeName + "_set"), Description = $"The columns of {table.Table.Name} to set." };
        set.Metadata[GraphQLBinding.Table] = table;
        var fields = new GraphQLNameScope([]);
        foreach (var column in columns)
        {
            var field = Field(fields.Take(table.Fields.GetValueOrDefault(column.Name) ?? GraphQLNames.Of(column.Name)), _scalars.For(column.Type));
            field.Metadata[GraphQLBinding.Column] = column.Name;
            set.AddField(field);
        }

        return set;
    }

    // What a write of rows returns: how many, the rows the caller may read (as they are after an insert or update, as
    // they were before a delete), and for inserts into tables of nodes their edges, which Relay adds to connections.
    private ObjectGraphType RowsPayload(GraphQLTable table, bool edges)
    {
        var name = table.TypeName + (edges ? "_insert_payload" : "_rows_payload");
        var payload = new ObjectGraphType { Name = _types.Take(name), Description = $"Rows of {table.Table.Name} written." };
        var affected = Field("affected", NonNull(_scalars.Named("Int")), GraphQLResolvers.Affected, description: "How many rows were written.");
        affected.Metadata[GraphQLBinding.Kind] = GraphQLFieldKind.Affected;
        payload.AddField(affected);
        if (table.Type is not null)
        {
            var rows = Field("rows", ListOf(table.Type), GraphQLResolvers.Rows, description: "The rows, those the caller may read.");
            rows.Metadata[GraphQLBinding.Kind] = GraphQLFieldKind.Rows;
            payload.AddField(rows);
            if (edges && table.IsNode)
            {
                var edge = Field("edges", ListOf(Edge(table)), GraphQLResolvers.Edges, description: "The rows as edges, to add to a connection.");
                edge.Metadata[GraphQLBinding.Kind] = GraphQLFieldKind.Edges;
                payload.AddField(edge);
            }
        }

        return payload;
    }

    private ObjectGraphType RowPayload(GraphQLTable table)
    {
        var payload = new ObjectGraphType { Name = _types.Take(table.TypeName + "_row_payload"), Description = $"A row of {table.Table.Name} written." };
        var affected = Field("affected", NonNull(_scalars.Named("Int")), GraphQLResolvers.Affected, description: "How many rows were written: 0 or 1.");
        affected.Metadata[GraphQLBinding.Kind] = GraphQLFieldKind.Affected;
        payload.AddField(affected);
        if (table.Type is not null)
        {
            var row = Field("row", table.Type, GraphQLResolvers.Row, description: "The row, if the caller may read it.");
            row.Metadata[GraphQLBinding.Kind] = GraphQLFieldKind.Row;
            payload.AddField(row);
        }

        return payload;
    }

    private GraphQLTable? Find(DapperMaticCallerTable model) => _tables.Find(t => ReferenceEquals(t.Model, model));
}
