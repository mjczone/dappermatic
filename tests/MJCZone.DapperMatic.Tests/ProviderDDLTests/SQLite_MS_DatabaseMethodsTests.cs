// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using Xunit.Abstractions;

namespace MJCZone.DapperMatic.Tests.ProviderDDLTests;

public class SQLite_MS_DatabaseMethodsTests(ITestOutputHelper output) : DatabaseMethodsTests(output), IDisposable
{
    static SQLite_MS_DatabaseMethodsTests()
    {
        Providers.DatabaseMethodsProvider.RegisterFactory(
            nameof(ProfiledMsSqLiteMethodsFactory),
            new ProfiledMsSqLiteMethodsFactory()
        );
    }

    public override async Task<IDbConnection> OpenConnectionAsync()
    {
        if (File.Exists("ms_sqlite_tests.sqlite"))
        {
            File.Delete("ms_sqlite_tests.sqlite");
        }

        var cstr = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
        {
            DataSource = "ms_sqlite_tests.sqlite",
            Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate,
            Cache = Microsoft.Data.Sqlite.SqliteCacheMode.Default,
            Pooling = true,
        };

        var db = new Logging.DbLoggingConnection(
            new Microsoft.Data.Sqlite.SqliteConnection(cstr.ConnectionString),
            new Logging.TestLogger(Output, nameof(Microsoft.Data.Sqlite.SqliteConnection))
        );

        await db.OpenAsync();
        return db;
    }

    public override void Dispose()
    {
        // Clear all connection pools to release file locks
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        // Give the OS a moment to release the file lock
        System.Threading.Thread.Sleep(100);

        if (File.Exists("ms_sqlite_tests.sqlite"))
        {
            try
            {
                File.Delete("ms_sqlite_tests.sqlite");
            }
            catch (IOException)
            {
                // File might still be locked, ignore
            }
        }

        base.Dispose();
    }
}

public class ProfiledMsSqLiteMethodsFactory : Providers.Sqlite.SqliteMethodsFactory
{
    public override bool SupportsConnectionCustom(IDbConnection db) =>
        db is Logging.DbLoggingConnection loggedDb && loggedDb.Inner is Microsoft.Data.Sqlite.SqliteConnection;
}
