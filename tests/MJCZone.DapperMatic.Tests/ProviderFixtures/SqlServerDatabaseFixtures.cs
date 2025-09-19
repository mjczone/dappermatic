// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Testcontainers.MsSql;

namespace MJCZone.DapperMatic.Tests.ProviderFixtures; //mcr.microsoft.com/mssql/server:2025-latest

// // Still waiting for 2025 CU1 image
// public class SqlServer_2025_Ubuntu_DatabaseFixture : SqlServerDatabaseFixture
// {
//     public SqlServer_2025_Ubuntu_DatabaseFixture()
//         : base("mcr.microsoft.com/mssql/server:2025-CU1-ubuntu-24.04") { }
//     // : base("mcr.microsoft.com/mssql/server:2025-latest") { }
// }

public class SqlServer_2022_DatabaseFixture : SqlServerDatabaseFixture
{
    public SqlServer_2022_DatabaseFixture()
        : base("mcr.microsoft.com/mssql/server:2022-CU21-ubuntu-22.04") { }
    // : base("mcr.microsoft.com/mssql/server:2022-CU13-ubuntu-22.04") { }
    // : base("mcr.microsoft.com/mssql/server:2022-latest") { }
}

public class SqlServer_2019_DatabaseFixture : SqlServerDatabaseFixture
{
    public SqlServer_2019_DatabaseFixture()
        : base("mcr.microsoft.com/mssql/server:2019-CU32-ubuntu-20.04") { }
    // : base("mcr.microsoft.com/mssql/server:2019-CU27-ubuntu-20.04") { }
    // : base("mcr.microsoft.com/mssql/server:2019-latest") { }
}

public class SqlServer_2017_DatabaseFixture : SqlServerDatabaseFixture
{
    public SqlServer_2017_DatabaseFixture()
        : base("mcr.microsoft.com/mssql/server:2017-CU31-ubuntu-18.04") { }
    // : base("mcr.microsoft.com/mssql/server:2017-CU29-ubuntu-16.04") { }
    // : base("mcr.microsoft.com/mssql/server:2017-latest") { }
}

public abstract class SqlServerDatabaseFixture(string imageName)
    : DatabaseFixtureBase<MsSqlContainer>
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage(imageName)
        .WithPassword("Strong_password_123!")
        .WithAutoRemove(true)
        .WithCleanUp(true)
        .Build();

    public override MsSqlContainer Container
    {
        get { return _container; }
    }
}
