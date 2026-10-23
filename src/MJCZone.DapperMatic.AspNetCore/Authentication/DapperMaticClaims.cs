// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Security.Claims;

namespace MJCZone.DapperMatic.AspNetCore.Authentication;

/// <summary>
/// The claims and authentication types of the callers DapperMatic signs in itself: by an API key, a built-in account,
/// a JWT it issued, a stream's ticket. Such a caller names its tenant itself (<see cref="Tenant"/>), and the tenancy
/// sources are not read for it: a request cannot choose another. The claim is read only on identities of DapperMatic's
/// own authentication types, which only DapperMatic's handlers make, so a token from elsewhere carrying it is not
/// believed.
/// </summary>
public static class DapperMaticClaims
{
    /// <summary>What the authentication types of DapperMatic's own identities begin with.</summary>
    public const string AuthenticationTypePrefix = "DapperMatic.";

    /// <summary>The authentication type of a caller signed in by an API key.</summary>
    public const string ApiKeyAuthenticationType = "DapperMatic.ApiKey";

    /// <summary>The authentication type of a caller signed in to a built-in account: by its token (a JWT too), or a cookie.</summary>
    public const string AccountAuthenticationType = "DapperMatic.Account";

    /// <summary>The authentication type of a stream's caller, signed in by a ticket.</summary>
    public const string TicketAuthenticationType = "DapperMatic.Ticket";

    /// <summary>The caller's tenant, when they act in one.</summary>
    public const string Tenant = "dm_tenant";

    /// <summary>The id of the API key the caller signed in with.</summary>
    public const string Key = "dm_key";

    /// <summary>The kind of that key: <c>service</c> or <c>personal</c>.</summary>
    public const string KeyKind = "dm_key_kind";

    /// <summary>The service account a service key is of.</summary>
    public const string ServiceAccount = "dm_service_account";

    /// <summary>The membership a built-in account acts in a tenant by.</summary>
    public const string Membership = "dm_membership";

    /// <summary>The grant a platform user acts in a tenant under, when they do.</summary>
    public const string Grant = "dm_grant";

    /// <summary>Who made that grant.</summary>
    public const string GrantedBy = "dm_granted_by";

    /// <summary>The sign-in session a built-in account's token or cookie is of: signing out ends it.</summary>
    public const string Session = "dm_session";

    /// <summary>
    /// Tells whether an identity is one DapperMatic signed in itself.
    /// </summary>
    /// <param name="identity">The identity.</param>
    /// <returns>True for an API key's, a built-in account's, a DapperMatic JWT's or a ticket's.</returns>
    public static bool IsDapperMatics(ClaimsIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return identity.AuthenticationType?.StartsWith(AuthenticationTypePrefix, StringComparison.Ordinal) == true;
    }

    /// <summary>
    /// Reads the tenant a caller DapperMatic signed in names: the <see cref="Tenant"/> of its DapperMatic identities.
    /// </summary>
    /// <param name="user">The caller.</param>
    /// <param name="tenant">The tenant, or null for none (a platform caller).</param>
    /// <returns>True if DapperMatic signed the caller in, whatever their tenant; false for anyone else.</returns>
    /// <exception cref="UnauthorizedAccessException">Its identities name different tenants.</exception>
    public static bool TryGetTenant(ClaimsPrincipal user, out string? tenant)
    {
        ArgumentNullException.ThrowIfNull(user);
        tenant = null;
        var found = false;
        foreach (var identity in user.Identities.Where(IsDapperMatics))
        {
            var named = identity.FindFirst(Tenant)?.Value;
            if (found && !string.Equals(named, tenant, StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException("This caller's credentials name different tenants.");
            }

            tenant = named;
            found = true;
        }

        return found;
    }

    /// <summary>
    /// Tells whether a caller signed in with an API key: such a caller cannot make keys, change accounts, invite or grant.
    /// </summary>
    /// <param name="user">The caller.</param>
    /// <returns>True if one of their identities is an API key's.</returns>
    public static bool ByKey(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return user.Identities.Any(i => i.AuthenticationType == ApiKeyAuthenticationType);
    }

    /// <summary>
    /// Tells whether a caller signed in with a ticket: such a caller cannot get another.
    /// </summary>
    /// <param name="user">The caller.</param>
    /// <returns>True if one of their identities is a ticket's.</returns>
    public static bool ByTicket(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return user.Identities.Any(i => i.AuthenticationType == TicketAuthenticationType);
    }
}
