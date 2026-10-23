// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Tests.GraphQL;

/// <summary>
/// GraphQL over WebSockets (graphql-transport-ws): the caller signs in on connection_init, live queries run again as
/// them when a table they read changes; sockets from other origins, past their limits or of refused callers are not
/// served.
/// </summary>
public class GraphQLSocketTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(15);
    private readonly TestcontainersAssemblyFixture _fixture;

    public GraphQLSocketTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task A_live_query_runs_again_as_its_caller_when_its_table_changes_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        using var factory = Host(datasource, graphql => graphql.MaxSubscriptionsPerSocket = 1);
        await GraphQLTestHost.CreateTablesAsync(factory, datasource, null);

        // An upgrade with no caller: browsers cannot set headers on a socket; the token comes on connection_init.
        using var socket = await Socket.OpenAsync(factory, datasource.Id!);
        (await socket.InitAsync(new { Authorization = "Bearer admin:ann" })).Should().Be("connection_ack");
        await socket.SendAsync(new { id = "1", type = "subscribe", payload = new { query = "subscription { gq_orders(orderBy: [{ id: ASC }]) { id status } }" } });
        var first = await socket.NextAsync("1");
        first.GetProperty("data").GetProperty("gq_orders").GetArrayLength().Should().Be(0);

        // One subscription a socket at most, here.
        await socket.SendAsync(new { id = "2", type = "subscribe", payload = new { query = "subscription { gq_orders { id } }" } });
        (await socket.ReceiveAsync("2")).GetProperty("type").GetString().Should().Be("error");

        // A write through the endpoint: the live query runs again, its new results sent.
        using var client = GraphQLTestHost.Client(factory);
        (await GraphQLTestHost.PostAsync(client, $"/api/dm/d/{datasource.Id}/graphql", """mutation { insert_gq_orders(rows: [{ total: 5, status: "open" }]) { affected } }""")).Data();
        var next = await socket.NextAsync("1");
        next.GetProperty("data").GetProperty("gq_orders").EnumerateArray().Select(o => o.GetProperty("status").GetString()).Should().Equal("open");

        // Queries run once over the socket too.
        await socket.SendAsync(new { id = "3", type = "complete" });
        await socket.SendAsync(new { id = "1", type = "complete" });
        await socket.SendAsync(new { id = "4", type = "subscribe", payload = new { query = "{ gq_orders { status } }" } });
        (await socket.NextAsync("4")).GetProperty("data").GetProperty("gq_orders").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Sockets_from_other_origins_or_of_refused_callers_are_not_served_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        using var factory = Host(datasource, tenancy: true);
        await GraphQLTestHost.CreateTablesAsync(factory, datasource, null);

        // A page elsewhere cannot open the caller's socket.
        var refused = () => Socket.OpenAsync(factory, datasource.Id!, origin: "http://elsewhere.example");
        await refused.Should().ThrowAsync<InvalidOperationException>();
        using (var own = await Socket.OpenAsync(factory, datasource.Id!, origin: "http://localhost"))
        {
            (await own.InitAsync(new { Authorization = "Bearer admin:ann:7" })).Should().Be("connection_ack");
        }

        // Signed in, with no tenant where one is needed: the socket is closed, 4403.
        using var socket = await Socket.OpenAsync(factory, datasource.Id!);
        await socket.InitAsync(new { Authorization = "Bearer admin:ann" });
        socket.CloseStatus.Should().Be((WebSocketCloseStatus)4403);
    }

    // A live query lasts as long as its caller's access: a policy taken away elsewhere ends it at its next run; a socket
    // closes when its caller's session ends.
    [Fact]
    public async Task A_live_query_ends_with_its_callers_access_and_session_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        var store = new MJCZone.DapperMatic.AspNetCore.Repositories.InMemoryDapperMaticAccessRepository();
        await store.SavePolicyAsync(DmPolicy.Allow(DmAction.Read, "clerk").OnTables("gq_orders").Named("clerks-read"));
        using var factory = GraphQLTestHost.Create(
            datasource,
            dm => dm.Policy(DmPolicy.Allow(DmAction.All, "admin")),
            options =>
            {
                options.RealtimeTables.Add(new DapperMaticRealtimeTable { TableName = "gq_orders" });
                options.Realtime.Debounce = TimeSpan.FromMilliseconds(50);
                options.AccessRefreshInterval = TimeSpan.FromMilliseconds(100);
                options.MaxSessionAge = TimeSpan.FromSeconds(3);
            }
        ).WithWebHostBuilder(web => web.ConfigureServices(services => services.AddSingleton<MJCZone.DapperMatic.AspNetCore.Repositories.IDapperMaticAccessRepository>(store)));
        await GraphQLTestHost.CreateTablesAsync(factory, datasource, null);

        using var socket = await Socket.OpenAsync(factory, datasource.Id!);
        (await socket.InitAsync(new { Authorization = "Bearer clerk:ann" })).Should().Be("connection_ack");
        await socket.SendAsync(new { id = "1", type = "subscribe", payload = new { query = "subscription { gq_orders { id } }" } });
        (await socket.NextAsync("1")).GetProperty("data").GetProperty("gq_orders").GetArrayLength().Should().Be(0);

        // The policy is removed by another instance, and a row written there: no request here reads the policies again,
        // so only the live query's own check of its caller's session can see the change. Its next run is refused.
        (await store.DeletePolicyAsync("clerks-read")).Should().BeTrue();
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        using (var db = factory.Services.GetRequiredService<MJCZone.DapperMatic.AspNetCore.Factories.IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!))
        {
            await Dapper.SqlMapper.ExecuteAsync(db, "INSERT INTO gq_orders (total, status) VALUES (1, 'open')");
        }

        factory.Services.GetRequiredService<MJCZone.DapperMatic.AspNetCore.Changes.DapperMaticRealtimeHub>().Notify(datasource.Id!, [(null, "gq_orders")]);
        // graphql-transport-ws: the run's error comes as a result with errors, then the subscription is complete.
        var refused = await socket.NextAsync("1");
        refused.GetProperty("errors")[0].GetProperty("extensions").GetProperty("code").GetString().Should().Be("FORBIDDEN");
        (await socket.ReceiveAsync("1")).GetProperty("type").GetString().Should().Be("complete");

        // The socket closes when the session's maximum age passes.
        (await socket.ReceiveAsync("never")).ValueKind.Should().Be(JsonValueKind.Undefined);
        socket.CloseStatus.Should().Be((WebSocketCloseStatus)4401);
    }

    // A sign-in the application's hook refuses, or a token not accepted, closes the socket: it is not left to the request
    // that opened it (whose cookies a page elsewhere may send), nor to no one.
    [Fact]
    public async Task A_socket_refused_at_sign_in_is_closed_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        using (var hooked = Host(datasource, graphql => graphql.SignIn = (_, _) => Task.FromResult<System.Security.Claims.ClaimsPrincipal?>(null)))
        {
            using var socket = await Socket.OpenAsync(hooked, datasource.Id!, headers: new() { ["X-Test-Role"] = "admin" });
            await socket.InitAsync(new { });
            socket.CloseStatus.Should().Be((WebSocketCloseStatus)4403);
        }

        using var factory = Host(datasource);
        using var token = await Socket.OpenAsync(factory, datasource.Id!);
        await token.InitAsync(new { Authorization = "Bearer :nobody" });
        token.CloseStatus.Should().Be((WebSocketCloseStatus)4403);
    }

    // A socket's tenant kept differently now (another datasource): each operation after is refused, not only the first.
    [Fact]
    public async Task A_socket_whose_tenant_moved_is_refused_each_time_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        var store = new MJCZone.DapperMatic.AspNetCore.Repositories.InMemoryDapperMaticAccessRepository();
        await store.SaveTenantAsync("7", new DapperMaticTenantSettings { Datasource = datasource.Id });
        using var factory = GraphQLTestHost.Create(
            datasource,
            dm => dm.Policy(DmPolicy.Allow(DmAction.All, "admin")).Tenancy(t => t.FromClaim("tenant_id").RequireTenant()),
            options => options.AccessRefreshInterval = TimeSpan.FromMilliseconds(100)
        ).WithWebHostBuilder(web => web.ConfigureServices(services => services.AddSingleton<MJCZone.DapperMatic.AspNetCore.Repositories.IDapperMaticAccessRepository>(store)));
        await GraphQLTestHost.CreateTablesAsync(factory, datasource, null);

        using var socket = await Socket.OpenAsync(factory, datasource.Id!);
        (await socket.InitAsync(new { Authorization = "Bearer admin:ann:7" })).Should().Be("connection_ack");
        await socket.SendAsync(new { id = "1", type = "subscribe", payload = new { query = "{ gq_orders { id } }" } });
        (await socket.NextAsync("1")).GetProperty("data").GetProperty("gq_orders").GetArrayLength().Should().Be(0);

        await store.SaveTenantAsync("7", new DapperMaticTenantSettings { Datasource = "elsewhere" });
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        foreach (var id in new[] { "2", "3" })
        {
            await socket.SendAsync(new { id, type = "subscribe", payload = new { query = "{ gq_orders { id } }" } });
            var refused = await socket.ReceiveAsync(id);
            refused.GetProperty("type").GetString().Should().Be("error", refused.ToString());
            refused.GetProperty("payload")[0].GetProperty("extensions").GetProperty("code").GetString().Should().Be("FORBIDDEN");
        }
    }

    // A live query its client completes while it first runs is never subscribed to: it holds nothing open.
    [Fact]
    public async Task A_live_query_completed_before_it_starts_holds_nothing_open_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        using var factory = GraphQLTestHost.Create(
            datasource,
            dm => dm.UseCustomPermissions(_ => new SlowPermissions()),
            options => options.RealtimeTables.Add(new DapperMaticRealtimeTable { TableName = "gq_orders" })
        );
        await GraphQLTestHost.CreateTablesAsync(factory, datasource, null);
        var hub = factory.Services.GetRequiredService<MJCZone.DapperMatic.AspNetCore.Changes.DapperMaticRealtimeHub>();

        using var socket = await Socket.OpenAsync(factory, datasource.Id!);
        (await socket.InitAsync(new { Authorization = "Bearer admin:ann" })).Should().Be("connection_ack");
        await socket.SendAsync(new { id = "1", type = "subscribe", payload = new { query = "subscription { gq_orders { id } }" } });
        await socket.SendAsync(new { id = "1", type = "complete" });

        // A query sent after it runs as long: once it is answered, and a while more, the live query has run too.
        await socket.SendAsync(new { id = "2", type = "subscribe", payload = new { query = "{ gq_orders { id } }" } });
        await socket.NextAsync("2");
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        hub.Datasources.Should().BeEmpty();
    }

    // A browser cannot send a header on a socket: a ticket, got by a signed-in request, opens it as that caller, once.
    [Fact]
    public async Task A_ticket_opens_a_socket_as_its_caller_once_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        using var factory = Host(datasource);
        await GraphQLTestHost.CreateTablesAsync(factory, datasource, null);
        var path = $"/api/dm/d/{datasource.Id}/graphql";
        using var client = GraphQLTestHost.Client(factory);
        using var response = await client.PostAsync("/api/dm/auth/ticket", new StringContent(JsonSerializer.Serialize(new { path }), Encoding.UTF8, "application/json"));
        var ticket = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("ticket").GetString()!;

        using (var socket = await Socket.OpenAsync(factory, datasource.Id!, query: "ticket=" + ticket))
        {
            (await socket.InitAsync(new { })).Should().Be("connection_ack");
            await socket.SendAsync(new { id = "1", type = "subscribe", payload = new { query = "{ gq_orders { id } }" } });
            (await socket.NextAsync("1")).GetProperty("data").GetProperty("gq_orders").GetArrayLength().Should().Be(0);
        }

        var again = () => Socket.OpenAsync(factory, datasource.Id!, query: "ticket=" + ticket);
        (await again.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("401");
    }

    // A message is held whole before it is read: one longer than the limit closes the socket, 1009.
    [Fact]
    public async Task A_socket_message_longer_than_the_limit_closes_the_socket_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        using var factory = Host(datasource, graphql => graphql.MaxSocketMessageBytes = 1024);
        await GraphQLTestHost.CreateTablesAsync(factory, datasource, null);

        using var socket = await Socket.OpenAsync(factory, datasource.Id!);
        (await socket.InitAsync(new { Authorization = "Bearer admin:ann" })).Should().Be("connection_ack");
        await socket.SendAsync(new { id = "1", type = "subscribe", payload = new { query = "{ gq_orders { id } } #" + new string('x', 4096) } });
        (await socket.ReceiveAsync("1")).ValueKind.Should().Be(JsonValueKind.Undefined);
        socket.CloseStatus.Should().Be(WebSocketCloseStatus.MessageTooBig);
    }

    private static WebApplicationFactory<Program> Host(
        AspNetCore.Models.Dtos.DatasourceDto datasource,
        Action<MJCZone.DapperMatic.GraphQL.DapperMaticGraphQLOptions>? graphql = null,
        bool tenancy = false
    ) =>
        GraphQLTestHost.Create(
            datasource,
            dm =>
            {
                dm.Policy(DmPolicy.Allow(DmAction.All, "admin"));
                if (tenancy)
                {
                    dm.Tenancy(t => t.FromClaim("tenant_id").RequireTenant());
                }
            },
            options =>
            {
                options.RealtimeTables.Add(new DapperMaticRealtimeTable { TableName = "gq_orders" });
                options.Realtime.Debounce = TimeSpan.FromMilliseconds(50);
            },
            graphql
        );

    // Allows everything, slowly: an operation's first run takes a while.
    private sealed class SlowPermissions : MJCZone.DapperMatic.AspNetCore.Security.IDapperMaticPermissions
    {
        public async Task<bool> IsAuthorizedAsync(DapperMaticOperation context)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50));
            return true;
        }
    }

    // A graphql-transport-ws client.
    private sealed class Socket : IDisposable
    {
        private readonly WebSocket _socket;

        private Socket(WebSocket socket)
        {
            _socket = socket;
        }

        public WebSocketCloseStatus? CloseStatus => _socket.CloseStatus;

        public static async Task<Socket> OpenAsync(WebApplicationFactory<Program> factory, string datasourceId, string? origin = null, Dictionary<string, string>? headers = null, string? query = null)
        {
            var client = factory.Server.CreateWebSocketClient();
            client.SubProtocols.Add("graphql-transport-ws");
            client.ConfigureRequest = r =>
            {
                if (origin is not null)
                {
                    r.Headers.Origin = origin;
                }

                foreach (var (name, value) in headers ?? [])
                {
                    r.Headers[name] = value;
                }
            };

            var url = $"ws://localhost/api/dm/d/{datasourceId}/graphql" + (query is null ? string.Empty : "?" + query);
            return new Socket(await client.ConnectAsync(new Uri(url), CancellationToken.None));
        }

        public async Task<string?> InitAsync(object payload)
        {
            await SendAsync(new { type = "connection_init", payload });
            var message = await ReceiveAsync(null);
            return message.ValueKind == JsonValueKind.Undefined ? null : message.GetProperty("type").GetString();
        }

        public Task SendAsync(object message) =>
            _socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(message), WebSocketMessageType.Text, true, CancellationToken.None);

        // The next message for an id (or any), skipping pings; default when the socket closes.
        public async Task<JsonElement> ReceiveAsync(string? id)
        {
            using var cancel = new CancellationTokenSource(Wait);
            var buffer = new byte[64 * 1024];
            while (true)
            {
                var text = new StringBuilder();
                WebSocketReceiveResult result;
                do
                {
                    result = await _socket.ReceiveAsync(buffer, cancel.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return default;
                    }

                    text.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
                while (!result.EndOfMessage);

                var message = JsonDocument.Parse(text.ToString()).RootElement.Clone();
                var type = message.GetProperty("type").GetString();
                if (type is "ping" or "pong")
                {
                    continue;
                }

                if (id is null || (message.TryGetProperty("id", out var of) && of.GetString() == id))
                {
                    return message;
                }
            }
        }

        public async Task<JsonElement> NextAsync(string id)
        {
            var message = await ReceiveAsync(id);
            message.GetProperty("type").GetString().Should().Be("next", message.ToString());
            return message.GetProperty("payload");
        }

        public void Dispose() => _socket.Dispose();
    }
}
