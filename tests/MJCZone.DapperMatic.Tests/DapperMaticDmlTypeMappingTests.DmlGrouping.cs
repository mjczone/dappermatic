// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.Dml;

namespace MJCZone.DapperMatic.Tests;

/// <summary>
/// Projections of related rows' values, computed by the database, and grouping.
/// </summary>
public abstract partial class DapperMaticDmlTypeMappingTests
{
    [Fact]
    protected virtual async Task Rows_are_projected_with_values_of_their_relations_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        var data = await SeedRelationsAsync(db);
        var (o1, o2, o3, o4, o5, o6) = (data.Orders[0], data.Orders[1], data.Orders[2], data.Orders[3], data.Orders[4], data.Orders[5]);

        // Related rows' columns, counts, sums and tests, in the select list; no row is loaded.
        var rows = await db.From<RelOrder>()
            .OrderBy(o => o.Id)
            .Select(o => new
            {
                o.Id,
                Customer = o.Customer!.Name,
                Lines = o.Lines!.Count(),
                Qty = o.Lines!.Sum(l => l.Qty),
                HasCustomer = o.Customer != null,
                Big = o.Lines!.Any(l => l.Qty >= 5),
            })
            .ToListAsync();
        Assert.Equal([o1, o2, o3, o4, o5, o6], rows.Select(r => r.Id));
        Assert.Equal(["Alice", "Bob", "Carol", null, "Dan", "Alice"], rows.Select(r => r.Customer));
        Assert.Equal([1, 2, 1, 1, 0, 1], rows.Select(r => r.Lines));
        Assert.Equal([2, 6, 3, 10, 0, 1], rows.Select(r => r.Qty));
        Assert.Equal([true, true, true, false, true, true], rows.Select(r => r.HasCustomer));
        Assert.Equal([false, true, false, true, false, false], rows.Select(r => r.Big));

        // The rest of the selector runs in .NET on the values; a missing row's value is NULL.
        var names = await db.From<RelOrder>().OrderBy(o => o.Id).Select(o => (o.Customer!.Name ?? "none").ToUpper()).ToListAsync();
        Assert.Equal(["ALICE", "BOB", "CAROL", "NONE", "DAN", "ALICE"], names);
        var ids = await db.From<RelOrder>().OrderBy(o => o.Id).Select(o => (int?)o.Customer!.Id).ToListAsync();
        Assert.Null(ids[3]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.From<RelOrder>().Select(o => o.Customer!.Id).ToListAsync());

        // Distinct values of a related row's column, compared by the database.
        var countries = await db.From<RelOrder>().Select(o => o.Customer!.Country).Distinct().ToListAsync();
        Assert.Equal([null, "France", "Germany", "Spain"], countries.Order());

        // With includes, which load the rows the selector reads in .NET.
        var included = await db.From<RelOrder>()
            .Where(o => o.Id == o2)
            .Include(o => o.Lines)
            .Select(o => new { Loaded = o.Lines!.Select(l => l.Qty).Sum(), Counted = o.Lines!.Count(), Customer = o.Customer!.Name })
            .SingleAsync();
        Assert.Equal((6, 2, "Bob"), (included!.Loaded, included.Counted, included.Customer));

        await DropRelationTablesAsync(db);
    }

    [Fact]
    protected virtual async Task Rows_are_grouped_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        var data = await SeedRelationsAsync(db);

        // By a related row's column, NULLs first in key order, with counts of all and of some.
        var byCountry = db.From<RelOrder>().GroupBy(o => o.Customer!.Country).OrderBy(g => g.Key);
        var countries = await byCountry.Select(g => new { Country = g.Key, Orders = g.Count(), Open = g.Count(o => o.Status == "open") }).ToListAsync();
        Assert.Equal([null, "France", "Germany", "Spain"], countries.Select(c => c.Country));
        Assert.Equal([2, 2, 1, 1], countries.Select(c => c.Orders));
        Assert.Equal([2, 1, 0, 1], countries.Select(c => c.Open));
        var busy = await byCountry.Having(g => g.Count() > 1).Select(g => g.Key).ToListAsync();
        Assert.Equal([null, "France"], busy);
        Assert.Equal(4, await byCountry.CountAsync());

        // By a key of two values, each row's element of two more, with sums, extremes and averages.
        var byStatusAndOrigin = db.From<RelLine>()
            .GroupBy(l => new { l.Order!.Status, l.Product!.Origin }, l => new { l.Qty, l.Product!.Price })
            .OrderByDescending(g => g.Sum(x => x.Qty * x.Price));
        var totals = await byStatusAndOrigin
            .Select(g => new { g.Key.Status, g.Key.Origin, Total = g.Sum(x => x.Qty * x.Price), Lines = g.Count(), Most = g.Max(x => x.Qty) })
            .ToListAsync();
        Assert.Equal([45m, 36m, 16.5m, 10m, 4.5m, 2m], totals.Select(t => t.Total));
        Assert.Equal(("open", "England"), (totals[0].Status, totals[0].Origin));
        Assert.Equal(("cancelled", null), (totals[5].Status, totals[5].Origin));
        Assert.All(totals, t => Assert.Equal(1, t.Lines));
        Assert.Equal([36m, 16.5m], (await byStatusAndOrigin.Skip(1).Take(2).Select(g => g.Sum(x => x.Qty * x.Price)).ToListAsync()));
        Assert.Equal(3, await byStatusAndOrigin.Having(g => g.Sum(x => x.Qty * x.Price) > 10).CountAsync());
        var key = await byStatusAndOrigin.Select(g => g.Key).FirstAsync();
        Assert.Equal(("open", "England"), (key!.Status, key.Origin));

        // Conditions first, then groups of the rows that meet them.
        var orders = await db.From<RelLine>()
            .Where(l => l.Order!.CustomerId != null)
            .GroupBy(l => l.OrderId)
            .OrderBy(g => g.Key)
            .Select(g => new { Order = g.Key, Average = g.Average(l => l.Qty), Least = g.Min(l => l.Qty), Some = g.Any(l => l.Qty > 4), All = g.All(l => l.Qty > 1) })
            .ToListAsync();
        Assert.Equal([data.Orders[0], data.Orders[1], data.Orders[2], data.Orders[5]], orders.Select(o => o.Order));
        Assert.Equal([2d, 3d, 3d, 1d], orders.Select(o => o.Average));
        Assert.Equal([2, 1, 3, 1], orders.Select(o => o.Least));
        Assert.Equal([false, true, false, false], orders.Select(o => o.Some));
        Assert.Equal([true, false, true, false], orders.Select(o => o.All));
        var open = await db.From<RelOrder>().GroupBy(o => o.Status).Select(g => new { g.Key, Ids = g.Where(o => o.CustomerId != null).Count() }).ToListAsync();
        Assert.Equal(3, open.Single(g => g.Key == "open").Ids);

        // A constant key is one group of every row, and no group of no rows; averages keep their fraction.
        var qtys = (await db.SelectAsync<RelLine>()).Select(l => l.Qty).ToList();
        var whole = await db.From<RelLine>().GroupBy(l => 1).Select(g => new { Count = g.Count(), Average = g.Average(l => l.Qty) }).ToListAsync();
        Assert.Equal(qtys.Count, Assert.Single(whole).Count);
        Assert.Equal(qtys.Average(), whole[0].Average, 10);
        Assert.Empty(await db.From<RelLine>().Where(l => l.Qty < 0).GroupBy(l => 1).Select(g => g.Count()).ToListAsync());

        await DropRelationTablesAsync(db);
    }

    [Fact]
    protected virtual async Task Rows_are_joined_explicitly_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        var data = await SeedRelationsAsync(db);
        var (o1, o2, o3, o4, o5, o6) = (data.Orders[0], data.Orders[1], data.Orders[2], data.Orders[3], data.Orders[4], data.Orders[5]);

        // A model with no relation property, joined by a condition; the query's conditions stay.
        var noted = await db.From<RelOrder>().Where(o => o.Status == "open").Join<RelNote>((o, n) => n.OrderId == o.Id).ToListAsync();
        Assert.Equal([(o3, false)], noted.Select(r => (r.Item1.Id, r.Item2!.Flagged)));

        // A left join reads as null where no row matched, and compares as C# would with a null row's column.
        var withCustomers = db.From<RelOrder>().LeftJoin<RelCustomer>((o, c) => c.Id == o.CustomerId);
        var all = await withCustomers.OrderBy((o, c) => o.Id).ToListAsync();
        Assert.Equal(["Alice", "Bob", "Carol", null, "Dan", "Alice"], all.Select(r => r.Item2?.Name));
        var notAlice = await withCustomers.Where((o, c) => c.Name != "Alice").OrderBy((o, c) => o.Id).ToListAsync();
        Assert.Equal([o2, o3, o4, o5], notAlice.Select(r => r.Item1.Id));

        // Projections read the rows' columns, related values and whole rows.
        var projected = await withCustomers
            .OrderBy((o, c) => o.Id)
            .Select((o, c) => new { o.Id, c.Name, Lines = o.Lines!.Count(), Customer = c })
            .ToListAsync();
        Assert.Equal(["Alice", "Bob", "Carol", null, "Dan", "Alice"], projected.Select(r => r.Name));
        Assert.Equal([1, 2, 1, 1, 0, 1], projected.Select(r => r.Lines));
        Assert.Null(projected[3].Customer);
        Assert.Equal("Bob", projected[1].Customer!.Name);

        // Three and four models.
        var english = await db.From<RelLine>()
            .Join<RelOrder>((l, o) => o.Id == l.OrderId)
            .Join<RelProduct>((l, o, p) => p.Id == l.ProductId)
            .Where((l, o, p) => p.Origin == "England")
            .OrderBy((l, o, p) => l.Qty)
            .Select((l, o, p) => new { o.Status, p.Name, l.Qty })
            .ToListAsync();
        Assert.Equal([("shipped", "Tea", 1), ("open", "Tea", 10)], english.Select(r => (r.Status, r.Name, r.Qty)));
        var four = db.From<RelLine>()
            .Join<RelOrder>((l, o) => o.Id == l.OrderId)
            .Join<RelProduct>((l, o, p) => p.Id == l.ProductId)
            .LeftJoin<RelCustomer>((l, o, p, c) => c.Id == o.CustomerId);
        Assert.Equal(6, await four.CountAsync());
        Assert.Equal(1, await four.Where((l, o, p, c) => c == null).CountAsync());
        // What a caller writes for a left-joined row that is missing: always false in C#, a NULL key in SQL.
#pragma warning disable CS0472
        Assert.Equal(1, await four.Where((l, o, p, c) => c.Id == null).CountAsync());
#pragma warning restore CS0472

        // Counts, distinct results, paging and the first row.
        var lines = db.From<RelOrder>().Join<RelLine>((o, l) => l.OrderId == o.Id);
        Assert.Equal(6, await lines.CountAsync());
        Assert.Equal(2, await lines.Take(2).CountAsync());
        Assert.True(await lines.Where((o, l) => l.Qty == 10).ExistsAsync());
        Assert.False(await lines.Where((o, l) => l.Qty == 99).ExistsAsync());
        Assert.Equal([o1, o2, o3, o4, o6], (await lines.Select((o, l) => o.Id).Distinct().ToListAsync()).Order());
        Assert.Equal(6, await lines.OrderBy((o, l) => o.Id).Distinct().CountAsync());
        Assert.Equal(o4, (await lines.OrderByDescending((o, l) => l.Qty).FirstAsync())?.Item1.Id);
        Assert.Equal(10, (await lines.OrderByDescending((o, l) => l.Qty).Select((o, l) => l.Qty).FirstAsync()));

        // Groups of joined rows, each row an element of both.
        var byOrigin = await db.From<RelLine>()
            .Join<RelProduct>((l, p) => p.Id == l.ProductId)
            .GroupBy((l, p) => p.Origin, (l, p) => new { l.Qty, p.Price })
            .OrderBy(g => g.Key)
            .Select(g => new { g.Key, Total = g.Sum(x => x.Qty * x.Price) })
            .ToListAsync();
        Assert.Equal([null, "England", "France", "Spain"], byOrigin.Select(g => g.Key));
        Assert.Equal([12m, 49.5m, 16.5m, 36m], byOrigin.Select(g => g.Total));

        await DropRelationTablesAsync(db);
    }
}
