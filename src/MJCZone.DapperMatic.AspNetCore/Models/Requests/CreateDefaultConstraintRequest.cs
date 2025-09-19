// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace MJCZone.DapperMatic.AspNetCore.Models.Requests;

/// <summary>
/// Request model for creating a default constraint.
/// </summary>
public class CreateDefaultConstraintRequest
{
    /// <summary>
    /// Gets or sets the constraint name.
    /// If not provided, a default name will be generated.
    /// </summary>
    [StringLength(128, MinimumLength = 1)]
    public string? ConstraintName { get; set; }

    /// <summary>
    /// Gets or sets the column name to apply the default constraint to.
    /// </summary>
    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string ColumnName { get; set; } = default!;

    /// <summary>
    /// Gets or sets the default value expression.
    /// </summary>
    [Required]
    public string DefaultExpression { get; set; } = default!;
}