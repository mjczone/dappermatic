// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;

namespace MJCZone.DapperMatic.AspNetCore.Security;

/// <summary>
/// Default implementation of IDapperMaticPermissions that allows all users, all authenticated user, or users with a specific role.
/// </summary>
public class DefaultDapperMaticPermissions : IDapperMaticPermissions
{
    private readonly PermissionDefault _defaultBehavior;
    private readonly string? _requireRole;
    private readonly string? _readOnlyRole;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultDapperMaticPermissions"/> class.
    /// </summary>
    /// <param name="options">The DapperMatic options.</param>
    /// <param name="defaultBehavior">The default permission behavior.</param>
    public DefaultDapperMaticPermissions(
        IOptions<DapperMaticOptions> options,
        PermissionDefault defaultBehavior = PermissionDefault.AllowAll
    )
    {
        _requireRole = options.Value.RequireRole;
        _readOnlyRole = options.Value.ReadOnlyRole;
        _defaultBehavior = string.IsNullOrWhiteSpace(_requireRole)
            ? defaultBehavior
            : PermissionDefault.RequireRole;
    }

    /// <inheritdoc />
    public Task<bool> IsAuthorizedAsync(OperationContext context)
    {
        var isAuthenticated = context.User?.Identity?.IsAuthenticated == true;
        var isInRequiredRole =
            isAuthenticated
            && !string.IsNullOrEmpty(_requireRole)
            && context.User?.IsInRole(_requireRole) == true;
        var isInReadOnlyRole =
            isAuthenticated
            && !string.IsNullOrEmpty(_readOnlyRole)
            && context.User?.IsInRole(_readOnlyRole) == true;
        var isGetOperation = OperationIdentifiers.IsGetOperation(context.Operation);
        if (isGetOperation && !string.IsNullOrWhiteSpace(_readOnlyRole))
        {
            return Task.FromResult(isInRequiredRole || isInReadOnlyRole);
        }

        return Task.FromResult(
            _defaultBehavior switch
            {
                PermissionDefault.AllowAll => true,
                PermissionDefault.RequireAuthentication => context.User?.Identity?.IsAuthenticated
                    == true,
                PermissionDefault.RequireRole => isInRequiredRole,
                _ => false,
            }
        );
    }
}

/// <summary>
/// Specifies the default permission behavior.
/// </summary>
public enum PermissionDefault
{
    /// <summary>
    /// Allow all operations.
    /// </summary>
    AllowAll,

    /// <summary>
    /// Require authentication for all operations.
    /// </summary>
    RequireAuthentication,

    /// <summary>
    /// Require a specific role for all operations.
    /// </summary>
    RequireRole,
}
