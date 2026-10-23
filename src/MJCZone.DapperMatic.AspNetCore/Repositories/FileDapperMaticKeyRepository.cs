// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Text.Json;
using MJCZone.DapperMatic.AspNetCore.Models;

namespace MJCZone.DapperMatic.AspNetCore.Repositories;

/// <summary>
/// Keeps API keys in a JSON file (their secrets only as hashes): for a single instance that keeps its access in a file.
/// </summary>
public class FileDapperMaticKeyRepository : IDapperMaticKeyRepository
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="FileDapperMaticKeyRepository"/> class.
    /// </summary>
    /// <param name="path">The file; created when first written.</param>
    public FileDapperMaticKeyRepository(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
    }

    /// <inheritdoc />
    public async Task<DapperMaticApiKey?> GetAsync(string id, CancellationToken cancellationToken = default) =>
        (await ReadLockedAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault(k => string.Equals(k.Id, id, StringComparison.Ordinal));

    /// <inheritdoc />
    public async Task<IReadOnlyList<DapperMaticApiKey>> ListAsync(DapperMaticKeyFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return DapperMaticKeyJson.Filter(await ReadLockedAsync(cancellationToken).ConfigureAwait(false), filter);
    }

    /// <inheritdoc />
    public Task SaveAsync(DapperMaticApiKey key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Id);
        return ChangeAsync(
            keys =>
            {
                keys.RemoveAll(k => string.Equals(k.Id, key.Id, StringComparison.Ordinal));
                keys.Add(key);
                return true;
            },
            cancellationToken
        );
    }

    /// <inheritdoc />
    public Task<bool> RevokeAsync(string id, DateTimeOffset at, CancellationToken cancellationToken = default) =>
        ChangeAsync(
            keys =>
            {
                if (keys.FirstOrDefault(k => string.Equals(k.Id, id, StringComparison.Ordinal)) is not { RevokedAt: null } key)
                {
                    return false;
                }

                key.RevokedAt = at;
                return true;
            },
            cancellationToken
        );

    /// <inheritdoc />
    public Task TouchAsync(IReadOnlyDictionary<string, DateTimeOffset> used, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(used);
        return ChangeAsync(
            keys =>
            {
                var changed = false;
                foreach (var key in keys.Where(k => used.ContainsKey(k.Id)))
                {
                    key.LastUsedAt = used[key.Id];
                    changed = true;
                }

                return changed;
            },
            cancellationToken
        );
    }

    private async Task<List<DapperMaticApiKey>> ReadLockedAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<bool> ChangeAsync(Func<List<DapperMaticApiKey>, bool> edit, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var keys = await ReadAsync(cancellationToken).ConfigureAwait(false);
            var changed = edit(keys);
            if (changed)
            {
                // Written beside the file, then moved over it: a reader sees the old file or the new, never half.
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                var temporary = _path + ".tmp";
                await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(keys, DapperMaticKeyJson.Json), cancellationToken).ConfigureAwait(false);
                File.Move(temporary, _path, overwrite: true);
            }

            return changed;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<List<DapperMaticApiKey>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        await using var stream = File.OpenRead(_path);
        return await JsonSerializer.DeserializeAsync<List<DapperMaticApiKey>>(stream, DapperMaticKeyJson.Json, cancellationToken).ConfigureAwait(false) ?? [];
    }
}
