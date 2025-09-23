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
/// Unit tests for DapperMatic service index operations.
/// </summary>
public class DapperMaticServiceIndexesTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private readonly TestcontainersAssemblyFixture _fixture;

    public DapperMaticServiceIndexesTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    #region Comprehensive Index Management Workflow Tests

    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer, "dbo")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql, "public")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql, null)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite, null)]
    public async Task IndexManagement_CompleteWorkflow_WorksCorrectly(
        string datasourceId,
        string? schemaName
    )
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        // Step 1: Create a test table for index operations
        var testTable = await CreateTestTableForIndexes(service, datasourceId, "IndexTest", schemaName);
        testTable.Should().NotBeNull();

        // Step 2: Get initial indexes (may include auto-created indexes)
        var initialIndexes = await service.GetIndexesAsync(datasourceId, "IndexTest", schemaName: schemaName);
        initialIndexes.Should().NotBeNull();
        var initialIndexesList = initialIndexes.ToList();

        // Step 3: Create a simple index on Name column
        var simpleIndexRequest = new CreateIndexRequest
        {
            IndexName = "IX_IndexTest_Name",
            Columns = ["Name"],
            IsUnique = false
        };

        var simpleIndex = await service.CreateIndexAsync(
            datasourceId,
            "IndexTest",
            simpleIndexRequest,
            schemaName: schemaName
        );
        simpleIndex.Should().NotBeNull();
        string.Equals(simpleIndex!.IndexName, "IX_IndexTest_Name", StringComparison.OrdinalIgnoreCase).Should().BeTrue();

        // Step 4: Create a unique index on Email column
        var uniqueIndexRequest = new CreateIndexRequest
        {
            IndexName = "IX_IndexTest_Email_Unique",
            Columns = ["Email"],
            IsUnique = true
        };

        var uniqueIndex = await service.CreateIndexAsync(
            datasourceId,
            "IndexTest",
            uniqueIndexRequest,
            schemaName: schemaName
        );
        uniqueIndex.Should().NotBeNull();
        string.Equals(uniqueIndex!.IndexName, "IX_IndexTest_Email_Unique", StringComparison.OrdinalIgnoreCase).Should().BeTrue();
        uniqueIndex.IsUnique.Should().BeTrue();

        // Step 5: Create a composite index on multiple columns
        var compositeIndexRequest = new CreateIndexRequest
        {
            IndexName = "IX_IndexTest_Composite",
            Columns = ["Name", "Email"],
            IsUnique = false
        };

        var compositeIndex = await service.CreateIndexAsync(
            datasourceId,
            "IndexTest",
            compositeIndexRequest,
            schemaName: schemaName
        );
        compositeIndex.Should().NotBeNull();
        string.Equals(compositeIndex!.IndexName, "IX_IndexTest_Composite", StringComparison.OrdinalIgnoreCase).Should().BeTrue();

        // Step 6: Verify all indexes were created - should have initial + 3 new indexes
        var indexesAfterCreation = await service.GetIndexesAsync(datasourceId, "IndexTest", schemaName: schemaName);
        var indexesAfterCreationList = indexesAfterCreation.ToList();
        indexesAfterCreationList.Should().HaveCount(initialIndexesList.Count + 3);

        // Verify our created indexes exist (case insensitive for PostgreSQL)
        indexesAfterCreationList.Should().Contain(i =>
            string.Equals(i.IndexName, "IX_IndexTest_Name", StringComparison.OrdinalIgnoreCase));
        indexesAfterCreationList.Should().Contain(i =>
            string.Equals(i.IndexName, "IX_IndexTest_Email_Unique", StringComparison.OrdinalIgnoreCase));
        indexesAfterCreationList.Should().Contain(i =>
            string.Equals(i.IndexName, "IX_IndexTest_Composite", StringComparison.OrdinalIgnoreCase));

        // Step 7: Get a specific index
        var specificIndex = await service.GetIndexAsync(
            datasourceId,
            "IndexTest",
            "IX_IndexTest_Email_Unique",
            schemaName: schemaName
        );
        specificIndex.Should().NotBeNull();
        string.Equals(specificIndex!.IndexName, "IX_IndexTest_Email_Unique", StringComparison.OrdinalIgnoreCase).Should().BeTrue();
        specificIndex.IsUnique.Should().BeTrue();

        // Step 8: Drop the simple index
        var dropResult = await service.DropIndexAsync(
            datasourceId,
            "IndexTest",
            "IX_IndexTest_Name",
            schemaName: schemaName
        );
        dropResult.Should().BeTrue();

        // Step 9: Verify index was dropped
        var indexesAfterDrop = await service.GetIndexesAsync(datasourceId, "IndexTest", schemaName: schemaName);
        var indexesAfterDropList = indexesAfterDrop.ToList();
        indexesAfterDropList.Should().HaveCount(initialIndexesList.Count + 2); // One less than after creation

        indexesAfterDropList.Should().NotContain(i =>
            string.Equals(i.IndexName, "IX_IndexTest_Name", StringComparison.OrdinalIgnoreCase));
        indexesAfterDropList.Should().Contain(i =>
            string.Equals(i.IndexName, "IX_IndexTest_Email_Unique", StringComparison.OrdinalIgnoreCase));
        indexesAfterDropList.Should().Contain(i =>
            string.Equals(i.IndexName, "IX_IndexTest_Composite", StringComparison.OrdinalIgnoreCase));

        // Step 10: Verify dropped index no longer exists
        var droppedIndex = await service.GetIndexAsync(
            datasourceId,
            "IndexTest",
            "IX_IndexTest_Name",
            schemaName: schemaName
        );
        droppedIndex.Should().BeNull();

        // Cleanup: Drop the test table (this will also drop remaining indexes)
        await service.DropTableAsync(datasourceId, "IndexTest", schemaName: schemaName);
    }

    #endregion

    #region Error Scenario Tests

    [Fact]
    public async Task GetIndexesAsync_NonExistentTable_ThrowsArgumentException()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var act = async () => await service.GetIndexesAsync(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "NonExistentTable",
            schemaName: "dbo"
        );

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*does not exist*");
    }

    [Fact]
    public async Task GetIndexAsync_NonExistentIndex_ReturnsNull()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        // Create a test table first
        var testTable = await CreateTestTableForIndexes(
            service,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "GetIndexTest",
            "dbo"
        );
        testTable.Should().NotBeNull();

        var result = await service.GetIndexAsync(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "GetIndexTest",
            "NonExistentIndex",
            schemaName: "dbo"
        );

        result.Should().BeNull();

        // Cleanup
        await service.DropTableAsync(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "GetIndexTest",
            schemaName: "dbo"
        );
    }

    [Fact]
    public async Task CreateIndexAsync_DuplicateIndex_ReturnsNull()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        // Create a test table first
        var testTable = await CreateTestTableForIndexes(
            service,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "CreateIndexTest",
            "dbo"
        );
        testTable.Should().NotBeNull();

        // Create an index first
        var indexRequest = new CreateIndexRequest
        {
            IndexName = "IX_Test_Duplicate",
            Columns = ["Name"],
            IsUnique = false
        };

        var firstResult = await service.CreateIndexAsync(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "CreateIndexTest",
            indexRequest,
            schemaName: "dbo"
        );
        firstResult.Should().NotBeNull();

        // Try to create the same index again
        var duplicateResult = await service.CreateIndexAsync(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "CreateIndexTest",
            indexRequest,
            schemaName: "dbo"
        );
        duplicateResult.Should().BeNull();

        // Cleanup
        await service.DropTableAsync(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "CreateIndexTest",
            schemaName: "dbo"
        );
    }

    [Fact]
    public async Task DropIndexAsync_NonExistentIndex_ReturnsFalse()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        // Create a test table first
        var testTable = await CreateTestTableForIndexes(
            service,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "DropIndexTest",
            "dbo"
        );
        testTable.Should().NotBeNull();

        var result = await service.DropIndexAsync(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "DropIndexTest",
            "NonExistentIndex",
            schemaName: "dbo"
        );

        result.Should().BeFalse();

        // Cleanup
        await service.DropTableAsync(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "DropIndexTest",
            schemaName: "dbo"
        );
    }

    [Fact]
    public async Task IndexManagement_NonExistentDatasource_ThrowsArgumentException()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var createIndexRequest = new CreateIndexRequest
        {
            IndexName = "IX_Test",
            Columns = ["TestColumn"],
            IsUnique = false
        };

        // Test all index methods with non-existent datasource
        var getIndexesAct = async () => await service.GetIndexesAsync("NonExistent", "TestTable");
        await getIndexesAct.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");

        var getIndexAct = async () => await service.GetIndexAsync("NonExistent", "TestTable", "TestIndex");
        await getIndexAct.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");

        var createIndexAct = async () => await service.CreateIndexAsync("NonExistent", "TestTable", createIndexRequest);
        await createIndexAct.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");

        var dropIndexAct = async () => await service.DropIndexAsync("NonExistent", "TestTable", "TestIndex");
        await dropIndexAct.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");
    }

    #endregion

    #region Helper Methods

    private static async Task<TableDto?> CreateTestTableForIndexes(
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
                new CreateColumnRequest
                {
                    ColumnName = "Id",
                    ProviderDataType = "int",
                    IsNullable = false,
                },
                new CreateColumnRequest
                {
                    ColumnName = "Name",
                    ProviderDataType = datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                        ? "nvarchar(255)"
                        : "varchar(255)",
                    IsNullable = true,
                },
                new CreateColumnRequest
                {
                    ColumnName = "Email",
                    ProviderDataType = datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                        ? "nvarchar(255)"
                        : "varchar(255)",
                    IsNullable = false,
                },
                new CreateColumnRequest
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
            PrimaryKey = new CreatePrimaryKeyRequest
            {
                ConstraintName = $"PK_{tableName}",
                Columns = ["Id"],
            },
        };

        return await service.CreateTableAsync(datasourceId, request);
    }

    #endregion
}