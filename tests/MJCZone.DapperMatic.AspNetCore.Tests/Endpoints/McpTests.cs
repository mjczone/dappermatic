// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using System.Text.Json;
using Dapper;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MJCZone.DapperMatic.AspNetCore.Factories;
using MJCZone.DapperMatic.AspNetCore.Tests.Factories;
using MJCZone.DapperMatic.AspNetCore.Tests.Infrastructure;
using MJCZone.DapperMatic.Models;
using MJCZone.DapperMatic.Security;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Endpoints;

/// <summary>
/// DapperMatic's tools for AI assistants, over the Model Context Protocol, run as the signed-in caller.
/// </summary>
public class McpTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private readonly TestcontainersAssemblyFixture _fixture;

    public McpTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Assistants_use_the_tools_as_the_signed_in_caller_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        using var factory = new WafWithInMemoryDatasourceRepository([datasource]).WithWebHostBuilder(web =>
            web.ConfigureServices(services =>
            {
                services.Configure<DapperMaticOptions>(options =>
                {
                    options.Policies.Clear();
                    options.Endpoints |= DapperMaticEndpoints.Mcp;
                });
                services.AddDapperMatic(dm =>
                    dm.Tenancy(t => t.FromClaim("tenant_id"))
                        .Policies(
                            DmPolicy.Allow(DmAction.Read | DmAction.Create, "clerk").OnTables("mcp_items").Where("tenant_id eq @tenant.id").Preset("tenant_id", "@tenant.id"),
                            DmPolicy.Allow(DmAction.ReadSchema, "clerk").OnDatasources(datasource.Id!),
                            DmPolicy.Allow(DmAction.All, "admin")
                        )
                );
            })
        );
        using (var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!))
        {
            db.Open();
            await db.DropTableIfExistsAsync(null, "mcp_items");
            await db.CreateTableIfNotExistsAsync(
                new DmTable(null, "mcp_items", [new DmColumn("id", typeof(int), isPrimaryKey: true, isAutoIncrement: true), new DmColumn("tenant_id", typeof(int)), new DmColumn("name", typeof(string), length: 20)])
            );
            await db.ExecuteAsync("INSERT INTO mcp_items (tenant_id, name) VALUES (2, 'theirs')");
        }

        using var http = factory.CreateAuthenticatedClient(new Claim(ClaimTypes.Role, "clerk"), new Claim("tenant_id", "1"));
        await using var client = await McpClient.CreateAsync(
            new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(http.BaseAddress!, "/api/dm/mcp") }, http, ownsHttpClient: false)
        );

        (await client.ListToolsAsync()).Select(t => t.Name).Should().BeEquivalentTo(
            "list_datasources", "describe_datasource", "query_rows", "insert_rows", "update_rows", "delete_rows", "run_function"
        );

        async Task<(bool Error, string Text)> Call(string tool, Dictionary<string, object?> arguments)
        {
            var result = await client.CallToolAsync(tool, arguments);
            return (result.IsError == true, string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text)));
        }

        var (error, text) = await Call("list_datasources", []);
        error.Should().BeFalse(text);
        JsonDocument.Parse(text).RootElement.EnumerateArray().Select(d => d.GetProperty("id").GetString()).Should().Equal(datasource.Id);

        // The assistant learns what this caller may do: read and insert, not update or delete.
        (error, text) = await Call("describe_datasource", new() { ["datasourceId"] = datasource.Id });
        error.Should().BeFalse(text);
        JsonDocument.Parse(text).RootElement.GetProperty("paths").EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("/t/mcp_items/query", "/t/mcp_items/rows");

        // Its writes and reads are the caller's: the tenant set, and only the tenant's rows read.
        (error, text) = await Call("insert_rows", new() { ["datasourceId"] = datasource.Id, ["table"] = "mcp_items", ["rows"] = new[] { new Dictionary<string, object> { ["name"] = "mine" } } });
        error.Should().BeFalse(text);
        (error, text) = await Call("query_rows", new() { ["datasourceId"] = datasource.Id, ["table"] = "mcp_items", ["orderby"] = "id" });
        error.Should().BeFalse(text);
        JsonDocument.Parse(text).RootElement.GetProperty("result").EnumerateArray().Select(r => $"{r.GetProperty("name").GetString()}:{r.GetProperty("tenant_id").GetInt32()}").Should().Equal("mine:1");

        // What the policies refuse is a tool error, said plainly.
        (error, text) = await Call("delete_rows", new() { ["datasourceId"] = datasource.Id, ["table"] = "mcp_items", ["filter"] = "true" });
        error.Should().BeTrue();
        text.Should().Contain("Forbidden");
        (error, text) = await Call("query_rows", new() { ["datasourceId"] = datasource.Id, ["table"] = "mcp_items", ["filter"] = "nope eq 1" });
        error.Should().BeTrue();
        text.Should().Contain("nope");

        using var anonymous = factory.CreateClient();
        await using var nobody = await McpClient.CreateAsync(
            new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(anonymous.BaseAddress!, "/api/dm/mcp") }, anonymous, ownsHttpClient: false)
        );
        (await nobody.CallToolAsync("query_rows", new Dictionary<string, object?> { ["datasourceId"] = datasource.Id, ["table"] = "mcp_items" })).IsError.Should().BeTrue();
    }

    [Fact]
    public async Task The_tools_are_served_only_when_turned_on_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);

        // Off by default: no endpoint, and an MCP server of the application's own does not get the tools.
        using var off = new WafWithInMemoryDatasourceRepository([datasource]);
        using (var client = off.CreateClient())
        {
            (await client.PostAsync("/api/dm/mcp", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"))).StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
        }

        var options = off.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<ModelContextProtocol.Server.McpServerOptions>>().Value;
        (options.ToolCollection?.Count ?? 0).Should().Be(0);

        // Every set, All included, turns it on.
        using var all = off.WithWebHostBuilder(web => web.ConfigureServices(s => s.Configure<DapperMaticOptions>(o => o.Endpoints = DapperMaticEndpoints.All)));
        all.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<ModelContextProtocol.Server.McpServerOptions>>().Value.ToolCollection!.Count.Should().Be(7);
    }

    // Each tool call runs as the caller of the request making it: the stateful mode running calls in the execution
    // context of the request that opened the session is refused when the endpoints are mapped.
    [Fact]
    public async Task A_per_session_execution_context_is_refused_Async()
    {
        var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder();
        Microsoft.AspNetCore.TestHost.WebHostBuilderExtensions.UseTestServer(builder.WebHost);
        builder.Services.AddDapperMatic(dm => dm.Policy(DmPolicy.Allow(DmAction.All, "*")));
        builder.Services.Configure<DapperMaticOptions>(o => o.Endpoints = DapperMaticEndpoints.All);
#pragma warning disable MCP9006
        builder.Services.Configure<ModelContextProtocol.AspNetCore.HttpServerTransportOptions>(o => o.PerSessionExecutionContext = true);
#pragma warning restore MCP9006
        await using var app = builder.Build();
        FluentActions.Invoking(() => app.UseDapperMatic()).Should().Throw<InvalidOperationException>().WithMessage("*PerSessionExecutionContext*");
    }
}
