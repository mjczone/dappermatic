// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Dapper;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Caching;
using MJCZone.DapperMatic.AspNetCore.Factories;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Services;
using MJCZone.DapperMatic.AspNetCore.Tests.Factories;
using MJCZone.DapperMatic.AspNetCore.Tests.Infrastructure;
using MJCZone.DapperMatic.Models;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Endpoints;

/// <summary>
/// Query results kept for the tables an application lists, made stale by every write DapperMatic makes, and ETags.
/// </summary>
public class ResultCacheTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private readonly TestcontainersAssemblyFixture _fixture;

    public ResultCacheTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer, "dbo")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql, "public")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql, null)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite, null)]
    public async Task Results_are_kept_until_a_write_makes_them_stale_Async(string datasourceId, string? schemaName)
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == datasourceId);
        var store = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        using var baseFactory = new WafWithInMemoryDatasourceRepository([datasource]);
        using var factory = baseFactory.WithWebHostBuilder(web =>
            web.ConfigureServices(services =>
                services.Configure<DapperMaticOptions>(options =>
                {
                    options.Relations.Add(new DapperMaticRelation { TableName = "rc_orders", Name = "customer", OtherTableName = "rc_customers", Columns = ["customer_id"], OtherColumns = ["id"] });
                    options.CachedTables.Add(new DapperMaticCachedTable { TableName = "rc_orders", Duration = TimeSpan.FromMinutes(10) });
                    options.CachedTables.Add(new DapperMaticCachedTable { TableName = "rc_customers", Duration = TimeSpan.FromMinutes(10) });
                    options.ResultCacheStore = store;
                })
            )
        );

        // Two instances of the application, sharing the store as they would share Redis.
        using var client = factory.CreateClient();
        using var otherInstance = factory.WithWebHostBuilder(_ => { });
        using var other = otherInstance.CreateClient();
        var baseUrl = schemaName is not null ? $"/api/dm/d/{datasourceId}/s/{schemaName}/t" : $"/api/dm/d/{datasourceId}/t";

        using var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!);
        db.Open();
        await db.DropTableIfExistsAsync(schemaName, "rc_orders");
        await db.DropTableIfExistsAsync(schemaName, "rc_customers");
        await db.CreateTableIfNotExistsAsync(new DmTable(schemaName, "rc_customers", [new DmColumn("id", typeof(int), isPrimaryKey: true), new DmColumn("name", typeof(string), length: 50)]));
        await db.CreateTableIfNotExistsAsync(
            new DmTable(schemaName, "rc_orders", [new DmColumn("id", typeof(int), isPrimaryKey: true), new DmColumn("customer_id", typeof(int)), new DmColumn("total", typeof(int))])
        );
        await db.ExecuteAsync("INSERT INTO rc_customers (id, name) VALUES (1, 'Acme')");
        await db.ExecuteAsync("INSERT INTO rc_orders (id, customer_id, total) VALUES (1, 1, 10), (2, 1, 20)");

        async Task<string> Read(HttpClient c)
        {
            var response = await c.GetAsync($"{baseUrl}/rc_orders/query?$orderby=id&$expand=customer&$count=true");
            var text = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, text);
            var json = JsonDocument.Parse(text).RootElement;
            var rows = json.GetProperty("result").EnumerateArray().ToList();
            return string.Join(",", rows.Select(r => r.GetProperty("total").GetInt32()))
                + "|" + string.Join(",", rows.Select(r => r.GetProperty("customer").GetProperty("name").GetString()).Distinct())
                + "|" + json.GetProperty("pagination").GetProperty("total").GetInt64();
        }

        async Task Write(HttpClient c, string table, object body)
        {
            var response = await c.PostAsync($"{baseUrl}/{table}/rows/update", new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        }

        (await Read(client)).Should().Be("10,20|Acme|2");

        // A change made outside DapperMatic is not seen while the results are kept, by either instance.
        await db.ExecuteAsync("UPDATE rc_orders SET total = 11 WHERE id = 1");
        (await Read(client)).Should().Be("10,20|Acme|2");
        (await Read(other)).Should().Be("10,20|Acme|2");

        // A write through DapperMatic makes them stale, on every instance sharing the store.
        await Write(other, "rc_orders", new { set = new { total = 21 }, filter = "id eq 2" });
        (await Read(client)).Should().Be("11,21|Acme|2");

        // So does a write to a table the query reads through a relation.
        await db.ExecuteAsync("UPDATE rc_orders SET total = 12 WHERE id = 1");
        await Write(client, "rc_customers", new { set = new { name = "Acme Corp" }, filter = "id eq 1" });
        (await Read(client)).Should().Be("12,21|Acme Corp|2");

        // And a write the application's own code makes as the request's caller, once the request's response starts.
        await db.ExecuteAsync("UPDATE rc_orders SET total = 13 WHERE id = 1");
        var response = new CallbackResponseFeature();
        using (var scope = factory.Services.CreateScope())
        {
            var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "app")], "Test")), RequestServices = scope.ServiceProvider };
            http.Features.Set<IHttpResponseFeature>(response);
            scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = http;
            var callerDb = await scope.ServiceProvider.GetRequiredService<DapperMaticCallerDb>().OpenAsync(datasourceId);
            (await callerDb.UpdateAsync(new CachedOrder { Id = 2, CustomerId = 1, Total = 22 })).Should().Be(1);
            (await Read(client)).Should().Be("12,21|Acme Corp|2");
            await response.StartAsync();
        }

        (await Read(client)).Should().Be("13,22|Acme Corp|2");

        // And one the application commits after its response started (a unit of work committing once the handler has
        // returned): a read in between kept what was not committed yet, which the request's end makes stale. (SQL Server
        // would hold that read until the commit.)
        if (datasourceId != TestcontainersAssemblyFixture.DatasourceId_SqlServer)
        {
            var late = new CallbackResponseFeature();
            using var scope = factory.Services.CreateScope();
            var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "app")], "Test")), RequestServices = scope.ServiceProvider };
            http.Features.Set<IHttpResponseFeature>(late);
            scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = http;
            var callerDb = await scope.ServiceProvider.GetRequiredService<DapperMaticCallerDb>().OpenAsync(datasourceId);
            if (callerDb.Connection.State != System.Data.ConnectionState.Open)
            {
                callerDb.Connection.Open();
            }

            using (var tx = callerDb.Connection.BeginTransaction())
            {
                (await callerDb.UpdateAsync(new CachedOrder { Id = 2, CustomerId = 1, Total = 23 }, tx)).Should().Be(1);
                await late.StartAsync();
                (await Read(client)).Should().Be("13,22|Acme Corp|2");
                tx.Commit();
            }

            await late.CompleteAsync();
            (await Read(client)).Should().Be("13,23|Acme Corp|2");

            // Back as the steps below expect it, and kept again.
            await db.ExecuteAsync("UPDATE rc_orders SET total = 22 WHERE id = 2");
            await factory.Services.GetRequiredService<DapperMaticResultCache>().InvalidateAsync(datasourceId, "rc_orders", null);
            (await Read(client)).Should().Be("13,22|Acme Corp|2");
        }

        // A change made elsewhere is seen at once after InvalidateAsync.
        await db.ExecuteAsync("UPDATE rc_orders SET total = 14 WHERE id = 1");
        (await Read(client)).Should().Be("13,22|Acme Corp|2");
        await factory.Services.GetRequiredService<DapperMaticResultCache>().InvalidateAsync(datasourceId, "rc_orders", null);
        (await Read(client)).Should().Be("14,22|Acme Corp|2");

        // A schema change through DapperMatic makes the datasource's results stale.
        await db.ExecuteAsync("UPDATE rc_orders SET total = 15 WHERE id = 1");
        var column = new ColumnDto { ColumnName = "note", ProviderDataType = "varchar(20)", IsNullable = true };
        var added = await client.PostAsync($"{baseUrl}/rc_customers/columns", new StringContent(JsonSerializer.Serialize(column), Encoding.UTF8, "application/json"));
        added.StatusCode.Should().Be(HttpStatusCode.Created, await added.Content.ReadAsStringAsync());
        (await Read(client)).Should().Be("15,22|Acme Corp|2");

        await db.DropTableIfExistsAsync(schemaName, "rc_orders");
        await db.DropTableIfExistsAsync(schemaName, "rc_customers");
    }

    [Fact]
    public async Task Callers_never_share_kept_results_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        var store = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        using var factory = new WafWithInMemoryDatasourceRepository([datasource]).WithWebHostBuilder(web =>
            web.ConfigureServices(services =>
            {
                services.Configure<DapperMaticOptions>(options =>
                {
                    options.Policies.Clear();
                    options.CachedTables.Add(new DapperMaticCachedTable { TableName = "rc_notes", Duration = TimeSpan.FromMinutes(10) });
                    options.ResultCacheStore = store;
                });
                services.AddDapperMatic(dm =>
                    dm.Tenancy(t => t.FromClaim("tenant_id"))
                        .Policies(
                            DmPolicy.Allow(DmAction.Read, "clerk").OnTables("rc_notes").Where("tenant_id eq @tenant.id").OnlyColumns("id", "tenant_id", "body"),
                            DmPolicy.Allow(DmAction.Read, "auditor").OnTables("rc_notes").OnlyColumns("id", "tenant_id")
                        )
                );
            })
        );
        using (var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!))
        {
            db.Open();
            await db.DropTableIfExistsAsync(null, "rc_notes");
            await db.CreateTableIfNotExistsAsync(
                new DmTable(null, "rc_notes", [new DmColumn("id", typeof(int), isPrimaryKey: true), new DmColumn("tenant_id", typeof(int)), new DmColumn("body", typeof(string), length: 50)])
            );
            await db.ExecuteAsync("INSERT INTO rc_notes (id, tenant_id, body) VALUES (1, 1, 'one'), (2, 2, 'two')");
        }

        async Task<string> Read(string role, string? tenant)
        {
            using var client = factory.CreateAuthenticatedClient([new Claim(ClaimTypes.Role, role), .. tenant is null ? Array.Empty<Claim>() : [new Claim("tenant_id", tenant)]]);
            var response = await client.GetAsync($"/api/dm/d/{datasource.Id}/t/rc_notes/query?$orderby=id");
            var text = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, text);
            return string.Join(",", JsonDocument.Parse(text).RootElement.GetProperty("result").EnumerateArray().Select(r =>
                r.GetProperty("id").GetInt32() + ":" + (r.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.String ? body.GetString() : "-")));
        }

        // Each caller reads what its policies let it, as the second time too, when its results are kept.
        for (var i = 0; i < 2; i++)
        {
            (await Read("clerk", "1")).Should().Be("1:one");
            (await Read("clerk", "2")).Should().Be("2:two");
            (await Read("auditor", null)).Should().Be("1:-,2:-");
        }
    }

    [Fact]
    public async Task Get_responses_carry_etags_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        using var factory = new WafWithInMemoryDatasourceRepository([datasource]);
        using var client = factory.CreateClient();
        using (var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!))
        {
            db.Open();
            await db.DropTableIfExistsAsync(null, "rc_tags");
            await db.CreateTableIfNotExistsAsync(new DmTable(null, "rc_tags", [new DmColumn("id", typeof(int), isPrimaryKey: true), new DmColumn("name", typeof(string), length: 50)]));
            await db.ExecuteAsync("INSERT INTO rc_tags (id, name) VALUES (1, 'a')");
        }

        var url = $"/api/dm/d/{datasource.Id}/t/rc_tags/query";
        var first = await client.GetAsync(url);
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var tag = first.Headers.ETag;
        tag.Should().NotBeNull();
        first.Headers.CacheControl!.Private.Should().BeTrue();
        first.Headers.CacheControl.NoCache.Should().BeTrue();
        first.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        JsonDocument.Parse(await first.Content.ReadAsStringAsync()).RootElement.GetProperty("result").GetArrayLength().Should().Be(1);

        // Unchanged: 304, no body.
        using var again = new HttpRequestMessage(HttpMethod.Get, url);
        again.Headers.IfNoneMatch.Add(tag!);
        var notModified = await client.SendAsync(again);
        notModified.StatusCode.Should().Be(HttpStatusCode.NotModified);
        (await notModified.Content.ReadAsByteArrayAsync()).Should().BeEmpty();

        // Changed: the new body, with a new tag.
        var inserted = await client.PostAsync($"/api/dm/d/{datasource.Id}/t/rc_tags/rows", new StringContent("""{"rows":[{"id":2,"name":"b"}]}""", Encoding.UTF8, "application/json"));
        inserted.StatusCode.Should().Be(HttpStatusCode.OK);
        inserted.Headers.ETag.Should().BeNull();
        using var afterWrite = new HttpRequestMessage(HttpMethod.Get, url);
        afterWrite.Headers.IfNoneMatch.Add(tag!);
        var changed = await client.SendAsync(afterWrite);
        changed.StatusCode.Should().Be(HttpStatusCode.OK);
        changed.Headers.ETag.Should().NotBe(tag);
        JsonDocument.Parse(await changed.Content.ReadAsStringAsync()).RootElement.GetProperty("result").GetArrayLength().Should().Be(2);

        // Turned off, there is none.
        using var off = factory.WithWebHostBuilder(web => web.ConfigureServices(s => s.Configure<DapperMaticOptions>(o => o.ETags = false))).CreateClient();
        (await off.GetAsync(url)).Headers.ETag.Should().BeNull();
    }

    [Fact]
    public async Task A_version_the_store_lost_is_made_again_never_assumed_Async()
    {
        var store = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var options = new DapperMaticOptions { ResultCacheStore = store };
        options.CachedTables.Add(new DapperMaticCachedTable { TableName = "t", Duration = TimeSpan.FromMinutes(1) });
        options.CachedTables.Add(new DapperMaticCachedTable { TableName = "v", Duration = TimeSpan.FromSeconds(30), Reads = ["t"] });
        var cache = new DapperMaticResultCache(Options.Create(options), new ServiceCollection().BuildServiceProvider());
        var table = new Dml.Rest.RestTable { SchemaName = null, Name = "t", QualifiedName = "t", Columns = [] };
        var view = new Dml.Rest.RestTable { SchemaName = null, Name = "v", QualifiedName = "v", Columns = [] };
        var other = new Dml.Rest.RestTable { SchemaName = null, Name = "other", QualifiedName = "other", Columns = [] };
        var rest = cache.For("ds", DbProviderType.Sqlite, string.Empty)!;

        rest.Duration([table]).Should().Be(TimeSpan.FromMinutes(1));
        rest.Duration([table, view]).Should().Be(TimeSpan.FromSeconds(30));
        rest.Duration([table, other]).Should().BeNull();

        var first = await rest.Versions([view], default);
        (await rest.Versions([view], default)).Should().Be(first);

        // A write to the table a view reads makes the view's results stale.
        await cache.WrittenAsync("ds", DbProviderType.Sqlite, [(null, "t")], default);
        var second = await rest.Versions([view], default);
        second.Should().NotBe(first);

        // The store dropping a version brings back nothing read before.
        foreach (var key in new[] { "dm:v:0:\u001fds", "dm:v:0:\u001fds\u001f\u001ft" })
        {
            await store.RemoveAsync(key);
        }

        var third = await rest.Versions([view], default);
        third.Should().NotBe(second).And.NotBe(first);
    }

    [Fact]
    public void Session_values_key_results_by_the_text_the_database_reads()
    {
        static string Key(params (string Name, object? Value)[] values) => DapperMaticService.SessionKey(values.Select(v => new KeyValuePair<string, object?>(v.Name, v.Value)));

        // Callers with several values each (claims of one type) never share: the key holds the values, not a list's type.
        Key(("app.orgs", new List<string> { "a", "b" })).Should().NotBe(Key(("app.orgs", new List<string> { "c", "d" })));
        Key(("app.orgs", new List<string> { "a", "b" })).Should().Be(Key(("app.orgs", "a,b")));
        Key(("x", null)).Should().NotBe(Key(("x", "null"))).And.NotBe(Key(("x", string.Empty)));
        Key(("a", "1\u001f1:b=1:2")).Should().NotBe(Key(("a", "1"), ("b", "2")));
    }

    [Fact]
    public async Task Applications_sharing_a_store_never_share_results_Async()
    {
        var store = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var table = new Dml.Rest.RestTable { SchemaName = null, Name = "t", QualifiedName = "t", Columns = [] };
        DapperMaticResultCache Cache(string? prefix, string application)
        {
            var options = new DapperMaticOptions { ResultCacheStore = store, ResultCacheKeyPrefix = prefix };
            options.CachedTables.Add(new DapperMaticCachedTable { TableName = "t", Duration = TimeSpan.FromMinutes(1) });
            var services = new ServiceCollection().AddSingleton<Microsoft.Extensions.Hosting.IHostEnvironment>(new Environment(application));
            return new DapperMaticResultCache(Options.Create(options), services.BuildServiceProvider());
        }

        // Two applications (or staging and production) using one Redis, each with a datasource "main".
        var shop = Cache(null, "shop");
        var admin = Cache(null, "admin");
        shop.For("main", DbProviderType.Sqlite, string.Empty)!.Scope.Should().NotBe(admin.For("main", DbProviderType.Sqlite, string.Empty)!.Scope);
        var before = await admin.For("main", DbProviderType.Sqlite, string.Empty)!.Versions([table], default);
        await shop.WrittenAsync("main", DbProviderType.Sqlite, [(null, "t")], default);
        (await admin.For("main", DbProviderType.Sqlite, string.Empty)!.Versions([table], default)).Should().Be(before);

        // Instances of one application share them; so do applications given one prefix.
        Cache(null, "shop").For("main", DbProviderType.Sqlite, string.Empty)!.Scope.Should().Be(shop.For("main", DbProviderType.Sqlite, string.Empty)!.Scope);
        Cache("shared", "shop").For("main", DbProviderType.Sqlite, string.Empty)!.Scope
            .Should().Be(Cache("shared", "admin").For("main", DbProviderType.Sqlite, string.Empty)!.Scope);
    }

    [MJCZone.DapperMatic.DataAnnotations.DmTable(tableName: "rc_orders")]
    public sealed class CachedOrder
    {
        [MJCZone.DapperMatic.DataAnnotations.DmColumn("id", isPrimaryKey: true)]
        public int Id { get; set; }

        [MJCZone.DapperMatic.DataAnnotations.DmColumn("customer_id")]
        public int CustomerId { get; set; }

        [MJCZone.DapperMatic.DataAnnotations.DmColumn("total")]
        public int Total { get; set; }
    }

    // A response whose start and end callbacks the test runs.
    private sealed class CallbackResponseFeature : HttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _starting = [];
        private readonly List<(Func<object, Task> Callback, object State)> _completed = [];

        public override void OnStarting(Func<object, Task> callback, object state) => _starting.Add((callback, state));

        public override void OnCompleted(Func<object, Task> callback, object state) => _completed.Add((callback, state));

        public async Task StartAsync()
        {
            foreach (var (callback, state) in _starting)
            {
                await callback(state);
            }
        }

        public async Task CompleteAsync()
        {
            foreach (var (callback, state) in _completed)
            {
                await callback(state);
            }
        }
    }

    // The application a cache is in, by name.
    private sealed class Environment(string application) : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string ApplicationName { get; set; } = application;

        public string EnvironmentName { get; set; } = "Production";

        public string ContentRootPath { get; set; } = "/";

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
