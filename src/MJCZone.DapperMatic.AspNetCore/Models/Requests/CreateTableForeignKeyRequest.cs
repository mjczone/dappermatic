// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace MJCZone.DapperMatic.AspNetCore.Models.Requests;

/// <summary>
/// Request model for creating a foreign key constraint.
/// </summary>
public class CreateTableForeignKeyRequest
{
    /// <summary>
    /// Gets or sets the foreign key constraint name. If not provided, a name will be generated.
    /// </summary>
    [StringLength(128)]
    public string? ConstraintName { get; set; }

    /// <summary>
    /// Gets or sets the column names in this table that reference the foreign table.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one column is required for a foreign key")]
    public List<string> Columns { get; set; } = default!;

    /// <summary>
    /// Gets or sets the referenced table name.
    /// </summary>
    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string ReferencedTableName { get; set; } = default!;

    /// <summary>
    /// Gets or sets the referenced table schema name. Optional for databases that don't support schemas.
    /// </summary>
    [StringLength(128)]
    public string? ReferencedSchemaName { get; set; }

    /// <summary>
    /// Gets or sets the referenced column names in the foreign table.
    /// Must have the same number of columns as the Columns property.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one referenced column is required")]
    public List<string> ReferencedColumns { get; set; } = default!;

    /// <summary>
    /// Gets or sets the action to take when the referenced row is deleted.
    /// Valid values: "NoAction", "Cascade", "SetNull", "SetDefault", "Restrict".
    /// </summary>
    public string? OnDelete { get; set; }

    /// <summary>
    /// Gets or sets the action to take when the referenced row is updated.
    /// Valid values: "NoAction", "Cascade", "SetNull", "SetDefault", "Restrict".
    /// </summary>
    public string? OnUpdate { get; set; }
}