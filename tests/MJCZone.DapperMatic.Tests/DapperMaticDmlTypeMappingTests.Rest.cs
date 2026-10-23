// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using MJCZone.DapperMatic.Dml;
using MJCZone.DapperMatic.Dml.Internal;
using MJCZone.DapperMatic.Dml.Rest;

namespace MJCZone.DapperMatic.Tests;

/// <summary>
/// REST requests against tables the database describes: queries with relations, expands and counts, and writes.
/// </summary>
public abstract partial class DapperMaticDmlTypeMappingTests
{
    [Fact]
    protected virtual async Task Rest_queries_group_and_aggregate_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        await SeedRelationsAsync(db);
        var engine = await RestEngineAsync(db);
        var orders = await RestTableAsync(db, "rel_orders");
        static long Number(object? value) => Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
        Task<(List<Dictionary<string, object?>> Rows, long? Count, string? NextCursor)> Query(Dictionary<string, string> options) =>
            engine.QueryAsync(RestParser.Parse(options), orders, null, default);

        // Groups with their counts and sums, as the typed GroupBy reads them.
        var expected = (await db.From<RelOrder>().GroupBy(o => o.Status).Select(g => new { g.Key, Count = g.Count(), Sum = g.Sum(o => o.Id) }).ToListAsync())
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToList();
        var (groups, total, _) = await Query(
            new() { ["$apply"] = "groupby((status),aggregate($count as orders,id with sum as ids,customer_id with countdistinct as customers,id with max as last))", ["$count"] = "true" }
        );
        Assert.Equal(expected.Count, total);
        Assert.Equal(expected.Select(g => g.Key), groups.Select(g => (string)g["status"]!));
        Assert.Equal(expected.Select(g => (long)g.Count), groups.Select(g => Number(g["orders"])));
        Assert.Equal(expected.Select(g => (long)g.Sum), groups.Select(g => Number(g["ids"])));
        Assert.Equal(["status", "orders", "ids", "customers", "last"], groups[0].Keys);

        // A filter step on the rows; $filter, $orderby and $top on the groups.
        var open = await db.CountAsync<RelOrder>(o => o.Status == "open");
        var (filtered, _, _) = await Query(new() { ["$apply"] = "filter(status eq 'open')/aggregate($count as n)" });
        Assert.Equal(open, Number(Assert.Single(filtered)["n"]));
        var (big, _, _) = await Query(new() { ["$apply"] = "groupby((status),aggregate($count as n))", ["$filter"] = "n gt 1", ["$orderby"] = "n desc,status", ["$top"] = "1" });
        Assert.Equal(expected.Where(g => g.Count > 1).OrderByDescending(g => g.Count).ThenBy(g => g.Key, StringComparer.Ordinal).Take(1).Select(g => g.Key), big.Select(g => (string)g["status"]!));
        var (average, _, _) = await Query(new() { ["$apply"] = "aggregate(id with average as mean)" });
        Assert.Equal((await db.SelectAsync<RelOrder>()).Average(o => o.Id), Convert.ToDouble(Assert.Single(average)["mean"], System.Globalization.CultureInfo.InvariantCulture), 3);

        // The JSON tree says the same.
        var tree = RestJson.Apply(System.Text.Json.JsonDocument.Parse("""{"filter": {"eq": [{"path": "status"}, {"value": "open"}]}, "aggregate": [{"with": "count", "as": "n"}]}""").RootElement);
        var (fromTree, _, _) = await engine.QueryAsync(new RestQuery { Apply = tree }, orders, null, default);
        Assert.Equal(open, Number(Assert.Single(fromTree)["n"]));

        // Refused: a related row's column grouped, text summed, columns selected, a name the groups do not have.
        await Assert.ThrowsAsync<ArgumentException>(() => Query(new() { ["$apply"] = "groupby((customer/name))" }));
        await Assert.ThrowsAsync<ArgumentException>(() => Query(new() { ["$apply"] = "aggregate(status with sum as s)" }));
        await Assert.ThrowsAsync<ArgumentException>(() => Query(new() { ["$apply"] = "aggregate($count as n)", ["$select"] = "id" }));
        await Assert.ThrowsAsync<ArgumentException>(() => Query(new() { ["$apply"] = "groupby((status))", ["$filter"] = "id gt 1" }));
        await Assert.ThrowsAsync<ArgumentException>(() => Query(new() { ["$apply"] = "groupby((status))/filter(id gt 1)" }));

        await DropRelationTablesAsync(db);
    }

    [Fact]
    protected virtual async Task Rest_queries_page_by_cursor_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        await SeedRelationsAsync(db);
        var engine = await RestEngineAsync(db);
        var orders = await RestTableAsync(db, "rel_orders");
        static int[] Ids(List<Dictionary<string, object?>> rows) => [.. rows.Select(r => Convert.ToInt32(r["id"], System.Globalization.CultureInfo.InvariantCulture))];

        // Pages joined end to end are the whole ordered list: ties broken by the key, NULLs where the order puts them.
        foreach (var order in new[] { "ship_country,status desc", "ship_country desc,status", "status,id desc", null })
        {
            var options = new Dictionary<string, string> { ["$select"] = "id", ["$top"] = "100" };
            if (order is not null)
            {
                options["$orderby"] = order;
            }

            var (all, _, end) = await engine.QueryAsync(RestParser.Parse(options), orders, null, default);
            Assert.Null(end);
            var paged = new List<int>();
            string? cursor = null;
            for (var page = 0; page < 10; page++)
            {
                var pageOptions = new Dictionary<string, string>(options) { ["$top"] = "2" };
                if (cursor is not null)
                {
                    pageOptions["$after"] = cursor;
                }

                var (rows, _, next) = await engine.QueryAsync(RestParser.Parse(pageOptions), orders, null, default);
                Assert.All(rows, r => Assert.Equal(["id"], r.Keys));
                paged.AddRange(Ids(rows));
                cursor = next;
                if (cursor is null)
                {
                    break;
                }
            }

            Assert.Equal(Ids(all), paged);
        }

        // A cursor continues only its own order, never with $skip, and only one this API gave.
        var (first, _, cursor2) = await engine.QueryAsync(RestParser.Parse(new Dictionary<string, string> { ["$orderby"] = "status", ["$top"] = "2" }), orders, null, default);
        Assert.Equal(2, first.Count);
        Assert.NotNull(cursor2);
        await Assert.ThrowsAsync<ArgumentException>(() => engine.QueryAsync(RestParser.Parse(new Dictionary<string, string> { ["$orderby"] = "id", ["$after"] = cursor2! }), orders, null, default));
        await Assert.ThrowsAsync<ArgumentException>(() => engine.QueryAsync(RestParser.Parse(new Dictionary<string, string> { ["$orderby"] = "status", ["$skip"] = "2", ["$after"] = cursor2! }), orders, null, default));
        await Assert.ThrowsAsync<ArgumentException>(() => engine.QueryAsync(RestParser.Parse(new Dictionary<string, string> { ["$after"] = "not-a-cursor" }), orders, null, default));

        // Typed code pages the same way, with the same cursors.
        var expected = (await db.From<RelOrder>().OrderBy(o => o.ShipCountry).ThenByDescending(o => o.Status).ThenBy(o => o.Id).ToListAsync()).Select(o => o.Id).ToList();
        var typed = new List<int>();
        string? at = null;
        for (var page = 0; page < 10; page++)
        {
            var result = await db.From<RelOrder>().OrderBy(o => o.ShipCountry).ThenByDescending(o => o.Status).After(at).ToPageAsync(2);
            typed.AddRange(result.Items.Select(o => o.Id));
            at = result.NextCursor;
            if (at is null)
            {
                break;
            }
        }

        Assert.Equal(expected, typed);
        var (restFirst, _, restNext) = await engine.QueryAsync(RestParser.Parse(new Dictionary<string, string> { ["$orderby"] = "ship_country,status desc", ["$top"] = "2" }), orders, null, default);
        var (restSecond, _, _) = await engine.QueryAsync(
            RestParser.Parse(new Dictionary<string, string> { ["$orderby"] = "ship_country,status desc", ["$top"] = "2", ["$after"] = restNext! }),
            orders,
            null,
            default
        );
        var typedSecond = await db.From<RelOrder>().OrderBy(o => o.ShipCountry).ThenByDescending(o => o.Status).After(restNext).ToPageAsync(2);
        Assert.Equal(Ids(restSecond), typedSecond.Items.Select(o => o.Id));
        Assert.Equal(2, restFirst.Count);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.From<RelOrder>().OrderBy(o => o.Customer!.Name).ToPageAsync(2));

        // Ordered by a related row's column, a query is not paged by keys.
        var (_, _, none) = await engine.QueryAsync(RestParser.Parse(new Dictionary<string, string> { ["$orderby"] = "customer/name", ["$top"] = "2" }), orders, null, default);
        Assert.Null(none);

        await DropRelationTablesAsync(db);
    }

    [Fact]
    protected virtual async Task Rest_requests_query_and_write_rows_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        var data = await SeedRelationsAsync(db);
        var (o1, o2, o3, o4, o5, o6) = (data.Orders[0], data.Orders[1], data.Orders[2], data.Orders[3], data.Orders[4], data.Orders[5]);
        var engine = await RestEngineAsync(db);
        var orders = await RestTableAsync(db, "rel_orders");

        // Relations declared for the endpoint, followed in conditions and expanded; the count ignores the top.
        var query = RestParser.Parse(
            new Dictionary<string, string>
            {
                ["$filter"] = "customer/country in ('France', 'Germany') or lines/any(l: l/product/origin in ('England', 'Spain'))",
                ["$select"] = "id,status",
                ["$orderby"] = "id",
                ["$top"] = "4",
                ["$count"] = "true",
                ["$expand"] = "lines($orderby=id;$expand=product),customer",
            }
        );
        var (rows, count, _) = await engine.QueryAsync(query, orders, null, default);
        Assert.Equal(5, count);
        Assert.Equal([o1, o2, o3, o4], rows.Select(r => Convert.ToInt32(r["id"], System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Equal(["id", "status", "lines", "customer"], rows[0].Keys);
        Assert.Equal("Alice", ((Dictionary<string, object?>)rows[0]["customer"]!)["name"]);
        Assert.Null(rows[3]["customer"]);
        var lines = (List<Dictionary<string, object?>>)rows[1]["lines"]!;
        Assert.Equal(["Tea", "Rice"], lines.Select(l => ((Dictionary<string, object?>)l["product"]!)["name"]));

        // An expand's own filter and top, per owner.
        var latest = await engine.QueryAsync(
            RestParser.Parse(new Dictionary<string, string> { ["$orderby"] = "id", ["$expand"] = "orders($filter=status ne 'cancelled';$orderby=id desc;$top=1;$select=id)" }),
            await RestTableAsync(db, "rel_customers"),
            null,
            default
        );
        Assert.Equal([o1, o2, o3, o5], latest.Rows.Select(c => Convert.ToInt32(((List<Dictionary<string, object?>>)c["orders"]!).Single()["id"], System.Globalization.CultureInfo.InvariantCulture)));

        // Names the request may not use are refused.
        await Assert.ThrowsAsync<ArgumentException>(() => engine.QueryAsync(RestParser.Parse(new Dictionary<string, string> { ["$filter"] = "shipper/name eq 'x'" }), orders, null, default));
        await Assert.ThrowsAsync<ArgumentException>(() => engine.QueryAsync(RestParser.Parse(new Dictionary<string, string> { ["$expand"] = "notes" }), orders, null, default));

        // Writes: rows inserted, columns set and rows deleted by condition.
        var notes = await RestTableAsync(db, "rel_notes");
        Assert.Equal(
            2,
            await engine.InsertAsync(
                notes,
                [
                    new Dictionary<string, object?> { ["order_id"] = (long)o5, ["flagged"] = true },
                    new Dictionary<string, object?> { ["order_id"] = (long)o6, ["flagged"] = "false" },
                ],
                null,
                default
            )
        );
        Assert.Equal(4, await db.CountAsync<RelNote>());
        Assert.Equal(2, await engine.UpdateAsync(notes, new Dictionary<string, object?> { ["flagged"] = true }, RestParser.ParseFilter("order_id ge " + o5), null, default));
        Assert.Equal(3, await db.CountAsync<RelNote>(n => n.Flagged));
        Assert.Equal(1, await engine.DeleteAsync(notes, RestParser.ParseFilter($"order_id eq {o6}"), null, default));
        await Assert.ThrowsAsync<ArgumentException>(() => engine.UpdateAsync(notes, new Dictionary<string, object?> { ["nosuch"] = 1 }, RestParser.ParseFilter("true"), null, default));
        Assert.Equal(3, await engine.DeleteAsync(notes, RestParser.ParseFilter("true"), null, default));

        await DropRelationTablesAsync(db);
    }

    // What GraphQL reads and writes through: pages with each row's cursor and whether more follow (one row past the limit
    // on a page), a relation expanded twice under two names, the keys of rows an update changed, and rows read by their
    // locked keys before they are deleted.
    [Fact]
    protected virtual async Task Rest_pages_with_cursors_aliases_and_keys_of_writes_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        await SeedRelationsAsync(db);
        var engine = await RestEngineAsync(db, new RestLimits { MaxTop = 2 });
        var orders = await RestTableAsync(db, "rel_orders");
        var lines = await RestTableAsync(db, "rel_lines");
        var ids = (await db.SelectAsync<RelOrder>()).Select(o => o.Id).Order().ToList();

        // Two rows a page, the most the limits allow: the probe reads a third to know more follow.
        var first = await engine.PageAsync(new RestQuery { Top = 2, OrderBy = [new RestOrder(new RestPath(["id"]), false)], Cursors = true, Probe = true }, orders, null, default);
        Assert.Equal(ids.Take(2), first.Rows.Select(r => Convert.ToInt32(r["id"], System.Globalization.CultureInfo.InvariantCulture)));
        Assert.True(first.HasMore);
        Assert.Equal(2, first.Cursors!.Count);
        Assert.Equal(first.Cursors[1], first.NextCursor);
        var second = await engine.PageAsync(new RestQuery { Top = 2, OrderBy = [new RestOrder(new RestPath(["id"]), false)], Cursors = true, Probe = true, After = first.Cursors[1] }, orders, null, default);
        Assert.Equal(ids.Skip(2).Take(2), second.Rows.Select(r => Convert.ToInt32(r["id"], System.Globalization.CultureInfo.InvariantCulture)));
        var last = await engine.PageAsync(new RestQuery { Top = 2, Filter = RestParser.ParseFilter($"id eq {ids[^1]}"), Probe = true }, orders, null, default);
        Assert.False(last.HasMore);
        await Assert.ThrowsAsync<ArgumentException>(() => engine.PageAsync(new RestQuery { Top = 3 }, orders, null, default));
        await Assert.ThrowsAsync<ArgumentException>(() => engine.PageAsync(new RestQuery { Top = 2, OrderBy = [new RestOrder(new RestPath(["customer", "name"]), false)], Cursors = true }, orders, null, default));

        // One relation, two names, two sets of rows.
        var everything = await RestEngineAsync(db);
        var (expanded, _, _) = await everything.QueryAsync(
            new RestQuery
            {
                Expand =
                [
                    new RestExpand("lines", new RestQuery { Filter = RestParser.ParseFilter("qty gt 1") }, "big"),
                    new RestExpand("lines", new RestQuery { Filter = RestParser.ParseFilter("qty le 1") }, "small"),
                ],
            },
            orders,
            null,
            default
        );
        var all = await db.SelectAsync<RelLine>();
        Assert.Equal(all.Count(l => l.Qty > 1), expanded.Sum(o => ((List<Dictionary<string, object?>>)o["big"]!).Count));
        Assert.Equal(all.Count(l => l.Qty <= 1), expanded.Sum(o => ((List<Dictionary<string, object?>>)o["small"]!).Count));
        Assert.DoesNotContain(expanded, o => o.ContainsKey("lines"));

        // The keys of the rows an update changed.
        if (db.State != ConnectionState.Open)
        {
            db.Open();
        }

        using (var tx = db.BeginTransaction())
        {
            var changed = new List<IReadOnlyDictionary<string, object?>>();
            var updated = await everything.UpdateAsync(lines, new Dictionary<string, object?> { ["qty"] = 9 }, RestParser.ParseFilter("qty gt 1"), tx, default, keys: changed);
            Assert.Equal(updated, changed.Count);
            Assert.Equal(
                all.Where(l => l.Qty > 1).Select(l => l.Id).Order(),
                changed.Select(k => Convert.ToInt32(k["id"], System.Globalization.CultureInfo.InvariantCulture)).Order()
            );

            // Rows read by their keys, locked, then exactly those deleted.
            IReadOnlyList<IReadOnlyDictionary<string, object?>>? seen = null;
            var deleted = await everything.DeleteReadingAsync(
                lines,
                RestParser.ParseFilter("qty eq 9"),
                tx,
                async keys =>
                {
                    seen = keys;
                    var (rows, _, _) = await everything.QueryAsync(new RestQuery { Filter = new RestTrusted(RestEngine.KeyFilter(lines, keys)) }, lines, tx, default);
                    Assert.All(rows, r => Assert.Equal(9, Convert.ToInt32(r["qty"], System.Globalization.CultureInfo.InvariantCulture)));
                    Assert.Equal(keys.Count, rows.Count);
                },
                default
            );
            Assert.Equal(changed.Count, deleted.Count);
            Assert.Same(deleted, seen);
            Assert.Equal(0, (await everything.QueryAsync(new RestQuery { Filter = RestParser.ParseFilter("qty eq 9"), Count = true }, lines, tx, default)).Count);
            tx.Rollback();
        }

        await DropRelationTablesAsync(db);
    }

    private static async Task<RestTable> RestTableAsync(IDbConnection db, string name)
    {
        var table = (await db.GetTableAsync(null, name))!;
        return RestTable.Of(table, DmlDialect.For(db).Table(null, name));
    }

    // The relations an endpoint would declare, over descriptions read once per table.
    private static async Task<RestEngine> RestEngineAsync(IDbConnection db, RestLimits? limits = null)
    {
        var tables = new Dictionary<string, RestTable>();
        foreach (var name in new[] { "rel_orders", "rel_customers", "rel_lines", "rel_products" })
        {
            tables[name] = await RestTableAsync(db, name);
        }

        var declared = new Dictionary<(string Table, string Name), (string Other, (string, string)[] Keys, bool Many)>
        {
            [("rel_orders", "customer")] = ("rel_customers", [("customer_id", "id")], false),
            [("rel_orders", "lines")] = ("rel_lines", [("id", "order_id")], true),
            [("rel_lines", "product")] = ("rel_products", [("product_id", "id")], false),
            [("rel_customers", "orders")] = ("rel_orders", [("id", "customer_id")], true),
        };

        return new RestEngine(
            db,
            limits ?? new RestLimits(),
            (from, name, _) =>
                Task.FromResult(
                    declared.TryGetValue((from.Name, name), out var d) ? new RestRelation(name, tables[d.Other], d.Keys, d.Many) : null
                )
        );
    }
}
