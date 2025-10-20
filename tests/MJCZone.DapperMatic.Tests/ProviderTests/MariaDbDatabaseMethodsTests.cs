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
/// Testing MariaDb 12.0
/// </summary>
public class MariaDb_12_0_DatabaseMethodsTests(MariaDb_12_0_DatabaseFixture fixture, ITestOutputHelper output)
    : MariaDbDatabaseMethodsTests<MariaDb_12_0_DatabaseFixture>(fixture, output) { }

/// <summary>
/// Testing MariaDb 11.8
/// </summary>
public class MariaDb_11_8_DatabaseMethodsTests(MariaDb_11_8_DatabaseFixture fixture, ITestOutputHelper output)
    : MariaDbDatabaseMethodsTests<MariaDb_11_8_DatabaseFixture>(fixture, output) { }

/// <summary>
/// Testing MariaDb 11.4
/// </summary>
public class MariaDb_11_4_DatabaseMethodsTests(MariaDb_11_4_DatabaseFixture fixture, ITestOutputHelper output)
    : MariaDbDatabaseMethodsTests<MariaDb_11_4_DatabaseFixture>(fixture, output) { }

/// <summary>
/// Testing MariaDb 10.11
/// </summary>
public class MariaDb_10_11_DatabaseMethodsTests(MariaDb_10_11_DatabaseFixture fixture, ITestOutputHelper output)
    : MariaDbDatabaseMethodsTests<MariaDb_10_11_DatabaseFixture>(fixture, output) { }

/// <summary>
/// Abstract class for MySql database tests
/// </summary>
/// <typeparam name="TDatabaseFixture"></typeparam>
public abstract class MariaDbDatabaseMethodsTests<TDatabaseFixture>(TDatabaseFixture fixture, ITestOutputHelper output)
    : DatabaseMethodsTests(output),
        IClassFixture<TDatabaseFixture>,
        IDisposable
    where TDatabaseFixture : MariaDbDatabaseFixture
{
    static MariaDbDatabaseMethodsTests()
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
        var db = new DbQueryLogging.LoggedDbConnection(
            new MySqlConnection(connectionString),
            new Logging.TestLogger(Output, nameof(MySqlConnection))
        );
        await db.OpenAsync();
        return db;
    }

    public override bool IgnoreSqlType(string sqlType)
    {
        return fixture.IgnoreSqlType(sqlType);
    }
}

public class ProfiledMariaDbMethodsFactory : Providers.MySql.MySqlMethodsFactory
{
    public override bool SupportsConnectionCustom(IDbConnection db) =>
        db is DbQueryLogging.LoggedDbConnection loggedDb && loggedDb.Inner is MySqlConnection;
}
