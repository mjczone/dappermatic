// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for a single provider data type.
/// </summary>
public class ProviderDataType
{
    /// <summary>
    /// Gets or sets the data type name (e.g., "VARCHAR", "INTEGER", "DECIMAL").
    /// </summary>
    public string DataType { get; set; } = default!;

    /// <summary>
    /// Gets or sets any aliases for this data type.
    /// </summary>
    public List<string> Aliases { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether this is a commonly used data type.
    /// </summary>
    public bool IsCommon { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is a custom/user-defined type.
    /// </summary>
    public bool IsCustom { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this data type supports length specification.
    /// </summary>
    public bool SupportsLength { get; set; }

    /// <summary>
    /// Gets or sets the minimum length for this data type (if supported).
    /// </summary>
    public int? MinLength { get; set; }

    /// <summary>
    /// Gets or sets the maximum length for this data type (if supported).
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// Gets or sets the default length for this data type (if supported).
    /// </summary>
    public int? DefaultLength { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this data type supports precision specification.
    /// </summary>
    public bool SupportsPrecision { get; set; }

    /// <summary>
    /// Gets or sets the minimum precision for this data type (if supported).
    /// </summary>
    public int? MinPrecision { get; set; }

    /// <summary>
    /// Gets or sets the maximum precision for this data type (if supported).
    /// </summary>
    public int? MaxPrecision { get; set; }

    /// <summary>
    /// Gets or sets the default precision for this data type (if supported).
    /// </summary>
    public int? DefaultPrecision { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this data type supports scale specification.
    /// </summary>
    public bool SupportsScale { get; set; }

    /// <summary>
    /// Gets or sets the minimum scale for this data type (if supported).
    /// </summary>
    public int? MinScale { get; set; }

    /// <summary>
    /// Gets or sets the maximum scale for this data type (if supported).
    /// </summary>
    public int? MaxScale { get; set; }

    /// <summary>
    /// Gets or sets the default scale for this data type (if supported).
    /// </summary>
    public int? DefaultScale { get; set; }

    /// <summary>
    /// Gets or sets the data type category (e.g., "Numeric", "String", "DateTime", "Binary").
    /// </summary>
    public string Category { get; set; } = default!;

    /// <summary>
    /// Gets or sets an optional description or example of the data type.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets example values or use cases for this data type.
    /// </summary>
    public List<string>? Examples { get; set; }
}
