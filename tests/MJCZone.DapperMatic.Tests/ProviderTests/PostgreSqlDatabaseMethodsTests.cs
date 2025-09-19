// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using Dapper;
using MJCZone.DapperMatic.Models;
using MJCZone.DapperMatic.Providers;
using MJCZone.DapperMatic.Tests.ProviderFixtures;
using Npgsql;
using Xunit.Abstractions;

namespace MJCZone.DapperMatic.Tests.ProviderTests;

/// <summary>
/// Testing Postgres 15
/// </summary>
public class PostgreSql_Postgres15_DatabaseMethodsTests(
    PostgreSql_Postgres15_DatabaseFixture fixture,
    ITestOutputHelper output
) : PostgreSqlDatabaseMethodsTests<PostgreSql_Postgres15_DatabaseFixture>(fixture, output) { }

/// <summary>
/// Testing Postgres 16
/// </summary>
public class PostgreSql_Postgres16_DatabaseMethodsTests(
    PostgreSql_Postgres16_DatabaseFixture fixture,
    ITestOutputHelper output
) : PostgreSqlDatabaseMethodsTests<PostgreSql_Postgres16_DatabaseFixture>(fixture, output) { }

/// <summary>
/// Testing Postgres 17
/// </summary>
public class PostgreSql_Postgres17_DatabaseMethodsTests(
    PostgreSql_Postgres17_DatabaseFixture fixture,
    ITestOutputHelper output
) : PostgreSqlDatabaseMethodsTests<PostgreSql_Postgres17_DatabaseFixture>(fixture, output) { }

/// <summary>
/// Testing Postgres 15 with Postgis
/// </summary>
public class PostgreSql_Postgis15_DatabaseMethodsTests(
    PostgreSql_Postgis15_DatabaseFixture fixture,
    ITestOutputHelper output
) : PostgreSqlDatabaseMethodsTests<PostgreSql_Postgis15_DatabaseFixture>(fixture, output) { }

/// <summary>
/// Testing Postgres 16 with Postgis
/// </summary>
public class PostgreSql_Postgis16_DatabaseMethodsTests(
    PostgreSql_Postgis16_DatabaseFixture fixture,
    ITestOutputHelper output
) : PostgreSqlDatabaseMethodsTests<PostgreSql_Postgis16_DatabaseFixture>(fixture, output) { }

/// <summary>
/// Testing Postgres 17 with Postgis
/// </summary>
public class PostgreSql_Postgis17_DatabaseMethodsTests(
    PostgreSql_Postgis17_DatabaseFixture fixture,
    ITestOutputHelper output
) : PostgreSqlDatabaseMethodsTests<PostgreSql_Postgis17_DatabaseFixture>(fixture, output) { }

/// <summary>
/// Abstract class for Postgres database tests
/// </summary>
/// <typeparam name="TDatabaseFixture"></typeparam>
public abstract class PostgreSqlDatabaseMethodsTests<TDatabaseFixture>(
    TDatabaseFixture fixture,
    ITestOutputHelper output
) : DatabaseMethodsTests(output), IClassFixture<TDatabaseFixture>, IDisposable
    where TDatabaseFixture : PostgreSqlDatabaseFixture
{
    static PostgreSqlDatabaseMethodsTests()
    {
        DatabaseMethodsProvider.RegisterFactory(
            nameof(ProfiledPostgreSqlMethodsFactory),
            new ProfiledPostgreSqlMethodsFactory()
        );
    }

    public override async Task<IDbConnection> OpenConnectionAsync()
    {
        var db = new DbQueryLogging.LoggedDbConnection(
            new NpgsqlConnection(fixture.ConnectionString),
            new Logging.TestLogger(Output, nameof(NpgsqlConnection))
        );
        await db.OpenAsync();
        await db.ExecuteAsync("CREATE EXTENSION IF NOT EXISTS \"uuid-ossp\";");
        await db.ExecuteAsync("CREATE EXTENSION IF NOT EXISTS \"hstore\";");
        if (
            await db.ExecuteScalarAsync<int>(
                @"select count(*) from pg_extension where extname = 'postgis'"
            ) > 0
        )
        {
            await db.ExecuteAsync("CREATE EXTENSION IF NOT EXISTS \"postgis\";");
            await db.ExecuteAsync("CREATE EXTENSION IF NOT EXISTS \"postgis_topology\";");
        }
        return db;
    }

    protected override async Task Provider_returns_custom_datatypes()
    {
        using var db = await OpenConnectionAsync();
        var databaseMethods = DatabaseMethodsProvider.GetMethods(db);

        // PostgreSQL has the most robust custom type discovery
        var customTypes = await databaseMethods.DiscoverCustomDataTypesAsync(db);
        Assert.NotNull(customTypes);

        // PostgreSQL might have system-defined custom types even in a fresh database
        // So we just verify the method completes successfully
    }

    [Fact]
    public async Task GetAvailableDataTypes_PostgreSQL_ShouldHaveAdvancedTypes()
    {
        using var db = await OpenConnectionAsync();
        var databaseMethods = DatabaseMethodsProvider.GetMethods(db);

        var allTypes = databaseMethods.GetAvailableDataTypes(includeAdvanced: true).ToList();

        // PostgreSQL should have the most types
        Assert.True(allTypes.Count > 30, "PostgreSQL should have many data types");

        // Verify PostgreSQL-specific types
        Assert.Contains(allTypes, t => t.DataType == "jsonb");
        Assert.Contains(allTypes, t => t.DataType == "uuid");
        Assert.Contains(allTypes, t => t.DataType == "inet");
        Assert.Contains(allTypes, t => t.DataType == "int4range");
        Assert.Contains(allTypes, t => t.DataType == "integer[]");

        // Verify advanced categories
        var categories = allTypes.Select(t => t.Category).Distinct().ToList();
        Assert.Contains(DataTypeCategory.Network, categories);
        Assert.Contains(DataTypeCategory.Range, categories);
        Assert.Contains(DataTypeCategory.Array, categories);
    }
}

public class ProfiledPostgreSqlMethodsFactory : Providers.PostgreSql.PostgreSqlMethodsFactory
{
    public override bool SupportsConnectionCustom(IDbConnection db) =>
        db is DbQueryLogging.LoggedDbConnection loggedDb && loggedDb.Inner is NpgsqlConnection;
}
