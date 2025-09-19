// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore.Models.Dtos;

/// <summary>
/// Data transfer object representing a database table optimized for API responses.
/// </summary>
public class TableDto
{
    /// <summary>
    /// Gets or sets the name of the table.
    /// </summary>
    public string TableName { get; set; } = default!;

    /// <summary>
    /// Gets or sets the columns of the table.
    /// </summary>
    public List<ColumnDto> Columns { get; set; } = [];

    /// <summary>
    /// Gets or sets the primary key constraint of the table.
    /// </summary>
    public PrimaryKeyConstraintDto? PrimaryKeyConstraint { get; set; }

    /// <summary>
    /// Gets or sets the check constraints of the table.
    /// </summary>
    public List<CheckConstraintDto> CheckConstraints { get; set; } = [];

    /// <summary>
    /// Gets or sets the default constraints of the table.
    /// </summary>
    public List<DefaultConstraintDto> DefaultConstraints { get; set; } = [];

    /// <summary>
    /// Gets or sets the unique constraints of the table.
    /// </summary>
    public List<UniqueConstraintDto> UniqueConstraints { get; set; } = [];

    /// <summary>
    /// Gets or sets the foreign key constraints of the table.
    /// </summary>
    public List<ForeignKeyConstraintDto> ForeignKeyConstraints { get; set; } = [];

    /// <summary>
    /// Gets or sets the indexes of the table.
    /// </summary>
    public List<IndexDto> Indexes { get; set; } = [];
}

/// <summary>
/// Data transfer object representing a primary key constraint.
/// </summary>
public class PrimaryKeyConstraintDto
{
    /// <summary>
    /// Gets or sets the constraint name.
    /// </summary>
    public string? ConstraintName { get; set; }

    /// <summary>
    /// Gets or sets the columns that make up the primary key.
    /// </summary>
    public List<string> ColumnNames { get; set; } = [];
}

/// <summary>
/// Data transfer object representing a check constraint.
/// </summary>
public class CheckConstraintDto
{
    /// <summary>
    /// Gets or sets the constraint name.
    /// </summary>
    public string? ConstraintName { get; set; }

    /// <summary>
    /// Gets or sets the column name.
    /// </summary>
    public string? ColumnName { get; set; }

    /// <summary>
    /// Gets or sets the check expression.
    /// </summary>
    public string? CheckExpression { get; set; }
}

/// <summary>
/// Data transfer object representing a default constraint.
/// </summary>
public class DefaultConstraintDto
{
    /// <summary>
    /// Gets or sets the constraint name.
    /// </summary>
    public string? ConstraintName { get; set; }

    /// <summary>
    /// Gets or sets the column name.
    /// </summary>
    public string? ColumnName { get; set; }

    /// <summary>
    /// Gets or sets the default expression.
    /// </summary>
    public string? DefaultExpression { get; set; }
}

/// <summary>
/// Data transfer object representing a unique constraint.
/// </summary>
public class UniqueConstraintDto
{
    /// <summary>
    /// Gets or sets the constraint name.
    /// </summary>
    public string? ConstraintName { get; set; }

    /// <summary>
    /// Gets or sets the columns that make up the unique constraint.
    /// </summary>
    public List<string> ColumnNames { get; set; } = [];
}

/// <summary>
/// Data transfer object representing a foreign key constraint.
/// </summary>
public class ForeignKeyConstraintDto
{
    /// <summary>
    /// Gets or sets the constraint name.
    /// </summary>
    public string? ConstraintName { get; set; }

    /// <summary>
    /// Gets or sets the column names in this table.
    /// </summary>
    public List<string> ColumnNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the referenced table name.
    /// </summary>
    public string? ReferencedTableName { get; set; }

    /// <summary>
    /// Gets or sets the referenced column names.
    /// </summary>
    public List<string> ReferencedColumnNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the action on delete.
    /// </summary>
    public string? OnDelete { get; set; }

    /// <summary>
    /// Gets or sets the action on update.
    /// </summary>
    public string? OnUpdate { get; set; }
}

/// <summary>
/// Data transfer object representing an index.
/// </summary>
public class IndexDto
{
    /// <summary>
    /// Gets or sets the index name.
    /// </summary>
    public string? IndexName { get; set; }

    /// <summary>
    /// Gets or sets the columns that make up the index.
    /// </summary>
    public List<string> ColumnNames { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the index is unique.
    /// </summary>
    public bool IsUnique { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the index is clustered.
    /// </summary>
    public bool IsClustered { get; set; }
}