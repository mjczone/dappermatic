// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Text.Json.Serialization;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Models;

/// <summary>
/// An account for a program, not a person: an integration, a scheduled job, an AI assistant. It has its own name,
/// roles, claims and tenant, and its keys sign in as it; policies treat it as any caller with those roles. It keeps
/// working whoever comes and goes. Kept with the policies and tenants, or given in code or configuration.
/// </summary>
public sealed class DapperMaticServiceAccount
{
    /// <summary>Gets or sets the account's name, unique within its tenant.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the tenant the account acts in, or null for the whole application.</summary>
    public string? Tenant { get; set; }

    /// <summary>Gets the account's roles.</summary>
    public List<string> Roles { get; init; } = [];

    /// <summary>Gets the account's claims, by type, which policies read as <c>@claims.type</c>.</summary>
    public Dictionary<string, List<string>> Claims { get; init; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets what the account is for.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets a value indicating whether the account is turned off: its keys do not sign in.</summary>
    public bool Disabled { get; set; }

    /// <summary>Gets or sets when the account was made.</summary>
    public DateTimeOffset? CreatedAt { get; set; }

    /// <summary>Gets or sets who made it.</summary>
    public string? CreatedBy { get; set; }
}

/// <summary>Whom a key signs in as.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DapperMaticKeyKind
{
    /// <summary>A service account.</summary>
    Service,

    /// <summary>A person, as they are each time the key is used: never more than its owner.</summary>
    Personal,
}

/// <summary>
/// An API key, as DapperMatic keeps it: its secret only as a hash (the key itself is shown once, when made).
/// </summary>
public sealed class DapperMaticApiKey
{
    /// <summary>Gets or sets the key's id: the public part of the key, which lists, logs and audits name it by.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the SHA-256 of the key's secret, as hexadecimal.</summary>
    public string SecretHash { get; set; } = string.Empty;

    /// <summary>Gets or sets the key's name, which says what it is for.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets whom the key signs in as.</summary>
    public DapperMaticKeyKind Kind { get; set; }

    /// <summary>Gets or sets the key's owner: a service account's name, or a person's user id.</summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>Gets or sets the tenant the key acts in, or null for none.</summary>
    public string? Tenant { get; set; }

    /// <summary>
    /// Gets the most the key may do: policies (of no roles) that, together with its owner's, decide what its caller may
    /// do. Empty for as much as its owner.
    /// </summary>
    public List<DmPolicy> Limits { get; init; } = [];

    /// <summary>Gets or sets when the key stops working, or null for never.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>Gets or sets when the key was made.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets who made it.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>Gets or sets when it was last used (to within a minute), or null for never.</summary>
    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>Gets or sets when it was revoked, or null.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>
    /// Tells whether the key signs in now.
    /// </summary>
    /// <param name="now">The time.</param>
    /// <returns>True unless revoked or expired.</returns>
    public bool IsLive(DateTimeOffset now) => RevokedAt is null && (ExpiresAt is null || ExpiresAt > now);
}

/// <summary>Which keys a listing is of.</summary>
public sealed class DapperMaticKeyFilter
{
    /// <summary>Gets or sets the kind, or null for both.</summary>
    public DapperMaticKeyKind? Kind { get; set; }

    /// <summary>Gets or sets the owner, or null for any.</summary>
    public string? Owner { get; set; }

    /// <summary>Gets or sets the tenant, or null for any (an empty string for none).</summary>
    public string? Tenant { get; set; }
}
