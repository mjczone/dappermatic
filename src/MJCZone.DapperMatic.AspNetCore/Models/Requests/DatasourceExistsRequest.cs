// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace MJCZone.DapperMatic.AspNetCore.Models.Requests;

/// <summary>
/// Request model for checking if a datasource exists by name.
/// </summary>
public class DatasourceExistsRequest
{
    /// <summary>
    /// Gets or sets the name of the datasource to check.
    /// </summary>
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Id { get; set; } = string.Empty;
}