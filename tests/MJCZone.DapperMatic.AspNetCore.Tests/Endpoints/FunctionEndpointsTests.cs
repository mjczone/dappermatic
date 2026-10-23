// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Dapper;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MJCZone.DapperMatic.AspNetCore.Factories;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Tests.Factories;
using MJCZone.DapperMatic.AspNetCore.Tests.Infrastructure;
using MJCZone.DapperMatic.Models;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Endpoints;

/// <summary>
/// Functions the application declares, run by callers the policies let run them.
/// </summary>
public class FunctionEndpointsTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private readonly TestcontainersAssemblyFixture _fixture;

    public FunctionEndpointsTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite)]
    public async Task Declared_functions_run_as_the_policies_allow_Async(string datasourceId)
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == datasourceId);
        var sqlite = datasourceId == TestcontainersAssemblyFixture.DatasourceId_Sqlite;
        using var factory = new WafWithInMemoryDatasourceRepository([datasource]).WithWebHostBuilder(web =>
            web.ConfigureServices(services =>
            {
                services.Configure<DapperMaticOptions>(options =>
                {
                    options.Policies.Clear();
                    options.CachedTables.Add(new DapperMaticCachedTable { TableName = "fx_orders", Duration = TimeSpan.FromMinutes(10) });
                    options.Functions.Add(
                        new DapperMaticFunction
                        {
                            Name = "fn_totals",
                            Description = "Each customer's total, of the caller's tenant",
                            Sql = "SELECT customer_id, SUM(total) AS total FROM fx_orders WHERE tenant_id = @tenant AND total >= @min GROUP BY customer_id ORDER BY customer_id",
                            Parameters =
                            {
                                new DapperMaticFunctionParameter { Name = "min", Type = typeof(int), Default = 0 },
                                new DapperMaticFunctionParameter { Name = "tenant", Type = typeof(int), Value = "@tenant.id" },
                            },
                        }
                    );
                    options.Functions.Add(
                        new DapperMaticFunction
                        {
                            Name = "fn_bump",
                            Sql = "UPDATE fx_orders SET total = total + @by WHERE id = @id",
                            Parameters = { new DapperMaticFunctionParameter { Name = "id", Type = typeof(int) }, new DapperMaticFunctionParameter { Name = "by", Type = typeof(int) } },
                            Writes = ["fx_orders"],
                        }
                    );
                    options.Functions.Add(new DapperMaticFunction { Name = "fn_all", Sql = "SELECT id FROM fx_orders", MaxRows = 2 });
                    options.Functions.Add(
                        new DapperMaticFunction
                        {
                            Name = "fn_orgs",
                            Sql = "SELECT id FROM fx_orders WHERE @org <> 'x'",
                            Parameters = { new DapperMaticFunctionParameter { Name = "org", Type = typeof(string), Value = "@claims.org" } },
                        }
                    );
                    if (sqlite)
                    {
                        options.Functions.Add(new DapperMaticFunction { Name = "fn_bump_all", Sql = "UPDATE fx_orders SET total = total + 1 RETURNING id", MaxRows = 1, Writes = ["fx_orders"] });
                    }
                    if (!sqlite)
                    {
                        options.Functions.Add(new DapperMaticFunction { Name = "fn_double", Routine = "fx_double", Parameters = { new DapperMaticFunctionParameter { Name = "x", Type = typeof(int) } } });
                    }
                });
                services.AddDapperMatic(dm =>
                    dm.Tenancy(t => t.FromClaim("tenant_id"))
                        .Policies(
                            DmPolicy.Allow(DmAction.Execute, "clerk").OnFunctions("fn_totals"),
                            DmPolicy.Allow(DmAction.All, "tabler").OnTables("fx_orders"),
                            DmPolicy.Allow(DmAction.All, "admin")
                        )
                );
            })
        );

        using (var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!))
        {
            db.Open();
            await db.DropTableIfExistsAsync(null, "fx_orders");
            await db.CreateTableIfNotExistsAsync(
                new DmTable(null, "fx_orders", [new DmColumn("id", typeof(int), isPrimaryKey: true), new DmColumn("tenant_id", typeof(int)), new DmColumn("customer_id", typeof(int)), new DmColumn("total", typeof(int))])
            );
            await db.ExecuteAsync("INSERT INTO fx_orders (id, tenant_id, customer_id, total) VALUES (1, 1, 10, 10), (2, 1, 10, 20), (3, 1, 11, 5), (4, 2, 12, 100)");
            switch (datasourceId)
            {
                case TestcontainersAssemblyFixture.DatasourceId_PostgreSql:
                    await db.ExecuteAsync("CREATE OR REPLACE FUNCTION fx_double(x integer) RETURNS TABLE(result integer) AS $$ SELECT x * 2 $$ LANGUAGE sql");
                    break;
                case TestcontainersAssemblyFixture.DatasourceId_SqlServer:
                    await db.ExecuteAsync("CREATE OR ALTER PROCEDURE fx_double @x int AS SELECT @x * 2 AS result");
                    break;
                case TestcontainersAssemblyFixture.DatasourceId_MySql:
                    await db.ExecuteAsync("DROP PROCEDURE IF EXISTS fx_double");
                    await db.ExecuteAsync("CREATE PROCEDURE fx_double(IN x INT) SELECT x * 2 AS result");
                    break;
            }
        }

        HttpClient Client(string role, string? tenant = null) =>
            factory.CreateAuthenticatedClient([new Claim(ClaimTypes.Role, role), .. tenant is null ? Array.Empty<Claim>() : [new Claim("tenant_id", tenant)]]);
        async Task<(HttpStatusCode Status, string Body)> Run(HttpClient client, string function, object? arguments = null)
        {
            var response = arguments is null
                ? await client.PostAsync($"/api/dm/d/{datasourceId}/f/{function}", null)
                : await client.PostAsync($"/api/dm/d/{datasourceId}/f/{function}", new StringContent(JsonSerializer.Serialize(arguments), Encoding.UTF8, "application/json"));
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        static string Rows(string body) =>
            string.Join(";", JsonDocument.Parse(body).RootElement.GetProperty("result").EnumerateArray().Select(r => string.Join(",", r.EnumerateObject().Select(p => $"{p.Name}={p.Value}"))));

        using var clerk = Client("clerk", "1");
        using var admin = Client("admin", "2");
        using var tabler = Client("tabler", "1");

        // The clerk runs what it is granted, in its tenant, with defaults and its own arguments.
        var (status, body) = await Run(clerk, "fn_totals");
        status.Should().Be(HttpStatusCode.OK, body);
        Rows(body).Should().Be("customer_id=10,total=30;customer_id=11,total=5");
        (status, body) = await Run(clerk, "fn_totals", new { min = 15 });
        Rows(body).Should().Be("customer_id=10,total=20");

        // A value the function takes from the request's context cannot be given; nor an unknown or mistyped argument.
        (await Run(clerk, "fn_totals", new { tenant = 2 })).Status.Should().Be(HttpStatusCode.BadRequest);
        (await Run(clerk, "fn_totals", new { nope = 1 })).Status.Should().Be(HttpStatusCode.BadRequest);
        (await Run(clerk, "fn_totals", new { min = "many" })).Status.Should().Be(HttpStatusCode.BadRequest);

        // Nothing else: not another function, and not whether one exists.
        (await Run(clerk, "fn_bump", new { id = 1, by = 1 })).Status.Should().Be(HttpStatusCode.Forbidden);
        (await Run(clerk, "fn_missing")).Status.Should().Be(HttpStatusCode.Forbidden);
        (await Run(admin, "fn_missing")).Status.Should().Be(HttpStatusCode.NotFound);

        // A grant confined to tables does not run functions, whose SQL reads and writes what it likes.
        (await Run(tabler, "fn_totals")).Status.Should().Be(HttpStatusCode.Forbidden);

        // A function's run makes stale the results kept of the tables it says it writes.
        async Task<string> Totals() =>
            string.Join(",", JsonDocument.Parse(await (await admin.GetAsync($"/api/dm/d/{datasourceId}/t/fx_orders/query?$orderby=id")).Content.ReadAsStringAsync())
                .RootElement.GetProperty("result").EnumerateArray().Select(r => r.GetProperty("total").GetInt32()));
        (await Totals()).Should().Be("10,20,5,100");
        (status, body) = await Run(admin, "fn_bump", new { id = 1, by = 5 });
        status.Should().Be(HttpStatusCode.OK, body);
        (await Totals()).Should().Be("15,20,5,100");

        // A function returns at most its rows.
        (status, body) = await Run(admin, "fn_all");
        status.Should().Be(HttpStatusCode.BadRequest, body);
        body.Should().Contain("more than 2 rows");

        // One that wrote outside a transaction before returning too many keeps its write: the results kept are stale.
        if (sqlite)
        {
            (await Run(admin, "fn_bump_all")).Status.Should().Be(HttpStatusCode.BadRequest);
            (await Totals()).Should().Be("16,21,6,101");
        }

        // A value the request has several of (claims of one type) is not one value: refused, never bound as some text.
        using (var oneOrg = factory.CreateAuthenticatedClient(new Claim(ClaimTypes.Role, "admin"), new Claim("org", "a")))
        {
            (status, body) = await Run(oneOrg, "fn_orgs");
            status.Should().Be(HttpStatusCode.OK, body);
        }

        using (var twoOrgs = factory.CreateAuthenticatedClient(new Claim(ClaimTypes.Role, "admin"), new Claim("org", "a"), new Claim("org", "b")))
        {
            (status, body) = await Run(twoOrgs, "fn_orgs");
            status.Should().Be(HttpStatusCode.BadRequest, body);
        }

        // A routine of the database runs as the database calls one.
        if (!sqlite)
        {
            (status, body) = await Run(admin, "fn_double", new { x = 21 });
            status.Should().Be(HttpStatusCode.OK, body);
            Rows(body).Should().Be("result=42");
        }

        // Each caller's OpenAPI document has the functions it may run, with the arguments it gives.
        var document = JsonDocument.Parse(await (await clerk.GetAsync($"/api/dm/d/{datasourceId}/openapi")).Content.ReadAsStringAsync()).RootElement;
        document.GetProperty("paths").EnumerateObject().Select(p => p.Name).Should().Equal("/f/fn_totals");
        document.GetProperty("components").GetProperty("schemas").GetProperty("fn_totalsArguments").GetProperty("properties").EnumerateObject().Select(p => p.Name).Should().Equal("min");
        var adminDocument = JsonDocument.Parse(await (await admin.GetAsync($"/api/dm/d/{datasourceId}/openapi")).Content.ReadAsStringAsync()).RootElement;
        adminDocument.GetProperty("paths").EnumerateObject().Select(p => p.Name).Should().Contain(["/f/fn_totals", "/f/fn_bump", "/f/fn_all", "/t/fx_orders/query"]);
        JsonDocument.Parse(await (await tabler.GetAsync($"/api/dm/d/{datasourceId}/openapi")).Content.ReadAsStringAsync())
            .RootElement.GetProperty("paths").EnumerateObject().Select(p => p.Name).Should().NotContain(n => n.StartsWith("/f/"));

        using (var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!))
        {
            db.Open();
            await db.DropTableIfExistsAsync(null, "fx_orders");
        }
    }
}
