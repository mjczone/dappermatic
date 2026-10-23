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
    /// Opens a connection that may create databases on the server, for providers whose databases share one catalog
    /// on a server.
    /// </summary>
    /// <returns>The connection, or null where the provider has no such databases.</returns>
    public virtual Task<IDbConnection?> OpenServerAdminConnectionAsync() => Task.FromResult<IDbConnection?>(null);

    // A PostgreSQL partitioned table (relkind 'p', not 'r') was invisible: it did not exist, was not listed or read,
    // and creating it again failed with "already exists" (#26).
    [Fact]
    protected virtual async Task Partitioned_tables_are_seen_as_tables_Async()
    {
        using var db = await OpenConnectionAsync();
        if (db.GetDbProviderType() != DbProviderType.PostgreSql)
        {
            return;
        }

        await InitFreshSchemaAsync(db, null);
        await db.ExecuteAsync("DROP TABLE IF EXISTS dm_part CASCADE");
        await db.ExecuteAsync("CREATE TABLE dm_part (id int NOT NULL, created date NOT NULL, note varchar(20), PRIMARY KEY (id, created)) PARTITION BY RANGE (created)");
        await db.ExecuteAsync("CREATE TABLE dm_part_2026 PARTITION OF dm_part FOR VALUES FROM ('2026-01-01') TO ('2027-01-01')");
        await db.ExecuteAsync("CREATE INDEX ix_dm_part_note ON dm_part (note)");

        Assert.True(await db.DoesTableExistAsync(null, "dm_part"));
        Assert.Contains("dm_part", await db.GetTableNamesAsync(null), StringComparer.OrdinalIgnoreCase);
        var table = (await db.GetTableAsync(null, "dm_part"))!;
        Assert.Equal(["id", "created", "note"], table.Columns.Select(c => c.ColumnName.ToLowerInvariant()));
        Assert.Equal(["id", "created"], table.PrimaryKeyConstraint!.Columns.Select(c => c.ColumnName.ToLowerInvariant()));
        Assert.Contains(table.Indexes, i => i.IndexName.Equals("ix_dm_part_note", StringComparison.OrdinalIgnoreCase));
        Assert.False(await db.CreateTableIfNotExistsAsync(table));

        await db.ExecuteAsync("DROP TABLE IF EXISTS dm_part CASCADE");
    }

    // Two tables' checks of one name, where names are per table (MariaDB, PostgreSQL, SQLite; SQL Server and MySQL
    // keep them per schema and refuse the second). MariaDB's checks were joined by name alone: each table read back
    // both.
    [Fact]
    protected virtual async Task Checks_of_one_name_on_two_tables_read_back_apart_Async()
    {
        using var db = await OpenConnectionAsync();
        var provider = db.GetDbProviderType();
        var perTable = provider is DbProviderType.PostgreSql or DbProviderType.Sqlite
            || (provider == DbProviderType.MySql && (await db.ExecuteScalarAsync<string>("SELECT VERSION()"))!.Contains("MariaDB", StringComparison.OrdinalIgnoreCase));
        if (!perTable)
        {
            return;
        }

        await InitFreshSchemaAsync(db, null);
        foreach (var name in new[] { "dm_ck_a", "dm_ck_b" })
        {
            await db.DropTableIfExistsAsync(null, name);
        }

        await db.ExecuteAsync("CREATE TABLE dm_ck_a (id int PRIMARY KEY, amount int, CONSTRAINT ck_same CHECK (amount >= 0))");
        await db.ExecuteAsync("CREATE TABLE dm_ck_b (id int PRIMARY KEY, qty int, CONSTRAINT ck_same CHECK (qty < 100))");

        var a = (await db.GetTableAsync(null, "dm_ck_a"))!;
        var b = (await db.GetTableAsync(null, "dm_ck_b"))!;
        Assert.Contains("amount", Assert.Single(a.CheckConstraints).Expression, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("qty", Assert.Single(b.CheckConstraints).Expression, StringComparison.OrdinalIgnoreCase);

        foreach (var name in new[] { "dm_ck_a", "dm_ck_b" })
        {
            await db.DropTableIfExistsAsync(null, name);
        }
    }

    // Composite keys, in two schemas that repeat every name, as a schema per tenant does. SQL Server joined its
    // catalog by constraint name alone: each primary key column came back once per schema, and a two-column foreign
    // key as 16 pairs.
    [Fact]
    protected virtual async Task Tables_read_back_as_created_beside_a_schema_with_the_same_names_Async()
    {
        using var db = await OpenConnectionAsync();
        string?[] schemas = db.SupportsSchemas() ? ["dm_rb_a", "dm_rb_b"] : [null];
        foreach (var schema in schemas)
        {
            await InitFreshSchemaAsync(db, schema);
            await db.DropTableIfExistsAsync(schema, "dm_rb_child");
            await db.DropTableIfExistsAsync(schema, "dm_rb_parent");
            await db.CreateTableIfNotExistsAsync(
                new DmTable(
                    schema,
                    "dm_rb_parent",
                    [new DmColumn("a", typeof(int)), new DmColumn("b", typeof(int)), new DmColumn("name", typeof(string), length: 20)],
                    primaryKey: new DmPrimaryKeyConstraint(schema, "dm_rb_parent", "pk_dm_rb_parent", [new("a"), new("b")])
                )
            );
            await db.CreateTableIfNotExistsAsync(
                new DmTable(
                    schema,
                    "dm_rb_child",
                    [new DmColumn("id", typeof(int), isPrimaryKey: true), new DmColumn("pa", typeof(int)), new DmColumn("pb", typeof(int))],
                    foreignKeyConstraints:
                    [
                        new DmForeignKeyConstraint(
                            schema,
                            "dm_rb_child",
                            "fk_dm_rb_child_parent",
                            [new("pa"), new("pb")],
                            "dm_rb_parent",
                            [new("a"), new("b")],
                            onDelete: DmForeignKeyAction.Cascade
                        ),
                    ]
                )
            );
        }

        foreach (var schema in schemas)
        {
            var parent = (await db.GetTableAsync(schema, "dm_rb_parent"))!;
            Assert.Equal(["a", "b", "name"], parent.Columns.Select(c => c.ColumnName));
            Assert.Equal(["a", "b"], parent.Columns.Where(c => c.IsPrimaryKey).Select(c => c.ColumnName));
            Assert.Equal(["a", "b"], parent.PrimaryKeyConstraint!.Columns.Select(c => c.ColumnName));

            var child = (await db.GetTableAsync(schema, "dm_rb_child"))!;
            Assert.Equal(["id", "pa", "pb"], child.Columns.Select(c => c.ColumnName));
            var fk = Assert.Single(child.ForeignKeyConstraints);
            Assert.Equal(["pa", "pb"], fk.SourceColumns.Select(c => c.ColumnName));
            Assert.Equal(["a", "b"], fk.ReferencedColumns.Select(c => c.ColumnName));
            Assert.Equal("dm_rb_parent", fk.ReferencedTableName, ignoreCase: true);
            Assert.Equal(DmForeignKeyAction.Cascade, fk.OnDelete);
        }

        foreach (var schema in schemas)
        {
            await db.DropTableIfExistsAsync(schema, "dm_rb_child");
            await db.DropTableIfExistsAsync(schema, "dm_rb_parent");
            if (schema is not null)
            {
                await db.DropSchemaIfExistsAsync(schema);
            }
        }
    }

    // A second database on the server with tables and constraints of the same names, as a database per tenant has on
    // MySQL: what is read back is this database's alone. MySQL joined the referential and check constraint views by
    // constraint name, without the database.
    [Fact]
    protected virtual async Task Tables_read_back_from_their_own_database_only_Async()
    {
        using var admin = await OpenServerAdminConnectionAsync();
        if (admin is null)
        {
            Output.WriteLine("Only for providers whose databases share one catalog on a server.");
            return;
        }

        using var db = await OpenConnectionAsync();
        var database = await db.ExecuteScalarAsync<string>("SELECT DATABASE()");
        var checks = await db.SupportsCheckConstraintsAsync();
        const string twin = "dm_rb_twin";
        await admin.ExecuteAsync($"DROP DATABASE IF EXISTS {twin}");
        await admin.ExecuteAsync($"CREATE DATABASE {twin}");

        // The catalog shows a user only what it may use, and an application's user may use every tenant's database.
        var user = (await db.ExecuteScalarAsync<string>("SELECT CURRENT_USER()"))!.Split('@');
        await admin.ExecuteAsync($"GRANT SELECT ON {twin}.* TO '{user[0]}'@'{user[1]}'");
        foreach (var (name, onDelete, minimum) in new[] { (database!, "CASCADE", 0), (twin, "SET NULL", 5) })
        {
            await admin.ExecuteAsync($"DROP TABLE IF EXISTS `{name}`.dm_rb_child");
            await admin.ExecuteAsync($"DROP TABLE IF EXISTS `{name}`.dm_rb_parent");
            await admin.ExecuteAsync($"CREATE TABLE `{name}`.dm_rb_parent (id int NOT NULL PRIMARY KEY)");
            var check = checks ? $", CONSTRAINT ck_dm_rb_child_id CHECK (id > {minimum})" : string.Empty;
            await admin.ExecuteAsync(
                $"CREATE TABLE `{name}`.dm_rb_child (id int NOT NULL PRIMARY KEY, parent_id int NULL, CONSTRAINT fk_dm_rb_child_parent "
                    + $"FOREIGN KEY (parent_id) REFERENCES `{name}`.dm_rb_parent (id) ON DELETE {onDelete}{check})"
            );
        }

        var child = (await db.GetTableAsync(null, "dm_rb_child"))!;
        var fk = Assert.Single(child.ForeignKeyConstraints);
        Assert.Equal(DmForeignKeyAction.Cascade, fk.OnDelete);
        Assert.Equal(["parent_id"], fk.SourceColumns.Select(c => c.ColumnName));
        if (checks)
        {
            var expression = Assert.Single(child.CheckConstraints).Expression;
            Assert.Contains("0", expression, StringComparison.Ordinal);
            Assert.DoesNotContain("5", expression, StringComparison.Ordinal);
        }

        await admin.ExecuteAsync($"DROP DATABASE {twin}");
        await db.DropTableIfExistsAsync(null, "dm_rb_child");
        await db.DropTableIfExistsAsync(null, "dm_rb_parent");
    }
}
