// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Providers;

/// <summary>
/// Provides default values used across all database providers for type mapping operations.
/// </summary>
/// <remarks>
/// These properties reference <see cref="DapperMaticSettings"/> for configurable defaults.
/// To change default behavior globally, modify <see cref="DapperMaticSettings"/> at application startup.
/// </remarks>
public static class TypeMappingDefaults
{
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

    /// <summary>
    /// Gets the default length for string/varchar columns when no length is specified.
    /// </summary>
    /// <remarks>
    /// References <see cref="DapperMaticSettings.DefaultStringLength"/>.
    /// Default is -1 (unlimited), matching Entity Framework Core's behavior.
    /// For better performance and indexing, set to 255 or another positive value.
    /// </remarks>
    public static int DefaultStringLength => DapperMaticSettings.DefaultStringLength;

    /// <summary>
    /// Gets the default length for binary/varbinary columns when no length is specified.
    /// </summary>
    /// <remarks>
    /// References <see cref="DapperMaticSettings.DefaultBinaryLength"/>.
    /// Default is -1 (unlimited), matching Entity Framework Core's behavior.
    /// For better performance and indexing, set to 255 or another positive value.
    /// </remarks>
    public static int DefaultBinaryLength => DapperMaticSettings.DefaultBinaryLength;

    /// <summary>
    /// Gets the default precision for decimal/numeric columns.
    /// </summary>
    /// <remarks>
    /// References <see cref="DapperMaticSettings.DefaultDecimalPrecision"/>.
    /// Default is 16.
    /// </remarks>
    public static int DefaultDecimalPrecision => DapperMaticSettings.DefaultDecimalPrecision;

    /// <summary>
    /// Gets the default scale for decimal/numeric columns.
    /// </summary>
    /// <remarks>
    /// References <see cref="DapperMaticSettings.DefaultDecimalScale"/>.
    /// Default is 4.
    /// </remarks>
    public static int DefaultDecimalScale => DapperMaticSettings.DefaultDecimalScale;
}
