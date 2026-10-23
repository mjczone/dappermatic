// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore.Models;

/// <summary>
/// A table <c>$search</c> searches, and the columns it searches. Where the database has a full-text index of them,
/// it is used: a PostgreSQL GIN or GiST index on <c>to_tsvector(...)</c> of the columns, a MySQL <c>FULLTEXT</c>
/// index of exactly the columns, or a SQLite FTS5 table named <c>{table}_fts</c> whose rowids are the table's. Else
/// every word must be contained, ignoring case, in one of the columns.
/// </summary>
/// <example>
/// <code>
/// options.Searchable.Add(new DapperMaticSearchable { TableName = "articles", Columns = ["title", "body"] });
/// </code>
/// </example>
public sealed class DapperMaticSearchable
{
    /// <summary>Gets or sets the datasource, or null for the table in every datasource.</summary>
    public string? DatasourceId { get; set; }

    /// <summary>Gets or sets the table's schema, or null for the table in every schema.</summary>
    public string? SchemaName { get; set; }

    /// <summary>Gets or sets the table.</summary>
    public required string TableName { get; set; }

    /// <summary>Gets or sets the columns searched.</summary>
    public required string[] Columns { get; set; }
}
