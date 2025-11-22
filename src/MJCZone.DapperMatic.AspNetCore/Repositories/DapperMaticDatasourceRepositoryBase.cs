// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Security;

namespace MJCZone.DapperMatic.AspNetCore.Repositories;

/// <summary>
/// Base class for implementations of IDapperMaticDatasourceRepository.
/// </summary>
public abstract class DapperMaticDatasourceRepositoryBase : IDapperMaticDatasourceRepository
{
    private readonly string? _encryptionKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticDatasourceRepositoryBase"/> class.
    /// </summary>
    /// <param name="options">The DapperMatic options.</param>
    protected DapperMaticDatasourceRepositoryBase(IOptions<DapperMaticOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _encryptionKey = options.Value.ConnectionStringEncryptionKey;
    }

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
    /// Encrypts a connection string for secure storage.
    /// </summary>
    /// <param name="connectionString">The plain text connection string.</param>
    /// <returns>The encrypted connection string.</returns>
    public virtual string EncryptConnectionString(string connectionString)
    {
        return CryptoUtils.EncryptToBase64(
            connectionString,
            _encryptionKey ?? throw new InvalidOperationException("Encryption key is not configured.")
        );
    }

    /// <summary>
    /// /// Decrypts an encrypted connection string for internal use.
    /// </summary>
    /// <param name="encryptedConnectionString">The encrypted connection string.</param>
    /// <returns>The decrypted plain text connection string.</returns>
    public virtual string DecryptConnectionString(string encryptedConnectionString)
    {
        return CryptoUtils.DecryptFromBase64(
            encryptedConnectionString,
            _encryptionKey ?? throw new InvalidOperationException("Encryption key is not configured.")
        );
    }
}
