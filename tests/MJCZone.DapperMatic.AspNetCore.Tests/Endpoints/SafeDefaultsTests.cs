// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MJCZone.DapperMatic.AspNetCore.Factories;
using MJCZone.DapperMatic.AspNetCore.Repositories;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.AspNetCore.Tests.Factories;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Endpoints;

/// <summary>
/// What the package does without being told: errors that keep the database's text to themselves, limits on writes,
/// only the endpoints asked for, connection strings checked, and table descriptions cached until DapperMatic changes them.
/// </summary>
public class SafeDefaultsTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private readonly TestcontainersAssemblyFixture _fixture;

    public SafeDefaultsTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Database_errors_keep_their_text_unless_details_are_included_Async()
    {
        var (factory, baseUrl) = await SqliteAsync("safe_errors", options => { });
        using (factory)
        {
            using var client = factory.CreateClient();
            (await PostAsync(client, $"{baseUrl}/safe_errors/rows", new { rows = new[] { new { id = 1, name = "a" } } })).StatusCode.Should().Be(HttpStatusCode.OK);

            var duplicate = await PostAsync(client, $"{baseUrl}/safe_errors/rows", new { rows = new[] { new { id = 1, name = "b" } } });
            duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
            var problem = JsonDocument.Parse(await duplicate.Content.ReadAsStringAsync()).RootElement;
            problem.GetProperty("code").GetString().Should().Be("unique_violation");
            problem.GetProperty("detail").GetString().Should().NotContain("safe_errors").And.NotContain("UNIQUE");
            problem.TryGetProperty("traceId", out _).Should().BeTrue();
        }

        var (detailed, detailedUrl) = await SqliteAsync("safe_errors", options => options.IncludeErrorDetails = true);
        using (detailed)
        {
            using var client = detailed.CreateClient();
            await PostAsync(client, $"{detailedUrl}/safe_errors/rows", new { rows = new[] { new { id = 1, name = "a" } } });
            var duplicate = await PostAsync(client, $"{detailedUrl}/safe_errors/rows", new { rows = new[] { new { id = 1, name = "b" } } });
            duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await duplicate.Content.ReadAsStringAsync()).Should().Contain("UNIQUE");
        }
    }

    [Fact]
    public async Task Writes_are_limited_and_can_be_dry_runs_Async()
    {
        var (factory, baseUrl) = await SqliteAsync(
            "safe_limits",
            options =>
            {
                options.QueryLimits.MaxInsertRows = 2;
                options.QueryLimits.MaxAffectedRows = 1;
            }
        );
        using (factory)
        {
            using var client = factory.CreateClient();
            object Rows(params int[] ids) => new { rows = ids.Select(id => new { id, name = $"n{id}" }).ToArray() };

            (await PostAsync(client, $"{baseUrl}/safe_limits/rows", Rows(1, 2, 3))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await AffectedAsync(client, $"{baseUrl}/safe_limits/rows", Rows(1, 2))).Should().Be(2);

            // A dry run reports what it would change, and changes nothing.
            (await AffectedAsync(client, $"{baseUrl}/safe_limits/rows/update", new { set = new { name = "x" }, filter = "id eq 1", dryRun = true })).Should().Be(1);
            (await AffectedAsync(client, $"{baseUrl}/safe_limits/rows", new { rows = new[] { new { id = 3, name = "n3" } }, dryRun = true })).Should().Be(1);

            // More rows than allowed: refused, and rolled back.
            var tooMany = await PostAsync(client, $"{baseUrl}/safe_limits/rows/delete", new { filter = "true" });
            tooMany.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await AffectedAsync(client, $"{baseUrl}/safe_limits/rows/delete", new { filter = "true", dryRun = true }))
                .Should()
                .Be(-1, "a dry run is limited too");

            var rows = await client.GetAsync($"{baseUrl}/safe_limits/query?$orderby=id");
            var names = JsonDocument.Parse(await rows.Content.ReadAsStringAsync()).RootElement.GetProperty("result").EnumerateArray().Select(r => r.GetProperty("name").GetString());
            names.Should().Equal("n1", "n2");
        }
    }

    [Fact]
    public async Task Only_the_endpoint_sets_asked_for_are_mapped_Async()
    {
        var (factory, baseUrl) = await SqliteAsync("safe_sets", options => options.Endpoints = DapperMaticEndpoints.Query);
        using (factory)
        {
            using var client = factory.CreateClient();
            (await client.GetAsync("/api/dm/d/")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await client.GetAsync($"{baseUrl}/safe_sets")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await client.GetAsync($"{baseUrl}/safe_sets/query")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await PostAsync(client, $"{baseUrl}/safe_sets/rows", new { rows = new[] { new { id = 1 } } })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }

    [Fact]
    public async Task Conventions_reach_every_DapperMatic_endpoint_and_anonymous_callers_get_401_Async()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDapperMatic(dm => dm.UseCustomPermissions(_ => new DenyAll()));

        await using var app = builder.Build();
        app.UseDapperMatic(group =>
            group.AddEndpointFilter(
                async (context, next) =>
                {
                    context.HttpContext.Response.Headers["X-DapperMatic-Group"] = "yes";
                    return await next(context);
                }
            )
        );
        await app.StartAsync();

        var response = await app.GetTestClient().GetAsync("/api/dm/d/");
        response.Headers.GetValues("X-DapperMatic-Group").Should().Equal("yes");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Datasources_added_through_the_API_follow_the_connection_policy_Async()
    {
        using var factory = new WafWithInMemoryDatasourceRepository([]);
        using var client = factory.CreateClient();
        object Datasource(string id, string provider, string connectionString) =>
            new { id, provider, connectionString, displayName = id };

        (await PostAsync(client, "/api/dm/d", Datasource("policy-mysql", "MySql", "Server=db;Database=x;AllowLoadLocalInfile=true")))
            .StatusCode.Should()
            .Be(HttpStatusCode.BadRequest);
        (await PostAsync(client, "/api/dm/d", Datasource("policy-escape", "Sqlite", "Data Source=../../escape.db")))
            .StatusCode.Should()
            .Be(HttpStatusCode.BadRequest);
        (await PostAsync(client, "/api/dm/d", Datasource("policy-attach", "SqlServer", "Server=db;AttachDbFilename=C:\\x.mdf")))
            .StatusCode.Should()
            .Be(HttpStatusCode.BadRequest);

        // A relative SQLite path is the allowed folder's.
        (await PostAsync(client, "/api/dm/d", Datasource("policy-file", "Sqlite", "Data Source=policy-file.db")))
            .IsSuccessStatusCode.Should()
            .BeTrue();
        var stored = await factory.Services.GetRequiredService<IDapperMaticDatasourceRepository>().GetConnectionStringAsync("policy-file");
        stored.Should().Contain(Path.Combine(Path.GetTempPath(), "policy-file.db"));
    }

    [Fact]
    public async Task Table_descriptions_are_cached_until_DapperMatic_changes_the_schema_Async()
    {
        var (factory, baseUrl) = await SqliteAsync("safe_cache", options => { });
        using (factory)
        {
            using var client = factory.CreateClient();
            async Task<string[]> Fields()
            {
                var response = await client.GetAsync($"{baseUrl}/safe_cache/query");
                var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
                return [.. json.GetProperty("fields").EnumerateArray().Select(f => f.GetProperty("name").GetString()!)];
            }

            (await Fields()).Should().Equal("id", "name");

            // Changed elsewhere: the cached description stands.
            var datasource = Sqlite();
            using (var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!))
            {
                db.Open();
                await db.CreateColumnIfNotExistsAsync(new DmColumn(null, "safe_cache", "note", typeof(string), length: 20, isNullable: true));
            }

            (await Fields()).Should().Equal("id", "name");

            // Any schema change through DapperMatic forgets the datasource's descriptions.
            (await client.DeleteAsync($"{baseUrl}/safe_cache_other")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            var created = await PostAsync(
                client,
                baseUrl,
                new { tableName = "safe_cache_other", columns = new[] { new { columnName = "id", providerDataType = "integer", isPrimaryKey = true } } }
            );
            created.IsSuccessStatusCode.Should().BeTrue(await created.Content.ReadAsStringAsync());
            (await Fields()).Should().Equal("id", "name", "note");
        }
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string url, object body) =>
        client.PostAsync(url, new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));

    private static async Task<int> AffectedAsync(HttpClient client, string url, object body)
    {
        var response = await PostAsync(client, url, body);
        return response.IsSuccessStatusCode
            ? JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("affected").GetInt32()
            : -1;
    }

    private Models.Dtos.DatasourceDto Sqlite() =>
        _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);

    // A host with the SQLite datasource and a fresh table (id, unique name), with the options changed as given.
    private async Task<(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Factory, string BaseUrl)> SqliteAsync(
        string table,
        Action<DapperMaticOptions> configure
    )
    {
        var datasource = Sqlite();
        var factory = new WafWithInMemoryDatasourceRepository([datasource]).WithWebHostBuilder(web =>
            web.ConfigureServices(services => services.Configure(configure))
        );

        using (var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!))
        {
            db.Open();
            await db.DropTableIfExistsAsync(null, table);
            await db.DropTableIfExistsAsync(null, table + "_other");
            await db.CreateTableIfNotExistsAsync(
                new DmTable(
                    null,
                    table,
                    [new DmColumn("id", typeof(int), isPrimaryKey: true), new DmColumn("name", typeof(string), length: 20, isNullable: true, isUnique: true)]
                )
            );
        }

        return (factory, $"/api/dm/d/{datasource.Id}/t");
    }

    private sealed class DenyAll : IDapperMaticPermissions
    {
        public Task<bool> IsAuthorizedAsync(DapperMaticOperation context) => Task.FromResult(false);
    }
}
