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
    public async Task Table_Management_Tests(string datasourceId, string? schemaName)
    {
        using var factory = GetDefaultWebApplicationFactory();
        var service = GetDapperMaticService(factory);

        // Non-existent datasource throws NotFound
        await CheckInvalidDatasourceHandlingFetchingTables(service, schemaName);

        // Non-existent schema throws NotFound
        await CheckInvalidSchemaHandlingFetchingTables(service, datasourceId, "NonExistentSchema");

        // Non-existent table throws NotFound
        var invalidTableContext = OperationIdentifiers.ForTableGet(
            datasourceId,
            "NonExistentTable",
            schemaName
        );
        var invalidTableAct = async () =>
            await service.GetTableAsync(
                invalidTableContext,
                datasourceId,
                "NonExistentTable",
                schemaName
            );
        await invalidTableAct.Should().ThrowAsync<KeyNotFoundException>();

        // Retrieve Tables (should have initial count, may include system tables)
        var listContext = OperationIdentifiers.ForTableList(datasourceId, schemaName);
        var initialTables = await service.GetTablesAsync(
            listContext,
            datasourceId,
            schemaName: schemaName
        );
        initialTables.Should().NotBeNull();
        var initialTableCount = initialTables.Count();

        // Add test Tables
        var tableName = "TBL_" + Guid.NewGuid().ToString("N")[..8];
        var createTableRequest = new TableDto
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
            PrimaryKeyConstraint = new PrimaryKeyConstraintDto
            {
                ConstraintName = $"PK_{tableName}",
                ColumnNames = ["Id"],
            },
        };
        var createContext = OperationIdentifiers.ForTableCreate(datasourceId, createTableRequest);
        var createdTable = await service.CreateTableAsync(
            createContext,
            datasourceId,
            createTableRequest
        );

        var createTableRequest2 = new TableDto
        {
            TableName = tableName + "2",
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
        var createContext2 = OperationIdentifiers.ForTableCreate(datasourceId, createTableRequest2);
        var createdTable2 = await service.CreateTableAsync(
            createContext2,
            datasourceId,
            createTableRequest2
        );

        // Verify Tables added
        var tablesAfterCreation = await service.GetTablesAsync(
            listContext,
            datasourceId,
            schemaName: schemaName
        );
        tablesAfterCreation.Should().HaveCount(initialTableCount + 2);

        // Verify single Table exists
        var tableContext = OperationIdentifiers.ForTableGet(
            datasourceId,
            createdTable.TableName!,
            schemaName
        );
        var retrievedTable = await service.GetTableAsync(
            tableContext,
            datasourceId,
            createdTable.TableName!,
            schemaName
        );
        retrievedTable.Should().NotBeNull();
        retrievedTable.TableName.Should().BeEquivalentTo(createdTable.TableName);
        retrievedTable.Columns.Should().HaveCount(3);

        // Attempt to add duplicate Table (should fail)
        var duplicateAct = async () =>
            await service.CreateTableAsync(createContext, datasourceId, createTableRequest);
        await duplicateAct.Should().ThrowAsync<DuplicateKeyException>();

        // Rename Table
        var newTableName = tableName + "_Renamed";
        var renameContext = OperationIdentifiers.ForTableRename(
            datasourceId,
            createdTable.TableName!,
            newTableName,
            schemaName
        );
        var renamedTable = await service.RenameTableAsync(
            renameContext,
            datasourceId,
            createdTable.TableName!,
            newTableName,
            schemaName
        );
        renamedTable.Should().NotBeNull();
        renamedTable.TableName.Should().BeEquivalentTo(newTableName);

        // Verify Table renamed
        var renamedTableContext = OperationIdentifiers.ForTableGet(
            datasourceId,
            newTableName,
            schemaName
        );
        renamedTable = await service.GetTableAsync(
            renamedTableContext,
            datasourceId,
            newTableName,
            schemaName
        );
        renamedTable.Should().NotBeNull();
        renamedTable.TableName.Should().BeEquivalentTo(newTableName);

        // Rename Table to existing name (should fail)
        var renameToExistingAct = async () =>
            await service.RenameTableAsync(
                renameContext,
                datasourceId,
                newTableName,
                createTableRequest2.TableName,
                schemaName
            );
        await renameToExistingAct.Should().ThrowAsync<DuplicateKeyException>();

        // Query Table data in a number of ways
        var queryRequest = new QueryDto
        {
            Take = 10,
            Skip = 0,
            Filters = new Dictionary<string, string> { { "Name.eq", "Test Name" } },
        };
        var queryContext = OperationIdentifiers.ForTableQuery(
            datasourceId,
            newTableName,
            queryRequest,
            schemaName
        );
        var queryResult = await service.QueryTableAsync(
            queryContext,
            datasourceId,
            newTableName,
            queryRequest,
            schemaName
        );
        queryResult.Should().NotBeNull();
        queryResult.Data.Should().NotBeNull();
        queryResult.Data.Should().BeEmpty();

        // Drop Table
        var dropContext = OperationIdentifiers.ForTableDrop(datasourceId, newTableName, schemaName);
        await service.DropTableAsync(dropContext, datasourceId, newTableName, schemaName);

        // Verify Table dropped using both GetTables and GetTable
        var tablesAfterDrop = await service.GetTablesAsync(
            listContext,
            datasourceId,
            schemaName: schemaName
        );
        tablesAfterDrop.Should().HaveCount(initialTableCount + 1);

        var getDroppedAct = async () =>
            await service.GetTableAsync(dropContext, datasourceId, newTableName, schemaName);
        await getDroppedAct.Should().ThrowAsync<KeyNotFoundException>();

        // Cleanup - drop second test table
        var dropTableContext2 = OperationIdentifiers.ForTableDrop(
            datasourceId,
            createTableRequest2.TableName!,
            schemaName
        );
        await service.DropTableAsync(
            dropTableContext2,
            datasourceId,
            createTableRequest2.TableName!,
            schemaName
        );
    }

    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer, "dbo")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql, "public")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql, null)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite, null)]
    public async Task Table_Query_Tests(string datasourceId, string? schemaName)
    {
        using var factory = GetDefaultWebApplicationFactory();
        var service = GetDapperMaticService(factory);

        // Verify initial state - get initial table count
        var context = OperationIdentifiers.ForTableList(datasourceId, schemaName);
        var initialTables = await service.GetTablesAsync(
            context,
            datasourceId,
            schemaName: schemaName
        );
        initialTables.Should().NotBeNull();
        var initialTableCount = initialTables.Count();

        // ***********************************************
        // More QUERY specific tests
        // ***********************************************

        // Create multiple test tables with different structures
        var simpleTable = await CreateSimpleTestTable(
            service,
            datasourceId,
            "WorkflowTest_SimpleTable",
            schemaName
        );
        var complexTable = await CreateComplexTestTable(
            service,
            datasourceId,
            "WorkflowTest_ComplexTable",
            schemaName
        );
        var dataTable = await CreateTestTableWithConstraints(
            service,
            datasourceId,
            "WorkflowTest_DataTable",
            schemaName
        );

        simpleTable.Should().NotBeNull();
        complexTable.Should().NotBeNull();
        dataTable.Should().NotBeNull();

        // Verify table creation - should now have 3 more tables
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
                    "WorkflowTest_SimpleTable",
                    StringComparison.OrdinalIgnoreCase
                )
            );
        tablesAfterCreation
            .Should()
            .Contain(t =>
                string.Equals(
                    t.TableName,
                    "WorkflowTest_ComplexTable",
                    StringComparison.OrdinalIgnoreCase
                )
            );
        tablesAfterCreation
            .Should()
            .Contain(t =>
                string.Equals(
                    t.TableName,
                    "WorkflowTest_DataTable",
                    StringComparison.OrdinalIgnoreCase
                )
            );

        // Step 4: Get specific table with full details
        var getContext = OperationIdentifiers.ForTableGet(
            datasourceId,
            "WorkflowTest_ComplexTable",
            schemaName
        );
        var specificTable = await service.GetTableAsync(
            getContext,
            datasourceId,
            "WorkflowTest_ComplexTable",
            schemaName: schemaName
        );
        specificTable.Should().NotBeNull();
        string.Equals(
                specificTable!.TableName,
                "WorkflowTest_ComplexTable",
                StringComparison.OrdinalIgnoreCase
            )
            .Should()
            .BeTrue();
        specificTable.Columns.Should().NotBeEmpty();

        // Step 5: Check table existence
        var simpleExistsContext = OperationIdentifiers.ForTableExists(
            datasourceId,
            "WorkflowTest_SimpleTable",
            schemaName
        );
        var simpleExists = await service.TableExistsAsync(
            simpleExistsContext,
            datasourceId,
            "WorkflowTest_SimpleTable",
            schemaName: schemaName
        );
        var complexExistsContext = OperationIdentifiers.ForTableExists(
            datasourceId,
            "WorkflowTest_ComplexTable",
            schemaName
        );
        var complexExists = await service.TableExistsAsync(
            complexExistsContext,
            datasourceId,
            "WorkflowTest_ComplexTable",
            schemaName: schemaName
        );
        var dataExistsContext = OperationIdentifiers.ForTableExists(
            datasourceId,
            "WorkflowTest_DataTable",
            schemaName
        );
        var dataExists = await service.TableExistsAsync(
            dataExistsContext,
            datasourceId,
            "WorkflowTest_DataTable",
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

        // Query with pagination
        var request = new QueryDto
        {
            Take = 10,
            Skip = 0,
            IncludeTotal = true,
        };

        var queryContext = OperationIdentifiers.ForTableQuery(
            datasourceId,
            "WorkflowTest_DataTable",
            request,
            schemaName
        );
        var result = await service.QueryTableAsync(
            queryContext,
            datasourceId,
            "WorkflowTest_DataTable",
            request,
            schemaName
        );

        result.Should().NotBeNull();
        result.Data.Should().NotBeNull();
        result.Pagination.Should().NotBeNull();
        result.Pagination.Take.Should().Be(10);
        result.Pagination.Skip.Should().Be(0);

        // Query with filters (empty result expected)
        request = new QueryDto
        {
            Take = 10,
            Skip = 0,
            Filters = new Dictionary<string, string> { { "Status.eq", "Active" } },
        };

        queryContext = OperationIdentifiers.ForTableQuery(
            datasourceId,
            "WorkflowTest_DataTable",
            request,
            schemaName
        );
        result = await service.QueryTableAsync(
            queryContext,
            datasourceId,
            "WorkflowTest_DataTable",
            request,
            schemaName
        );

        result.Should().NotBeNull();
        result.Data.Should().NotBeNull();

        // Query with column selection
        request = new QueryDto
        {
            Take = 10,
            Skip = 0,
            Select = "Id,Email",
        };

        queryContext = OperationIdentifiers.ForTableQuery(
            datasourceId,
            "WorkflowTest_ComplexTable",
            request,
            schemaName
        );
        result = await service.QueryTableAsync(
            queryContext,
            datasourceId,
            "WorkflowTest_ComplexTable",
            request,
            schemaName
        );

        result.Should().NotBeNull();
        result.Data.Should().NotBeNull();
        result.Fields.Should().NotBeEmpty();

        // Query with sorting
        request = new QueryDto
        {
            Take = 10,
            Skip = 0,
            OrderBy = "Email.desc",
        };

        queryContext = OperationIdentifiers.ForTableQuery(
            datasourceId,
            "WorkflowTest_DataTable",
            request,
            schemaName
        );
        result = await service.QueryTableAsync(
            queryContext,
            datasourceId,
            "WorkflowTest_DataTable",
            request,
            schemaName
        );

        result.Should().NotBeNull();
        result.Data.Should().NotBeNull();
    }

    private async Task CheckInvalidDatasourceHandlingFetchingTables(
        IDapperMaticService service,
        string? schemaName
    )
    {
        var invalidDatasourceId = "NonExistent";
        var invalidContext = OperationIdentifiers.ForTableList(invalidDatasourceId, schemaName);
        var invalidAct = async () =>
            await service.GetTablesAsync(
                invalidContext,
                invalidDatasourceId,
                schemaName: schemaName
            );
        await invalidAct.Should().ThrowAsync<KeyNotFoundException>();
    }

    private async Task CheckInvalidSchemaHandlingFetchingTables(
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

        var invalidContext = OperationIdentifiers.ForTableList(datasourceId, schemaName);
        var invalidAct = async () =>
            await service.GetTablesAsync(invalidContext, datasourceId, schemaName);
        await invalidAct.Should().ThrowAsync<KeyNotFoundException>();
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
                    ColumnName = "Email",
                    ProviderDataType =
                        datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                            ? "nvarchar(255)"
                            : "varchar(255)",
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
}
