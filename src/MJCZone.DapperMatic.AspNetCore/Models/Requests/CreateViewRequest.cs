// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace MJCZone.DapperMatic.AspNetCore.Models.Requests;

/// <summary>
/// Request model for creating a new view.
/// </summary>
public class CreateViewRequest
{
    /// <summary>
    /// Gets or sets the view name.
    /// </summary>
    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string ViewName { get; set; } = default!;

    /// <summary>
    /// Gets or sets the schema name. Optional for databases that don't support schemas.
    /// </summary>
    [StringLength(128)]
    public string? SchemaName { get; set; }

    /// <summary>
    /// Gets or sets the SQL definition of the view.
    /// </summary>
    [Required]
    public string ViewDefinition { get; set; } = default!;
}