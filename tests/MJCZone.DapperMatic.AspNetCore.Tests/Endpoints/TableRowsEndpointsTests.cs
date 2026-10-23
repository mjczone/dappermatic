// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MJCZone.DapperMatic.AspNetCore.Factories;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Models.Responses;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.AspNetCore.Tests.Factories;
using MJCZone.DapperMatic.AspNetCore.Tests.Infrastructure;
using MJCZone.DapperMatic.Models;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Endpoints;

/// <summary>
/// Writing rows, and querying them with OData-shaped options, JSON trees and the relations an application declares.
/// </summary>
public class TableRowsEndpointsTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private readonly TestcontainersAssemblyFixture _fixture;

    public TableRowsEndpointsTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer, "dbo")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql, "public")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql, null)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite, null)]
    public async Task Rows_are_written_and_queried_through_declared_relations_Async(string datasourceId, string? schemaName)
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == datasourceId);
        using var baseFactory = new WafWithInMemoryDatasourceRepository([datasource]);
        using var factory = baseFactory.WithWebHostBuilder(web =>
            web.ConfigureServices(services =>
                services.Configure<DapperMaticOptions>(options =>
                {
                    options.Relations.Add(new DapperMaticRelation { TableName = "rest_orders", Name = "customer", OtherTableName = "rest_customers", Columns = ["customer_id"], OtherColumns = ["id"] });
                    options.Relations.Add(new DapperMaticRelation { TableName = "rest_customers", Name = "orders", OtherTableName = "rest_orders", Columns = ["id"], OtherColumns = ["customer_id"], Many = true });
                })
            )
        );
        using var client = factory.CreateClient();

        using (var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!))
        {
            db.Open();
            await db.DropTableIfExistsAsync(schemaName, "rest_orders");
            await db.DropTableIfExistsAsync(schemaName, "rest_customers");
            await db.CreateTableIfNotExistsAsync(
                new DmTable(
                    schemaName,
                    "rest_customers",
                    [
                        new DmColumn("id", typeof(int), isPrimaryKey: true),
                        new DmColumn("name", typeof(string), length: 50),
                        new DmColumn("country", typeof(string), length: 50, isNullable: true),
                    ]
                )
            );
            await db.CreateTableIfNotExistsAsync(
                new DmTable(
                    schemaName,
                    "rest_orders",
                    [
                        new DmColumn("id", typeof(int), isPrimaryKey: true),
                        new DmColumn("customer_id", typeof(int), isNullable: true),
                        new DmColumn("status", typeof(string), length: 20),
                    ]
                )
            );
        }

        var baseUrl = schemaName is not null ? $"/api/dm/d/{datasourceId}/s/{schemaName}/t" : $"/api/dm/d/{datasourceId}/t";
        async Task<int> Write(string path, object body, HttpStatusCode expected = HttpStatusCode.OK)
        {
            var response = await client.PostAsync($"{baseUrl}/{path}", new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));
            response.StatusCode.Should().Be(expected, await response.Content.ReadAsStringAsync());
            return expected == HttpStatusCode.OK ? (await response.ReadAsJsonAsync<RowsResponse>())!.Affected : -1;
        }

        async Task<(List<JsonElement> Rows, long? Total)> Query(string tableAndQuery, object? body = null)
        {
            var response = body is null
                ? await client.GetAsync($"{baseUrl}/{tableAndQuery}")
                : await client.PostAsync($"{baseUrl}/{tableAndQuery}", new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));
            var text = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, text);
            var json = JsonDocument.Parse(text).RootElement;
            var pagination = json.GetProperty("pagination");
            return (
                [.. json.GetProperty("result").EnumerateArray()],
                pagination.TryGetProperty("total", out var total) && total.ValueKind == JsonValueKind.Number ? total.GetInt64() : null
            );
        }

        // Rows inserted, all or none, each value converted to its column's type.
        (await Write("rest_customers/rows", new { rows = new object[] { new { id = 1, name = "Alice", country = "France" }, new { id = 2, name = "Bob", country = "Germany" } } })).Should().Be(2);
        (await Write("rest_orders/rows", new { rows = new object?[] { new { id = 10, customer_id = 1, status = "open" }, new { id = 11, customer_id = "2", status = "shipped" }, new { id = 12, customer_id = (int?)null, status = "open" } } })).Should().Be(3);
        await Write("rest_orders/rows", new { rows = new object[] { new { id = 13, nosuch = 1 } } }, HttpStatusCode.BadRequest);

        // A declared relation in a condition and an expand, with the count.
        var (french, total) = await Query("rest_orders/query?$filter=customer/country eq 'France'&$expand=customer&$count=true");
        total.Should().Be(1);
        french.Single().GetProperty("id").GetInt32().Should().Be(10);
        french.Single().GetProperty("customer").GetProperty("name").GetString().Should().Be("Alice");

        // The same as a JSON tree in a POST body: customers with a shipped order, with their orders.
        var (shipped, _) = await Query(
            "rest_customers/query",
            JsonSerializer.Deserialize<JsonElement>(
                """{"where": {"any": {"path": "orders", "as": "o", "where": {"eq": [{"path": "o/status"}, {"value": "shipped"}]}}}, "expand": "orders($orderby=id)", "order": "id"}"""
            )
        );
        shipped.Single().GetProperty("name").GetString().Should().Be("Bob");
        shipped.Single().GetProperty("orders").EnumerateArray().Select(o => o.GetProperty("id").GetInt32()).Should().Equal(11);

        // Pages by cursor: each full page gives the next one's cursor, and $after reads from it.
        var page1 = JsonDocument.Parse(await (await client.GetAsync($"{baseUrl}/rest_orders/query?$orderby=status&$top=2&$select=id")).Content.ReadAsStringAsync()).RootElement;
        var cursor = page1.GetProperty("pagination").GetProperty("nextCursor").GetString();
        cursor.Should().NotBeNullOrEmpty();
        var page2 = JsonDocument.Parse(await (await client.GetAsync($"{baseUrl}/rest_orders/query?$orderby=status&$top=2&$select=id&$after={Uri.EscapeDataString(cursor!)}")).Content.ReadAsStringAsync()).RootElement;
        page1.GetProperty("result").EnumerateArray().Concat(page2.GetProperty("result").EnumerateArray()).Select(r => r.GetProperty("id").GetInt32())
            .Should().Equal(10, 12, 11);
        page2.GetProperty("pagination").GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
        (await client.GetAsync($"{baseUrl}/rest_orders/query?$after=junk")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Relations not declared, and conditions that are not, are refused.
        (await client.GetAsync($"{baseUrl}/rest_customers/query?$expand=invoices")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync($"{baseUrl}/rest_orders/query?$filter=status eq")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync($"{baseUrl}/rest_orders/query?$filter=nosuch eq 1")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Updates and deletes by condition; a write with no condition is refused.
        (await Write("rest_orders/rows/update", new { set = new { status = "closed" }, filter = "status eq 'open'" })).Should().Be(2);
        (await Write("rest_orders/rows/delete", new { filter = "customer_id eq null" })).Should().Be(1);
        await Write("rest_orders/rows/delete", new { }, HttpStatusCode.BadRequest);

        // The simple filters still work, and agree with the OData options.
        (await Query("rest_orders/query?status.eq=closed")).Rows.Should().HaveCount(1);
        (await Query("rest_orders/query?$filter=status ne 'closed'&$select=id")).Rows.Single().EnumerateObject().Select(p => p.Name).Should().Equal("id");
        (await Write("rest_orders/rows/delete", new { filter = "true" })).Should().Be(2);
    }

    /// <summary>
    /// A batch: writes in order, all or none, a later one using a key an earlier one's insert generated.
    /// </summary>
    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer, "dbo")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql, "public")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql, null)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite, null)]
    public async Task A_batch_writes_all_or_nothing_with_references_Async(string datasourceId, string? schemaName)
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == datasourceId);
        using var factory = new WafWithInMemoryDatasourceRepository([datasource]);
        using var client = factory.CreateClient();
        var connections = factory.Services.GetRequiredService<IDbConnectionFactory>();
        using (var db = connections.CreateConnection(datasource.Provider!, datasource.ConnectionString!))
        {
            db.Open();
            await db.DropTableIfExistsAsync(schemaName, "bat_orders");
            await db.DropTableIfExistsAsync(schemaName, "bat_customers");
            await db.CreateTableIfNotExistsAsync(
                new DmTable(schemaName, "bat_customers", [new DmColumn("id", typeof(int), isPrimaryKey: true, isAutoIncrement: true), new DmColumn("name", typeof(string), length: 50)])
            );
            await db.CreateTableIfNotExistsAsync(
                new DmTable(
                    schemaName,
                    "bat_orders",
                    [
                        new DmColumn("id", typeof(int), isPrimaryKey: true, isAutoIncrement: true),
                        new DmColumn("customer_id", typeof(int)),
                        new DmColumn("amount", typeof(int)),
                    ]
                )
            );
        }

        async Task<HttpResponseMessage> Batch(object body) =>
            await client.PostAsync($"/api/dm/d/{datasourceId}/batch", new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));
        async Task<long> Count(string table)
        {
            using var db = connections.CreateConnection(datasource.Provider!, datasource.ConnectionString!);
            db.Open();
            return await Dapper.SqlMapper.ExecuteScalarAsync<long>(db, $"SELECT COUNT(*) FROM {db.GetSchemaQualifiedTableName(schemaName, table)}");
        }

        var response = await Batch(new
        {
            operations = new object[]
            {
                new { op = "insert", table = "bat_customers", schema = schemaName, rows = new[] { new { name = "Acme" } } },
                new { op = "insert", table = "bat_orders", schema = schemaName, rows = new[] { new { customer_id = "$1.id", amount = 10 }, new { customer_id = "$1.id", amount = 20 } } },
                new { op = "update", table = "bat_orders", schema = schemaName, set = new { amount = 30 }, filter = "amount eq 20" },
            },
        });
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, text);
        var results = JsonDocument.Parse(text).RootElement.GetProperty("results").EnumerateArray().ToList();
        var customerId = results[0].GetProperty("rows")[0].GetProperty("id").GetInt32();
        results[1].GetProperty("rows").EnumerateArray().Select(r => r.GetProperty("customer_id").GetInt32()).Should().Equal(customerId, customerId);
        results.Select(r => r.GetProperty("affected").GetInt32()).Should().Equal(1, 2, 1);

        // Nothing is written when an operation fails, when a reference is wrong, or in a dry run.
        (await Batch(new
        {
            operations = new object[]
            {
                new { op = "insert", table = "bat_customers", schema = schemaName, rows = new[] { new { name = "Globex" } } },
                new { op = "insert", table = "bat_orders", schema = schemaName, rows = new[] { new { nosuch = 1 } } },
            },
        })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Batch(new { operations = new object[] { new { op = "insert", table = "bat_orders", schema = schemaName, rows = new[] { new { customer_id = "$2.id", amount = 1 } } } } }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Batch(new { dryRun = true, operations = new object[] { new { op = "delete", table = "bat_orders", schema = schemaName, filter = "true" } } })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Count("bat_customers")).Should().Be(1);
        (await Count("bat_orders")).Should().Be(2);
    }

    /// <summary>
    /// Rows written with their related rows: a customer an order refers to first, the order, then its lines with its
    /// key; and saved again, lines with a key updated, those without inserted, those left out left alone.
    /// </summary>
    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer, "dbo")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql, "public")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql, null)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite, null)]
    public async Task Rows_are_written_with_their_related_rows_Async(string datasourceId, string? schemaName)
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == datasourceId);
        using var baseFactory = new WafWithInMemoryDatasourceRepository([datasource]);
        using var factory = baseFactory.WithWebHostBuilder(web =>
            web.ConfigureServices(services =>
                services.Configure<DapperMaticOptions>(options =>
                {
                    options.Relations.Add(new DapperMaticRelation { TableName = "nest_orders", Name = "customer", OtherTableName = "nest_customers", Columns = ["customer_id"], OtherColumns = ["id"] });
                    options.Relations.Add(new DapperMaticRelation { TableName = "nest_orders", Name = "lines", OtherTableName = "nest_lines", Columns = ["id"], OtherColumns = ["order_id"], Many = true });
                })
            )
        );
        using var client = factory.CreateClient();
        var connections = factory.Services.GetRequiredService<IDbConnectionFactory>();
        using (var db = connections.CreateConnection(datasource.Provider!, datasource.ConnectionString!))
        {
            db.Open();
            foreach (var name in new[] { "nest_lines", "nest_orders", "nest_customers" })
            {
                await db.DropTableIfExistsAsync(schemaName, name);
            }

            DmColumn Key() => new("id", typeof(int), isPrimaryKey: true, isAutoIncrement: true);
            await db.CreateTableIfNotExistsAsync(new DmTable(schemaName, "nest_customers", [Key(), new DmColumn("name", typeof(string), length: 50)]));
            await db.CreateTableIfNotExistsAsync(new DmTable(schemaName, "nest_orders", [Key(), new DmColumn("customer_id", typeof(int)), new DmColumn("amount", typeof(int))]));
            await db.CreateTableIfNotExistsAsync(new DmTable(schemaName, "nest_lines", [Key(), new DmColumn("order_id", typeof(int)), new DmColumn("qty", typeof(int))]));
        }

        var baseUrl = schemaName is not null ? $"/api/dm/d/{datasourceId}/s/{schemaName}/t" : $"/api/dm/d/{datasourceId}/t";
        async Task<(HttpStatusCode Status, JsonElement Body)> Post(string path, object body)
        {
            var response = await client.PostAsync($"{baseUrl}/{path}", new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));
            var text = await response.Content.ReadAsStringAsync();
            return (response.StatusCode, JsonDocument.Parse(text).RootElement);
        }

        async Task<List<(int Id, int OrderId, int Qty)>> Lines()
        {
            using var db = connections.CreateConnection(datasource.Provider!, datasource.ConnectionString!);
            db.Open();
            var rows = await Dapper.SqlMapper.QueryAsync<(int, int, int)>(db, $"SELECT id, order_id, qty FROM {db.GetSchemaQualifiedTableName(schemaName, "nest_lines")} ORDER BY id");
            return [.. rows];
        }

        var (status, created) = await Post("nest_orders/rows", new { rows = new object[] { new { amount = 10, customer = new { name = "Acme" }, lines = new object[] { new { qty = 1 }, new { qty = 2 } } } } });
        status.Should().Be(HttpStatusCode.OK, created.GetRawText());
        var order = created.GetProperty("rows")[0];
        var orderId = order.GetProperty("id").GetInt32();
        order.GetProperty("customer_id").GetInt32().Should().Be(order.GetProperty("customer").GetProperty("id").GetInt32());
        var lines = await Lines();
        lines.Select(l => (l.OrderId, l.Qty)).Should().Equal((orderId, 1), (orderId, 2));

        // Saved again: the order and its first line changed, a new line added, the second left alone.
        var (saved, body) = await Post("nest_orders/rows/save", new { rows = new object[] { new { id = orderId, amount = 11, lines = new object[] { new { id = lines[0].Id, qty = 5 }, new { qty = 3 } } } } });
        saved.Should().Be(HttpStatusCode.OK, body.GetRawText());
        (await Lines()).Select(l => (l.OrderId, l.Qty)).Should().Equal((orderId, 5), (orderId, 2), (orderId, 3));

        // Refused: a relation not declared, and a generated key no row has.
        (await Post("nest_orders/rows", new { rows = new object[] { new { amount = 1, notes = new object[] { new { text = "x" } } } } })).Status.Should().Be(HttpStatusCode.BadRequest);
        (await Post("nest_orders/rows/save", new { rows = new object[] { new { id = 999, amount = 1 } } })).Status.Should().Be(HttpStatusCode.BadRequest);
        (await Lines()).Should().HaveCount(3);
    }

    /// <summary>
    /// $search: every word in a searchable column, by the database's full-text index where it has one, or a contains
    /// match; the index matches words, the contains match parts of them.
    /// </summary>
    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer, "dbo")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql, "public")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql, null)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite, null)]
    public async Task Search_finds_rows_by_their_words_Async(string datasourceId, string? schemaName)
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == datasourceId);
        using var baseFactory = new WafWithInMemoryDatasourceRepository([datasource]);
        using var factory = baseFactory.WithWebHostBuilder(web =>
            web.ConfigureServices(services =>
                services.Configure<DapperMaticOptions>(options =>
                {
                    options.Searchable.Add(new DapperMaticSearchable { TableName = "srch_plain", Columns = ["title", "body"] });
                    options.Searchable.Add(new DapperMaticSearchable { TableName = "srch_native", Columns = ["title", "body"] });
                    options.Searchable.Add(new DapperMaticSearchable { TableName = "srch_title", Columns = ["title"] });
                })
            )
        );
        using var client = factory.CreateClient();
        var provider = datasource.Provider!.ToLowerInvariant();
        using (var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!))
        {
            db.Open();
            foreach (var name in new[] { "srch_native_fts", "srch_title_fts", "srch_plain", "srch_native", "srch_title" })
            {
                await db.DropTableIfExistsAsync(schemaName, name);
            }

            foreach (var name in new[] { "srch_plain", "srch_native", "srch_title" })
            {
                await db.CreateTableIfNotExistsAsync(
                    new DmTable(schemaName, name, [new DmColumn("id", typeof(int), isPrimaryKey: true), new DmColumn("title", typeof(string), length: 100), new DmColumn("body", typeof(string), length: 200)])
                );
                await Dapper.SqlMapper.ExecuteAsync(
                    db,
                    $"INSERT INTO {db.GetSchemaQualifiedTableName(schemaName, name)} (id, title, body) VALUES (1, 'Acme rockets', 'Fast and loud'), (2, 'Globex', 'Acme partner for rockets'), (3, 'Initech', 'Printers')"
                );
            }

            var native = db.GetSchemaQualifiedTableName(schemaName, "srch_native");
            var indexed = provider switch
            {
                "postgresql" => $"CREATE INDEX ix_srch_native ON {native} USING gin (to_tsvector('english', title || ' ' || body))",
                "mysql" => $"ALTER TABLE {native} ADD FULLTEXT ft_srch_native (title, body)",
                "sqlite" => "CREATE VIRTUAL TABLE srch_native_fts USING fts5(title, body, content='srch_native', content_rowid='id'); "
                    + "INSERT INTO srch_native_fts(rowid, title, body) SELECT id, title, body FROM srch_native",
                _ => null,
            };
            if (indexed is not null)
            {
                await Dapper.SqlMapper.ExecuteAsync(db, indexed);
            }

            // An index over more columns than the table is declared searchable by.
            var title = db.GetSchemaQualifiedTableName(schemaName, "srch_title");
            var wider = provider switch
            {
                "postgresql" => $"CREATE INDEX ix_srch_title ON {title} USING gin (to_tsvector('english', title || ' ' || body))",
                "mysql" => $"ALTER TABLE {title} ADD FULLTEXT ft_srch_title (title, body)",
                "sqlite" => "CREATE VIRTUAL TABLE srch_title_fts USING fts5(title, body, content='srch_title', content_rowid='id'); "
                    + "INSERT INTO srch_title_fts(rowid, title, body) SELECT id, title, body FROM srch_title",
                _ => null,
            };
            if (wider is not null)
            {
                await Dapper.SqlMapper.ExecuteAsync(db, wider);
            }
        }

        var baseUrl = schemaName is not null ? $"/api/dm/d/{datasourceId}/s/{schemaName}/t" : $"/api/dm/d/{datasourceId}/t";
        async Task<int[]> Ids(string table, string query)
        {
            var response = await client.GetAsync($"{baseUrl}/{table}/query?$orderby=id&{query}");
            var text = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, text);
            return [.. JsonDocument.Parse(text).RootElement.GetProperty("result").EnumerateArray().Select(r => r.GetProperty("id").GetInt32())];
        }

        foreach (var table in new[] { "srch_plain", "srch_native" })
        {
            (await Ids(table, "$search=rockets")).Should().Equal(1, 2);
            (await Ids(table, "$search=acme rockets")).Should().Equal(1, 2);
            (await Ids(table, "$search=printers")).Should().Equal(3);
            (await Ids(table, $"$search={Uri.EscapeDataString("\"acme partner\"")}")).Should().Equal(2);
            (await Ids(table, "$search=rockets&$filter=id gt 1")).Should().Equal(2);
        }

        // A part of a word: the contains match finds it, a full-text index does not.
        (await Ids("srch_plain", "$search=ock")).Should().Equal(1, 2);
        if (provider != "sqlserver")
        {
            (await Ids("srch_native", "$search=ock")).Should().BeEmpty();
        }

        (await client.GetAsync($"{baseUrl}/srch_other/query?$search=x")).StatusCode.Should().NotBe(HttpStatusCode.OK);

        // A table searchable by its title is searched by its title, whatever its index covers besides: a column the
        // caller may not read must not be searched through it.
        (await Ids("srch_title", "$search=rockets")).Should().Equal(1);
        (await Ids("srch_title", "$search=loud")).Should().BeEmpty();
    }

    [Fact]
    public async Task Related_rows_are_written_only_where_the_request_may_write_their_table_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        using var baseFactory = new WafWithInMemoryDatasourceRepository([datasource]);
        using var factory = baseFactory.WithWebHostBuilder(web =>
            web.ConfigureServices(services =>
            {
                services.Configure<DapperMaticOptions>(options =>
                {
                    options.Policies.Clear();
                    options.Relations.Add(new DapperMaticRelation { TableName = "held_orders", Name = "customer", OtherTableName = "held_customers", Columns = ["customer_id"], OtherColumns = ["id"] });
                });

                // The application decides, and lets no request write customers.
                services.RemoveAll<IDapperMaticPermissions>();
                services.AddSingleton<IDapperMaticPermissions>(new NoCustomerWrites());
            })
        );
        using var client = factory.CreateAuthenticatedClient(new System.Security.Claims.Claim("sub", "someone"));
        var connections = factory.Services.GetRequiredService<IDbConnectionFactory>();
        using var db = connections.CreateConnection(datasource.Provider!, datasource.ConnectionString!);
        db.Open();
        foreach (var name in new[] { "held_orders", "held_customers" })
        {
            await db.DropTableIfExistsAsync(null, name);
        }

        DmColumn Key() => new("id", typeof(int), isPrimaryKey: true, isAutoIncrement: true);
        await db.CreateTableIfNotExistsAsync(new DmTable(null, "held_customers", [Key(), new DmColumn("name", typeof(string), length: 50)]));
        await db.CreateTableIfNotExistsAsync(new DmTable(null, "held_orders", [Key(), new DmColumn("customer_id", typeof(int)), new DmColumn("amount", typeof(int))]));
        await Dapper.SqlMapper.ExecuteAsync(db, "INSERT INTO held_customers (name) VALUES ('Acme')");
        async Task<HttpStatusCode> Insert(object row) =>
            (await client.PostAsync($"/api/dm/d/{datasource.Id}/t/held_orders/rows", new StringContent(JsonSerializer.Serialize(new { rows = new[] { row } }), Encoding.UTF8, "application/json"))).StatusCode;

        // An order may be inserted, but not a customer changed or added through it.
        (await Insert(new { amount = 1, customer = new { id = 1, name = "Renamed" } })).Should().Be(HttpStatusCode.Forbidden);
        (await Insert(new { amount = 1, customer = new { name = "New" } })).Should().Be(HttpStatusCode.Forbidden);
        (await Dapper.SqlMapper.QueryAsync<string>(db, "SELECT name FROM held_customers")).Should().Equal("Acme");
        (await Dapper.SqlMapper.ExecuteScalarAsync<long>(db, "SELECT COUNT(*) FROM held_orders")).Should().Be(0);
        (await Insert(new { amount = 1, customer_id = 1 })).Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Written_rows_are_answered_with_what_the_caller_may_see_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        using var baseFactory = new WafWithInMemoryDatasourceRepository([datasource]);
        using var factory = baseFactory.WithWebHostBuilder(web =>
            web.ConfigureServices(services =>
                services.Configure<DapperMaticOptions>(options =>
                {
                    options.Policies.Add(DmPolicy.Restrict(DmAction.Read).OnTables("echo_rows").ExceptColumns("owner"));
                    options.Policies.Add(DmPolicy.Restrict(DmAction.Create).OnTables("echo_rows").Preset("owner", "kept"));
                })
            )
        );
        using var client = factory.CreateClient();
        using (var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!))
        {
            db.Open();
            await db.DropTableIfExistsAsync(null, "echo_rows");
            await db.CreateTableIfNotExistsAsync(
                new DmTable(null, "echo_rows", [new DmColumn("id", typeof(int), isPrimaryKey: true, isAutoIncrement: true), new DmColumn("owner", typeof(string), length: 20), new DmColumn("amount", typeof(int))])
            );
        }

        async Task<JsonElement> Post(string path, object body)
        {
            var response = await client.PostAsync($"/api/dm/d/{datasource.Id}/{path}", new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));
            var text = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, text);
            return JsonDocument.Parse(text).RootElement;
        }

        // The preset the policies wrote is not told: the caller may not read that column. Its own values and the key are.
        var row = (await Post("t/echo_rows/rows", new { rows = new[] { new { amount = 5 } } })).GetProperty("rows")[0];
        row.EnumerateObject().Select(p => p.Name).Order().Should().Equal("amount", "id");

        // Nor in a batch's answer, which later operations could copy from.
        var batch = await Post("batch", new { operations = new object[] { new { op = "insert", table = "echo_rows", rows = new[] { new { amount = 6 } } } } });
        batch.GetProperty("results")[0].GetProperty("rows")[0].EnumerateObject().Select(p => p.Name).Order().Should().Equal("amount", "id");
    }

    // Lets a request do anything but write customers.
    private sealed class NoCustomerWrites : IDapperMaticPermissions
    {
        public Task<bool> IsAuthorizedAsync(DapperMaticOperation context) =>
            Task.FromResult(!(context.TableName == "held_customers" && context.Operation is "tables/insert-rows" or "tables/update-rows"));
    }

    /// <summary>
    /// Requests and their statements are traced for OpenTelemetry: the statement's SQL by parameter name, never a value.
    /// </summary>
    [Fact]
    public async Task Requests_and_statements_are_traced_without_values_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        using var factory = new WafWithInMemoryDatasourceRepository([datasource]);
        using var client = factory.CreateClient();
        using (var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!))
        {
            db.Open();
            await db.DropTableIfExistsAsync(null, "otel_items");
            await db.CreateTableIfNotExistsAsync(new DmTable(null, "otel_items", [new DmColumn("id", typeof(int), isPrimaryKey: true), new DmColumn("name", typeof(string), length: 50)]));
        }

        var activities = new System.Collections.Concurrent.ConcurrentBag<System.Diagnostics.Activity>();
        using var listener = new System.Diagnostics.ActivityListener
        {
            ShouldListenTo = source => source.Name == DmTelemetry.Name,
            Sample = (ref System.Diagnostics.ActivityCreationOptions<System.Diagnostics.ActivityContext> _) => System.Diagnostics.ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activities.Add,
        };
        System.Diagnostics.ActivitySource.AddActivityListener(listener);

        (await client.GetAsync($"/api/dm/d/{datasource.Id}/t/otel_items/query?$filter=name eq 'very secret'")).StatusCode.Should().Be(HttpStatusCode.OK);

        var statement = activities.Single(a => a.DisplayName == "SELECT otel_items");
        var query = statement.GetTagItem("db.query.text") as string;
        query.Should().Contain("@").And.NotContain("very secret");
        statement.GetTagItem("db.system.name").Should().Be("sqlite");
        var request = activities.Single(a => a.DisplayName == "DapperMatic tables/query" && (string?)a.GetTagItem("dappermatic.table") == "otel_items");
        statement.ParentSpanId.Should().Be(request.SpanId);
    }
}
