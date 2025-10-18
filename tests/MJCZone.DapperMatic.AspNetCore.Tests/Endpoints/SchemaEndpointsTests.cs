// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text;
using System.Text.Json;
using Azure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
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

    #region Schema Endpoint Tests

    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer, "dbo", true)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql, "public", true)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql, null, false)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite, null, false)]
    public async Task SchemaEndpoints_CompleteWorkflow_Success(
        string datasourceId,
        string? defaultSchema,
        bool supportsSchemaOperations
    )
    {
        var datasources = _fixture.GetTestDatasources().Where(ds => ds.Id == datasourceId).ToList();
        using var factory = new WafWithInMemoryDatasourceRepository(datasources);
        using var client = factory.CreateClient();
        const string testSchemaName = "WorkflowTestSchema";

        // 1. GET MULTI - List all schemas
        var listResponse1 = await client.GetAsync($"/api/dm/d/{datasourceId}/s/");
        listResponse1.StatusCode.Should().Be(HttpStatusCode.OK);
        var listResult1 = await listResponse1.ReadAsJsonAsync<SchemaListResponse>();
        listResult1.Should().NotBeNull();
        listResult1!.Result.Should().NotBeNull();

        if (!supportsSchemaOperations)
            return;

        var initialSchemaCount = listResult1.Result!.Count();
        listResult1.Result.Should().Contain(s => s.SchemaName == defaultSchema);

        // 2. GET SINGLE - Try to get non-existent schema (should return 404)
        var getResponse1 = await client.GetAsync($"/api/dm/d/{datasourceId}/s/{testSchemaName}");
        getResponse1.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 3. CREATE - Create a new schema
        var createRequest = new SchemaDto { SchemaName = testSchemaName };
        var createContent = new StringContent(
            JsonSerializer.Serialize(createRequest),
            Encoding.UTF8,
            "application/json"
        );
        var createResponse = await client.PostAsync($"/api/dm/d/{datasourceId}/s", createContent);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.ReadAsJsonAsync<SchemaResponse>();
        createResult.Should().NotBeNull();
        createResult!.Result.Should().NotBeNull();
        createResult.Result!.SchemaName.Should().BeEquivalentTo(testSchemaName);

        // 4. EXISTS - Check if schema exists (should return true)
        var existsResponse1 = await client.GetAsync(
            $"/api/dm/d/{datasourceId}/s/{testSchemaName}/exists"
        );
        existsResponse1.StatusCode.Should().Be(HttpStatusCode.OK);
        var existsResult1 = await existsResponse1.ReadAsJsonAsync<SchemaExistsResponse>();
        existsResult1.Should().NotBeNull();
        existsResult1!.Result.Should().BeTrue();

        // 5. GET MULTI - List schemas again (should contain new schema)
        var listResponse2 = await client.GetAsync($"/api/dm/d/{datasourceId}/s/");
        listResponse2.StatusCode.Should().Be(HttpStatusCode.OK);
        var listResult2 = await listResponse2.ReadAsJsonAsync<SchemaListResponse>();
        listResult2.Should().NotBeNull();
        listResult2!.Result.Should().NotBeNull();
        listResult2.Result.Should().HaveCount(initialSchemaCount + 1);
        listResult2
            .Result.Should()
            .Contain(s => s.SchemaName.Equals(testSchemaName, StringComparison.OrdinalIgnoreCase));

        // 6. GET SINGLE - Get the created schema (should return schema details)
        var getResponse2 = await client.GetAsync($"/api/dm/d/{datasourceId}/s/{testSchemaName}");
        getResponse2.StatusCode.Should().Be(HttpStatusCode.OK);
        var getResult2 = await getResponse2.ReadAsJsonAsync<SchemaResponse>();
        getResult2.Should().NotBeNull();
        getResult2!.Result.Should().NotBeNull();
        getResult2.Result!.SchemaName.Should().BeEquivalentTo(testSchemaName);

        // 7. DELETE - Delete the schema
        var deleteResponse = await client.DeleteAsync(
            $"/api/dm/d/{datasourceId}/s/{testSchemaName}"
        );
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 8. EXISTS - Check if schema exists (should return false)
        var existsResponse2 = await client.GetAsync(
            $"/api/dm/d/{datasourceId}/s/{testSchemaName}/exists"
        );
        existsResponse2.StatusCode.Should().Be(HttpStatusCode.OK);
        var existsResult2 = await existsResponse2.ReadAsJsonAsync<SchemaExistsResponse>();
        existsResult2.Should().NotBeNull();
        existsResult2!.Result.Should().BeFalse();

        // 9. GET MULTI - List schemas (should be back to initial count)
        var listResponse3 = await client.GetAsync($"/api/dm/d/{datasourceId}/s/");
        listResponse3.StatusCode.Should().Be(HttpStatusCode.OK);
        var listResult3 = await listResponse3.ReadAsJsonAsync<SchemaListResponse>();
        listResult3.Should().NotBeNull();
        listResult3!.Result.Should().NotBeNull();
        listResult3.Result.Should().HaveCount(initialSchemaCount);
        listResult3
            .Result.Should()
            .NotContain(s =>
                s.SchemaName.Equals(testSchemaName, StringComparison.OrdinalIgnoreCase)
            );

        // 10. GET SINGLE - Try to get deleted schema (should return 404)
        var getResponse3 = await client.GetAsync($"/api/dm/d/{datasourceId}/s/{testSchemaName}");
        getResponse3.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SchemaEndpoints_FilteredList_Success()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        using var client = factory.CreateClient();
        const string datasourceId = TestcontainersAssemblyFixture.DatasourceId_SqlServer;

        // Test filtering schemas
        var filteredResponse = await client.GetAsync($"/api/dm/d/{datasourceId}/s/?filter=dbo");
        filteredResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var filteredResult = await filteredResponse.ReadAsJsonAsync<SchemaListResponse>();
        filteredResult.Should().NotBeNull();
        filteredResult!.Result.Should().NotBeNull();
        filteredResult.Result.Should().Contain(s => s.SchemaName == "dbo");

        // All returned schemas should match the filter
        foreach (var schema in filteredResult.Result!)
        {
            schema.SchemaName?.ToLower().Should().Contain("dbo".ToLower());
        }
    }

    #endregion // Schema Endpoint Tests

    #region Error Scenarios Tests

    [Fact]
    public async Task SchemaEndpoints_ErrorScenarios_HandledCorrectly()
    {
        var datasources = _fixture
            .GetTestDatasources()
            .Where(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_SqlServer)
            .ToList();
        using var factory = new WafWithInMemoryDatasourceRepository(datasources);
        using var client = factory.CreateClient();
        const string datasourceId = TestcontainersAssemblyFixture.DatasourceId_SqlServer;

        // Test non-existent datasource
        var nonExistentDatasourceResponse = await client.GetAsync("/api/dm/d/NonExistent/s/");
        nonExistentDatasourceResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Test invalid schema name (empty)
        var invalidCreateRequest = new SchemaDto
        {
            SchemaName = "", // Invalid empty name
        };
        var invalidCreateContent = new StringContent(
            JsonSerializer.Serialize(invalidCreateRequest),
            Encoding.UTF8,
            "application/json"
        );
        var invalidCreateResponse = await client.PostAsync(
            $"/api/dm/d/{datasourceId}/s/",
            invalidCreateContent
        );
        invalidCreateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Test duplicate schema creation (try to create dbo)
        var duplicateCreateRequest = new SchemaDto { SchemaName = "dbo" };
        var duplicateCreateContent = new StringContent(
            JsonSerializer.Serialize(duplicateCreateRequest),
            Encoding.UTF8,
            "application/json"
        );
        var duplicateCreateResponse = await client.PostAsync(
            $"/api/dm/d/{datasourceId}/s",
            duplicateCreateContent
        );
        duplicateCreateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Test operations on non-existent schema
        const string nonExistentSchema = "NonExistentSchema";

        // Get non-existent schema
        var getNonExistentResponse = await client.GetAsync(
            $"/api/dm/d/{datasourceId}/s/{nonExistentSchema}"
        );
        getNonExistentResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Delete non-existent schema
        var deleteNonExistentResponse = await client.DeleteAsync(
            $"/api/dm/d/{datasourceId}/s/{nonExistentSchema}"
        );
        deleteNonExistentResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Check existence of non-existent schema
        var existsNonExistentResponse = await client.GetAsync(
            $"/api/dm/d/{datasourceId}/s/{nonExistentSchema}/exists"
        );
        existsNonExistentResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var existsNonExistentResult =
            await existsNonExistentResponse.ReadAsJsonAsync<SchemaExistsResponse>();
        existsNonExistentResult.Should().NotBeNull();
        existsNonExistentResult!.Result.Should().BeFalse();
    }

    [Fact]
    public async Task SchemaEndpoints_UnsupportedOperations_SQLite()
    {
        var datasources = _fixture
            .GetTestDatasources()
            .Where(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_Sqlite)
            .ToList();
        using var factory = new WafWithInMemoryDatasourceRepository(datasources);
        using var client = factory.CreateClient();
        const string datasourceId = TestcontainersAssemblyFixture.DatasourceId_Sqlite;

        // Test that CREATE schema is not supported in SQLite
        var createRequest = new SchemaDto { SchemaName = "TestSchema" };
        var createContent = new StringContent(
            JsonSerializer.Serialize(createRequest),
            Encoding.UTF8,
            "application/json"
        );
        var createResponse = await client.PostAsync($"/api/dm/d/{datasourceId}/s", createContent);
        var responseBody = await createResponse.ReadAsJsonAsync<ProblemDetails>();
        responseBody.Should().NotBeNull();
        responseBody.Detail.Should().NotBeNull();
        responseBody.Detail.Contains("The provider does not support schema operations.");
        createResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Test that DROP schema is not supported in SQLite
        var deleteResponse = await client.DeleteAsync($"/api/dm/d/{datasourceId}/s/someschema");
        responseBody.Should().NotBeNull();
        responseBody.Detail.Should().NotBeNull();
        responseBody.Detail.Contains("The provider does not support schema operations.");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Test that invalid schema returns not found
        var getInvalidResponse = await client.GetAsync($"/api/dm/d/{datasourceId}/s/invalidschema");
        responseBody.Should().NotBeNull();
        responseBody.Detail.Should().NotBeNull();
        responseBody.Detail.Contains("The provider does not support schema operations.");
        getInvalidResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Test that invalid schema exists returns false
        var existsInvalidResponse = await client.GetAsync(
            $"/api/dm/d/{datasourceId}/s/invalidschema/exists"
        );
        responseBody.Should().NotBeNull();
        responseBody.Detail.Should().NotBeNull();
        responseBody.Detail.Contains("The provider does not support schema operations.");
        existsInvalidResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Database-Specific Tests

    [Fact]
    public async Task SchemaEndpoints_PostgreSQL_PublicSchema()
    {
        var datasources = _fixture
            .GetTestDatasources()
            .Where(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_PostgreSql)
            .ToList();
        using var factory = new WafWithInMemoryDatasourceRepository(datasources);
        using var client = factory.CreateClient();
        const string datasourceId = TestcontainersAssemblyFixture.DatasourceId_PostgreSql;

        // Verify PostgreSQL has public schema
        var listResponse = await client.GetAsync($"/api/dm/d/{datasourceId}/s/");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listResult = await listResponse.ReadAsJsonAsync<SchemaListResponse>();
        listResult.Should().NotBeNull();
        listResult!.Result.Should().NotBeNull();
        listResult.Result.Should().Contain(s => s.SchemaName == "public");

        // Get the public schema
        var getResponse = await client.GetAsync($"/api/dm/d/{datasourceId}/s/public");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var getResult = await getResponse.ReadAsJsonAsync<SchemaResponse>();
        getResult.Should().NotBeNull();
        getResult!.Result.Should().NotBeNull();
        getResult.Result!.SchemaName.Should().Be("public");

        // Check public schema exists
        var existsResponse = await client.GetAsync($"/api/dm/d/{datasourceId}/s/public/exists");
        existsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var existsResult = await existsResponse.ReadAsJsonAsync<SchemaExistsResponse>();
        existsResult.Should().NotBeNull();
        existsResult!.Result.Should().BeTrue();
    }

    [Fact]
    public async Task SchemaEndpoints_SqlServer_DefaultSchema()
    {
        var datasources = _fixture
            .GetTestDatasources()
            .Where(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_SqlServer)
            .ToList();
        using var factory = new WafWithInMemoryDatasourceRepository(datasources);
        using var client = factory.CreateClient();
        const string datasourceId = TestcontainersAssemblyFixture.DatasourceId_SqlServer;

        // Verify SQL Server has dbo schema
        var listResponse = await client.GetAsync($"/api/dm/d/{datasourceId}/s/");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listResult = await listResponse.ReadAsJsonAsync<SchemaListResponse>();
        listResult.Should().NotBeNull();
        listResult!.Result.Should().NotBeNull();
        listResult.Result.Should().Contain(s => s.SchemaName == "dbo");

        // Get the dbo schema
        var getResponse = await client.GetAsync($"/api/dm/d/{datasourceId}/s/dbo");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var getResult = await getResponse.ReadAsJsonAsync<SchemaResponse>();
        getResult.Should().NotBeNull();
        getResult!.Result.Should().NotBeNull();
        getResult.Result!.SchemaName.Should().Be("dbo");

        // Check dbo schema exists
        var existsResponse = await client.GetAsync($"/api/dm/d/{datasourceId}/s/dbo/exists");
        existsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var existsResult = await existsResponse.ReadAsJsonAsync<SchemaExistsResponse>();
        existsResult.Should().NotBeNull();
        existsResult!.Result.Should().BeTrue();
    }

    #endregion
}
