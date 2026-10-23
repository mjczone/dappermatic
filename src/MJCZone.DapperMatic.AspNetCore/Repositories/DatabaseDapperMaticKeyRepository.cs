// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Text.Json;
using MJCZone.DapperMatic.AspNetCore.Factories;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.DataAnnotations;
using MJCZone.DapperMatic.Dml;
using MJCZone.DapperMatic.Models;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Repositories;

/// <summary>
/// Keeps API keys in a database table, one row each (their secrets only as hashes): every instance of the application
/// reads the same ones. The table is created when first used.
/// </summary>
public class DatabaseDapperMaticKeyRepository : IDapperMaticKeyRepository
{
    private readonly string _provider;
    private readonly string _connectionString;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly string? _schemaName;
    private readonly string _tableName;
    private int _created;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseDapperMaticKeyRepository"/> class.
    /// </summary>
    /// <param name="provider">The database provider.</param>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="connectionFactory">The connection factory.</param>
    /// <param name="schemaName">The schema of the table, or null for the default one.</param>
    /// <param name="tableName">The table's name.</param>
    public DatabaseDapperMaticKeyRepository(
        string provider,
        string connectionString,
        IDbConnectionFactory connectionFactory,
        string? schemaName = null,
        string tableName = "dm_api_keys"
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        _provider = provider;
        _connectionString = connectionString;
        _connectionFactory = connectionFactory;
        _schemaName = schemaName;
        _tableName = tableName;
    }

    /// <inheritdoc />
    public async Task<DapperMaticApiKey?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        using var db = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var row = await Scope(db).SingleByIdAsync<KeyRow>(id, cancellationToken: cancellationToken).ConfigureAwait(false);
        return row is null ? null : Key(row);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DapperMaticApiKey>> ListAsync(DapperMaticKeyFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        using var db = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var query = Scope(db).From<KeyRow>();
        if (filter.Kind is { } kind)
        {
            var text = kind.ToString();
            query = query.Where(r => r.Kind == text);
        }

        if (filter.Owner is { } owner)
        {
            query = query.Where(r => r.Owner == owner);
        }

        if (filter.Tenant is { } tenant)
        {
            query = query.Where(r => r.Tenant == tenant);
        }

        var rows = await query.OrderByDescending(r => r.CreatedAt).ToListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        return [.. rows.Select(Key)];
    }

    /// <inheritdoc />
    public async Task SaveAsync(DapperMaticApiKey key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Id);
        using var db = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await Scope(db).UpsertAsync(Row(key), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> RevokeAsync(string id, DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        using var db = await OpenAsync(cancellationToken).ConfigureAwait(false);
        return await Scope(db)
            .UpdateAsync<KeyRow>(r => new KeyRow { RevokedAt = at }, r => r.Id == id && r.RevokedAt == null, cancellationToken: cancellationToken)
            .ConfigureAwait(false) > 0;
    }

    /// <inheritdoc />
    public async Task TouchAsync(IReadOnlyDictionary<string, DateTimeOffset> used, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(used);
        if (used.Count == 0)
        {
            return;
        }

        using var db = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var scope = Scope(db);
        foreach (var (id, at) in used)
        {
            DateTimeOffset? when = at;
            await scope.UpdateAsync<KeyRow>(r => new KeyRow { LastUsedAt = when }, r => r.Id == id, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    private static KeyRow Row(DapperMaticApiKey key) =>
        new()
        {
            Id = key.Id,
            SecretHash = key.SecretHash,
            Name = key.Name,
            Kind = key.Kind.ToString(),
            Owner = key.Owner,
            Tenant = key.Tenant ?? string.Empty,
            Limits = JsonSerializer.Serialize(key.Limits, DapperMaticAccessDocument.Json),
            ExpiresAt = key.ExpiresAt,
            CreatedAt = key.CreatedAt,
            CreatedBy = key.CreatedBy,
            LastUsedAt = key.LastUsedAt,
            RevokedAt = key.RevokedAt,
        };

    private static DapperMaticApiKey Key(KeyRow row) =>
        new()
        {
            Id = row.Id,
            SecretHash = row.SecretHash,
            Name = row.Name,
            Kind = Enum.Parse<DapperMaticKeyKind>(row.Kind),
            Owner = row.Owner,
            Tenant = row.Tenant.Length == 0 ? null : row.Tenant,
            Limits = JsonSerializer.Deserialize<List<DmPolicy>>(row.Limits, DapperMaticAccessDocument.Json) ?? [],
            ExpiresAt = row.ExpiresAt,
            CreatedAt = row.CreatedAt,
            CreatedBy = row.CreatedBy,
            LastUsedAt = row.LastUsedAt,
            RevokedAt = row.RevokedAt,
        };

    private DmScope Scope(IDbConnection db)
    {
        var scope = new DmScope(db).WithTableName<KeyRow>(_tableName);
        return _schemaName is null ? scope : scope.InSchema(_schemaName);
    }

    private async Task<IDbConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var db = _connectionFactory.CreateConnection(_provider, _connectionString);
        db.Open();
        if (Volatile.Read(ref _created) == 0)
        {
            await db.CreateTableIfNotExistsAsync(
                    new DmTable(
                        _schemaName,
                        _tableName,
                        [
                            new DmColumn("id", typeof(string), length: 32),
                            new DmColumn("secret_hash", typeof(string), length: 64),
                            new DmColumn("name", typeof(string), length: 256),
                            new DmColumn("kind", typeof(string), length: 16),
                            new DmColumn("owner", typeof(string), length: 256),
                            new DmColumn("tenant", typeof(string), length: 128),
                            new DmColumn("limits", typeof(string), length: int.MaxValue),
                            new DmColumn("expires_at", typeof(DateTimeOffset), isNullable: true),
                            new DmColumn("created_at", typeof(DateTimeOffset)),
                            new DmColumn("created_by", typeof(string), length: 256, isNullable: true),
                            new DmColumn("last_used_at", typeof(DateTimeOffset), isNullable: true),
                            new DmColumn("revoked_at", typeof(DateTimeOffset), isNullable: true),
                        ],
                        primaryKey: new DmPrimaryKeyConstraint($"pk_{_tableName}", [new DmOrderedColumn("id")]),
                        indexes: [new DmIndex($"ix_{_tableName}_owner", [new DmOrderedColumn("owner")])]
                    ),
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);
            Volatile.Write(ref _created, 1);
        }

        return db;
    }

#pragma warning disable SA1600 // The row's members are the table's columns.
    [DmTable(null, "dm_api_keys")]
    private sealed class KeyRow
    {
        [DmColumn("id", length: 32, isPrimaryKey: true)]
        public string Id { get; set; } = string.Empty;

        [DmColumn("secret_hash", length: 64)]
        public string SecretHash { get; set; } = string.Empty;

        [DmColumn("name", length: 256)]
        public string Name { get; set; } = string.Empty;

        [DmColumn("kind", length: 16)]
        public string Kind { get; set; } = string.Empty;

        [DmColumn("owner", length: 256)]
        public string Owner { get; set; } = string.Empty;

        [DmColumn("tenant", length: 128)]
        public string Tenant { get; set; } = string.Empty;

        [DmColumn("limits", length: int.MaxValue)]
        public string Limits { get; set; } = "[]";

        [DmColumn("expires_at", isNullable: true)]
        public DateTimeOffset? ExpiresAt { get; set; }

        [DmColumn("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [DmColumn("created_by", length: 256, isNullable: true)]
        public string? CreatedBy { get; set; }

        [DmColumn("last_used_at", isNullable: true)]
        public DateTimeOffset? LastUsedAt { get; set; }

        [DmColumn("revoked_at", isNullable: true)]
        public DateTimeOffset? RevokedAt { get; set; }
    }
#pragma warning restore SA1600
}
