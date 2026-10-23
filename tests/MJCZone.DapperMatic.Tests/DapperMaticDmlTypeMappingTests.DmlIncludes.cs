// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.Dml;

namespace MJCZone.DapperMatic.Tests;

/// <summary>
/// Loading relations: Include on a query, LoadAsync onto rows already read.
/// </summary>
public abstract partial class DapperMaticDmlTypeMappingTests
{
    [Fact]
    protected virtual async Task Rows_are_read_with_their_included_relations_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        var data = await SeedRelationsAsync(db);
        var (o1, o2, o3, o4, o5, o6) = (data.Orders[0], data.Orders[1], data.Orders[2], data.Orders[3], data.Orders[4], data.Orders[5]);

        // One row joined, collections by batch, to any depth: lines, each line's product, each product's tags.
        var orders = await db.From<RelOrder>()
            .Include(o => o.Customer)
            .Include(o => o.Lines, l => l.OrderBy(x => x.Id).Include(x => x.Product, p => p.Include(y => y.Tags)))
            .OrderBy(o => o.Id)
            .ToListAsync();
        Assert.Equal([o1, o2, o3, o4, o5, o6], orders.Select(o => o.Id));
        Assert.Equal("Alice", orders[0].Customer?.Name);
        Assert.Null(orders[3].Customer);
        Assert.Equal(["Cheese"], orders[0].Lines!.Select(l => l.Product!.Name));
        Assert.Equal(["Tea", "Rice"], orders[1].Lines!.Select(l => l.Product!.Name));
        Assert.Equal(["organic", "sale"], orders[1].Lines![0].Product!.Tags!.Select(t => t.Name).Order());
        Assert.Empty(orders[1].Lines![1].Product!.Tags!);
        Assert.NotNull(orders[4].Lines);
        Assert.Empty(orders[4].Lines!);
        Assert.All(orders, o => Assert.Null(o.Customer?.Orders));

        // Which rows of a collection, per owner: each customer's latest order that is not cancelled, then the latest.
        var customers = await db.From<RelCustomer>()
            .OrderBy(c => c.Id)
            .Include(c => c.Orders, o => o.Where(x => x.Status != "cancelled").OrderByDescending(x => x.Id).Take(1))
            .ToListAsync();
        Assert.Equal([[o1], [o2], [o3], [o5]], customers.Select(c => c.Orders!.Select(o => o.Id).ToArray()));
        var latest = await db.From<RelCustomer>().OrderBy(c => c.Id).Include(c => c.Orders, o => o.OrderByDescending(x => x.Id).Take(1)).ToListAsync();
        Assert.Equal(o6, latest[0].Orders!.Single().Id);
        var two = await db.From<RelOrder>().Where(o => o.Id == o2).Include(o => o.Lines, l => l.OrderByDescending(x => x.Qty).Take(5)).SingleAsync();
        Assert.Equal([5, 1], two!.Lines!.Select(l => l.Qty));

        // A relation of a relation joined; a collection of a joined row loaded by batch.
        var lines = await db.From<RelLine>()
            .Include(l => l.Order, o => o.Include(x => x.Customer).Include(x => x.Lines))
            .OrderBy(l => l.Id)
            .ToListAsync();
        Assert.Equal("Alice", lines[0].Order!.Customer!.Name);
        Assert.Null(lines[4].Order!.Customer);
        Assert.Equal(2, lines[1].Order!.Lines!.Count);
        Assert.Equal(2, lines[2].Order!.Lines!.Count);

        // Many-to-many, and attribute rows by type and UUID.
        var products = await db.From<RelProduct>().Include(p => p.Tags, t => t.OrderBy(x => x.Name)).OrderBy(p => p.Id).ToListAsync();
        Assert.Equal([["organic", "sale"], ["sale"], [], []], products.Select(p => p.Tags!.Select(t => t.Name).ToArray()));
        var documents = await db.From<RelDocument>()
            .Include(d => d.Attributes, a => a.OrderBy(x => x.Name))
            .Include(d => d.Color)
            .OrderBy(d => d.Title)
            .ToListAsync();
        Assert.Equal([["color", "size"], ["color"], []], documents.Select(d => d.Attributes!.Select(a => a.Name).ToArray()));
        Assert.Equal(["red", "blue", null], documents.Select(d => d.Color?.Value));

        // A condition and an include of the same relation share its join; First, Single and projections load too.
        var french = db.From<RelOrder>().Where(o => o.Customer!.Country == "France").Include(o => o.Customer).OrderBy(o => o.Id);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(french.ToSql().Sql, "LEFT JOIN"));
        Assert.Equal(["Alice", "Alice"], (await french.ToListAsync()).Select(o => o.Customer!.Name));
        Assert.Equal(o1, (await french.FirstAsync())?.Id);
        Assert.Equal("Alice", (await french.Take(1).SingleAsync())?.Customer?.Name);
        Assert.Equal(["Alice", "Alice"], await french.Select(o => o.Customer!.Name).ToListAsync());
        Assert.Throws<InvalidOperationException>(() => french.Select(o => o.Status).Distinct().ToSql());
        Assert.Equal(2, await french.CountAsync());

        await DropRelationTablesAsync(db);
    }

    [Fact]
    protected virtual async Task Relations_are_loaded_onto_rows_already_read_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        var data = await SeedRelationsAsync(db);

        var orders = (await db.SelectAsync<RelOrder>()).OrderBy(o => o.Id).ToList();
        await db.LoadAsync(orders, q => q.Include(o => o.Customer).Include(o => o.Lines, l => l.Include(x => x.Product)));
        Assert.Equal(["Alice", "Bob", "Carol", null, "Dan", "Alice"], orders.Select(o => o.Customer?.Name));
        Assert.Equal(["Tea", "Rice"], orders[1].Lines!.Select(l => l.Product!.Name).Order().Reverse());
        Assert.Empty(orders[4].Lines!);

        // A relation already loaded is kept, unless reloaded.
        orders[0].Customer = new RelCustomer { Name = "kept" };
        await db.LoadAsync(orders, q => q.Include(o => o.Customer));
        Assert.Equal("kept", orders[0].Customer!.Name);
        await db.LoadAsync(orders, q => q.Include(o => o.Customer), reload: true);
        Assert.Equal("Alice", orders[0].Customer!.Name);

        // A relation of one row, with a collection of its own.
        var lines = await db.SelectAsync<RelLine>();
        await db.LoadAsync(lines, q => q.Include(l => l.Order, o => o.Include(x => x.Lines)));
        Assert.All(lines, l => Assert.Contains(l.Order!.Lines!, x => x.Id == l.Id));

        // Filters choose among a relation's rows, inside its Include.
        await Assert.ThrowsAsync<ArgumentException>(() => db.LoadAsync(orders, q => q.Where(o => o.Id > 0).Include(o => o.Lines)));

        // Many owners: their keys go in batches, and Take is per owner across them.
        await DropRelationTablesAsync(db);
        await SeedRelationsAsync(db);
        var count = 2_100;
        await db.BulkInsertAsync(Enumerable.Range(0, count).Select(i => new RelCustomer { Name = $"c{i:D4}", Country = "Many" }));
        var many = await db.From<RelCustomer>().Where(c => c.Country == "Many").ToListAsync();
        await db.BulkInsertAsync(many.SelectMany(c => new[] { new RelOrder { CustomerId = c.Id, Status = "a" }, new RelOrder { CustomerId = c.Id, Status = "b" } }));
        var loaded = await db.From<RelCustomer>()
            .Where(c => c.Country == "Many")
            .Include(c => c.Orders, o => o.OrderByDescending(x => x.Status).Take(1))
            .ToListAsync();
        Assert.Equal(count, loaded.Count);
        Assert.All(loaded, c => Assert.Equal("b", Assert.Single(c.Orders!).Status));
        await db.LoadAsync(many, q => q.Include(c => c.Orders));
        Assert.All(many, c => Assert.Equal(2, c.Orders!.Count));

        await DropRelationTablesAsync(db);
    }
}
