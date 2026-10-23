// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Dapper;

namespace MJCZone.DapperMatic.Tests;

/// <summary>
/// Schema changes that a provider can only make indirectly -- by rebuilding a table, or by restating a
/// column -- must not lose data or anything about the column along the way.
/// </summary>
public abstract partial class DatabaseMethodsTests
{
    /// <summary>
    /// SQLite makes most schema changes by rebuilding the table: copy the rows out, drop the table, create
    /// it again, copy the rows back. The drop must not fire other tables' foreign keys. Outside a
    /// transaction foreign keys are switched off for the rebuild; inside one SQLite cannot switch them off,
    /// and the change used to delete every ON DELETE CASCADE row that referenced the table.
    /// </summary>
    [Fact]
    protected virtual async Task Sqlite_table_rebuild_never_deletes_rows_in_referencing_tables_Async()
    {
        using var db = await OpenConnectionAsync();
        if (db.GetDbProviderType() != DbProviderType.Sqlite)
        {
            Output.WriteLine("Only SQLite rebuilds tables to alter them; nothing to test.");
            return;
        }

        await db.ExecuteAsync("DROP TABLE IF EXISTS rebuild_child");
        await db.ExecuteAsync("DROP TABLE IF EXISTS rebuild_parent");
        await db.ExecuteAsync("CREATE TABLE rebuild_parent (id INTEGER PRIMARY KEY, name TEXT)");
        await db.ExecuteAsync(
            "CREATE TABLE rebuild_child (id INTEGER PRIMARY KEY, parent_id INTEGER REFERENCES rebuild_parent(id) ON DELETE CASCADE)"
        );
        await db.ExecuteAsync("INSERT INTO rebuild_parent (id, name) VALUES (1, 'a')");
        await db.ExecuteAsync("INSERT INTO rebuild_child (id, parent_id) VALUES (10, 1), (11, 1)");
        await db.ExecuteAsync("PRAGMA foreign_keys = 1");

        async Task<long> ChildRowsAsync() => await db.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM rebuild_child");

        // Inside a transaction, rebuilding a referenced table is refused, and nothing is lost.
        using (var tx = db.BeginTransaction())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                db.CreateColumnIfNotExistsAsync(null, "rebuild_parent", "extra", typeof(string), isNullable: true, tx: tx)
            );
            Output.WriteLine(ex.Message);

            // Our refusal, not some other InvalidOperationException from the driver.
            Assert.Contains("rebuild_child", ex.Message, StringComparison.Ordinal);
            tx.Rollback();
        }

        Assert.Equal(2, await ChildRowsAsync());
        Assert.False(await db.DoesColumnExistAsync(null, "rebuild_parent", "extra"));

        // A table that nothing else references is safe to rebuild inside a transaction.
        using (var tx = db.BeginTransaction())
        {
            Assert.True(
                await db.CreateColumnIfNotExistsAsync(null, "rebuild_child", "note", typeof(string), isNullable: true, tx: tx)
            );
            tx.Commit();
        }

        Assert.Equal(2, await ChildRowsAsync());

        // Outside a transaction the rebuild runs with foreign keys off, keeps the child rows, and leaves the
        // connection's setting as it found it.
        Assert.True(
            await db.CreateColumnIfNotExistsAsync(null, "rebuild_parent", "extra", typeof(string), isNullable: true)
        );
        Assert.Equal(2, await ChildRowsAsync());
        Assert.Equal(1, await db.ExecuteScalarAsync<long>("PRAGMA foreign_keys"));

        // A connection with foreign keys off keeps them off, rather than having them forced on.
        await db.ExecuteAsync("PRAGMA foreign_keys = 0");
        Assert.True(
            await db.CreateColumnIfNotExistsAsync(null, "rebuild_parent", "extra2", typeof(string), isNullable: true)
        );
        Assert.Equal(0, await db.ExecuteScalarAsync<long>("PRAGMA foreign_keys"));

        await db.ExecuteAsync("DROP TABLE IF EXISTS rebuild_child");
        await db.ExecuteAsync("DROP TABLE IF EXISTS rebuild_parent");
    }

    /// <summary>
    /// Renaming a column must keep everything else about it. MySQL before 8.0 has no RENAME COLUMN, so the
    /// column is restated with CHANGE. That restatement was rebuilt from INFORMATION_SCHEMA, which dropped the
    /// collation, a generated column's expression and ON UPDATE, and failed outright on a string default,
    /// which INFORMATION_SCHEMA reports unquoted.
    /// </summary>
    [Fact]
    protected virtual async Task Rename_column_keeps_the_rest_of_the_column_on_mysql_Async()
    {
        using var db = await OpenConnectionAsync();
        if (db.GetDbProviderType() != DbProviderType.MySql)
        {
            Output.WriteLine("This test covers the MySQL and MariaDB rename paths only.");
            return;
        }

        const string tableName = "rename_keeps_column";
        await db.DropTableIfExistsAsync(null, tableName);
        await db.ExecuteAsync(
            $"""
            CREATE TABLE {tableName} (
                id INT NOT NULL PRIMARY KEY,
                title VARCHAR(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL DEFAULT 'abc' COMMENT 'the title',
                label VARCHAR(50) NULL,
                label_upper VARCHAR(50) GENERATED ALWAYS AS (UPPER(label)) VIRTUAL,
                updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
            )
            """
        );

        async Task<(string?, string?, string?, string?, string?, string?, string?)> DescribeAsync(string column) =>
            await db.QuerySingleAsync<(string?, string?, string?, string?, string?, string?, string?)>(
                """
                SELECT COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT, EXTRA, COLUMN_COMMENT, COLLATION_NAME, GENERATION_EXPRESSION
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName AND COLUMN_NAME = @column
                """,
                new { tableName, column }
            );

        var renames = new[] { ("title", "heading"), ("label_upper", "label_caps"), ("updated_at", "modified_at") };

        foreach (var (from, to) in renames)
        {
            var before = await DescribeAsync(from);
            Output.WriteLine("{0}: {1}", from, before);

            Assert.True(await db.RenameColumnIfExistsAsync(null, tableName, from, to));

            Assert.Equal(before, await DescribeAsync(to));
        }

        await db.DropTableIfExistsAsync(null, tableName);
    }
}
