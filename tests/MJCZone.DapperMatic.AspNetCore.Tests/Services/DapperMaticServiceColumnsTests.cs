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
        var testTable = await CreateTestTableForColumns(service, datasourceId, "ColumnTest", schemaName);
        testTable.Should().NotBeNull();

        // Step 2: Get all columns for the table
        var initialColumns = await service.GetColumnsAsync(datasourceId, "ColumnTest", schemaName: schemaName);
        initialColumns.Should().NotBeNull();
        var initialColumnsList = initialColumns.ToList();
        initialColumnsList.Should().HaveCount(3); // Id, Name, CreatedAt

        // Verify initial columns exist (case insensitive for PostgreSQL)
        initialColumnsList.Should().Contain(c =>
            string.Equals(c.ColumnName, "Id", StringComparison.OrdinalIgnoreCase));
        initialColumnsList.Should().Contain(c =>
            string.Equals(c.ColumnName, "Name", StringComparison.OrdinalIgnoreCase));
        initialColumnsList.Should().Contain(c =>
            string.Equals(c.ColumnName, "CreatedAt", StringComparison.OrdinalIgnoreCase));

        // Step 3: Get a specific column
        var nameColumn = await service.GetColumnAsync(datasourceId, "ColumnTest", "Name", schemaName: schemaName);
        nameColumn.Should().NotBeNull();
        string.Equals(nameColumn!.ColumnName, "Name", StringComparison.OrdinalIgnoreCase).Should().BeTrue();

        // Step 4: Add a new column
        var addColumnRequest = new CreateTableColumnRequest
        {
            ColumnName = "Description",
            ProviderDataType = datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                ? "nvarchar(500)"
                : "varchar(500)",
            IsNullable = true
        };

        var addedColumn = await service.AddColumnAsync(
            datasourceId,
            "ColumnTest",
            addColumnRequest,
            schemaName: schemaName
        );
        addedColumn.Should().NotBeNull();
        string.Equals(addedColumn!.ColumnName, "Description", StringComparison.OrdinalIgnoreCase).Should().BeTrue();

        // Step 5: Verify column was added - should now have 4 columns
        var columnsAfterAdd = await service.GetColumnsAsync(datasourceId, "ColumnTest", schemaName: schemaName);
        columnsAfterAdd.Should().HaveCount(4);
        columnsAfterAdd.Should().Contain(c =>
            string.Equals(c.ColumnName, "Description", StringComparison.OrdinalIgnoreCase));

        // Step 6: Rename the newly added column
        var renamedColumn = await service.RenameColumnAsync(
            datasourceId,
            "ColumnTest",
            "Description",
            "LongDescription",
            schemaName: schemaName
        );
        renamedColumn.Should().NotBeNull();
        string.Equals(renamedColumn!.ColumnName, "LongDescription", StringComparison.OrdinalIgnoreCase).Should().BeTrue();

        // Step 7: Verify rename - old name shouldn't exist, new name should exist
        var columnsAfterRename = await service.GetColumnsAsync(datasourceId, "ColumnTest", schemaName: schemaName);
        columnsAfterRename.Should().HaveCount(4);
        columnsAfterRename.Should().NotContain(c =>
            string.Equals(c.ColumnName, "Description", StringComparison.OrdinalIgnoreCase));
        columnsAfterRename.Should().Contain(c =>
            string.Equals(c.ColumnName, "LongDescription", StringComparison.OrdinalIgnoreCase));

        // Step 8: Get the renamed column by its new name
        var renamedColumnResult = await service.GetColumnAsync(
            datasourceId,
            "ColumnTest",
            "LongDescription",
            schemaName: schemaName
        );
        renamedColumnResult.Should().NotBeNull();
        string.Equals(renamedColumnResult!.ColumnName, "LongDescription", StringComparison.OrdinalIgnoreCase).Should().BeTrue();

        // Step 9: Drop the renamed column
        var dropResult = await service.DropColumnAsync(
            datasourceId,
            "ColumnTest",
            "LongDescription",
            schemaName: schemaName
        );
        dropResult.Should().BeTrue();

        // Step 10: Verify final state - should be back to 3 columns
        var finalColumns = await service.GetColumnsAsync(datasourceId, "ColumnTest", schemaName: schemaName);
        finalColumns.Should().HaveCount(3);
        finalColumns.Should().NotContain(c =>
            string.Equals(c.ColumnName, "LongDescription", StringComparison.OrdinalIgnoreCase));
        finalColumns.Should().NotContain(c =>
            string.Equals(c.ColumnName, "Description", StringComparison.OrdinalIgnoreCase));

        // Verify original columns still exist
        finalColumns.Should().Contain(c =>
            string.Equals(c.ColumnName, "Id", StringComparison.OrdinalIgnoreCase));
        finalColumns.Should().Contain(c =>
            string.Equals(c.ColumnName, "Name", StringComparison.OrdinalIgnoreCase));
        finalColumns.Should().Contain(c =>
            string.Equals(c.ColumnName, "CreatedAt", StringComparison.OrdinalIgnoreCase));

        // Cleanup: Drop the test table
        await service.DropTableAsync(datasourceId, "ColumnTest", schemaName: schemaName);
    }

    #endregion

    #region Error Scenario Tests

    [Fact]
    public async Task GetColumnsAsync_NonExistentTable_ThrowsArgumentException()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var act = async () => await service.GetColumnsAsync(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "NonExistentTable",
            schemaName: "dbo"
        );

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*does not exist*");
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

        var result = await service.GetColumnAsync(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "GetColumnTest",
            "NonExistentColumn",
            schemaName: "dbo"
        );

        result.Should().BeNull();

        // Cleanup
        await service.DropTableAsync(
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
        var addColumnRequest = new CreateTableColumnRequest
        {
            ColumnName = "Name", // This column already exists
            ProviderDataType = "nvarchar(100)",
            IsNullable = true
        };

        var result = await service.AddColumnAsync(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "AddColumnTest",
            addColumnRequest,
            schemaName: "dbo"
        );

        result.Should().BeNull();

        // Cleanup
        await service.DropTableAsync(
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

        var result = await service.RenameColumnAsync(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "RenameColumnTest",
            "NonExistentColumn",
            "NewName",
            schemaName: "dbo"
        );

        result.Should().BeNull();

        // Cleanup
        await service.DropTableAsync(
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

        var result = await service.DropColumnAsync(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "DropColumnTest",
            "NonExistentColumn",
            schemaName: "dbo"
        );

        result.Should().BeFalse();

        // Cleanup
        await service.DropTableAsync(
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

        var addColumnRequest = new CreateTableColumnRequest
        {
            ColumnName = "TestColumn",
            ProviderDataType = "varchar(100)",
            IsNullable = true
        };

        // Test all column methods with non-existent datasource
        var getColumnsAct = async () => await service.GetColumnsAsync("NonExistent", "TestTable");
        await getColumnsAct.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");

        var getColumnAct = async () => await service.GetColumnAsync("NonExistent", "TestTable", "TestColumn");
        await getColumnAct.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");

        var addColumnAct = async () => await service.AddColumnAsync("NonExistent", "TestTable", addColumnRequest);
        await addColumnAct.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");

        var renameColumnAct = async () => await service.RenameColumnAsync("NonExistent", "TestTable", "OldName", "NewName");
        await renameColumnAct.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");

        var dropColumnAct = async () => await service.DropColumnAsync("NonExistent", "TestTable", "TestColumn");
        await dropColumnAct.Should().ThrowAsync<ArgumentException>()
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
                    ProviderDataType = datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                        ? "nvarchar(255)"
                        : "varchar(255)",
                    IsNullable = true,
                },
                new CreateTableColumnRequest
                {
                    ColumnName = "CreatedAt",
                    ProviderDataType = datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                        ? "datetime2"
                        : datasourceId == TestcontainersAssemblyFixture.DatasourceId_PostgreSql
                            ? "timestamp"
                            : "datetime",
                    IsNullable = false,
                },
            ],
        };

        return await service.CreateTableAsync(datasourceId, request);
    }

    #endregion
}