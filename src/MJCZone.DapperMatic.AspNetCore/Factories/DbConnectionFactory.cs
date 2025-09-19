// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Data.Common;
using Npgsql;

namespace MJCZone.DapperMatic.AspNetCore.Factories;

/// <summary>
/// Default implementation of IDbConnectionFactory that creates provider-specific database connections.
/// </summary>
public sealed class DbConnectionFactory : IDbConnectionFactory
{
    /// <summary>
    /// Creates a database connection for the specified provider and connection string.
    /// </summary>
    /// <param name="provider">The database provider (e.g., SqlServer, PostgreSQL, MySQL, SQLite).</param>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>A configured database connection.</returns>
    public IDbConnection CreateConnection(string provider, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        DbConnection? connection = null;
        if (provider.Contains("mysql", StringComparison.OrdinalIgnoreCase))
        {
            connection = DbConnectionProviderDetector.DetectMySqlProvider(connectionString);
        }
        else if (
            provider.Contains("pg", StringComparison.OrdinalIgnoreCase)
            || provider.Contains("postgres", StringComparison.OrdinalIgnoreCase)
        )
        {
            connection = new NpgsqlConnection(connectionString);
        }
        else if (provider.Contains("sqlite", StringComparison.OrdinalIgnoreCase))
        {
            connection = DbConnectionProviderDetector.DetectSqliteProvider(connectionString);
        }
        else if (
            provider.Contains("sqlserver", StringComparison.OrdinalIgnoreCase)
            || provider.Contains("mssql", StringComparison.OrdinalIgnoreCase)
        )
        {
            connection = DbConnectionProviderDetector.DetectSqlServerProvider(connectionString);
        }

        return connection
            ?? throw new ArgumentException(
                $"Unsupported database provider: {provider}",
                nameof(provider)
            );
    }
}
