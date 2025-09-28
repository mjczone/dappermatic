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
        var testTable = await CreateTestTableForIndexes(
            service,
            datasourceId,
            "IndexTest",
            schemaName
        );
        testTable.Should().NotBeNull();

        // Step 2: Get initial indexes (may include auto-created indexes)
        var listContext = OperationIdentifiers.ForIndexList(datasourceId, "IndexTest", schemaName);
        var initialIndexes = await service.GetIndexesAsync(
            listContext,
            datasourceId,
            "IndexTest",
            schemaName: schemaName
        );
        initialIndexes.Should().NotBeNull();
        var initialIndexesList = initialIndexes.ToList();

        // Step 3: Create a simple index on Name column
        var simpleIndexRequest = new IndexDto
        {
            IndexName = "IX_IndexTest_Name",
            ColumnNames = ["Name"],
            IsUnique = false,
        };

        var createContext = OperationIdentifiers.ForIndexCreate(
            datasourceId,
            "IndexTest",
            simpleIndexRequest,
            schemaName
        );
        var simpleIndex = await service.CreateIndexAsync(
            createContext,
            datasourceId,
            "IndexTest",
            simpleIndexRequest,
            schemaName: schemaName
        );
        simpleIndex.Should().NotBeNull();
        string.Equals(
                simpleIndex!.IndexName,
                "IX_IndexTest_Name",
                StringComparison.OrdinalIgnoreCase
            )
            .Should()
            .BeTrue();

        // Step 4: Create a unique index on Email column
        var uniqueIndexRequest = new IndexDto
        {
            IndexName = "IX_IndexTest_Email_Unique",
            ColumnNames = ["Email"],
            IsUnique = true,
        };

        var uniqueCreateContext = OperationIdentifiers.ForIndexCreate(
            datasourceId,
            "IndexTest",
            uniqueIndexRequest,
            schemaName
        );
        var uniqueIndex = await service.CreateIndexAsync(
            uniqueCreateContext,
            datasourceId,
            "IndexTest",
            uniqueIndexRequest,
            schemaName: schemaName
        );
        uniqueIndex.Should().NotBeNull();
        string.Equals(
                uniqueIndex!.IndexName,
                "IX_IndexTest_Email_Unique",
                StringComparison.OrdinalIgnoreCase
            )
            .Should()
            .BeTrue();
        uniqueIndex.IsUnique.Should().BeTrue();

        // Step 5: Create a composite index on multiple columns
        var compositeIndexRequest = new IndexDto
        {
            IndexName = "IX_IndexTest_Composite",
            ColumnNames = ["Name", "Email"],
            IsUnique = false,
        };

        var compositeCreateContext = OperationIdentifiers.ForIndexCreate(
            datasourceId,
            "IndexTest",
            compositeIndexRequest,
            schemaName
        );
        var compositeIndex = await service.CreateIndexAsync(
            compositeCreateContext,
            datasourceId,
            "IndexTest",
            compositeIndexRequest,
            schemaName: schemaName
        );
        compositeIndex.Should().NotBeNull();
        string.Equals(
                compositeIndex!.IndexName,
                "IX_IndexTest_Composite",
                StringComparison.OrdinalIgnoreCase
            )
            .Should()
            .BeTrue();

        // Step 6: Verify all indexes were created - should have initial + 3 new indexes
        var listAfterCreationContext = OperationIdentifiers.ForIndexList(
            datasourceId,
            "IndexTest",
            schemaName
        );
        var indexesAfterCreation = await service.GetIndexesAsync(
            listAfterCreationContext,
            datasourceId,
            "IndexTest",
            schemaName: schemaName
        );
        var indexesAfterCreationList = indexesAfterCreation.ToList();
        indexesAfterCreationList.Should().HaveCount(initialIndexesList.Count + 3);

        // Verify our created indexes exist (case insensitive for PostgreSQL)
        indexesAfterCreationList
            .Should()
            .Contain(i =>
                string.Equals(i.IndexName, "IX_IndexTest_Name", StringComparison.OrdinalIgnoreCase)
            );
        indexesAfterCreationList
            .Should()
            .Contain(i =>
                string.Equals(
                    i.IndexName,
                    "IX_IndexTest_Email_Unique",
                    StringComparison.OrdinalIgnoreCase
                )
            );
        indexesAfterCreationList
            .Should()
            .Contain(i =>
                string.Equals(
                    i.IndexName,
                    "IX_IndexTest_Composite",
                    StringComparison.OrdinalIgnoreCase
                )
            );

        // Step 7: Get a specific index
        var getIndexContext = OperationIdentifiers.ForIndexGet(
            datasourceId,
            "IndexTest",
            "IX_IndexTest_Email_Unique",
            schemaName
        );
        var specificIndex = await service.GetIndexAsync(
            getIndexContext,
            datasourceId,
            "IndexTest",
            "IX_IndexTest_Email_Unique",
            schemaName: schemaName
        );
        specificIndex.Should().NotBeNull();
        string.Equals(
                specificIndex!.IndexName,
                "IX_IndexTest_Email_Unique",
                StringComparison.OrdinalIgnoreCase
            )
            .Should()
            .BeTrue();
        specificIndex.IsUnique.Should().BeTrue();

        // Step 8: Drop the simple index
        var dropIndexContext = OperationIdentifiers.ForIndexDrop(
            datasourceId,
            "IndexTest",
            "IX_IndexTest_Name",
            schemaName
        );
        await service.DropIndexAsync(
            dropIndexContext,
            datasourceId,
            "IndexTest",
            "IX_IndexTest_Name",
            schemaName: schemaName
        );

        // Step 9: Verify index was dropped
        var listAfterDropContext = OperationIdentifiers.ForIndexList(
            datasourceId,
            "IndexTest",
            schemaName
        );
        var indexesAfterDrop = await service.GetIndexesAsync(
            listAfterDropContext,
            datasourceId,
            "IndexTest",
            schemaName: schemaName
        );
        var indexesAfterDropList = indexesAfterDrop.ToList();
        indexesAfterDropList.Should().HaveCount(initialIndexesList.Count + 2); // One less than after creation

        indexesAfterDropList
            .Should()
            .NotContain(i =>
                string.Equals(i.IndexName, "IX_IndexTest_Name", StringComparison.OrdinalIgnoreCase)
            );
        indexesAfterDropList
            .Should()
            .Contain(i =>
                string.Equals(
                    i.IndexName,
                    "IX_IndexTest_Email_Unique",
                    StringComparison.OrdinalIgnoreCase
                )
            );
        indexesAfterDropList
            .Should()
            .Contain(i =>
                string.Equals(
                    i.IndexName,
                    "IX_IndexTest_Composite",
                    StringComparison.OrdinalIgnoreCase
                )
            );

        // Step 10: Verify dropped index no longer exists
        var getDroppedIndexContext = OperationIdentifiers.ForIndexGet(
            datasourceId,
            "IndexTest",
            "IX_IndexTest_Name",
            schemaName
        );
        var droppedIndex = await service.GetIndexAsync(
            getDroppedIndexContext,
            datasourceId,
            "IndexTest",
            "IX_IndexTest_Name",
            schemaName: schemaName
        );
        droppedIndex.Should().BeNull();

        // Cleanup: Drop the test table (this will also drop remaining indexes)
        var dropTableContext = OperationIdentifiers.ForTableDrop(
            datasourceId,
            "IndexTest",
            schemaName
        );
        await service.DropTableAsync(
            dropTableContext,
            datasourceId,
            "IndexTest",
            schemaName: schemaName
        );
    }

    #endregion

    #region Error Scenario Tests

    [Fact]
    public async Task GetIndexesAsync_NonExistentTable_ThrowsArgumentException()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var context = OperationIdentifiers.ForIndexList(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "NonExistentTable",
            "dbo"
        );
        var act = async () =>
            await service.GetIndexesAsync(
                context,
                TestcontainersAssemblyFixture.DatasourceId_SqlServer,
                "NonExistentTable",
                schemaName: "dbo"
            );

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*does not exist*");
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

        var getContext = OperationIdentifiers.ForIndexGet(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "GetIndexTest",
            "NonExistentIndex",
            "dbo"
        );
        var result = await service.GetIndexAsync(
            getContext,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "GetIndexTest",
            "NonExistentIndex",
            schemaName: "dbo"
        );

        result.Should().BeNull();

        // Cleanup
        var dropTableContext = OperationIdentifiers.ForTableDrop(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "GetIndexTest",
            "dbo"
        );
        await service.DropTableAsync(
            dropTableContext,
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
        var indexRequest = new IndexDto
        {
            IndexName = "IX_Test_Duplicate",
            ColumnNames = ["Name"],
            IsUnique = false,
        };

        var createContext = OperationIdentifiers.ForIndexCreate(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "CreateIndexTest",
            indexRequest,
            "dbo"
        );
        var firstResult = await service.CreateIndexAsync(
            createContext,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "CreateIndexTest",
            indexRequest,
            schemaName: "dbo"
        );
        firstResult.Should().NotBeNull();

        // Try to create the same index again
        var duplicateContext = OperationIdentifiers.ForIndexCreate(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "CreateIndexTest",
            indexRequest,
            "dbo"
        );
        var duplicateResult = await service.CreateIndexAsync(
            duplicateContext,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "CreateIndexTest",
            indexRequest,
            schemaName: "dbo"
        );
        duplicateResult.Should().BeNull();

        // Cleanup
        var dropTableContext = OperationIdentifiers.ForTableDrop(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "CreateIndexTest",
            "dbo"
        );
        await service.DropTableAsync(
            dropTableContext,
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

        var dropContext = OperationIdentifiers.ForIndexDrop(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "DropIndexTest",
            "NonExistentIndex",
            "dbo"
        );
        await service.DropIndexAsync(
            dropContext,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "DropIndexTest",
            "NonExistentIndex",
            schemaName: "dbo"
        );

        // Cleanup
        var dropTableContext = OperationIdentifiers.ForTableDrop(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "DropIndexTest",
            "dbo"
        );
        await service.DropTableAsync(
            dropTableContext,
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

        var createIndexRequest = new IndexDto
        {
            IndexName = "IX_Test",
            ColumnNames = ["TestColumn"],
            IsUnique = false,
        };

        // Test all index methods with non-existent datasource
        var getIndexesContext = OperationIdentifiers.ForIndexList("NonExistent", "TestTable");
        var getIndexesAct = async () =>
            await service.GetIndexesAsync(getIndexesContext, "NonExistent", "TestTable");
        await getIndexesAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");

        var getIndexContext = OperationIdentifiers.ForIndexGet(
            "NonExistent",
            "TestTable",
            "TestIndex"
        );
        var getIndexAct = async () =>
            await service.GetIndexAsync(getIndexContext, "NonExistent", "TestTable", "TestIndex");
        await getIndexAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");

        var createIndexContext = OperationIdentifiers.ForIndexCreate(
            "NonExistent",
            "TestTable",
            createIndexRequest
        );
        var createIndexAct = async () =>
            await service.CreateIndexAsync(
                createIndexContext,
                "NonExistent",
                "TestTable",
                createIndexRequest
            );
        await createIndexAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");

        var dropIndexContext = OperationIdentifiers.ForIndexDrop(
            "NonExistent",
            "TestTable",
            "TestIndex"
        );
        var dropIndexAct = async () =>
            await service.DropIndexAsync(dropIndexContext, "NonExistent", "TestTable", "TestIndex");
        await dropIndexAct
            .Should()
            .ThrowAsync<ArgumentException>()
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
        return await service.CreateTableAsync(context, datasourceId, request);
    }

    #endregion
}
