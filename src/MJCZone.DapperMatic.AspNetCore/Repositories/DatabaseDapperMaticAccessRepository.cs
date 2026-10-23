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
/// Keeps policies and tenants in a database table, one row each, as JSON: every instance of the application reads the
/// same ones. The table is created when first used.
/// </summary>
public class DatabaseDapperMaticAccessRepository : IDapperMaticAccessRepository
{
    private const string PolicyKind = "policy";
    private const string TenantKind = "tenant";
    private const string WebhookKind = "webhook";
    private const string ServiceAccountKind = "serviceaccount";

    private readonly string _provider;
    private readonly string _connectionString;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly string? _schemaName;
    private readonly string _tableName;
    private int _created;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseDapperMaticAccessRepository"/> class.
    /// </summary>
    /// <param name="provider">The database provider.</param>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="connectionFactory">The connection factory.</param>
    /// <param name="schemaName">The schema of the table, or null for the default one.</param>
    /// <param name="tableName">The table's name.</param>
    public DatabaseDapperMaticAccessRepository(
        string provider,
        string connectionString,
        IDbConnectionFactory connectionFactory,
        string? schemaName = null,
        string tableName = "dm_access"
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

    /// <summary>Gets the database provider, for DapperMatic's other tables kept beside this one.</summary>
    internal string Provider => _provider;

    /// <summary>Gets the connection string.</summary>
    internal string ConnectionString => _connectionString;

    /// <summary>Gets the schema of the table, or null for the default one.</summary>
    internal string? SchemaName => _schemaName;

    /// <inheritdoc />
    public async Task<DapperMaticAccessDocument> GetAsync(CancellationToken cancellationToken = default)
    {
        using var db = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await Scope(db).SelectAsync<AccessRow>(cancellationToken: cancellationToken).ConfigureAwait(false);
        var document = new DapperMaticAccessDocument();
        foreach (var row in rows.OrderBy(r => r.Name, StringComparer.Ordinal))
        {
            if (row.Kind == PolicyKind)
            {
                document.Policies.Add(JsonSerializer.Deserialize<DmPolicy>(row.Body, DapperMaticAccessDocument.Json)!);
            }
            else if (row.Kind == TenantKind)
            {
                document.Tenants[row.Name] = JsonSerializer.Deserialize<DapperMaticTenantSettings>(row.Body, DapperMaticAccessDocument.Json)!;
            }
            else if (row.Kind == WebhookKind)
            {
                document.Webhooks.Add(JsonSerializer.Deserialize<DapperMaticWebhook>(row.Body, DapperMaticAccessDocument.Json)!);
            }
            else if (row.Kind == ServiceAccountKind)
            {
                document.ServiceAccounts.Add(JsonSerializer.Deserialize<DapperMaticServiceAccount>(row.Body, DapperMaticAccessDocument.Json)!);
            }
        }

        return document;
    }

    /// <inheritdoc />
    public async Task SavePolicyAsync(DmPolicy policy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentException.ThrowIfNullOrWhiteSpace(policy.Name);
        using var db = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await Scope(db).UpsertAsync(Row(PolicyKind, policy.Tenant, policy.Name, policy), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<bool> DeletePolicyAsync(string name, string? tenant = null, CancellationToken cancellationToken = default) =>
        DeleteAsync(PolicyKind, tenant, name, cancellationToken);

    /// <inheritdoc />
    public async Task SaveTenantAsync(string id, DapperMaticTenantSettings tenant, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(tenant);
        using var db = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await Scope(db).UpsertAsync(Row(TenantKind, null, id, tenant), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<bool> DeleteTenantAsync(string id, CancellationToken cancellationToken = default) => DeleteAsync(TenantKind, null, id, cancellationToken);

    /// <inheritdoc />
    public async Task SaveWebhookAsync(DapperMaticWebhook webhook, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(webhook);
        ArgumentException.ThrowIfNullOrWhiteSpace(webhook.Name);
        using var db = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await Scope(db).UpsertAsync(Row(WebhookKind, webhook.Tenant, webhook.Name, webhook), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<bool> DeleteWebhookAsync(string name, string? tenant = null, CancellationToken cancellationToken = default) =>
        DeleteAsync(WebhookKind, tenant, name, cancellationToken);

    /// <inheritdoc />
    public async Task SaveServiceAccountAsync(DapperMaticServiceAccount account, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentException.ThrowIfNullOrWhiteSpace(account.Name);
        using var db = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await Scope(db).UpsertAsync(Row(ServiceAccountKind, account.Tenant, account.Name, account), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<bool> DeleteServiceAccountAsync(string name, string? tenant = null, CancellationToken cancellationToken = default) =>
        DeleteAsync(ServiceAccountKind, tenant, name, cancellationToken);

    /// <inheritdoc />
    public async Task ReplaceAsync(DapperMaticAccessDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        using var db = await OpenAsync(cancellationToken).ConfigureAwait(false);
        using var tx = db.BeginTransaction();
        var scope = Scope(db);
        await scope.DeleteAllAsync<AccessRow>(tx, cancellationToken).ConfigureAwait(false);
        var rows = document.Policies.Select(p => Row(PolicyKind, p.Tenant, p.Name ?? throw new ArgumentException("A policy kept by DapperMatic has a name."), p))
            .Concat(document.Tenants.Select(t => Row(TenantKind, null, t.Key, t.Value)))
            .Concat(document.Webhooks.Select(w => Row(WebhookKind, w.Tenant, w.Name, w)))
            .Concat(document.ServiceAccounts.Select(a => Row(ServiceAccountKind, a.Tenant, a.Name, a)))
            .ToList();
        if (rows.Count > 0)
        {
            await scope.InsertManyAsync(rows, tx, cancellationToken).ConfigureAwait(false);
        }

        tx.Commit();
    }

    // A tenant's policies have names of their own: the tenant is part of the key, empty for the whole application's.
    private static AccessRow Row(string kind, string? tenant, string name, object value) =>
        new()
        {
            Kind = kind,
            Tenant = tenant ?? string.Empty,
            Name = name,
            Body = JsonSerializer.Serialize(value, value.GetType(), DapperMaticAccessDocument.Json),
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    private async Task<bool> DeleteAsync(string kind, string? tenant, string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var key = tenant ?? string.Empty;
        using var db = await OpenAsync(cancellationToken).ConfigureAwait(false);
        return await Scope(db)
            .DeleteAsync<AccessRow>(r => r.Kind == kind && r.Tenant == key && r.Name == name, cancellationToken: cancellationToken)
            .ConfigureAwait(false) > 0;
    }

    private DmScope Scope(IDbConnection db)
    {
        var scope = new DmScope(db).WithTableName<AccessRow>(_tableName);
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
                            new DmColumn("kind", typeof(string), length: 16),
                            new DmColumn("tenant", typeof(string), length: 128),
                            new DmColumn("name", typeof(string), length: 256),
                            new DmColumn("body", typeof(string), length: int.MaxValue),
                            new DmColumn("updated_at", typeof(DateTimeOffset)),
                        ],
                        primaryKey: new DmPrimaryKeyConstraint(
                            $"pk_{_tableName}",
                            [new DmOrderedColumn("kind"), new DmOrderedColumn("tenant"), new DmOrderedColumn("name")]
                        )
                    ),
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);
            Volatile.Write(ref _created, 1);
        }

        return db;
    }

#pragma warning disable SA1600 // The row's members are the table's columns.
    [DmTable(null, "dm_access")]
    private sealed class AccessRow
    {
        [DmColumn("kind", length: 16, isPrimaryKey: true)]
        public string Kind { get; set; } = string.Empty;

        [DmColumn("tenant", length: 128, isPrimaryKey: true)]
        public string Tenant { get; set; } = string.Empty;

        [DmColumn("name", length: 256, isPrimaryKey: true)]
        public string Name { get; set; } = string.Empty;

        [DmColumn("body", length: int.MaxValue)]
        public string Body { get; set; } = string.Empty;

        [DmColumn("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; }
    }
#pragma warning restore SA1600
}
