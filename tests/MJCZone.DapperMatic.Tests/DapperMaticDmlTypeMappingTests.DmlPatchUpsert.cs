// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.DataAnnotations;
using MJCZone.DapperMatic.Dml;

namespace MJCZone.DapperMatic.Tests;

/// <summary>
/// Patches, upserts, and lists longer than a provider's parameter limit.
/// </summary>
public abstract partial class DapperMaticDmlTypeMappingTests
{
    [Fact]
    protected virtual async Task Patches_set_only_what_they_hold_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        await SeedFilterOrdersAsync(db);

        // By condition, from an anonymous object; the other columns are unchanged.
        Assert.Equal(1, await db.PatchAsync<FilterOrder>(new { Note = "patched" }, o => o.Id == 1));
        var one = (await db.SingleByIdAsync<FilterOrder>(1))!;
        Assert.Equal("patched", one.Note);
        Assert.Equal("Ada", one.CustomerName);
        Assert.Equal(10m, one.Total);

        // By the key the patch holds: null properties are left alone.
        Assert.Equal(1, await db.PatchAsync<FilterOrder>(new FilterOrderPatch { Id = 2, Total = 99m }));
        var two = (await db.SingleByIdAsync<FilterOrder>(2))!;
        Assert.Equal(99m, two.Total);
        Assert.Equal("leave at door", two.Note);
        Assert.Equal(FilterStatus.Shipped, two.Status);

        // Clearing columns, which a null in the patch cannot say.
        Assert.Equal(1, await db.PatchAsync<FilterOrder>(new FilterOrderPatch { Id = 2 }, setNull: o => new { o.Note, o.Total }));
        two = (await db.SingleByIdAsync<FilterOrder>(2))!;
        Assert.Null(two.Note);
        Assert.Null(two.Total);
        Assert.Equal(1, await db.PatchAsync<FilterOrder>(new FilterOrderPatch { Id = 3 }, setNull: o => o.Note));
        Assert.Null((await db.SingleByIdAsync<FilterOrder>(3))!.Note);

        // A value of another type is converted to the column's; a property may name the column instead.
        Assert.Equal(2, await db.PatchAsync<FilterOrder>(new { Total = 5, customer_name = "shipped" }, o => o.Shipped));
        Assert.Equal(2, await db.CountAsync<FilterOrder>(o => o.CustomerName == "shipped" && o.Total == 5));

        // Nothing to set: no statement at all.
        Assert.Equal(0, await db.PatchAsync<FilterOrder>(new FilterOrderPatch { Id = 3 }));

        // A model known at run time, clearing columns by name.
        Assert.Equal(1, await db.PatchAsync(typeof(FilterOrder), new FilterOrderPatch { Id = 4, Note = "typed" }, ["Total"]));
        var four = (await db.SingleByIdAsync<FilterOrder>(4))!;
        Assert.Equal("typed", four.Note);
        Assert.Null(four.Total);

        // Refused before anything is sent.
        await Assert.ThrowsAsync<ArgumentException>(() => db.PatchAsync<FilterOrder>(new { Nope = 1 }, o => o.Id == 1));
        await Assert.ThrowsAsync<ArgumentException>(() => db.PatchAsync<FilterOrder>(new { Note = "x" }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            db.PatchAsync<FilterOrder>(new FilterOrderPatch { Id = 1 }, setNull: o => o.CustomerName)
        );
        await Assert.ThrowsAsync<ArgumentException>(() =>
            db.PatchAsync<FilterOrder>(new FilterOrderPatch { Id = 1, Note = "x" }, setNull: o => o.Note)
        );

        await db.DropTableIfExistsAsync(null, "dml_filter_orders");
    }

    [Fact]
    protected virtual async Task Upserts_insert_or_update_by_key_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        await RecreateAsync<CrudCode>(db);
        await RecreateAsync<CrudOrderLine>(db);
        await RecreateAsync<CrudOrder>(db);

        Assert.Equal("A", await db.UpsertAsync(new CrudCode { Code = "A", Label = "first" }));
        Assert.Equal("A", await db.UpsertAsync(new CrudCode { Code = "A", Label = "second" }));
        Assert.Equal("second", Assert.Single(await db.SelectAsync<CrudCode>()).Label);

        // A composite key.
        await db.UpsertAsync(new CrudOrderLine { OrderId = 1, LineNumber = 1, Sku = "a", Qty = 1 });
        await db.UpsertAsync(new CrudOrderLine { OrderId = 1, LineNumber = 1, Sku = "a", Qty = 5 });
        await db.UpsertAsync(new CrudOrderLine { OrderId = 1, LineNumber = 2, Sku = "b", Qty = 2 });
        Assert.Equal("1:5,2:2", string.Join(",", (await db.From<CrudOrderLine>().OrderBy(l => l.LineNumber).ToListAsync()).Select(l => $"{l.LineNumber}:{l.Qty}")));

        // A generated key: unset inserts and is given one; set updates; set but absent is an error, not an insert.
        var order = NewOrder("Ada", 1m);
        var id = await db.UpsertAsync(order);
        Assert.Equal(order.Id, id);
        order.Total = 2m;
        Assert.Equal(id, await db.UpsertAsync(order));
        Assert.Equal(2m, Assert.Single(await db.SelectAsync<CrudOrder>()).Total);
        order.Id = 999_999;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.UpsertAsync(order));

        Assert.Equal("B", await db.UpsertAsync(typeof(CrudCode), new CrudCode { Code = "B", Label = "typed" }));
        Assert.Equal(2, await db.CountAsync<CrudCode>());

        await db.DropTableIfExistsAsync(null, "dml_crud_codes");
        await db.DropTableIfExistsAsync(null, "dml_crud_lines");
        await db.DropTableIfExistsAsync(null, "dml_crud_orders");
    }

    /// <summary>
    /// Upserts of one key from several connections at once leave one row and raise nothing: a check-then-insert
    /// would let two of them insert.
    /// </summary>
    [Fact]
    protected virtual async Task Concurrent_upserts_of_a_key_leave_one_row_Async()
    {
        using var db = await OpenConnectionAsync();
        if (db.GetDbProviderType() == DbProviderType.Sqlite)
        {
            // Each test connection opens a fresh database file; SQLite also runs one writer at a time.
            return;
        }

        await InitFreshSchemaAsync(db, null);
        await RecreateAsync<CrudCode>(db);

        var upserts = Enumerable.Range(0, 8).Select(async i =>
        {
            using var connection = await OpenConnectionAsync();
            for (var round = 0; round < 5; round++)
            {
                await connection.UpsertAsync(new CrudCode { Code = "C", Label = $"writer {i} round {round}" });
            }
        });
        await Task.WhenAll(upserts);

        Assert.Single(await db.SelectAsync<CrudCode>());
        await db.DropTableIfExistsAsync(null, "dml_crud_codes");
    }

    [Fact]
    protected virtual async Task Upserts_merge_on_unique_columns_with_expressions_and_conditions_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        await RecreateAsync<PageStat>(db);

        var day = new DateOnly(2026, 10, 21);
        var t1 = new DateTime(2026, 10, 21, 9, 0, 0);
        DmUpsertOptions<PageStat> counting = new()
        {
            OnConflict = s => new { s.Day, s.Page },
            Update = (current, incoming) => new PageStat { Views = current.Views + incoming.Views, LastSeen = incoming.LastSeen },
            UpdateWhere = (current, incoming) => incoming.LastSeen > current.LastSeen,
        };

        var first = new PageStat { Day = day, Page = "home", Views = 1, LastSeen = t1 };
        var id = await db.UpsertAsync(first, counting);
        Assert.Equal(first.Id, id);
        Assert.True(first.Id > 0);

        // A conflict: the views add up, and the conflicting row's key comes back.
        var second = new PageStat { Day = day, Page = "home", Views = 2, LastSeen = t1.AddHours(1) };
        Assert.Equal(id, await db.UpsertAsync(second, counting));
        Assert.Equal(first.Id, second.Id);
        var stat = Assert.Single(await db.SelectAsync<PageStat>());
        Assert.Equal((3, t1.AddHours(1)), (stat.Views, stat.LastSeen));

        // An older row: the condition leaves the row as it is, and its key still comes back.
        var stale = new PageStat { Day = day, Page = "home", Views = 50, LastSeen = t1.AddHours(-1) };
        Assert.Equal(id, await db.UpsertAsync(stale, counting));
        Assert.Equal(3, Assert.Single(await db.SelectAsync<PageStat>()).Views);

        // By the constraint's name, and with no Update: every other column is the incoming row's.
        await db.UpsertAsync(
            new PageStat { Day = day, Page = "home", Views = 7, LastSeen = t1 },
            new DmUpsertOptions<PageStat> { OnConflictConstraint = "uc_page_stats_day_page" }
        );
        stat = Assert.Single(await db.SelectAsync<PageStat>());
        Assert.Equal((7, t1), (stat.Views, stat.LastSeen));

        // Another page is another row.
        await db.UpsertAsync(new PageStat { Day = day, Page = "about", Views = 1, LastSeen = t1 }, counting);
        Assert.Equal(2, await db.CountAsync<PageStat>());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            db.UpsertAsync(first, new DmUpsertOptions<PageStat> { OnConflict = s => s.Views })
        );

        await db.DropTableIfExistsAsync(null, "dml_page_stats");
    }

    [Fact]
    protected virtual async Task Rows_are_inserted_only_if_absent_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        await RecreateAsync<CrudCode>(db);
        await RecreateAsync<PageStat>(db);
        await RecreateAsync<CrudOrder>(db);

        Assert.True(await db.InsertIfAbsentAsync(new CrudCode { Code = "A", Label = "first" }));
        Assert.False(await db.InsertIfAbsentAsync(new CrudCode { Code = "A", Label = "second" }));
        Assert.Equal("first", (await db.SingleByIdAsync<CrudCode>("A"))?.Label);
        Assert.True(await db.InsertIfAbsentAsync(typeof(CrudCode), new CrudCode { Code = "B", Label = "typed" }));

        var day = new DateOnly(2026, 10, 21);
        var stat = new PageStat { Day = day, Page = "home", Views = 1, LastSeen = DateTime.UnixEpoch };
        Assert.True(await db.InsertIfAbsentAsync(stat, s => new { s.Day, s.Page }));
        Assert.True(stat.Id > 0);
        var again = new PageStat { Day = day, Page = "home", Views = 9, LastSeen = DateTime.UnixEpoch };
        Assert.False(await db.InsertIfAbsentAsync(again, s => new { s.Day, s.Page }));
        Assert.Equal(0, again.Id);
        Assert.Equal(1, Assert.Single(await db.SelectAsync<PageStat>()).Views);

        // On a generated key: unset, nothing can conflict.
        var order = NewOrder("Ada", 1m);
        Assert.True(await db.InsertIfAbsentAsync(order));
        Assert.True(order.Id > 0);

        await db.DropTableIfExistsAsync(null, "dml_crud_codes");
        await db.DropTableIfExistsAsync(null, "dml_page_stats");
        await db.DropTableIfExistsAsync(null, "dml_crud_orders");
    }

    /// <summary>
    /// Counter upserts from several connections at once lose no increments, and inserts-if-absent of one key from
    /// several connections insert it once.
    /// </summary>
    [Fact]
    protected virtual async Task Concurrent_counters_and_inserts_if_absent_Async()
    {
        using var db = await OpenConnectionAsync();
        if (db.GetDbProviderType() == DbProviderType.Sqlite)
        {
            // Each test connection opens a fresh database file; SQLite also runs one writer at a time.
            return;
        }

        await InitFreshSchemaAsync(db, null);
        await RecreateAsync<PageStat>(db);

        var day = new DateOnly(2026, 10, 21);
        DmUpsertOptions<PageStat> counting = new()
        {
            OnConflict = s => new { s.Day, s.Page },
            Update = (current, incoming) => new PageStat { Views = current.Views + incoming.Views },
        };

        var inserted = 0;
        await Task.WhenAll(
            Enumerable.Range(0, 8).Select(async _ =>
            {
                using var connection = await OpenConnectionAsync();
                for (var round = 0; round < 5; round++)
                {
                    await connection.UpsertAsync(new PageStat { Day = day, Page = "home", Views = 1, LastSeen = DateTime.UnixEpoch }, counting);
                    if (await connection.InsertIfAbsentAsync(new PageStat { Day = day, Page = $"once", Views = 1, LastSeen = DateTime.UnixEpoch }, s => new { s.Day, s.Page }))
                    {
                        Interlocked.Increment(ref inserted);
                    }
                }
            })
        );

        Assert.Equal(40, (await db.SingleAsync<PageStat>(s => s.Page == "home"))?.Views);
        Assert.Equal(1, inserted);
        Assert.Equal(2, await db.CountAsync<PageStat>());

        await db.DropTableIfExistsAsync(null, "dml_page_stats");
    }

    /// <summary>
    /// A list longer than the provider allows parameters in one statement: SQL Server allows 2,100.
    /// </summary>
    [Fact]
    protected virtual async Task Long_lists_are_matched_past_the_parameter_limit_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        await SeedFilterOrdersAsync(db);

        var ids = Enumerable.Range(1, 2_500).ToList();
        Assert.Equal(6, (await db.SelectAsync<FilterOrder>(o => ids.Contains(o.Id))).Count);
        Assert.Equal(0, await db.CountAsync<FilterOrder>(o => !ids.Contains(o.Id)));
        Assert.Equal(1, await db.CountAsync<FilterOrder>(o => ids.Skip(3).Contains(o.Id) && o.CustomerName == "Barbara"));

        var statuses = Enumerable.Repeat(FilterStatus.Shipped, 2_500).ToArray();
        Assert.Equal(2, await db.CountAsync<FilterOrder>(o => statuses.Contains(o.Status)));

        var names = Enumerable.Range(0, 2_500).Select(i => $"name {i}").Append("Grace").ToList();
        Assert.Equal(2, (await db.SingleAsync<FilterOrder>(o => names.Contains(o.CustomerName)))?.Id);

        await db.DropTableIfExistsAsync(null, "dml_filter_orders");
    }

    [DmTable(tableName: "dml_page_stats")]
    [DmUniqueConstraint(columnNames: ["stat_day", "page"], constraintName: "uc_page_stats_day_page")]
    public class PageStat
    {
        [DmColumn("id", isPrimaryKey: true, isAutoIncrement: true)]
        public int Id { get; set; }

        [DmColumn("stat_day")]
        public DateOnly Day { get; set; }

        [DmColumn("page", length: 50)]
        public string Page { get; set; } = string.Empty;

        [DmColumn("views")]
        public int Views { get; set; }

        [DmColumn("last_seen")]
        public DateTime LastSeen { get; set; }
    }

    public class FilterOrderPatch
    {
        public int? Id { get; set; }

        public string? Note { get; set; }

        public decimal? Total { get; set; }

        public FilterStatus? Status { get; set; }
    }
}
