// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace MJCZone.DapperMatic.AspNetCore.Models.Requests;

/// <summary>
/// Request model for creating a primary key constraint.
/// </summary>
public class CreateTablePrimaryKeyRequest
{
    /// <summary>
    /// Gets or sets the primary key constraint name. If not provided, a name will be generated.
    /// </summary>
    [StringLength(128)]
    public string? ConstraintName { get; set; }

    /// <summary>
    /// Gets or sets the column names that make up the primary key.
    /// For single-column primary keys, use a list with one item.
    /// For composite primary keys, list all columns in order.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one column is required for a primary key")]
    public List<string> Columns { get; set; } = default!;
}