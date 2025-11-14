// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using MJCZone.DapperMatic.AspNetCore;
using MJCZone.DapperMatic.AspNetCore.Security;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Infrastructure;

/// <summary>
/// Test implementation of DapperMatic permissions for testing various authorization scenarios.
/// </summary>
public class TestDapperMaticPermissions : IDapperMaticPermissions
{
    private readonly Dictionary<string, Func<OperationContext, bool>> _rules = [];
    private readonly List<(string operation, string pattern, bool allowed)> _datasourceRules = [];

    /// <summary>
    /// Sets a permission rule for a specific operation.
    /// </summary>
    /// <param name="operation">The operation name (e.g., "datasources/list", "datasources/post").</param>
    /// <param name="rule">The permission rule function.</param>
    public void SetRule(string operation, Func<OperationContext, bool> rule)
    {
        _rules[operation] = rule;
    }

    /// <summary>
    /// Sets permission to always allow for an operation.
    /// </summary>
    /// <param name="operation">The operation name.</param>
    public void AllowOperation(string operation)
    {
        SetRule(operation, _ => true);
    }

    /// <summary>
    /// Sets permission to always deny for an operation.
    /// </summary>
    /// <param name="operation">The operation name.</param>
    public void DenyOperation(string operation)
    {
        SetRule(operation, _ => false);
    }

    /// <summary>
    /// Sets permission to require authentication for an operation.
    /// </summary>
    /// <param name="operation">The operation name.</param>
    public void RequireAuthenticationForOperation(string operation)
    {
        SetRule(operation, (context) => context.User?.Identity?.IsAuthenticated == true);
    }

    /// <summary>
    /// Sets permission to require a specific claim for an operation.
    /// </summary>
    /// <param name="operation">The operation name.</param>
    /// <param name="claimType">The required claim type.</param>
    /// <param name="claimValue">The required claim value (optional).</param>
    public void RequireClaimForOperation(string operation, string claimType, string? claimValue = null)
    {
        SetRule(
            operation,
            (context) =>
            {
                var claims = context.User?.Claims ?? [];

                if (claimValue == null)
                {
                    return claims.Any(c => c.Type == claimType);
                }

                return claims.Any(c => c.Type == claimType && c.Value == claimValue);
            }
        );
    }

    /// <summary>
    /// Sets permission to require a specific role for an operation.
    /// </summary>
    /// <param name="operation">The operation name.</param>
    /// <param name="role">The required role.</param>
    public void RequireRoleForOperation(string operation, string role)
    {
        SetRule(operation, (context) => context.User?.IsInRole(role) == true);
    }

    /// <summary>
    /// Sets permission based on datasource name pattern.
    /// /// </summary>
    /// <param name="operation">The operation name.</param>
    /// <param name="datasourcePattern">The datasource name pattern (supports wildcards *).</param>
    /// <param name="allowed">Whether to allow or deny access.</param>
    public void SetDatasourceRule(string operation, string datasourcePattern, bool allowed)
    {
        _datasourceRules.Add((operation, datasourcePattern, allowed));
    }

    /// <summary>
    /// Clears all permission rules (defaults to allow all).
    /// </summary>
    public void ClearRules()
    {
        _rules.Clear();
    }

    /// <inheritdoc />
    public Task<bool> IsAuthorizedAsync(IOperationContext context)
    {
        if (context is not OperationContext opContext || opContext.Operation is null)
        {
            // Default to allow if no operation context
            return Task.FromResult(true);
        }

        // Check datasource rules first if datasource ID is present
        if (!string.IsNullOrEmpty(opContext.DatasourceId))
        {
            var applicableRules = _datasourceRules.Where(r => r.operation == opContext.Operation).ToList();

            if (applicableRules.Count > 0)
            {
                // Check each pattern - if ANY pattern matches and is allowed, return true
                foreach (var (_, pattern, allowed) in applicableRules)
                {
                    var regexPattern = pattern.Replace("*", ".*");
                    var regex = new System.Text.RegularExpressions.Regex(
                        $"^{regexPattern}$",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase
                    );

                    if (regex.IsMatch(opContext.DatasourceId))
                    {
                        // Pattern matched - return the allowed value for this pattern
                        return Task.FromResult(allowed);
                    }
                }

                // No patterns matched - deny by default when datasource rules exist
                return Task.FromResult(false);
            }
        }

        // Check operation-level rules
        if (_rules.TryGetValue(opContext.Operation, out var rule))
        {
            return Task.FromResult(rule(opContext));
        }

        // Default to allow if no specific rule is set
        return Task.FromResult(true);
    }
}
