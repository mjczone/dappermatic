// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Runtime.CompilerServices;
using MJCZone.DapperMatic.Tests.ProviderDMLTests;
using MJCZone.DapperMatic.Tests.ProviderFixtures;
using MJCZone.DapperMatic.TypeMapping;
using Xunit.Abstractions;

namespace MJCZone.DapperMatic.Tests.JsonCollections;

// The DML tests again, one database per provider, with collections of simple values stored as JSON.
// DapperMaticTypeMapping.Initialize runs once per process and Dapper's handlers are process-wide, so this setting
// gets a process of its own: this test project.

/// <summary>
/// Initializes DapperMatic's type mapping for this test run, before any test can.
/// </summary>
internal static class JsonCollectionsMode
{
    [ModuleInitializer]
    internal static void Initialize() =>
        DapperMaticTypeMapping.Initialize(
            new DapperMaticMappingOptions
            {
                HandlerPrecedence = TypeHandlerPrecedence.OverrideExisting,
                StoreSimpleCollectionsAsJson = true,
            }
        );
}

/// <summary>
/// DML tests on SQL Server 2022 through System.Data.SqlClient, with collections of simple values stored as JSON.
/// </summary>
public class SqlServer_2022_JsonCollections_DmlTests(SqlServer_2022_DatabaseFixture fixture, ITestOutputHelper output)
    : SqlServerDapperMaticDmlTests<SqlServer_2022_DatabaseFixture>(fixture, output) { }

/// <summary>
/// DML tests on SQL Server 2022 through Microsoft.Data.SqlClient, with collections of simple values stored as JSON.
/// </summary>
public class SqlServer_2022_MicrosoftDataSqlClient_JsonCollections_DmlTests(
    SqlServer_2022_DatabaseFixture fixture,
    ITestOutputHelper output
) : SqlServer_2022_MicrosoftDataSqlClient_DapperMaticDmlTests(fixture, output) { }

/// <summary>
/// DML tests on MySQL 8.4, with collections of simple values stored as JSON.
/// </summary>
public class MySql_84_JsonCollections_DmlTests(MySql_84_DatabaseFixture fixture, ITestOutputHelper output)
    : MySqlDapperMaticDmlTests<MySql_84_DatabaseFixture>(fixture, output) { }

/// <summary>
/// DML tests on MariaDB 11.8, with collections of simple values stored as JSON.
/// </summary>
public class MariaDb_11_8_JsonCollections_DmlTests(MariaDb_11_8_DatabaseFixture fixture, ITestOutputHelper output)
    : MariaDbDapperMaticDmlTests<MariaDb_11_8_DatabaseFixture>(fixture, output) { }

/// <summary>
/// DML tests on PostgreSQL 17, with collections of simple values stored as JSON.
/// </summary>
public class PostgreSql_Postgres17_JsonCollections_DmlTests(
    PostgreSql_Postgres17_DatabaseFixture fixture,
    ITestOutputHelper output
) : PostgreSqlDapperMaticDmlTests<PostgreSql_Postgres17_DatabaseFixture>(fixture, output) { }

/// <summary>
/// DML tests on SQLite through System.Data.SQLite, with collections of simple values stored as JSON.
/// </summary>
public class SQLite_3_JsonCollections_DmlTests(ITestOutputHelper output) : SQLite_3_DapperMaticDmlTests(output) { }

/// <summary>
/// DML tests on SQLite through Microsoft.Data.Sqlite, with collections of simple values stored as JSON.
/// </summary>
public class SQLite_MicrosoftDataSqlite_JsonCollections_DmlTests(ITestOutputHelper output)
    : SQLite_MicrosoftDataSqlite_DapperMaticDmlTests(output) { }
