// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore.Models;

/// <summary>
/// How much a query request may ask for; a request that asks for more is refused with 400.
/// </summary>
public sealed class DapperMaticQueryLimits
{
    /// <summary>
    /// Gets or sets the most rows a request, or an expand per row, may ask for; a request that asks for no count (a
    /// GraphQL list without <c>first</c>) reads this many at most. REST's own page, without <c>$top</c>, is 100 rows.
    /// </summary>
    public int MaxTop { get; set; } = 1_000;

    /// <summary>Gets or sets the most levels of <c>$expand</c>.</summary>
    public int MaxExpandDepth { get; set; } = 3;

    /// <summary>Gets or sets the most levels of <c>any</c>, <c>all</c> and <c>$count</c> within each other.</summary>
    public int MaxLambdaDepth { get; set; } = 2;

    /// <summary>Gets or sets the most parts of a request's conditions, orderings and values.</summary>
    public int MaxNodes { get; set; } = 200;

    /// <summary>
    /// Gets or sets the most related rows a request's expands may read, all of them together. An expand without
    /// <c>$top</c> reads every related row of each row read.
    /// </summary>
    public int MaxExpandedRows { get; set; } = 10_000;

    /// <summary>Gets or sets the most rows one insert request may hold.</summary>
    public int MaxInsertRows { get; set; } = 1_000;

    /// <summary>
    /// Gets or sets the most rows one update or delete request may change, or null for no limit. A request that would
    /// change more is rolled back and refused.
    /// </summary>
    public int? MaxAffectedRows { get; set; }

    /// <summary>Gets or sets the most operations one batch may hold.</summary>
    public int MaxBatchOperations { get; set; } = 100;
}
