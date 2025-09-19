// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace MJCZone.DapperMatic.AspNetCore.Models.Requests;

/// <summary>
/// Request model for updating an existing datasource.
/// </summary>
public class UpdateDatasourceRequest
{
    /// <summary>
    /// Gets or sets the database provider type.
    /// </summary>
    [StringLength(10)]
    public string? Provider { get; set; }

    /// <summary>
    /// Gets or sets the connection string for the datasource.
    /// </summary>
    [StringLength(2000)]
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the display name for the datasource.
    /// </summary>
    [StringLength(128)]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the description of the datasource.
    /// </summary>
    [StringLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets additional tags for the datasource.
    /// </summary>
    public ICollection<string>? Tags { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the datasource is enabled.
    /// </summary>
    public bool? IsEnabled { get; set; }
}