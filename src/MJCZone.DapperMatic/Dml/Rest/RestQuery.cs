// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

#pragma warning disable SA1313 // Positional record parameters are properties, so they are named as properties.
#pragma warning disable SA1402 // The REST query's parts belong together.
#pragma warning disable SA1649

namespace MJCZone.DapperMatic.Dml.Rest;

/// <summary>
/// A query from a REST request, before it is checked against a table and translated: what an OData-shaped query string
/// (<c>$filter</c>, <c>$select</c>, <c>$orderby</c>, <c>$top</c>, <c>$skip</c>, <c>$count</c>, <c>$expand</c>) or a
/// JSON body parses to.
/// </summary>
internal sealed record RestQuery
{
    /// <summary>Gets the condition, or null for every row.</summary>
    public RestNode? Filter { get; init; }

    /// <summary>Gets the columns to read; empty for all.</summary>
    public IReadOnlyList<string> Select { get; init; } = [];

    /// <summary>Gets the ordering keys.</summary>
    public IReadOnlyList<RestOrder> OrderBy { get; init; } = [];

    /// <summary>Gets the most rows, or null.</summary>
    public int? Top { get; init; }

    /// <summary>Gets the rows to skip, or null.</summary>
    public int? Skip { get; init; }

    /// <summary>Gets a value indicating whether to count the rows that match, ignoring Top and Skip.</summary>
    public bool Count { get; init; }

    /// <summary>Gets the relations to read with each row.</summary>
    public IReadOnlyList<RestExpand> Expand { get; init; } = [];

    /// <summary>Gets the cursor of the page this one follows, as a page's <c>nextCursor</c> gives it; or null.</summary>
    public string? After { get; init; }

    /// <summary>
    /// Gets the rows' grouping and aggregates (<c>$apply</c>), or null: with it, <see cref="Filter"/>,
    /// <see cref="OrderBy"/>, <see cref="Top"/> and <see cref="Skip"/> apply to the groups, as OData has them.
    /// </summary>
    public RestApply? Apply { get; init; }

    /// <summary>Gets the words the rows' searchable columns must hold (<c>$search</c>), or null.</summary>
    public string? Search { get; init; }

    /// <summary>
    /// Gets a value indicating whether each row's cursor is read too (a Relay edge's): the query must be paged by keys.
    /// </summary>
    public bool Cursors { get; init; }

    /// <summary>
    /// Gets a value indicating whether one row more than <see cref="Top"/> is read, to tell whether more follow; it is
    /// not returned, and not held to <c>MaxTop</c>.
    /// </summary>
    public bool Probe { get; init; }
}

/// <summary>
/// The rows' grouping and aggregates, as OData's <c>$apply</c> writes them: <c>filter(...)</c> steps over the rows,
/// then one <c>groupby((keys),aggregate(...))</c> or <c>aggregate(...)</c>.
/// </summary>
internal sealed record RestApply
{
    /// <summary>Gets the condition on the rows grouped, the filter steps ANDed; or null.</summary>
    public RestNode? Filter { get; init; }

    /// <summary>Gets the columns grouped by; none for one group of every row.</summary>
    public IReadOnlyList<RestPath> GroupBy { get; init; } = [];

    /// <summary>Gets the aggregates of each group.</summary>
    public IReadOnlyList<RestAggregate> Aggregates { get; init; } = [];
}

/// <summary>
/// An aggregate of a group: <c>amount with sum as total</c>, or <c>$count as n</c>.
/// </summary>
/// <param name="Value">The value aggregated, or null for the count of rows.</param>
/// <param name="Method"><c>sum</c>, <c>min</c>, <c>max</c>, <c>average</c>, <c>countdistinct</c>, or <c>count</c> of rows.</param>
/// <param name="Alias">The name of the aggregate in the results.</param>
internal sealed record RestAggregate(RestNode? Value, string Method, string Alias);

/// <summary>
/// An ordering key.
/// </summary>
/// <param name="Key">The key: a column, or a related row's column.</param>
/// <param name="Descending">Whether the order is descending.</param>
internal sealed record RestOrder(RestNode Key, bool Descending);

/// <summary>
/// A page of a query's rows.
/// </summary>
/// <param name="Rows">The rows, as column name to value, with each expand under its name.</param>
/// <param name="Count">How many rows match, when asked for; else null.</param>
/// <param name="NextCursor">The cursor of the next page, or null when this is the last or the rows are not paged by keys.</param>
/// <param name="Cursors">Each row's cursor, when asked for; else null.</param>
/// <param name="HasMore">Whether rows follow the page, when the query probed for them.</param>
internal sealed record RestPage(List<Dictionary<string, object?>> Rows, long? Count, string? NextCursor, List<string>? Cursors, bool HasMore);

/// <summary>
/// A relation to read with each row, and which of its rows.
/// </summary>
/// <param name="Relation">The relation's name.</param>
/// <param name="Query">Its rows' own filter, columns, order, top, skip and expands.</param>
/// <param name="As">
/// The name the related rows are put under in each row, or null for the relation's: one relation may be expanded twice,
/// with other rows, under two names.
/// </param>
internal sealed record RestExpand(string Relation, RestQuery Query, string? As = null);

/// <summary>
/// A node of a REST condition or value.
/// </summary>
internal abstract record RestNode;

/// <summary>
/// A column, a related row's column (<c>customer/country</c>), or a lambda variable's (<c>l/qty</c>).
/// </summary>
/// <param name="Segments">The path's names, in order.</param>
internal sealed record RestPath(IReadOnlyList<string> Segments) : RestNode
{
    /// <inheritdoc />
    public override string ToString() => string.Join('/', Segments);
}

/// <summary>
/// A value: a string, a number, true, false or null; converted to the type of the column it is compared with.
/// </summary>
/// <param name="Value">The value: a string, a decimal or long, a bool, or null.</param>
internal sealed record RestLiteral(object? Value) : RestNode;

/// <summary>
/// <c>left op right</c>: <c>eq</c>, <c>ne</c>, <c>gt</c>, <c>ge</c>, <c>lt</c>, <c>le</c>, <c>and</c>, <c>or</c>,
/// <c>add</c>, <c>sub</c>, <c>mul</c>, <c>div</c>, <c>mod</c>.
/// </summary>
/// <param name="Operator">The operator.</param>
/// <param name="Left">The left side.</param>
/// <param name="Right">The right side.</param>
internal sealed record RestBinary(string Operator, RestNode Left, RestNode Right) : RestNode;

/// <summary>
/// <c>not condition</c>.
/// </summary>
/// <param name="Operand">The condition.</param>
internal sealed record RestNot(RestNode Operand) : RestNode;

/// <summary>
/// A function: <c>contains</c>, <c>startswith</c>, <c>endswith</c>, <c>tolower</c>, <c>toupper</c>, <c>trim</c>,
/// <c>length</c>, <c>year</c>, <c>month</c>, <c>day</c>, <c>hour</c>, <c>minute</c>, <c>second</c>.
/// </summary>
/// <param name="Function">The function, lower case.</param>
/// <param name="Arguments">Its arguments.</param>
internal sealed record RestCall(string Function, IReadOnlyList<RestNode> Arguments) : RestNode;

/// <summary>
/// <c>operand in (a, b, c)</c>.
/// </summary>
/// <param name="Operand">The operand.</param>
/// <param name="Values">The values.</param>
/// <param name="ValuesFrom">A value of the context whose list is the values, as <c>in @tenant.below</c>; null for the listed values.</param>
internal sealed record RestIn(RestNode Operand, IReadOnlyList<RestLiteral> Values, RestParameter? ValuesFrom = null) : RestNode;

/// <summary>
/// A value of the request's context, as policies read them: <c>@tenant.id</c>, <c>@tenant.below</c>,
/// <c>@user.id</c>, <c>@claims.region</c>, <c>@values.name</c>. Replaced by its value before translation.
/// </summary>
/// <param name="Path">The value's path, without the <c>@</c>.</param>
internal sealed record RestParameter(string Path) : RestNode;

/// <summary>
/// <c>lines/any()</c>, <c>lines/any(l: l/qty gt 5)</c>, <c>lines/all(l: ...)</c>; and a collection's count,
/// <c>lines/$count</c>.
/// </summary>
/// <param name="Kind"><c>any</c>, <c>all</c> or <c>$count</c>.</param>
/// <param name="Collection">The relation, from the row or a related row.</param>
/// <param name="Variable">The lambda variable, or null.</param>
/// <param name="Predicate">The condition on each related row, or null.</param>
internal sealed record RestLambda(string Kind, RestPath Collection, string? Variable, RestNode? Predicate) : RestNode;

/// <summary>
/// A condition from the application's policies, not the request: it is not counted against the request's limits.
/// </summary>
/// <param name="Inner">The condition.</param>
internal sealed record RestTrusted(RestNode Inner) : RestNode;

/// <summary>
/// A search of a table's searchable columns for words: by the database's full-text index where it has one, or a
/// case-insensitive contains match of every word in any of the columns.
/// </summary>
/// <param name="Text">The words, as <c>$search</c> gives them; a phrase in double quotes is one.</param>
internal sealed record RestSearch(string Text) : RestNode;
