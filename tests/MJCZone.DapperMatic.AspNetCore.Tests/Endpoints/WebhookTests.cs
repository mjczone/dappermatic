// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MJCZone.DapperMatic.AspNetCore.Changes;
using MJCZone.DapperMatic.AspNetCore.Factories;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Services;
using MJCZone.DapperMatic.AspNetCore.Tests.Factories;
using MJCZone.DapperMatic.AspNetCore.Tests.Infrastructure;
using MJCZone.DapperMatic.Models;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Endpoints;

/// <summary>
/// Webhooks told of the changes made through DapperMatic: in order, with keys, signed, retried, and per tenant.
/// </summary>
public class WebhookTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private readonly TestcontainersAssemblyFixture _fixture;

    public WebhookTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite)]
    public async Task Changes_reach_webhooks_in_order_with_their_keys_Async(string datasourceId)
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == datasourceId);
        var receiver = new Receiver();
        using var factory = new WafWithInMemoryDatasourceRepository([datasource]).WithWebHostBuilder(web =>
            web.ConfigureServices(services =>
            {
                services.Configure<DapperMaticOptions>(options =>
                {
                    options.WebhookDelivery.Enabled = false;
                    options.Webhooks.Add(new DapperMaticWebhook { Name = "orders", Url = "https://receiver.test/hook", Secret = "s3cret", Tables = { "wh_orders" } });
                });
                services.AddHttpClient(DapperMaticWebhookWorker.ClientName).ConfigurePrimaryHttpMessageHandler(() => receiver);
            })
        );
        using var client = factory.CreateClient();
        await ResetAsync(factory, datasource);
        var worker = factory.Services.GetRequiredService<DapperMaticWebhookWorker>();
        await worker.RunOnceAsync();

        async Task Post(string path, string body)
        {
            var response = await client.PostAsync($"/api/dm/d/{datasourceId}/t/wh_orders/{path}", new StringContent(body, Encoding.UTF8, "application/json"));
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        }

        await Post("rows", """{"rows":[{"tenant_id":1,"total":10},{"tenant_id":1,"total":20}]}""");
        await Post("rows/update", """{"set":{"total":5},"filter":"total eq 10"}""");
        await Post("rows/delete", """{"filter":"total eq 20"}""");
        await Post("rows/update", """{"set":{"total":6},"filter":"total eq 5","dryRun":true}""");

        await worker.RunOnceAsync();
        var messages = receiver.Messages();
        messages.Select(m => $"{m.Body.GetProperty("action").GetString()} {m.Body.GetProperty("keys")}").Should().Equal("insert [1,2]", "update [1]", "delete [2]");
        foreach (var (request, text, body) in receiver.Received)
        {
            body.GetProperty("table").GetString().Should().Be("wh_orders");
            request.Headers.GetValues("X-DapperMatic-Event").Single().Should().Be($"wh_orders.{body.GetProperty("action").GetString()}");
            var timestamp = request.Headers.GetValues("X-DapperMatic-Timestamp").Single();
            var signature = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes("s3cret"), Encoding.UTF8.GetBytes($"{timestamp}.{text}")));
            request.Headers.GetValues("X-DapperMatic-Signature").Single().Should().Be("sha256=" + signature);
        }

        // An address that fails is tried again later, with the same message, and nothing after it is sent first.
        receiver.Clear();
        receiver.Answers.Enqueue(HttpStatusCode.InternalServerError);
        await Post("rows", """{"rows":[{"tenant_id":1,"total":30}]}""");
        await worker.RunOnceAsync();
        await worker.RunOnceAsync();
        receiver.Received.Should().HaveCount(1);
        using (var db = Open(factory, datasource))
        {
            await db.ExecuteAsync("UPDATE dm_webhook_state SET next_attempt_at = NULL");
        }

        await worker.RunOnceAsync();
        receiver.Messages().Select(m => $"{m.Body.GetProperty("action").GetString()} {m.Body.GetProperty("keys")}").Should().Equal("insert [3]", "insert [3]");

        // Application code writing as the request's caller is heard too, in its transaction.
        receiver.Clear();
        var feature = new CallbackResponseFeature();
        using (var scope = factory.Services.CreateScope())
        {
            var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "app")], "Test")), RequestServices = scope.ServiceProvider };
            http.Features.Set<IHttpResponseFeature>(feature);
            scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = http;
            var db = await scope.ServiceProvider.GetRequiredService<DapperMaticCallerDb>().OpenAsync(datasourceId);
            var key = await db.InsertAsync(new WebhookOrder { TenantId = 1, Total = 40 });
            await db.DeleteByIdAsync<WebhookOrder>(key!);
        }

        await worker.RunOnceAsync();
        receiver.Messages().Select(m => $"{m.Body.GetProperty("action").GetString()} {m.Body.GetProperty("keys")}").Should().Equal("insert [4]", "delete [4]");

        // Where it has got to.
        var status = JsonDocument.Parse(await (await client.GetAsync("/api/dm/access/webhooks/orders/status")).Content.ReadAsStringAsync()).RootElement.GetProperty("result").GetProperty(datasourceId);
        status.GetProperty("delivered").GetInt64().Should().Be(6);
        status.GetProperty("attempts").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Tenants_keep_webhooks_that_hear_only_their_rows_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        var receiver = new Receiver();

        // Each client is a host of its own: they share where webhooks are kept, as servers of one application would.
        var store = new MJCZone.DapperMatic.AspNetCore.Repositories.InMemoryDapperMaticAccessRepository();
        using var factory = new WafWithInMemoryDatasourceRepository([datasource]).WithWebHostBuilder(web =>
            web.ConfigureServices(services =>
            {
                services.AddSingleton<MJCZone.DapperMatic.AspNetCore.Repositories.IDapperMaticAccessRepository>(store);
                services.Configure<DapperMaticOptions>(options =>
                {
                    options.Policies.Clear();
                    options.WebhookDelivery.Enabled = false;
                    options.WebhookDelivery.AllowPrivateAddresses = true;
                });
                services.AddDapperMatic(dm =>
                    dm.Tenancy(t => t.FromClaim("tenant_id").RowsMarkedBy("tenant_id"))
                        .Policies(
                            DmPolicy.Allow(DmAction.All, "admin"),
                            DmPolicy.Allow(DmAction.ManageAccess, "tenant-admin").WithinOwnTenant(),
                            DmPolicy.Allow(DmAction.Read, "tenant-admin").OnTables("wh_orders")
                        )
                );
                services.AddHttpClient(DapperMaticWebhookWorker.ClientName).ConfigurePrimaryHttpMessageHandler(() => receiver);
            })
        );
        await ResetAsync(factory, datasource);
        using var admin = factory.CreateAuthenticatedClient(new Claim(ClaimTypes.Role, "admin"));
        using var tenantAdmin = factory.CreateAuthenticatedClient(new Claim(ClaimTypes.Role, "tenant-admin"), new Claim("tenant_id", "1"));
        Task<HttpResponseMessage> Put(HttpClient c, string name, object body) =>
            c.PutAsync($"/api/dm/access/webhooks/{name}", new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));

        // A tenant's administrator names the datasource and tables it may read; nothing else.
        (await Put(tenantAdmin, "mine", new { url = "https://tenant.test/hook", tables = new[] { "wh_orders" } })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Put(tenantAdmin, "mine", new { url = "https://tenant.test/hook", datasourceId = datasource.Id, tables = new[] { "dm_access" } })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Put(tenantAdmin, "mine", new { url = "file:///etc/passwd", datasourceId = datasource.Id, tables = new[] { "wh_orders" } })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var saved = await Put(tenantAdmin, "mine", new { url = "https://tenant.test/hook", secret = "t1", datasourceId = datasource.Id, tables = new[] { "wh_orders" } });
        saved.StatusCode.Should().Be(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
        var kept = JsonDocument.Parse(await saved.Content.ReadAsStringAsync()).RootElement.GetProperty("result");
        kept.GetProperty("tenant").GetString().Should().Be("1");
        kept.GetProperty("secret").GetString().Should().Be("********");
        (await Put(admin, "all", new { url = "https://platform.test/hook", tables = new[] { "wh_orders" } })).StatusCode.Should().Be(HttpStatusCode.OK);

        // Each administrator sees theirs; the tenant's sees no other.
        JsonDocument.Parse(await (await tenantAdmin.GetAsync("/api/dm/access/webhooks")).Content.ReadAsStringAsync())
            .RootElement.GetProperty("result").EnumerateArray().Select(w => w.GetProperty("name").GetString()).Should().Equal("mine");

        var worker = factory.Services.GetRequiredService<DapperMaticWebhookWorker>();
        await worker.RunOnceAsync();
        var inserted = await admin.PostAsync(
            $"/api/dm/d/{datasource.Id}/t/wh_orders/rows",
            new StringContent("""{"rows":[{"tenant_id":1,"total":1},{"tenant_id":2,"total":2},{"tenant_id":1,"total":3},{"total":4}]}""", Encoding.UTF8, "application/json")
        );
        inserted.StatusCode.Should().Be(HttpStatusCode.OK, await inserted.Content.ReadAsStringAsync());
        await worker.RunOnceAsync();

        // The tenant's webhook hears its rows; the application's hears every tenant's, and the rows of none.
        var byAddress = receiver.Messages().GroupBy(m => m.Request.RequestUri!.Host).ToDictionary(g => g.Key, g => g.Select(m => $"{m.Body.GetProperty("tenant")}:{m.Body.GetProperty("keys")}").Order().ToList());
        byAddress["tenant.test"].Should().Equal("1:[1,3]");
        byAddress["platform.test"].Should().Equal(":[4]", "1:[1,3]", "2:[2]");
        receiver.Messages().Single(m => m.Request.RequestUri!.Host == "tenant.test").Request.Headers.Contains("X-DapperMatic-Signature").Should().BeTrue();
    }

    [Fact]
    public async Task Webhooks_kept_through_the_api_never_reach_internal_addresses_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        using var factory = new WafWithInMemoryDatasourceRepository([datasource]).WithWebHostBuilder(web =>
            web.ConfigureServices(services => services.Configure<DapperMaticOptions>(options => options.WebhookDelivery.Enabled = false))
        );
        using var client = factory.CreateClient();
        await ResetAsync(factory, datasource);
        foreach (var (name, url) in new[] { ("loopback", "http://127.0.0.1:9/"), ("metadata", "http://169.254.169.254/latest/meta-data/"), ("private", "http://10.0.0.1/") })
        {
            (await client.PutAsync($"/api/dm/access/webhooks/{name}", new StringContent(JsonSerializer.Serialize(new { url, tables = new[] { "wh_orders" } }), Encoding.UTF8, "application/json")))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var worker = factory.Services.GetRequiredService<DapperMaticWebhookWorker>();
        await worker.RunOnceAsync();
        (await client.PostAsync($"/api/dm/d/{datasource.Id}/t/wh_orders/rows", new StringContent("""{"rows":[{"tenant_id":1,"total":1}]}""", Encoding.UTF8, "application/json")))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        await worker.RunOnceAsync();

        foreach (var name in new[] { "loopback", "metadata", "private" })
        {
            var status = JsonDocument.Parse(await (await client.GetAsync($"/api/dm/access/webhooks/{name}/status")).Content.ReadAsStringAsync()).RootElement.GetProperty("result").GetProperty(datasource.Id!);
            status.GetProperty("attempts").GetInt32().Should().Be(1, name);
            status.GetProperty("lastError").GetString().Should().Contain("internal", name);
        }

        DapperMaticWebhookWorker.IsInternal(IPAddress.Parse("::ffff:192.168.1.1")).Should().BeTrue();
        DapperMaticWebhookWorker.IsInternal(IPAddress.Parse("fd00::1")).Should().BeTrue();
        DapperMaticWebhookWorker.IsInternal(IPAddress.Parse("100.64.0.1")).Should().BeTrue();
        DapperMaticWebhookWorker.IsInternal(IPAddress.Parse("93.184.216.34")).Should().BeFalse();
        DapperMaticWebhookWorker.IsInternal(IPAddress.Parse("2606:2800:220:1::1")).Should().BeFalse();
    }

    private static System.Data.IDbConnection Open(WebApplicationFactory<Program> factory, Models.Dtos.DatasourceDto datasource)
    {
        var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!);
        db.Open();
        return db;
    }

    private static async Task ResetAsync(WebApplicationFactory<Program> factory, Models.Dtos.DatasourceDto datasource)
    {
        using var db = Open(factory, datasource);
        foreach (var table in new[] { "wh_orders", "dm_changes", "dm_webhook_state" })
        {
            await db.DropTableIfExistsAsync(null, table);
        }

        await db.CreateTableIfNotExistsAsync(DmTableFactory.GetTable(typeof(WebhookOrder)));
    }

    [MJCZone.DapperMatic.DataAnnotations.DmTable(tableName: "wh_orders")]
    public sealed class WebhookOrder
    {
        [MJCZone.DapperMatic.DataAnnotations.DmColumn("id", isPrimaryKey: true, isAutoIncrement: true)]
        public int Id { get; set; }

        [MJCZone.DapperMatic.DataAnnotations.DmColumn("tenant_id", isNullable: true)]
        public int? TenantId { get; set; }

        [MJCZone.DapperMatic.DataAnnotations.DmColumn("total")]
        public int Total { get; set; }
    }

    private sealed class Receiver : HttpMessageHandler
    {
        public ConcurrentQueue<(HttpRequestMessage Request, string Text, JsonElement Body)> Received { get; private set; } = new();

        public ConcurrentQueue<HttpStatusCode> Answers { get; } = new();

        public List<(HttpRequestMessage Request, JsonElement Body)> Messages() => [.. Received.Select(r => (r.Request, r.Body))];

        public void Clear() => Received = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var text = await request.Content!.ReadAsStringAsync(cancellationToken);
            Received.Enqueue((request, text, JsonDocument.Parse(text).RootElement.Clone()));
            return new HttpResponseMessage(Answers.TryDequeue(out var status) ? status : HttpStatusCode.OK);
        }
    }

    // A response whose start callbacks the test runs.
    private sealed class CallbackResponseFeature : HttpResponseFeature
    {
        public override void OnStarting(Func<object, Task> callback, object state)
        {
        }
    }
}
