// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using MJCZone.DapperMatic.Dml;

namespace MJCZone.DapperMatic.Tests;

/// <summary>
/// The SQL of projections the database computes, of grouping, and of joins written out, without a database.
/// </summary>
public class DmlGroupingTranslationTests
{
    private static readonly IDbConnection Sqlite = new Microsoft.Data.Sqlite.SqliteConnection();
    private static readonly IDbConnection SqlServer = new Microsoft.Data.SqlClient.SqlConnection();
    private static readonly IDbConnection MySql = new MySqlConnector.MySqlConnection();

    [Fact]
    public void Projections_read_related_values_in_the_select_list()
    {
        Assert.Equal(
            "SELECT t0.\"id\" AS \"Id\", t1.\"name\" AS \"dm_p0\" FROM \"rel_orders\" t0 LEFT JOIN \"rel_customers\" t1 ON t1.\"id\" = t0.\"customer_id\"",
            Sqlite.From<RelOrder>().Select(o => new { o.Id, Customer = o.Customer!.Name }).ToSql().Sql
        );
        Assert.Equal(
            "SELECT DISTINCT t1.\"country\" AS \"dm_p0\" FROM \"rel_orders\" t0 LEFT JOIN \"rel_customers\" t1 ON t1.\"id\" = t0.\"customer_id\"",
            Sqlite.From<RelOrder>().Select(o => o.Customer!.Country).Distinct().ToSql().Sql
        );

        // A test is read as 1 or 0: SQL Server has no boolean expressions in a select list.
        Assert.Equal(
            "SELECT t0.[id] AS [Id], CASE WHEN EXISTS (SELECT 1 FROM [dbo].[rel_customers] t1 WHERE t1.[id] = t0.[customer_id]) "
                + "THEN 1 ELSE 0 END AS [dm_p0] FROM [dbo].[rel_orders] t0",
            SqlServer.From<RelOrder>().Select(o => new { o.Id, Has = o.Customer != null }).ToSql().Sql
        );

        // The condition, the ordering and the selector share one join; what runs in .NET is left out.
        Assert.Equal(
            "SELECT t0.\"status\" AS \"Status\", t1.\"name\" AS \"dm_p0\" FROM \"rel_orders\" t0 LEFT JOIN \"rel_customers\" t1 ON "
                + "t1.\"id\" = t0.\"customer_id\" WHERE t1.\"is_active\" = @p0 ORDER BY t1.\"name\"",
            Sqlite.From<RelOrder>().Where(o => o.Customer!.IsActive).OrderBy(o => o.Customer!.Name).Select(o => o.Status + "/" + o.Customer!.Name.ToUpper()).ToSql().Sql
        );

        // SQL Server refuses an aggregate of a subquery, so every provider does.
        Assert.Throws<DmTranslationException>(() => Sqlite.From<RelCustomer>().Select(c => c.Orders!.Sum(o => o.Lines!.Count())).ToSql());
    }

    [Fact]
    public void Groups_are_one_group_by()
    {
        Assert.Equal(
            "SELECT \"customer_id\" AS \"dm_p0\", COUNT(*) AS \"dm_p1\" FROM \"rel_orders\" GROUP BY \"customer_id\"",
            Sqlite.From<RelOrder>().GroupBy(o => o.CustomerId).Select(g => new { g.Key, Count = g.Count() }).ToSql().Sql
        );

        // A related row's column as the key; counts of some rows as CASE inside the aggregate; Having and ordering.
        Assert.Equal(
            "SELECT t1.\"country\" AS \"dm_p0\", COUNT(*) AS \"dm_p1\", COUNT(CASE WHEN t0.\"status\" = @p0 THEN 1 ELSE NULL END) AS \"dm_p2\", "
                + "COUNT(CASE WHEN t0.\"ship_country\" IS NULL THEN 1 ELSE NULL END) AS \"dm_p3\" FROM \"rel_orders\" t0 LEFT JOIN "
                + "\"rel_customers\" t1 ON t1.\"id\" = t0.\"customer_id\" WHERE t0.\"status\" <> @p1 GROUP BY t1.\"country\" "
                + "HAVING COUNT(*) > @p2 ORDER BY COUNT(*) DESC",
            Sqlite.From<RelOrder>()
                .Where(o => o.Status != "x")
                .GroupBy(o => o.Customer!.Country)
                .Having(g => g.Count() > 1)
                .OrderByDescending(g => g.Count())
                .Select(g => new { Country = g.Key, N = g.Count(), Open = g.Count(o => o.Status == "open"), Any = g.Any(o => o.ShipCountry == null) })
                .ToSql()
                .Sql
        );

        // A key of two values and an element of two; sums are 0 for no values, as in C#.
        Assert.Equal(
            "SELECT t0.\"order_id\" AS \"dm_p0\", t0.\"order_id\" AS \"dm_p1\", t1.\"origin\" AS \"dm_p2\", COALESCE(SUM((t0.\"qty\" * t1.\"price\")), 0) "
                + "AS \"dm_p3\", MAX(t0.\"qty\") AS \"dm_p4\" FROM \"rel_lines\" t0 LEFT JOIN \"rel_products\" t1 ON t1.\"id\" = t0.\"product_id\" "
                + "GROUP BY t0.\"order_id\", t1.\"origin\"",
            Sqlite.From<RelLine>()
                .GroupBy(l => new { l.OrderId, l.Product!.Origin }, l => new { l.Qty, l.Product!.Price })
                .Select(g => new { g.Key.OrderId, g.Key, Total = g.Sum(x => x.Qty * x.Price), Max = g.Max(x => x.Qty) })
                .ToSql()
                .Sql
        );

        // All counts the rows that fail, Where filters inside the aggregate, and a value over the key is the database's.
        Assert.Contains(
            "COUNT(CASE WHEN (\"customer_id\" <= @p0 OR \"customer_id\" IS NULL) THEN 1 ELSE NULL END) AS \"dm_p1\", "
                + "COALESCE(SUM(CASE WHEN \"id\" > @p1 THEN \"id\" ELSE NULL END), 0) AS \"dm_p2\"",
            Sqlite.From<RelOrder>().GroupBy(o => o.Status).Select(g => new { g.Key, All = g.All(o => o.CustomerId > 1), W = g.Where(o => o.Id > 2).Sum(o => o.Id) }).ToSql().Sql,
            StringComparison.Ordinal
        );
        Assert.Equal(
            "SELECT (COALESCE([status], '') + @p0) AS [dm_p0] FROM [dbo].[rel_orders] GROUP BY [status] ORDER BY [status] OFFSET 1 ROWS FETCH NEXT 2 ROWS ONLY",
            SqlServer.From<RelOrder>().GroupBy(o => o.Status).OrderBy(g => g.Key).Skip(1).Take(2).Select(g => g.Key + "!").ToSql().Sql
        );

        Assert.Throws<DmTranslationException>(() => Sqlite.From<RelOrder>().GroupBy(o => o.CustomerId).Select(g => g.First()).ToSql());
        Assert.Throws<DmTranslationException>(() => Sqlite.From<RelOrder>().GroupBy(o => o.CustomerId).Select(g => g.Sum(o => o.Lines!.Count())).ToSql());
        Assert.Throws<InvalidOperationException>(() => Sqlite.From<RelOrder>().OrderBy(o => o.Id).GroupBy(o => o.Status));

        // A constant key groups by nothing, and makes no group of no rows.
        var everything = SqlServer.From<RelOrder>().GroupBy(o => 1).Select(g => new { Count = g.Count() }).ToSql().Sql;
        Assert.DoesNotContain("GROUP BY", everything, StringComparison.Ordinal);
        Assert.Contains("HAVING COUNT(*) > 0", everything, StringComparison.Ordinal);
        Assert.EndsWith("GROUP BY [status]", SqlServer.From<RelOrder>().GroupBy(o => new { o.Status, One = 1 }).Select(g => g.Count()).ToSql().Sql, StringComparison.Ordinal);

        // Averages of integers keep their fraction: SQL Server would average as an integer, MySQL to four places.
        Assert.Contains("AVG(`id` * 1.0E0)", MySql.From<RelOrder>().GroupBy(o => o.Status).Select(g => g.Average(o => o.Id)).ToSql().Sql, StringComparison.Ordinal);
        Assert.Contains("AVG(CAST(", SqlServer.From<RelOrder>().Select(o => o.Lines!.Average(l => (double)l.Qty)).ToSql().Sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Joins_are_written_out()
    {
        Assert.Equal(
            "SELECT t0.\"id\" AS \"Id\", t0.\"customer_id\" AS \"CustomerId\", t0.\"status\" AS \"Status\", t0.\"ship_country\" AS \"ShipCountry\", "
                + "t1.\"id\" AS \"Id\", t1.\"order_id\" AS \"OrderId\", t1.\"flagged\" AS \"Flagged\" FROM \"rel_orders\" t0 INNER JOIN "
                + "\"rel_notes\" t1 ON t1.\"order_id\" = t0.\"id\" WHERE t0.\"status\" = @p0",
            Sqlite.From<RelOrder>().Where(o => o.Status == "open").Join<RelNote>((o, n) => n.OrderId == o.Id).ToSql().Sql
        );

        // A LEFT JOIN's condition is over rows that exist; after it, its row's columns compare as C# would with a null row.
        Assert.Equal(
            "SELECT t0.\"id\" AS \"dm_p0\", t1.\"name\" AS \"dm_p1\", (SELECT COUNT(*) FROM \"rel_lines\" t2 WHERE t2.\"order_id\" = t0.\"id\") "
                + "AS \"dm_p2\" FROM \"rel_orders\" t0 LEFT JOIN \"rel_customers\" t1 ON t1.\"id\" = t0.\"customer_id\" WHERE "
                + "(t1.\"name\" <> @p0 OR t1.\"name\" IS NULL) ORDER BY t1.\"name\"",
            Sqlite.From<RelOrder>()
                .LeftJoin<RelCustomer>((o, c) => c.Id == o.CustomerId)
                .Where((o, c) => c.Name != "Alice")
                .OrderBy((o, c) => c.Name)
                .Select((o, c) => new { o.Id, c.Name, Lines = o.Lines!.Count() })
                .ToSql()
                .Sql
        );

        // A row the selector uses whole is read whole; groups of joined rows.
        Assert.Equal(
            "SELECT t2.\"id\" AS \"Id\", t2.\"name\" AS \"Name\", t2.\"country\" AS \"Country\", t2.\"is_active\" AS \"IsActive\", t0.\"qty\" AS \"dm_p0\" "
                + "FROM \"rel_lines\" t0 INNER JOIN \"rel_orders\" t1 ON t1.\"id\" = t0.\"order_id\" LEFT JOIN \"rel_customers\" t2 ON t2.\"id\" = t1.\"customer_id\"",
            Sqlite.From<RelLine>().Join<RelOrder>((l, o) => o.Id == l.OrderId).LeftJoin<RelCustomer>((l, o, c) => c.Id == o.CustomerId).Select((l, o, c) => new { l.Qty, c }).ToSql().Sql
        );
        Assert.Equal(
            "SELECT t1.\"origin\" AS \"dm_p0\", COALESCE(SUM((t0.\"qty\" * t1.\"price\")), 0) AS \"dm_p1\" FROM \"rel_lines\" t0 INNER JOIN "
                + "\"rel_products\" t1 ON t1.\"id\" = t0.\"product_id\" GROUP BY t1.\"origin\"",
            Sqlite.From<RelLine>()
                .Join<RelProduct>((l, p) => p.Id == l.ProductId)
                .GroupBy((l, p) => p.Origin, (l, p) => new { l.Qty, p.Price })
                .Select(g => new { g.Key, Total = g.Sum(x => x.Qty * x.Price) })
                .ToSql()
                .Sql
        );
        Assert.Equal(
            "SELECT DISTINCT t1.[country] AS [dm_p0] FROM [dbo].[rel_orders] t0 INNER JOIN [dbo].[rel_customers] t1 ON t1.[id] = t0.[customer_id]",
            SqlServer.From<RelOrder>().Join<RelCustomer>((o, c) => c.Id == o.CustomerId).Select((o, c) => c.Country).Distinct().ToSql().Sql
        );

        // A join's condition does not follow relations: they would be joined after it.
        Assert.Throws<DmTranslationException>(() => Sqlite.From<RelOrder>().Join<RelCustomer>((o, c) => c.Id == o.CustomerId && c.Orders!.Any()).ToSql());
        Assert.Throws<InvalidOperationException>(() => Sqlite.From<RelOrder>().Take(1).Join<RelNote>((o, n) => n.OrderId == o.Id));
    }
}
