// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using FluentAssertions;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Services;
using MJCZone.DapperMatic.AspNetCore.Tests.Factories;
using MJCZone.DapperMatic.AspNetCore.Validation;
using Xunit.Abstractions;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Services;

/// <summary>
/// Unit tests for DapperMatic service constraint operations.
/// </summary>
public partial class DapperMaticServiceTests
{
    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer, "dbo")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql, "public")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql, null)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite, null)]
    public async Task PrimaryKeyConstraint_Management_Tests(string datasourceId, string? schemaName)
    {
        using var factory = GetDefaultWebApplicationFactory();
        var service = GetDapperMaticService(factory);

        // Non-existent datasource throws NotFound
        await CheckInvalidDatasourceHandlingFetchingPrimaryKey(service, schemaName);

        // Non-existent table throws NotFound
        await CheckInvalidTableHandlingFetchingPrimaryKey(service, datasourceId, schemaName);

        // Create test table for primary key operations
        var tableName = "PKTest_" + Guid.NewGuid().ToString("N")[..8];
        await CreateTestTableWithoutPrimaryKey(service, datasourceId, tableName, schemaName);

        // Get Primary Key on table without PK returns null
        var pk = await service.GetPrimaryKeyConstraintAsync(
            context: OperationIdentifiers.ForPrimaryKeyGet(datasourceId, tableName, schemaName),
            datasourceId,
            tableName,
            schemaName
        );
        pk.Should().BeNull();

        // Add Primary Key
        var pkRequest = new PrimaryKeyConstraintDto
        {
            ConstraintName = $"PK_{tableName}_Id",
            ColumnNames = ["Id"],
        };
        var createdPk = await service.CreatePrimaryKeyConstraintAsync(
            context: OperationIdentifiers.ForPrimaryKeyCreate(
                datasourceId,
                tableName,
                pkRequest,
                schemaName
            ),
            datasourceId,
            tableName,
            pkRequest,
            schemaName
        );
        createdPk.Should().NotBeNull();
        createdPk!.ConstraintName.Should().BeEquivalentTo(pkRequest.ConstraintName);
        createdPk
            .ColumnNames.Should()
            .BeEquivalentTo(pkRequest.ColumnNames, (_) => _.IgnoringCase());

        // Verify Primary Key was added
        pk = await service.GetPrimaryKeyConstraintAsync(
            context: OperationIdentifiers.ForPrimaryKeyGet(datasourceId, tableName, schemaName),
            datasourceId,
            tableName,
            schemaName
        );
        pk.Should().NotBeNull();

        // Attempt to add duplicate Primary Key throws DuplicateKeyException
        var duplicatePkAct = async () =>
            await service.CreatePrimaryKeyConstraintAsync(
                context: OperationIdentifiers.ForPrimaryKeyCreate(
                    datasourceId,
                    tableName,
                    pkRequest,
                    schemaName
                ),
                datasourceId,
                tableName,
                pkRequest,
                schemaName
            );
        await duplicatePkAct.Should().ThrowAsync<DuplicateKeyException>();

        // Drop Primary Key
        await service.DropPrimaryKeyConstraintAsync(
            context: OperationIdentifiers.ForPrimaryKeyDrop(datasourceId, tableName, schemaName),
            datasourceId,
            tableName,
            schemaName
        );

        // Verify Primary Key was dropped
        pk = await service.GetPrimaryKeyConstraintAsync(
            context: OperationIdentifiers.ForPrimaryKeyGet(datasourceId, tableName, schemaName),
            datasourceId,
            tableName,
            schemaName
        );
        pk.Should().BeNull();

        // Create primary key with two columns, and without a constraint name (should auto-generate)
        var multiColPkRequest = new PrimaryKeyConstraintDto
        {
            ConstraintName = null,
            ColumnNames = ["Id", "Name"],
        };
        var multiColPk = await service.CreatePrimaryKeyConstraintAsync(
            context: OperationIdentifiers.ForPrimaryKeyCreate(
                datasourceId,
                tableName,
                multiColPkRequest,
                schemaName
            ),
            datasourceId,
            tableName,
            multiColPkRequest,
            schemaName
        );
        multiColPk.Should().NotBeNull();
        multiColPk!.ColumnNames.Should().HaveCount(2);

        // Cleanup: Drop the test table
        await service.DropTableAsync(
            context: OperationIdentifiers.ForTableDrop(datasourceId, tableName, schemaName),
            datasourceId,
            tableName,
            schemaName
        );
    }

    private async Task CreateTestTableWithoutPrimaryKey(
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
                    IsNullable = false,
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
        await service.CreateTableAsync(context, datasourceId, request);
    }

    private async Task CheckInvalidDatasourceHandlingFetchingPrimaryKey(
        IDapperMaticService service,
        string? schemaName
    )
    {
        var invalidDatasourceId = "NonExistent";
        var invalidContext = OperationIdentifiers.ForPrimaryKeyGet(
            invalidDatasourceId,
            "AnyTable",
            schemaName
        );
        var invalidAct = async () =>
            await service.GetPrimaryKeyConstraintAsync(
                invalidContext,
                invalidDatasourceId,
                "AnyTable",
                schemaName: schemaName
            );
        await invalidAct.Should().ThrowAsync<KeyNotFoundException>();
    }

    private async Task CheckInvalidTableHandlingFetchingPrimaryKey(
        IDapperMaticService service,
        string datasourceId,
        string? schemaName
    )
    {
        var invalidTableContext = OperationIdentifiers.ForPrimaryKeyGet(
            datasourceId,
            "NonExistentTable",
            schemaName
        );
        var invalidTableAct = async () =>
            await service.GetPrimaryKeyConstraintAsync(
                invalidTableContext,
                datasourceId,
                "NonExistentTable",
                schemaName: schemaName
            );
        await invalidTableAct.Should().ThrowAsync<KeyNotFoundException>();
    }
}
