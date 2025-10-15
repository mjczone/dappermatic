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
    public async Task CheckConstraint_Management_Tests(string datasourceId, string? schemaName)
    {
        using var factory = GetDefaultWebApplicationFactory();
        var service = GetDapperMaticService(factory);

        // Non-existent datasource throws NotFound
        await CheckInvalidDatasourceHandlingFetchingCheckConstraints(service, schemaName);

        // Non-existent table throws NotFound
        await CheckInvalidTableHandlingFetchingCheckConstraints(service, datasourceId, schemaName);

        // MySQL doesn't support check constraints in older versions, skip if MySQL
        if (datasourceId == TestcontainersAssemblyFixture.DatasourceId_MySql)
        {
            return; // MySQL 5.7 and earlier don't support CHECK constraints
        }

        // Create test table for check constraint operations
        var tableName = "CCTest_" + Guid.NewGuid().ToString("N")[..8];
        await CreateTestTableWithoutCheckConstraints(service, datasourceId, tableName, schemaName);

        // Get Check Constraints on table without check constraints returns empty list
        var checkConstraints = await service.GetCheckConstraintsAsync(
            context: OperationIdentifiers.ForCheckConstraintList(
                datasourceId,
                tableName,
                schemaName
            ),
            datasourceId,
            tableName,
            schemaName
        );
        checkConstraints.Should().NotBeNull();
        checkConstraints.Should().BeEmpty();

        // Add Check Constraint
        var checkConstraintName = "CC_" + Guid.NewGuid().ToString("N")[..8];
        var checkConstraintRequest = new CheckConstraintDto
        {
            ConstraintName = checkConstraintName,
            ColumnName = "Age",
            CheckExpression =
                datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                    ? "[Age] >= 0 AND [Age] <= 120"
                : datasourceId == TestcontainersAssemblyFixture.DatasourceId_PostgreSql
                    ? "Age >= 0 AND Age <= 120"
                : datasourceId == TestcontainersAssemblyFixture.DatasourceId_Sqlite
                    ? "Age >= 0 AND Age <= 120"
                : "Age >= 0 AND Age <= 120",
        };
        var ccCreateContext = OperationIdentifiers.ForCheckConstraintCreate(
            datasourceId,
            tableName,
            checkConstraintRequest,
            schemaName
        );
        var addCcResult = await service.CreateCheckConstraintAsync(
            context: ccCreateContext,
            datasourceId,
            tableName,
            checkConstraintRequest,
            schemaName
        );
        addCcResult.Should().NotBeNull();
        addCcResult.ConstraintName.Should().BeEquivalentTo(checkConstraintName);

        // Verify Check Constraint was added
        checkConstraints = await service.GetCheckConstraintsAsync(
            context: OperationIdentifiers.ForCheckConstraintList(
                datasourceId,
                tableName,
                schemaName
            ),
            datasourceId,
            tableName,
            schemaName
        );
        checkConstraints.Should().NotBeNull();
        checkConstraints.Should().HaveCount(1);

        // Verify single Check Constraint details
        var cc = await service.GetCheckConstraintAsync(
            context: OperationIdentifiers.ForCheckConstraintGet(
                datasourceId,
                tableName,
                checkConstraintName,
                schemaName
            ),
            datasourceId,
            tableName,
            checkConstraintName,
            schemaName
        );
        cc.Should().NotBeNull();
        cc!.ConstraintName.Should().BeEquivalentTo(checkConstraintName);

        // Attempt to add duplicate Check Constraint throws Duplicate exception
        var duplicateCcAct = async () =>
            await service.CreateCheckConstraintAsync(
                context: ccCreateContext,
                datasourceId,
                tableName,
                checkConstraintRequest,
                schemaName
            );
        await duplicateCcAct.Should().ThrowAsync<DuplicateKeyException>();

        // Drop Check Constraint
        var dropContext = OperationIdentifiers.ForCheckConstraintDrop(
            datasourceId,
            tableName,
            checkConstraintName,
            schemaName
        );
        await service.DropCheckConstraintAsync(
            context: dropContext,
            datasourceId,
            tableName,
            checkConstraintName,
            schemaName
        );

        // Verify Check Constraint was dropped
        checkConstraints = await service.GetCheckConstraintsAsync(
            context: OperationIdentifiers.ForCheckConstraintList(
                datasourceId,
                tableName,
                schemaName
            ),
            datasourceId,
            tableName,
            schemaName
        );
        checkConstraints.Should().NotBeNull();
        checkConstraints.Should().BeEmpty();

        // Then using the Get single method
        var invalidCcAct = async () =>
            await service.GetCheckConstraintAsync(
                context: OperationIdentifiers.ForCheckConstraintGet(
                    datasourceId,
                    tableName,
                    checkConstraintName,
                    schemaName
                ),
                datasourceId,
                tableName,
                checkConstraintName,
                schemaName
            );
        await invalidCcAct.Should().ThrowAsync<KeyNotFoundException>();

        // Test creating check constraint with complex expression
        var complexCheckName = "CC_Complex_" + Guid.NewGuid().ToString("N")[..8];
        var complexCheckRequest = new CheckConstraintDto
        {
            ConstraintName = complexCheckName,
            ColumnName = "Status",
            CheckExpression =
                datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                    ? "[Status] IN ('Active', 'Inactive', 'Pending')"
                    : "Status IN ('Active', 'Inactive', 'Pending')",
        };
        var complexCcResult = await service.CreateCheckConstraintAsync(
            context: OperationIdentifiers.ForCheckConstraintCreate(
                datasourceId,
                tableName,
                complexCheckRequest,
                schemaName
            ),
            datasourceId,
            tableName,
            complexCheckRequest,
            schemaName
        );
        complexCcResult.Should().NotBeNull();
        complexCcResult.ConstraintName.Should().BeEquivalentTo(complexCheckName);

        // Clean up complex check constraint
        await service.DropCheckConstraintAsync(
            context: OperationIdentifiers.ForCheckConstraintDrop(
                datasourceId,
                tableName,
                complexCheckName,
                schemaName
            ),
            datasourceId,
            tableName,
            complexCheckName,
            schemaName
        );

        // Test creating check constraint with multiple columns
        var multiColCheckName = "CC_MultiCol_" + Guid.NewGuid().ToString("N")[..8];
        var multiColCheckRequest = new CheckConstraintDto
        {
            ConstraintName = multiColCheckName,
            CheckExpression =
                datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                    ? "[StartDate] < [EndDate]"
                    : "StartDate < EndDate",
        };
        var multiColCcResult = await service.CreateCheckConstraintAsync(
            context: OperationIdentifiers.ForCheckConstraintCreate(
                datasourceId,
                tableName,
                multiColCheckRequest,
                schemaName
            ),
            datasourceId,
            tableName,
            multiColCheckRequest,
            schemaName
        );
        multiColCcResult.Should().NotBeNull();
        multiColCcResult.ConstraintName.Should().BeEquivalentTo(multiColCheckName);

        // Clean up multi-column check constraint
        await service.DropCheckConstraintAsync(
            context: OperationIdentifiers.ForCheckConstraintDrop(
                datasourceId,
                tableName,
                multiColCheckName,
                schemaName
            ),
            datasourceId,
            tableName,
            multiColCheckName,
            schemaName
        );

        // Drop the test table
        await service.DropTableAsync(
            context: OperationIdentifiers.ForTableDrop(datasourceId, tableName, schemaName),
            datasourceId,
            tableName,
            schemaName
        );
    }

    private async Task CheckInvalidDatasourceHandlingFetchingCheckConstraints(
        IDapperMaticService service,
        string? schemaName
    )
    {
        var invalidDatasourceId = "NonExistent";
        var invalidContext = OperationIdentifiers.ForCheckConstraintList(
            invalidDatasourceId,
            "AnyTable",
            schemaName
        );
        var invalidAct = async () =>
            await service.GetCheckConstraintsAsync(
                invalidContext,
                invalidDatasourceId,
                "AnyTable",
                schemaName: schemaName
            );
        await invalidAct.Should().ThrowAsync<KeyNotFoundException>();
    }

    private async Task CheckInvalidTableHandlingFetchingCheckConstraints(
        IDapperMaticService service,
        string datasourceId,
        string? schemaName
    )
    {
        var invalidTableContext = OperationIdentifiers.ForCheckConstraintList(
            datasourceId,
            "NonExistentTable",
            schemaName
        );
        var invalidTableAct = async () =>
            await service.GetCheckConstraintsAsync(
                invalidTableContext,
                datasourceId,
                "NonExistentTable",
                schemaName: schemaName
            );
        await invalidTableAct.Should().ThrowAsync<KeyNotFoundException>();
    }

    private async Task CreateTestTableWithoutCheckConstraints(
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
                    ColumnName = "Age",
                    ProviderDataType = "int",
                    IsNullable = true,
                },
                new()
                {
                    ColumnName = "Status",
                    ProviderDataType =
                        datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                            ? "nvarchar(50)"
                            : "varchar(50)",
                    IsNullable = true,
                },
                new()
                {
                    ColumnName = "StartDate",
                    ProviderDataType =
                        datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                            ? "datetime2"
                        : datasourceId == TestcontainersAssemblyFixture.DatasourceId_PostgreSql
                            ? "timestamp"
                        : "datetime",
                    IsNullable = true,
                },
                new()
                {
                    ColumnName = "EndDate",
                    ProviderDataType =
                        datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                            ? "datetime2"
                        : datasourceId == TestcontainersAssemblyFixture.DatasourceId_PostgreSql
                            ? "timestamp"
                        : "datetime",
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
}
