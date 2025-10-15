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
    public async Task Column_Management_Tests(string datasourceId, string? schemaName)
    {
        using var factory = GetDefaultWebApplicationFactory();
        var service = GetDapperMaticService(factory);

        // Non-existent datasource throws NotFound
        await CheckInvalidDatasourceHandlingFetchingColumns(service, schemaName);

        // Non-existent table throws NotFound
        await CheckInvalidTableHandlingFetchingColumns(service, datasourceId, schemaName);

        // Create test table for column operations
        var tableName = "ColTest_" + Guid.NewGuid().ToString("N")[..8];
        var testTable = await CreateTestTableForColumns(
            service,
            datasourceId,
            tableName,
            schemaName
        );
        testTable.Should().NotBeNull();

        // Non-existent column throws NotFound
        await CheckInvalidColumnHandlingFetchingColumns(
            service,
            datasourceId,
            schemaName,
            tableName
        );

        // Retrieve columns (should have 3 initial columns)
        var listContext = OperationIdentifiers.ForColumnList(datasourceId, tableName, schemaName);
        var initialColumns = await service.GetColumnsAsync(
            listContext,
            datasourceId,
            tableName,
            schemaName
        );
        initialColumns.Should().NotBeNull();
        initialColumns.Should().HaveCount(3); // Id, Name, CreatedAt

        // Verify initial columns exist
        initialColumns
            .Should()
            .Contain(c => string.Equals(c.ColumnName, "Id", StringComparison.OrdinalIgnoreCase));
        initialColumns
            .Should()
            .Contain(c => string.Equals(c.ColumnName, "Name", StringComparison.OrdinalIgnoreCase));
        initialColumns
            .Should()
            .Contain(c =>
                string.Equals(c.ColumnName, "CreatedAt", StringComparison.OrdinalIgnoreCase)
            );

        // Verify single column exists
        var getContext = OperationIdentifiers.ForColumnGet(
            datasourceId,
            tableName,
            "Name",
            schemaName
        );
        var nameColumn = await service.GetColumnAsync(
            getContext,
            datasourceId,
            tableName,
            "Name",
            schemaName
        );
        nameColumn.Should().NotBeNull();
        nameColumn!.ColumnName.Should().BeEquivalentTo("Name");

        // Add test column
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
            tableName,
            addColumnRequest,
            schemaName
        );
        var addedColumn = await service.AddColumnAsync(
            addContext,
            datasourceId,
            tableName,
            addColumnRequest,
            schemaName
        );
        addedColumn.Should().NotBeNull();
        addedColumn!.ColumnName.Should().BeEquivalentTo("Description");

        // Verify column added
        var columnsAfterAdd = await service.GetColumnsAsync(
            listContext,
            datasourceId,
            tableName,
            schemaName
        );
        columnsAfterAdd.Should().HaveCount(4);
        columnsAfterAdd
            .Should()
            .Contain(c =>
                string.Equals(c.ColumnName, "Description", StringComparison.OrdinalIgnoreCase)
            );

        // Attempt to add duplicate column (should fail)
        var duplicateAct = async () =>
            await service.AddColumnAsync(
                addContext,
                datasourceId,
                tableName,
                new ColumnDto
                {
                    ColumnName = "Name", // Already exists
                    ProviderDataType = "varchar(100)",
                    IsNullable = true,
                },
                schemaName
            );
        await duplicateAct.Should().ThrowAsync<DuplicateKeyException>();

        // Rename column
        var renameContext = OperationIdentifiers.ForColumnRename(
            datasourceId,
            tableName,
            "Description",
            "LongDescription",
            schemaName
        );
        var renamedColumn = await service.RenameColumnAsync(
            renameContext,
            datasourceId,
            tableName,
            "Description",
            "LongDescription",
            schemaName
        );
        renamedColumn.Should().NotBeNull();
        renamedColumn!.ColumnName.Should().BeEquivalentTo("LongDescription");

        // Verify column renamed
        var columnsAfterRename = await service.GetColumnsAsync(
            listContext,
            datasourceId,
            tableName,
            schemaName
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

        // Get the renamed column
        var getRenamedContext = OperationIdentifiers.ForColumnGet(
            datasourceId,
            tableName,
            "LongDescription",
            schemaName
        );
        var renamedColumnResult = await service.GetColumnAsync(
            getRenamedContext,
            datasourceId,
            tableName,
            "LongDescription",
            schemaName
        );
        renamedColumnResult.Should().NotBeNull();
        renamedColumnResult!.ColumnName.Should().BeEquivalentTo("LongDescription");

        // Attempt to rename column to existing name (should fail)
        var duplicateRenameAct = async () =>
            await service.RenameColumnAsync(
                renameContext,
                datasourceId,
                tableName,
                "LongDescription",
                "Name", // Already exists
                schemaName
            );
        await duplicateRenameAct.Should().ThrowAsync<DuplicateKeyException>();

        // Drop column
        var dropContext = OperationIdentifiers.ForColumnDrop(
            datasourceId,
            tableName,
            "LongDescription",
            schemaName
        );
        await service.DropColumnAsync(
            dropContext,
            datasourceId,
            tableName,
            "LongDescription",
            schemaName
        );

        // Verify column dropped using GetColumns
        var columnsAfterDrop = await service.GetColumnsAsync(
            listContext,
            datasourceId,
            tableName,
            schemaName
        );
        columnsAfterDrop.Should().HaveCount(3);
        columnsAfterDrop
            .Should()
            .NotContain(c =>
                string.Equals(c.ColumnName, "LongDescription", StringComparison.OrdinalIgnoreCase)
            );

        // Verify column dropped using GetColumn (should throw)
        var getDroppedAct = async () =>
            await service.GetColumnAsync(
                getRenamedContext,
                datasourceId,
                tableName,
                "LongDescription",
                schemaName
            );
        await getDroppedAct.Should().ThrowAsync<KeyNotFoundException>();

        // Cleanup - drop test table
        await service.DropTableAsync(
            OperationIdentifiers.ForTableDrop(datasourceId, tableName, schemaName),
            datasourceId,
            tableName,
            schemaName
        );
    }

    private async Task CheckInvalidDatasourceHandlingFetchingColumns(
        IDapperMaticService service,
        string? schemaName
    )
    {
        var invalidDatasourceId = "NonExistent";
        var invalidContext = OperationIdentifiers.ForColumnList(
            invalidDatasourceId,
            "AnyTable",
            schemaName
        );
        var invalidAct = async () =>
            await service.GetColumnsAsync(
                invalidContext,
                invalidDatasourceId,
                "AnyTable",
                schemaName
            );
        await invalidAct.Should().ThrowAsync<KeyNotFoundException>();
    }

    private async Task CheckInvalidTableHandlingFetchingColumns(
        IDapperMaticService service,
        string datasourceId,
        string? schemaName
    )
    {
        var invalidTableContext = OperationIdentifiers.ForColumnList(
            datasourceId,
            "NonExistentTable",
            schemaName
        );
        var invalidTableAct = async () =>
            await service.GetColumnsAsync(
                invalidTableContext,
                datasourceId,
                "NonExistentTable",
                schemaName
            );
        await invalidTableAct.Should().ThrowAsync<KeyNotFoundException>();
    }

    private async Task CheckInvalidColumnHandlingFetchingColumns(
        IDapperMaticService service,
        string datasourceId,
        string? schemaName,
        string tableName
    )
    {
        var invalidColumnContext = OperationIdentifiers.ForColumnGet(
            datasourceId,
            tableName,
            "NonExistentColumn",
            schemaName
        );
        var invalidColumnAct = async () =>
            await service.GetColumnAsync(
                invalidColumnContext,
                datasourceId,
                tableName,
                "NonExistentColumn",
                schemaName
            );
        await invalidColumnAct.Should().ThrowAsync<KeyNotFoundException>();
    }

    private async Task<TableDto?> CreateTestTableForColumns(
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
}
