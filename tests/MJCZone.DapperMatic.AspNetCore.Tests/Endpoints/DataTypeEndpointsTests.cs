// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;
using FluentAssertions;
using MJCZone.DapperMatic.AspNetCore.Models.Responses;
using MJCZone.DapperMatic.AspNetCore.Tests.Factories;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Endpoints;

/// <summary>
/// Integration tests for data type endpoints.
/// </summary>
public class DataTypeEndpointsTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private readonly TestcontainersAssemblyFixture _fixture;

    public DataTypeEndpointsTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Should_expect_get_datasource_data_types_with_valid_datasource_return_data_types_Async()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        const string datasourceId = TestcontainersAssemblyFixture.DatasourceId_SqlServer;

        // Act
        var response = await client.GetAsync($"/api/dm/d/{datasourceId}/datatypes");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().NotBeEmpty();

        var providerDataTypesResponse = JsonSerializer.Deserialize<ProviderDataTypeListResponse>(
            content,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
        );

        providerDataTypesResponse.Should().NotBeNull();
        providerDataTypesResponse!.ProviderName.ToLowerInvariant().Should().Be("sqlserver");
        providerDataTypesResponse.Result.Should().NotBeEmpty();
        providerDataTypesResponse.Result.Should().HaveCountGreaterThan(10);

        // Verify common data types are present
        providerDataTypesResponse
            .Result.Should()
            .Contain(dt => dt.DataType == "varchar" && dt.IsCommon);
        providerDataTypesResponse
            .Result.Should()
            .Contain(dt => dt.DataType == "int" && dt.IsCommon);
        providerDataTypesResponse
            .Result.Should()
            .Contain(dt => dt.DataType == "datetime" && dt.IsCommon);
        providerDataTypesResponse
            .Result.Should()
            .Contain(dt => dt.DataType == "decimal" && dt.IsCommon);

        // Verify categories are properly assigned
        var categories = providerDataTypesResponse
            .Result!.Select(dt => dt.Category)
            .Distinct()
            .ToList();
        categories.Should().Contain("Text");
        categories.Should().Contain("Integer");
        categories.Should().Contain("DateTime");
        categories.Should().Contain("Decimal");
    }

    [Fact]
    public async Task Should_expect_get_datasource_data_types_with_include_custom_types_include_static_types_Async()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        // Arrange
        const string datasourceId = TestcontainersAssemblyFixture.DatasourceId_SqlServer;

        // Act
        var response = await client.GetAsync(
            $"/api/dm/d/{datasourceId}/datatypes?include=customTypes"
        );
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().NotBeEmpty();

        var result = JsonSerializer.Deserialize<ProviderDataTypeListResponse>(
            content,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
        );

        result.Should().NotBeNull();
        result!.Result.Should().NotBeEmpty();

        // Should still contain all static types
        result.Result.Should().Contain(dt => dt.DataType == "varchar");
        result.Result.Should().Contain(dt => dt.DataType == "int");

        // Note: Custom types would only be discoverable with a real database connection
        // In this test environment, we're primarily testing the static type registry functionality
    }

    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql, "PostgreSQL")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql, "MySQL")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite, "SQLite")]
    public async Task Should_handle_get_datasource_data_types_with_different_providers_return_correct_types_Async(
        string datasourceId,
        string expectedProvider
    )
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/dm/d/{datasourceId}/datatypes");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().NotBeEmpty();

        var providerDataTypesResponse = JsonSerializer.Deserialize<ProviderDataTypeListResponse>(
            content,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
        );

        providerDataTypesResponse.Should().NotBeNull();
        providerDataTypesResponse!
            .ProviderName.ToLowerInvariant()
            .Should()
            .Be(expectedProvider.ToLowerInvariant());
        providerDataTypesResponse.Result.Should().NotBeEmpty();

        // Each provider should have at least some common types
        providerDataTypesResponse.Result.Should().Contain(dt => dt.IsCommon);

        // Verify provider-specific characteristics
        switch (expectedProvider)
        {
            case "PostgreSQL":
                providerDataTypesResponse.Result.Should().HaveCountGreaterThan(30); // PostgreSQL has many types
                providerDataTypesResponse.Result.Should().Contain(dt => dt.DataType == "jsonb");
                providerDataTypesResponse.Result.Should().Contain(dt => dt.DataType == "uuid");
                break;
            case "MySQL":
                providerDataTypesResponse.Result.Should().Contain(dt => dt.DataType == "json");
                providerDataTypesResponse.Result.Should().Contain(dt => dt.DataType == "enum");
                break;
            case "SQLite":
                providerDataTypesResponse.Result.Should().Contain(dt => dt.DataType == "integer");
                providerDataTypesResponse.Result.Should().Contain(dt => dt.DataType == "text");
                providerDataTypesResponse.Result.Should().Contain(dt => dt.DataType == "blob");
                break;
        }
    }

    [Fact]
    public async Task Should_handle_get_datasource_data_types_with_non_existent_datasource_return_not_found_Async()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        // Arrange
        const string datasourceId = "non-existent-datasource";

        // Act
        var response = await client.GetAsync($"/api/dm/d/{datasourceId}/datatypes");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_handle_get_datasource_data_types_include_metadata_Async()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        // Arrange
        const string datasourceId = TestcontainersAssemblyFixture.DatasourceId_SqlServer;

        // Act
        var response = await client.GetAsync($"/api/dm/d/{datasourceId}/datatypes");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = JsonSerializer.Deserialize<ProviderDataTypeListResponse>(
            content,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
        );

        result.Should().NotBeNull();
        result!.Result.Should().NotBeNull();

        // Find a varchar type to test metadata
        var varcharType = result!.Result!.FirstOrDefault(dt => dt.DataType == "varchar");
        varcharType.Should().NotBeNull();
        varcharType!.Category.Should().Be("Text");
        varcharType.SupportsLength.Should().BeTrue();
        varcharType.MaxLength.Should().BeGreaterThan(0);
        varcharType.DefaultLength.Should().BeGreaterThan(0);
        varcharType.Description.Should().NotBeNullOrWhiteSpace();

        // Find a decimal type to test precision/scale
        var decimalType = result!.Result!.FirstOrDefault(dt => dt.DataType == "decimal");
        decimalType.Should().NotBeNull();
        decimalType!.Category.Should().Be("Decimal");
        decimalType.SupportsPrecision.Should().BeTrue();
        decimalType.MaxPrecision.Should().BeGreaterThan(0);
        decimalType.SupportsScale.Should().BeTrue();
        decimalType.MaxScale.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Should_handle_get_datasource_data_types_include_aliases_Async()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        // Arrange
        const string datasourceId = TestcontainersAssemblyFixture.DatasourceId_SqlServer;

        // Act
        var response = await client.GetAsync($"/api/dm/d/{datasourceId}/datatypes");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = JsonSerializer.Deserialize<ProviderDataTypeListResponse>(
            content,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
        );

        result.Should().NotBeNull();
        result!.Result.Should().NotBeNull();

        // Find a type that should have aliases
        var intType = result!.Result!.FirstOrDefault(dt => dt.DataType == "int");
        intType.Should().NotBeNull();
        intType!.Aliases.Should().NotBeNull();
        intType.Aliases.Should().Contain("integer");
    }

    [Fact]
    public async Task Should_handle_get_datasource_data_types_response_structure_be_valid_Async()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        // Arrange
        const string datasourceId = TestcontainersAssemblyFixture.DatasourceId_SqlServer;

        // Act
        var response = await client.GetAsync($"/api/dm/d/{datasourceId}/datatypes");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var result = JsonSerializer.Deserialize<ProviderDataTypeListResponse>(
            content,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
        );

        result.Should().NotBeNull();
        result!.ProviderName.Should().NotBeNullOrWhiteSpace();
        result.Result.Should().NotBeNull();
        result
            .Result.Should()
            .AllSatisfy(dataType =>
            {
                dataType.DataType.Should().NotBeNullOrWhiteSpace();
                dataType.Category.Should().NotBeNullOrWhiteSpace();
                dataType.Aliases.Should().NotBeNull();
            });
    }

    [Fact]
    public async Task Should_handle_get_datasource_data_types_return_sorted_results_Async()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        // Arrange
        const string datasourceId = TestcontainersAssemblyFixture.DatasourceId_SqlServer;

        // Act
        var response = await client.GetAsync($"/api/dm/d/{datasourceId}/datatypes");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var providerDataTypesResponse = JsonSerializer.Deserialize<ProviderDataTypeListResponse>(
            content,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
        );

        providerDataTypesResponse.Should().NotBeNull();
        providerDataTypesResponse!.Result.Should().NotBeEmpty();

        // Verify types are sorted (registries sort by category then by type name)
        var sortedTypes = providerDataTypesResponse
            .Result!.OrderBy(dt => dt.Category)
            .ThenBy(dt => dt.DataType)
            .ToList();

        providerDataTypesResponse
            .Result.Should()
            .BeEquivalentTo(sortedTypes, options => options.WithStrictOrdering());
    }

    [Theory]
    [InlineData("customTypes")]
    [InlineData("CUSTOMTYPES")]
    [InlineData("customtypes")]
    [InlineData("CustomTypes")]
    public async Task Should_handle_get_datasource_data_types_include_parameter_be_case_insensitive_Async(
        string includeValue
    )
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var client = factory.CreateClient();

        // Arrange
        const string datasourceId = TestcontainersAssemblyFixture.DatasourceId_SqlServer;

        // Act
        var response = await client.GetAsync(
            $"/api/dm/d/{datasourceId}/datatypes?include={includeValue}"
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ProviderDataTypeListResponse>(
            content,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
        );

        result.Should().NotBeNull();
        result!.Result.Should().NotBeEmpty();
    }
}
