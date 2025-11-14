// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Data.SQLite;
using MJCZone.DapperMatic.Tests.ProviderDDLTests;
using Xunit.Abstractions;

namespace MJCZone.DapperMatic.Tests.ProviderDMLTests;

/// <summary>
/// DapperMatic DML type mapping tests for SQLite 3.
/// </summary>
public class SQLite_3_DapperMaticDmlTests(ITestOutputHelper output)
    : DapperMaticDmlTypeMappingTests(output),
        IDisposable
{
    static SQLite_3_DapperMaticDmlTests()
    {
        Providers.DatabaseMethodsProvider.RegisterFactory(
            nameof(ProfiledSqLiteMethodsFactory),
            new ProfiledSqLiteMethodsFactory()
        );
    }

    private const string DatabaseFileName = "sqlite_dml_tests.sqlite";

    public override async Task<IDbConnection> OpenConnectionAsync()
    {
        if (File.Exists(DatabaseFileName))
        {
            File.Delete(DatabaseFileName);
        }

        var db = new Logging.DbLoggingConnection(
            new SQLiteConnection($"Data Source={DatabaseFileName};Version=3;BinaryGuid=False;"),
            new Logging.TestLogger(Output, nameof(SQLiteConnection))
        );
        await db.OpenAsync();
        return db;
    }

    public override void Dispose()
    {
        if (File.Exists(DatabaseFileName))
        {
            File.Delete(DatabaseFileName);
        }

        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
