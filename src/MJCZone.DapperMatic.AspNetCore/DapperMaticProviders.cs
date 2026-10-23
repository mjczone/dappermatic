// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data.Common;

namespace MJCZone.DapperMatic.AspNetCore;

/// <summary>
/// Names datasource providers and the drivers that serve them.
/// </summary>
public static class DapperMaticProviders
{
    /// <summary>
    /// Reads a datasource's provider name: SqlServer (or MsSql), PostgreSql (or Postgres, Pg), MySql (or MariaDb),
    /// Sqlite, in any case.
    /// </summary>
    /// <param name="provider">The provider name.</param>
    /// <returns>The provider type.</returns>
    /// <exception cref="ArgumentException">The name is not one of them.</exception>
    public static DbProviderType Parse(string provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        var name = provider.Trim().ToLowerInvariant();
        return name switch
        {
            "sqlserver" or "mssql" => DbProviderType.SqlServer,
            "postgresql" or "postgres" or "pg" or "npgsql" => DbProviderType.PostgreSql,
            "mysql" or "mariadb" => DbProviderType.MySql,
            "sqlite" => DbProviderType.Sqlite,
            _ => throw new ArgumentException(
                $"'{provider}' is not a provider DapperMatic supports: use SqlServer, PostgreSql, MySql or Sqlite.",
                nameof(provider)
            ),
        };
    }

    /// <summary>
    /// Tells which provider a driver's factory serves, from the driver's own type.
    /// </summary>
    /// <param name="factory">The driver's factory, such as <c>NpgsqlFactory.Instance</c>.</param>
    /// <returns>The provider type, or null for a driver DapperMatic does not recognize.</returns>
    public static DbProviderType? Of(DbProviderFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        return factory.GetType().FullName switch
        {
            "Npgsql.NpgsqlFactory" => DbProviderType.PostgreSql,
            "Microsoft.Data.SqlClient.SqlClientFactory" or "System.Data.SqlClient.SqlClientFactory" => DbProviderType.SqlServer,
            "MySqlConnector.MySqlConnectorFactory" or "MySql.Data.MySqlClient.MySqlClientFactory" => DbProviderType.MySql,
            "Microsoft.Data.Sqlite.SqliteFactory" or "System.Data.SQLite.SQLiteFactory" => DbProviderType.Sqlite,
            _ => null,
        };
    }

    /// <summary>
    /// Reads a datasource's provider name, as <see cref="Parse(string)"/> does, without throwing.
    /// </summary>
    /// <param name="provider">The provider name.</param>
    /// <param name="type">The provider type, when the name is one.</param>
    /// <returns>True if the name is one of them.</returns>
    internal static bool TryParse(string provider, out DbProviderType type)
    {
        try
        {
            type = Parse(provider);
            return true;
        }
        catch (ArgumentException)
        {
            type = default;
            return false;
        }
    }

    /// <summary>
    /// The schema a provider puts a name with none in: dbo, public; null for providers without schemas.
    /// </summary>
    /// <param name="provider">The provider type.</param>
    /// <returns>The schema name, or null.</returns>
    internal static string? DefaultSchemaName(DbProviderType provider) =>
        provider switch
        {
            DbProviderType.SqlServer => MJCZone.DapperMatic.Providers.SqlServer.SqlServerMethods.DefaultSchemaName,
            DbProviderType.PostgreSql => MJCZone.DapperMatic.Providers.PostgreSql.PostgreSqlMethods.DefaultSchemaName,
            _ => null,
        };

    /// <summary>
    /// Says how to register a driver for a provider that has none.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <returns>The message.</returns>
    internal static string NotRegisteredMessage(DbProviderType provider) =>
        $"No driver is registered for {provider} datasources. Register one in AddDapperMatic: "
        + provider switch
        {
            DbProviderType.PostgreSql => "dm.AddProvider(NpgsqlFactory.Instance), from the Npgsql package.",
            DbProviderType.SqlServer => "dm.AddProvider(SqlClientFactory.Instance), from the Microsoft.Data.SqlClient package.",
            DbProviderType.MySql => "dm.AddProvider(MySqlConnectorFactory.Instance), from the MySqlConnector package.",
            DbProviderType.Sqlite => "dm.AddProvider(SqliteFactory.Instance), from the Microsoft.Data.Sqlite package.",
            _ => "dm.AddProvider(...) with the driver's DbProviderFactory.",
        };
}
