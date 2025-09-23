// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore.Models.Dtos;

/// <summary>
/// Data transfer object representing a database table optimized for API responses.
/// Properties are nullable to support partial updates via PUT/PATCH operations.
/// </summary>
public class TableDto
{
    /// <summary>
    /// Gets or sets the schema name of the table.
    /// </summary>
    public string? SchemaName { get; set; }

    /// <summary>
    /// Gets or sets the name of the table.
    /// </summary>
    public string? TableName { get; set; }

    /// <summary>
    /// Gets or sets the columns of the table.
    /// </summary>
    public List<ColumnDto>? Columns { get; set; }

    /// <summary>
    /// Gets or sets the primary key constraint of the table.
    /// </summary>
    public PrimaryKeyConstraintDto? PrimaryKeyConstraint { get; set; }

    /// <summary>
    /// Gets or sets the check constraints of the table.
    /// </summary>
    public List<CheckConstraintDto>? CheckConstraints { get; set; }

    /// <summary>
    /// Gets or sets the default constraints of the table.
    /// </summary>
    public List<DefaultConstraintDto>? DefaultConstraints { get; set; }

    /// <summary>
    /// Gets or sets the unique constraints of the table.
    /// </summary>
    public List<UniqueConstraintDto>? UniqueConstraints { get; set; }

    /// <summary>
    /// Gets or sets the foreign key constraints of the table.
    /// </summary>
    public List<ForeignKeyConstraintDto>? ForeignKeyConstraints { get; set; }

    /// <summary>
    /// Gets or sets the indexes of the table.
    /// </summary>
    public List<IndexDto>? Indexes { get; set; }
}
