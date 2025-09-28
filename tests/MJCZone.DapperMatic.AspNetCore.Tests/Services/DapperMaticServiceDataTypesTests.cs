// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MJCZone.DapperMatic.AspNetCore.Extensions;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Services;
using MJCZone.DapperMatic.AspNetCore.Tests.Factories;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Services;

/// <summary>
/// Tests for DapperMaticService data types functionality.
/// </summary>
public class DapperMaticServiceDataTypesTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private readonly TestcontainersAssemblyFixture _fixture;

    public DapperMaticServiceDataTypesTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetDatasourceDataTypesAsync_WithValidDatasource_ShouldReturnDataTypes()
    {
        // Arrange
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();
        const string datasourceId = TestcontainersAssemblyFixture.DatasourceId_SqlServer;

        // Act
        var context = OperationIdentifiers.ForDataTypeGet(datasourceId, includeCustomTypes: false);
        var result = await service.GetDatasourceDataTypesAsync(
            context,
            datasourceId,
            includeCustomTypes: false
        );

        // Assert
        result.providerName.ToLowerInvariant().Should().Be("sqlserver");
        result.dataTypes.Should().NotBeEmpty();
        result.dataTypes.Should().HaveCountGreaterThan(10);

        // Verify common SQL Server types are present
        result.dataTypes.Should().Contain(dt => dt.DataType == "varchar");
        result.dataTypes.Should().Contain(dt => dt.DataType == "int");
        result.dataTypes.Should().Contain(dt => dt.DataType == "datetime");
        result.dataTypes.Should().Contain(dt => dt.DataType == "decimal");

        // Verify categories
        var categories = result
            .dataTypes.ToDataTypeDtos()
            .Select(dt => dt.Category)
            .Distinct()
            .ToList();
        categories.Should().Contain("Text");
        categories.Should().Contain("Integer");
        categories.Should().Contain("DateTime");
        categories.Should().Contain("Decimal");
    }

    [Fact]
    public async Task GetDatasourceDataTypesAsync_WithIncludeCustomTypes_ShouldIncludeStaticTypes()
    {
        // Arrange
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();
        const string datasourceId = TestcontainersAssemblyFixture.DatasourceId_SqlServer;

        // Act
        var context = OperationIdentifiers.ForDataTypeGet(datasourceId, includeCustomTypes: true);
        var result = await service.GetDatasourceDataTypesAsync(
            context,
            datasourceId,
            includeCustomTypes: true
        );

        // Assert
        result.providerName.ToLowerInvariant().Should().Be("sqlserver");
        result.dataTypes.Should().NotBeEmpty();

        // Should still contain all static types
        result.dataTypes.Should().Contain(dt => dt.DataType == "varchar");
        result.dataTypes.Should().Contain(dt => dt.DataType == "int");

        // Note: Custom types would only be discoverable with a real database connection
        // In this test environment, we're primarily testing the static type registry functionality
    }

    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql, "postgresql")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql, "mysql")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite, "sqlite")]
    public async Task GetDatasourceDataTypesAsync_WithDifferentProviders_ShouldReturnCorrectTypes(
        string datasourceId,
        string expectedProvider
    )
    {
        // Arrange
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        // Act
        var context = OperationIdentifiers.ForDataTypeGet(datasourceId, includeCustomTypes: false);
        var result = await service.GetDatasourceDataTypesAsync(
            context,
            datasourceId,
            includeCustomTypes: false
        );

        // Assert
        result.providerName.ToLowerInvariant().Should().Be(expectedProvider.ToLowerInvariant());
        result.dataTypes.Should().NotBeEmpty();

        // Each provider should have at least some common types
        result.dataTypes.Should().Contain(dt => dt.IsCommon);

        // Verify provider-specific characteristics
        switch (expectedProvider)
        {
            case "postgresql":
                result.dataTypes.Should().HaveCountGreaterThan(30); // PostgreSQL has many types
                result.dataTypes.Should().Contain(dt => dt.DataType == "jsonb");
                result.dataTypes.Should().Contain(dt => dt.DataType == "uuid");
                result.dataTypes.Should().Contain(dt => dt.DataType == "integer[]");
                break;
            case "mysql":
                result.dataTypes.Should().Contain(dt => dt.DataType == "json");
                result.dataTypes.Should().Contain(dt => dt.DataType == "enum");
                result.dataTypes.Should().Contain(dt => dt.DataType == "geometry");
                break;
            case "sqlite":
                result.dataTypes.Should().Contain(dt => dt.DataType == "integer");
                result.dataTypes.Should().Contain(dt => dt.DataType == "text");
                result.dataTypes.Should().Contain(dt => dt.DataType == "blob");
                result.dataTypes.Should().Contain(dt => dt.DataType == "real");
                break;
        }
    }

    [Fact]
    public async Task GetDatasourceDataTypesAsync_WithNonExistentDatasource_ShouldThrowArgumentException()
    {
        // Arrange
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();
        const string datasourceId = "non-existent-datasource";

        // Act & Assert
        var context = OperationIdentifiers.ForDataTypeGet(datasourceId);
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetDatasourceDataTypesAsync(context, datasourceId)
        );

        exception.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task GetDatasourceDataTypesAsync_ShouldIncludeCompleteMetadata()
    {
        // Arrange
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();
        const string datasourceId = TestcontainersAssemblyFixture.DatasourceId_SqlServer;

        // Act
        var context = OperationIdentifiers.ForDataTypeGet(datasourceId, includeCustomTypes: false);
        var result = await service.GetDatasourceDataTypesAsync(
            context,
            datasourceId,
            includeCustomTypes: false
        );

        // Assert
        result.dataTypes.Should().NotBeEmpty();

        var dataTypeDtos = result.dataTypes.ToDataTypeDtos().ToList();

        // Test string type metadata
        var varcharType = dataTypeDtos.FirstOrDefault(dt => dt.DataType == "varchar");
        varcharType.Should().NotBeNull();
        varcharType!.Category.Should().Be("Text");
        varcharType.SupportsLength.Should().BeTrue();
        varcharType.MaxLength.Should().BeGreaterThan(0);
        varcharType.DefaultLength.Should().BeGreaterThan(0);
        varcharType.Description.Should().NotBeNullOrWhiteSpace();
        varcharType.Aliases.Should().NotBeNull();

        // Test decimal type metadata
        var decimalType = dataTypeDtos.FirstOrDefault(dt => dt.DataType == "decimal");
        decimalType.Should().NotBeNull();
        decimalType!.Category.Should().Be("Decimal");
        decimalType.SupportsPrecision.Should().BeTrue();
        decimalType.MaxPrecision.Should().BeGreaterThan(0);
        decimalType.SupportsScale.Should().BeTrue();
        decimalType.MaxScale.Should().BeGreaterThan(0);
        decimalType.DefaultPrecision.Should().BeGreaterThan(0);
        decimalType.DefaultScale.Should().BeGreaterOrEqualTo(0);

        // Test integer type metadata
        var intType = dataTypeDtos.FirstOrDefault(dt => dt.DataType == "int");
        intType.Should().NotBeNull();
        intType!.Category.Should().Be("Integer");
        intType.SupportsLength.Should().BeFalse();
        intType.SupportsPrecision.Should().BeFalse();
        intType.SupportsScale.Should().BeFalse();
        intType.Aliases.Should().Contain("integer");
    }

    [Fact]
    public async Task GetDatasourceDataTypesAsync_ShouldReturnSortedResults()
    {
        // Arrange
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();
        const string datasourceId = TestcontainersAssemblyFixture.DatasourceId_SqlServer;

        // Act
        var context = OperationIdentifiers.ForDataTypeGet(datasourceId, includeCustomTypes: false);
        var result = await service.GetDatasourceDataTypesAsync(
            context,
            datasourceId,
            includeCustomTypes: false
        );

        // Assert
        result.dataTypes.Should().NotBeEmpty();

        // Verify types are sorted (by category then by type name)
        var sortedTypes = result
            .dataTypes.OrderBy(dt => dt.Category)
            .ThenBy(dt => dt.DataType)
            .ToList();

        result
            .dataTypes.Should()
            .BeEquivalentTo(sortedTypes, options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task GetDatasourceDataTypesAsync_ShouldHaveCorrectProviderNames()
    {
        // Arrange
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var testCases = new[]
        {
            (TestcontainersAssemblyFixture.DatasourceId_SqlServer, "sqlserver"),
            (TestcontainersAssemblyFixture.DatasourceId_PostgreSql, "postgresql"),
            (TestcontainersAssemblyFixture.DatasourceId_MySql, "mysql"),
            (TestcontainersAssemblyFixture.DatasourceId_Sqlite, "sqlite"),
        };

        foreach (var (datasourceId, expectedProvider) in testCases)
        {
            // Act
            var context = OperationIdentifiers.ForDataTypeGet(
                datasourceId,
                includeCustomTypes: false
            );
            var result = await service.GetDatasourceDataTypesAsync(
                context,
                datasourceId,
                includeCustomTypes: false
            );

            // Assert
            result
                .providerName.ToLowerInvariant()
                .Should()
                .Be(expectedProvider, $"for datasource {datasourceId}");
        }
    }

    [Fact]
    public async Task GetDatasourceDataTypesAsync_WithDefaultParameters_ShouldWork()
    {
        // Arrange
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();
        const string datasourceId = TestcontainersAssemblyFixture.DatasourceId_SqlServer;

        // Act - test with default parameters
        var context = OperationIdentifiers.ForDataTypeGet(datasourceId);
        var result = await service.GetDatasourceDataTypesAsync(context, datasourceId);

        // Assert
        result.providerName.ToLowerInvariant().Should().Be("sqlserver");
        result.dataTypes.Should().NotBeEmpty();
        result.dataTypes.Should().Contain(dt => dt.DataType == "varchar");
    }

    [Fact]
    public async Task GetDatasourceDataTypesAsync_ShouldIncludeCommonAndAdvancedTypes()
    {
        // Arrange
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();
        const string datasourceId = TestcontainersAssemblyFixture.DatasourceId_SqlServer;

        // Act
        var context = OperationIdentifiers.ForDataTypeGet(datasourceId, includeCustomTypes: false);
        var result = await service.GetDatasourceDataTypesAsync(
            context,
            datasourceId,
            includeCustomTypes: false
        );

        // Assert
        result.dataTypes.Should().NotBeEmpty();

        var commonTypes = result.dataTypes.Where(dt => dt.IsCommon).ToList();
        var advancedTypes = result.dataTypes.Where(dt => !dt.IsCommon).ToList();

        commonTypes.Should().NotBeEmpty();
        advancedTypes.Should().NotBeEmpty();

        // Verify some expected common types
        commonTypes.Should().Contain(dt => dt.DataType == "varchar");
        commonTypes.Should().Contain(dt => dt.DataType == "int");

        // Verify some advanced types exist
        advancedTypes.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task GetDatasourceDataTypesAsync_PostgreSQL_ShouldHaveAdvancedTypes()
    {
        // Arrange
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();
        const string datasourceId = TestcontainersAssemblyFixture.DatasourceId_PostgreSql;

        // Act
        var context = OperationIdentifiers.ForDataTypeGet(datasourceId, includeCustomTypes: false);
        var result = await service.GetDatasourceDataTypesAsync(
            context,
            datasourceId,
            includeCustomTypes: false
        );

        // Assert
        result.providerName.ToLowerInvariant().Should().Be("postgresql");
        result.dataTypes.Should().NotBeEmpty();

        // Verify PostgreSQL-specific advanced types
        result.dataTypes.Should().Contain(dt => dt.DataType == "jsonb");
        result.dataTypes.Should().Contain(dt => dt.DataType == "uuid");
        result.dataTypes.Should().Contain(dt => dt.DataType == "inet");
        result.dataTypes.Should().Contain(dt => dt.DataType == "int4range");
        result.dataTypes.Should().Contain(dt => dt.DataType == "integer[]");
        result.dataTypes.Should().Contain(dt => dt.DataType == "hstore");

        // Verify categories specific to PostgreSQL
        var categories = result
            .dataTypes.ToDataTypeDtos()
            .Select(dt => dt.Category)
            .Distinct()
            .ToList();
        categories.Should().Contain("Network");
        categories.Should().Contain("Range");
        categories.Should().Contain("Array");
    }

    [Fact]
    public async Task GetDatasourceDataTypesAsync_MySQL_ShouldHaveSpatialTypes()
    {
        // Arrange
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();
        const string datasourceId = TestcontainersAssemblyFixture.DatasourceId_MySql;

        // Act
        var context = OperationIdentifiers.ForDataTypeGet(datasourceId, includeCustomTypes: false);
        var result = await service.GetDatasourceDataTypesAsync(
            context,
            datasourceId,
            includeCustomTypes: false
        );

        // Assert
        result.providerName.ToLowerInvariant().Should().Be("mysql");
        result.dataTypes.Should().NotBeEmpty();

        // Verify MySQL-specific types
        result.dataTypes.Should().Contain(dt => dt.DataType == "json");
        result.dataTypes.Should().Contain(dt => dt.DataType == "enum");
        result.dataTypes.Should().Contain(dt => dt.DataType == "set");
        result.dataTypes.Should().Contain(dt => dt.DataType == "geometry");
        result.dataTypes.Should().Contain(dt => dt.DataType == "point");

        // Verify MySQL has spatial category
        var categories = result
            .dataTypes.ToDataTypeDtos()
            .Select(dt => dt.Category)
            .Distinct()
            .ToList();
        categories.Should().Contain("Spatial");
    }

    [Fact]
    public async Task GetDatasourceDataTypesAsync_SQLite_ShouldHaveFundamentalTypes()
    {
        // Arrange
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();
        const string datasourceId = TestcontainersAssemblyFixture.DatasourceId_Sqlite;

        // Act
        var context = OperationIdentifiers.ForDataTypeGet(datasourceId, includeCustomTypes: false);
        var result = await service.GetDatasourceDataTypesAsync(
            context,
            datasourceId,
            includeCustomTypes: false
        );

        // Assert
        result.providerName.ToLowerInvariant().Should().Be("sqlite");
        result.dataTypes.Should().NotBeEmpty();

        // Verify SQLite fundamental types (SQLite has type affinity)
        result.dataTypes.Should().Contain(dt => dt.DataType == "integer");
        result.dataTypes.Should().Contain(dt => dt.DataType == "text");
        result.dataTypes.Should().Contain(dt => dt.DataType == "blob");
        result.dataTypes.Should().Contain(dt => dt.DataType == "real");
        result.dataTypes.Should().Contain(dt => dt.DataType == "numeric");

        // SQLite should have fewer types than other databases
        result.dataTypes.Should().HaveCountLessThan(50);
    }
}
