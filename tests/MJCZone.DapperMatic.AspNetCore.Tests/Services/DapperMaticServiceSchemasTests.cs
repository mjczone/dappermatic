// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using FluentAssertions;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Services;
using MJCZone.DapperMatic.AspNetCore.Tests.Factories;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Services;

/// <summary>
/// Unit tests for DapperMatic service schema operations.
/// </summary>
public class DapperMaticServiceSchemasTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private readonly TestcontainersAssemblyFixture _fixture;

    public DapperMaticServiceSchemasTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetSchemasAsync_SqlServer_ReturnsSchemas()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var result = await service.GetSchemasAsync(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer
        );

        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result.Should().Contain(s => s.SchemaName == "dbo");
    }

    [Fact]
    public async Task GetSchemasAsync_PostgreSQL_ReturnsSchemas()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var result = await service.GetSchemasAsync(
            TestcontainersAssemblyFixture.DatasourceId_PostgreSql
        );

        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result.Should().Contain(s => s.SchemaName == "public");
    }

    [Fact]
    public async Task GetSchemasAsync_NonExistentDatasource_ThrowsArgumentException()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var act = async () => await service.GetSchemasAsync("NonExistent");

        await act.Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");
    }

    [Fact]
    public async Task GetSchemaAsync_SqlServer_dbo_ReturnsSchema()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var result = await service.GetSchemaAsync(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "dbo"
        );

        result.Should().NotBeNull();
        result!.SchemaName.Should().Be("dbo");
    }

    [Fact]
    public async Task GetSchemaAsync_NonExistentSchema_ReturnsNull()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var result = await service.GetSchemaAsync(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "nonexistent"
        );

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateSchemaAsync_SqlServer_CreatesSchema()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var schema = new SchemaDto { SchemaName = "TestSchema" };
        var result = await service.CreateSchemaAsync(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            schema
        );

        result.Should().NotBeNull();
        result!.SchemaName.Should().Be("TestSchema");

        // Verify it was created
        var exists = await service.SchemaExistsAsync(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "TestSchema"
        );
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task CreateSchemaAsync_DuplicateSchema_ReturnsNull()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var schema = new SchemaDto { SchemaName = "dbo" };
        var result = await service.CreateSchemaAsync(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            schema
        );

        result.Should().BeNull(); // dbo already exists
    }

    [Fact]
    public async Task DropSchemaAsync_SqlServer_DropsSchema()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        // First create a schema
        var schema = new SchemaDto { SchemaName = "TempSchema" };
        await service.CreateSchemaAsync(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            schema
        );

        // Then drop it
        var result = await service.DropSchemaAsync(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "TempSchema"
        );

        result.Should().BeTrue();

        // Verify it was dropped
        var exists = await service.SchemaExistsAsync(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "TempSchema"
        );
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task DropSchemaAsync_NonExistentSchema_ReturnsFalse()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var result = await service.DropSchemaAsync(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "nonexistent"
        );

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SchemaExistsAsync_SqlServer_dbo_ReturnsTrue()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var result = await service.SchemaExistsAsync(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "dbo"
        );

        result.Should().BeTrue();
    }

    [Fact]
    public async Task SchemaExistsAsync_NonExistentSchema_ReturnsFalse()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var result = await service.SchemaExistsAsync(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "nonexistent"
        );

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task GetSchemasAsync_InvalidDatasourceId_ThrowsArgumentException(
        string? datasourceId
    )
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var act = async () => await service.GetSchemasAsync(datasourceId!);

        await act.Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("*Datasource ID is required*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task GetSchemaAsync_InvalidSchemaName_ThrowsArgumentException(string? schemaName)
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var act = async () =>
            await service.GetSchemaAsync(
                TestcontainersAssemblyFixture.DatasourceId_SqlServer,
                schemaName!
            );

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Schema name is required*");
    }

    [Fact]
    public async Task CreateSchemaAsync_NullSchema_ThrowsArgumentNullException()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var act = async () =>
            await service.CreateSchemaAsync(
                TestcontainersAssemblyFixture.DatasourceId_SqlServer,
                null!
            );

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateSchemaAsync_EmptySchemaName_ThrowsArgumentException()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var schema = new SchemaDto { SchemaName = "" };
        var act = async () =>
            await service.CreateSchemaAsync(
                TestcontainersAssemblyFixture.DatasourceId_SqlServer,
                schema
            );

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Schema name is required*");
    }

    [Fact]
    public async Task GetSchemasAsync_WithUser_WorksCorrectly()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "test-user")]));

        var result = await service.GetSchemasAsync(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            user
        );

        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result.Should().Contain(s => s.SchemaName == "dbo");
    }

    #region Provider-Specific Schema Support Tests (from SchemaSupportTests.cs)

    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer, true)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql, false)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql, true)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite, false)]
    public async Task GetSchemasAsync_ProviderSchemaSupport_ReturnsExpectedBehavior(
        string datasourceId,
        bool supportsSchemas
    )
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var result = await service.GetSchemasAsync(datasourceId);

        result.Should().NotBeNull();
        result.Should().NotBeEmpty();

        if (supportsSchemas)
        {
            // Providers that support schemas should return actual schema names
            result.Should().NotContain(s => s.SchemaName == "_");
            result.Count().Should().BeGreaterThan(0);

            switch (datasourceId)
            {
                case TestcontainersAssemblyFixture.DatasourceId_SqlServer:
                    result.Should().Contain(s => s.SchemaName == "dbo");
                    break;
                case TestcontainersAssemblyFixture.DatasourceId_PostgreSql:
                    result.Should().Contain(s => s.SchemaName == "public");
                    break;
            }
        }
        else
        {
            // Providers that don't support schemas should return only "_"
            result.Should().HaveCount(1);
            result.Should().Contain(s => s.SchemaName == "_");
        }
    }

    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql)]
    public async Task CreateSchemaAsync_SupportedProviders_AllowsSchemaCreation(string datasourceId)
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var schema = new SchemaDto { SchemaName = $"TestSchema_{Guid.NewGuid():N}"[..20] };

        var result = await service.CreateSchemaAsync(datasourceId, schema);

        result.Should().NotBeNull();
        result!.SchemaName.Should().Be(schema.SchemaName);

        // Verify it exists
        var exists = await service.SchemaExistsAsync(datasourceId, schema.SchemaName);
        exists.Should().BeTrue();
    }

    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql)]
    public async Task CreateSchemaAsync_UnsupportedProviders_ThrowsNotSupportedException(
        string datasourceId
    )
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var schema = new SchemaDto { SchemaName = "TestSchema" };

        var act = async () => await service.CreateSchemaAsync(datasourceId, schema);

        await act.Should()
            .ThrowAsync<NotSupportedException>()
            .WithMessage("*provider does not support schema operations*");
    }

    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql)]
    public async Task DropSchemaAsync_SupportedProviders_AllowsSchemaDrop(string datasourceId)
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        // First create a schema
        var schema = new SchemaDto { SchemaName = $"TempSchema_{Guid.NewGuid():N}"[..20] };
        await service.CreateSchemaAsync(datasourceId, schema);

        // Then drop it
        var result = await service.DropSchemaAsync(datasourceId, schema.SchemaName);

        result.Should().BeTrue();

        // Verify it's gone
        var exists = await service.SchemaExistsAsync(datasourceId, schema.SchemaName);
        exists.Should().BeFalse();
    }

    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql)]
    public async Task DropSchemaAsync_UnsupportedProviders_ThrowsNotSupportedException(
        string datasourceId
    )
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var act = async () => await service.DropSchemaAsync(datasourceId, "someschema");

        await act.Should()
            .ThrowAsync<NotSupportedException>()
            .WithMessage("*provider does not support schema operations*");
    }

    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer, "dbo", true)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer, "nonexistent", false)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql, "public", true)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql, "nonexistent", false)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql, "_", true)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql, "nonexistent", false)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite, "_", true)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite, "nonexistent", false)]
    public async Task SchemaExistsAsync_AllProviders_ReturnsCorrectResults(
        string datasourceId,
        string schemaName,
        bool expectedExists
    )
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var result = await service.SchemaExistsAsync(datasourceId, schemaName);

        result.Should().Be(expectedExists);
    }

    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer, "dbo")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql, "public")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql, "_")]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite, "_")]
    public async Task GetSchemaAsync_KnownSchemas_ReturnsSchema(
        string datasourceId,
        string schemaName
    )
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var result = await service.GetSchemaAsync(datasourceId, schemaName);

        result.Should().NotBeNull();
        result!.SchemaName.Should().Be(schemaName);
    }

    [Fact]
    public async Task SQLite_OnlyUnderscoreSchemaIsValid()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        // Only "_" should exist for SQLite
        var schemas = await service.GetSchemasAsync(
            TestcontainersAssemblyFixture.DatasourceId_Sqlite
        );
        schemas.Should().HaveCount(1);
        schemas.Should().Contain(s => s.SchemaName == "_");

        // Only "_" should be retrievable
        var validSchema = await service.GetSchemaAsync(
            TestcontainersAssemblyFixture.DatasourceId_Sqlite,
            "_"
        );
        validSchema.Should().NotBeNull();

        var invalidSchema = await service.GetSchemaAsync(
            TestcontainersAssemblyFixture.DatasourceId_Sqlite,
            "anything_else"
        );
        invalidSchema.Should().BeNull();

        // Only "_" should exist
        var underscoreExists = await service.SchemaExistsAsync(
            TestcontainersAssemblyFixture.DatasourceId_Sqlite,
            "_"
        );
        underscoreExists.Should().BeTrue();

        var otherExists = await service.SchemaExistsAsync(
            TestcontainersAssemblyFixture.DatasourceId_Sqlite,
            "other"
        );
        otherExists.Should().BeFalse();
    }

    [Fact]
    public async Task MySQL_OnlyUnderscoreSchemaIsValid()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        // Only "_" should exist for MySQL
        var schemas = await service.GetSchemasAsync(
            TestcontainersAssemblyFixture.DatasourceId_MySql
        );
        schemas.Should().HaveCount(1);
        schemas.Should().Contain(s => s.SchemaName == "_");

        // Only "_" should be retrievable
        var validSchema = await service.GetSchemaAsync(
            TestcontainersAssemblyFixture.DatasourceId_MySql,
            "_"
        );
        validSchema.Should().NotBeNull();

        var invalidSchema = await service.GetSchemaAsync(
            TestcontainersAssemblyFixture.DatasourceId_MySql,
            "anything_else"
        );
        invalidSchema.Should().BeNull();

        // Only "_" should exist
        var underscoreExists = await service.SchemaExistsAsync(
            TestcontainersAssemblyFixture.DatasourceId_MySql,
            "_"
        );
        underscoreExists.Should().BeTrue();

        var otherExists = await service.SchemaExistsAsync(
            TestcontainersAssemblyFixture.DatasourceId_MySql,
            "other"
        );
        otherExists.Should().BeFalse();
    }

    [Fact]
    public async Task SchemaOperations_CrossProvider_Consistency()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var testCases = new[]
        {
            (TestcontainersAssemblyFixture.DatasourceId_SqlServer, "dbo"),
            (TestcontainersAssemblyFixture.DatasourceId_PostgreSql, "public"),
            (TestcontainersAssemblyFixture.DatasourceId_MySql, "_"),
            (TestcontainersAssemblyFixture.DatasourceId_Sqlite, "_"),
        };

        foreach (var (datasourceId, expectedSchema) in testCases)
        {
            // All providers should have at least one schema
            var schemas = await service.GetSchemasAsync(datasourceId);
            schemas.Should().NotBeEmpty($"datasource {datasourceId} should have schemas");

            // The expected schema should exist
            var schema = await service.GetSchemaAsync(datasourceId, expectedSchema);
            schema.Should().NotBeNull($"schema {expectedSchema} should exist in {datasourceId}");

            // The expected schema should report as existing
            var exists = await service.SchemaExistsAsync(datasourceId, expectedSchema);
            exists.Should().BeTrue($"schema {expectedSchema} should exist in {datasourceId}");
        }
    }

    #endregion
}
