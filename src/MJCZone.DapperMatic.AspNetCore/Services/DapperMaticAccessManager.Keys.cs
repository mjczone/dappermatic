// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using MJCZone.DapperMatic.AspNetCore.Authentication;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Repositories;
using MJCZone.DapperMatic.AspNetCore.Validation;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Services;

/// <summary>
/// Service accounts and API keys: kept by those who manage access to everything, or by a tenant's administrators for
/// their tenant; personal keys by their owners. A key never makes a key.
/// </summary>
public sealed partial class DapperMaticAccessManager
{
    /// <summary>
    /// Lists the service accounts the caller manages: a tenant administrator's, their tenant's.
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <param name="tenant">A tenant whose accounts to list, or null for every one.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The accounts: of code and configuration, and kept.</returns>
    public async Task<IReadOnlyList<DapperMaticServiceAccount>> GetServiceAccountsAsync(DapperMaticOperation context, string? tenant = null, CancellationToken cancellationToken = default)
    {
        await AuthorizeAsync(context, cancellationToken).ConfigureAwait(false);
        var own = KeysScope(context, tenant);
        return [.. AllServiceAccounts().Where(a => own is not null ? a.Tenant == own : tenant is null || a.Tenant == tenant)];
    }

    /// <summary>
    /// Adds a service account, or replaces the kept one of the same name (and tenant). A tenant's administrator keeps
    /// their tenant's, giving only roles they hold or roles only their tenant's policies name, and no claims.
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <param name="name">The account's name.</param>
    /// <param name="account">The account.</param>
    /// <param name="tenant">The tenant it is one of, or null for the application's.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The account kept.</returns>
    /// <exception cref="DuplicateKeyException">An account of code or configuration has the name.</exception>
    /// <exception cref="UnauthorizedAccessException">The account gives more than the caller may.</exception>
    public async Task<DapperMaticServiceAccount> SaveServiceAccountAsync(
        DapperMaticOperation context,
        string name,
        DapperMaticServiceAccount account,
        string? tenant = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(account);
        await AuthorizeChangeAsync(context, cancellationToken).ConfigureAwait(false);
        var own = KeysScope(context, tenant);
        account.Name = name;
        account.Tenant = own ?? tenant ?? account.Tenant;
        if (_options.ServiceAccounts.Exists(a => DapperMaticAccessEdits.Same(a, name, account.Tenant)))
        {
            throw new DuplicateKeyException($"The service account '{name}' is given in code or configuration, and is changed there.");
        }

        AssertMayGive(context, own, account.Roles, account.Claims);
        var kept = _stored.ServiceAccounts.FirstOrDefault(a => DapperMaticAccessEdits.Same(a, name, account.Tenant));
        account.CreatedAt = kept?.CreatedAt ?? DateTimeOffset.UtcNow;
        account.CreatedBy = kept?.CreatedBy ?? UserIdOf(context.User);
        await _stored.Repository.SaveServiceAccountAsync(account, cancellationToken).ConfigureAwait(false);
        await ChangedAsync(context, $"Saved the service account '{name}'" + Of(account.Tenant), cancellationToken).ConfigureAwait(false);
        return account;
    }

    /// <summary>
    /// Removes a kept service account, and revokes its keys.
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <param name="name">The account's name.</param>
    /// <param name="tenant">The tenant it is one of, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    /// <exception cref="KeyNotFoundException">No kept account the caller manages has the name.</exception>
    public async Task DeleteServiceAccountAsync(DapperMaticOperation context, string name, string? tenant = null, CancellationToken cancellationToken = default)
    {
        await AuthorizeChangeAsync(context, cancellationToken).ConfigureAwait(false);
        var own = KeysScope(context, tenant);
        var accountTenant = own ?? tenant;
        if (!await _stored.Repository.DeleteServiceAccountAsync(name, accountTenant, cancellationToken).ConfigureAwait(false))
        {
            throw new KeyNotFoundException($"No kept service account is named '{name}'" + Of(accountTenant) + ".");
        }

        if (_keys is not null)
        {
            foreach (var key in await _keys.Repository.ListAsync(new DapperMaticKeyFilter { Kind = DapperMaticKeyKind.Service, Owner = name, Tenant = accountTenant ?? string.Empty }, cancellationToken).ConfigureAwait(false))
            {
                await RevokeKeyRowAsync(key, cancellationToken).ConfigureAwait(false);
            }
        }

        await ChangedAsync(context, $"Removed the service account '{name}'" + Of(accountTenant) + ", and revoked its keys", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Makes a key for a service account: shown this once.
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <param name="name">The account's name.</param>
    /// <param name="request">The key's name, expiry and limits.</param>
    /// <param name="tenant">The tenant the account is one of, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The key, and what is kept of it.</returns>
    /// <exception cref="KeyNotFoundException">No account the caller manages has the name.</exception>
    public async Task<ApiKeyCreated> CreateServiceKeyAsync(
        DapperMaticOperation context,
        string name,
        ApiKeyRequest request,
        string? tenant = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        var keys = Keys();
        await AuthorizeChangeAsync(context, cancellationToken).ConfigureAwait(false);
        NotByKey(context, "make keys");
        var own = KeysScope(context, tenant);
        var account = AllServiceAccounts().FirstOrDefault(a => DapperMaticAccessEdits.Same(a, name, own ?? tenant))
            ?? throw new KeyNotFoundException($"No service account is named '{name}'" + Of(own ?? tenant) + ".");
        var created = await CreateKeyAsync(keys, DapperMaticKeyKind.Service, account.Name, account.Tenant, request, keys.Settings.MaxServiceKeyLifetime, UserIdOf(context.User), cancellationToken)
            .ConfigureAwait(false);
        await ChangedAsync(context, $"Made the key '{created.Info.Id}' of the service account '{name}'" + Of(account.Tenant), cancellationToken).ConfigureAwait(false);
        return created;
    }

    /// <summary>
    /// Lists keys the caller manages, without their secrets: every one, or a tenant administrator's tenant's.
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <param name="filter">Which keys.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The keys.</returns>
    public async Task<IReadOnlyList<ApiKeyInfo>> GetKeysAsync(DapperMaticOperation context, DapperMaticKeyFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var keys = Keys();
        await AuthorizeAsync(context, cancellationToken).ConfigureAwait(false);
        var own = KeysScope(context, filter.Tenant is { Length: > 0 } t ? t : null);
        if (own is not null)
        {
            filter.Tenant = own;
        }

        return [.. (await keys.Repository.ListAsync(filter, cancellationToken).ConfigureAwait(false)).Select(ApiKeyInfo.Of)];
    }

    /// <summary>
    /// Revokes a key the caller manages: it no longer signs in, and is still listed.
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <param name="id">The key's id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    /// <exception cref="KeyNotFoundException">No key the caller manages has the id.</exception>
    public async Task RevokeKeyAsync(DapperMaticOperation context, string id, CancellationToken cancellationToken = default)
    {
        var keys = Keys();
        await AuthorizeChangeAsync(context, cancellationToken).ConfigureAwait(false);
        var own = KeysScope(context, null);
        var key = await keys.Repository.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (key is null || (own is not null && key.Tenant != own))
        {
            throw new KeyNotFoundException($"No key the caller manages has the id '{id}'.");
        }

        await RevokeKeyRowAsync(key, cancellationToken).ConfigureAwait(false);
        await ChangedAsync(context, $"Revoked the key '{id}'", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Lists the caller's own personal keys.
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The keys.</returns>
    public async Task<IReadOnlyList<ApiKeyInfo>> GetOwnKeysAsync(DapperMaticOperation context, CancellationToken cancellationToken = default)
    {
        var keys = Keys();
        var owner = OwnerOf(context);
        return [.. (await keys.Repository.ListAsync(new DapperMaticKeyFilter { Kind = DapperMaticKeyKind.Personal, Owner = owner }, cancellationToken).ConfigureAwait(false)).Select(ApiKeyInfo.Of)];
    }

    /// <summary>
    /// Makes a personal key for the caller, in their tenant: it acts as them, as they are each time it is used, never
    /// more. Shown this once.
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <param name="request">The key's name, expiry and limits.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The key, and what is kept of it.</returns>
    /// <exception cref="UnauthorizedAccessException">The caller may not make their own keys.</exception>
    /// <exception cref="InvalidOperationException">DapperMatic cannot find keys' owners.</exception>
    public async Task<ApiKeyCreated> CreateOwnKeyAsync(DapperMaticOperation context, ApiKeyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var keys = Keys();
        var owner = OwnerOf(context);
        var access = Access(context);
        if (!keys.Settings.PersonalKeyRoles.Any(access.IsInRole))
        {
            throw new UnauthorizedAccessException("This caller may not make their own API keys.");
        }

        var tenant = access.Tenant?.Id.ToString();
        if (context.Access?.Services is { } services && await keys.OwnerAsync(owner, tenant, services, cancellationToken).ConfigureAwait(false) is not { Identity.IsAuthenticated: true })
        {
            throw new UnauthorizedAccessException("This caller cannot be found as a key's owner: a key of theirs would not sign in.");
        }

        var created = await CreateKeyAsync(keys, DapperMaticKeyKind.Personal, owner, tenant, request, keys.Settings.MaxPersonalKeyLifetime, owner, cancellationToken).ConfigureAwait(false);
        await _auditLogger.LogOperationAsync(context.ToAuditEvent(true, $"Made the personal key '{created.Info.Id}'", _options.AuditClaims)).ConfigureAwait(false);
        return created;
    }

    /// <summary>
    /// Revokes one of the caller's personal keys, or every one.
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <param name="id">The key's id, or null for every one.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many were revoked.</returns>
    /// <exception cref="KeyNotFoundException">The caller has no key of that id.</exception>
    public async Task<int> RevokeOwnKeysAsync(DapperMaticOperation context, string? id, CancellationToken cancellationToken = default)
    {
        var keys = Keys();
        var owner = OwnerOf(context, byKey: true);
        var own = await keys.Repository.ListAsync(new DapperMaticKeyFilter { Kind = DapperMaticKeyKind.Personal, Owner = owner }, cancellationToken).ConfigureAwait(false);
        var revoking = own.Where(k => k.RevokedAt is null && (id is null || k.Id == id)).ToList();
        if (id is not null && revoking.Count == 0)
        {
            throw new KeyNotFoundException($"This caller has no live key '{id}'.");
        }

        foreach (var key in revoking)
        {
            await RevokeKeyRowAsync(key, cancellationToken).ConfigureAwait(false);
        }

        await _auditLogger.LogOperationAsync(context.ToAuditEvent(true, $"Revoked {revoking.Count} personal keys", _options.AuditClaims)).ConfigureAwait(false);
        return revoking.Count;
    }

    // The service accounts in force: kept, and of code and configuration.
    private IEnumerable<DapperMaticServiceAccount> AllServiceAccounts() => _stored.ServiceAccounts.Concat(_options.ServiceAccounts);

    private static string Of(string? tenant) => tenant is null ? string.Empty : $" of the tenant '{tenant}'";

    private static string? UserIdOf(ClaimsPrincipal? user) => (user?.FindFirst(ClaimTypes.NameIdentifier) ?? user?.FindFirst("sub"))?.Value;

    // A key never makes or changes durable credentials: a leaked one would outlive its revocation.
    private static void NotByKey(DapperMaticOperation context, string what)
    {
        if (context.User is { } user && DapperMaticClaims.ByKey(user))
        {
            throw new UnauthorizedAccessException($"A caller signed in with an API key may not {what}.");
        }
    }

    // The caller as a key's owner: signed in (not by a key, unless only revoking), with an id.
    private static string OwnerOf(DapperMaticOperation context, bool byKey = false)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedAccessException("Personal keys are a signed-in caller's.");
        }

        if (!byKey)
        {
            NotByKey(context, "make keys");
        }

        return UserIdOf(context.User) ?? throw new UnauthorizedAccessException("This caller has no user id (a name identifier or sub claim) to own keys by.");
    }

    // Who a call about keys and service accounts is about: a tenant's administrator manages their tenant's; else only
    // those who manage access to everything (an account's roles reach every datasource its policies name).
    private string? KeysScope(DapperMaticOperation context, string? tenant)
    {
        var (managing, own) = Scope(context, tenant);
        if (own is null && !DmPolicies.IsWithin(managing, [], []))
        {
            throw new UnauthorizedAccessException("Service accounts and keys are managed by those who manage access to everything, or by a tenant's administrators.");
        }

        return own;
    }

    // What a tenant's administrator may give: roles they hold, or roles only their tenant's policies name; no claims.
    // A role no policy names may still be read by the application itself (its own [Authorize(Roles = …)] on the
    // DapperMatic scheme, its permissions), so it is not theirs to give. Those who manage everything give any.
    private void AssertMayGive(DapperMaticOperation context, string? own, IEnumerable<string> roles, IReadOnlyDictionary<string, List<string>>? claims)
    {
        if (own is null)
        {
            return;
        }

        if (claims is { Count: > 0 })
        {
            throw new UnauthorizedAccessException("Claims are given by those who manage access to everything.");
        }

        var access = Access(context);
        var named = Kept().Where(p => !OwnTenant(p)).SelectMany(p => p.Roles).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tenants = Kept().Where(p => p.WithinOwnTenant || string.Equals(p.Tenant, own, StringComparison.Ordinal)).SelectMany(p => p.Roles).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var role in roles)
        {
            if (role is "*" or DmAccessContext.AnonymousRole || !(access.IsInRole(role) || (tenants.Contains(role) && !named.Contains(role))))
            {
                throw new UnauthorizedAccessException($"A tenant's administrator gives only roles they hold, or roles only their tenant's policies name: not '{role}'.");
            }
        }
    }

    private DapperMaticApiKeyService Keys() =>
        _keys ?? throw new InvalidOperationException("API keys are not turned on: call dm.AddApiKeys().");

    private async Task RevokeKeyRowAsync(DapperMaticApiKey key, CancellationToken cancellationToken)
    {
        await _keys!.Repository.RevokeAsync(key.Id, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        _keys.Forget(key.Id);
    }

    // A new key: its limits checked (policies of no roles), its expiry within the maximum; kept with its secret hashed.
    private static async Task<ApiKeyCreated> CreateKeyAsync(
        DapperMaticApiKeyService keys,
        DapperMaticKeyKind kind,
        string owner,
        string? tenant,
        ApiKeyRequest request,
        TimeSpan? maxLifetime,
        string? createdBy,
        CancellationToken cancellationToken
    )
    {
        var now = DateTimeOffset.UtcNow;
        if (request.ExpiresAt is { } expires && expires <= now)
        {
            throw new ArgumentException("A key's expiry is in the future.");
        }

        if (maxLifetime is { } max && (request.ExpiresAt is null || request.ExpiresAt > now + max))
        {
            throw new ArgumentException($"A key here lasts at most {max}: give an expiry within it.");
        }

        var (id, hash, secret) = DapperMaticApiKeyFormat.Create(keys.Settings.Prefix);
        var key = new DapperMaticApiKey
        {
            Id = id,
            SecretHash = hash,
            Name = string.IsNullOrWhiteSpace(request.Name) ? id : request.Name,
            Kind = kind,
            Owner = owner,
            Tenant = tenant,
            ExpiresAt = request.ExpiresAt,
            CreatedAt = now,
            CreatedBy = createdBy,
            Limits = LimitsOf(request),
        };
        await keys.Repository.SaveAsync(key, cancellationToken).ConfigureAwait(false);
        return new ApiKeyCreated(secret, ApiKeyInfo.Of(key));
    }

    // A request's limits as policies of no roles (they apply to the key's caller): its own, and its shortcuts as one more.
    private static List<DmPolicy> LimitsOf(ApiKeyRequest request)
    {
        var limits = new List<DmPolicy>();
        foreach (var limit in request.Limits)
        {
            if (limit.Kind != DmPolicyKind.Permissive || limit.Roles.Any(r => r != "*") || limit.Claims.Count > 0 || limit.Tenant is not null || limit.Presets.Count > 0)
            {
                throw new ArgumentException("A key's limits say what it may do at most: permissive policies of no roles, claims, tenant or presets.");
            }

            limit.Roles.Clear();
            limit.Roles.Add("*");
            DmPolicies.Validate(limit);
            limits.Add(limit);
        }

        if (request.ReadOnly || request.Datasources.Count > 0 || request.Schemas.Count > 0 || request.Tables.Count > 0)
        {
            // Limits are ORed, as grants are: shortcuts beside policies would widen them.
            if (limits.Count > 0)
            {
                throw new ArgumentException("A key is limited by policies or by shortcuts (read only, datasources, schemas, tables), not both: put the shortcuts into the policies.");
            }

            var shortcut = DmPolicy.Allow(request.ReadOnly ? DmAction.Read | DmAction.ReadSchema : DmAction.All, "*");
            shortcut.Name = "shortcuts";
            shortcut.Datasources.AddRange(request.Datasources);
            shortcut.Schemas.AddRange(request.Schemas);
            shortcut.Tables.AddRange(request.Tables);
            DmPolicies.Validate(shortcut);
            limits.Add(shortcut);
        }

        return limits;
    }
}
