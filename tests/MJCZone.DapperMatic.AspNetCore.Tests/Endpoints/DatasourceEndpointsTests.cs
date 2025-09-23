// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Models.Requests;
using MJCZone.DapperMatic.AspNetCore.Models.Responses;
using MJCZone.DapperMatic.AspNetCore.Tests.Factories;
using MJCZone.DapperMatic.AspNetCore.Tests.Infrastructure;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Endpoints;

/// <summary>
/// Integration tests for DapperMatic datasource REST endpoints.
/// </summary>
public class DatasourceEndpointsTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private readonly TestcontainersAssemblyFixture _fixture;

    public DatasourceEndpointsTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ListDatasources_GET_ReturnsAllConfiguredDatasources()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dm/d/");

        response.Should().HaveStatusCode(HttpStatusCode.OK);
        var result = await response.ReadAsJsonAsync<DatasourceListResponse>();
        result.Should().NotBeNull();
        result!.Result.Should().NotBeNull();
        result.Result.Should().HaveCountGreaterOrEqualTo(4); // SQL Server, MySQL, PostgreSQL, SQLite
        result.Result.Should().Contain(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_SqlServer);
        result.Result.Should().Contain(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_MySql);
        result.Result.Should().Contain(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_PostgreSql);
        result.Result.Should().Contain(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);
    }

    [Fact]
    public async Task ListDatasources_GET_WithFilter_ReturnsFilteredDatasources()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dm/d/?filter=Server");

        response.Should().HaveStatusCode(HttpStatusCode.OK);
        var result = await response.ReadAsJsonAsync<DatasourceListResponse>();
        result.Should().NotBeNull();
        result!.Result.Should().NotBeNull();
        result.Result.Should().Contain(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_SqlServer); // Contains "Server"
        result.Result.Should().NotContain(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_MySql); // Doesn't contain "Server"
        result.Result.Should().NotContain(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_PostgreSql); // Doesn't contain "Server"
        result.Result.Should().NotContain(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite); // Doesn't contain "Server"
    }

    [Fact]
    public async Task GetDatasource_GET_WithValidId_ReturnsDatasource()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dm/d/Test-SqlServer");

        response.Should().HaveStatusCode(HttpStatusCode.OK);
        var result = await response.ReadAsJsonAsync<DatasourceResponse>();
        result.Should().NotBeNull();
        result!.Result.Should().NotBeNull();
        result.Result!.Id.Should().Be(TestcontainersAssemblyFixture.DatasourceId_SqlServer);
        result.Result.Provider.Should().Be("SqlServer");
        result.Result.DisplayName.Should().Be("Test SQL Server");
    }

    [Fact]
    public async Task GetDatasource_GET_WithInvalidId_ReturnsNotFound()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dm/d/NonExistent");

        response.Should().HaveStatusCode(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddDatasource_POST_WithValidData_ReturnsCreated()
    {
        using var factory = new WafWithInMemoryDatasourceRepository([]);
        var client = factory.CreateClient();

        var request = new CreateDatasourceRequest
        {
            Id = "NewDatasource",
            Provider = "Sqlite",
            ConnectionString = "Data Source=test.db",
            DisplayName = "New Test Datasource",
            Description = "A new test datasource",
            Tags = ["test", "new"],
            IsEnabled = true
        };

        var response = await client.PostAsJsonAsync("/api/dm/d/", request);

        response.Should().HaveStatusCode(HttpStatusCode.Created);
        var result = await response.ReadAsJsonAsync<DatasourceResponse>();
        result.Should().NotBeNull();
        result!.Result.Should().NotBeNull();
        result.Result!.Id.Should().Be("NewDatasource");
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("added successfully");
    }

    [Fact]
    public async Task AddDatasource_POST_WitMissingId_GeneratesGuidId()
    {
        using var factory = new WafWithInMemoryDatasourceRepository([]);
        var client = factory.CreateClient();

        var request = new CreateDatasourceRequest
        {
            Provider = "Sqlite",
            ConnectionString = "Data Source=test.db",
            DisplayName = "New Test Datasource",
            Description = "A new test datasource",
            Tags = ["test", "new"],
            IsEnabled = true,
        };

        var response = await client.PostAsJsonAsync("/api/dm/d/", request);

        response.Should().HaveStatusCode(HttpStatusCode.Created);
        var result = await response.ReadAsJsonAsync<DatasourceResponse>();
        result.Should().NotBeNull();
        result!.Result.Should().NotBeNull();
        result.Result!.Id.Should().NotBeNullOrEmpty();
        Guid.TryParse(result.Result.Id, out _).Should().BeTrue("Id should be a valid GUID");
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("added successfully");
    }

    [Fact]
    public async Task AddDatasource_POST_WithDuplicateId_SignalsConflict()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var request = new CreateDatasourceRequest
        {
            Id = "Test-SqlServer-ABC", // Duplicate ID - gets overwritten
            Provider = "Sqlite",
            ConnectionString = "Data Source=test.db",
            DisplayName = "Overwritten Datasource",
        };
        // First, create it
        var response = await client.PostAsJsonAsync("/api/dm/d/", request);
        response.Should().HaveStatusCode(HttpStatusCode.Created);
        // Next, try again to trigger duplicate handling
        response = await client.PostAsJsonAsync("/api/dm/d/", request);
        response.Should().HaveStatusCode(HttpStatusCode.Conflict);
        var result = await response.ReadAsJsonAsync<DatasourceResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Message.Should().Contain("already exists");
    }

    [Fact]
    public async Task UpdateDatasource_PUT_WithValidData_ReturnsOk()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var request = new UpdateDatasourceRequest
        {
            Provider = "Sqlite",
            ConnectionString = "Data Source=updated.db",
            DisplayName = "Updated Display Name",
            Description = "Updated description",
            Tags = ["updated", "test"],
            IsEnabled = false
        };

        var response = await client.PutAsJsonAsync("/api/dm/d/Test-SqlServer", request);

        response.Should().HaveStatusCode(HttpStatusCode.OK);
        var result = await response.ReadAsJsonAsync<DatasourceResponse>();
        result.Should().NotBeNull();
        result!.Result.Should().NotBeNull();
        result.Result!.Id.Should().Be(TestcontainersAssemblyFixture.DatasourceId_SqlServer);
        result.Result.DisplayName.Should().Be("Updated Display Name");
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateDatasource_PUT_WithInvalidId_ReturnsNotFound()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var request = new UpdateDatasourceRequest
        {
            Provider = "Sqlite",
            ConnectionString = "Data Source=test.db"
        };

        var response = await client.PutAsJsonAsync("/api/dm/d/NonExistent", request);

        response.Should().HaveStatusCode(HttpStatusCode.NotFound);
        var result = await response.ReadAsJsonAsync<DatasourceResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task UpdateDatasource_PATCH_WithValidData_ReturnsOk()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var request = new UpdateDatasourceRequest
        {
            DisplayName = "Patched Display Name"
        };

        var response = await client.PatchAsJsonAsync("/api/dm/d/Test-SqlServer", request);

        response.Should().HaveStatusCode(HttpStatusCode.OK);
        var result = await response.ReadAsJsonAsync<DatasourceResponse>();
        result.Should().NotBeNull();
        result!.Result.Should().NotBeNull();
        result.Result!.DisplayName.Should().Be("Patched Display Name");
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveDatasource_DELETE_WithValidId_ReturnsOk()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/dm/d/Test-SqlServer");

        response.Should().HaveStatusCode(HttpStatusCode.OK);
        var result = await response.ReadAsJsonAsync<DatasourceResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Message.Should().Contain("removed successfully");

        // Verify it's actually removed
        var getResponse = await client.GetAsync("/api/dm/d/Test-SqlServer");
        getResponse.Should().HaveStatusCode(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemoveDatasource_DELETE_WithInvalidId_ReturnsNotFound()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/dm/d/NonExistent");

        response.Should().HaveStatusCode(HttpStatusCode.NotFound);
        var result = await response.ReadAsJsonAsync<DatasourceResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }
}