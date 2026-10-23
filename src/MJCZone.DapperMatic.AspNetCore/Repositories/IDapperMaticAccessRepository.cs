// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Repositories;

/// <summary>
/// Keeps the policies and tenants edited at runtime through DapperMatic's access endpoints, alongside those given in
/// code and configuration: in memory (the default), in a file, or in a database table.
/// </summary>
public interface IDapperMaticAccessRepository
{
    /// <summary>
    /// Reads every policy and tenant kept.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The policies and tenants.</returns>
    Task<DapperMaticAccessDocument> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a policy, or replaces the one of the same name and tenant: a tenant's policies have names of their own.
    /// </summary>
    /// <param name="policy">The policy, which has a name, and a tenant if it is one of a tenant's.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task SavePolicyAsync(DmPolicy policy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a policy.
    /// </summary>
    /// <param name="name">The policy's name.</param>
    /// <param name="tenant">The tenant it is one of, or null for one of the whole application.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if it was kept.</returns>
    Task<bool> DeletePolicyAsync(string name, string? tenant = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a tenant, or replaces the one of the same id.
    /// </summary>
    /// <param name="id">The tenant's id.</param>
    /// <param name="tenant">What is known of it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task SaveTenantAsync(string id, DapperMaticTenantSettings tenant, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a tenant.
    /// </summary>
    /// <param name="id">The tenant's id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if it was kept.</returns>
    Task<bool> DeleteTenantAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a webhook, or replaces the one of the same name and tenant.
    /// </summary>
    /// <param name="webhook">The webhook, which has a name, and a tenant if it is one of a tenant's.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task SaveWebhookAsync(DapperMaticWebhook webhook, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a webhook.
    /// </summary>
    /// <param name="name">The webhook's name.</param>
    /// <param name="tenant">The tenant it is one of, or null for one of the whole application.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if it was kept.</returns>
    Task<bool> DeleteWebhookAsync(string name, string? tenant = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a service account, or replaces the one of the same name and tenant.
    /// </summary>
    /// <param name="account">The account, which has a name, and a tenant if it is one of a tenant's.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task SaveServiceAccountAsync(DapperMaticServiceAccount account, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a service account.
    /// </summary>
    /// <param name="name">The account's name.</param>
    /// <param name="tenant">The tenant it is one of, or null for one of the whole application.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if it was kept.</returns>
    Task<bool> DeleteServiceAccountAsync(string name, string? tenant = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces everything kept, at once.
    /// </summary>
    /// <param name="document">The policies and tenants to keep.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task ReplaceAsync(DapperMaticAccessDocument document, CancellationToken cancellationToken = default);
}
