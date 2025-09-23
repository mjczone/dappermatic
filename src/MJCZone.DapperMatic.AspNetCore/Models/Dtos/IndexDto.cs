// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace MJCZone.DapperMatic.AspNetCore.Models.Dtos;

/// <summary>
/// Data transfer object representing an index.
/// </summary>
public class IndexDto
{
    /// <summary>
    /// Gets or sets the index name.
    /// </summary>
    [Required]
    [StringLength(128)]
    public string? IndexName { get; set; }

    /// <summary>
    /// Gets or sets the columns that make up the index.
    /// </summary>
    [Required]
    [MinLength(1)]
    public List<string> ColumnNames { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the index is unique.
    /// </summary>
    public bool IsUnique { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the index is clustered.
    /// </summary>
    public bool IsClustered { get; set; }
}