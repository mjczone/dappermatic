// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore;
using MJCZone.DapperMatic.AspNetCore.Security;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Security;

/// <summary>
/// Unit tests for DefaultDapperMaticPermissions.
/// </summary>
public class DefaultDapperMaticPermissionsTests
{
    [Fact]
    public async Task IsAuthorizedAsync_AllowAll_ReturnsTrue()
    {
        var options = Options.Create(new DapperMaticOptions());
        var permissions = new DefaultDapperMaticPermissions(options, PermissionDefault.AllowAll);
        var context = CreateOperationContext("test/operation");

        var result = await permissions.IsAuthorizedAsync(context);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAuthorizedAsync_RequireAuthentication_UnauthenticatedUser_ReturnsFalse()
    {
        var options = Options.Create(new DapperMaticOptions());
        var permissions = new DefaultDapperMaticPermissions(
            options,
            PermissionDefault.RequireAuthentication
        );
        var context = CreateOperationContext("test/operation", authenticated: false);

        var result = await permissions.IsAuthorizedAsync(context);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsAuthorizedAsync_RequireAuthentication_AuthenticatedUser_ReturnsTrue()
    {
        var options = Options.Create(new DapperMaticOptions());
        var permissions = new DefaultDapperMaticPermissions(
            options,
            PermissionDefault.RequireAuthentication
        );
        var context = CreateOperationContext("test/operation", authenticated: true);

        var result = await permissions.IsAuthorizedAsync(context);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAuthorizedAsync_RequireRole_UserWithRole_ReturnsTrue()
    {
        var options = Options.Create(new DapperMaticOptions { RequireRole = "DataAdmin" });
        var permissions = new DefaultDapperMaticPermissions(options);
        var context = CreateOperationContext(
            "test/operation",
            authenticated: true,
            roles: ["DataAdmin"]
        );

        var result = await permissions.IsAuthorizedAsync(context);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAuthorizedAsync_RequireRole_UserWithoutRole_ReturnsFalse()
    {
        var options = Options.Create(new DapperMaticOptions { RequireRole = "DataAdmin" });
        var permissions = new DefaultDapperMaticPermissions(options);
        var context = CreateOperationContext(
            "test/operation",
            authenticated: true,
            roles: ["User"]
        );

        var result = await permissions.IsAuthorizedAsync(context);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsAuthorizedAsync_RequireRole_NoRoleSpecified_FallsBackToDefault()
    {
        var options = Options.Create(new DapperMaticOptions { RequireRole = null });
        var permissions = new DefaultDapperMaticPermissions(
            options,
            PermissionDefault.RequireAuthentication
        );
        var context = CreateOperationContext(
            "test/operation",
            authenticated: true,
            roles: ["DataAdmin"]
        );

        var result = await permissions.IsAuthorizedAsync(context);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAuthorizedAsync_WithOptions_UsesRequiredRole()
    {
        var options = Options.Create(new DapperMaticOptions { RequireRole = "DapperMaticAdmin" });
        var permissions = new DefaultDapperMaticPermissions(options);
        var context = CreateOperationContext(
            "test/operation",
            authenticated: true,
            roles: ["DapperMaticAdmin"]
        );

        var result = await permissions.IsAuthorizedAsync(context);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAuthorizedAsync_WithOptions_ReadOnlyRole_GetOperation_ReturnsTrue()
    {
        var options = Options.Create(
            new DapperMaticOptions { RequireRole = "DataAdmin", ReadOnlyRole = "DataReader" }
        );
        var permissions = new DefaultDapperMaticPermissions(options);
        var context = CreateOperationContext(
            OperationIdentifiers.GetDatasource,
            authenticated: true,
            roles: ["DataReader"]
        );

        var result = await permissions.IsAuthorizedAsync(context);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAuthorizedAsync_WithOptions_ReadOnlyRole_NonGetOperation_ReturnsFalse()
    {
        var options = Options.Create(
            new DapperMaticOptions { RequireRole = "DataAdmin", ReadOnlyRole = "DataReader" }
        );
        var permissions = new DefaultDapperMaticPermissions(options);
        var context = CreateOperationContext(
            OperationIdentifiers.AddDatasource,
            authenticated: true,
            roles: ["DataReader"]
        );

        var result = await permissions.IsAuthorizedAsync(context);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsAuthorizedAsync_WithOptions_RequiredRoleOverridesReadOnly()
    {
        var options = Options.Create(
            new DapperMaticOptions { RequireRole = "DataAdmin", ReadOnlyRole = "DataReader" }
        );
        var permissions = new DefaultDapperMaticPermissions(options);
        var context = CreateOperationContext(
            OperationIdentifiers.AddDatasource,
            authenticated: true,
            roles: ["DataAdmin"]
        );

        var result = await permissions.IsAuthorizedAsync(context);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAuthorizedAsync_WithOptions_NoRequiredRole_FallsBackToDefaultBehavior()
    {
        var options = Options.Create(new DapperMaticOptions { RequireRole = null });
        var permissions = new DefaultDapperMaticPermissions(
            options,
            PermissionDefault.RequireAuthentication
        );
        var context = CreateOperationContext("test/operation", authenticated: true);

        var result = await permissions.IsAuthorizedAsync(context);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAuthorizedAsync_DefaultCase_ReturnsFalse()
    {
        var options = Options.Create(new DapperMaticOptions());
        var permissions = new DefaultDapperMaticPermissions(options, (PermissionDefault)999); // Invalid enum value
        var context = CreateOperationContext("test/operation", authenticated: true);

        var result = await permissions.IsAuthorizedAsync(context);

        result.Should().BeFalse();
    }

    private static OperationContext CreateOperationContext(
        string operation,
        bool authenticated = false,
        string[]? roles = null
    )
    {
        ClaimsPrincipal? user = null;
        if (authenticated)
        {
            var claims = new List<Claim> { new(ClaimTypes.Name, "testuser") };
            if (roles != null)
            {
                claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
            }

            var identity = new ClaimsIdentity(claims, "Test");
            user = new ClaimsPrincipal(identity);
        }

        return new OperationContext { User = user, Operation = operation };
    }
}
