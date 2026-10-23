// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using GraphQL;
using GraphQL.Resolvers;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.Dml.Rest;
using MJCZone.DapperMatic.GraphQL.Schema;

namespace MJCZone.DapperMatic.GraphQL.Execution;

/// <summary>
/// The resolvers of a caller's schema: root fields run their whole selection through DapperMatic's engine as one
/// query or write; the fields below read what it returned.
/// </summary>
internal static class GraphQLResolvers
{
    /// <summary>Gets the resolver of a row's global id.</summary>
    public static IFieldResolver NodeId { get; } = new FuncFieldResolver<object?>(c => ((GraphQLRow)c.Source!).NodeId());

    /// <summary>Gets the resolver of <c>node(id:)</c>.</summary>
    public static IFieldResolver Node { get; } = new FuncFieldResolver<object?>(c => GraphQLExecutor.NodeAsync(c));

    /// <summary>Gets the resolver of an edge's cursor.</summary>
    public static IFieldResolver Cursor { get; } = new FuncFieldResolver<object?>(c => ((GraphQLEdge)c.Source!).Cursor);

    /// <summary>Gets the resolver of an edge's node.</summary>
    public static IFieldResolver EdgeNode { get; } = new FuncFieldResolver<object?>(c => ((GraphQLEdge)c.Source!).Node);

    /// <summary>Gets the resolver of a connection's total count.</summary>
    public static IFieldResolver TotalCount { get; } = new FuncFieldResolver<object?>(c => ((GraphQLPage)c.Source!).TotalCount);

    /// <summary>Gets the resolver of a connection's page information.</summary>
    public static IFieldResolver PageInfoOf { get; } = new FuncFieldResolver<object?>(c => c.Source);

    /// <summary>Gets the resolver of a connection's or payload's edges.</summary>
    public static IFieldResolver Edges { get; } = new FuncFieldResolver<object?>(c => ((GraphQLPage)c.Source!).Edges);

    /// <summary>Gets the resolver of a payload's count of rows written.</summary>
    public static IFieldResolver Affected { get; } = new FuncFieldResolver<object?>(c => ((GraphQLPage)c.Source!).Affected);

    /// <summary>Gets the resolver of a payload's rows.</summary>
    public static IFieldResolver Rows { get; } = new FuncFieldResolver<object?>(c => ((GraphQLPage)c.Source!).Edges.Select(e => e.Node).ToList());

    /// <summary>Gets the resolver of a payload's row.</summary>
    public static IFieldResolver Row { get; } = new FuncFieldResolver<object?>(c => ((GraphQLPage)c.Source!).Edges.FirstOrDefault()?.Node);

    /// <summary>Gets the resolver of a subscription's event: the results the stream sent.</summary>
    public static IFieldResolver Event { get; } = new FuncFieldResolver<object?>(c => c.Source);

    /// <summary>Gets the resolver of a column.</summary>
    /// <param name="column">The column.</param>
    /// <returns>The resolver.</returns>
    public static IFieldResolver Column(RestColumn column) => new FuncFieldResolver<object?>(c => ((GraphQLRow)c.Source!).Column(column));

    /// <summary>Gets the resolver of a related row or rows, read under the field's response key.</summary>
    /// <param name="other">The related table.</param>
    /// <param name="many">Whether the relation is to many rows.</param>
    /// <returns>The resolver.</returns>
    public static IFieldResolver Related(GraphQLTable other, bool many) =>
        new FuncFieldResolver<object?>(c => ((GraphQLRow)c.Source!).Related(ExpandKey(c), other, many));

    /// <summary>Gets the resolver of page information.</summary>
    /// <param name="name">The field.</param>
    /// <returns>The resolver.</returns>
    public static IFieldResolver PageInfo(string name) =>
        new FuncFieldResolver<object?>(c =>
        {
            var page = (GraphQLPage)c.Source!;
            return name switch
            {
                "hasNextPage" => page.HasNextPage,
                "hasPreviousPage" => false,
                "startCursor" => page.Edges.FirstOrDefault()?.Cursor,
                _ => page.Edges.LastOrDefault()?.Cursor,
            };
        });

    /// <summary>Gets the resolver of a table's rows.</summary>
    /// <param name="table">The table.</param>
    /// <returns>The resolver.</returns>
    public static IFieldResolver List(GraphQLTable table) => new FuncFieldResolver<object?>(c => GraphQLExecutor.ListAsync(c, table));

    /// <summary>Gets the resolver of a table's rows as a connection.</summary>
    /// <param name="table">The table.</param>
    /// <returns>The resolver.</returns>
    public static IFieldResolver Connection(GraphQLTable table) => new FuncFieldResolver<object?>(c => GraphQLExecutor.ConnectionAsync(c, table));

    /// <summary>Gets the resolver of a row by its key.</summary>
    /// <param name="table">The table.</param>
    /// <returns>The resolver.</returns>
    public static IFieldResolver ByKey(GraphQLTable table) => new FuncFieldResolver<object?>(c => GraphQLExecutor.ByKeyAsync(c, table));

    /// <summary>Gets the resolver of a write.</summary>
    /// <param name="table">The table.</param>
    /// <param name="kind">The write.</param>
    /// <returns>The resolver.</returns>
    public static IFieldResolver Mutation(GraphQLTable table, GraphQLFieldKind kind) => new FuncFieldResolver<object?>(c => GraphQLExecutor.MutateAsync(c, table, kind));

    /// <summary>Gets the resolver of a declared function.</summary>
    /// <param name="function">The function.</param>
    /// <returns>The resolver.</returns>
    public static IFieldResolver Function(DapperMaticFunction function) => new FuncFieldResolver<object?>(c => GraphQLExecutor.FunctionAsync(c, function));

    /// <summary>Gets the stream of a live query of a table's rows.</summary>
    /// <param name="table">The table.</param>
    /// <returns>The stream resolver.</returns>
    public static ISourceStreamResolver Live(GraphQLTable table) => new SourceStreamResolver<object?>(c => GraphQLExecutor.LiveAsync(c, table));

    // The key the query put the field's rows under: chosen when it was built; else its response key.
    private static string ExpandKey(IResolveFieldContext context)
    {
        if (context.UserContext is DapperMaticGraphQLContext request)
        {
            lock (request.ExpandKeys)
            {
                if (request.ExpandKeys.TryGetValue(context.FieldAst, out var key))
                {
                    return key;
                }
            }
        }

        return context.FieldAst.Alias?.Name.StringValue ?? context.FieldDefinition.Name;
    }
}

/// <summary>A row in a page, with its cursor.</summary>
/// <param name="Node">The row.</param>
/// <param name="Cursor">Its cursor.</param>
#pragma warning disable SA1313
internal sealed record GraphQLEdge(GraphQLRow Node, string Cursor);
#pragma warning restore SA1313

/// <summary>A page of rows, or the rows a write wrote: what connections and payloads resolve from.</summary>
internal sealed class GraphQLPage
{
    /// <summary>Gets the rows, with their cursors (empty for rows of tables that are not nodes).</summary>
    public IReadOnlyList<GraphQLEdge> Edges { get; init; } = [];

    /// <summary>Gets a value indicating whether rows follow the page.</summary>
    public bool HasNextPage { get; init; }

    /// <summary>Gets how many rows match, when asked for.</summary>
    public long? TotalCount { get; init; }

    /// <summary>Gets how many rows were written.</summary>
    public int Affected { get; init; }
}
