// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Data.SQLite;
using Xunit.Abstractions;

namespace MJCZone.DapperMatic.Tests.ProviderDDLTests;

public class SQLite_3_DatabaseMethodsTests(ITestOutputHelper output) : DatabaseMethodsTests(output), IDisposable
{
    static SQLite_3_DatabaseMethodsTests()
    {
        Providers.DatabaseMethodsProvider.RegisterFactory(
            nameof(ProfiledSqLiteMethodsFactory),
            new ProfiledSqLiteMethodsFactory()
        );
    }

    public override async Task<IDbConnection> OpenConnectionAsync()
    {
        if (File.Exists("sqlite_tests.sqlite"))
        {
            File.Delete("sqlite_tests.sqlite");
        }

        var db = new Logging.DbLoggingConnection(
            new SQLiteConnection("Data Source=sqlite_tests.sqlite;Version=3;BinaryGuid=False;"),
            new Logging.TestLogger(Output, nameof(SQLiteConnection))
        );
        await db.OpenAsync();
        return db;
    }

    public override void Dispose()
    {
        // Clear all connection pools to release file locks
        SQLiteConnection.ClearAllPools();

        // Give the OS a moment to release the file lock
        System.Threading.Thread.Sleep(100);

        if (File.Exists("sqlite_tests.sqlite"))
        {
            try
            {
                File.Delete("sqlite_tests.sqlite");
            }
            catch (IOException)
            {
                // File might still be locked, ignore
            }
        }

        base.Dispose();
    }
}

public class ProfiledSqLiteMethodsFactory : Providers.Sqlite.SqliteMethodsFactory
{
    public override bool SupportsConnectionCustom(IDbConnection db) =>
        db is Logging.DbLoggingConnection loggedDb && loggedDb.Inner is SQLiteConnection;
}
