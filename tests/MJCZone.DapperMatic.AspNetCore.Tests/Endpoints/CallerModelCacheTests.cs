// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MJCZone.DapperMatic.AspNetCore.Caching;
using MJCZone.DapperMatic.AspNetCore.Factories;
using MJCZone.DapperMatic.AspNetCore.Tests.Factories;
using MJCZone.DapperMatic.Models;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Endpoints;

/// <summary>
/// What each caller may use of a datasource is kept, shared by callers alike in everything the policies read, and built
/// again when what decides it changes.
/// </summary>
public class CallerModelCacheTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private readonly TestcontainersAssemblyFixture _fixture;

    public CallerModelCacheTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Callers_alike_share_one_kept_model_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        using var factory = Host(
            datasource,
            DmPolicy.Allow(DmAction.Read, "clerk").OnTables("cm_orders"),
            DmPolicy.Allow(DmAction.Read).WithClaim("plan", "pro").OnTables("cm_reports"),
            DmPolicy.Allow(DmAction.All, "admin")
        );
        await CreateTablesAsync(factory, datasource);
        var cache = factory.Services.GetRequiredService<DapperMaticCallerModelCache>();
        cache.Clear();

        (await TablesAsync(factory, datasource.Id!, "ann", "clerk")).Should().Equal("cm_orders");
        (await TablesAsync(factory, datasource.Id!, "bob", "clerk")).Should().Equal("cm_orders");
        cache.Count.Should().Be(1);

        // A claim a policy names, or another role, is another model.
        (await TablesAsync(factory, datasource.Id!, "cy", "clerk", plan: "pro")).Should().Equal("cm_orders", "cm_reports");
        (await TablesAsync(factory, datasource.Id!, "di", "admin")).Should().Equal("cm_orders", "cm_reports");
        cache.Count.Should().Be(3);

        // A change to the schema through DapperMatic makes the models stale.
        factory.Services.GetRequiredService<DapperMaticSchemaCache>().Invalidate(datasource.Id!);
        (await TablesAsync(factory, datasource.Id!, "ann", "clerk")).Should().Equal("cm_orders");
        cache.Count.Should().Be(4);
    }

    // Code DapperMatic cannot read decides what callers may do: their models are kept only by a key the application gives.
    [Fact]
    public async Task Models_decided_in_code_are_kept_only_by_the_applications_key_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        var predicate = DmPolicy.Allow(DmAction.Read).OnTables("cm_orders").When(c => c.User.Identity?.Name == "ann");
        using var unkeyed = Host(datasource, predicate);
        await CreateTablesAsync(unkeyed, datasource);
        var cache = unkeyed.Services.GetRequiredService<DapperMaticCallerModelCache>();
        (await TablesAsync(unkeyed, datasource.Id!, "ann", "clerk")).Should().Equal("cm_orders");
        (await TablesAsync(unkeyed, datasource.Id!, "bob", "clerk")).Should().BeEmpty();
        cache.Count.Should().Be(0);

        using var keyed = Host(datasource, predicate).WithWebHostBuilder(web =>
            web.ConfigureServices(services => services.Configure<DapperMaticOptions>(o => o.CallerModelCacheKey = op => op.User?.Identity?.Name))
        );
        cache = keyed.Services.GetRequiredService<DapperMaticCallerModelCache>();
        (await TablesAsync(keyed, datasource.Id!, "ann", "clerk")).Should().Equal("cm_orders");
        (await TablesAsync(keyed, datasource.Id!, "bob", "clerk")).Should().BeEmpty();
        (await TablesAsync(keyed, datasource.Id!, "ann", "admin")).Should().Equal("cm_orders");
        cache.Count.Should().Be(3);
    }

    private static WebApplicationFactory<Program> Host(MJCZone.DapperMatic.AspNetCore.Models.Dtos.DatasourceDto datasource, params DmPolicy[] policies) =>
        new WafWithInMemoryDatasourceRepository([datasource]).WithWebHostBuilder(web =>
            web.ConfigureServices(services =>
            {
                services.Configure<DapperMaticOptions>(options => options.Policies.Clear());
                services.AddDapperMatic(dm => dm.Policies(policies));
                services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, HeaderAuthentication>("Test", _ => { });
            })
        );

    private static async Task CreateTablesAsync(WebApplicationFactory<Program> factory, MJCZone.DapperMatic.AspNetCore.Models.Dtos.DatasourceDto datasource)
    {
        using var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!);
        db.Open();
        foreach (var table in new[] { "cm_orders", "cm_reports" })
        {
            await db.DropTableIfExistsAsync(null, table);
            await db.CreateTableIfNotExistsAsync(new DmTable(null, table, [new DmColumn("id", typeof(int), isPrimaryKey: true)]));
        }
    }

    // The cm_ tables a caller's OpenAPI document describes.
    private static async Task<List<string>> TablesAsync(WebApplicationFactory<Program> factory, string datasourceId, string name, string role, string? plan = null)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Name", name);
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        if (plan is not null)
        {
            client.DefaultRequestHeaders.Add("X-Test-Plan", plan);
        }

        var response = await client.GetAsync($"/api/dm/d/{datasourceId}/openapi");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, text);
        return
        [
            .. JsonDocument.Parse(text).RootElement.GetProperty("paths").EnumerateObject()
                .Select(p => p.Name.Split('/').SkipWhile(s => s != "t").Skip(1).FirstOrDefault())
                .OfType<string>()
                .Where(t => t.StartsWith("cm_", StringComparison.Ordinal))
                .Distinct()
                .Order(),
        ];
    }

    // Signs requests in with the name, role and plan their headers give.
    private sealed class HeaderAuthentication(
        Microsoft.Extensions.Options.IOptionsMonitor<AuthenticationSchemeOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder
    ) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Name", out var name))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            List<Claim> claims = [new Claim(ClaimTypes.Name, name.ToString()), new Claim(ClaimTypes.Role, Request.Headers["X-Test-Role"].ToString())];
            if (Request.Headers.TryGetValue("X-Test-Plan", out var plan))
            {
                claims.Add(new Claim("plan", plan.ToString()));
            }

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")), "Test")));
        }
    }
}
