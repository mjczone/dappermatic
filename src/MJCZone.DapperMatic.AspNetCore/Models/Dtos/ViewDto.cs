// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore.Models.Dtos;

/// <summary>
/// Data transfer object for view information.
/// </summary>
public class ViewDto
{
    /// <summary>
    /// Gets or sets the schema name of the view.
    /// </summary>
    public string? SchemaName { get; set; }

    /// <summary>
    /// Gets or sets the name of the view.
    /// </summary>
    public required string ViewName { get; set; }

    /// <summary>
    /// Gets or sets the definition of the view.
    /// </summary>
    public required string Definition { get; set; }
}