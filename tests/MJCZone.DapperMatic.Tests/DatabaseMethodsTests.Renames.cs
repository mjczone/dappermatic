// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Dapper;
using MJCZone.DapperMatic.DataAnnotations;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Tests;

public abstract partial class DatabaseMethodsTests
{
    /// <summary>
    /// Every provider renames indexes and constraints, natively where it can and otherwise by replacing them
    /// with an identical one under the new name -- without losing the rows or what the constraint enforces.
    /// </summary>
    [Fact]
    protected virtual async Task Rename_methods_rename_indexes_and_constraints_Async()
    {
        var parent = DmTableFactory.GetTable(typeof(RenameTargetParent));
        var table = DmTableFactory.GetTable(typeof(RenameTarget));
        var t = table.TableName;

        using var db = await OpenConnectionAsync();
        await db.DropTableIfExistsAsync(null, t);
        await db.DropTableIfExistsAsync(null, parent.TableName);
        await db.CreateTableIfNotExistsAsync(parent);
        await db.CreateTableIfNotExistsAsync(table);
        await db.ExecuteAsync($"INSERT INTO {parent.TableName} (id) VALUES (1)");
        await db.ExecuteAsync($"INSERT INTO {t} (id, a, b, c, p) VALUES (1, 'x', 'y', 5, 1)");

        // Index
        Assert.True(await db.RenameIndexIfExistsAsync(null, t, "ix_rn_a", "ix_rn_a2"));
        Assert.False(await db.DoesIndexExistAsync(null, t, "ix_rn_a"));
        Assert.True(await db.DoesIndexExistAsync(null, t, "ix_rn_a2"));
        Assert.False(await db.RenameIndexIfExistsAsync(null, t, "ix_rn_a", "ix_rn_a3"));
        Assert.False(await db.RenameIndexIfExistsAsync(null, t, "ix_rn_a2", "ix_rn_other"));

        // Unique constraint, still enforced
        Assert.True(await db.RenameUniqueConstraintIfExistsAsync(null, t, "uc_rn_b", "uc_rn_b2"));
        Assert.False(await db.DoesUniqueConstraintExistAsync(null, t, "uc_rn_b"));
        Assert.True(await db.DoesUniqueConstraintExistAsync(null, t, "uc_rn_b2"));
        await Assert.ThrowsAnyAsync<Exception>(() =>
            db.ExecuteAsync($"INSERT INTO {t} (id, a, b, c, p) VALUES (2, 'x', 'y', 5, 1)")
        );

        // Check constraint, still enforced (MySQL before 8.0.16 has none to rename)
        if (await db.SupportsCheckConstraintsAsync())
        {
            Assert.True(await db.RenameCheckConstraintIfExistsAsync(null, t, "ck_rn_c", "ck_rn_c2"));
            Assert.False(await db.DoesCheckConstraintExistAsync(null, t, "ck_rn_c"));
            Assert.True(await db.DoesCheckConstraintExistAsync(null, t, "ck_rn_c2"));
            await Assert.ThrowsAnyAsync<Exception>(() =>
                db.ExecuteAsync($"INSERT INTO {t} (id, a, b, c, p) VALUES (3, 'x', 'z', -1, 1)")
            );

            // An expression with a string literal, which MySQL reports escaped in INFORMATION_SCHEMA.
            Assert.True(await db.RenameCheckConstraintIfExistsAsync(null, t, "ck_rn_a", "ck_rn_a2"));
            Assert.True(await db.DoesCheckConstraintExistAsync(null, t, "ck_rn_a2"));
            await Assert.ThrowsAnyAsync<Exception>(() =>
                db.ExecuteAsync($"INSERT INTO {t} (id, a, b, c, p) VALUES (4, '', 'w', 1, 1)")
            );
        }

        // Foreign key, same definition
        Assert.True(await db.RenameForeignKeyConstraintIfExistsAsync(null, t, "fk_rn_p", "fk_rn_p2"));
        Assert.False(await db.DoesForeignKeyConstraintExistAsync(null, t, "fk_rn_p"));
        var fk = await db.GetForeignKeyConstraintAsync(null, t, "fk_rn_p2");
        Assert.NotNull(fk);
        Assert.Equal("p", fk!.SourceColumns.Single().ColumnName, ignoreCase: true);
        Assert.Equal(parent.TableName, fk.ReferencedTableName, ignoreCase: true);

        // Nothing was lost along the way.
        Assert.Equal(1, await db.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {t}"));

        await db.DropTableIfExistsAsync(null, t);
        await db.DropTableIfExistsAsync(null, parent.TableName);
    }

    /// <summary>
    /// SQL Server and MySQL refuse to rename a column that a check constraint uses. The rename has to work
    /// anyway, and the check has to keep enforcing the renamed column.
    /// </summary>
    [Fact]
    protected virtual async Task Rename_column_used_by_a_check_constraint_Async()
    {
        var table = DmTableFactory.GetTable(typeof(RenameCheckedColumn));
        var t = table.TableName;

        using var db = await OpenConnectionAsync();
        await db.DropTableIfExistsAsync(null, t);
        await db.CreateTableIfNotExistsAsync(table);
        await db.ExecuteAsync($"INSERT INTO {t} (id, qty) VALUES (1, 5)");

        Assert.True(await db.RenameColumnIfExistsAsync(null, t, "qty", "quantity"));
        Assert.True(await db.DoesColumnExistAsync(null, t, "quantity"));
        Assert.Equal(5, await db.ExecuteScalarAsync<int>($"SELECT quantity FROM {t} WHERE id = 1"));

        if (await db.SupportsCheckConstraintsAsync())
        {
            Assert.True(await db.DoesCheckConstraintExistAsync(null, t, "ck_rcc_qty"));
            await Assert.ThrowsAnyAsync<Exception>(() =>
                db.ExecuteAsync($"INSERT INTO {t} (id, quantity) VALUES (2, -1)")
            );
        }

        await db.DropTableIfExistsAsync(null, t);
    }
}

[DmTable(null, "rename_target_parent")]
public class RenameTargetParent
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }
}

[DmTable(null, "rename_target")]
public class RenameTarget
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }

    [DmColumn("a", length: 20)]
    [DmIndex(false, null, "ix_rn_a")]
    [DmCheckConstraint("a <> ''", "ck_rn_a")]
    public string A { get; set; } = default!;

    [DmColumn("b", length: 20)]
    [DmUniqueConstraint(constraintName: "uc_rn_b")]
    public string B { get; set; } = default!;

    [DmColumn("c")]
    [DmCheckConstraint("c >= 0", "ck_rn_c")]
    [DmIndex(false, null, "ix_rn_other")]
    public int C { get; set; }

    [DmColumn("p")]
    [DmForeignKeyConstraint(
        referencedTableName: "rename_target_parent",
        referencedColumnNames: new[] { "id" },
        constraintName: "fk_rn_p"
    )]
    public int P { get; set; }
}

[DmTable(null, "rename_checked_column")]
public class RenameCheckedColumn
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }

    [DmColumn("qty")]
    [DmCheckConstraint("qty >= 0", "ck_rcc_qty")]
    public int Qty { get; set; }
}
