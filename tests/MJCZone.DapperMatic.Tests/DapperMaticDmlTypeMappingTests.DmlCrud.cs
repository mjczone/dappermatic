// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using Dapper;
using MJCZone.DapperMatic.DataAnnotations;
using MJCZone.DapperMatic.Dml;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Tests;

/// <summary>
/// The DML methods that work by key: insert, update, delete, and reads by key.
/// </summary>
public abstract partial class DapperMaticDmlTypeMappingTests
{
    [Fact]
    protected virtual async Task Rows_are_inserted_read_updated_and_deleted_by_key_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        await RecreateAsync<CrudOrder>(db);

        var order = NewOrder("Ada", 12.5m);
        var key = await db.InsertAsync(order);
        Assert.Equal(order.Id, key);
        Assert.True(order.Id > 0);

        var second = NewOrder("Grace", 7m);
        await db.InsertAsync(second);
        Assert.NotEqual(order.Id, second.Id);

        var read = await db.SingleByIdAsync<CrudOrder>(order.Id);
        Assert.NotNull(read);
        Assert.Equal("Ada", read.CustomerName);
        Assert.Equal(12.5m, read.Total);
        Assert.Equal(CrudStatus.Pending, read.Status);
        Assert.Null(read.Note);
        Assert.Equal(order.PlacedAt, read.PlacedAt);
        Assert.Equal("sku-1", Assert.Single(read.Lines!).Sku);

        read.Status = CrudStatus.Shipped;
        read.Note = "left on the porch";
        read.Lines = [new CrudLine { Sku = "sku-2", Qty = 3 }];
        Assert.Equal(1, await db.UpdateAsync(read));

        var updated = await db.SingleByIdAsync<CrudOrder>(order.Id);
        Assert.Equal(CrudStatus.Shipped, updated!.Status);
        Assert.Equal("left on the porch", updated.Note);
        Assert.Equal(3, Assert.Single(updated.Lines!).Qty);

        // The key as another numeric type is converted to the key's own.
        Assert.NotNull(await db.SingleByIdAsync<CrudOrder>((long)order.Id));

        var both = await db.SelectByIdsAsync<CrudOrder>([order.Id, second.Id, 999_999]);
        Assert.Equal([order.Id, second.Id], both.Select(o => o.Id).Order().ToArray());

        Assert.Equal(1, await db.DeleteAsync(updated));
        Assert.Null(await db.SingleByIdAsync<CrudOrder>(order.Id));
        Assert.Equal(0, await db.UpdateAsync(updated));

        Assert.Equal(1, await db.DeleteByIdAsync<CrudOrder>(second.Id));
        Assert.Equal(0, await db.DeleteByIdAsync<CrudOrder>(second.Id));

        await db.DropTableIfExistsAsync(null, "dml_crud_orders");
    }

    [Fact]
    protected virtual async Task Composite_and_caller_provided_keys_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        await RecreateAsync<CrudOrderLine>(db);
        await RecreateAsync<CrudCode>(db);

        Assert.Null(await db.InsertAsync(new CrudOrderLine { OrderId = 1, LineNumber = 1, Sku = "a", Qty = 1 }));
        await db.InsertAsync(new CrudOrderLine { OrderId = 1, LineNumber = 2, Sku = "b", Qty = 2 });

        Assert.Equal("b", (await db.SingleByIdAsync<CrudOrderLine>(new { OrderId = 1, LineNumber = 2 }))?.Sku);
        Assert.Equal("a", (await db.SingleByIdAsync<CrudOrderLine>((1, 1)))?.Sku);
        Assert.Null(await db.SingleByIdAsync<CrudOrderLine>((2, 1)));

        var line = (await db.SingleByIdAsync<CrudOrderLine>((1, 2)))!;
        line.Qty = 20;
        Assert.Equal(1, await db.UpdateAsync(line));
        Assert.Equal(20, (await db.SingleByIdAsync<CrudOrderLine>((1, 2)))?.Qty);
        Assert.Equal(1, await db.DeleteByIdAsync<CrudOrderLine>((1, 2)));
        Assert.Null(await db.SingleByIdAsync<CrudOrderLine>((1, 2)));

        Assert.Equal("A-1", await db.InsertAsync(new CrudCode { Code = "A-1", Label = "first" }));
        Assert.Equal("first", (await db.SingleByIdAsync<CrudCode>("A-1"))?.Label);

        await db.DropTableIfExistsAsync(null, "dml_crud_lines");
        await db.DropTableIfExistsAsync(null, "dml_crud_codes");
    }

    /// <summary>
    /// Rows are inserted several to a statement, under the provider's limits on parameters and rows, and all or none:
    /// SQL Server allows 2,100 parameters and 1,000 rows a statement, and this takes several statements everywhere.
    /// </summary>
    [Fact]
    protected virtual async Task Many_rows_are_inserted_in_statements_within_the_limits_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        await RecreateAsync<CrudOrder>(db);

        var rows = Enumerable.Range(0, 2_500).Select(i => NewOrder($"customer {i}", i)).ToList();
        Assert.Equal(2_500, await db.InsertManyAsync(rows));
        Assert.Equal(2_500, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dml_crud_orders"));

        var last = (await db.QueryAsync<int>("SELECT MAX(id) FROM dml_crud_orders")).Single();
        var read = await db.SingleByIdAsync<CrudOrder>(last);
        Assert.Equal("customer 2499", read!.CustomerName);
        Assert.Equal("sku-1", Assert.Single(read.Lines!).Sku);

        // A row the database refuses, in the last statement: nothing from the earlier statements stays.
        await db.ExecuteAsync("DELETE FROM dml_crud_orders");
        rows[^1].CustomerName = null!;
        await Assert.ThrowsAnyAsync<Exception>(() => db.InsertManyAsync(rows));
        Assert.Equal(0, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dml_crud_orders"));

        await db.DropTableIfExistsAsync(null, "dml_crud_orders");
    }

    /// <summary>
    /// The same calls against a tenant's schema, or another table, as SaaS applications with a schema per tenant need.
    /// </summary>
    [Fact]
    protected virtual async Task A_schema_or_table_can_be_substituted_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        var model = DmTableFactory.GetTable(typeof(CrudCode));

        if (db.SupportsSchemas())
        {
            foreach (var tenant in new[] { "dml_tenant_a", "dml_tenant_b" })
            {
                await db.CreateSchemaIfNotExistsAsync(tenant);
                await db.DropTableIfExistsAsync(tenant, model.TableName);
                await db.CreateTableIfNotExistsAsync(tenant, model.TableName, ColumnsFor(model, tenant, model.TableName));
            }

            await db.InSchema("dml_tenant_a").InsertAsync(new CrudCode { Code = "T", Label = "tenant a" });

            Assert.Equal("tenant a", (await db.InSchema("dml_tenant_a").SingleByIdAsync<CrudCode>("T"))?.Label);
            Assert.Null(await db.InSchema("dml_tenant_b").SingleByIdAsync<CrudCode>("T"));

            foreach (var tenant in new[] { "dml_tenant_a", "dml_tenant_b" })
            {
                await db.DropTableIfExistsAsync(tenant, model.TableName);
            }
        }
        else
        {
            Assert.Throws<NotSupportedException>(() => db.InSchema("dml_tenant_a"));
        }

        await RecreateAsync<CrudCode>(db);
        const string archive = "dml_crud_codes_archive";
        await db.DropTableIfExistsAsync(null, archive);
        await db.CreateTableIfNotExistsAsync(null, archive, ColumnsFor(model, null, archive));

        await db.WithTableName<CrudCode>(archive).InsertAsync(new CrudCode { Code = "Z", Label = "archived" });

        Assert.Equal("archived", (await db.WithTableName<CrudCode>(archive).SingleByIdAsync<CrudCode>("Z"))?.Label);
        Assert.Null(await db.SingleByIdAsync<CrudCode>("Z"));

        await db.DropTableIfExistsAsync(null, archive);
        await db.DropTableIfExistsAsync(null, model.TableName);
    }

    [Fact]
    protected virtual async Task Operations_by_key_refuse_a_model_without_one_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        await RecreateAsync<CrudNoKey>(db);

        Assert.Null(await db.InsertAsync(new CrudNoKey { Name = "x" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.UpdateAsync(new CrudNoKey { Name = "x" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.DeleteAsync(new CrudNoKey { Name = "x" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SingleByIdAsync<CrudNoKey>("x"));

        await db.DropTableIfExistsAsync(null, "dml_crud_no_key");
    }

    /// <summary>
    /// A model known only at run time, as when scanning an assembly or serving a generic endpoint, works through the
    /// Type overloads, and a row passed as object is a row of its own type.
    /// </summary>
    [Fact]
    protected virtual async Task Rows_of_a_model_given_as_a_type_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        await RecreateAsync<CrudOrder>(db);
        var type = typeof(CrudOrder);

        object row = NewOrder("Ada", 12.5m);
        var key = await db.InsertAsync(row);
        Assert.Equal(((CrudOrder)row).Id, key);
        Assert.Equal(2, await db.InsertManyAsync(type, [NewOrder("Grace", 7m), NewOrder("Linus", 3m)]));

        var read = Assert.IsType<CrudOrder>(await db.SingleByIdAsync(type, key!));
        Assert.Equal("Ada", read.CustomerName);
        Assert.Equal(3, await db.CountAsync(type));
        Assert.True(await db.ExistsAsync(type));
        Assert.Equal(key, Assert.IsType<CrudOrder>(await db.FirstAsync(type)).Id);
        Assert.Equal(3, (await db.SelectAsync(type)).OfType<CrudOrder>().Count());
        Assert.Single(await db.SelectByIdsAsync(type, [key!]));

        read.Note = "typed at run time";
        Assert.Equal(1, await db.UpdateAsync(type, read));
        Assert.Equal("typed at run time", ((CrudOrder)(await db.SingleByIdAsync(type, key!))!).Note);

        Assert.Equal(1, await db.DeleteAsync(type, read));
        Assert.Equal(0, await db.DeleteByIdAsync(type, key!));
        Assert.Equal(2, await db.DeleteAllAsync(type));
        Assert.False(await db.ExistsAsync(type));

        // object is not a model; nor is a value.
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SelectAsync<object>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.CountAsync(typeof(int)));

        await db.DropTableIfExistsAsync(null, "dml_crud_orders");
    }

    private static CrudOrder NewOrder(string customer, decimal total) =>
        new()
        {
            CustomerName = customer,
            Total = total,
            Status = CrudStatus.Pending,
            PlacedAt = new DateTime(2026, 10, 20, 9, 30, 0, DateTimeKind.Unspecified),
            Lines = [new CrudLine { Sku = "sku-1", Qty = 1 }],
        };

    private static async Task RecreateAsync<T>(IDbConnection db)
    {
        var table = DmTableFactory.GetTable(typeof(T));
        await db.DropTableIfExistsAsync(table.SchemaName, table.TableName);
        await db.CreateTableIfNotExistsAsync(table);
    }

    private static DmColumn[] ColumnsFor(DmTable model, string? schemaName, string tableName) =>
        [
            .. model.Columns.Select(c =>
            {
                var column = c.Copy();
                column.SchemaName = schemaName;
                column.TableName = tableName;
                return column;
            }),
        ];

    [DmTable(tableName: "dml_crud_orders")]
    public class CrudOrder
    {
        [DmColumn("id", isPrimaryKey: true, isAutoIncrement: true)]
        public int Id { get; set; }

        [DmColumn("customer_name", length: 100)]
        public string CustomerName { get; set; } = string.Empty;

        [DmColumn("total", precision: 12, scale: 2)]
        public decimal Total { get; set; }

        [DmColumn("status")]
        public CrudStatus Status { get; set; }

        [DmColumn("note", length: 200, isNullable: true)]
        public string? Note { get; set; }

        [DmColumn("placed_at")]
        public DateTime PlacedAt { get; set; }

        [DmColumn("order_lines", isNullable: true)]
        public List<CrudLine>? Lines { get; set; }
    }

    public class CrudLine
    {
        public string Sku { get; set; } = string.Empty;

        public int Qty { get; set; }
    }

    public enum CrudStatus
    {
        Pending,
        Shipped,
    }

    // Unattributed key columns, which PostgreSQL's DDL lowercases: DML has to name them the same way. (Not LineNo:
    // a reserved word on SQL Server, and DDL does not quote column names.)
    [DmTable(tableName: "dml_crud_lines")]
    [DmPrimaryKeyConstraint([nameof(OrderId), nameof(LineNumber)])]
    public class CrudOrderLine
    {
        public int OrderId { get; set; }

        public int LineNumber { get; set; }

        [DmColumn("sku", length: 20)]
        public string Sku { get; set; } = string.Empty;

        public int Qty { get; set; }
    }

    [DmTable(tableName: "dml_crud_codes")]
    public class CrudCode
    {
        [DmColumn("code", isPrimaryKey: true, length: 20)]
        public string Code { get; set; } = string.Empty;

        [DmColumn("label", length: 50)]
        public string Label { get; set; } = string.Empty;
    }

    [DmTable(tableName: "dml_crud_no_key")]
    public class CrudNoKey
    {
        [DmColumn("name", length: 20)]
        public string Name { get; set; } = string.Empty;
    }
}
