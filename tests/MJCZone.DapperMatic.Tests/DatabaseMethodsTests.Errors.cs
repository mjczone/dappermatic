// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using Dapper;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Tests;

public abstract partial class DatabaseMethodsTests
{
    /// <summary>
    /// Each driver's errors are classified by their codes, the same kinds on every provider: what lets an API answer
    /// with the right status and a message that names nothing of the schema.
    /// </summary>
    [Fact]
    protected virtual async Task Database_errors_are_classified_by_their_codes_Async()
    {
        // The short constructor once passed the check as the default and the default as the check.
        var both = new DmColumn("c", typeof(int), defaultExpression: "1", checkExpression: "c > 0");
        Assert.Equal(("1", "c > 0"), (both.DefaultExpression, both.CheckExpression));

        using var db = await OpenConnectionAsync();
        await db.DropTableIfExistsAsync(null, "err_child");
        await db.DropTableIfExistsAsync(null, "err_parent");
        await db.CreateTableIfNotExistsAsync(new DmTable(null, "err_parent", [new DmColumn("id", typeof(int), isPrimaryKey: true)]));
        var checks = await db.SupportsCheckConstraintsAsync();
        await db.CreateTableIfNotExistsAsync(
            new DmTable(
                null,
                "err_child",
                [
                    new DmColumn("id", typeof(int), isPrimaryKey: true),
                    new DmColumn("parent_id", typeof(int), isNullable: true, isForeignKey: true, referencedTableName: "err_parent", referencedColumnName: "id"),
                    new DmColumn("name", typeof(string), length: 5, isUnique: true),
                    new DmColumn("qty", typeof(int), checkExpression: checks ? "qty > 0" : null),
                ]
            )
        );
        await db.ExecuteAsync("INSERT INTO err_parent (id) VALUES (1)");
        await db.ExecuteAsync("INSERT INTO err_child (id, parent_id, name, qty) VALUES (1, 1, 'a', 1)");

        async Task<DmDatabaseErrorKind> Kind(string sql)
        {
            var error = await Record.ExceptionAsync(() => db.ExecuteAsync(sql));
            Assert.NotNull(error);
            return DmDatabaseErrors.Classify(error);
        }

        Assert.Equal(DmDatabaseErrorKind.UniqueViolation, await Kind("INSERT INTO err_child (id, parent_id, name, qty) VALUES (1, 1, 'b', 1)"));
        Assert.Equal(DmDatabaseErrorKind.UniqueViolation, await Kind("INSERT INTO err_child (id, parent_id, name, qty) VALUES (2, 1, 'a', 1)"));
        Assert.Equal(DmDatabaseErrorKind.NotNullViolation, await Kind("INSERT INTO err_child (id, parent_id, name, qty) VALUES (3, 1, NULL, 1)"));
        Assert.Equal(DmDatabaseErrorKind.ObjectNotFound, await Kind("SELECT * FROM err_missing"));

        var sqlite = db.GetDbProviderType() == DbProviderType.Sqlite;
        if (!sqlite || await db.ExecuteScalarAsync<long>("PRAGMA foreign_keys") == 1)
        {
            Assert.Equal(DmDatabaseErrorKind.ForeignKeyViolation, await Kind("INSERT INTO err_child (id, parent_id, name, qty) VALUES (4, 99, 'c', 1)"));
        }

        if (checks)
        {
            Assert.Equal(DmDatabaseErrorKind.CheckViolation, await Kind("INSERT INTO err_child (id, parent_id, name, qty) VALUES (5, 1, 'd', 0)"));
        }

        // SQLite keeps any length; the others refuse a value longer than its column.
        if (!sqlite)
        {
            Assert.Equal(DmDatabaseErrorKind.ValueOutOfRange, await Kind("INSERT INTO err_child (id, parent_id, name, qty) VALUES (6, 1, 'abcdefgh', 1)"));
        }

        Assert.Equal(DmDatabaseErrorKind.Unknown, DmDatabaseErrors.Classify(new InvalidOperationException("not a database's")));

        await db.DropTableIfExistsAsync(null, "err_child");
        await db.DropTableIfExistsAsync(null, "err_parent");
    }
}
