// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Testcontainers.PostgreSql;

namespace MJCZone.DapperMatic.Tests.ProviderFixtures;

public class PostgreSql_Postgres15_DatabaseFixture : PostgreSqlDatabaseFixture
{
    public PostgreSql_Postgres15_DatabaseFixture()
        : base("postgres:15") { }
}

public class PostgreSql_Postgres16_DatabaseFixture : PostgreSqlDatabaseFixture
{
    public PostgreSql_Postgres16_DatabaseFixture()
        : base("postgres:16") { }
}

public class PostgreSql_Postgres17_DatabaseFixture : PostgreSqlDatabaseFixture
{
    public PostgreSql_Postgres17_DatabaseFixture()
        : base("postgres:17") { }
}

public class PostgreSql_Postgis15_DatabaseFixture : PostgreSqlDatabaseFixture
{
    public PostgreSql_Postgis15_DatabaseFixture()
        : base("postgis/postgis:15-3.4") { }
}

public class PostgreSql_Postgis16_DatabaseFixture : PostgreSqlDatabaseFixture
{
    public PostgreSql_Postgis16_DatabaseFixture()
        : base("postgis/postgis:16-3.4") { }
}

public class PostgreSql_Postgis17_DatabaseFixture : PostgreSqlDatabaseFixture
{
    public PostgreSql_Postgis17_DatabaseFixture()
        : base("postgis/postgis:17-3.5") { }
}

public abstract class PostgreSqlDatabaseFixture(string imageName) : DatabaseFixtureBase<PostgreSqlContainer>
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage(imageName)
        .WithPassword("Strong_password_123!")
        .WithAutoRemove(true)
        .WithCleanUp(true)
        .Build();

    public override PostgreSqlContainer Container
    {
        get { return _container; }
    }
}
