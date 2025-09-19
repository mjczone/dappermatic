// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace MJCZone.DapperMatic.AspNetCore.Models.Requests;

/// <summary>
/// Request model for updating an existing view.
/// </summary>
public class UpdateViewRequest
{
    /// <summary>
    /// Gets or sets the new name for the view.
    /// </summary>
    public string? NewViewName { get; set; } = default!;

    /// <summary>
    /// Gets or sets the SQL definition of the view.
    /// </summary>
    public string? ViewDefinition { get; set; } = default!;
}
