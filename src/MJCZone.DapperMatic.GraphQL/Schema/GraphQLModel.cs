// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

#pragma warning disable SA1402 // A schema and what its types stand for belong together.
#pragma warning disable SA1649

using GraphQL.Types;
using MJCZone.DapperMatic.AspNetCore.Services;
using MJCZone.DapperMatic.Dml.Rest;

namespace MJCZone.DapperMatic.GraphQL.Schema;

/// <summary>
/// A caller's GraphQL schema, and what its types stand for: each table's types and the database names behind their
/// fields. Built once for every caller model of the same shape.
/// </summary>
internal sealed class GraphQLModel
{
    /// <summary>Gets the schema.</summary>
    public required ISchema Schema { get; init; }

    /// <summary>Gets the tables of the schema, by their database name.</summary>
    public Dictionary<string, GraphQLTable> Tables { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the names given other than the database's (to avoid a clash), as (asked for, given).</summary>
    public List<(string Wanted, string Given)> Renamed { get; } = [];
}

/// <summary>
/// One table or view of a schema: its object type and the names of its fields.
/// </summary>
internal sealed class GraphQLTable
{
    /// <summary>Gets the table as the caller may use it.</summary>
    public required DapperMaticCallerTable Model { get; init; }

    /// <summary>Gets the table's description.</summary>
    public RestTable Table => Model.Table;

    /// <summary>Gets the name of the table's object type.</summary>
    public required string TypeName { get; init; }

    /// <summary>
    /// Gets a value indicating whether the table's rows are nodes: a table (not a view) with a key, every key column
    /// readable. Only nodes have <c>nodeId</c>, <c>t_by_key</c>, connections and by-key mutations.
    /// </summary>
    public bool IsNode { get; init; }

    /// <summary>Gets or sets the table's object type, when the caller may read it.</summary>
    public ObjectGraphType? Type { get; set; }

    /// <summary>Gets or sets the table's condition input type, when the caller may read it.</summary>
    public InputObjectGraphType? Where { get; set; }

    /// <summary>Gets or sets the table's order input type, when the caller may read it.</summary>
    public InputObjectGraphType? OrderBy { get; set; }

    /// <summary>Gets or sets the table's edge type, when its rows are nodes.</summary>
    public ObjectGraphType? Edge { get; set; }

    /// <summary>Gets the GraphQL names of the readable columns, by column.</summary>
    public Dictionary<string, string> Fields { get; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Names of what types and fields stand for, kept in their metadata.
/// </summary>
internal static class GraphQLBinding
{
    /// <summary>On an object or input type: the <see cref="GraphQLTable"/> it stands for.</summary>
    public const string Table = "dm:table";

    /// <summary>On a field: the database column it stands for.</summary>
    public const string Column = "dm:column";

    /// <summary>On a field: the <see cref="DapperMaticCallerRelation"/> it follows.</summary>
    public const string Relation = "dm:relation";

    /// <summary>On a field: what it is, a <see cref="GraphQLFieldKind"/>.</summary>
    public const string Kind = "dm:kind";

    /// <summary>On a root field: the declared function it runs.</summary>
    public const string Function = "dm:function";
}

/// <summary>
/// What a field of a schema is, when it is more than a column.
/// </summary>
internal enum GraphQLFieldKind
{
    /// <summary>A column.</summary>
    Column,

    /// <summary>A related row or rows.</summary>
    Relation,

    /// <summary>A row's global id.</summary>
    NodeId,

    /// <summary>A table's rows (<c>Query.t</c>, <c>Subscription.t</c>).</summary>
    List,

    /// <summary>A table's rows as a Relay connection.</summary>
    Connection,

    /// <summary>A row by its key.</summary>
    ByKey,

    /// <summary>A row by its global id.</summary>
    Node,

    /// <summary>Rows inserted.</summary>
    Insert,

    /// <summary>Rows inserted, or updated by their key.</summary>
    Save,

    /// <summary>Rows updated by a condition.</summary>
    Update,

    /// <summary>Rows deleted by a condition.</summary>
    Delete,

    /// <summary>A row updated by its key.</summary>
    UpdateByKey,

    /// <summary>A row deleted by its key.</summary>
    DeleteByKey,

    /// <summary>A declared function.</summary>
    Function,

    /// <summary>A connection's total count.</summary>
    TotalCount,

    /// <summary>A connection's page information.</summary>
    PageInfo,

    /// <summary>A connection's edges.</summary>
    Edges,

    /// <summary>An edge's cursor.</summary>
    Cursor,

    /// <summary>An edge's node.</summary>
    EdgeNode,

    /// <summary>A payload's count of rows written.</summary>
    Affected,

    /// <summary>A payload's rows.</summary>
    Rows,

    /// <summary>A payload's row.</summary>
    Row,
}
