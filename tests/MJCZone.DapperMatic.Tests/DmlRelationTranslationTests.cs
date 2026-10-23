// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Linq.Expressions;
using MJCZone.DapperMatic.DataAnnotations;
using MJCZone.DapperMatic.Dml;
using MJCZone.DapperMatic.Dml.Internal;

namespace MJCZone.DapperMatic.Tests;

/// <summary>
/// The SQL conditions through relations become, without a database.
/// </summary>
public class DmlRelationTranslationTests
{
    private static readonly IDbConnection Sqlite = new Microsoft.Data.Sqlite.SqliteConnection();
    private static readonly IDbConnection SqlServer = new Microsoft.Data.SqlClient.SqlConnection();
    private static readonly IDbConnection PostgreSql = new Npgsql.NpgsqlConnection();
    private static readonly IDbConnection MySql = new MySqlConnector.MySqlConnection();

    [Fact]
    public void A_related_rows_columns_are_joined_and_read_as_nulls_when_it_is_missing()
    {
        Assert.Equal(
            "FROM \"rel_orders\" t0 LEFT JOIN \"rel_customers\" t1 ON t1.\"id\" = t0.\"customer_id\" WHERE t1.\"country\" = @p0",
            Orders(o => o.Customer!.Country == "France")
        );

        // As o.Customer?.Country != "France" would be: true for an order with no customer.
        Assert.Equal(
            "FROM \"rel_orders\" t0 LEFT JOIN \"rel_customers\" t1 ON t1.\"id\" = t0.\"customer_id\" "
                + "WHERE (t1.\"country\" <> @p0 OR t1.\"country\" IS NULL)",
            Orders(o => o.Customer!.Country != "France")
        );

        // One join per relation, however often it is used, here by a condition and an ordering.
        Assert.Equal(
            "FROM \"rel_orders\" t0 LEFT JOIN \"rel_customers\" t1 ON t1.\"id\" = t0.\"customer_id\" "
                + "WHERE (t1.\"country\" = @p0 AND t1.\"is_active\" = @p1) ORDER BY t1.\"name\" LIMIT 5",
            From(Sqlite.From<RelOrder>().Where(o => o.Customer!.Country == "France" && o.Customer.IsActive).OrderBy(o => o.Customer!.Name).Take(5))
        );

        // Paths of paths join each relation in turn.
        Assert.Equal(
            "FROM \"rel_lines\" t0 LEFT JOIN \"rel_orders\" t1 ON t1.\"id\" = t0.\"order_id\" "
                + "LEFT JOIN \"rel_customers\" t2 ON t2.\"id\" = t1.\"customer_id\" WHERE t2.\"country\" = @p0",
            From(Sqlite.From<RelLine>().Where(l => l.Order!.Customer!.Country == "France"))
        );

        Assert.Equal(
            "SELECT DISTINCT t0.[id] AS [Id], t0.[customer_id] AS [CustomerId], t0.[status] AS [Status], t0.[ship_country] AS "
                + "[ShipCountry] FROM [dbo].[rel_orders] t0 LEFT JOIN [dbo].[rel_customers] t1 ON t1.[id] = t0.[customer_id] "
                + "WHERE t1.[is_active] = @p0 ORDER BY 1 OFFSET 1 ROWS FETCH NEXT 2 ROWS ONLY",
            SqlServer.From<RelOrder>().Where(o => o.Customer!.IsActive).Distinct().Skip(1).Take(2).ToSql().Sql
        );
    }

    [Fact]
    public void Related_rows_are_tested_with_exists()
    {
        string[] countries = ["France", "Germany"];
        string[] origins = ["England", "Spain"];
        Assert.Equal(
            "FROM \"rel_orders\" t0 LEFT JOIN \"rel_customers\" t1 ON t1.\"id\" = t0.\"customer_id\" WHERE "
                + "(t1.\"country\" IN (@p0, @p1) OR EXISTS (SELECT 1 FROM \"rel_lines\" t2 LEFT JOIN \"rel_products\" t3 ON "
                + "t3.\"id\" = t2.\"product_id\" WHERE t2.\"order_id\" = t0.\"id\" AND t3.\"origin\" IN (@p2, @p3)))",
            Orders(o => countries.Contains(o.Customer!.Country) || o.Lines!.Any(l => origins.Contains(l.Product!.Origin)))
        );

        Assert.Equal(
            "FROM \"rel_orders\" t0 WHERE NOT (EXISTS (SELECT 1 FROM \"rel_customers\" t1 WHERE t1.\"id\" = t0.\"customer_id\"))",
            Orders(o => o.Customer == null)
        );
        Assert.Equal(
            "FROM \"rel_lines\" t0 LEFT JOIN \"rel_orders\" t1 ON t1.\"id\" = t0.\"order_id\" WHERE EXISTS "
                + "(SELECT 1 FROM \"rel_customers\" t2 WHERE t2.\"id\" = t1.\"customer_id\")",
            From(Sqlite.From<RelLine>().Where(l => l.Order!.Customer != null))
        );

        // All: no row fails the condition, a NULL quantity failing as it does in C#.
        Assert.Equal(
            "FROM \"rel_orders\" t0 WHERE NOT (EXISTS (SELECT 1 FROM \"rel_lines\" t1 WHERE t1.\"order_id\" = t0.\"id\" AND t1.\"qty\" <= @p0))",
            Orders(o => o.Lines!.All(l => l.Qty > 1))
        );
        Assert.Equal(
            "FROM \"rel_orders\" t0 WHERE EXISTS (SELECT 1 FROM \"rel_lines\" t1 WHERE t1.\"order_id\" = t0.\"id\" AND t1.\"qty\" > @p0)",
            Orders(o => o.Lines!.Where(l => l.Qty > 1).Any())
        );

        // A related row of the query's row, inside a subquery, is joined to the query.
        Assert.Equal(
            "FROM \"rel_orders\" t0 LEFT JOIN \"rel_customers\" t3 ON t3.\"id\" = t0.\"customer_id\" WHERE EXISTS (SELECT 1 FROM "
                + "\"rel_lines\" t1 LEFT JOIN \"rel_products\" t2 ON t2.\"id\" = t1.\"product_id\" WHERE t1.\"order_id\" = t0.\"id\" "
                + "AND (t2.\"origin\" = t3.\"country\" OR (t2.\"origin\" IS NULL AND t3.\"country\" IS NULL)))",
            Orders(o => o.Lines!.Any(l => l.Product!.Origin == o.Customer!.Country))
        );
    }

    [Fact]
    public void Values_over_related_rows_are_subqueries()
    {
        Assert.Equal(
            "FROM \"rel_orders\" t0 WHERE (SELECT COUNT(*) FROM \"rel_lines\" t1 WHERE t1.\"order_id\" = t0.\"id\") > @p0",
            Orders(o => o.Lines!.Count() > 2)
        );
        Assert.Equal(Orders(o => o.Lines!.Count() >= 1), Orders(o => o.Lines!.Count >= 1));

        // Sum of no rows is 0, as in C#.
        Assert.Equal(
            "FROM \"rel_orders\" t0 WHERE (SELECT COALESCE(SUM((t1.\"qty\" * t2.\"price\")), 0) FROM \"rel_lines\" t1 LEFT JOIN "
                + "\"rel_products\" t2 ON t2.\"id\" = t1.\"product_id\" WHERE t1.\"order_id\" = t0.\"id\") > CAST(@p0 AS NUMERIC)",
            Orders(o => o.Lines!.Sum(l => l.Qty * l.Product!.Price) > 100)
        );
        Assert.Contains("(SELECT AVG(CAST(t1.[qty] AS float)) FROM", SqlServer.From<RelOrder>().Where(o => o.Lines!.Average(l => l.Qty) > 2).ToSql().Sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Many_to_many_relations_go_through_the_link_table()
    {
        Assert.Equal(
            "FROM \"rel_products\" t0 WHERE EXISTS (SELECT 1 FROM \"rel_product_tags\" t1 INNER JOIN \"rel_tags\" t2 ON "
                + "t2.\"id\" = t1.\"tag_id\" WHERE t1.\"product_id\" = t0.\"id\" AND t2.\"name\" = @p0)",
            From(Sqlite.From<RelProduct>().Where(p => p.Tags!.Any(t => t.Name == "sale")))
        );
        Assert.Contains(
            "EXISTS (SELECT 1 FROM `rel_product_tags` t3 INNER JOIN `rel_tags` t4 ON t4.`id` = t3.`tag_id` WHERE t3.`product_id` = t2.`id`",
            MySql.From<RelOrder>().Where(o => o.Lines!.Any(l => l.Product!.Tags!.Any(t => t.Name == "sale"))).ToSql().Sql,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Relations_without_foreign_keys_join_on_their_keys_values_and_condition()
    {
        // The type value and the relation's condition (not deleted) join the attribute rows to their document.
        Assert.Equal(
            "FROM \"rel_documents\" t0 WHERE EXISTS (SELECT 1 FROM \"rel_attributes\" t1 WHERE t1.\"object_id\" = t0.\"id\" AND "
                + "t1.\"object_type\" = @p0 AND t1.\"is_deleted\" = @p1 AND (t1.\"name\" = @p2 AND t1.\"value\" = @p3))",
            From(Sqlite.From<RelDocument>().Where(d => d.Attributes!.Any(a => a.Name == "color" && a.Value == "red")))
        );
        var one = Sqlite.From<RelDocument>().Where(d => d.Color!.Value == "red").ToSql();
        Assert.EndsWith(
            "FROM \"rel_documents\" t0 LEFT JOIN \"rel_attributes\" t1 ON (t1.\"object_id\" = t0.\"id\" AND t1.\"object_type\" = @p0 "
                + "AND t1.\"name\" = @p1) WHERE t1.\"value\" = @p2",
            one.Sql
        );
        Assert.Equal(["document", "color", "red"], one.Parameters.Select(p => p.Value?.ToString()));
    }

    [Fact]
    public void Exists_correlates_models_with_no_relation_property()
    {
        Assert.Equal(
            "FROM \"rel_orders\" t0 WHERE EXISTS (SELECT 1 FROM \"rel_notes\" t1 WHERE (t1.\"order_id\" = t0.\"id\" AND t1.\"flagged\" = @p0))",
            From(Sqlite.From<RelOrder>().WhereExists<RelNote>((o, n) => n.OrderId == o.Id && n.Flagged))
        );
        Assert.Equal(
            "FROM \"rel_orders\" t0 WHERE NOT (EXISTS (SELECT 1 FROM \"rel_lines\" t1 CROSS JOIN \"rel_products\" t2 WHERE "
                + "((t1.\"order_id\" = t0.\"id\" AND t2.\"id\" = t1.\"product_id\") AND t2.\"origin\" = @p0)))",
            From(Sqlite.From<RelOrder>().WhereNotExists<RelLine, RelProduct>((o, l, p) => l.OrderId == o.Id && p.Id == l.ProductId && p.Origin == "Spain"))
        );
        Assert.Contains(
            "FROM \"public\".\"rel_orders\" t0 WHERE EXISTS (SELECT 1 FROM \"public\".\"rel_notes\" t1",
            PostgreSql.From<RelOrder>().WhereExists<RelNote>((o, n) => n.OrderId == o.Id).ToSql().Sql,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Related_tables_follow_the_scope()
    {
        var sql = new DmScope(PostgreSql).InSchema("tenant_7").WithTableName<RelCustomer>("clients").From<RelOrder>()
            .Where(o => o.Customer!.Country == "France")
            .ToSql()
            .Sql;
        Assert.Contains("FROM \"tenant_7\".\"rel_orders\" t0 LEFT JOIN \"tenant_7\".\"clients\" t1 ON", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Relations_that_cannot_be_resolved_or_followed_are_refused()
    {
        // Two foreign keys to customers, and no key named.
        var ambiguous = Assert.Throws<InvalidOperationException>(() => Sqlite.From<Shipment>().Where(s => s.From!.Name == "x").ToSql());
        Assert.Contains("declares 2 foreign keys to RelCustomer", ambiguous.Message, StringComparison.Ordinal);
        Assert.Contains("t1.\"id\" = t0.\"to_id\"", Sqlite.From<Shipment>().Where(s => s.To!.Name == "x").ToSql().Sql, StringComparison.Ordinal);

        // One related row whose keys are not unique, keys of two types, and a link to one row.
        Assert.Contains(
            "several rows could match",
            Assert.Throws<InvalidOperationException>(() => Sqlite.From<Shipment>().Where(s => s.SameCountry!.Name == "x").ToSql()).Message,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "must be of one type",
            Assert.Throws<InvalidOperationException>(() => Sqlite.From<Shipment>().Where(s => s.Mismatched!.Any()).ToSql()).Message,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "must be a collection",
            Assert.Throws<InvalidOperationException>(() => Sqlite.From<Shipment>().Where(s => s.OneTag!.Name == "x").ToSql()).Message,
            StringComparison.Ordinal
        );

        // A collection is not a row, and is never null in the database.
        Assert.Throws<DmTranslationException>(() => Orders(o => o.Lines == null));
        Assert.Throws<DmTranslationException>(() => Orders(o => o.Lines!.First().Qty > 1));
        Assert.Throws<DmTranslationException>(() => Orders(o => o.Customer == new RelCustomer()));

        // An upsert's expressions are over the rows' own columns.
        var model = DmlModel.For(typeof(RelOrder));
        var current = new SqlSource("\"rel_orders\"", "t", model.Table, model);
        var incoming = new SqlSource("excluded", "excluded", model.Table, model);
        Expression<Func<RelOrder, RelOrder, bool>> newer = (c, i) => c.Customer!.IsActive;
        Assert.Throws<DmTranslationException>(() => DmlTranslator.Condition(newer, [current, incoming], DmlDialect.For(Sqlite)));
    }

    private static string Orders(Expression<Func<RelOrder, bool>> where) => From(Sqlite.From<RelOrder>().Where(where));

    private static string From<T>(DmQuery<T> query)
        where T : class
    {
        var sql = query.ToSql().Sql;
        return sql[(sql.IndexOf(" FROM ", StringComparison.Ordinal) + 1)..];
    }

    [DmTable(tableName: "rel_shipments")]
    public class Shipment
    {
        [DmColumn("id", isPrimaryKey: true)]
        public int Id { get; set; }

        [DmColumn("from_id", isForeignKey: true, referencedTableName: "rel_customers", referencedColumnName: "id")]
        public int FromId { get; set; }

        [DmColumn("to_id", isForeignKey: true, referencedTableName: "rel_customers", referencedColumnName: "id")]
        public int ToId { get; set; }

        [DmColumn("label", length: 50)]
        public string Label { get; set; } = string.Empty;

        [DmRelation]
        public RelCustomer? From { get; set; }

        [DmRelation(nameof(ToId))]
        public RelCustomer? To { get; set; }

        [DmRelation(thisKeys: [nameof(Label)], otherKeys: [nameof(RelCustomer.Country)])]
        public RelCustomer? SameCountry { get; set; }

        [DmRelation(thisKeys: [nameof(Label)], otherKeys: [nameof(RelCustomer.Id)])]
        public List<RelCustomer>? Mismatched { get; set; }

        [DmRelation(through: typeof(RelProductTag))]
        public RelTag? OneTag { get; set; }
    }
}
