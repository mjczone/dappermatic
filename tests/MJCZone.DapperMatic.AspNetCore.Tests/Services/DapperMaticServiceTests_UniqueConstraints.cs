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
    public async Task Can_manage_unique_constraint_Async(string datasourceId, string? schemaName)
    {
        using var factory = GetDefaultWebApplicationFactory();
        var service = GetDapperMaticService(factory);

        // Non-existent datasource throws NotFound
        await CheckInvalidDatasourceHandlingFetchingUniqueConstraints(service, schemaName);

        // Non-existent table throws NotFound
        await CheckInvalidTableHandlingFetchingUniqueConstraints(service, datasourceId, schemaName);

        // Create test table for unique constraint operations
        var tableName = "UCTest_" + Guid.NewGuid().ToString("N")[..8];
        await CreateTestTableWithoutUniqueConstraint(service, datasourceId, tableName, schemaName);

        // Get Unique Constraints on table without unique constraints returns empty list
        var uniqueConstraints = await service.GetUniqueConstraintsAsync(
            context: OperationIdentifiers.ForUniqueConstraintList(datasourceId, tableName, schemaName),
            datasourceId,
            tableName,
            schemaName
        );
        uniqueConstraints.Should().NotBeNull();
        uniqueConstraints.Should().BeEmpty();

        // Add Unique Constraint
        var uniqueConstraintName = "UQ_" + Guid.NewGuid().ToString("N")[..8];
        var uniqueConstraintRequest = new UniqueConstraintDto
        {
            ConstraintName = uniqueConstraintName,
            ColumnNames = ["Email"],
        };
        var uniqueCreateContext = OperationIdentifiers.ForUniqueConstraintCreate(
            datasourceId,
            tableName,
            uniqueConstraintRequest,
            schemaName
        );
        var addUcResult = await service.CreateUniqueConstraintAsync(
            context: uniqueCreateContext,
            datasourceId,
            tableName,
            uniqueConstraintRequest,
            schemaName
        );
        addUcResult.Should().NotBeNull();
        addUcResult.ConstraintName.Should().BeEquivalentTo(uniqueConstraintName);
        addUcResult.ColumnNames.Should().BeEquivalentTo(uniqueConstraintRequest.ColumnNames, (_) => _.IgnoringCase());

        // Verify Unique Constraint was added
        uniqueConstraints = await service.GetUniqueConstraintsAsync(
            context: OperationIdentifiers.ForUniqueConstraintList(datasourceId, tableName, schemaName),
            datasourceId,
            tableName,
            schemaName
        );
        uniqueConstraints.Should().NotBeNull();
        uniqueConstraints.Should().HaveCount(1);

        // Verify single Unique Constraint details
        var uc = await service.GetUniqueConstraintAsync(
            context: OperationIdentifiers.ForUniqueConstraintGet(
                datasourceId,
                tableName,
                uniqueConstraintName,
                schemaName
            ),
            datasourceId,
            tableName,
            uniqueConstraintName,
            schemaName
        );
        uc.Should().NotBeNull();
        uc!.ConstraintName.Should().BeEquivalentTo(uniqueConstraintName);

        // Attempt to add duplicate Unique Constraint throws Duplicate exception
        var duplicateUcAct = async () =>
            await service.CreateUniqueConstraintAsync(
                context: uniqueCreateContext,
                datasourceId,
                tableName,
                uniqueConstraintRequest,
                schemaName
            );
        await duplicateUcAct.Should().ThrowAsync<DuplicateKeyException>();

        // Drop Unique Constraint
        var dropContext = OperationIdentifiers.ForUniqueConstraintDrop(
            datasourceId,
            tableName,
            uniqueConstraintName,
            schemaName
        );
        await service.DropUniqueConstraintAsync(
            context: dropContext,
            datasourceId,
            tableName,
            uniqueConstraintName,
            schemaName
        );

        // Verify Unique Constraint was dropped
        uniqueConstraints = await service.GetUniqueConstraintsAsync(
            context: OperationIdentifiers.ForUniqueConstraintList(datasourceId, tableName, schemaName),
            datasourceId,
            tableName,
            schemaName
        );
        uniqueConstraints.Should().NotBeNull();
        uniqueConstraints.Should().BeEmpty();

        // Then using the Get single method
        var invalidUcAct = async () =>
            await service.GetUniqueConstraintAsync(
                context: OperationIdentifiers.ForUniqueConstraintGet(
                    datasourceId,
                    tableName,
                    uniqueConstraintName,
                    schemaName
                ),
                datasourceId,
                tableName,
                uniqueConstraintName,
                schemaName
            );
        await invalidUcAct.Should().ThrowAsync<KeyNotFoundException>();

        // Drop the test table
        await service.DropTableAsync(
            context: OperationIdentifiers.ForTableDrop(datasourceId, tableName, schemaName),
            datasourceId,
            tableName,
            schemaName
        );
    }

    private async Task CheckInvalidDatasourceHandlingFetchingUniqueConstraints(
        IDapperMaticService service,
        string? schemaName
    )
    {
        var invalidDatasourceId = "NonExistent";
        var invalidContext = OperationIdentifiers.ForUniqueConstraintGet(
            invalidDatasourceId,
            "AnyTable",
            "AnyConstraint",
            schemaName
        );
        var invalidAct = async () =>
            await service.GetUniqueConstraintsAsync(
                invalidContext,
                invalidDatasourceId,
                "AnyTable",
                schemaName: schemaName
            );
        await invalidAct.Should().ThrowAsync<KeyNotFoundException>();
    }

    private async Task CheckInvalidTableHandlingFetchingUniqueConstraints(
        IDapperMaticService service,
        string datasourceId,
        string? schemaName
    )
    {
        var invalidTableName = "NonExistent";
        var invalidContext = OperationIdentifiers.ForUniqueConstraintGet(
            datasourceId,
            invalidTableName,
            "AnyConstraint",
            schemaName
        );
        var invalidAct = async () =>
            await service.GetUniqueConstraintsAsync(
                invalidContext,
                datasourceId,
                invalidTableName,
                schemaName: schemaName
            );
        await invalidAct.Should().ThrowAsync<KeyNotFoundException>();
    }

    private async Task CreateTestTableWithoutUniqueConstraint(
        IDapperMaticService service,
        string datasourceId,
        string tableName,
        string? schemaName
    )
    {
        var request = new TableDto
        {
            TableName = tableName,
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
                    ColumnName = "Email",
                    ProviderDataType =
                        datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                            ? "nvarchar(255)"
                            : "varchar(255)",
                    IsNullable = false,
                },
                new()
                {
                    ColumnName = "Name",
                    ProviderDataType =
                        datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                            ? "nvarchar(255)"
                            : "varchar(255)",
                    IsNullable = true,
                },
                new()
                {
                    ColumnName = "CreatedAt",
                    ProviderDataType =
                        datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer ? "datetime2"
                        : datasourceId == TestcontainersAssemblyFixture.DatasourceId_PostgreSql ? "timestamp"
                        : "datetime",
                    IsNullable = false,
                },
            },
            UniqueConstraints = null,
            Indexes = null,
        };

        var context = OperationIdentifiers.ForTableCreate(datasourceId, request);
        await service.CreateTableAsync(context, datasourceId, request);
    }
}
