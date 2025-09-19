// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace MJCZone.DapperMatic.AspNetCore.Models.Requests;

/// <summary>
/// Request model for creating a check constraint.
/// </summary>
public class CreateTableCheckConstraintRequest
{
    /// <summary>
    /// Gets or sets the check constraint name. If not provided, a name will be generated.
    /// </summary>
    [StringLength(128)]
    public string? ConstraintName { get; set; }

    /// <summary>
    /// Gets or sets the column name the check constraint applies to. Optional; if not provided, the constraint is table-level.
    /// </summary>
    [StringLength(128)]
    public string? ColumnName { get; set; }

    /// <summary>
    /// Gets or sets the check constraint expression (e.g., "Age >= 0", "Status IN ('Active', 'Inactive')").
    /// </summary>
    [Required]
    public string CheckExpression { get; set; } = default!;
}
