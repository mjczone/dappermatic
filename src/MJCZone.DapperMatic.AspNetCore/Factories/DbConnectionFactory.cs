// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Data.Common;
using Microsoft.Extensions.Options;

namespace MJCZone.DapperMatic.AspNetCore.Factories;

/// <summary>
/// Creates connections with the drivers the application registered with
/// <see cref="DapperMaticConfigurationBuilder.AddProvider(DbProviderFactory)"/>: DapperMatic bundles none.
/// </summary>
public sealed class DbConnectionFactory : IDbConnectionFactory
{
    private readonly IReadOnlyDictionary<DbProviderType, DbProviderFactory> _factories;

    /// <summary>
    /// Initializes a new instance of the <see cref="DbConnectionFactory"/> class.
    /// </summary>
    /// <param name="options">The DapperMatic options, which hold the registered drivers.</param>
    public DbConnectionFactory(IOptions<DapperMaticOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _factories = options.Value.ProviderFactories;
    }

    /// <summary>
    /// Creates a database connection for the specified provider and connection string.
    /// </summary>
    /// <param name="provider">The database provider: SqlServer, PostgreSql, MySql (or MariaDb) or Sqlite.</param>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>A connection, not yet open.</returns>
    /// <exception cref="ArgumentException">The provider is not one DapperMatic supports.</exception>
    /// <exception cref="InvalidOperationException">No driver is registered for the provider.</exception>
    public IDbConnection CreateConnection(string provider, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var providerType = DapperMaticProviders.Parse(provider);
        if (!_factories.TryGetValue(providerType, out var factory))
        {
            throw new InvalidOperationException(DapperMaticProviders.NotRegisteredMessage(providerType));
        }

        var connection = factory.CreateConnection()
            ?? throw new InvalidOperationException($"The {providerType} driver's factory created no connection.");
        connection.ConnectionString = connectionString;
        return connection;
    }
}
