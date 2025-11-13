// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using Dapper;
using MJCZone.DapperMatic.Tests.ProviderDDLTests;
using MJCZone.DapperMatic.Tests.ProviderFixtures;
using Npgsql;
using Xunit.Abstractions;

namespace MJCZone.DapperMatic.Tests.ProviderDMLTests;

/// <summary>
/// DapperMatic DML type mapping tests for PostgreSQL 15.
/// </summary>
public class PostgreSql_Postgres15_DapperMaticDmlTests(
    PostgreSql_Postgres15_DatabaseFixture fixture,
    ITestOutputHelper output
) : PostgreSqlDapperMaticDmlTests<PostgreSql_Postgres15_DatabaseFixture>(fixture, output) { }

/// <summary>
/// DapperMatic DML type mapping tests for PostgreSQL 16.
/// </summary>
public class PostgreSql_Postgres16_DapperMaticDmlTests(
    PostgreSql_Postgres16_DatabaseFixture fixture,
    ITestOutputHelper output
) : PostgreSqlDapperMaticDmlTests<PostgreSql_Postgres16_DatabaseFixture>(fixture, output) { }

/// <summary>
/// DapperMatic DML type mapping tests for PostgreSQL 17.
/// </summary>
public class PostgreSql_Postgres17_DapperMaticDmlTests(
    PostgreSql_Postgres17_DatabaseFixture fixture,
    ITestOutputHelper output
) : PostgreSqlDapperMaticDmlTests<PostgreSql_Postgres17_DatabaseFixture>(fixture, output) { }

/// <summary>
/// DapperMatic DML type mapping tests for PostgreSQL 17 with PostGIS.
/// </summary>
public class PostgreSql_Postgis17_DapperMaticDmlTests(
    PostgreSql_Postgis17_DatabaseFixture fixture,
    ITestOutputHelper output
) : PostgreSqlDapperMaticDmlTests<PostgreSql_Postgis17_DatabaseFixture>(fixture, output) { }

/// <summary>
/// Abstract base class for PostgreSQL DML type mapping tests.
/// </summary>
/// <typeparam name="TDatabaseFixture">The database fixture type.</typeparam>
public abstract class PostgreSqlDapperMaticDmlTests<TDatabaseFixture>(
    TDatabaseFixture fixture,
    ITestOutputHelper output
) : DapperMaticDmlTypeMappingTests(output), IClassFixture<TDatabaseFixture>, IDisposable
    where TDatabaseFixture : PostgreSqlDatabaseFixture
{
    private readonly NpgsqlDataSource dataSource = CreateDataSource(fixture.ConnectionString);

    static PostgreSqlDapperMaticDmlTests()
    {
        Providers.DatabaseMethodsProvider.RegisterFactory(
            nameof(ProfiledPostgreSqlMethodsFactory),
            new ProfiledPostgreSqlMethodsFactory()
        );
    }

    private static NpgsqlDataSource CreateDataSource(string connectionString)
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseNetTopologySuite();
        return dataSourceBuilder.Build();
    }

    public override async Task<IDbConnection> OpenConnectionAsync()
    {
        var connection = await dataSource.OpenConnectionAsync();
        var db = new Logging.DbLoggingConnection(
            connection,
            new Logging.TestLogger(Output, nameof(NpgsqlConnection))
        );

        // Create extensions needed for DML tests
        await db.ExecuteAsync("CREATE EXTENSION IF NOT EXISTS \"uuid-ossp\";");
        await db.ExecuteAsync("CREATE EXTENSION IF NOT EXISTS \"hstore\";");
        if (await db.ExecuteScalarAsync<int>(@"select count(*) from pg_extension where extname = 'postgis'") > 0)
        {
            await db.ExecuteAsync("CREATE EXTENSION IF NOT EXISTS \"postgis\";");
            await db.ExecuteAsync("CREATE EXTENSION IF NOT EXISTS \"postgis_topology\";");
        }

        return db;
    }

    public override void Dispose()
    {
        dataSource?.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
