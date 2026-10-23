// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using MJCZone.DapperMatic.Dml;

namespace MJCZone.DapperMatic.Tests;

/// <summary>
/// Mass loading through each provider's native bulk path, and mass upserts through a staging table.
/// </summary>
public abstract partial class DapperMaticDmlTypeMappingTests
{
    /// <summary>
    /// Gets a value indicating whether the connection's driver has a native bulk path the tests can rely on: all but
    /// MySql.Data, which has none (MySqlConnector's is tested with local_infile enabled).
    /// </summary>
    /// <param name="db">The connection.</param>
    /// <returns>True if a bulk load must not fall back.</returns>
    protected static bool HasNativeBulkPath(IDbConnection db) =>
        db.GetDbProviderType() != DbProviderType.MySql || db.GetType().Namespace == "MySqlConnector";

    [Fact]
    protected virtual async Task Rows_are_bulk_inserted_through_the_native_path_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        await RecreateAsync<CrudOrder>(db);

        // Fallback: Throw where a native path exists, so that a silent fallback fails the test.
        var options = new DmBulkOptions
        {
            Fallback = HasNativeBulkPath(db) ? DmBulkFallback.Throw : DmBulkFallback.MultiRowValues,
            BatchSize = 1_000,
        };

        var rows = Enumerable.Range(0, 5_000).Select(i =>
        {
            var order = NewOrder($"customer {i}", i + 0.25m);
            order.Note = i % 2 == 0 ? null : $"note {i}";
            order.Status = i % 3 == 0 ? CrudStatus.Shipped : CrudStatus.Pending;
            order.Lines = [new CrudLine { Sku = $"sku-{i}", Qty = i }];
            return order;
        });

        Assert.Equal(5_000, await db.BulkInsertAsync(rows, options));
        Assert.Equal(5_000, await db.CountAsync<CrudOrder>());

        var read = await db.SingleAsync<CrudOrder>(o => o.CustomerName == "customer 4999");
        Assert.NotNull(read);
        Assert.Equal(4999.25m, read.Total);
        Assert.Equal("note 4999", read.Note);
        Assert.Equal(CrudStatus.Pending, read.Status);
        Assert.Equal(new DateTime(2026, 10, 20, 9, 30, 0), read.PlacedAt);
        Assert.Equal(("sku-4999", 4999), (Assert.Single(read.Lines!).Sku, read.Lines![0].Qty));
        Assert.Null((await db.SingleAsync<CrudOrder>(o => o.CustomerName == "customer 4998"))?.Note);
        Assert.Equal(1_667, await db.CountAsync<CrudOrder>(o => o.Status == CrudStatus.Shipped));

        // All or none: the last row breaks a NOT NULL, so nothing stays.
        await db.DeleteAllAsync<CrudOrder>();
        var broken = Enumerable.Range(0, 2_500).Select(i => NewOrder(i == 2_499 ? null! : $"c{i}", i)).ToList();
        await Assert.ThrowsAnyAsync<Exception>(() => db.BulkInsertAsync(broken, options));
        Assert.Equal(0, await db.CountAsync<CrudOrder>());

        // In the caller's transaction, which then rolls back.
        if (db.State != ConnectionState.Open)
        {
            db.Open();
        }

        using (var tx = db.BeginTransaction())
        {
            Assert.Equal(10, await db.BulkInsertAsync(Enumerable.Range(0, 10).Select(i => NewOrder($"t{i}", i)), options, tx));
            Assert.Equal(10, await db.CountAsync<CrudOrder>(tx: tx));
            tx.Rollback();
        }

        Assert.Equal(0, await db.CountAsync<CrudOrder>());

        // A model known at run time, and rows typed object.
        Assert.Equal(3, await db.BulkInsertAsync(typeof(CrudOrder), [NewOrder("a", 1m), NewOrder("b", 2m), NewOrder("c", 3m)], options));
        Assert.Equal(2, await db.BulkInsertAsync<object>([NewOrder("d", 4m), NewOrder("e", 5m)], options));
        Assert.Equal(5, await db.CountAsync<CrudOrder>());

        await db.DropTableIfExistsAsync(null, "dml_crud_orders");
    }

    [Fact]
    protected virtual async Task Rows_are_bulk_upserted_through_a_staging_table_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        await RecreateAsync<PageStat>(db);
        await RecreateAsync<CrudCode>(db);

        var options = new DmBulkOptions { Fallback = HasNativeBulkPath(db) ? DmBulkFallback.Throw : DmBulkFallback.MultiRowValues };
        var day = new DateOnly(2026, 10, 21);
        var t1 = new DateTime(2026, 10, 21, 9, 0, 0);
        PageStat Stat(string page, int views, DateTime seen) => new() { Day = day, Page = page, Views = views, LastSeen = seen };

        await db.BulkInsertAsync([Stat("a", 1, t1), Stat("b", 1, t1), Stat("c", 1, t1)], options);

        DmUpsertOptions<PageStat> counting = new()
        {
            OnConflict = s => new { s.Day, s.Page },
            Update = (current, incoming) => new PageStat { Views = current.Views + incoming.Views, LastSeen = incoming.LastSeen },
            UpdateWhere = (current, incoming) => incoming.LastSeen > current.LastSeen,
        };

        // a and b conflict and are newer; c conflicts and is older, so it stays; d and e are new.
        var loaded = await db.BulkUpsertAsync(
            [Stat("a", 10, t1.AddHours(1)), Stat("b", 20, t1.AddHours(1)), Stat("c", 30, t1.AddHours(-1)), Stat("d", 1, t1), Stat("e", 2, t1)],
            counting,
            options
        );
        Assert.Equal(5, loaded);

        var views = (await db.SelectAsync<PageStat>()).ToDictionary(s => s.Page, s => s.Views);
        Assert.Equal("a=11,b=21,c=1,d=1,e=2", string.Join(",", views.OrderBy(v => v.Key).Select(v => $"{v.Key}={v.Value}")));

        // By the primary key, every other column replaced.
        await db.BulkInsertAsync([new CrudCode { Code = "A", Label = "first" }], options);
        Assert.Equal(2, await db.BulkUpsertAsync([new CrudCode { Code = "A", Label = "second" }, new CrudCode { Code = "B", Label = "new" }]));
        Assert.Equal("A=second,B=new", string.Join(",", (await db.SelectAsync<CrudCode>()).OrderBy(c => c.Code).Select(c => $"{c.Code}={c.Label}")));

        // New rows have no generated key to merge on.
        await Assert.ThrowsAsync<ArgumentException>(() => db.BulkUpsertAsync([NewOrder("x", 1m)]));

        await db.DropTableIfExistsAsync(null, "dml_page_stats");
        await db.DropTableIfExistsAsync(null, "dml_crud_codes");
    }
}
