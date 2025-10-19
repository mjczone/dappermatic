// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using FluentAssertions;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Services;
using MJCZone.DapperMatic.AspNetCore.Validation;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Services;

public partial class DapperMaticServiceTests
{
    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer, "dbo")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql, "public")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql, null)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite, null)]
    public async Task Can_manage_view_Async(string datasourceId, string? schemaName)
    {
        using var factory = GetDefaultWebApplicationFactory();
        var service = GetDapperMaticService(factory);

        // Non-existent datasource throws NotFound
        await CheckInvalidDatasourceHandlingFetchingViews(service, schemaName);

        // Non-existent schema throws NotFound
        await CheckInvalidSchemaHandlingFetchingViews(service, datasourceId, "NonExistentSchema");

        // Create test table for View operations
        var tableName = "VWTableTest_" + Guid.NewGuid().ToString("N")[..8];
        await CreateTestTableWithoutViews(service, datasourceId, tableName, schemaName);

        // Non-existent view throws NotFound
        var invalidViewContext = OperationIdentifiers.ForViewGet(
            datasourceId,
            "NonExistentView",
            schemaName
        );
        var invalidViewAct = async () =>
            await service.GetViewAsync(
                invalidViewContext,
                datasourceId,
                "NonExistentView",
                schemaName
            );
        await invalidViewAct.Should().ThrowAsync<KeyNotFoundException>();

        // Retrieve Views (should be empty)
        var listContext = OperationIdentifiers.ForViewList(datasourceId, schemaName);
        var views = await service.GetViewsAsync(listContext, datasourceId, schemaName: schemaName);
        // Log the views for debugging
        Log.WriteLine(
            $"Initial views in datasource '{datasourceId}' schema '{schemaName}': {string.Join(", ", views.Select(v => v.ViewName))}"
        );
        views.Should().BeEmpty();

        // Add test Views
        var viewName = "VW_" + Guid.NewGuid().ToString("N")[..8];
        var createViewRequest = new ViewDto
        {
            ViewName = viewName,
            SchemaName = schemaName,
            Definition = $"SELECT * FROM {tableName}",
        };
        var createContext = OperationIdentifiers.ForViewCreate(datasourceId, createViewRequest);
        var createdView = await service.CreateViewAsync(
            createContext,
            datasourceId,
            createViewRequest
        );
        var createViewRequest2 = new ViewDto
        {
            ViewName = viewName + "2",
            SchemaName = schemaName,
            Definition = $"SELECT Id, Name FROM {tableName}",
        };
        var createContext2 = OperationIdentifiers.ForViewCreate(datasourceId, createViewRequest2);
        var createdView2 = await service.CreateViewAsync(
            createContext2,
            datasourceId,
            createViewRequest2
        );

        // Verify Views added
        views = await service.GetViewsAsync(listContext, datasourceId, schemaName: schemaName);
        views.Should().HaveCount(2);

        // Verify single View exists
        var viewContext = OperationIdentifiers.ForViewGet(
            datasourceId,
            createdView.ViewName!,
            schemaName
        );
        var retrievedView = await service.GetViewAsync(
            viewContext,
            datasourceId,
            createdView.ViewName!,
            schemaName
        );
        retrievedView.Should().BeEquivalentTo(createdView);
        retrievedView.Definition.Should().ContainEquivalentOf(tableName);
        // This actually only works for SQL Server and SQLite due to how the other DBs store view definitions
        // MySQL alters the formatting and adds backticks around identifiers
        // PostgreSQL alters the formatting and changes capitalization of unquoted identifiers
        if (
            datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
            || datasourceId == TestcontainersAssemblyFixture.DatasourceId_Sqlite
        )
        {
            retrievedView.Definition.Should().BeEquivalentTo(createViewRequest.Definition);
        }

        // Attempt to add duplicate View (should fail)
        var duplicateAct = async () =>
            await service.CreateViewAsync(createContext, datasourceId, createViewRequest);
        await duplicateAct.Should().ThrowAsync<DuplicateKeyException>();

        // Rename View
        var newViewName = viewName + "_Renamed";
        var renameContext = OperationIdentifiers.ForViewRename(
            datasourceId,
            createdView.ViewName!,
            newViewName,
            schemaName
        );
        var renamedView = await service.RenameViewAsync(
            renameContext,
            datasourceId,
            createdView.ViewName!,
            newViewName,
            schemaName
        );
        renamedView.Should().NotBeNull();
        renamedView.ViewName.Should().BeEquivalentTo(newViewName);

        // Verify View renamed
        renamedView = await service.GetViewAsync(
            renameContext,
            datasourceId,
            newViewName,
            schemaName
        );
        renamedView.Should().NotBeNull();
        renamedView.ViewName.Should().BeEquivalentTo(newViewName);

        // Rename View to existing name (should fail)
        var renameToExistingAct = async () =>
            await service.RenameViewAsync(
                renameContext,
                datasourceId,
                newViewName,
                createViewRequest2.ViewName,
                schemaName
            );
        await renameToExistingAct.Should().ThrowAsync<DuplicateKeyException>();

        // Update View definition
        var updateRequest = new ViewDto
        {
            ViewName = newViewName,
            SchemaName = schemaName,
            Definition = $"SELECT Id, Name, CreatedAt FROM {tableName}",
        };
        var updateContext = OperationIdentifiers.ForViewUpdate(
            datasourceId,
            newViewName,
            updateRequest
        );
        var updatedView = await service.UpdateViewAsync(
            updateContext,
            datasourceId,
            newViewName,
            updateRequest
        );
        updatedView.Should().NotBeNull();
        updatedView.Definition.Should().ContainEquivalentOf(tableName);
        updatedView.Definition.Should().ContainEquivalentOf("CreatedAt");

        // Query View data in a number of ways
        var queryRequest = new QueryDto
        {
            Take = 10,
            Skip = 0,
            Filters = new Dictionary<string, string> { { "Name.eq", "Chili Flakes" } },
        };
        var queryContext = OperationIdentifiers.ForViewQuery(
            datasourceId,
            newViewName,
            queryRequest,
            schemaName
        );
        var queryResult = await service.QueryViewAsync(
            queryContext,
            datasourceId,
            newViewName,
            queryRequest
        );
        queryResult.Should().NotBeNull();
        queryResult.Data.Should().NotBeNull();
        queryResult.Data.Should().BeEmpty();

        // Drop View
        var dropContext = OperationIdentifiers.ForViewDrop(datasourceId, newViewName, schemaName);
        await service.DropViewAsync(dropContext, datasourceId, newViewName, schemaName);

        // Verify View dropped using both GetViews and GetView
        views = await service.GetViewsAsync(listContext, datasourceId, schemaName: schemaName);
        views.Should().HaveCount(1);

        var getDroppedAct = async () =>
            await service.GetViewAsync(dropContext, datasourceId, newViewName, schemaName);
        await getDroppedAct.Should().ThrowAsync<KeyNotFoundException>();

        // Cleanup - drop test table
        var dropTableContext = OperationIdentifiers.ForTableDrop(
            datasourceId,
            tableName,
            schemaName
        );
        await service.DropTableAsync(dropTableContext, datasourceId, tableName, schemaName);
    }

    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer, "dbo")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql, "public")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql, null)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite, null)]
    public async Task Can_query_view_Async(string datasourceId, string? schemaName)
    {
        using var factory = GetDefaultWebApplicationFactory();
        var service = GetDapperMaticService(factory);

        // Verify initial state - get initial view count
        var context = OperationIdentifiers.ForViewList(datasourceId, schemaName);
        var initialViews = await service.GetViewsAsync(
            context,
            datasourceId,
            schemaName: schemaName
        );
        initialViews.Should().NotBeNull();
        var initialViewCount = initialViews.Count();

        // ***********************************************
        // More QUERY specific tests
        // ***********************************************

        // Create multiple test views with different structures
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

        // Verify view creation - should now have 3 more views
        var viewsAfterCreation = await service.GetViewsAsync(
            context,
            datasourceId,
            schemaName: schemaName
        );
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
        var getContext = OperationIdentifiers.ForViewGet(
            datasourceId,
            "WorkflowTest_ComplexView",
            schemaName
        );
        var specificView = await service.GetViewAsync(
            getContext,
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
        var simpleExistsContext = OperationIdentifiers.ForViewExists(
            datasourceId,
            "WorkflowTest_SimpleView",
            schemaName
        );
        var simpleExists = await service.ViewExistsAsync(
            simpleExistsContext,
            datasourceId,
            "WorkflowTest_SimpleView",
            schemaName: schemaName
        );
        var complexExistsContext = OperationIdentifiers.ForViewExists(
            datasourceId,
            "WorkflowTest_ComplexView",
            schemaName
        );
        var complexExists = await service.ViewExistsAsync(
            complexExistsContext,
            datasourceId,
            "WorkflowTest_ComplexView",
            schemaName: schemaName
        );
        var dataExistsContext = OperationIdentifiers.ForViewExists(
            datasourceId,
            "WorkflowTest_DataView",
            schemaName
        );
        var dataExists = await service.ViewExistsAsync(
            dataExistsContext,
            datasourceId,
            "WorkflowTest_DataView",
            schemaName: schemaName
        );
        var nonExistentExistsContext = OperationIdentifiers.ForViewExists(
            datasourceId,
            "NonExistentView",
            schemaName
        );
        var nonExistentExists = await service.ViewExistsAsync(
            nonExistentExistsContext,
            datasourceId,
            "NonExistentView",
            schemaName: schemaName
        );

        simpleExists.Should().BeTrue();
        complexExists.Should().BeTrue();
        dataExists.Should().BeTrue();
        nonExistentExists.Should().BeFalse();

        // Create view with known data
        await CreateTestView(
            service,
            datasourceId,
            "QueryableView",
            "SELECT 1 AS Id, 'Test' AS Name"
        );

        var request = new QueryDto
        {
            Take = 10,
            Skip = 0,
            IncludeTotal = true,
        };

        var queryContext = OperationIdentifiers.ForViewQuery(
            datasourceId,
            "QueryableView",
            request
        );
        var result = await service.QueryViewAsync(
            queryContext,
            datasourceId,
            "QueryableView",
            request
        );

        result.Should().NotBeNull();
        result.Data.Should().NotBeEmpty();
        result.Pagination.Should().NotBeNull();
        result.Pagination.Take.Should().Be(10);
        result.Pagination.Skip.Should().Be(0);

        // Create view with test data
        await CreateTestView(
            service,
            datasourceId,
            "FilterableView",
            "SELECT 1 AS Id, 'Active' AS Status UNION SELECT 2, 'Inactive'"
        );

        request = new QueryDto
        {
            Take = 10,
            Skip = 0,
            Filters = new Dictionary<string, string> { { "Status.eq", "Active" } },
        };

        queryContext = OperationIdentifiers.ForViewQuery(datasourceId, "FilterableView", request);
        result = await service.QueryViewAsync(
            queryContext,
            datasourceId,
            "FilterableView",
            request
        );

        result.Should().NotBeNull();
        result.Data.Should().NotBeEmpty();

        // Create view with multiple columns
        await CreateTestView(
            service,
            datasourceId,
            "SelectableView",
            "SELECT 1 AS Id, 'Test' AS Name, 'Description' AS Description"
        );

        request = new QueryDto
        {
            Take = 10,
            Skip = 0,
            Select = "Id,Name",
        };

        queryContext = OperationIdentifiers.ForViewQuery(datasourceId, "SelectableView", request);
        result = await service.QueryViewAsync(
            queryContext,
            datasourceId,
            "SelectableView",
            request
        );

        result.Should().NotBeNull();
        result.Data.Should().NotBeEmpty();
        result.Fields.Should().NotBeEmpty();

        // Query the views in the database and then delete them
        var allViews = await service.GetViewsAsync(
            OperationIdentifiers.ForViewList(datasourceId, schemaName),
            datasourceId,
            schemaName: schemaName
        );
        foreach (var view in allViews)
        {
            var dropContext = OperationIdentifiers.ForViewDrop(
                datasourceId,
                view.ViewName!,
                schemaName
            );
            await service.DropViewAsync(dropContext, datasourceId, view.ViewName!, schemaName);
        }
    }

    private async Task<TableDto> CreateTestTableWithoutViews(
        IDapperMaticService service,
        string datasourceId,
        string tableName,
        string? schemaName
    )
    {
        var request = new TableDto
        {
            TableName = tableName,
            SchemaName = schemaName,
            Columns =
            [
                new ColumnDto
                {
                    ColumnName = "Id",
                    ProviderDataType = "int",
                    IsNullable = false,
                },
                new ColumnDto
                {
                    ColumnName = "Name",
                    ProviderDataType =
                        datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                            ? "nvarchar(255)"
                            : "varchar(255)",
                    IsNullable = true,
                },
                new ColumnDto
                {
                    ColumnName = "CreatedAt",
                    ProviderDataType =
                        datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                            ? "datetime2"
                        : datasourceId == TestcontainersAssemblyFixture.DatasourceId_PostgreSql
                            ? "timestamp"
                        : "datetime",
                    IsNullable = false,
                },
            ],
        };

        var context = OperationIdentifiers.ForTableCreate(datasourceId, request);
        return await service.CreateTableAsync(context, datasourceId, request);
    }

    private async Task CheckInvalidDatasourceHandlingFetchingViews(
        IDapperMaticService service,
        string? schemaName
    )
    {
        var invalidDatasourceId = "NonExistent";
        var invalidContext = OperationIdentifiers.ForViewList(invalidDatasourceId, schemaName);
        var invalidAct = async () =>
            await service.GetViewsAsync(
                invalidContext,
                invalidDatasourceId,
                schemaName: schemaName
            );
        await invalidAct.Should().ThrowAsync<KeyNotFoundException>();
    }

    private async Task CheckInvalidSchemaHandlingFetchingViews(
        IDapperMaticService service,
        string datasourceId,
        string schemaName
    )
    {
        // only matters for SQL Server and PostgreSQL
        if (
            datasourceId != TestcontainersAssemblyFixture.DatasourceId_SqlServer
            && datasourceId != TestcontainersAssemblyFixture.DatasourceId_PostgreSql
        )
        {
            return;
        }

        var invalidContext = OperationIdentifiers.ForViewList(datasourceId, schemaName);
        var invalidAct = async () =>
            await service.GetViewsAsync(invalidContext, datasourceId, schemaName);
        await invalidAct.Should().ThrowAsync<KeyNotFoundException>();
    }

    private static async Task CreateTestView(
        IDapperMaticService service,
        string datasourceId,
        string viewName,
        string viewDefinition = "SELECT 1 AS TestColumn"
    )
    {
        var view = new ViewDto { ViewName = viewName, Definition = viewDefinition };

        var context = OperationIdentifiers.ForViewCreate(datasourceId, view);
        var result = await service.CreateViewAsync(context, datasourceId, view);
        result
            .Should()
            .NotBeNull($"Failed to create view '{viewName}' in datasource '{datasourceId}'");
    }

    private static async Task<ViewDto?> CreateSimpleTestView(
        IDapperMaticService service,
        string datasourceId,
        string viewName,
        string? schemaName
    )
    {
        var view = new ViewDto
        {
            SchemaName = schemaName,
            ViewName = viewName,
            Definition = "SELECT 1 AS Id, 'Simple' AS Name",
        };

        var context = OperationIdentifiers.ForViewCreate(datasourceId, view);
        return await service.CreateViewAsync(context, datasourceId, view);
    }

    private static async Task<ViewDto?> CreateComplexTestView(
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

        var view = new ViewDto
        {
            SchemaName = schemaName,
            ViewName = viewName,
            Definition = definition,
        };

        var context = OperationIdentifiers.ForViewCreate(datasourceId, view);
        return await service.CreateViewAsync(context, datasourceId, view);
    }

    private static async Task<ViewDto?> CreateTestViewWithData(
        IDapperMaticService service,
        string datasourceId,
        string viewName,
        string? schemaName
    )
    {
        var view = new ViewDto
        {
            SchemaName = schemaName,
            ViewName = viewName,
            Definition =
                "SELECT 1 AS Id, 'Active' AS Status, 25 AS Age UNION ALL SELECT 2, 'Inactive', 30",
        };

        var context = OperationIdentifiers.ForViewCreate(datasourceId, view);
        return await service.CreateViewAsync(context, datasourceId, view);
    }
}
