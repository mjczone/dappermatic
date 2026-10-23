// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.DataAnnotations;

namespace MJCZone.DapperMatic.AspNetCore.Accounts;

/// <summary>
/// A built-in account: a person who signs in to DapperMatic with an email and a password (ASP.NET Core Identity's
/// sign-in, kept in DapperMatic's tables). Their roles are per place: the platform's (with no tenant), or a
/// membership's in a tenant.
/// </summary>
[DmTable(null, "dm_users")]
public sealed class DapperMaticUser
{
    /// <summary>Gets or sets the account's id.</summary>
    [DmColumn("id", length: 64, isPrimaryKey: true)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets or sets the name the account signs in with: its email.</summary>
    [DmColumn("user_name", length: 256, isNullable: true)]
    public string? UserName { get; set; }

    /// <summary>Gets or sets the name, normalized for lookups.</summary>
    [DmColumn("normalized_user_name", length: 256, isNullable: true, isUnique: true)]
    public string? NormalizedUserName { get; set; }

    /// <summary>Gets or sets the email.</summary>
    [DmColumn("email", length: 256, isNullable: true)]
    public string? Email { get; set; }

    /// <summary>Gets or sets the email, normalized for lookups.</summary>
    [DmColumn("normalized_email", length: 256, isNullable: true, isUnique: true)]
    public string? NormalizedEmail { get; set; }

    /// <summary>Gets or sets a value indicating whether the email is confirmed.</summary>
    [DmColumn("email_confirmed")]
    public bool EmailConfirmed { get; set; }

    /// <summary>Gets or sets the password's hash (Identity's).</summary>
    [DmColumn("password_hash", length: 1024, isNullable: true)]
    public string? PasswordHash { get; set; }

    /// <summary>Gets or sets the security stamp: changed when what the account signs in with changes, ending its sign-ins.</summary>
    [DmColumn("security_stamp", length: 64, isNullable: true)]
    public string? SecurityStamp { get; set; }

    /// <summary>Gets or sets the concurrency stamp: changed with each change of the row.</summary>
    [DmColumn("concurrency_stamp", length: 64, isNullable: true)]
    public string? ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets or sets the phone number.</summary>
    [DmColumn("phone_number", length: 64, isNullable: true)]
    public string? PhoneNumber { get; set; }

    /// <summary>Gets or sets a value indicating whether the phone number is confirmed.</summary>
    [DmColumn("phone_number_confirmed")]
    public bool PhoneNumberConfirmed { get; set; }

    /// <summary>Gets or sets a value indicating whether signing in needs a second factor.</summary>
    [DmColumn("two_factor_enabled")]
    public bool TwoFactorEnabled { get; set; }

    /// <summary>Gets or sets when a lockout ends, or null.</summary>
    [DmColumn("lockout_end", isNullable: true)]
    public DateTimeOffset? LockoutEnd { get; set; }

    /// <summary>Gets or sets a value indicating whether the account can be locked out.</summary>
    [DmColumn("lockout_enabled")]
    public bool LockoutEnabled { get; set; } = true;

    /// <summary>Gets or sets how many sign-ins failed in a row.</summary>
    [DmColumn("access_failed_count")]
    public int AccessFailedCount { get; set; }

    /// <summary>Gets or sets the authenticator key (for authenticator apps).</summary>
    [DmColumn("authenticator_key", length: 256, isNullable: true)]
    public string? AuthenticatorKey { get; set; }

    /// <summary>Gets or sets the recovery codes, as Identity keeps them.</summary>
    [DmColumn("recovery_codes", length: 4096, isNullable: true)]
    public string? RecoveryCodes { get; set; }

    /// <summary>Gets or sets a value indicating whether the account is turned off: it does not sign in, nor do its keys.</summary>
    [DmColumn("disabled")]
    public bool Disabled { get; set; }

    /// <summary>Gets or sets when the account was made.</summary>
    [DmColumn("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// A built-in account's place in a tenant: a member's, or a platform user's grant (which expires, and which the audit
/// names). Its roles are the account's roles there.
/// </summary>
[DmTable(null, "dm_memberships")]
public sealed class DapperMaticMembership
{
    /// <summary>Gets or sets the membership's id.</summary>
    [DmColumn("id", length: 64, isPrimaryKey: true)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets or sets the account's id.</summary>
    [DmColumn("user_id", length: 64, isIndexed: true)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>Gets or sets the tenant.</summary>
    [DmColumn("tenant_id", length: 128, isIndexed: true)]
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Gets or sets the roles there, as a JSON array.</summary>
    [DmColumn("roles", length: 4096)]
    public string Roles { get; set; } = "[]";

    /// <summary>Gets or sets a value indicating whether this is a grant: a platform user's, for a while.</summary>
    [DmColumn("is_grant")]
    public bool IsGrant { get; set; }

    /// <summary>Gets or sets who made it.</summary>
    [DmColumn("granted_by", length: 256, isNullable: true)]
    public string? GrantedBy { get; set; }

    /// <summary>Gets or sets why (a grant's reason).</summary>
    [DmColumn("reason", length: 1024, isNullable: true)]
    public string? Reason { get; set; }

    /// <summary>Gets or sets when it ends, or null for never (a grant always ends).</summary>
    [DmColumn("expires_at", isNullable: true)]
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>Gets or sets when it was made.</summary>
    [DmColumn("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Tells whether the membership holds now.
    /// </summary>
    /// <param name="now">The time.</param>
    /// <returns>True unless it has ended.</returns>
    public bool IsLive(DateTimeOffset now) => ExpiresAt is null || ExpiresAt > now;
}

/// <summary>
/// An invitation to an email: to a tenant, with roles there, or to the platform, with platform roles. Accepted once,
/// before it expires; its token is kept only as a hash.
/// </summary>
[DmTable(null, "dm_invitations")]
public sealed class DapperMaticInvitation
{
    /// <summary>Gets or sets the invitation's id.</summary>
    [DmColumn("id", length: 64, isPrimaryKey: true)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets or sets the email invited.</summary>
    [DmColumn("email", length: 256)]
    public string Email { get; set; } = string.Empty;

    /// <summary>Gets or sets the email, normalized.</summary>
    [DmColumn("normalized_email", length: 256, isIndexed: true)]
    public string NormalizedEmail { get; set; } = string.Empty;

    /// <summary>Gets or sets the tenant, or null for the platform.</summary>
    [DmColumn("tenant_id", length: 128, isNullable: true)]
    public string? TenantId { get; set; }

    /// <summary>Gets or sets the roles given, as a JSON array.</summary>
    [DmColumn("roles", length: 4096)]
    public string Roles { get; set; } = "[]";

    /// <summary>Gets or sets who invited.</summary>
    [DmColumn("invited_by", length: 256, isNullable: true)]
    public string? InvitedBy { get; set; }

    /// <summary>Gets or sets the SHA-256 of the invitation's token.</summary>
    [DmColumn("token_hash", length: 64, isUnique: true)]
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Gets or sets when it stops working.</summary>
    [DmColumn("expires_at")]
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Gets or sets when it was made.</summary>
    [DmColumn("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets when it was accepted, or null.</summary>
    [DmColumn("accepted_at", isNullable: true)]
    public DateTimeOffset? AcceptedAt { get; set; }
}

/// <summary>
/// A sign-in: what a login started, which its refreshes keep going; ended by signing out, or the account turned off.
/// </summary>
[DmTable(null, "dm_user_sessions")]
public sealed class DapperMaticUserSession
{
    /// <summary>Gets or sets the session's id.</summary>
    [DmColumn("id", length: 64, isPrimaryKey: true)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets or sets the account's id.</summary>
    [DmColumn("user_id", length: 64, isIndexed: true)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>Gets or sets when it began.</summary>
    [DmColumn("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets when it was last refreshed.</summary>
    [DmColumn("refreshed_at", isNullable: true)]
    public DateTimeOffset? RefreshedAt { get; set; }

    /// <summary>Gets or sets when it was ended, or null.</summary>
    [DmColumn("revoked_at", isNullable: true)]
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>
    /// Gets or sets the account's security stamp the session holds to: set at sign-in, and moved on when the
    /// session's own caller changes their password or second factor, so that change ends every other sign-in only.
    /// </summary>
    [DmColumn("stamp", length: 256, isNullable: true)]
    public string? Stamp { get; set; }
}

#pragma warning disable SA1402 // The tables of the accounts, together.
#pragma warning disable SA1600 // The rows' members are the tables' columns.

/// <summary>An account's platform role.</summary>
[DmTable(null, "dm_user_roles")]
internal sealed class DapperMaticUserRole
{
    [DmColumn("user_id", length: 64, isPrimaryKey: true)]
    public string UserId { get; set; } = string.Empty;

    [DmColumn("role", length: 256, isPrimaryKey: true)]
    public string Role { get; set; } = string.Empty;
}

/// <summary>An account's claim.</summary>
[DmTable(null, "dm_user_claims")]
internal sealed class DapperMaticUserClaim
{
    [DmColumn("id", length: 64, isPrimaryKey: true)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [DmColumn("user_id", length: 64, isIndexed: true)]
    public string UserId { get; set; } = string.Empty;

    [DmColumn("claim_type", length: 256)]
    public string ClaimType { get; set; } = string.Empty;

    [DmColumn("claim_value", length: 1024)]
    public string ClaimValue { get; set; } = string.Empty;
}

/// <summary>A token Identity keeps for an account.</summary>
[DmTable(null, "dm_user_tokens")]
internal sealed class DapperMaticUserToken
{
    [DmColumn("user_id", length: 64, isPrimaryKey: true)]
    public string UserId { get; set; } = string.Empty;

    [DmColumn("login_provider", length: 128, isPrimaryKey: true)]
    public string LoginProvider { get; set; } = string.Empty;

    [DmColumn("name", length: 128, isPrimaryKey: true)]
    public string Name { get; set; } = string.Empty;

    [DmColumn("value", length: 4096, isNullable: true)]
    public string? Value { get; set; }
}

/// <summary>A setting of the accounts: the setup code's hash.</summary>
[DmTable(null, "dm_account_settings")]
internal sealed class DapperMaticAccountSetting
{
    [DmColumn("name", length: 128, isPrimaryKey: true)]
    public string Name { get; set; } = string.Empty;

    [DmColumn("value", length: 4096)]
    public string Value { get; set; } = string.Empty;
}

/// <summary>A Data Protection key, as Data Protection writes it (encrypted).</summary>
[DmTable(null, "dm_data_protection_keys")]
internal sealed class DapperMaticDataProtectionKey
{
    [DmColumn("id", length: 128, isPrimaryKey: true)]
    public string Id { get; set; } = string.Empty;

    [DmColumn("xml", length: int.MaxValue)]
    public string Xml { get; set; } = string.Empty;
}

#pragma warning restore SA1600
#pragma warning restore SA1402
