// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace MJCZone.DapperMatic.AspNetCore.Models.Requests;

/// <summary>
/// Request model for creating a unique constraint.
/// </summary>
public class CreateTableUniqueConstraintRequest
{
    /// <summary>
    /// Gets or sets the unique constraint name. If not provided, a name will be generated.
    /// </summary>
    [StringLength(128)]
    public string? ConstraintName { get; set; }

    /// <summary>
    /// Gets or sets the column names that make up the unique constraint.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one column is required")]
    public List<string> ColumnNames { get; set; } = default!;
}