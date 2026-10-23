// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using MJCZone.DapperMatic.Tests.ProviderFixtures;
using Xunit.Abstractions;

namespace MJCZone.DapperMatic.Tests.ProviderDMLTests;

// The other DML test classes use the older driver for each database, wrapped in a logging connection. These use
// the drivers most applications use today, unwrapped, as an application would: type handlers decide what to do
// from the driver's own parameter type, and drivers differ in what they accept and return.

/// <summary>
/// DapperMatic DML type mapping tests for SQL Server 2022 through Microsoft.Data.SqlClient.
/// </summary>
public class SqlServer_2022_MicrosoftDataSqlClient_DapperMaticDmlTests(
    SqlServer_2022_DatabaseFixture fixture,
    ITestOutputHelper output
) : DapperMaticDmlTypeMappingTests(output), IClassFixture<SqlServer_2022_DatabaseFixture>
{
    public override async Task<IDbConnection> OpenConnectionAsync()
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            TrustServerCertificate = true,
        };
        var db = new Microsoft.Data.SqlClient.SqlConnection(builder.ConnectionString);
        await db.OpenAsync();
        return db;
    }
}

/// <summary>
/// DapperMatic DML type mapping tests for MySQL 8.4 through MySqlConnector.
/// </summary>
public class MySql_84_MySqlConnector_DapperMaticDmlTests(MySql_84_DatabaseFixture fixture, ITestOutputHelper output)
    : DapperMaticDmlTypeMappingTests(output),
        IClassFixture<MySql_84_DatabaseFixture>
{
    public override async Task<IDbConnection> OpenConnectionAsync()
    {
        var builder = new MySqlConnector.MySqlConnectionStringBuilder(fixture.ConnectionString)
        {
            SslMode = MySqlConnector.MySqlSslMode.None,
            AllowPublicKeyRetrieval = true,
            AllowLoadLocalInfile = true,
        };
        var db = new MySqlConnector.MySqlConnection(builder.ConnectionString);
        await db.OpenAsync();
        return db;
    }
}

/// <summary>
/// DapperMatic DML type mapping tests for SQLite through Microsoft.Data.Sqlite.
/// </summary>
public class SQLite_MicrosoftDataSqlite_DapperMaticDmlTests(ITestOutputHelper output)
    : DapperMaticDmlTypeMappingTests(output),
        IDisposable
{
    private const string DatabaseFileName = "sqlite_ms_dml_tests.sqlite";

    public override async Task<IDbConnection> OpenConnectionAsync()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(DatabaseFileName))
        {
            File.Delete(DatabaseFileName);
        }

        var db = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={DatabaseFileName}");
        await db.OpenAsync();
        return db;
    }

    public override void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(DatabaseFileName))
        {
            File.Delete(DatabaseFileName);
        }

        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
