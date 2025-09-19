// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace MJCZone.DapperMatic.AspNetCore.Models.Requests;

/// <summary>
/// Request model for creating a new table.
/// </summary>
public class CreateTableRequest
{
    /// <summary>
    /// Gets or sets the table name.
    /// </summary>
    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string TableName { get; set; } = default!;

    /// <summary>
    /// Gets or sets the schema name. Optional for databases that don't support schemas.
    /// </summary>
    [StringLength(128)]
    public string? SchemaName { get; set; }

    /// <summary>
    /// Gets or sets the columns for the table.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one column is required")]
    public List<CreateTableColumnRequest> Columns { get; set; } = default!;

    /// <summary>
    /// Gets or sets the primary key constraint for the table. Optional.
    /// </summary>
    public CreateTablePrimaryKeyRequest? PrimaryKey { get; set; }

    /// <summary>
    /// Gets or sets any foreign key constraints to create with the table.
    /// </summary>
    public List<CreateTableForeignKeyRequest>? ForeignKeys { get; set; }

    /// <summary>
    /// Gets or sets any check constraints to create with the table.
    /// </summary>
    public List<CreateTableCheckConstraintRequest>? CheckConstraints { get; set; }

    /// <summary>
    /// Gets or sets any default constraints to create with the table.
    /// </summary>
    public List<CreateTableDefaultConstraintRequest>? DefaultConstraints { get; set; }

    /// <summary>
    /// Gets or sets any unique constraints to create with the table.
    /// </summary>
    public List<CreateTableUniqueConstraintRequest>? UniqueConstraints { get; set; }

    /// <summary>
    /// Gets or sets any indexes to create with the table.
    /// </summary>
    public List<CreateTableIndexRequest>? Indexes { get; set; }
}