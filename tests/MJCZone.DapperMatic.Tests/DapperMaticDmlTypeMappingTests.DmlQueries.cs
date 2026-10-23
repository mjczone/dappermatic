// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.Dml;

namespace MJCZone.DapperMatic.Tests;

/// <summary>
/// Queries built with <c>From&lt;T&gt;()</c>: ordering, paging, functions, projections and aggregates.
/// </summary>
public abstract partial class DapperMaticDmlTypeMappingTests
{
    [Fact]
    protected virtual async Task Queries_are_ordered_and_paged_with_nulls_where_linq_puts_them_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        await SeedFilterOrdersAsync(db);

        // The ids as text: an expected int[] would bind to xUnit's span overload, which cannot span an await.
        static async Task<string> Ids(DmQuery<FilterOrder> query) =>
            string.Join(",", (await query.ToListAsync()).Select(o => o.Id));

        var orders = db.From<FilterOrder>();
        Assert.Equal("3,6,5,1,2,4", await Ids(orders.OrderBy(o => o.Total)));
        Assert.Equal("4,2,1,5,6,3", await Ids(orders.OrderByDescending(o => o.Total)));
        Assert.Equal("6,3,1,5,2,4", await Ids(orders.OrderBy(o => o.Status).ThenByDescending(o => o.Id)));

        var byId = orders.OrderBy(o => o.Id);
        Assert.Equal("3,4", await Ids(byId.Skip(2).Take(2)));
        Assert.Equal("5,6", await Ids(byId.Skip(4)));
        Assert.Equal("1,2", await Ids(byId.Take(2)));
        Assert.Equal(string.Empty, await Ids(byId.Take(0)));
        Assert.Equal("2,5", await Ids(orders.Where(o => o.Shipped).OrderByDescending(o => o.Total)));

        Assert.Equal(1, (await orders.FirstAsync())?.Id);
        Assert.Equal(4, (await orders.OrderByDescending(o => o.Total).FirstAsync())?.Id);
        Assert.Equal(2, (await orders.Where(o => o.CustomerName == "Grace").SingleAsync())?.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => orders.Where(o => o.Shipped).SingleAsync());

        await db.DropTableIfExistsAsync(null, "dml_filter_orders");
    }

    [Fact]
    protected virtual async Task Conditions_compute_with_functions_arithmetic_and_dates_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        await SeedFilterOrdersAsync(db);

        async Task<string> Ids(System.Linq.Expressions.Expression<Func<FilterOrder, bool>> where) =>
            string.Join(",", (await db.SelectAsync(where)).Select(o => o.Id).Order());

        Assert.Equal("1", await Ids(o => o.CustomerName.ToUpper() == "ADA"));
        Assert.Equal("2,3,6", await Ids(o => o.CustomerName.Length == 5));

        // A single space has a length of 1, though SQL Server's LEN leaves trailing spaces out.
        Assert.Equal("6", await Ids(o => o.Note!.Length == 1));
        Assert.Equal("4,6", await Ids(o => o.Note!.Trim() == string.Empty));

        Assert.Equal("1", await Ids(o => o.CustomerName.Contains("ADA", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal("2", await Ids(o => string.Equals(o.CustomerName, "grace", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal("2", await Ids(o => o.CustomerName.Substring(0, 2) == "Gr"));
        Assert.Equal("5", await Ids(o => o.CustomerName.Replace(" [admin]", string.Empty) == "Ken"));

        Assert.Equal("2,4", await Ids(o => o.Total * 2 > 50));
        Assert.Equal("2,3", await Ids(o => o.Id / 2 == 1));
        Assert.Equal("3", await Ids(o => (double)o.Id / 2 == 1.5));
        Assert.Equal("2,4,6", await Ids(o => o.Id % 2 == 0));
        Assert.Equal("1", await Ids(o => (o.Note ?? "none") == "none"));
        Assert.Equal("2", await Ids(o => (o.Shipped ? o.Total : 0) > 20));

        // Joining a NULL note adds nothing, as in C#.
        Assert.Equal("1", await Ids(o => o.CustomerName + "!" == "Ada!"));
        Assert.Equal("1", await Ids(o => o.CustomerName + o.Note == "Ada"));

        Assert.Equal(
            "1,2,3,4,5,6",
            await Ids(o =>
                o.PlacedAt.Year == 2026
                && o.PlacedAt.Month == 10
                && o.PlacedAt.Day == 20
                && o.PlacedAt.Hour == 9
                && o.PlacedAt.Minute == 30
                && o.PlacedAt.Second == 0
            )
        );

        await db.DropTableIfExistsAsync(null, "dml_filter_orders");
    }

    [Fact]
    protected virtual async Task Projections_read_the_columns_they_use_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        await SeedFilterOrdersAsync(db);

        var shipped = await db.From<FilterOrder>()
            .Where(o => o.Shipped)
            .OrderBy(o => o.Id)
            .Select(o => new { o.Id, Name = o.CustomerName.ToUpper() })
            .ToListAsync();
        Assert.Equal("2 GRACE, 5 KEN [ADMIN]", string.Join(", ", shipped.Select(s => $"{s.Id} {s.Name}")));

        var statuses = await db.From<FilterOrder>().Select(o => o.Status).Distinct().ToListAsync();
        Assert.Equal(
            [FilterStatus.Pending, FilterStatus.Shipped, FilterStatus.Cancelled],
            statuses.Order().ToArray()
        );

        var summaries = await db.From<FilterOrder>().Where(o => o.Total > 20).Select(o => new FilterSummary(o.Id, o.Total)).ToListAsync();
        Assert.Equal("2 25.5, 4 40", string.Join(", ", summaries.OrderBy(s => s.Id).Select(s => $"{s.Id} {s.Total:0.##}")));

        Assert.Equal(
            "Barbara",
            await db.From<FilterOrder>().OrderByDescending(o => o.Total).Select(o => o.CustomerName).FirstAsync()
        );
        Assert.Null(await db.From<FilterOrder>().Where(o => o.Id > 100).Select(o => o.CustomerName).SingleAsync());

        await db.DropTableIfExistsAsync(null, "dml_filter_orders");
    }

    [Fact]
    protected virtual async Task Counts_and_aggregates_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        await SeedFilterOrdersAsync(db);

        var orders = db.From<FilterOrder>();
        Assert.Equal(6, await orders.CountAsync());
        Assert.Equal(2, await orders.Where(o => o.Shipped).CountAsync());
        Assert.Equal(3, await orders.Take(3).CountAsync());
        Assert.Equal(2, await orders.OrderBy(o => o.Id).Skip(4).CountAsync());
        Assert.Equal(6, await orders.Distinct().CountAsync());

        Assert.True(await orders.Where(o => o.Total > 30).ExistsAsync());
        Assert.False(await orders.OrderBy(o => o.Id).Skip(6).ExistsAsync());
        Assert.True(await orders.OrderBy(o => o.Id).Skip(5).ExistsAsync());

        Assert.Equal(81.5m, await orders.SumAsync(o => o.Total));
        Assert.Equal(0m, await orders.Where(o => o.Total > 1000).SumAsync(o => o.Total));
        Assert.Equal(21, await orders.SumAsync(o => o.Id));
        Assert.Equal(35.5m, await orders.OrderBy(o => o.Id).Take(2).SumAsync(o => o.Total));
        Assert.Equal(163m, await orders.SumAsync(o => o.Total * 2));

        Assert.Equal(1m, await orders.MinAsync(o => o.Total));
        Assert.Equal(40m, await orders.MaxAsync(o => o.Total));
        Assert.Equal("Space", await orders.MaxAsync(o => o.CustomerName));
        Assert.Equal(new DateTime(2026, 10, 20, 9, 30, 0), await orders.MaxAsync(o => o.PlacedAt));
        Assert.Null(await orders.Where(o => o.Total > 1000).MinAsync(o => o.Total));
        await Assert.ThrowsAsync<InvalidOperationException>(() => orders.Where(o => o.Id > 100).MinAsync(o => o.Id));

        Assert.Equal(3.5, await orders.AverageAsync(o => o.Id));
        Assert.Equal(16.3m, await orders.AverageAsync(o => o.Total));
        Assert.Null(await orders.Where(o => o.Id > 100).AverageAsync(o => o.Id));

        await db.DropTableIfExistsAsync(null, "dml_filter_orders");
    }

    public record FilterSummary(int Id, decimal? Total);
}
