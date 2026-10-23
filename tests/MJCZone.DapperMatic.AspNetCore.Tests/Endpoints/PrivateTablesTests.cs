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
using Microsoft.Extensions.DependencyInjection;
using MJCZone.DapperMatic.AspNetCore.Factories;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Services;
using MJCZone.DapperMatic.AspNetCore.Tests.Factories;
using MJCZone.DapperMatic.AspNetCore.Tests.Infrastructure;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Endpoints;

/// <summary>
/// DapperMatic's own tables (accounts, keys, tickets, the change log, ...), which may share a datasource the API serves,
/// and the tables an application keeps from the API, are served by no endpoint, whatever the policies grant.
/// </summary>
public class PrivateTablesTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private readonly TestcontainersAssemblyFixture _fixture;

    public PrivateTablesTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Private_tables_are_served_by_no_endpoint_Async()
    {
        var datasource = _fixture.GetTestDatasources().Single(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
        using var factory = new WafWithInMemoryDatasourceRepository([datasource]).WithWebHostBuilder(web =>
            web.ConfigureServices(services => services.Configure<DapperMaticOptions>(options => options.PrivateTables.Add("app_secrets")))
        );
        using var client = factory.CreateAuthenticatedClient(new System.Security.Claims.Claim("sub", "someone"));
        using var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!);
        db.Open();
        foreach (var name in new[] { "dm_users", "app_secrets", "dm_changes" })
        {
            await db.DropTableIfExistsAsync(null, name);
        }

        await db.CreateTableIfNotExistsAsync(new DmTable(null, "dm_users", [new DmColumn("id", typeof(int), isPrimaryKey: true), new DmColumn("password_hash", typeof(string), length: 100)]));
        await db.CreateTableIfNotExistsAsync(new DmTable(null, "app_secrets", [new DmColumn("id", typeof(int), isPrimaryKey: true), new DmColumn("value", typeof(string), length: 100)]));
        await db.ExecuteAsync("INSERT INTO dm_users (id, password_hash) VALUES (1, 'hash')");
        await db.ExecuteAsync("INSERT INTO app_secrets (id, value) VALUES (1, 'secret')");
        var baseUrl = $"/api/dm/d/{datasource.Id}/t";
        StringContent Json(object body) => new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        // The policies grant everything here, yet the tables are neither listed nor in the caller's OpenAPI document.
        var listed = await client.GetStringAsync(baseUrl);
        listed.Should().NotContain("dm_users").And.NotContain("app_secrets");
        var document = await client.GetStringAsync($"/api/dm/d/{datasource.Id}/openapi");
        document.Should().NotContain("dm_users").And.NotContain("app_secrets");

        // Nor read, written, described, altered or dropped.
        foreach (var table in new[] { "dm_users", "app_secrets" })
        {
            (await client.GetAsync($"{baseUrl}/{table}/query")).StatusCode.Should().Be(HttpStatusCode.Forbidden, table);
            (await client.PostAsync($"{baseUrl}/{table}/rows", Json(new { rows = new[] { new { id = 2 } } }))).StatusCode.Should().Be(HttpStatusCode.Forbidden, table);
            (await client.PostAsync($"{baseUrl}/{table}/rows/delete", Json(new { filter = "id eq 1" }))).StatusCode.Should().Be(HttpStatusCode.Forbidden, table);
            (await client.GetAsync($"{baseUrl}/{table}")).StatusCode.Should().Be(HttpStatusCode.Forbidden, table);
            (await client.DeleteAsync($"{baseUrl}/{table}")).StatusCode.Should().Be(HttpStatusCode.Forbidden, table);
        }

        (await db.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM dm_users")).Should().Be(1);
        (await db.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM app_secrets")).Should().Be(1);

        // Nor made under a DapperMatic name, which DapperMatic would then take for its own.
        var created = await client.PostAsync(baseUrl, Json(new { tableName = "dm_changes", columns = new[] { new { columnName = "id", providerDataType = "integer", isPrimaryKey = true } } }));
        created.StatusCode.Should().Be(HttpStatusCode.Forbidden, await created.Content.ReadAsStringAsync());
        (await db.DoesTableExistAsync(null, "dm_changes")).Should().BeFalse();

        // The service refuses it too, to code that calls it with an operation naming no table.
        using (var scope = factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IDapperMaticService>();
            var operation = new DapperMaticOperation { Operation = "tables/post", DatasourceId = datasource.Id, User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "app")], "Test")) };
            var dto = new TableDto { TableName = "dm_changes", Columns = [new ColumnDto { ColumnName = "id", ProviderDataType = "integer", IsPrimaryKey = true }] };
            await FluentActions.Awaiting(() => service.CreateTableAsync(operation, datasource.Id!, dto)).Should().ThrowAsync<UnauthorizedAccessException>();
        }

        (await db.DoesTableExistAsync(null, "dm_changes")).Should().BeFalse();

        foreach (var name in new[] { "dm_users", "app_secrets" })
        {
            await db.DropTableIfExistsAsync(null, name);
        }
    }
}
