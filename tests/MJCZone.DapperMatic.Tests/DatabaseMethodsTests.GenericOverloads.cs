// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.DataAnnotations;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Tests;

public abstract partial class DatabaseMethodsTests
{
    /// <summary>
    /// The overloads that take the table from a model class find the same table the named overloads do.
    /// </summary>
    [Fact]
    protected virtual async Task Rename_methods_take_the_table_from_a_model_class_Async()
    {
        var parent = DmTableFactory.GetTable(typeof(RenameTargetParent));
        var table = DmTableFactory.GetTable(typeof(RenameTarget));
        var t = table.TableName;

        using var db = await OpenConnectionAsync();
        await db.DropTableIfExistsAsync(null, t);
        await db.DropTableIfExistsAsync(null, parent.TableName);
        await db.CreateTableIfNotExistsAsync(parent);
        await db.CreateTableIfNotExistsAsync(table);

        Assert.True(await db.RenameIndexIfExistsAsync<RenameTarget>("ix_rn_a", "ix_rn_a2"));
        Assert.True(await db.DoesIndexExistAsync(null, t, "ix_rn_a2"));

        Assert.True(await db.RenameUniqueConstraintIfExistsAsync<RenameTarget>("uc_rn_b", "uc_rn_b2"));
        Assert.True(await db.DoesUniqueConstraintExistAsync(null, t, "uc_rn_b2"));

        if (await db.SupportsCheckConstraintsAsync())
        {
            Assert.True(await db.RenameCheckConstraintIfExistsAsync<RenameTarget>("ck_rn_c", "ck_rn_c2"));
            Assert.True(await db.DoesCheckConstraintExistAsync(null, t, "ck_rn_c2"));
        }

        Assert.True(await db.RenameForeignKeyConstraintIfExistsAsync<RenameTarget>("fk_rn_p", "fk_rn_p2"));
        Assert.True(await db.DoesForeignKeyConstraintExistAsync(null, t, "fk_rn_p2"));

        // Nothing left under the old names.
        Assert.False(await db.RenameIndexIfExistsAsync<RenameTarget>("ix_rn_a", "ix_rn_a3"));
        Assert.False(await db.RenameForeignKeyConstraintIfExistsAsync<RenameTarget>("fk_rn_p", "fk_rn_p3"));

        await db.DropTableIfExistsAsync(null, t);
        await db.DropTableIfExistsAsync(null, parent.TableName);
    }

    /// <summary>
    /// A column can be altered by name on a model's table, or back to the model's own definition of it, which
    /// leaves the cached model as it was.
    /// </summary>
    [Fact]
    protected virtual async Task Alter_column_takes_the_table_and_definition_from_a_model_class_Async()
    {
        var model = DmTableFactory.GetTable(typeof(GenericAlterTarget));
        var before = DmTableFingerprint.GetCanonicalForm(model);

        using var db = await OpenConnectionAsync();
        await db.DropTableIfExistsAsync(null, model.TableName);
        await db.CreateTableIfNotExistsAsync(model);

        Assert.True(await db.AlterColumnIfExistsAsync<GenericAlterTarget>("note", "varchar(40)", isNullable: true));
        var altered = await db.GetColumnAsync(null, model.TableName, "note");
        Assert.Equal(40, altered!.Length);
        Assert.True(altered.IsNullable);

        Assert.True(await db.AlterColumnIfExistsAsync<GenericAlterTarget>(t => t.Note));
        var restored = await db.GetColumnAsync(null, model.TableName, "note");
        Assert.Equal(100, restored!.Length);
        Assert.False(restored.IsNullable);

        Assert.Equal(before, DmTableFingerprint.GetCanonicalForm(model));

        // A column the table does not have is not altered.
        await db.DropColumnIfExistsAsync(null, model.TableName, "note");
        Assert.False(await db.AlterColumnIfExistsAsync<GenericAlterTarget>(t => t.Note));
        Assert.False(await db.AlterColumnIfExistsAsync<GenericAlterTarget>("note", "varchar(40)", isNullable: true));

        await db.DropTableIfExistsAsync(null, model.TableName);
    }

    /// <summary>
    /// A model type known only at run time, as when scanning an assembly, works the same as one given as a type
    /// argument.
    /// </summary>
    [Fact]
    protected virtual async Task Model_methods_take_the_model_as_a_type_Async()
    {
        var type = typeof(RenameTargetParent);
        var table = DmTableFactory.GetTable(type);

        using var db = await OpenConnectionAsync();
        await db.DropTableIfExistsAsync(type);
        Assert.False(await db.DoesTableExistAsync(type));

        Assert.True(await db.CreateTableIfNotExistsAsync(type));
        Assert.True(await db.DoesTableExistAsync(type));
        Assert.Equal(
            string.Join(",", (await db.GetColumnNamesAsync<RenameTargetParent>()).Order()),
            string.Join(",", (await db.GetColumnNamesAsync(type)).Order())
        );
        Assert.NotNull(await db.GetPrimaryKeyConstraintAsync(type));
        Assert.False((await db.GetTableReconciliationPlanAsync(type)).HasChanges);

        // The named overloads keep their meaning with a null schema, which the type overloads could have taken.
        Assert.Equal(table.Columns.Count, (await db.GetColumnNamesAsync(null, table.TableName)).Count);

        Assert.True(await db.DropTableIfExistsAsync(type));
        await Assert.ThrowsAsync<ArgumentNullException>(() => db.DoesTableExistAsync((Type)null!));
    }
}

[DmTable(null, "generic_alter_target")]
public class GenericAlterTarget
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }

    [DmColumn("note", length: 100)]
    public string Note { get; set; } = default!;
}
