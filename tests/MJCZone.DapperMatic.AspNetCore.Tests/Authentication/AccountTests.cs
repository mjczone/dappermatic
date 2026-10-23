// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MJCZone.DapperMatic.AspNetCore.Accounts;
using MJCZone.DapperMatic.AspNetCore.Authentication;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.AspNetCore.Tests.GraphQL;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Authentication;

/// <summary>
/// The built-in accounts: the first administrator; invitations; memberships, one tenant per sign-in, and switching;
/// grants; refresh, logout and turning accounts off; sign-up; two-factor sign-in; cookies; personal keys of accounts;
/// and what keys and tenant administrators may not do.
/// </summary>
public partial class AccountTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private const string Password = "Correct-horse-9";
    private const string EncryptionKey = "dappermatic-tests-encryption-key-0123456789";
    private readonly TestcontainersAssemblyFixture _fixture;

    public AccountTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    // With no account and no first administrator configured, a setup code is logged: it makes the first administrator,
    // once.
    [Fact]
    public async Task The_setup_code_makes_the_first_administrator_once_Async()
    {
        using var host = await HostAsync();
        using var client = host.Factory.CreateClient();
        var code = host.SetupCode();
        (await client.PostAsJsonAsync("/api/dm/auth/setup", new { code = "WRONG", email = "ops@example.com", password = Password })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PostAsJsonAsync("/api/dm/auth/setup", new { code, email = "ops@example.com", password = "short" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync("/api/dm/auth/setup", new { code, email = "ops@example.com", password = Password })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync("/api/dm/auth/setup", new { code, email = "two@example.com", password = Password })).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var (token, _) = await LoginAsync(host, "ops@example.com");
        var me = await Bearer(host, token).GetFromJsonAsync<JsonElement>("/api/dm/auth/me");
        me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).Should().Equal("admin");
        me.GetProperty("tenant").ValueKind.Should().Be(JsonValueKind.Null);
    }

    // Invited to a tenant, a new account signs in there (its only place); its roles are the membership's.
    [Fact]
    public async Task An_invitation_makes_a_member_who_signs_in_to_their_tenant_Async()
    {
        using var host = await HostAsync();
        var admin = await AdminAsync(host);
        var token = await InviteAsync(host, admin, "ann@example.com", "7", ["clerk"]);

        using var anonymous = host.Factory.CreateClient();
        (await anonymous.PostAsJsonAsync("/api/dm/auth/invitations/accept", new { token, password = Password })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await anonymous.PostAsJsonAsync("/api/dm/auth/invitations/accept", new { token, password = Password })).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var (access, _) = await LoginAsync(host, "ann@example.com");
        var me = await Bearer(host, access).GetFromJsonAsync<JsonElement>("/api/dm/auth/me");
        me.GetProperty("tenant").GetString().Should().Be("7");
        me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).Should().Equal("clerk");

        // The clerk reads orders, in their tenant's datasource.
        (await Bearer(host, access).GetAsync($"/api/dm/d/{host.Datasource.Id}/t/gq_orders/query")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // An account of several tenants signs in to one at a time, and switches; refreshing keeps its tenant, until its
    // membership there goes.
    [Fact]
    public async Task An_account_of_several_tenants_signs_in_to_one_switches_and_refreshes_in_it_Async()
    {
        using var host = await HostAsync();
        var admin = await AdminAsync(host);
        await AcceptAsync(host, await InviteAsync(host, admin, "bo@example.com", "7", ["clerk"]));
        var userId = (await AccountsAsync(host, admin)).Single(a => a.GetProperty("email").GetString() == "bo@example.com").GetProperty("id").GetString();
        (await admin.PutAsJsonAsync($"/api/dm/access/memberships/8/{userId}", new { roles = new[] { "clerk" } })).StatusCode.Should().Be(HttpStatusCode.OK);

        var (noTenant, _) = await LoginAsync(host, "bo@example.com");
        (await Bearer(host, noTenant).GetFromJsonAsync<JsonElement>("/api/dm/auth/me")).GetProperty("tenant").ValueKind.Should().Be(JsonValueKind.Null);

        var (inEight, refresh) = await LoginAsync(host, "bo@example.com", "8");
        (await Bearer(host, inEight).GetFromJsonAsync<JsonElement>("/api/dm/auth/me")).GetProperty("tenant").GetString().Should().Be("8");

        var switched = await (await Bearer(host, inEight).PostAsJsonAsync("/api/dm/auth/switch", new { tenant = "7" })).Content.ReadFromJsonAsync<JsonElement>();
        (await Bearer(host, switched.GetProperty("accessToken").GetString()!).GetFromJsonAsync<JsonElement>("/api/dm/auth/me")).GetProperty("tenant").GetString().Should().Be("7");
        (await Bearer(host, inEight).PostAsJsonAsync("/api/dm/auth/switch", new { tenant = "9" })).IsSuccessStatusCode.Should().BeFalse();

        using var anonymous = host.Factory.CreateClient();
        var refreshed = await (await anonymous.PostAsJsonAsync("/api/dm/auth/refresh", new { refreshToken = refresh })).Content.ReadFromJsonAsync<JsonElement>();
        (await Bearer(host, refreshed.GetProperty("accessToken").GetString()!).GetFromJsonAsync<JsonElement>("/api/dm/auth/me")).GetProperty("tenant").GetString().Should().Be("8");

        // Its roles there changed: the token in it ends; a refresh gives the new ones.
        (await admin.PutAsJsonAsync($"/api/dm/access/memberships/8/{userId}", new { roles = new[] { "clerk", "tenant-admin" } })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Bearer(host, inEight).GetAsync("/api/dm/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var renewed = (await (await anonymous.PostAsJsonAsync("/api/dm/auth/refresh", new { refreshToken = refresh })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        (await Bearer(host, renewed).GetFromJsonAsync<JsonElement>("/api/dm/auth/me")).GetProperty("roles").EnumerateArray().Select(r => r.GetString()).Should().Equal("clerk", "tenant-admin");

        // The membership gone: the token in it no longer signs in, nor refreshes.
        (await admin.DeleteAsync($"/api/dm/access/memberships/8/{userId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Bearer(host, renewed).GetAsync("/api/dm/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync("/api/dm/auth/refresh", new { refreshToken = refresh })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // A platform administrator granted a tenant acts there with the grant's roles only, the audit naming the grant;
    // revoked, the grant's sign-in ends.
    [Fact]
    public async Task A_grant_gives_a_tenants_roles_only_and_ends_when_revoked_Async()
    {
        using var host = await HostAsync();
        var admin = await AdminAsync(host);
        var adminId = (await AccountsAsync(host, admin)).Single().GetProperty("id").GetString();
        using var created = await admin.PostAsJsonAsync("/api/dm/access/grants", new { userId = adminId, tenant = "7", roles = new[] { "clerk" }, expiresAt = DateTimeOffset.UtcNow.AddHours(1), reason = "support ticket 42" });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var grantId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("result").GetProperty("id").GetString();

        // Too long a grant is refused.
        (await admin.PostAsJsonAsync("/api/dm/access/grants", new { userId = adminId, tenant = "7", roles = new[] { "clerk" }, expiresAt = DateTimeOffset.UtcNow.AddDays(2), reason = "x" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var (granted, _) = await LoginAsync(host, "ops@example.com", "7");
        var me = await Bearer(host, granted).GetFromJsonAsync<JsonElement>("/api/dm/auth/me");
        me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).Should().Equal("clerk");
        (await Bearer(host, granted).GetAsync($"/api/dm/d/{host.Datasource.Id}/t/gq_orders/query")).StatusCode.Should().Be(HttpStatusCode.OK);
        host.Audit.AuditEvents.Last(e => e.Success && e.Operation == "tables/query").Claims.Should().ContainKey("dm_grant");

        // Revoked, the grant's sign-in ends, though the account has since become a member there with the same roles.
        (await admin.PutAsJsonAsync($"/api/dm/access/memberships/7/{adminId}", new { roles = new[] { "clerk" } })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Bearer(host, granted).GetAsync("/api/dm/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.DeleteAsync($"/api/dm/access/grants/{grantId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Bearer(host, granted).GetAsync("/api/dm/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var (member, _) = await LoginAsync(host, "ops@example.com", "7");
        (await Bearer(host, member).GetFromJsonAsync<JsonElement>("/api/dm/auth/me")).GetProperty("roles").EnumerateArray().Select(r => r.GetString()).Should().Equal("clerk");
    }

    // Logging out ends the sign-in (its tokens and refresh tokens); turning an account off ends all of them.
    [Fact]
    public async Task Logout_and_turning_an_account_off_end_its_sign_ins_Async()
    {
        using var host = await HostAsync();
        var admin = await AdminAsync(host);
        await AcceptAsync(host, await InviteAsync(host, admin, "cy@example.com", "7", ["clerk"]));
        var (first, firstRefresh) = await LoginAsync(host, "cy@example.com");
        var (second, _) = await LoginAsync(host, "cy@example.com");

        (await Bearer(host, first).PostAsync("/api/dm/auth/logout", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Bearer(host, first).GetAsync("/api/dm/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await host.Factory.CreateClient().PostAsJsonAsync("/api/dm/auth/refresh", new { refreshToken = firstRefresh })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Bearer(host, second).GetAsync("/api/dm/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        var id = (await AccountsAsync(host, admin)).Single(a => a.GetProperty("email").GetString() == "cy@example.com").GetProperty("id").GetString();
        (await admin.PostAsync($"/api/dm/access/accounts/{id}/disable", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Bearer(host, second).GetAsync("/api/dm/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await host.Factory.CreateClient().PostAsJsonAsync("/api/dm/auth/login", new { email = "cy@example.com", password = Password })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Sign-up: closed by default; open to a domain, with the email confirmed before signing in; never telling whether
    // an email has an account.
    [Fact]
    public async Task Sign_up_is_by_invitation_unless_opened_and_proves_the_email_Async()
    {
        using (var closed = await HostAsync())
        {
            (await closed.Factory.CreateClient().PostAsJsonAsync("/api/dm/auth/register", new { email = "dee@example.com" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        // Signing up sends the email what makes the account: whoever does not read it chooses no password for it.
        using var open = await HostAsync(accounts: a => a.SignUp.EmailDomains.Add("example.com"));
        using var client = open.Factory.CreateClient();
        (await client.PostAsJsonAsync("/api/dm/auth/register", new { email = "dee@elsewhere.com" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsJsonAsync("/api/dm/auth/register", new { email = "dee@example.com" })).StatusCode.Should().Be(HttpStatusCode.OK);
        var token = open.Emails.Last("dee@example.com", DapperMaticEmailKind.SignUp).Code!;
        (await client.PostAsJsonAsync("/api/dm/auth/invitations/accept", new { token, password = Password })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync("/api/dm/auth/login", new { email = "dee@example.com", password = Password })).StatusCode.Should().Be(HttpStatusCode.OK);

        // Signing up again tells nothing, and sends nothing.
        var sent = open.Emails.Sent.Count();
        (await client.PostAsJsonAsync("/api/dm/auth/register", new { email = "dee@example.com" })).StatusCode.Should().Be(HttpStatusCode.OK);
        open.Emails.Sent.Count().Should().Be(sent);
    }

    // Changing a password keeps the caller's own sign-in and ends the others; a reset, by the code sent to the email,
    // ends every one.
    [Fact]
    public async Task A_password_change_ends_other_sign_ins_and_a_reset_every_one_Async()
    {
        using var host = await HostAsync();
        var admin = await AdminAsync(host);
        await AcceptAsync(host, await InviteAsync(host, admin, "eve@mail.example.com", "7", ["clerk"]));
        var (other, _) = await LoginAsync(host, "eve@mail.example.com");
        var (token, _) = await LoginAsync(host, "eve@mail.example.com");
        using var changed = await Bearer(host, token).PostAsJsonAsync("/api/dm/auth/manage/info", new { oldPassword = Password, newPassword = Password + "x" });
        changed.StatusCode.Should().Be(HttpStatusCode.OK, await changed.Content.ReadAsStringAsync());
        (await Bearer(host, token).GetAsync("/api/dm/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Bearer(host, other).GetAsync("/api/dm/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var client = host.Factory.CreateClient();

        // An address is found case-folded (and, by some databases' collations, without its accents): the code goes to the
        // account's own address, never the one written, which may be another mailbox or domain.
        (await client.PostAsJsonAsync("/api/dm/auth/forgot-password", new { email = "EVE@MAIL.example.com" })).StatusCode.Should().Be(HttpStatusCode.OK);
        host.Emails.Sent.Where(e => e.Kind == DapperMaticEmailKind.ResetPassword).Select(e => e.To).Should().Equal("eve@mail.example.com");
        (await client.PostAsJsonAsync("/api/dm/auth/forgot-password", new { email = "nobody@example.com" })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync("/api/dm/auth/forgot-password", new { email = "eve@mail.example.com" })).StatusCode.Should().Be(HttpStatusCode.OK);
        var code = host.Emails.Last("eve@mail.example.com", DapperMaticEmailKind.ResetPassword).Code;
        (await client.PostAsJsonAsync("/api/dm/auth/reset-password", new { email = "eve@mail.example.com", resetCode = code, newPassword = "Another-horse-7" })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Bearer(host, token).GetAsync("/api/dm/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/dm/auth/login", new { email = "eve@mail.example.com", password = "Another-horse-7" })).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // Two-factor sign-in: turned on with an authenticator's code (the caller's own sign-in going on, every other one
    // ended); signing in then needs one.
    [Fact]
    public async Task Two_factor_sign_in_needs_the_authenticators_code_Async()
    {
        using var host = await HostAsync();
        var admin = await AdminAsync(host);
        await AcceptAsync(host, await InviteAsync(host, admin, "fay@example.com", "7", ["clerk"]));
        var (token, refresh) = await LoginAsync(host, "fay@example.com");
        var (other, _) = await LoginAsync(host, "fay@example.com");
        var key = (await (await Bearer(host, token).PostAsJsonAsync("/api/dm/auth/manage/2fa", new { })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sharedKey").GetString()!;
        (await Bearer(host, other).GetAsync("/api/dm/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
        using var enabled = await Bearer(host, token).PostAsJsonAsync("/api/dm/auth/manage/2fa", new { enable = true, twoFactorCode = Totp(key) });
        enabled.StatusCode.Should().Be(HttpStatusCode.OK, await enabled.Content.ReadAsStringAsync());
        (await enabled.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("recoveryCodes").GetArrayLength().Should().Be(10);
        (await Bearer(host, token).GetAsync("/api/dm/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await host.Factory.CreateClient().PostAsJsonAsync("/api/dm/auth/refresh", new { refreshToken = refresh })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Bearer(host, other).GetAsync("/api/dm/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var client = host.Factory.CreateClient();
        (await client.PostAsJsonAsync("/api/dm/auth/login", new { email = "fay@example.com", password = Password })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/dm/auth/login", new { email = "fay@example.com", password = Password, twoFactorCode = Totp(key) })).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // A cookie signs in as a token does.
    [Fact]
    public async Task A_cookie_signs_in_Async()
    {
        using var host = await HostAsync();
        await AdminAsync(host);
        using var client = host.Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        (await client.PostAsJsonAsync("/api/dm/auth/login?useCookies=true", new { email = "ops@example.com", password = Password })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetFromJsonAsync<JsonElement>("/api/dm/auth/me")).GetProperty("email").GetString().Should().Be("ops@example.com");
    }

    // A personal key of an account acts as it in the key's tenant, while its membership lasts; keys and tenant
    // administrators make no durable grants.
    [Fact]
    public async Task Personal_keys_of_accounts_and_what_keys_and_tenant_administrators_may_not_do_Async()
    {
        using var host = await HostAsync(keys: true);
        var admin = await AdminAsync(host);
        await AcceptAsync(host, await InviteAsync(host, admin, "gil@example.com", "7", ["clerk", "tenant-admin"]));
        var (token, _) = await LoginAsync(host, "gil@example.com");
        using var created = await Bearer(host, token).PostAsJsonAsync("/api/dm/auth/keys", new { name = "laptop" });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var key = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("result").GetProperty("key").GetString()!;
        (await Bearer(host, key).GetAsync($"/api/dm/d/{host.Datasource.Id}/t/gq_orders/query")).StatusCode.Should().Be(HttpStatusCode.OK);

        // A key changes nothing of its owner's account, and invites no one.
        (await Bearer(host, key).PostAsJsonAsync("/api/dm/auth/manage/info", new { newPassword = "Hijacked-horse-1", oldPassword = Password })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Bearer(host, key).PostAsJsonAsync("/api/dm/access/invitations", new { email = "mal@example.com", tenant = "7", roles = new[] { "clerk" } })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // A tenant administrator invites to their tenant only roles they may give; turns no account off; grants nothing.
        // Its token reaches the email only: accepting it makes a confirmed account of the address.
        using (var invited = await Bearer(host, token).PostAsJsonAsync("/api/dm/access/invitations", new { email = "hal@example.com", tenant = "7", roles = new[] { "clerk" } }))
        {
            invited.StatusCode.Should().Be(HttpStatusCode.Created);
            (await invited.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("result").GetProperty("token").ValueKind.Should().Be(JsonValueKind.Null);
        }

        (await Bearer(host, token).PostAsJsonAsync("/api/dm/access/invitations", new { email = "hal@example.com", tenant = "7", roles = new[] { "admin" } })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var adminId = (await AccountsAsync(host, admin)).Single(a => a.GetProperty("email").GetString() == "ops@example.com").GetProperty("id").GetString();
        (await Bearer(host, token).PostAsync($"/api/dm/access/accounts/{adminId}/disable", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // The membership gone, the key stops.
        var gilId = (await AccountsAsync(host, admin)).Single(a => a.GetProperty("email").GetString() == "gil@example.com").GetProperty("id").GetString();
        (await admin.DeleteAsync($"/api/dm/access/memberships/7/{gilId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Bearer(host, key).GetAsync($"/api/dm/d/{host.Datasource.Id}/t/gq_orders/query")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // An application requiring sign-in on DapperMatic's whole route group still lets people sign in: the endpoints that
    // exist for it allow anonymous callers, and DapperMatic's scheme signs the others in.
    [Fact]
    public async Task Signing_in_stays_open_when_the_group_requires_authorization_Async()
    {
        var file = Path.Combine(Path.GetTempPath(), $"dm-accounts-{Guid.NewGuid():N}.db");
        var logs = new Logs();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.AddProvider(logs);
        builder.Services.AddAuthorization();
        builder.Services.AddDapperMatic(dm => dm
            .AddProvider(Microsoft.Data.Sqlite.SqliteFactory.Instance)
            .Policy(DmPolicy.Allow(DmAction.All, "admin"))
            .AddUserAccounts(a => a.UseDatabase("Sqlite", $"Data Source={file}")));
        builder.Services.Configure<DapperMaticOptions>(o => o.ConnectionStringEncryptionKey = EncryptionKey);
        await using var app = builder.Build();
        app.UseAuthorization();
        app.UseDapperMatic(group => group.RequireAuthorization(p => p.AddAuthenticationSchemes(DapperMaticAuthenticationDefaults.Scheme).RequireAuthenticatedUser()));
        await app.StartAsync();
        using var client = app.GetTestClient();
        (await client.GetAsync("/api/dm/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/dm/auth/setup", new { code = logs.SetupCode(), email = "ops@example.com", password = Password })).StatusCode.Should().Be(HttpStatusCode.OK);
        using var login = await client.PostAsJsonAsync("/api/dm/auth/login", new { email = "ops@example.com", password = Password });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await client.GetFromJsonAsync<JsonElement>("/api/dm/auth/me")).GetProperty("email").GetString().Should().Be("ops@example.com");
    }

    // Signing out everywhere, as after a password reset, revokes the account's personal keys too: one made by whoever
    // had the account would outlast its sign-ins.
    [Fact]
    public async Task Signing_out_everywhere_revokes_personal_keys_Async()
    {
        using var host = await HostAsync(keys: true);
        var admin = await AdminAsync(host);
        await AcceptAsync(host, await InviteAsync(host, admin, "ida@example.com", "7", ["clerk"]));
        var (token, _) = await LoginAsync(host, "ida@example.com");
        using var created = await Bearer(host, token).PostAsJsonAsync("/api/dm/auth/keys", new { name = "laptop" });
        var key = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("result").GetProperty("key").GetString()!;
        (await Bearer(host, key).GetAsync("/api/dm/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Bearer(host, token).PostAsync("/api/dm/auth/manage/sign-out-everywhere", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Bearer(host, key).GetAsync("/api/dm/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // A wrong old password counts toward the lockout, which ends the sign-in: a sign-in taken over cannot guess it.
    [Fact]
    public async Task A_wrong_old_password_counts_toward_the_lockout_Async()
    {
        using var host = await HostAsync();
        var admin = await AdminAsync(host);
        await AcceptAsync(host, await InviteAsync(host, admin, "jo@example.com", "7", ["clerk"]));
        var (token, _) = await LoginAsync(host, "jo@example.com");
        for (var i = 0; i < 5; i++)
        {
            (await Bearer(host, token).PostAsJsonAsync("/api/dm/auth/manage/info", new { oldPassword = $"Guess-{i}-horse", newPassword = "Taken-horse-1" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        (await Bearer(host, token).GetAsync("/api/dm/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await host.Factory.CreateClient().PostAsJsonAsync("/api/dm/auth/login", new { email = "jo@example.com", password = Password })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Failed sign-ins at the same time each count: guessing in parallel does not multiply the attempts the lockout allows.
    [Fact]
    public async Task Failed_sign_ins_at_once_each_count_Async()
    {
        using var host = await HostAsync();
        var admin = await AdminAsync(host);
        await AcceptAsync(host, await InviteAsync(host, admin, "kim@example.com", "7", ["clerk"]));
        using var client = host.Factory.CreateClient();
        var guesses = Enumerable.Range(0, 12).Select(i => client.PostAsJsonAsync("/api/dm/auth/login", new { email = "kim@example.com", password = $"Guess-{i}-horse" }));
        foreach (var guess in await Task.WhenAll(guesses))
        {
            guess.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            guess.Dispose();
        }

        (await client.PostAsJsonAsync("/api/dm/auth/login", new { email = "kim@example.com", password = Password })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // The application's own endpoints follow a sign-in's end too: a cookie whose membership is gone is refused (not
    // renewed with the roles it had), and DapperMatic's scheme refuses a token whose membership is gone.
    [Fact]
    public async Task The_applications_own_endpoints_refuse_a_sign_in_whose_place_is_gone_Async()
    {
        var file = Path.Combine(Path.GetTempPath(), $"dm-accounts-{Guid.NewGuid():N}.db");
        var logs = new Logs();
        var emails = new Emails();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.AddProvider(logs);
        builder.Services.AddSingleton<IDapperMaticEmailSender>(emails);
        builder.Services.AddAuthorization();
        builder.Services.AddDapperMatic(dm => dm
            .AddProvider(Microsoft.Data.Sqlite.SqliteFactory.Instance)
            .Policy(DmPolicy.Allow(DmAction.All, "admin"))
            .AddUserAccounts(a => a.UseDatabase("Sqlite", $"Data Source={file}")));
        builder.Services.Configure<DapperMaticOptions>(o =>
        {
            o.ConnectionStringEncryptionKey = EncryptionKey;
            o.AccessRefreshInterval = TimeSpan.Zero;
        });
        await using var app = builder.Build();
        app.UseAuthorization();
        app.MapGet("/cookie", () => "ok").RequireAuthorization(p => p.AddAuthenticationSchemes(Microsoft.AspNetCore.Identity.IdentityConstants.ApplicationScheme).RequireRole("clerk"));
        app.MapGet("/ours", () => "ok").RequireAuthorization(p => p.AddAuthenticationSchemes(DapperMaticAuthenticationDefaults.Scheme).RequireRole("clerk"));
        app.UseDapperMatic();
        await app.StartAsync();

        using var anonymous = app.GetTestClient();
        (await anonymous.PostAsJsonAsync("/api/dm/auth/setup", new { code = logs.SetupCode(), email = "ops@example.com", password = Password })).EnsureSuccessStatusCode();
        using var adminLogin = await anonymous.PostAsJsonAsync("/api/dm/auth/login", new { email = "ops@example.com", password = Password });
        using var admin = app.GetTestClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await adminLogin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString());
        using var invited = await admin.PostAsJsonAsync("/api/dm/access/invitations", new { email = "lu@example.com", tenant = "7", roles = new[] { "clerk" } });
        var invitation = (await invited.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("result").GetProperty("token").GetString();
        (await anonymous.PostAsJsonAsync("/api/dm/auth/invitations/accept", new { token = invitation, password = Password })).EnsureSuccessStatusCode();

        // A cookie, kept by hand (the test client keeps none).
        using var cookieLogin = await anonymous.PostAsJsonAsync("/api/dm/auth/login?useCookies=true", new { email = "lu@example.com", password = Password });
        var cookie = string.Join("; ", cookieLogin.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]));
        using var bearerLogin = await anonymous.PostAsJsonAsync("/api/dm/auth/login", new { email = "lu@example.com", password = Password });
        var bearer = (await bearerLogin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        async Task<HttpStatusCode> GetAsync(string path, string? withCookie, string? withToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            if (withCookie is not null)
            {
                request.Headers.Add("Cookie", withCookie);
            }

            if (withToken is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", withToken);
            }

            using var response = await anonymous.SendAsync(request);
            return response.StatusCode;
        }

        (await GetAsync("/cookie", cookie, null)).Should().Be(HttpStatusCode.OK);
        (await GetAsync("/ours", null, bearer)).Should().Be(HttpStatusCode.OK);

        var lu = (await admin.GetFromJsonAsync<JsonElement>("/api/dm/access/accounts")).GetProperty("result").EnumerateArray().Single(a => a.GetProperty("email").GetString() == "lu@example.com").GetProperty("id").GetString();
        (await admin.DeleteAsync($"/api/dm/access/memberships/7/{lu}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetAsync("/cookie", cookie, null)).Should().Be(HttpStatusCode.Unauthorized);
        (await GetAsync("/ours", null, bearer)).Should().Be(HttpStatusCode.Unauthorized);
    }

    // The application's Data Protection keys are kept in the accounts' database, encrypted.
    [Fact]
    public async Task Data_protection_keys_are_kept_encrypted_Async()
    {
        using var host = await HostAsync();
        await AdminAsync(host);
        using var db = new Microsoft.Data.Sqlite.SqliteConnection(host.AccountsConnectionString);
        var keys = (await db.QueryAsync<string>("SELECT xml FROM dm_data_protection_keys")).ToList();
        keys.Should().NotBeEmpty();
        keys.Should().OnlyContain(x => x.Contains("dapperMaticEncryptedKey") && !x.Contains("masterKey"));
    }

    // The accounts' tables and flows work on every database.
    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql)]
    public async Task Accounts_work_on_every_database_Async(string datasourceId)
    {
        var accountsDatasource = _fixture.GetTestDatasources().Single(d => d.Id == datasourceId);
        using var host = await HostAsync(accounts: a => a.UseDatabase(accountsDatasource.Provider!, accountsDatasource.ConnectionString!), firstAdmin: true);
        var email = $"u{Guid.NewGuid():N}"[..12] + "@example.com";
        var invitation = host.Emails.Last("ops@example.com", DapperMaticEmailKind.Invitation).Code!;
        using var client = host.Factory.CreateClient();
        await client.PostAsJsonAsync("/api/dm/auth/invitations/accept", new { token = invitation, password = Password });
        var admin = Bearer(host, (await LoginAsync(host, "ops@example.com")).Access);
        await AcceptAsync(host, await InviteAsync(host, admin, email, "7", ["clerk"]));
        var (token, refresh) = await LoginAsync(host, email);
        (await Bearer(host, token).GetFromJsonAsync<JsonElement>("/api/dm/auth/me")).GetProperty("tenant").GetString().Should().Be("7");
        (await client.PostAsJsonAsync("/api/dm/auth/refresh", new { refreshToken = refresh })).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static HttpClient Bearer(AccountHost host, string token)
    {
        var client = host.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<(string Access, string Refresh)> LoginAsync(AccountHost host, string email, string? tenant = null)
    {
        using var client = host.Factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/dm/auth/login", new { email, password = Password, tenant });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var tokens = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (tokens.GetProperty("accessToken").GetString()!, tokens.GetProperty("refreshToken").GetString()!);
    }

    // The first administrator, ops@example.com, made with the setup code; a client signed in as them.
    private static async Task<HttpClient> AdminAsync(AccountHost host)
    {
        using var client = host.Factory.CreateClient();
        (await client.PostAsJsonAsync("/api/dm/auth/setup", new { code = host.SetupCode(), email = "ops@example.com", password = Password })).EnsureSuccessStatusCode();
        return Bearer(host, (await LoginAsync(host, "ops@example.com")).Access);
    }

    private static async Task<string> InviteAsync(AccountHost host, HttpClient inviter, string email, string? tenant, string[] roles)
    {
        using var response = await inviter.PostAsJsonAsync("/api/dm/access/invitations", new { email, tenant, roles });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("result").GetProperty("token").GetString()!;
    }

    private static async Task AcceptAsync(AccountHost host, string token)
    {
        using var client = host.Factory.CreateClient();
        (await client.PostAsJsonAsync("/api/dm/auth/invitations/accept", new { token, password = Password })).EnsureSuccessStatusCode();
    }

    private static async Task<List<JsonElement>> AccountsAsync(AccountHost host, HttpClient admin) =>
        [.. (await admin.GetFromJsonAsync<JsonElement>("/api/dm/access/accounts")).GetProperty("result").EnumerateArray()];

    // An authenticator app's code (RFC 6238: 30-second steps, 6 digits, HMAC-SHA1) of a base32 key.
    private static string Totp(string key)
    {
        var bytes = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in key.ToUpperInvariant().Where(c => c != ' ' && c != '='))
        {
            buffer = (buffer << 5) | "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".IndexOf(c, StringComparison.Ordinal);
            bits += 5;
            if (bits >= 8)
            {
                bytes.Add((byte)(buffer >> (bits - 8)));
                bits -= 8;
            }
        }

        var step = BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(step);
        }

        var hash = System.Security.Cryptography.HMACSHA1.HashData(bytes.ToArray(), step);
        var offset = hash[^1] & 0xf;
        var value = ((hash[offset] & 0x7f) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (value % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    // A host whose accounts are kept in a database of their own: a new SQLite file unless another is given.
    private async Task<AccountHost> HostAsync(Action<DapperMaticAccountOptions>? accounts = null, bool keys = false, bool firstAdmin = false)
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        var file = Path.Combine(Path.GetTempPath(), $"dm-accounts-{Guid.NewGuid():N}.db");
        var emails = new Emails();
        var logs = new Logs();
        var audit = new Infrastructure.TestDapperMaticAuditLogger();
        var factory = GraphQLTestHost.Create(
                datasource,
                dm =>
                {
                    dm.Policies(
                        DmPolicy.Allow(DmAction.All, "admin"),
                        DmPolicy.Allow(DmAction.Read | DmAction.ReadSchema, "clerk"),
                        DmPolicy.Allow(DmAction.ManageAccess, "tenant-admin").WithinOwnTenant()
                    );
                    dm.Tenancy(t => t.Map("7", datasource: datasource.Id).Map("8", datasource: datasource.Id));
                    dm.AddUserAccounts(a =>
                    {
                        a.UseDatabase("Sqlite", $"Data Source={file}");
                        if (firstAdmin)
                        {
                            a.FirstAdmin = new DapperMaticFirstAdmin { Email = "ops@example.com" };
                        }

                        accounts?.Invoke(a);
                    });
                    if (keys)
                    {
                        dm.AddApiKeys();
                    }
                },
                options =>
                {
                    options.ConnectionStringEncryptionKey = EncryptionKey;
                    options.AccessRefreshInterval = TimeSpan.Zero;
                }
            )
            .WithWebHostBuilder(web => web.ConfigureServices(services =>
            {
                services.AddSingleton<IDapperMaticEmailSender>(emails);
                services.AddSingleton<ILoggerProvider>(logs);
                services.AddSingleton<MJCZone.DapperMatic.AspNetCore.Auditing.IDapperMaticAuditLogger>(audit);
                services.AddSingleton<MJCZone.DapperMatic.AspNetCore.Repositories.IDapperMaticAccessRepository>(new MJCZone.DapperMatic.AspNetCore.Repositories.InMemoryDapperMaticAccessRepository());
            }));
        await GraphQLTestHost.CreateTablesAsync(factory, datasource, null);
        return new AccountHost(factory, datasource, emails, logs, audit, $"Data Source={file}");
    }

    private sealed record AccountHost(
        WebApplicationFactory<Program> Factory,
        Models.Dtos.DatasourceDto Datasource,
        Emails Emails,
        Logs Logs,
        Infrastructure.TestDapperMaticAuditLogger Audit,
        string AccountsConnectionString
    ) : IDisposable
    {
        public string SetupCode() => Logs.SetupCode() ?? throw new InvalidOperationException("No setup code was logged.");

        public void Dispose() => Factory.Dispose();
    }

    // The emails sent, kept for the test to read.
    private sealed class Emails : IDapperMaticEmailSender
    {
        private readonly ConcurrentQueue<DapperMaticEmail> _sent = new();

        public Task SendAsync(DapperMaticEmail email, CancellationToken cancellationToken)
        {
            _sent.Enqueue(email);
            return Task.CompletedTask;
        }

        public IEnumerable<DapperMaticEmail> Sent => _sent;

        public DapperMaticEmail Last(string to, DapperMaticEmailKind kind) => _sent.Last(e => e.To == to && e.Kind == kind);
    }

    // The log, kept for the setup code.
    private sealed partial class Logs : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _lines = new();

        public ILogger CreateLogger(string categoryName) => new Logger(_lines);

        public string? SetupCode() => _lines.Select(l => Code().Match(l)).LastOrDefault(m => m.Success)?.Groups[1].Value;

        public void Dispose()
        {
        }

        [GeneratedRegex("code ([0-9A-F]{24})")]
        private static partial Regex Code();

        private sealed class Logger(ConcurrentQueue<string> lines) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                lines.Enqueue(formatter(state, exception));
        }
    }
}
