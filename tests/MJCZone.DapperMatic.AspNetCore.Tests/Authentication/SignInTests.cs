// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Authentication;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Authentication;

/// <summary>
/// How DapperMatic's endpoints sign their callers in: the methods they accept, credentials refused, roles found in
/// other claims, tickets for streams, and the tenants DapperMatic's own callers name.
/// </summary>
public class SignInTests
{
    [Fact]
    public async Task Listed_methods_are_the_only_ones_read_and_a_default_caller_is_kept_without_a_list_Async()
    {
        await using (var listed = await StartAsync(dm => dm.SignInWith("B")))
        {
            var client = listed.GetTestClient();
            (await WhoAsync(client, ("X-A", "ann"))).Should().Be("(no one)");
            (await WhoAsync(client, ("X-B", "bo"))).Should().Be("bo");
        }

        await using var unlisted = await StartAsync(_ => { });
        (await WhoAsync(unlisted.GetTestClient(), ("X-A", "ann"))).Should().Be("ann");
    }

    // Credentials sent and refused are refused: not taken for no one, whose policies would then apply.
    [Fact]
    public async Task Credentials_a_method_refuses_are_a_401_Async()
    {
        await using var app = await StartAsync(dm => dm.SignInWith("B"));
        var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/dm/who");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Test", "refused");
        (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // With DapperMatic's methods beside it, an application's lone method stays its default (its own [Authorize]).
    [Fact]
    public void A_lone_method_of_the_application_stays_its_default()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, HeaderHandler>("Only", null);
        services.AddDapperMatic(dm => dm.SignInWith("Only"));
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value.DefaultScheme.Should().Be("Only");
    }

    [Fact]
    public async Task A_method_that_is_not_registered_refuses_to_start_Async()
    {
        var starting = () => StartAsync(dm => dm.SignInWith("Nope"));
        await starting.Should().ThrowAsync<InvalidOperationException>().WithMessage("*'Nope'*");
    }

    // Roles from another claim (values, or a JSON array), from JSON in a claim, and from code, added to the session's
    // caller; the application's own user is left as it was.
    [Fact]
    public async Task Roles_are_found_in_other_claims_and_by_code_Async()
    {
        var options = new DapperMaticOptions();
        options.RoleSources.Add(new DapperMaticRoleSource { ClaimType = "roles" });
        options.RoleSources.Add(new DapperMaticRoleSource { ClaimType = "realm_access", JsonPath = "roles" });
        options.RoleSources.Add(new DapperMaticRoleSource { Code = (http, user, _) => ValueTask.FromResult<IEnumerable<string>?>(["e"]) });
        var access = new DapperMaticAccess(Options.Create(options));
        var http = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider(),
            User = new ClaimsPrincipal(new ClaimsIdentity([new("roles", "a"), new("roles", "[\"b\",\"c\"]"), new("realm_access", "{\"roles\":[\"d\"]}")], "Test")),
        };

        var session = await access.GetSessionAsync(http);
        new[] { "a", "b", "c", "d", "e" }.Where(r => !session.User.IsInRole(r)).Should().BeEmpty();
        http.User.IsInRole("a").Should().BeFalse();
        (await access.GetSessionAsync(http)).Should().BeSameAs(session);
    }

    // A caller DapperMatic signed in names their tenant themselves; a claim of the same name from elsewhere is not
    // believed, and the tenancy sources are read for such a caller as for anyone.
    [Fact]
    public async Task Only_DapperMatics_own_callers_name_their_tenant_Async()
    {
        var options = new DapperMaticOptions();
        options.Tenancy.Sources.Add(new MJCZone.DapperMatic.AspNetCore.Models.DapperMaticTenantSource { Claim = "tenant_id" });
        var access = new DapperMaticAccess(Options.Create(options));
        async Task<string?> TenantAsync(string authenticationType) =>
            (await access.BuildAsync(
                new DefaultHttpContext
                {
                    RequestServices = new ServiceCollection().BuildServiceProvider(),
                    User = new ClaimsPrincipal(new ClaimsIdentity([new("tenant_id", "5"), new(DapperMaticClaims.Tenant, "9")], authenticationType)),
                }
            )).Tenant?.Id.ToString();

        (await TenantAsync("External")).Should().Be("5");
        (await TenantAsync(DapperMaticClaims.ApiKeyAuthenticationType)).Should().Be("9");
    }

    // A ticket opens one stream once, as its caller in their tenant; not another path, not twice, not when stale; and
    // only where the endpoint takes tickets.
    [Fact]
    public async Task A_ticket_opens_its_stream_once_as_its_caller_Async()
    {
        await using var app = await StartAsync(dm => dm.Tenancy(t => t.FromHeader("X-Tenant")));
        var client = app.GetTestClient();

        async Task<string> TicketAsync(string path)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/dm/auth/ticket") { Content = JsonContent.Create(new { path }) };
            request.Headers.Add("X-A", "ann");
            request.Headers.Add("X-Tenant", "7");
            using var response = await client.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<TicketBody>())!.Ticket;
        }

        var ticket = await TicketAsync("/api/dm/d/main/stream");
        (await client.GetStringAsync($"/api/dm/d/main/stream?ticket={ticket}")).Should().Be("ann in 7");
        (await client.GetAsync($"/api/dm/d/main/stream?ticket={ticket}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var elsewhere = await TicketAsync("/api/dm/d/other/stream");
        (await client.GetAsync($"/api/dm/d/main/stream?ticket={elsewhere}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // An endpoint that takes no tickets does not read one.
        var unread = await TicketAsync("/api/dm/who");
        (await client.GetStringAsync($"/api/dm/who?ticket={unread}")).Should().Be("(no one)");

        // No one gets a ticket.
        (await client.PostAsJsonAsync("/api/dm/auth/ticket", new { path = "/api/dm/d/main/stream" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_ticket_not_used_in_time_does_not_open_its_stream_Async()
    {
        await using var app = await StartAsync(_ => { }, options => options.Tickets.Lifetime = TimeSpan.FromMilliseconds(200));
        var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/dm/auth/ticket") { Content = JsonContent.Create(new { path = "/api/dm/d/main/stream" }) };
        request.Headers.Add("X-A", "ann");
        var ticket = (await (await client.SendAsync(request)).Content.ReadFromJsonAsync<TicketBody>())!.Ticket;

        await Task.Delay(TimeSpan.FromMilliseconds(400));
        (await client.GetAsync($"/api/dm/d/main/stream?ticket={ticket}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<string> WhoAsync(HttpClient client, (string Name, string Value) header)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/dm/who");
        request.Headers.Add(header.Name, header.Value);
        using var response = await client.SendAsync(request);
        return await response.Content.ReadAsStringAsync();
    }

    // Two header methods: "A" (the default, reads X-A) and "B" (reads X-B; refuses Authorization: Test refused).
    private static async Task<WebApplication> StartAsync(Action<DapperMaticConfigurationBuilder> configure, Action<DapperMaticOptions>? options = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthentication("A")
            .AddScheme<AuthenticationSchemeOptions, HeaderHandler>("A", o => o.ClaimsIssuer = "X-A")
            .AddScheme<AuthenticationSchemeOptions, HeaderHandler>("B", o => o.ClaimsIssuer = "X-B");
        builder.Services.AddDapperMatic(dm =>
        {
            dm.Policy(DmPolicy.Allow(DmAction.All, "*"));
            configure(dm);
        });
        if (options is not null)
        {
            builder.Services.Configure(options);
        }

        var app = builder.Build();
        app.UseAuthentication();
        app.UseDapperMatic(group =>
        {
            group.MapGet("/who", new RequestDelegate(http => http.Response.WriteAsync(http.User.Identity?.Name ?? "(no one)")));
            group.MapGet(
                    "/d/{datasourceId}/stream",
                    new RequestDelegate(http => http.Response.WriteAsync($"{http.User.Identity?.Name} in {DapperMaticCallerSession.Of(http)?.Tenant?.Id}"))
                )
                .WithMetadata(new DapperMaticOperationMetadata(null) { AcceptsTickets = true });
        });
        await app.StartAsync();
        return app;
    }

    private sealed record TicketBody(string Ticket, int ExpiresIn);

    // Signs in the name its header gives (the header is the scheme's ClaimsIssuer); refuses "Authorization: Test refused".
    private sealed class HeaderHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers.Authorization.ToString() == "Test refused")
            {
                return Task.FromResult(AuthenticateResult.Fail("refused"));
            }

            var name = Request.Headers[Options.ClaimsIssuer ?? "X-A"].ToString();
            return Task.FromResult(
                name.Length == 0
                    ? AuthenticateResult.NoResult()
                    : AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, name)], Scheme.Name)), Scheme.Name))
            );
        }
    }
}
