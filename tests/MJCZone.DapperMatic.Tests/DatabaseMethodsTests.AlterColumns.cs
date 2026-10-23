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
    /// Altering a column changes its type and nullability and nothing else: its default and its data survive.
    /// </summary>
    [Fact]
    protected virtual async Task Alter_column_changes_type_and_nullability_and_keeps_the_rest_Async()
    {
        var tableDef = DmTableFactory.GetTable(typeof(AlterTarget));
        var tableName = tableDef.TableName;

        using var db = await OpenConnectionAsync();
        await db.DropTableIfExistsAsync(null, tableName);
        await db.CreateTableIfNotExistsAsync(tableDef);
        await db.ExecuteAsync($"INSERT INTO {tableName} (id, label, amount) VALUES (1, 'hello', 12.34)");

        Assert.True(
            await db.AlterColumnIfExistsAsync(
                new DmColumn(null, tableName, "label", typeof(string), length: 200, isNullable: true)
            )
        );
        Assert.True(
            await db.AlterColumnIfExistsAsync(
                new DmColumn(null, tableName, "amount", typeof(decimal), precision: 12, scale: 2, isNullable: true)
            )
        );

        // A column or table that is not there is reported, not created.
        Assert.False(
            await db.AlterColumnIfExistsAsync(new DmColumn(null, tableName, "missing", typeof(string), length: 10))
        );
        Assert.False(
            await db.AlterColumnIfExistsAsync(new DmColumn(null, "missing_table", "label", typeof(string), length: 10))
        );

        var label = await db.GetColumnAsync(null, tableName, "label");
        var amount = await db.GetColumnAsync(null, tableName, "amount");
        Assert.Equal(200, label!.Length);
        Assert.True(label.IsNullable);
        Assert.Equal(12, amount!.Precision);
        Assert.Equal(2, amount.Scale);
        Assert.True(amount.IsNullable);

        var table = await db.GetTableAsync(null, tableName);
        Assert.Contains(table!.DefaultConstraints, d => d.ColumnName.Equals("label", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(table.DefaultConstraints, d => d.ColumnName.Equals("amount", StringComparison.OrdinalIgnoreCase));

        Assert.Equal("hello", await db.ExecuteScalarAsync<string>($"SELECT label FROM {tableName} WHERE id = 1"));
        Assert.Equal(12.34m, await db.ExecuteScalarAsync<decimal>($"SELECT amount FROM {tableName} WHERE id = 1"));

        // The columns accept nulls now.
        await db.ExecuteAsync($"INSERT INTO {tableName} (id, label, amount) VALUES (2, NULL, NULL)");

        await db.DropTableIfExistsAsync(null, tableName);
    }

    /// <summary>
    /// SQL Server's ALTER COLUMN and PostgreSQL's ALTER COLUMN ... TYPE both give a character column the
    /// default collation unless it is restated, and MySQL's MODIFY COLUMN drops whatever is not restated.
    /// </summary>
    [Fact]
    protected virtual async Task Alter_column_keeps_the_collation_Async()
    {
        using var db = await OpenConnectionAsync();
        const string tableName = "alter_keeps_collation";

        var (create, collationQuery, expected) = db.GetDbProviderType() switch
        {
            DbProviderType.SqlServer => (
                $"CREATE TABLE {tableName} (id int PRIMARY KEY, label varchar(50) COLLATE Latin1_General_BIN NOT NULL)",
                $"SELECT collation_name FROM sys.columns WHERE object_id = OBJECT_ID('{tableName}') AND name = 'label'",
                "Latin1_General_BIN"
            ),
            DbProviderType.PostgreSql => (
                $"CREATE TABLE {tableName} (id int PRIMARY KEY, label varchar(50) COLLATE \"C\" NOT NULL)",
                $"SELECT collation_name FROM information_schema.columns WHERE table_name = '{tableName}' AND column_name = 'label'",
                "C"
            ),
            DbProviderType.MySql => (
                $"CREATE TABLE {tableName} (id int PRIMARY KEY, label varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL)",
                $"SELECT COLLATION_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'label'",
                "utf8mb4_bin"
            ),
            _ => (string.Empty, string.Empty, string.Empty),
        };

        if (string.IsNullOrEmpty(create))
        {
            Output.WriteLine("Covered for SQL Server, PostgreSQL and MySQL/MariaDB only.");
            return;
        }

        await db.DropTableIfExistsAsync(null, tableName);
        await db.ExecuteAsync(create);

        Assert.True(await db.AlterColumnIfExistsAsync(null, tableName, "label", "varchar(200)", isNullable: true));

        Assert.Equal(expected, await db.ExecuteScalarAsync<string>(collationQuery));
        var label = await db.GetColumnAsync(null, tableName, "label");
        Assert.Equal(200, label!.Length);
        Assert.True(label.IsNullable);

        await db.DropTableIfExistsAsync(null, tableName);
    }

    /// <summary>
    /// Reconciliation alters nothing unless asked, and when asked applies only the changes that cannot lose
    /// data. Everything else stays drift.
    /// </summary>
    [Fact]
    protected virtual async Task Reconcile_applies_safe_alterations_only_when_asked_Async()
    {
        var v1 = DmTableFactory.GetTable(typeof(ReconcileAlterV1));
        var v2 = DmTableFactory.GetTable(typeof(ReconcileAlterV2));
        var tableName = v1.TableName;

        using var db = await OpenConnectionAsync();
        await db.DropTableIfExistsAsync(null, tableName);
        await db.CreateTableIfNotExistsAsync(v1);
        await db.ExecuteAsync(
            $"INSERT INTO {tableName} (id, label, amount, code, ratio) VALUES (1, 'hello', 12.34, 'abc', 1.5)"
        );

        bool HasDrift(IEnumerable<DmSchemaDrift> drift, string column) =>
            drift.Any(d => d.Name.Equals(column, StringComparison.OrdinalIgnoreCase));

        // Not asked: every difference is drift, and nothing is planned.
        var withoutFlag = await db.GetTableReconciliationPlanAsync(v2);
        Assert.Empty(withoutFlag.Alterations);
        Assert.False(withoutFlag.HasChanges);
        Assert.True(HasDrift(withoutFlag.Drift, "label"));
        Assert.True(HasDrift(withoutFlag.Drift, "amount"));

        // None reports the same drift, and plans nothing.
        var none = await db.GetTableReconciliationPlanAsync(v2, DmReconcileElements.None);
        Assert.False(none.HasChanges);
        Assert.True(HasDrift(none.Drift, "label"));

        var notAsked = await db.ReconcileTableAsync(v2);
        Assert.Empty(notAsked.Altered);
        Assert.Equal(50, (await db.GetColumnAsync(null, tableName, "label"))!.Length);

        // Asked: the safe changes are applied, the unsafe ones stay drift.
        var result = await db.ReconcileTableAsync(v2, applySafeAlterations: true);

        foreach (var a in result.Altered)
        {
            Output.WriteLine("ALTERED: {0}", a);
        }

        foreach (var d in result.Drift)
        {
            Output.WriteLine("DRIFT: {0}", d);
        }

        Assert.Empty(result.Blocked);
        Assert.Contains(result.Altered, a => a.ColumnName == "label");
        Assert.Contains(result.Altered, a => a.ColumnName == "amount");
        Assert.DoesNotContain(result.Altered, a => a.ColumnName == "code");
        Assert.DoesNotContain(result.Altered, a => a.ColumnName == "ratio");
        Assert.False(HasDrift(result.Drift, "label"));
        Assert.False(HasDrift(result.Drift, "amount"));
        Assert.True(HasDrift(result.Drift, "code"));
        Assert.True(HasDrift(result.Drift, "ratio"));

        var label = await db.GetColumnAsync(null, tableName, "label");
        Assert.Equal(200, label!.Length);
        Assert.True(label.IsNullable);
        Assert.Equal(12, (await db.GetColumnAsync(null, tableName, "amount"))!.Precision);

        // The unsafe differences were left exactly as they were.
        var code = await db.GetColumnAsync(null, tableName, "code");
        Assert.Equal(20, code!.Length);
        Assert.True(code.IsNullable);
        Assert.Equal(2, (await db.GetColumnAsync(null, tableName, "ratio"))!.Scale);

        Assert.Equal("hello", await db.ExecuteScalarAsync<string>($"SELECT label FROM {tableName} WHERE id = 1"));

        // Nothing left to alter.
        var second = await db.ReconcileTableAsync(v2, applySafeAlterations: true);
        Assert.False(second.Changed);

        await db.DropTableIfExistsAsync(null, tableName);
    }

    /// <summary>
    /// Some providers refuse alterations that are safe for the data -- PostgreSQL will not change the type of
    /// a column a view depends on, SQL Server will not resize an indexed decimal column. A refusal is reported
    /// as blocked, and the rest of the run, inside the same transaction, carries on.
    /// </summary>
    [Fact]
    protected virtual async Task Reconcile_reports_a_refused_alteration_as_blocked_Async()
    {
        var v1 = DmTableFactory.GetTable(typeof(ReconcileRefusedV1));
        var v2 = DmTableFactory.GetTable(typeof(ReconcileRefusedV2));
        var tableName = v1.TableName;
        const string viewName = "reconcile_refused_v";

        using var db = await OpenConnectionAsync();
        await db.DropViewIfExistsAsync(null, viewName);
        await db.DropTableIfExistsAsync(null, tableName);
        await db.CreateTableIfNotExistsAsync(v1);
        await db.CreateViewIfNotExistsAsync(null, viewName, $"SELECT v_col FROM {tableName}");

        var columns = new[] { "d_idx", "v_col", "c" };
        var plan = await db.GetTableReconciliationPlanAsync(v2, applySafeAlterations: true);

        // Each column is planned, or blocked up front where the provider is known to refuse it.
        foreach (var name in columns)
        {
            Assert.True(
                plan.Alterations.Any(a => a.ColumnName == name) ^ plan.Blocked.Any(b => b.Name == name),
                $"{name} must be planned or blocked"
            );
        }

        DmTableReconciliationResult result;
        using (var tx = db.BeginTransaction())
        {
            result = await db.ReconcileTableAsync(v2, applySafeAlterations: true, tx: tx);
            tx.Commit();
        }

        foreach (var a in result.Altered)
        {
            Output.WriteLine("ALTERED: {0}", a);
        }

        foreach (var b in result.Blocked)
        {
            Output.WriteLine("BLOCKED: {0}", b);
        }

        var table = await db.GetTableAsync(null, tableName);

        foreach (var name in columns)
        {
            var column = table!.Columns.Single(c => c.ColumnName.Equals(name, StringComparison.OrdinalIgnoreCase));
            var wasAltered = result.Altered.Any(a => a.ColumnName == name);
            var wasBlocked = result.Blocked.Any(b =>
                b.ElementType == DmSchemaElementType.Column && b.Name == name && b.Note is { Length: > 0 }
            );

            // Every column ends up in exactly one place, and the database agrees with it.
            Assert.True(wasAltered ^ wasBlocked, $"{name} must be altered or blocked, not both or neither");
            var (expectedSize, actualSize) = name == "d_idx"
                ? (wasAltered ? 12 : 10, column.Precision)
                : (wasAltered ? 200 : 50, column.Length);
            Assert.Equal(expectedSize, actualSize);
        }

        // Last in the plan, after any refusal: the transaction survived it.
        Assert.Contains(result.Altered, a => a.ColumnName == "c");

        // Changing only the nullability of the view's column works everywhere -- on PostgreSQL because no
        // type change is issued, which is what the view prevents.
        var viewColumn = table!.Columns.Single(c => c.ColumnName.Equals("v_col", StringComparison.OrdinalIgnoreCase));
        Assert.True(
            await db.AlterColumnIfExistsAsync(
                null,
                tableName,
                "v_col",
                viewColumn.GetProviderDataType(db.GetDbProviderType())!,
                isNullable: true
            )
        );
        Assert.True((await db.GetColumnAsync(null, tableName, "v_col"))!.IsNullable);

        await db.DropViewIfExistsAsync(null, viewName);
        await db.DropTableIfExistsAsync(null, tableName);
    }
}

[DmTable(null, "alter_target")]
public class AlterTarget
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }

    [DmColumn("label", length: 50)]
    [DmDefaultConstraint("'abc'")]
    public string Label { get; set; } = default!;

    [DmColumn("amount", precision: 10, scale: 2)]
    [DmDefaultConstraint("0")]
    public decimal Amount { get; set; }
}

[DmTable(null, "reconcile_alter")]
public class ReconcileAlterV1
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }

    [DmColumn("label", length: 50)]
    public string Label { get; set; } = default!;

    [DmColumn("amount", precision: 10, scale: 2)]
    public decimal Amount { get; set; }

    [DmColumn("code", length: 20, isNullable: true)]
    public string? Code { get; set; }

    [DmColumn("ratio", precision: 10, scale: 2)]
    public decimal Ratio { get; set; }
}

[DmTable(null, "reconcile_alter")]
public class ReconcileAlterV2
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }

    // Longer and nullable: both safe.
    [DmColumn("label", length: 200, isNullable: true)]
    public string? Label { get; set; }

    // More digits before the point: safe.
    [DmColumn("amount", precision: 12, scale: 2)]
    public decimal Amount { get; set; }

    // Shorter and NOT NULL: neither is safe.
    [DmColumn("code", length: 10)]
    public string Code { get; set; } = default!;

    // Same precision, more scale: loses two digits before the point.
    [DmColumn("ratio", precision: 10, scale: 4)]
    public decimal Ratio { get; set; }
}

[DmTable(null, "reconcile_refused")]
public class ReconcileRefusedV1
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }

    [DmColumn("d_idx", precision: 10, scale: 2)]
    [DmIndex(false, null, "ix_reconcile_refused_d_idx")]
    public decimal DIdx { get; set; }

    [DmColumn("v_col", length: 50)]
    public string VCol { get; set; } = default!;

    [DmColumn("c", length: 50)]
    public string C { get; set; } = default!;
}

[DmTable(null, "reconcile_refused")]
public class ReconcileRefusedV2
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }

    [DmColumn("d_idx", precision: 12, scale: 2)]
    [DmIndex(false, null, "ix_reconcile_refused_d_idx")]
    public decimal DIdx { get; set; }

    [DmColumn("v_col", length: 200)]
    public string VCol { get; set; } = default!;

    [DmColumn("c", length: 200)]
    public string C { get; set; } = default!;
}
