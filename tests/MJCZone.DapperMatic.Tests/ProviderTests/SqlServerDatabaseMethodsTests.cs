// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Data.SqlClient;
using MJCZone.DapperMatic.Tests.ProviderFixtures;
using Xunit.Abstractions;

namespace MJCZone.DapperMatic.Tests.ProviderTests;

/// <summary>
/// Testing SqlServer 2025 Linux (CU image)
/// </summary>
// public class SqlServer_2025_Ubuntu_DatabaseMethodsTests(
//     SqlServer_2025_Ubuntu_DatabaseFixture fixture,
//     ITestOutputHelper output
// ) : SqlServerDatabaseMethodsests<SqlServer_2025_Ubuntu_DatabaseFixture>(fixture, output) { }

/// <summary>
/// Testing SqlServer 2022 Linux (CU image)
/// </summary>
public class SqlServer_2022_DatabaseMethodsTests(
    SqlServer_2022_DatabaseFixture fixture,
    ITestOutputHelper output
) : SqlServerDatabaseMethodsests<SqlServer_2022_DatabaseFixture>(fixture, output) { }

/// <summary>
/// Testing SqlServer 2019
/// </summary>
public class SqlServer_2019_DatabaseMethodsTests(
    SqlServer_2019_DatabaseFixture fixture,
    ITestOutputHelper output
) : SqlServerDatabaseMethodsests<SqlServer_2019_DatabaseFixture>(fixture, output) { }

/// <summary>
/// Testing SqlServer 2017
/// </summary>
public class SqlServer_2017_DatabaseMethodsTests(
    SqlServer_2017_DatabaseFixture fixture,
    ITestOutputHelper output
) : SqlServerDatabaseMethodsests<SqlServer_2017_DatabaseFixture>(fixture, output) { }

/// <summary>
/// Abstract class for Postgres database tests
/// </summary>
/// <typeparam name="TDatabaseFixture"></typeparam>
public abstract class SqlServerDatabaseMethodsests<TDatabaseFixture>(
    TDatabaseFixture fixture,
    ITestOutputHelper output
) : DatabaseMethodsTests(output), IClassFixture<TDatabaseFixture>, IDisposable
    where TDatabaseFixture : SqlServerDatabaseFixture
{
    static SqlServerDatabaseMethodsests()
    {
        Providers.DatabaseMethodsProvider.RegisterFactory(
            nameof(ProfiledSqlServerMethodsFactory),
            new ProfiledSqlServerMethodsFactory()
        );
    }

    public override async Task<IDbConnection> OpenConnectionAsync()
    {
        var db = new DbQueryLogging.LoggedDbConnection(
            new SqlConnection(fixture.ConnectionString),
            new Logging.TestLogger(Output, nameof(SqlConnection))
        );
        await db.OpenAsync();
        return db;
    }
}

public class ProfiledSqlServerMethodsFactory : Providers.SqlServer.SqlServerMethodsFactory
{
    public override bool SupportsConnectionCustom(IDbConnection db) =>
        db is DbQueryLogging.LoggedDbConnection loggedDb && loggedDb.Inner is SqlConnection;
}
