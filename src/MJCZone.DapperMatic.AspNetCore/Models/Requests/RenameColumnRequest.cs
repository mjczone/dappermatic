// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace MJCZone.DapperMatic.AspNetCore.Models.Requests;

/// <summary>
/// Request model for updating an existing column.
/// </summary>
public class RenameColumnRequest
{
    /// <summary>
    /// Gets or sets the new name for the column.
    /// If null, the column name is not changed.
    /// </summary>
    [StringLength(128, MinimumLength = 1)]
    public string? NewColumnName { get; set; }
}