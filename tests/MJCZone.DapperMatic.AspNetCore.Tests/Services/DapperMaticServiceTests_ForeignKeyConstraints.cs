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
    public async Task ForeignKeyConstraint_Management_Tests(string datasourceId, string? schemaName)
    {
        using var factory = GetDefaultWebApplicationFactory();
        var service = GetDapperMaticService(factory);

        // Non-existent datasource throws NotFound
        await CheckInvalidDatasourceHandlingFetchingForeignKeys(service, schemaName);

        // Non-existent table throws NotFound
        await CheckInvalidTableHandlingFetchingForeignKeys(service, datasourceId, schemaName);

        // Create test tables for foreign key operations
        var parentTableName = "FKParent_" + Guid.NewGuid().ToString("N")[..8];
        var childTableName = "FKChild_" + Guid.NewGuid().ToString("N")[..8];

        await CreateParentTableForForeignKey(service, datasourceId, parentTableName, schemaName);
        await CreateChildTableForForeignKey(service, datasourceId, childTableName, schemaName);

        // Get Foreign Keys on table without foreign keys returns empty list
        var foreignKeys = await service.GetForeignKeyConstraintsAsync(
            context: OperationIdentifiers.ForForeignKeyList(
                datasourceId,
                childTableName,
                schemaName
            ),
            datasourceId,
            childTableName,
            schemaName
        );
        foreignKeys.Should().NotBeNull();
        foreignKeys.Should().BeEmpty();

        // Add Foreign Key Constraint
        var foreignKeyName = "FK_" + Guid.NewGuid().ToString("N")[..8];
        var foreignKeyRequest = new ForeignKeyConstraintDto
        {
            ConstraintName = foreignKeyName,
            ColumnNames = ["ParentId"],
            ReferencedTableName = parentTableName,
            ReferencedColumnNames = ["Id"],
            OnUpdate = "NoAction",
            OnDelete = "Cascade",
        };
        var fkCreateContext = OperationIdentifiers.ForForeignKeyCreate(
            datasourceId,
            childTableName,
            foreignKeyRequest,
            schemaName
        );
        var addFkResult = await service.CreateForeignKeyConstraintAsync(
            context: fkCreateContext,
            datasourceId,
            childTableName,
            foreignKeyRequest,
            schemaName
        );
        addFkResult.Should().NotBeNull();
        addFkResult.ConstraintName.Should().BeEquivalentTo(foreignKeyName);
        addFkResult
            .ColumnNames.Should()
            .BeEquivalentTo(foreignKeyRequest.ColumnNames, (_) => _.IgnoringCase());
        addFkResult.ReferencedTableName.Should().BeEquivalentTo(parentTableName);
        addFkResult
            .ReferencedColumnNames.Should()
            .BeEquivalentTo(foreignKeyRequest.ReferencedColumnNames, (_) => _.IgnoringCase());

        // Verify Foreign Key was added
        foreignKeys = await service.GetForeignKeyConstraintsAsync(
            context: OperationIdentifiers.ForForeignKeyList(
                datasourceId,
                childTableName,
                schemaName
            ),
            datasourceId,
            childTableName,
            schemaName
        );
        foreignKeys.Should().NotBeNull();
        foreignKeys.Should().HaveCount(1);

        // Verify single Foreign Key details
        var fk = await service.GetForeignKeyConstraintAsync(
            context: OperationIdentifiers.ForForeignKeyGet(
                datasourceId,
                childTableName,
                foreignKeyName,
                schemaName
            ),
            datasourceId,
            childTableName,
            foreignKeyName,
            schemaName
        );
        fk.Should().NotBeNull();
        fk!.ConstraintName.Should().BeEquivalentTo(foreignKeyName);
        fk.ReferencedTableName.Should().BeEquivalentTo(parentTableName);

        // Attempt to add duplicate Foreign Key throws Duplicate exception
        var duplicateFkAct = async () =>
            await service.CreateForeignKeyConstraintAsync(
                context: fkCreateContext,
                datasourceId,
                childTableName,
                foreignKeyRequest,
                schemaName
            );
        await duplicateFkAct.Should().ThrowAsync<DuplicateKeyException>();

        // Drop Foreign Key
        var dropContext = OperationIdentifiers.ForForeignKeyDrop(
            datasourceId,
            childTableName,
            foreignKeyName,
            schemaName
        );
        await service.DropForeignKeyConstraintAsync(
            context: dropContext,
            datasourceId,
            childTableName,
            foreignKeyName,
            schemaName
        );

        // Verify Foreign Key was dropped
        foreignKeys = await service.GetForeignKeyConstraintsAsync(
            context: OperationIdentifiers.ForForeignKeyList(
                datasourceId,
                childTableName,
                schemaName
            ),
            datasourceId,
            childTableName,
            schemaName
        );
        foreignKeys.Should().NotBeNull();
        foreignKeys.Should().BeEmpty();

        // Then using the Get single method
        var invalidFkAct = async () =>
            await service.GetForeignKeyConstraintAsync(
                context: OperationIdentifiers.ForForeignKeyGet(
                    datasourceId,
                    childTableName,
                    foreignKeyName,
                    schemaName
                ),
                datasourceId,
                childTableName,
                foreignKeyName,
                schemaName
            );
        await invalidFkAct.Should().ThrowAsync<KeyNotFoundException>();

        // Drop the test tables (child first due to foreign key dependency)
        await service.DropTableAsync(
            context: OperationIdentifiers.ForTableDrop(datasourceId, childTableName, schemaName),
            datasourceId,
            childTableName,
            schemaName
        );
        await service.DropTableAsync(
            context: OperationIdentifiers.ForTableDrop(datasourceId, parentTableName, schemaName),
            datasourceId,
            parentTableName,
            schemaName
        );
    }

    private async Task CheckInvalidDatasourceHandlingFetchingForeignKeys(
        IDapperMaticService service,
        string? schemaName
    )
    {
        var invalidDatasourceId = "NonExistent";
        var invalidContext = OperationIdentifiers.ForForeignKeyList(
            invalidDatasourceId,
            "AnyTable",
            schemaName
        );
        var invalidAct = async () =>
            await service.GetForeignKeyConstraintsAsync(
                invalidContext,
                invalidDatasourceId,
                "AnyTable",
                schemaName: schemaName
            );
        await invalidAct.Should().ThrowAsync<KeyNotFoundException>();
    }

    private async Task CheckInvalidTableHandlingFetchingForeignKeys(
        IDapperMaticService service,
        string datasourceId,
        string? schemaName
    )
    {
        var invalidTableContext = OperationIdentifiers.ForForeignKeyList(
            datasourceId,
            "NonExistentTable",
            schemaName
        );
        var invalidTableAct = async () =>
            await service.GetForeignKeyConstraintsAsync(
                invalidTableContext,
                datasourceId,
                "NonExistentTable",
                schemaName: schemaName
            );
        await invalidTableAct.Should().ThrowAsync<KeyNotFoundException>();
    }

    private async Task CreateParentTableForForeignKey(
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
            Columns = new List<ColumnDto>
            {
                new()
                {
                    ColumnName = "Id",
                    ProviderDataType = "int",
                    IsNullable = false,
                    IsPrimaryKey = true,
                },
                new()
                {
                    ColumnName = "Name",
                    ProviderDataType =
                        datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                            ? "nvarchar(255)"
                            : "varchar(255)",
                    IsNullable = false,
                },
                new()
                {
                    ColumnName = "Description",
                    ProviderDataType =
                        datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                            ? "nvarchar(500)"
                            : "varchar(500)",
                    IsNullable = true,
                },
            },
            PrimaryKeyConstraint = new PrimaryKeyConstraintDto
            {
                ConstraintName = $"PK_{tableName}",
                ColumnNames = ["Id"],
            },
        };

        var context = OperationIdentifiers.ForTableCreate(datasourceId, request);
        await service.CreateTableAsync(context, datasourceId, request);
    }

    private async Task CreateChildTableForForeignKey(
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
            Columns = new List<ColumnDto>
            {
                new()
                {
                    ColumnName = "Id",
                    ProviderDataType = "int",
                    IsNullable = false,
                    IsPrimaryKey = true,
                },
                new()
                {
                    ColumnName = "ParentId",
                    ProviderDataType = "int",
                    IsNullable = false,
                },
                new()
                {
                    ColumnName = "ChildName",
                    ProviderDataType =
                        datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                            ? "nvarchar(255)"
                            : "varchar(255)",
                    IsNullable = false,
                },
                new()
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
            },
            PrimaryKeyConstraint = new PrimaryKeyConstraintDto
            {
                ConstraintName = $"PK_{tableName}",
                ColumnNames = ["Id"],
            },
        };

        var context = OperationIdentifiers.ForTableCreate(datasourceId, request);
        await service.CreateTableAsync(context, datasourceId, request);
    }
}