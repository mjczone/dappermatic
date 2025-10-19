// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Providers;

/// <summary>
/// Provides default values used across all database providers for type mapping operations.
/// </summary>
public static class TypeMappingDefaults
{
    /// <summary>
    /// Default length for string/varchar columns when no length is specified.
    /// </summary>
    public const int DefaultStringLength = 255;

    /// <summary>
    /// Default length for binary/varbinary columns when no length is specified.
    /// </summary>
    public const int DefaultBinaryLength = 255;

    /// <summary>
    /// Default precision for decimal/numeric columns.
    /// </summary>
    public const int DefaultDecimalPrecision = 16;

    /// <summary>
    /// Default scale for decimal/numeric columns.
    /// </summary>
    public const int DefaultDecimalScale = 4;

    /// <summary>
    /// Default length for enum columns stored as varchar.
    /// </summary>
    public const int DefaultEnumLength = 128;

    /// <summary>
    /// String length required to store a GUID as a string.
    /// </summary>
    public const int GuidStringLength = 36;

    /// <summary>
    /// Represents maximum/unlimited length for text columns.
    /// Value of -1 is used as a semantic marker for unlimited/MAX types across all providers.
    /// For backward compatibility, both -1 and int.MaxValue are accepted when creating columns.
    /// When reading columns, all providers normalize to -1 for consistency.
    /// </summary>
    public const int MaxLength = -1;
}