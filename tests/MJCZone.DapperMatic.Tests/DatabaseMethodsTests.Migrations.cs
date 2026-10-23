// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using Dapper;
using MJCZone.DapperMatic.Migrations;
using MJCZone.DapperMatic.Migrations.Internal;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Tests;

public abstract partial class DatabaseMethodsTests
{
    /// <summary>
    /// The point of the migrator: an unchanged model costs one ledger read and no lock, and a changed model
    /// reconciles that table and no other.
    /// </summary>
    [Fact]
    protected virtual async Task Migrator_reconciles_only_tables_whose_model_changed_Async()
    {
        using var db = await OpenConnectionAsync();
        await ResetMigrationTablesAsync(db, "mg_ledger_changed", "mg_a", "mg_b");

        var a1 = MgTable("mg_a", MgId("mg_a"), MgText("mg_a", "name"));
        var b = MgTable("mg_b", MgId("mg_b"));
        var events = new List<DmMigrationEvent>();

        var first = await Migrator("mg_ledger_changed", events.Add, a1, b).MigrateAsync(db);
        Assert.True(first.LedgerCreated);
        Assert.True(first.LockTaken);
        Assert.All(first.Tables, t => Assert.Equal(DmTableMigrationOutcome.Reconciled, t.Outcome));
        Assert.All(first.Tables, t => Assert.Equal(DmTableStatus.NotRecorded, t.State.Status));
        Assert.All(first.Tables, t => Assert.True(t.Reconciliation!.TableCreated));
        Assert.Contains(events, e => e.Kind == DmMigrationEventKind.LedgerCreated);

        var plan = await Migrator("mg_ledger_changed", null, a1, b).GetPlanAsync(db);
        Assert.True(plan.LedgerExists);
        Assert.False(plan.HasChanges);

        var second = await Migrator("mg_ledger_changed", null, a1, b).MigrateAsync(db);
        Assert.False(second.LockTaken);
        Assert.All(second.Tables, t => Assert.Equal(DmTableMigrationOutcome.Unchanged, t.Outcome));
        Assert.All(second.Tables, t => Assert.Null(t.Reconciliation));

        var a2 = MgTable("mg_a", MgId("mg_a"), MgText("mg_a", "name"), MgText("mg_a", "note"));
        var changedPlan = await Migrator("mg_ledger_changed", null, a2, b).GetPlanAsync(db);
        Assert.Equal("mg_a", Assert.Single(changedPlan.ChangedTables).Model.TableName);

        // SQL Server runs read under an isolation level of their own; the connection keeps the caller's.
        var sqlServer = db.GetDbProviderType() == DbProviderType.SqlServer;
        if (sqlServer)
        {
            await db.ExecuteAsync("SET TRANSACTION ISOLATION LEVEL SERIALIZABLE");
        }

        events.Clear();
        var third = await Migrator("mg_ledger_changed", events.Add, a2, b).MigrateAsync(db);
        if (sqlServer)
        {
            const string level = "SELECT transaction_isolation_level FROM sys.dm_exec_sessions WHERE session_id = @@SPID";
            Assert.Equal(4, await db.ExecuteScalarAsync<int>(level));
            await db.ExecuteAsync("SET TRANSACTION ISOLATION LEVEL READ COMMITTED");
        }

        var reconciled = Assert.Single(third.Reconciled);
        Assert.Equal("mg_a", reconciled.State.Model.TableName);
        Assert.Equal(DmTableStatus.ModelChanged, reconciled.State.Status);
        Assert.Contains(reconciled.Reconciliation!.Applied, x => x.Name == "note");
        Assert.Equal(DmTableMigrationOutcome.Unchanged, third.Tables.Single(t => t.State.Model.TableName == "mg_b").Outcome);
        Assert.True(await db.DoesColumnExistAsync(null, "mg_a", "note"));
        Assert.Contains(events, e => e.Kind == DmMigrationEventKind.TableReconciled && e.TableKey!.EndsWith("mg_a"));

        var fourth = await Migrator("mg_ledger_changed", null, a2, b).MigrateAsync(db);
        Assert.False(fourth.LockTaken);
        Assert.All(fourth.Tables, t => Assert.Equal(DmTableMigrationOutcome.Unchanged, t.Outcome));

        // Force reconciles everything, finds nothing, and records the same fingerprints again.
        var forced = await Migrator("mg_ledger_changed", null, a2, b).MigrateAsync(db, force: true);
        Assert.All(forced.Tables, t => Assert.Equal(DmTableStatus.Forced, t.State.Status));
        Assert.All(forced.Tables, t => Assert.False(t.Reconciliation!.Changed));

        await ResetMigrationTablesAsync(db, "mg_ledger_changed", "mg_a", "mg_b");
    }

    /// <summary>
    /// Foreign keys go on after every table in the run exists, so tables can reference each other in any
    /// registration order, in a cycle, and through a column added in the same run.
    /// </summary>
    [Fact]
    protected virtual async Task Migrator_adds_foreign_keys_after_every_table_Async()
    {
        using var db = await OpenConnectionAsync();
        await ResetMigrationTablesAsync(db, "mg_ledger_fk", "mg_pet", "mg_child", "mg_parent", "mg_owner");

        // A cycle, with the referencing table registered first.
        var parent = MgTable(
            "mg_parent",
            [MgId("mg_parent"), MgInt("mg_parent", "favourite_child_id", nullable: true)],
            MgForeignKey("mg_parent", "fk_mg_parent_child", "favourite_child_id", "mg_child")
        );
        var child = MgTable(
            "mg_child",
            [MgId("mg_child"), MgInt("mg_child", "parent_id")],
            MgForeignKey("mg_child", "fk_mg_child_parent", "parent_id", "mg_parent")
        );
        var owner = MgTable("mg_owner", MgId("mg_owner"));

        var first = await Migrator("mg_ledger_fk", null, child, parent, owner).MigrateAsync(db);
        Assert.True(first.IsComplete);
        Assert.True(await db.DoesForeignKeyConstraintExistAsync(null, "mg_parent", "fk_mg_parent_child"));
        Assert.True(await db.DoesForeignKeyConstraintExistAsync(null, "mg_child", "fk_mg_child_parent"));

        // An existing table gains a column referencing a table this same run creates, which is registered after it.
        var owner2 = MgTable(
            "mg_owner",
            [MgId("mg_owner"), MgInt("mg_owner", "best_pet_id", nullable: true)],
            MgForeignKey("mg_owner", "fk_mg_owner_pet", "best_pet_id", "mg_pet")
        );
        var pet = MgTable(
            "mg_pet",
            [MgId("mg_pet"), MgInt("mg_pet", "owner_id")],
            MgForeignKey("mg_pet", "fk_mg_pet_owner", "owner_id", "mg_owner")
        );

        var second = await Migrator("mg_ledger_fk", null, child, parent, owner2, pet).MigrateAsync(db);
        Assert.True(second.IsComplete);
        Assert.Equal(new[] { "mg_owner", "mg_pet" }, second.Reconciled.Select(t => t.State.Model.TableName));
        Assert.True(await db.DoesForeignKeyConstraintExistAsync(null, "mg_owner", "fk_mg_owner_pet"));
        Assert.True(await db.DoesForeignKeyConstraintExistAsync(null, "mg_pet", "fk_mg_pet_owner"));

        // Each foreign key exists once, under the model's name: a column added with its foreign key flag would
        // have made a second one under a generated name.
        var ownerFks = await db.GetForeignKeyConstraintsAsync(null, "mg_owner");
        Assert.Single(ownerFks);

        var third = await Migrator("mg_ledger_fk", null, child, parent, owner2, pet).MigrateAsync(db);
        Assert.False(third.LockTaken);

        await ResetMigrationTablesAsync(db, "mg_ledger_fk", "mg_pet", "mg_child", "mg_parent", "mg_owner");
    }

    /// <summary>
    /// A foreign key declared only on its column, as <c>[DmColumn(isForeignKey: true, ...)]</c> declares it, goes on
    /// after every table too: created with its column, it would reference a table the run has not created yet.
    /// </summary>
    [Fact]
    protected virtual async Task Migrator_adds_a_column_foreign_key_after_every_table_Async()
    {
        using var db = await OpenConnectionAsync();
        await ResetMigrationTablesAsync(db, "mg_ledger_column_fk", "mg_cfk_order", "mg_cfk_customer");

        var order = MgTable(
            "mg_cfk_order",
            MgId("mg_cfk_order"),
            new DmColumn(
                null,
                "mg_cfk_order",
                "customer_id",
                typeof(int),
                isForeignKey: true,
                referencedTableName: "mg_cfk_customer",
                referencedColumnName: "id"
            )
        );
        var customer = MgTable("mg_cfk_customer", MgId("mg_cfk_customer"));

        // The referencing table registered first.
        var first = await Migrator("mg_ledger_column_fk", null, order, customer).MigrateAsync(db);
        Assert.True(first.IsComplete);
        var fk = Assert.Single(await db.GetForeignKeyConstraintsAsync(null, "mg_cfk_order"));
        Assert.Equal("mg_cfk_customer", fk.ReferencedTableName, ignoreCase: true);
        Assert.True(order.Columns.Single(c => c.ColumnName == "customer_id").IsForeignKey);

        var second = await Migrator("mg_ledger_column_fk", null, order, customer).MigrateAsync(db);
        Assert.False(second.LockTaken);

        await ResetMigrationTablesAsync(db, "mg_ledger_column_fk", "mg_cfk_order", "mg_cfk_customer");
    }

    /// <summary>
    /// Renames are written down when they are made, even when something else keeps the table from being recorded:
    /// the next run finds nothing left to rename, and the previous version's model must still be recognised.
    /// </summary>
    [Fact]
    protected virtual async Task Migrator_records_a_rename_even_when_the_table_stays_pending_Async()
    {
        using var db = await OpenConnectionAsync();
        await ResetMigrationTablesAsync(db, "mg_ledger_pending_rename", "mg_pending");

        var v1 = MgTable("mg_pending", MgId("mg_pending"), MgText("mg_pending", "email"));
        await Migrator("mg_ledger_pending_rename", null, v1).MigrateAsync(db);

        // v2 renames the column, and has an index over a column it does not have, which stays blocked.
        var renamed = MgText("mg_pending", "email_address");
        renamed.PreviousNames = ["email"];
        var v2 = MgTable("mg_pending", MgId("mg_pending"), renamed);
        v2.Indexes = [new DmIndex(null, "mg_pending", "ix_mg_pending_nope", [new DmOrderedColumn("nope")])];

        var deploy = await Migrator("mg_ledger_pending_rename", null, v2).MigrateAsync(db);
        var pending = Assert.Single(deploy.Pending);
        Assert.Single(pending.Reconciliation!.Renamed);

        var old = await Migrator("mg_ledger_pending_rename", null, v1).MigrateAsync(db);
        Assert.Single(old.Superseded);
        Assert.False(await db.DoesColumnExistAsync(null, "mg_pending", "email"));

        await ResetMigrationTablesAsync(db, "mg_ledger_pending_rename", "mg_pending");
    }

    /// <summary>
    /// A table left with something blocked is not recorded, so it is tried again on every run until fixed, while
    /// the other tables in the run are recorded as normal.
    /// </summary>
    [Fact]
    protected virtual async Task Migrator_does_not_record_a_table_left_blocked_Async()
    {
        using var db = await OpenConnectionAsync();
        await ResetMigrationTablesAsync(db, "mg_ledger_blocked", "mg_fine", "mg_new", "mg_old1", "mg_old2");

        // The table exists under both of its previous names, which reconciliation will not choose between.
        await db.CreateTableIfNotExistsAsync(MgTable("mg_old1", MgId("mg_old1")));
        await db.CreateTableIfNotExistsAsync(MgTable("mg_old2", MgId("mg_old2")));
        var ambiguous = MgTable("mg_new", MgId("mg_new"));
        ambiguous.PreviousNames = ["mg_old1", "mg_old2"];
        var fine = MgTable("mg_fine", MgId("mg_fine"));

        var events = new List<DmMigrationEvent>();
        var first = await Migrator("mg_ledger_blocked", events.Add, fine, ambiguous).MigrateAsync(db);
        Assert.False(first.IsComplete);
        var pending = Assert.Single(first.Pending);
        Assert.Equal("mg_new", pending.State.Model.TableName);
        Assert.Contains(pending.Reconciliation!.Blocked, b => b.ElementType == DmSchemaElementType.Table);
        Assert.Equal(DmTableMigrationOutcome.Reconciled, first.Tables.Single(t => t.State.Model.TableName == "mg_fine").Outcome);
        Assert.Contains(events, e => e.Kind == DmMigrationEventKind.TablePending && e.Level == DmMigrationEventLevel.Warning);

        // Still pending next time, and the run takes the lock again to retry it.
        var second = await Migrator("mg_ledger_blocked", null, fine, ambiguous).MigrateAsync(db);
        Assert.True(second.LockTaken);
        Assert.Equal(DmTableStatus.NotRecorded, Assert.Single(second.Pending).State.Status);

        var options = new DmMigrationOptions { TableStateTableName = "mg_ledger_blocked", ThrowOnBlocked = true };
        var ex = await Assert.ThrowsAsync<DmMigrationException>(() =>
            new DmMigrator(options).AddTable(fine).AddTable(ambiguous).MigrateAsync(db)
        );
        Assert.Single(ex.Result!.Pending);

        // Resolved by hand, it is reconciled and recorded.
        await db.DropTableIfExistsAsync(null, "mg_old2");
        var third = await Migrator("mg_ledger_blocked", null, fine, ambiguous).MigrateAsync(db);
        Assert.True(third.IsComplete);
        Assert.True(await db.DoesTableExistAsync(null, "mg_new"));

        await ResetMigrationTablesAsync(db, "mg_ledger_blocked", "mg_fine", "mg_new", "mg_old1", "mg_old2");
    }

    /// <summary>
    /// During a deploy the previous version of an application runs beside the new one. Its model still has the
    /// column the new model renamed; reconciling it would add that column back, empty. The rename history
    /// recognises the old model and leaves the table alone.
    /// </summary>
    [Fact]
    protected virtual async Task Migrator_leaves_alone_a_model_a_column_rename_superseded_Async()
    {
        using var db = await OpenConnectionAsync();
        await ResetMigrationTablesAsync(db, "mg_ledger_superseded", "mg_person");

        var v1 = MgTable("mg_person", MgId("mg_person"), MgText("mg_person", "email"));
        await Migrator("mg_ledger_superseded", null, v1).MigrateAsync(db);
        await db.ExecuteAsync("INSERT INTO mg_person (id, email) VALUES (1, 'a@example.com')");

        var renamed = MgText("mg_person", "email_address");
        renamed.PreviousNames = ["email"];
        var v2 = MgTable("mg_person", MgId("mg_person"), renamed);

        var deploy = await Migrator("mg_ledger_superseded", null, v2).MigrateAsync(db);
        var rename = Assert.Single(Assert.Single(deploy.Reconciled).Reconciliation!.Renamed);
        Assert.Equal("email", rename.OldName);
        Assert.Equal(
            "a@example.com",
            await db.ExecuteScalarAsync<string>("SELECT email_address FROM mg_person WHERE id = 1")
        );

        var events = new List<DmMigrationEvent>();
        var old = await Migrator("mg_ledger_superseded", events.Add, v1).MigrateAsync(db);
        var superseded = Assert.Single(old.Superseded);
        Assert.Contains("email", superseded.State.SupersededBy);
        Assert.False(old.LockTaken);
        Assert.True(old.IsComplete);
        Assert.False(await db.DoesColumnExistAsync(null, "mg_person", "email"));
        Assert.Contains(events, e => e.Kind == DmMigrationEventKind.TableSuperseded);

        var current = await Migrator("mg_ledger_superseded", null, v2).MigrateAsync(db);
        Assert.Equal(DmTableMigrationOutcome.Unchanged, Assert.Single(current.Tables).Outcome);

        await ResetMigrationTablesAsync(db, "mg_ledger_superseded", "mg_person");
    }

    /// <summary>
    /// The same for a renamed table: the old model's ledger row stays, so the old version does not create the
    /// table again under its old name.
    /// </summary>
    [Fact]
    protected virtual async Task Migrator_leaves_alone_a_model_a_table_rename_superseded_Async()
    {
        using var db = await OpenConnectionAsync();
        await ResetMigrationTablesAsync(db, "mg_ledger_table_rename", "mg_order", "mg_purchase");

        var v1 = MgTable("mg_order", MgId("mg_order"), MgText("mg_order", "item"));
        await Migrator("mg_ledger_table_rename", null, v1).MigrateAsync(db);
        await db.ExecuteAsync("INSERT INTO mg_order (id, item) VALUES (1, 'tea')");

        var v2 = MgTable("mg_purchase", MgId("mg_purchase"), MgText("mg_purchase", "item"));
        v2.PreviousNames = ["mg_order"];

        var deploy = await Migrator("mg_ledger_table_rename", null, v2).MigrateAsync(db);
        Assert.Contains(
            Assert.Single(deploy.Reconciled).Reconciliation!.Renamed,
            r => r.ElementType == DmSchemaElementType.Table
        );
        Assert.Equal("tea", await db.ExecuteScalarAsync<string>("SELECT item FROM mg_purchase WHERE id = 1"));

        var old = await Migrator("mg_ledger_table_rename", null, v1).MigrateAsync(db);
        Assert.Single(old.Superseded);
        Assert.False(await db.DoesTableExistAsync(null, "mg_order"));

        await ResetMigrationTablesAsync(db, "mg_ledger_table_rename", "mg_order", "mg_purchase");
    }

    /// <summary>
    /// Two instances starting at once: one migrates, the other waits for it, reads the ledger again under the
    /// lock and finds nothing left to do.
    /// </summary>
    [Fact]
    protected virtual async Task Migrators_on_two_connections_take_turns_Async()
    {
        using var db = await OpenConnectionAsync();
        await ResetMigrationTablesAsync(db, "mg_ledger_turns", "mg_turns");
        using var other = await OpenAnotherConnectionAsync();

        var table = MgTable("mg_turns", MgId("mg_turns"), MgText("mg_turns", "name"));
        var results = await Task.WhenAll(
            Task.Run(() => Migrator("mg_ledger_turns", null, table).MigrateAsync(db)),
            Task.Run(() => Migrator("mg_ledger_turns", null, table).MigrateAsync(other))
        );

        Assert.Single(results, r => r.Reconciled.Count == 1);
        Assert.Single(results, r => r.Tables.Single().Outcome == DmTableMigrationOutcome.Unchanged);
        Assert.Equal(1, results.Count(r => r.LedgerCreated));

        await ResetMigrationTablesAsync(db, "mg_ledger_turns", "mg_turns");
    }

    /// <summary>
    /// An instance that cannot get the lock in time gives up with a timeout instead of waiting forever.
    /// </summary>
    [Fact]
    protected virtual async Task Migrator_gives_up_waiting_for_the_lock_Async()
    {
        using var db = await OpenConnectionAsync();

        // On SQLite the lock is the write transaction, so the wait is the driver's busy timeout instead.
        if (db.GetDbProviderType() == DbProviderType.Sqlite)
        {
            return;
        }

        await ResetMigrationTablesAsync(db, "mg_ledger_timeout", "mg_timeout");
        using var holder = await OpenAnotherConnectionAsync();
        using var holderTx = holder.GetDbProviderType() == DbProviderType.MySql ? null : holder.BeginTransaction();
        var release = await MigrationLock.AcquireAsync(
            holder,
            holderTx,
            MigrationLock.GetName("mg-timeout-lock"),
            TimeSpan.FromSeconds(5),
            () => { },
            CancellationToken.None
        );

        var events = new List<DmMigrationEvent>();
        var options = new DmMigrationOptions
        {
            TableStateTableName = "mg_ledger_timeout",
            LockName = "mg-timeout-lock",
            LockTimeout = TimeSpan.FromSeconds(1),
            Log = events.Add,
        };
        await Assert.ThrowsAsync<TimeoutException>(() =>
            new DmMigrator(options).AddTable(MgTable("mg_timeout", MgId("mg_timeout"))).MigrateAsync(db)
        );
        Assert.Contains(events, e => e.Kind == DmMigrationEventKind.WaitingForLock);

        // Nothing was done: the ledger the run would have created was rolled back, or never created.
        if (release is not null)
        {
            await release();
        }

        holderTx?.Rollback();
        Assert.False(await db.DoesTableExistAsync(null, "mg_timeout"));

        await ResetMigrationTablesAsync(db, "mg_ledger_timeout", "mg_timeout");
    }

    /// <summary>
    /// A run that throws part-way leaves nothing behind where DDL is transactional, and on MySQL leaves the
    /// tables it reached unrecorded, so the next run reconciles them again.
    /// </summary>
    [Fact]
    protected virtual async Task Migrator_failing_part_way_records_nothing_Async()
    {
        using var db = await OpenConnectionAsync();
        await ResetMigrationTablesAsync(db, "mg_ledger_failure", "mg_good", "mg_bad");

        var good = MgTable("mg_good", MgId("mg_good"));
        var bad = MgTable(
            "mg_bad",
            MgId("mg_bad"),
            new DmColumn(null, "mg_bad", "qty", typeof(int)) { DefaultExpression = "0; DROP TABLE mg_good" }
        );

        await Assert.ThrowsAnyAsync<Exception>(() => Migrator("mg_ledger_failure", null, good, bad).MigrateAsync(db));

        if (db.GetDbProviderType() == DbProviderType.MySql)
        {
            Assert.True(await db.DoesTableExistAsync(null, "mg_good"));
            var plan = await Migrator("mg_ledger_failure", null, good).GetPlanAsync(db);
            Assert.Equal(DmTableStatus.NotRecorded, Assert.Single(plan.Tables).Status);
        }
        else
        {
            Assert.False(await db.DoesTableExistAsync(null, "mg_good"));
            Assert.False(await db.DoesTableExistAsync(null, "mg_ledger_failure"));
        }

        var fixedRun = await Migrator("mg_ledger_failure", null, good).MigrateAsync(db);
        Assert.True(fixedRun.IsComplete);

        await ResetMigrationTablesAsync(db, "mg_ledger_failure", "mg_good", "mg_bad");
    }

    /// <summary>
    /// On SQLite, adding a unique constraint rebuilds the table. The migrator turns foreign keys off for the run,
    /// so a table other tables reference can be rebuilt inside its transaction without touching their rows.
    /// </summary>
    [Fact]
    protected virtual async Task Migrator_rebuilds_a_referenced_table_without_touching_referencing_rows_Async()
    {
        using var db = await OpenConnectionAsync();
        await ResetMigrationTablesAsync(db, "mg_ledger_rebuild", "mg_rb_child", "mg_rb_parent");

        var parent = MgTable("mg_rb_parent", MgId("mg_rb_parent"), MgText("mg_rb_parent", "code"));
        var cascade = MgForeignKey("mg_rb_child", "fk_mg_rb_child_parent", "parent_id", "mg_rb_parent");
        cascade.OnDelete = DmForeignKeyAction.Cascade;
        var child = MgTable("mg_rb_child", [MgId("mg_rb_child"), MgInt("mg_rb_child", "parent_id")], cascade);

        await Migrator("mg_ledger_rebuild", null, parent, child).MigrateAsync(db);
        await db.ExecuteAsync("INSERT INTO mg_rb_parent (id, code) VALUES (1, 'a')");
        await db.ExecuteAsync("INSERT INTO mg_rb_child (id, parent_id) VALUES (1, 1)");
        await db.ExecuteAsync("INSERT INTO mg_rb_child (id, parent_id) VALUES (2, 1)");

        var parent2 = MgTable("mg_rb_parent", MgId("mg_rb_parent"), MgText("mg_rb_parent", "code"));
        parent2.UniqueConstraints.Add(
            new DmUniqueConstraint(null, "mg_rb_parent", "uc_mg_rb_parent_code", [new DmOrderedColumn("code")])
        );

        var result = await Migrator("mg_ledger_rebuild", null, parent2, child).MigrateAsync(db);
        Assert.True(result.IsComplete);
        Assert.True(await db.DoesUniqueConstraintExistAsync(null, "mg_rb_parent", "uc_mg_rb_parent_code"));
        Assert.Equal(2, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mg_rb_child"));

        await ResetMigrationTablesAsync(db, "mg_ledger_rebuild", "mg_rb_child", "mg_rb_parent");
    }

    /// <summary>
    /// The ledger can live in a schema of its own, and its names are refused unless they are plain identifiers,
    /// since they go into SQL the migrator writes.
    /// </summary>
    [Fact]
    protected virtual async Task Migrator_ledger_name_and_schema_are_configurable_Async()
    {
        using var db = await OpenConnectionAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new DmMigrator(new DmMigrationOptions { TableStateTableName = "mg_ledger; DROP TABLE x" }).MigrateAsync(db)
        );

        // Any plain identifier, in any case: the ledger is created, then found again by the next run's fast path.
        await db.DropTableIfExistsAsync(null, "MgLedgerMixedCase");
        await db.DropTableIfExistsAsync(null, "mg_mixed_case");
        var mixedCase = new DmMigrationOptions { TableStateTableName = "MgLedgerMixedCase" };
        Assert.True(
            (await new DmMigrator(mixedCase).AddTable(MgTable("mg_mixed_case", MgId("mg_mixed_case"))).MigrateAsync(db)).LedgerCreated
        );
        var rerun = await new DmMigrator(mixedCase).AddTable(MgTable("mg_mixed_case", MgId("mg_mixed_case"))).MigrateAsync(db);
        Assert.False(rerun.LockTaken);
        Assert.Equal(DmTableMigrationOutcome.Unchanged, rerun.Tables.Single().Outcome);
        await db.DropTableIfExistsAsync(null, "MgLedgerMixedCase");
        await db.DropTableIfExistsAsync(null, "mg_mixed_case");

        if (!db.SupportsSchemas())
        {
            return;
        }

        await db.DropTableIfExistsAsync("mg_ledger_schema", "mg_state");
        await db.DropTableIfExistsAsync(null, "mg_in_default_schema");

        var options = new DmMigrationOptions { SchemaName = "mg_ledger_schema", TableStateTableName = "mg_state" };
        var result = await new DmMigrator(options)
            .AddTable(MgTable("mg_in_default_schema", MgId("mg_in_default_schema")))
            .MigrateAsync(db);

        Assert.True(result.LedgerCreated);
        Assert.True(await db.DoesTableExistAsync("mg_ledger_schema", "mg_state"));
        Assert.False(
            (await new DmMigrator(options).AddTable(MgTable("mg_in_default_schema", MgId("mg_in_default_schema"))).MigrateAsync(db)).LockTaken
        );

        await db.DropTableIfExistsAsync("mg_ledger_schema", "mg_state");
        await db.DropTableIfExistsAsync(null, "mg_in_default_schema");
    }

    private static DmMigrator Migrator(string ledger, Action<DmMigrationEvent>? log, params DmTable[] tables)
    {
        var migrator = new DmMigrator(new DmMigrationOptions { TableStateTableName = ledger, Log = log });
        foreach (var table in tables)
        {
            migrator.AddTable(table);
        }

        return migrator;
    }

    private static DmTable MgTable(string name, params DmColumn[] columns) => new(null, name, columns);

    private static DmTable MgTable(string name, DmColumn[] columns, DmForeignKeyConstraint foreignKey) =>
        new(null, name, columns, foreignKeyConstraints: [foreignKey]);

    private static DmColumn MgId(string table) => new(null, table, "id", typeof(int), isPrimaryKey: true);

    private static DmColumn MgInt(string table, string name, bool nullable = false) =>
        new(null, table, name, typeof(int), isNullable: nullable);

    private static DmColumn MgText(string table, string name) =>
        new(null, table, name, typeof(string), length: 50, isNullable: true);

    private static DmForeignKeyConstraint MgForeignKey(string table, string name, string column, string referenced) =>
        new(null, table, name, [new DmOrderedColumn(column)], referenced, [new DmOrderedColumn("id")]);

    /// <summary>
    /// Drops a test's ledger and tables, dropping the tables' foreign keys first so that tables referencing each
    /// other can go in any order.
    /// </summary>
    private static async Task ResetMigrationTablesAsync(IDbConnection db, string ledger, params string[] tables)
    {
        foreach (var table in tables)
        {
            if (db.GetDbProviderType() != DbProviderType.Sqlite && await db.DoesTableExistAsync(null, table))
            {
                foreach (var fk in await db.GetForeignKeyConstraintsAsync(null, table))
                {
                    await db.DropForeignKeyConstraintIfExistsAsync(null, table, fk.ConstraintName);
                }
            }
        }

        foreach (var table in tables)
        {
            await db.DropTableIfExistsAsync(null, table);
        }

        await db.DropTableIfExistsAsync(null, ledger);
    }
}
