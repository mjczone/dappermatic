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
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_SqlServer)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_PostgreSql)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_MySql)]
    [InlineData(TestcontainersAssemblyFixture.DatasourceId_Sqlite)]
    public async Task Can_manage_datasource_Async(string datasourceId)
    {
        using var factory = GetDefaultWebApplicationFactory();
        var service = GetDapperMaticService(factory);

        // Get initial datasources (should contain test datasources from fixture)
        var listContext = OperationIdentifiers.ForDatasourceList();
        var initialDatasources = await service.GetDatasourcesAsync(listContext);
        initialDatasources.Should().NotBeNull();
        var initialCount = initialDatasources.Count();
        initialCount.Should().BeGreaterThanOrEqualTo(4); // At least 4 test datasources

        // Verify single datasource exists
        var getContext = OperationIdentifiers.ForDatasourceGet(datasourceId);
        var existingDatasource = await service.GetDatasourceAsync(getContext, datasourceId);
        existingDatasource.Should().NotBeNull();
        existingDatasource!.Id.Should().Be(datasourceId);
        existingDatasource.IsEnabled.Should().BeTrue();

        // Check datasource exists
        var existsContext = OperationIdentifiers.ForDatasourceExists(datasourceId);
        var exists = await service.DatasourceExistsAsync(existsContext, datasourceId);
        exists.Should().BeTrue();

        // Non-existent datasource throws NotFound
        var nonExistentId = "NonExistent_" + Guid.NewGuid().ToString("N")[..8];
        var nonExistentGetContext = OperationIdentifiers.ForDatasourceGet(nonExistentId);
        var nonExistentAct = async () => await service.GetDatasourceAsync(nonExistentGetContext, nonExistentId);
        await nonExistentAct.Should().ThrowAsync<KeyNotFoundException>();

        // Check non-existent datasource
        var nonExistentExistsContext = OperationIdentifiers.ForDatasourceExists(nonExistentId);
        var nonExistentExists = await service.DatasourceExistsAsync(nonExistentExistsContext, nonExistentId);
        nonExistentExists.Should().BeFalse();

        // Add test datasource
        var newDatasourceId = "TestDS_" + Guid.NewGuid().ToString("N")[..8];
        var newDatasource = new DatasourceDto
        {
            Id = newDatasourceId,
            Provider = "Sqlite",
            ConnectionString = "Data Source=:memory:",
            DisplayName = "Test Datasource",
            Description = "A test datasource for service tests",
            Tags = ["test", "service"],
            IsEnabled = true,
        };
        var addContext = OperationIdentifiers.ForDatasourceAdd(newDatasource);
        var addedDatasource = await service.AddDatasourceAsync(addContext, newDatasource);
        addedDatasource.Should().NotBeNull();
        addedDatasource!.Id.Should().Be(newDatasourceId);
        addedDatasource.DisplayName.Should().Be("Test Datasource");

        // Verify datasource added
        var datasourcesAfterAdd = await service.GetDatasourcesAsync(listContext);
        datasourcesAfterAdd.Should().HaveCount(initialCount + 1);
        datasourcesAfterAdd.Should().Contain(d => d.Id == newDatasourceId);

        // Attempt to add duplicate datasource (should fail)
        var duplicateAct = async () => await service.AddDatasourceAsync(addContext, newDatasource);
        await duplicateAct.Should().ThrowAsync<DuplicateKeyException>();

        // Update datasource
        var updateDatasource = new DatasourceDto
        {
            Id = newDatasourceId,
            Provider = "Sqlite",
            ConnectionString = "Data Source=updated.db",
            DisplayName = "Updated Test Datasource",
            Description = "Updated description",
            Tags = ["updated"],
            IsEnabled = false,
        };
        var updateContext = OperationIdentifiers.ForDatasourceUpdate(updateDatasource);
        var updatedDatasource = await service.UpdateDatasourceAsync(updateContext, updateDatasource);
        updatedDatasource.Should().NotBeNull();
        updatedDatasource!.DisplayName.Should().Be("Updated Test Datasource");
        updatedDatasource.IsEnabled.Should().BeFalse();
        updatedDatasource.Tags.Should().Contain("updated");
        updatedDatasource.Tags.Should().NotContain("test");

        // Verify datasource updated
        var updatedGetContext = OperationIdentifiers.ForDatasourceGet(newDatasourceId);
        var retrievedUpdated = await service.GetDatasourceAsync(updatedGetContext, newDatasourceId);
        retrievedUpdated.Should().NotBeNull();
        retrievedUpdated!.DisplayName.Should().Be("Updated Test Datasource");
        retrievedUpdated.IsEnabled.Should().BeFalse();

        // Test datasource connectivity
        var testContext = OperationIdentifiers.ForDatasourceTest(newDatasourceId);
        var testResult = await service.TestDatasourceAsync(testContext, newDatasourceId);
        testResult.Should().NotBeNull();
        testResult.Connected.Should().BeTrue();

        // Remove datasource
        var removeContext = OperationIdentifiers.ForDatasourceRemove(newDatasourceId);
        await service.RemoveDatasourceAsync(removeContext, newDatasourceId);

        // Verify datasource removed using GetDatasources
        var datasourcesAfterRemove = await service.GetDatasourcesAsync(listContext);
        datasourcesAfterRemove.Should().HaveCount(initialCount);
        datasourcesAfterRemove.Should().NotContain(d => d.Id == newDatasourceId);

        // Verify datasource removed using GetDatasource (should throw)
        var getRemovedAct = async () => await service.GetDatasourceAsync(updatedGetContext, newDatasourceId);
        await getRemovedAct.Should().ThrowAsync<KeyNotFoundException>();

        // Verify datasource removed using DatasourceExists
        var removedExists = await service.DatasourceExistsAsync(nonExistentExistsContext, newDatasourceId);
        removedExists.Should().BeFalse();
    }
}
