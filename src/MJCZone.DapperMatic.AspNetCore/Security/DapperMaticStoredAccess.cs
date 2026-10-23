// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Repositories;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Security;

/// <summary>
/// The policies and tenants kept by DapperMatic, as last read from <see cref="IDapperMaticAccessRepository"/>: read
/// again when <see cref="DapperMaticOptions.AccessRefreshInterval"/> has passed, so that every instance of the
/// application sees another's changes, and at once after this instance's own.
/// </summary>
public sealed class DapperMaticStoredAccess
{
    private readonly IDapperMaticAccessRepository _repository;
    private readonly TimeSpan _interval;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string[] _keys;
    private Snapshot _snapshot = new([], new Dictionary<string, DapperMaticTenantSettings>(), [], [], DateTimeOffset.MinValue, string.Empty);
    private long _changes;

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticStoredAccess"/> class.
    /// </summary>
    /// <param name="repository">Where the policies and tenants are kept.</param>
    /// <param name="options">The options, with the refresh interval.</param>
    /// <param name="logger">The logger, told of a kept policy that no longer works.</param>
    public DapperMaticStoredAccess(
        IDapperMaticAccessRepository repository,
        IOptions<DapperMaticOptions> options,
        ILogger<DapperMaticStoredAccess>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(options);
        _repository = repository;
        _interval = options.Value.AccessRefreshInterval;
        _keys = [.. new[] { options.Value.ConnectionStringEncryptionKey }.Concat(options.Value.PreviousConnectionStringEncryptionKeys).OfType<string>().Where(k => k.Length > 0)];
        _logger = (ILogger?)logger ?? NullLogger.Instance;
    }

    /// <summary>Gets the policies kept, as last read; each one validated.</summary>
    public IReadOnlyList<DmPolicy> Policies => Volatile.Read(ref _snapshot).Policies;

    /// <summary>Gets the tenants kept, by id, as last read.</summary>
    public IReadOnlyDictionary<string, DapperMaticTenantSettings> Tenants => Volatile.Read(ref _snapshot).Tenants;

    /// <summary>Gets the webhooks kept, as last read, their secrets readable.</summary>
    public IReadOnlyList<DapperMaticWebhook> Webhooks => Volatile.Read(ref _snapshot).Webhooks;

    /// <summary>Gets the service accounts kept, as last read.</summary>
    public IReadOnlyList<DapperMaticServiceAccount> ServiceAccounts => Volatile.Read(ref _snapshot).ServiceAccounts;

    /// <summary>Gets where the policies and tenants are kept.</summary>
    internal IDapperMaticAccessRepository Repository => _repository;

    /// <summary>Gets a version of the policies and tenants as last read: it changes when they do.</summary>
    internal string Version => Volatile.Read(ref _snapshot).Version;

    /// <summary>
    /// Reads the policies and tenants again if they were last read longer ago than the refresh interval.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async ValueTask RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (DateTimeOffset.UtcNow - Volatile.Read(ref _snapshot).ReadAt < _interval)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (DateTimeOffset.UtcNow - Volatile.Read(ref _snapshot).ReadAt < _interval)
            {
                return;
            }

            // A change made while this read runs may be missing from it: the snapshot is then kept as stale, and read
            // again by the next request.
            var changes = Interlocked.Read(ref _changes);
            var document = await _repository.GetAsync(cancellationToken).ConfigureAwait(false);
            var policies = new List<DmPolicy>();
            foreach (var policy in document.Policies)
            {
                try
                {
                    DmPolicies.Validate(policy);
                    policy.Source = "stored";
                    policies.Add(policy);
                }
                catch (ArgumentException ex)
                {
                    // A policy kept before a change to what is valid: left out, never half applied.
                    _logger.LogWarning("DapperMatic left out the kept policy '{Policy}': {Reason}", policy.Name, ex.Message);
                }
            }

            var webhooks = new List<DapperMaticWebhook>();
            foreach (var webhook in document.Webhooks)
            {
                try
                {
                    webhook.Source = "stored";
                    webhook.Secret = Reveal(webhook.Secret);
                    webhooks.Add(webhook);
                }
                catch (System.Security.Cryptography.CryptographicException)
                {
                    _logger.LogWarning("DapperMatic left out the kept webhook '{Webhook}': its secret was kept with a key it no longer has.", webhook.Name);
                }
            }

            var readAt = changes == Interlocked.Read(ref _changes) ? DateTimeOffset.UtcNow : DateTimeOffset.MinValue;
            // What callers may use changes only when the policies or tenants do: a version of them, kept with the snapshot.
            var version = Convert.ToHexStringLower(
                System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { document.Policies, document.Tenants }))
            );
            Volatile.Write(ref _snapshot, new Snapshot(policies, document.Tenants, webhooks, document.ServiceAccounts, readAt, version));
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Forgets what was read, so that the next request reads the policies and tenants again.
    /// </summary>
    public void Invalidate()
    {
        Interlocked.Increment(ref _changes);
        Volatile.Write(ref _snapshot, Volatile.Read(ref _snapshot) with { ReadAt = DateTimeOffset.MinValue });
    }

    /// <summary>
    /// Hides a webhook's secret for keeping: encrypted with the connection string encryption key, when there is one.
    /// </summary>
    /// <param name="secret">The secret, or null.</param>
    /// <returns>What is kept.</returns>
    internal string? Hide(string? secret) =>
        secret is null || _keys.Length == 0 ? secret : "enc:" + CryptoUtils.EncryptToBase64(secret, _keys[0]);

    // A kept secret, readable: one encrypted with the current key or a previous one.
    private string? Reveal(string? kept)
    {
        if (kept is null || !kept.StartsWith("enc:", StringComparison.Ordinal))
        {
            return kept;
        }

        foreach (var key in _keys)
        {
            try
            {
                return CryptoUtils.DecryptFromBase64(kept[4..], key);
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                // Another key's.
            }
        }

        throw new System.Security.Cryptography.CryptographicException("No key reads the secret.");
    }

#pragma warning disable SA1313 // Positional record parameters are properties.
    private sealed record Snapshot(
        IReadOnlyList<DmPolicy> Policies,
        IReadOnlyDictionary<string, DapperMaticTenantSettings> Tenants,
        IReadOnlyList<DapperMaticWebhook> Webhooks,
        IReadOnlyList<DapperMaticServiceAccount> ServiceAccounts,
        DateTimeOffset ReadAt,
        string Version
    );
#pragma warning restore SA1313
}
