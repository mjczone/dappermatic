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
    public async Task DefaultConstraint_Management_Tests(string datasourceId, string? schemaName)
    {
        using var factory = GetDefaultWebApplicationFactory();
        var service = GetDapperMaticService(factory);

        // Non-existent datasource throws NotFound
        await CheckInvalidDatasourceHandlingFetchingDefaultConstraints(service, schemaName);

        // Non-existent table throws NotFound
        await CheckInvalidTableHandlingFetchingDefaultConstraints(
            service,
            datasourceId,
            schemaName
        );

        // Create test table for default constraint operations
        var tableName = "DCTest_" + Guid.NewGuid().ToString("N")[..8];
        await CreateTestTableWithoutDefaultConstraints(
            service,
            datasourceId,
            tableName,
            schemaName
        );

        // Get Default Constraints on table without default constraints returns empty list
        var defaultConstraints = await service.GetDefaultConstraintsAsync(
            context: OperationIdentifiers.ForDefaultConstraintList(
                datasourceId,
                tableName,
                schemaName
            ),
            datasourceId,
            tableName,
            schemaName
        );
        defaultConstraints.Should().NotBeNull();
        defaultConstraints.Should().BeEmpty();

        // Add Default Constraint
        // Internally ONLY SQL Server allows naming default constraints, others do not, so DapperMatic
        // automatically generates a name for SQLite, MySQL and PostgreSQL based on the DF_{table}_{column} pattern
        var defaultConstraintName = "DF_" + Guid.NewGuid().ToString("N")[..8];
        var defaultConstraintRequest = new DefaultConstraintDto
        {
            ConstraintName = defaultConstraintName,
            ColumnName = "Status",
            DefaultExpression =
                datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer ? "'Active'"
                : datasourceId == TestcontainersAssemblyFixture.DatasourceId_PostgreSql
                    ? "'Active'::varchar"
                : "'Active'",
        };
        var dcCreateContext = OperationIdentifiers.ForDefaultConstraintCreate(
            datasourceId,
            tableName,
            defaultConstraintRequest,
            schemaName
        );
        var addDcResult = await service.CreateDefaultConstraintAsync(
            context: dcCreateContext,
            datasourceId,
            tableName,
            defaultConstraintRequest,
            schemaName
        );
        addDcResult.Should().NotBeNull();
        // if SQL Server, name is as provided, otherwise generated
        if (datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer)
        {
            addDcResult.ConstraintName.Should().BeEquivalentTo(defaultConstraintName);
        }
        else
        {
            addDcResult
                .ConstraintName.Should()
                .BeEquivalentTo($"DF_{tableName}_Status", options => options.IgnoringCase());
        }
        addDcResult.ColumnName.Should().BeEquivalentTo(defaultConstraintRequest.ColumnName);

        // Verify Default Constraint was added
        defaultConstraints = await service.GetDefaultConstraintsAsync(
            context: OperationIdentifiers.ForDefaultConstraintList(
                datasourceId,
                tableName,
                schemaName
            ),
            datasourceId,
            tableName,
            schemaName
        );
        defaultConstraints.Should().NotBeNull();
        defaultConstraints.Should().HaveCount(1);

        // Test constraint-name-based access (provider-specific behavior)
        if (datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer)
        {
            // SQL Server: Should be able to get by constraint name
            var dcByName = await service.GetDefaultConstraintAsync(
                context: OperationIdentifiers.ForDefaultConstraintGet(
                    datasourceId,
                    tableName,
                    defaultConstraintName,
                    schemaName
                ),
                datasourceId,
                tableName,
                defaultConstraintName,
                schemaName
            );
            dcByName.Should().NotBeNull();
            dcByName!.ConstraintName.Should().BeEquivalentTo(defaultConstraintName);
            dcByName.ColumnName.Should().BeEquivalentTo("Status");
        }
        else
        {
            // Other providers: Should throw KeyNotFoundException when using constraint name
            var invalidConstraintNameAct = async () =>
                await service.GetDefaultConstraintAsync(
                    context: OperationIdentifiers.ForDefaultConstraintGet(
                        datasourceId,
                        tableName,
                        defaultConstraintName,
                        schemaName
                    ),
                    datasourceId,
                    tableName,
                    defaultConstraintName,
                    schemaName
                );
            await invalidConstraintNameAct.Should().ThrowAsync<KeyNotFoundException>();
        }

        // Test column-based access (should work for all providers)
        var dcByColumn = await service.GetDefaultConstraintOnColumnAsync(
            context: OperationIdentifiers.ForDefaultConstraintOnColumnGet(
                datasourceId,
                tableName,
                "Status",
                schemaName
            ),
            datasourceId,
            tableName,
            "Status",
            schemaName
        );
        dcByColumn.Should().NotBeNull();
        dcByColumn!.ColumnName.Should().BeEquivalentTo("Status");
        // Verify the constraint name matches expected pattern
        if (datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer)
        {
            dcByColumn.ConstraintName.Should().BeEquivalentTo(defaultConstraintName);
        }
        else
        {
            dcByColumn
                .ConstraintName.Should()
                .BeEquivalentTo($"DF_{tableName}_Status", options => options.IgnoringCase());
        }

        // Attempt to add duplicate Default Constraint throws Duplicate exception
        var duplicateDcAct = async () =>
            await service.CreateDefaultConstraintAsync(
                context: dcCreateContext,
                datasourceId,
                tableName,
                defaultConstraintRequest,
                schemaName
            );
        await duplicateDcAct.Should().ThrowAsync<DuplicateKeyException>();

        // Test dropping via column-based method (should work for all providers)
        await service.DropDefaultConstraintOnColumnAsync(
            context: OperationIdentifiers.ForDefaultConstraintOnColumnDrop(
                datasourceId,
                tableName,
                "Status",
                schemaName
            ),
            datasourceId,
            tableName,
            "Status",
            schemaName
        );

        // Verify Default Constraint was dropped
        defaultConstraints = await service.GetDefaultConstraintsAsync(
            context: OperationIdentifiers.ForDefaultConstraintList(
                datasourceId,
                tableName,
                schemaName
            ),
            datasourceId,
            tableName,
            schemaName
        );
        defaultConstraints.Should().NotBeNull();
        defaultConstraints.Should().BeEmpty();

        // Verify constraint was dropped using constraint-name method (should fail for all providers)
        var invalidDcByNameAct = async () =>
            await service.GetDefaultConstraintAsync(
                context: OperationIdentifiers.ForDefaultConstraintGet(
                    datasourceId,
                    tableName,
                    defaultConstraintName,
                    schemaName
                ),
                datasourceId,
                tableName,
                defaultConstraintName,
                schemaName
            );
        await invalidDcByNameAct.Should().ThrowAsync<KeyNotFoundException>();

        // Verify constraint was dropped using column-based method (should fail for all providers)
        var invalidDcByColumnAct = async () =>
            await service.GetDefaultConstraintOnColumnAsync(
                context: OperationIdentifiers.ForDefaultConstraintOnColumnGet(
                    datasourceId,
                    tableName,
                    "Status",
                    schemaName
                ),
                datasourceId,
                tableName,
                "Status",
                schemaName
            );
        await invalidDcByColumnAct.Should().ThrowAsync<KeyNotFoundException>();

        // Test creating default constraint with numeric expression
        var numericDefaultName = "DF_Numeric_" + Guid.NewGuid().ToString("N")[..8];
        var numericDefaultRequest = new DefaultConstraintDto
        {
            ConstraintName = numericDefaultName,
            ColumnName = "Score",
            DefaultExpression = "0",
        };
        var numericDcResult = await service.CreateDefaultConstraintAsync(
            context: OperationIdentifiers.ForDefaultConstraintCreate(
                datasourceId,
                tableName,
                numericDefaultRequest,
                schemaName
            ),
            datasourceId,
            tableName,
            numericDefaultRequest,
            schemaName
        );
        numericDcResult.Should().NotBeNull();
        if (datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer)
        {
            numericDcResult.ConstraintName.Should().BeEquivalentTo(numericDefaultName);
        }
        else
        {
            numericDcResult
                .ConstraintName.Should()
                .BeEquivalentTo($"DF_{tableName}_Score", options => options.IgnoringCase());
        }

        // Clean up numeric default constraint using column-based method
        await service.DropDefaultConstraintOnColumnAsync(
            context: OperationIdentifiers.ForDefaultConstraintOnColumnDrop(
                datasourceId,
                tableName,
                "Score",
                schemaName
            ),
            datasourceId,
            tableName,
            "Score",
            schemaName
        );

        // Test creating default constraint with date expression (provider-specific)
        if (datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer)
        {
            var dateDefaultName = "DF_Date_" + Guid.NewGuid().ToString("N")[..8];
            var dateDefaultRequest = new DefaultConstraintDto
            {
                ConstraintName = dateDefaultName,
                ColumnName = "CreatedAt",
                DefaultExpression = "GETUTCDATE()",
            };
            var dateDcResult = await service.CreateDefaultConstraintAsync(
                context: OperationIdentifiers.ForDefaultConstraintCreate(
                    datasourceId,
                    tableName,
                    dateDefaultRequest,
                    schemaName
                ),
                datasourceId,
                tableName,
                dateDefaultRequest,
                schemaName
            );
            dateDcResult.Should().NotBeNull();

            // Clean up date default constraint using column-based method
            await service.DropDefaultConstraintOnColumnAsync(
                context: OperationIdentifiers.ForDefaultConstraintOnColumnDrop(
                    datasourceId,
                    tableName,
                    "CreatedAt",
                    schemaName
                ),
                datasourceId,
                tableName,
                "CreatedAt",
                schemaName
            );
        }

        // Drop the test table
        await service.DropTableAsync(
            context: OperationIdentifiers.ForTableDrop(datasourceId, tableName, schemaName),
            datasourceId,
            tableName,
            schemaName
        );
    }

    private async Task CheckInvalidDatasourceHandlingFetchingDefaultConstraints(
        IDapperMaticService service,
        string? schemaName
    )
    {
        var invalidDatasourceId = "NonExistent";
        var invalidContext = OperationIdentifiers.ForDefaultConstraintList(
            invalidDatasourceId,
            "AnyTable",
            schemaName
        );
        var invalidAct = async () =>
            await service.GetDefaultConstraintsAsync(
                invalidContext,
                invalidDatasourceId,
                "AnyTable",
                schemaName: schemaName
            );
        await invalidAct.Should().ThrowAsync<KeyNotFoundException>();
    }

    private async Task CheckInvalidTableHandlingFetchingDefaultConstraints(
        IDapperMaticService service,
        string datasourceId,
        string? schemaName
    )
    {
        var invalidTableContext = OperationIdentifiers.ForDefaultConstraintList(
            datasourceId,
            "NonExistentTable",
            schemaName
        );
        var invalidTableAct = async () =>
            await service.GetDefaultConstraintsAsync(
                invalidTableContext,
                datasourceId,
                "NonExistentTable",
                schemaName: schemaName
            );
        await invalidTableAct.Should().ThrowAsync<KeyNotFoundException>();
    }

    private async Task CreateTestTableWithoutDefaultConstraints(
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
                    ColumnName = "Status",
                    ProviderDataType =
                        datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                            ? "nvarchar(50)"
                            : "varchar(50)",
                    IsNullable = true,
                },
                new()
                {
                    ColumnName = "Score",
                    ProviderDataType = "int",
                    IsNullable = true,
                },
                new()
                {
                    ColumnName = "CreatedAt",
                    ProviderDataType =
                        datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
                            ? "datetime2"
                        : datasourceId == TestcontainersAssemblyFixture.DatasourceId_PostgreSql
                            ? "timestamp"
                        : "datetime",
                    IsNullable = true,
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
