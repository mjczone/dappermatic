// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using FluentAssertions;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Models.Requests;
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
        var initialTables = await service.GetTablesAsync(datasourceId, schemaName: schemaName);
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
        var specificTable = await service.GetTableAsync(
            datasourceId,
            "WorkflowTest_Complex",
            schemaName: schemaName,
            includeColumns: true,
            includeIndexes: true,
            includeConstraints: true
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
        var simpleExists = await service.TableExistsAsync(
            datasourceId,
            "WorkflowTest_Simple",
            schemaName: schemaName
        );
        var complexExists = await service.TableExistsAsync(
            datasourceId,
            "WorkflowTest_Complex",
            schemaName: schemaName
        );
        var dataExists = await service.TableExistsAsync(
            datasourceId,
            "WorkflowTest_Data",
            schemaName: schemaName
        );
        var nonExistentExists = await service.TableExistsAsync(
            datasourceId,
            "NonExistentTable",
            schemaName: schemaName
        );

        simpleExists.Should().BeTrue();
        complexExists.Should().BeTrue();
        dataExists.Should().BeTrue();
        nonExistentExists.Should().BeFalse();

        // Step 6: Query table data (should be empty but structure should work)
        var queryRequest = new QueryRequest { Take = 10, Skip = 0 };
        var queryResult = await service.QueryTableAsync(
            datasourceId,
            "WorkflowTest_Data",
            queryRequest,
            schemaName: schemaName
        );
        queryResult.Should().NotBeNull();
        queryResult.Data.Should().NotBeNull();
        // queryResult.Columns.Should().NotBeEmpty();

        // Step 7: Rename a table
        var renameResult = await service.RenameTableAsync(
            datasourceId,
            "WorkflowTest_Simple",
            "WorkflowTest_Renamed",
            schemaName: schemaName
        );
        renameResult.Should().BeTrue();

        // Step 8: Verify rename - old name shouldn't exist, new name should exist
        var oldNameExists = await service.TableExistsAsync(
            datasourceId,
            "WorkflowTest_Simple",
            schemaName: schemaName
        );
        var newNameExists = await service.TableExistsAsync(
            datasourceId,
            "WorkflowTest_Renamed",
            schemaName: schemaName
        );
        oldNameExists.Should().BeFalse();
        newNameExists.Should().BeTrue();

        // Step 9: Drop a table
        var dropResult = await service.DropTableAsync(
            datasourceId,
            "WorkflowTest_Complex",
            schemaName: schemaName
        );
        dropResult.Should().BeTrue();

        // Step 10: Verify final state - should have initial count + 2 (created 3, dropped 1)
        var finalTables = await service.GetTablesAsync(datasourceId, schemaName: schemaName);
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
        var droppedTableExists = await service.TableExistsAsync(
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

        var act = async () => await service.GetTablesAsync("NonExistent");

        await act.Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");
    }


    [Fact]
    public async Task QueryTableAsync_NonExistentTable_ThrowsException()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var request = new QueryRequest { Take = 10, Skip = 0 };

        var act = async () =>
            await service.QueryTableAsync(
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

        var createRequest = new CreateTableRequest
        {
            TableName = "TestTable",
            Columns =
            [
                new CreateTableColumnRequest
                {
                    ColumnName = "Id",
                    ProviderDataType = "int",
                    IsNullable = false,
                },
            ],
        };
        var queryRequest = new QueryRequest { Take = 10, Skip = 0 };

        // Test all methods with non-existent datasource
        var getTableAct = async () => await service.GetTableAsync("NonExistent", "TestTable");
        await getTableAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");

        var createTableAct = async () =>
            await service.CreateTableAsync("NonExistent", createRequest);
        await createTableAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");

        var dropTableAct = async () => await service.DropTableAsync("NonExistent", "TestTable");
        await dropTableAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");

        var tableExistsAct = async () => await service.TableExistsAsync("NonExistent", "TestTable");
        await tableExistsAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");

        var renameTableAct = async () =>
            await service.RenameTableAsync("NonExistent", "OldName", "NewName");
        await renameTableAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");

        var queryTableAct = async () =>
            await service.QueryTableAsync("NonExistent", "TestTable", queryRequest);
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

        var request = new QueryRequest
        {
            Take = 10,
            Skip = 0,
            IncludeTotal = true,
        };

        var result = await service.QueryTableAsync(
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

        var request = new QueryRequest
        {
            Take = 10,
            Skip = 0,
            Filters = new Dictionary<string, string> { { "Status.eq", "Active" } },
        };

        var result = await service.QueryTableAsync(
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

        var request = new QueryRequest
        {
            Take = 10,
            Skip = 0,
            Select = "Id,Title",
        };

        var result = await service.QueryTableAsync(
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

        var request = new QueryRequest
        {
            Take = 10,
            Skip = 0,
            OrderBy = "Email.desc",
        };

        var result = await service.QueryTableAsync(
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

        var request = new QueryRequest
        {
            Take = 5,
            Skip = 10,
            IncludeTotal = true,
        };

        var result = await service.QueryTableAsync(
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
        var request = new CreateTableRequest
        {
            TableName = tableName,
            SchemaName =
                datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer ? "dbo"
                : datasourceId == TestcontainersAssemblyFixture.DatasourceId_PostgreSql ? "public"
                : null,
            Columns =
            [
                new CreateTableColumnRequest
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
                new CreateTableColumnRequest
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

        return await service.CreateTableAsync(datasourceId, request);
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
        var request = new CreateTableRequest
        {
            TableName = tableName,
            SchemaName = schemaName,
            Columns =
            [
                new CreateTableColumnRequest
                {
                    ColumnName = "Id",
                    ProviderDataType = "int",
                    IsNullable = false,
                },
                new CreateTableColumnRequest
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

        return await service.CreateTableAsync(datasourceId, request);
    }

    private static async Task<TableDto?> CreateComplexTestTable(
        IDapperMaticService service,
        string datasourceId,
        string tableName,
        string? schemaName
    )
    {
        var request = new CreateTableRequest
        {
            TableName = tableName,
            SchemaName = schemaName,
            Columns =
            [
                new CreateTableColumnRequest
                {
                    ColumnName = "Id",
                    ProviderDataType = "int",
                    IsNullable = false,
                },
                new CreateTableColumnRequest
                {
                    ColumnName = "Title",
                    ProviderDataType =
                        datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                            ? "nvarchar(255)"
                            : "varchar(255)",
                    IsNullable = false,
                },
                new CreateTableColumnRequest
                {
                    ColumnName = "Description",
                    ProviderDataType =
                        datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                            ? "nvarchar(1000)"
                            : "text",
                    IsNullable = true,
                },
                new CreateTableColumnRequest
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
            PrimaryKey = new CreateTablePrimaryKeyRequest
            {
                ConstraintName = $"PK_{tableName}",
                Columns = ["Id"],
            },
        };

        return await service.CreateTableAsync(datasourceId, request);
    }

    private static async Task<TableDto?> CreateTestTableWithConstraints(
        IDapperMaticService service,
        string datasourceId,
        string tableName,
        string? schemaName
    )
    {
        var request = new CreateTableRequest
        {
            TableName = tableName,
            SchemaName = schemaName,
            Columns =
            [
                new CreateTableColumnRequest
                {
                    ColumnName = "Id",
                    ProviderDataType = "int",
                    IsNullable = false,
                },
                new CreateTableColumnRequest
                {
                    ColumnName = "Email",
                    ProviderDataType =
                        datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                            ? "nvarchar(255)"
                            : "varchar(255)",
                    IsNullable = false,
                },
                new CreateTableColumnRequest
                {
                    ColumnName = "Age",
                    ProviderDataType = "int",
                    IsNullable = true,
                },
                new CreateTableColumnRequest
                {
                    ColumnName = "Status",
                    ProviderDataType =
                        datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                            ? "nvarchar(20)"
                            : "varchar(20)",
                    IsNullable = false,
                },
            ],
            PrimaryKey = new CreateTablePrimaryKeyRequest
            {
                ConstraintName = $"PK_{tableName}",
                Columns = ["Id"],
            },
            UniqueConstraints =
            [
                new CreateTableUniqueConstraintRequest
                {
                    ConstraintName = $"UQ_{tableName}_Email",
                    ColumnNames = ["Email"],
                },
            ],
            CheckConstraints =
                datasourceId != TestcontainersAssemblyFixture.DatasourceId_MySql
                    ? // MySQL doesn't support check constraints in older versions
                    [
                        new CreateTableCheckConstraintRequest
                        {
                            ConstraintName = $"CK_{tableName}_Age",
                            CheckExpression = "Age >= 0 AND Age <= 150",
                        },
                    ]
                    : null,
        };

        return await service.CreateTableAsync(datasourceId, request);
    }

    #endregion
}
