// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Data;
using MJCZone.DapperMatic.Interfaces;

namespace MJCZone.DapperMatic.Providers;

/// <summary>
/// Provides methods for registering and retrieving database method factories.
/// </summary>
public static class DatabaseMethodsProvider
{
    private static readonly ConcurrentDictionary<DbProviderType, IDatabaseMethodsFactory> NativeFactories = new()
    {
        [DbProviderType.Sqlite] = new Sqlite.SqliteMethodsFactory(),
        [DbProviderType.SqlServer] = new SqlServer.SqlServerMethodsFactory(),
        [DbProviderType.MySql] = new MySql.MySqlMethodsFactory(),
        [DbProviderType.PostgreSql] = new PostgreSql.PostgreSqlMethodsFactory(),
    };

    private static readonly ConcurrentDictionary<string, IDatabaseMethodsFactory> CustomFactories = new();

    /// <summary>
    /// Registers a custom database methods factory.
    /// </summary>
    /// <param name="name">The name of the custom factory.</param>
    /// <param name="factory">The custom factory to register.</param>
    public static void RegisterFactory(string name, IDatabaseMethodsFactory factory)
    {
        CustomFactories.TryAdd(name.ToLowerInvariant(), factory);
    }

    /// <summary>
    /// Registers a database methods factory for a specific provider type.
    /// </summary>
    /// <param name="providerType">The provider type.</param>
    /// <param name="factory">The factory to register.</param>
    public static void RegisterFactory(DbProviderType providerType, IDatabaseMethodsFactory factory)
    {
        if (providerType == DbProviderType.Other)
        {
            RegisterFactory(Guid.NewGuid().ToString(), factory);
            return;
        }

        NativeFactories.AddOrUpdate(providerType, factory, (_, _) => factory);
    }

    /// <summary>
    /// Gets the database methods for a given database connection.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <returns>The database methods.</returns>
    /// <exception cref="NotSupportedException">Thrown when no factory is found for the connection type.</exception>
    public static IDatabaseMethods GetMethods(IDbConnection db)
    {
        foreach (var factory in CustomFactories.Values)
        {
            if (factory.SupportsConnection(db))
            {
                return factory.GetMethods(db);
            }
        }

        foreach (var factory in NativeFactories.Values)
        {
            if (factory.SupportsConnection(db))
            {
                return factory.GetMethods(db);
            }
        }

        throw new NotSupportedException($"No factory found for connection type {db.GetType().FullName}");
    }
}
