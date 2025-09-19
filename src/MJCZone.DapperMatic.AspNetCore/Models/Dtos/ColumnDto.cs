// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore.Models.Dtos;

/// <summary>
/// Data transfer object representing a database column optimized for API responses.
/// </summary>
public class ColumnDto
{
    /// <summary>
    /// Gets or sets the column name.
    /// </summary>
    public string ColumnName { get; set; } = default!;

    /// <summary>
    /// Gets or sets the friendly .NET type name (e.g., "string", "int?", "Dictionary&lt;string, object&gt;").
    /// </summary>
    public string? DotnetTypeName { get; set; } = default!;

    /// <summary>
    /// Gets or sets the provider-specific data type (e.g., "VARCHAR(255)", "INTEGER", "DECIMAL(10,2)").
    /// </summary>
    public string ProviderDataType { get; set; } = default!;

    /// <summary>
    /// Gets or sets the length of the column.
    /// </summary>
    public int? Length { get; set; }

    /// <summary>
    /// Gets or sets the precision of the column.
    /// </summary>
    public int? Precision { get; set; }

    /// <summary>
    /// Gets or sets the scale of the column.
    /// </summary>
    public int? Scale { get; set; }

    /// <summary>
    /// Gets or sets the check constraint expression.
    /// </summary>
    public string? CheckExpression { get; set; }

    /// <summary>
    /// Gets or sets the default value expression.
    /// </summary>
    public string? DefaultExpression { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column is nullable.
    /// </summary>
    public bool IsNullable { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column is a primary key.
    /// </summary>
    public bool IsPrimaryKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column is auto-incremented.
    /// </summary>
    public bool IsAutoIncrement { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column is unique.
    /// </summary>
    public bool IsUnique { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column explicitly supports unicode characters.
    /// </summary>
    public bool IsUnicode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column is of a fixed length.
    /// </summary>
    public bool IsFixedLength { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column is indexed.
    /// </summary>
    public bool IsIndexed { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column is a foreign key.
    /// </summary>
    public bool IsForeignKey { get; set; }

    /// <summary>
    /// Gets or sets the referenced table name (if this is a foreign key).
    /// </summary>
    public string? ReferencedTableName { get; set; }

    /// <summary>
    /// Gets or sets the referenced column name (if this is a foreign key).
    /// </summary>
    public string? ReferencedColumnName { get; set; }
}