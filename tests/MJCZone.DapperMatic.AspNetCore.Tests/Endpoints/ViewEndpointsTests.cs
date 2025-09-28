// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Models.Responses;
using MJCZone.DapperMatic.AspNetCore.Tests.Factories;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Endpoints;

/// <summary>
/// Integration tests for DapperMatic view REST endpoints.
/// </summary>
public class ViewEndpointsTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private readonly TestcontainersAssemblyFixture _fixture;

    public ViewEndpointsTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    #region Comprehensive Workflow Tests

    [Fact]
    public async Task ViewEndpoints_CompleteWorkflow_Success()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();
        const string viewName = "WorkflowTestView";
        const string datasourceId = TestcontainersAssemblyFixture.DatasourceId_SqlServer;

        // 1. GET MULTI - List all views (should be empty initially)
        var listResponse1 = await client.GetAsync($"/api/dm/d/{datasourceId}/v/");
        listResponse1.Should().HaveStatusCode(HttpStatusCode.OK);
        var listResult1 = await listResponse1.ReadAsJsonAsync<ViewListResponse>();
        listResult1.Should().NotBeNull();
        listResult1!.Result.Should().NotBeNull();
        var initialViewCount = listResult1.Result!.Count();
        listResult1.Result.Should().NotContain(v => v.ViewName == viewName);

        // 2. GET SINGLE - Try to get non-existent view (should return 404)
        var getResponse1 = await client.GetAsync($"/api/dm/d/{datasourceId}/v/{viewName}");
        getResponse1.Should().HaveStatusCode(HttpStatusCode.NotFound);

        // 3. CREATE - Create a new view
        var createRequest = new ViewDto
        {
            ViewName = viewName,
            Definition = "SELECT 1 AS Id, 'Initial' AS Name",
        };
        var createContent = new StringContent(
            JsonSerializer.Serialize(createRequest),
            Encoding.UTF8,
            "application/json"
        );
        var createResponse = await client.PostAsync(
            $"/api/dm/d/{datasourceId}/v/{viewName}",
            createContent
        );
        createResponse.Should().HaveStatusCode(HttpStatusCode.Created);
        var createResult = await createResponse.ReadAsJsonAsync<ViewResponse>();
        createResult.Should().NotBeNull();
        createResult!.Result.Should().NotBeNull();
        createResult.Result!.ViewName.Should().Be(viewName);

        // 4. EXISTS - Check if view exists (should return true)
        var existsResponse1 = await client.GetAsync(
            $"/api/dm/d/{datasourceId}/v/{viewName}/exists"
        );
        existsResponse1.Should().HaveStatusCode(HttpStatusCode.OK);
        var existsResult1 = await existsResponse1.ReadAsJsonAsync<ViewExistsResponse>();
        existsResult1.Should().NotBeNull();
        existsResult1!.Result.Should().BeTrue();

        // 5. GET MULTI - List views again (should contain new view)
        var listResponse2 = await client.GetAsync($"/api/dm/d/{datasourceId}/v/");
        listResponse2.Should().HaveStatusCode(HttpStatusCode.OK);
        var listResult2 = await listResponse2.ReadAsJsonAsync<ViewListResponse>();
        listResult2.Should().NotBeNull();
        listResult2!.Result.Should().NotBeNull();
        listResult2.Result.Should().HaveCount(initialViewCount + 1);
        listResult2.Result.Should().Contain(v => v.ViewName == viewName);

        // 6. GET SINGLE - Get the created view (should return view details)
        var getResponse2 = await client.GetAsync($"/api/dm/d/{datasourceId}/v/{viewName}");
        getResponse2.Should().HaveStatusCode(HttpStatusCode.OK);
        var getResult2 = await getResponse2.ReadAsJsonAsync<ViewResponse>();
        getResult2.Should().NotBeNull();
        getResult2!.Result.Should().NotBeNull();
        getResult2.Result!.ViewName.Should().Be(viewName);
        getResult2.Result.Definition.Should().Contain("Initial");

        // 7. UPDATE - Update the view definition
        var updateRequest = new ViewDto
        {
            Definition = "SELECT 2 AS Id, 'Updated' AS Name, 'Modified' AS Description",
        };
        var updateContent = new StringContent(
            JsonSerializer.Serialize(updateRequest),
            Encoding.UTF8,
            "application/json"
        );
        var updateResponse = await client.PutAsync(
            $"/api/dm/d/{datasourceId}/v/{viewName}",
            updateContent
        );
        updateResponse.Should().HaveStatusCode(HttpStatusCode.OK);
        var updateResult = await updateResponse.ReadAsJsonAsync<ViewResponse>();
        updateResult.Should().NotBeNull();
        updateResult!.Result.Should().NotBeNull();

        // 8. GET SINGLE - Get updated view (should show changes)
        var getResponse3 = await client.GetAsync($"/api/dm/d/{datasourceId}/v/{viewName}");
        getResponse3.Should().HaveStatusCode(HttpStatusCode.OK);
        var getResult3 = await getResponse3.ReadAsJsonAsync<ViewResponse>();
        getResult3.Should().NotBeNull();
        getResult3!.Result.Should().NotBeNull();
        getResult3.Result!.ViewName.Should().Be(viewName);
        getResult3.Result.Definition.Should().Contain("Updated");
        getResult3.Result.Definition.Should().Contain("Modified");

        // 9. DELETE - Delete the view
        var deleteResponse = await client.DeleteAsync($"/api/dm/d/{datasourceId}/v/{viewName}");
        deleteResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

        // 10. EXISTS - Check if view exists (should return false)
        var existsResponse2 = await client.GetAsync(
            $"/api/dm/d/{datasourceId}/v/{viewName}/exists"
        );
        existsResponse2.Should().HaveStatusCode(HttpStatusCode.OK);
        var existsResult2 = await existsResponse2.ReadAsJsonAsync<ViewExistsResponse>();
        existsResult2.Should().NotBeNull();
        existsResult2!.Result.Should().BeFalse();

        // 11. GET MULTI - List views (should be back to initial count)
        var listResponse3 = await client.GetAsync($"/api/dm/d/{datasourceId}/v/");
        listResponse3.Should().HaveStatusCode(HttpStatusCode.OK);
        var listResult3 = await listResponse3.ReadAsJsonAsync<ViewListResponse>();
        listResult3.Should().NotBeNull();
        listResult3!.Result.Should().NotBeNull();
        listResult3.Result.Should().HaveCount(initialViewCount);
        listResult3.Result.Should().NotContain(v => v.ViewName == viewName);

        // 12. GET SINGLE - Try to get deleted view (should return 404)
        var getResponse4 = await client.GetAsync($"/api/dm/d/{datasourceId}/v/{viewName}");
        getResponse4.Should().HaveStatusCode(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ViewQueryEndpoints_CompleteWorkflow_Success()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();
        const string viewName = "QueryWorkflowView";
        const string datasourceId = TestcontainersAssemblyFixture.DatasourceId_SqlServer;

        // Create a test view with sample data for querying
        await CreateTestView(
            client,
            datasourceId,
            viewName,
            "SELECT 1 AS Id, 'Test1' AS Name, 'Description1' AS Description UNION ALL SELECT 2 AS Id, 'Test2' AS Name, 'Description2' AS Description"
        );

        try
        {
            // Test POST query with pagination
            var postQueryRequest = new QueryDto
            {
                Take = 10,
                Skip = 0,
                IncludeTotal = true,
            };
            var postQueryContent = new StringContent(
                JsonSerializer.Serialize(postQueryRequest),
                Encoding.UTF8,
                "application/json"
            );
            var postQueryResponse = await client.PostAsync(
                $"/api/dm/d/{datasourceId}/v/{viewName}/query",
                postQueryContent
            );
            postQueryResponse.Should().HaveStatusCode(HttpStatusCode.OK);
            var postQueryResult = await postQueryResponse.ReadAsJsonAsync<QueryResponse>();
            postQueryResult.Should().NotBeNull();
            postQueryResult!.Result.Should().NotBeNull();
            postQueryResult.Result.Should().NotBeEmpty();
            postQueryResult.Pagination.Should().NotBeNull();
            postQueryResult.Pagination.Take.Should().Be(10);

            // Test GET query with parameters
            var getQueryResponse = await client.GetAsync(
                $"/api/dm/d/{datasourceId}/v/{viewName}/query?take=5&skip=0"
            );
            getQueryResponse.Should().HaveStatusCode(HttpStatusCode.OK);
            var getQueryResult = await getQueryResponse.ReadAsJsonAsync<QueryResponse>();
            getQueryResult.Should().NotBeNull();
            getQueryResult!.Result.Should().NotBeNull();
            getQueryResult.Pagination.Should().NotBeNull();
            getQueryResult.Pagination.Take.Should().Be(5);

            // Test GET query with column selection
            var selectQueryResponse = await client.GetAsync(
                $"/api/dm/d/{datasourceId}/v/{viewName}/query?select=Id,Name&take=10"
            );
            selectQueryResponse.Should().HaveStatusCode(HttpStatusCode.OK);
            var selectQueryResult = await selectQueryResponse.ReadAsJsonAsync<QueryResponse>();
            selectQueryResult.Should().NotBeNull();
            selectQueryResult!.Result.Should().NotBeNull();
            selectQueryResult.Result.Should().NotBeEmpty();
        }
        finally
        {
            // Clean up: Delete the test view
            await client.DeleteAsync($"/api/dm/d/{datasourceId}/v/{viewName}");
        }
    }

    #endregion

    #region Error Scenarios Tests

    [Fact]
    public async Task ViewEndpoints_ErrorScenarios_HandledCorrectly()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();
        const string datasourceId = TestcontainersAssemblyFixture.DatasourceId_SqlServer;

        // Test non-existent datasource
        var nonExistentDatasourceResponse = await client.GetAsync("/api/dm/d/NonExistent/v/");
        nonExistentDatasourceResponse.Should().HaveStatusCode(HttpStatusCode.NotFound);

        // Test invalid view definition (empty)
        var invalidCreateRequest = new ViewDto
        {
            ViewName = "InvalidView",
            Definition = "", // Empty definition
        };
        var invalidCreateContent = new StringContent(
            JsonSerializer.Serialize(invalidCreateRequest),
            Encoding.UTF8,
            "application/json"
        );
        var invalidCreateResponse = await client.PostAsync(
            $"/api/dm/d/{datasourceId}/v/InvalidView",
            invalidCreateContent
        );
        invalidCreateResponse.Should().HaveStatusCode(HttpStatusCode.BadRequest);
        var invalidCreateResult = await invalidCreateResponse.ReadAsJsonAsync<ViewResponse>();
        invalidCreateResult.Should().NotBeNull();
        // Note: Specific error message may vary based on validation implementation

        // Test duplicate view creation
        const string duplicateViewName = "DuplicateTestView";
        await CreateTestView(client, datasourceId, duplicateViewName);

        try
        {
            var duplicateCreateRequest = new ViewDto
            {
                ViewName = duplicateViewName,
                Definition = "SELECT 1 AS TestColumn",
            };
            var duplicateCreateContent = new StringContent(
                JsonSerializer.Serialize(duplicateCreateRequest),
                Encoding.UTF8,
                "application/json"
            );
            var duplicateCreateResponse = await client.PostAsync(
                $"/api/dm/d/{datasourceId}/v/{duplicateViewName}",
                duplicateCreateContent
            );
            duplicateCreateResponse.Should().HaveStatusCode(HttpStatusCode.Conflict);
            var duplicateCreateResult =
                await duplicateCreateResponse.ReadAsJsonAsync<ViewResponse>();
            duplicateCreateResult.Should().NotBeNull();
            // Note: Specific error message may vary based on validation implementation
        }
        finally
        {
            // Clean up
            await client.DeleteAsync($"/api/dm/d/{datasourceId}/v/{duplicateViewName}");
        }

        // Test operations on non-existent view
        const string nonExistentView = "NonExistentView";

        // Update non-existent view
        var updateNonExistentRequest = new ViewDto { Definition = "SELECT 1 AS TestColumn" };
        var updateNonExistentContent = new StringContent(
            JsonSerializer.Serialize(updateNonExistentRequest),
            Encoding.UTF8,
            "application/json"
        );
        var updateNonExistentResponse = await client.PutAsync(
            $"/api/dm/d/{datasourceId}/v/{nonExistentView}",
            updateNonExistentContent
        );
        updateNonExistentResponse.Should().HaveStatusCode(HttpStatusCode.NotFound);

        // Delete non-existent view
        var deleteNonExistentResponse = await client.DeleteAsync(
            $"/api/dm/d/{datasourceId}/v/{nonExistentView}"
        );
        deleteNonExistentResponse.Should().HaveStatusCode(HttpStatusCode.NotFound);

        // Test invalid update (empty definition)
        var invalidUpdateRequest = new ViewDto
        {
            Definition = "", // Empty definition
        };
        var invalidUpdateContent = new StringContent(
            JsonSerializer.Serialize(invalidUpdateRequest),
            Encoding.UTF8,
            "application/json"
        );
        var invalidUpdateResponse = await client.PutAsync(
            $"/api/dm/d/{datasourceId}/v/SomeView",
            invalidUpdateContent
        );
        invalidUpdateResponse.Should().HaveStatusCode(HttpStatusCode.BadRequest);
        var invalidUpdateResult = await invalidUpdateResponse.ReadAsJsonAsync<ViewResponse>();
        invalidUpdateResult.Should().NotBeNull();
        // Note: Specific error message may vary based on validation implementation
    }

    #endregion

    #region Schema-Specific Workflow Tests

    [Fact]
    public async Task SchemaSpecificViewEndpoints_CompleteWorkflow_Success()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();
        const string viewName = "SchemaWorkflowTestView";
        const string schemaName = "dbo";
        const string datasourceId = TestcontainersAssemblyFixture.DatasourceId_SqlServer;

        // Test complete workflow in schema-specific endpoints

        // 1. GET MULTI - List all views in schema (should be empty initially)
        var listResponse1 = await client.GetAsync($"/api/dm/d/{datasourceId}/s/{schemaName}/v/");
        listResponse1.Should().HaveStatusCode(HttpStatusCode.OK);
        var listResult1 = await listResponse1.ReadAsJsonAsync<ViewListResponse>();
        listResult1.Should().NotBeNull();
        var initialViewCount = listResult1!.Result?.Count() ?? 0;

        // 2. CREATE - Create a new view in schema
        var createRequest = new ViewDto
        {
            ViewName = viewName,
            Definition = "SELECT 1 AS Id, 'Schema Test' AS Name",
        };
        var createContent = new StringContent(
            JsonSerializer.Serialize(createRequest),
            Encoding.UTF8,
            "application/json"
        );
        var createResponse = await client.PostAsync(
            $"/api/dm/d/{datasourceId}/s/{schemaName}/v/{viewName}",
            createContent
        );
        createResponse.Should().HaveStatusCode(HttpStatusCode.Created);
        var createResult = await createResponse.ReadAsJsonAsync<ViewResponse>();
        createResult.Should().NotBeNull();
        createResult!.Result.Should().NotBeNull();
        createResult.Result!.ViewName.Should().Be(viewName);
        createResult.Result.SchemaName.Should().Be(schemaName);

        // 3. GET SINGLE - Get the created view from schema
        var getResponse = await client.GetAsync(
            $"/api/dm/d/{datasourceId}/s/{schemaName}/v/{viewName}"
        );
        getResponse.Should().HaveStatusCode(HttpStatusCode.OK);
        var getResult = await getResponse.ReadAsJsonAsync<ViewResponse>();
        getResult.Should().NotBeNull();
        getResult!.Result.Should().NotBeNull();
        getResult.Result!.ViewName.Should().Be(viewName);
        getResult.Result.SchemaName.Should().Be(schemaName);

        // 4. UPDATE - Update the view in schema
        var updateRequest = new ViewDto
        {
            Definition = "SELECT 2 AS Id, 'Updated Schema Test' AS Name",
        };
        var updateContent = new StringContent(
            JsonSerializer.Serialize(updateRequest),
            Encoding.UTF8,
            "application/json"
        );
        var updateResponse = await client.PutAsync(
            $"/api/dm/d/{datasourceId}/s/{schemaName}/v/{viewName}",
            updateContent
        );
        updateResponse.Should().HaveStatusCode(HttpStatusCode.OK);

        // 5. DELETE - Delete the view from schema
        var deleteResponse = await client.DeleteAsync(
            $"/api/dm/d/{datasourceId}/s/{schemaName}/v/{viewName}"
        );
        deleteResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

        // 6. GET MULTI - List views again (should be back to initial count)
        var listResponse2 = await client.GetAsync($"/api/dm/d/{datasourceId}/s/{schemaName}/v/");
        listResponse2.Should().HaveStatusCode(HttpStatusCode.OK);
        var listResult2 = await listResponse2.ReadAsJsonAsync<ViewListResponse>();
        listResult2.Should().NotBeNull();
        (listResult2!.Result?.Count() ?? 0).Should().Be(initialViewCount);
    }

    #endregion

    #region SQLite-Specific Tests

    [Fact]
    public async Task SQLiteViewEndpoints_BasicWorkflow_Success()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();
        const string viewName = "SQLiteTestView";
        const string datasourceId = TestcontainersAssemblyFixture.DatasourceId_Sqlite;

        // Create view
        var createRequest = new ViewDto
        {
            ViewName = viewName,
            Definition = "SELECT 1 AS TestColumn",
        };
        var createContent = new StringContent(
            JsonSerializer.Serialize(createRequest),
            Encoding.UTF8,
            "application/json"
        );
        var createResponse = await client.PostAsync(
            $"/api/dm/d/{datasourceId}/v/{viewName}",
            createContent
        );
        createResponse.Should().HaveStatusCode(HttpStatusCode.Created);
        var createResult = await createResponse.ReadAsJsonAsync<ViewResponse>();
        createResult.Should().NotBeNull();
        createResult!.Result.Should().NotBeNull();
        createResult.Result!.ViewName.Should().Be(viewName);

        // Clean up
        await client.DeleteAsync($"/api/dm/d/{datasourceId}/v/{viewName}");
    }

    #endregion

    #region Helper Methods

    private static async Task CreateTestView(
        HttpClient client,
        string datasourceId,
        string viewName,
        string viewDefinition = "SELECT 1 AS TestColumn"
    )
    {
        var request = new ViewDto { ViewName = viewName, Definition = viewDefinition };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json"
        );

        var response = await client.PostAsync($"/api/dm/d/{datasourceId}/v/{viewName}", content);
        response.EnsureSuccessStatusCode();
    }

    private static async Task CreateTestViewInSchema(
        HttpClient client,
        string datasourceId,
        string schemaName,
        string viewName,
        string viewDefinition = "SELECT 1 AS TestColumn"
    )
    {
        var request = new ViewDto { ViewName = viewName, Definition = viewDefinition };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json"
        );

        var response = await client.PostAsync(
            $"/api/dm/d/{datasourceId}/s/{schemaName}/v/{viewName}",
            content
        );
        response.EnsureSuccessStatusCode();
    }

    #endregion
}
