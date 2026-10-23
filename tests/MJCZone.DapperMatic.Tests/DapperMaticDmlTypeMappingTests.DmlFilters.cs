// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using MJCZone.DapperMatic.DataAnnotations;
using MJCZone.DapperMatic.Dml;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Tests;

/// <summary>
/// The DML methods that take a condition: reads, counts, updates and deletes of the rows that match.
/// </summary>
public abstract partial class DapperMaticDmlTypeMappingTests
{
    [Fact]
    protected virtual async Task Rows_are_selected_by_conditions_with_their_meaning_in_csharp_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        await SeedFilterOrdersAsync(db);

        // The ids as text: an expected int[] would bind to xUnit's span overload, which cannot span an await.
        async Task<string> Ids(System.Linq.Expressions.Expression<Func<FilterOrder, bool>> where) =>
            string.Join(",", (await db.SelectAsync(where)).Select(o => o.Id).Order());

        Assert.Equal("2,5", await Ids(o => o.Status == FilterStatus.Shipped));
        Assert.Equal("2,4", await Ids(o => o.Total > 20));

        // A NULL note is != "leave at door" in C#, and a NULL total is not > 20, as SQL alone would not have it.
        Assert.Equal("1,3,4,5,6", await Ids(o => o.Note != "leave at door"));
        Assert.Equal("1,3,5,6", await Ids(o => !(o.Total > 20)));
        Assert.Equal("1", await Ids(o => o.Note == null));
        Assert.Equal("3", await Ids(o => !o.Total.HasValue));

        Assert.Equal("2,5", await Ids(o => o.Shipped));
        Assert.Equal("1,3,4,6", await Ids(o => !o.Shipped));
        Assert.Equal("5", await Ids(o => o.Shipped && o.Total < 10));
        Assert.Equal("1,3,6", await Ids(o => o.Status == FilterStatus.Pending || o.Note == null));

        var ids = new List<int> { 1, 3 };
        int[] array = [2];
        Assert.Equal("1,3", await Ids(o => ids.Contains(o.Id)));
        Assert.Equal("2", await Ids(o => array.Contains(o.Id)));
        Assert.Equal("2,4,5,6", await Ids(o => !ids.Contains(o.Id)));
        Assert.Equal(string.Empty, await Ids(o => new List<int>().Contains(o.Id)));

        var notes = new[] { "a~b", null };
        var notesWithoutNull = new[] { "a~b" };
        Assert.Equal("1,5", await Ids(o => notes.Contains(o.Note)));
        Assert.Equal("1,2,3,4,6", await Ids(o => !notesWithoutNull.Contains(o.Note)));

        // Wildcard characters in the value are matched as themselves.
        Assert.Equal("3", await Ids(o => o.Note!.Contains("%")));
        Assert.Equal("3", await Ids(o => o.Note!.StartsWith("50% off_")));
        Assert.Equal(string.Empty, await Ids(o => o.Note!.StartsWith("50_")));
        Assert.Equal("5", await Ids(o => o.Note!.Contains("~")));
        Assert.Equal("5", await Ids(o => o.CustomerName.Contains("[admin]")));
        Assert.Equal("1,4", await Ids(o => o.CustomerName.EndsWith("a")));

        // '' exactly: not the note of a single space, which SQL Server's = would also match.
        Assert.Equal("1,4", await Ids(o => string.IsNullOrEmpty(o.Note)));
        Assert.Equal("2,3,5,6", await Ids(o => !string.IsNullOrEmpty(o.Note)));

        var filter = new { MinTotal = 20m };
        Assert.Equal("2,4", await Ids(o => o.Total >= filter.MinTotal));
        Assert.Equal("1,2,3,4,5,6", await Ids(o => o.PlacedAt < DateTime.UtcNow.AddYears(10)));

        await db.DropTableIfExistsAsync(null, "dml_filter_orders");
    }

    [Fact]
    protected virtual async Task Counts_firsts_singles_and_existence_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        await SeedFilterOrdersAsync(db);

        Assert.Equal(6, await db.CountAsync<FilterOrder>());
        Assert.Equal(2, await db.CountAsync<FilterOrder>(o => o.Shipped));

        Assert.True(await db.ExistsAsync<FilterOrder>());
        Assert.True(await db.ExistsAsync<FilterOrder>(o => o.Total > 30));
        Assert.False(await db.ExistsAsync<FilterOrder>(o => o.Total > 100));

        Assert.Equal(2, (await db.FirstAsync<FilterOrder>(o => o.Shipped))?.Id);
        Assert.Equal(1, (await db.FirstAsync<FilterOrder>())?.Id);
        Assert.Null(await db.FirstAsync<FilterOrder>(o => o.Total > 1000));

        Assert.Equal(1, (await db.SingleAsync<FilterOrder>(o => o.CustomerName == "Ada"))?.Id);
        Assert.Null(await db.SingleAsync<FilterOrder>(o => o.CustomerName == "nobody"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SingleAsync<FilterOrder>(o => o.Shipped));

        await db.DropTableIfExistsAsync(null, "dml_filter_orders");
    }

    [Fact]
    protected virtual async Task Rows_are_updated_and_deleted_by_condition_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        await SeedFilterOrdersAsync(db);

        Assert.Equal(
            1,
            await db.UpdateAsync<FilterOrder>(o => new FilterOrder { Status = FilterStatus.Shipped, Note = null }, o => o.Id == 2)
        );
        var two = (await db.SingleByIdAsync<FilterOrder>(2))!;
        Assert.Equal(FilterStatus.Shipped, two.Status);
        Assert.Null(two.Note);

        // A column from another column of the same row.
        await db.UpdateAsync<FilterOrder>(o => new FilterOrder { Note = o.CustomerName }, o => o.Id == 4);
        Assert.Equal("Barbara", (await db.SingleByIdAsync<FilterOrder>(4))?.Note);

        Assert.Equal(4, await db.UpdateAsync<FilterOrder>(o => new FilterOrder { Note = "bulk" }, o => !o.Shipped));
        Assert.Equal(4, await db.CountAsync<FilterOrder>(o => o.Note == "bulk"));

        // Nothing to set: no statement at all.
        Assert.Equal(0, await db.UpdateAsync<FilterOrder>(o => new FilterOrder(), o => true));

        Assert.Equal(1, await db.DeleteAsync<FilterOrder>(o => o.Status == FilterStatus.Cancelled));
        Assert.Equal(5, await db.CountAsync<FilterOrder>());
        Assert.Equal(5, await db.DeleteAllAsync<FilterOrder>());
        Assert.Equal(0, await db.CountAsync<FilterOrder>());

        await db.DropTableIfExistsAsync(null, "dml_filter_orders");
    }

    [Fact]
    protected virtual async Task Conditions_that_cannot_be_translated_and_writes_to_views_are_refused_Async()
    {
        using var db = await OpenConnectionAsync();

        // Refused before anything is sent: the table does not exist.
        await Assert.ThrowsAsync<DmTranslationException>(() =>
            db.SelectAsync<FilterOrder>(o => o.CustomerName.GetHashCode() == 1)
        );
        await Assert.ThrowsAsync<DmTranslationException>(() =>
            db.DeleteAsync<FilterOrder>(o => o.CustomerName.Normalize() == "x")
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.InsertAsync(new FilterOrderView()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.DeleteAllAsync<FilterOrderView>());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            db.UpdateAsync<FilterOrderView>(o => new FilterOrderView { CustomerName = "x" }, o => true)
        );
    }

    [Fact]
    protected virtual async Task Views_are_read_like_tables_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        await SeedFilterOrdersAsync(db);

        var view = DmViewFactory.GetView(typeof(FilterOrderView))!;
        await db.DropViewIfExistsAsync(view.SchemaName, view.ViewName);
        await db.CreateViewIfNotExistsAsync(view);

        var shipped = await db.SelectAsync<FilterOrderView>(o => o.CustomerName.StartsWith("G"));
        Assert.Equal("Grace", Assert.Single(shipped).CustomerName);
        Assert.Equal(2, await db.CountAsync<FilterOrderView>());

        await db.DropViewIfExistsAsync(view.SchemaName, view.ViewName);
        await db.DropTableIfExistsAsync(null, "dml_filter_orders");
    }

    private static async Task SeedFilterOrdersAsync(IDbConnection db)
    {
        await RecreateAsync<FilterOrder>(db);
        var placed = new DateTime(2026, 10, 20, 9, 30, 0, DateTimeKind.Unspecified);
        await db.InsertManyAsync(
            [
                new FilterOrder { CustomerName = "Ada", Total = 10m, Status = FilterStatus.Pending, PlacedAt = placed },
                new FilterOrder
                {
                    CustomerName = "Grace",
                    Total = 25.5m,
                    Status = FilterStatus.Shipped,
                    Note = "leave at door",
                    Shipped = true,
                    PlacedAt = placed,
                },
                new FilterOrder { CustomerName = "Linus", Status = FilterStatus.Pending, Note = "50% off_now", PlacedAt = placed },
                new FilterOrder
                {
                    CustomerName = "Barbara",
                    Total = 40m,
                    Status = FilterStatus.Cancelled,
                    Note = string.Empty,
                    PlacedAt = placed,
                },
                new FilterOrder
                {
                    CustomerName = "Ken [admin]",
                    Total = 5m,
                    Status = FilterStatus.Shipped,
                    Note = "a~b",
                    Shipped = true,
                    PlacedAt = placed,
                },
                new FilterOrder { CustomerName = "Space", Total = 1m, Note = " ", PlacedAt = placed },
            ]
        );
    }

    [DmTable(tableName: "dml_filter_orders")]
    public class FilterOrder
    {
        [DmColumn("id", isPrimaryKey: true, isAutoIncrement: true)]
        public int Id { get; set; }

        [DmColumn("customer_name", length: 100)]
        public string CustomerName { get; set; } = string.Empty;

        [DmColumn("total", precision: 12, scale: 2, isNullable: true)]
        public decimal? Total { get; set; }

        [DmColumn("status")]
        public FilterStatus Status { get; set; }

        [DmColumn("note", length: 200, isNullable: true)]
        public string? Note { get; set; }

        [DmColumn("shipped")]
        public bool Shipped { get; set; }

        [DmColumn("placed_at")]
        public DateTime PlacedAt { get; set; }
    }

    public enum FilterStatus
    {
        Pending,
        Shipped,
        Cancelled,
    }

    [DmView("SELECT id, customer_name FROM dml_filter_orders WHERE total > 20", viewName: "dml_filter_large_orders")]
    public class FilterOrderView
    {
        [DmColumn("id")]
        public int Id { get; set; }

        [DmColumn("customer_name")]
        public string CustomerName { get; set; } = string.Empty;
    }
}
