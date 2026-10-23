// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models;

namespace MJCZone.DapperMatic.AspNetCore.Repositories;

/// <summary>
/// Keeps API keys: their secrets only as hashes. Keys are looked up one at a time, by id, as requests sign in, so they
/// are kept apart from the policies and tenants (which every instance reads whole): in memory, in a file, or in a
/// database table.
/// </summary>
public interface IDapperMaticKeyRepository
{
    /// <summary>
    /// Reads a key.
    /// </summary>
    /// <param name="id">The key's id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The key, or null if none has that id.</returns>
    Task<DapperMaticApiKey?> GetAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists keys, revoked ones among them.
    /// </summary>
    /// <param name="filter">Which keys.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The keys, newest first.</returns>
    Task<IReadOnlyList<DapperMaticApiKey>> ListAsync(DapperMaticKeyFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a key, or replaces the one of the same id.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task SaveAsync(DapperMaticApiKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a key: it no longer signs in; it is still listed.
    /// </summary>
    /// <param name="id">The key's id.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the key was live.</returns>
    Task<bool> RevokeAsync(string id, DateTimeOffset at, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records when keys were last used.
    /// </summary>
    /// <param name="used">When each key was last used, by id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task TouchAsync(IReadOnlyDictionary<string, DateTimeOffset> used, CancellationToken cancellationToken = default);
}
