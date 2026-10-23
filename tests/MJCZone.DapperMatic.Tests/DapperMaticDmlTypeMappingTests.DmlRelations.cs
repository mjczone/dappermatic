// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Linq.Expressions;
using MJCZone.DapperMatic.Dml;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Tests;

/// <summary>
/// Conditions through relations: a related row's columns, tests of related rows, values over them, many-to-many
/// through a link table, attribute rows with no foreign key, and EXISTS over models with no relation property.
/// </summary>
public abstract partial class DapperMaticDmlTypeMappingTests
{
    private static readonly Type[] RelationTables =
    [
        typeof(RelCustomer),
        typeof(RelProduct),
        typeof(RelTag),
        typeof(RelOrder),
        typeof(RelLine),
        typeof(RelProductTag),
        typeof(RelDocument),
        typeof(RelAttribute),
        typeof(RelNote),
    ];

    [Fact]
    protected virtual async Task Rows_are_filtered_through_their_relations_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        var data = await SeedRelationsAsync(db);
        var (o1, o2, o3, o4, o5, o6) = (data.Orders[0], data.Orders[1], data.Orders[2], data.Orders[3], data.Orders[4], data.Orders[5]);

        async Task<int[]> Orders(Expression<Func<RelOrder, bool>> where) =>
            [.. (await db.From<RelOrder>().Where(where).ToListAsync()).Select(o => o.Id).Order()];

        // "The orders of customers in France and Germany, or with a product made in England or Spain."
        string[] countries = ["France", "Germany"];
        string[] origins = ["England", "Spain"];
        Assert.Equal(
            [o1, o2, o3, o4, o6],
            await Orders(o => countries.Contains(o.Customer!.Country) || o.Lines!.Any(l => origins.Contains(l.Product!.Origin)))
        );

        // A missing related row reads as NULLs, as o.Customer?.Country would: order 4 has no customer, and order 5's
        // customer has no country.
        Assert.Equal([o2, o3, o4, o5], await Orders(o => o.Customer!.Country != "France"));
        Assert.Equal([o2, o3, o4, o5], await Orders(o => !(o.Customer!.Country == "France")));
        Assert.Equal([o4, o5], await Orders(o => o.Customer!.Country == null));
        Assert.Equal([o4], await Orders(o => o.Customer == null));
        Assert.Equal([o1, o2, o3, o5, o6], await Orders(o => o.Customer != null));
        Assert.Equal([o1, o2, o5, o6], await Orders(o => o.Customer!.IsActive));
        Assert.Equal([o1, o6], await Orders(o => o.Customer!.Name.StartsWith("Al") && o.Customer.IsActive));

        // Collections: Any, All (true with no rows), counts and sums, filtered.
        Assert.Equal([o1, o2, o3, o4, o6], await Orders(o => o.Lines!.Any()));
        Assert.Equal([o5], await Orders(o => !o.Lines!.Any()));
        Assert.Equal([o1, o3, o4, o5], await Orders(o => o.Lines!.All(l => l.Qty > 1)));
        Assert.Equal([o2, o6], await Orders(o => !o.Lines!.All(l => l.Qty > 1)));
        Assert.Equal([o2], await Orders(o => o.Lines!.Count() >= 2));
        Assert.Equal([o5], await Orders(o => o.Lines!.Count == 0));
        Assert.Equal([o2, o3, o4], await Orders(o => o.Lines!.Count(l => l.Qty > 2) == 1));
        Assert.Equal([o2, o3, o4], await Orders(o => o.Lines!.Where(l => l.Qty > 2).Any()));
        Assert.Equal([o3, o4], await Orders(o => o.Lines!.Sum(l => l.Qty * l.Product!.Price) > 20));
        Assert.Equal([o5], await Orders(o => o.Lines!.Sum(l => l.Qty) == 0));
        Assert.Equal([o2, o4], await Orders(o => o.Lines!.Max(l => l.Qty) > 4));
        Assert.Equal([o2, o3, o4], await Orders(o => o.Lines!.Average(l => l.Qty) > 2.5));

        // The query's row inside a subquery's condition, and a related row of it.
        Assert.Equal([o1, o3], await Orders(o => o.Lines!.Any(l => l.Product!.Origin == o.Customer!.Country)));
        Assert.Equal([o4], await Orders(o => o.Lines!.Any(l => l.Product!.Origin == "England" && o.ShipCountry == "Spain")));

        // Many-to-many through the link table, nested in another collection.
        Assert.Equal([o2, o3, o4], await Orders(o => o.Lines!.Any(l => l.Product!.Tags!.Any(t => t.Name == "sale"))));
        var organic = await db.From<RelProduct>().Where(p => p.Tags!.Any(t => t.Name == "organic")).ToListAsync();
        Assert.Equal(["Tea"], organic.Select(p => p.Name));
        var untagged = await db.From<RelProduct>().Where(p => !p.Tags!.Any()).OrderBy(p => p.Name).ToListAsync();
        Assert.Equal(["Cheese", "Rice"], untagged.Select(p => p.Name));

        // Collections of collections, and paths of paths.
        var english = await db.From<RelCustomer>().Where(c => c.Orders!.Any(o => o.Lines!.Any(l => l.Product!.Origin == "England"))).ToListAsync();
        Assert.Equal(["Bob"], english.Select(c => c.Name));
        Assert.Equal(["Alice"], (await db.From<RelCustomer>().Where(c => c.Orders!.Count() == 2).ToListAsync()).Select(c => c.Name));
        var french = await db.From<RelLine>().Where(l => l.Order!.Customer!.Country == "France").ToListAsync();
        Assert.Equal([o1, o6], french.Select(l => l.OrderId).Order());
        Assert.Equal([o4], (await db.From<RelLine>().Where(l => l.Order!.Customer == null).ToListAsync()).Select(l => l.OrderId));

        // Ordering by a related row's column, paging, counting and aggregates over joined rows.
        var byName = await db.From<RelOrder>().Where(o => o.CustomerId != null).OrderBy(o => o.Customer!.Name).ThenBy(o => o.Id).ToListAsync();
        Assert.Equal([o1, o6, o2, o3, o5], byName.Select(o => o.Id));
        var page = await db.From<RelOrder>().Where(o => o.Customer!.IsActive).OrderBy(o => o.Id).Skip(1).Take(2).ToListAsync();
        Assert.Equal([o2, o5], page.Select(o => o.Id));
        Assert.Equal(2, await db.From<RelOrder>().Where(o => o.Customer!.Country == "France").CountAsync());
        Assert.Equal(1, await db.From<RelOrder>().Where(o => o.Customer!.Country == "France").Take(1).CountAsync());
        Assert.True(await db.From<RelOrder>().Where(o => o.Lines!.Any(l => l.Qty == 10)).ExistsAsync());
        Assert.False(await db.From<RelOrder>().Where(o => o.Customer!.Country == "Italy").ExistsAsync());
        Assert.Equal(15, await db.From<RelLine>().Where(l => l.Order!.Status == "open").SumAsync(l => l.Qty));
        Assert.Equal(12.75m, await db.From<RelLine>().OrderBy(l => l.Id).Take(2).SumAsync(l => l.Product!.Price));
        Assert.Equal(12m, await db.From<RelLine>().MaxAsync(l => l.Product!.Price));
        var distinct = await db.From<RelOrder>().Where(o => o.Customer!.IsActive).Select(o => new { o.Status }).Distinct().ToListAsync();
        Assert.Equal(["cancelled", "open", "shipped"], distinct.Select(o => o.Status).Order());

        // The scope's own methods.
        Assert.Equal([o3], (await db.SelectAsync<RelOrder>(o => o.Customer!.Country == "Spain")).Select(o => o.Id));
        Assert.Equal(5, await db.CountAsync<RelOrder>(o => o.Lines!.Any()));
        Assert.False(await db.ExistsAsync<RelOrder>(o => o.Customer!.Country == "Italy"));
        Assert.Equal(o1, (await db.FirstAsync<RelOrder>(o => o.Customer!.Name == "Alice"))?.Id);
        Assert.Equal(o2, (await db.SingleAsync<RelOrder>(o => o.Lines!.Count() == 2))?.Id);

        await DropRelationTablesAsync(db);
    }

    [Fact]
    protected virtual async Task Rows_are_filtered_through_relations_without_foreign_keys_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        var data = await SeedRelationsAsync(db);
        var (o1, o2, o3, o4, o5, o6) = (data.Orders[0], data.Orders[1], data.Orders[2], data.Orders[3], data.Orders[4], data.Orders[5]);

        // Attribute rows of documents: joined by type and UUID, deleted rows left out by the relation's condition.
        async Task<string[]> Documents(Expression<Func<RelDocument, bool>> where) =>
            [.. (await db.From<RelDocument>().Where(where).ToListAsync()).Select(d => d.Title).Order()];

        Assert.Equal(["d1"], await Documents(d => d.Attributes!.Any(a => a.Name == "size")));
        Assert.Equal(["d2"], await Documents(d => d.Attributes!.Count() == 1));
        Assert.Equal(["d3"], await Documents(d => !d.Attributes!.Any()));
        Assert.Equal(["d1"], await Documents(d => d.Color!.Value == "red"));
        Assert.Equal(["d3"], await Documents(d => d.Color == null));
        Assert.Equal(["d1", "d2"], await Documents(d => d.Color != null));

        // EXISTS over models with no relation property, with one other model or two.
        async Task<int[]> Ids(DmQuery<RelOrder> query) => [.. (await query.ToListAsync()).Select(o => o.Id).Order()];
        Assert.Equal([o2], await Ids(db.From<RelOrder>().WhereExists<RelNote>((o, n) => n.OrderId == o.Id && n.Flagged)));
        Assert.Equal([o1, o4, o5, o6], await Ids(db.From<RelOrder>().WhereNotExists<RelNote>((o, n) => n.OrderId == o.Id)));
        Assert.Equal(
            [o3],
            await Ids(db.From<RelOrder>().WhereExists<RelLine, RelProduct>((o, l, p) => l.OrderId == o.Id && p.Id == l.ProductId && p.Origin == "Spain"))
        );
        Assert.Equal(
            [o1, o2, o4, o5, o6],
            await Ids(db.From<RelOrder>().WhereNotExists<RelLine, RelProduct>((o, l, p) => l.OrderId == o.Id && p.Id == l.ProductId && p.Origin == "Spain"))
        );
        Assert.Equal(
            [o3],
            await Ids(db.From<RelOrder>().Where(o => o.Status == "open").WhereExists<RelNote>((o, n) => n.OrderId == o.Id && !n.Flagged))
        );

        await DropRelationTablesAsync(db);
    }

    [Fact]
    protected virtual async Task Rows_are_updated_and_deleted_through_their_relations_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        var data = await SeedRelationsAsync(db);
        var (o1, o2, o3, o4, o5, o6) = (data.Orders[0], data.Orders[1], data.Orders[2], data.Orders[3], data.Orders[4], data.Orders[5]);

        // An UPDATE joins nothing: related rows are read by subqueries.
        var flagged = await db.UpdateAsync<RelOrder>(
            o => new RelOrder { Status = "flagged" },
            o => o.Customer!.Country == "Spain" || o.Lines!.Any(l => l.Qty >= 10)
        );
        Assert.Equal(2, flagged);
        Assert.Equal([o3, o4], (await db.SelectAsync<RelOrder>(o => o.Status == "flagged")).Select(o => o.Id).Order());

        // A value from the related row, NULL where there is none.
        await db.UpdateAsync<RelOrder>(o => new RelOrder { ShipCountry = o.Customer!.Country }, o => o.ShipCountry == null);
        var ship = (await db.SelectAsync<RelOrder>()).ToDictionary(o => o.Id, o => o.ShipCountry);
        Assert.Equal("Spain", ship[o3]);
        Assert.Null(ship[o5]);
        Assert.Equal("France", ship[o6]);
        Assert.Equal("Germany", ship[o2]);

        // A count of related rows in the condition of a patch.
        Assert.Equal(1, await db.PatchAsync<RelOrder>(new { Status = "big" }, o => o.Lines!.Count() == 2));
        Assert.Equal("big", (await db.SingleByIdAsync<RelOrder>(o2))?.Status);

        // A DELETE through a to-one path and through a collection.
        Assert.Equal(1, await db.DeleteAsync<RelLine>(l => l.Order!.Status == "cancelled"));
        Assert.Equal(2, await db.DeleteAsync<RelOrder>(o => !o.Lines!.Any()));
        Assert.Equal([o1, o2, o3, o4], (await db.SelectAsync<RelOrder>()).Select(o => o.Id).Order());

        await DropRelationTablesAsync(db);
    }

    private static async Task DropRelationTablesAsync(IDbConnection db)
    {
        foreach (var type in RelationTables.Reverse())
        {
            var table = DmTableFactory.GetTable(type);
            await db.DropTableIfExistsAsync(table.SchemaName, table.TableName);
        }
    }

    // Customers, products, tags, orders and their lines, product tags, documents with attribute rows, and notes.
    private static async Task<(List<int> Orders, List<Guid> Documents)> SeedRelationsAsync(IDbConnection db)
    {
        await DropRelationTablesAsync(db);
        foreach (var type in RelationTables)
        {
            await db.CreateTableIfNotExistsAsync(DmTableFactory.GetTable(type));
        }

        async Task<int> Customer(string name, string? country, bool active)
        {
            var row = new RelCustomer { Name = name, Country = country, IsActive = active };
            await db.InsertAsync(row);
            return row.Id;
        }

        async Task<int> Product(string name, string? origin, decimal price)
        {
            var row = new RelProduct { Name = name, Origin = origin, Price = price };
            await db.InsertAsync(row);
            return row.Id;
        }

        async Task<int> Order(int? customer, string status, string? ship)
        {
            var row = new RelOrder { CustomerId = customer, Status = status, ShipCountry = ship };
            await db.InsertAsync(row);
            return row.Id;
        }

        var alice = await Customer("Alice", "France", active: true);
        var bob = await Customer("Bob", "Germany", active: true);
        var carol = await Customer("Carol", "Spain", active: false);
        var dan = await Customer("Dan", null, active: true);

        var tea = await Product("Tea", "England", 4.50m);
        var wine = await Product("Wine", "Spain", 12m);
        var cheese = await Product("Cheese", "France", 8.25m);
        var rice = await Product("Rice", null, 2m);

        var sale = new RelTag { Name = "sale" };
        var organicTag = new RelTag { Name = "organic" };
        await db.InsertAsync(sale);
        await db.InsertAsync(organicTag);
        await db.InsertManyAsync<RelProductTag>(
            [
                new() { ProductId = tea, TagId = sale.Id },
                new() { ProductId = tea, TagId = organicTag.Id },
                new() { ProductId = wine, TagId = sale.Id },
            ]
        );

        List<int> orders =
        [
            await Order(alice, "open", "France"),
            await Order(bob, "shipped", "Germany"),
            await Order(carol, "open", null),
            await Order(null, "open", "Spain"),
            await Order(dan, "open", null),
            await Order(alice, "cancelled", null),
        ];

        // Lines in this order: their ids follow it.
        await db.InsertManyAsync<RelLine>(
            [
                new() { OrderId = orders[0], ProductId = cheese, Qty = 2 },
                new() { OrderId = orders[1], ProductId = tea, Qty = 1 },
                new() { OrderId = orders[1], ProductId = rice, Qty = 5 },
                new() { OrderId = orders[2], ProductId = wine, Qty = 3 },
                new() { OrderId = orders[3], ProductId = tea, Qty = 10 },
                new() { OrderId = orders[5], ProductId = rice, Qty = 1 },
            ]
        );

        await db.InsertManyAsync<RelNote>(
            [
                new() { OrderId = orders[1], Flagged = true },
                new() { OrderId = orders[2], Flagged = false },
            ]
        );

        List<Guid> documents = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        for (var i = 0; i < documents.Count; i++)
        {
            await db.InsertAsync(new RelDocument { Id = documents[i], Title = $"d{i + 1}" });
        }

        RelAttribute Attribute(string type, Guid id, string name, string value, bool deleted = false) =>
            new() { ObjectType = type, ObjectId = id, Name = name, Value = value, IsDeleted = deleted };

        // d3's id also names an image, whose attributes are not the document's.
        await db.InsertManyAsync(
            [
                Attribute("document", documents[0], "color", "red"),
                Attribute("document", documents[0], "size", "10"),
                Attribute("document", documents[1], "color", "blue"),
                Attribute("document", documents[1], "size", "12", deleted: true),
                Attribute("image", documents[2], "color", "red"),
            ]
        );

        return (orders, documents);
    }
}
