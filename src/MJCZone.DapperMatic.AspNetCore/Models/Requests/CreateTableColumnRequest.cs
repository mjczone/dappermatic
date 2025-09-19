// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace MJCZone.DapperMatic.AspNetCore.Models.Requests;

/// <summary>
/// Request model for creating a table column.
/// </summary>
public class CreateTableColumnRequest
{
    /// <summary>
    /// Gets or sets the column name.
    /// </summary>
    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string ColumnName { get; set; } = default!;

    /// <summary>
    /// Gets or sets the provider-specific data type (e.g., "VARCHAR(255)", "INTEGER", "DECIMAL(10,2)").
    /// </summary>
    [Required]
    public string ProviderDataType { get; set; } = default!;

    /// <summary>
    /// Gets or sets the check constraint expression.
    /// </summary>
    public string? CheckExpression { get; set; }

    /// <summary>
    /// Gets or sets the default value expression.
    /// </summary>
    public string? DefaultExpression { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column allows null values.
    /// </summary>
    public bool IsNullable { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the column is part of the primary key.
    /// </summary>
    public bool IsPrimaryKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column is auto-incremented.
    /// </summary>
    public bool IsAutoIncrement { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column has a unique constraint.
    /// </summary>
    public bool IsUnique { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column explicitly supports unicode characters.
    /// For MySQL, this controls whether CHARACTER SET utf8mb4 is added. Leave null for other providers.
    /// </summary>
    public bool? IsUnicode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column should have an index created.
    /// </summary>
    public bool IsIndexed { get; set; }
}