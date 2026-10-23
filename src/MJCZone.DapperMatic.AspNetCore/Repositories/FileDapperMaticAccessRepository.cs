// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Text.Json;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Repositories;

/// <summary>
/// Keeps policies and tenants in a JSON file, written whole and atomically on each change. Several processes may read
/// the file; one should write it.
/// </summary>
public class FileDapperMaticAccessRepository : IDapperMaticAccessRepository
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="FileDapperMaticAccessRepository"/> class.
    /// </summary>
    /// <param name="path">The file; created when first written.</param>
    public FileDapperMaticAccessRepository(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
    }

    /// <summary>Gets the file, beside which DapperMatic keeps its other files.</summary>
    internal string FilePath => _path;

    /// <inheritdoc />
    public async Task<DapperMaticAccessDocument> GetAsync(CancellationToken cancellationToken = default)
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

    /// <inheritdoc />
    public Task SavePolicyAsync(DmPolicy policy, CancellationToken cancellationToken = default) =>
        ChangeAsync(d => DapperMaticAccessEdits.SavePolicy(d, policy), cancellationToken);

    /// <inheritdoc />
    public Task<bool> DeletePolicyAsync(string name, string? tenant = null, CancellationToken cancellationToken = default) =>
        ChangeAsync(d => DapperMaticAccessEdits.DeletePolicy(d, name, tenant), cancellationToken);

    /// <inheritdoc />
    public Task SaveWebhookAsync(DapperMaticWebhook webhook, CancellationToken cancellationToken = default) =>
        ChangeAsync(d => DapperMaticAccessEdits.SaveWebhook(d, webhook), cancellationToken);

    /// <inheritdoc />
    public Task<bool> DeleteWebhookAsync(string name, string? tenant = null, CancellationToken cancellationToken = default) =>
        ChangeAsync(d => DapperMaticAccessEdits.DeleteWebhook(d, name, tenant), cancellationToken);

    /// <inheritdoc />
    public Task SaveTenantAsync(string id, DapperMaticTenantSettings tenant, CancellationToken cancellationToken = default) =>
        ChangeAsync(d => DapperMaticAccessEdits.SaveTenant(d, id, tenant), cancellationToken);

    /// <inheritdoc />
    public Task<bool> DeleteTenantAsync(string id, CancellationToken cancellationToken = default) =>
        ChangeAsync(d => d.Tenants.Remove(id), cancellationToken);

    /// <inheritdoc />
    public Task SaveServiceAccountAsync(DapperMaticServiceAccount account, CancellationToken cancellationToken = default) =>
        ChangeAsync(d => DapperMaticAccessEdits.SaveServiceAccount(d, account), cancellationToken);

    /// <inheritdoc />
    public Task<bool> DeleteServiceAccountAsync(string name, string? tenant = null, CancellationToken cancellationToken = default) =>
        ChangeAsync(d => DapperMaticAccessEdits.DeleteServiceAccount(d, name, tenant), cancellationToken);

    /// <inheritdoc />
    public Task ReplaceAsync(DapperMaticAccessDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return ChangeAsync(
            d =>
            {
                d.Policies.Clear();
                d.Policies.AddRange(document.Copy().Policies);
                d.Tenants.Clear();
                foreach (var (id, tenant) in document.Copy().Tenants)
                {
                    d.Tenants[id] = tenant;
                }

                d.ServiceAccounts.Clear();
                d.ServiceAccounts.AddRange(document.Copy().ServiceAccounts);

                return true;
            },
            cancellationToken
        );
    }

    private async Task<bool> ChangeAsync(Func<DapperMaticAccessDocument, bool> edit, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await ReadAsync(cancellationToken).ConfigureAwait(false);
            var changed = edit(document);
            if (changed)
            {
                // Written beside the file, then moved over it: a reader sees the old file or the new, never half.
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                var temporary = _path + ".tmp";
                await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(document, DapperMaticAccessDocument.Json), cancellationToken)
                    .ConfigureAwait(false);
                File.Move(temporary, _path, overwrite: true);
            }

            return changed;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<DapperMaticAccessDocument> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return new DapperMaticAccessDocument();
        }

        await using var stream = File.OpenRead(_path);
        return await JsonSerializer.DeserializeAsync<DapperMaticAccessDocument>(stream, DapperMaticAccessDocument.Json, cancellationToken).ConfigureAwait(false)
            ?? new DapperMaticAccessDocument();
    }
}
