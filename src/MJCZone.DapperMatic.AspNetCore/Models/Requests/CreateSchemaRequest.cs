// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace MJCZone.DapperMatic.AspNetCore.Models.Requests;

/// <summary>
/// Request model for creating a new schema.
/// </summary>
public class CreateSchemaRequest
{
    /// <summary>
    /// Gets or sets the schema name.
    /// </summary>
    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string SchemaName { get; set; } = default!;
}