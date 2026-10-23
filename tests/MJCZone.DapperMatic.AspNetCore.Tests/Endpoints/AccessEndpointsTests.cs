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
using Microsoft.Extensions.DependencyInjection.Extensions;
using MJCZone.DapperMatic.AspNetCore.Auditing;
using MJCZone.DapperMatic.AspNetCore.Factories;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Repositories;
using MJCZone.DapperMatic.AspNetCore.Tests.Factories;
using MJCZone.DapperMatic.AspNetCore.Tests.Infrastructure;
using MJCZone.DapperMatic.Models;
using Microsoft.AspNetCore.Http;
using MJCZone.DapperMatic.AspNetCore.Services;
using MJCZone.DapperMatic.Dml;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Endpoints;

/// <summary>
/// Policies and tenants kept by DapperMatic and edited through its access endpoints: they apply to the next request,
/// on every instance sharing the store, and an administrator of some datasources keeps policies for those only.
/// </summary>
public class AccessEndpointsTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private readonly TestcontainersAssemblyFixture _fixture;

    public AccessEndpointsTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Policies_kept_through_the_API_apply_to_the_next_request_on_every_instance_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        using var factory = Host(datasource, DmPolicy.Allow(DmAction.All, "access-admin").Named("admins"));
        using (var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!))
        {
            db.Open();
            await db.DropTableIfExistsAsync(null, "acc_items");
            await db.CreateTableIfNotExistsAsync(
                new DmTable(null, "acc_items", [new DmColumn("id", typeof(int), isPrimaryKey: true), new DmColumn("tenant_id", typeof(int)), new DmColumn("name", typeof(string), length: 20)])
            );
            await Dapper.SqlMapper.ExecuteAsync(db, "INSERT INTO acc_items (id, tenant_id, name) VALUES (1, 1, 'a'), (2, 2, 'b'), (3, 1, 'c')");
        }

        // Each client is a host of its own, sharing the store: as instances of an application behind a load balancer.
        using var admin = factory.CreateAuthenticatedClient(new Claim(ClaimTypes.Role, "access-admin"));
        using var reader = factory.CreateAuthenticatedClient(new Claim(ClaimTypes.Role, "reader"));
        using var anonymous = factory.CreateClient();
        var query = $"/api/dm/d/{datasource.Id}/t/acc_items/query?$orderby=id";

        (await reader.GetAsync(query)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var readers = new { actions = "Read, ReadSchema", roles = new[] { "reader" }, tables = new[] { "acc_items" }, filter = "tenant_id eq 1" };
        (await Put(admin, "policies/readers", readers)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Ids(reader, query)).Should().Equal(1, 3);

        // Refused: a policy that cannot work, one named as a policy of code, and callers who do not manage access.
        (await Put(admin, "policies/bad", new { actions = "Read", roles = new[] { "x" }, filter = "customer/country eq 'FR'" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Put(admin, "policies/nobody", new { actions = "Read" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Put(admin, "policies/admins", readers)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Put(reader, "policies/mine", readers)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Put(anonymous, "policies/mine", readers)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // The list shows where each policy comes from.
        var listed = await Result(admin, "policies");
        listed.EnumerateArray().Select(p => (p.GetProperty("name").GetString(), p.GetProperty("source").GetString()))
            .Should().BeEquivalentTo([("admins", "code"), ("readers", "stored")]);
        (await Result(admin, "policies/readers")).GetProperty("actions").GetString().Should().Be("Read, ReadSchema");

        // Effective permissions: what the caller may do; the conditions only for one who manages access.
        var effective = await Result(reader, $"effective?datasourceId={datasource.Id}&tableName=acc_items");
        effective.GetProperty("actions").GetProperty("Read").GetProperty("allowed").GetBoolean().Should().BeTrue();
        effective.GetProperty("actions").GetProperty("Create").GetProperty("allowed").GetBoolean().Should().BeFalse();
        effective.GetProperty("actions").GetProperty("Read").GetProperty("filter").ValueKind.Should().Be(JsonValueKind.Null);
        var asAdmin = await Result(admin, $"effective?datasourceId={datasource.Id}&tableName=acc_items");
        asAdmin.GetProperty("actions").GetProperty("Read").GetProperty("policies").EnumerateArray().Select(p => p.GetString()).Should().Equal("admins");

        // Export, then import the same into an empty store; removing the policy takes the grant away.
        var exported = await (await admin.GetAsync("/api/dm/access/export")).Content.ReadAsStringAsync();
        (await admin.DeleteAsync("/api/dm/access/policies/readers")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.DeleteAsync("/api/dm/access/policies/readers")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await reader.GetAsync(query)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.PostAsync("/api/dm/access/import?replace=true", new StringContent(exported, Encoding.UTF8, "application/json"))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Ids(reader, query)).Should().Equal(1, 3);
    }

    [Fact]
    public async Task An_administrator_of_some_datasources_keeps_policies_and_tenants_for_those_only_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        using var factory = Host(
            datasource,
            DmPolicy.Allow(DmAction.All, "access-admin").Named("admins"),
            DmPolicy.Allow(DmAction.ManageAccess | DmAction.ReadSchema, "tenant-admin").Named("tenant a's administrators").OnDatasources("ds-a")
        ).WithWebHostBuilder(web => web.ConfigureServices(services => services.AddDapperMatic(dm => dm.Tenancy(t => t.Map("victim", datasource: "ds-b")))));
        using var tenantAdmin = factory.CreateAuthenticatedClient(new Claim(ClaimTypes.Role, "tenant-admin"));
        using var admin = factory.CreateAuthenticatedClient(new Claim(ClaimTypes.Role, "access-admin"));

        object Policy(params string[] datasources) => new { actions = "Read", roles = new[] { "staff" }, datasources };
        (await Put(tenantAdmin, "policies/a-staff", Policy("ds-a"))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Put(tenantAdmin, "policies/everywhere", Policy())).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Put(tenantAdmin, "policies/pattern", Policy("ds-*"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Put(tenantAdmin, "policies/b-staff", Policy("ds-b"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Put(admin, "policies/b-staff", Policy("ds-b"))).StatusCode.Should().Be(HttpStatusCode.OK);

        // Nor can it replace or remove a policy reaching further, or see one.
        (await Put(tenantAdmin, "policies/b-staff", Policy("ds-a"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await tenantAdmin.DeleteAsync("/api/dm/access/policies/b-staff")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Result(tenantAdmin, "policies")).EnumerateArray().Select(p => p.GetProperty("name").GetString())
            .Should().BeEquivalentTo("tenant a's administrators", "a-staff");

        // Tenants: in its datasource only.
        (await Put(tenantAdmin, "tenants/t1", new { datasource = "ds-a", schema = "t1" })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Put(tenantAdmin, "tenants/t2", new { datasource = "ds-b" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Put(tenantAdmin, "tenants/t3", new { schema = "t3" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Result(tenantAdmin, "tenants")).EnumerateObject().Select(t => t.Name).Should().Equal("t1");

        // Nor can it override a tenant configuration puts in another datasource.
        (await Put(tenantAdmin, "tenants/victim", new { datasource = "ds-a" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Effective permissions show conditions and policies only for what it manages: not across every datasource.
        var everywhere = await Result(tenantAdmin, "effective");
        everywhere.GetProperty("actions").GetProperty("Read").GetProperty("policies").ValueKind.Should().Be(JsonValueKind.Null);
        var mine = await Result(tenantAdmin, "effective?datasourceId=ds-a");
        mine.GetProperty("actions").GetProperty("Read").GetProperty("policies").ValueKind.Should().Be(JsonValueKind.Array);

        // Importing is for whoever manages everything.
        (await tenantAdmin.PostAsync("/api/dm/access/import", new StringContent("{}", Encoding.UTF8, "application/json"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_tenant_kept_through_the_API_is_held_to_its_schema_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_PostgreSql);
        using var factory = Host(
            datasource,
            DmPolicy.Allow(DmAction.All, "access-admin"),
            DmPolicy.Allow(DmAction.Read, "authenticated").InSchemas("acc_*"),
            DmPolicy.Allow(DmAction.ChangeSchema | DmAction.ReadSchema, "builder")
        ).WithWebHostBuilder(web => web.ConfigureServices(services => services.AddDapperMatic(dm => dm.Tenancy(t => t.FromClaim("tenant_id")))));
        using (var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!))
        {
            db.Open();
            await Dapper.SqlMapper.ExecuteAsync(
                db,
                "DROP SCHEMA IF EXISTS acc_acme CASCADE; CREATE SCHEMA acc_acme; CREATE TABLE acc_acme.acc_things (id int PRIMARY KEY); INSERT INTO acc_acme.acc_things VALUES (7);"
            );
        }

        using var admin = factory.CreateAuthenticatedClient(new Claim(ClaimTypes.Role, "access-admin"));
        using var acme = factory.CreateAuthenticatedClient(new Claim("tenant_id", "acme"));
        var query = $"/api/dm/d/{datasource.Id}/t/acc_things/query";

        (await acme.GetAsync(query)).StatusCode.Should().NotBe(HttpStatusCode.OK);
        (await Put(admin, "tenants/acme", new { schema = "acc_acme" })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Ids(acme, query)).Should().Equal(7);
        (await acme.GetAsync($"/api/dm/d/{datasource.Id}/s/public/t/acc_things/query")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // A schema named in the body or the query is held to the tenant's as one in the URL is.
        using var builder = factory.CreateAuthenticatedClient(new Claim("tenant_id", "acme"), new Claim(ClaimTypes.Role, "builder"));
        var schema = new StringContent(JsonSerializer.Serialize(new { schemaName = "acc_beta" }), Encoding.UTF8, "application/json");
        (await builder.PostAsync($"/api/dm/d/{datasource.Id}/s", schema)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await acme.GetAsync($"/api/dm/access/effective?datasourceId={datasource.Id}&schemaName=public&tableName=acc_things")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// A tenant's administrator keeps policies for their own tenant: held to the tenant's rows, giving no more than
    /// they may do themselves, named apart from other tenants', and never the application's.
    /// </summary>
    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite, null)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql, "public")]
    public async Task A_tenant_administrator_keeps_policies_for_their_own_tenant_Async(string datasourceId, string? schemaName)
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == datasourceId);
        using var factory = Host(
            datasource,
            DmPolicy.Allow(DmAction.All, "platform-admin"),
            DmPolicy.Allow(DmAction.Read | DmAction.Create | DmAction.Update | DmAction.ReadSchema, "tenant-admin")
                .OnTables("tr_invoices")
                .Where("tenant_id eq @tenant.id")
                .ExceptColumns("note"),
            DmPolicy.Allow(DmAction.ManageAccess, "tenant-admin").WithinOwnTenant()
        ).WithWebHostBuilder(web => web.ConfigureServices(services => services.AddDapperMatic(dm => dm.Tenancy(t => t.FromClaim("tenant_id").RowsMarkedBy("tenant_id")))));
        using (var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!))
        {
            db.Open();
            await db.DropTableIfExistsAsync(schemaName, "tr_invoices");
            await db.CreateTableIfNotExistsAsync(
                new DmTable(
                    schemaName,
                    "tr_invoices",
                    [
                        new DmColumn("id", typeof(int), isPrimaryKey: true),
                        new DmColumn("tenant_id", typeof(int), isNullable: true),
                        new DmColumn("amount", typeof(int)),
                        new DmColumn("note", typeof(string), length: 50, isNullable: true),
                    ]
                )
            );
            await Dapper.SqlMapper.ExecuteAsync(
                db,
                $"INSERT INTO {db.GetSchemaQualifiedTableName(schemaName, "tr_invoices")} (id, tenant_id, amount, note) VALUES (1, 1, 10, 'a'), (2, 1, 20, 'b'), (3, 2, 30, 'c'), (4, NULL, 40, 'platform')"
            );
        }

        HttpClient Client(string role, string? tenant) =>
            factory.CreateAuthenticatedClient([new Claim(ClaimTypes.Role, role), .. tenant is null ? Array.Empty<Claim>() : [new Claim("tenant_id", tenant)]]);
        using var admin1 = Client("tenant-admin", "1");
        using var admin2 = Client("tenant-admin", "2");
        using var clerk1 = Client("clerk", "1");
        using var clerk2 = Client("clerk", "2");
        using var platform = Client("platform-admin", null);
        var query = $"/api/dm/d/{datasource.Id}/t/tr_invoices/query?$orderby=id";
        object Clerks(string actions, params string[] columns) =>
            new { actions, roles = new[] { "clerk" }, datasources = new[] { datasource.Id }, tables = new[] { "tr_invoices" }, columns };

        // Tenant 1's administrator lets its clerks read and edit invoices: tenant 1's rows only, never the platform's.
        var saved = await Put(admin1, "policies/clerks", Clerks("Read, Create, Update", "id", "tenant_id", "amount"));
        saved.StatusCode.Should().Be(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
        var kept = JsonDocument.Parse(await saved.Content.ReadAsStringAsync()).RootElement.GetProperty("result");
        kept.GetProperty("tenant").GetString().Should().Be("1");
        if (schemaName is not null)
        {
            kept.GetProperty("schemas").EnumerateArray().Select(e => e.GetString()).Should().Equal(schemaName);
        }

        (await Ids(clerk1, query)).Should().Equal(1, 2);
        (await clerk1.GetAsync($"{query}&$select=note")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await clerk2.GetAsync(query)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var update = new StringContent(JsonSerializer.Serialize(new { set = new { amount = 99 }, filter = "true", dryRun = true }), Encoding.UTF8, "application/json");
        var updated = await clerk1.PostAsync($"/api/dm/d/{datasource.Id}/t/tr_invoices/rows/update", update);
        JsonDocument.Parse(await updated.Content.ReadAsStringAsync()).RootElement.GetProperty("affected").GetInt32().Should().Be(2);

        // A row a clerk adds is the tenant's, whatever it says.
        var insert = new StringContent(JsonSerializer.Serialize(new { rows = new object[] { new { id = 5, tenant_id = 2, amount = 5 }, new { id = 6, amount = 6 } } }), Encoding.UTF8, "application/json");
        (await clerk1.PostAsync($"/api/dm/d/{datasource.Id}/t/tr_invoices/rows", insert)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Ids(clerk1, query)).Should().Equal(1, 2, 5, 6);

        // No more than the administrator has: no delete, no kept column, no columns left unnamed, no anonymous callers,
        // no patterns, nothing reaching a whole datasource.
        (await Put(admin1, "policies/deleters", Clerks("Delete", "id"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Put(admin1, "policies/notes", Clerks("Read", "id", "note"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Put(admin1, "policies/everything", Clerks("Read"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Put(admin1, "policies/public", new { actions = "Read", roles = new[] { "*" }, datasources = new[] { datasource.Id }, tables = new[] { "tr_invoices" }, columns = new[] { "id" } }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Put(admin1, "policies/pattern", new { actions = "Read", roles = new[] { "clerk" }, datasources = new[] { datasource.Id }, tables = new[] { "tr_*" }, columns = new[] { "id" } }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Put(admin1, "policies/views", new { actions = "DefineViews", roles = new[] { "clerk" }, datasources = new[] { datasource.Id } })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Each tenant names its own: tenant 2's clerks are tenant 2's, and each administrator sees only theirs.
        (await Put(admin2, "policies/clerks", Clerks("Read", "id", "amount"))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Ids(clerk2, query)).Should().Equal(3);
        (await Ids(clerk1, query)).Should().Equal(1, 2, 5, 6);
        (await Result(admin1, "policies")).EnumerateArray().Select(p => (p.GetProperty("name").GetString(), p.GetProperty("tenant").GetString()))
            .Should().Equal(("clerks", "1"));

        // A tenant's administrator manages nothing of the application's, nor another tenant's.
        (await Put(admin1, "policies/x?tenant=2", Clerks("Read", "id"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Put(admin1, "tenants/1", new { schema = "x" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin1.PostAsync("/api/dm/access/import", new StringContent("{}", Encoding.UTF8, "application/json"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // It may make sub-administrators of its own tenant.
        (await Put(admin1, "policies/sub-admins", new { actions = "ManageAccess", roles = new[] { "sub-admin" } })).StatusCode.Should().Be(HttpStatusCode.OK);
        using var sub1 = Client("sub-admin", "1");
        (await Result(sub1, "policies")).EnumerateArray().Select(p => p.GetProperty("name").GetString()).Should().BeEquivalentTo("clerks", "sub-admins");
        (await Put(sub1, "policies/x", new { actions = "Read", roles = new[] { "clerk" }, tables = new[] { "tr_invoices" } })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // A platform administrator sees every tenant's, and manages them by naming the tenant.
        (await Result(platform, "policies")).EnumerateArray().Where(p => p.GetProperty("tenant").ValueKind == JsonValueKind.String)
            .Select(p => (p.GetProperty("name").GetString(), p.GetProperty("tenant").GetString()))
            .Should().BeEquivalentTo([("clerks", "1"), ("clerks", "2"), ("sub-admins", "1")]);
        (await platform.DeleteAsync("/api/dm/access/policies/clerks?tenant=2")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await clerk2.GetAsync(query)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin1.DeleteAsync("/api/dm/access/policies/clerks")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await clerk1.GetAsync(query)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Application_code_reads_and_writes_as_the_request_caller_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        using var factory = Host(
            datasource,
            DmPolicy.Allow(DmAction.ReadWrite, "authenticated").OnTables("cdb_items").Where("tenant_id eq @tenant.id").Preset("tenant_id", "@tenant.id")
        ).WithWebHostBuilder(web => web.ConfigureServices(services => services.AddDapperMatic(dm => dm.Tenancy(t => t.FromClaim("tenant_id").Map("elsewhere", datasource: "other")))));
        using (var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!))
        {
            db.Open();
            await db.DropTableIfExistsAsync(null, "cdb_items");
            await db.CreateTableIfNotExistsAsync(DmTableFactory.GetTable(typeof(CallerDbItem)));
            await Dapper.SqlMapper.ExecuteAsync(db, "INSERT INTO cdb_items (id, tenant_id, name) VALUES (1, 1, 'a'), (2, 2, 'b')");
        }

        async Task<DmScope> OpenAs(IServiceScope scope, string tenant)
        {
            var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
            accessor.HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("tenant_id", tenant)], "Test")),
                RequestServices = scope.ServiceProvider,
            };
            return await scope.ServiceProvider.GetRequiredService<DapperMaticCallerDb>().OpenAsync(datasource.Id!);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = await OpenAs(scope, "1");
            Assert.Equal([1], (await db.SelectAsync<CallerDbItem>()).Select(i => i.Id));
            await db.InsertAsync(new CallerDbItem { Id = 3, TenantId = 2, Name = "c" });
            Assert.Equal([1, 3], (await db.SelectAsync<CallerDbItem>()).Select(i => i.Id).Order());
            Assert.Equal(0, await db.DeleteByIdAsync<CallerDbItem>(2));
        }

        // A request held to another datasource cannot open this one.
        using (var scope = factory.Services.CreateScope())
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => OpenAs(scope, "elsewhere"));
        }
    }

    [Fact]
    public async Task Audit_events_name_the_tenant_and_the_claims_asked_for_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        var audit = new TestDapperMaticAuditLogger();
        using var factory = Host(datasource, DmPolicy.Allow(DmAction.ReadSchema, "authenticated")).WithWebHostBuilder(web =>
            web.ConfigureServices(services =>
            {
                services.AddDapperMatic(dm => dm.Tenancy(t => t.FromClaim("tenant_id")).AuditClaims("grant_id"));
                services.RemoveAll<IDapperMaticAuditLogger>();
                services.AddSingleton<IDapperMaticAuditLogger>(audit);
            })
        );

        // A platform administrator working in tenant 42 under grant 17.
        using var admin = factory.CreateAuthenticatedClient(new Claim("tenant_id", "42"), new Claim("grant_id", "17"), new Claim(ClaimTypes.Role, "platform-admin"));
        (await admin.GetAsync($"/api/dm/d/{datasource.Id}/t")).StatusCode.Should().Be(HttpStatusCode.OK);

        var logged = audit.GetLastEvent();
        logged.Should().NotBeNull();
        logged!.TenantId.Should().Be("42");
        logged.Claims.Should().Equal(new Dictionary<string, string> { ["grant_id"] = "17" });
    }

    [Fact]
    public async Task A_change_is_checked_against_what_is_kept_now_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        using var factory = Host(
            datasource,
            TimeSpan.FromHours(1),
            DmPolicy.Allow(DmAction.All, "access-admin"),
            DmPolicy.Allow(DmAction.ManageAccess, "tenant-admin").OnDatasources("ds-a")
        );

        // Two instances: the second read the store before the first added a policy reaching another datasource.
        using var tenantAdmin = factory.CreateAuthenticatedClient(new Claim(ClaimTypes.Role, "tenant-admin"));
        (await Result(tenantAdmin, "policies")).GetArrayLength().Should().Be(1);
        using var admin = factory.CreateAuthenticatedClient(new Claim(ClaimTypes.Role, "access-admin"));
        (await Put(admin, "policies/b-staff", new { actions = "Read", roles = new[] { "staff" }, datasources = new[] { "ds-b" } })).StatusCode.Should().Be(HttpStatusCode.OK);

        (await Put(tenantAdmin, "policies/b-staff", new { actions = "Read", roles = new[] { "staff" }, datasources = new[] { "ds-a" } })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite)]
    public async Task The_database_repository_keeps_policies_and_tenants_Async(string datasourceId)
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == datasourceId);
        using var factory = new WafWithInMemoryDatasourceRepository([datasource]);
        var connections = factory.Services.GetRequiredService<IDbConnectionFactory>();
        using (var db = connections.CreateConnection(datasource.Provider!, datasource.ConnectionString!))
        {
            db.Open();
            await db.DropTableIfExistsAsync(null, "acc_store");
        }

        IDapperMaticAccessRepository[] repositories =
        [
            new DatabaseDapperMaticAccessRepository(datasource.Provider!, datasource.ConnectionString!, connections, tableName: "acc_store"),
            new FileDapperMaticAccessRepository(Path.Combine(Path.GetTempPath(), $"dm-access-{Guid.NewGuid():N}.json")),
            new InMemoryDapperMaticAccessRepository(),
        ];

        foreach (var repository in repositories)
        {
            await repository.SavePolicyAsync(DmPolicy.Allow(DmAction.Read | DmAction.Create, "staff").Named("p1").OnTablesWithColumns("tenant_id").Where("tenant_id eq @tenant.id").Preset("tenant_id", "@tenant.id"));
            await repository.SavePolicyAsync(DmPolicy.Restrict(DmAction.ReadWrite).Named("p2").ExceptColumns("secret"));
            await repository.SavePolicyAsync(DmPolicy.Allow(DmAction.Read, "staff").Named("p1").Where("tenant_id eq 9"));
            var tenant = new DapperMaticTenantSettings { Datasource = "ds", Schema = "s" };
            tenant.Values["region"] = "eu";
            await repository.SaveTenantAsync("acme", tenant);

            var document = await repository.GetAsync();
            document.Policies.Select(p => p.Name).Should().BeEquivalentTo("p1", "p2");
            document.Policies.Single(p => p.Name == "p1").Filter.Should().Be("tenant_id eq 9");
            document.Policies.Single(p => p.Name == "p2").Kind.Should().Be(DmPolicyKind.Restrictive);
            document.Policies.Single(p => p.Name == "p2").ExcludeColumns.Should().Equal("secret");
            document.Tenants["acme"].Values["region"].Should().Be("eu");

            (await repository.DeletePolicyAsync("p2")).Should().BeTrue();
            (await repository.DeletePolicyAsync("p2")).Should().BeFalse();
            (await repository.DeleteTenantAsync("acme")).Should().BeTrue();

            var replacement = new DapperMaticAccessDocument();
            replacement.Policies.Add(DmPolicy.Allow(DmAction.All, "root").Named("only"));
            await repository.ReplaceAsync(replacement);
            (await repository.GetAsync()).Policies.Select(p => p.Name).Should().Equal("only");
        }
    }

    // A host with one datasource and these policies only, whose access store is shared by every client made from it
    // and read again on each request.
    private static WebApplicationFactory<Program> Host(Models.Dtos.DatasourceDto datasource, params DmPolicy[] policies) =>
        Host(datasource, TimeSpan.Zero, policies);

    private static WebApplicationFactory<Program> Host(Models.Dtos.DatasourceDto datasource, TimeSpan refresh, params DmPolicy[] policies)
    {
        var store = new InMemoryDapperMaticAccessRepository();
        return new WafWithInMemoryDatasourceRepository([datasource]).WithWebHostBuilder(web =>
            web.ConfigureServices(services =>
            {
                services.Configure<DapperMaticOptions>(options =>
                {
                    options.Policies.Clear();
                    options.AccessRefreshInterval = refresh;
                });
                services.AddSingleton<IDapperMaticAccessRepository>(store);
                services.AddDapperMatic(dm => dm.Policies(policies));
            })
        );
    }

    private static Task<HttpResponseMessage> Put(HttpClient client, string path, object body) =>
        client.PutAsync($"/api/dm/access/{path}", new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));

    private static async Task<JsonElement> Result(HttpClient client, string path)
    {
        var response = await client.GetAsync($"/api/dm/access/{path}");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, text);
        return JsonDocument.Parse(text).RootElement.GetProperty("result");
    }

    private static async Task<int[]> Ids(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, text);
        return [.. JsonDocument.Parse(text).RootElement.GetProperty("result").EnumerateArray().Select(r => r.GetProperty("id").GetInt32())];
    }

    [MJCZone.DapperMatic.DataAnnotations.DmTable(tableName: "cdb_items")]
    public sealed class CallerDbItem
    {
        [MJCZone.DapperMatic.DataAnnotations.DmColumn("id", isPrimaryKey: true)]
        public int Id { get; set; }

        [MJCZone.DapperMatic.DataAnnotations.DmColumn("tenant_id")]
        public int TenantId { get; set; }

        [MJCZone.DapperMatic.DataAnnotations.DmColumn("name", length: 20)]
        public string Name { get; set; } = string.Empty;
    }
}
