// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Security;

namespace MJCZone.DapperMatic.AspNetCore.Repositories;

/// <summary>
/// Base class for datasource repositories: connection strings are encrypted with the configured key, each tagged with
/// the id of the key that encrypted it, so that keys can be rotated.
/// </summary>
public abstract class DapperMaticDatasourceRepositoryBase : IDapperMaticDatasourceRepository
{
    private readonly string? _encryptionKey;
    private readonly List<string> _previousKeys;
    private string? _processKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticDatasourceRepositoryBase"/> class.
    /// </summary>
    /// <param name="options">The DapperMatic options.</param>
    protected DapperMaticDatasourceRepositoryBase(IOptions<DapperMaticOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _encryptionKey = string.IsNullOrWhiteSpace(options.Value.ConnectionStringEncryptionKey) ? null : options.Value.ConnectionStringEncryptionKey;
        _previousKeys = [.. options.Value.PreviousConnectionStringEncryptionKeys.Where(k => !string.IsNullOrWhiteSpace(k))];
    }

    // Without a configured key, one made for this process: what it encrypts never outlives the process, which is
    // right for a repository in memory and nothing else (persistent repositories call RequireEncryptionKey).
    private string CurrentKey => _encryptionKey ?? LazyInitializer.EnsureInitialized(ref _processKey, CryptoUtils.GenerateEncryptionKey);

    /// <inheritdoc />
    public virtual void Initialize()
    {
        // Default implementation does nothing
    }

    /// <inheritdoc />
    public abstract Task<bool> AddDatasourceAsync(DatasourceDto datasource);

    /// <inheritdoc />
    public abstract Task<bool> DatasourceExistsAsync(string id);

    /// <inheritdoc />
    public abstract Task<string?> GetConnectionStringAsync(string id);

    /// <inheritdoc />
    public abstract Task<DatasourceDto?> GetDatasourceAsync(string id);

    /// <inheritdoc />
    public abstract Task<List<DatasourceDto>> GetDatasourcesAsync(string? tag = null);

    /// <inheritdoc />
    public abstract Task<bool> RemoveDatasourceAsync(string id);

    /// <inheritdoc />
    public abstract Task<bool> UpdateDatasourceAsync(DatasourceDto datasource);

    /// <summary>
    /// Encrypts a connection string for secure storage, with the current key, prefixed by that key's id.
    /// </summary>
    /// <param name="connectionString">The plain text connection string.</param>
    /// <returns>The encrypted connection string, as <c>keyId:base64</c>.</returns>
    public virtual string EncryptConnectionString(string connectionString) =>
        $"{KeyId(CurrentKey)}:{CryptoUtils.EncryptToBase64(connectionString, CurrentKey)}";

    /// <summary>
    /// Decrypts an encrypted connection string for internal use, with the key whose id it carries: the current key
    /// or one of the previous ones. One stored without a key id is tried with each.
    /// </summary>
    /// <param name="encryptedConnectionString">The encrypted connection string.</param>
    /// <returns>The decrypted plain text connection string.</returns>
    /// <exception cref="CryptographicException">No configured key decrypts it.</exception>
    public virtual string DecryptConnectionString(string encryptedConnectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedConnectionString);
        string[] keys = [CurrentKey, .. _previousKeys];
        var separator = encryptedConnectionString.IndexOf(':', StringComparison.Ordinal);
        if (separator > 0)
        {
            var id = encryptedConnectionString[..separator];
            var key = keys.FirstOrDefault(k => KeyId(k) == id)
                ?? throw new CryptographicException(
                    $"The connection string was encrypted with key {id}, which is neither the current key nor a previous one."
                );
            return CryptoUtils.DecryptFromBase64(encryptedConnectionString[(separator + 1)..], key);
        }

        foreach (var key in keys)
        {
            try
            {
                return CryptoUtils.DecryptFromBase64(encryptedConnectionString, key);
            }
            catch (CryptographicException) when (key != keys[^1])
            {
                // Stored before key ids: try the next key.
            }
        }

        throw new CryptographicException("No configured key decrypts the connection string.");
    }

    /// <summary>
    /// Refuses to go on without a configured key: a repository that outlives the process cannot use one of its own.
    /// </summary>
    /// <param name="repository">What needs it, for the message, such as "a file repository".</param>
    /// <exception cref="InvalidOperationException">No key is configured.</exception>
    protected void RequireEncryptionKey(string repository)
    {
        if (_encryptionKey is null)
        {
            throw new InvalidOperationException(
                $"Datasources kept in {repository} have their connection strings encrypted: set "
                    + "DapperMaticOptions.ConnectionStringEncryptionKey to a key from CryptoUtils.GenerateEncryptionKey()."
            );
        }
    }

    // A key's id: the start of its hash, which says nothing of the key itself.
    private static string KeyId(string key) =>
        "k" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..8];
}
