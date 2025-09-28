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

        var operationContext = OperationIdentifiers.ForSchemaList(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer
        );
        var result = await service.GetSchemasAsync(
            operationContext,
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

        var operationContext = OperationIdentifiers.ForSchemaList(
            TestcontainersAssemblyFixture.DatasourceId_PostgreSql
        );
        var result = await service.GetSchemasAsync(
            operationContext,
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

        var operationContext = OperationIdentifiers.ForSchemaList("NonExistent");
        var act = async () => await service.GetSchemasAsync(operationContext, "NonExistent");

        await act.Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("Datasource 'NonExistent' not found. (Parameter 'datasourceId')");
    }

    [Fact]
    public async Task GetSchemaAsync_SqlServer_dbo_ReturnsSchema()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var operationContext = OperationIdentifiers.ForSchemaGet(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "dbo"
        );
        var result = await service.GetSchemaAsync(
            operationContext,
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

        var operationContext = OperationIdentifiers.ForSchemaGet(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "nonexistent"
        );
        var result = await service.GetSchemaAsync(
            operationContext,
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
        var createContext = OperationIdentifiers.ForSchemaCreate(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            schema
        );
        var result = await service.CreateSchemaAsync(
            createContext,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            schema
        );

        result.Should().NotBeNull();
        result!.SchemaName.Should().Be("TestSchema");

        // Verify it was created
        var existsContext = OperationIdentifiers.ForSchemaExists(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "TestSchema"
        );
        var exists = await service.SchemaExistsAsync(
            existsContext,
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
        var createContext = OperationIdentifiers.ForSchemaCreate(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            schema
        );
        var result = await service.CreateSchemaAsync(
            createContext,
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
        var createContext = OperationIdentifiers.ForSchemaCreate(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            schema
        );
        await service.CreateSchemaAsync(
            createContext,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            schema
        );

        // Then drop it
        var dropContext = OperationIdentifiers.ForSchemaDrop(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "TempSchema"
        );
        await service.DropSchemaAsync(
            dropContext,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "TempSchema"
        );

        // Verify it was dropped
        var existsContext = OperationIdentifiers.ForSchemaExists(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "TempSchema"
        );
        var exists = await service.SchemaExistsAsync(
            existsContext,
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

        var dropContext = OperationIdentifiers.ForSchemaDrop(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "nonexistent"
        );
        await service.DropSchemaAsync(
            dropContext,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "nonexistent"
        );

        // Drop operation doesn't return a result - it either succeeds or throws an exception
    }

    [Fact]
    public async Task SchemaExistsAsync_SqlServer_dbo_ReturnsTrue()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var existsContext = OperationIdentifiers.ForSchemaExists(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "dbo"
        );
        var result = await service.SchemaExistsAsync(
            existsContext,
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

        var existsContext = OperationIdentifiers.ForSchemaExists(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            "nonexistent"
        );
        var result = await service.SchemaExistsAsync(
            existsContext,
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

        var context = OperationIdentifiers.ForSchemaList(datasourceId!);
        var act = async () => await service.GetSchemasAsync(context, datasourceId!);

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

        var context = OperationIdentifiers.ForSchemaGet(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            schemaName!
        );
        var act = async () =>
            await service.GetSchemaAsync(
                context,
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

        var context = OperationIdentifiers.ForSchemaCreate(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            null!
        );
        var act = async () =>
            await service.CreateSchemaAsync(
                context,
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
        var context = OperationIdentifiers.ForSchemaCreate(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            schema
        );
        var act = async () =>
            await service.CreateSchemaAsync(
                context,
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

        var operationContext = OperationIdentifiers.ForSchemaList(
            TestcontainersAssemblyFixture.DatasourceId_SqlServer,
            user
        );
        var result = await service.GetSchemasAsync(
            operationContext,
            TestcontainersAssemblyFixture.DatasourceId_SqlServer
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

        var operationContext = OperationIdentifiers.ForSchemaList(datasourceId);
        var result = await service.GetSchemasAsync(operationContext, datasourceId);

        result.Should().NotBeNull();
        result.Should().NotBeEmpty();

        if (supportsSchemas)
        {
            // Providers that support schemas should return actual schema names
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
            // Providers that don't support schemas
            result.Should().HaveCount(0);
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

        var createContext = OperationIdentifiers.ForSchemaCreate(datasourceId, schema);
        var result = await service.CreateSchemaAsync(createContext, datasourceId, schema);

        result.Should().NotBeNull();
        result!.SchemaName.Should().Be(schema.SchemaName);

        // Verify it exists
        var existsContext = OperationIdentifiers.ForSchemaExists(datasourceId, schema.SchemaName);
        var exists = await service.SchemaExistsAsync(
            existsContext,
            datasourceId,
            schema.SchemaName
        );
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

        var operationContext = OperationIdentifiers.ForSchemaCreate(datasourceId, schema);
        var act = async () =>
            await service.CreateSchemaAsync(operationContext, datasourceId, schema);

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
        var createContext = OperationIdentifiers.ForSchemaCreate(datasourceId, schema);
        await service.CreateSchemaAsync(createContext, datasourceId, schema);

        // Then drop it
        var dropContext = OperationIdentifiers.ForSchemaDrop(datasourceId, schema.SchemaName);
        await service.DropSchemaAsync(dropContext, datasourceId, schema.SchemaName);

        // Verify it's gone
        var existsContext = OperationIdentifiers.ForSchemaExists(datasourceId, schema.SchemaName);
        var exists = await service.SchemaExistsAsync(
            existsContext,
            datasourceId,
            schema.SchemaName
        );
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

        var operationContext = OperationIdentifiers.ForSchemaDrop(datasourceId, "someschema");
        var act = async () =>
            await service.DropSchemaAsync(operationContext, datasourceId, "someschema");

        await act.Should()
            .ThrowAsync<NotSupportedException>()
            .WithMessage("*provider does not support schema operations*");
    }

    [Theory]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer, "dbo", true)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer, "nonexistent", false)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql, "public", true)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql, "nonexistent", false)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql, "_", false)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql, "nonexistent", false)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite, "_", false)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite, "nonexistent", false)]
    public async Task SchemaExistsAsync_AllProviders_ReturnsCorrectResults(
        string datasourceId,
        string schemaName,
        bool expectedExists
    )
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        var operationContext = OperationIdentifiers.ForSchemaExists(datasourceId, schemaName);
        var result = await service.SchemaExistsAsync(operationContext, datasourceId, schemaName);

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

        var operationContext = OperationIdentifiers.ForSchemaGet(datasourceId, schemaName);
        var result = await service.GetSchemaAsync(operationContext, datasourceId, schemaName);

        result.Should().NotBeNull();
        result!.SchemaName.Should().Be(schemaName);
    }

    [Fact]
    public async Task Providers_WithoutSchemas_SchemasAreInvalid()
    {
        using var factory = new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
        var service = factory.Services.GetRequiredService<IDapperMaticService>();

        foreach (
            var dsId in new[]
            {
                TestcontainersAssemblyFixture.DatasourceId_Sqlite,
                TestcontainersAssemblyFixture.DatasourceId_MySql,
            }
        )
        {
            var listContext = OperationIdentifiers.ForSchemaList(dsId);
            var schemas = await service.GetSchemasAsync(listContext, dsId);
            schemas.Should().HaveCount(0);

            var validSchemaContext = OperationIdentifiers.ForSchemaGet(dsId, "_");
            var validSchema = await service.GetSchemaAsync(validSchemaContext, dsId, "_");
            validSchema.Should().BeNull();

            var invalidSchemaContext = OperationIdentifiers.ForSchemaGet(dsId, "anything_else");
            var invalidSchema = await service.GetSchemaAsync(
                invalidSchemaContext,
                dsId,
                "anything_else"
            );
            invalidSchema.Should().BeNull();

            var underscoreExistsContext = OperationIdentifiers.ForSchemaExists(dsId, "_");
            var underscoreExists = await service.SchemaExistsAsync(
                underscoreExistsContext,
                dsId,
                "_"
            );
            underscoreExists.Should().BeFalse();

            var otherExistsContext = OperationIdentifiers.ForSchemaExists(dsId, "other");
            var otherExists = await service.SchemaExistsAsync(otherExistsContext, dsId, "other");
            otherExists.Should().BeFalse();
        }
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
            var listContext = OperationIdentifiers.ForSchemaList(datasourceId);
            var schemas = await service.GetSchemasAsync(listContext, datasourceId);
            schemas.Should().NotBeEmpty($"datasource {datasourceId} should have schemas");

            // The expected schema should exist
            var getContext = OperationIdentifiers.ForSchemaGet(datasourceId, expectedSchema);
            var schema = await service.GetSchemaAsync(getContext, datasourceId, expectedSchema);
            schema.Should().NotBeNull($"schema {expectedSchema} should exist in {datasourceId}");

            // The expected schema should report as existing
            var existsContext = OperationIdentifiers.ForSchemaExists(datasourceId, expectedSchema);
            var exists = await service.SchemaExistsAsync(
                existsContext,
                datasourceId,
                expectedSchema
            );
            exists.Should().BeTrue($"schema {expectedSchema} should exist in {datasourceId}");
        }
    }

    #endregion
}
