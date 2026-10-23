// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Dml;

/// <summary>
/// Where the DML methods find the values a scope does not give them, set once at startup: the current tenant, the
/// current user, and the clock. A scope's own <c>ForTenant</c> and <c>WithUser</c> win over them.
/// </summary>
/// <remarks>
/// The providers are called on every statement that needs them, never cached, so they can read the current request:
/// the AspNetCore package sets them from <c>IHttpContextAccessor</c> with <c>UseDapperMaticRequestValues</c>.
/// </remarks>
/// <example>
/// <code>
/// DmDefaults.TenantId = () => tenantAccessor.CurrentTenantId;
/// DmDefaults.User = () => userAccessor.CurrentUserName;
/// </code>
/// </example>
public static class DmDefaults
{
    /// <summary>
    /// Gets or sets the current tenant's id, for models with a <c>[DmTenantKey]</c>; null for none, which refuses them.
    /// </summary>
    public static Func<object?>? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the current tenant's schema, for a deployment with a schema per tenant: every model but the shared
    /// ones (<see cref="DmSchemaLayout"/>) is read and written there unless a scope says <c>InSchema</c>. Null for none,
    /// which leaves each model in its own schema.
    /// </summary>
    public static Func<string?>? TenantSchema { get; set; }

    /// <summary>
    /// Gets or sets the current user, for <c>[DmCreatedBy]</c> and <c>[DmUpdatedBy]</c>; null for none, which leaves
    /// them as they are.
    /// </summary>
    public static Func<object?>? User { get; set; }

    /// <summary>
    /// Gets or sets the clock for <c>[DmCreatedAt]</c>, <c>[DmUpdatedAt]</c> and the time of a soft delete.
    /// </summary>
    public static TimeProvider Clock { get; set; } = TimeProvider.System;
}
