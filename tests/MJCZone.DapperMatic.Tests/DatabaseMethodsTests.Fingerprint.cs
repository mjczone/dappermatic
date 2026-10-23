// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Tests;

public abstract partial class DatabaseMethodsTests
{
    /// <summary>
    /// The library writes normalized names back onto the models it creates tables from. A fingerprint that
    /// moved when that happened would differ between a process that has created the table and one that has not,
    /// so a migrator serving several databases from one process would reconcile them on every run.
    /// </summary>
    [Fact]
    protected virtual async Task Creating_and_reconciling_a_table_leaves_its_fingerprint_unchanged_Async()
    {
        using var db = await OpenConnectionAsync();
        await db.DropTableIfExistsAsync(null, "fp_child");
        await db.DropTableIfExistsAsync(null, "fp_parent");

        var parent = new DmTable(
            null,
            "fp_parent",
            [new DmColumn(null, "fp_parent", "id", typeof(int), isPrimaryKey: true)]
        );

        foreach (var model in new[] { parent, FingerprintChild(withNote: false) })
        {
            var before = DmTableFingerprint.GetCanonicalForm(model);

            await db.CreateTableIfNotExistsAsync(model);
            await db.ReconcileTableAsync(model, applySafeAlterations: true);
            await db.GetTableReconciliationPlanAsync(model, applySafeAlterations: true);

            Assert.Equal(before, DmTableFingerprint.GetCanonicalForm(model));
        }

        // Adding to an existing table goes through the column and index methods instead.
        var grown = FingerprintChild(withNote: true);
        var grownBefore = DmTableFingerprint.GetCanonicalForm(grown);

        var result = await db.ReconcileTableAsync(grown, applySafeAlterations: true);

        Assert.Contains(result.Applied, a => a.ElementType == DmSchemaElementType.Column && a.Name == "note");
        Assert.Equal(grownBefore, DmTableFingerprint.GetCanonicalForm(grown));

        await db.DropTableIfExistsAsync(null, "fp_child");
        await db.DropTableIfExistsAsync(null, "fp_parent");
    }

    private static DmTable FingerprintChild(bool withNote)
    {
        var columns = new List<DmColumn>
        {
            new(null, "fp_child", "id", typeof(int), isPrimaryKey: true),
            new(null, "fp_child", "code", typeof(string), length: 20),
            new(null, "fp_child", "qty", typeof(int)),
            new(null, "fp_child", "parent_id", typeof(int)),
        };

        var indexes = new List<DmIndex> { new(null, "fp_child", "ix_fp_child_qty", [new DmOrderedColumn("qty")]) };

        if (withNote)
        {
            columns.Add(new DmColumn(null, "fp_child", "note", typeof(string), length: 50, isNullable: true));
            indexes.Add(new DmIndex(null, "fp_child", "ix_fp_child_note", [new DmOrderedColumn("note")]));
        }

        return new DmTable(
            null,
            "fp_child",
            [.. columns],
            new DmPrimaryKeyConstraint(null, "fp_child", "pk_fp_child", [new DmOrderedColumn("id")]),
            [new DmCheckConstraint(null, "fp_child", "qty", "ck_fp_child_qty", "qty >= 0")],
            [new DmDefaultConstraint(null, "fp_child", "qty", "df_fp_child_qty", "0")],
            [new DmUniqueConstraint(null, "fp_child", "uc_fp_child_code", [new DmOrderedColumn("code")])],
            [
                new DmForeignKeyConstraint(
                    null,
                    "fp_child",
                    "fk_fp_child_parent",
                    [new DmOrderedColumn("parent_id")],
                    "fp_parent",
                    [new DmOrderedColumn("id")]
                ),
            ],
            [.. indexes]
        );
    }
}
