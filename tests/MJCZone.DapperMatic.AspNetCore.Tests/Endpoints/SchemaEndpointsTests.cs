// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using MJCZone.DapperMatic.AspNetCore.Models.Requests;
using MJCZone.DapperMatic.AspNetCore.Models.Responses;
using MJCZone.DapperMatic.AspNetCore.Tests.Factories;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Endpoints;

/// <summary>
/// Integration tests for DapperMatic schema REST endpoints.
/// </summary>
public class SchemaEndpointsTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private readonly TestcontainersAssemblyFixture _fixture;

    public SchemaEndpointsTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ListSchemas_GET_SqlServer_ReturnsSchemas()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dm/d/Test-SqlServer/s/");

        response.Should().HaveStatusCode(HttpStatusCode.OK);
        var result = await response.ReadAsJsonAsync<SchemaListResponse>();
        result.Should().NotBeNull();
        result!.Result.Should().NotBeNull();
        result.Result.Should().NotBeEmpty();
        result.Result.Should().Contain(s => s.SchemaName == "dbo");
    }

    [Fact]
    public async Task ListSchemas_GET_PostgreSQL_ReturnsSchemas()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dm/d/Test-PostgreSQL/s/");

        response.Should().HaveStatusCode(HttpStatusCode.OK);
        var result = await response.ReadAsJsonAsync<SchemaListResponse>();
        result.Should().NotBeNull();
        result!.Result.Should().NotBeNull();
        result.Result.Should().NotBeEmpty();
        result.Result.Should().Contain(s => s.SchemaName == "public");
    }

    [Fact]
    public async Task ListSchemas_GET_SQLite_ReturnsUnderscoreSchema()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dm/d/Test-SQLite/s/");

        response.Should().HaveStatusCode(HttpStatusCode.OK);
        var result = await response.ReadAsJsonAsync<SchemaListResponse>();
        result.Should().NotBeNull();
        result!.Result.Should().NotBeNull();
        result.Result.Should().HaveCount(0);
    }

    [Fact]
    public async Task ListSchemas_GET_WithFilter_ReturnsFilteredSchemas()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dm/d/Test-SqlServer/s/?filter=dbo");

        response.Should().HaveStatusCode(HttpStatusCode.OK);
        var result = await response.ReadAsJsonAsync<SchemaListResponse>();
        result.Should().NotBeNull();
        result!.Result.Should().NotBeNull();
        result.Result.Should().Contain(s => s.SchemaName == "dbo");
    }

    [Fact]
    public async Task ListSchemas_GET_NonExistentDatasource_ReturnsNotFound()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dm/d/NonExistent/s/");

        response.Should().HaveStatusCode(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetSchema_GET_SqlServer_dbo_ReturnsSchema()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dm/d/Test-SqlServer/s/dbo");

        response.Should().HaveStatusCode(HttpStatusCode.OK);
        var result = await response.ReadAsJsonAsync<SchemaResponse>();
        result.Should().NotBeNull();
        result!.Result.Should().NotBeNull();
        result.Result!.SchemaName.Should().Be("dbo");
    }

    [Fact]
    public async Task GetSchema_GET_SQLite_Underscore_ReturnsSchema()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dm/d/Test-SQLite/s/_");

        response.Should().HaveStatusCode(HttpStatusCode.OK);
        var result = await response.ReadAsJsonAsync<SchemaResponse>();
        result.Should().NotBeNull();
        result!.Result.Should().BeNull();
    }

    [Fact]
    public async Task GetSchema_GET_SQLite_InvalidSchema_ReturnsNotFound()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dm/d/Test-SQLite/s/invalidschema");

        response.Should().HaveStatusCode(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetSchema_GET_NonExistentSchema_ReturnsNotFound()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dm/d/Test-SqlServer/s/nonexistent");

        response.Should().HaveStatusCode(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateSchema_POST_SqlServer_CreatesSchema()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var request = new CreateSchemaRequest { SchemaName = "TestSchema" };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json"
        );

        var response = await client.PostAsync("/api/dm/d/Test-SqlServer/s/", content);

        response.Should().HaveStatusCode(HttpStatusCode.Created);
        var result = await response.ReadAsJsonAsync<SchemaResponse>();
        result.Should().NotBeNull();
        result!.Result.Should().NotBeNull();
        result.Result!.SchemaName.Should().Be("TestSchema");
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task CreateSchema_POST_SQLite_ReturnsNotSupported()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var request = new CreateSchemaRequest { SchemaName = "TestSchema" };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json"
        );

        var response = await client.PostAsync("/api/dm/d/Test-SQLite/s/", content);

        response.Should().HaveStatusCode(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task CreateSchema_POST_DuplicateSchema_ReturnsConflict()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var request = new CreateSchemaRequest { SchemaName = "dbo" };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json"
        );

        var response = await client.PostAsync("/api/dm/d/Test-SqlServer/s/", content);

        response.Should().HaveStatusCode(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DropSchema_DELETE_SqlServer_DropsSchema()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        // First create a schema
        var createRequest = new CreateSchemaRequest { SchemaName = "TempSchema" };
        var createContent = new StringContent(
            JsonSerializer.Serialize(createRequest),
            Encoding.UTF8,
            "application/json"
        );
        await client.PostAsync("/api/dm/d/Test-SqlServer/s/", createContent);

        // Then drop it
        var response = await client.DeleteAsync("/api/dm/d/Test-SqlServer/s/TempSchema");

        response.Should().HaveStatusCode(HttpStatusCode.OK);
        var result = await response.ReadAsJsonAsync<SchemaResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task DropSchema_DELETE_SQLite_ReturnsNotSupported()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/dm/d/Test-SQLite/s/someschema");

        response.Should().HaveStatusCode(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task DropSchema_DELETE_NonExistentSchema_ReturnsNotFound()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/dm/d/Test-SqlServer/s/nonexistent");

        response.Should().HaveStatusCode(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SchemaExists_GET_SqlServer_dbo_ReturnsTrue()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dm/d/Test-SqlServer/s/dbo/exists");

        response.Should().HaveStatusCode(HttpStatusCode.OK);
        var result = await response.ReadAsJsonAsync<SchemaExistsResponse>();
        result.Should().NotBeNull();
        result!.Result.Should().BeTrue();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task SchemaExists_GET_SQLite_Underscore_ReturnsTrue()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dm/d/Test-SQLite/s/_/exists");

        response.Should().HaveStatusCode(HttpStatusCode.OK);
        var result = await response.ReadAsJsonAsync<SchemaExistsResponse>();
        result.Should().NotBeNull();
        result!.Result.Should().BeTrue();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task SchemaExists_GET_SQLite_InvalidSchema_ReturnsFalse()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dm/d/Test-SQLite/s/invalidschema/exists");

        response.Should().HaveStatusCode(HttpStatusCode.OK);
        var result = await response.ReadAsJsonAsync<SchemaExistsResponse>();
        result.Should().NotBeNull();
        result!.Result.Should().BeFalse();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task SchemaExists_GET_NonExistentSchema_ReturnsFalse()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dm/d/Test-SqlServer/s/nonexistent/exists");

        response.Should().HaveStatusCode(HttpStatusCode.OK);
        var result = await response.ReadAsJsonAsync<SchemaExistsResponse>();
        result.Should().NotBeNull();
        result!.Result.Should().BeFalse();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task CreateSchema_POST_InvalidRequest_ReturnsBadRequest()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var request = new CreateSchemaRequest { SchemaName = "" }; // Invalid empty name
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json"
        );

        var response = await client.PostAsync("/api/dm/d/Test-SqlServer/s/", content);

        response.Should().HaveStatusCode(HttpStatusCode.BadRequest);
    }
}
