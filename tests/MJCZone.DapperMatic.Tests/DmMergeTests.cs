// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using MJCZone.DapperMatic.DataAnnotations;
using MJCZone.DapperMatic.Dml;

namespace MJCZone.DapperMatic.Tests;

/// <summary>
/// Merging rows already in memory into relation properties, and the SQL an include adds to a query.
/// </summary>
public class DmMergeTests
{
    private static readonly IDbConnection Sqlite = new Microsoft.Data.Sqlite.SqliteConnection();

    [Fact]
    public void Rows_merge_by_their_relations()
    {
        var alice = new RelCustomer { Id = 1, Name = "Alice" };
        var bob = new RelCustomer { Id = 2, Name = "Bob" };
        RelOrder[] orders = [new() { Id = 10, CustomerId = 1 }, new() { Id = 11, CustomerId = 2 }, new() { Id = 12, CustomerId = null }];
        RelLine[] lines = [new() { Id = 1, OrderId = 10 }, new() { Id = 2, OrderId = 10 }, new() { Id = 3, OrderId = 11 }];

        DmMerge.Into(orders, o => o.Customer, [alice, bob]);
        DmMerge.Into(orders, o => o.Lines, lines);
        Assert.Equal([alice, bob, null], orders.Select(o => o.Customer));
        Assert.Equal([[1, 2], [3], []], orders.Select(o => o.Lines!.Select(l => l.Id).ToArray()));

        // The other way: each customer's orders.
        RelCustomer[] customers = [alice, bob, new() { Id = 3, Name = "Carol" }];
        DmMerge.Into(customers, c => c.Orders, orders);
        Assert.Equal([[10], [11], []], customers.Select(c => c.Orders!.Select(o => o.Id).ToArray()));

        // Many-to-many through the link rows.
        RelProduct[] products = [new() { Id = 1, Name = "Tea" }, new() { Id = 2, Name = "Wine" }, new() { Id = 3, Name = "Rice" }];
        RelTag[] tags = [new() { Id = 1, Name = "sale" }, new() { Id = 2, Name = "organic" }];
        RelProductTag[] links = [new() { ProductId = 1, TagId = 1 }, new() { ProductId = 1, TagId = 2 }, new() { ProductId = 2, TagId = 1 }];
        DmMerge.Into(products, p => p.Tags, tags, links);
        Assert.Equal([["sale", "organic"], ["sale"], []], products.Select(p => p.Tags!.Select(t => t.Name).ToArray()));

        // Attribute rows: by type and UUID, deleted rows left out by the relation's condition.
        var d1 = new RelDocument { Id = Guid.NewGuid(), Title = "d1" };
        var d2 = new RelDocument { Id = Guid.NewGuid(), Title = "d2" };
        RelAttribute[] attributes =
        [
            new() { ObjectType = "document", ObjectId = d1.Id, Name = "color", Value = "red" },
            new() { ObjectType = "document", ObjectId = d1.Id, Name = "size", Value = "1", IsDeleted = true },
            new() { ObjectType = "image", ObjectId = d2.Id, Name = "color", Value = "blue" },
        ];
        DmMerge.Into([d1, d2], d => d.Attributes, attributes);
        DmMerge.Into([d1, d2], d => d.Color, attributes);
        Assert.Equal(["color"], d1.Attributes!.Select(a => a.Name));
        Assert.Empty(d2.Attributes!);
        Assert.Equal("red", d1.Color?.Value);
        Assert.Null(d2.Color);
    }

    [Fact]
    public void Rows_merge_by_explicit_keys()
    {
        RelOrder[] orders = [new() { Id = 10, CustomerId = 1 }, new() { Id = 11, CustomerId = 9 }];
        RelLine[] lines = [new() { Id = 1, OrderId = 10 }, new() { Id = 2, OrderId = 10 }];
        DmMerge.Into(orders, o => o.Lines, lines, o => o.Id, l => l.OrderId);
        Assert.Equal([2, 0], orders.Select(o => o.Lines!.Count));

        // A nullable key and its value match; a key of several values is a tuple.
        DmMerge.Into(orders, o => o.Customer, [new RelCustomer { Id = 1, Name = "Alice" }], o => o.CustomerId, c => c.Id);
        Assert.Equal(["Alice", null], orders.Select(o => o.Customer?.Name));

        Pair[] pairs = [new() { A = 1, B = "x" }, new() { A = 1, B = "y" }];
        Part[] parts = [new() { A = 1, B = "x", Name = "p1" }, new() { A = 1, B = "x", Name = "p2" }, new() { A = 2, B = "y", Name = "p3" }];
        DmMerge.Into(pairs, p => p.Parts, parts, p => (p.A, p.B), p => (p.A, p.B));
        Assert.Equal([["p1", "p2"], []], pairs.Select(p => p.Parts!.Select(x => x.Name).ToArray()));
    }

    [Fact]
    public void Merges_that_cannot_be_made_are_refused()
    {
        RelOrder[] orders = [new() { Id = 10, CustomerId = 1 }];
        Assert.Throws<InvalidOperationException>(() =>
            DmMerge.Into(orders, o => o.Customer, [new RelCustomer { Id = 1 }, new RelCustomer { Id = 1 }])
        );
        Assert.Throws<ArgumentException>(() => DmMerge.Into([new Pair()], p => p.Parts, Array.Empty<Part>()));
        Assert.Throws<ArgumentException>(() => DmMerge.Into([new RelProduct()], p => p.Tags, Array.Empty<RelTag>()));
        Assert.Throws<ArgumentException>(() => DmMerge.Into(orders, o => o.Lines, Array.Empty<RelLine>(), Array.Empty<RelProductTag>()));
    }

    [Fact]
    public void Includes_of_one_row_are_joined_into_the_query()
    {
        var sql = Sqlite.From<RelLine>().Include(l => l.Order, o => o.Include(x => x.Customer)).ToSql().Sql;
        Assert.Equal(
            "SELECT t0.\"id\" AS \"Id\", t0.\"order_id\" AS \"OrderId\", t0.\"product_id\" AS \"ProductId\", t0.\"qty\" AS \"Qty\", "
                + "t1.\"id\" AS \"Id\", t1.\"customer_id\" AS \"CustomerId\", t1.\"status\" AS \"Status\", t1.\"ship_country\" AS \"ShipCountry\", "
                + "t2.\"id\" AS \"Id\", t2.\"name\" AS \"Name\", t2.\"country\" AS \"Country\", t2.\"is_active\" AS \"IsActive\" "
                + "FROM \"rel_lines\" t0 LEFT JOIN \"rel_orders\" t1 ON t1.\"id\" = t0.\"order_id\" "
                + "LEFT JOIN \"rel_customers\" t2 ON t2.\"id\" = t1.\"customer_id\"",
            sql
        );

        // Collections are read after the rows, so the query is the rows' alone.
        Assert.Equal(Sqlite.From<RelOrder>().ToSql().Sql, Sqlite.From<RelOrder>().Include(o => o.Lines).ToSql().Sql);

        Assert.Throws<InvalidOperationException>(() => Sqlite.From<RelOrder>().Include(o => o.Lines).Include(o => o.Lines, l => l.Take(1)));
        Assert.Throws<ArgumentException>(() => Sqlite.From<RelOrder>().Include(o => o.Customer, c => c.Where(x => x.IsActive)));
        Assert.Throws<ArgumentException>(() => Sqlite.From<Pair>().Include(p => p.Parts));
    }

    [DmTable(tableName: "merge_pairs")]
    public class Pair
    {
        public int A { get; set; }

        public string B { get; set; } = string.Empty;

        [DmIgnore]
        public List<Part>? Parts { get; set; }
    }

    public class Part
    {
        public int A { get; set; }

        public string B { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }
}
