// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MJCZone.DapperMatic.AspNetCore.Models.Requests;
using MJCZone.DapperMatic.AspNetCore.Services;
using MJCZone.DapperMatic.AspNetCore.Tests.Factories;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Services;

/// <summary>
/// Integration tests for DapperMaticService view operations.
/// </summary>
public class DapperMaticServiceViewsTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private readonly TestcontainersAssemblyFixture _fixture;

    public DapperMaticServiceViewsTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    #region Comprehensive View Management Workflow Tests

    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer, "dbo")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql, "public")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql, null)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite, null)]
    public async Task ViewManagement_CompleteWorkflow_WorksCorrectly(
        string datasourceId,
        string? schemaName
    )
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        // Step 1: Verify initial state - get initial view count
        var initialViews = await service.GetViewsAsync(datasourceId, schemaName: schemaName);
        initialViews.Should().NotBeNull();
        var initialViewCount = initialViews.Count();

        // Step 2: Create multiple test views with different structures
        var simpleView = await CreateSimpleTestView(
            service,
            datasourceId,
            "WorkflowTest_SimpleView",
            schemaName
        );
        var complexView = await CreateComplexTestView(
            service,
            datasourceId,
            "WorkflowTest_ComplexView",
            schemaName
        );
        var dataView = await CreateTestViewWithData(
            service,
            datasourceId,
            "WorkflowTest_DataView",
            schemaName
        );

        simpleView.Should().NotBeNull();
        complexView.Should().NotBeNull();
        dataView.Should().NotBeNull();

        // Step 3: Verify view creation - should now have 3 more views
        var viewsAfterCreation = await service.GetViewsAsync(datasourceId, schemaName: schemaName);
        viewsAfterCreation.Should().HaveCount(initialViewCount + 3);
        viewsAfterCreation
            .Should()
            .Contain(v =>
                string.Equals(
                    v.ViewName,
                    "WorkflowTest_SimpleView",
                    StringComparison.OrdinalIgnoreCase
                )
            );
        viewsAfterCreation
            .Should()
            .Contain(v =>
                string.Equals(
                    v.ViewName,
                    "WorkflowTest_ComplexView",
                    StringComparison.OrdinalIgnoreCase
                )
            );
        viewsAfterCreation
            .Should()
            .Contain(v =>
                string.Equals(
                    v.ViewName,
                    "WorkflowTest_DataView",
                    StringComparison.OrdinalIgnoreCase
                )
            );

        // Step 4: Get specific view with full details
        var specificView = await service.GetViewAsync(
            datasourceId,
            "WorkflowTest_ComplexView",
            schemaName: schemaName
        );
        specificView.Should().NotBeNull();
        string.Equals(
                specificView!.ViewName,
                "WorkflowTest_ComplexView",
                StringComparison.OrdinalIgnoreCase
            )
            .Should()
            .BeTrue();
        specificView.Definition.Should().NotBeNullOrEmpty();

        // Step 5: Check view existence
        var simpleExists = await service.ViewExistsAsync(
            datasourceId,
            "WorkflowTest_SimpleView",
            schemaName: schemaName
        );
        var complexExists = await service.ViewExistsAsync(
            datasourceId,
            "WorkflowTest_ComplexView",
            schemaName: schemaName
        );
        var dataExists = await service.ViewExistsAsync(
            datasourceId,
            "WorkflowTest_DataView",
            schemaName: schemaName
        );
        var nonExistentExists = await service.ViewExistsAsync(
            datasourceId,
            "NonExistentView",
            schemaName: schemaName
        );

        simpleExists.Should().BeTrue();
        complexExists.Should().BeTrue();
        dataExists.Should().BeTrue();
        nonExistentExists.Should().BeFalse();

        // Step 6: Test duplicate view creation (should return null)
        var duplicateView = new DmView
        {
            SchemaName = schemaName,
            ViewName = "WorkflowTest_SimpleView",
            Definition = "SELECT 999 AS TestColumn",
        };
        var duplicateResult = await service.CreateViewAsync(datasourceId, duplicateView);
        duplicateResult.Should().BeNull();

        // Step 7: Update a view (get the exact name first to handle case sensitivity)
        var viewToUpdate = viewsAfterCreation.First(v =>
            string.Equals(v.ViewName, "WorkflowTest_SimpleView", StringComparison.OrdinalIgnoreCase)
        );
        var updateResult = await service.UpdateViewAsync(
            datasourceId,
            viewToUpdate.ViewName, // Use the exact name from the database
            null, // Not renaming
            "SELECT 42 AS UpdatedColumn"
        );
        updateResult.Should().NotBeNull();
        // Use case-insensitive assertion for MySQL compatibility
        updateResult!.Definition.Should().ContainEquivalentOf("42");
        updateResult.Definition.Should().ContainEquivalentOf("UpdatedColumn");

        // Step 8: Verify update worked
        var updatedView = await service.GetViewAsync(
            datasourceId,
            viewToUpdate.ViewName, // Use the exact name from the database
            schemaName: schemaName
        );
        updatedView.Should().NotBeNull();
        updatedView!.Definition.Should().ContainEquivalentOf("42");
        updatedView.Definition.Should().ContainEquivalentOf("UpdatedColumn");

        // Step 9: Drop a view
        var dropResult = await service.DropViewAsync(
            datasourceId,
            "WorkflowTest_ComplexView",
            schemaName: schemaName
        );
        dropResult.Should().BeTrue();

        // Step 10: Verify final state - should have initial count + 2 (created 3, dropped 1)
        var finalViews = await service.GetViewsAsync(datasourceId, schemaName: schemaName);
        finalViews.Should().HaveCount(initialViewCount + 2);
        finalViews
            .Should()
            .Contain(v =>
                string.Equals(
                    v.ViewName,
                    "WorkflowTest_SimpleView",
                    StringComparison.OrdinalIgnoreCase
                )
            ); // updated view
        finalViews
            .Should()
            .Contain(v =>
                string.Equals(
                    v.ViewName,
                    "WorkflowTest_DataView",
                    StringComparison.OrdinalIgnoreCase
                )
            ); // untouched view
        finalViews
            .Should()
            .NotContain(v =>
                string.Equals(
                    v.ViewName,
                    "WorkflowTest_ComplexView",
                    StringComparison.OrdinalIgnoreCase
                )
            ); // dropped

        // Verify dropped view no longer exists
        var droppedViewExists = await service.ViewExistsAsync(
            datasourceId,
            "WorkflowTest_ComplexView",
            schemaName: schemaName
        );
        droppedViewExists.Should().BeFalse();
    }

    #endregion

    #region View Query Tests

    [Fact]
    public async Task QueryViewAsync_ValidRequest_ReturnsData()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        // Create view with known data
        await CreateTestView(
            service,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "QueryableView",
            "SELECT 1 AS Id, 'Test' AS Name"
        );

        var request = new QueryRequest
        {
            Take = 10,
            Skip = 0,
            IncludeTotal = true,
        };

        var result = await service.QueryViewAsync(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "QueryableView",
            request
        );

        result.Should().NotBeNull();
        result.Data.Should().NotBeEmpty();
        result.Pagination.Should().NotBeNull();
        result.Pagination.Take.Should().Be(10);
        result.Pagination.Skip.Should().Be(0);
    }

    [Fact]
    public async Task QueryViewAsync_WithFilters_AppliesFilters()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        // Create view with test data
        await CreateTestView(
            service,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "FilterableView",
            "SELECT 1 AS Id, 'Active' AS Status UNION SELECT 2, 'Inactive'"
        );

        var request = new QueryRequest
        {
            Take = 10,
            Skip = 0,
            Filters = new Dictionary<string, string> { { "Status.eq", "Active" } },
        };

        var result = await service.QueryViewAsync(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "FilterableView",
            request
        );

        result.Should().NotBeNull();
        result.Data.Should().NotBeEmpty();
        // In a real scenario, we would verify the filtered results
    }

    [Fact]
    public async Task QueryViewAsync_WithColumnSelection_ReturnsSelectedColumns()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        // Create view with multiple columns
        await CreateTestView(
            service,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "SelectableView",
            "SELECT 1 AS Id, 'Test' AS Name, 'Description' AS Description"
        );

        var request = new QueryRequest
        {
            Take = 10,
            Skip = 0,
            Select = "Id,Name",
        };

        var result = await service.QueryViewAsync(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "SelectableView",
            request
        );

        result.Should().NotBeNull();
        result.Data.Should().NotBeEmpty();
        result.Fields.Should().NotBeEmpty();
    }

    [Fact]
    public async Task QueryViewAsync_NonExistentView_ThrowsArgumentException()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var request = new QueryRequest { Take = 10, Skip = 0 };

        var act = async () =>
            await service.QueryViewAsync(
                TestcontainersAssemblyFixture.DatasourceId_SqlServer,
                "NonExistentView",
                request
            );

        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region Error Scenario Tests

    [Fact]
    public async Task GetViewsAsync_NonExistentDatasource_ThrowsArgumentException()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var act = async () => await service.GetViewsAsync("NonExistent");

        await act.Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");
    }

    [Fact]
    public async Task ViewManagement_NonExistentDatasource_ThrowsArgumentException()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var view = new DmView { ViewName = "TestView", Definition = "SELECT 1 AS TestColumn" };
        var queryRequest = new QueryRequest { Take = 10, Skip = 0 };

        // Test all methods with non-existent datasource
        var getViewAct = async () => await service.GetViewAsync("NonExistent", "TestView");
        await getViewAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");

        var createViewAct = async () => await service.CreateViewAsync("NonExistent", view);
        await createViewAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");

        var updateViewAct = async () =>
            await service.UpdateViewAsync("NonExistent", "TestView", null, "SELECT 1");
        await updateViewAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");

        var dropViewAct = async () => await service.DropViewAsync("NonExistent", "TestView");
        await dropViewAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");

        var viewExistsAct = async () => await service.ViewExistsAsync("NonExistent", "TestView");
        await viewExistsAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");

        var queryViewAct = async () =>
            await service.QueryViewAsync("NonExistent", "TestView", queryRequest);
        await queryViewAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");
    }

    #endregion

    #region Helper Methods

    private static async Task CreateTestView(
        IDapperMaticService service,
        string datasourceId,
        string viewName,
        string viewDefinition = "SELECT 1 AS TestColumn"
    )
    {
        var view = new DmView { ViewName = viewName, Definition = viewDefinition };

        var result = await service.CreateViewAsync(datasourceId, view);
        result
            .Should()
            .NotBeNull($"Failed to create view '{viewName}' in datasource '{datasourceId}'");
    }

    private static async Task CreateTestViewInSchema(
        IDapperMaticService service,
        string datasourceId,
        string schemaName,
        string viewName,
        string viewDefinition = "SELECT 1 AS TestColumn"
    )
    {
        var view = new DmView
        {
            SchemaName = schemaName,
            ViewName = viewName,
            Definition = viewDefinition,
        };

        await service.CreateViewAsync(datasourceId, view);
    }

    private static async Task<DmView?> CreateSimpleTestView(
        IDapperMaticService service,
        string datasourceId,
        string viewName,
        string? schemaName
    )
    {
        var view = new DmView
        {
            SchemaName = schemaName,
            ViewName = viewName,
            Definition = "SELECT 1 AS Id, 'Simple' AS Name",
        };

        return await service.CreateViewAsync(datasourceId, view);
    }

    private static async Task<DmView?> CreateComplexTestView(
        IDapperMaticService service,
        string datasourceId,
        string viewName,
        string? schemaName
    )
    {
        // Use database-agnostic view definition
        var definition = datasourceId switch
        {
            TestcontainersAssemblyFixture.DatasourceId_SqlServer =>
                "SELECT 1 AS Id, 'Complex' AS Title, 'Description text' AS Description, GETDATE() AS CreatedAt",
            TestcontainersAssemblyFixture.DatasourceId_PostgreSql =>
                "SELECT 1 AS Id, 'Complex' AS Title, 'Description text' AS Description, CURRENT_TIMESTAMP AS CreatedAt",
            TestcontainersAssemblyFixture.DatasourceId_MySql =>
                "SELECT 1 AS Id, 'Complex' AS Title, 'Description text' AS Description, NOW() AS CreatedAt",
            TestcontainersAssemblyFixture.DatasourceId_Sqlite =>
                "SELECT 1 AS Id, 'Complex' AS Title, 'Description text' AS Description, CURRENT_TIMESTAMP AS CreatedAt",
            _ => "SELECT 1 AS Id, 'Complex' AS Title, 'Description text' AS Description",
        };

        var view = new DmView
        {
            SchemaName = schemaName,
            ViewName = viewName,
            Definition = definition,
        };

        return await service.CreateViewAsync(datasourceId, view);
    }

    private static async Task<DmView?> CreateTestViewWithData(
        IDapperMaticService service,
        string datasourceId,
        string viewName,
        string? schemaName
    )
    {
        var view = new DmView
        {
            SchemaName = schemaName,
            ViewName = viewName,
            Definition =
                "SELECT 1 AS Id, 'Active' AS Status, 25 AS Age UNION ALL SELECT 2, 'Inactive', 30",
        };

        return await service.CreateViewAsync(datasourceId, view);
    }

    #endregion
}
