// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using MJCZone.DapperMatic.Tests.ProviderFixtures;
using MySql.Data.MySqlClient;
using Xunit.Abstractions;

namespace MJCZone.DapperMatic.Tests.ProviderTests;

/// <summary>
/// DapperMatic DML type mapping tests for MariaDb 12.0.
/// </summary>
public class MariaDb_12_0_DapperMaticDmlTests(MariaDb_12_0_DatabaseFixture fixture, ITestOutputHelper output)
    : MariaDbDapperMaticDmlTests<MariaDb_12_0_DatabaseFixture>(fixture, output) { }

/// <summary>
/// DapperMatic DML type mapping tests for MariaDb 11.8.
/// </summary>
public class MariaDb_11_8_DapperMaticDmlTests(MariaDb_11_8_DatabaseFixture fixture, ITestOutputHelper output)
    : MariaDbDapperMaticDmlTests<MariaDb_11_8_DatabaseFixture>(fixture, output) { }

/// <summary>
/// DapperMatic DML type mapping tests for MariaDb 11.4.
/// </summary>
public class MariaDb_11_4_DapperMaticDmlTests(MariaDb_11_4_DatabaseFixture fixture, ITestOutputHelper output)
    : MariaDbDapperMaticDmlTests<MariaDb_11_4_DatabaseFixture>(fixture, output) { }

/// <summary>
/// DapperMatic DML type mapping tests for MariaDb 10.11.
/// </summary>
public class MariaDb_10_11_DapperMaticDmlTests(MariaDb_10_11_DatabaseFixture fixture, ITestOutputHelper output)
    : MariaDbDapperMaticDmlTests<MariaDb_10_11_DatabaseFixture>(fixture, output) { }

/// <summary>
/// Abstract base class for MariaDB DML type mapping tests.
/// </summary>
/// <typeparam name="TDatabaseFixture">The database fixture type.</typeparam>
public abstract class MariaDbDapperMaticDmlTests<TDatabaseFixture>(TDatabaseFixture fixture, ITestOutputHelper output)
    : DapperMaticDmlTypeMappingTests(output),
        IClassFixture<TDatabaseFixture>,
        IDisposable
    where TDatabaseFixture : MariaDbDatabaseFixture
{
    static MariaDbDapperMaticDmlTests()
    {
        Providers.DatabaseMethodsProvider.RegisterFactory(
            nameof(ProfiledMariaDbMethodsFactory),
            new ProfiledMariaDbMethodsFactory()
        );
    }

    public override async Task<IDbConnection> OpenConnectionAsync()
    {
        var connectionString = fixture.ConnectionString;
        // Disable SSL for local testing and CI environments
        if (!connectionString.Contains("SSL Mode", StringComparison.OrdinalIgnoreCase))
        {
            connectionString += ";SSL Mode=None";
        }
        var db = new Logging.DbLoggingConnection(
            new MySqlConnection(connectionString),
            new Logging.TestLogger(Output, nameof(MySqlConnection))
        );
        await db.OpenAsync();
        return db;
    }
}
