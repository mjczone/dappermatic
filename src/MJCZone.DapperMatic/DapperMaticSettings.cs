// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic;

/// <summary>
/// Global settings for DapperMatic behavior across all database providers.
/// These settings can be modified at application startup to customize default behavior.
/// </summary>
public static class DapperMaticSettings
{
    private static int _defaultStringLength = -1;
    private static int _defaultBinaryLength = -1;
    private static int _defaultDecimalPrecision = 16;
    private static int _defaultDecimalScale = 4;

    /// <summary>
    /// Gets or sets the default length for string/varchar columns when no length is specified.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Default value is -1 (unlimited), matching Entity Framework Core's behavior.
    /// This translates to VARCHAR(MAX) in SQL Server, TEXT in PostgreSQL/SQLite, and LONGTEXT in MySQL.
    /// </para>
    /// <para>
    /// Set to a positive value (e.g., 255) for better performance and indexing compatibility:
    /// - Safe for indexing in all major databases (within 767-900 byte limits)
    /// - Better performance for temporary tables and sorting operations
    /// - Reduces memory overhead for in-memory operations
    /// </para>
    /// <para>
    /// Note: Unlimited columns cannot be used in indexes and may have performance implications.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// // Use 255 for better performance and indexing (recommended for production)
    /// DapperMaticSettings.DefaultStringLength = 255;
    ///
    /// // Use unlimited (default, matches EF Core)
    /// DapperMaticSettings.DefaultStringLength = -1;
    ///
    /// // Use a custom default
    /// DapperMaticSettings.DefaultStringLength = 500;
    /// </code>
    /// </para>
    /// </remarks>
    public static int DefaultStringLength
    {
        get => _defaultStringLength;
        set =>
            _defaultStringLength =
                value > 0 || value == -1
                    ? value
                    : throw new ArgumentException(
                        "DefaultStringLength must be positive or -1 for unlimited",
                        nameof(value)
                    );
    }

    /// <summary>
    /// Gets or sets the default length for binary/varbinary columns when no length is specified.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Default value is -1 (unlimited), matching Entity Framework Core's behavior.
    /// This translates to VARBINARY(MAX) in SQL Server, BYTEA in PostgreSQL, BLOB in SQLite/MySQL.
    /// </para>
    /// <para>
    /// Same performance considerations as DefaultStringLength apply.
    /// Set to a positive value (e.g., 255) for better performance and indexing compatibility.
    /// </para>
    /// </remarks>
    public static int DefaultBinaryLength
    {
        get => _defaultBinaryLength;
        set =>
            _defaultBinaryLength =
                value > 0 || value == -1
                    ? value
                    : throw new ArgumentException(
                        "DefaultBinaryLength must be positive or -1 for unlimited",
                        nameof(value)
                    );
    }

    /// <summary>
    /// Gets or sets the default precision for decimal/numeric columns when not specified.
    /// </summary>
    /// <remarks>
    /// Default value is 16. Valid range: 1-38 (depends on database provider).
    /// </remarks>
    public static int DefaultDecimalPrecision
    {
        get => _defaultDecimalPrecision;
        set =>
            _defaultDecimalPrecision = value is > 0 and <= 38
                ? value
                : throw new ArgumentException("DefaultDecimalPrecision must be between 1 and 38", nameof(value));
    }

    /// <summary>
    /// Gets or sets the default scale for decimal/numeric columns when not specified.
    /// </summary>
    /// <remarks>
    /// Default value is 4. Must be less than or equal to DefaultDecimalPrecision.
    /// </remarks>
    public static int DefaultDecimalScale
    {
        get => _defaultDecimalScale;
        set =>
            _defaultDecimalScale =
                value >= 0
                    ? value
                    : throw new ArgumentException("DefaultDecimalScale must be non-negative", nameof(value));
    }

    /// <summary>
    /// Resets all settings to their default values.
    /// </summary>
    /// <remarks>
    /// Resets to EF Core-compatible defaults:
    /// - String length: -1 (unlimited).
    /// - Binary length: -1 (unlimited).
    /// - Decimal precision: 16.
    /// - Decimal scale: 4.
    /// </remarks>
    public static void ResetToDefaults()
    {
        _defaultStringLength = -1;
        _defaultBinaryLength = -1;
        _defaultDecimalPrecision = 16;
        _defaultDecimalScale = 4;
    }
}
