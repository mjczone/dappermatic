// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using FluentAssertions;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Models.Requests;
using MJCZone.DapperMatic.AspNetCore.Models.Responses;
using MJCZone.DapperMatic.AspNetCore.Tests.Factories;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Endpoints;

/// <summary>
/// Integration tests for DapperMatic table endpoints.
/// </summary>
public class TableEndpointsTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private readonly TestcontainersAssemblyFixture _fixture;

    public TableEndpointsTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    #region Table Management Endpoints Tests

    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer, "dbo")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql, "public")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql, null)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite, null)]
    public async Task TableEndpoints_CompleteWorkflow_WorksCorrectly(
        string datasourceId,
        string? schemaName
    )
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        using var client = factory.CreateClient();

        var baseUrl =
            schemaName != null
                ? $"/api/dm/d/{datasourceId}/s/{schemaName}/t"
                : $"/api/dm/d/{datasourceId}/t";

        // Step 1: List initial tables
        var initialTablesResponse = await client.GetAsync($"{baseUrl}/");
        initialTablesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var initialTablesResult =
            await initialTablesResponse.Content.ReadFromJsonAsync<TableListResponse>();
        var initialTables = initialTablesResult?.Result;
        initialTables.Should().NotBeNull();
        var initialTableCount = initialTables!.Count();

        // Step 2: Create a new table
        var createTableRequest = new CreateTableRequest
        {
            TableName = "EndpointTestTable",
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
                    ProviderDataType =
                        datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                            ? "nvarchar(255)"
                            : "varchar(255)",
                    IsNullable = true,
                },
                new CreateTableColumnRequest
                {
                    ColumnName = "Email",
                    ProviderDataType =
                        datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                            ? "nvarchar(255)"
                            : "varchar(255)",
                    IsNullable = false,
                },
            ],
        };

        var createResponse = await client.PostAsJsonAsync($"{baseUrl}/", createTableRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdTable = (
            await createResponse.Content.ReadFromJsonAsync<TableResponse>()
        )?.Result;
        createdTable.Should().NotBeNull();
        string.Equals(
                createdTable!.TableName,
                "EndpointTestTable",
                StringComparison.OrdinalIgnoreCase
            )
            .Should()
            .BeTrue();

        // Step 3: Check if table exists
        var existsResponse = await client.GetAsync($"{baseUrl}/EndpointTestTable/exists");
        existsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var existsResult =
            (await existsResponse.Content.ReadFromJsonAsync<TableExistsResponse>())?.Result == true;
        existsResult.Should().BeTrue();

        // Step 4: Get the created table
        var getTableResponse = await client.GetAsync($"{baseUrl}/EndpointTestTable");
        getTableResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var retrievedTable = (
            await getTableResponse.Content.ReadFromJsonAsync<TableResponse>()
        )?.Result;
        retrievedTable.Should().NotBeNull();
        string.Equals(
                retrievedTable!.TableName,
                "EndpointTestTable",
                StringComparison.OrdinalIgnoreCase
            )
            .Should()
            .BeTrue();

        // Step 5: List tables after creation - should have one more
        var tablesAfterCreateResponse = await client.GetAsync($"{baseUrl}/");
        tablesAfterCreateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var tablesAfterCreate = (
            await tablesAfterCreateResponse.Content.ReadFromJsonAsync<TableListResponse>()
        )?.Result;
        tablesAfterCreate.Should().NotBeNull();
        tablesAfterCreate.Should().HaveCount(initialTableCount + 1);

        // Step 6: Query the table (should be empty but structure should work)
        var queryRequest = new QueryRequest { Take = 10, Skip = 0 };
        var queryResponse = await client.PostAsJsonAsync(
            $"{baseUrl}/EndpointTestTable/query",
            queryRequest
        );
        queryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var queryResult = await queryResponse.Content.ReadFromJsonAsync<QueryResponse>();
        queryResult.Should().NotBeNull();
        queryResult!.Result.Should().NotBeNull();
        // queryResult!.Result!.Columns.Should().NotBeEmpty();

        // Step 7: Test GET version of query
        var queryGetResponse = await client.GetAsync(
            $"{baseUrl}/EndpointTestTable/query?take=5&skip=0"
        );
        queryGetResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var queryGetResult = await queryGetResponse.Content.ReadFromJsonAsync<QueryResponse>();
        queryGetResult.Should().NotBeNull();
        queryGetResult!.Result.Should().NotBeNull();

        // Step 8: Rename the table
        var renameRequest = new RenameTableRequest { NewTableName = "EndpointTestTableRenamed" };
        var renameResponse = await client.PutAsJsonAsync(
            $"{baseUrl}/EndpointTestTable",
            renameRequest
        );
        renameResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 9: Verify rename worked
        var oldTableExistsResponse = await client.GetAsync($"{baseUrl}/EndpointTestTable/exists");
        oldTableExistsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var oldTableExists =
            (await oldTableExistsResponse.Content.ReadFromJsonAsync<TableExistsResponse>())?.Result
            == true;
        oldTableExists.Should().BeFalse();

        var newTableExistsResponse = await client.GetAsync(
            $"{baseUrl}/EndpointTestTableRenamed/exists"
        );
        newTableExistsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var newTableExists =
            (await newTableExistsResponse.Content.ReadFromJsonAsync<TableExistsResponse>())?.Result
            == true;
        newTableExists.Should().BeTrue();

        // Step 10: Drop the table
        var dropResponse = await client.DeleteAsync($"{baseUrl}/EndpointTestTableRenamed");
        dropResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 11: Verify table was dropped
        var finalTablesResponse = await client.GetAsync($"{baseUrl}/");
        finalTablesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var finalTables = (
            await finalTablesResponse.Content.ReadFromJsonAsync<TableListResponse>()
        )?.Result;
        finalTables.Should().HaveCount(initialTableCount);
    }

    #endregion

    #region Column Endpoints Tests

    [Fact]
    public async Task ColumnEndpoints_CompleteWorkflow_WorksCorrectly()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        using var client = factory.CreateClient();

        var datasourceId = TestcontainersAssemblyFixture.DatasourceId_SqlServer;
        var schemaName = "dbo";
        var baseUrl = $"/api/dm/d/{datasourceId}/s/{schemaName}/t";

        // Step 1: Create a test table first
        var createTableRequest = new CreateTableRequest
        {
            TableName = "ColumnTestTable",
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
                    ProviderDataType = "nvarchar(255)",
                    IsNullable = true,
                },
            ],
        };

        var createResponse = await client.PostAsJsonAsync($"{baseUrl}/", createTableRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Step 2: List initial columns
        var initialColumnsResponse = await client.GetAsync($"{baseUrl}/ColumnTestTable/columns");
        initialColumnsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var initialColumns = (
            await initialColumnsResponse.Content.ReadFromJsonAsync<ColumnListResponse>()
        )?.Result;
        initialColumns.Should().HaveCount(2);

        // Step 3: Get a specific column
        var getColumnResponse = await client.GetAsync($"{baseUrl}/ColumnTestTable/columns/Name");
        getColumnResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var column = (await getColumnResponse.Content.ReadFromJsonAsync<ColumnResponse>())?.Result;
        column.Should().NotBeNull();
        string.Equals(column!.ColumnName, "Name", StringComparison.OrdinalIgnoreCase)
            .Should()
            .BeTrue();

        // Step 4: Add a new column
        var addColumnRequest = new CreateTableColumnRequest
        {
            ColumnName = "Email",
            ProviderDataType = "nvarchar(255)",
            IsNullable = true,
        };

        var addColumnResponse = await client.PostAsJsonAsync(
            $"{baseUrl}/ColumnTestTable/columns",
            addColumnRequest
        );
        addColumnResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Step 5: Verify column was added
        var columnsAfterAddResponse = await client.GetAsync($"{baseUrl}/ColumnTestTable/columns");
        columnsAfterAddResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var columnsAfterAdd = (
            await columnsAfterAddResponse.Content.ReadFromJsonAsync<ColumnListResponse>()
        )?.Result;
        columnsAfterAdd.Should().NotBeNull();
        columnsAfterAdd.Should().HaveCount(3);

        // Step 6: Update/Rename the column
        var updateColumnRequest = new RenameColumnRequest { NewColumnName = "EmailAddress" };
        var updateResponse = await client.PutAsJsonAsync(
            $"{baseUrl}/ColumnTestTable/columns/Email",
            updateColumnRequest
        );
        var updateContent = await updateResponse.Content.ReadAsStringAsync();
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 7: Drop the added column
        var dropColumnResponse = await client.DeleteAsync(
            $"{baseUrl}/ColumnTestTable/columns/EmailAddress"
        );
        dropColumnResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 8: Verify final column count
        var finalColumnsResponse = await client.GetAsync($"{baseUrl}/ColumnTestTable/columns");
        finalColumnsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var finalColumns = (
            await finalColumnsResponse.Content.ReadFromJsonAsync<ColumnListResponse>()
        )?.Result;
        finalColumns.Should().HaveCount(2);

        // Cleanup: Drop the test table
        await client.DeleteAsync($"{baseUrl}/ColumnTestTable");
    }

    #endregion

    #region Index Endpoints Tests

    [Fact]
    public async Task IndexEndpoints_CompleteWorkflow_WorksCorrectly()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        using var client = factory.CreateClient();

        var datasourceId = TestcontainersAssemblyFixture.DatasourceId_SqlServer;
        var schemaName = "dbo";
        var baseUrl = $"/api/dm/d/{datasourceId}/s/{schemaName}/t";

        // Step 1: Create a test table first
        var createTableRequest = new CreateTableRequest
        {
            TableName = "IndexTestTable",
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
                    ProviderDataType = "nvarchar(255)",
                    IsNullable = true,
                },
                new CreateTableColumnRequest
                {
                    ColumnName = "Email",
                    ProviderDataType = "nvarchar(255)",
                    IsNullable = false,
                },
            ],
            PrimaryKey = new CreateTablePrimaryKeyRequest
            {
                ConstraintName = "PK_IndexTestTable",
                Columns = ["Id"],
            },
        };

        var createResponse = await client.PostAsJsonAsync($"{baseUrl}/", createTableRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Step 2: List initial indexes (may include auto-created ones)
        var initialIndexesResponse = await client.GetAsync($"{baseUrl}/IndexTestTable/indexes");
        initialIndexesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var initialIndexes = (
            await initialIndexesResponse.Content.ReadFromJsonAsync<IndexListResponse>()
        )?.Result;
        initialIndexes.Should().NotBeNull();
        var initialIndexCount = initialIndexes!.Count();

        // Step 3: Create a new index
        var createIndexRequest = new CreateIndexRequest
        {
            IndexName = "IX_IndexTestTable_Name",
            Columns = ["Name"],
            IsUnique = false,
        };

        var createIndexResponse = await client.PostAsJsonAsync(
            $"{baseUrl}/IndexTestTable/indexes",
            createIndexRequest
        );
        var responseContent = await createIndexResponse.Content.ReadAsStringAsync();
        createIndexResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Step 4: List indexes after creation
        var indexesAfterCreateResponse = await client.GetAsync($"{baseUrl}/IndexTestTable/indexes");
        indexesAfterCreateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var indexesAfterCreate = (
            await indexesAfterCreateResponse.Content.ReadFromJsonAsync<IndexListResponse>()
        )?.Result;
        indexesAfterCreate.Should().HaveCount(initialIndexCount + 1);

        // Step 5: Get the specific index
        var getIndexResponse = await client.GetAsync(
            $"{baseUrl}/IndexTestTable/indexes/IX_IndexTestTable_Name"
        );
        getIndexResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var index = (await getIndexResponse.Content.ReadFromJsonAsync<IndexResponse>())?.Result;
        index.Should().NotBeNull();
        string.Equals(
                index!.IndexName,
                "IX_IndexTestTable_Name",
                StringComparison.OrdinalIgnoreCase
            )
            .Should()
            .BeTrue();

        // Step 6: Drop the index
        var dropIndexResponse = await client.DeleteAsync(
            $"{baseUrl}/IndexTestTable/indexes/IX_IndexTestTable_Name"
        );
        dropIndexResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 7: Verify index was dropped
        var finalIndexesResponse = await client.GetAsync($"{baseUrl}/IndexTestTable/indexes");
        finalIndexesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var finalIndexes = (
            await finalIndexesResponse.Content.ReadFromJsonAsync<IndexListResponse>()
        )?.Result;
        finalIndexes.Should().HaveCount(initialIndexCount);

        // Cleanup: Drop the test table
        var dropped = await client.DeleteAsync($"{baseUrl}/IndexTestTable");
        dropped.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Primary Key Constraint Endpoints Tests

    [Fact]
    public async Task PrimaryKeyEndpoints_CompleteWorkflow_WorksCorrectly()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        using var client = factory.CreateClient();

        var datasourceId = TestcontainersAssemblyFixture.DatasourceId_SqlServer;
        var schemaName = "dbo";
        var baseUrl = $"/api/dm/d/{datasourceId}/s/{schemaName}/t";

        // Step 1: Create a test table without a primary key
        var createTableRequest = new CreateTableRequest
        {
            TableName = "PkTestTable",
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
                    ProviderDataType = "nvarchar(255)",
                    IsNullable = true,
                },
                new CreateTableColumnRequest
                {
                    ColumnName = "Email",
                    ProviderDataType = "nvarchar(255)",
                    IsNullable = false,
                },
            ],
        };

        var createResponse = await client.PostAsJsonAsync($"{baseUrl}/", createTableRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Step 2: Verify no primary key exists initially
        var initialPkResponse = await client.GetAsync($"{baseUrl}/PkTestTable/primarykey");
        initialPkResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Step 3: Create a primary key constraint
        var createPkRequest = new CreatePrimaryKeyRequest
        {
            ConstraintName = "PK_PkTestTable",
            Columns = ["Id"],
        };

        var createPkResponse = await client.PostAsJsonAsync(
            $"{baseUrl}/PkTestTable/primarykey",
            createPkRequest
        );
        createPkResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdPk = (
            await createPkResponse.Content.ReadFromJsonAsync<PrimaryKeyResponse>()
        )?.Result;
        createdPk.Should().NotBeNull();
        string.Equals(
                createdPk!.ConstraintName,
                "PK_PkTestTable",
                StringComparison.OrdinalIgnoreCase
            )
            .Should()
            .BeTrue();

        // Step 4: Get the primary key constraint
        var getPkResponse = await client.GetAsync($"{baseUrl}/PkTestTable/primarykey");
        getPkResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var retrievedPk = (
            await getPkResponse.Content.ReadFromJsonAsync<PrimaryKeyResponse>()
        )?.Result;
        retrievedPk.Should().NotBeNull();
        string.Equals(
                retrievedPk!.ConstraintName,
                "PK_PkTestTable",
                StringComparison.OrdinalIgnoreCase
            )
            .Should()
            .BeTrue();
        retrievedPk.ColumnNames.Should().HaveCount(1);
        string.Equals(retrievedPk.ColumnNames.First(), "Id", StringComparison.OrdinalIgnoreCase)
            .Should()
            .BeTrue();

        // Step 5: Drop the primary key constraint
        var dropPkResponse = await client.DeleteAsync($"{baseUrl}/PkTestTable/primarykey");
        dropPkResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 6: Verify primary key was dropped
        var finalPkResponse = await client.GetAsync($"{baseUrl}/PkTestTable/primarykey");
        finalPkResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Cleanup: Drop the test table
        await client.DeleteAsync($"{baseUrl}/PkTestTable");
    }

    #endregion

    #region Foreign Key Constraint Endpoints Tests

    [Fact]
    public async Task ForeignKeyEndpoints_CompleteWorkflow_WorksCorrectly()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        using var client = factory.CreateClient();

        var datasourceId = TestcontainersAssemblyFixture.DatasourceId_SqlServer;
        var schemaName = "dbo";
        var baseUrl = $"/api/dm/d/{datasourceId}/s/{schemaName}/t";

        // Step 1: Create a referenced table first
        var createReferencedTableRequest = new CreateTableRequest
        {
            TableName = "FkReferencedTable",
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
                    ProviderDataType = "nvarchar(255)",
                    IsNullable = false,
                },
            ],
            PrimaryKey = new CreateTablePrimaryKeyRequest
            {
                ConstraintName = "PK_FkReferencedTable",
                Columns = ["Id"],
            },
        };

        var createReferencedResponse = await client.PostAsJsonAsync(
            $"{baseUrl}/",
            createReferencedTableRequest
        );
        createReferencedResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Step 2: Create a referencing table
        var createReferencingTableRequest = new CreateTableRequest
        {
            TableName = "FkTestTable",
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
                    ColumnName = "ReferencedId",
                    ProviderDataType = "int",
                    IsNullable = true,
                },
                new CreateTableColumnRequest
                {
                    ColumnName = "Name",
                    ProviderDataType = "nvarchar(255)",
                    IsNullable = true,
                },
            ],
            PrimaryKey = new CreateTablePrimaryKeyRequest
            {
                ConstraintName = "PK_FkTestTable",
                Columns = ["Id"],
            },
        };

        var createReferencingResponse = await client.PostAsJsonAsync(
            $"{baseUrl}/",
            createReferencingTableRequest
        );
        createReferencingResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Step 3: List initial foreign keys (should be empty)
        var initialFksResponse = await client.GetAsync($"{baseUrl}/FkTestTable/foreignkeys");
        initialFksResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var initialFks = (
            await initialFksResponse.Content.ReadFromJsonAsync<ForeignKeyListResponse>()
        )?.Result;
        initialFks.Should().NotBeNull();
        var initialFkCount = initialFks!.Count();

        // Step 4: Create a foreign key constraint
        var createFkRequest = new CreateForeignKeyRequest
        {
            ConstraintName = "FK_FkTestTable_ReferencedId",
            Columns = ["ReferencedId"],
            ReferencedTableName = "FkReferencedTable",
            ReferencedColumns = ["Id"],
            OnDelete = "CASCADE",
            OnUpdate = "CASCADE",
        };

        var createFkResponse = await client.PostAsJsonAsync(
            $"{baseUrl}/FkTestTable/foreignkeys",
            createFkRequest
        );
        createFkResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdFk = (
            await createFkResponse.Content.ReadFromJsonAsync<ForeignKeyResponse>()
        )?.Result;
        createdFk.Should().NotBeNull();
        string.Equals(
                createdFk!.ConstraintName,
                "FK_FkTestTable_ReferencedId",
                StringComparison.OrdinalIgnoreCase
            )
            .Should()
            .BeTrue();

        // Step 5: List foreign keys after creation
        var fksAfterCreateResponse = await client.GetAsync($"{baseUrl}/FkTestTable/foreignkeys");
        fksAfterCreateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fksAfterCreate = (
            await fksAfterCreateResponse.Content.ReadFromJsonAsync<ForeignKeyListResponse>()
        )?.Result;
        fksAfterCreate.Should().HaveCount(initialFkCount + 1);

        // Step 6: Get the specific foreign key
        var getFkResponse = await client.GetAsync(
            $"{baseUrl}/FkTestTable/foreignkeys/FK_FkTestTable_ReferencedId"
        );
        getFkResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var retrievedFk = (
            await getFkResponse.Content.ReadFromJsonAsync<ForeignKeyResponse>()
        )?.Result;
        retrievedFk.Should().NotBeNull();
        string.Equals(
                retrievedFk!.ConstraintName,
                "FK_FkTestTable_ReferencedId",
                StringComparison.OrdinalIgnoreCase
            )
            .Should()
            .BeTrue();
        retrievedFk.ColumnNames.Should().HaveCount(1);
        string.Equals(
                retrievedFk.ColumnNames.First(),
                "ReferencedId",
                StringComparison.OrdinalIgnoreCase
            )
            .Should()
            .BeTrue();

        // Step 7: Drop the foreign key constraint
        var dropFkResponse = await client.DeleteAsync(
            $"{baseUrl}/FkTestTable/foreignkeys/FK_FkTestTable_ReferencedId"
        );
        dropFkResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 8: Verify foreign key was dropped
        var finalFksResponse = await client.GetAsync($"{baseUrl}/FkTestTable/foreignkeys");
        finalFksResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var finalFks = (
            await finalFksResponse.Content.ReadFromJsonAsync<ForeignKeyListResponse>()
        )?.Result;
        finalFks.Should().HaveCount(initialFkCount);

        // Cleanup: Drop the test tables
        await client.DeleteAsync($"{baseUrl}/FkTestTable");
        await client.DeleteAsync($"{baseUrl}/FkReferencedTable");
    }

    #endregion

    #region Check Constraint Endpoints Tests

    [Fact]
    public async Task CheckConstraintEndpoints_CompleteWorkflow_WorksCorrectly()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        using var client = factory.CreateClient();

        var datasourceId = TestcontainersAssemblyFixture.DatasourceId_SqlServer;
        var schemaName = "dbo";
        var baseUrl = $"/api/dm/d/{datasourceId}/s/{schemaName}/t";

        // Step 1: Create a test table
        var createTableRequest = new CreateTableRequest
        {
            TableName = "CheckTestTable",
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
                    ColumnName = "Age",
                    ProviderDataType = "int",
                    IsNullable = true,
                },
                new CreateTableColumnRequest
                {
                    ColumnName = "Score",
                    ProviderDataType = "decimal(5,2)",
                    IsNullable = true,
                },
            ],
            PrimaryKey = new CreateTablePrimaryKeyRequest
            {
                ConstraintName = "PK_CheckTestTable",
                Columns = ["Id"],
            },
        };

        var createResponse = await client.PostAsJsonAsync($"{baseUrl}/", createTableRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Step 2: List initial check constraints (should be empty)
        var initialChecksResponse = await client.GetAsync(
            $"{baseUrl}/CheckTestTable/checkconstraints"
        );
        initialChecksResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var initialChecks = (
            await initialChecksResponse.Content.ReadFromJsonAsync<CheckConstraintListResponse>()
        )?.Result;
        initialChecks.Should().NotBeNull();
        var initialCheckCount = initialChecks!.Count();

        // Step 3: Create a check constraint
        var createCheckRequest = new CreateCheckConstraintRequest
        {
            ConstraintName = "CHK_CheckTestTable_Age",
            CheckExpression = "[Age] >= 0 AND [Age] <= 150",
        };

        var createCheckResponse = await client.PostAsJsonAsync(
            $"{baseUrl}/CheckTestTable/checkconstraints",
            createCheckRequest
        );
        createCheckResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdCheck = (
            await createCheckResponse.Content.ReadFromJsonAsync<CheckConstraintResponse>()
        )?.Result;
        createdCheck.Should().NotBeNull();
        string.Equals(
                createdCheck!.ConstraintName,
                "CHK_CheckTestTable_Age",
                StringComparison.OrdinalIgnoreCase
            )
            .Should()
            .BeTrue();

        // Step 4: Create another check constraint
        var createCheck2Request = new CreateCheckConstraintRequest
        {
            ConstraintName = "CHK_CheckTestTable_Score",
            CheckExpression = "[Score] >= 0.0 AND [Score] <= 100.0",
        };

        var createCheck2Response = await client.PostAsJsonAsync(
            $"{baseUrl}/CheckTestTable/checkconstraints",
            createCheck2Request
        );
        createCheck2Response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Step 5: List check constraints after creation
        var checksAfterCreateResponse = await client.GetAsync(
            $"{baseUrl}/CheckTestTable/checkconstraints"
        );
        checksAfterCreateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var checksAfterCreate = (
            await checksAfterCreateResponse.Content.ReadFromJsonAsync<CheckConstraintListResponse>()
        )?.Result;
        checksAfterCreate.Should().HaveCount(initialCheckCount + 2);

        // Step 6: Get the specific check constraint
        var getCheckResponse = await client.GetAsync(
            $"{baseUrl}/CheckTestTable/checkconstraints/CHK_CheckTestTable_Age"
        );
        getCheckResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var retrievedCheck = (
            await getCheckResponse.Content.ReadFromJsonAsync<CheckConstraintResponse>()
        )?.Result;
        retrievedCheck.Should().NotBeNull();
        string.Equals(
                retrievedCheck!.ConstraintName,
                "CHK_CheckTestTable_Age",
                StringComparison.OrdinalIgnoreCase
            )
            .Should()
            .BeTrue();
        retrievedCheck.CheckExpression.Should().NotBeNullOrEmpty();

        // Step 7: Drop one check constraint
        var dropCheckResponse = await client.DeleteAsync(
            $"{baseUrl}/CheckTestTable/checkconstraints/CHK_CheckTestTable_Age"
        );
        dropCheckResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 8: Verify check constraint was dropped
        var finalChecksResponse = await client.GetAsync(
            $"{baseUrl}/CheckTestTable/checkconstraints"
        );
        finalChecksResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var finalChecks = (
            await finalChecksResponse.Content.ReadFromJsonAsync<CheckConstraintListResponse>()
        )?.Result;
        finalChecks.Should().HaveCount(initialCheckCount + 1); // One less than after creation

        // Cleanup: Drop the test table
        await client.DeleteAsync($"{baseUrl}/CheckTestTable");
    }

    #endregion

    #region Unique Constraint Endpoints Tests

    [Fact]
    public async Task UniqueConstraintEndpoints_CompleteWorkflow_WorksCorrectly()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        using var client = factory.CreateClient();

        var datasourceId = TestcontainersAssemblyFixture.DatasourceId_SqlServer;
        var schemaName = "dbo";
        var baseUrl = $"/api/dm/d/{datasourceId}/s/{schemaName}/t";

        // Step 1: Create a test table
        var createTableRequest = new CreateTableRequest
        {
            TableName = "UniqueTestTable",
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
                    ColumnName = "Email",
                    ProviderDataType = "nvarchar(255)",
                    IsNullable = false,
                },
                new CreateTableColumnRequest
                {
                    ColumnName = "Username",
                    ProviderDataType = "nvarchar(100)",
                    IsNullable = false,
                },
                new CreateTableColumnRequest
                {
                    ColumnName = "PhoneNumber",
                    ProviderDataType = "nvarchar(20)",
                    IsNullable = true,
                },
            ],
            PrimaryKey = new CreateTablePrimaryKeyRequest
            {
                ConstraintName = "PK_UniqueTestTable",
                Columns = ["Id"],
            },
        };

        var createResponse = await client.PostAsJsonAsync($"{baseUrl}/", createTableRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Step 2: List initial unique constraints (should be empty except maybe PK)
        var initialUniquesResponse = await client.GetAsync(
            $"{baseUrl}/UniqueTestTable/uniqueconstraints"
        );
        initialUniquesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var initialUniques = (
            await initialUniquesResponse.Content.ReadFromJsonAsync<UniqueConstraintListResponse>()
        )?.Result;
        initialUniques.Should().NotBeNull();
        var initialUniqueCount = initialUniques!.Count();

        // Step 3: Create a unique constraint on Email
        var createUniqueRequest = new CreateUniqueConstraintRequest
        {
            ConstraintName = "UQ_UniqueTestTable_Email",
            Columns = ["Email"],
        };

        var createUniqueResponse = await client.PostAsJsonAsync(
            $"{baseUrl}/UniqueTestTable/uniqueconstraints",
            createUniqueRequest
        );
        createUniqueResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdUnique = (
            await createUniqueResponse.Content.ReadFromJsonAsync<UniqueConstraintResponse>()
        )?.Result;
        createdUnique.Should().NotBeNull();
        string.Equals(
                createdUnique!.ConstraintName,
                "UQ_UniqueTestTable_Email",
                StringComparison.OrdinalIgnoreCase
            )
            .Should()
            .BeTrue();

        // Step 4: Create a composite unique constraint
        var createCompositeUniqueRequest = new CreateUniqueConstraintRequest
        {
            ConstraintName = "UQ_UniqueTestTable_Username_PhoneNumber",
            Columns = ["Username", "PhoneNumber"],
        };

        var createCompositeUniqueResponse = await client.PostAsJsonAsync(
            $"{baseUrl}/UniqueTestTable/uniqueconstraints",
            createCompositeUniqueRequest
        );
        createCompositeUniqueResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Step 5: List unique constraints after creation
        var uniquesAfterCreateResponse = await client.GetAsync(
            $"{baseUrl}/UniqueTestTable/uniqueconstraints"
        );
        uniquesAfterCreateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var uniquesAfterCreate = (
            await uniquesAfterCreateResponse.Content.ReadFromJsonAsync<UniqueConstraintListResponse>()
        )?.Result;
        uniquesAfterCreate.Should().HaveCount(initialUniqueCount + 2);

        // Step 6: Get the specific unique constraint
        var getUniqueResponse = await client.GetAsync(
            $"{baseUrl}/UniqueTestTable/uniqueconstraints/UQ_UniqueTestTable_Email"
        );
        getUniqueResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var retrievedUnique = (
            await getUniqueResponse.Content.ReadFromJsonAsync<UniqueConstraintResponse>()
        )?.Result;
        retrievedUnique.Should().NotBeNull();
        string.Equals(
                retrievedUnique!.ConstraintName,
                "UQ_UniqueTestTable_Email",
                StringComparison.OrdinalIgnoreCase
            )
            .Should()
            .BeTrue();
        retrievedUnique.ColumnNames.Should().HaveCount(1);
        string.Equals(
                retrievedUnique.ColumnNames.First(),
                "Email",
                StringComparison.OrdinalIgnoreCase
            )
            .Should()
            .BeTrue();

        // Step 7: Drop one unique constraint
        var dropUniqueResponse = await client.DeleteAsync(
            $"{baseUrl}/UniqueTestTable/uniqueconstraints/UQ_UniqueTestTable_Email"
        );
        dropUniqueResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 8: Verify unique constraint was dropped
        var finalUniquesResponse = await client.GetAsync(
            $"{baseUrl}/UniqueTestTable/uniqueconstraints"
        );
        finalUniquesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var finalUniques = (
            await finalUniquesResponse.Content.ReadFromJsonAsync<UniqueConstraintListResponse>()
        )?.Result;
        finalUniques.Should().HaveCount(initialUniqueCount + 1); // One less than after creation

        // Cleanup: Drop the test table
        await client.DeleteAsync($"{baseUrl}/UniqueTestTable");
    }

    #endregion

    #region Default Constraint Endpoints Tests

    [Fact]
    public async Task DefaultConstraintEndpoints_CompleteWorkflow_WorksCorrectly()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        using var client = factory.CreateClient();

        var datasourceId = TestcontainersAssemblyFixture.DatasourceId_SqlServer;
        var schemaName = "dbo";
        var baseUrl = $"/api/dm/d/{datasourceId}/s/{schemaName}/t";

        // Step 1: Create a test table
        var createTableRequest = new CreateTableRequest
        {
            TableName = "DefaultTestTable",
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
                    ColumnName = "CreatedDate",
                    ProviderDataType = "datetime2",
                    IsNullable = false,
                },
                new CreateTableColumnRequest
                {
                    ColumnName = "Status",
                    ProviderDataType = "nvarchar(50)",
                    IsNullable = false,
                },
                new CreateTableColumnRequest
                {
                    ColumnName = "IsActive",
                    ProviderDataType = "bit",
                    IsNullable = false,
                },
            ],
            PrimaryKey = new CreateTablePrimaryKeyRequest
            {
                ConstraintName = "PK_DefaultTestTable",
                Columns = ["Id"],
            },
        };

        var createResponse = await client.PostAsJsonAsync($"{baseUrl}/", createTableRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Step 2: List initial default constraints (should be empty)
        var initialDefaultsResponse = await client.GetAsync(
            $"{baseUrl}/DefaultTestTable/defaultconstraints"
        );
        initialDefaultsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var initialDefaults = (
            await initialDefaultsResponse.Content.ReadFromJsonAsync<DefaultConstraintListResponse>()
        )?.Result;
        initialDefaults.Should().NotBeNull();
        var initialDefaultCount = initialDefaults!.Count();

        // Step 3: Create a default constraint for CreatedDate
        var createDefaultRequest = new CreateDefaultConstraintRequest
        {
            ConstraintName = "DF_DefaultTestTable_CreatedDate",
            ColumnName = "CreatedDate",
            DefaultExpression = "GETUTCDATE()",
        };

        var createDefaultResponse = await client.PostAsJsonAsync(
            $"{baseUrl}/DefaultTestTable/defaultconstraints",
            createDefaultRequest
        );
        createDefaultResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdDefault = (
            await createDefaultResponse.Content.ReadFromJsonAsync<DefaultConstraintResponse>()
        )?.Result;
        createdDefault.Should().NotBeNull();
        string.Equals(
                createdDefault!.ConstraintName,
                "DF_DefaultTestTable_CreatedDate",
                StringComparison.OrdinalIgnoreCase
            )
            .Should()
            .BeTrue();

        // Step 4: Create another default constraint for Status
        var createDefault2Request = new CreateDefaultConstraintRequest
        {
            ConstraintName = "DF_DefaultTestTable_Status",
            ColumnName = "Status",
            DefaultExpression = "'Pending'",
        };

        var createDefault2Response = await client.PostAsJsonAsync(
            $"{baseUrl}/DefaultTestTable/defaultconstraints",
            createDefault2Request
        );
        createDefault2Response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Step 5: Create a default constraint for IsActive
        var createDefault3Request = new CreateDefaultConstraintRequest
        {
            ConstraintName = "DF_DefaultTestTable_IsActive",
            ColumnName = "IsActive",
            DefaultExpression = "1",
        };

        var createDefault3Response = await client.PostAsJsonAsync(
            $"{baseUrl}/DefaultTestTable/defaultconstraints",
            createDefault3Request
        );
        createDefault3Response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Step 6: List default constraints after creation
        var defaultsAfterCreateResponse = await client.GetAsync(
            $"{baseUrl}/DefaultTestTable/defaultconstraints"
        );
        defaultsAfterCreateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var defaultsAfterCreate = (
            await defaultsAfterCreateResponse.Content.ReadFromJsonAsync<DefaultConstraintListResponse>()
        )?.Result;
        defaultsAfterCreate.Should().HaveCount(initialDefaultCount + 3);

        // Step 7: Get the specific default constraint
        var getDefaultResponse = await client.GetAsync(
            $"{baseUrl}/DefaultTestTable/defaultconstraints/DF_DefaultTestTable_CreatedDate"
        );
        getDefaultResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var retrievedDefault = (
            await getDefaultResponse.Content.ReadFromJsonAsync<DefaultConstraintResponse>()
        )?.Result;
        retrievedDefault.Should().NotBeNull();
        string.Equals(
                retrievedDefault!.ConstraintName,
                "DF_DefaultTestTable_CreatedDate",
                StringComparison.OrdinalIgnoreCase
            )
            .Should()
            .BeTrue();
        string.Equals(
                retrievedDefault.ColumnName,
                "CreatedDate",
                StringComparison.OrdinalIgnoreCase
            )
            .Should()
            .BeTrue();
        retrievedDefault.DefaultExpression.Should().NotBeNullOrEmpty();

        // Step 8: Drop one default constraint
        var dropDefaultResponse = await client.DeleteAsync(
            $"{baseUrl}/DefaultTestTable/defaultconstraints/DF_DefaultTestTable_CreatedDate"
        );
        dropDefaultResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 9: Verify default constraint was dropped
        var finalDefaultsResponse = await client.GetAsync(
            $"{baseUrl}/DefaultTestTable/defaultconstraints"
        );
        finalDefaultsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var finalDefaults = (
            await finalDefaultsResponse.Content.ReadFromJsonAsync<DefaultConstraintListResponse>()
        )?.Result;
        finalDefaults.Should().HaveCount(initialDefaultCount + 2); // One less than after creation

        // Cleanup: Drop the test table
        await client.DeleteAsync($"{baseUrl}/DefaultTestTable");
    }

    #endregion

    #region Error Scenarios Tests

    [Fact]
    public async Task TableEndpoints_NonExistentDatasource_ReturnsNotFound()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        using var client = factory.CreateClient();

        var baseUrl = "/api/dm/d/NonExistentDatasource/t";

        var response = await client.GetAsync($"{baseUrl}/");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TableEndpoints_NonExistentTable_ReturnsNotFound()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        using var client = factory.CreateClient();

        var datasourceId = TestcontainersAssemblyFixture.DatasourceId_SqlServer;
        var baseUrl = $"/api/dm/d/{datasourceId}/s/dbo/t";

        var response = await client.GetAsync($"{baseUrl}/NonExistentTable");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TableEndpoints_InvalidRequest_ReturnsBadRequest()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        using var client = factory.CreateClient();

        var datasourceId = TestcontainersAssemblyFixture.DatasourceId_SqlServer;
        var baseUrl = $"/api/dm/d/{datasourceId}/s/dbo/t";

        // Try to create a table with no columns
        var invalidRequest = new CreateTableRequest
        {
            TableName = "InvalidTable",
            Columns = [], // Empty columns should cause validation error
        };

        var response = await client.PostAsJsonAsync($"{baseUrl}/", invalidRequest);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion
}
