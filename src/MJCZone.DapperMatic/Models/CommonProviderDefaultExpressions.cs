// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Models;

/// <summary>
/// Provides common provider-specific default expressions for database columns.
/// These functions generate appropriate default values based on the database provider type.
/// </summary>
public static class CommonProviderDefaultExpressions
{
    /// <summary>
    /// Gets a function that returns the appropriate boolean true value for each provider.
    /// PostgreSQL uses "true", while other providers use "1".
    /// </summary>
    public static Func<DbProviderType, string> TrueValue =>
        provider =>
            provider switch
            {
                DbProviderType.PostgreSql => "true",
                _ => "1",
            };

    /// <summary>
    /// Gets a function that returns the appropriate boolean false value for each provider.
    /// PostgreSQL uses "false", while other providers use "0".
    /// </summary>
    public static Func<DbProviderType, string> FalseValue =>
        provider =>
            provider switch
            {
                DbProviderType.PostgreSql => "false",
                _ => "0",
            };

    /// <summary>
    /// Gets a function that returns the appropriate current timestamp expression for each provider.
    /// </summary>
    public static Func<DbProviderType, string> CurrentTimestamp =>
        provider =>
            provider switch
            {
                DbProviderType.SqlServer => "GETDATE()",
                DbProviderType.MySql => "NOW()",
                DbProviderType.PostgreSql => "NOW()",
                DbProviderType.Sqlite => "datetime('now')",
                _ => "CURRENT_TIMESTAMP",
            };

    /// <summary>
    /// Gets a function that returns the appropriate current UTC timestamp expression for each provider.
    /// </summary>
    public static Func<DbProviderType, string> CurrentUtcTimestamp =>
        provider =>
            provider switch
            {
                DbProviderType.SqlServer => "GETUTCDATE()",
                DbProviderType.MySql => "UTC_TIMESTAMP()",
                DbProviderType.PostgreSql => "NOW() AT TIME ZONE 'UTC'",
                DbProviderType.Sqlite => "datetime('now', 'utc')",
                _ => "CURRENT_TIMESTAMP",
            };

    /// <summary>
    /// Gets a function that returns the appropriate new GUID generation expression for each provider.
    /// </summary>
    public static Func<DbProviderType, string> NewGuid =>
        provider =>
            provider switch
            {
                DbProviderType.SqlServer => "NEWID()",
                DbProviderType.PostgreSql => "gen_random_uuid()",
                DbProviderType.MySql => "UUID()",
                DbProviderType.Sqlite =>
                    "lower(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-' || '4' || substr(hex(randomblob(2)),2) || '-' || substr('89ab',abs(random()) % 4 + 1, 1) || substr(hex(randomblob(2)),2) || '-' || hex(randomblob(6)))",
                _ => throw new NotSupportedException(
                    $"GUID generation not supported for {provider}"
                ),
            };

    /// <summary>
    /// Gets a function that returns zero value appropriate for numeric columns in each provider.
    /// </summary>
    public static Func<DbProviderType, string> Zero => _ => "0";

    /// <summary>
    /// Gets a function that returns empty string value appropriate for text columns in each provider.
    /// </summary>
    public static Func<DbProviderType, string> EmptyString => _ => "''";
}