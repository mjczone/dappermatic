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
/// DapperMatic DML type mapping tests for MySql 9.0 (9.4).
/// </summary>
public class MySql_90_DapperMaticDmlTests(MySql_94_DatabaseFixture fixture, ITestOutputHelper output)
    : MySqlDapperMaticDmlTests<MySql_94_DatabaseFixture>(fixture, output) { }

/// <summary>
/// DapperMatic DML type mapping tests for MySql 8.4.
/// </summary>
public class MySql_84_DapperMaticDmlTests(MySql_84_DatabaseFixture fixture, ITestOutputHelper output)
    : MySqlDapperMaticDmlTests<MySql_84_DatabaseFixture>(fixture, output) { }

/// <summary>
/// DapperMatic DML type mapping tests for MySql 5.7.
/// </summary>
public class MySql_57_DapperMaticDmlTests(MySql_57_DatabaseFixture fixture, ITestOutputHelper output)
    : MySqlDapperMaticDmlTests<MySql_57_DatabaseFixture>(fixture, output) { }

/// <summary>
/// Abstract base class for MySQL DML type mapping tests.
/// </summary>
/// <typeparam name="TDatabaseFixture">The database fixture type.</typeparam>
public abstract class MySqlDapperMaticDmlTests<TDatabaseFixture>(TDatabaseFixture fixture, ITestOutputHelper output)
    : DapperMaticDmlTypeMappingTests(output),
        IClassFixture<TDatabaseFixture>,
        IDisposable
    where TDatabaseFixture : MySqlDatabaseFixture
{
    static MySqlDapperMaticDmlTests()
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
}
