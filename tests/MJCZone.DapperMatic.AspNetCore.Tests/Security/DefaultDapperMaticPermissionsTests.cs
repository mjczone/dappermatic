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
    public async Task Should_expect_is_authorized_async_allow_all_returns_true_Async()
    {
        var options = Options.Create(new DapperMaticOptions());
        var permissions = new DefaultDapperMaticPermissions(options, PermissionDefault.AllowAll);
        var context = CreateOperationContext("test/operation");

        var result = await permissions.IsAuthorizedAsync(context);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Should_expect_is_authorized_async_require_authentication_unauthenticated_user_returns_false_Async()
    {
        var options = Options.Create(new DapperMaticOptions());
        var permissions = new DefaultDapperMaticPermissions(options, PermissionDefault.RequireAuthentication);
        var context = CreateOperationContext("test/operation", authenticated: false);

        var result = await permissions.IsAuthorizedAsync(context);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Should_expect_is_authorized_async_require_authentication_authenticated_user_returns_true_Async()
    {
        var options = Options.Create(new DapperMaticOptions());
        var permissions = new DefaultDapperMaticPermissions(options, PermissionDefault.RequireAuthentication);
        var context = CreateOperationContext("test/operation", authenticated: true);

        var result = await permissions.IsAuthorizedAsync(context);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Should_expect_is_authorized_async_require_role_user_with_role_returns_true_Async()
    {
        var options = Options.Create(new DapperMaticOptions { RequireRole = "DataAdmin" });
        var permissions = new DefaultDapperMaticPermissions(options);
        var context = CreateOperationContext("test/operation", authenticated: true, roles: ["DataAdmin"]);

        var result = await permissions.IsAuthorizedAsync(context);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Should_expect_is_authorized_async_require_role_user_without_role_returns_false_Async()
    {
        var options = Options.Create(new DapperMaticOptions { RequireRole = "DataAdmin" });
        var permissions = new DefaultDapperMaticPermissions(options);
        var context = CreateOperationContext("test/operation", authenticated: true, roles: ["User"]);

        var result = await permissions.IsAuthorizedAsync(context);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Should_expect_is_authorized_async_require_role_no_role_specified_falls_back_to_default_Async()
    {
        var options = Options.Create(new DapperMaticOptions { RequireRole = null });
        var permissions = new DefaultDapperMaticPermissions(options, PermissionDefault.RequireAuthentication);
        var context = CreateOperationContext("test/operation", authenticated: true, roles: ["DataAdmin"]);

        var result = await permissions.IsAuthorizedAsync(context);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Should_expect_is_authorized_async_with_options_uses_required_role_Async()
    {
        var options = Options.Create(new DapperMaticOptions { RequireRole = "DapperMaticAdmin" });
        var permissions = new DefaultDapperMaticPermissions(options);
        var context = CreateOperationContext("test/operation", authenticated: true, roles: ["DapperMaticAdmin"]);

        var result = await permissions.IsAuthorizedAsync(context);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Should_expect_is_authorized_async_with_options_read_only_role_get_operation_returns_true_Async()
    {
        var options = Options.Create(new DapperMaticOptions { RequireRole = "DataAdmin", ReadOnlyRole = "DataReader" });
        var permissions = new DefaultDapperMaticPermissions(options);
        var context = CreateOperationContext("datasources/get", authenticated: true, roles: ["DataReader"]);

        var result = await permissions.IsAuthorizedAsync(context);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Should_expect_is_authorized_async_with_options_read_only_role_non_get_operation_returns_false_Async()
    {
        var options = Options.Create(new DapperMaticOptions { RequireRole = "DataAdmin", ReadOnlyRole = "DataReader" });
        var permissions = new DefaultDapperMaticPermissions(options);
        var context = CreateOperationContext("datasources/post", authenticated: true, roles: ["DataReader"]);

        var result = await permissions.IsAuthorizedAsync(context);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Should_expect_is_authorized_async_with_options_required_role_overrides_read_only_Async()
    {
        var options = Options.Create(new DapperMaticOptions { RequireRole = "DataAdmin", ReadOnlyRole = "DataReader" });
        var permissions = new DefaultDapperMaticPermissions(options);
        var context = CreateOperationContext("datasources/post", authenticated: true, roles: ["DataAdmin"]);

        var result = await permissions.IsAuthorizedAsync(context);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Should_expect_is_authorized_async_with_options_no_required_role_falls_back_to_default_behavior_Async()
    {
        var options = Options.Create(new DapperMaticOptions { RequireRole = null });
        var permissions = new DefaultDapperMaticPermissions(options, PermissionDefault.RequireAuthentication);
        var context = CreateOperationContext("test/operation", authenticated: true);

        var result = await permissions.IsAuthorizedAsync(context);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Should_expect_is_authorized_async_default_case_returns_false_Async()
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
