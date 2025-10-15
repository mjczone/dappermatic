// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MJCZone.DapperMatic.AspNetCore;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Models.Responses;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.AspNetCore.Tests.Factories;
using MJCZone.DapperMatic.AspNetCore.Tests.Infrastructure;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Endpoints;

/// <summary>
/// Integration tests for DapperMatic permissions and authorization.
/// </summary>
public class DatasourcePermissionsTests : IClassFixture<TestcontainersAssemblyFixture>
{
    private readonly TestcontainersAssemblyFixture _fixture;

    public DatasourcePermissionsTests(TestcontainersAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    #region Comprehensive Permission Workflow Tests

    [Fact]
    public async Task DatasourcePermissions_CompleteWorkflow_Success()
    {
        var testPermissions = new TestDapperMaticPermissions();

        // Set up permissions for complete workflow
        testPermissions.AllowOperation("datasources/list");
        testPermissions.AllowOperation("datasources/get");
        testPermissions.AllowOperation("datasources/add");
        testPermissions.AllowOperation("datasources/update");
        testPermissions.AllowOperation("datasources/remove");
        testPermissions.AllowOperation("datasources/exists");

        using var client = CreateClientWithPermissions(testPermissions);
        const string testDatasourceId = "PermissionWorkflowTest";

        // 1. LIST - Get initial datasource count
        var listResponse1 = await client.GetAsync("/api/dm/d/");
        listResponse1.StatusCode.Should().Be(HttpStatusCode.OK);
        var listResult1 = await listResponse1.ReadAsJsonAsync<DatasourceListResponse>();
        listResult1.Should().NotBeNull();
        listResult1!.Result.Should().NotBeNull();
        var initialCount = listResult1.Result!.Count();

        // 2. GET SINGLE - Try to get non-existent datasource (should return 404)
        var getResponse1 = await client.GetAsync($"/api/dm/d/{testDatasourceId}");
        getResponse1.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 3. CREATE - Create a new datasource
        var createRequest = new DatasourceDto
        {
            Id = testDatasourceId,
            Provider = "Sqlite",
            ConnectionString = "Data Source=:memory:",
            DisplayName = "Permission Workflow Test",
        };
        var createResponse = await client.PostAsJsonAsync("/api/dm/d/", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.ReadAsJsonAsync<DatasourceResponse>();
        createResult.Should().NotBeNull();
        createResult!.Result.Should().NotBeNull();
        createResult.Result!.Id.Should().Be(testDatasourceId);

        // 4. EXISTS - Check if datasource exists (should return true)
        var existsResponse1 = await client.GetAsync($"/api/dm/d/{testDatasourceId}/exists");
        existsResponse1.StatusCode.Should().Be(HttpStatusCode.OK);
        var existsResult1 = await existsResponse1.ReadAsJsonAsync<DatasourceTestResponse>();
        existsResult1.Should().NotBeNull();
        // Note: Using test response as there's no dedicated exists response

        // 5. LIST - List datasources again (should contain new datasource)
        var listResponse2 = await client.GetAsync("/api/dm/d/");
        listResponse2.StatusCode.Should().Be(HttpStatusCode.OK);
        var listResult2 = await listResponse2.ReadAsJsonAsync<DatasourceListResponse>();
        listResult2.Should().NotBeNull();
        listResult2!.Result.Should().NotBeNull();
        listResult2.Result.Should().HaveCount(initialCount + 1);
        listResult2.Result.Should().Contain(d => d.Id == testDatasourceId);

        // 6. GET SINGLE - Get the created datasource (should return datasource details)
        var getResponse2 = await client.GetAsync($"/api/dm/d/{testDatasourceId}");
        getResponse2.StatusCode.Should().Be(HttpStatusCode.OK);
        var getResult2 = await getResponse2.ReadAsJsonAsync<DatasourceResponse>();
        getResult2.Should().NotBeNull();
        getResult2!.Result.Should().NotBeNull();
        getResult2.Result!.Id.Should().Be(testDatasourceId);
        getResult2.Result.DisplayName.Should().Be("Permission Workflow Test");

        // 7. UPDATE - Update the datasource
        var updateRequest = new DatasourceDto
        {
            DisplayName = "Updated Permission Test",
        };
        var updateResponse = await client.PutAsJsonAsync($"/api/dm/d/{testDatasourceId}", updateRequest);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updateResult = await updateResponse.ReadAsJsonAsync<DatasourceResponse>();
        updateResult.Should().NotBeNull();
        updateResult!.Result.Should().NotBeNull();

        // 8. GET SINGLE - Get updated datasource (should show changes)
        var getResponse3 = await client.GetAsync($"/api/dm/d/{testDatasourceId}");
        getResponse3.StatusCode.Should().Be(HttpStatusCode.OK);
        var getResult3 = await getResponse3.ReadAsJsonAsync<DatasourceResponse>();
        getResult3.Should().NotBeNull();
        getResult3!.Result.Should().NotBeNull();
        getResult3.Result!.DisplayName.Should().Be("Updated Permission Test");

        // 9. DELETE - Delete the datasource
        var deleteResponse = await client.DeleteAsync($"/api/dm/d/{testDatasourceId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 10. EXISTS - Check if datasource exists (should return false)
        var existsResponse2 = await client.GetAsync($"/api/dm/d/{testDatasourceId}/exists");
        existsResponse2.StatusCode.Should().Be(HttpStatusCode.NotFound);
        // Note: Non-existent datasource returns 404

        // 11. LIST - List datasources (should be back to initial count)
        var listResponse3 = await client.GetAsync("/api/dm/d/");
        listResponse3.StatusCode.Should().Be(HttpStatusCode.OK);
        var listResult3 = await listResponse3.ReadAsJsonAsync<DatasourceListResponse>();
        listResult3.Should().NotBeNull();
        listResult3!.Result.Should().NotBeNull();
        listResult3.Result.Should().HaveCount(initialCount);
        listResult3.Result.Should().NotContain(d => d.Id == testDatasourceId);
    }

    [Fact]
    public async Task RoleBasedPermissions_CompleteWorkflow_Success()
    {
        var testPermissions = new TestDapperMaticPermissions();

        // Set up role-based permissions
        testPermissions.RequireRoleForOperation("datasources/list", "DataReader");
        testPermissions.RequireRoleForOperation("datasources/get", "DataReader");
        testPermissions.RequireRoleForOperation("datasources/add", "DataAdmin");
        testPermissions.RequireRoleForOperation("datasources/update", "DataAdmin");
        testPermissions.RequireRoleForOperation("datasources/remove", "DataAdmin");

        // Test with DataReader role (read operations only)
        var readerClaims = new[]
        {
            new Claim(ClaimTypes.Name, "reader"),
            new Claim(ClaimTypes.Role, "DataReader"),
        };

        using var readerClient = CreateClientWithPermissionsAndAuth(testPermissions, readerClaims);

        // Should allow read operations
        var listResponse = await readerClient.GetAsync("/api/dm/d/");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await readerClient.GetAsync("/api/dm/d/Test-SqlServer");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Should deny write operations
        var createRequest = new DatasourceDto
        {
            Id = "ReaderTestDatasource",
            Provider = "Sqlite",
            ConnectionString = "Data Source=:memory:",
            DisplayName = "Reader Test",
        };
        var createResponse = await readerClient.PostAsJsonAsync("/api/dm/d/", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Test with DataAdmin role (full access)
        var adminClaims = new[]
        {
            new Claim(ClaimTypes.Name, "admin"),
            new Claim(ClaimTypes.Role, "DataAdmin"),
        };

        using var adminClient = CreateClientWithPermissionsAndAuth(testPermissions, adminClaims);

        // Should allow all operations
        var adminListResponse = await adminClient.GetAsync("/api/dm/d/");
        adminListResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var adminCreateResponse = await adminClient.PostAsJsonAsync("/api/dm/d/", createRequest);
        adminCreateResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Clean up
        await adminClient.DeleteAsync($"/api/dm/d/{createRequest.Id}");
    }

    [Fact]
    public async Task DatasourceSpecificPermissions_CompleteWorkflow_Success()
    {
        var testPermissions = new TestDapperMaticPermissions();

        // Set up datasource-specific rules
        testPermissions.SetDatasourceRule("datasources/get", "Test-*", true);
        testPermissions.SetDatasourceRule("datasources/get", "Private-*", false);
        testPermissions.AllowOperation("datasources/add");
        testPermissions.AllowOperation("datasources/remove");

        using var client = CreateClientWithPermissions(testPermissions);

        // Should allow access to Test-* datasources
        var allowedResponse = await client.GetAsync("/api/dm/d/Test-SqlServer");
        allowedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Create a private datasource for testing
        var createPrivateRequest = new DatasourceDto
        {
            Id = "Private-TestDB",
            Provider = "Sqlite",
            ConnectionString = "Data Source=:memory:",
            DisplayName = "Private Database",
        };
        var createPrivateResponse = await client.PostAsJsonAsync("/api/dm/d/", createPrivateRequest);
        createPrivateResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        try
        {
            // Should deny access to Private-* datasources
            var deniedResponse = await client.GetAsync("/api/dm/d/Private-TestDB");
            deniedResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            // Clean up
            await client.DeleteAsync("/api/dm/d/Private-TestDB");
        }
    }

    #endregion

    #region Permission Denial Tests

    [Fact]
    public async Task DatasourcePermissions_DeniedOperations_ReturnsForbidden()
    {
        var testPermissions = new TestDapperMaticPermissions();

        // Explicitly deny all operations
        testPermissions.DenyOperation("datasources/list");
        testPermissions.DenyOperation("datasources/get");
        testPermissions.DenyOperation("datasources/add");
        testPermissions.DenyOperation("datasources/update");
        testPermissions.DenyOperation("datasources/remove");
        testPermissions.DenyOperation("datasources/exists");

        using var client = CreateClientWithPermissions(testPermissions);

        // All operations should return Forbidden
        var listResponse = await client.GetAsync("/api/dm/d/");
        listResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var getResponse = await client.GetAsync("/api/dm/d/Test-SqlServer");
        getResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var existsResponse = await client.GetAsync("/api/dm/d/Test-SqlServer/exists");
        existsResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var createRequest = new DatasourceDto
        {
            Id = "DeniedTest",
            Provider = "Sqlite",
            ConnectionString = "Data Source=:memory:",
            DisplayName = "Denied Test",
        };
        var createResponse = await client.PostAsJsonAsync("/api/dm/d/", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var updateRequest = new DatasourceDto { DisplayName = "Updated" };
        var updateResponse = await client.PutAsJsonAsync("/api/dm/d/Test-SqlServer", updateRequest);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var deleteResponse = await client.DeleteAsync("/api/dm/d/Test-SqlServer");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RoleBasedPermissions_InsufficientRoles_ReturnsForbidden()
    {
        var testPermissions = new TestDapperMaticPermissions();

        // Require high-privilege roles for operations
        testPermissions.RequireRoleForOperation("datasources/list", "SuperAdmin");
        testPermissions.RequireRoleForOperation("datasources/add", "DataAdmin");

        // Create user with insufficient roles
        var userClaims = new[]
        {
            new Claim(ClaimTypes.Name, "basicuser"),
            new Claim(ClaimTypes.Role, "User"),
        };

        using var client = CreateClientWithPermissionsAndAuth(testPermissions, userClaims);

        // Operations should be forbidden due to insufficient roles
        var listResponse = await client.GetAsync("/api/dm/d/");
        listResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var createRequest = new DatasourceDto
        {
            Id = "RoleTest",
            Provider = "Sqlite",
            ConnectionString = "Data Source=:memory:",
            DisplayName = "Role Test",
        };
        var createResponse = await client.PostAsJsonAsync("/api/dm/d/", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DatasourceSpecificDenials_PatternMatching_Success()
    {
        var testPermissions = new TestDapperMaticPermissions();

        // Set up complex pattern-based rules
        testPermissions.SetDatasourceRule("datasources/get", "Prod-*", false); // Deny production
        testPermissions.SetDatasourceRule("datasources/get", "Test-*", true);  // Allow test
        testPermissions.SetDatasourceRule("datasources/get", "Dev-*", true);   // Allow dev
        testPermissions.AllowOperation("datasources/add");
        testPermissions.AllowOperation("datasources/remove");

        using var client = CreateClientWithPermissions(testPermissions);

        // Test allowed patterns
        var testResponse = await client.GetAsync("/api/dm/d/Test-SqlServer");
        testResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Create datasources for testing patterns
        var devDatasource = new DatasourceDto
        {
            Id = "Dev-Database",
            Provider = "Sqlite",
            ConnectionString = "Data Source=:memory:",
            DisplayName = "Dev Database",
        };

        var prodDatasource = new DatasourceDto
        {
            Id = "Prod-Database",
            Provider = "Sqlite",
            ConnectionString = "Data Source=:memory:",
            DisplayName = "Prod Database",
        };

        // Create both datasources
        await client.PostAsJsonAsync("/api/dm/d/", devDatasource);
        await client.PostAsJsonAsync("/api/dm/d/", prodDatasource);

        try
        {
            // Dev should be allowed
            var devResponse = await client.GetAsync("/api/dm/d/Dev-Database");
            devResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Prod should be denied
            var prodResponse = await client.GetAsync("/api/dm/d/Prod-Database");
            prodResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            // Clean up
            await client.DeleteAsync("/api/dm/d/Dev-Database");
            await client.DeleteAsync("/api/dm/d/Prod-Database");
        }
    }

    #endregion

    #region Helper Methods

    private HttpClient CreateClientWithPermissions(TestDapperMaticPermissions permissions)
    {
        var factory = new WafWithInMemoryDatasourceRepository(
            _fixture.GetTestDatasources()
        ).WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Add authentication for testing
                services
                    .AddAuthentication("Test")
                    .AddScheme<
                        Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
                        TestAuthenticationHandler
                    >("Test", options => { });
                // Add empty test user for unauthenticated tests
                services.AddSingleton(new TestAuthenticationHandler.TestUser([]));

                // Replace the default permissions with our test permissions
                services.RemoveAll<IDapperMaticPermissions>();
                services.AddSingleton<IDapperMaticPermissions>(permissions);
            });
        });

        return factory.CreateClient();
    }

    private HttpClient CreateClientWithPermissionsAndAuth(
        TestDapperMaticPermissions permissions,
        Claim[] claims
    )
    {
        return new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources())
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Add authentication for testing
                    services
                        .AddAuthentication("Test")
                        .AddScheme<
                            Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
                            TestAuthenticationHandler
                        >("Test", options => { });
                    services.AddSingleton(new TestAuthenticationHandler.TestUser(claims));

                    // Replace the default permissions with our test permissions
                    services.RemoveAll<IDapperMaticPermissions>();
                    services.AddSingleton<IDapperMaticPermissions>(permissions);
                });
            })
            .CreateClient();
    }

    #endregion
}