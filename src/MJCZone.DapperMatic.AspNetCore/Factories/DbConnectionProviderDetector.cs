// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data.Common;

namespace MJCZone.DapperMatic.AspNetCore.Factories;

/// <summary>
/// Utility class for detecting and creating database connections based on connection string characteristics.
/// </summary>
public static class DbConnectionProviderDetector
{
    /// <summary>
    /// Detects the appropriate SQL Server provider (Microsoft.Data.SqlClient vs System.Data.SqlClient)
    /// using explicit Provider=..., heuristics, and finally connection string builder parsing.
    /// </summary>
    /// <param name="connectionString">The SQL Server connection string.</param>
    /// <returns>An open DbConnection instance.</returns>
    public static DbConnection DetectSqlServerProvider(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException(
                "Connection string cannot be null or empty.",
                nameof(connectionString)
            );
        }

        // --- Explicit override ---connectionString.Contains("Provider=MySqlConnector", StringComparison.OrdinalIgnoreCase)
        if (
            connectionString.Contains(
                "Microsoft.Data.SqlClient",
                StringComparison.OrdinalIgnoreCase
            )
            || connectionString.Contains("MsDataSqlClient", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("MicrosoftSqlClient", StringComparison.OrdinalIgnoreCase)
        )
        {
            var cleaned = RemoveProviderKeyword(connectionString);
            return new Microsoft.Data.SqlClient.SqlConnection(cleaned);
        }

        if (
            connectionString.Contains("System.Data.SqlClient", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("SystemDataSqlClient", StringComparison.OrdinalIgnoreCase)
        )
        {
            var cleaned = RemoveProviderKeyword(connectionString);
#pragma warning disable CS0618 // Type or member is obsolete
            return new System.Data.SqlClient.SqlConnection(cleaned);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        // --- Heuristics ---

        // Strong hints for Microsoft.Data.SqlClient (modern AAD & enclaves)
        var microsoftHints = new[]
        {
            "authentication=active directory", // any AAD mode
            "active directory integrated",
            "active directory interactive",
            "active directory password",
            "active directory managed identity",
            "active directory service principal",
            "active directory device code",
            "enclave attestation url",
            "attestation protocol",
        };

        if (
            microsoftHints.Any(h =>
                connectionString.Contains(h, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            return new Microsoft.Data.SqlClient.SqlConnection(connectionString);
        }

        // Hints for System.Data.SqlClient (legacy features)
        var systemHints = new[]
        {
            "user instance=true", // SQL Express user instance (deprecated)
            "context connection=true", // in-proc SQL CLR scenarios
            "asynchronous processing=true", // old APM flag
        };

        if (systemHints.Any(h => connectionString.Contains(h, StringComparison.OrdinalIgnoreCase)))
        {
#pragma warning disable CS0618 // Type or member is obsolete
            return new System.Data.SqlClient.SqlConnection(connectionString);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        // --- Ambiguous: try builders (parsing only; no I/O). ---
        // Most common default: prefer Microsoft.Data.SqlClient when both succeed.
        try
        {
            var csb = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
            return new Microsoft.Data.SqlClient.SqlConnection(csb.ConnectionString);
        }
        catch
        {
            // ignore
        }
        try
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var csb = new System.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
            return new System.Data.SqlClient.SqlConnection(csb.ConnectionString);
#pragma warning restore CS0618 // Type or member is obsolete
        }
        catch
        {
            // ignore
        }

        throw new ArgumentException(
            "Unsupported or ambiguous SQL Server connection string.",
            nameof(connectionString)
        );
    }

    /// <summary>
    /// Detects the appropriate MySQL provider (MySqlConnector vs MySql.Data)
    /// based on explicit Provider=..., lightweight heuristics, and
    /// finally by attempting to parse with each provider's connection-string builder.
    /// </summary>
    /// <param name="connectionString">The MySQL connection string.</param>
    /// <returns>An open DbConnection instance.</returns>
    public static DbConnection DetectMySqlProvider(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException(
                "Connection string cannot be null or empty.",
                nameof(connectionString)
            );
        }

        // --- Explicit override ---
        if (
            connectionString.Contains("Provider=MySqlConnector", StringComparison.OrdinalIgnoreCase)
        )
        {
            var cleaned = RemoveProviderKeyword(connectionString);
            return new MySqlConnector.MySqlConnection(cleaned);
        }
        if (
            connectionString.Contains("Provider=MySql.Data", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("Provider=MySqlData", StringComparison.OrdinalIgnoreCase)
        )
        {
            var cleaned = RemoveProviderKeyword(connectionString);
            return new MySql.Data.MySqlClient.MySqlConnection(cleaned);
        }

        // --- Heuristics (favor MySql.Data for certain legacy/Connector.NET-only keys) ---
        // These keywords are historically associated with Oracle's MySql.Data:
        // If you spot them, it's a strong hint to pick MySql.Data quickly.
        var mysqlDataHeuristics = new[]
        {
            "treattinyasboolean", // Connector/NET boolean handling for TINYINT(1)
            "oldguids", // Legacy GUID behavior
            "useusageadvisor", // Usage advisor (Connector/NET feature)
            "functionsreturnstring",
            "interactivesession",
            "respectbinaryflags",
            "useprocedurebodies",
        };

        if (
            mysqlDataHeuristics.Any(h =>
                connectionString.Contains(h, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            return new MySql.Data.MySqlClient.MySqlConnection(connectionString);
        }

        // --- Ambiguous: try both connection string builders (no I/O) ---
        // Prefer MySqlConnector first (modern, async-first), then MySql.Data.
        try
        {
            var csb = new MySqlConnector.MySqlConnectionStringBuilder(connectionString);
            return new MySqlConnector.MySqlConnection(csb.ConnectionString);
        }
        catch
        {
            // ignore
        }

        try
        {
            var csb = new MySql.Data.MySqlClient.MySqlConnectionStringBuilder(connectionString);
            return new MySql.Data.MySqlClient.MySqlConnection(csb.ConnectionString);
        }
        catch
        {
            // ignore
        }

        throw new ArgumentException(
            "Unsupported or ambiguous MySQL connection string.",
            nameof(connectionString)
        );
    }

    /// <summary>
    /// Removes the "Provider=..." keyword from the connection string.
    /// </summary>
    /// <param name="connectionString">The original connection string.</param>
    /// <returns>The cleaned connection string without the provider keyword.</returns>
    public static DbConnection DetectSqliteProvider(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException(
                "Connection string cannot be null or empty.",
                nameof(connectionString)
            );
        }

        // Explicit override
        if (
            connectionString.Contains(
                "Provider=MicrosoftDataSqlite",
                StringComparison.OrdinalIgnoreCase
            )
            || connectionString.Contains(
                "Provider=Microsoft.Data.Sqlite",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            var cleaned = RemoveProviderKeyword(connectionString);
            return new Microsoft.Data.Sqlite.SqliteConnection(cleaned);
        }

        if (
            connectionString.Contains(
                "Provider=SystemDataSqlite",
                StringComparison.OrdinalIgnoreCase
            )
            || connectionString.Contains(
                "Provider=System.Data.SQLite",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            var cleaned = RemoveProviderKeyword(connectionString);
            return new System.Data.SQLite.SQLiteConnection(cleaned);
        }

        // Keywords unique to System.Data.SQLite
        var systemDataSQLiteHeuristics = new[]
        {
            "binaryguid",
            "busytimeout",
            "cache size",
            "datetimeformat",
            "datetimekind",
            "default isolationlevel",
            "defaultdbtype",
            "defaultmaximumsleeptime",
            "failifmissing",
            "flags",
            "fulluri",
            "journal mode",
            "legacy format",
            "max page count",
            "nodefaultflags",
            "nosharedflags",
            "page size",
            "prepareretries",
            "progressops",
            "read only",
            "setdefaults",
            "stepretries",
            "synchronous",
            "uri",
            "useutf16encoding",
            "version",
            "vfsname",
            "waittimeout",
            "zipvfsversion",
        };
        if (
            systemDataSQLiteHeuristics.Any(h =>
                connectionString.Contains(h, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            // More likely System.Data.SQLite
            return new System.Data.SQLite.SQLiteConnection(connectionString);
        }

        // Ambiguous: try both connection string builders
        try
        {
            var msCsb = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
            return new Microsoft.Data.Sqlite.SqliteConnection(msCsb.ToString());
        }
        catch
        {
            // Ignore exceptions
        }
        try
        {
            var sysCsb = new System.Data.SQLite.SQLiteConnectionStringBuilder(connectionString);
            return new System.Data.SQLite.SQLiteConnection(sysCsb.ToString());
        }
        catch
        {
            // Ignore exceptions
        }

        throw new ArgumentException(
            "Unsupported or ambiguous SQLite connection string.",
            nameof(connectionString)
        );
    }

    /// <summary>
    /// Extracts the value of the "Provider=..." keyword from the connection string, if present.
    /// </summary>
    /// <param name="connectionString">The connection string to inspect.</param>
    /// <returns>The provider value if found; otherwise, an empty string.</returns>
    internal static string ExtractProviderKeyword(string connectionString)
    {
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part.TrimStart().StartsWith("Provider=", StringComparison.OrdinalIgnoreCase))
            {
                return part[(part.IndexOf('=', StringComparison.OrdinalIgnoreCase) + 1)..].Trim();
            }
        }
        return string.Empty;
    }

    /// <summary>
    /// Removes the "Provider=..." keyword from the connection string.
    /// </summary>
    private static string RemoveProviderKeyword(string connectionString)
    {
        // crude cleanup: remove "Provider=..." if user explicitly sets it
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(
            ";",
            Array.FindAll(
                parts,
                p => !p.TrimStart().StartsWith("Provider=", StringComparison.OrdinalIgnoreCase)
            )
        );
    }
}
