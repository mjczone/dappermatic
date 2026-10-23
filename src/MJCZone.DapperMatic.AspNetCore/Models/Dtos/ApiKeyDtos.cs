// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Models.Dtos;

/// <summary>
/// Asks for a new API key: its name, when it expires, and the most it may do (never more than its owner).
/// </summary>
public sealed class ApiKeyRequest
{
    /// <summary>Gets or sets the key's name: what it is for.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets when it stops working, or null for never (unless a maximum lifetime is set).</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>Gets or sets a value indicating whether the key only reads: rows and definitions.</summary>
    public bool ReadOnly { get; set; }

    /// <summary>Gets the only datasources the key reaches, or none for any.</summary>
    public List<string> Datasources { get; init; } = [];

    /// <summary>Gets the only schemas the key reaches, or none for any.</summary>
    public List<string> Schemas { get; init; } = [];

    /// <summary>Gets the only tables and views the key reaches, or none for any.</summary>
    public List<string> Tables { get; init; } = [];

    /// <summary>
    /// Gets more limits, as policies of no roles: what the key may do at most, with filters, checks and columns. The
    /// shortcuts above are one more such policy.
    /// </summary>
    public List<DmPolicy> Limits { get; init; } = [];
}

/// <summary>
/// An API key as it is shown: everything but its secret.
/// </summary>
/// <param name="Id">The key's id.</param>
/// <param name="Name">Its name.</param>
/// <param name="Kind">Whom it signs in as.</param>
/// <param name="Owner">Its service account's name, or its owner's user id.</param>
/// <param name="Tenant">Its tenant, or null.</param>
/// <param name="Limits">The most it may do.</param>
/// <param name="ExpiresAt">When it stops working, or null.</param>
/// <param name="CreatedAt">When it was made.</param>
/// <param name="CreatedBy">Who made it.</param>
/// <param name="LastUsedAt">When it was last used (to within a minute), or null.</param>
/// <param name="RevokedAt">When it was revoked, or null.</param>
#pragma warning disable SA1313 // Positional record parameters are properties.
public sealed record ApiKeyInfo(
    string Id,
    string Name,
    DapperMaticKeyKind Kind,
    string Owner,
    string? Tenant,
    IReadOnlyList<DmPolicy> Limits,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt
)
{
    /// <summary>
    /// Shows a key, without its secret.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>What is shown of it.</returns>
    public static ApiKeyInfo Of(DapperMaticApiKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return new(key.Id, key.Name, key.Kind, key.Owner, key.Tenant, key.Limits, key.ExpiresAt, key.CreatedAt, key.CreatedBy, key.LastUsedAt, key.RevokedAt);
    }
}

/// <summary>
/// A key just made: the key itself, shown this once, and what is kept of it.
/// </summary>
/// <param name="Key">The key: keep it now, it is never shown again.</param>
/// <param name="Info">The key as it is listed.</param>
public sealed record ApiKeyCreated(string Key, ApiKeyInfo Info);
#pragma warning restore SA1313
