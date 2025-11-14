// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace MJCZone.DapperMatic.AspNetCore.Models.Dtos;

/// <summary>
/// Data transfer object representing a foreign key constraint.
/// </summary>
public class ForeignKeyConstraintDto
{
    /// <summary>
    /// Gets or sets the constraint name.
    /// </summary>
    [StringLength(128)]
    public string? ConstraintName { get; set; }

    /// <summary>
    /// Gets or sets the column names in this table.
    /// </summary>
    [Required]
    [MinLength(1)]
    public List<string> ColumnNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the referenced table name.
    /// </summary>
    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string? ReferencedTableName { get; set; }

    /// <summary>
    /// Gets or sets the referenced column names.
    /// </summary>
    [Required]
    [MinLength(1)]
    public List<string> ReferencedColumnNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the action to take on update.
    /// Options: "NoAction", "Cascade", "SetNull", "SetDefault", "Restrict"
    /// </summary>
    [StringLength(20)]
    public string? OnDelete { get; set; }

    /// <summary>
    /// Gets or sets the action to take on delete.
    /// Options: "NoAction", "Cascade", "SetNull", "SetDefault", "Restrict"
    /// </summary>
    [StringLength(20)]
    public string? OnUpdate { get; set; }
}
