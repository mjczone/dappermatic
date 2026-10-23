// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

#pragma warning disable SA1313 // Positional record parameters are properties, so they are named as properties.
#pragma warning disable SA1402 // A REST source's parts belong together.
#pragma warning disable SA1649

using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Dml.Rest;

/// <summary>
/// A table or view a REST request reads or writes, as the database describes it: no model, only its columns.
/// </summary>
internal sealed class RestTable
{
    /// <summary>Gets the schema, or null for the default.</summary>
    public required string? SchemaName { get; init; }

    /// <summary>Gets the table's or view's name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the name quoted and schema-qualified.</summary>
    public required string QualifiedName { get; init; }

    /// <summary>Gets the columns, as the database names them.</summary>
    public required IReadOnlyList<RestColumn> Columns { get; init; }

    /// <summary>Gets the primary key's columns; empty for a view or a table without one.</summary>
    public IReadOnlyList<string> Keys { get; init; } = [];

    /// <summary>Gets a value indicating whether rows can be written: a table, not a view.</summary>
    public bool Writable { get; init; }

    /// <summary>Gets the column whose values the database generates on insert (an identity), or null.</summary>
    public string? Generated { get; init; }

    /// <summary>Gets how <c>$search</c> searches the table, or null when it is not declared searchable.</summary>
    public RestSearchable? Searchable { get; init; }

    /// <summary>
    /// Finds a column by name, ignoring case.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="where">Where the name was given, for the message.</param>
    /// <returns>The column.</returns>
    /// <exception cref="ArgumentException">There is no such column.</exception>
    public RestColumn Column(string name, string where) =>
        Columns.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"'{name}' in {where} is not a column of {Name}.");

    /// <summary>
    /// Describes a table the DDL methods read back.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="qualifiedName">Its name, quoted and schema-qualified.</param>
    /// <returns>The description.</returns>
    public static RestTable Of(DmTable table, string qualifiedName) =>
        new()
        {
            SchemaName = table.SchemaName,
            Name = table.TableName,
            QualifiedName = qualifiedName,
            Columns =
            [
                .. table.Columns.Select(c =>
                    new RestColumn(c.ColumnName, c.DotnetType ?? typeof(string), c.IsNullable && !c.IsPrimaryKey, c.IsAutoIncrement || !string.IsNullOrWhiteSpace(c.DefaultExpression))
                ),
            ],
            Keys = table.PrimaryKeyConstraint is { } key ? [.. key.Columns.Select(c => c.ColumnName)] : [.. table.Columns.Where(c => c.IsPrimaryKey).Select(c => c.ColumnName)],
            Generated = table.Columns.FirstOrDefault(c => c.IsAutoIncrement)?.ColumnName,
            Writable = true,
        };

    /// <summary>
    /// Copies the table, searched as given.
    /// </summary>
    /// <param name="searchable">How <c>$search</c> searches it.</param>
    /// <returns>The copy.</returns>
    public RestTable WithSearchable(RestSearchable searchable) =>
        new()
        {
            SchemaName = SchemaName,
            Name = Name,
            QualifiedName = QualifiedName,
            Columns = Columns,
            Keys = Keys,
            Writable = Writable,
            Generated = Generated,
            Searchable = searchable,
        };

    /// <summary>
    /// Makes the definition the query tree reads sources with.
    /// </summary>
    /// <returns>The definition.</returns>
    public DmTable ToTable() =>
        new()
        {
            SchemaName = SchemaName,
            TableName = Name,
            Columns = [.. Columns.Select(c => new DmColumn(SchemaName, Name, c.Name, c.Type, isNullable: c.Nullable))],
        };
}

/// <summary>
/// A column of a REST source.
/// </summary>
/// <param name="Name">The name, as the database gives it.</param>
/// <param name="Type">The .NET type its values are read and compared as.</param>
/// <param name="Nullable">Whether it can be NULL.</param>
/// <param name="Filled">Whether the database fills it when a row does not give it: a default or an identity.</param>
internal sealed record RestColumn(string Name, Type Type, bool Nullable, bool Filled = false);

/// <summary>
/// How a table is searched: its columns, and the database's full-text index when it has one.
/// </summary>
/// <param name="Columns">The columns searched.</param>
/// <param name="Native">
/// The full-text condition, trusted SQL read from the database's index definitions: {0} is the source's key column
/// (or its rowid on SQLite), {1} the search text as <paramref name="Query"/> writes it; null for a contains match.
/// </param>
/// <param name="Query">How the native condition takes the search's words.</param>
internal sealed record RestSearchable(IReadOnlyList<string> Columns, string? Native = null, RestSearchQuery Query = RestSearchQuery.Text);

/// <summary>How a full-text condition takes the search's words.</summary>
internal enum RestSearchQuery
{
    /// <summary>As the request gives them: PostgreSQL's <c>websearch_to_tsquery</c> reads them.</summary>
    Text,

    /// <summary>MySQL's boolean mode: every word required, each a prefix (<c>+acme* +corp*</c>).</summary>
    MySqlBoolean,

    /// <summary>SQLite's FTS5: every word a quoted prefix, ANDed (<c>"acme"* AND "corp"*</c>).</summary>
    Fts5,
}

/// <summary>
/// A relation a REST request may follow from a source: declared for the endpoint, never inferred.
/// </summary>
/// <param name="Name">The name requests use: <c>customer</c> in <c>customer/country</c>.</param>
/// <param name="Other">The related table.</param>
/// <param name="Keys">The key pairs: this table's column, then the related table's.</param>
/// <param name="Many">Whether a row relates to many rows.</param>
internal sealed record RestRelation(string Name, RestTable Other, IReadOnlyList<(string This, string Other)> Keys, bool Many);

/// <summary>
/// How much a REST request may ask for.
/// </summary>
internal sealed class RestLimits
{
    /// <summary>Gets the most rows a request may read, and the default.</summary>
    public int MaxTop { get; init; } = 1_000;

    /// <summary>Gets the most levels of <c>$expand</c>.</summary>
    public int MaxExpandDepth { get; init; } = 3;

    /// <summary>Gets the most levels of <c>any</c> and <c>all</c> within each other.</summary>
    public int MaxLambdaDepth { get; init; } = 2;

    /// <summary>Gets the most nodes of a request's conditions, orderings and values.</summary>
    public int MaxNodes { get; init; } = 200;

    /// <summary>Gets the most related rows a request's expands may read, all of them together.</summary>
    public int MaxExpandedRows { get; init; } = 10_000;

    /// <summary>Gets the most rows one insert may hold.</summary>
    public int MaxInsertRows { get; init; } = 1_000;

    /// <summary>Gets the most rows one update or delete may change, or null for no limit.</summary>
    public int? MaxAffectedRows { get; init; }

    /// <summary>
    /// Gets the most levels a request's conditions and orderings may nest, however many nodes they may have: the
    /// conditions are walked recursively, and a stack overflow cannot be caught.
    /// </summary>
    public int MaxDepth { get; init; } = 200;

    /// <summary>
    /// Checks a whole request, its expands' too, before anything walks it: its nodes, values included, against
    /// <see cref="MaxNodes"/>, and how deep they nest against <see cref="MaxDepth"/>. It does not recurse itself.
    /// </summary>
    /// <param name="query">The request.</param>
    /// <exception cref="ArgumentException">The request has too many nodes, or nests them too deep.</exception>
    public void Check(RestQuery query)
    {
        var nodes = new Stack<(RestNode Node, int Depth)>();
        var queries = new Stack<RestQuery>([query]);
        while (queries.TryPop(out var q))
        {
            if (q.Filter is not null)
            {
                nodes.Push((q.Filter, 1));
            }

            foreach (var order in q.OrderBy)
            {
                nodes.Push((order.Key, 1));
            }

            foreach (var expand in q.Expand)
            {
                queries.Push(expand.Query);
            }
        }

        var count = 0;
        while (nodes.TryPop(out var item))
        {
            var (node, depth) = item;
            count += node is RestIn list ? 1 + list.Values.Count : 1;
            if (count > MaxNodes)
            {
                throw new ArgumentException($"The request's conditions, orderings and values have more than {MaxNodes} parts.");
            }

            if (depth > MaxDepth)
            {
                throw new ArgumentException($"The request's conditions nest more than {MaxDepth} levels deep.");
            }

            List<RestNode?> children = node switch
            {
                RestBinary binary => [binary.Left, binary.Right],
                RestNot not => [not.Operand],
                RestIn values => [values.Operand],
                RestCall call => [.. call.Arguments],
                RestLambda lambda => [lambda.Collection, lambda.Predicate],
                _ => [],
            };
            foreach (var child in children.OfType<RestNode>())
            {
                nodes.Push((child, depth + 1));
            }
        }
    }
}
