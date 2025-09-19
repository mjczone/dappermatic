// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace MJCZone.DapperMatic.AspNetCore.Models.Requests;

/// <summary>
/// Request model for creating a foreign key constraint.
/// </summary>
public class CreateForeignKeyRequest
{
    /// <summary>
    /// Gets or sets the constraint name.
    /// If not provided, a default name will be generated.
    /// </summary>
    [StringLength(128, MinimumLength = 1)]
    public string? ConstraintName { get; set; }

    /// <summary>
    /// Gets or sets the columns in the table that reference the foreign table.
    /// </summary>
    [Required]
    [MinLength(1)]
    public List<string> Columns { get; set; } = default!;

    /// <summary>
    /// Gets or sets the referenced table name.
    /// </summary>
    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string ReferencedTableName { get; set; } = default!;

    /// <summary>
    /// Gets or sets the columns in the referenced table.
    /// </summary>
    [Required]
    [MinLength(1)]
    public List<string> ReferencedColumns { get; set; } = default!;

    /// <summary>
    /// Gets or sets the action to take on update.
    /// Options: "NoAction", "Cascade", "SetNull", "SetDefault", "Restrict"
    /// </summary>
    public string? OnUpdate { get; set; }

    /// <summary>
    /// Gets or sets the action to take on delete.
    /// Options: "NoAction", "Cascade", "SetNull", "SetDefault", "Restrict"
    /// </summary>
    public string? OnDelete { get; set; }
}