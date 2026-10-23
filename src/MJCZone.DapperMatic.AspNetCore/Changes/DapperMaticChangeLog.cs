// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.DataAnnotations;
using MJCZone.DapperMatic.Dml;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.AspNetCore.Changes;

/// <summary>
/// The changes made through DapperMatic to tables webhooks or subscriptions hear, noted in each datasource's
/// <c>dm_changes</c> table in the writes' own transactions; and where each webhook's messages have got to, in
/// <c>dm_webhook_state</c>.
/// </summary>
public sealed class DapperMaticChangeLog
{
    // Keys per change noted: a write of more rows is noted as several changes.
    private const int KeysPerChange = 500;

    private readonly DapperMaticOptions _options;
    private readonly DapperMaticStoredAccess? _stored;
    private readonly ConcurrentDictionary<string, bool> _made = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticChangeLog"/> class.
    /// </summary>
    /// <param name="options">The options: the webhooks, the tables subscribed to, the tenancy.</param>
    /// <param name="stored">The webhooks and tenants kept through the API, or null for none.</param>
    public DapperMaticChangeLog(IOptions<DapperMaticOptions> options, DapperMaticStoredAccess? stored = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _stored = stored;
    }

    /// <summary>Gets the change table's name.</summary>
    internal string ChangesTable => "dm_changes";

    /// <summary>Gets the webhook state table's name.</summary>
    internal string StateTable => "dm_webhook_state";

    /// <summary>Gets the schema the tables are in, or null for the default.</summary>
    internal string? Schema => _options.ChangeTablesSchema;

    /// <summary>
    /// Gets every webhook: those of code and configuration, then those kept through the API (<c>Source</c> "stored").
    /// </summary>
    internal IReadOnlyList<DapperMaticWebhook> Webhooks => [.. _options.Webhooks, .. _stored?.Webhooks ?? []];

    /// <summary>
    /// Tells whether anything hears a datasource's changes: a webhook, or a table subscriptions may read.
    /// </summary>
    /// <param name="datasourceId">The datasource.</param>
    /// <returns>True if something may.</returns>
    internal bool HearsAny(string datasourceId) =>
        Webhooks.Any(w => w.Enabled && Same(w.DatasourceId ?? datasourceId, datasourceId))
        || _options.RealtimeTables.Any(t => Same(t.DatasourceId ?? datasourceId, datasourceId));

    /// <summary>
    /// Tells whether a table's changes are heard: by a webhook, of any tenant, or by subscriptions.
    /// </summary>
    /// <param name="datasourceId">The datasource.</param>
    /// <param name="schemaName">The table's schema, as resolved, or null where there are none.</param>
    /// <param name="tableName">The table.</param>
    /// <returns>True if they are.</returns>
    internal bool Hears(string datasourceId, string? schemaName, string tableName) =>
        Webhooks.Any(w => w.Enabled && Covers(w, datasourceId, schemaName, tableName))
        || RealtimeTable(datasourceId, schemaName, tableName) is not null
        || _options.RealtimeTables.Any(t => (t.DatasourceId is null || Same(t.DatasourceId, datasourceId)) && t.Reads.Any(r => Names(r, schemaName, tableName)));

    /// <summary>
    /// Finds the entry that lets callers subscribe to a table or view.
    /// </summary>
    /// <param name="datasourceId">The datasource.</param>
    /// <param name="schemaName">The schema, as resolved.</param>
    /// <param name="tableName">The table or view.</param>
    /// <returns>The entry, or null.</returns>
    internal DapperMaticRealtimeTable? RealtimeTable(string datasourceId, string? schemaName, string tableName) =>
        _options.RealtimeTables.FirstOrDefault(t =>
            (t.DatasourceId is null || Same(t.DatasourceId, datasourceId))
            && (t.SchemaName is null || Same(t.SchemaName, schemaName))
            && Same(t.TableName, tableName)
        );

    /// <summary>
    /// Tells whether a webhook hears a table's changes.
    /// </summary>
    /// <param name="webhook">The webhook.</param>
    /// <param name="datasourceId">The datasource.</param>
    /// <param name="schemaName">The schema, as resolved.</param>
    /// <param name="tableName">The table.</param>
    /// <returns>True if it does.</returns>
    internal static bool Covers(DapperMaticWebhook webhook, string datasourceId, string? schemaName, string tableName) =>
        (webhook.DatasourceId is null || Same(webhook.DatasourceId, datasourceId))
        && (webhook.Schemas.Count == 0 || (schemaName is not null && webhook.Schemas.Any(p => Matches(p, schemaName))))
        && (webhook.Tables.Count == 0 || webhook.Tables.Any(p => Matches(p, tableName)));

    /// <summary>
    /// Makes the change tables in a datasource, once a process: before any write's transaction, since MySQL commits
    /// one on DDL.
    /// </summary>
    /// <param name="datasourceId">The datasource.</param>
    /// <param name="connection">A connection to it, in no transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    internal async Task EnsureAsync(string datasourceId, IDbConnection connection, CancellationToken cancellationToken)
    {
        if (_made.ContainsKey(datasourceId))
        {
            return;
        }

        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        if (Schema is { } schema)
        {
            await connection.CreateSchemaIfNotExistsAsync(schema, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        await connection
            .CreateTableIfNotExistsAsync(
                new DmTable(
                    Schema,
                    ChangesTable,
                    [
                        new DmColumn("id", typeof(long), isPrimaryKey: true, isAutoIncrement: true),
                        new DmColumn("created_at", typeof(DateTime)),
                        new DmColumn("schema_name", typeof(string), length: 128, isNullable: true),
                        new DmColumn("table_name", typeof(string), length: 128),
                        new DmColumn("action", typeof(string), length: 8),
                        new DmColumn("tenant", typeof(string), length: 128, isNullable: true),
                        new DmColumn("row_keys", typeof(string), length: int.MaxValue, isNullable: true),
                        new DmColumn("row_count", typeof(int), isNullable: true),
                    ],
                    indexes: [new DmIndex($"ix_{ChangesTable}_created_at", [new DmOrderedColumn("created_at")])]
                ),
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
        await connection
            .CreateTableIfNotExistsAsync(
                new DmTable(
                    Schema,
                    StateTable,
                    [
                        new DmColumn("webhook", typeof(string), length: 300, isPrimaryKey: true),
                        new DmColumn("change_cursor", typeof(long)),
                        new DmColumn("attempts", typeof(int)),
                        new DmColumn("next_attempt_at", typeof(DateTime), isNullable: true),
                        new DmColumn("last_error", typeof(string), length: 2000, isNullable: true),
                        new DmColumn("lease_until", typeof(DateTime), isNullable: true),
                        new DmColumn("lease_owner", typeof(string), length: 64, isNullable: true),
                        new DmColumn("delivered", typeof(long)),
                        new DmColumn("dropped", typeof(long)),
                        new DmColumn("updated_at", typeof(DateTime)),
                    ]
                ),
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
        _made[datasourceId] = true;
    }

    /// <summary>
    /// Makes what hears a datasource's changes for writes, or null when nothing does.
    /// </summary>
    /// <param name="datasourceId">The datasource.</param>
    /// <param name="provider">Its database, which names the default schema.</param>
    /// <returns>The sink, or null.</returns>
    internal IDmChangeSink? SinkFor(string datasourceId, DbProviderType provider) =>
        HearsAny(datasourceId) ? new Sink(this, datasourceId, provider) : null;

    /// <summary>
    /// Reads the changes noted after one, in order.
    /// </summary>
    /// <param name="connection">A connection to the datasource.</param>
    /// <param name="after">The last change already read, or 0.</param>
    /// <param name="limit">The most to read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The changes.</returns>
    internal async Task<List<DapperMaticChange>> ReadAsync(IDbConnection connection, long after, int limit, CancellationToken cancellationToken) =>
        await Scope(connection).From<DapperMaticChange>().Where(c => c.Id > after).OrderBy(c => c.Id).Take(limit).ToListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Reads the last change noted.
    /// </summary>
    /// <param name="connection">A connection to the datasource.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Its id, or 0.</returns>
    internal async Task<long> LastAsync(IDbConnection connection, CancellationToken cancellationToken) =>
        await Scope(connection).From<DapperMaticChange>().MaxAsync(c => (long?)c.Id, cancellationToken: cancellationToken).ConfigureAwait(false) ?? 0;

    /// <summary>
    /// Forgets the changes noted before a time.
    /// </summary>
    /// <param name="connection">A connection to the datasource.</param>
    /// <param name="before">The time, UTC.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    internal Task PruneAsync(IDbConnection connection, DateTime before, CancellationToken cancellationToken) =>
        Scope(connection).DeleteAsync<DapperMaticChange>(c => c.CreatedAt < before, cancellationToken: cancellationToken);

    /// <summary>
    /// Makes the scope the change tables are read and written through.
    /// </summary>
    /// <param name="connection">A connection to the datasource.</param>
    /// <returns>The scope.</returns>
    internal DmScope Scope(IDbConnection connection)
    {
        var scope = new DmScope(connection).WithTableName<DapperMaticChange>(ChangesTable).WithTableName<DapperMaticWebhookState>(StateTable);
        return Schema is null ? scope : scope.InSchema(Schema);
    }

    // The tenant whose schema or datasource a table is in: a row with no tenant column is the place's.
    private string? TenantOfPlace(string datasourceId, string? schemaName)
    {
        var tenants = _options.Tenancy.Tenants.Concat(_stored?.Tenants ?? new Dictionary<string, DapperMaticTenantSettings>()).ToList();
        return tenants.FirstOrDefault(t => t.Value.Schema is { } s && Same(s, schemaName) && (t.Value.Datasource is null || Same(t.Value.Datasource, datasourceId))).Key
            ?? tenants.FirstOrDefault(t => t.Value.Schema is null && t.Value.Datasource is { } d && Same(d, datasourceId)).Key;
    }

    private string? TenantColumn(string tableName) => _options.Tenancy.RowsColumns.GetValueOrDefault(tableName) ?? _options.Tenancy.RowsColumn;

    private static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    // "table" or "schema.table" naming a table.
    private static bool Names(string read, string? schemaName, string tableName)
    {
        var dot = read.LastIndexOf('.');
        return Same(read[(dot + 1)..], tableName) && (dot < 0 || Same(read[..dot], schemaName));
    }

    private static bool Matches(string pattern, string value) =>
        Regex.IsMatch(
            value,
            "^" + Regex.Escape(pattern).Replace(@"\*", ".*", StringComparison.Ordinal).Replace(@"\?", ".", StringComparison.Ordinal) + "$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1)
        );

    private static string? Text(object? value) => value is null ? null : Convert.ToString(value, CultureInfo.InvariantCulture);

    // What a datasource's writes tell: noted in its change table, in the write's transaction.
    private sealed class Sink(DapperMaticChangeLog log, string datasourceId, DbProviderType provider) : IDmChangeSink
    {
        public IReadOnlyList<string>? Wants(string? schemaName, string tableName)
        {
            var schema = SchemaOf(schemaName);
            if (!log.Hears(datasourceId, schema, tableName))
            {
                return null;
            }

            return log.TenantColumn(tableName) is { } column ? [column] : [];
        }

        public async Task WrittenAsync(IDbConnection connection, IDbTransaction transaction, DmChange change, CancellationToken cancellationToken)
        {
            var schema = SchemaOf(change.SchemaName);
            var column = log.TenantColumn(change.TableName);
            var place = log.TenantOfPlace(datasourceId, schema);

            // A change of rows of several tenants is noted once per tenant, so that each tenant's webhooks hear theirs.
            var groups = change.Rows.Count == 0
                ? [(Tenant: place, Rows: change.Rows.ToList())]
                : change.Rows
                    .GroupBy(r => column is not null && r.TryGetValue(column, out var value) ? Text(value) : place)
                    .Select(g => (Tenant: g.Key, Rows: g.ToList()))
                    .ToList();
            var noted = new List<DapperMaticChange>();
            foreach (var (tenant, rows) in groups)
            {
                for (var start = 0; start == 0 || start < rows.Count; start += KeysPerChange)
                {
                    var chunk = rows.Skip(start).Take(KeysPerChange).ToList();
                    noted.Add(
                        new DapperMaticChange
                        {
                            CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                            SchemaName = schema,
                            TableName = change.TableName,
                            Action = change.Action.ToString().ToLowerInvariant(),
                            Tenant = tenant,
                            RowKeys = change.Keys.Count == 0 || chunk.Count == 0 ? null : Keys(change.Keys, chunk),
                            RowCount = change.Rows.Count == 0 ? change.Count : chunk.Count,
                        }
                    );
                }
            }

            await log.Scope(connection).InsertManyAsync(noted, transaction, cancellationToken).ConfigureAwait(false);
        }

        // One key column: its values; several: an object of them per row.
        private static string Keys(IReadOnlyList<string> keys, List<IReadOnlyDictionary<string, object?>> rows) =>
            keys.Count == 1
                ? JsonSerializer.Serialize(rows.Select(r => r.GetValueOrDefault(keys[0])))
                : JsonSerializer.Serialize(rows.Select(r => keys.ToDictionary(k => k, k => r.GetValueOrDefault(k))));

        private string? SchemaOf(string? schemaName) =>
            schemaName is { Length: > 0 } and not "_" ? schemaName : DapperMaticProviders.DefaultSchemaName(provider);
    }
}

#pragma warning disable SA1402, SA1600 // The rows of DapperMatic's change tables, whose members are their columns.

/// <summary>A change noted in a datasource's <c>dm_changes</c>.</summary>
[DmTable(null, "dm_changes")]
internal sealed class DapperMaticChange
{
    [DmColumn("id", isPrimaryKey: true, isAutoIncrement: true)]
    public long Id { get; set; }

    [DmColumn("created_at")]
    public DateTime CreatedAt { get; set; }

    [DmColumn("schema_name", length: 128, isNullable: true)]
    public string? SchemaName { get; set; }

    [DmColumn("table_name", length: 128)]
    public string TableName { get; set; } = string.Empty;

    [DmColumn("action", length: 8)]
    public string Action { get; set; } = string.Empty;

    [DmColumn("tenant", length: 128, isNullable: true)]
    public string? Tenant { get; set; }

    [DmColumn("row_keys", length: int.MaxValue, isNullable: true)]
    public string? RowKeys { get; set; }

    [DmColumn("row_count", isNullable: true)]
    public int? RowCount { get; set; }
}

/// <summary>Where a webhook's messages have got to in a datasource, in <c>dm_webhook_state</c>.</summary>
[DmTable(null, "dm_webhook_state")]
internal sealed class DapperMaticWebhookState
{
    [DmColumn("webhook", length: 300, isPrimaryKey: true)]
    public string Webhook { get; set; } = string.Empty;

    [DmColumn("change_cursor")]
    public long ChangeCursor { get; set; }

    [DmColumn("attempts")]
    public int Attempts { get; set; }

    [DmColumn("next_attempt_at", isNullable: true)]
    public DateTime? NextAttemptAt { get; set; }

    [DmColumn("last_error", length: 2000, isNullable: true)]
    public string? LastError { get; set; }

    [DmColumn("lease_until", isNullable: true)]
    public DateTime? LeaseUntil { get; set; }

    [DmColumn("lease_owner", length: 64, isNullable: true)]
    public string? LeaseOwner { get; set; }

    [DmColumn("delivered")]
    public long Delivered { get; set; }

    [DmColumn("dropped")]
    public long Dropped { get; set; }

    [DmColumn("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
