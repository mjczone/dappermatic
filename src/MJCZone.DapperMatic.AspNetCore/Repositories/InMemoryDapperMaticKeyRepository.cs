// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Text.Json;
using MJCZone.DapperMatic.AspNetCore.Models;

namespace MJCZone.DapperMatic.AspNetCore.Repositories;

/// <summary>
/// Keeps API keys in memory: lost when the application stops, and not shared by its instances. For tests and
/// development.
/// </summary>
public class InMemoryDapperMaticKeyRepository : IDapperMaticKeyRepository
{
    private readonly ConcurrentDictionary<string, string> _keys = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<DapperMaticApiKey?> GetAsync(string id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_keys.TryGetValue(id, out var json) ? DapperMaticKeyJson.Read(json) : null);

    /// <inheritdoc />
    public Task<IReadOnlyList<DapperMaticApiKey>> ListAsync(DapperMaticKeyFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return Task.FromResult<IReadOnlyList<DapperMaticApiKey>>(DapperMaticKeyJson.Filter(_keys.Values.Select(DapperMaticKeyJson.Read), filter));
    }

    /// <inheritdoc />
    public Task SaveAsync(DapperMaticApiKey key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Id);
        _keys[key.Id] = DapperMaticKeyJson.Write(key);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> RevokeAsync(string id, DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        while (_keys.TryGetValue(id, out var json))
        {
            var key = DapperMaticKeyJson.Read(json);
            if (key.RevokedAt is not null)
            {
                return Task.FromResult(false);
            }

            key.RevokedAt = at;
            if (_keys.TryUpdate(id, DapperMaticKeyJson.Write(key), json))
            {
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task TouchAsync(IReadOnlyDictionary<string, DateTimeOffset> used, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(used);
        foreach (var (id, at) in used)
        {
            if (_keys.TryGetValue(id, out var json))
            {
                var key = DapperMaticKeyJson.Read(json);
                key.LastUsedAt = at;
                _keys.TryUpdate(id, DapperMaticKeyJson.Write(key), json);
            }
        }

        return Task.CompletedTask;
    }
}

/// <summary>Keys as JSON, as the memory and file repositories keep them; and listing them.</summary>
internal static class DapperMaticKeyJson
{
    /// <summary>Gets the options keys are written with.</summary>
    public static JsonSerializerOptions Json => DapperMaticAccessDocument.Json;

    /// <summary>Writes a key.</summary>
    /// <param name="key">The key.</param>
    /// <returns>Its JSON.</returns>
    public static string Write(DapperMaticApiKey key) => JsonSerializer.Serialize(key, Json);

    /// <summary>Reads a key.</summary>
    /// <param name="json">Its JSON.</param>
    /// <returns>The key.</returns>
    public static DapperMaticApiKey Read(string json) => JsonSerializer.Deserialize<DapperMaticApiKey>(json, Json)!;

    /// <summary>The keys a filter takes, newest first.</summary>
    /// <param name="keys">The keys.</param>
    /// <param name="filter">The filter.</param>
    /// <returns>Those it takes.</returns>
    public static List<DapperMaticApiKey> Filter(IEnumerable<DapperMaticApiKey> keys, DapperMaticKeyFilter filter) =>
        [
            .. keys.Where(k =>
                    (filter.Kind is null || k.Kind == filter.Kind)
                    && (filter.Owner is null || string.Equals(k.Owner, filter.Owner, StringComparison.Ordinal))
                    && (filter.Tenant is null || string.Equals(k.Tenant ?? string.Empty, filter.Tenant, StringComparison.Ordinal))
                )
                .OrderByDescending(k => k.CreatedAt),
        ];
}
