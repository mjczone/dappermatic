// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Repositories;
using MJCZone.DapperMatic.AspNetCore.Security;

namespace MJCZone.DapperMatic.AspNetCore.Authentication;

/// <summary>
/// Finds a personal key's owner as they are now, each time the key signs in.
/// </summary>
public interface IDapperMaticKeyOwners
{
    /// <summary>
    /// Finds a key's owner.
    /// </summary>
    /// <param name="ownerId">The owner's user id.</param>
    /// <param name="tenant">The key's tenant, or null for none.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The owner as they are now, in that tenant; null when they are gone, turned off, or no longer in it.</returns>
    ValueTask<ClaimsPrincipal?> FindAsync(string ownerId, string? tenant, CancellationToken cancellationToken);
}

/// <summary>
/// API keys: made, found (each looked up for a while, as the policies are), checked, and turned into their callers;
/// their use recorded at most once a minute each.
/// </summary>
internal sealed class DapperMaticApiKeyService
{
    /// <summary>Gets why personal keys cannot be used without a way to find their owners.</summary>
    internal const string PersonalKeysNeedOwners =
        "Personal keys act as their owners as they are now, so DapperMatic needs to find them: turn on the built-in accounts (dm.AddUserAccounts()), or set ApiKeys.ResolveOwner.";

    private static readonly TimeSpan UseRecorded = TimeSpan.FromMinutes(1);

    private readonly DapperMaticOptions _options;
    private readonly IDapperMaticKeyRepository _keys;
    private readonly DapperMaticStoredAccess _stored;
    private readonly ConcurrentDictionary<string, (DapperMaticApiKey? Key, DateTimeOffset ReadAt)> _found = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _recorded = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _used = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticApiKeyService"/> class.
    /// </summary>
    /// <param name="options">The DapperMatic options.</param>
    /// <param name="keys">Where keys are kept.</param>
    /// <param name="stored">The access DapperMatic keeps: the service accounts.</param>
    public DapperMaticApiKeyService(IOptions<DapperMaticOptions> options, IDapperMaticKeyRepository keys, DapperMaticStoredAccess stored)
    {
        _options = options.Value;
        _keys = keys;
        _stored = stored;
    }

    /// <summary>Gets where keys are kept.</summary>
    public IDapperMaticKeyRepository Repository => _keys;

    /// <summary>Gets the settings.</summary>
    public DapperMaticApiKeyOptions Settings => _options.ApiKeys;

    /// <summary>Gets how many keys are kept as read.</summary>
    internal int Cached => _found.Count;

    /// <summary>
    /// Finds a key by its id: read again once the access refresh interval has passed since it was last read.
    /// </summary>
    /// <param name="id">The key's id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The key, or null.</returns>
    public async Task<DapperMaticApiKey?> FindAsync(string id, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (_found.TryGetValue(id, out var found) && now - found.ReadAt < _options.AccessRefreshInterval)
        {
            return found.Key;
        }

        // Ids no key has are not kept: anyone can send them.
        var key = await _keys.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (key is null)
        {
            _found.TryRemove(id, out _);
        }
        else
        {
            _found[id] = (key, now);
        }

        return key;
    }

    /// <summary>
    /// Forgets what was read of a key: changed here, it is read again at once.
    /// </summary>
    /// <param name="id">The key's id.</param>
    public void Forget(string id) => _found.TryRemove(id, out _);

    /// <summary>
    /// Finds a service account, kept or given in code or configuration.
    /// </summary>
    /// <param name="name">Its name.</param>
    /// <param name="tenant">Its tenant, or null.</param>
    /// <returns>The account, or null.</returns>
    public DapperMaticServiceAccount? AccountOf(string name, string? tenant) =>
        _stored.ServiceAccounts.Concat(_options.ServiceAccounts)
            .FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase) && string.Equals(a.Tenant, tenant, StringComparison.Ordinal));

    /// <summary>
    /// Signs a caller in with a key.
    /// </summary>
    /// <param name="presented">What was sent.</param>
    /// <param name="services">The request's services, for finding a personal key's owner.</param>
    /// <param name="scheme">The sign-in method's name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result: the key's caller, or why it does not sign in.</returns>
    public async Task<AuthenticateResult> AuthenticateAsync(string presented, IServiceProvider services, string scheme, CancellationToken cancellationToken)
    {
        if (!DapperMaticApiKeyFormat.TryParse(presented, Settings.Prefix, out var id, out var secret))
        {
            return AuthenticateResult.Fail("This is not an API key.");
        }

        await _stored.RefreshAsync(cancellationToken).ConfigureAwait(false);
        var key = await FindAsync(id, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        if (key is null || !DapperMaticApiKeyFormat.Matches(secret, key.SecretHash))
        {
            return AuthenticateResult.Fail("The API key is not known.");
        }

        if (!key.IsLive(now))
        {
            return AuthenticateResult.Fail("The API key has been revoked, or has expired.");
        }

        var caller = await CallerOfAsync(key, services, cancellationToken).ConfigureAwait(false);
        if (caller is null)
        {
            return AuthenticateResult.Fail("The API key's owner cannot sign in now.");
        }

        Used(key.Id, now);
        return AuthenticateResult.Success(new AuthenticationTicket(caller, new AuthenticationProperties { ExpiresUtc = key.ExpiresAt }, scheme));
    }

    /// <summary>
    /// Tells whether a key still signs in: a stream checks before each use.
    /// </summary>
    /// <param name="id">The key's id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The key, live; null when it no longer is.</returns>
    public async Task<DapperMaticApiKey?> LiveAsync(string id, CancellationToken cancellationToken)
    {
        var key = await FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (key is null || !key.IsLive(DateTimeOffset.UtcNow))
        {
            return null;
        }

        return key.Kind == DapperMaticKeyKind.Service && AccountOf(key.Owner, key.Tenant) is not { Disabled: false } ? null : key;
    }

    /// <summary>
    /// Takes the uses recorded since last taken, to be kept.
    /// </summary>
    /// <returns>When each key was last used.</returns>
    public Dictionary<string, DateTimeOffset> TakeUsed()
    {
        var taken = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
        foreach (var id in _used.Keys)
        {
            if (_used.TryRemove(id, out var at))
            {
                taken[id] = at;
            }
        }

        return taken;
    }

    /// <summary>
    /// Finds a personal key's owner as they are now: by the built-in accounts, else the application's lookup.
    /// </summary>
    /// <param name="ownerId">The owner's user id.</param>
    /// <param name="tenant">The key's tenant.</param>
    /// <param name="services">The request's services.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The owner, or null.</returns>
    /// <exception cref="InvalidOperationException">Nothing finds owners: personal keys cannot be used.</exception>
    public async ValueTask<ClaimsPrincipal?> OwnerAsync(string ownerId, string? tenant, IServiceProvider services, CancellationToken cancellationToken)
    {
        if (services.GetService<IDapperMaticKeyOwners>() is { } owners)
        {
            return await owners.FindAsync(ownerId, tenant, cancellationToken).ConfigureAwait(false);
        }

        return Settings.ResolveOwner is { } resolve
            ? await resolve(ownerId, tenant, services, cancellationToken).ConfigureAwait(false)
            : throw new InvalidOperationException(PersonalKeysNeedOwners);
    }

    /// <summary>
    /// Tells whether personal keys can be used: something finds their owners.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>True if they can.</returns>
    public bool CanFindOwners(IServiceProvider services) => Settings.ResolveOwner is not null || services.GetService<IDapperMaticKeyOwners>() is not null;

    // The key's caller: its service account, or its owner as they are now; null when that cannot sign in.
    private async Task<ClaimsPrincipal?> CallerOfAsync(DapperMaticApiKey key, IServiceProvider services, CancellationToken cancellationToken)
    {
        var claims = new List<Claim>
        {
            new(DapperMaticClaims.Key, key.Id),
            new(DapperMaticClaims.KeyKind, key.Kind == DapperMaticKeyKind.Service ? "service" : "personal"),
        };
        if (key.Tenant is not null)
        {
            claims.Add(new Claim(DapperMaticClaims.Tenant, key.Tenant));
        }

        if (key.Kind == DapperMaticKeyKind.Service)
        {
            if (AccountOf(key.Owner, key.Tenant) is not { Disabled: false } account)
            {
                return null;
            }

            claims.Add(new Claim(ClaimTypes.NameIdentifier, $"service:{account.Tenant ?? "-"}:{account.Name}"));
            claims.Add(new Claim(ClaimTypes.Name, account.Name));
            claims.Add(new Claim(DapperMaticClaims.ServiceAccount, account.Name));
            claims.AddRange(account.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
            claims.AddRange(account.Claims.SelectMany(c => c.Value.Select(v => new Claim(c.Key, v))));
            return new ClaimsPrincipal(new ClaimsIdentity(claims, DapperMaticClaims.ApiKeyAuthenticationType, ClaimTypes.Name, ClaimTypes.Role));
        }

        // A personal key is its owner as they are now, never more: their identities, and the key's own naming it.
        var owner = await OwnerAsync(key.Owner, key.Tenant, services, cancellationToken).ConfigureAwait(false);
        if (owner?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return new ClaimsPrincipal(owner.Identities.Append(new ClaimsIdentity(claims, DapperMaticClaims.ApiKeyAuthenticationType, ClaimTypes.Name, ClaimTypes.Role)));
    }

    // A key's use, recorded at most once a minute: kept by the worker that takes them.
    private void Used(string id, DateTimeOffset now)
    {
        if (_recorded.TryGetValue(id, out var last) && now - last < UseRecorded)
        {
            return;
        }

        _recorded[id] = now;
        _used[id] = now;
    }
}
