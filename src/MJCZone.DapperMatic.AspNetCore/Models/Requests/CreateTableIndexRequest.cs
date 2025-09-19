// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace MJCZone.DapperMatic.AspNetCore.Models.Requests;

/// <summary>
/// Request model for creating a table index.
/// </summary>
public class CreateTableIndexRequest
{
    /// <summary>
    /// Gets or sets the index name.
    /// </summary>
    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string IndexName { get; set; } = default!;

    /// <summary>
    /// Gets or sets the column names with optional sort order.
    /// Format: "ColumnName" or "ColumnName ASC" or "ColumnName DESC".
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one column is required for an index")]
    public List<string> Columns { get; set; } = default!;

    /// <summary>
    /// Gets or sets a value indicating whether the index enforces uniqueness.
    /// </summary>
    public bool IsUnique { get; set; }
}