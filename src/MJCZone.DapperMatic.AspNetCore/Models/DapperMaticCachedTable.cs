// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore.Models;

/// <summary>
/// A table or view whose query results are kept, for at most <see cref="Duration"/>. A write made through DapperMatic
/// (its row and batch endpoints, or <c>DapperMaticCallerDb</c>) makes them stale at once; a write made elsewhere is seen
/// when the duration runs out, or at once after <c>DapperMaticResultCache.InvalidateAsync</c>.
/// </summary>
/// <remarks>
/// A query is cached only when every table it reads is listed: its own, and those of the relations it follows.
/// </remarks>
/// <example>
/// <code>
/// options.CachedTables.Add(new DapperMaticCachedTable { TableName = "products", Duration = TimeSpan.FromMinutes(5) });
/// options.CachedTables.Add(new DapperMaticCachedTable { TableName = "product_sales", Duration = TimeSpan.FromMinutes(1), Reads = ["products", "sales"] });
/// </code>
/// </example>
public sealed class DapperMaticCachedTable
{
    /// <summary>Gets or sets the datasource, or null for the table in every datasource.</summary>
    public string? DatasourceId { get; set; }

    /// <summary>Gets or sets the table's schema, or null for the table in every schema.</summary>
    public string? SchemaName { get; set; }

    /// <summary>Gets or sets the table's or view's name.</summary>
    public required string TableName { get; set; }

    /// <summary>Gets or sets the longest its results are kept.</summary>
    public required TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets, for a view, the tables it reads (<c>table</c> or <c>schema.table</c>): a write to one of them makes
    /// the view's results stale. A view's rows change with its tables, which DapperMatic cannot read from its definition.
    /// </summary>
    public string[] Reads { get; set; } = [];
}
