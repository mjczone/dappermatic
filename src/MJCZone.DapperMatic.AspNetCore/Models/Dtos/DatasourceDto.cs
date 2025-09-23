// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.Providers;

namespace MJCZone.DapperMatic.AspNetCore.Models.Dtos;

/// <summary>
/// Represents a datasource registration for DapperMatic operations.
/// Contains connection information and metadata for database access.
/// </summary>
public sealed class DatasourceDto
{
    /// <summary>
    /// Gets or sets the unique name identifier for this datasource.
    /// </summary>
    [StringLength(64)]
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the database provider type.
    /// </summary>
    [StringLength(10)]
    public string? Provider { get; set; }

    /// <summary>
    /// Gets or sets the connection string for database access.
    /// </summary>
    [StringLength(2000)]
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the display name for this datasource.
    /// </summary>
    [StringLength(128)]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets a description of this datasource.
    /// </summary>
    [StringLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets additional tags for categorizing this datasource.
    /// </summary>
    public ICollection<string>? Tags { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether gets or sets whether this datasource is enabled for use.
    /// </summary>
    public bool? IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the date and time when this datasource was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the date and time when this datasource was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
