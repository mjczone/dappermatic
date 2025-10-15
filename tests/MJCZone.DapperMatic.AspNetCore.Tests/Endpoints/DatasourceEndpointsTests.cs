// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using FluentAssertions;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Models.Responses;
using MJCZone.DapperMatic.AspNetCore.Tests.Factories;

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

    #region Comprehensive Workflow Tests

    [Fact]
    public async Task DatasourceEndpoints_CompleteWorkflow_Success()
    {
        using var factory = new WafWithInMemoryDatasourceRepository([]);
        using var client = factory.CreateClient();
        const string testDatasourceId = "WorkflowTestDatasource";

        // 1. GET MULTI - List all datasources (should be empty initially)
        var listResponse1 = await client.GetAsync("/api/dm/d/");
        listResponse1.StatusCode.Should().Be(HttpStatusCode.OK);
        var listResult1 = await listResponse1.ReadAsJsonAsync<DatasourceListResponse>();
        listResult1.Should().NotBeNull();
        listResult1!.Result.Should().NotBeNull();
        listResult1.Result.Should().BeEmpty();

        // 2. GET SINGLE - Try to get non-existent datasource (should return 404)
        var getResponse1 = await client.GetAsync($"/api/dm/d/{testDatasourceId}");
        getResponse1.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 3. CREATE - Create a new datasource
        var createRequest = new DatasourceDto
        {
            Id = testDatasourceId,
            Provider = "Sqlite",
            ConnectionString = "Data Source=:memory:",
            DisplayName = "Workflow Test Datasource",
            Description = "A test datasource for workflow validation",
            Tags = ["test", "workflow"],
            IsEnabled = true,
        };
        var createResponse = await client.PostAsJsonAsync("/api/dm/d/", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.ReadAsJsonAsync<DatasourceResponse>();
        createResult.Should().NotBeNull();
        createResult!.Result.Should().NotBeNull();
        createResult.Result!.Id.Should().Be(testDatasourceId);
        createResult.Result.DisplayName.Should().Be("Workflow Test Datasource");

        // 4. EXISTS - Check if datasource exists (should return 200 - found)
        var existsResponse1 = await client.GetAsync($"/api/dm/d/{testDatasourceId}");
        existsResponse1.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. GET MULTI - List datasources again (should contain new datasource)
        var listResponse2 = await client.GetAsync("/api/dm/d/");
        listResponse2.StatusCode.Should().Be(HttpStatusCode.OK);
        var listResult2 = await listResponse2.ReadAsJsonAsync<DatasourceListResponse>();
        listResult2.Should().NotBeNull();
        listResult2!.Result.Should().NotBeNull();
        listResult2.Result.Should().HaveCount(1);
        listResult2.Result.Should().Contain(d => d.Id == testDatasourceId);

        // 6. GET SINGLE - Get the created datasource (should return datasource details)
        var getResponse2 = await client.GetAsync($"/api/dm/d/{testDatasourceId}");
        getResponse2.StatusCode.Should().Be(HttpStatusCode.OK);
        var getResult2 = await getResponse2.ReadAsJsonAsync<DatasourceResponse>();
        getResult2.Should().NotBeNull();
        getResult2!.Result.Should().NotBeNull();
        getResult2.Result!.Id.Should().Be(testDatasourceId);
        getResult2.Result.DisplayName.Should().Be("Workflow Test Datasource");
        getResult2.Result.Description.Should().Be("A test datasource for workflow validation");
        getResult2.Result.Tags.Should().Contain("test");
        getResult2.Result.Tags.Should().Contain("workflow");
        getResult2.Result.IsEnabled.Should().BeTrue();

        // 7. UPDATE (PUT) - Update the datasource completely
        var updateRequest = new DatasourceDto
        {
            Provider = "Sqlite",
            ConnectionString = "Data Source=updated.db",
            DisplayName = "Updated Workflow Test",
            Description = "Updated description for workflow test",
            Tags = ["updated", "test"],
            IsEnabled = false,
        };
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/dm/d/{testDatasourceId}",
            updateRequest
        );
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updateResult = await updateResponse.ReadAsJsonAsync<DatasourceResponse>();
        updateResult.Should().NotBeNull();
        updateResult!.Result.Should().NotBeNull();

        // 8. GET SINGLE - Get updated datasource (should show changes)
        var getResponse3 = await client.GetAsync($"/api/dm/d/{testDatasourceId}");
        getResponse3.StatusCode.Should().Be(HttpStatusCode.OK);
        var getResult3 = await getResponse3.ReadAsJsonAsync<DatasourceResponse>();
        getResult3.Should().NotBeNull();
        getResult3!.Result.Should().NotBeNull();
        getResult3.Result!.DisplayName.Should().Be("Updated Workflow Test");
        getResult3.Result.Description.Should().Be("Updated description for workflow test");
        getResult3.Result.Tags.Should().Contain("updated");
        getResult3.Result.Tags.Should().NotContain("workflow");
        getResult3.Result.IsEnabled.Should().BeFalse();

        // 9. PATCH - Partial update of the datasource
        var patchRequest = new DatasourceDto
        {
            DisplayName = "Partially Updated Test",
            IsEnabled = true, // Only update these fields
        };
        var patchResponse = await client.PatchAsJsonAsync(
            $"/api/dm/d/{testDatasourceId}",
            patchRequest
        );
        patchResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 10. GET SINGLE - Verify patch changes
        var getResponse4 = await client.GetAsync($"/api/dm/d/{testDatasourceId}");
        getResponse4.StatusCode.Should().Be(HttpStatusCode.OK);
        var getResult4 = await getResponse4.ReadAsJsonAsync<DatasourceResponse>();
        getResult4.Should().NotBeNull();
        getResult4!.Result.Should().NotBeNull();
        getResult4.Result!.DisplayName.Should().Be("Partially Updated Test");
        getResult4.Result.IsEnabled.Should().BeTrue();
        getResult4.Result.Description.Should().Be("Updated description for workflow test"); // Should remain from PUT

        // 11. DELETE - Delete the datasource
        var deleteResponse = await client.DeleteAsync($"/api/dm/d/{testDatasourceId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 12. GET SINGLE - Try to get deleted datasource (should return 404)
        var getResponse5 = await client.GetAsync($"/api/dm/d/{testDatasourceId}");
        getResponse5.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 13. GET MULTI - List datasources (should be empty again)
        var listResponse3 = await client.GetAsync("/api/dm/d/");
        listResponse3.StatusCode.Should().Be(HttpStatusCode.OK);
        var listResult3 = await listResponse3.ReadAsJsonAsync<DatasourceListResponse>();
        listResult3.Should().NotBeNull();
        listResult3!.Result.Should().NotBeNull();
        listResult3.Result.Should().BeEmpty();
    }

    [Fact]
    public async Task DatasourceEndpoints_FilteringAndSearch_Success()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        using var client = factory.CreateClient();

        // Test basic listing with configured datasources
        var listResponse = await client.GetAsync("/api/dm/d/");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listResult = await listResponse.ReadAsJsonAsync<DatasourceListResponse>();
        listResult.Should().NotBeNull();
        listResult!.Result.Should().NotBeNull();
        listResult.Result.Should().HaveCountGreaterThanOrEqualTo(4); // SQL Server, MySQL, PostgreSQL, SQLite
        listResult
            .Result.Should()
            .Contain(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_SqlServer);
        listResult
            .Result.Should()
            .Contain(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_MySql);
        listResult
            .Result.Should()
            .Contain(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_PostgreSql);
        listResult
            .Result.Should()
            .Contain(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite);

        // Test filtering by name pattern
        var filterResponse = await client.GetAsync("/api/dm/d/?filter=Server");
        filterResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var filterResult = await filterResponse.ReadAsJsonAsync<DatasourceListResponse>();
        filterResult.Should().NotBeNull();
        filterResult!.Result.Should().NotBeNull();
        filterResult
            .Result.Should()
            .Contain(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_SqlServer);

        // Test specific datasource retrieval
        var getResponse = await client.GetAsync(
            $"/api/dm/d/{TestcontainersAssemblyFixture.DatasourceId_SqlServer}"
        );
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var getResult = await getResponse.ReadAsJsonAsync<DatasourceResponse>();
        getResult.Should().NotBeNull();
        getResult!.Result.Should().NotBeNull();
        getResult.Result!.Id.Should().Be(TestcontainersAssemblyFixture.DatasourceId_SqlServer);
        getResult.Result.Provider.Should().Be("SqlServer");
        getResult.Result.DisplayName.Should().Be("Test SQL Server");
    }

    [Fact]
    public async Task DatasourceEndpoints_AutoGeneratedId_Success()
    {
        using var factory = new WafWithInMemoryDatasourceRepository([]);
        using var client = factory.CreateClient();

        // Test auto-generated GUID ID when not provided
        var createRequest = new DatasourceDto
        {
            // Id intentionally omitted
            Provider = "Sqlite",
            ConnectionString = "Data Source=:memory:",
            DisplayName = "Auto-Generated ID Test",
            IsEnabled = true,
        };

        var createResponse = await client.PostAsJsonAsync("/api/dm/d/", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.ReadAsJsonAsync<DatasourceResponse>();
        createResult.Should().NotBeNull();
        createResult!.Result.Should().NotBeNull();
        createResult.Result!.Id.Should().NotBeNullOrEmpty();
        Guid.TryParse(createResult.Result.Id, out _).Should().BeTrue("Id should be a valid GUID");

        // Clean up
        await client.DeleteAsync($"/api/dm/d/{createResult.Result.Id}");
    }

    #endregion

    #region Error Scenarios Tests

    [Fact]
    public async Task DatasourceEndpoints_ErrorScenarios_HandledCorrectly()
    {
        using var factory = new WafWithInMemoryDatasourceRepository([]);
        using var client = factory.CreateClient();

        // Test operations on non-existent datasource
        const string nonExistentId = "NonExistentDatasource";

        // GET non-existent datasource
        var getNonExistentResponse = await client.GetAsync($"/api/dm/d/{nonExistentId}");
        getNonExistentResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // UPDATE non-existent datasource
        var updateRequest = new DatasourceDto { DisplayName = "Updated Non-Existent" };
        var updateNonExistentResponse = await client.PutAsJsonAsync(
            $"/api/dm/d/{nonExistentId}",
            updateRequest
        );
        updateNonExistentResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // PATCH non-existent datasource
        var patchNonExistentResponse = await client.PatchAsJsonAsync(
            $"/api/dm/d/{nonExistentId}",
            updateRequest
        );
        patchNonExistentResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // DELETE non-existent datasource
        var deleteNonExistentResponse = await client.DeleteAsync($"/api/dm/d/{nonExistentId}");
        deleteNonExistentResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Test duplicate datasource creation
        const string duplicateId = "DuplicateTest";
        var duplicateRequest = new DatasourceDto
        {
            Id = duplicateId,
            Provider = "Sqlite",
            ConnectionString = "Data Source=:memory:",
            DisplayName = "Duplicate Test",
        };

        // First creation should succeed
        var createResponse1 = await client.PostAsJsonAsync("/api/dm/d/", duplicateRequest);
        createResponse1.StatusCode.Should().Be(HttpStatusCode.Created);

        // Second creation with same ID should conflict
        var createResponse2 = await client.PostAsJsonAsync("/api/dm/d/", duplicateRequest);
        createResponse2.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Clean up
        await client.DeleteAsync($"/api/dm/d/{duplicateId}");
    }

    [Fact]
    public async Task DatasourceEndpoints_InvalidData_ReturnsBadRequest()
    {
        using var factory = new WafWithInMemoryDatasourceRepository([]);
        using var client = factory.CreateClient();

        // Test invalid provider
        var invalidProviderRequest = new DatasourceDto
        {
            Id = "InvalidProvider",
            Provider = "InvalidProvider", // Invalid provider
            ConnectionString = "Data Source=:memory:",
            DisplayName = "Invalid Provider Test",
        };
        var invalidProviderResponse = await client.PostAsJsonAsync(
            "/api/dm/d/",
            invalidProviderRequest
        );
        invalidProviderResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Test missing required fields
        var missingFieldsRequest = new DatasourceDto
        {
            Id = "MissingFields",
            // Provider missing
            // ConnectionString missing
            DisplayName = "Missing Fields Test",
        };
        var missingFieldsResponse = await client.PostAsJsonAsync(
            "/api/dm/d/",
            missingFieldsRequest
        );
        missingFieldsResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Test empty connection string
        var emptyConnectionRequest = new DatasourceDto
        {
            Id = "EmptyConnection",
            Provider = "Sqlite",
            ConnectionString = "", // Empty connection string
            DisplayName = "Empty Connection Test",
        };
        var emptyConnectionResponse = await client.PostAsJsonAsync(
            "/api/dm/d/",
            emptyConnectionRequest
        );
        emptyConnectionResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task DatasourceEndpoints_WithTestContainers_Success()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        using var client = factory.CreateClient();

        // Test that all configured test datasources are accessible
        var testDatasources = new[]
        {
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            TestcontainersAssemblyFixture.DatasourceId_MySql,
            TestcontainersAssemblyFixture.DatasourceId_PostgreSql,
            TestcontainersAssemblyFixture.DatasourceId_Sqlite,
        };

        foreach (var datasourceId in testDatasources)
        {
            var response = await client.GetAsync($"/api/dm/d/{datasourceId}");
            response
                .StatusCode.Should()
                .Be(HttpStatusCode.OK, $"Datasource {datasourceId} should be accessible");

            var result = await response.ReadAsJsonAsync<DatasourceResponse>();
            result.Should().NotBeNull();
            result!.Result.Should().NotBeNull();
            result.Result!.Id.Should().Be(datasourceId);
            result.Result.Provider.Should().NotBeNullOrEmpty();
            result.Result.ConnectionString.Should().NotBeNullOrEmpty();
            result.Result.DisplayName.Should().NotBeNullOrEmpty();
            result.Result.IsEnabled.Should().BeTrue();
        }
    }

    [Fact]
    public async Task DatasourceEndpoints_ProviderSpecific_Success()
    {
        using var factory = new WafWithInMemoryDatasourceRepository([]);
        using var client = factory.CreateClient();

        // Test creating datasources for different providers
        var providerTests = new[]
        {
            new { Provider = "Sqlite", ConnectionString = "Data Source=:memory:" },
            new
            {
                Provider = "SqlServer",
                ConnectionString = "Server=localhost;Database=test;Trusted_Connection=true;",
            },
            new
            {
                Provider = "MySql",
                ConnectionString = "Server=localhost;Database=test;Uid=root;Pwd=password;",
            },
            new
            {
                Provider = "PostgreSql",
                ConnectionString = "Host=localhost;Database=test;Username=postgres;Password=password;",
            },
        };

        var createdIds = new List<string>();

        try
        {
            foreach (var test in providerTests)
            {
                var request = new DatasourceDto
                {
                    Id = $"Test-{test.Provider}",
                    Provider = test.Provider,
                    ConnectionString = test.ConnectionString,
                    DisplayName = $"Test {test.Provider}",
                    IsEnabled = true,
                };

                var response = await client.PostAsJsonAsync("/api/dm/d/", request);
                response
                    .StatusCode.Should()
                    .Be(HttpStatusCode.Created, $"Provider {test.Provider} should be supported");

                var result = await response.ReadAsJsonAsync<DatasourceResponse>();
                result.Should().NotBeNull();
                result!.Result.Should().NotBeNull();
                result.Result!.Provider.Should().Be(test.Provider);

                createdIds.Add(request.Id!);
            }
        }
        finally
        {
            // Clean up all created datasources
            foreach (var id in createdIds)
            {
                await client.DeleteAsync($"/api/dm/d/{id}");
            }
        }
    }

    #endregion
}
