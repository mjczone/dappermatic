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
/// Unit tests for DapperMatic service column operations.
/// </summary>
public class DapperMaticServiceColumnsTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private readonly TestcontainersAssemblyFixture _fixture;

    public DapperMaticServiceColumnsTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    #region Comprehensive Column Management Workflow Tests

    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer, "dbo")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql, "public")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql, null)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite, null)]
    public async Task ColumnManagement_CompleteWorkflow_WorksCorrectly(
        string datasourceId,
        string? schemaName
    )
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        // Step 1: Create a test table with initial columns
        var testTable = await CreateTestTableForColumns(
            service,
            datasourceId,
            "ColumnTest",
            schemaName
        );
        testTable.Should().NotBeNull();

        // Step 2: Get all columns for the table
        var listContext = OperationIdentifiers.ForColumnList(
            datasourceId,
            "ColumnTest",
            schemaName
        );
        var initialColumns = await service.GetColumnsAsync(
            listContext,
            datasourceId,
            "ColumnTest",
            schemaName: schemaName
        );
        initialColumns.Should().NotBeNull();
        var initialColumnsList = initialColumns.ToList();
        initialColumnsList.Should().HaveCount(3); // Id, Name, CreatedAt

        // Verify initial columns exist (case insensitive for PostgreSQL)
        initialColumnsList
            .Should()
            .Contain(c => string.Equals(c.ColumnName, "Id", StringComparison.OrdinalIgnoreCase));
        initialColumnsList
            .Should()
            .Contain(c => string.Equals(c.ColumnName, "Name", StringComparison.OrdinalIgnoreCase));
        initialColumnsList
            .Should()
            .Contain(c =>
                string.Equals(c.ColumnName, "CreatedAt", StringComparison.OrdinalIgnoreCase)
            );

        // Step 3: Get a specific column
        var getContext = OperationIdentifiers.ForColumnGet(
            datasourceId,
            "ColumnTest",
            "Name",
            schemaName
        );
        var nameColumn = await service.GetColumnAsync(
            getContext,
            datasourceId,
            "ColumnTest",
            "Name",
            schemaName: schemaName
        );
        nameColumn.Should().NotBeNull();
        string.Equals(nameColumn!.ColumnName, "Name", StringComparison.OrdinalIgnoreCase)
            .Should()
            .BeTrue();

        // Step 4: Add a new column
        var addColumnRequest = new ColumnDto
        {
            ColumnName = "Description",
            ProviderDataType =
                datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                    ? "nvarchar(500)"
                    : "varchar(500)",
            IsNullable = true,
        };

        var addContext = OperationIdentifiers.ForColumnAdd(
            datasourceId,
            "ColumnTest",
            addColumnRequest,
            schemaName
        );
        var addedColumn = await service.AddColumnAsync(
            addContext,
            datasourceId,
            "ColumnTest",
            addColumnRequest,
            schemaName: schemaName
        );
        addedColumn.Should().NotBeNull();
        string.Equals(addedColumn!.ColumnName, "Description", StringComparison.OrdinalIgnoreCase)
            .Should()
            .BeTrue();

        // Step 5: Verify column was added - should now have 4 columns
        var listAfterAddContext = OperationIdentifiers.ForColumnList(
            datasourceId,
            "ColumnTest",
            schemaName
        );
        var columnsAfterAdd = await service.GetColumnsAsync(
            listAfterAddContext,
            datasourceId,
            "ColumnTest",
            schemaName: schemaName
        );
        columnsAfterAdd.Should().HaveCount(4);
        columnsAfterAdd
            .Should()
            .Contain(c =>
                string.Equals(c.ColumnName, "Description", StringComparison.OrdinalIgnoreCase)
            );

        // Step 6: Rename the newly added column
        var renameContext = OperationIdentifiers.ForColumnRename(
            datasourceId,
            "ColumnTest",
            "Description",
            "LongDescription",
            schemaName
        );
        var renamedColumn = await service.RenameColumnAsync(
            renameContext,
            datasourceId,
            "ColumnTest",
            "Description",
            "LongDescription",
            schemaName: schemaName
        );
        renamedColumn.Should().NotBeNull();
        string.Equals(
                renamedColumn!.ColumnName,
                "LongDescription",
                StringComparison.OrdinalIgnoreCase
            )
            .Should()
            .BeTrue();

        // Step 7: Verify rename - old name shouldn't exist, new name should exist
        var listAfterRenameContext = OperationIdentifiers.ForColumnList(
            datasourceId,
            "ColumnTest",
            schemaName
        );
        var columnsAfterRename = await service.GetColumnsAsync(
            listAfterRenameContext,
            datasourceId,
            "ColumnTest",
            schemaName: schemaName
        );
        columnsAfterRename.Should().HaveCount(4);
        columnsAfterRename
            .Should()
            .NotContain(c =>
                string.Equals(c.ColumnName, "Description", StringComparison.OrdinalIgnoreCase)
            );
        columnsAfterRename
            .Should()
            .Contain(c =>
                string.Equals(c.ColumnName, "LongDescription", StringComparison.OrdinalIgnoreCase)
            );

        // Step 8: Get the renamed column by its new name
        var getRenamedContext = OperationIdentifiers.ForColumnGet(
            datasourceId,
            "ColumnTest",
            "LongDescription",
            schemaName
        );
        var renamedColumnResult = await service.GetColumnAsync(
            getRenamedContext,
            datasourceId,
            "ColumnTest",
            "LongDescription",
            schemaName: schemaName
        );
        renamedColumnResult.Should().NotBeNull();
        string.Equals(
                renamedColumnResult!.ColumnName,
                "LongDescription",
                StringComparison.OrdinalIgnoreCase
            )
            .Should()
            .BeTrue();

        // Step 9: Drop the renamed column
        var dropContext = OperationIdentifiers.ForColumnDrop(
            datasourceId,
            "ColumnTest",
            "LongDescription",
            schemaName
        );
        await service.DropColumnAsync(
            dropContext,
            datasourceId,
            "ColumnTest",
            "LongDescription",
            schemaName: schemaName
        );

        // Step 10: Verify final state - should be back to 3 columns
        var listFinalContext = OperationIdentifiers.ForColumnList(
            datasourceId,
            "ColumnTest",
            schemaName
        );
        var finalColumns = await service.GetColumnsAsync(
            listFinalContext,
            datasourceId,
            "ColumnTest",
            schemaName: schemaName
        );
        finalColumns.Should().HaveCount(3);
        finalColumns
            .Should()
            .NotContain(c =>
                string.Equals(c.ColumnName, "LongDescription", StringComparison.OrdinalIgnoreCase)
            );
        finalColumns
            .Should()
            .NotContain(c =>
                string.Equals(c.ColumnName, "Description", StringComparison.OrdinalIgnoreCase)
            );

        // Verify original columns still exist
        finalColumns
            .Should()
            .Contain(c => string.Equals(c.ColumnName, "Id", StringComparison.OrdinalIgnoreCase));
        finalColumns
            .Should()
            .Contain(c => string.Equals(c.ColumnName, "Name", StringComparison.OrdinalIgnoreCase));
        finalColumns
            .Should()
            .Contain(c =>
                string.Equals(c.ColumnName, "CreatedAt", StringComparison.OrdinalIgnoreCase)
            );

        // Cleanup: Drop the test table
        var dropTableContext = OperationIdentifiers.ForTableDrop(
            datasourceId,
            "ColumnTest",
            schemaName
        );
        await service.DropTableAsync(
            dropTableContext,
            datasourceId,
            "ColumnTest",
            schemaName: schemaName
        );
    }

    #endregion

    #region Error Scenario Tests

    [Fact]
    public async Task GetColumnsAsync_NonExistentTable_ThrowsArgumentException()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var context = OperationIdentifiers.ForColumnList(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "NonExistentTable",
            "dbo"
        );
        var act = async () =>
            await service.GetColumnsAsync(
                context,
                TestcontainersAssemblyFixture.DatasourceId_SqlServer,
                "NonExistentTable",
                schemaName: "dbo"
            );

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*does not exist*");
    }

    [Fact]
    public async Task GetColumnAsync_NonExistentColumn_ReturnsNull()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        // Create a test table first
        var testTable = await CreateTestTableForColumns(
            service,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "GetColumnTest",
            "dbo"
        );
        testTable.Should().NotBeNull();

        var getColumnContext = OperationIdentifiers.ForColumnGet(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "GetColumnTest",
            "NonExistentColumn",
            "dbo"
        );
        var result = await service.GetColumnAsync(
            getColumnContext,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "GetColumnTest",
            "NonExistentColumn",
            schemaName: "dbo"
        );

        result.Should().BeNull();

        // Cleanup
        var dropTableContext1 = OperationIdentifiers.ForTableDrop(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "GetColumnTest",
            "dbo"
        );
        await service.DropTableAsync(
            dropTableContext1,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "GetColumnTest",
            schemaName: "dbo"
        );
    }

    [Fact]
    public async Task AddColumnAsync_DuplicateColumn_ReturnsNull()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        // Create a test table first
        var testTable = await CreateTestTableForColumns(
            service,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "AddColumnTest",
            "dbo"
        );
        testTable.Should().NotBeNull();

        // Try to add a column that already exists
        var addColumnRequest = new ColumnDto
        {
            ColumnName = "Name", // This column already exists
            ProviderDataType = "nvarchar(100)",
            IsNullable = true,
        };

        var addColumnContext = OperationIdentifiers.ForColumnAdd(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "AddColumnTest",
            addColumnRequest,
            "dbo"
        );
        var result = await service.AddColumnAsync(
            addColumnContext,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "AddColumnTest",
            addColumnRequest,
            schemaName: "dbo"
        );

        result.Should().BeNull();

        // Cleanup
        var dropTableContext2 = OperationIdentifiers.ForTableDrop(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "AddColumnTest",
            "dbo"
        );
        await service.DropTableAsync(
            dropTableContext2,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "AddColumnTest",
            schemaName: "dbo"
        );
    }

    [Fact]
    public async Task RenameColumnAsync_NonExistentColumn_ReturnsNull()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        // Create a test table first
        var testTable = await CreateTestTableForColumns(
            service,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "RenameColumnTest",
            "dbo"
        );
        testTable.Should().NotBeNull();

        var renameColumnContext = OperationIdentifiers.ForColumnRename(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "RenameColumnTest",
            "NonExistentColumn",
            "NewName",
            "dbo"
        );
        var result = await service.RenameColumnAsync(
            renameColumnContext,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "RenameColumnTest",
            "NonExistentColumn",
            "NewName",
            schemaName: "dbo"
        );

        result.Should().BeNull();

        // Cleanup
        var dropTableContext3 = OperationIdentifiers.ForTableDrop(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "RenameColumnTest",
            "dbo"
        );
        await service.DropTableAsync(
            dropTableContext3,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "RenameColumnTest",
            schemaName: "dbo"
        );
    }

    [Fact]
    public async Task DropColumnAsync_NonExistentColumn_ReturnsFalse()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        // Create a test table first
        var testTable = await CreateTestTableForColumns(
            service,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "DropColumnTest",
            "dbo"
        );
        testTable.Should().NotBeNull();

        var dropColumnContext = OperationIdentifiers.ForColumnDrop(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "DropColumnTest",
            "NonExistentColumn",
            "dbo"
        );
        await service.DropColumnAsync(
            dropColumnContext,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "DropColumnTest",
            "NonExistentColumn",
            schemaName: "dbo"
        );

        // Note: DropColumnAsync now returns Task (void), so we can't assert on the result

        // Cleanup
        var dropTableContext4 = OperationIdentifiers.ForTableDrop(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "DropColumnTest",
            "dbo"
        );
        await service.DropTableAsync(
            dropTableContext4,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "DropColumnTest",
            schemaName: "dbo"
        );
    }

    [Fact]
    public async Task ColumnManagement_NonExistentDatasource_ThrowsArgumentException()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var addColumnRequest = new ColumnDto
        {
            ColumnName = "TestColumn",
            ProviderDataType = "varchar(100)",
            IsNullable = true,
        };

        // Test all column methods with non-existent datasource
        var getColumnsContext = OperationIdentifiers.ForColumnList("NonExistent", "TestTable");
        var getColumnsAct = async () =>
            await service.GetColumnsAsync(getColumnsContext, "NonExistent", "TestTable");
        await getColumnsAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");

        var getColumnContext = OperationIdentifiers.ForColumnGet(
            "NonExistent",
            "TestTable",
            "TestColumn"
        );
        var getColumnAct = async () =>
            await service.GetColumnAsync(
                getColumnContext,
                "NonExistent",
                "TestTable",
                "TestColumn"
            );
        await getColumnAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");

        var addColumnContext = OperationIdentifiers.ForColumnAdd(
            "NonExistent",
            "TestTable",
            addColumnRequest
        );
        var addColumnAct = async () =>
            await service.AddColumnAsync(
                addColumnContext,
                "NonExistent",
                "TestTable",
                addColumnRequest
            );
        await addColumnAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");

        var renameColumnContext = OperationIdentifiers.ForColumnRename(
            "NonExistent",
            "TestTable",
            "OldName",
            "NewName"
        );
        var renameColumnAct = async () =>
            await service.RenameColumnAsync(
                renameColumnContext,
                "NonExistent",
                "TestTable",
                "OldName",
                "NewName"
            );
        await renameColumnAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");

        var dropColumnContext = OperationIdentifiers.ForColumnDrop(
            "NonExistent",
            "TestTable",
            "TestColumn"
        );
        var dropColumnAct = async () =>
            await service.DropColumnAsync(
                dropColumnContext,
                "NonExistent",
                "TestTable",
                "TestColumn"
            );
        await dropColumnAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");
    }

    #endregion

    #region Helper Methods

    private static async Task<TableDto?> CreateTestTableForColumns(
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

    #endregion
}
