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
        var primaryKeyRequest = new PrimaryKeyConstraintDto
        {
            ConstraintName = "PK_ConstraintTest_Id",
            ColumnNames = ["Id"],
        };

        var primaryKeyCreateContext = OperationIdentifiers.ForPrimaryKeyCreate(
            datasourceId,
            "ConstraintTest",
            primaryKeyRequest,
            schemaName
        );
        var primaryKey = await service.CreatePrimaryKeyAsync(
            primaryKeyCreateContext,
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
        var primaryKeyGetContext = OperationIdentifiers.ForPrimaryKeyGet(
            datasourceId,
            "ConstraintTest",
            schemaName
        );
        var retrievedPrimaryKey = await service.GetPrimaryKeyAsync(
            primaryKeyGetContext,
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
        var uniqueConstraintRequest = new UniqueConstraintDto
        {
            ConstraintName = "UQ_ConstraintTest_Email",
            ColumnNames = ["Email"],
        };

        var uniqueCreateContext = OperationIdentifiers.ForUniqueConstraintCreate(
            datasourceId,
            "ConstraintTest",
            uniqueConstraintRequest,
            schemaName
        );
        var uniqueConstraint = await service.CreateUniqueConstraintAsync(
            uniqueCreateContext,
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
        var uniqueListContext = OperationIdentifiers.ForUniqueConstraintList(
            datasourceId,
            "ConstraintTest",
            schemaName
        );
        var uniqueConstraints = await service.GetUniqueConstraintsAsync(
            uniqueListContext,
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
        var uniqueGetContext = OperationIdentifiers.ForUniqueConstraintGet(
            datasourceId,
            "ConstraintTest",
            "UQ_ConstraintTest_Email",
            schemaName
        );
        var specificUniqueConstraint = await service.GetUniqueConstraintAsync(
            uniqueGetContext,
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
            var checkConstraintRequest = new CheckConstraintDto
            {
                ConstraintName = "CK_ConstraintTest_Age",
                ColumnName = "Age",
                CheckExpression = "Age >= 0 AND Age <= 120",
            };

            var checkCreateContext = OperationIdentifiers.ForCheckConstraintCreate(
                datasourceId,
                "ConstraintTest",
                checkConstraintRequest,
                schemaName
            );
            var checkConstraint = await service.CreateCheckConstraintAsync(
                checkCreateContext,
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
            var checkListContext = OperationIdentifiers.ForCheckConstraintList(
                datasourceId,
                "ConstraintTest",
                schemaName
            );
            var checkConstraints = await service.GetCheckConstraintsAsync(
                checkListContext,
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
            var checkGetContext = OperationIdentifiers.ForCheckConstraintGet(
                datasourceId,
                "ConstraintTest",
                "CK_ConstraintTest_Age",
                schemaName
            );
            var specificCheckConstraint = await service.GetCheckConstraintAsync(
                checkGetContext,
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
        var foreignKeyRequest = new ForeignKeyConstraintDto
        {
            ConstraintName = "FK_ConstraintTest_CategoryId",
            ColumnNames = ["CategoryId"],
            ReferencedTableName = "RefTable",
            ReferencedColumnNames = ["Id"],
            OnUpdate = "Cascade",
            OnDelete = "SetNull",
        };

        var foreignKeyCreateContext = OperationIdentifiers.ForForeignKeyCreate(
            datasourceId,
            "ConstraintTest",
            foreignKeyRequest,
            schemaName
        );
        var foreignKey = await service.CreateForeignKeyAsync(
            foreignKeyCreateContext,
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
        var foreignKeyListContext = OperationIdentifiers.ForForeignKeyList(
            datasourceId,
            "ConstraintTest",
            schemaName
        );
        var foreignKeys = await service.GetForeignKeysAsync(
            foreignKeyListContext,
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
        var foreignKeyGetContext = OperationIdentifiers.ForForeignKeyGet(
            datasourceId,
            "ConstraintTest",
            "FK_ConstraintTest_CategoryId",
            schemaName
        );
        var specificForeignKey = await service.GetForeignKeyAsync(
            foreignKeyGetContext,
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
            var defaultConstraintRequest = new DefaultConstraintDto
            {
                ConstraintName = "DF_ConstraintTest_Status",
                ColumnName = "Status",
                DefaultExpression = "'Active'",
            };

            var defaultCreateContext = OperationIdentifiers.ForDefaultConstraintCreate(
                datasourceId,
                "ConstraintTest",
                defaultConstraintRequest,
                schemaName
            );
            var defaultConstraint = await service.CreateDefaultConstraintAsync(
                defaultCreateContext,
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
            var defaultListContext = OperationIdentifiers.ForDefaultConstraintList(
                datasourceId,
                "ConstraintTest",
                schemaName
            );
            var defaultConstraints = await service.GetDefaultConstraintsAsync(
                defaultListContext,
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
            var defaultGetContext = OperationIdentifiers.ForDefaultConstraintGet(
                datasourceId,
                "ConstraintTest",
                "DF_ConstraintTest_Status",
                schemaName
            );
            var specificDefaultConstraint = await service.GetDefaultConstraintAsync(
                defaultGetContext,
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
        var dropForeignKeyContext = OperationIdentifiers.ForForeignKeyDrop(
            datasourceId,
            "ConstraintTest",
            "FK_ConstraintTest_CategoryId",
            schemaName
        );
        await service.DropForeignKeyAsync(
            dropForeignKeyContext,
            datasourceId,
            "ConstraintTest",
            "FK_ConstraintTest_CategoryId",
            schemaName: schemaName
        );

        // Drop default constraint (SQL Server only)
        if (datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer)
        {
            var dropDefaultContext = OperationIdentifiers.ForDefaultConstraintDrop(
                datasourceId,
                "ConstraintTest",
                "DF_ConstraintTest_Status",
                schemaName
            );
            await service.DropDefaultConstraintAsync(
                dropDefaultContext,
                datasourceId,
                "ConstraintTest",
                "DF_ConstraintTest_Status",
                schemaName: schemaName
            );
        }

        // Drop check constraint (non-MySQL)
        if (datasourceId != TestcontainersAssemblyFixture.DatasourceId_MySql)
        {
            var dropCheckContext = OperationIdentifiers.ForCheckConstraintDrop(
                datasourceId,
                "ConstraintTest",
                "CK_ConstraintTest_Age",
                schemaName
            );
            await service.DropCheckConstraintAsync(
                dropCheckContext,
                datasourceId,
                "ConstraintTest",
                "CK_ConstraintTest_Age",
                schemaName: schemaName
            );
        }

        // Drop unique constraint
        var dropUniqueContext = OperationIdentifiers.ForUniqueConstraintDrop(
            datasourceId,
            "ConstraintTest",
            "UQ_ConstraintTest_Email",
            schemaName
        );
        await service.DropUniqueConstraintAsync(
            dropUniqueContext,
            datasourceId,
            "ConstraintTest",
            "UQ_ConstraintTest_Email",
            schemaName: schemaName
        );

        // Drop primary key last
        var dropPrimaryKeyContext = OperationIdentifiers.ForPrimaryKeyDrop(
            datasourceId,
            "ConstraintTest",
            schemaName
        );
        await service.DropPrimaryKeyAsync(
            dropPrimaryKeyContext,
            datasourceId,
            "ConstraintTest",
            schemaName: schemaName
        );

        // Step 16: Verify constraints were dropped
        var finalPrimaryKeyContext = OperationIdentifiers.ForPrimaryKeyGet(
            datasourceId,
            "ConstraintTest",
            schemaName
        );
        var finalPrimaryKeyAct = async () =>
            await service.GetPrimaryKeyAsync(
                finalPrimaryKeyContext,
                datasourceId,
                "ConstraintTest",
                schemaName: schemaName
            );
        await finalPrimaryKeyAct.Should().ThrowAsync<KeyNotFoundException>();

        var finalUniqueConstraintsContext = OperationIdentifiers.ForUniqueConstraintList(
            datasourceId,
            "ConstraintTest",
            schemaName
        );
        var finalUniqueConstraints = await service.GetUniqueConstraintsAsync(
            finalUniqueConstraintsContext,
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

        var finalForeignKeysContext = OperationIdentifiers.ForForeignKeyList(
            datasourceId,
            "ConstraintTest",
            schemaName
        );
        var finalForeignKeys = await service.GetForeignKeysAsync(
            finalForeignKeysContext,
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
        var dropTableContext1 = OperationIdentifiers.ForTableDrop(
            datasourceId,
            "ConstraintTest",
            schemaName
        );
        await service.DropTableAsync(
            dropTableContext1,
            datasourceId,
            "ConstraintTest",
            schemaName: schemaName
        );
        var dropTableContext2 = OperationIdentifiers.ForTableDrop(
            datasourceId,
            "RefTable",
            schemaName
        );
        await service.DropTableAsync(
            dropTableContext2,
            datasourceId,
            "RefTable",
            schemaName: schemaName
        );
    }

    #endregion

    #region Error Scenario Tests

    [Fact]
    public async Task ConstraintManagement_NonExistentDatasource_ThrowsArgumentException()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var primaryKeyRequest = new PrimaryKeyConstraintDto
        {
            ConstraintName = "PK_Test",
            ColumnNames = ["Id"],
        };
        var foreignKeyRequest = new ForeignKeyConstraintDto
        {
            ConstraintName = "FK_Test",
            ColumnNames = ["RefId"],
            ReferencedTableName = "RefTable",
            ReferencedColumnNames = ["Id"],
        };
        var uniqueConstraintRequest = new UniqueConstraintDto
        {
            ConstraintName = "UQ_Test",
            ColumnNames = ["Email"],
        };
        var checkConstraintRequest = new CheckConstraintDto
        {
            ConstraintName = "CK_Test",
            CheckExpression = "Age >= 0",
        };
        var defaultConstraintRequest = new DefaultConstraintDto
        {
            ConstraintName = "DF_Test",
            ColumnName = "Status",
            DefaultExpression = "'Active'",
        };

        // Test all constraint methods with non-existent datasource
        var expectedMessage = "Datasource 'NonExistent' not found. (Parameter 'datasourceId')";

        // Primary Key methods
        var getPrimaryKeyContext = OperationIdentifiers.ForPrimaryKeyGet(
            "NonExistent",
            "TestTable"
        );
        var getPrimaryKeyAct = async () =>
            await service.GetPrimaryKeyAsync(getPrimaryKeyContext, "NonExistent", "TestTable");
        await getPrimaryKeyAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        var createPrimaryKeyContext = OperationIdentifiers.ForPrimaryKeyCreate(
            "NonExistent",
            "TestTable",
            primaryKeyRequest
        );
        var createPrimaryKeyAct = async () =>
            await service.CreatePrimaryKeyAsync(
                createPrimaryKeyContext,
                "NonExistent",
                "TestTable",
                primaryKeyRequest
            );
        await createPrimaryKeyAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        var dropPrimaryKeyContext = OperationIdentifiers.ForPrimaryKeyDrop(
            "NonExistent",
            "TestTable"
        );
        var dropPrimaryKeyAct = async () =>
            await service.DropPrimaryKeyAsync(dropPrimaryKeyContext, "NonExistent", "TestTable");
        await dropPrimaryKeyAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        // Foreign Key methods
        var getForeignKeysContext = OperationIdentifiers.ForForeignKeyList(
            "NonExistent",
            "TestTable"
        );
        var getForeignKeysAct = async () =>
            await service.GetForeignKeysAsync(getForeignKeysContext, "NonExistent", "TestTable");
        await getForeignKeysAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        var getForeignKeyContext = OperationIdentifiers.ForForeignKeyGet(
            "NonExistent",
            "TestTable",
            "FK_Test"
        );
        var getForeignKeyAct = async () =>
            await service.GetForeignKeyAsync(
                getForeignKeyContext,
                "NonExistent",
                "TestTable",
                "FK_Test"
            );
        await getForeignKeyAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        var createForeignKeyContext = OperationIdentifiers.ForForeignKeyCreate(
            "NonExistent",
            "TestTable",
            foreignKeyRequest
        );
        var createForeignKeyAct = async () =>
            await service.CreateForeignKeyAsync(
                createForeignKeyContext,
                "NonExistent",
                "TestTable",
                foreignKeyRequest
            );
        await createForeignKeyAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        var dropForeignKeyContext = OperationIdentifiers.ForForeignKeyDrop(
            "NonExistent",
            "TestTable",
            "FK_Test"
        );
        var dropForeignKeyAct = async () =>
            await service.DropForeignKeyAsync(
                dropForeignKeyContext,
                "NonExistent",
                "TestTable",
                "FK_Test"
            );
        await dropForeignKeyAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        // Unique Constraint methods
        var getUniqueConstraintsContext = OperationIdentifiers.ForUniqueConstraintList(
            "NonExistent",
            "TestTable"
        );
        var getUniqueConstraintsAct = async () =>
            await service.GetUniqueConstraintsAsync(
                getUniqueConstraintsContext,
                "NonExistent",
                "TestTable"
            );
        await getUniqueConstraintsAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        var getUniqueConstraintContext = OperationIdentifiers.ForUniqueConstraintGet(
            "NonExistent",
            "TestTable",
            "UQ_Test"
        );
        var getUniqueConstraintAct = async () =>
            await service.GetUniqueConstraintAsync(
                getUniqueConstraintContext,
                "NonExistent",
                "TestTable",
                "UQ_Test"
            );
        await getUniqueConstraintAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        var createUniqueConstraintContext = OperationIdentifiers.ForUniqueConstraintCreate(
            "NonExistent",
            "TestTable",
            uniqueConstraintRequest
        );
        var createUniqueConstraintAct = async () =>
            await service.CreateUniqueConstraintAsync(
                createUniqueConstraintContext,
                "NonExistent",
                "TestTable",
                uniqueConstraintRequest
            );
        await createUniqueConstraintAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        var dropUniqueConstraintContext = OperationIdentifiers.ForUniqueConstraintDrop(
            "NonExistent",
            "TestTable",
            "UQ_Test"
        );
        var dropUniqueConstraintAct = async () =>
            await service.DropUniqueConstraintAsync(
                dropUniqueConstraintContext,
                "NonExistent",
                "TestTable",
                "UQ_Test"
            );
        await dropUniqueConstraintAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        // Check Constraint methods
        var getCheckConstraintsContext = OperationIdentifiers.ForCheckConstraintList(
            "NonExistent",
            "TestTable"
        );
        var getCheckConstraintsAct = async () =>
            await service.GetCheckConstraintsAsync(
                getCheckConstraintsContext,
                "NonExistent",
                "TestTable"
            );
        await getCheckConstraintsAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        var getCheckConstraintContext = OperationIdentifiers.ForCheckConstraintGet(
            "NonExistent",
            "TestTable",
            "CK_Test"
        );
        var getCheckConstraintAct = async () =>
            await service.GetCheckConstraintAsync(
                getCheckConstraintContext,
                "NonExistent",
                "TestTable",
                "CK_Test"
            );
        await getCheckConstraintAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        var createCheckConstraintContext = OperationIdentifiers.ForCheckConstraintCreate(
            "NonExistent",
            "TestTable",
            checkConstraintRequest
        );
        var createCheckConstraintAct = async () =>
            await service.CreateCheckConstraintAsync(
                createCheckConstraintContext,
                "NonExistent",
                "TestTable",
                checkConstraintRequest
            );
        await createCheckConstraintAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        var dropCheckConstraintContext = OperationIdentifiers.ForCheckConstraintDrop(
            "NonExistent",
            "TestTable",
            "CK_Test"
        );
        var dropCheckConstraintAct = async () =>
            await service.DropCheckConstraintAsync(
                dropCheckConstraintContext,
                "NonExistent",
                "TestTable",
                "CK_Test"
            );
        await dropCheckConstraintAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        // Default Constraint methods
        var getDefaultConstraintsContext = OperationIdentifiers.ForDefaultConstraintList(
            "NonExistent",
            "TestTable"
        );
        var getDefaultConstraintsAct = async () =>
            await service.GetDefaultConstraintsAsync(
                getDefaultConstraintsContext,
                "NonExistent",
                "TestTable"
            );
        await getDefaultConstraintsAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        var getDefaultConstraintContext = OperationIdentifiers.ForDefaultConstraintGet(
            "NonExistent",
            "TestTable",
            "Status"
        );
        var getDefaultConstraintAct = async () =>
            await service.GetDefaultConstraintAsync(
                getDefaultConstraintContext,
                "NonExistent",
                "TestTable",
                "Status"
            );
        await getDefaultConstraintAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        var createDefaultConstraintContext = OperationIdentifiers.ForDefaultConstraintCreate(
            "NonExistent",
            "TestTable",
            defaultConstraintRequest
        );
        var createDefaultConstraintAct = async () =>
            await service.CreateDefaultConstraintAsync(
                createDefaultConstraintContext,
                "NonExistent",
                "TestTable",
                defaultConstraintRequest
            );
        await createDefaultConstraintAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);

        var dropDefaultConstraintContext = OperationIdentifiers.ForDefaultConstraintDrop(
            "NonExistent",
            "TestTable",
            "Status"
        );
        var dropDefaultConstraintAct = async () =>
            await service.DropDefaultConstraintAsync(
                dropDefaultConstraintContext,
                "NonExistent",
                "TestTable",
                "Status"
            );
        await dropDefaultConstraintAct
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);
    }

    [Fact]
    public async Task GetConstraints_NonExistentTable_ThrowsKeyNotFoundException()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var primaryKeyContext = OperationIdentifiers.ForPrimaryKeyGet(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "NonExistentTable",
            "dbo"
        );
        var primaryKeyAct = async () =>
            await service.GetPrimaryKeyAsync(
                primaryKeyContext,
                TestcontainersAssemblyFixture.DatasourceId_SqlServer,
                "NonExistentTable",
                schemaName: "dbo"
            );
        await primaryKeyAct.Should().ThrowAsync<KeyNotFoundException>();

        var foreignKeyConstraintsContext = OperationIdentifiers.ForForeignKeyList(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "NonExistentTable",
            "dbo"
        );
        var foreignKeyConstraintsAct = async () =>
            await service.GetForeignKeysAsync(
                foreignKeyConstraintsContext,
                TestcontainersAssemblyFixture.DatasourceId_SqlServer,
                "NonExistentTable",
                schemaName: "dbo"
            );
        await foreignKeyConstraintsAct.Should().ThrowAsync<KeyNotFoundException>();

        var uniqueConstraintsContext = OperationIdentifiers.ForUniqueConstraintList(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "NonExistentTable",
            "dbo"
        );
        var uniqueConstraintsAct = async () =>
            await service.GetUniqueConstraintsAsync(
                uniqueConstraintsContext,
                TestcontainersAssemblyFixture.DatasourceId_SqlServer,
                "NonExistentTable",
                schemaName: "dbo"
            );
        await uniqueConstraintsAct.Should().ThrowAsync<KeyNotFoundException>();

        var checkConstraintsContext = OperationIdentifiers.ForCheckConstraintList(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "NonExistentTable",
            "dbo"
        );
        var checkConstraintsAct = async () =>
            await service.GetCheckConstraintsAsync(
                checkConstraintsContext,
                TestcontainersAssemblyFixture.DatasourceId_SqlServer,
                "NonExistentTable",
                schemaName: "dbo"
            );
        await checkConstraintsAct.Should().ThrowAsync<KeyNotFoundException>();

        var defaultConstraintsContext = OperationIdentifiers.ForDefaultConstraintList(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "NonExistentTable",
            "dbo"
        );
        var defaultConstraintsAct = async () =>
            await service.GetDefaultConstraintsAsync(
                defaultConstraintsContext,
                TestcontainersAssemblyFixture.DatasourceId_SqlServer,
                "NonExistentTable",
                schemaName: "dbo"
            );
        await defaultConstraintsAct.Should().ThrowAsync<KeyNotFoundException>();
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
                    ColumnName = "Email",
                    ProviderDataType =
                        datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                            ? "nvarchar(255)"
                            : "varchar(255)",
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
                    ColumnName = "Status",
                    ProviderDataType =
                        datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                            ? "nvarchar(50)"
                            : "varchar(50)",
                    IsNullable = true,
                },
                new ColumnDto
                {
                    ColumnName = "CategoryId",
                    ProviderDataType = "int",
                    IsNullable = true,
                },
            ],
        };

        var context = OperationIdentifiers.ForTableCreate(datasourceId, request);
        return await service.CreateTableAsync(context, datasourceId, request);
    }

    private static async Task<TableDto?> CreateReferencedTableForConstraints(
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
