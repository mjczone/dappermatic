// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Dapper;
using MJCZone.DapperMatic.DataAnnotations;
using MJCZone.DapperMatic.Models;
using MJCZone.DapperMatic.Providers;

namespace MJCZone.DapperMatic.Tests;

public abstract partial class DatabaseMethodsTests
{
    /// <summary>
    /// The guard against a noisy drift report: a table created straight from its model must reconcile
    /// against that same model with nothing to add and nothing flagged as drifted. Anything reported
    /// here is a false positive by construction, because the database was built from this very model.
    /// </summary>
    [Theory]
    [InlineData(typeof(TestDao1))]
    [InlineData(typeof(TestDao2))]
    [InlineData(typeof(TestDao3))]
    [InlineData(typeof(TestTable4))]
    [InlineData(typeof(TestTable5))]
    [InlineData(typeof(TestTable6))]
    protected virtual async Task Reconciling_a_table_against_its_own_model_reports_nothing_Async(Type type)
    {
        var tableDef = DmTableFactory.GetTable(type);

        using var db = await OpenConnectionAsync();

        if (!string.IsNullOrWhiteSpace(tableDef.SchemaName))
        {
            await db.CreateSchemaIfNotExistsAsync(tableDef.SchemaName);
        }

        await db.DropTableIfExistsAsync(tableDef.SchemaName, tableDef.TableName);
        await db.CreateTableIfNotExistsAsync(tableDef);

        var plan = await db.GetTableReconciliationPlanAsync(tableDef);

        Output.WriteLine(
            "{0}: {1} addition(s), {2} drift entr(ies)",
            tableDef.TableName,
            plan.Additions.Count,
            plan.Drift.Count
        );
        foreach (var d in plan.Drift)
        {
            Output.WriteLine("  DRIFT: {0}", d);
        }

        foreach (var a in plan.Additions)
        {
            Output.WriteLine("  ADD:   {0}", a);
        }

        Assert.False(plan.TableMissing);
        Assert.False(plan.HasChanges);
        Assert.False(plan.HasDrift);

        // Nor is anything worth altering: a false positive here would rewrite a column that is already right.
        var withAlterations = await db.GetTableReconciliationPlanAsync(tableDef, applySafeAlterations: true);
        foreach (var a in withAlterations.Alterations)
        {
            Output.WriteLine("  ALTER: {0}", a);
        }

        Assert.Empty(withAlterations.Alterations);
        Assert.False(withAlterations.HasChanges);

        var result = await db.ReconcileTableAsync(tableDef);
        Assert.False(result.Changed);
        Assert.False(result.TableCreated);

        await db.DropTableIfExistsAsync(tableDef.SchemaName, tableDef.TableName);
    }

    [Fact]
    protected virtual async Task Reconcile_creates_the_table_when_it_does_not_exist_Async()
    {
        var tableDef = DmTableFactory.GetTable(typeof(ReconcileV1));

        using var db = await OpenConnectionAsync();
        await db.DropTableIfExistsAsync(tableDef.SchemaName, tableDef.TableName);

        var plan = await db.GetTableReconciliationPlanAsync<ReconcileV1>();
        Assert.True(plan.TableMissing);
        Assert.True(plan.HasChanges);

        // Computing a plan must not touch the database.
        Assert.False(await db.DoesTableExistAsync(tableDef.SchemaName, tableDef.TableName));

        var result = await db.ReconcileTableAsync<ReconcileV1>();

        Assert.True(result.TableCreated);
        Assert.True(result.Changed);
        Assert.True(await db.DoesTableExistAsync(tableDef.SchemaName, tableDef.TableName));

        await db.DropTableIfExistsAsync(tableDef.SchemaName, tableDef.TableName);
    }

    /// <summary>
    /// The headline case from the issue: a model gained columns and indexes, and reconcile adds them
    /// to the table that is already there without recreating it.
    /// </summary>
    [Fact]
    protected virtual async Task Reconcile_adds_missing_columns_and_indexes_Async()
    {
        var v1 = DmTableFactory.GetTable(typeof(ReconcileV1));
        var v2 = DmTableFactory.GetTable(typeof(ReconcileV2));

        using var db = await OpenConnectionAsync();
        await db.DropTableIfExistsAsync(v1.SchemaName, v1.TableName);
        await db.CreateTableIfNotExistsAsync(v1);

        var plan = await db.GetTableReconciliationPlanAsync(v2);

        Assert.False(plan.TableMissing);
        Assert.Contains(plan.MissingColumns, c => c.ColumnName.Equals("email", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            plan.MissingColumns,
            c => c.ColumnName.Equals("created_at", StringComparison.OrdinalIgnoreCase)
        );

        var result = await db.ReconcileTableAsync(v2);

        Assert.True(result.Changed);
        Assert.Contains(result.Applied, a => a.ElementType == DmSchemaElementType.Column);
        Assert.True(await db.DoesColumnExistAsync(v2.SchemaName, v2.TableName, "email"));
        Assert.True(await db.DoesColumnExistAsync(v2.SchemaName, v2.TableName, "created_at"));

        // The pre-existing column and its data-bearing table survived; this was not a recreate.
        Assert.True(await db.DoesColumnExistAsync(v2.SchemaName, v2.TableName, "name"));

        // Idempotent: a second pass finds nothing left to do.
        var second = await db.ReconcileTableAsync(v2);
        Assert.False(second.Changed);

        await db.DropTableIfExistsAsync(v1.SchemaName, v1.TableName);
    }

    /// <summary>
    /// Additive-only means a column that exists but does not match is reported and left alone.
    /// </summary>
    [Fact]
    protected virtual async Task Reconcile_reports_drift_without_altering_the_column_Async()
    {
        var narrow = DmTableFactory.GetTable(typeof(ReconcileNarrow));
        var wide = DmTableFactory.GetTable(typeof(ReconcileWide));

        using var db = await OpenConnectionAsync();
        await db.DropTableIfExistsAsync(narrow.SchemaName, narrow.TableName);
        await db.CreateTableIfNotExistsAsync(narrow);

        var result = await db.ReconcileTableAsync(wide);

        Assert.True(result.HasDrift);
        Assert.Contains(
            result.Drift,
            d => d.ElementType == DmSchemaElementType.Column && d.Name.Equals("label", StringComparison.OrdinalIgnoreCase)
        );

        foreach (var d in result.Drift)
        {
            Output.WriteLine("DRIFT: {0}", d);
        }

        // The column was reported, not widened.
        var column = await db.GetColumnAsync(narrow.SchemaName, narrow.TableName, "label");
        Assert.NotNull(column);
        Assert.Equal(50, column!.Length);

        await db.DropTableIfExistsAsync(narrow.SchemaName, narrow.TableName);
    }

    /// <summary>
    /// The element flags narrow what reconcile is allowed to create.
    /// </summary>
    [Fact]
    protected virtual async Task Reconcile_honours_the_elements_filter_Async()
    {
        var v1 = DmTableFactory.GetTable(typeof(ReconcileV1));
        var v2 = DmTableFactory.GetTable(typeof(ReconcileV2));

        using var db = await OpenConnectionAsync();
        await db.DropTableIfExistsAsync(v1.SchemaName, v1.TableName);
        await db.CreateTableIfNotExistsAsync(v1);

        // Restricting to indexes must leave the missing columns alone. The model's index covers a
        // column the filter excludes, so it is reported as blocked rather than attempted -- issuing
        // that CREATE INDEX would fail against a column that is not there.
        var indexesOnly = await db.ReconcileTableAsync(v2, DmReconcileElements.Indexes);

        Assert.DoesNotContain(indexesOnly.Applied, a => a.ElementType == DmSchemaElementType.Column);
        Assert.False(await db.DoesColumnExistAsync(v2.SchemaName, v2.TableName, "email"));
        Assert.Contains(
            indexesOnly.Blocked,
            b => b.ElementType == DmSchemaElementType.Index && b.Note is { Length: > 0 }
        );
        foreach (var b in indexesOnly.Blocked)
        {
            Output.WriteLine("BLOCKED: {0}", b);
        }

        // A plan restricted to a kind reports only that kind.
        var columnsOnlyPlan = await db.GetTableReconciliationPlanAsync(v2, DmReconcileElements.Columns);
        Assert.NotEmpty(columnsOnlyPlan.MissingColumns);
        Assert.Empty(columnsOnlyPlan.MissingIndexes);

        // Widening the filter to columns lets them through. Note that creating a column marked as
        // indexed also creates its index, so the index is not separately outstanding afterwards.
        var withColumns = await db.ReconcileTableAsync(v2, DmReconcileElements.Columns);

        Assert.All(withColumns.Applied, a => Assert.Equal(DmSchemaElementType.Column, a.ElementType));
        Assert.True(await db.DoesColumnExistAsync(v2.SchemaName, v2.TableName, "email"));

        await db.DropTableIfExistsAsync(v1.SchemaName, v1.TableName);
    }

    /// <summary>
    /// A foreign key whose referential actions no longer match the model is drift, not something to
    /// silently replace.
    /// </summary>
    [Fact]
    protected virtual async Task Reconcile_reports_foreign_key_action_drift_Async()
    {
        var parent = DmTableFactory.GetTable(typeof(ReconcileFkParent));
        var noAction = DmTableFactory.GetTable(typeof(ReconcileFkNoAction));
        var cascade = DmTableFactory.GetTable(typeof(ReconcileFkCascade));

        using var db = await OpenConnectionAsync();

        await db.DropTableIfExistsAsync(noAction.SchemaName, noAction.TableName);
        await db.DropTableIfExistsAsync(parent.SchemaName, parent.TableName);
        await db.CreateTableIfNotExistsAsync(parent);
        await db.CreateTableIfNotExistsAsync(noAction);

        // Same model, same actions: nothing to report.
        var clean = await db.GetTableReconciliationPlanAsync(noAction);
        Assert.False(clean.HasDrift);

        // ON DELETE changed in the model: reported, and the constraint is left as it is.
        var result = await db.ReconcileTableAsync(cascade);

        foreach (var d in result.Drift)
        {
            Output.WriteLine("DRIFT: {0}", d);
        }

        Assert.Contains(
            result.Drift,
            d =>
                d.ElementType == DmSchemaElementType.ForeignKeyConstraint
                && d.Description.Contains("referential actions", StringComparison.OrdinalIgnoreCase)
        );
        Assert.DoesNotContain(result.Applied, a => a.ElementType == DmSchemaElementType.ForeignKeyConstraint);

        await db.DropTableIfExistsAsync(noAction.SchemaName, noAction.TableName);
        await db.DropTableIfExistsAsync(parent.SchemaName, parent.TableName);
    }

    /// <summary>
    /// A column added by reconcile gets each of its constraints once, under the model's name. Adding a
    /// column also creates the constraints its flags imply, under generated names, and reconcile used to
    /// add the model's explicitly named versions on top: a second index, a second foreign key, and on SQL
    /// Server a failed second default. No other test reconciles in a foreign-key column.
    /// </summary>
    [Fact]
    protected virtual async Task Reconcile_adds_a_column_with_named_constraints_exactly_once_Async()
    {
        var parent = DmTableFactory.GetTable(typeof(ReconcileNamedParent));
        var v1 = DmTableFactory.GetTable(typeof(ReconcileNamedChildV1));
        var v2 = DmTableFactory.GetTable(typeof(ReconcileNamedChildV2));
        var tableName = v1.TableName;

        using var db = await OpenConnectionAsync();
        await db.DropTableIfExistsAsync(v1.SchemaName, tableName);
        await db.DropTableIfExistsAsync(parent.SchemaName, parent.TableName);
        await db.CreateTableIfNotExistsAsync(parent);
        await db.CreateTableIfNotExistsAsync(v1);

        // An existing row means the NOT NULL column can only be added with its DEFAULT inline.
        await db.ExecuteAsync($"INSERT INTO {tableName} (id) VALUES (1)");

        var plan = await db.GetTableReconciliationPlanAsync(v2);
        var result = await db.ReconcileTableAsync(v2);

        foreach (var a in result.Applied)
        {
            Output.WriteLine("APPLIED: {0}", a);
        }

        foreach (var b in result.Blocked)
        {
            Output.WriteLine("BLOCKED: {0}", b);
        }

        // Every planned addition is accounted for: applied, or blocked with a reason.
        foreach (var addition in plan.Additions)
        {
            Assert.True(
                result.Applied.Any(a => IsSameAddition(a, addition)) || result.Blocked.Any(b => IsSameAddition(b, addition)),
                $"{addition} was planned but is neither applied nor blocked"
            );
        }

        Assert.All(result.Blocked, b => Assert.False(string.IsNullOrWhiteSpace(b.Note)));

        var table = await db.GetTableAsync(v2.SchemaName, tableName);
        Assert.NotNull(table);

        static bool IsOnly(List<DmOrderedColumn> columns, string name) =>
            columns.Count == 1 && columns[0].ColumnName.Equals(name, StringComparison.OrdinalIgnoreCase);

        var fk = Assert.Single(table!.ForeignKeyConstraints, f => IsOnly(f.SourceColumns, "parent_id"));
        Assert.Equal("fk_named_child_parent", fk.ConstraintName, ignoreCase: true);

        var ix = Assert.Single(table.Indexes, i => IsOnly(i.Columns, "parent_id"));
        Assert.Equal("ix_named_child_parent", ix.IndexName, ignoreCase: true);

        var uc = Assert.Single(table.UniqueConstraints, u => IsOnly(u.Columns, "code"));
        Assert.Equal("uc_named_child_code", uc.ConstraintName, ignoreCase: true);

        if (await db.SupportsCheckConstraintsAsync())
        {
            Assert.Contains(
                table.CheckConstraints,
                c => c.ConstraintName.Equals("ck_named_child_qty", StringComparison.OrdinalIgnoreCase)
            );
            Assert.DoesNotContain(
                table.CheckConstraints,
                c =>
                    c.ConstraintName.Equals(
                        DbProviderUtils.GenerateCheckConstraintName(tableName, "qty"),
                        StringComparison.OrdinalIgnoreCase
                    )
            );
        }
        else
        {
            Assert.Contains(result.Blocked, b => b.ElementType == DmSchemaElementType.CheckConstraint && b.Unsupported);
        }

        Assert.Single(table.DefaultConstraints, d => d.ColumnName.Equals("qty", StringComparison.OrdinalIgnoreCase));
        Assert.Single(
            table.DefaultConstraints,
            d => d.ColumnName.Equals("status", StringComparison.OrdinalIgnoreCase)
        );

        // The existing row survived and took the new column's default.
        Assert.Equal("new", await db.ExecuteScalarAsync<string>($"SELECT status FROM {tableName} WHERE id = 1"));

        // Nothing left to do, and nothing reported as existing under a different name.
        var second = await db.ReconcileTableAsync(v2);
        foreach (var d in second.Drift)
        {
            Output.WriteLine("DRIFT: {0}", d);
        }

        Assert.False(second.Changed);
        Assert.False(second.HasDrift);

        await db.DropTableIfExistsAsync(v1.SchemaName, tableName);
        await db.DropTableIfExistsAsync(parent.SchemaName, parent.TableName);
    }

    /// <summary>
    /// A model in a schema that does not exist yet: CREATE TABLE fails outright unless the schema is
    /// created first.
    /// </summary>
    [Fact]
    protected virtual async Task Reconcile_creates_a_missing_schema_with_the_table_Async()
    {
        using var db = await OpenConnectionAsync();
        if (!db.SupportsSchemas())
        {
            Output.WriteLine("This provider has no schemas; nothing to test.");
            return;
        }

        var tableDef = DmTableFactory.GetTable(typeof(ReconcileInNewSchema));
        var schemaName = tableDef.SchemaName!;

        await db.DropTableIfExistsAsync(schemaName, tableDef.TableName);
        await db.DropSchemaIfExistsAsync(schemaName);

        var result = await db.ReconcileTableAsync(tableDef);

        Assert.True(result.TableCreated);
        Assert.True(await db.DoesTableExistAsync(schemaName, tableDef.TableName));

        await db.DropTableIfExistsAsync(schemaName, tableDef.TableName);
        await db.DropSchemaIfExistsAsync(schemaName);
    }

    private static bool IsSameAddition(DmSchemaAddition left, DmSchemaAddition right) =>
        left.ElementType == right.ElementType && left.Name.Equals(right.Name, StringComparison.OrdinalIgnoreCase);
}

[DmTable(null, "reconcile_target")]
public class ReconcileV1
{
    [DmColumn("id")]
    public int Id { get; set; }

    [DmColumn("name", length: 100)]
    public string Name { get; set; } = default!;
}

[DmTable(null, "reconcile_target")]
public class ReconcileV2
{
    [DmColumn("id")]
    public int Id { get; set; }

    [DmColumn("name", length: 100)]
    public string Name { get; set; } = default!;

    [DmColumn("email", length: 320, isNullable: true)]
    [DmIndex(false, null, "ix_reconcile_target_email")]
    public string? Email { get; set; }

    [DmColumn("created_at", isNullable: true)]
    public DateTime? CreatedAt { get; set; }
}

[DmTable(null, "reconcile_drift")]
public class ReconcileNarrow
{
    [DmColumn("id")]
    public int Id { get; set; }

    [DmColumn("label", length: 50)]
    public string Label { get; set; } = default!;
}

[DmTable(null, "reconcile_drift")]
public class ReconcileWide
{
    [DmColumn("id")]
    public int Id { get; set; }

    [DmColumn("label", length: 200)]
    public string Label { get; set; } = default!;
}

[DmTable(null, "reconcile_fk_parent")]
public class ReconcileFkParent
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }
}

[DmTable(null, "reconcile_fk_child")]
[DmForeignKeyConstraint(
    new[] { "parent_id" },
    referencedTableName: "reconcile_fk_parent",
    referencedColumnNames: new[] { "id" },
    constraintName: "fk_reconcile_fk_child_parent",
    onDelete: DmForeignKeyAction.NoAction,
    onUpdate: DmForeignKeyAction.NoAction
)]
public class ReconcileFkNoAction
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }

    [DmColumn("parent_id")]
    public int ParentId { get; set; }
}

[DmTable(null, "reconcile_fk_child")]
[DmForeignKeyConstraint(
    new[] { "parent_id" },
    referencedTableName: "reconcile_fk_parent",
    referencedColumnNames: new[] { "id" },
    constraintName: "fk_reconcile_fk_child_parent",
    onDelete: DmForeignKeyAction.Cascade,
    onUpdate: DmForeignKeyAction.NoAction
)]
public class ReconcileFkCascade
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }

    [DmColumn("parent_id")]
    public int ParentId { get; set; }
}

[DmTable(null, "reconcile_named_parent")]
public class ReconcileNamedParent
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }
}

[DmTable(null, "reconcile_named_child")]
public class ReconcileNamedChildV1
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }
}

[DmTable(null, "reconcile_named_child")]
public class ReconcileNamedChildV2
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }

    [DmColumn("parent_id", isNullable: true)]
    [DmForeignKeyConstraint(
        referencedTableName: "reconcile_named_parent",
        referencedColumnNames: new[] { "id" },
        constraintName: "fk_named_child_parent"
    )]
    [DmIndex(false, null, "ix_named_child_parent")]
    public int? ParentId { get; set; }

    [DmColumn("code", length: 20, isNullable: true)]
    [DmUniqueConstraint(constraintName: "uc_named_child_code")]
    public string? Code { get; set; }

    [DmColumn("qty", isNullable: true)]
    [DmCheckConstraint("qty >= 0", "ck_named_child_qty")]
    [DmDefaultConstraint("0", "df_named_child_qty")]
    public int? Qty { get; set; }

    [DmColumn("status", length: 10)]
    [DmDefaultConstraint("'new'", "df_named_child_status")]
    public string Status { get; set; } = "new";
}

[DmTable("reconcile_new_schema", "reconcile_in_schema")]
public class ReconcileInNewSchema
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }
}
