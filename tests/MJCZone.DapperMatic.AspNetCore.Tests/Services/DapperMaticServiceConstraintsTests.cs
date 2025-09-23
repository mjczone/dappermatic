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
/// Unit tests for DapperMatic service constraint operations.
/// </summary>
public class DapperMaticServiceConstraintsTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private readonly TestcontainersAssemblyFixture _fixture;

    public DapperMaticServiceConstraintsTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    #region Comprehensive Constraint Management Workflow Tests

    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer, "dbo")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql, "public")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql, null)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite, null)]
    public async Task ConstraintManagement_CompleteWorkflow_WorksCorrectly(
        string datasourceId,
        string? schemaName
    )
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        // Create test tables for constraint operations
        var mainTable = await CreateTestTableForConstraints(
            service,
            datasourceId,
            "ConstraintTest",
            schemaName
        );
        var referencedTable = await CreateReferencedTableForConstraints(
            service,
            datasourceId,
            "RefTable",
            schemaName
        );
        mainTable.Should().NotBeNull();
        referencedTable.Should().NotBeNull();

        // === PRIMARY KEY CONSTRAINT TESTS (3 methods) ===

        // Step 1: Create a primary key constraint
        var primaryKeyRequest = new CreatePrimaryKeyRequest
        {
            ConstraintName = "PK_ConstraintTest_Id",
            Columns = ["Id"],
        };

        var primaryKey = await service.CreatePrimaryKeyAsync(
            datasourceId,
            "ConstraintTest",
            primaryKeyRequest,
            schemaName: schemaName
        );
        primaryKey.Should().NotBeNull();
        string.Equals(
                primaryKey!.ConstraintName,
                "PK_ConstraintTest_Id",
                StringComparison.OrdinalIgnoreCase
            )
            .Should()
            .BeTrue();

        // Step 2: Get the primary key
        var retrievedPrimaryKey = await service.GetPrimaryKeyAsync(
            datasourceId,
            "ConstraintTest",
            schemaName: schemaName
        );
        retrievedPrimaryKey.Should().NotBeNull();
        string.Equals(
                retrievedPrimaryKey!.ConstraintName,
                "PK_ConstraintTest_Id",
                StringComparison.OrdinalIgnoreCase
            )
            .Should()
            .BeTrue();

        // === UNIQUE CONSTRAINT TESTS (4 methods) ===

        // Step 3: Create a unique constraint
        var uniqueConstraintRequest = new CreateUniqueConstraintRequest
        {
            ConstraintName = "UQ_ConstraintTest_Email",
            Columns = ["Email"],
        };

        var uniqueConstraint = await service.CreateUniqueConstraintAsync(
            datasourceId,
            "ConstraintTest",
            uniqueConstraintRequest,
            schemaName: schemaName
        );
        uniqueConstraint.Should().NotBeNull();
        string.Equals(
                uniqueConstraint!.ConstraintName,
                "UQ_ConstraintTest_Email",
                StringComparison.OrdinalIgnoreCase
            )
            .Should()
            .BeTrue();

        // Step 4: Get all unique constraints
        var uniqueConstraints = await service.GetUniqueConstraintsAsync(
            datasourceId,
            "ConstraintTest",
            schemaName: schemaName
        );
        uniqueConstraints.Should().NotBeNull().And.NotBeEmpty();
        uniqueConstraints
            .Should()
            .Contain(uc =>
                string.Equals(
                    uc.ConstraintName,
                    "UQ_ConstraintTest_Email",
                    StringComparison.OrdinalIgnoreCase
                )
            );

        // Step 5: Get specific unique constraint
        var specificUniqueConstraint = await service.GetUniqueConstraintAsync(
            datasourceId,
            "ConstraintTest",
            "UQ_ConstraintTest_Email",
            schemaName: schemaName
        );
        specificUniqueConstraint.Should().NotBeNull();
        string.Equals(
                specificUniqueConstraint!.ConstraintName,
                "UQ_ConstraintTest_Email",
                StringComparison.OrdinalIgnoreCase
            )
            .Should()
            .BeTrue();

        // === CHECK CONSTRAINT TESTS (4 methods) - Skip for MySQL as it may not support them ===
        if (datasourceId != TestcontainersAssemblyFixture.DatasourceId_MySql)
        {
            // Step 6: Create a check constraint
            var checkConstraintRequest = new CreateCheckConstraintRequest
            {
                ConstraintName = "CK_ConstraintTest_Age",
                ColumnName = "Age",
                CheckExpression = "Age >= 0 AND Age <= 120",
            };

            var checkConstraint = await service.CreateCheckConstraintAsync(
                datasourceId,
                "ConstraintTest",
                checkConstraintRequest,
                schemaName: schemaName
            );
            checkConstraint.Should().NotBeNull();
            string.Equals(
                    checkConstraint!.ConstraintName,
                    "CK_ConstraintTest_Age",
                    StringComparison.OrdinalIgnoreCase
                )
                .Should()
                .BeTrue();

            // Step 7: Get all check constraints
            var checkConstraints = await service.GetCheckConstraintsAsync(
                datasourceId,
                "ConstraintTest",
                schemaName: schemaName
            );
            checkConstraints.Should().NotBeNull();
            checkConstraints
                .Should()
                .Contain(cc =>
                    string.Equals(
                        cc.ConstraintName,
                        "CK_ConstraintTest_Age",
                        StringComparison.OrdinalIgnoreCase
                    )
                );

            // Step 8: Get specific check constraint
            var specificCheckConstraint = await service.GetCheckConstraintAsync(
                datasourceId,
                "ConstraintTest",
                "CK_ConstraintTest_Age",
                schemaName: schemaName
            );
            specificCheckConstraint.Should().NotBeNull();
            string.Equals(
                    specificCheckConstraint!.ConstraintName,
                    "CK_ConstraintTest_Age",
                    StringComparison.OrdinalIgnoreCase
                )
                .Should()
                .BeTrue();
        }

        // === FOREIGN KEY CONSTRAINT TESTS (4 methods) ===

        // Step 9: Create a foreign key constraint
        var foreignKeyRequest = new CreateForeignKeyRequest
        {
            ConstraintName = "FK_ConstraintTest_CategoryId",
            Columns = ["CategoryId"],
            ReferencedTableName = "RefTable",
            ReferencedColumns = ["Id"],
            OnUpdate = "Cascade",
            OnDelete = "SetNull",
        };

        var foreignKey = await service.CreateForeignKeyAsync(
            datasourceId,
            "ConstraintTest",
            foreignKeyRequest,
            schemaName: schemaName
        );
        foreignKey.Should().NotBeNull();
        string.Equals(
                foreignKey!.ConstraintName,
                "FK_ConstraintTest_CategoryId",
                StringComparison.OrdinalIgnoreCase
            )
            .Should()
            .BeTrue();

        // Step 10: Get all foreign keys
        var foreignKeys = await service.GetForeignKeysAsync(
            datasourceId,
            "ConstraintTest",
            schemaName: schemaName
        );
        foreignKeys.Should().NotBeNull().And.NotBeEmpty();
        foreignKeys
            .Should()
            .Contain(fk =>
                string.Equals(
                    fk.ConstraintName,
                    "FK_ConstraintTest_CategoryId",
                    StringComparison.OrdinalIgnoreCase
                )
            );

        // Step 11: Get specific foreign key
        var specificForeignKey = await service.GetForeignKeyAsync(
            datasourceId,
            "ConstraintTest",
            "FK_ConstraintTest_CategoryId",
            schemaName: schemaName
        );
        specificForeignKey.Should().NotBeNull();
        string.Equals(
                specificForeignKey!.ConstraintName,
                "FK_ConstraintTest_CategoryId",
                StringComparison.OrdinalIgnoreCase
            )
            .Should()
            .BeTrue();

        // === DEFAULT CONSTRAINT TESTS (4 methods) - Skip for some databases that handle defaults differently ===
        if (datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer)
        {
            // Step 12: Create a default constraint
            var defaultConstraintRequest = new CreateDefaultConstraintRequest
            {
                ConstraintName = "DF_ConstraintTest_Status",
                ColumnName = "Status",
                DefaultExpression = "'Active'",
            };

            var defaultConstraint = await service.CreateDefaultConstraintAsync(
                datasourceId,
                "ConstraintTest",
                defaultConstraintRequest,
                schemaName: schemaName
            );
            defaultConstraint.Should().NotBeNull();
            string.Equals(
                    defaultConstraint!.ConstraintName,
                    "DF_ConstraintTest_Status",
                    StringComparison.OrdinalIgnoreCase
                )
                .Should()
                .BeTrue();

            // Step 13: Get all default constraints
            var defaultConstraints = await service.GetDefaultConstraintsAsync(
                datasourceId,
                "ConstraintTest",
                schemaName: schemaName
            );
            defaultConstraints.Should().NotBeNull();
            defaultConstraints
                .Should()
                .Contain(dc =>
                    string.Equals(
                        dc.ConstraintName,
                        "DF_ConstraintTest_Status",
                        StringComparison.OrdinalIgnoreCase
                    )
                );

            // Step 14: Get specific default constraint
            var specificDefaultConstraint = await service.GetDefaultConstraintAsync(
                datasourceId,
                "ConstraintTest",
                "DF_ConstraintTest_Status",
                schemaName: schemaName
            );
            specificDefaultConstraint.Should().NotBeNull();
            string.Equals(
                    specificDefaultConstraint!.ConstraintName,
                    "DF_ConstraintTest_Status",
                    StringComparison.OrdinalIgnoreCase
                )
                .Should()
                .BeTrue();
        }

        // === CONSTRAINT DELETION TESTS ===

        // Step 15: Drop constraints (in proper order to avoid dependency issues)

        // Drop foreign key first
        var dropForeignKeyResult = await service.DropForeignKeyAsync(
            datasourceId,
            "ConstraintTest",
            "FK_ConstraintTest_CategoryId",
            schemaName: schemaName
        );
        dropForeignKeyResult.Should().BeTrue();

        // Drop default constraint (SQL Server only)
        if (datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer)
        {
            var dropDefaultResult = await service.DropDefaultConstraintAsync(
                datasourceId,
                "ConstraintTest",
                "DF_ConstraintTest_Status",
                schemaName: schemaName
            );
            dropDefaultResult.Should().BeTrue();
        }

        // Drop check constraint (non-MySQL)
        if (datasourceId != TestcontainersAssemblyFixture.DatasourceId_MySql)
        {
            var dropCheckResult = await service.DropCheckConstraintAsync(
                datasourceId,
                "ConstraintTest",
                "CK_ConstraintTest_Age",
                schemaName: schemaName
            );
            dropCheckResult.Should().BeTrue();
        }

        // Drop unique constraint
        var dropUniqueResult = await service.DropUniqueConstraintAsync(
            datasourceId,
            "ConstraintTest",
            "UQ_ConstraintTest_Email",
            schemaName: schemaName
        );
        dropUniqueResult.Should().BeTrue();

        // Drop primary key last
        var dropPrimaryKeyResult = await service.DropPrimaryKeyAsync(
            datasourceId,
            "ConstraintTest",
            schemaName: schemaName
        );
        dropPrimaryKeyResult.Should().BeTrue();

        // Step 16: Verify constraints were dropped
        var finalPrimaryKey = await service.GetPrimaryKeyAsync(
            datasourceId,
            "ConstraintTest",
            schemaName: schemaName
        );
        finalPrimaryKey.Should().BeNull();

        var finalUniqueConstraints = await service.GetUniqueConstraintsAsync(
            datasourceId,
            "ConstraintTest",
            schemaName: schemaName
        );
        finalUniqueConstraints
            .Should()
            .NotContain(uc =>
                string.Equals(
                    uc.ConstraintName,
                    "UQ_ConstraintTest_Email",
                    StringComparison.OrdinalIgnoreCase
                )
            );

        var finalForeignKeys = await service.GetForeignKeysAsync(
            datasourceId,
            "ConstraintTest",
            schemaName: schemaName
        );
        finalForeignKeys
            .Should()
            .NotContain(fk =>
                string.Equals(
                    fk.ConstraintName,
                    "FK_ConstraintTest_CategoryId",
                    StringComparison.OrdinalIgnoreCase
                )
            );

        // Cleanup: Drop test tables
        await service.DropTableAsync(datasourceId, "ConstraintTest", schemaName: schemaName);
        await service.DropTableAsync(datasourceId, "RefTable", schemaName: schemaName);
    }

    #endregion

    #region Error Scenario Tests

    [Fact]
    public async Task ConstraintManagement_NonExistentDatasource_ThrowsArgumentException()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var primaryKeyRequest = new CreatePrimaryKeyRequest
        {
            ConstraintName = "PK_Test",
            Columns = ["Id"],
        };
        var foreignKeyRequest = new CreateForeignKeyRequest
        {
            ConstraintName = "FK_Test",
            Columns = ["RefId"],
            ReferencedTableName = "RefTable",
            ReferencedColumns = ["Id"],
        };
        var uniqueConstraintRequest = new CreateUniqueConstraintRequest
        {
            ConstraintName = "UQ_Test",
            Columns = ["Email"],
        };
        var checkConstraintRequest = new CreateCheckConstraintRequest
        {
            ConstraintName = "CK_Test",
            CheckExpression = "Age >= 0",
        };
        var defaultConstraintRequest = new CreateDefaultConstraintRequest
        {
            ConstraintName = "DF_Test",
            ColumnName = "Status",
            DefaultExpression = "'Active'",
        };

        // Test all constraint methods with non-existent datasource
        var expectedMessage = "Datasource 'NonExistent' not found. (Parameter 'datasourceId')";

        // Primary Key methods
        var getPrimaryKeyAct = async () =>
            await service.GetPrimaryKeyAsync("NonExistent", "TestTable");
        await getPrimaryKeyAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        var createPrimaryKeyAct = async () =>
            await service.CreatePrimaryKeyAsync("NonExistent", "TestTable", primaryKeyRequest);
        await createPrimaryKeyAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        var dropPrimaryKeyAct = async () =>
            await service.DropPrimaryKeyAsync("NonExistent", "TestTable");
        await dropPrimaryKeyAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        // Foreign Key methods
        var getForeignKeysAct = async () =>
            await service.GetForeignKeysAsync("NonExistent", "TestTable");
        await getForeignKeysAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        var getForeignKeyAct = async () =>
            await service.GetForeignKeyAsync("NonExistent", "TestTable", "FK_Test");
        await getForeignKeyAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        var createForeignKeyAct = async () =>
            await service.CreateForeignKeyAsync("NonExistent", "TestTable", foreignKeyRequest);
        await createForeignKeyAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        var dropForeignKeyAct = async () =>
            await service.DropForeignKeyAsync("NonExistent", "TestTable", "FK_Test");
        await dropForeignKeyAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        // Unique Constraint methods
        var getUniqueConstraintsAct = async () =>
            await service.GetUniqueConstraintsAsync("NonExistent", "TestTable");
        await getUniqueConstraintsAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        var getUniqueConstraintAct = async () =>
            await service.GetUniqueConstraintAsync("NonExistent", "TestTable", "UQ_Test");
        await getUniqueConstraintAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        var createUniqueConstraintAct = async () =>
            await service.CreateUniqueConstraintAsync(
                "NonExistent",
                "TestTable",
                uniqueConstraintRequest
            );
        await createUniqueConstraintAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        var dropUniqueConstraintAct = async () =>
            await service.DropUniqueConstraintAsync("NonExistent", "TestTable", "UQ_Test");
        await dropUniqueConstraintAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        // Check Constraint methods
        var getCheckConstraintsAct = async () =>
            await service.GetCheckConstraintsAsync("NonExistent", "TestTable");
        await getCheckConstraintsAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        var getCheckConstraintAct = async () =>
            await service.GetCheckConstraintAsync("NonExistent", "TestTable", "CK_Test");
        await getCheckConstraintAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        var createCheckConstraintAct = async () =>
            await service.CreateCheckConstraintAsync(
                "NonExistent",
                "TestTable",
                checkConstraintRequest
            );
        await createCheckConstraintAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        var dropCheckConstraintAct = async () =>
            await service.DropCheckConstraintAsync("NonExistent", "TestTable", "CK_Test");
        await dropCheckConstraintAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        // Default Constraint methods
        var getDefaultConstraintsAct = async () =>
            await service.GetDefaultConstraintsAsync("NonExistent", "TestTable");
        await getDefaultConstraintsAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        var getDefaultConstraintAct = async () =>
            await service.GetDefaultConstraintAsync("NonExistent", "TestTable", "Status");
        await getDefaultConstraintAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        var createDefaultConstraintAct = async () =>
            await service.CreateDefaultConstraintAsync(
                "NonExistent",
                "TestTable",
                defaultConstraintRequest
            );
        await createDefaultConstraintAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        var dropDefaultConstraintAct = async () =>
            await service.DropDefaultConstraintAsync("NonExistent", "TestTable", "Status");
        await dropDefaultConstraintAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);
    }

    [Fact]
    public async Task GetConstraints_NonExistentTable_ReturnsNull()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var primaryKey = await service.GetPrimaryKeyAsync(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "NonExistentTable",
            schemaName: "dbo"
        );
        primaryKey.Should().BeNull();

        var foreignKeyConstraints = await service.GetForeignKeysAsync(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "NonExistentTable",
            schemaName: "dbo"
        );
        foreignKeyConstraints.Should().BeEmpty();

        var uniqueConstraints = await service.GetUniqueConstraintsAsync(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "NonExistentTable",
            schemaName: "dbo"
        );
        uniqueConstraints.Should().BeEmpty();

        var checkConstraints = await service.GetCheckConstraintsAsync(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "NonExistentTable",
            schemaName: "dbo"
        );
        checkConstraints.Should().BeEmpty();

        var defaultConstraints = await service.GetDefaultConstraintsAsync(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "NonExistentTable",
            schemaName: "dbo"
        );
        defaultConstraints.Should().BeEmpty();
    }

    #endregion

    #region Helper Methods

    private static async Task<TableDto?> CreateTestTableForConstraints(
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
                    ColumnName = "Email",
                    ProviderDataType =
                        datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                            ? "nvarchar(255)"
                            : "varchar(255)",
                    IsNullable = false,
                },
                new CreateColumnRequest
                {
                    ColumnName = "Age",
                    ProviderDataType = "int",
                    IsNullable = true,
                },
                new CreateColumnRequest
                {
                    ColumnName = "Status",
                    ProviderDataType =
                        datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                            ? "nvarchar(50)"
                            : "varchar(50)",
                    IsNullable = true,
                },
                new CreateColumnRequest
                {
                    ColumnName = "CategoryId",
                    ProviderDataType = "int",
                    IsNullable = true,
                },
            ],
        };

        return await service.CreateTableAsync(datasourceId, request);
    }

    private static async Task<TableDto?> CreateReferencedTableForConstraints(
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
                    ProviderDataType =
                        datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                            ? "nvarchar(255)"
                            : "varchar(255)",
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
