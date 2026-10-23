// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Auditing;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.AspNetCore.Tests.Infrastructure;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Endpoints;

/// <summary>
/// Every endpoint in DapperMatic's group goes through one request pipeline, whatever maps it: route handlers, and raw
/// request delegates as MCP, GraphQL and streams are.
/// </summary>
public class RequestPipelineTests
{
    [Fact]
    public async Task A_raw_endpoint_that_throws_answers_with_problem_details_and_is_audited_Async()
    {
        var audit = new TestDapperMaticAuditLogger();
        await using var app = await StartAsync(audit);

        var response = await app.GetTestClient().GetAsync("/api/dm/raw/boom");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        (await response.Content.ReadAsStringAsync()).Should().Contain("nothing here");
        audit.AuditEvents.Should().ContainSingle(e => !e.Success && e.Message == "nothing here");
    }

    // The group carries no endpoint filters, so a raw delegate is not rebuilt around one: its response is its own.
    [Fact]
    public async Task A_raw_endpoint_keeps_its_own_response_Async()
    {
        await using var app = await StartAsync(new TestDapperMaticAuditLogger());

        var response = await app.GetTestClient().GetAsync("/api/dm/raw/text");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("raw");
        response.Headers.ETag.Should().BeNull();
    }

    // A socket's caller signs in on the socket (GraphQL's connection_init), after the request that opened it: that
    // request is not refused for having no caller, as any other request to the same endpoint is.
    [Fact]
    public async Task A_socket_is_opened_before_its_caller_signs_in_Async()
    {
        await using var app = await StartAsync(new TestDapperMaticAuditLogger());
        var server = app.GetTestServer();

        var plain = await server.CreateClient().GetAsync("/api/dm/d/main/socket");
        plain.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var socket = await server.CreateWebSocketClient().ConnectAsync(new Uri("ws://localhost/api/dm/d/main/socket"), CancellationToken.None);
        var buffer = new byte[16];
        var received = await socket.ReceiveAsync(buffer, CancellationToken.None);
        System.Text.Encoding.UTF8.GetString(buffer, 0, received.Count).Should().Be("hello");
    }

    // Only an endpoint that signs a socket's caller in on the socket leaves the opening request without a session. A
    // request to any other endpoint is its caller's, held to their tenant, whatever handshake headers its sender adds.
    [Fact]
    public async Task A_request_that_only_looks_like_a_socket_is_still_its_callers_Async()
    {
        await using var app = await StartAsync(new TestDapperMaticAuditLogger());
        var server = app.GetTestServer();

        var opening = () => server.CreateWebSocketClient().ConnectAsync(new Uri("ws://localhost/api/dm/d/main/plain"), CancellationToken.None);
        (await opening.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("401");
    }

    [Fact]
    public void Only_changes_to_definitions_forget_what_is_kept_of_a_datasource()
    {
        string[] changes = ["tables/post", "tables/put", "tables/delete", "views/post", "columns/post", "indexes/delete", "schemas/post", "datasources/put", "datasources/delete"];
        string[] others = ["graphql", "graphql/schema", "unknown/post", "tables/query", "tables/insert-rows", "tables/delete-rows", "batch/post", "functions/post", "access/post", "policies/delete", "datasources/test", "openapi/list", "tables/get"];
        changes.Where(o => !DapperMatic.AspNetCore.Extensions.DapperMaticRequestPipeline.ChangesSchema(o)).Should().BeEmpty();
        others.Where(DapperMatic.AspNetCore.Extensions.DapperMaticRequestPipeline.ChangesSchema).Should().BeEmpty();
    }

    // A client that goes away has not met a failure: nothing is audited, and no one is answered.
    [Fact]
    public async Task A_request_its_client_abandoned_is_not_audited_as_a_failure_Async()
    {
        var audit = new TestDapperMaticAuditLogger();
        await using var app = await StartAsync(audit);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Tenant", "7");
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        var waiting = () => client.GetAsync("/api/dm/d/main/wait", cancel.Token);
        await waiting.Should().ThrowAsync<OperationCanceledException>();
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        audit.AuditEvents.Should().BeEmpty();
    }

    // A request refused for naming another tenant's datasource is audited as its caller's, with their tenant.
    [Fact]
    public async Task A_request_refused_for_its_tenant_is_audited_with_the_tenant_Async()
    {
        var audit = new TestDapperMaticAuditLogger();
        await using var app = await StartAsync(audit);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Tenant", "7");

        (await client.GetAsync("/api/dm/d/main/plain")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/dm/d/other/plain")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        audit.AuditEvents.Should().ContainSingle(e => !e.Success).Which.TenantId.Should().Be("7");
    }

    // A session built before the request's caller was known (an application's middleware ahead of authentication) is
    // built again for them, not kept as no one's.
    [Fact]
    public async Task A_session_built_before_sign_in_is_built_again_for_the_caller_Async()
    {
        var access = new DapperMaticAccess(Options.Create(new DapperMaticOptions()));
        var http = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        var early = await access.GetSessionAsync(http);
        http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "ann")], "Test"));

        var session = await access.GetSessionAsync(http);
        session.Should().NotBeSameAs(early);
        session.User.Identity!.Name.Should().Be("ann");
        (await access.GetSessionAsync(http)).Should().BeSameAs(session);
    }

    // A token's exp is in seconds; one no date can have (milliseconds) is not read, and the session lasts its maximum
    // age.
    [Fact]
    public async Task An_exp_no_date_can_have_is_not_read_Async()
    {
        var access = new DapperMaticAccess(Options.Create(new DapperMaticOptions { MaxSessionAge = TimeSpan.FromMinutes(5) }));
        var http = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider(),
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("exp", "1893456000000")], "Test")),
        };

        var session = await access.BuildAsync(http);
        session.ExpiresAt.Should().Be(session.StartedAt + TimeSpan.FromMinutes(5));
    }

    private static async Task<WebApplication> StartAsync(IDapperMaticAuditLogger audit)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDapperMatic(dm => dm.Tenancy(t => t.FromClaim("tenant_id").RequireTenant().Map("7", datasource: "main")).Policy(DmPolicy.Allow(DmAction.All, "*")));
        builder.Services.AddSingleton(audit);

        var app = builder.Build();
        app.UseWebSockets();

        // A caller of tenant X-Tenant, when the header is given.
        app.Use((http, next) =>
        {
            if (http.Request.Headers["X-Tenant"].ToString() is { Length: > 0 } tenant)
            {
                http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "ann"), new Claim("tenant_id", tenant)], "Test"));
            }

            return next(http);
        });
        app.UseDapperMatic(group =>
        {
            group.MapGet("/raw/boom", new RequestDelegate(_ => throw new KeyNotFoundException("nothing here")));
            group.MapGet("/raw/text", new RequestDelegate(http => http.Response.WriteAsync("raw")));
            group.MapGet("/d/{datasourceId}/plain", new RequestDelegate(http => http.Response.WriteAsync("served")));
            group.MapGet("/d/{datasourceId}/wait", new RequestDelegate(http => Task.Delay(Timeout.Infinite, http.RequestAborted)));
            group.Map(
                "/d/{datasourceId}/socket",
                new RequestDelegate(async http =>
                {
                    if (!http.WebSockets.IsWebSocketRequest)
                    {
                        http.Response.StatusCode = StatusCodes.Status400BadRequest;
                        return;
                    }

                    using var socket = await http.WebSockets.AcceptWebSocketAsync();
                    await socket.SendAsync("hello"u8.ToArray(), System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None);
                    await socket.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                })
            )
            .WithMetadata(new DapperMaticOperationMetadata("socket") { SignsInOnSocket = true });
        });
        await app.StartAsync();
        return app;
    }
}
