// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Dapper;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MJCZone.DapperMatic.AspNetCore.Factories;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Tests.Factories;
using MJCZone.DapperMatic.AspNetCore.Tests.Infrastructure;
using Npgsql;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Endpoints;

/// <summary>
/// Tenants found in requests and held to their data, as products really keep it: a key on rows with global rows and a
/// tree of tenants, enforced by the database's own row-level security; and a schema per tenant.
/// </summary>
public class TenancyTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private readonly TestcontainersAssemblyFixture _fixture;

    public TenancyTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Rows keyed by tenant, with global rows (no tenant) every tenant sees, and a tree of tenants where each sees its
    /// own rows and those below it, never above. PostgreSQL's row-level security enforces it, reading the ids below the
    /// request's tenant from the session, where DapperMatic puts them.
    /// </summary>
    [Fact]
    public async Task Database_row_level_security_sees_the_tenants_rows_global_rows_and_the_tenants_below_Async()
    {
        var admin = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_PostgreSql);
        await using (var db = new NpgsqlConnection(admin.ConnectionString))
        {
            await db.OpenAsync();
            await db.ExecuteAsync(
                """
                DROP TABLE IF EXISTS ten_notes;
                DROP TABLE IF EXISTS ten_tree;
                CREATE TABLE ten_tree (id int PRIMARY KEY, path text NOT NULL);
                INSERT INTO ten_tree VALUES (1, '1/'), (2, '1/2/'), (3, '1/2/3/'), (4, '1/4/');
                CREATE TABLE ten_notes (id int PRIMARY KEY, tenant_id int NULL, body text NOT NULL);
                INSERT INTO ten_notes VALUES (1, NULL, 'global'), (2, 1, 'root'), (3, 2, 'a'), (4, 3, 'a1'), (5, 4, 'b');
                ALTER TABLE ten_notes ENABLE ROW LEVEL SECURITY;
                CREATE POLICY ten_notes_tenant ON ten_notes
                    USING (tenant_id IS NULL OR tenant_id = ANY (string_to_array(current_setting('app.below', true), ',')::int[]));
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'dm_tenant_app') THEN
                        CREATE ROLE dm_tenant_app LOGIN PASSWORD 'dm_tenant_app';
                    END IF;
                END $$;
                GRANT USAGE ON SCHEMA public TO dm_tenant_app;
                GRANT SELECT ON ten_notes, ten_tree TO dm_tenant_app;
                """
            );
        }

        // The application connects as a role row-level security applies to (a superuser bypasses it).
        var datasource = new DatasourceDto
        {
            Id = "Test-PostgreSQL-Tenants",
            Provider = "PostgreSql",
            DisplayName = "Tenants",
            ConnectionString = new NpgsqlConnectionStringBuilder(admin.ConnectionString) { Username = "dm_tenant_app", Password = "dm_tenant_app" }.ToString(),
        };
        using var factory = new WafWithInMemoryDatasourceRepository([datasource]).WithWebHostBuilder(web =>
            web.ConfigureServices(services =>
                services.AddDapperMatic(dm => dm
                    .Tenancy(t => t
                        .FromClaim("tenant_id")
                        .ValueFromSql("below", "SELECT id FROM ten_tree WHERE path LIKE (SELECT path FROM ten_tree WHERE id = CAST(@tenant.id AS int)) || '%'"))
                    .SessionValue("app.below", "@tenant.below"))
            )
        );

        async Task<int[]> Visible(string? tenant)
        {
            using var client = tenant is null ? factory.CreateClient() : factory.CreateAuthenticatedClient(new Claim("tenant_id", tenant));
            var response = await client.GetAsync($"/api/dm/d/{datasource.Id}/s/public/t/ten_notes/query?$orderby=id");
            var text = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, text);
            return [.. JsonDocument.Parse(text).RootElement.GetProperty("result").EnumerateArray().Select(r => r.GetProperty("id").GetInt32())];
        }

        (await Visible("2")).Should().Equal(1, 3, 4);       // global, its own, the one below
        (await Visible("1")).Should().Equal(1, 2, 3, 4, 5); // the root sees everything below it
        (await Visible("4")).Should().Equal(1, 5);          // a sibling sees nothing of the other branch
        (await Visible(null)).Should().Equal(1);            // no tenant: global rows only
    }

    /// <summary>
    /// A tenant with a schema of its own: a URL naming no schema uses the tenant's, and one naming another schema or
    /// datasource is refused.
    /// </summary>
    [Fact]
    public async Task A_tenant_with_its_own_schema_uses_it_and_no_other_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_PostgreSql);
        using var factory = new WafWithInMemoryDatasourceRepository([datasource]).WithWebHostBuilder(web =>
            web.ConfigureServices(services =>
                services.AddDapperMatic(dm => dm.Tenancy(t => t.FromClaim("tenant_id").Map("acme", schema: "ten_acme").Map("beta", datasource: "beta-db")))
            )
        );
        using (var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!))
        {
            db.Open();
            await db.ExecuteAsync("DROP SCHEMA IF EXISTS ten_acme CASCADE; CREATE SCHEMA ten_acme; CREATE TABLE ten_acme.ten_items (id int PRIMARY KEY); INSERT INTO ten_acme.ten_items VALUES (7);");
        }

        using var acme = factory.CreateAuthenticatedClient(new Claim("tenant_id", "acme"));
        var own = await acme.GetAsync($"/api/dm/d/{datasource.Id}/t/ten_items/query");
        var text = await own.Content.ReadAsStringAsync();
        own.StatusCode.Should().Be(HttpStatusCode.OK, text);
        JsonDocument.Parse(text).RootElement.GetProperty("result")[0].GetProperty("id").GetInt32().Should().Be(7);

        (await acme.GetAsync($"/api/dm/d/{datasource.Id}/s/public/t/ten_items/query")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var beta = factory.CreateAuthenticatedClient(new Claim("tenant_id", "beta"));
        (await beta.GetAsync($"/api/dm/d/{datasource.Id}/s/public/t/ten_items/query")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
