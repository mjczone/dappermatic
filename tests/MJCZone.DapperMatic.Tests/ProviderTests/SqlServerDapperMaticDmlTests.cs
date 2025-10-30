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
/// DapperMatic DML type mapping tests for SqlServer 2022.
/// </summary>
public class SqlServer_2022_DapperMaticDmlTests(SqlServer_2022_DatabaseFixture fixture, ITestOutputHelper output)
    : SqlServerDapperMaticDmlTests<SqlServer_2022_DatabaseFixture>(fixture, output) { }

/// <summary>
/// DapperMatic DML type mapping tests for SqlServer 2019.
/// </summary>
public class SqlServer_2019_DapperMaticDmlTests(SqlServer_2019_DatabaseFixture fixture, ITestOutputHelper output)
    : SqlServerDapperMaticDmlTests<SqlServer_2019_DatabaseFixture>(fixture, output) { }

/// <summary>
/// DapperMatic DML type mapping tests for SqlServer 2017.
/// </summary>
public class SqlServer_2017_DapperMaticDmlTests(SqlServer_2017_DatabaseFixture fixture, ITestOutputHelper output)
    : SqlServerDapperMaticDmlTests<SqlServer_2017_DatabaseFixture>(fixture, output) { }

/// <summary>
/// Abstract base class for SQL Server DML type mapping tests.
/// </summary>
/// <typeparam name="TDatabaseFixture">The database fixture type.</typeparam>
public abstract class SqlServerDapperMaticDmlTests<TDatabaseFixture>(TDatabaseFixture fixture, ITestOutputHelper output)
    : DapperMaticDmlTypeMappingTests(output),
        IClassFixture<TDatabaseFixture>,
        IDisposable
    where TDatabaseFixture : SqlServerDatabaseFixture
{
    static SqlServerDapperMaticDmlTests()
    {
        Providers.DatabaseMethodsProvider.RegisterFactory(
            nameof(ProfiledSqlServerMethodsFactory),
            new ProfiledSqlServerMethodsFactory()
        );
    }

    public override async Task<IDbConnection> OpenConnectionAsync()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        var db = new Logging.DbLoggingConnection(
            new SqlConnection(fixture.ConnectionString),
            new Logging.TestLogger(Output, nameof(SqlConnection))
        );
#pragma warning restore CS0618 // Type or member is obsolete
        await db.OpenAsync();
        return db;
    }
}
