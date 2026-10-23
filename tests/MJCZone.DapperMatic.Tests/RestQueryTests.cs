// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Text.Json;
using MJCZone.DapperMatic.Dml.Internal;
using MJCZone.DapperMatic.Dml.Rest;

namespace MJCZone.DapperMatic.Tests;

/// <summary>
/// REST queries: OData-shaped options and JSON trees parsed, and translated to SQL over tables the database describes.
/// </summary>
public class RestQueryTests
{
    private static readonly IDbConnection Sqlite = new Microsoft.Data.Sqlite.SqliteConnection();

    private static readonly RestTable Orders = Table("orders", ["id", typeof(int), false], ["customer_id", typeof(int), true], ["status", typeof(string), false], ["note", typeof(string), true], ["total", typeof(decimal), true]);
    private static readonly RestTable Customers = Table("customers", ["id", typeof(int), false], ["country", typeof(string), true]);
    private static readonly RestTable Lines = Table("lines", ["id", typeof(int), false], ["order_id", typeof(int), false], ["qty", typeof(int), false], ["product_id", typeof(int), false]);
    private static readonly RestTable Products = Table("products", ["id", typeof(int), false], ["origin", typeof(string), true]);

    private static readonly Dictionary<(RestTable Table, string Name), RestRelation> Relations = new()
    {
        [(Orders, "customer")] = new RestRelation("customer", Customers, [("customer_id", "id")], Many: false),
        [(Orders, "lines")] = new RestRelation("lines", Lines, [("id", "order_id")], Many: true),
        [(Lines, "product")] = new RestRelation("product", Products, [("product_id", "id")], Many: false),
    };

    [Fact]
    public void Options_parse_to_one_tree()
    {
        var query = RestParser.Parse(
            new Dictionary<string, string>
            {
                ["$filter"] = "status eq 'it''s' and (total gt 10.5 or not (customer/country in ('FR', 'DE', null)))",
                ["$select"] = "id, status",
                ["$orderby"] = "total desc, id",
                ["$top"] = "5",
                ["$skip"] = "10",
                ["$count"] = "true",
                ["$expand"] = "lines($filter=qty gt 1;$top=2;$expand=product),customer",
            }
        );

        Assert.Equal(["id", "status"], query.Select);
        Assert.Equal((5, 10, true), (query.Top, query.Skip, query.Count));
        Assert.Equal([true, false], query.OrderBy.Select(o => o.Descending));
        Assert.Equal(["lines", "customer"], query.Expand.Select(e => e.Relation));
        Assert.Equal(2, query.Expand[0].Query.Top);
        Assert.Equal("product", Assert.Single(query.Expand[0].Query.Expand).Relation);
        var and = Assert.IsType<RestBinary>(query.Filter);
        Assert.Equal(new RestLiteral("it's"), Assert.IsType<RestBinary>(and.Left).Right);

        // The JSON tree is the same tree.
        var json = JsonDocument.Parse("""{"and": [{"eq": [{"path": "status"}, {"value": "open"}]}, {"any": {"path": "lines", "as": "l", "where": {"gt": [{"path": "l/qty"}, {"value": 5}]}}}]}""");
        Assert.Equal(
            Sql(RestParser.ParseFilter("status eq 'open' and lines/any(l: l/qty gt 5)")),
            Sql(RestJson.Node(json.RootElement))
        );

        Assert.Throws<ArgumentException>(() => RestParser.ParseFilter("status eq"));
        Assert.Throws<ArgumentException>(() => RestParser.ParseFilter("status eq 'open"));
        Assert.Throws<ArgumentException>(() => RestParser.ParseFilter("status; drop table orders"));
        Assert.Throws<ArgumentException>(() => RestJson.Node(JsonDocument.Parse("""{"eq": ["status", "open"]}""").RootElement));
    }

    [Fact]
    public void Conditions_translate_as_the_typed_methods_do()
    {
        // NULLs keep their C# meaning; negations are pushed down.
        Assert.Equal("(\"note\" <> @p0 OR \"note\" IS NULL)", Sql("note ne 'x'"));
        Assert.Equal("(\"total\" <= CAST(@p0 AS NUMERIC) OR \"total\" IS NULL)", Sql("not (total gt 5)"));
        Assert.Equal("\"note\" IS NULL", Sql("note eq null"));
        Assert.Equal("(\"status\" IN (@p0, @p1) OR \"status\" IS NULL)", Sql("status in ('a', 'b', null)"));
        Assert.Equal("\"status\" LIKE @p0 ESCAPE '~'", Sql("startswith(status, 'op')"));
        Assert.Equal("LOWER(\"status\") = @p0", Sql("tolower(status) eq 'open'"));

        // Relations: a to-one path joined, collections as EXISTS, counts as subqueries.
        var select = Rows("customer/country eq 'FR' or lines/any(l: l/product/origin eq 'England')");
        Assert.Equal(
            "SELECT t0.\"id\" AS \"id\" FROM \"orders\" t0 LEFT JOIN \"customers\" t1 ON t1.\"id\" = t0.\"customer_id\" WHERE (t1.\"country\" = @p0 OR "
                + "EXISTS (SELECT 1 FROM \"lines\" t2 LEFT JOIN \"products\" t3 ON t3.\"id\" = t2.\"product_id\" WHERE t2.\"order_id\" = t0.\"id\" AND t3.\"origin\" = @p1))",
            select
        );
        Assert.Contains("NOT (EXISTS (SELECT 1 FROM \"lines\" t1 WHERE t1.\"order_id\" = t0.\"id\" AND t1.\"qty\" <= @p0))", Rows("lines/all(l: l/qty gt 1)"), StringComparison.Ordinal);
        Assert.Contains("(SELECT COUNT(*) FROM \"lines\" t1 WHERE t1.\"order_id\" = t0.\"id\") > @p0", Rows("lines/$count gt 2"), StringComparison.Ordinal);

        // Names are checked: unknown columns and relations, values of the wrong type, too much asked for.
        Assert.Throws<ArgumentException>(() => Sql("nosuch eq 1"));
        Assert.Throws<ArgumentException>(() => Sql("shipper/name eq 'x'"));
        Assert.Throws<ArgumentException>(() => Sql("lines/qty eq 1"));
        Assert.Throws<ArgumentException>(() => Sql("id eq 'abc'"));
        Assert.Throws<ArgumentException>(() => Sql(string.Join(" or ", Enumerable.Range(0, 150).Select(i => $"id eq {i}"))));
        Assert.Throws<ArgumentException>(() => Sql("lines/any(a: a/product/origin eq 'x' and lines/any(b: lines/any(c: c/qty eq 1)))"));
    }

    [Fact]
    public void Requests_too_deep_or_too_large_are_refused_before_anything_recurses_through_them()
    {
        // Each nesting is a level of recursion, and a stack overflow ends the process: the parser stops at 64.
        Assert.Equal("\"id\" = @p0", Sql(new string('(', 60) + "id eq 1" + new string(')', 60)));
        Assert.Throws<ArgumentException>(() => RestParser.ParseFilter(new string('(', 8_000) + "id eq 1" + new string(')', 8_000)));
        Assert.Throws<ArgumentException>(() => RestParser.ParseFilter(string.Concat(Enumerable.Repeat("not ", 1_000)) + "id eq 1"));
        Assert.Throws<ArgumentException>(() => RestParser.ParseFilter("id eq " + new string('-', 1_000) + "1"));
        Assert.Throws<ArgumentException>(() => RestParser.ParseExpand(string.Concat(Enumerable.Repeat("lines($expand=", 1_000)) + "lines" + new string(')', 1_000)));

        // A long chain of and or or is deep without nesting: the whole request is checked first, without recursing.
        RestNode chain = new RestLiteral(true);
        for (var i = 0; i < 100_000; i++)
        {
            chain = new RestBinary("and", chain, new RestLiteral(true));
        }

        Assert.Throws<ArgumentException>(() => new RestLimits { MaxNodes = int.MaxValue }.Check(new RestQuery { Filter = chain }));

        // Values of an in list count as parts, and expands' conditions count with the request's.
        var many = RestParser.ParseFilter($"id in ({string.Join(", ", Enumerable.Range(0, 250))})");
        Assert.Throws<ArgumentException>(() => new RestLimits().Check(new RestQuery { Filter = many }));
        var half = RestParser.ParseFilter(string.Join(" or ", Enumerable.Range(0, 40).Select(i => $"id eq {i}")));
        new RestLimits().Check(new RestQuery { Filter = half });
        Assert.Throws<ArgumentException>(
            () => new RestLimits().Check(new RestQuery { Filter = half, Expand = [new RestExpand("lines", new RestQuery { Filter = half })] })
        );

        // A number too large for a decimal is malformed input, not an error of the server.
        Assert.Throws<ArgumentException>(() => RestParser.ParseFilter("id eq 99999999999999999999999999999999"));
        Assert.Throws<ArgumentException>(() => RestJson.Node(JsonDocument.Parse("""{"eq": [{"path": "id"}, {"value": 1e400}]}""").RootElement));
        Assert.Equal(new RestLiteral(99999999999999999999m), Assert.IsType<RestBinary>(RestParser.ParseFilter("id eq 99999999999999999999")).Right);
    }

    [Fact]
    public void Each_statement_of_a_request_has_the_whole_node_budget()
    {
        // The rows, their count and each expand batch are translated by one translator, each within the limit.
        var filter = RestParser.ParseFilter(string.Join(" or ", Enumerable.Range(0, 40).Select(i => $"id eq {i}")));
        var translator = new RestTranslator(DmlDialect.For(Sqlite), Relations, new RestLimits());
        translator.Rows(new RestQuery { Filter = filter }, Orders, []);
        translator.Count(new RestQuery { Filter = filter }, Orders);
        translator.Rows(new RestQuery { Filter = filter }, Orders, []);
    }

    [Fact]
    public void A_search_counts_its_words_toward_the_request_size()
    {
        // Matched by contains, each word is a condition per column: 60 words over 2 columns pass a 100-part budget. (The
        // engine makes $search this node, ANDed with the filter.)
        var searchable = Orders.WithSearchable(new RestSearchable(["status", "note"]));
        var words = string.Join(' ', Enumerable.Range(0, 60).Select(i => $"w{i}"));
        Assert.Throws<ArgumentException>(() => new RestTranslator(DmlDialect.For(Sqlite), Relations, new RestLimits { MaxNodes = 100 }).Rows(new RestQuery { Filter = new RestSearch(words) }, searchable, []));
        new RestTranslator(DmlDialect.For(Sqlite), Relations, new RestLimits { MaxNodes = 100 }).Rows(new RestQuery { Filter = new RestSearch("a few words") }, searchable, []);
    }

    [Fact]
    public void A_count_of_related_rows_counts_those_its_condition_matches()
    {
        var json = JsonDocument.Parse("""{"gt": [{"count": {"path": "lines", "as": "l", "where": {"eq": [{"path": "l/qty"}, {"value": 2}]}}}, {"value": 0}]}""");
        var sql = Sql(RestJson.Node(json.RootElement));
        Assert.Contains("COUNT(*)", sql, StringComparison.Ordinal);
        Assert.Contains("\"qty\" = @p", sql, StringComparison.Ordinal);
    }

    private static string Rows(string filter)
    {
        var translator = new RestTranslator(DmlDialect.For(Sqlite), Relations, new RestLimits());
        var (select, _) = translator.Rows(new RestQuery { Filter = RestParser.ParseFilter(filter), Select = ["id"] }, Orders, []);
        return new SqlRenderer(DmlDialect.For(Sqlite), new DmlParameters()).For(select).Render(select);
    }

    private static string Sql(string filter) => Sql(RestParser.ParseFilter(filter));

    private static string Sql(RestNode filter)
    {
        var source = new SqlSource("\"orders\"", "t", Orders.ToTable(), null);
        var condition = new RestTranslator(DmlDialect.For(Sqlite), Relations, new RestLimits()).Condition(filter, Orders, source);
        return new SqlRenderer(DmlDialect.For(Sqlite), new DmlParameters()).For(condition).Render(condition);
    }

    private static RestTable Table(string name, params object[][] columns) =>
        new()
        {
            SchemaName = null,
            Name = name,
            QualifiedName = $"\"{name}\"",
            Columns = [.. columns.Select(c => new RestColumn((string)c[0], (Type)c[1], (bool)c[2]))],
            Keys = ["id"],
            Writable = true,
        };
}
