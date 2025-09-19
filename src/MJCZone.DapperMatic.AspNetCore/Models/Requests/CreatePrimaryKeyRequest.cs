// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace MJCZone.DapperMatic.AspNetCore.Models.Requests;

/// <summary>
/// Request model for creating a primary key constraint.
/// </summary>
public class CreatePrimaryKeyRequest
{
    /// <summary>
    /// Gets or sets the constraint name.
    /// If not provided, a default name will be generated.
    /// </summary>
    [StringLength(128, MinimumLength = 1)]
    public string? ConstraintName { get; set; }

    /// <summary>
    /// Gets or sets the columns that make up the primary key.
    /// Column names can include sort order (e.g., "ColumnName ASC" or "ColumnName DESC").
    /// </summary>
    [Required]
    [MinLength(1)]
    public List<string> Columns { get; set; } = default!;
}