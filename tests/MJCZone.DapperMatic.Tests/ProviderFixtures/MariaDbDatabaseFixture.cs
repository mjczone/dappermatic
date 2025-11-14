// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Testcontainers.MariaDb;
using Testcontainers.MySql;

namespace MJCZone.DapperMatic.Tests.ProviderFixtures;

public class MariaDb_12_0_DatabaseFixture : MariaDbDatabaseFixture
{
    public MariaDb_12_0_DatabaseFixture()
        : base("mariadb:12.0") { }

    public override bool IgnoreSqlType(string sqlType)
    {
        return sqlType.Equals("geomcollection", StringComparison.OrdinalIgnoreCase) || base.IgnoreSqlType(sqlType);
    }
}

public class MariaDb_11_8_DatabaseFixture : MariaDbDatabaseFixture
{
    public MariaDb_11_8_DatabaseFixture()
        : base("mariadb:11.8") { }

    public override bool IgnoreSqlType(string sqlType)
    {
        return sqlType.Equals("geomcollection", StringComparison.OrdinalIgnoreCase) || base.IgnoreSqlType(sqlType);
    }
}

public class MariaDb_11_4_DatabaseFixture : MariaDbDatabaseFixture
{
    public MariaDb_11_4_DatabaseFixture()
        : base("mariadb:11.4") { }

    public override bool IgnoreSqlType(string sqlType)
    {
        return sqlType.Equals("geomcollection", StringComparison.OrdinalIgnoreCase) || base.IgnoreSqlType(sqlType);
    }
}

public class MariaDb_10_11_DatabaseFixture : MariaDbDatabaseFixture
{
    public MariaDb_10_11_DatabaseFixture()
        : base("mariadb:10.11") { }

    public override bool IgnoreSqlType(string sqlType)
    {
        return sqlType.Equals("geomcollection", StringComparison.OrdinalIgnoreCase) || base.IgnoreSqlType(sqlType);
    }
}

public abstract class MariaDbDatabaseFixture(string imageName) : DatabaseFixtureBase<MariaDbContainer>
{
    private readonly MariaDbContainer _container = new MariaDbBuilder()
        .WithImage(imageName)
        .WithPassword("Strong_password_123!")
        .WithAutoRemove(true)
        .WithCleanUp(true)
        .Build();

    public override MariaDbContainer Container
    {
        get { return _container; }
    }
}
