// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Dapper;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MJCZone.DapperMatic.AspNetCore.Factories;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Tests.GraphQL;

/// <summary>
/// Each caller's own schema and the policies at run time; the limits on operations; GraphQL over HTTP.
/// </summary>
public class GraphQLAccessTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private readonly TestcontainersAssemblyFixture _fixture;

    public GraphQLAccessTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Each_caller_has_a_schema_of_what_they_may_use_and_the_policies_hold_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        using var factory = GraphQLTestHost.Create(
            datasource,
            dm => dm.Policies(
                DmPolicy.Allow(DmAction.Read, "clerk").OnTables("gq_orders").OnlyColumns("id", "status", "customer_id").Where("status ne 'secret'"),
                DmPolicy.Allow(DmAction.Create, "clerk").OnTables("gq_orders").OnlyColumns("total").Preset("status", "draft"),
                DmPolicy.Allow(DmAction.All, "admin")
            )
        );
        await GraphQLTestHost.CreateTablesAsync(factory, datasource, null);
        using (var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!))
        {
            await db.ExecuteAsync("INSERT INTO gq_orders (total, status) VALUES (1, 'open'), (2, 'secret')");
        }

        var path = $"/api/dm/d/{datasource.Id}/graphql";
        using var clerk = GraphQLTestHost.Client(factory, "clerk");
        using var admin = GraphQLTestHost.Client(factory, "admin");
        static List<string> Names(JsonElement type, string list) => [.. type.GetProperty(list).EnumerateArray().Select(f => f.GetProperty("name").GetString()!)];
        const string Types = """
            {
              order: __type(name: "gq_orders") { fields { name } }
              insert: __type(name: "gq_orders_insert") { inputFields { name } }
              customers: __type(name: "gq_customers") { name }
              mutation: __schema { mutationType { fields { name } } }
            }
            """;

        // The clerk's schema: the columns they may read; inserts of what they may write, the preset left out; nothing else.
        var mine = (await GraphQLTestHost.PostAsync(clerk, path, Types)).Data();
        Names(mine.GetProperty("order"), "fields").Should().BeEquivalentTo("nodeId", "id", "status", "customer_id");
        Names(mine.GetProperty("insert"), "inputFields").Should().BeEquivalentTo("total");
        mine.GetProperty("customers").ValueKind.Should().Be(JsonValueKind.Null);
        Names(mine.GetProperty("mutation").GetProperty("mutationType"), "fields").Should().BeEquivalentTo("insert_gq_orders");
        var theirs = (await GraphQLTestHost.PostAsync(admin, path, Types)).Data();
        Names(theirs.GetProperty("order"), "fields").Should().Contain(["total", "customer", "placed"]);

        // An operation that does not fit the caller's schema is refused whole (422), as it would be for a narrower role.
        var hidden = await GraphQLTestHost.PostAsync(clerk, path, "{ gq_orders { id total } }");
        hidden.Status.Should().Be(HttpStatusCode.UnprocessableEntity);

        // Filters at run time; presets on insert.
        var rows = (await GraphQLTestHost.PostAsync(clerk, path, "{ gq_orders { status } }")).Data().GetProperty("gq_orders");
        rows.EnumerateArray().Select(r => r.GetProperty("status").GetString()).Should().Equal("open");
        var inserted = (await GraphQLTestHost.PostAsync(clerk, path, "mutation { insert_gq_orders(rows: [{ total: 3 }]) { affected rows { status } } }")).Data();
        inserted.GetProperty("insert_gq_orders").GetProperty("rows")[0].GetProperty("status").GetString().Should().Be("draft");
    }

    // Outside Development an error says what kind of failure it was, as REST's problem details do, and no more: not the
    // database's message, nor (as GraphQL.NET would write a code for each exception an error holds) its driver.
    [Fact]
    public async Task Errors_outside_development_do_not_name_the_database_or_its_driver_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        using var factory = GraphQLTestHost.Create(datasource).WithWebHostBuilder(web => web.UseEnvironment("Production"));
        await GraphQLTestHost.CreateTablesAsync(factory, datasource, null);
        using var admin = GraphQLTestHost.Client(factory);
        var path = $"/api/dm/d/{datasource.Id}/graphql";
        const string Insert = """mutation { insert_gq_orders(rows: [{ id: 1, total: 1, status: "open" }]) { affected } }""";
        (await GraphQLTestHost.PostAsync(admin, path, Insert)).Data();

        var conflict = await GraphQLTestHost.PostAsync(admin, path, Insert);
        var extensions = conflict.Body.GetProperty("errors")[0].GetProperty("extensions");
        extensions.GetProperty("code").GetString().Should().Be("UNIQUE_VIOLATION");
        extensions.ToString().Should().NotContainAny("SQLITE", "Sqlite", "UNIQUE constraint");
        conflict.Body.GetProperty("errors")[0].GetProperty("message").GetString().Should().NotContain("UNIQUE constraint");
    }

    [Fact]
    public async Task Operations_are_held_to_the_limits_and_GraphQL_over_HTTP_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        using var factory = GraphQLTestHost.Create(datasource, graphql: g => (g.MaxDepth, g.MaxRootFields) = (4, 2));
        await GraphQLTestHost.CreateTablesAsync(factory, datasource, null);
        var path = $"/api/dm/d/{datasource.Id}/graphql";
        using var client = GraphQLTestHost.Client(factory);

        // Too deep, too many root fields.
        (await GraphQLTestHost.PostAsync(client, path, "{ gq_customers { orders { customer { orders { customer { name } } } } } }")).Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await GraphQLTestHost.PostAsync(client, path, "{ a: gq_orders { id } b: gq_orders { id } c: gq_orders { id } }")).Status.Should().Be(HttpStatusCode.UnprocessableEntity);

        // The schema itself, as code generators ask for it, is not held to the depth limit.
        var introspection = await GraphQLTestHost.PostAsync(client, path, IntrospectionQuery);
        introspection.Data().GetProperty("__schema").GetProperty("types").GetArrayLength().Should().BeGreaterThan(10);

        // GET for queries, with a header no form can send; a mutation over GET is 405.
        async Task<HttpResponseMessage> GetAsync(string query, bool preflight = true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{path}?query={Uri.EscapeDataString(query)}");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(GraphQLTestHost.GraphQLResponse));
            if (preflight)
            {
                request.Headers.Add("GraphQL-Require-Preflight", "1");
            }

            return await client.SendAsync(request);
        }

        (await GetAsync("{ gq_orders { id } }")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetAsync("{ gq_orders { id } }", preflight: false)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var mutation = await GetAsync("mutation { delete_gq_orders(filter: \"true\") { affected } }");
        mutation.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        mutation.Content.Headers.Allow.Should().Contain("POST", mutation.Headers.ToString() + mutation.Content.Headers.ToString() + await mutation.Content.ReadAsStringAsync());

        // application/json: errors in the body, 200.
        (await GraphQLTestHost.PostAsync(client, path, "{ gq_orders { nosuch } }", accept: "application/json")).Status.Should().Be(HttpStatusCode.OK);

        // Introspection turned off.
        using var closed = GraphQLTestHost.Create(datasource, graphql: g => g.Introspection = false);
        using var closedClient = GraphQLTestHost.Client(closed);
        (await GraphQLTestHost.PostAsync(closedClient, path, "{ __schema { types { name } } }")).Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await GraphQLTestHost.PostAsync(closedClient, path, "{ gq_orders { id } }")).Data();
    }

    // The query graphql-js's getIntrospectionQuery() makes, as Apollo, Relay and graphql-codegen send it.
    private const string IntrospectionQuery = """
        query IntrospectionQuery {
          __schema {
            queryType { name } mutationType { name } subscriptionType { name }
            types { ...FullType }
            directives { name description locations args { ...InputValue } }
          }
        }
        fragment FullType on __Type {
          kind name description
          fields(includeDeprecated: true) { name description args { ...InputValue } type { ...TypeRef } isDeprecated deprecationReason }
          inputFields { ...InputValue }
          interfaces { ...TypeRef }
          enumValues(includeDeprecated: true) { name description isDeprecated deprecationReason }
          possibleTypes { ...TypeRef }
        }
        fragment InputValue on __InputValue { name description type { ...TypeRef } defaultValue }
        fragment TypeRef on __Type {
          kind name
          ofType { kind name ofType { kind name ofType { kind name ofType { kind name ofType { kind name ofType { kind name ofType { kind name } } } } } } }
        }
        """;
}
