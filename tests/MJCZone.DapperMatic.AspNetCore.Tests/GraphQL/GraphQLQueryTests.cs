// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;
using Dapper;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MJCZone.DapperMatic.AspNetCore.Factories;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Tests.GraphQL;

/// <summary>
/// Reading through GraphQL: lists with conditions, orders and pages; relations (one relation twice, under aliases);
/// Relay connections paged by cursor; rows by key and by global id.
/// </summary>
public class GraphQLQueryTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private readonly TestcontainersAssemblyFixture _fixture;

    public GraphQLQueryTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite, null)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql, "public")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer, "dbo")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql, null)]
    public async Task Queries_follow_relations_and_page_by_cursor_Async(string datasourceId, string? schema)
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == datasourceId);
        using var factory = GraphQLTestHost.Create(datasource);
        await GraphQLTestHost.CreateTablesAsync(factory, datasource, schema);
        using (var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!))
        {
            db.Open();
            await db.ExecuteAsync("INSERT INTO gq_customers (name, country) VALUES ('Acme', 'FR'), ('Beta', 'DE')");
            await db.ExecuteAsync(
                "INSERT INTO gq_orders (customer_id, total, status) VALUES (1, 10.50, 'open'), (1, 99.00, 'open'), (2, 75.25, 'shipped'), (2, 5.00, 'open'), (NULL, 1.00, 'open')"
            );
        }

        using var client = GraphQLTestHost.Client(factory);
        var path = $"/api/dm/d/{datasourceId}/graphql";
        var data = (await GraphQLTestHost.PostAsync(
            client,
            path,
            """
            query Orders($status: String) {
              open: gq_orders(where: { status: { eq: $status }, total: { gte: 5 } }, orderBy: [{ total: DESC }]) {
                id
                total
                customer { name }
                ...Ids
              }
              gq_customers(orderBy: [{ name: ASC }]) {
                name
                big: orders(where: { total: { gt: 50 } }) { total }
                all: orders(orderBy: [{ id: ASC }], first: 2) { id }
              }
            }
            fragment Ids on gq_orders { nodeId }
            """,
            new { status = "open" }
        )).Data();

        var open = data.GetProperty("open").EnumerateArray().ToList();
        open.Select(o => o.GetProperty("total").GetDecimal()).Should().Equal(99.00m, 10.50m, 5.00m);
        open.Select(o => o.GetProperty("customer").GetProperty("name").GetString()).Should().Equal("Acme", "Acme", "Beta");
        open.Should().OnlyContain(o => o.GetProperty("nodeId").GetString()!.Length > 0);

        var customers = data.GetProperty("gq_customers").EnumerateArray().ToList();
        customers.Select(c => c.GetProperty("name").GetString()).Should().Equal("Acme", "Beta");
        customers[0].GetProperty("big").EnumerateArray().Select(o => o.GetProperty("total").GetDecimal()).Should().Equal(99.00m);
        customers[1].GetProperty("big").EnumerateArray().Select(o => o.GetProperty("total").GetDecimal()).Should().Equal(75.25m);
        customers[0].GetProperty("all").GetArrayLength().Should().Be(2);

        // A connection, two rows a page, by cursor to the end.
        const string Page = """
            query Page($after: String) {
              gq_orders_connection(first: 2, after: $after, orderBy: [{ id: ASC }]) {
                totalCount
                pageInfo { hasNextPage hasPreviousPage endCursor }
                edges { cursor node { id nodeId } }
              }
            }
            """;
        var seen = new List<int>();
        string? after = null;
        for (var page = 0; page < 5; page++)
        {
            var connection = (await GraphQLTestHost.PostAsync(client, path, Page, new { after })).Data().GetProperty("gq_orders_connection");
            connection.GetProperty("totalCount").GetInt32().Should().Be(5);
            var edges = connection.GetProperty("edges").EnumerateArray().ToList();
            seen.AddRange(edges.Select(e => e.GetProperty("node").GetProperty("id").GetInt32()));
            var info = connection.GetProperty("pageInfo");
            info.GetProperty("hasPreviousPage").GetBoolean().Should().BeFalse();
            if (edges.Count > 0)
            {
                info.GetProperty("endCursor").GetString().Should().Be(edges[^1].GetProperty("cursor").GetString());
            }

            if (!info.GetProperty("hasNextPage").GetBoolean())
            {
                break;
            }

            after = info.GetProperty("endCursor").GetString();
        }

        seen.Should().Equal(1, 2, 3, 4, 5);

        // A row by its key, and by its global id: a Node, of its own type.
        var byKey = (await GraphQLTestHost.PostAsync(client, path, "{ gq_orders_by_key(id: 3) { id status nodeId } }")).Data().GetProperty("gq_orders_by_key");
        byKey.GetProperty("status").GetString().Should().Be("shipped");
        var node = (await GraphQLTestHost.PostAsync(
            client,
            path,
            "query Node($id: ID!) { node(id: $id) { __typename nodeId ... on gq_orders { id status } } }",
            new { id = byKey.GetProperty("nodeId").GetString() }
        )).Data().GetProperty("node");
        node.GetProperty("__typename").GetString().Should().Be("gq_orders");
        node.GetProperty("id").GetInt32().Should().Be(3);
        (await GraphQLTestHost.PostAsync(client, path, "{ node(id: \"bm90IGFuIGlk\") { nodeId } }")).Data().GetProperty("node").ValueKind.Should().Be(JsonValueKind.Null);

        // The same condition as OData text, ANDed with where.
        var filtered = (await GraphQLTestHost.PostAsync(client, path, "{ gq_orders(filter: \"total gt 50\", where: { status: { eq: \"open\" } }) { id } }")).Data();
        filtered.GetProperty("gq_orders").EnumerateArray().Select(o => o.GetProperty("id").GetInt32()).Should().Equal(2);

        // A field the schema does not have is refused before anything runs: 422 with graphql-response+json.
        var unknown = await GraphQLTestHost.PostAsync(client, path, "{ gq_orders { nosuch } }");
        unknown.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        var broken = await GraphQLTestHost.PostAsync(client, path, "{ gq_orders { id ");
        broken.Status.Should().Be(HttpStatusCode.BadRequest);
    }

    // What the walker builds is what GraphQL resolves, whatever the shape: two nodes of a connection giving a relation
    // of one name other arguments; an alias that is a column's name; counts below zero; rows read for their relations
    // when their key is hidden.
    [Fact]
    public async Task Queries_of_any_shape_read_what_each_field_asks_for_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        using var factory = GraphQLTestHost.Create(
            datasource,
            dm => dm.Policies(
                DmPolicy.Allow(DmAction.Read, "clerk").OnTables("gq_orders").OnlyColumns("status", "customer_id"),
                DmPolicy.Allow(DmAction.Read, "clerk").OnTables("gq_customers"),
                DmPolicy.Allow(DmAction.All, "admin")
            )
        );
        await GraphQLTestHost.CreateTablesAsync(factory, datasource, null);
        using (var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!))
        {
            await db.ExecuteAsync("INSERT INTO gq_customers (name) VALUES ('Ann')");
            await db.ExecuteAsync("INSERT INTO gq_orders (customer_id, total, status) VALUES (1, 1, 'open'), (1, 2, 'shipped')");
        }

        var path = $"/api/dm/d/{datasource.Id}/graphql";
        using var admin = GraphQLTestHost.Client(factory);
        static List<string?> Statuses(JsonElement rows) => [.. rows.EnumerateArray().Select(r => r.GetProperty("status").GetString())];

        // Two nodes of one edge, each with its own arguments to the same relation.
        var nodes = (await GraphQLTestHost.PostAsync(
                admin,
                path,
                """
                { gq_customers_connection(first: 5) { edges {
                    a: node { orders(where: { status: { eq: "open" } }) { status } }
                    b: node { orders(where: { status: { eq: "shipped" } }) { status } } } } }
                """
            ))
            .Data().GetProperty("gq_customers_connection").GetProperty("edges")[0];
        Statuses(nodes.GetProperty("a").GetProperty("orders")).Should().Equal("open");
        Statuses(nodes.GetProperty("b").GetProperty("orders")).Should().Equal("shipped");

        // A relation under a column's name: the column the engine reads to join it, and the key nodeId is made of.
        var aliased = (await GraphQLTestHost.PostAsync(
                admin,
                path,
                """
                { a: gq_orders_connection(first: 1) { edges { cursor node { nodeId customer_id: customer { name } id: customer { name } } } }
                  b: gq_orders_connection(first: 1) { edges { cursor node { nodeId } } } }
                """
            ))
            .Data();
        var node = aliased.GetProperty("a").GetProperty("edges")[0];
        node.GetProperty("node").GetProperty("customer_id").GetProperty("name").GetString().Should().Be("Ann");
        node.GetProperty("node").GetProperty("id").GetProperty("name").GetString().Should().Be("Ann");
        var plain = aliased.GetProperty("b").GetProperty("edges")[0];
        node.GetProperty("node").GetProperty("nodeId").GetString().Should().Be(plain.GetProperty("node").GetProperty("nodeId").GetString());
        node.GetProperty("cursor").GetString().Should().Be(plain.GetProperty("cursor").GetString());

        // A count of rows is 0 or more: below that, SQLite would read every row.
        (await GraphQLTestHost.PostAsync(admin, path, "{ gq_orders(first: -1) { status } }")).Codes().Should().Equal("BAD_USER_INPUT");
        (await GraphQLTestHost.PostAsync(admin, path, "{ gq_orders(skip: -1) { status } }")).Codes().Should().Equal("BAD_USER_INPUT");

        // A caller who may not read the key reads rows for their relations, or their type, all the same.
        using var clerk = GraphQLTestHost.Client(factory, "clerk");
        var rows = (await GraphQLTestHost.PostAsync(clerk, path, "{ gq_orders { __typename customer { name } } }")).Data().GetProperty("gq_orders");
        rows.EnumerateArray().Select(r => r.GetProperty("customer").GetProperty("name").GetString()).Should().Equal("Ann", "Ann");
    }

    // A list or connection that asks for no count reads MaxTop rows at most; a connection says more follow.
    [Fact]
    public async Task Lists_without_first_read_at_most_MaxTop_rows_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        using var factory = GraphQLTestHost.Create(datasource, options: o => o.QueryLimits.MaxTop = 2);
        await GraphQLTestHost.CreateTablesAsync(factory, datasource, null);
        using (var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!))
        {
            await db.ExecuteAsync("INSERT INTO gq_orders (total, status) VALUES (1, 'a'), (2, 'b'), (3, 'c')");
        }

        using var admin = GraphQLTestHost.Client(factory);
        var data = (await GraphQLTestHost.PostAsync(
                admin,
                $"/api/dm/d/{datasource.Id}/graphql",
                "{ gq_orders(orderBy: [{ id: ASC }]) { status } gq_orders_connection { edges { node { status } } pageInfo { hasNextPage } } }"
            ))
            .Data();
        data.GetProperty("gq_orders").EnumerateArray().Select(o => o.GetProperty("status").GetString()).Should().Equal("a", "b");
        data.GetProperty("gq_orders_connection").GetProperty("edges").GetArrayLength().Should().Be(2);
        data.GetProperty("gq_orders_connection").GetProperty("pageInfo").GetProperty("hasNextPage").GetBoolean().Should().BeTrue();
    }
}
