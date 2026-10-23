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
    /// A table and its columns renamed in the model, with <c>PreviousNames</c> declared, are renamed in the
    /// database rather than created again, and the data comes with them. The index and check constraint carry
    /// generated names, which move with the table and column; they must be matched, not created a second time.
    /// </summary>
    [Fact]
    protected virtual async Task Reconcile_renames_a_table_and_its_columns_keeping_the_data_Async()
    {
        var v1 = DmTableFactory.GetTable(typeof(RrUsersV1));
        var v2 = DmTableFactory.GetTable(typeof(RrCustomersV2));

        using var db = await OpenConnectionAsync();
        await db.DropTableIfExistsAsync(null, v2.TableName);
        await db.DropTableIfExistsAsync(null, v1.TableName);
        await db.CreateTableIfNotExistsAsync(v1);
        await db.ExecuteAsync($"INSERT INTO {v1.TableName} (id, email, name) VALUES (1, 'a@example.com', 'Ada')");

        var plan = await db.GetTableReconciliationPlanAsync(v2);
        foreach (var r in plan.Renames)
        {
            Output.WriteLine("RENAME: {0}", r);
        }

        foreach (var d in plan.Drift)
        {
            Output.WriteLine("DRIFT: {0}", d);
        }

        Assert.False(plan.TableMissing);
        Assert.Contains(plan.Renames, r => r.ElementType == DmSchemaElementType.Table && r.OldName == v1.TableName);
        Assert.Contains(plan.Renames, r => r.ElementType == DmSchemaElementType.Column && r.OldName == "email");
        Assert.Contains(plan.Renames, r => r.ElementType == DmSchemaElementType.Column && r.OldName == "name");
        Assert.Empty(plan.MissingColumns);
        Assert.Empty(plan.MissingIndexes);
        Assert.Empty(plan.MissingCheckConstraints);
        Assert.False(plan.HasDrift);

        // Planning touched nothing.
        Assert.True(await db.DoesTableExistAsync(null, v1.TableName));
        Assert.False(await db.DoesTableExistAsync(null, v2.TableName));

        var result = await db.ReconcileTableAsync(v2);
        foreach (var r in result.Renamed)
        {
            Output.WriteLine("RENAMED: {0}", r);
        }

        foreach (var b in result.Blocked)
        {
            Output.WriteLine("BLOCKED: {0}", b);
        }

        var supportsChecks = await db.SupportsCheckConstraintsAsync();
        Assert.Empty(result.Applied);
        Assert.All(
            result.Blocked,
            b => Assert.True(!supportsChecks && b.ElementType == DmSchemaElementType.CheckConstraint, b.ToString())
        );
        Assert.False(await db.DoesTableExistAsync(null, v1.TableName));
        Assert.True(await db.DoesTableExistAsync(null, v2.TableName));

        // The data came along.
        Assert.Equal(
            "a@example.com",
            await db.ExecuteScalarAsync<string>($"SELECT email_address FROM {v2.TableName} WHERE id = 1")
        );
        Assert.Equal("Ada", await db.ExecuteScalarAsync<string>($"SELECT full_name FROM {v2.TableName} WHERE id = 1"));

        // One index and at most one check over the renamed column: nothing was created twice.
        var table = await db.GetTableAsync(null, v2.TableName);
        Assert.Single(
            table!.Indexes,
            i => i.Columns.Count == 1 && i.Columns[0].ColumnName.Equals("email_address", StringComparison.OrdinalIgnoreCase)
        );

        if (supportsChecks)
        {
            Assert.Single(table.CheckConstraints);
            await Assert.ThrowsAnyAsync<Exception>(() =>
                db.ExecuteAsync($"INSERT INTO {v2.TableName} (id, email_address) VALUES (2, '')")
            );
        }

        // Nothing left to do.
        var second = await db.ReconcileTableAsync(v2);
        foreach (var d in second.Drift)
        {
            Output.WriteLine("SECOND DRIFT: {0}", d);
        }

        foreach (var a in second.Applied)
        {
            Output.WriteLine("SECOND APPLIED: {0}", a);
        }

        foreach (var r in second.Renamed)
        {
            Output.WriteLine("SECOND RENAMED: {0}", r);
        }

        Assert.False(second.Changed);
        Assert.False(second.HasDrift);

        await db.DropTableIfExistsAsync(null, v2.TableName);
    }

    /// <summary>
    /// Indexes and constraints renamed in the model, with their old names declared, are renamed in place.
    /// </summary>
    [Fact]
    protected virtual async Task Reconcile_renames_indexes_and_constraints_declared_with_previous_names_Async()
    {
        var parent = DmTableFactory.GetTable(typeof(RrParent));
        var v1 = DmTableFactory.GetTable(typeof(RrNamedV1));
        var v2 = DmTableFactory.GetTable(typeof(RrNamedV2));
        var t = v1.TableName;

        using var db = await OpenConnectionAsync();
        await db.DropTableIfExistsAsync(null, t);
        await db.DropTableIfExistsAsync(null, parent.TableName);
        await db.CreateTableIfNotExistsAsync(parent);
        await db.CreateTableIfNotExistsAsync(v1);
        await db.ExecuteAsync($"INSERT INTO {parent.TableName} (id) VALUES (1)");
        await db.ExecuteAsync($"INSERT INTO {t} (id, code, code2, amount, parent_id) VALUES (1, 'a', 'b', 5, 1)");

        var supportsChecks = await db.SupportsCheckConstraintsAsync();

        var result = await db.ReconcileTableAsync(v2);
        foreach (var r in result.Renamed)
        {
            Output.WriteLine("RENAMED: {0}", r);
        }

        foreach (var b in result.Blocked)
        {
            Output.WriteLine("BLOCKED: {0}", b);
        }

        Assert.Contains(result.Renamed, r => r.ElementType == DmSchemaElementType.Index && r.NewName == "ix_rr_new");
        Assert.Contains(
            result.Renamed,
            r => r.ElementType == DmSchemaElementType.UniqueConstraint && r.NewName == "uc_rr_new"
        );
        Assert.Contains(
            result.Renamed,
            r => r.ElementType == DmSchemaElementType.ForeignKeyConstraint && r.NewName == "fk_rr_new"
        );
        Assert.Empty(result.Applied);

        Assert.True(await db.DoesIndexExistAsync(null, t, "ix_rr_new"));
        Assert.False(await db.DoesIndexExistAsync(null, t, "ix_rr_old"));
        Assert.True(await db.DoesUniqueConstraintExistAsync(null, t, "uc_rr_new"));
        Assert.False(await db.DoesUniqueConstraintExistAsync(null, t, "uc_rr_old"));
        Assert.True(await db.DoesForeignKeyConstraintExistAsync(null, t, "fk_rr_new"));
        Assert.False(await db.DoesForeignKeyConstraintExistAsync(null, t, "fk_rr_old"));

        if (supportsChecks)
        {
            Assert.Contains(
                result.Renamed,
                r => r.ElementType == DmSchemaElementType.CheckConstraint && r.NewName == "ck_rr_new"
            );
            Assert.True(await db.DoesCheckConstraintExistAsync(null, t, "ck_rr_new"));
            Assert.False(await db.DoesCheckConstraintExistAsync(null, t, "ck_rr_old"));
        }

        Assert.Equal(1, await db.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {t}"));

        var second = await db.ReconcileTableAsync(v2);
        Assert.False(second.Changed);

        await db.DropTableIfExistsAsync(null, t);
        await db.DropTableIfExistsAsync(null, parent.TableName);
    }

    /// <summary>
    /// Reconciliation never guesses. A column found under two of its previous names is blocked rather than
    /// renamed or added; a previous name that still exists beside the current one is reported; and a rename
    /// the model did not declare is added as a new column, with drift that says how to declare it.
    /// </summary>
    [Fact]
    protected virtual async Task Reconcile_does_not_guess_ambiguous_or_undeclared_renames_Async()
    {
        var guessV1 = DmTableFactory.GetTable(typeof(RrGuessV1));
        var guessV2 = DmTableFactory.GetTable(typeof(RrGuessV2));
        var hintV1 = DmTableFactory.GetTable(typeof(RrHintV1));
        var hintV2 = DmTableFactory.GetTable(typeof(RrHintV2));

        using var db = await OpenConnectionAsync();
        await db.DropTableIfExistsAsync(null, guessV1.TableName);
        await db.DropTableIfExistsAsync(null, hintV1.TableName);
        await db.CreateTableIfNotExistsAsync(guessV1);
        await db.CreateTableIfNotExistsAsync(hintV1);

        var guess = await db.ReconcileTableAsync(guessV2);
        foreach (var b in guess.Blocked)
        {
            Output.WriteLine("BLOCKED: {0}", b);
        }

        foreach (var d in guess.Drift)
        {
            Output.WriteLine("DRIFT: {0}", d);
        }

        // Two previous names present: blocked, and neither renamed nor added.
        Assert.Contains(
            guess.Blocked,
            b => b.ElementType == DmSchemaElementType.Column && b.Name == "email_address" && b.Note is { Length: > 0 }
        );
        Assert.Empty(guess.Renamed);
        Assert.False(await db.DoesColumnExistAsync(null, guessV1.TableName, "email_address"));
        Assert.True(await db.DoesColumnExistAsync(null, guessV1.TableName, "email"));
        Assert.True(await db.DoesColumnExistAsync(null, guessV1.TableName, "mail"));

        // A previous name beside the current one: reported, left alone.
        Assert.Contains(guess.Drift, d => d.Name == "alias_old" && d.Description.Contains("also exists", StringComparison.Ordinal));
        Assert.True(await db.DoesColumnExistAsync(null, guessV1.TableName, "alias_old"));

        // An undeclared rename: added as a new column, with a hint about PreviousNames.
        var hintPlan = await db.GetTableReconciliationPlanAsync(hintV2);
        foreach (var d in hintPlan.Drift)
        {
            Output.WriteLine("HINT DRIFT: {0}", d);
        }

        Assert.Contains(hintPlan.MissingColumns, c => c.ColumnName == "nickname");
        Assert.Contains(
            hintPlan.Drift,
            d => d.Name == "nick" && d.Description.Contains("PreviousNames", StringComparison.Ordinal)
        );

        await db.DropTableIfExistsAsync(null, guessV1.TableName);
        await db.DropTableIfExistsAsync(null, hintV1.TableName);
    }

    /// <summary>
    /// A table that exists under two of its previous names is neither renamed nor created; one that exists
    /// beside its previous name is reconciled as it is, with the old one reported.
    /// </summary>
    [Fact]
    protected virtual async Task Reconcile_does_not_guess_ambiguous_table_renames_Async()
    {
        var oldOne = DmTableFactory.GetTable(typeof(RrTableOld1));
        var oldTwo = DmTableFactory.GetTable(typeof(RrTableOld2));
        var model = DmTableFactory.GetTable(typeof(RrTableNew));

        using var db = await OpenConnectionAsync();
        await db.DropTableIfExistsAsync(null, model.TableName);
        await db.DropTableIfExistsAsync(null, oldOne.TableName);
        await db.DropTableIfExistsAsync(null, oldTwo.TableName);
        await db.CreateTableIfNotExistsAsync(oldOne);
        await db.CreateTableIfNotExistsAsync(oldTwo);

        var ambiguous = await db.ReconcileTableAsync(model);
        Assert.False(ambiguous.TableCreated);
        Assert.Empty(ambiguous.Renamed);
        Assert.Contains(ambiguous.Blocked, b => b.ElementType == DmSchemaElementType.Table);
        Assert.False(await db.DoesTableExistAsync(null, model.TableName));

        // With the current table there too, it is reconciled as it is and the leftover reported.
        await db.DropTableIfExistsAsync(null, oldTwo.TableName);
        await db.CreateTableIfNotExistsAsync(model);

        var beside = await db.GetTableReconciliationPlanAsync(model);
        Assert.Empty(beside.Renames);
        Assert.Contains(beside.Drift, d => d.ElementType == DmSchemaElementType.Table && d.Name == oldOne.TableName);

        await db.DropTableIfExistsAsync(null, model.TableName);
        await db.DropTableIfExistsAsync(null, oldOne.TableName);
    }
}

[DmTable(null, "rr_users")]
public class RrUsersV1
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }

    [DmColumn("email", length: 320)]
    [DmIndex]
    [DmCheckConstraint("email <> ''")]
    public string Email { get; set; } = default!;

    [DmColumn("name", length: 100, isNullable: true)]
    public string? Name { get; set; }
}

[DmTable(null, "rr_customers", PreviousNames = new[] { "rr_users" })]
public class RrCustomersV2
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }

    [DmColumn("email_address", length: 320, PreviousNames = new[] { "email" })]
    [DmIndex]
    [DmCheckConstraint("email_address <> ''")]
    public string EmailAddress { get; set; } = default!;

    [DmColumn("full_name", length: 100, isNullable: true, PreviousNames = new[] { "name" })]
    public string? FullName { get; set; }
}

[DmTable(null, "rr_parent")]
public class RrParent
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }
}

[DmTable(null, "rr_named")]
public class RrNamedV1
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }

    [DmColumn("code", length: 20)]
    [DmIndex(false, null, "ix_rr_old")]
    public string Code { get; set; } = default!;

    [DmColumn("code2", length: 20)]
    [DmUniqueConstraint(constraintName: "uc_rr_old")]
    public string Code2 { get; set; } = default!;

    [DmColumn("amount")]
    [DmCheckConstraint("amount >= 0", "ck_rr_old")]
    public int Amount { get; set; }

    [DmColumn("parent_id")]
    [DmForeignKeyConstraint(referencedTableName: "rr_parent", referencedColumnNames: new[] { "id" }, constraintName: "fk_rr_old")]
    public int ParentId { get; set; }
}

[DmTable(null, "rr_named")]
public class RrNamedV2
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }

    [DmColumn("code", length: 20)]
    [DmIndex(false, null, "ix_rr_new", PreviousNames = new[] { "ix_rr_old" })]
    public string Code { get; set; } = default!;

    [DmColumn("code2", length: 20)]
    [DmUniqueConstraint(constraintName: "uc_rr_new", PreviousNames = new[] { "uc_rr_old" })]
    public string Code2 { get; set; } = default!;

    [DmColumn("amount")]
    [DmCheckConstraint("amount >= 0", "ck_rr_new", PreviousNames = new[] { "ck_rr_old" })]
    public int Amount { get; set; }

    [DmColumn("parent_id")]
    [DmForeignKeyConstraint(
        referencedTableName: "rr_parent",
        referencedColumnNames: new[] { "id" },
        constraintName: "fk_rr_new",
        PreviousNames = new[] { "fk_rr_old" }
    )]
    public int ParentId { get; set; }
}

[DmTable(null, "rr_guess")]
public class RrGuessV1
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }

    [DmColumn("email", length: 100, isNullable: true)]
    public string? Email { get; set; }

    [DmColumn("mail", length: 100, isNullable: true)]
    public string? Mail { get; set; }

    [DmColumn("alias", length: 50, isNullable: true)]
    public string? Alias { get; set; }

    [DmColumn("alias_old", length: 50, isNullable: true)]
    public string? AliasOld { get; set; }
}

[DmTable(null, "rr_guess")]
public class RrGuessV2
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }

    [DmColumn("email_address", length: 100, isNullable: true, PreviousNames = new[] { "email", "mail" })]
    public string? EmailAddress { get; set; }

    [DmColumn("alias", length: 50, isNullable: true, PreviousNames = new[] { "alias_old" })]
    public string? Alias { get; set; }
}

[DmTable(null, "rr_hint")]
public class RrHintV1
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }

    [DmColumn("nick", length: 50, isNullable: true)]
    public string? Nick { get; set; }
}

[DmTable(null, "rr_hint")]
public class RrHintV2
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }

    [DmColumn("nickname", length: 50, isNullable: true)]
    public string? Nickname { get; set; }
}

[DmTable(null, "rr_t_old1")]
public class RrTableOld1
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }
}

[DmTable(null, "rr_t_old2")]
public class RrTableOld2
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }
}

[DmTable(null, "rr_t_new", PreviousNames = new[] { "rr_t_old1", "rr_t_old2" })]
public class RrTableNew
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }
}
