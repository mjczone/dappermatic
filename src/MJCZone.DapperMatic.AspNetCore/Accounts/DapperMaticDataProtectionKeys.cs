// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.AspNetCore.Accounts;

/// <summary>
/// The application's Data Protection keys, kept in the accounts' database (encrypted, see
/// <see cref="DapperMaticKeyEncryptor"/>), so that every instance signs and reads the same tokens, cookies and tickets.
/// Data Protection reads them synchronously, rarely.
/// </summary>
internal sealed class DapperMaticDataProtectionKeys(DapperMaticAccountDb db) : IXmlRepository
{
    private int _created;

    /// <inheritdoc />
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var connection = Open();
        var rows = db.Scope(connection).SelectAsync<DapperMaticDataProtectionKey>(k => true).GetAwaiter().GetResult();
        return [.. rows.Select(r => XElement.Parse(r.Xml))];
    }

    /// <inheritdoc />
    public void StoreElement(XElement element, string friendlyName)
    {
        ArgumentNullException.ThrowIfNull(element);
        using var connection = Open();
        var row = new DapperMaticDataProtectionKey { Id = string.IsNullOrWhiteSpace(friendlyName) ? Guid.NewGuid().ToString("N") : friendlyName, Xml = element.ToString(SaveOptions.DisableFormatting) };
        db.Scope(connection).UpsertAsync(row).GetAwaiter().GetResult();
    }

    // The table may be read before the accounts' tables are made (Data Protection loads its keys as it starts).
    private System.Data.IDbConnection Open()
    {
        var connection = db.Open();
        if (Volatile.Read(ref _created) == 0)
        {
            var table = DmTableFactory.GetTable(typeof(DapperMaticDataProtectionKey));
            table = db.Schema is { Length: > 0 } schema ? Migrations.DmMigrator.CopyToSchema(table, schema) : table;
            connection.CreateTableIfNotExistsAsync(table).GetAwaiter().GetResult();
            Volatile.Write(ref _created, 1);
        }

        return connection;
    }
}

/// <summary>
/// Encrypts Data Protection's keys with the connection string encryption key before they are kept: whoever reads the
/// table cannot sign tokens with them.
/// </summary>
/// <param name="key">The encryption key.</param>
internal sealed class DapperMaticKeyEncryptor(string key) : IXmlEncryptor
{
    /// <inheritdoc />
    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        ArgumentNullException.ThrowIfNull(plaintextElement);
        var encrypted = new XElement("dapperMaticEncryptedKey", CryptoUtils.EncryptToBase64(plaintextElement.ToString(SaveOptions.DisableFormatting), key));
        return new EncryptedXmlInfo(encrypted, typeof(DapperMaticKeyDecryptor));
    }
}

/// <summary>
/// Decrypts Data Protection's keys: with the connection string encryption key, or a previous one.
/// </summary>
internal sealed class DapperMaticKeyDecryptor : IXmlDecryptor
{
    private readonly DapperMaticOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticKeyDecryptor"/> class.
    /// </summary>
    /// <param name="services">The application's services.</param>
    public DapperMaticKeyDecryptor(IServiceProvider services)
    {
        _options = services.GetRequiredService<IOptions<DapperMaticOptions>>().Value;
    }

    /// <inheritdoc />
    public XElement Decrypt(XElement encryptedElement)
    {
        ArgumentNullException.ThrowIfNull(encryptedElement);
        foreach (var key in new[] { _options.ConnectionStringEncryptionKey }.Concat(_options.PreviousConnectionStringEncryptionKeys).Where(k => !string.IsNullOrWhiteSpace(k)))
        {
            try
            {
                return XElement.Parse(CryptoUtils.DecryptFromBase64(encryptedElement.Value, key!));
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                // Another key's.
            }
        }

        throw new System.Security.Cryptography.CryptographicException("No connection string encryption key reads DapperMatic's Data Protection keys.");
    }
}
