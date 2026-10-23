// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Dapper;
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
/// Subscriptions to queries: new results pushed as the tables they read change, on this server or another.
/// </summary>
public class RealtimeTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(15);

    private readonly TestcontainersAssemblyFixture _fixture;

    public RealtimeTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite)]
    public async Task Subscriptions_get_new_results_when_their_tables_change_Async(string datasourceId)
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == datasourceId);
        using var factory = new WafWithInMemoryDatasourceRepository([datasource]).WithWebHostBuilder(web =>
            web.ConfigureServices(services =>
                services.Configure<DapperMaticOptions>(options =>
                {
                    options.Relations.Add(new DapperMaticRelation { TableName = "rt_items", Name = "kind", OtherTableName = "rt_kinds", Columns = ["kind_id"], OtherColumns = ["id"] });
                    options.RealtimeTables.Add(new DapperMaticRealtimeTable { TableName = "rt_items" });
                    options.RealtimeTables.Add(new DapperMaticRealtimeTable { TableName = "rt_kinds" });
                    options.Realtime.PollInterval = TimeSpan.FromHours(1);

                    // The test drops the change tables under the running host: no worker keeps them meanwhile.
                    options.WebhookDelivery.Enabled = false;
                    options.Realtime.Debounce = TimeSpan.FromMilliseconds(50);
                })
            )
        );
        var poller = factory.Services.GetRequiredService<MJCZone.DapperMatic.AspNetCore.Changes.DapperMaticRealtimePoller>();
        using var client = factory.CreateClient();
        using var otherServer = factory.WithWebHostBuilder(_ => { });
        using var other = otherServer.CreateClient();
        using (var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!))
        {
            db.Open();
            foreach (var table in new[] { "rt_items", "rt_kinds", "rt_secret", "dm_changes", "dm_webhook_state" })
            {
                await db.DropTableIfExistsAsync(null, table);
            }

            await db.CreateTableIfNotExistsAsync(new DmTable(null, "rt_kinds", [new DmColumn("id", typeof(int), isPrimaryKey: true), new DmColumn("name", typeof(string), length: 20)]));
            await db.CreateTableIfNotExistsAsync(
                new DmTable(null, "rt_items", [new DmColumn("id", typeof(int), isPrimaryKey: true), new DmColumn("kind_id", typeof(int)), new DmColumn("label", typeof(string), length: 20)])
            );
            await db.CreateTableIfNotExistsAsync(new DmTable(null, "rt_secret", [new DmColumn("id", typeof(int), isPrimaryKey: true)]));
            await db.ExecuteAsync("INSERT INTO rt_kinds (id, name) VALUES (1, 'tool')");
            await db.ExecuteAsync("INSERT INTO rt_items (id, kind_id, label) VALUES (1, 1, 'hammer')");
        }

        async Task Write(HttpClient c, string path, string body)
        {
            var response = await c.PostAsync($"/api/dm/d/{datasourceId}/t/{path}", new StringContent(body, Encoding.UTF8, "application/json"));
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        }

        static string Summary(JsonElement results) =>
            string.Join(",", results.GetProperty("result").EnumerateArray().Select(r => $"{r.GetProperty("label").GetString()}/{r.GetProperty("kind").GetProperty("name").GetString()}"));

        // A table not listed cannot be subscribed to.
        (await client.GetAsync($"/api/dm/d/{datasourceId}/t/rt_secret/subscribe")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var events = await Events.OpenAsync(client, $"/api/dm/d/{datasourceId}/t/rt_items/subscribe?$orderby=id&$expand=kind");
        Summary(await events.NextAsync(Wait)).Should().Be("hammer/tool");
        await poller.RunOnceAsync();

        // A write on this server is heard at once, not when the change table is next read.
        await Write(client, "rt_items/rows", """{"rows":[{"id":2,"kind_id":1,"label":"saw"}]}""");
        Summary(await events.NextAsync(Wait)).Should().Be("hammer/tool,saw/tool");

        // So is one to a table the query reads through a relation.
        await Write(client, "rt_kinds/rows/update", """{"set":{"name":"hand tool"},"filter":"id eq 1"}""");
        Summary(await events.NextAsync(Wait)).Should().Be("hammer/hand tool,saw/hand tool");

        // A write on another server is heard through the change table.
        await Write(other, "rt_items/rows/delete", """{"filter":"id eq 1"}""");
        await poller.RunOnceAsync();
        Summary(await events.NextAsync(Wait)).Should().Be("saw/hand tool");
    }

    [Fact]
    public async Task Each_subscriber_gets_only_what_its_policies_let_it_read_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        using var factory = new WafWithInMemoryDatasourceRepository([datasource]).WithWebHostBuilder(web =>
            web.ConfigureServices(services =>
            {
                services.Configure<DapperMaticOptions>(options =>
                {
                    options.Policies.Clear();
                    options.RealtimeTables.Add(new DapperMaticRealtimeTable { TableName = "rt_notes" });
                    options.Realtime.Debounce = TimeSpan.FromMilliseconds(50);
                });
                services.AddDapperMatic(dm =>
                    dm.Tenancy(t => t.FromClaim("tenant_id"))
                        .Policies(
                            DmPolicy.Allow(DmAction.Read, "clerk").OnTables("rt_notes").Where("tenant_id eq @tenant.id"),
                            DmPolicy.Allow(DmAction.All, "admin")
                        )
                );
            })
        );
        using (var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!))
        {
            db.Open();
            await db.DropTableIfExistsAsync(null, "rt_notes");
            await db.CreateTableIfNotExistsAsync(
                new DmTable(null, "rt_notes", [new DmColumn("id", typeof(int), isPrimaryKey: true), new DmColumn("tenant_id", typeof(int)), new DmColumn("body", typeof(string), length: 20)])
            );
        }

        // The subscribers share one server, whose hub tells both of every change.
        using var host = factory.WithWebHostBuilder(web =>
            web.ConfigureServices(services =>
                services.AddAuthentication("Test").AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, HeaderAuthentication>("Test", _ => { })
            )
        );
        HttpClient As(string role, string? tenant)
        {
            var c = host.CreateClient();
            c.DefaultRequestHeaders.Add("X-Test-Role", role);
            if (tenant is not null)
            {
                c.DefaultRequestHeaders.Add("X-Test-Tenant", tenant);
            }

            return c;
        }

        using var clerk1 = As("clerk", "1");
        using var clerk2 = As("clerk", "2");
        using var admin = As("admin", null);
        static string Bodies(JsonElement results) => string.Join(",", results.GetProperty("result").EnumerateArray().Select(r => r.GetProperty("body").GetString()));

        using var events1 = await Events.OpenAsync(clerk1, $"/api/dm/d/{datasource.Id}/t/rt_notes/subscribe?$orderby=id");
        using var events2 = await Events.OpenAsync(clerk2, $"/api/dm/d/{datasource.Id}/t/rt_notes/subscribe?$orderby=id");
        Bodies(await events1.NextAsync(Wait)).Should().BeEmpty();
        Bodies(await events2.NextAsync(Wait)).Should().BeEmpty();

        (await admin.PostAsync($"/api/dm/d/{datasource.Id}/t/rt_notes/rows", new StringContent("""{"rows":[{"id":1,"tenant_id":1,"body":"one"}]}""", Encoding.UTF8, "application/json")))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        Bodies(await events1.NextAsync(Wait)).Should().Be("one");

        // Tenant 2's results did not change, so it was sent nothing: its next results are of its own row.
        (await admin.PostAsync($"/api/dm/d/{datasource.Id}/t/rt_notes/rows", new StringContent("""{"rows":[{"id":2,"tenant_id":2,"body":"two"}]}""", Encoding.UTF8, "application/json")))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        Bodies(await events2.NextAsync(Wait)).Should().Be("two");
    }

    // A policy removed by another instance of the application is read by the next re-run, through the stream's check of
    // its caller's session: the subscription ends, rather than going on with access the caller no longer has.
    [Fact]
    public async Task A_policy_revoked_while_a_stream_is_open_ends_the_stream_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        var store = new MJCZone.DapperMatic.AspNetCore.Repositories.InMemoryDapperMaticAccessRepository();
        await store.SavePolicyAsync(DmPolicy.Allow(DmAction.Read, "clerk").OnTables("rt_revoked").Named("clerks-read"));
        using var factory = new WafWithInMemoryDatasourceRepository([datasource]).WithWebHostBuilder(web =>
            web.ConfigureServices(services =>
            {
                services.AddSingleton<MJCZone.DapperMatic.AspNetCore.Repositories.IDapperMaticAccessRepository>(store);
                services.Configure<DapperMaticOptions>(options =>
                {
                    options.Policies.Clear();
                    options.RealtimeTables.Add(new DapperMaticRealtimeTable { TableName = "rt_revoked" });
                    options.Realtime.Debounce = TimeSpan.FromMilliseconds(50);
                    options.Realtime.Heartbeat = TimeSpan.FromHours(1);
                    options.AccessRefreshInterval = TimeSpan.FromMilliseconds(100);
                });
                services.AddAuthentication("Test").AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, HeaderAuthentication>("Test", _ => { });
            })
        );
        using (var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!))
        {
            db.Open();
            await db.DropTableIfExistsAsync(null, "rt_revoked");
            await db.CreateTableIfNotExistsAsync(new DmTable(null, "rt_revoked", [new DmColumn("id", typeof(int), isPrimaryKey: true), new DmColumn("body", typeof(string), length: 20)]));
        }

        using var clerk = factory.CreateClient();
        clerk.DefaultRequestHeaders.Add("X-Test-Role", "clerk");
        using var events = await Events.OpenAsync(clerk, $"/api/dm/d/{datasource.Id}/t/rt_revoked/subscribe");
        (await events.NextAsync(Wait)).GetProperty("result").GetArrayLength().Should().Be(0);

        // Another instance removes the policy, and a row is written where this server hears of it but no request comes.
        (await store.DeletePolicyAsync("clerks-read")).Should().BeTrue();
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        using (var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!))
        {
            await db.ExecuteAsync("INSERT INTO rt_revoked (id, body) VALUES (1, 'one')");
        }

        factory.Services.GetRequiredService<MJCZone.DapperMatic.AspNetCore.Changes.DapperMaticRealtimeHub>().Notify(datasource.Id!, [(null, "rt_revoked")]);
        var (kind, data) = await events.NextEventAsync(Wait);
        kind.Should().Be("error", data);
        data.Should().Contain("Forbidden");
        await FluentActions.Awaiting(() => events.NextEventAsync(Wait)).Should().ThrowAsync<EndOfStreamException>();
    }

    [Fact]
    public async Task A_change_while_a_subscription_opens_is_not_missed_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        var audit = new FirstQueryHook();
        using var factory = new WafWithInMemoryDatasourceRepository([datasource]).WithWebHostBuilder(web =>
            web.ConfigureServices(services =>
            {
                services.Configure<DapperMaticOptions>(options =>
                {
                    options.RealtimeTables.Add(new DapperMaticRealtimeTable { TableName = "rt_opening" });
                    options.Realtime.PollInterval = TimeSpan.FromHours(1);
                    options.Realtime.Debounce = TimeSpan.FromMilliseconds(10);
                    options.Realtime.Heartbeat = TimeSpan.FromHours(1);
                });
                services.AddSingleton<MJCZone.DapperMatic.AspNetCore.Auditing.IDapperMaticAuditLogger>(audit);
            })
        );
        IDbConnection Open()
        {
            var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!);
            db.Open();
            return db;
        }

        using (var db = Open())
        {
            await db.DropTableIfExistsAsync(null, "rt_opening");
            await db.CreateTableIfNotExistsAsync(new DmTable(null, "rt_opening", [new DmColumn("id", typeof(int), isPrimaryKey: true), new DmColumn("body", typeof(string), length: 20)]));
        }

        // A row is written, and this server told of it, after the subscription's query first ran and before it listens.
        audit.OnFirstQuery = async () =>
        {
            using var db = Open();
            await db.ExecuteAsync("INSERT INTO rt_opening (id, body) VALUES (1, 'one')");
            factory.Services.GetRequiredService<MJCZone.DapperMatic.AspNetCore.Changes.DapperMaticRealtimeHub>().Notify(datasource.Id!, [(null, "rt_opening")]);
        };
        using var client = factory.CreateClient();
        using var events = await Events.OpenAsync(client, $"/api/dm/d/{datasource.Id}/t/rt_opening/subscribe");
        (await events.NextAsync(Wait)).GetProperty("result").GetArrayLength().Should().Be(0);
        (await events.NextAsync(Wait)).GetProperty("result").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Pings_keep_their_time_whatever_changes_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        using var factory = new WafWithInMemoryDatasourceRepository([datasource]).WithWebHostBuilder(web =>
            web.ConfigureServices(services =>
                services.Configure<DapperMaticOptions>(options =>
                {
                    options.RealtimeTables.Add(new DapperMaticRealtimeTable { TableName = "rt_pings" });
                    options.Realtime.PollInterval = TimeSpan.FromHours(1);
                    options.Realtime.Debounce = TimeSpan.FromMilliseconds(10);
                    options.Realtime.Heartbeat = TimeSpan.FromMilliseconds(300);
                })
            )
        );
        using (var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!))
        {
            db.Open();
            await db.DropTableIfExistsAsync(null, "rt_pings");
            await db.CreateTableIfNotExistsAsync(new DmTable(null, "rt_pings", [new DmColumn("id", typeof(int), isPrimaryKey: true)]));
        }

        using var client = factory.CreateClient();
        using var events = await Events.OpenAsync(client, $"/api/dm/d/{datasource.Id}/t/rt_pings/subscribe");
        (await events.NextAsync(Wait)).GetProperty("result").GetArrayLength().Should().Be(0);

        // Changes the subscriber's results do not show (another tenant's, say) come every 100 ms: had each one put the
        // next ping off, a subscriber timing them would learn of those changes.
        var hub = factory.Services.GetRequiredService<MJCZone.DapperMatic.AspNetCore.Changes.DapperMaticRealtimeHub>();
        using var stop = new CancellationTokenSource();
        var signals = Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                hub.Notify(datasource.Id!, [(null, "rt_pings")]);
                await Task.Delay(100);
            }
        });
        var pings = await events.PingsWithinAsync(TimeSpan.FromSeconds(2));
        await stop.CancelAsync();
        await signals;
        pings.Should().BeGreaterThanOrEqualTo(3);
    }

    // Runs a hook when the first query of a table succeeds, as the query is audited: after it ran.
    private sealed class FirstQueryHook : MJCZone.DapperMatic.AspNetCore.Auditing.IDapperMaticAuditLogger
    {
        private int _fired;

        public Func<Task>? OnFirstQuery { get; set; }

        public async Task LogOperationAsync(MJCZone.DapperMatic.AspNetCore.Auditing.DapperMaticAuditEvent auditEvent)
        {
            if (auditEvent.Success && auditEvent.TableName == "rt_opening" && OnFirstQuery is { } hook && Interlocked.Exchange(ref _fired, 1) == 0)
            {
                await hook();
            }
        }
    }

    // Reads Server-Sent Events from a response kept open.
    private sealed class Events : IDisposable
    {
        private readonly HttpResponseMessage _response;
        private readonly StreamReader _reader;

        private Events(HttpResponseMessage response, Stream stream)
        {
            _response = response;
            _reader = new StreamReader(stream, Encoding.UTF8);
        }

        public static async Task<Events> OpenAsync(HttpClient client, string url)
        {
            var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");
            return new Events(response, await response.Content.ReadAsStreamAsync());
        }

        // The next results event, skipping comments; an error event fails.
        public async Task<JsonElement> NextAsync(TimeSpan timeout)
        {
            var (kind, data) = await NextEventAsync(timeout);
            kind.Should().Be("results", data);
            return JsonDocument.Parse(data).RootElement.Clone();
        }

        // The next event, of any kind, skipping comments.
        public async Task<(string Kind, string Data)> NextEventAsync(TimeSpan timeout)
        {
            using var cancel = new CancellationTokenSource(timeout);
            string? kind = null;
            var data = new StringBuilder();
            while (true)
            {
                var line = await _reader.ReadLineAsync(cancel.Token) ?? throw new EndOfStreamException("The subscription ended.");
                if (line.Length == 0)
                {
                    if (kind is null)
                    {
                        continue;
                    }

                    return (kind, data.ToString());
                }

                if (line.StartsWith("event: ", StringComparison.Ordinal))
                {
                    kind = line[7..];
                }
                else if (line.StartsWith("data: ", StringComparison.Ordinal))
                {
                    data.Append(line[6..]);
                }
            }
        }

        // The pings (comment lines) read in a while, events skipped.
        public async Task<int> PingsWithinAsync(TimeSpan duration)
        {
            var pings = 0;
            using var cancel = new CancellationTokenSource(duration);
            try
            {
                while (await _reader.ReadLineAsync(cancel.Token) is { } line)
                {
                    if (line.StartsWith(':'))
                    {
                        pings++;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // The while is over.
            }

            return pings;
        }

        public void Dispose()
        {
            _reader.Dispose();
            _response.Dispose();
        }
    }

    // Signs requests in as the role and tenant their headers name.
    private sealed class HeaderAuthentication(
        Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder
    ) : Microsoft.AspNetCore.Authentication.AuthenticationHandler<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<Microsoft.AspNetCore.Authentication.AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Role", out var role))
            {
                return Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.NoResult());
            }

            List<Claim> claims = [new Claim(ClaimTypes.Role, role.ToString())];
            if (Request.Headers.TryGetValue("X-Test-Tenant", out var tenant))
            {
                claims.Add(new Claim("tenant_id", tenant.ToString()));
            }

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
            return Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.Success(new Microsoft.AspNetCore.Authentication.AuthenticationTicket(principal, "Test")));
        }
    }
}
