// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MJCZone.DapperMatic.AspNetCore;
using MJCZone.DapperMatic.AspNetCore.Models.Requests;
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

    [Fact]
    public async Task GetDatasources_WithDenyOperation_ReturnsForbidden()
    {
        var testPermissions = new TestDapperMaticPermissions();
        testPermissions.DenyOperation(OperationIdentifiers.ListDatasources);

        using var client = CreateClientWithPermissions(testPermissions);

        var response = await client.GetAsync("/api/dm/d/");

        response.Should().HaveStatusCode(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetDatasources_WithAllowOperation_ReturnsSuccess()
    {
        var testPermissions = new TestDapperMaticPermissions();
        testPermissions.AllowOperation(OperationIdentifiers.ListDatasources);

        using var client = CreateClientWithPermissions(testPermissions);

        var response = await client.GetAsync("/api/dm/d/");

        response.Should().HaveStatusCode(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AddDatasource_WithRequiredRole_UserWithRole_ReturnsSuccess()
    {
        var testPermissions = new TestDapperMaticPermissions();
        testPermissions.RequireRoleForOperation(
            OperationIdentifiers.AddDatasource,
            "DataAdmin"
        );

        // Create an authenticated client with the required role
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Role, "DataAdmin"),
        };
        using var client = CreateClientWithPermissionsAndAuth(testPermissions, claims);

        var request = new CreateDatasourceRequest
        {
            Id = "PermissionTest",
            Provider = "Sqlite",
            ConnectionString = "Data Source=test.db",
            DisplayName = "Permission Test",
        };

        var response = await client.PostAsJsonAsync("/api/dm/d/", request);

        response.Should().HaveStatusCode(HttpStatusCode.Created);
    }

    [Fact]
    public async Task AddDatasource_WithRequiredRole_UserWithoutRole_ReturnsForbidden()
    {
        var testPermissions = new TestDapperMaticPermissions();
        testPermissions.RequireRoleForOperation(
            OperationIdentifiers.AddDatasource,
            "DataAdmin"
        );

        // Create an authenticated client without the required role
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Role, "User"),
        };
        using var client = CreateClientWithPermissionsAndAuth(testPermissions, claims);

        var request = new CreateDatasourceRequest
        {
            Id = "PermissionTest",
            Provider = "Sqlite",
            ConnectionString = "Data Source=test.db",
            DisplayName = "Permission Test",
        };

        var response = await client.PostAsJsonAsync("/api/dm/d/", request);

        response.Should().HaveStatusCode(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetDatasource_WithDatasourceSpecificRule_ReturnsExpectedResult()
    {
        var testPermissions = new TestDapperMaticPermissions();
        testPermissions.SetDatasourceRule(
            OperationIdentifiers.GetDatasource,
            "Test-*",
            true
        );
        testPermissions.SetDatasourceRule(
            OperationIdentifiers.GetDatasource,
            "Private-*",
            false
        );

        using var client = CreateClientWithPermissions(testPermissions);

        // Should allow access to Test-* datasources
        var response1 = await client.GetAsync("/api/dm/d/Test-SqlServer");
        response1.Should().HaveStatusCode(HttpStatusCode.OK);

        // Should deny access to Private-* datasources (would need to exist first)
        var response2 = await client.GetAsync("/api/dm/d/Private-Database");
        response2.Should().HaveStatusCode(HttpStatusCode.Forbidden);
    }

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
}
