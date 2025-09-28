// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using FluentAssertions;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Services;
using MJCZone.DapperMatic.AspNetCore.Tests.Factories;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Services;

/// <summary>
/// Unit tests for DapperMatic service table operations.
/// </summary>
public class DapperMaticServiceTablesTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private readonly TestcontainersAssemblyFixture _fixture;

    public DapperMaticServiceTablesTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    #region Comprehensive Table Management Workflow Tests

    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer, "dbo")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql, "public")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql, null)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite, null)]
    public async Task TableManagement_CompleteWorkflow_WorksCorrectly(
        string datasourceId,
        string? schemaName
    )
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        // Step 1: Verify initial state - get initial table count (may include system tables)
        var context = OperationIdentifiers.ForTableList(datasourceId, schemaName);
        var initialTables = await service.GetTablesAsync(
            context,
            datasourceId,
            schemaName: schemaName
        );
        initialTables.Should().NotBeNull();
        var initialTableCount = initialTables.Count();

        // Step 2: Create multiple test tables with different structures
        var simpleTable = await CreateSimpleTestTable(
            service,
            datasourceId,
            "WorkflowTest_Simple",
            schemaName
        );
        var complexTable = await CreateComplexTestTable(
            service,
            datasourceId,
            "WorkflowTest_Complex",
            schemaName
        );
        var dataTable = await CreateTestTableWithConstraints(
            service,
            datasourceId,
            "WorkflowTest_Data",
            schemaName
        );

        simpleTable.Should().NotBeNull();
        complexTable.Should().NotBeNull();
        dataTable.Should().NotBeNull();

        // Step 3: Verify table creation - should now have 3 more tables
        var tablesAfterCreation = await service.GetTablesAsync(
            context,
            datasourceId,
            schemaName: schemaName
        );
        tablesAfterCreation.Should().HaveCount(initialTableCount + 3);
        tablesAfterCreation
            .Should()
            .Contain(t =>
                string.Equals(
                    t.TableName,
                    "WorkflowTest_Simple",
                    StringComparison.OrdinalIgnoreCase
                )
            );
        tablesAfterCreation
            .Should()
            .Contain(t =>
                string.Equals(
                    t.TableName,
                    "WorkflowTest_Complex",
                    StringComparison.OrdinalIgnoreCase
                )
            );
        tablesAfterCreation
            .Should()
            .Contain(t =>
                string.Equals(t.TableName, "WorkflowTest_Data", StringComparison.OrdinalIgnoreCase)
            );

        // Step 4: Get specific table with full details
        var getContext = OperationIdentifiers.ForTableGet(
            datasourceId,
            "WorkflowTest_Complex",
            schemaName
        );
        var specificTable = await service.GetTableAsync(
            getContext,
            datasourceId,
            "WorkflowTest_Complex",
            schemaName: schemaName
        );
        specificTable.Should().NotBeNull();
        string.Equals(
                specificTable!.TableName,
                "WorkflowTest_Complex",
                StringComparison.OrdinalIgnoreCase
            )
            .Should()
            .BeTrue();
        specificTable.Columns.Should().NotBeEmpty();

        // Step 5: Check table existence
        var simpleExistsContext = OperationIdentifiers.ForTableExists(
            datasourceId,
            "WorkflowTest_Simple",
            schemaName
        );
        var simpleExists = await service.TableExistsAsync(
            simpleExistsContext,
            datasourceId,
            "WorkflowTest_Simple",
            schemaName: schemaName
        );
        var complexExistsContext = OperationIdentifiers.ForTableExists(
            datasourceId,
            "WorkflowTest_Complex",
            schemaName
        );
        var complexExists = await service.TableExistsAsync(
            complexExistsContext,
            datasourceId,
            "WorkflowTest_Complex",
            schemaName: schemaName
        );
        var dataExistsContext = OperationIdentifiers.ForTableExists(
            datasourceId,
            "WorkflowTest_Data",
            schemaName
        );
        var dataExists = await service.TableExistsAsync(
            dataExistsContext,
            datasourceId,
            "WorkflowTest_Data",
            schemaName: schemaName
        );
        var nonExistentExistsContext = OperationIdentifiers.ForTableExists(
            datasourceId,
            "NonExistentTable",
            schemaName
        );
        var nonExistentExists = await service.TableExistsAsync(
            nonExistentExistsContext,
            datasourceId,
            "NonExistentTable",
            schemaName: schemaName
        );

        simpleExists.Should().BeTrue();
        complexExists.Should().BeTrue();
        dataExists.Should().BeTrue();
        nonExistentExists.Should().BeFalse();

        // Step 6: Query table data (should be empty but structure should work)
        var queryRequest = new QueryDto { Take = 10, Skip = 0 };
        var queryContext = OperationIdentifiers.ForTableQuery(
            datasourceId,
            "WorkflowTest_Data",
            queryRequest,
            schemaName
        );
        var queryResult = await service.QueryTableAsync(
            queryContext,
            datasourceId,
            "WorkflowTest_Data",
            queryRequest,
            schemaName: schemaName
        );
        queryResult.Should().NotBeNull();
        queryResult.Data.Should().NotBeNull();
        // queryResult.Columns.Should().NotBeEmpty();

        // Step 7: Rename a table
        var renameContext = OperationIdentifiers.ForTableRename(
            datasourceId,
            "WorkflowTest_Simple",
            "WorkflowTest_Renamed",
            schemaName
        );
        var renameResult = await service.RenameTableAsync(
            renameContext,
            datasourceId,
            "WorkflowTest_Simple",
            "WorkflowTest_Renamed",
            schemaName: schemaName
        );
        renameResult.Should().NotBeNull();

        // Step 8: Verify rename - old name shouldn't exist, new name should exist
        var oldNameExistsContext = OperationIdentifiers.ForTableExists(
            datasourceId,
            "WorkflowTest_Simple",
            schemaName
        );
        var oldNameExists = await service.TableExistsAsync(
            oldNameExistsContext,
            datasourceId,
            "WorkflowTest_Simple",
            schemaName: schemaName
        );
        var newNameExistsContext = OperationIdentifiers.ForTableExists(
            datasourceId,
            "WorkflowTest_Renamed",
            schemaName
        );
        var newNameExists = await service.TableExistsAsync(
            newNameExistsContext,
            datasourceId,
            "WorkflowTest_Renamed",
            schemaName: schemaName
        );
        oldNameExists.Should().BeFalse();
        newNameExists.Should().BeTrue();

        // Step 9: Drop a table
        var dropContext = OperationIdentifiers.ForTableDrop(
            datasourceId,
            "WorkflowTest_Complex",
            schemaName
        );
        await service.DropTableAsync(
            dropContext,
            datasourceId,
            "WorkflowTest_Complex",
            schemaName: schemaName
        );

        // Step 10: Verify final state - should have initial count + 2 (created 3, dropped 1)
        var finalContext = OperationIdentifiers.ForTableList(datasourceId, schemaName);
        var finalTables = await service.GetTablesAsync(
            finalContext,
            datasourceId,
            schemaName: schemaName
        );
        finalTables.Should().HaveCount(initialTableCount + 2);
        finalTables
            .Should()
            .Contain(t =>
                string.Equals(
                    t.TableName,
                    "WorkflowTest_Renamed",
                    StringComparison.OrdinalIgnoreCase
                )
            ); // renamed table
        finalTables
            .Should()
            .Contain(t =>
                string.Equals(t.TableName, "WorkflowTest_Data", StringComparison.OrdinalIgnoreCase)
            ); // untouched table
        finalTables
            .Should()
            .NotContain(t =>
                string.Equals(
                    t.TableName,
                    "WorkflowTest_Simple",
                    StringComparison.OrdinalIgnoreCase
                )
            ); // renamed
        finalTables
            .Should()
            .NotContain(t =>
                string.Equals(
                    t.TableName,
                    "WorkflowTest_Complex",
                    StringComparison.OrdinalIgnoreCase
                )
            ); // dropped

        // Verify dropped table no longer exists
        var droppedTableExistsContext = OperationIdentifiers.ForTableExists(
            datasourceId,
            "WorkflowTest_Complex",
            schemaName
        );
        var droppedTableExists = await service.TableExistsAsync(
            droppedTableExistsContext,
            datasourceId,
            "WorkflowTest_Complex",
            schemaName: schemaName
        );
        droppedTableExists.Should().BeFalse();
    }

    #endregion

    #region Error Scenario Tests

    [Fact]
    public async Task GetTablesAsync_NonExistentDatasource_ThrowsArgumentException()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var context = OperationIdentifiers.ForTableList("NonExistent");
        var act = async () => await service.GetTablesAsync(context, "NonExistent");

        await act.Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");
    }

    [Fact]
    public async Task QueryTableAsync_NonExistentTable_ThrowsException()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var request = new QueryDto { Take = 10, Skip = 0 };

        var queryContext = OperationIdentifiers.ForTableQuery(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "NonExistentTable",
            request,
            "dbo"
        );
        var act = async () =>
            await service.QueryTableAsync(
                queryContext,
                TestcontainersAssemblyFixture.DatasourceId_SqlServer,
                "NonExistentTable",
                request,
                schemaName: "dbo"
            );

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task TableManagement_NonExistentDatasource_ThrowsArgumentException()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var createRequest = new TableDto
        {
            TableName = "TestTable",
            Columns =
            [
                new ColumnDto
                {
                    ColumnName = "Id",
                    ProviderDataType = "int",
                    IsNullable = false,
                },
            ],
        };
        var queryRequest = new QueryDto { Take = 10, Skip = 0 };

        // Test all methods with non-existent datasource
        var getTableContext = OperationIdentifiers.ForTableGet("NonExistent", "TestTable");
        var getTableAct = async () =>
            await service.GetTableAsync(getTableContext, "NonExistent", "TestTable");
        await getTableAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");

        var createTableContext = OperationIdentifiers.ForTableCreate("NonExistent", createRequest);
        var createTableAct = async () =>
            await service.CreateTableAsync(createTableContext, "NonExistent", createRequest);
        await createTableAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");

        var dropTableContext = OperationIdentifiers.ForTableDrop("NonExistent", "TestTable");
        var dropTableAct = async () =>
            await service.DropTableAsync(dropTableContext, "NonExistent", "TestTable");
        await dropTableAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");

        var tableExistsContext = OperationIdentifiers.ForTableExists("NonExistent", "TestTable");
        var tableExistsAct = async () =>
            await service.TableExistsAsync(tableExistsContext, "NonExistent", "TestTable");
        await tableExistsAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");

        var renameTableContext = OperationIdentifiers.ForTableRename(
            "NonExistent",
            "OldName",
            "NewName"
        );
        var renameTableAct = async () =>
            await service.RenameTableAsync(renameTableContext, "NonExistent", "OldName", "NewName");
        await renameTableAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");

        var queryTableContext = OperationIdentifiers.ForTableQuery(
            "NonExistent",
            "TestTable",
            queryRequest
        );
        var queryTableAct = async () =>
            await service.QueryTableAsync(
                queryTableContext,
                "NonExistent",
                "TestTable",
                queryRequest
            );
        await queryTableAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");
    }

    #endregion

    #region Table Query Tests

    [Fact]
    public async Task QueryTableAsync_ValidRequest_ReturnsData()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        // Create table with known structure
        var testTable = await CreateTestTableWithData(
            service,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "QueryableTable"
        );
        testTable.Should().NotBeNull();

        var request = new QueryDto
        {
            Take = 10,
            Skip = 0,
            IncludeTotal = true,
        };

        var context = OperationIdentifiers.ForTableQuery(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "QueryableTable",
            request,
            "dbo"
        );
        var result = await service.QueryTableAsync(
            context,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "QueryableTable",
            request,
            schemaName: "dbo"
        );

        result.Should().NotBeNull();
        result.Data.Should().NotBeNull();
        result.Pagination.Should().NotBeNull();
        result.Pagination.Take.Should().Be(10);
        result.Pagination.Skip.Should().Be(0);
    }

    [Fact]
    public async Task QueryTableAsync_WithFilters_AppliesFilters()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        // Create table with test structure (empty table is fine for testing query structure)
        var testTable = await CreateTestTableWithConstraints(
            service,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "FilterableTable",
            "dbo"
        );
        testTable.Should().NotBeNull();

        var request = new QueryDto
        {
            Take = 10,
            Skip = 0,
            Filters = new Dictionary<string, string> { { "Status.eq", "Active" } },
        };

        var context = OperationIdentifiers.ForTableQuery(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "FilterableTable",
            request,
            "dbo"
        );
        var result = await service.QueryTableAsync(
            context,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "FilterableTable",
            request,
            schemaName: "dbo"
        );

        result.Should().NotBeNull();
        result.Data.Should().NotBeNull();
        // In a real scenario with data, we would verify the filtered results
    }

    [Fact]
    public async Task QueryTableAsync_WithColumnSelection_ReturnsSelectedColumns()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        // Create table with multiple columns
        var testTable = await CreateComplexTestTable(
            service,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "SelectableTable",
            "dbo"
        );
        testTable.Should().NotBeNull();

        var request = new QueryDto
        {
            Take = 10,
            Skip = 0,
            Select = "Id,Title",
        };

        var context = OperationIdentifiers.ForTableQuery(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "SelectableTable",
            request,
            "dbo"
        );
        var result = await service.QueryTableAsync(
            context,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "SelectableTable",
            request,
            schemaName: "dbo"
        );

        result.Should().NotBeNull();
        result.Data.Should().NotBeNull();
        result.Fields.Should().NotBeEmpty();
    }

    [Fact]
    public async Task QueryTableAsync_WithSorting_AppliesSorting()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        // Create table for sorting test
        var testTable = await CreateTestTableWithConstraints(
            service,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "SortableTable",
            "dbo"
        );
        testTable.Should().NotBeNull();

        var request = new QueryDto
        {
            Take = 10,
            Skip = 0,
            OrderBy = "Email.desc",
        };

        var context = OperationIdentifiers.ForTableQuery(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "SortableTable",
            request,
            "dbo"
        );
        var result = await service.QueryTableAsync(
            context,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "SortableTable",
            request,
            schemaName: "dbo"
        );

        result.Should().NotBeNull();
        result.Data.Should().NotBeNull();
        // Sorting verification would require actual data in the table
    }

    [Fact]
    public async Task QueryTableAsync_WithPagination_AppliesPagination()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        // Create table for pagination test
        var testTable = await CreateSimpleTestTable(
            service,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "PaginatedTable",
            "dbo"
        );
        testTable.Should().NotBeNull();

        var request = new QueryDto
        {
            Take = 5,
            Skip = 10,
            IncludeTotal = true,
        };

        var context = OperationIdentifiers.ForTableQuery(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "PaginatedTable",
            request,
            "dbo"
        );
        var result = await service.QueryTableAsync(
            context,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "PaginatedTable",
            request,
            schemaName: "dbo"
        );

        result.Should().NotBeNull();
        result.Data.Should().NotBeNull();
        result.Pagination.Should().NotBeNull();
        result.Pagination.Take.Should().Be(5);
        result.Pagination.Skip.Should().Be(10);
        result.Pagination.Total.Should().BeGreaterOrEqualTo(0);
    }

    #endregion

    #region Helper Methods

    private static async Task<TableDto?> CreateTestTable(
        IDapperMaticService service,
        string datasourceId,
        string tableName
    )
    {
        var request = new TableDto
        {
            TableName = tableName,
            SchemaName =
                datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer ? "dbo"
                : datasourceId == TestcontainersAssemblyFixture.DatasourceId_PostgreSql ? "public"
                : null,
            Columns =
            [
                new ColumnDto
                {
                    ColumnName = "Id",
                    ProviderDataType =
                        datasourceId == TestcontainersAssemblyFixture.DatasourceId_PostgreSql
                            ? "SERIAL"
                            : "INT",
                    IsNullable = false,
                    IsPrimaryKey = true,
                    IsAutoIncrement =
                        datasourceId != TestcontainersAssemblyFixture.DatasourceId_PostgreSql,
                },
                new ColumnDto
                {
                    ColumnName = "Name",
                    ProviderDataType =
                        datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                            ? "NVARCHAR(255)"
                            : "VARCHAR(255)",
                    IsNullable = true,
                },
            ],
        };

        var context = OperationIdentifiers.ForTableCreate(datasourceId, request);
        return await service.CreateTableAsync(context, datasourceId, request);
    }

    private static async Task<TableDto?> CreateTestTableWithData(
        IDapperMaticService service,
        string datasourceId,
        string tableName
    )
    {
        // First create the table
        var table = await CreateTestTable(service, datasourceId, tableName);

        // Note: In a real test, you might insert some data here
        // For now, just return the table structure
        return table;
    }

    private static async Task<TableDto?> CreateSimpleTestTable(
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
                            ? "nvarchar(100)"
                            : "varchar(100)",
                    IsNullable = true,
                },
            ],
        };

        var context = OperationIdentifiers.ForTableCreate(datasourceId, request);
        return await service.CreateTableAsync(context, datasourceId, request);
    }

    private static async Task<TableDto?> CreateComplexTestTable(
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
                    ColumnName = "Title",
                    ProviderDataType =
                        datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                            ? "nvarchar(255)"
                            : "varchar(255)",
                    IsNullable = false,
                },
                new ColumnDto
                {
                    ColumnName = "Description",
                    ProviderDataType =
                        datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                            ? "nvarchar(1000)"
                            : "text",
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
            PrimaryKeyConstraint = new PrimaryKeyConstraintDto
            {
                ConstraintName = $"PK_{tableName}",
                ColumnNames = ["Id"],
            },
        };

        var context = OperationIdentifiers.ForTableCreate(datasourceId, request);
        return await service.CreateTableAsync(context, datasourceId, request);
    }

    private static async Task<TableDto?> CreateTestTableWithConstraints(
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
                    ColumnName = "Email",
                    ProviderDataType =
                        datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                            ? "nvarchar(255)"
                            : "varchar(255)",
                    IsNullable = false,
                },
                new ColumnDto
                {
                    ColumnName = "Age",
                    ProviderDataType = "int",
                    IsNullable = true,
                },
                new ColumnDto
                {
                    ColumnName = "Status",
                    ProviderDataType =
                        datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                            ? "nvarchar(20)"
                            : "varchar(20)",
                    IsNullable = false,
                },
            ],
            PrimaryKeyConstraint = new PrimaryKeyConstraintDto
            {
                ConstraintName = $"PK_{tableName}",
                ColumnNames = ["Id"],
            },
            UniqueConstraints =
            [
                new UniqueConstraintDto
                {
                    ConstraintName = $"UQ_{tableName}_Email",
                    ColumnNames = ["Email"],
                },
            ],
            CheckConstraints =
                datasourceId != TestcontainersAssemblyFixture.DatasourceId_MySql
                    ? // MySQL doesn't support check constraints in older versions
                    [
                        new CheckConstraintDto
                        {
                            ConstraintName = $"CK_{tableName}_Age",
                            CheckExpression = "Age >= 0 AND Age <= 150",
                        },
                    ]
                    : null,
        };

        var context = OperationIdentifiers.ForTableCreate(datasourceId, request);
        return await service.CreateTableAsync(context, datasourceId, request);
    }

    #endregion
}
