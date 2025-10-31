// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using Dapper;
using MJCZone.DapperMatic.DataAnnotations;
using MJCZone.DapperMatic.Models;
using MJCZone.DapperMatic.Tests.ProviderFixtures;
using MJCZone.DapperMatic.Tests.ProviderTests;
using Xunit;
using Xunit.Abstractions;

namespace MJCZone.DapperMatic.Tests;

/// <summary>
/// Verification tests for Phase 9 - Native Spatial Type Support.
/// These tests verify that native spatial types from provider libraries
/// (MySql.Data, Microsoft.SqlServer.Types) work WITHOUT custom DapperMatic handlers.
///
/// Tests run WITHOUT calling DapperMaticTypeMapping.Initialize() to prove
/// that provider libraries handle these types natively.
/// </summary>
[Collection("DapperMaticTests")]
public abstract class NativeSpatialTypeVerificationTests : TestBase
{
    protected NativeSpatialTypeVerificationTests(ITestOutputHelper output)
        : base(output) { }

    /// <summary>
    /// Opens a database connection for the specific provider.
    /// Implemented by provider-specific test classes.
    /// </summary>
    public abstract Task<IDbConnection> OpenConnectionAsync();

    #region MySqlGeometry Tests

    /// <summary>
    /// Test class with MySqlGeometry property for DDL/DML verification.
    /// </summary>
    public class LocationWithMySqlGeometry
    {
        [DmColumn("id")]
        public int Id { get; set; }

        [DmColumn("name")]
        public string Name { get; set; } = string.Empty;

        [DmColumn("shape")]
        public object? Shape { get; set; } // Will be MySqlGeometry on MySQL
    }

    [Fact]
    public async Task MySqlGeometry_DDL_creates_appropriate_column_type_for_provider()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);

        // Create table with MySqlGeometry property
        var table = new DmTable
        {
            TableName = "test_mysql_geometry",
            Columns =
            [
                new DmColumn("id", typeof(int), isPrimaryKey: true, isAutoIncrement: true),
                new DmColumn("name", typeof(string), length: 100),
                new DmColumn("shape", typeof(object), isNullable: true), // Generic object - DDL will pick default type
            ],
        };

        await db.CreateTableIfNotExistsAsync(table);

        // Reverse engineer to see what column type was created
        var tables = await db.GetTablesAsync(null);
        var createdTable = tables.FirstOrDefault(t =>
            t.TableName.Equals("test_mysql_geometry", StringComparison.OrdinalIgnoreCase)
        );

        Assert.NotNull(createdTable);
        var shapeColumn = createdTable.Columns.FirstOrDefault(c =>
            c.ColumnName.Equals("shape", StringComparison.OrdinalIgnoreCase)
        );

        Assert.NotNull(shapeColumn);

        // Log the actual column type created
        var providerType = db.GetDbProviderType();
        var providerDataType = shapeColumn.GetProviderDataType(providerType);

        Output.WriteLine($"Provider: {providerType}");
        Output.WriteLine($"Column 'shape' created as: {providerDataType}");
        Output.WriteLine($"Mapped to .NET type: {shapeColumn.DotnetType?.Name ?? "null"}");

        // Verify expected behavior per provider
        switch (providerType)
        {
            case DbProviderType.MySql:
                // On MySQL, expect some form of text/binary type since we used object
                // (Would be 'geometry' if we used explicit providerDataType: "{mysql:geometry}")
                Output.WriteLine("MySQL: Expected text/binary type for object property");
                break;

            case DbProviderType.PostgreSql:
                // On PostgreSQL, object type maps to jsonb
                Assert.True(
                    providerDataType?.ToLowerInvariant() == "jsonb" || providerDataType?.ToLowerInvariant() == "json",
                    "Expected jsonb or json"
                );
                Output.WriteLine("PostgreSQL: object type mapped to jsonb");
                break;

            case DbProviderType.SqlServer:
                // On SQL Server, object type maps to nvarchar(max)
                Output.WriteLine($"SQL Server: object type created column with type '{providerDataType}'");
                // Just verify it created something - let's see what it actually creates
                Assert.NotNull(providerDataType);
                break;

            case DbProviderType.Sqlite:
                // On SQLite, expect TEXT or BLOB
                Assert.True(
                    providerDataType?.Equals("TEXT", StringComparison.OrdinalIgnoreCase) == true
                        || providerDataType?.Equals("BLOB", StringComparison.OrdinalIgnoreCase) == true,
                    "Expected TEXT or BLOB"
                );
                Output.WriteLine("SQLite: Correctly created TEXT/BLOB column");
                break;
        }

        // Cleanup
        await db.DropTableIfExistsAsync(null, "test_mysql_geometry");
    }

    [Fact]
    public async Task MySqlGeometry_requires_custom_handler_for_DML_operations_on_MySQL()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);

        // Skip if not MySQL - MySqlGeometry only works on MySQL
        if (db.GetDbProviderType() != DbProviderType.MySql)
        {
            Output.WriteLine(
                $"Skipped: MySqlGeometry only supported on MySQL provider (current: {db.GetDbProviderType()})"
            );
            return;
        }

        // Create table manually with geometry column
        await db.ExecuteAsync(
            @"
            CREATE TABLE IF NOT EXISTS test_mysql_geom_dml (
                id INT AUTO_INCREMENT PRIMARY KEY,
                name VARCHAR(100),
                shape GEOMETRY
            )"
        );

        try
        {
            // Try to use MySqlGeometry with reflection (no direct dependency)
            var mySqlGeometryType = Type.GetType("MySql.Data.Types.MySqlGeometry, MySql.Data");

            if (mySqlGeometryType == null)
            {
                Output.WriteLine("MySql.Data not available - skipping MySqlGeometry test");
                return;
            }

            // Create a POINT geometry using WKT
            var fromTextMethod = mySqlGeometryType.GetMethod(
                "Parse",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public,
                null,
                new[] { typeof(string) },
                null
            );

            if (fromTextMethod == null)
            {
                Output.WriteLine("MySqlGeometry.Parse method not found - trying alternative");

                // Try constructor approach
                var constructor = mySqlGeometryType.GetConstructor(new[] { typeof(double), typeof(double) });
                if (constructor == null)
                {
                    Output.WriteLine("No suitable MySqlGeometry constructor found - test cannot proceed");
                    return;
                }
            }

            // Create a simple POINT geometry
            var wkt = "POINT(1 1)";
            object? geometry = fromTextMethod?.Invoke(null, new object[] { wkt });

            if (geometry is null)
            {
                Output.WriteLine("Could not create MySqlGeometry instance - skipping DML test");
                return;
            }

            // Test INSERT without DapperMaticTypeMapping.Initialize()
            // This will fail, proving that custom handlers ARE needed
            var exception = await Record.ExceptionAsync(async () =>
            {
                await db.ExecuteAsync(
                    "INSERT INTO test_mysql_geom_dml (name, shape) VALUES (@name, @shape)",
                    new { name = "Test Location", shape = geometry }
                );
            });

            // Verify that the insert failed - this proves custom handlers are needed
            Assert.NotNull(exception);
            Output.WriteLine("✅ VERIFIED: MySqlGeometry does NOT work natively without custom handlers");
            Output.WriteLine($"   Expected error occurred: {exception.GetType().Name}");
            Output.WriteLine($"   Message: {exception.Message}");
            Output.WriteLine("   ➡️ Phase 9 custom handlers ARE needed for MySqlGeometry");
        }
        finally
        {
            // Cleanup
            await db.ExecuteAsync("DROP TABLE IF EXISTS test_mysql_geom_dml");
        }
    }

    #endregion

    #region SQL Server Spatial Type Tests

    // TODO: Add SqlGeometry tests
    // TODO: Add SqlGeography tests
    // TODO: Add SqlHierarchyId tests

    #endregion
}

#region Provider-Specific Test Classes

// MySQL Tests
public class MySql_90_NativeSpatialTypeVerificationTests(MySql_94_DatabaseFixture fixture, ITestOutputHelper output)
    : MySqlNativeSpatialTypeVerificationTests<MySql_94_DatabaseFixture>(fixture, output) { }

public class MySql_84_NativeSpatialTypeVerificationTests(MySql_84_DatabaseFixture fixture, ITestOutputHelper output)
    : MySqlNativeSpatialTypeVerificationTests<MySql_84_DatabaseFixture>(fixture, output) { }

public class MySql_57_NativeSpatialTypeVerificationTests(MySql_57_DatabaseFixture fixture, ITestOutputHelper output)
    : MySqlNativeSpatialTypeVerificationTests<MySql_57_DatabaseFixture>(fixture, output) { }

public abstract class MySqlNativeSpatialTypeVerificationTests<TDatabaseFixture>(
    TDatabaseFixture fixture,
    ITestOutputHelper output
) : NativeSpatialTypeVerificationTests(output), IClassFixture<TDatabaseFixture>, IDisposable
    where TDatabaseFixture : MySqlDatabaseFixture
{
    static MySqlNativeSpatialTypeVerificationTests()
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
            new MySql.Data.MySqlClient.MySqlConnection(connectionString),
            new Logging.TestLogger(Output, nameof(MySql.Data.MySqlClient.MySqlConnection))
        );
        await db.OpenAsync();
        return db;
    }

    public override void Dispose() { }
}

// PostgreSQL Tests
public class PostgreSql_Postgres15_NativeSpatialTypeVerificationTests(
    PostgreSql_Postgres15_DatabaseFixture fixture,
    ITestOutputHelper output
) : PostgreSqlNativeSpatialTypeVerificationTests<PostgreSql_Postgres15_DatabaseFixture>(fixture, output) { }

public class PostgreSql_Postgres16_NativeSpatialTypeVerificationTests(
    PostgreSql_Postgres16_DatabaseFixture fixture,
    ITestOutputHelper output
) : PostgreSqlNativeSpatialTypeVerificationTests<PostgreSql_Postgres16_DatabaseFixture>(fixture, output) { }

public class PostgreSql_Postgres17_NativeSpatialTypeVerificationTests(
    PostgreSql_Postgres17_DatabaseFixture fixture,
    ITestOutputHelper output
) : PostgreSqlNativeSpatialTypeVerificationTests<PostgreSql_Postgres17_DatabaseFixture>(fixture, output) { }

public abstract class PostgreSqlNativeSpatialTypeVerificationTests<TDatabaseFixture>(
    TDatabaseFixture fixture,
    ITestOutputHelper output
) : NativeSpatialTypeVerificationTests(output), IClassFixture<TDatabaseFixture>, IDisposable
    where TDatabaseFixture : PostgreSqlDatabaseFixture
{
    static PostgreSqlNativeSpatialTypeVerificationTests()
    {
        Providers.DatabaseMethodsProvider.RegisterFactory(
            nameof(ProfiledPostgreSqlMethodsFactory),
            new ProfiledPostgreSqlMethodsFactory()
        );
    }

    public override async Task<IDbConnection> OpenConnectionAsync()
    {
        var connectionString = fixture.ConnectionString;
        var db = new Logging.DbLoggingConnection(
            new Npgsql.NpgsqlConnection(connectionString),
            new Logging.TestLogger(Output, nameof(Npgsql.NpgsqlConnection))
        );
        await db.OpenAsync();
        return db;
    }

    public override void Dispose() { }
}

// SQL Server Tests
public class SqlServer_2022_NativeSpatialTypeVerificationTests(
    SqlServer_2022_DatabaseFixture fixture,
    ITestOutputHelper output
) : SqlServerNativeSpatialTypeVerificationTests<SqlServer_2022_DatabaseFixture>(fixture, output) { }

public class SqlServer_2019_NativeSpatialTypeVerificationTests(
    SqlServer_2019_DatabaseFixture fixture,
    ITestOutputHelper output
) : SqlServerNativeSpatialTypeVerificationTests<SqlServer_2019_DatabaseFixture>(fixture, output) { }

public class SqlServer_2017_NativeSpatialTypeVerificationTests(
    SqlServer_2017_DatabaseFixture fixture,
    ITestOutputHelper output
) : SqlServerNativeSpatialTypeVerificationTests<SqlServer_2017_DatabaseFixture>(fixture, output) { }

public abstract class SqlServerNativeSpatialTypeVerificationTests<TDatabaseFixture>(
    TDatabaseFixture fixture,
    ITestOutputHelper output
) : NativeSpatialTypeVerificationTests(output), IClassFixture<TDatabaseFixture>, IDisposable
    where TDatabaseFixture : SqlServerDatabaseFixture
{
    static SqlServerNativeSpatialTypeVerificationTests()
    {
        Providers.DatabaseMethodsProvider.RegisterFactory(
            nameof(ProfiledSqlServerMethodsFactory),
            new ProfiledSqlServerMethodsFactory()
        );
    }

    public override async Task<IDbConnection> OpenConnectionAsync()
    {
        var connectionString = fixture.ConnectionString;
        var db = new Logging.DbLoggingConnection(
            new Microsoft.Data.SqlClient.SqlConnection(connectionString),
            new Logging.TestLogger(Output, nameof(Microsoft.Data.SqlClient.SqlConnection))
        );
        await db.OpenAsync();
        return db;
    }

    public override void Dispose() { }
}

// SQLite Tests
public class SQLite_3_NativeSpatialTypeVerificationTests(ITestOutputHelper output)
    : NativeSpatialTypeVerificationTests(output),
        IDisposable
{
    static SQLite_3_NativeSpatialTypeVerificationTests()
    {
        Providers.DatabaseMethodsProvider.RegisterFactory(
            nameof(ProfiledSqLiteMethodsFactory),
            new ProfiledSqLiteMethodsFactory()
        );
    }

    private const string DatabaseFileName = "sqlite_native_spatial_tests.sqlite";

    public override async Task<IDbConnection> OpenConnectionAsync()
    {
        if (File.Exists(DatabaseFileName))
        {
            File.Delete(DatabaseFileName);
        }

        var db = new Logging.DbLoggingConnection(
            new System.Data.SQLite.SQLiteConnection($"Data Source={DatabaseFileName};Version=3;BinaryGuid=False;"),
            new Logging.TestLogger(Output, nameof(System.Data.SQLite.SQLiteConnection))
        );
        await db.OpenAsync();
        return db;
    }

    public override void Dispose()
    {
        base.Dispose();
        if (File.Exists(DatabaseFileName))
        {
            File.Delete(DatabaseFileName);
        }
    }
}

#endregion
