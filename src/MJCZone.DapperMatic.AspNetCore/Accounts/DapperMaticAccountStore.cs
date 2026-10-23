// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Text.Json;

namespace MJCZone.DapperMatic.AspNetCore.Accounts;

/// <summary>
/// What DapperMatic keeps of the built-in accounts besides Identity's: platform roles, memberships and grants,
/// invitations, sign-in sessions and the accounts' settings.
/// </summary>
internal sealed class DapperMaticAccountStore
{
    private readonly DapperMaticAccountDb _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticAccountStore"/> class.
    /// </summary>
    /// <param name="db">The account tables' database.</param>
    public DapperMaticAccountStore(DapperMaticAccountDb db)
    {
        _db = db;
    }

    /// <summary>Reads a JSON array of roles.</summary>
    /// <param name="roles">The JSON.</param>
    /// <returns>The roles.</returns>
    public static List<string> RolesOf(string roles) => JsonSerializer.Deserialize<List<string>>(roles) ?? [];

    /// <summary>Writes roles as a JSON array.</summary>
    /// <param name="roles">The roles.</param>
    /// <returns>The JSON.</returns>
    public static string RolesText(IEnumerable<string> roles) => JsonSerializer.Serialize(roles.Distinct(StringComparer.Ordinal).ToList());

    /// <summary>Reads an account.</summary>
    /// <param name="userId">Its id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The account, or null.</returns>
    public async Task<DapperMaticUser?> UserAsync(string userId, CancellationToken cancellationToken)
    {
        using var db = await _db.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await _db.Scope(db).SingleByIdAsync<DapperMaticUser>(userId, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Tells whether any account exists.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if one does.</returns>
    public async Task<bool> AnyUserAsync(CancellationToken cancellationToken)
    {
        using var db = await _db.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await _db.Scope(db).ExistsAsync<DapperMaticUser>(u => true, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Lists accounts, by email, at most a page.</summary>
    /// <param name="ids">Only these, or null for any.</param>
    /// <param name="after">The last email of the page before, or null.</param>
    /// <param name="take">How many at most.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The accounts.</returns>
    public async Task<List<DapperMaticUser>> UsersAsync(IReadOnlyCollection<string>? ids, string? after, int take, CancellationToken cancellationToken)
    {
        using var db = await _db.OpenAsync(cancellationToken).ConfigureAwait(false);
        var query = _db.Scope(db).From<DapperMaticUser>();
        if (ids is not null)
        {
            var list = ids.ToList();
            query = query.Where(u => list.Contains(u.Id));
        }

        if (after is not null)
        {
            query = query.Where(u => string.Compare(u.NormalizedEmail, after) > 0);
        }

        return await query.OrderBy(u => u.NormalizedEmail).Take(take).ToListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads an account's platform roles.</summary>
    /// <param name="userId">The account's id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The roles.</returns>
    public async Task<List<string>> PlatformRolesAsync(string userId, CancellationToken cancellationToken)
    {
        using var db = await _db.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await _db.Scope(db).SelectAsync<DapperMaticUserRole>(r => r.UserId == userId, cancellationToken: cancellationToken).ConfigureAwait(false);
        return [.. rows.Select(r => r.Role).Order(StringComparer.Ordinal)];
    }

    /// <summary>Sets an account's platform roles.</summary>
    /// <param name="userId">The account's id.</param>
    /// <param name="roles">The roles.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task SetPlatformRolesAsync(string userId, IEnumerable<string> roles, CancellationToken cancellationToken)
    {
        using var db = await _db.OpenAsync(cancellationToken).ConfigureAwait(false);
        using var tx = db.BeginTransaction();
        var scope = _db.Scope(db);
        await scope.DeleteAsync<DapperMaticUserRole>(r => r.UserId == userId, tx, cancellationToken).ConfigureAwait(false);
        var rows = roles.Distinct(StringComparer.Ordinal).Select(r => new DapperMaticUserRole { UserId = userId, Role = r }).ToList();
        if (rows.Count > 0)
        {
            await scope.InsertManyAsync(rows, tx, cancellationToken).ConfigureAwait(false);
        }

        tx.Commit();
    }

    /// <summary>Reads an account's memberships and grants, live or not.</summary>
    /// <param name="userId">The account's id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The memberships.</returns>
    public async Task<List<DapperMaticMembership>> MembershipsAsync(string userId, CancellationToken cancellationToken)
    {
        using var db = await _db.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await _db.Scope(db).SelectAsync<DapperMaticMembership>(m => m.UserId == userId, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Lists a tenant's memberships and grants (or every one's), live or not.</summary>
    /// <param name="tenant">The tenant, or null for every one.</param>
    /// <param name="grants">True for grants only, false for members only, null for both.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The memberships.</returns>
    public async Task<List<DapperMaticMembership>> MembersAsync(string? tenant, bool? grants, CancellationToken cancellationToken)
    {
        using var db = await _db.OpenAsync(cancellationToken).ConfigureAwait(false);
        var query = _db.Scope(db).From<DapperMaticMembership>();
        if (tenant is not null)
        {
            query = query.Where(m => m.TenantId == tenant);
        }

        if (grants is { } isGrant)
        {
            query = query.Where(m => m.IsGrant == isGrant);
        }

        return await query.OrderBy(m => m.CreatedAt).ToListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads a membership by its id.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The membership, or null.</returns>
    public async Task<DapperMaticMembership?> MembershipAsync(string id, CancellationToken cancellationToken)
    {
        using var db = await _db.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await _db.Scope(db).SingleByIdAsync<DapperMaticMembership>(id, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds an account to a tenant, or replaces its membership there of the same kind: a member's replaces a member's, a
    /// grant a grant (an account can be a member of a tenant and hold a grant to it).
    /// </summary>
    /// <param name="membership">The membership.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task SaveMembershipAsync(DapperMaticMembership membership, CancellationToken cancellationToken)
    {
        using var db = await _db.OpenAsync(cancellationToken).ConfigureAwait(false);
        using var tx = db.BeginTransaction();
        var scope = _db.Scope(db);
        var isGrant = membership.IsGrant;
        await scope.DeleteAsync<DapperMaticMembership>(
                m => m.UserId == membership.UserId && m.TenantId == membership.TenantId && m.IsGrant == isGrant && m.Id != membership.Id,
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
        await scope.UpsertAsync(membership, tx, cancellationToken).ConfigureAwait(false);
        tx.Commit();
    }

    /// <summary>Removes a membership.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if it was there.</returns>
    public async Task<bool> DeleteMembershipAsync(string id, CancellationToken cancellationToken)
    {
        using var db = await _db.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await _db.Scope(db).DeleteByIdAsync<DapperMaticMembership>(id, cancellationToken: cancellationToken).ConfigureAwait(false) > 0;
    }

    /// <summary>Keeps an invitation.</summary>
    /// <param name="invitation">The invitation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task SaveInvitationAsync(DapperMaticInvitation invitation, CancellationToken cancellationToken)
    {
        using var db = await _db.OpenAsync(cancellationToken).ConfigureAwait(false);
        await _db.Scope(db).InsertAsync(invitation, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Finds an invitation by its token's hash.</summary>
    /// <param name="tokenHash">The hash.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The invitation, or null.</returns>
    public async Task<DapperMaticInvitation?> InvitationAsync(string tokenHash, CancellationToken cancellationToken)
    {
        using var db = await _db.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await _db.Scope(db).SingleAsync<DapperMaticInvitation>(i => i.TokenHash == tokenHash, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads an invitation by its id.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The invitation, or null.</returns>
    public async Task<DapperMaticInvitation?> InvitationByIdAsync(string id, CancellationToken cancellationToken)
    {
        using var db = await _db.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await _db.Scope(db).SingleByIdAsync<DapperMaticInvitation>(id, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Lists invitations not accepted: a tenant's, the platform's (an empty tenant), or every one.</summary>
    /// <param name="tenant">The tenant, an empty string for the platform's, or null for every one.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The invitations.</returns>
    public async Task<List<DapperMaticInvitation>> InvitationsAsync(string? tenant, CancellationToken cancellationToken)
    {
        using var db = await _db.OpenAsync(cancellationToken).ConfigureAwait(false);
        var query = _db.Scope(db).From<DapperMaticInvitation>().Where(i => i.AcceptedAt == null);
        if (tenant is { Length: 0 })
        {
            query = query.Where(i => i.TenantId == null);
        }
        else if (tenant is not null)
        {
            query = query.Where(i => i.TenantId == tenant);
        }

        return await query.OrderBy(i => i.CreatedAt).ToListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Accepts an invitation: once, before it expires; a second request accepts nothing.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="now">The time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if this request accepted it.</returns>
    public async Task<bool> ClaimInvitationAsync(string id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        using var db = await _db.OpenAsync(cancellationToken).ConfigureAwait(false);
        DateTimeOffset? at = now;
        return await _db.Scope(db)
            .UpdateAsync<DapperMaticInvitation>(i => new DapperMaticInvitation { AcceptedAt = at }, i => i.Id == id && i.AcceptedAt == null && i.ExpiresAt > now, cancellationToken: cancellationToken)
            .ConfigureAwait(false) == 1;
    }

    /// <summary>Gives an invitation back, when what accepting it would do failed.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task UnclaimInvitationAsync(string id, CancellationToken cancellationToken)
    {
        using var db = await _db.OpenAsync(cancellationToken).ConfigureAwait(false);
        await _db.Scope(db).UpdateAsync<DapperMaticInvitation>(i => new DapperMaticInvitation { AcceptedAt = null }, i => i.Id == id, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Removes an invitation.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if it was there.</returns>
    public async Task<bool> DeleteInvitationAsync(string id, CancellationToken cancellationToken)
    {
        using var db = await _db.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await _db.Scope(db).DeleteByIdAsync<DapperMaticInvitation>(id, cancellationToken: cancellationToken).ConfigureAwait(false) > 0;
    }

    /// <summary>Starts a sign-in session.</summary>
    /// <param name="session">The session.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task StartSessionAsync(DapperMaticUserSession session, CancellationToken cancellationToken)
    {
        using var db = await _db.OpenAsync(cancellationToken).ConfigureAwait(false);
        await _db.Scope(db).InsertAsync(session, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads a sign-in session.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The session, or null.</returns>
    public async Task<DapperMaticUserSession?> SessionAsync(string id, CancellationToken cancellationToken)
    {
        using var db = await _db.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await _db.Scope(db).SingleByIdAsync<DapperMaticUserSession>(id, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Notes a session refreshed.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="now">The time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task TouchSessionAsync(string id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        using var db = await _db.OpenAsync(cancellationToken).ConfigureAwait(false);
        DateTimeOffset? at = now;
        await _db.Scope(db).UpdateAsync<DapperMaticUserSession>(s => new DapperMaticUserSession { RefreshedAt = at }, s => s.Id == id, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Moves a live session on to the account's new security stamp, if it held to the one before.
    /// </summary>
    /// <param name="id">The session's id.</param>
    /// <param name="before">The stamp before the change.</param>
    /// <param name="after">The stamp after it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task KeepSessionAsync(string id, string? before, string? after, CancellationToken cancellationToken)
    {
        using var db = await _db.OpenAsync(cancellationToken).ConfigureAwait(false);
        await _db.Scope(db)
            .UpdateAsync<DapperMaticUserSession>(
                s => new DapperMaticUserSession { Stamp = after },
                s => s.Id == id && s.RevokedAt == null && (s.Stamp == null || s.Stamp == before),
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>Ends sign-in sessions: one, or every one of an account.</summary>
    /// <param name="userId">The account's id.</param>
    /// <param name="id">The session's id, or null for every one.</param>
    /// <param name="now">The time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task EndSessionsAsync(string userId, string? id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        using var db = await _db.OpenAsync(cancellationToken).ConfigureAwait(false);
        DateTimeOffset? at = now;
        await _db.Scope(db)
            .UpdateAsync<DapperMaticUserSession>(
                s => new DapperMaticUserSession { RevokedAt = at },
                s => s.UserId == userId && s.RevokedAt == null && (id == null || s.Id == id),
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>Keeps a setting unless one of its name is kept already.</summary>
    /// <param name="name">The setting.</param>
    /// <param name="value">Its value.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if this kept it.</returns>
    public async Task<bool> AddSettingAsync(string name, string value, CancellationToken cancellationToken)
    {
        using var db = await _db.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _db.Scope(db).InsertAsync(new DapperMaticAccountSetting { Name = name, Value = value }, cancellationToken: cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (DmDatabaseErrors.Classify(ex) == DmDatabaseErrorKind.UniqueViolation)
        {
            return false;
        }
    }

    /// <summary>Reads a setting.</summary>
    /// <param name="name">The setting.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Its value, or null.</returns>
    public async Task<string?> SettingAsync(string name, CancellationToken cancellationToken)
    {
        using var db = await _db.OpenAsync(cancellationToken).ConfigureAwait(false);
        return (await _db.Scope(db).SingleByIdAsync<DapperMaticAccountSetting>(name, cancellationToken: cancellationToken).ConfigureAwait(false))?.Value;
    }

    /// <summary>Takes a setting of a value: removes it, once; a second request takes nothing.</summary>
    /// <param name="name">The setting.</param>
    /// <param name="value">The value it must have.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if this request took it.</returns>
    public async Task<bool> TakeSettingAsync(string name, string value, CancellationToken cancellationToken)
    {
        using var db = await _db.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await _db.Scope(db).DeleteAsync<DapperMaticAccountSetting>(s => s.Name == name && s.Value == value, cancellationToken: cancellationToken).ConfigureAwait(false) == 1;
    }
}
