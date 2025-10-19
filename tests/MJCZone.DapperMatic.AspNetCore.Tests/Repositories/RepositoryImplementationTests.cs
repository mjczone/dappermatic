// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Models.Responses;
using MJCZone.DapperMatic.AspNetCore.Repositories;
using MJCZone.DapperMatic.AspNetCore.Tests.Factories;
using MJCZone.DapperMatic.AspNetCore.Tests.Infrastructure;
using Testcontainers.MsSql;
using Xunit.Abstractions;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Repositories;

/// <summary>
/// Integration tests for different repository implementations.
/// </summary>
public class RepositoryImplementationTests(
    TestcontainersAssemblyFixture fixture,
    ITestOutputHelper outputHelper
) : IClassFixture<TestcontainersAssemblyFixture>
{
    private readonly TestcontainersAssemblyFixture _fixture = fixture;
    private readonly ITestOutputHelper _outputHelper = outputHelper;

    [Fact]
    public async Task Should_handle_in_memory_repository_data_persists_within_application_but_not_between_restarts_Async()
    {
        var addRequest = new DatasourceDto
        {
            Id = "Test-InMemoryPersistence",
            Provider = "Sqlite",
            ConnectionString = "Data Source=in-memory-test.db",
            DisplayName = "In-Memory Test",
        };

        using (var waFactory = new WafWithInMemoryDatasourceRepository([]))
        {
            // Arrange
            var client = waFactory.CreateClient();

            var addResponse = await client.PostAsJsonAsync("/api/dm/d/", addRequest);
            if (addResponse.StatusCode != HttpStatusCode.Created)
            {
                var errorContent = await addResponse.Content.ReadAsStringAsync();
                throw new Exception($"Add failed with {addResponse.StatusCode}: {errorContent}");
            }
            addResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            // Verify datasource exists in same application instance
            var getResponse1 = await client.GetAsync($"/api/dm/d/{addRequest.Id}");
            getResponse1.StatusCode.Should().Be(HttpStatusCode.OK);

            await waFactory.DisposeAsync();
        }

        using (var waFactory = new WafWithInMemoryDatasourceRepository([]))
        {
            // Arrange
            var client = waFactory.CreateClient();

            // Verify datasource does NOT exist in new application instance
            var getResponse2 = await client.GetAsync($"/api/dm/d/{addRequest.Id}");
            getResponse2.StatusCode.Should().Be(HttpStatusCode.NotFound);

            await waFactory.DisposeAsync();
        }
    }

    [Fact]
    public async Task Should_handle_file_repository_data_persists_between_application_restarts_Async()
    {
        WafWithFileDatasourceRepository.DeleteDatasourcesFile();

        var addRequest = new DatasourceDto
        {
            Id = "Test-FilePersistence",
            Provider = "Sqlite",
            ConnectionString = "Data Source=file-test.db",
            DisplayName = "File Repository Test",
            Description = "Test file persistence",
        };

        // Arrange & Act - First application instance with file repository
        using (var factory1 = new WafWithFileDatasourceRepository([]))
        {
            using var client1 = factory1.CreateClient();

            var addResponse = await client1.PostAsJsonAsync("/api/dm/d/", addRequest);
            if (addResponse.StatusCode != HttpStatusCode.Created)
            {
                var errorContent = await addResponse.Content.ReadAsStringAsync();
                throw new Exception($"Add failed with {addResponse.StatusCode}: {errorContent}");
            }
            addResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            // Verify datasource exists
            var getResponse1 = await client1.GetAsync($"/api/dm/d/{addRequest.Id}");
            getResponse1.StatusCode.Should().Be(HttpStatusCode.OK);
            var result1 = await getResponse1.ReadAsJsonAsync<DatasourceResponse>();
            result1.Should().NotBeNull();
            result1!.Result.Should().NotBeNull();
            result1.Result!.Id.Should().Be(addRequest.Id);

            await factory1.DisposeAsync();
        }

        // Act - Second application instance with same file (simulates restart)
        using (var factory2 = new WafWithFileDatasourceRepository([]))
        {
            using var client2 = factory2.CreateClient();

            // Verify datasource DOES exist in new application instance
            var getResponse2 = await client2.GetAsync($"/api/dm/d/{addRequest.Id}");
            getResponse2.StatusCode.Should().Be(HttpStatusCode.OK);
            var result2 = await getResponse2.ReadAsJsonAsync<DatasourceResponse>();
            result2.Should().NotBeNull();
            result2!.Result.Should().NotBeNull();
            result2.Result!.Id.Should().Be(addRequest.Id);
            result2.Result.DisplayName.Should().Be(addRequest.DisplayName);
            result2.Result.Description.Should().Be(addRequest.Description);

            await factory2.DisposeAsync();
        }

        WafWithFileDatasourceRepository.DeleteDatasourcesFile();
    }

    [Fact]
    public async Task Should_handle_database_repository_data_persists_between_application_restarts_Async()
    {
        WafWithDatabaseDatasourceRepository.DeleteDatabaseFile();

        var addRequest = new DatasourceDto
        {
            Id = "Test-DatabasePersistence",
            Provider = "Sqlite",
            ConnectionString = "Data Source=db-test.db",
            DisplayName = "Database Repository Test",
            Description = "Test database persistence",
            Tags = ["test", "database"],
        };

        // Arrange & Act - First application instance with database repository
        using (var factory1 = new WafWithDatabaseDatasourceRepository([]))
        {
            using var client1 = factory1.CreateClient();

            var addResponse = await client1.PostAsJsonAsync("/api/dm/d/", addRequest);
            if (addResponse.StatusCode != HttpStatusCode.Created)
            {
                var errorContent = await addResponse.Content.ReadAsStringAsync();
                throw new Exception($"Add failed with {addResponse.StatusCode}: {errorContent}");
            }
            addResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            // Verify datasource exists
            var getResponse1 = await client1.GetAsync($"/api/dm/d/{addRequest.Id}");
            getResponse1.StatusCode.Should().Be(HttpStatusCode.OK);
            var result1 = await getResponse1.ReadAsJsonAsync<DatasourceResponse>();
            result1.Should().NotBeNull();
            result1!.Result.Should().NotBeNull();
            result1.Result!.Id!.ToLowerInvariant().Should().Be(addRequest.Id.ToLowerInvariant());

            await factory1.DisposeAsync();
        }

        // Act - Second application instance with same database (simulates restart)
        using (var factory2 = new WafWithDatabaseDatasourceRepository([]))
        {
            using var client2 = factory2.CreateClient();

            // Verify datasource DOES exist in new application instance
            var getResponse2 = await client2.GetAsync($"/api/dm/d/{addRequest.Id}");
            getResponse2.StatusCode.Should().Be(HttpStatusCode.OK);
            var result2 = await getResponse2.ReadAsJsonAsync<DatasourceResponse>();
            result2.Should().NotBeNull();
            result2!.Result.Should().NotBeNull();
            result2.Result!.Id!.ToLowerInvariant().Should().Be(addRequest.Id.ToLowerInvariant());
            result2.Result.DisplayName.Should().Be(addRequest.DisplayName);
            result2.Result.Description.Should().Be(addRequest.Description);
            result2.Result.Tags.Should().BeEquivalentTo(addRequest.Tags);

            factory2.DeleteDatabase();
        }
    }

    [Fact]
    public async Task Should_require_all_repository_types_support_basic_crud_operations_Async()
    {
        // Clean up any existing files before starting
        WafWithDatabaseDatasourceRepository.DeleteDatabaseFile();
        WafWithFileDatasourceRepository.DeleteDatasourcesFile();

        // Add a small delay to ensure file system operations complete
        await Task.Delay(100);

        var testDatasource = new DatasourceDto
        {
            Id = "CRUD-Test",
            Provider = "Sqlite",
            ConnectionString = "Data Source=crud-test.db",
            DisplayName = "CRUD Test Datasource",
            Description = "Testing CRUD operations",
            Tags = ["crud", "test"],
            IsEnabled = true,
        };

        // Generate a unique identifier for this test run to avoid conflicts
        var testId = Guid.NewGuid().ToString("N")[..8];

        var repositoryFactories = new Func<WebApplicationFactory<Program>>[]
        {
            () => new WafWithInMemoryDatasourceRepository([]),
            () => new WafWithFileDatasourceRepository([]),
            () => new WafWithDatabaseDatasourceRepository([], $"crud-test-{testId}.db"),
        };

        foreach (var createFactory in repositoryFactories)
        {
            using var factory = createFactory();
            using var client = factory.CreateClient();

            // CREATE
            var addResponse = await client.PostAsJsonAsync("/api/dm/d/", testDatasource);
            if (addResponse.StatusCode != HttpStatusCode.Created)
            {
                var errorContent = await addResponse.Content.ReadAsStringAsync();
                throw new Exception(
                    $"Add failed for {factory.GetType().Name} with {addResponse.StatusCode}: {errorContent}"
                );
            }
            addResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            // READ
            var getResponse = await client.GetAsync($"/api/dm/d/{testDatasource.Id}");
            getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var getResult = await getResponse.ReadAsJsonAsync<DatasourceResponse>();
            getResult.Should().NotBeNull();
            getResult!.Result.Should().NotBeNull();
            getResult
                .Result!.Id!.ToLowerInvariant()
                .Should()
                .Be(testDatasource.Id.ToLowerInvariant());

            // UPDATE
            var updateRequest = new DatasourceDto
            {
                DisplayName = "Updated CRUD Test",
                Description = "Updated description",
                IsEnabled = false,
            };
            var updateResponse = await client.PutAsJsonAsync(
                $"/api/dm/d/{testDatasource.Id}",
                updateRequest
            );
            // If not OK, read the error content for debugging
            if (updateResponse.StatusCode != HttpStatusCode.OK)
            {
                var errorContent = await updateResponse.Content.ReadAsStringAsync();
                _outputHelper.WriteLine(
                    $"Update failed for {factory.GetType().Name} with {updateResponse.StatusCode}: {errorContent}"
                );
            }
            updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Verify update
            var getUpdatedResponse = await client.GetAsync($"/api/dm/d/{testDatasource.Id}");
            var getUpdatedResult = await getUpdatedResponse.ReadAsJsonAsync<DatasourceResponse>();
            getUpdatedResult!.Result!.DisplayName.Should().Be("Updated CRUD Test");
            getUpdatedResult.Result.IsEnabled.Should().BeFalse();

            // DELETE
            var deleteResponse = await client.DeleteAsync($"/api/dm/d/{testDatasource.Id}");
            deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            // Verify deletion
            var getDeletedResponse = await client.GetAsync($"/api/dm/d/{testDatasource.Id}");
            getDeletedResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

            // Ensure proper disposal and cleanup after each factory iteration
            client.Dispose();
            factory.Dispose();

            // Force garbage collection to release any database connections
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // Add cleanup delay
            await Task.Delay(50);

            if (factory is WafWithFileDatasourceRepository fileFactory)
            {
                fileFactory.DeleteFile();
            }
            else if (factory is WafWithDatabaseDatasourceRepository dbFactory)
            {
                dbFactory.DeleteDatabase();
            }
        }

        // Final cleanup
        WafWithDatabaseDatasourceRepository.DeleteDatabaseFile();
        WafWithFileDatasourceRepository.DeleteDatasourcesFile();
    }
}
