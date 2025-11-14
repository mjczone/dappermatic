// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Models;

/// <summary>
/// Provides common provider-specific check expressions for database columns.
/// These functions generate appropriate check constraint expressions based on the database provider type.
/// </summary>
public static class CommonProviderCheckExpressions
{
    /// <summary>
    /// Creates a check expression for string length greater than specified value.
    /// SQL Server uses LEN(), others use LENGTH().
    /// </summary>
    /// <param name="columnName">The column name to check.</param>
    /// <param name="length">The minimum length (exclusive). Default is 0.</param>
    /// <returns>A function that generates provider-specific check expressions.</returns>
    public static Func<DbProviderType, string> LengthGreaterThanCheck(string columnName, int length = 0) =>
        provider =>
            provider switch
            {
                DbProviderType.SqlServer => $"LEN([{columnName}]) > {length}",
                DbProviderType.PostgreSql => $"LENGTH({columnName.ToLowerInvariant()}) > {length}",
                _ => $"LENGTH({columnName}) > {length}",
            };

    /// <summary>
    /// Creates a check expression for string length greater than or equal to specified value.
    /// SQL Server uses LEN(), others use LENGTH().
    /// </summary>
    /// <param name="columnName">The column name to check.</param>
    /// <param name="length">The minimum length (inclusive). Default is 0.</param>
    /// <returns>A function that generates provider-specific check expressions.</returns>
    public static Func<DbProviderType, string> LengthGreaterThanOrEqualCheck(string columnName, int length = 0) =>
        provider =>
            provider switch
            {
                DbProviderType.SqlServer => $"LEN([{columnName}]) >= {length}",
                DbProviderType.PostgreSql => $"LENGTH({columnName.ToLowerInvariant()}) >= {length}",
                _ => $"LENGTH({columnName}) >= {length}",
            };

    /// <summary>
    /// Creates a check expression for string length less than specified value.
    /// SQL Server uses LEN(), others use LENGTH().
    /// </summary>
    /// <param name="columnName">The column name to check.</param>
    /// <param name="length">The maximum length (exclusive).</param>
    /// <returns>A function that generates provider-specific check expressions.</returns>
    public static Func<DbProviderType, string> LengthLessThanCheck(string columnName, int length) =>
        provider =>
            provider switch
            {
                DbProviderType.SqlServer => $"LEN([{columnName}]) < {length}",
                DbProviderType.PostgreSql => $"LENGTH({columnName.ToLowerInvariant()}) < {length}",
                _ => $"LENGTH({columnName}) < {length}",
            };

    /// <summary>
    /// Creates a check expression for string length less than or equal to specified value.
    /// SQL Server uses LEN(), others use LENGTH().
    /// </summary>
    /// <param name="columnName">The column name to check.</param>
    /// <param name="length">The maximum length (inclusive).</param>
    /// <returns>A function that generates provider-specific check expressions.</returns>
    public static Func<DbProviderType, string> LengthLessThanOrEqualCheck(string columnName, int length) =>
        provider =>
            provider switch
            {
                DbProviderType.SqlServer => $"LEN([{columnName}]) <= {length}",
                DbProviderType.PostgreSql => $"LENGTH({columnName.ToLowerInvariant()}) <= {length}",
                _ => $"LENGTH({columnName}) <= {length}",
            };
}
