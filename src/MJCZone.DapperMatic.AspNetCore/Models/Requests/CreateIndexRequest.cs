// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace MJCZone.DapperMatic.AspNetCore.Models.Requests;

/// <summary>
/// Request model for creating an index on a table.
/// </summary>
public class CreateIndexRequest
{
    /// <summary>
    /// Gets or sets the index name.
    /// </summary>
    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string IndexName { get; set; } = default!;

    /// <summary>
    /// Gets or sets the columns that make up the index.
    /// Column names can include sort order (e.g., "ColumnName ASC" or "ColumnName DESC").
    /// </summary>
    [Required]
    [MinLength(1)]
    public List<string> Columns { get; set; } = default!;

    /// <summary>
    /// Gets or sets a value indicating whether this is a unique index.
    /// </summary>
    public bool IsUnique { get; set; }

    /// <summary>
    /// Gets or sets the filter expression for creating a filtered/partial index.
    /// This is supported by some databases like SQL Server and PostgreSQL.
    /// </summary>
    public string? FilterExpression { get; set; }
}