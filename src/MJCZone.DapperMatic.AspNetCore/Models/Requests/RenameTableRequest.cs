// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace MJCZone.DapperMatic.AspNetCore.Models.Requests;

/// <summary>
/// Request model for updating an existing table.
/// </summary>
public class RenameTableRequest
{
    /// <summary>
    /// Gets or sets the new name for the table.
    /// </summary>
    [Required]
    public string NewTableName { get; set; } = default!;

    /// <summary>
    /// Gets or sets the schema name where the table resides.
    /// This is optional and used when working with schema-specific tables.
    /// </summary>
    public string? SchemaName { get; set; }
}
