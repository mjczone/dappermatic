// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MJCZone.DapperMatic.AspNetCore.Factories;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Tests.Factories;
using MJCZone.DapperMatic.AspNetCore.Tests.Infrastructure;
using MJCZone.DapperMatic.Models;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Endpoints;

/// <summary>
/// Each caller's own OpenAPI document: the tables it may use, the actions it may take, the columns it may see.
/// </summary>
public class OpenApiDocumentTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private readonly TestcontainersAssemblyFixture _fixture;

    public OpenApiDocumentTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer, "dbo")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql, "public")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql, null)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite, null)]
    public async Task Each_caller_is_described_only_what_it_may_use_Async(string datasourceId, string? schemaName)
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == datasourceId);
        using var factory = new WafWithInMemoryDatasourceRepository([datasource]).WithWebHostBuilder(web =>
            web.ConfigureServices(services =>
            {
                services.Configure<DapperMaticOptions>(options =>
                {
                    options.Policies.Clear();
                    options.Relations.Add(new DapperMaticRelation { TableName = "oa_orders", Name = "customer", OtherTableName = "oa_customers", Columns = ["customer_id"], OtherColumns = ["id"] });
                    options.Relations.Add(new DapperMaticRelation { TableName = "oa_orders", Name = "notes", OtherTableName = "oa_hidden", Columns = ["id"], OtherColumns = ["id"], Many = true });
                });
                services.AddDapperMatic(dm =>
                    dm.Policies(
                        DmPolicy.Allow(DmAction.Read | DmAction.Create, "clerk").OnTables("oa_orders"),
                        DmPolicy.Allow(DmAction.Read, "clerk").OnTables("oa_customers").OnlyColumns("id", "name"),
                        DmPolicy.Allow(DmAction.All, "admin")
                    )
                );
            })
        );

        using (var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!))
        {
            db.Open();
            foreach (var table in new[] { "oa_orders", "oa_customers", "oa_hidden" })
            {
                await db.DropTableIfExistsAsync(schemaName, table);
            }

            await db.CreateTableIfNotExistsAsync(
                new DmTable(schemaName, "oa_customers", [new DmColumn("id", typeof(int), isPrimaryKey: true), new DmColumn("name", typeof(string), length: 50, isNullable: true), new DmColumn("secret", typeof(string), length: 50, isNullable: true)])
            );
            await db.CreateTableIfNotExistsAsync(
                new DmTable(
                    schemaName,
                    "oa_orders",
                    [
                        new DmColumn("id", typeof(int), isPrimaryKey: true, isAutoIncrement: true),
                        new DmColumn("customer_id", typeof(int)),
                        new DmColumn("total", typeof(decimal), precision: 10, scale: 2, isNullable: true),
                        new DmColumn("placed", typeof(DateTime)),
                    ]
                )
            );
            await db.CreateTableIfNotExistsAsync(new DmTable(schemaName, "oa_hidden", [new DmColumn("id", typeof(int), isPrimaryKey: true)]));
        }

        var url = schemaName is null ? $"/api/dm/d/{datasourceId}/openapi" : $"/api/dm/d/{datasourceId}/s/{schemaName}/openapi";
        async Task<JsonElement> Document(string role)
        {
            using var client = factory.CreateAuthenticatedClient(new Claim(ClaimTypes.Role, role));
            var response = await client.GetAsync(url);
            var text = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, text);
            return JsonDocument.Parse(text).RootElement.Clone();
        }

        static List<string> Operations(JsonElement document) =>
            [.. document.GetProperty("paths").EnumerateObject().Where(p => p.Name.Contains("/oa_", StringComparison.Ordinal)).SelectMany(p => p.Value.EnumerateObject().Select(o => $"{o.Name} {p.Name}")).Order()];

        // The clerk reads and inserts orders, reads customers' names, and knows nothing of the rest.
        var clerk = await Document("clerk");
        clerk.GetProperty("openapi").GetString().Should().Be("3.1.1");
        clerk.GetProperty("servers")[0].GetProperty("url").GetString().Should().EndWith(url[..^"/openapi".Length]);
        Operations(clerk).Should().Equal("get /t/oa_customers/query", "get /t/oa_orders/query", "post /t/oa_orders/rows");
        clerk.GetRawText().Should().NotContain("oa_hidden").And.NotContain("secret");

        var schemas = clerk.GetProperty("components").GetProperty("schemas");
        schemas.GetProperty("oa_customers").GetProperty("properties").EnumerateObject().Select(p => p.Name).Should().Equal("id", "name");
        var order = schemas.GetProperty("oa_orders").GetProperty("properties");
        order.GetProperty("id").GetProperty("type").GetString().Should().Be("integer");
        order.GetProperty("total").GetProperty("type").EnumerateArray().Select(t => t.GetString()).Should().Equal("number", "null");
        order.GetProperty("placed").GetProperty("format").GetString().Should().Be("date-time");
        order.GetProperty("customer").GetProperty("oneOf")[0].GetProperty("$ref").GetString().Should().Be("#/components/schemas/oa_customers");
        order.TryGetProperty("notes", out _).Should().BeFalse();
        schemas.GetProperty("oa_ordersNew").GetProperty("required").EnumerateArray().Select(r => r.GetString()).Should().BeEquivalentTo("customer_id", "placed");
        clerk.GetProperty("paths").GetProperty("/t/oa_orders/query").GetProperty("get").GetProperty("parameters").EnumerateArray()
            .Single(p => p.GetProperty("name").GetString() == "$expand").GetProperty("description").GetString().Should().Contain("customer").And.NotContain("notes");

        // The administrator does everything, to every table.
        Operations(await Document("admin")).Should().Contain(["post /t/oa_orders/rows/update", "post /t/oa_orders/rows/delete", "post /t/oa_orders/rows/save", "get /t/oa_hidden/query"]);

        // A caller no policy names is described nothing.
        Operations(await Document("nobody")).Should().BeEmpty();

        using (var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!))
        {
            db.Open();
            foreach (var table in new[] { "oa_orders", "oa_customers", "oa_hidden" })
            {
                await db.DropTableIfExistsAsync(schemaName, table);
            }
        }
    }
}
