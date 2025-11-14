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
    public async Task Can_manage_schema_Async(string datasourceId, string? schemaName)
    {
        using var factory = GetDefaultWebApplicationFactory();
        var service = GetDapperMaticService(factory);

        // Determine if provider supports schemas
        var supportsSchemas =
            datasourceId == TestcontainersAssemblyFixture.DatasourceId_SqlServer
            || datasourceId == TestcontainersAssemblyFixture.DatasourceId_PostgreSql;

        // Non-existent datasource throws NotFound
        await CheckInvalidDatasourceHandlingFetchingSchemas(service);

        // Non-existent schema throws NotFound (only for providers that support schemas)
        if (supportsSchemas)
        {
            var invalidSchemaContext = OperationIdentifiers.ForSchemaGet(datasourceId, "NonExistentSchema");
            var invalidSchemaAct = async () =>
                await service.GetSchemaAsync(invalidSchemaContext, datasourceId, "NonExistentSchema");
            await invalidSchemaAct.Should().ThrowAsync<KeyNotFoundException>();
        }

        // Retrieve schemas (should match expectations)
        var listContext = OperationIdentifiers.ForSchemaList(datasourceId);
        var schemas = await service.GetSchemasAsync(listContext, datasourceId);
        schemas.Should().NotBeNull();

        if (supportsSchemas)
        {
            schemas.Should().NotBeEmpty();
            schemas.Should().Contain(s => s.SchemaName == schemaName);
        }
        else
        {
            schemas.Should().BeEmpty();
        }

        // Test schema operations only for providers that support them
        if (!supportsSchemas)
        {
            // Test that unsupported providers throw InvalidOperationException
            var testSchema = new SchemaDto { SchemaName = "TestSchema" };
            var createContext = OperationIdentifiers.ForSchemaCreate(datasourceId, testSchema);
            var createAct = async () => await service.CreateSchemaAsync(createContext, datasourceId, testSchema);
            await createAct.Should().ThrowAsync<InvalidOperationException>();

            var dropContext = OperationIdentifiers.ForSchemaDrop(datasourceId, "TestSchema");
            var dropAct = async () => await service.DropSchemaAsync(dropContext, datasourceId, "TestSchema");
            await dropAct.Should().ThrowAsync<InvalidOperationException>();

            return; // Skip remaining tests for unsupported providers
        }

        // Add test schema
        var newSchemaName = "TestSchema_" + Guid.NewGuid().ToString("N")[..8];
        var createSchemaRequest = new SchemaDto { SchemaName = newSchemaName };
        var createSchemaContext = OperationIdentifiers.ForSchemaCreate(datasourceId, createSchemaRequest);
        var createdSchema = await service.CreateSchemaAsync(createSchemaContext, datasourceId, createSchemaRequest);
        createdSchema.Should().NotBeNull();
        createdSchema!.SchemaName.Should().Be(newSchemaName);

        // Add second test schema
        var newSchemaName2 = "TestSchema2_" + Guid.NewGuid().ToString("N")[..8];
        var createSchemaRequest2 = new SchemaDto { SchemaName = newSchemaName2 };
        var createSchemaContext2 = OperationIdentifiers.ForSchemaCreate(datasourceId, createSchemaRequest2);
        var createdSchema2 = await service.CreateSchemaAsync(createSchemaContext2, datasourceId, createSchemaRequest2);
        createdSchema2.Should().NotBeNull();

        // Retrieve schemas again
        var schemasAfterCreation = await service.GetSchemasAsync(listContext, datasourceId);
        schemasAfterCreation
            .Should()
            .Contain(s => s.SchemaName.Equals(newSchemaName, StringComparison.OrdinalIgnoreCase));
        schemasAfterCreation
            .Should()
            .Contain(s => s.SchemaName.Equals(newSchemaName, StringComparison.OrdinalIgnoreCase));

        // Verify single schema exists
        var schemaContext = OperationIdentifiers.ForSchemaGet(datasourceId, newSchemaName);
        var retrievedSchema = await service.GetSchemaAsync(schemaContext, datasourceId, newSchemaName);
        retrievedSchema.Should().NotBeNull();
        retrievedSchema!.SchemaName.Should().Be(newSchemaName);

        // Check schema existence
        var existsContext = OperationIdentifiers.ForSchemaExists(datasourceId, newSchemaName);
        var exists = await service.SchemaExistsAsync(existsContext, datasourceId, newSchemaName);
        exists.Should().BeTrue();

        var nonExistentExistsContext = OperationIdentifiers.ForSchemaExists(datasourceId, "NonExistentSchema");
        var nonExistentExists = await service.SchemaExistsAsync(
            nonExistentExistsContext,
            datasourceId,
            "NonExistentSchema"
        );
        nonExistentExists.Should().BeFalse();

        // Attempt to add duplicate schema (should return null or fail)
        var duplicateSchema = new SchemaDto { SchemaName = newSchemaName };
        var duplicateContext = OperationIdentifiers.ForSchemaCreate(datasourceId, duplicateSchema);
        await Assert.ThrowsAsync<DuplicateKeyException>(async () =>
            await service.CreateSchemaAsync(duplicateContext, datasourceId, duplicateSchema)
        );

        // Drop first schema
        var dropSchemaContext = OperationIdentifiers.ForSchemaDrop(datasourceId, newSchemaName);
        await service.DropSchemaAsync(dropSchemaContext, datasourceId, newSchemaName);

        // Verify schema dropped
        var existsAfterDrop = await service.SchemaExistsAsync(existsContext, datasourceId, newSchemaName);
        existsAfterDrop.Should().BeFalse();

        var getDroppedAct = async () => await service.GetSchemaAsync(schemaContext, datasourceId, newSchemaName);
        await getDroppedAct.Should().ThrowAsync<KeyNotFoundException>();

        // Cleanup - drop second test schema
        var dropSchemaContext2 = OperationIdentifiers.ForSchemaDrop(datasourceId, newSchemaName2);
        await service.DropSchemaAsync(dropSchemaContext2, datasourceId, newSchemaName2);
    }

    private async Task CheckInvalidDatasourceHandlingFetchingSchemas(IDapperMaticService service)
    {
        var invalidDatasourceId = "NonExistent";
        var invalidContext = OperationIdentifiers.ForSchemaList(invalidDatasourceId);
        var invalidAct = async () => await service.GetSchemasAsync(invalidContext, invalidDatasourceId);
        await invalidAct.Should().ThrowAsync<KeyNotFoundException>();
    }
}
