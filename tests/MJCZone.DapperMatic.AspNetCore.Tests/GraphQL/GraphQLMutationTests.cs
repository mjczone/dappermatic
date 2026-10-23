// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Dapper;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MJCZone.DapperMatic.AspNetCore.Factories;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Tests.GraphQL;

/// <summary>
/// Writing through GraphQL: inserts, updates and deletes returning their rows as the caller reads them; a mutation's
/// fields all or none.
/// </summary>
public class GraphQLMutationTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private readonly TestcontainersAssemblyFixture _fixture;

    public GraphQLMutationTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite, null)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql, "public")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer, "dbo")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql, null)]
    public async Task Mutations_return_their_rows_and_write_all_or_none_Async(string datasourceId, string? schema)
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == datasourceId);
        using var factory = GraphQLTestHost.Create(datasource);
        await GraphQLTestHost.CreateTablesAsync(factory, datasource, schema);
        using var client = GraphQLTestHost.Client(factory);
        var path = $"/api/dm/d/{datasourceId}/graphql";
        async Task<long> CountAsync(string where)
        {
            using var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!);
            return await db.ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM gq_orders WHERE {where}");
        }

        // An insert with a related row: the rows as written, with their keys, in the order given; edges for Relay.
        var inserted = (await GraphQLTestHost.PostAsync(
            client,
            path,
            """
            mutation {
              insert_gq_customers(rows: [{ name: "Acme", country: "FR", orders: [{ total: 12.5, status: "open" }, { total: 40, status: "draft" }] }]) {
                affected
                rows { id name orders(orderBy: [{ total: ASC }]) { total status } }
              }
              insert_gq_orders(rows: [{ total: 7, status: "open" }, { total: 3, status: "draft" }]) {
                affected
                rows { id total status }
                edges { cursor node { nodeId } }
              }
            }
            """
        )).Data();
        var customer = inserted.GetProperty("insert_gq_customers");
        customer.GetProperty("affected").GetInt32().Should().Be(1);
        var acme = customer.GetProperty("rows")[0];
        acme.GetProperty("orders").EnumerateArray().Select(o => o.GetProperty("total").GetDecimal()).Should().Equal(12.5m, 40m);
        var orders = inserted.GetProperty("insert_gq_orders");
        orders.GetProperty("rows").EnumerateArray().Select(o => o.GetProperty("total").GetDecimal()).Should().Equal(7m, 3m);
        orders.GetProperty("edges").EnumerateArray().Should().OnlyContain(e => e.GetProperty("cursor").GetString()!.Length > 0);

        // An update by condition, its rows as they are after it.
        var updated = (await GraphQLTestHost.PostAsync(
            client,
            path,
            """mutation { update_gq_orders(where: { status: { eq: "draft" } }, set: { status: "open" }) { affected rows { total status } } }"""
        )).Data().GetProperty("update_gq_orders");
        updated.GetProperty("affected").GetInt32().Should().Be(2);
        updated.GetProperty("rows").EnumerateArray().Should().OnlyContain(r => r.GetProperty("status").GetString() == "open");
        (await CountAsync("status = 'draft'")).Should().Be(0);

        // A delete by global id, the row as it was.
        var nodeId = orders.GetProperty("edges")[0].GetProperty("node").GetProperty("nodeId").GetString();
        var deleted = (await GraphQLTestHost.PostAsync(
            client,
            path,
            "mutation Delete($id: ID) { delete_gq_orders_by_key(nodeId: $id) { affected row { total status } } }",
            new { id = nodeId }
        )).Data().GetProperty("delete_gq_orders_by_key");
        deleted.GetProperty("affected").GetInt32().Should().Be(1);
        deleted.GetProperty("row").GetProperty("total").GetDecimal().Should().Be(7m);
        (await CountAsync("total = 7")).Should().Be(0);

        // All or none: the second field fails (the key is taken), so the first is undone.
        var before = await CountAsync("1 = 1");
        var failed = await GraphQLTestHost.PostAsync(
            client,
            path,
            """
            mutation Both($id: Int!) {
              first: insert_gq_customers(rows: [{ name: "Gamma" }]) { affected }
              second: insert_gq_orders(rows: [{ id: $id, total: 1, status: "open" }]) { affected }
            }
            """,
            new { id = orders.GetProperty("rows")[1].GetProperty("id").GetInt32() }
        );
        failed.Body.GetProperty("data").ValueKind.Should().Be(JsonValueKind.Null);
        // The key taken: a unique violation, or (SQL Server) an explicit value for an identity column refused.
        failed.Codes().Should().HaveCount(2).And.Contain("ROLLED_BACK").And.ContainSingle(c => c != "ROLLED_BACK");
        (await CountAsync("1 = 1")).Should().Be(before);
        using (var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!))
        {
            (await db.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM gq_customers WHERE name = 'Gamma'")).Should().Be(0);
        }

        // An update or delete by condition says which rows.
        (await GraphQLTestHost.PostAsync(client, path, """mutation { update_gq_orders(set: { status: "x" }) { affected } }""")).Codes().Should().Contain("BAD_USER_INPUT");
        (await GraphQLTestHost.PostAsync(client, path, """mutation { delete_gq_orders(filter: "true") { affected } }""")).Data().GetProperty("delete_gq_orders").GetProperty("affected").GetInt32().Should().Be(3);
    }

    // A refused or failed field is audited as an endpoint's failure is, as what was refused; a function writes in the
    // mutation's transaction, all or none with the other fields; a mutation committed stays reported, though what
    // follows its commit (here the audit) fails.
    [Fact]
    public async Task Mutations_are_audited_and_reported_as_they_ended_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        var audit = new Audit();
        using var factory = GraphQLTestHost.Create(
                datasource,
                dm => dm.Policies(
                    DmPolicy.Allow(DmAction.Create, "clerk").OnTables("gq_orders").Check("total gt 0"),
                    DmPolicy.Allow(DmAction.Read, "clerk").OnTables("gq_orders", "gq_customers"),
                    DmPolicy.Allow(DmAction.All, "admin")
                ),
                options => options.Functions.Add(new DapperMaticFunction { Name = "fn_touch", Sql = "UPDATE gq_orders SET status = 'touched'", Writes = ["gq_orders"] })
            )
            .WithWebHostBuilder(web => web.ConfigureServices(services => services.AddSingleton<AspNetCore.Auditing.IDapperMaticAuditLogger>(audit)));
        await GraphQLTestHost.CreateTablesAsync(factory, datasource, null);
        var path = $"/api/dm/d/{datasource.Id}/graphql";

        // Refused by its policy's check: audited as the insert into orders.
        using var clerk = GraphQLTestHost.Client(factory, "clerk");
        (await GraphQLTestHost.PostAsync(clerk, path, """mutation { insert_gq_orders(rows: [{ total: 0, status: "open" }]) { affected } }""")).Codes().Should().Equal("FORBIDDEN");
        audit.AuditEvents.Should().ContainSingle(e => !e.Success).Which.TableName.Should().Be("gq_orders");

        // A function runs in the mutation's transaction: undone with the other fields when one fails, kept when none does.
        using (var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!))
        {
            await db.ExecuteAsync("INSERT INTO gq_orders (total, status) VALUES (5, 'open')");
        }

        using var admin = GraphQLTestHost.Client(factory);
        async Task<List<string?>> StatusesAsync() =>
            [.. (await GraphQLTestHost.PostAsync(admin, path, "{ gq_orders(orderBy: [{ id: ASC }]) { status } }")).Data().GetProperty("gq_orders").EnumerateArray().Select(o => o.GetProperty("status").GetString())];
        var mixed = await GraphQLTestHost.PostAsync(admin, path, """mutation { fn_touch insert_gq_orders(rows: [{ total: 1, status: "open" }]) { affected } delete_gq_orders { affected } }""");
        mixed.Codes().Should().BeEquivalentTo(["ROLLED_BACK", "ROLLED_BACK", "BAD_USER_INPUT"]);
        (await StatusesAsync()).Should().Equal("open");
        (await GraphQLTestHost.PostAsync(admin, path, """mutation { fn_touch insert_gq_orders(rows: [{ total: 1, status: "new" }]) { affected } }""")).Data();
        (await StatusesAsync()).Should().Equal("touched", "new");

        // Committed, then the audit fails: the rows are written and said to be, with the failure.
        audit.Down = true;
        var written = await GraphQLTestHost.PostAsync(admin, path, """mutation { insert_gq_orders(rows: [{ total: 2, status: "open" }]) { affected } }""");
        written.Body.GetProperty("data").GetProperty("insert_gq_orders").GetProperty("affected").GetInt32().Should().Be(1);
        written.Body.GetProperty("errors")[0].GetProperty("message").GetString().Should().StartWith("Written; then this failed");
    }

    // Records what is audited; once down, fails to record what succeeded.
    private sealed class Audit : Infrastructure.TestDapperMaticAuditLogger, AspNetCore.Auditing.IDapperMaticAuditLogger
    {
        public bool Down { get; set; }

        Task AspNetCore.Auditing.IDapperMaticAuditLogger.LogOperationAsync(AspNetCore.Auditing.DapperMaticAuditEvent auditEvent) =>
            Down && auditEvent.Success ? throw new InvalidOperationException("The audit is down.") : LogOperationAsync(auditEvent);
    }
}
