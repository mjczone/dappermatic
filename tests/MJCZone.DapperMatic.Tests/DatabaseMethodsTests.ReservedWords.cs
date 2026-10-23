// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.DataAnnotations;
using MJCZone.DapperMatic.Dml;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Tests;

public abstract partial class DatabaseMethodsTests
{
    /// <summary>
    /// Names that are reserved words on some provider name columns, keys, indexes and constraints on every provider:
    /// DDL quotes every name it writes.
    /// </summary>
    [Fact]
    protected virtual async Task Reserved_words_can_name_columns_and_constraints_Async()
    {
        using var db = await OpenConnectionAsync();
        await db.DropTableIfExistsAsync<ReservedWords>();
        await db.DropTableIfExistsAsync<ReservedParent>();

        Assert.True(await db.CreateTableIfNotExistsAsync<ReservedParent>());
        Assert.True(await db.CreateTableIfNotExistsAsync<ReservedWords>());

        var model = DmTableFactory.GetTable(typeof(ReservedWords));
        var table = await db.GetTableAsync<ReservedWords>();
        Assert.NotNull(table);
        Assert.Equal(
            string.Join(",", model.Columns.Select(c => c.ColumnName.ToLowerInvariant()).Order()),
            string.Join(",", table.Columns.Select(c => c.ColumnName.ToLowerInvariant()).Order())
        );
        Assert.Equal("order,lineno", string.Join(",", table.PrimaryKeyConstraint!.Columns.Select(c => c.ColumnName.ToLowerInvariant())));
        Assert.True(await db.DoesIndexExistAsync<ReservedWords>("ix_reserved_key_lines"));
        Assert.Single(table.ForeignKeyConstraints);

        // Read back, the table matches the model it was built from.
        Assert.False((await db.GetTableReconciliationPlanAsync<ReservedWords>()).HasChanges);

        await db.InsertAsync(new ReservedParent { User = "ada" });
        await db.InsertAsync(new ReservedWords { Order = 1, LineNo = 2, User = "ada", Key = "k", Group = 3, Lines = 4, Qty = 5 });
        Assert.Equal("k", (await db.SingleByIdAsync<ReservedWords>((1, 2)))?.Key);

        Assert.True(await db.CreateColumnIfNotExistsAsync<ReservedWords>("limit", typeof(int), isNullable: true));
        Assert.True(await db.RenameColumnIfExistsAsync<ReservedWords>("select", "where"));
        Assert.True(await db.DoesColumnExistAsync<ReservedWords>("where"));
        Assert.True(await db.DropColumnIfExistsAsync<ReservedWords>("where"));
        Assert.True(
            await db.CreateIndexIfNotExistsAsync<ReservedWords>("ix_reserved_limit", [new DmOrderedColumn("limit", DmColumnOrder.Descending)])
        );
        Assert.True(await db.DropIndexIfExistsAsync<ReservedWords>("ix_reserved_limit"));
        Assert.True(await db.DropColumnIfExistsAsync<ReservedWords>("limit"));

        // The row survived every change.
        Assert.Equal(1, await db.CountAsync<ReservedWords>());

        await db.DropTableIfExistsAsync<ReservedWords>();
        await db.DropTableIfExistsAsync<ReservedParent>();
    }
}

[DmTable(tableName: "reserved_parents")]
public class ReservedParent
{
    [DmColumn("user", isPrimaryKey: true, length: 50)]
    public string User { get; set; } = string.Empty;
}

[DmTable(tableName: "reserved_words")]
[DmPrimaryKeyConstraint(["order", nameof(LineNo)])]
[DmIndex(columnNames: ["key", "lines DESC"], indexName: "ix_reserved_key_lines")]
public class ReservedWords
{
    [DmColumn("order")]
    public int Order { get; set; }

    // Unattributed, so named by the property: a T-SQL keyword.
    public int LineNo { get; set; }

    [DmColumn("user", length: 50, isForeignKey: true, referencedTableName: "reserved_parents", referencedColumnName: "user")]
    public string User { get; set; } = string.Empty;

    [DmColumn("select", length: 50, isNullable: true)]
    public string? Select { get; set; }

    [DmColumn("group", isIndexed: true)]
    public int Group { get; set; }

    [DmColumn("key", length: 50, isUnique: true)]
    public string Key { get; set; } = string.Empty;

    [DmColumn("lines")]
    public int Lines { get; set; }

    [DmColumn("index", defaultExpression: "0")]
    public int Index { get; set; }

    [DmColumn("qty", checkExpression: "qty >= 0")]
    public int Qty { get; set; }
}
