// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using Dapper;
using Newtonsoft.Json;
using Xunit.Abstractions;

namespace MJCZone.DapperMatic.Tests;

public abstract partial class DatabaseMethodsTests : TestBase
{
    protected DatabaseMethodsTests(ITestOutputHelper output)
        : base(output) { }

    public abstract Task<IDbConnection> OpenConnectionAsync();

    [Fact]
    protected virtual async Task Database_can_run_arbitrary_queries_async()
    {
        using var db = await OpenConnectionAsync();
        const int expected = 1;
        var actual = await db.QueryFirstAsync<int>("SELECT 1");
        Assert.Equal(expected, actual);

        // run a statement with many sql statements at the same time
        await db.ExecuteAsync(
            """

                        CREATE TABLE test (id INT PRIMARY KEY);
                        INSERT INTO test VALUES (1);
                        INSERT INTO test VALUES (2);
                        INSERT INTO test VALUES (3);
                        
            """
        );
        var values = await db.QueryAsync<int>("SELECT id FROM test");
        Assert.Equal(3, values.Count());

        // run multiple select statements and read multiple result sets
        var result = await db.QueryMultipleAsync(
            """

                        SELECT id FROM test WHERE id = 1;
                        SELECT id FROM test WHERE id = 2;
                        SELECT id FROM test;
                        -- this statement is ignored by the grid reader
                        -- because it doesn't return any results
                        INSERT INTO test VALUES (4);
                        SELECT id FROM test WHERE id = 4;
                        
            """
        );
        var id1 = result.Read<int>().Single();
        var id2 = result.Read<int>().Single();
        var allIds = result.Read<int>().ToArray();
        var id4 = result.Read<int>().Single();
        Assert.Equal(1, id1);
        Assert.Equal(2, id2);
        Assert.Equal(3, allIds.Length);
        Assert.Equal(4, id4);
    }

    [Fact]
    protected virtual async Task Get_database_version_returns_version_async()
    {
        using var db = await OpenConnectionAsync();

        var version = await db.GetDatabaseVersionAsync();
        Assert.True(version.Major > 0);

        Output.WriteLine("Database version: {0}", version);
    }
}
