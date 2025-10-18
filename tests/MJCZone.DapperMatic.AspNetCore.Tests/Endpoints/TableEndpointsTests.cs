// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Models.Responses;
using MJCZone.DapperMatic.AspNetCore.Tests.Factories;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Endpoints;

/// <summary>
/// Integration tests for DapperMatic table REST endpoints.
/// </summary>
public class TableEndpointsTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private readonly TestcontainersAssemblyFixture _fixture;

    public TableEndpointsTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    #region Comprehensive Workflow Tests

    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer, "dbo")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql, "public")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql, null)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite, null)]
    public async Task TableEndpoints_CompleteWorkflow_Success(
        string datasourceId,
        string? schemaName
    )
    {
        var datasources = _fixture.GetTestDatasources().Where(ds => ds.Id == datasourceId).ToList();
        using var factory = new WafWithInMemoryDatasourceRepository(datasources);
        using var client = factory.CreateClient();
        const string tableName = "WorkflowTestTable";

        var baseUrl =
            schemaName != null
                ? $"/api/dm/d/{datasourceId}/s/{schemaName}/t"
                : $"/api/dm/d/{datasourceId}/t";

        // 1. GET MULTI - List all tables (get initial count)
        var listResponse1 = await client.GetAsync($"{baseUrl}/");
        listResponse1.StatusCode.Should().Be(HttpStatusCode.OK);
        var listResult1 = await listResponse1.ReadAsJsonAsync<TableListResponse>();
        listResult1.Should().NotBeNull();
        listResult1!.Result.Should().NotBeNull();
        var initialTableCount = listResult1.Result!.Count();
        listResult1
            .Result.Should()
            .NotContain(t =>
                string.Equals(t.TableName, tableName, StringComparison.OrdinalIgnoreCase)
            );

        // 2. GET SINGLE - Try to get non-existent table (should return 404)
        var getResponse1 = await client.GetAsync($"{baseUrl}/{tableName}");
        getResponse1.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 3. CREATE - Create a new table
        var createRequest = new TableDto
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
            ],
        };
        var createContent = new StringContent(
            JsonSerializer.Serialize(createRequest),
            Encoding.UTF8,
            "application/json"
        );
        var createResponse = await client.PostAsync($"{baseUrl}", createContent);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.ReadAsJsonAsync<TableResponse>();
        createResult.Should().NotBeNull();
        createResult!.Result.Should().NotBeNull();
        string.Equals(createResult.Result!.TableName, tableName, StringComparison.OrdinalIgnoreCase)
            .Should()
            .BeTrue();

        // 4. EXISTS - Check if table exists (should return true)
        var existsResponse1 = await client.GetAsync($"{baseUrl}/{tableName}/exists");
        existsResponse1.StatusCode.Should().Be(HttpStatusCode.OK);
        var existsResult1 = await existsResponse1.ReadAsJsonAsync<TableExistsResponse>();
        existsResult1.Should().NotBeNull();
        existsResult1!.Result.Should().BeTrue();

        // 5. GET MULTI - List tables again (should contain new table)
        var listResponse2 = await client.GetAsync($"{baseUrl}/");
        listResponse2.StatusCode.Should().Be(HttpStatusCode.OK);
        var listResult2 = await listResponse2.ReadAsJsonAsync<TableListResponse>();
        listResult2.Should().NotBeNull();
        listResult2!.Result.Should().NotBeNull();
        listResult2.Result.Should().HaveCount(initialTableCount + 1);
        listResult2
            .Result.Should()
            .Contain(t =>
                string.Equals(t.TableName, tableName, StringComparison.OrdinalIgnoreCase)
            );

        // 6. GET SINGLE - Get the created table (should return table details)
        var getResponse2 = await client.GetAsync($"{baseUrl}/{tableName}");
        getResponse2.StatusCode.Should().Be(HttpStatusCode.OK);
        var getResult2 = await getResponse2.ReadAsJsonAsync<TableResponse>();
        getResult2.Should().NotBeNull();
        getResult2!.Result.Should().NotBeNull();
        string.Equals(getResult2.Result!.TableName, tableName, StringComparison.OrdinalIgnoreCase)
            .Should()
            .BeTrue();
        getResult2.Result.Columns.Should().HaveCount(3);

        // 7. UPDATE - Update the table (rename)
        const string newTableName = "WorkflowTestTableRenamed";
        var updateRequest = new TableDto { TableName = newTableName };
        var updateContent = new StringContent(
            JsonSerializer.Serialize(updateRequest),
            Encoding.UTF8,
            "application/json"
        );
        var updateResponse = await client.PutAsync($"{baseUrl}/{tableName}", updateContent);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updateResult = await updateResponse.ReadAsJsonAsync<TableResponse>();
        updateResult.Should().NotBeNull();
        updateResult!.Result.Should().NotBeNull();

        // 8. GET SINGLE - Get updated table (should show new name)
        var getResponse3 = await client.GetAsync($"{baseUrl}/{newTableName}");
        getResponse3.StatusCode.Should().Be(HttpStatusCode.OK);
        var getResult3 = await getResponse3.ReadAsJsonAsync<TableResponse>();
        getResult3.Should().NotBeNull();
        getResult3!.Result.Should().NotBeNull();
        string.Equals(
                getResult3.Result!.TableName,
                newTableName,
                StringComparison.OrdinalIgnoreCase
            )
            .Should()
            .BeTrue();

        // 9. DELETE - Delete the table
        var deleteResponse = await client.DeleteAsync($"{baseUrl}/{newTableName}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 10. EXISTS - Check if table exists (should return false)
        var existsResponse2 = await client.GetAsync($"{baseUrl}/{newTableName}/exists");
        existsResponse2.StatusCode.Should().Be(HttpStatusCode.OK);
        var existsResult2 = await existsResponse2.ReadAsJsonAsync<TableExistsResponse>();
        existsResult2.Should().NotBeNull();
        existsResult2!.Result.Should().BeFalse();

        // 11. GET MULTI - List tables (should be back to initial count)
        var listResponse3 = await client.GetAsync($"{baseUrl}/");
        listResponse3.StatusCode.Should().Be(HttpStatusCode.OK);
        var listResult3 = await listResponse3.ReadAsJsonAsync<TableListResponse>();
        listResult3.Should().NotBeNull();
        listResult3!.Result.Should().NotBeNull();
        listResult3.Result.Should().HaveCount(initialTableCount);
        listResult3
            .Result.Should()
            .NotContain(t =>
                string.Equals(t.TableName, newTableName, StringComparison.OrdinalIgnoreCase)
            );

        // 12. GET SINGLE - Try to get deleted table (should return 404)
        var getResponse4 = await client.GetAsync($"{baseUrl}/{newTableName}");
        getResponse4.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TableQueryEndpoints_CompleteWorkflow_Success()
    {
        var datasources = _fixture
            .GetTestDatasources()
            .Where(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_SqlServer)
            .ToList();
        using var factory = new WafWithInMemoryDatasourceRepository(datasources);
        using var client = factory.CreateClient();
        const string tableName = "QueryWorkflowTable";
        const string datasourceId = TestcontainersAssemblyFixture.DatasourceId_SqlServer;
        const string schemaName = "dbo";
        var baseUrl = $"/api/dm/d/{datasourceId}/s/{schemaName}/t";

        // Create a test table for querying
        await CreateTestTable(client, baseUrl, tableName, schemaName);

        try
        {
            // Test POST query with pagination
            var postQueryRequest = new QueryDto
            {
                Take = 10,
                Skip = 0,
                IncludeTotal = true,
            };
            var postQueryContent = new StringContent(
                JsonSerializer.Serialize(postQueryRequest),
                Encoding.UTF8,
                "application/json"
            );
            var postQueryResponse = await client.PostAsync(
                $"{baseUrl}/{tableName}/query",
                postQueryContent
            );
            postQueryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var postQueryResult = await postQueryResponse.ReadAsJsonAsync<QueryResponse>();
            postQueryResult.Should().NotBeNull();
            postQueryResult!.Result.Should().NotBeNull();
            postQueryResult.Pagination.Should().NotBeNull();
            postQueryResult.Pagination.Take.Should().Be(10);

            // Test GET query with parameters
            var getQueryResponse = await client.GetAsync(
                $"{baseUrl}/{tableName}/query?take=5&skip=0"
            );
            getQueryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var getQueryResult = await getQueryResponse.ReadAsJsonAsync<QueryResponse>();
            getQueryResult.Should().NotBeNull();
            getQueryResult!.Result.Should().NotBeNull();
            getQueryResult.Pagination.Should().NotBeNull();
            getQueryResult.Pagination.Take.Should().Be(5);

            // Test GET query with column selection
            var selectQueryResponse = await client.GetAsync(
                $"{baseUrl}/{tableName}/query?select=Id,Name&take=10"
            );
            selectQueryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var selectQueryResult = await selectQueryResponse.ReadAsJsonAsync<QueryResponse>();
            selectQueryResult.Should().NotBeNull();
            selectQueryResult!.Result.Should().NotBeNull();
        }
        finally
        {
            // Clean up: Delete the test table
            await client.DeleteAsync($"{baseUrl}/{tableName}");
        }
    }

    #endregion

    #region Column Management Workflow Tests

    [Fact]
    public async Task ColumnEndpoints_CompleteWorkflow_Success()
    {
        var datasources = _fixture
            .GetTestDatasources()
            .Where(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_SqlServer)
            .ToList();
        using var factory = new WafWithInMemoryDatasourceRepository(datasources);
        using var client = factory.CreateClient();
        const string tableName = "ColumnTestTable";
        const string datasourceId = TestcontainersAssemblyFixture.DatasourceId_SqlServer;
        const string schemaName = "dbo";
        var baseUrl = $"/api/dm/d/{datasourceId}/s/{schemaName}/t";

        // Create a test table first
        await CreateTestTable(client, baseUrl, tableName, schemaName);

        try
        {
            // 1. List initial columns
            var initialColumnsResponse = await client.GetAsync($"{baseUrl}/{tableName}/columns");
            initialColumnsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var initialColumns = await initialColumnsResponse.ReadAsJsonAsync<ColumnListResponse>();
            initialColumns.Should().NotBeNull();
            initialColumns!.Result.Should().NotBeNull();
            var initialColumnCount = initialColumns.Result!.Count();
            initialColumnCount.Should().BeGreaterThan(0); // Should have columns from table creation

            // 2. Get a specific column
            var getColumnResponse = await client.GetAsync($"{baseUrl}/{tableName}/columns/Name");
            getColumnResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var column = await getColumnResponse.ReadAsJsonAsync<ColumnResponse>();
            column.Should().NotBeNull();
            column!.Result.Should().NotBeNull();
            string.Equals(column.Result!.ColumnName, "Name", StringComparison.OrdinalIgnoreCase)
                .Should()
                .BeTrue();

            // 3. Add a new column
            var addColumnRequest = new ColumnDto
            {
                ColumnName = "PhoneNumber",
                ProviderDataType = "nvarchar(20)",
                IsNullable = true,
            };
            var addColumnContent = new StringContent(
                JsonSerializer.Serialize(addColumnRequest),
                Encoding.UTF8,
                "application/json"
            );
            var addColumnResponse = await client.PostAsync(
                $"{baseUrl}/{tableName}/columns",
                addColumnContent
            );
            addColumnResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            // 4. Verify column was added
            var columnsAfterAddResponse = await client.GetAsync($"{baseUrl}/{tableName}/columns");
            columnsAfterAddResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var columnsAfterAdd =
                await columnsAfterAddResponse.ReadAsJsonAsync<ColumnListResponse>();
            columnsAfterAdd.Should().NotBeNull();
            columnsAfterAdd!.Result.Should().NotBeNull();
            columnsAfterAdd.Result.Should().HaveCount(initialColumnCount + 1);

            // 5. Update/Rename the column
            var updateColumnRequest = new ColumnDto { ColumnName = "PhoneNumberUpdated" };
            var updateContent = new StringContent(
                JsonSerializer.Serialize(updateColumnRequest),
                Encoding.UTF8,
                "application/json"
            );
            var updateResponse = await client.PutAsync(
                $"{baseUrl}/{tableName}/columns/PhoneNumber",
                updateContent
            );
            updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // 6. Drop the added column
            var dropColumnResponse = await client.DeleteAsync(
                $"{baseUrl}/{tableName}/columns/PhoneNumberUpdated"
            );
            dropColumnResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            // 7. Verify final column count
            var finalColumnsResponse = await client.GetAsync($"{baseUrl}/{tableName}/columns");
            finalColumnsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var finalColumns = await finalColumnsResponse.ReadAsJsonAsync<ColumnListResponse>();
            finalColumns.Should().NotBeNull();
            finalColumns!.Result.Should().NotBeNull();
            finalColumns.Result.Should().HaveCount(initialColumnCount);
        }
        finally
        {
            // Clean up: Drop the test table
            await client.DeleteAsync($"{baseUrl}/{tableName}");
        }
    }

    #endregion

    #region Index Management Workflow Tests

    [Fact]
    public async Task IndexEndpoints_CompleteWorkflow_Success()
    {
        var datasources = _fixture
            .GetTestDatasources()
            .Where(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_SqlServer)
            .ToList();
        using var factory = new WafWithInMemoryDatasourceRepository(datasources);
        using var client = factory.CreateClient();
        const string tableName = "IndexTestTable";
        const string datasourceId = TestcontainersAssemblyFixture.DatasourceId_SqlServer;
        const string schemaName = "dbo";
        var baseUrl = $"/api/dm/d/{datasourceId}/s/{schemaName}/t";

        // Create a test table with primary key first
        await CreateTestTableWithPrimaryKey(client, baseUrl, tableName, schemaName);

        try
        {
            // 1. List initial indexes (may include auto-created ones)
            var initialIndexesResponse = await client.GetAsync($"{baseUrl}/{tableName}/indexes");
            initialIndexesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var initialIndexes = await initialIndexesResponse.ReadAsJsonAsync<IndexListResponse>();
            initialIndexes.Should().NotBeNull();
            initialIndexes!.Result.Should().NotBeNull();
            var initialIndexCount = initialIndexes.Result!.Count();

            // 2. Create a new index
            var createIndexRequest = new IndexDto
            {
                IndexName = "IX_IndexTestTable_Name",
                ColumnNames = ["Name"],
                IsUnique = false,
            };
            var createIndexContent = new StringContent(
                JsonSerializer.Serialize(createIndexRequest),
                Encoding.UTF8,
                "application/json"
            );
            var createIndexResponse = await client.PostAsync(
                $"{baseUrl}/{tableName}/indexes",
                createIndexContent
            );
            createIndexResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            // 3. List indexes after creation
            var indexesAfterCreateResponse = await client.GetAsync(
                $"{baseUrl}/{tableName}/indexes"
            );
            indexesAfterCreateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var indexesAfterCreate =
                await indexesAfterCreateResponse.ReadAsJsonAsync<IndexListResponse>();
            indexesAfterCreate.Should().NotBeNull();
            indexesAfterCreate!.Result.Should().NotBeNull();
            indexesAfterCreate.Result.Should().HaveCount(initialIndexCount + 1);

            // 4. Get the specific index
            var getIndexResponse = await client.GetAsync(
                $"{baseUrl}/{tableName}/indexes/IX_IndexTestTable_Name"
            );
            getIndexResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var index = await getIndexResponse.ReadAsJsonAsync<IndexResponse>();
            index.Should().NotBeNull();
            index!.Result.Should().NotBeNull();
            string.Equals(
                    index.Result!.IndexName,
                    "IX_IndexTestTable_Name",
                    StringComparison.OrdinalIgnoreCase
                )
                .Should()
                .BeTrue();

            // 5. Drop the index
            var dropIndexResponse = await client.DeleteAsync(
                $"{baseUrl}/{tableName}/indexes/IX_IndexTestTable_Name"
            );
            dropIndexResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            // 6. Verify index was dropped
            var finalIndexesResponse = await client.GetAsync($"{baseUrl}/{tableName}/indexes");
            finalIndexesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var finalIndexes = await finalIndexesResponse.ReadAsJsonAsync<IndexListResponse>();
            finalIndexes.Should().NotBeNull();
            finalIndexes!.Result.Should().NotBeNull();
            finalIndexes.Result.Should().HaveCount(initialIndexCount);
        }
        finally
        {
            // Clean up: Drop the test table
            await client.DeleteAsync($"{baseUrl}/{tableName}");
        }
    }

    #endregion

    #region Constraint Management Workflow Tests

    [Fact]
    public async Task ConstraintEndpoints_CompleteWorkflow_Success()
    {
        var datasources = _fixture
            .GetTestDatasources()
            .Where(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_SqlServer)
            .ToList();
        using var factory = new WafWithInMemoryDatasourceRepository(datasources);
        using var client = factory.CreateClient();
        const string tableName = "ConstraintTestTable";
        const string datasourceId = TestcontainersAssemblyFixture.DatasourceId_SqlServer;
        const string schemaName = "dbo";
        var baseUrl = $"/api/dm/d/{datasourceId}/s/{schemaName}/t";

        // Create a test table without constraints first
        await CreateTestTableWithoutConstraints(client, baseUrl, tableName, schemaName);

        try
        {
            // Test Primary Key workflow
            await TestPrimaryKeyWorkflow(client, baseUrl, tableName);

            // Test Check Constraint workflow
            await TestCheckConstraintWorkflow(client, baseUrl, tableName);

            // Test Unique Constraint workflow
            await TestUniqueConstraintWorkflow(client, baseUrl, tableName);

            // Test Default Constraint workflow
            await TestDefaultConstraintWorkflow(client, baseUrl, tableName);
        }
        finally
        {
            // Clean up: Drop the test table
            await client.DeleteAsync($"{baseUrl}/{tableName}");
        }
    }

    #endregion

    #region Error Scenarios Tests

    [Fact]
    public async Task TableEndpoints_ErrorScenarios_HandledCorrectly()
    {
        var datasources = _fixture
            .GetTestDatasources()
            .Where(ds => ds.Id == TestcontainersAssemblyFixture.DatasourceId_SqlServer)
            .ToList();
        using var factory = new WafWithInMemoryDatasourceRepository(datasources);
        using var client = factory.CreateClient();
        const string datasourceId = TestcontainersAssemblyFixture.DatasourceId_SqlServer;
        const string schemaName = "dbo";
        var baseUrl = $"/api/dm/d/{datasourceId}/s/{schemaName}/t";

        // Test non-existent datasource
        var nonExistentDatasourceResponse = await client.GetAsync("/api/dm/d/NonExistent/t/");
        nonExistentDatasourceResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Test non-existent table
        var nonExistentTableResponse = await client.GetAsync($"{baseUrl}/NonExistentTable");
        nonExistentTableResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Test invalid table creation (no columns)
        var invalidCreateRequest = new TableDto
        {
            TableName = "InvalidTable",
            Columns = [], // Empty columns should cause validation error
        };
        var invalidCreateContent = new StringContent(
            JsonSerializer.Serialize(invalidCreateRequest),
            Encoding.UTF8,
            "application/json"
        );
        var invalidCreateResponse = await client.PostAsync($"{baseUrl}", invalidCreateContent);
        invalidCreateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Test duplicate table creation
        const string duplicateTableName = "DuplicateTestTable";
        await CreateTestTable(client, baseUrl, duplicateTableName, schemaName);

        try
        {
            var duplicateCreateRequest = new TableDto
            {
                TableName = duplicateTableName,
                SchemaName = schemaName,
                Columns =
                [
                    new ColumnDto
                    {
                        ColumnName = "Id",
                        ProviderDataType = "int",
                        IsNullable = false,
                    },
                ],
            };
            var duplicateCreateContent = new StringContent(
                JsonSerializer.Serialize(duplicateCreateRequest),
                Encoding.UTF8,
                "application/json"
            );
            var duplicateCreateResponse = await client.PostAsync(
                $"{baseUrl}",
                duplicateCreateContent
            );
            duplicateCreateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }
        finally
        {
            // Clean up
            await client.DeleteAsync($"{baseUrl}/{duplicateTableName}");
        }
    }

    #endregion

    #region Helper Methods

    private static async Task CreateTestTable(
        HttpClient client,
        string baseUrl,
        string tableName,
        string? schemaName
    )
    {
        var createRequest = new TableDto
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
                    ProviderDataType = "nvarchar(255)",
                    IsNullable = true,
                },
                new ColumnDto
                {
                    ColumnName = "Email",
                    ProviderDataType = "nvarchar(255)",
                    IsNullable = false,
                },
            ],
        };
        var content = new StringContent(
            JsonSerializer.Serialize(createRequest),
            Encoding.UTF8,
            "application/json"
        );

        var response = await client.PostAsync($"{baseUrl}", content);
        response.EnsureSuccessStatusCode();
    }

    private static async Task CreateTestTableWithPrimaryKey(
        HttpClient client,
        string baseUrl,
        string tableName,
        string? schemaName
    )
    {
        var createRequest = new TableDto
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
                    ProviderDataType = "nvarchar(255)",
                    IsNullable = true,
                },
                new ColumnDto
                {
                    ColumnName = "Email",
                    ProviderDataType = "nvarchar(255)",
                    IsNullable = false,
                },
            ],
            PrimaryKeyConstraint = new PrimaryKeyConstraintDto
            {
                ConstraintName = $"PK_{tableName}",
                ColumnNames = ["Id"],
            },
        };
        var content = new StringContent(
            JsonSerializer.Serialize(createRequest),
            Encoding.UTF8,
            "application/json"
        );

        var response = await client.PostAsync($"{baseUrl}", content);
        response.EnsureSuccessStatusCode();
    }

    private static async Task CreateTestTableWithoutConstraints(
        HttpClient client,
        string baseUrl,
        string tableName,
        string? schemaName
    )
    {
        var createRequest = new TableDto
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
                    ProviderDataType = "nvarchar(255)",
                    IsNullable = true,
                },
                new ColumnDto
                {
                    ColumnName = "Email",
                    ProviderDataType = "nvarchar(255)",
                    IsNullable = false,
                },
                new ColumnDto
                {
                    ColumnName = "Age",
                    ProviderDataType = "int",
                    IsNullable = true,
                },
                new ColumnDto
                {
                    ColumnName = "CreatedDate",
                    ProviderDataType = "datetime2",
                    IsNullable = false,
                },
            ],
        };
        var content = new StringContent(
            JsonSerializer.Serialize(createRequest),
            Encoding.UTF8,
            "application/json"
        );

        var response = await client.PostAsync($"{baseUrl}", content);
        response.EnsureSuccessStatusCode();
    }

    private static async Task TestPrimaryKeyWorkflow(
        HttpClient client,
        string baseUrl,
        string tableName
    )
    {
        // 1. Verify no primary key exists initially
        var initialPkResponse = await client.GetAsync(
            $"{baseUrl}/{tableName}/primary-key-constraint"
        );
        initialPkResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var retrievedPk = await initialPkResponse.ReadAsJsonAsync<PrimaryKeyResponse>();
        retrievedPk.Should().NotBeNull();
        retrievedPk!.Result.Should().BeNull();

        // 2. Create a primary key constraint
        var createPkRequest = new PrimaryKeyConstraintDto
        {
            ConstraintName = $"PK_{tableName}",
            ColumnNames = ["Id"],
        };
        var createPkContent = new StringContent(
            JsonSerializer.Serialize(createPkRequest),
            Encoding.UTF8,
            "application/json"
        );
        var createPkResponse = await client.PostAsync(
            $"{baseUrl}/{tableName}/primary-key-constraint",
            createPkContent
        );
        createPkResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // 3. Get the primary key constraint
        var getPkResponse = await client.GetAsync($"{baseUrl}/{tableName}/primary-key-constraint");
        getPkResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        retrievedPk = await getPkResponse.ReadAsJsonAsync<PrimaryKeyResponse>();
        retrievedPk.Should().NotBeNull();
        retrievedPk!.Result.Should().NotBeNull();
        string.Equals(
                retrievedPk.Result!.ConstraintName,
                $"PK_{tableName}",
                StringComparison.OrdinalIgnoreCase
            )
            .Should()
            .BeTrue();

        // 4. Drop the primary key constraint
        var dropPkResponse = await client.DeleteAsync(
            $"{baseUrl}/{tableName}/primary-key-constraint"
        );
        dropPkResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 5. Verify primary key was dropped
        var finalPkResponse = await client.GetAsync(
            $"{baseUrl}/{tableName}/primary-key-constraint"
        );
        retrievedPk = await finalPkResponse.ReadAsJsonAsync<PrimaryKeyResponse>();
        retrievedPk.Should().NotBeNull();
        retrievedPk!.Result.Should().BeNull();
    }

    private static async Task TestCheckConstraintWorkflow(
        HttpClient client,
        string baseUrl,
        string tableName
    )
    {
        // 1. List initial check constraints
        var initialChecksResponse = await client.GetAsync(
            $"{baseUrl}/{tableName}/check-constraints"
        );
        initialChecksResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var initialChecks =
            await initialChecksResponse.ReadAsJsonAsync<CheckConstraintListResponse>();
        var initialCheckCount = initialChecks?.Result?.Count() ?? 0;

        // 2. Create a check constraint
        var createCheckRequest = new CheckConstraintDto
        {
            ConstraintName = $"CHK_{tableName}_Age",
            CheckExpression = "[Age] >= 0 AND [Age] <= 150",
        };
        var createCheckContent = new StringContent(
            JsonSerializer.Serialize(createCheckRequest),
            Encoding.UTF8,
            "application/json"
        );
        var createCheckResponse = await client.PostAsync(
            $"{baseUrl}/{tableName}/check-constraints",
            createCheckContent
        );
        createCheckResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // 3. List check constraints after creation
        var checksAfterCreateResponse = await client.GetAsync(
            $"{baseUrl}/{tableName}/check-constraints"
        );
        checksAfterCreateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var checksAfterCreate =
            await checksAfterCreateResponse.ReadAsJsonAsync<CheckConstraintListResponse>();
        checksAfterCreate.Should().NotBeNull();
        checksAfterCreate!.Result.Should().NotBeNull();
        checksAfterCreate.Result.Should().HaveCount(initialCheckCount + 1);

        // 4. Drop the check constraint
        var dropCheckResponse = await client.DeleteAsync(
            $"{baseUrl}/{tableName}/check-constraints/CHK_{tableName}_Age"
        );
        dropCheckResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private static async Task TestUniqueConstraintWorkflow(
        HttpClient client,
        string baseUrl,
        string tableName
    )
    {
        // 1. List initial unique constraints
        var initialUniquesResponse = await client.GetAsync(
            $"{baseUrl}/{tableName}/unique-constraints"
        );
        initialUniquesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var initialUniques =
            await initialUniquesResponse.ReadAsJsonAsync<UniqueConstraintListResponse>();
        var initialUniqueCount = initialUniques?.Result?.Count() ?? 0;

        // 2. Create a unique constraint
        var createUniqueRequest = new UniqueConstraintDto
        {
            ConstraintName = $"UQ_{tableName}_Email",
            ColumnNames = ["Email"],
        };
        var createUniqueContent = new StringContent(
            JsonSerializer.Serialize(createUniqueRequest),
            Encoding.UTF8,
            "application/json"
        );
        var createUniqueResponse = await client.PostAsync(
            $"{baseUrl}/{tableName}/unique-constraints",
            createUniqueContent
        );
        createUniqueResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // 3. List unique constraints after creation
        var uniquesAfterCreateResponse = await client.GetAsync(
            $"{baseUrl}/{tableName}/unique-constraints"
        );
        uniquesAfterCreateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var uniquesAfterCreate =
            await uniquesAfterCreateResponse.ReadAsJsonAsync<UniqueConstraintListResponse>();
        uniquesAfterCreate.Should().NotBeNull();
        uniquesAfterCreate!.Result.Should().NotBeNull();
        uniquesAfterCreate.Result.Should().HaveCount(initialUniqueCount + 1);

        // 4. Drop the unique constraint
        var dropUniqueResponse = await client.DeleteAsync(
            $"{baseUrl}/{tableName}/unique-constraints/UQ_{tableName}_Email"
        );
        dropUniqueResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private static async Task TestDefaultConstraintWorkflow(
        HttpClient client,
        string baseUrl,
        string tableName
    )
    {
        // 1. List initial default constraints
        var initialDefaultsResponse = await client.GetAsync(
            $"{baseUrl}/{tableName}/default-constraints"
        );
        initialDefaultsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var initialDefaults =
            await initialDefaultsResponse.ReadAsJsonAsync<DefaultConstraintListResponse>();
        var initialDefaultCount = initialDefaults?.Result?.Count() ?? 0;

        // 2. Create a default constraint
        var createDefaultRequest = new DefaultConstraintDto
        {
            ConstraintName = $"DF_{tableName}_CreatedDate",
            ColumnName = "CreatedDate",
            DefaultExpression = "GETUTCDATE()",
        };
        var createDefaultContent = new StringContent(
            JsonSerializer.Serialize(createDefaultRequest),
            Encoding.UTF8,
            "application/json"
        );
        var createDefaultResponse = await client.PostAsync(
            $"{baseUrl}/{tableName}/default-constraints",
            createDefaultContent
        );
        createDefaultResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // 3. List default constraints after creation
        var defaultsAfterCreateResponse = await client.GetAsync(
            $"{baseUrl}/{tableName}/default-constraints"
        );
        defaultsAfterCreateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var defaultsAfterCreate =
            await defaultsAfterCreateResponse.ReadAsJsonAsync<DefaultConstraintListResponse>();
        defaultsAfterCreate.Should().NotBeNull();
        defaultsAfterCreate!.Result.Should().NotBeNull();
        defaultsAfterCreate.Result.Should().HaveCount(initialDefaultCount + 1);

        // 4. Drop the default constraint
        var dropDefaultResponse = await client.DeleteAsync(
            $"{baseUrl}/{tableName}/default-constraints/DF_{tableName}_CreatedDate"
        );
        dropDefaultResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    #endregion
}
