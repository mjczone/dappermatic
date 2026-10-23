// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Authentication;
using MJCZone.DapperMatic.AspNetCore.Security;

namespace MJCZone.DapperMatic.AspNetCore.Accounts;

/// <summary>
/// Where a sign-in puts its account, for the principal it makes (Identity asks for a principal from the account alone):
/// the tenant chosen, if one was, and the sign-in session. One per request.
/// </summary>
internal sealed class DapperMaticAccountPlace
{
    /// <summary>Gets or sets the tenant chosen, or null for the platform.</summary>
    public string? Tenant { get; set; }

    /// <summary>Gets or sets a value indicating whether a place was chosen; when not, the account's only tenant (if it has no platform roles), or the platform.</summary>
    public bool Chosen { get; set; }

    /// <summary>Gets or sets the sign-in session, or null.</summary>
    public string? Session { get; set; }
}

/// <summary>
/// Makes a built-in account's principal for a place. Its roles are the place's: a membership's in a tenant (a grant's
/// marked as one), the platform roles with no tenant; never both.
/// </summary>
internal sealed class DapperMaticAccountPrincipals : IUserClaimsPrincipalFactory<DapperMaticUser>
{
    private readonly DapperMaticAccountStore _store;
    private readonly IdentityOptions _identity;
    private readonly DapperMaticAccountPlace _place;

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticAccountPrincipals"/> class.
    /// </summary>
    /// <param name="store">The accounts' memberships and roles.</param>
    /// <param name="identity">Identity's options: its claim types.</param>
    /// <param name="place">The request's place.</param>
    public DapperMaticAccountPrincipals(DapperMaticAccountStore store, IOptions<IdentityOptions> identity, DapperMaticAccountPlace place)
    {
        _store = store;
        _identity = identity.Value;
        _place = place;
    }

    /// <inheritdoc />
    public Task<ClaimsPrincipal> CreateAsync(DapperMaticUser user) => CreateAsync(user, _place.Tenant, _place.Chosen, _place.Session, CancellationToken.None);

    /// <summary>
    /// Makes an account's principal in a place.
    /// </summary>
    /// <param name="user">The account.</param>
    /// <param name="tenant">The tenant, or null for the platform.</param>
    /// <param name="chosen">Whether the place was chosen; when not, the account's only tenant if it has no platform roles, else the platform.</param>
    /// <param name="session">The sign-in session, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The principal.</returns>
    /// <exception cref="UnauthorizedAccessException">The account is not a member of the tenant chosen.</exception>
    public async Task<ClaimsPrincipal> CreateAsync(DapperMaticUser user, string? tenant, bool chosen, string? session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        var now = DateTimeOffset.UtcNow;
        var memberships = (await _store.MembershipsAsync(user.Id, cancellationToken).ConfigureAwait(false)).Where(m => m.IsLive(now)).ToList();
        var platform = await _store.PlatformRolesAsync(user.Id, cancellationToken).ConfigureAwait(false);
        DapperMaticMembership? membership = null;
        if (chosen)
        {
            if (tenant is not null)
            {
                // A membership before a grant: a grant is for where the account has no place of its own.
                membership = memberships.Where(m => m.TenantId == tenant).OrderBy(m => m.IsGrant).FirstOrDefault()
                    ?? throw new UnauthorizedAccessException($"This account is not a member of the tenant '{tenant}'.");
            }
        }
        else if (platform.Count == 0 && memberships.Count == 1)
        {
            membership = memberships[0];
        }

        var types = _identity.ClaimsIdentity;
        var claims = new List<Claim> { new(types.UserIdClaimType, user.Id), new(types.UserNameClaimType, user.UserName ?? user.Id) };
        if (user.Email is { } email)
        {
            claims.Add(new Claim(types.EmailClaimType, email));
        }

        if (user.SecurityStamp is { } stamp)
        {
            claims.Add(new Claim(types.SecurityStampClaimType, stamp));
        }

        if (session is not null)
        {
            claims.Add(new Claim(DapperMaticClaims.Session, session));
        }

        List<string> roles;
        if (membership is not null)
        {
            claims.Add(new Claim(DapperMaticClaims.Tenant, membership.TenantId));
            claims.Add(new Claim(DapperMaticClaims.Membership, membership.Id));
            if (membership.IsGrant)
            {
                claims.Add(new Claim(DapperMaticClaims.Grant, membership.Id));
                if (membership.GrantedBy is { } by)
                {
                    claims.Add(new Claim(DapperMaticClaims.GrantedBy, by));
                }
            }

            roles = DapperMaticAccountStore.RolesOf(membership.Roles);
        }
        else
        {
            roles = platform;
        }

        claims.AddRange(roles.Select(r => new Claim(types.RoleClaimType, r)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, DapperMaticClaims.AccountAuthenticationType, types.UserNameClaimType, types.RoleClaimType));
    }
}

/// <summary>
/// Identity's sign-in, refusing an account turned off.
/// </summary>
internal sealed class DapperMaticSignInManager : SignInManager<DapperMaticUser>
{
    private readonly DapperMaticAccountStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticSignInManager"/> class.
    /// </summary>
    /// <param name="users">The user manager.</param>
    /// <param name="http">The request's accessor.</param>
    /// <param name="principals">The principal factory.</param>
    /// <param name="options">Identity's options.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="schemes">The sign-in methods.</param>
    /// <param name="confirmation">The account confirmation check.</param>
    /// <param name="store">The accounts' sign-in sessions.</param>
    public DapperMaticSignInManager(
        UserManager<DapperMaticUser> users,
        IHttpContextAccessor http,
        IUserClaimsPrincipalFactory<DapperMaticUser> principals,
        IOptions<IdentityOptions> options,
        ILogger<SignInManager<DapperMaticUser>> logger,
        IAuthenticationSchemeProvider schemes,
        IUserConfirmation<DapperMaticUser> confirmation,
        DapperMaticAccountStore store
    )
        : base(users, http, principals, options, logger, schemes, confirmation)
    {
        _store = store;
    }

    /// <inheritdoc />
    public override async Task<bool> CanSignInAsync(DapperMaticUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return !user.Disabled && await base.CanSignInAsync(user).ConfigureAwait(false);
    }

    /// <summary>
    /// Checks a cookie's or refresh token's principal: one of a sign-in session holds while the session is live and
    /// holds to the account's security stamp; any other, by its own stamp.
    /// </summary>
    /// <param name="principal">The principal.</param>
    /// <returns>The account, or null.</returns>
    public override async Task<DapperMaticUser?> ValidateSecurityStampAsync(ClaimsPrincipal? principal)
    {
        if (principal?.Identities.FirstOrDefault(i => i.AuthenticationType == DapperMaticClaims.AccountAuthenticationType)?.FindFirst(DapperMaticClaims.Session)?.Value is not { } id)
        {
            return await base.ValidateSecurityStampAsync(principal).ConfigureAwait(false);
        }

        var user = await UserManager.GetUserAsync(principal).ConfigureAwait(false);
        var session = await _store.SessionAsync(id, CancellationToken.None).ConfigureAwait(false);
        return user is not null && DapperMaticAccountCheck.Holds(user, session, principal.FindFirst(Options.ClaimsIdentity.SecurityStampClaimType)?.Value) ? user : null;
    }
}

/// <summary>
/// The check of a built-in account's caller, each time their session is built or used again (read for a while, as the
/// policies are): the account there and on, its security stamp unchanged, its sign-in session not ended, its
/// membership of the tenant still live (a grant's expiry ending the session).
/// </summary>
internal sealed class DapperMaticAccountCheck(DapperMaticAccountStore store, IOptions<IdentityOptions> identity, IOptions<DapperMaticOptions> options) : IDapperMaticCallerCheck
{
    private readonly ConcurrentDictionary<string, (DapperMaticCallerTerms? Terms, DateTimeOffset ReadAt)> _checked = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public async ValueTask<DapperMaticCallerTerms?> CheckAsync(ClaimsPrincipal user, HttpContext http, bool again, CancellationToken cancellationToken)
    {
        if (user.Identities.FirstOrDefault(i => i.AuthenticationType == DapperMaticClaims.AccountAuthenticationType) is not { } account
            || account.FindFirst(identity.Value.ClaimsIdentity.UserIdClaimType)?.Value is not { } id)
        {
            return null;
        }

        var stamp = account.FindFirst(identity.Value.ClaimsIdentity.SecurityStampClaimType)?.Value;
        var tenant = account.FindFirst(DapperMaticClaims.Tenant)?.Value;
        var membership = account.FindFirst(DapperMaticClaims.Membership)?.Value;
        var session = account.FindFirst(DapperMaticClaims.Session)?.Value;
        var roles = account.FindAll(account.RoleClaimType).Select(c => c.Value).Order(StringComparer.Ordinal).ToList();
        var key = string.Join('\u001f', [id, stamp, tenant, membership, session, .. roles]);
        var now = DateTimeOffset.UtcNow;
        if (!_checked.TryGetValue(key, out var found) || now - found.ReadAt >= options.Value.AccessRefreshInterval)
        {
            found = (await TermsAsync(id, stamp, tenant, membership, session, roles, now, cancellationToken).ConfigureAwait(false), now);
            _checked[key] = found;
        }

        return found.Terms ?? throw new DapperMaticSignInEndedException("This account's sign-in has ended: sign in again.");
    }

    /// <summary>
    /// Forgets what was read of an account: changed here, it is read again at once.
    /// </summary>
    /// <param name="userId">The account's id.</param>
    public void Forget(string userId)
    {
        foreach (var key in _checked.Keys.Where(k => k.StartsWith(userId + "\u001f", StringComparison.Ordinal)))
        {
            _checked.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Tells whether a sign-in still holds to its account's security stamp: a session's by the stamp it holds to (moved
    /// on by its own caller's changes), else the token's own; a sign-in with neither never holds.
    /// </summary>
    /// <param name="user">The account, as it is now.</param>
    /// <param name="session">The sign-in session, if the sign-in has one (null if it has one no longer kept).</param>
    /// <param name="stamp">The stamp the token carries.</param>
    /// <returns>True if it holds.</returns>
    internal static bool Holds(DapperMaticUser user, DapperMaticUserSession? session, string? stamp) =>
        session is null ? stamp is not null && stamp == user.SecurityStamp
            : session.RevokedAt is null && session.UserId == user.Id && (session.Stamp ?? stamp) == user.SecurityStamp;

    // The terms of a sign-in still good, or null for one that has ended: the account turned off or locked out, its
    // stamp changed, its session ended, its membership (or grant) gone, or its place's roles changed (a refresh gives
    // the new ones).
    private async Task<DapperMaticCallerTerms?> TermsAsync(
        string id,
        string? stamp,
        string? tenant,
        string? membershipId,
        string? sessionId,
        List<string> roles,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        if (await store.UserAsync(id, cancellationToken).ConfigureAwait(false) is not { Disabled: false } account || account.LockoutEnd > now)
        {
            return null;
        }

        var session = sessionId is null ? null : await store.SessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if ((sessionId is not null && session is null) || !Holds(account, session, stamp))
        {
            return null;
        }

        DateTimeOffset? ends = null;
        List<string> current;
        if (tenant is not null)
        {
            if ((await store.MembershipsAsync(id, cancellationToken).ConfigureAwait(false)).FirstOrDefault(m => m.Id == membershipId && m.TenantId == tenant && m.IsLive(now)) is not { } membership)
            {
                return null;
            }

            ends = membership.ExpiresAt;
            current = DapperMaticAccountStore.RolesOf(membership.Roles);
        }
        else
        {
            current = await store.PlatformRolesAsync(id, cancellationToken).ConfigureAwait(false);
        }

        return current.Order(StringComparer.Ordinal).SequenceEqual(roles, StringComparer.Ordinal) ? new DapperMaticCallerTerms(null, ends) : null;
    }
}

/// <summary>
/// Finds a personal key's owner among the built-in accounts, as they are now: on, not locked out, a member of the
/// key's tenant.
/// </summary>
internal sealed class DapperMaticAccountOwners(DapperMaticAccountStore store, DapperMaticAccountPrincipals principals) : IDapperMaticKeyOwners
{
    /// <inheritdoc />
    public async ValueTask<ClaimsPrincipal?> FindAsync(string ownerId, string? tenant, CancellationToken cancellationToken)
    {
        if (await store.UserAsync(ownerId, cancellationToken).ConfigureAwait(false) is not { Disabled: false } user || user.LockoutEnd > DateTimeOffset.UtcNow)
        {
            return null;
        }

        try
        {
            return await principals.CreateAsync(user, tenant, chosen: true, session: null, cancellationToken).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
