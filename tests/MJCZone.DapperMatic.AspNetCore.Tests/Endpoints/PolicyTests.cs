// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MJCZone.DapperMatic.AspNetCore.Factories;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.AspNetCore.Tests.Factories;
using MJCZone.DapperMatic.AspNetCore.Tests.Infrastructure;
using MJCZone.DapperMatic.Models;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Endpoints;

/// <summary>
/// Policies enforced by DapperMatic, on every provider: rows keyed by tenant with global rows every tenant reads, a
/// tree of tenants where each reads its own rows and those below it, a column kept from callers, values the policy
/// writes, a check on what is written, a fence for everyone, and nothing at all where no policy grants it.
/// </summary>
public class PolicyTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private readonly TestcontainersAssemblyFixture _fixture;

    public PolicyTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer, "dbo")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql, "public")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql, null)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite, null)]
    public async Task Policies_hold_each_tenant_to_its_rows_and_columns_Async(string datasourceId, string? schemaName)
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == datasourceId);
        using var factory = new WafWithInMemoryDatasourceRepository([datasource]).WithWebHostBuilder(web =>
            web.ConfigureServices(services =>
            {
                // The test host grants everything; these tests start from nothing.
                services.Configure<DapperMaticOptions>(options => options.Policies.Clear());
                services.AddDapperMatic(dm => dm
                    .Tenancy(t => t
                        .FromClaim("tenant_id")
                        .ValueFromSql("below", "SELECT descendant FROM pol_tree WHERE ancestor = @tenant.id"))
                    .Policies(
                        DmPolicy.Allow(DmAction.Read, "*")
                            .Named("notes: global rows, and the tenant's and those below")
                            .OnTables("pol_notes")
                            .Where("tenant_id eq null or tenant_id in @tenant.below")
                            .ExceptColumns("secret"),
                        DmPolicy.Allow(DmAction.Create | DmAction.Update, "authenticated")
                            .OnTables("pol_notes")
                            .Where("tenant_id eq @tenant.id")
                            .Check("tenant_id eq @tenant.id and body ne 'forbidden'")
                            .Preset("tenant_id", "@tenant.id")
                            .ExceptColumns("secret"),
                        DmPolicy.Allow(DmAction.Delete, "admin").OnTables("pol_notes").Where("tenant_id eq @tenant.id"),
                        DmPolicy.Restrict(DmAction.ReadWrite).OnTables("pol_notes").Where("body ne 'hidden'"),
                        DmPolicy.Allow(DmAction.ReadSchema, "authenticated").OnTables("pol_*").ExceptTables("pol_tree")
                    ));
            })
        );

        using (var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!))
        {
            db.Open();
            foreach (var table in new[] { "pol_notes", "pol_tree", "pol_other" })
            {
                await db.DropTableIfExistsAsync(schemaName, table);
            }

            await db.CreateTableIfNotExistsAsync(
                new DmTable(
                    schemaName,
                    "pol_tree",
                    [
                        new DmColumn("id", typeof(int), isPrimaryKey: true),
                        new DmColumn("ancestor", typeof(string), length: 10),
                        new DmColumn("descendant", typeof(int)),
                    ]
                )
            );
            await db.CreateTableIfNotExistsAsync(
                new DmTable(
                    schemaName,
                    "pol_notes",
                    [
                        new DmColumn("id", typeof(int), isPrimaryKey: true),
                        new DmColumn("tenant_id", typeof(int), isNullable: true),
                        new DmColumn("body", typeof(string), length: 50),
                        new DmColumn("secret", typeof(string), length: 50, isNullable: true),
                    ]
                )
            );
            await db.CreateTableIfNotExistsAsync(new DmTable(schemaName, "pol_other", [new DmColumn("id", typeof(int), isPrimaryKey: true)]));

            // Tenant 1 is the root, with 2 and 4 below it, and 3 below 2.
            var tree = new[] { ("1", 1), ("1", 2), ("1", 3), ("1", 4), ("2", 2), ("2", 3), ("3", 3), ("4", 4) };
            var qualified = (string name) => db.GetSchemaQualifiedTableName(schemaName, name);
            for (var i = 0; i < tree.Length; i++)
            {
                await Dapper.SqlMapper.ExecuteAsync(db, $"INSERT INTO {qualified("pol_tree")} (id, ancestor, descendant) VALUES ({i + 1}, '{tree[i].Item1}', {tree[i].Item2})");
            }

            await Dapper.SqlMapper.ExecuteAsync(
                db,
                $"INSERT INTO {qualified("pol_notes")} (id, tenant_id, body, secret) VALUES "
                    + "(1, NULL, 'global', 's1'), (2, 1, 'root', 's2'), (3, 2, 'a', 's3'), (4, 3, 'a1', 's4'), (5, 4, 'b', 's5'), (6, 2, 'hidden', 's6')"
            );
        }

        var baseUrl = schemaName is not null ? $"/api/dm/d/{datasourceId}/s/{schemaName}/t" : $"/api/dm/d/{datasourceId}/t";
        HttpClient Client(string? tenant, params string[] roles) =>
            tenant is null
                ? factory.CreateClient()
                : factory.CreateAuthenticatedClient([new Claim("tenant_id", tenant), .. roles.Select(r => new Claim(ClaimTypes.Role, r))]);

        async Task<int[]> Visible(HttpClient client)
        {
            var response = await client.GetAsync($"{baseUrl}/pol_notes/query?$orderby=id");
            var text = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, text);
            var rows = JsonDocument.Parse(text).RootElement.GetProperty("result").EnumerateArray().ToList();
            rows.Should().AllSatisfy(r => r.TryGetProperty("secret", out _).Should().BeFalse("the secret column is kept from every caller"));
            return [.. rows.Select(r => r.GetProperty("id").GetInt32())];
        }

        async Task<HttpResponseMessage> Post(HttpClient client, string path, object body) =>
            await client.PostAsync($"{baseUrl}/{path}", new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));

        async Task<int> Affected(HttpClient client, string path, object body)
        {
            var response = await Post(client, path, body);
            var text = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, text);
            return JsonDocument.Parse(text).RootElement.GetProperty("affected").GetInt32();
        }

        using var anonymous = Client(null);
        using var root = Client("1");
        using var a = Client("2");
        using var aAdmin = Client("2", "admin");
        using var b = Client("4");

        // Reads: global rows, the tenant's own and those below it, never above or beside; the fence hides row 6.
        (await Visible(a)).Should().Equal(1, 3, 4);
        (await Visible(root)).Should().Equal(1, 2, 3, 4, 5);
        (await Visible(b)).Should().Equal(1, 5);
        (await Visible(anonymous)).Should().Equal(1);

        // The kept column cannot be selected, compared or ordered by.
        (await a.GetAsync($"{baseUrl}/pol_notes/query?$select=id,secret")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await a.GetAsync($"{baseUrl}/pol_notes/query?$filter=secret eq 's3'")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await anonymous.GetAsync($"{baseUrl}/pol_notes/query?$orderby=secret")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Inserts: the policy writes the tenant whatever the request says; a row breaking the check is refused, and
        // so is the kept column, and an anonymous caller.
        (await Affected(a, "pol_notes/rows", new { rows = new object[] { new { id = 7, tenant_id = 99, body = "mine" } } })).Should().Be(1);
        (await Visible(a)).Should().Equal(1, 3, 4, 7);
        (await Visible(b)).Should().Equal(1, 5);
        (await Post(a, "pol_notes/rows", new { rows = new object[] { new { id = 8, body = "forbidden" } } })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Post(a, "pol_notes/rows", new { rows = new object[] { new { id = 8, body = "hidden" } } })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Post(a, "pol_notes/rows", new { rows = new object[] { new { id = 8, body = "x", secret = "y" } } })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Post(anonymous, "pol_notes/rows", new { rows = new object[] { new { id = 8, body = "x" } } })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Visible(root)).Should().Equal(1, 2, 3, 4, 5, 7);

        // Updates reach only the tenant's own rows (not the global one, not the one below, not the fenced one), and a
        // change breaking the check is refused whole.
        (await Affected(a, "pol_notes/rows/update", new { set = new { body = "changed" }, filter = "true" })).Should().Be(2);
        (await Affected(a, "pol_notes/rows/update", new { set = new { body = "changed" }, filter = "id eq 4" })).Should().Be(0);
        (await Post(a, "pol_notes/rows/update", new { set = new { body = "forbidden" }, filter = "true" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // A condition cannot compare the kept column: the count of rows changed would tell its values.
        (await Post(a, "pol_notes/rows/update", new { set = new { body = "changed" }, filter = "secret eq 's3'" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var bodies = JsonDocument.Parse(await (await root.GetAsync($"{baseUrl}/pol_notes/query?$filter=tenant_id eq 2&$orderby=id")).Content.ReadAsStringAsync())
            .RootElement.GetProperty("result").EnumerateArray().Select(r => r.GetProperty("body").GetString());
        bodies.Should().Equal("changed", "changed");

        // Deletes need the admin role, and reach the tenant's own rows only; the fenced row stays.
        (await Post(a, "pol_notes/rows/delete", new { filter = "true" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Affected(aAdmin, "pol_notes/rows/delete", new { filter = "true" })).Should().Be(2);
        (await Visible(root)).Should().Equal(1, 2, 4, 5);

        // Nothing is allowed where no policy grants it: rows of another table, or the tree itself.
        (await a.GetAsync($"{baseUrl}/pol_other/query")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await a.GetAsync($"{baseUrl}/pol_tree/query")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await anonymous.GetAsync($"{baseUrl}/pol_other/query")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Listings show only what the caller may read the definition of.
        var listed = JsonDocument.Parse(await (await a.GetAsync($"{baseUrl}/")).Content.ReadAsStringAsync())
            .RootElement.GetProperty("result").EnumerateArray().Select(t => t.GetProperty("tableName").GetString()!.ToLowerInvariant()).ToList();
        listed.Should().Contain(["pol_notes", "pol_other"]).And.NotContain("pol_tree").And.OnlyContain(n => n.StartsWith("pol_"));
        (await a.GetAsync($"{baseUrl}/pol_tree")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await anonymous.GetAsync($"{baseUrl}/")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// What a request reaches through a related table, a value it does not have, a write's condition, a relation's
    /// keys or a column the database fills is held to the policies as a plain read or write is.
    /// </summary>
    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer, "dbo")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql, "public")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql, null)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite, null)]
    public async Task Policies_are_not_reached_around_Async(string datasourceId, string? schemaName)
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == datasourceId);
        using var factory = new WafWithInMemoryDatasourceRepository([datasource]).WithWebHostBuilder(web =>
            web.ConfigureServices(services =>
            {
                services.Configure<DapperMaticOptions>(options =>
                {
                    options.Policies.Clear();
                    options.Relations.Add(new DapperMaticRelation { TableName = "pr_orders", Name = "customer", OtherTableName = "pr_customers", Columns = ["customer_id"], OtherColumns = ["id"] });
                    options.Relations.Add(new DapperMaticRelation { TableName = "pr_orders", Name = "ref", OtherTableName = "pr_customers", Columns = ["ref_id"], OtherColumns = ["id"] });
                });
                services.AddDapperMatic(dm => dm
                    .Tenancy(t => t.FromClaim("tenant_id"))
                    .Policies(
                        DmPolicy.Allow(DmAction.Read, "*").OnTables("pr_customers").Where("tenant_id eq @tenant.id"),
                        DmPolicy.Allow(DmAction.Read, "*").OnTables("pr_orders").ExceptColumns("secret", "ref_id"),
                        DmPolicy.Allow(DmAction.Delete, "*").OnTables("pr_orders"),
                        DmPolicy.Allow(DmAction.Create, "*").OnTables("pr_orders").Preset("tenant_id", "@tenant.id").Check("status ne 'approved'")
                    ));
            })
        );

        using (var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!))
        {
            db.Open();
            await db.DropTableIfExistsAsync(schemaName, "pr_orders");
            await db.DropTableIfExistsAsync(schemaName, "pr_customers");
            await db.CreateTableIfNotExistsAsync(
                new DmTable(
                    schemaName,
                    "pr_customers",
                    [
                        new DmColumn("id", typeof(int), isPrimaryKey: true),
                        new DmColumn("tenant_id", typeof(int), isNullable: true),
                        new DmColumn("name", typeof(string), length: 50),
                    ]
                )
            );
            await db.CreateTableIfNotExistsAsync(
                new DmTable(
                    schemaName,
                    "pr_orders",
                    [
                        new DmColumn("id", typeof(int), isPrimaryKey: true),
                        new DmColumn("tenant_id", typeof(int), isNullable: true),
                        new DmColumn("customer_id", typeof(int), isNullable: true),
                        new DmColumn("ref_id", typeof(int), isNullable: true),
                        new DmColumn("status", typeof(string), length: 20, defaultExpression: "'approved'"),
                        new DmColumn("secret", typeof(string), length: 50, isNullable: true),
                    ]
                )
            );
            var qualified = (string name) => db.GetSchemaQualifiedTableName(schemaName, name);
            await Dapper.SqlMapper.ExecuteAsync(db, $"INSERT INTO {qualified("pr_customers")} (id, tenant_id, name) VALUES (1, 1, 'Acme'), (2, 2, 'Globex'), (3, NULL, 'Shared')");
            await Dapper.SqlMapper.ExecuteAsync(
                db,
                $"INSERT INTO {qualified("pr_orders")} (id, tenant_id, customer_id, ref_id, status, secret) VALUES "
                    + "(1, 1, 1, 1, 'pending', 's1'), (2, 2, 2, 2, 'pending', 's2'), (3, 1, 2, 2, 'pending', 's3')"
            );
        }

        var baseUrl = schemaName is not null ? $"/api/dm/d/{datasourceId}/s/{schemaName}/t" : $"/api/dm/d/{datasourceId}/t";
        using var tenant1 = factory.CreateAuthenticatedClient(new Claim("tenant_id", "1"));
        using var noTenant = factory.CreateAuthenticatedClient(new Claim(ClaimTypes.Role, "staff"));

        async Task<int[]> Ids(HttpClient client, string query)
        {
            var response = await client.GetAsync($"{baseUrl}/{query}");
            var text = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, text);
            return [.. JsonDocument.Parse(text).RootElement.GetProperty("result").EnumerateArray().Select(r => r.GetProperty("id").GetInt32())];
        }

        Task<HttpResponseMessage> Post(HttpClient client, string path, object body) =>
            client.PostAsync($"{baseUrl}/{path}", new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));

        // A related table's policy reads the related row: order 3 is tenant 1's, its customer is tenant 2's.
        (await Ids(tenant1, "pr_orders/query?$filter=customer/name eq 'Acme'")).Should().Equal(1);
        (await Ids(tenant1, "pr_orders/query?$filter=customer/name eq 'Globex'")).Should().BeEmpty();

        // With no tenant, a test of the tenant holds for no row, not for the rows that have none.
        (await Ids(tenant1, "pr_customers/query")).Should().Equal(1);
        (await Ids(noTenant, "pr_customers/query")).Should().BeEmpty();
        (await Post(noTenant, "pr_orders/rows", new { rows = new object[] { new { id = 10, status = "pending" } } })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // A check reading a column the database would fill needs the row to give it.
        (await Post(tenant1, "pr_orders/rows", new { rows = new object[] { new { id = 11, customer_id = 1 } } })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Post(tenant1, "pr_orders/rows", new { rows = new object[] { new { id = 11, customer_id = 1, status = "pending" } } })).StatusCode.Should().Be(HttpStatusCode.OK);

        // A write's condition reads as a query does: not the kept column, even with a grant to delete every row.
        (await Post(tenant1, "pr_orders/rows/delete", new { filter = "startswith(secret,'s')", dryRun = true })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Totals count only the rows the caller may read, and never a kept column.
        var counted = JsonDocument.Parse(await (await tenant1.GetAsync($"{baseUrl}/pr_customers/query?$apply=aggregate($count as n)")).Content.ReadAsStringAsync()).RootElement;
        counted.GetProperty("result").EnumerateArray().Single().GetProperty("n").GetInt64().Should().Be(1);
        (await tenant1.GetAsync($"{baseUrl}/pr_orders/query?$apply=aggregate(secret with countdistinct as n)")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // A batch is refused whole, before anything is written, when one operation is not allowed.
        var batch = new
        {
            operations = new object[]
            {
                new { op = "insert", table = "pr_orders", schema = schemaName, rows = new[] { new { id = 20, status = "pending" } } },
                new { op = "insert", table = "pr_customers", schema = schemaName, rows = new[] { new { id = 20, name = "x" } } },
            },
        };
        (await tenant1.PostAsync($"/api/dm/d/{datasourceId}/batch", new StringContent(JsonSerializer.Serialize(batch), Encoding.UTF8, "application/json")))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Ids(tenant1, "pr_orders/query?$filter=id eq 20")).Should().BeEmpty();

        // A relation over a kept column would tell its values.
        (await tenant1.GetAsync($"{baseUrl}/pr_orders/query?$filter=ref/name eq 'Acme'")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await tenant1.GetAsync($"{baseUrl}/pr_orders/query?$expand=ref")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// A change to a schema is held to what its policies name: a schema left out of the URL is the default one, a view's
    /// definition reads only what its author may read, a rename's new name is authorized too, and a foreign key needs
    /// the table it references readable.
    /// </summary>
    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer, "dbo")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql, "public")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql, null)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite, null)]
    public async Task Schema_changes_reach_no_further_than_the_policies_Async(string datasourceId, string? schemaName)
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == datasourceId);
        using var factory = new WafWithInMemoryDatasourceRepository([datasource]).WithWebHostBuilder(web =>
            web.ConfigureServices(services =>
            {
                services.Configure<DapperMaticOptions>(options => options.Policies.Clear());
                services.AddDapperMatic(dm => dm.Policies(
                    DmPolicy.Allow(DmAction.ChangeSchema | DmAction.ReadSchema | DmAction.ReadWrite, "dev").OnTables("sc_dev_*"),
                    DmPolicy.Allow(DmAction.ChangeSchema | DmAction.ReadSchema | DmAction.ReadWrite | DmAction.DefineViews, "dba"),
                    DmPolicy.Allow(DmAction.DefineViews, "dev").InSchemas("sandbox"),
                    DmPolicy.Allow(DmAction.ReadSchema, "sandboxer").ExceptSchemas(schemaName ?? "none")
                ));
            })
        );

        using (var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!))
        {
            db.Open();
            await db.DropViewIfExistsAsync(schemaName, "sc_dev_view");
            foreach (var table in new[] { "sc_dev_a", "sc_prod_a", "sc_salaries" })
            {
                await db.DropTableIfExistsAsync(schemaName, table);
            }

            await db.CreateTableIfNotExistsAsync(new DmTable(schemaName, "sc_salaries", [new DmColumn("id", typeof(int), isPrimaryKey: true), new DmColumn("amount", typeof(int))]));
            await db.CreateTableIfNotExistsAsync(
                new DmTable(schemaName, "sc_dev_a", [new DmColumn("id", typeof(int), isPrimaryKey: true), new DmColumn("sal_id", typeof(int), isNullable: true)])
            );
        }

        var schemaPath = schemaName is not null ? $"/api/dm/d/{datasourceId}/s/{schemaName}" : $"/api/dm/d/{datasourceId}";
        using var dev = factory.CreateAuthenticatedClient(new Claim(ClaimTypes.Role, "dev"));
        using var dba = factory.CreateAuthenticatedClient(new Claim(ClaimTypes.Role, "dba"));
        using var sandboxer = factory.CreateAuthenticatedClient(new Claim(ClaimTypes.Role, "sandboxer"));
        Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, object body) =>
            client.SendAsync(new HttpRequestMessage(method, path) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") });

        // A view's SQL can read any table: writing one needs DefineViews for the whole datasource, which a grant
        // confined to a schema is not.
        var salaries = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!)
            .GetSchemaQualifiedTableName(schemaName, "sc_salaries");
        var view = new { viewName = "sc_dev_view", definition = $"SELECT id, amount FROM {salaries}" };
        (await Send(dev, HttpMethod.Post, $"{schemaPath}/v", view)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Send(dba, HttpMethod.Post, $"{schemaPath}/v", view)).StatusCode.Should().Be(HttpStatusCode.Created);

        // A rename is authorized as the new name too.
        (await Send(dev, HttpMethod.Put, $"{schemaPath}/t/sc_dev_a", new { tableName = "sc_prod_a" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // A foreign key reads the table it references.
        var key = new { columnNames = new[] { "sal_id" }, referencedTableName = "sc_salaries", referencedColumnNames = new[] { "id" } };
        (await Send(dev, HttpMethod.Post, $"{schemaPath}/t/sc_dev_a/foreign-key-constraints", key)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Send(dba, HttpMethod.Post, $"{schemaPath}/t/sc_dev_a/foreign-key-constraints", key)).StatusCode.Should().Be(HttpStatusCode.Created);

        // A URL naming no schema is about the default one: a policy excepting it is not passed over.
        if (schemaName is not null)
        {
            (await sandboxer.GetAsync($"/api/dm/d/{datasourceId}/t/sc_dev_a")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await sandboxer.GetAsync($"/api/dm/d/{datasourceId}/t")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await sandboxer.GetAsync($"/api/dm/d/{datasourceId}/s/{schemaName}/t/sc_dev_a")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    /// <summary>
    /// A grant reaches what it names and no more: a datasource-wide action needs a grant for all of the datasource, and
    /// with permissions of the application's own, a table whose rows any policy is about is held for every row action.
    /// </summary>
    [Fact]
    public async Task Grants_reach_no_further_than_they_say_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        using (var factory = new WafWithInMemoryDatasourceRepository([datasource]).WithWebHostBuilder(web =>
            web.ConfigureServices(services =>
            {
                services.Configure<DapperMaticOptions>(options => options.Policies.Clear());
                services.AddDapperMatic(dm => dm.Policies(
                    DmPolicy.Allow(DmAction.ManageDatasources | DmAction.ReadSchema, "schema-dba").OnDatasources(datasource.Id!).InSchemas("t1"),
                    DmPolicy.Allow(DmAction.ManageDatasources | DmAction.ReadSchema, "dba").OnDatasources(datasource.Id!)
                ));
            })))
        {
            using var schemaDba = factory.CreateAuthenticatedClient(new Claim(ClaimTypes.Role, "schema-dba"));
            using var dba = factory.CreateAuthenticatedClient(new Claim(ClaimTypes.Role, "dba"));
            (await schemaDba.GetAsync($"/api/dm/d/{datasource.Id}/test")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await dba.GetAsync($"/api/dm/d/{datasource.Id}/test")).StatusCode.Should().Be(HttpStatusCode.OK);

            // A datasource added without an id is not one a list of datasources names.
            var added = new { provider = "Sqlite", connectionString = "Data Source=unnamed.db", displayName = "Unnamed" };
            (await dba.PostAsync("/api/dm/d", new StringContent(JsonSerializer.Serialize(added), Encoding.UTF8, "application/json"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        using (var factory = new WafWithInMemoryDatasourceRepository([datasource]).WithWebHostBuilder(web =>
            web.ConfigureServices(services =>
            {
                services.Configure<DapperMaticOptions>(options => options.Policies.Clear());
                services.AddDapperMatic(dm => dm
                    .UseCustomPermissions(_ => new AllowEverything())
                    .Policy(DmPolicy.Allow(DmAction.Read, "*").OnTables("cp_items").Where("tenant_id eq 1")));
            })))
        {
            using (var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!))
            {
                db.Open();
                foreach (var table in new[] { "cp_items", "cp_free" })
                {
                    await db.DropTableIfExistsAsync(null, table);
                    await db.CreateTableIfNotExistsAsync(
                        new DmTable(null, table, [new DmColumn("id", typeof(int), isPrimaryKey: true), new DmColumn("tenant_id", typeof(int)), new DmColumn("name", typeof(string), length: 20)])
                    );
                    await Dapper.SqlMapper.ExecuteAsync(db, $"INSERT INTO {table} (id, tenant_id, name) VALUES (1, 1, 'a'), (2, 2, 'b')");
                }
            }

            using var client = factory.CreateAuthenticatedClient(new Claim(ClaimTypes.Role, "staff"));
            var update = new { set = new { name = "x" }, filter = "true", dryRun = true };
            (await client.PostAsync($"/api/dm/d/{datasource.Id}/t/cp_items/rows/update", new StringContent(JsonSerializer.Serialize(update), Encoding.UTF8, "application/json")))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await client.PostAsync($"/api/dm/d/{datasource.Id}/t/cp_free/rows/update", new StringContent(JsonSerializer.Serialize(update), Encoding.UTF8, "application/json")))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    /// <summary>
    /// With no policies and the default permissions, every request is refused.
    /// </summary>
    [Fact]
    public async Task Without_policies_everything_is_refused_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        using var factory = new WafWithInMemoryDatasourceRepository([datasource]).WithWebHostBuilder(web =>
            web.ConfigureServices(services => services.Configure<DapperMaticOptions>(options => options.Policies.Clear()))
        );
        using var client = factory.CreateAuthenticatedClient(new Claim(ClaimTypes.Role, "admin"));
        (await client.GetAsync("/api/dm/d")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync($"/api/dm/d/{datasource.Id}/t")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync($"/api/dm/d/{datasource.Id}/t/anything/query")).StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
        using var anonymous = factory.CreateClient();
        (await anonymous.GetAsync("/api/dm/d")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Permissions of an application's own that allow every operation, leaving rows to the policies.
    private sealed class AllowEverything : IDapperMaticPermissions
    {
        public Task<bool> IsAuthorizedAsync(DapperMaticOperation context) => Task.FromResult(true);
    }
}
