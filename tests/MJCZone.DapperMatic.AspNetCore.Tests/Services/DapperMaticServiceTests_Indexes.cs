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
    public async Task Index_Management_Tests(string datasourceId, string? schemaName)
    {
        using var factory = GetDefaultWebApplicationFactory();
        var service = GetDapperMaticService(factory);

        // Non-existent datasource throws NotFound
        await CheckInvalidDatasourceHandlingFetchingIndexes(service, schemaName);

        // Non-existent table throws NotFound
        await CheckInvalidTableHandlingFetchingIndexes(service, datasourceId, schemaName);

        // Create test table for index operations
        var tableName = "IdxTest_" + Guid.NewGuid().ToString("N")[..8];
        await CreateTestTableForIndexes(service, datasourceId, tableName, schemaName);

        // Retrieve indexes (may include auto-created indexes like PK)
        var listContext = OperationIdentifiers.ForIndexList(datasourceId, tableName, schemaName);
        var initialIndexes = await service.GetIndexesAsync(
            listContext,
            datasourceId,
            tableName,
            schemaName
        );
        initialIndexes.Should().NotBeNull();
        var initialIndexCount = initialIndexes.Count();

        // Add test indexes - simple non-unique index
        var simpleIndexName = "IX_" + Guid.NewGuid().ToString("N")[..8];
        var simpleIndexRequest = new IndexDto
        {
            IndexName = simpleIndexName,
            ColumnNames = ["Name"],
            IsUnique = false,
        };
        var createSimpleContext = OperationIdentifiers.ForIndexCreate(
            datasourceId,
            tableName,
            simpleIndexRequest,
            schemaName
        );
        var simpleIndex = await service.CreateIndexAsync(
            createSimpleContext,
            datasourceId,
            tableName,
            simpleIndexRequest,
            schemaName
        );
        simpleIndex.Should().NotBeNull();
        simpleIndex!.IndexName.Should().BeEquivalentTo(simpleIndexName);
        simpleIndex.IsUnique.Should().BeFalse();

        // Add unique index
        var uniqueIndexName = "IX_Unique_" + Guid.NewGuid().ToString("N")[..8];
        var uniqueIndexRequest = new IndexDto
        {
            IndexName = uniqueIndexName,
            ColumnNames = ["Email"],
            IsUnique = true,
        };
        var createUniqueContext = OperationIdentifiers.ForIndexCreate(
            datasourceId,
            tableName,
            uniqueIndexRequest,
            schemaName
        );
        var uniqueIndex = await service.CreateIndexAsync(
            createUniqueContext,
            datasourceId,
            tableName,
            uniqueIndexRequest,
            schemaName
        );
        uniqueIndex.Should().NotBeNull();
        uniqueIndex!.IndexName.Should().BeEquivalentTo(uniqueIndexName);
        uniqueIndex.IsUnique.Should().BeTrue();

        // Add composite index
        var compositeIndexName = "IX_Composite_" + Guid.NewGuid().ToString("N")[..8];
        var compositeIndexRequest = new IndexDto
        {
            IndexName = compositeIndexName,
            ColumnNames = ["Name", "Email"],
            IsUnique = false,
        };
        var createCompositeContext = OperationIdentifiers.ForIndexCreate(
            datasourceId,
            tableName,
            compositeIndexRequest,
            schemaName
        );
        var compositeIndex = await service.CreateIndexAsync(
            createCompositeContext,
            datasourceId,
            tableName,
            compositeIndexRequest,
            schemaName
        );
        compositeIndex.Should().NotBeNull();
        compositeIndex!.IndexName.Should().BeEquivalentTo(compositeIndexName);

        // Verify indexes added
        var indexesAfterCreation = await service.GetIndexesAsync(
            listContext,
            datasourceId,
            tableName,
            schemaName
        );
        indexesAfterCreation.Should().HaveCount(initialIndexCount + 3);

        // Verify single index exists
        var getIndexContext = OperationIdentifiers.ForIndexGet(
            datasourceId,
            tableName,
            uniqueIndexName,
            schemaName
        );
        var retrievedIndex = await service.GetIndexAsync(
            getIndexContext,
            datasourceId,
            tableName,
            uniqueIndexName,
            schemaName
        );
        retrievedIndex.Should().NotBeNull();
        retrievedIndex!.IndexName.Should().BeEquivalentTo(uniqueIndexName);
        retrievedIndex.IsUnique.Should().BeTrue();

        // Attempt to add duplicate index, throws DuplicateKeyException
        Func<Task> act = async () =>
            await service.CreateIndexAsync(
                createSimpleContext,
                datasourceId,
                tableName,
                simpleIndexRequest,
                schemaName
            );
        await act.Should().ThrowAsync<DuplicateKeyException>();

        // Drop simple index
        var dropSimpleContext = OperationIdentifiers.ForIndexDrop(
            datasourceId,
            tableName,
            simpleIndexName,
            schemaName
        );
        await service.DropIndexAsync(
            dropSimpleContext,
            datasourceId,
            tableName,
            simpleIndexName,
            schemaName
        );

        // Verify index dropped using GetIndexes
        var indexesAfterDrop = await service.GetIndexesAsync(
            listContext,
            datasourceId,
            tableName,
            schemaName
        );
        indexesAfterDrop.Should().HaveCount(initialIndexCount + 2);
        indexesAfterDrop
            .Should()
            .NotContain(i =>
                string.Equals(i.IndexName, simpleIndexName, StringComparison.OrdinalIgnoreCase)
            );

        // Verify using GetIndex, throws KeyNotFoundException
        act = async () =>
            await service.GetIndexAsync(
                OperationIdentifiers.ForIndexGet(
                    datasourceId,
                    tableName,
                    simpleIndexName,
                    schemaName
                ),
                datasourceId,
                tableName,
                simpleIndexName,
                schemaName
            );
        await act.Should().ThrowAsync<KeyNotFoundException>();

        // Cleanup - drop test table
        await service.DropTableAsync(
            OperationIdentifiers.ForTableDrop(datasourceId, tableName, schemaName),
            datasourceId,
            tableName,
            schemaName
        );
    }

    private async Task CheckInvalidDatasourceHandlingFetchingIndexes(
        IDapperMaticService service,
        string? schemaName
    )
    {
        var invalidDatasourceId = "NonExistent";
        var invalidContext = OperationIdentifiers.ForIndexList(
            invalidDatasourceId,
            "AnyTable",
            schemaName
        );
        var invalidAct = async () =>
            await service.GetIndexesAsync(
                invalidContext,
                invalidDatasourceId,
                "AnyTable",
                schemaName
            );
        await invalidAct.Should().ThrowAsync<KeyNotFoundException>();
    }

    private async Task CheckInvalidTableHandlingFetchingIndexes(
        IDapperMaticService service,
        string datasourceId,
        string? schemaName
    )
    {
        var invalidTableName = "NonExistent";
        var invalidContext = OperationIdentifiers.ForIndexList(
            datasourceId,
            invalidTableName,
            schemaName
        );
        var invalidAct = async () =>
            await service.GetIndexesAsync(
                invalidContext,
                datasourceId,
                invalidTableName,
                schemaName
            );
        await invalidAct.Should().ThrowAsync<KeyNotFoundException>();
    }

    private async Task CreateTestTableForIndexes(
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
                    ColumnName = "Email",
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
