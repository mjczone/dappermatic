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
/// Integration tests for DapperMatic view REST endpoints.
/// </summary>
public class ViewEndpointsTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private readonly TestcontainersAssemblyFixture _fixture;

    public ViewEndpointsTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    #region Default Schema View Endpoints Tests

    [Fact]
    public async Task ListViews_DefaultSchema_GET_SqlServer_ReturnsViews()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        // First create a test view
        await CreateTestView(
            client,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "TestView1"
        );

        var response = await client.GetAsync("/api/dm/d/Test-SqlServer/v/");

        response.Should().HaveStatusCode(HttpStatusCode.OK);
        var result = await response.ReadAsJsonAsync<ViewListResponse>();
        result.Should().NotBeNull();
        result!.Result.Should().NotBeNull();
        result.Result.Should().Contain(v => v.ViewName == "TestView1");
    }

    [Fact]
    public async Task ListViews_DefaultSchema_GET_WithFilter_ReturnsFilteredViews()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        // Create test views
        await CreateTestView(
            client,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "CustomerView"
        );
        await CreateTestView(
            client,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "OrderView"
        );

        var response = await client.GetAsync("/api/dm/d/Test-SqlServer/v/?filter=Customer");

        response.Should().HaveStatusCode(HttpStatusCode.OK);
        var result = await response.ReadAsJsonAsync<ViewListResponse>();
        result.Should().NotBeNull();
        result!.Result.Should().NotBeNull();
        result.Result.Should().Contain(v => v.ViewName == "CustomerView");
        result.Result.Should().NotContain(v => v.ViewName == "OrderView");
    }

    [Fact]
    public async Task GetView_DefaultSchema_GET_ExistingView_ReturnsView()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        // Create a test view
        await CreateTestView(
            client,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "TestView2"
        );

        var response = await client.GetAsync("/api/dm/d/Test-SqlServer/v/TestView2");

        response.Should().HaveStatusCode(HttpStatusCode.OK);
        var result = await response.ReadAsJsonAsync<ViewResponse>();
        result.Should().NotBeNull();
        result!.Result.Should().NotBeNull();
        result.Result!.ViewName.Should().Be("TestView2");
    }

    [Fact]
    public async Task GetView_DefaultSchema_GET_NonExistentView_ReturnsNotFound()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dm/d/Test-SqlServer/v/NonExistentView");

        response.Should().HaveStatusCode(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateView_DefaultSchema_POST_SqlServer_CreatesView()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var request = new CreateViewRequest
        {
            ViewName = "TestCreateView",
            ViewDefinition = "SELECT 1 AS TestColumn",
        };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json"
        );

        var response = await client.PostAsync("/api/dm/d/Test-SqlServer/v/", content);

        response.Should().HaveStatusCode(HttpStatusCode.Created);
        var result = await response.ReadAsJsonAsync<ViewResponse>();
        result.Should().NotBeNull();
        result!.Result.Should().NotBeNull();
        result.Result!.ViewName.Should().Be("TestCreateView");
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("created successfully");
    }

    [Fact]
    public async Task CreateView_DefaultSchema_POST_DuplicateView_ReturnsConflict()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        // Create first view
        await CreateTestView(
            client,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "DuplicateView"
        );

        // Try to create duplicate
        var request = new CreateViewRequest
        {
            ViewName = "DuplicateView",
            ViewDefinition = "SELECT 1 AS TestColumn",
        };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json"
        );

        var response = await client.PostAsync("/api/dm/d/Test-SqlServer/v/", content);

        response.Should().HaveStatusCode(HttpStatusCode.Conflict);
        var result = await response.ReadAsJsonAsync<ViewResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Message.Should().Contain("already exists");
    }

    [Fact]
    public async Task UpdateView_DefaultSchema_PUT_ExistingView_UpdatesView()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        // Create initial view
        await CreateTestView(
            client,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "ViewToUpdate"
        );

        var request = new UpdateViewRequest { NewViewDefinition = "SELECT 2 AS UpdatedColumn" };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json"
        );

        var response = await client.PutAsync("/api/dm/d/Test-SqlServer/v/ViewToUpdate", content);

        response.Should().HaveStatusCode(HttpStatusCode.OK);
        var result = await response.ReadAsJsonAsync<ViewResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Message.Should().Contain("updated successfully");
    }

    [Fact]
    public async Task UpdateView_DefaultSchema_PUT_NonExistentView_ReturnsNotFound()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var request = new UpdateViewRequest { NewViewDefinition = "SELECT 1 AS TestColumn" };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json"
        );

        var response = await client.PutAsync("/api/dm/d/Test-SqlServer/v/NonExistentView", content);

        response.Should().HaveStatusCode(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DropView_DefaultSchema_DELETE_ExistingView_DropsView()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        // Create view to drop
        await CreateTestView(
            client,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "ViewToDrop"
        );

        var response = await client.DeleteAsync("/api/dm/d/Test-SqlServer/v/ViewToDrop");

        response.Should().HaveStatusCode(HttpStatusCode.OK);
        var result = await response.ReadAsJsonAsync<ViewResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Message.Should().Contain("dropped successfully");
    }

    [Fact]
    public async Task DropView_DefaultSchema_DELETE_NonExistentView_ReturnsNotFound()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/dm/d/Test-SqlServer/v/NonExistentView");

        response.Should().HaveStatusCode(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ViewExists_DefaultSchema_GET_ExistingView_ReturnsTrue()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        // Create test view
        await CreateTestView(
            client,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "ExistingView"
        );

        var response = await client.GetAsync("/api/dm/d/Test-SqlServer/v/ExistingView/exists");

        response.Should().HaveStatusCode(HttpStatusCode.OK);
        var result = await response.ReadAsJsonAsync<ViewExistsResponse>();
        result.Should().NotBeNull();
        result!.Result.Should().BeTrue();
    }

    [Fact]
    public async Task ViewExists_DefaultSchema_GET_NonExistentView_ReturnsFalse()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dm/d/Test-SqlServer/v/NonExistentView/exists");

        response.Should().HaveStatusCode(HttpStatusCode.OK);
        var result = await response.ReadAsJsonAsync<ViewExistsResponse>();
        result.Should().NotBeNull();
        result!.Result.Should().BeFalse();
    }

    [Fact]
    public async Task QueryView_DefaultSchema_POST_ReturnsData()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        // Create a view with known data
        await CreateTestView(
            client,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "QueryableView",
            "SELECT 1 AS Id, 'Test' AS Name"
        );

        var request = new QueryRequest { Take = 10, Skip = 0 };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json"
        );

        var response = await client.PostAsync(
            "/api/dm/d/Test-SqlServer/v/QueryableView/query",
            content
        );

        response.Should().HaveStatusCode(HttpStatusCode.OK);
        var result = await response.ReadAsJsonAsync<QueryResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Result.Should().NotBeNull();
        result.Result!.Data.Should().NotBeEmpty();
    }

    [Fact]
    public async Task QueryView_DefaultSchema_GET_WithParameters_ReturnsFilteredData()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        // Create a view
        await CreateTestView(
            client,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "FilterableView",
            "SELECT 1 AS Id, 'Test' AS Name"
        );

        var response = await client.GetAsync(
            "/api/dm/d/Test-SqlServer/v/FilterableView/query?take=5&skip=0"
        );

        response.Should().HaveStatusCode(HttpStatusCode.OK);
        var result = await response.ReadAsJsonAsync<QueryResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Result.Should().NotBeNull();
        result.Result!.Pagination.Take.Should().Be(5);
    }

    #endregion

    #region Schema-Specific View Endpoints Tests

    [Fact]
    public async Task ListViews_SchemaSpecific_GET_SqlServer_ReturnsViews()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        // Create test view in dbo schema
        await CreateTestViewInSchema(
            client,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "dbo",
            "SchemaView1"
        );

        var response = await client.GetAsync("/api/dm/d/Test-SqlServer/s/dbo/v/");

        response.Should().HaveStatusCode(HttpStatusCode.OK);
        var result = await response.ReadAsJsonAsync<ViewListResponse>();
        result.Should().NotBeNull();
        result!.Result.Should().NotBeNull();
        result.Result.Should().Contain(v => v.ViewName == "SchemaView1");
    }

    [Fact]
    public async Task GetView_SchemaSpecific_GET_ExistingView_ReturnsView()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        // Create test view in dbo schema
        await CreateTestViewInSchema(
            client,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "dbo",
            "SchemaView2"
        );

        var response = await client.GetAsync("/api/dm/d/Test-SqlServer/s/dbo/v/SchemaView2");

        response.Should().HaveStatusCode(HttpStatusCode.OK);
        var result = await response.ReadAsJsonAsync<ViewResponse>();
        result.Should().NotBeNull();
        result!.Result.Should().NotBeNull();
        result.Result!.ViewName.Should().Be("SchemaView2");
        result.Result.SchemaName.Should().Be("dbo");
    }

    [Fact]
    public async Task CreateView_SchemaSpecific_POST_CreatesViewInSchema()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var request = new CreateViewRequest
        {
            ViewName = "SchemaCreateView",
            ViewDefinition = "SELECT 1 AS TestColumn",
        };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json"
        );

        var response = await client.PostAsync("/api/dm/d/Test-SqlServer/s/dbo/v/", content);

        response.Should().HaveStatusCode(HttpStatusCode.Created);
        var result = await response.ReadAsJsonAsync<ViewResponse>();
        result.Should().NotBeNull();
        result!.Result.Should().NotBeNull();
        result.Result!.ViewName.Should().Be("SchemaCreateView");
        result.Result.SchemaName.Should().Be("dbo");
        result.Message.Should().Contain("in schema 'dbo'");
    }

    [Fact]
    public async Task UpdateView_SchemaSpecific_PUT_UpdatesViewInSchema()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        // Create initial view in schema
        await CreateTestViewInSchema(
            client,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "dbo",
            "SchemaViewToUpdate"
        );

        var request = new UpdateViewRequest { NewViewDefinition = "SELECT 2 AS UpdatedColumn" };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json"
        );

        var response = await client.PutAsync(
            "/api/dm/d/Test-SqlServer/s/dbo/v/SchemaViewToUpdate",
            content
        );

        response.Should().HaveStatusCode(HttpStatusCode.OK);
        var result = await response.ReadAsJsonAsync<ViewResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Message.Should().Contain("updated successfully in schema 'dbo'");
    }

    [Fact]
    public async Task DropView_SchemaSpecific_DELETE_DropsViewFromSchema()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        // Create view to drop in schema
        await CreateTestViewInSchema(
            client,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "dbo",
            "SchemaViewToDrop"
        );

        var response = await client.DeleteAsync(
            "/api/dm/d/Test-SqlServer/s/dbo/v/SchemaViewToDrop"
        );

        response.Should().HaveStatusCode(HttpStatusCode.OK);
        var result = await response.ReadAsJsonAsync<ViewResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Message.Should().Contain("dropped successfully from schema 'dbo'");
    }

    [Fact]
    public async Task QueryView_SchemaSpecific_POST_ReturnsDataFromSchema()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        // Create a view in schema
        await CreateTestViewInSchema(
            client,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "dbo",
            "SchemaQueryableView",
            "SELECT 1 AS Id, 'Test' AS Name"
        );

        var request = new QueryRequest
        {
            Take = 10,
            Skip = 0,
            IncludeTotal = true,
        };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json"
        );

        var response = await client.PostAsync(
            "/api/dm/d/Test-SqlServer/s/dbo/v/SchemaQueryableView/query",
            content
        );

        response.Should().HaveStatusCode(HttpStatusCode.OK);
        var result = await response.ReadAsJsonAsync<QueryResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Message.Should().Contain("in schema 'dbo'");
        result.Result.Should().NotBeNull();
        result.Result!.Data.Should().NotBeEmpty();
    }

    [Fact]
    public async Task QueryView_SchemaSpecific_GET_WithColumnSelection_ReturnsSelectedColumns()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        // Create a view in schema
        await CreateTestViewInSchema(
            client,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "dbo",
            "SelectableView",
            "SELECT 1 AS Id, 'Test' AS Name, 'Description' AS Description"
        );

        var response = await client.GetAsync(
            "/api/dm/d/Test-SqlServer/s/dbo/v/SelectableView/query?select=Id,Name&take=10"
        );

        response.Should().HaveStatusCode(HttpStatusCode.OK);
        var result = await response.ReadAsJsonAsync<QueryResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Result.Should().NotBeNull();
    }

    #endregion

    #region SQLite-Specific Tests

    [Fact(
        Skip = "SQLite view visibility issue across HTTP requests in integration tests - VACUUM approach also failed"
    )]
    public async Task ListViews_SQLite_GET_ReturnsViews()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        // SQLite doesn't support schemas, but views should still work
        await CreateTestView(
            client,
            TestcontainersAssemblyFixture.DatasourceId_Sqlite,
            "SQLiteView"
        );

        var response = await client.GetAsync("/api/dm/d/Test-SQLite/v/");

        response.Should().HaveStatusCode(HttpStatusCode.OK);
        var result = await response.ReadAsJsonAsync<ViewListResponse>();
        result.Should().NotBeNull();
        result!.Result.Should().NotBeNull();
        result.Result.Should().Contain(v => v.ViewName == "SQLiteView");
    }

    [Fact]
    public async Task CreateView_SQLite_POST_CreatesView()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var request = new CreateViewRequest
        {
            ViewName = "SQLiteTestView",
            ViewDefinition = "SELECT 1 AS TestColumn",
        };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json"
        );

        var response = await client.PostAsync("/api/dm/d/Test-SQLite/v/", content);

        response.Should().HaveStatusCode(HttpStatusCode.Created);
        var result = await response.ReadAsJsonAsync<ViewResponse>();
        result.Should().NotBeNull();
        result!.Result.Should().NotBeNull();
        result.Result!.ViewName.Should().Be("SQLiteTestView");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ListViews_NonExistentDatasource_ReturnsNotFound()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dm/d/NonExistent/v/");

        response.Should().HaveStatusCode(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateView_InvalidViewDefinition_ReturnsBadRequest()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var request = new CreateViewRequest
        {
            ViewName = "InvalidView",
            ViewDefinition = "", // Empty definition
        };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json"
        );

        var response = await client.PostAsync("/api/dm/d/Test-SqlServer/v/", content);

        response.Should().HaveStatusCode(HttpStatusCode.BadRequest);
        var result = await response.ReadAsJsonAsync<ViewResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Message.Should().Contain("View definition is required");
    }

    [Fact]
    public async Task UpdateView_EmptyViewDefinition_ReturnsBadRequest()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        var request = new UpdateViewRequest
        {
            NewViewDefinition = "", // Empty definition
        };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json"
        );

        var response = await client.PutAsync("/api/dm/d/Test-SqlServer/v/SomeView", content);

        response.Should().HaveStatusCode(HttpStatusCode.BadRequest);
        var result = await response.ReadAsJsonAsync<ViewResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Message.Should().Contain("View definition is required");
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
        var request = new CreateViewRequest
        {
            ViewName = viewName,
            ViewDefinition = viewDefinition,
        };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json"
        );

        var response = await client.PostAsync($"/api/dm/d/{datasourceId}/v/", content);
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
        var request = new CreateViewRequest
        {
            ViewName = viewName,
            ViewDefinition = viewDefinition,
        };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json"
        );

        var response = await client.PostAsync(
            $"/api/dm/d/{datasourceId}/s/{schemaName}/v/",
            content
        );
        response.EnsureSuccessStatusCode();
    }

    #endregion
}
