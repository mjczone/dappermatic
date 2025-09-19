// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore.Models.Dtos;

/// <summary>
/// Represents information about a field in query results.
/// </summary>
public class FieldDto
{
    /// <summary>
    /// Gets or sets the field name.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// Gets or sets the friendly .NET type name for the field (e.g., "String", "Int32", "List&lt;String&gt;").
    /// </summary>
    public string FieldType { get; set; } = default!;

    /// <summary>
    /// Gets or sets a value indicating whether the field allows null values.
    /// </summary>
    public bool IsNullable { get; set; }
}