// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MJCZone.DapperMatic.AspNetCore.Authentication;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Tests.GraphQL;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Authentication;

/// <summary>
/// API keys: service accounts' keys sign in as them; personal keys as their owners as they are now; a key's limits hold
/// whatever its owner may do; a key names its tenant; keys never make keys; tenant administrators give no more than
/// they may.
/// </summary>
public class ApiKeyTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private readonly TestcontainersAssemblyFixture _fixture;

    public ApiKeyTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task A_service_accounts_key_signs_in_as_it_until_revoked_Async()
    {
        var (factory, datasource) = await HostAsync(dm => dm.Policies(DmPolicy.Allow(DmAction.All, "admin"), DmPolicy.Allow(DmAction.Read, "importer")));
        using var host = factory;
        using var admin = GraphQLTestHost.Client(factory);
        (await admin.PutAsJsonAsync("/api/dm/access/service-accounts/sync", new { roles = new[] { "importer" } })).StatusCode.Should().Be(HttpStatusCode.OK);
        var (key, id) = await ServiceKeyAsync(admin, "sync", new { name = "nightly" });

        var query = $"/api/dm/d/{datasource.Id}/t/gq_orders/query";
        (await KeyClient(factory, key).GetAsync(query)).StatusCode.Should().Be(HttpStatusCode.OK);
        using (var header = factory.CreateClient())
        {
            header.DefaultRequestHeaders.Add("X-Api-Key", key);
            (await header.GetAsync(query)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Its policies are its roles': reading, not writing.
        (await KeyClient(factory, key).PostAsJsonAsync($"/api/dm/d/{datasource.Id}/t/gq_orders/rows", new { rows = new[] { new { total = 1, status = "x" } } }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // A wrong secret, an unknown key: refused, never no one.
        (await KeyClient(factory, key[..^4] + "aaaa").GetAsync(query)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Listed without its secret; revoked, it no longer signs in.
        var listed = await admin.GetStringAsync("/api/dm/access/keys");
        listed.Should().Contain(id).And.NotContain(key.Split('_')[2]).And.NotContain("secretHash");
        (await admin.DeleteAsync($"/api/dm/access/keys/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await KeyClient(factory, key).GetAsync(query)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // A key's limits are the most it may do: read-only, or some tables; with the application's own permissions too.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_keys_limits_hold_whatever_its_owner_may_do_Async(bool customPermissions)
    {
        // With the application's own permissions and no policy about the tables, code decides: the limits still hold.
        var (factory, datasource) = await HostAsync(dm =>
        {
            if (customPermissions)
            {
                dm.UseCustomPermissions(_ => new AllowEverything());
            }
            else
            {
                dm.Policies(DmPolicy.Allow(DmAction.All, "admin"));
            }
        });
        using var host = factory;
        using var admin = GraphQLTestHost.Client(factory);
        (await admin.PutAsJsonAsync("/api/dm/access/service-accounts/ops", new { roles = new[] { "admin" } })).EnsureSuccessStatusCode();
        var (reader, _) = await ServiceKeyAsync(admin, "ops", new { readOnly = true });
        var (customers, _) = await ServiceKeyAsync(admin, "ops", new { tables = new[] { "gq_customers" } });

        var rows = $"/api/dm/d/{datasource.Id}/t/gq_orders/rows";
        var insert = new { rows = new[] { new { total = 1, status = "open" } } };
        (await KeyClient(factory, reader).GetAsync($"/api/dm/d/{datasource.Id}/t/gq_orders/query")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await KeyClient(factory, reader).PostAsJsonAsync(rows, insert)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await KeyClient(factory, customers).GetAsync($"/api/dm/d/{datasource.Id}/t/gq_orders/query")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await KeyClient(factory, customers).GetAsync($"/api/dm/d/{datasource.Id}/t/gq_customers/query")).StatusCode.Should().Be(HttpStatusCode.OK);

        // Changing a schema is not reading.
        (await KeyClient(factory, reader).DeleteAsync($"/api/dm/d/{datasource.Id}/t/gq_orders")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // A key's limits only take away: presets in them are refused, as are limits beside shortcuts (which would widen
    // them); a key confined to some datasources, schemas or tables manages no access; ids of no key are not kept.
    [Fact]
    public async Task A_keys_limits_only_take_away_Async()
    {
        var (factory, datasource) = await HostAsync(dm => dm.Policies(DmPolicy.Allow(DmAction.All, "admin")));
        using var host = factory;
        using var admin = GraphQLTestHost.Client(factory);
        (await admin.PutAsJsonAsync("/api/dm/access/service-accounts/ops", new { roles = new[] { "admin" } })).EnsureSuccessStatusCode();
        var preset = new { limits = new[] { new { actions = "All", roles = new[] { "*" }, presets = new Dictionary<string, string> { ["status"] = "approved" } } } };
        (await admin.PostAsJsonAsync("/api/dm/access/service-accounts/ops/keys", preset)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var both = new { readOnly = true, limits = new[] { new { actions = "Read", roles = new[] { "*" }, tables = new[] { "gq_orders" } } } };
        (await admin.PostAsJsonAsync("/api/dm/access/service-accounts/ops/keys", both)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var policy = new { actions = "Read", roles = new[] { "viewer" } };
        var (confined, _) = await ServiceKeyAsync(admin, "ops", new { datasources = new[] { datasource.Id } });
        (await KeyClient(factory, confined).PutAsJsonAsync("/api/dm/access/policies/viewers", policy)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var (whole, _) = await ServiceKeyAsync(admin, "ops", new { name = "whole" });
        using var saved = await KeyClient(factory, whole).PutAsJsonAsync("/api/dm/access/policies/viewers", policy);
        saved.StatusCode.Should().Be(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());

        var keys = factory.Services.GetRequiredService<DapperMaticApiKeyService>();
        var kept = keys.Cached;
        for (var i = 0; i < 5; i++)
        {
            var made = DapperMaticApiKeyFormat.Create("dm").Key;
            (await KeyClient(factory, made).GetAsync($"/api/dm/d/{datasource.Id}/t/gq_orders/query")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        keys.Cached.Should().Be(kept);
    }

    // A key's caller is in the key's tenant: the request's headers do not choose another.
    [Fact]
    public async Task A_key_names_its_tenant_and_the_request_cannot_choose_another_Async()
    {
        string? id = null;
        var (factory, datasource) = await HostAsync(
            dm => dm.Policies(DmPolicy.Allow(DmAction.All, "admin")).Tenancy(t => t.FromHeader("X-Tenant").Map("7", datasource: id).Map("8", datasource: "elsewhere")),
            ds => id = ds.Id
        );
        using var host = factory;
        using var admin = GraphQLTestHost.Client(factory);
        (await admin.PutAsJsonAsync("/api/dm/access/service-accounts/sync?tenant=7", new { roles = new[] { "admin" } })).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync("/api/dm/access/service-accounts/sync?tenant=8", new { roles = new[] { "admin" } })).EnsureSuccessStatusCode();
        var (seven, _) = await ServiceKeyAsync(admin, "sync", new { }, "7");
        var (eight, _) = await ServiceKeyAsync(admin, "sync", new { }, "8");

        var query = $"/api/dm/d/{datasource.Id}/t/gq_orders/query";
        using var asSeven = KeyClient(factory, seven);
        asSeven.DefaultRequestHeaders.Add("X-Tenant", "8");
        (await asSeven.GetAsync(query)).StatusCode.Should().Be(HttpStatusCode.OK);
        using var asEight = KeyClient(factory, eight);
        asEight.DefaultRequestHeaders.Add("X-Tenant", "7");
        (await asEight.GetAsync(query)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // A personal key is its owner as they are now: roles taken away, the key loses them; the owner gone, it stops. Keys
    // never make keys, and one's own keys are listed and revoked.
    [Fact]
    public async Task Personal_keys_act_as_their_owners_as_they_are_now_Async()
    {
        var owners = new ConcurrentDictionary<string, string[]>(StringComparer.Ordinal) { ["ann"] = ["clerk"] };
        var (factory, datasource) = await HostAsync(
            dm => dm.Policies(DmPolicy.Allow(DmAction.Read, "clerk")).AddApiKeys(k =>
                k.ResolveOwner = (owner, _, _, _) =>
                    ValueTask.FromResult(owners.TryGetValue(owner, out var roles) ? new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, owner), new(ClaimTypes.Name, owner), .. roles.Select(r => new Claim(ClaimTypes.Role, r))], "Owners")) : null)
            )
        );
        using var host = factory;
        using var ann = GraphQLTestHost.Client(factory, "clerk", name: "ann");
        using var created = await ann.PostAsJsonAsync("/api/dm/auth/keys", new { name = "laptop" });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var key = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("result").GetProperty("key").GetString()!;

        var query = $"/api/dm/d/{datasource.Id}/t/gq_orders/query";
        (await KeyClient(factory, key).GetAsync(query)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await KeyClient(factory, key).PostAsJsonAsync("/api/dm/auth/keys", new { name = "more" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        owners["ann"] = [];
        (await KeyClient(factory, key).GetAsync(query)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        owners.TryRemove("ann", out _);
        (await KeyClient(factory, key).GetAsync(query)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        owners["ann"] = ["clerk"];

        (await ann.GetStringAsync("/api/dm/auth/keys")).Should().Contain("laptop");
        (await ann.DeleteAsync("/api/dm/auth/keys")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await KeyClient(factory, key).GetAsync(query)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Personal_keys_need_a_way_to_find_their_owners_Async()
    {
        var (factory, _) = await HostAsync(dm => dm.Policies(DmPolicy.Allow(DmAction.Read, "clerk")));
        using var host = factory;
        using var ann = GraphQLTestHost.Client(factory, "clerk", name: "ann");
        using var refused = await ann.PostAsJsonAsync("/api/dm/auth/keys", new { name = "laptop" });
        refused.IsSuccessStatusCode.Should().BeFalse();
        (await refused.Content.ReadAsStringAsync()).Should().Contain("ResolveOwner");
    }

    // A tenant's administrator gives a service account only roles they hold, or roles no policy of the whole application
    // names, and no claims; those who manage only some datasources manage no service accounts.
    [Fact]
    public async Task Tenant_administrators_give_no_more_than_they_may_Async()
    {
        var (factory, datasource) = await HostAsync(dm => dm
            .Policies(
                DmPolicy.Allow(DmAction.All, "admin"),
                DmPolicy.Allow(DmAction.All, "owner"),
                DmPolicy.Allow(DmAction.ManageAccess, "tenant-admin").WithinOwnTenant(),
                DmPolicy.Allow(DmAction.Read, "clerk").WithinOwnTenant()
            )
            .Tenancy(t => t.FromClaim("tenant_id")));
        using var host = factory;
        using var tenantAdmin = GraphQLTestHost.Client(factory, "tenant-admin", tenant: "7");
        (await tenantAdmin.PutAsJsonAsync("/api/dm/access/service-accounts/sync", new { roles = new[] { "owner" } })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // A role no policy names may still mean something to the application itself: not theirs to give. The tenant's
        // own roles are.
        (await tenantAdmin.PutAsJsonAsync("/api/dm/access/service-accounts/sync", new { roles = new[] { "SuperAdmin" } })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await tenantAdmin.PutAsJsonAsync("/api/dm/access/service-accounts/clerks", new { roles = new[] { "clerk" } })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await tenantAdmin.PutAsJsonAsync("/api/dm/access/service-accounts/sync", new { roles = new[] { "tenant-admin" }, claims = new { region = new[] { "eu" } } }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var held = await tenantAdmin.PutAsJsonAsync("/api/dm/access/service-accounts/sync", new { roles = new[] { "tenant-admin" } });
        held.StatusCode.Should().Be(HttpStatusCode.OK, await held.Content.ReadAsStringAsync());
        (await held.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("result").GetProperty("tenant").GetString().Should().Be("7");
    }

    [Fact]
    public async Task Managers_of_some_datasources_manage_no_service_accounts_Async()
    {
        string? id = null;
        var (factory, _) = await HostAsync(dm => dm.Policies(DmPolicy.Allow(DmAction.ManageAccess, "dsadmin").OnDatasources(id!)), ds => id = ds.Id);
        using var host = factory;
        using var dsAdmin = GraphQLTestHost.Client(factory, "dsadmin");
        (await dsAdmin.PutAsJsonAsync("/api/dm/access/service-accounts/sync", new { roles = new[] { "dsadmin" } })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // When a key was last used is kept (to within a minute); a ticket got with a key stops when the key is revoked.
    [Fact]
    public async Task Key_use_is_kept_and_a_tickets_stream_ends_with_its_key_Async()
    {
        var (factory, datasource) = await HostAsync(dm => dm.Policies(DmPolicy.Allow(DmAction.All, "admin")));
        using var host = factory;
        using var admin = GraphQLTestHost.Client(factory);
        (await admin.PutAsJsonAsync("/api/dm/access/service-accounts/ops", new { roles = new[] { "admin" } })).EnsureSuccessStatusCode();
        var (key, id) = await ServiceKeyAsync(admin, "ops", new { });
        var path = $"/api/dm/d/{datasource.Id}/t/gq_orders/subscribe";
        (await KeyClient(factory, key).GetAsync($"/api/dm/d/{datasource.Id}/t/gq_orders/query")).EnsureSuccessStatusCode();

        await factory.Services.GetRequiredService<DapperMaticKeyUseWorker>().FlushAsync(CancellationToken.None);
        (await admin.GetFromJsonAsync<JsonElement>("/api/dm/access/keys")).GetProperty("result").EnumerateArray().Single(k => k.GetProperty("id").GetString() == id)
            .GetProperty("lastUsedAt").ValueKind.Should().Be(JsonValueKind.String);

        using var ticketResponse = await KeyClient(factory, key).PostAsJsonAsync("/api/dm/auth/ticket", new { path });
        var ticket = (await ticketResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("ticket").GetString();
        (await admin.DeleteAsync($"/api/dm/access/keys/{id}")).EnsureSuccessStatusCode();
        (await factory.CreateClient().GetAsync($"{path}?ticket={ticket}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Service accounts are exported to those who manage everything; keys never are.
    [Fact]
    public async Task Service_accounts_are_exported_and_keys_are_not_Async()
    {
        var (factory, _) = await HostAsync(dm => dm.Policies(DmPolicy.Allow(DmAction.All, "admin")));
        using var host = factory;
        using var admin = GraphQLTestHost.Client(factory);
        (await admin.PutAsJsonAsync("/api/dm/access/service-accounts/sync", new { roles = new[] { "importer" } })).EnsureSuccessStatusCode();
        var (key, _) = await ServiceKeyAsync(admin, "sync", new { });
        var exported = await admin.GetStringAsync("/api/dm/access/export");
        exported.Should().Contain("\"sync\"").And.NotContain(key.Split('_')[1]);
    }

    private static HttpClient KeyClient(WebApplicationFactory<Program> factory, string key)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return client;
    }

    private static async Task<(string Key, string Id)> ServiceKeyAsync(HttpClient admin, string account, object request, string? tenant = null)
    {
        using var response = await admin.PostAsJsonAsync($"/api/dm/access/service-accounts/{account}/keys" + (tenant is null ? string.Empty : $"?tenant={tenant}"), request);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var result = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("result");
        return (result.GetProperty("key").GetString()!, result.GetProperty("info").GetProperty("id").GetString()!);
    }

    // A host with API keys on (unless configure turns them on itself), over the GraphQL tests' tables.
    private async Task<(WebApplicationFactory<Program> Factory, Models.Dtos.DatasourceDto Datasource)> HostAsync(
        Action<DapperMaticConfigurationBuilder> configure,
        Action<Models.Dtos.DatasourceDto>? seen = null
    )
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        seen?.Invoke(datasource);
        var factory = GraphQLTestHost.Create(
            datasource,
            dm =>
            {
                configure(dm);
                dm.AddApiKeys();
            },
            options => options.AccessRefreshInterval = TimeSpan.Zero
        ).WithWebHostBuilder(web => web.ConfigureServices(services => services.AddSingleton<MJCZone.DapperMatic.AspNetCore.Repositories.IDapperMaticAccessRepository>(new MJCZone.DapperMatic.AspNetCore.Repositories.InMemoryDapperMaticAccessRepository())));
        await GraphQLTestHost.CreateTablesAsync(factory, datasource, null);
        return (factory, datasource);
    }

    private sealed class AllowEverything : MJCZone.DapperMatic.AspNetCore.Security.IDapperMaticPermissions
    {
        public Task<bool> IsAuthorizedAsync(DapperMaticOperation context) => Task.FromResult(true);
    }
}
