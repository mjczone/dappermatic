// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace MJCZone.DapperMatic.AspNetCore.Models.Dtos;

/// <summary>
/// Data transfer object representing a unique constraint.
/// </summary>
public class UniqueConstraintDto
{
    /// <summary>
    /// Gets or sets the constraint name.
    /// If not provided, a default name will be generated.
    /// </summary>
    [StringLength(128)]
    public string? ConstraintName { get; set; }

    /// <summary>
    /// Gets or sets the columns that make up the unique constraint.
    /// Column names can include sort order (e.g., "ColumnName ASC" or "ColumnName DESC").
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one column is required")]
    public List<string> ColumnNames { get; set; } = [];
}
