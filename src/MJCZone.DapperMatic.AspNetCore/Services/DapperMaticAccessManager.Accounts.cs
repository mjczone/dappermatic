// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using MJCZone.DapperMatic.AspNetCore.Accounts;

namespace MJCZone.DapperMatic.AspNetCore.Services;

/// <summary>A built-in account as administrators see it.</summary>
/// <param name="Id">The account's id.</param>
/// <param name="Email">Its email.</param>
/// <param name="EmailConfirmed">Whether its email is confirmed.</param>
/// <param name="Disabled">Whether it is turned off.</param>
/// <param name="LockedOutUntil">When a lockout ends, or null.</param>
/// <param name="PlatformRoles">Its platform roles (shown to those who manage access to everything).</param>
/// <param name="Memberships">Its memberships and grants, in the caller's scope.</param>
#pragma warning disable SA1313 // Positional record parameters are properties.
public sealed record DapperMaticAccountSummary(
    string Id,
    string? Email,
    bool EmailConfirmed,
    bool Disabled,
    DateTimeOffset? LockedOutUntil,
    IReadOnlyList<string>? PlatformRoles,
    IReadOnlyList<DapperMaticMembershipInfo> Memberships
);

/// <summary>Gives an account a membership of a tenant.</summary>
/// <param name="Roles">The roles there.</param>
public sealed record DapperMaticMembershipRequest(IReadOnlyList<string> Roles);

/// <summary>Grants a platform user a tenant's roles, for a while: they sign in to the tenant to use them.</summary>
/// <param name="UserId">The account's id.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="Roles">The roles there.</param>
/// <param name="ExpiresAt">When it ends: within the longest grant the options allow.</param>
/// <param name="Reason">Why: the audit names it.</param>
public sealed record DapperMaticGrantRequest(string UserId, string Tenant, IReadOnlyList<string> Roles, DateTimeOffset ExpiresAt, string? Reason);

/// <summary>Invites an email: to a tenant with roles there, or (with no tenant) to the platform with platform roles.</summary>
/// <param name="Email">The email.</param>
/// <param name="Tenant">The tenant, or null for the platform.</param>
/// <param name="Roles">The roles.</param>
public sealed record DapperMaticInvitationRequest(string Email, string? Tenant, IReadOnlyList<string> Roles);

/// <summary>An invitation made: its token, shown this once (also sent to the email).</summary>
/// <param name="Id">The invitation's id.</param>
/// <param name="Token">The token, shown to those who manage access to everything only (a tenant's administrator's invitations reach their email only).</param>
/// <param name="ExpiresAt">When it stops working.</param>
public sealed record DapperMaticInvitationCreated(string Id, string? Token, DateTimeOffset ExpiresAt);

/// <summary>An invitation as it is listed.</summary>
/// <param name="Id">Its id.</param>
/// <param name="Email">The email invited.</param>
/// <param name="Tenant">The tenant, or null for the platform.</param>
/// <param name="Roles">The roles.</param>
/// <param name="InvitedBy">Who invited.</param>
/// <param name="ExpiresAt">When it stops working.</param>
public sealed record DapperMaticInvitationInfo(string Id, string Email, string? Tenant, IReadOnlyList<string> Roles, string? InvitedBy, DateTimeOffset ExpiresAt);
#pragma warning restore SA1313

/// <summary>
/// The built-in accounts, for administrators: those who manage access to everything see and manage every account,
/// its platform roles and grants; a tenant's administrators manage their tenant's members and invitations (giving only
/// roles they may give) and see, and revoke, the grants to their tenant. Callers signed in by a key manage none of it.
/// </summary>
public sealed partial class DapperMaticAccessManager
{
    /// <summary>
    /// Lists accounts: every one, a tenant's members, or a tenant administrator's members.
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <param name="tenant">A tenant whose members to list, or null.</param>
    /// <param name="after">The last email of the page before, or null.</param>
    /// <param name="take">How many at most (1 to 1,000).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The accounts.</returns>
    public async Task<IReadOnlyList<DapperMaticAccountSummary>> GetAccountsAsync(DapperMaticOperation context, string? tenant, string? after, int take, CancellationToken cancellationToken = default)
    {
        await AuthorizeAsync(context, cancellationToken).ConfigureAwait(false);
        var own = KeysScope(context, tenant);
        var store = AccountStore();
        var scopeTenant = own ?? tenant;
        IReadOnlyCollection<string>? ids = scopeTenant is null ? null : [.. (await store.MembersAsync(scopeTenant, null, cancellationToken).ConfigureAwait(false)).Select(m => m.UserId).Distinct()];
        var users = await store.UsersAsync(ids, after, Math.Clamp(take, 1, 1_000), cancellationToken).ConfigureAwait(false);
        var accounts = new List<DapperMaticAccountSummary>();
        foreach (var user in users)
        {
            var memberships = (await store.MembershipsAsync(user.Id, cancellationToken).ConfigureAwait(false))
                .Where(m => own is null || m.TenantId == own)
                .Select(DapperMaticMembershipInfo.Of)
                .ToList();
            var platform = own is null ? await store.PlatformRolesAsync(user.Id, cancellationToken).ConfigureAwait(false) : null;
            accounts.Add(new DapperMaticAccountSummary(user.Id, user.Email, user.EmailConfirmed, user.Disabled, user.LockoutEnd > DateTimeOffset.UtcNow ? user.LockoutEnd : null, platform, memberships));
        }

        return accounts;
    }

    /// <summary>
    /// Sets an account's platform roles: by those who manage access to everything.
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <param name="userId">The account's id.</param>
    /// <param name="roles">The roles.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task SetPlatformRolesAsync(DapperMaticOperation context, string userId, IReadOnlyList<string> roles, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(roles);
        await AuthorizeChangeAsync(context, cancellationToken).ConfigureAwait(false);
        NotByKey(context, "manage accounts");
        AppWideOnly(context, "Platform roles");
        var user = await AccountAsync(userId, cancellationToken).ConfigureAwait(false);
        await AccountStore().SetPlatformRolesAsync(user.Id, roles, cancellationToken).ConfigureAwait(false);
        Forget(user.Id);
        await ChangedAsync(context, $"Set the platform roles of the account '{user.Email}'", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Turns an account off (its sign-ins, sessions and keys end) or on: by those who manage access to everything.
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <param name="userId">The account's id.</param>
    /// <param name="disabled">Whether it is off.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task SetDisabledAsync(DapperMaticOperation context, string userId, bool disabled, CancellationToken cancellationToken = default)
    {
        await AuthorizeChangeAsync(context, cancellationToken).ConfigureAwait(false);
        NotByKey(context, "manage accounts");
        AppWideOnly(context, "Turning accounts off or on");
        var users = Services().GetRequiredService<UserManager<DapperMaticUser>>();
        var user = await AccountAsync(userId, cancellationToken).ConfigureAwait(false);
        user.Disabled = disabled;
        var result = await users.UpdateAsync(user).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        if (disabled)
        {
            await users.UpdateSecurityStampAsync(user).ConfigureAwait(false);
            await AccountStore().EndSessionsAsync(user.Id, null, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        }

        Forget(user.Id);
        await ChangedAsync(context, $"Turned the account '{user.Email}' {(disabled ? "off" : "on")}", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gives an account a membership of a tenant, or changes its roles there.
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <param name="tenant">The tenant (a tenant administrator's own).</param>
    /// <param name="userId">The account's id.</param>
    /// <param name="request">The roles.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The membership.</returns>
    public async Task<DapperMaticMembershipInfo> SaveMembershipAsync(DapperMaticOperation context, string tenant, string userId, DapperMaticMembershipRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await AuthorizeChangeAsync(context, cancellationToken).ConfigureAwait(false);
        NotByKey(context, "manage memberships");
        var own = KeysScope(context, tenant);
        tenant = own ?? tenant;
        AssertMayGive(context, own, request.Roles, null);
        var user = await AccountAsync(userId, cancellationToken).ConfigureAwait(false);
        var store = AccountStore();
        var kept = (await store.MembershipsAsync(user.Id, cancellationToken).ConfigureAwait(false)).FirstOrDefault(m => m.TenantId == tenant && !m.IsGrant);
        var membership = kept ?? new DapperMaticMembership { UserId = user.Id, TenantId = tenant, GrantedBy = UserIdOf(context.User) };
        membership.Roles = DapperMaticAccountStore.RolesText(request.Roles);
        await store.SaveMembershipAsync(membership, cancellationToken).ConfigureAwait(false);
        Forget(user.Id);
        await ChangedAsync(context, $"Saved the membership of '{user.Email}' in the tenant '{tenant}'", cancellationToken).ConfigureAwait(false);
        return DapperMaticMembershipInfo.Of(membership);
    }

    /// <summary>
    /// Removes an account's membership of a tenant.
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <param name="tenant">The tenant (a tenant administrator's own).</param>
    /// <param name="userId">The account's id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task DeleteMembershipAsync(DapperMaticOperation context, string tenant, string userId, CancellationToken cancellationToken = default)
    {
        await AuthorizeChangeAsync(context, cancellationToken).ConfigureAwait(false);
        NotByKey(context, "manage memberships");
        tenant = KeysScope(context, tenant) ?? tenant;
        var store = AccountStore();
        var kept = (await store.MembershipsAsync(userId, cancellationToken).ConfigureAwait(false)).FirstOrDefault(m => m.TenantId == tenant && !m.IsGrant)
            ?? throw new KeyNotFoundException($"The account is not a member of the tenant '{tenant}'.");
        await store.DeleteMembershipAsync(kept.Id, cancellationToken).ConfigureAwait(false);
        Forget(userId);
        await ChangedAsync(context, $"Removed the membership of '{userId}' in the tenant '{tenant}'", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Lists grants: to a tenant administrator's tenant, or to any tenant for those who manage everything.
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <param name="tenant">A tenant, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The grants, live or not.</returns>
    public async Task<IReadOnlyList<DapperMaticMembershipInfo>> GetGrantsAsync(DapperMaticOperation context, string? tenant, CancellationToken cancellationToken = default)
    {
        await AuthorizeAsync(context, cancellationToken).ConfigureAwait(false);
        var own = KeysScope(context, tenant);
        return [.. (await AccountStore().MembersAsync(own ?? tenant, grants: true, cancellationToken).ConfigureAwait(false)).Select(DapperMaticMembershipInfo.Of)];
    }

    /// <summary>
    /// Grants a platform user a tenant's roles until a time (within the longest grant): by those who manage
    /// everything. The audit names the grant on everything done under it.
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <param name="request">The account, tenant, roles, end and reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The grant.</returns>
    public async Task<DapperMaticMembershipInfo> GrantAsync(DapperMaticOperation context, DapperMaticGrantRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await AuthorizeChangeAsync(context, cancellationToken).ConfigureAwait(false);
        NotByKey(context, "make grants");
        AppWideOnly(context, "Grants");
        var now = DateTimeOffset.UtcNow;
        var longest = _options.Accounts.MaxGrantDuration;
        if (request.ExpiresAt <= now || request.ExpiresAt > now + longest)
        {
            throw new ArgumentException($"A grant ends in the future, within {longest}.");
        }

        var user = await AccountAsync(request.UserId, cancellationToken).ConfigureAwait(false);
        var grant = new DapperMaticMembership
        {
            UserId = user.Id,
            TenantId = request.Tenant,
            Roles = DapperMaticAccountStore.RolesText(request.Roles),
            IsGrant = true,
            GrantedBy = UserIdOf(context.User),
            Reason = request.Reason,
            ExpiresAt = request.ExpiresAt,
        };
        await AccountStore().SaveMembershipAsync(grant, cancellationToken).ConfigureAwait(false);
        Forget(user.Id);
        await ChangedAsync(context, $"Granted '{user.Email}' the tenant '{request.Tenant}' until {request.ExpiresAt:O}: {request.Reason}", cancellationToken).ConfigureAwait(false);
        return DapperMaticMembershipInfo.Of(grant);
    }

    /// <summary>
    /// Revokes a grant: the tenant's administrators may revoke those to their tenant.
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <param name="id">The grant's id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task RevokeGrantAsync(DapperMaticOperation context, string id, CancellationToken cancellationToken = default)
    {
        await AuthorizeChangeAsync(context, cancellationToken).ConfigureAwait(false);
        NotByKey(context, "revoke grants");
        var own = KeysScope(context, null);
        var store = AccountStore();
        if (await store.MembershipAsync(id, cancellationToken).ConfigureAwait(false) is not { IsGrant: true } grant || (own is not null && grant.TenantId != own))
        {
            throw new KeyNotFoundException($"No grant the caller manages has the id '{id}'.");
        }

        await store.DeleteMembershipAsync(id, cancellationToken).ConfigureAwait(false);
        Forget(grant.UserId);
        await ChangedAsync(context, $"Revoked the grant '{id}' of the tenant '{grant.TenantId}'", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Lists invitations not accepted: a tenant administrator's tenant's, or any for those who manage everything.
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <param name="tenant">A tenant, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The invitations.</returns>
    public async Task<IReadOnlyList<DapperMaticInvitationInfo>> GetInvitationsAsync(DapperMaticOperation context, string? tenant, CancellationToken cancellationToken = default)
    {
        await AuthorizeAsync(context, cancellationToken).ConfigureAwait(false);
        var own = KeysScope(context, tenant);
        return
        [
            .. (await AccountStore().InvitationsAsync(own ?? tenant, cancellationToken).ConfigureAwait(false))
                .Select(i => new DapperMaticInvitationInfo(i.Id, i.Email, i.TenantId, DapperMaticAccountStore.RolesOf(i.Roles), i.InvitedBy, i.ExpiresAt)),
        ];
    }

    /// <summary>
    /// Invites an email: a tenant administrator to their tenant (roles they may give); those who manage everything to
    /// any tenant, or to the platform.
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <param name="request">The email, tenant and roles.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The invitation, its token shown this once.</returns>
    public async Task<DapperMaticInvitationCreated> InviteAsync(DapperMaticOperation context, DapperMaticInvitationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await AuthorizeChangeAsync(context, cancellationToken).ConfigureAwait(false);
        NotByKey(context, "invite");
        var own = KeysScope(context, request.Tenant);
        if (own is not null && request.Tenant is not null && request.Tenant != own)
        {
            throw new UnauthorizedAccessException($"The tenant '{request.Tenant}' is not this caller's.");
        }

        AssertMayGive(context, own, request.Roles, null);
        var (invitation, token) = await Services().GetRequiredService<DapperMaticInvitations>()
            .InviteAsync(request.Email, own ?? request.Tenant, request.Roles, UserIdOf(context.User), cancellationToken)
            .ConfigureAwait(false);
        await ChangedAsync(context, $"Invited '{request.Email}'" + Of(invitation.TenantId), cancellationToken).ConfigureAwait(false);
        // A tenant's administrator does not see the token: accepting it makes a confirmed account of the email, which
        // only its owner may (they get it by email). Those who manage everything may deliver invitations themselves.
        return new DapperMaticInvitationCreated(invitation.Id, own is null ? token : null, invitation.ExpiresAt);
    }

    /// <summary>
    /// Withdraws an invitation.
    /// </summary>
    /// <param name="context">The operation.</param>
    /// <param name="id">The invitation's id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task DeleteInvitationAsync(DapperMaticOperation context, string id, CancellationToken cancellationToken = default)
    {
        await AuthorizeChangeAsync(context, cancellationToken).ConfigureAwait(false);
        NotByKey(context, "invite");
        var own = KeysScope(context, null);
        var store = AccountStore();
        if (await store.InvitationByIdAsync(id, cancellationToken).ConfigureAwait(false) is not { } invitation || (own is not null && invitation.TenantId != own))
        {
            throw new KeyNotFoundException($"No invitation the caller manages has the id '{id}'.");
        }

        await store.DeleteInvitationAsync(id, cancellationToken).ConfigureAwait(false);
        await ChangedAsync(context, $"Withdrew the invitation '{id}'", cancellationToken).ConfigureAwait(false);
    }

    private IServiceProvider Services() => _services ?? throw new InvalidOperationException("The built-in accounts are managed within a request.");

    private DapperMaticAccountStore AccountStore() =>
        Services().GetService<DapperMaticAccountStore>() ?? throw new InvalidOperationException("The built-in accounts are not turned on: call dm.AddUserAccounts().");

    private void Forget(string userId) => Services().GetService<DapperMaticAccountCheck>()?.Forget(userId);

    private async Task<DapperMaticUser> AccountAsync(string userId, CancellationToken cancellationToken) =>
        await AccountStore().UserAsync(userId, cancellationToken).ConfigureAwait(false) ?? throw new KeyNotFoundException($"No account has the id '{userId}'.");

    // What only those who manage access to everything do: a tenant's administrator changes nothing beyond their tenant.
    private void AppWideOnly(DapperMaticOperation context, string what)
    {
        if (KeysScope(context, null) is not null)
        {
            throw new UnauthorizedAccessException($"{what} are managed by those who manage access to everything.");
        }
    }
}
