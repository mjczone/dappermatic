// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.DataAnnotations;
using MJCZone.DapperMatic.Migrations;
using MJCZone.DapperMatic.Migrations.Internal;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Tests;

/// <summary>
/// The parts of the migrator that need no database.
/// </summary>
public class DmMigratorTests
{
    [Fact]
    public void Registering_two_models_for_one_table_is_refused()
    {
        var migrator = new DmMigrator().AddTable(new DmTable(null, "orders", [new DmColumn(null, "orders", "id")]));

        Assert.Throws<ArgumentException>(() =>
            migrator.AddTable(new DmTable(null, "ORDERS", [new DmColumn(null, "ORDERS", "id")]))
        );

        // A different schema is a different table.
        migrator.AddTable(new DmTable("sales", "orders", [new DmColumn("sales", "orders", "id")]));
        Assert.Equal(2, migrator.Tables.Count);
    }

    [Fact]
    public void Scanning_an_assembly_registers_its_table_classes_in_name_order()
    {
        var migrator = new DmMigrator().AddTablesFromAssembly(
            typeof(DmMigratorTests).Assembly,
            t => t.Name.StartsWith("MigratorScan", StringComparison.Ordinal)
        );

        Assert.Equal(["migrator_scan_owner", "migrator_scan_target"], migrator.Tables.Select(t => t.TableName).ToArray());
    }

    /// <summary>
    /// The first half of a run adds everything but foreign keys, from a copy of the model. The model itself is
    /// cached by <see cref="DmTableFactory"/> and shared process-wide, so it must come out untouched.
    /// </summary>
    [Fact]
    public void The_first_pass_leaves_out_foreign_keys_without_touching_the_model()
    {
        var model = DmTableFactory.GetTable(typeof(MigratorScanTarget));
        var before = DmTableFingerprint.GetCanonicalForm(model);

        var firstPass = DmMigrator.WithoutForeignKeys(model);

        Assert.Empty(firstPass.ForeignKeyConstraints);
        Assert.False(firstPass.Columns.Single(c => c.ColumnName == "owner_id").IsForeignKey);
        Assert.Equal(before, DmTableFingerprint.GetCanonicalForm(model));
        Assert.True(model.Columns.Single(c => c.ColumnName == "owner_id").IsForeignKey);

        // A model with no foreign keys is used as it is.
        var plain = new DmTable(null, "plain", [new DmColumn(null, "plain", "id")]);
        Assert.Same(plain, DmMigrator.WithoutForeignKeys(plain));
    }

    [Fact]
    public void Rename_history_survives_a_round_trip_and_keeps_only_recent_entries()
    {
        var entries = Enumerable
            .Range(0, RenameHistory.MaxEntries + 5)
            .Select(i => new RenameHistoryEntry(
                i % 2 == 0 ? $"hash{i}" : null,
                new DateTime(2026, 10, 20, 12, 0, i % 60, DateTimeKind.Utc),
                [$"Column 'a{i}' to 'b{i}'", "with \"quotes\" and 'apostrophes'"]
            ))
            .ToList();

        var parsed = RenameHistory.Parse(RenameHistory.Serialize(entries));

        Assert.Equal(RenameHistory.MaxEntries, parsed.Count);
        Assert.Equal(entries[^1].Supersedes, parsed[^1].Supersedes);
        Assert.Equal(entries[^1].At, parsed[^1].At);
        Assert.Equal(entries[^1].Renames, parsed[^1].Renames);
        Assert.Equal(entries[5].Supersedes, parsed[0].Supersedes);
        Assert.Null(RenameHistory.Serialize([]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"supersedes\":\"x\"}")]
    [InlineData("[1, \"two\", null]")]
    public void Unreadable_rename_history_counts_as_none(string? stored)
    {
        Assert.Empty(RenameHistory.Parse(stored));
    }

    [Theory]
    [InlineData("dm_table_state")]
    [InlineData("_ledger2")]
    public void Plain_ledger_names_are_accepted(string name)
    {
        Assert.Equal(name, new TableStateLedger(null, name).TableName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("2ledger")]
    [InlineData("ledger table")]
    [InlineData("ledger; DROP TABLE users")]
    [InlineData("ledger\"")]
    public void Other_ledger_names_are_refused(string name)
    {
        Assert.Throws<ArgumentException>(() => new TableStateLedger(null, name));
        Assert.Throws<ArgumentException>(() => new TableStateLedger(name, "ledger"));
    }

    [Fact]
    public void Lock_names_fit_mysql_and_depend_on_the_seed()
    {
        var name = MigrationLock.GetName("db\n\"public\".\"dm_table_state\"");

        Assert.True(name.Length <= 64);
        Assert.Equal(name, MigrationLock.GetName("DB\n\"PUBLIC\".\"DM_TABLE_STATE\""));
        Assert.NotEqual(name, MigrationLock.GetName("other\n\"public\".\"dm_table_state\""));
    }
}

// Declared before the class it references: registration follows the full name, not the order in the source.
[DmTable(null, "migrator_scan_target")]
public class MigratorScanTarget
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }

    [DmColumn("owner_id")]
    [DmForeignKeyConstraint(referencedTableName: "migrator_scan_owner", referencedColumnNames: new[] { "id" })]
    public int OwnerId { get; set; }
}

[DmTable(null, "migrator_scan_owner")]
public class MigratorScanOwner
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }
}
