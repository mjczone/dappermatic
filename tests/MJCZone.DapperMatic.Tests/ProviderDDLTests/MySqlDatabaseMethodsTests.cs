// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using MJCZone.DapperMatic.Tests.ProviderFixtures;
using MySql.Data.MySqlClient;
using Xunit.Abstractions;

namespace MJCZone.DapperMatic.Tests.ProviderDDLTests;

/// <summary>
/// Testing MySql 90
/// </summary>
public class MySql_90_DatabaseMethodsTests(MySql_94_DatabaseFixture fixture, ITestOutputHelper output)
    : MySqlDatabaseMethodsTests<MySql_94_DatabaseFixture>(fixture, output) { }

/// <summary>
/// Testing MySql 84
/// </summary>
public class MySql_84_DatabaseMethodsTests(MySql_84_DatabaseFixture fixture, ITestOutputHelper output)
    : MySqlDatabaseMethodsTests<MySql_84_DatabaseFixture>(fixture, output) { }

/// <summary>
/// Testing MySql 57
/// </summary>
public class MySql_57_DatabaseMethodsTests(MySql_57_DatabaseFixture fixture, ITestOutputHelper output)
    : MySqlDatabaseMethodsTests<MySql_57_DatabaseFixture>(fixture, output) { }

/// <summary>
/// Abstract class for MySql database tests
/// </summary>
/// <typeparam name="TDatabaseFixture"></typeparam>
public abstract class MySqlDatabaseMethodsTests<TDatabaseFixture>(TDatabaseFixture fixture, ITestOutputHelper output)
    : DatabaseMethodsTests(output),
        IClassFixture<TDatabaseFixture>,
        IDisposable
    where TDatabaseFixture : MySqlDatabaseFixture
{
    static MySqlDatabaseMethodsTests()
    {
        Providers.DatabaseMethodsProvider.RegisterFactory(
            nameof(ProfiledMySqlMethodsFactory),
            new ProfiledMySqlMethodsFactory()
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

    public override bool IgnoreSqlType(string sqlType)
    {
        return fixture.IgnoreSqlType(sqlType);
    }
}

public class ProfiledMySqlMethodsFactory : Providers.MySql.MySqlMethodsFactory
{
    public override bool SupportsConnectionCustom(IDbConnection db) =>
        db is Logging.DbLoggingConnection loggedDb && loggedDb.Inner is MySqlConnection;
}
