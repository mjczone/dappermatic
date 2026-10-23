// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Repositories;

/// <summary>
/// Keeps policies and tenants in memory, for the life of the process: the default.
/// </summary>
public class InMemoryDapperMaticAccessRepository : IDapperMaticAccessRepository
{
    private readonly object _gate = new();
    private DapperMaticAccessDocument _document = new();

    /// <inheritdoc />
    public Task<DapperMaticAccessDocument> GetAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_document.Copy());
        }
    }

    /// <inheritdoc />
    public Task SavePolicyAsync(DmPolicy policy, CancellationToken cancellationToken = default) =>
        Change(d => DapperMaticAccessEdits.SavePolicy(d, policy));

    /// <inheritdoc />
    public Task<bool> DeletePolicyAsync(string name, string? tenant = null, CancellationToken cancellationToken = default) =>
        Change(d => DapperMaticAccessEdits.DeletePolicy(d, name, tenant));

    /// <inheritdoc />
    public Task SaveTenantAsync(string id, DapperMaticTenantSettings tenant, CancellationToken cancellationToken = default) =>
        Change(d => DapperMaticAccessEdits.SaveTenant(d, id, tenant));

    /// <inheritdoc />
    public Task<bool> DeleteTenantAsync(string id, CancellationToken cancellationToken = default) =>
        Change(d => d.Tenants.Remove(id));

    /// <inheritdoc />
    public Task SaveWebhookAsync(DapperMaticWebhook webhook, CancellationToken cancellationToken = default) =>
        Change(d => DapperMaticAccessEdits.SaveWebhook(d, webhook));

    /// <inheritdoc />
    public Task<bool> DeleteWebhookAsync(string name, string? tenant = null, CancellationToken cancellationToken = default) =>
        Change(d => DapperMaticAccessEdits.DeleteWebhook(d, name, tenant));

    /// <inheritdoc />
    public Task SaveServiceAccountAsync(DapperMaticServiceAccount account, CancellationToken cancellationToken = default) =>
        Change(d => DapperMaticAccessEdits.SaveServiceAccount(d, account));

    /// <inheritdoc />
    public Task<bool> DeleteServiceAccountAsync(string name, string? tenant = null, CancellationToken cancellationToken = default) =>
        Change(d => DapperMaticAccessEdits.DeleteServiceAccount(d, name, tenant));

    /// <inheritdoc />
    public Task ReplaceAsync(DapperMaticAccessDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        lock (_gate)
        {
            _document = document.Copy();
        }

        return Task.CompletedTask;
    }

    private Task<bool> Change(Func<DapperMaticAccessDocument, bool> edit)
    {
        lock (_gate)
        {
            var copy = _document.Copy();
            var changed = edit(copy);
            _document = copy;
            return Task.FromResult(changed);
        }
    }
}

/// <summary>
/// Edits of a document of policies and tenants, shared by the repositories that keep a whole document.
/// </summary>
internal static class DapperMaticAccessEdits
{
    /// <summary>Adds a policy, replacing the one of the same name and tenant.</summary>
    /// <param name="document">The document.</param>
    /// <param name="policy">The policy.</param>
    /// <returns>True.</returns>
    public static bool SavePolicy(DapperMaticAccessDocument document, DmPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentException.ThrowIfNullOrWhiteSpace(policy.Name);
        document.Policies.RemoveAll(p => Same(p, policy.Name, policy.Tenant));
        document.Policies.Add(policy);
        return true;
    }

    /// <summary>Removes a policy.</summary>
    /// <param name="document">The document.</param>
    /// <param name="name">The policy's name.</param>
    /// <param name="tenant">The tenant it is one of, or null.</param>
    /// <returns>True if it was there.</returns>
    public static bool DeletePolicy(DapperMaticAccessDocument document, string name, string? tenant) =>
        document.Policies.RemoveAll(p => Same(p, name, tenant)) > 0;

    /// <summary>Tells whether a policy has a name, in a tenant (or none).</summary>
    /// <param name="policy">The policy.</param>
    /// <param name="name">The name.</param>
    /// <param name="tenant">The tenant, or null.</param>
    /// <returns>True if it does.</returns>
    public static bool Same(DmPolicy policy, string? name, string? tenant) =>
        string.Equals(policy.Name, name, StringComparison.OrdinalIgnoreCase) && string.Equals(policy.Tenant, tenant, StringComparison.Ordinal);

    /// <summary>Adds a webhook, replacing the one of the same name and tenant.</summary>
    /// <param name="document">The document.</param>
    /// <param name="webhook">The webhook.</param>
    /// <returns>True.</returns>
    public static bool SaveWebhook(DapperMaticAccessDocument document, DapperMaticWebhook webhook)
    {
        ArgumentNullException.ThrowIfNull(webhook);
        ArgumentException.ThrowIfNullOrWhiteSpace(webhook.Name);
        document.Webhooks.RemoveAll(w => Same(w, webhook.Name, webhook.Tenant));
        document.Webhooks.Add(webhook);
        return true;
    }

    /// <summary>Removes a webhook.</summary>
    /// <param name="document">The document.</param>
    /// <param name="name">The webhook's name.</param>
    /// <param name="tenant">The tenant it is one of, or null.</param>
    /// <returns>True if it was there.</returns>
    public static bool DeleteWebhook(DapperMaticAccessDocument document, string name, string? tenant) =>
        document.Webhooks.RemoveAll(w => Same(w, name, tenant)) > 0;

    /// <summary>Tells whether a webhook has a name, in a tenant (or none).</summary>
    /// <param name="webhook">The webhook.</param>
    /// <param name="name">The name.</param>
    /// <param name="tenant">The tenant, or null.</param>
    /// <returns>True if it does.</returns>
    public static bool Same(DapperMaticWebhook webhook, string? name, string? tenant) =>
        string.Equals(webhook.Name, name, StringComparison.OrdinalIgnoreCase) && string.Equals(webhook.Tenant, tenant, StringComparison.Ordinal);

    /// <summary>Adds a service account, replacing the one of the same name and tenant.</summary>
    /// <param name="document">The document.</param>
    /// <param name="account">The account.</param>
    /// <returns>True.</returns>
    public static bool SaveServiceAccount(DapperMaticAccessDocument document, DapperMaticServiceAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentException.ThrowIfNullOrWhiteSpace(account.Name);
        document.ServiceAccounts.RemoveAll(a => Same(a, account.Name, account.Tenant));
        document.ServiceAccounts.Add(account);
        return true;
    }

    /// <summary>Removes a service account.</summary>
    /// <param name="document">The document.</param>
    /// <param name="name">The account's name.</param>
    /// <param name="tenant">The tenant it is one of, or null.</param>
    /// <returns>True if it was there.</returns>
    public static bool DeleteServiceAccount(DapperMaticAccessDocument document, string name, string? tenant) =>
        document.ServiceAccounts.RemoveAll(a => Same(a, name, tenant)) > 0;

    /// <summary>Tells whether a service account has a name, in a tenant (or none).</summary>
    /// <param name="account">The account.</param>
    /// <param name="name">The name.</param>
    /// <param name="tenant">The tenant, or null.</param>
    /// <returns>True if it does.</returns>
    public static bool Same(DapperMaticServiceAccount account, string? name, string? tenant) =>
        string.Equals(account.Name, name, StringComparison.OrdinalIgnoreCase) && string.Equals(account.Tenant, tenant, StringComparison.Ordinal);

    /// <summary>Adds a tenant, replacing the one of the same id.</summary>
    /// <param name="document">The document.</param>
    /// <param name="id">The tenant's id.</param>
    /// <param name="tenant">The tenant.</param>
    /// <returns>True.</returns>
    public static bool SaveTenant(DapperMaticAccessDocument document, string id, DapperMaticTenantSettings tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(tenant);
        document.Tenants[id] = tenant;
        return true;
    }
}
