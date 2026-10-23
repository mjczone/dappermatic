// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.Dml.Rest;

namespace MJCZone.DapperMatic.AspNetCore.Caching;

/// <summary>
/// The query results kept for the tables listed in <see cref="DapperMaticOptions.CachedTables"/>, in
/// <see cref="DapperMaticOptions.ResultCacheStore"/>, the application's <see cref="IDistributedCache"/>, or memory.
/// </summary>
/// <remarks>
/// <para>
/// Each statement's results are kept under a key made of its SQL, its parameters' values (the policies' values among
/// them), the session values the database's own row-level security reads, and the versions of the tables it reads.
/// So callers whose policies or tenants differ never share results, and a write changes a table's version: results
/// read before it are never found again, and expire.
/// </para>
/// <para>
/// The versions are kept in the store too, so that every instance of the application sharing a distributed store sees
/// a write made by any of them.
/// </para>
/// </remarks>
public sealed class DapperMaticResultCache
{
    private readonly IDistributedCache _store;
    private readonly IReadOnlyList<DapperMaticCachedTable> _tables;

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticResultCache"/> class.
    /// </summary>
    /// <param name="options">The options, which list the cached tables and may name the store.</param>
    /// <param name="services">The services, for the application's <see cref="IDistributedCache"/>.</param>
    public DapperMaticResultCache(IOptions<DapperMaticOptions> options, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(services);
        _tables = options.Value.CachedTables;
        _store =
            options.Value.ResultCacheStore
            ?? services.GetService<IDistributedCache>()
            ?? new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
    }

    /// <summary>
    /// Makes every result read from a datasource stale: for a change DapperMatic did not make, to its schema or to any
    /// table. DapperMatic's own schema endpoints do this when they change a datasource.
    /// </summary>
    /// <param name="datasourceId">The datasource.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public Task InvalidateAsync(string datasourceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(datasourceId);
        return BumpAsync(DatasourceKey(datasourceId), cancellationToken);
    }

    /// <summary>
    /// Makes the results that read a table stale: for a write DapperMatic did not make.
    /// </summary>
    /// <param name="datasourceId">The datasource.</param>
    /// <param name="tableName">The table.</param>
    /// <param name="schemaName">The table's schema, or null for the table in every schema.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public Task InvalidateAsync(string datasourceId, string tableName, string? schemaName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(datasourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        return BumpAsync(TableKey(datasourceId, schemaName, tableName), cancellationToken);
    }

    /// <summary>
    /// Makes the results that read tables stale, after writes to them were committed.
    /// </summary>
    /// <param name="datasourceId">The datasource.</param>
    /// <param name="provider">The datasource's database, which names the default schema.</param>
    /// <param name="tables">The tables, by schema (null for the default) and name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    internal async Task WrittenAsync(string datasourceId, DbProviderType provider, IEnumerable<(string? Schema, string Table)> tables, CancellationToken cancellationToken)
    {
        if (_tables.Count == 0)
        {
            return;
        }

        var keys = tables.Select(t => TableKey(datasourceId, SchemaOf(t.Schema, provider), t.Table)).Distinct(StringComparer.Ordinal).ToList();
        await Task.WhenAll(keys.Select(k => BumpAsync(k, cancellationToken))).ConfigureAwait(false);
    }

    /// <summary>
    /// Makes the cache a REST engine keeps a request's results in, or null when nothing of the datasource is cached.
    /// </summary>
    /// <param name="datasourceId">The datasource.</param>
    /// <param name="provider">The datasource's database, which names the default schema.</param>
    /// <param name="session">The session values the database's own row-level security reads, as text.</param>
    /// <returns>The cache, or null.</returns>
    internal RestCache? For(string datasourceId, DbProviderType provider, string session)
    {
        if (!_tables.Any(t => t.DatasourceId is null || Same(t.DatasourceId, datasourceId)))
        {
            return null;
        }

        return new RestCache
        {
            Duration = tables =>
            {
                TimeSpan? shortest = null;
                foreach (var table in tables)
                {
                    if (Find(datasourceId, SchemaOf(table.SchemaName, provider), table.Name) is not { } cached)
                    {
                        return null;
                    }

                    shortest = shortest is null || cached.Duration < shortest ? cached.Duration : shortest;
                }

                return shortest;
            },
            Versions = async (tables, cancellationToken) =>
            {
                var keys = new List<string> { DatasourceKey(datasourceId) };
                foreach (var table in tables)
                {
                    var schema = SchemaOf(table.SchemaName, provider);
                    keys.Add(TableKey(datasourceId, schema, table.Name));
                    keys.Add(TableKey(datasourceId, null, table.Name));
                    foreach (var read in Find(datasourceId, schema, table.Name)?.Reads ?? [])
                    {
                        var dot = read.LastIndexOf('.');
                        var name = read[(dot + 1)..];
                        keys.Add(TableKey(datasourceId, dot > 0 ? read[..dot] : schema, name));
                        keys.Add(TableKey(datasourceId, null, name));
                    }
                }

                var versions = await Task.WhenAll(keys.Distinct(StringComparer.Ordinal).Select(k => VersionAsync(k, cancellationToken))).ConfigureAwait(false);
                return string.Join('|', versions);
            },
            Get = (key, cancellationToken) => _store.GetAsync(key, cancellationToken),
            Set = (key, value, duration, cancellationToken) =>
                _store.SetAsync(key, value, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = duration }, cancellationToken),
            Scope = datasourceId.ToLowerInvariant() + "\u001f" + session,
        };
    }

    private static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static string? SchemaOf(string? schemaName, DbProviderType provider) =>
        schemaName is { Length: > 0 } and not "_" ? schemaName : DapperMaticProviders.DefaultSchemaName(provider);

    private static string DatasourceKey(string datasourceId) => "dm:v:" + datasourceId.ToLowerInvariant();

    // A table's version in one schema, or, with no schema, its version in every schema.
    private static string TableKey(string datasourceId, string? schemaName, string tableName) =>
        $"dm:v:{datasourceId.ToLowerInvariant()}\u001f{schemaName?.ToLowerInvariant()}\u001f{tableName.ToLowerInvariant()}";

    private DapperMaticCachedTable? Find(string datasourceId, string? schemaName, string tableName) =>
        _tables.FirstOrDefault(t =>
            (t.DatasourceId is null || Same(t.DatasourceId, datasourceId))
            && (t.SchemaName is null || Same(t.SchemaName, schemaName))
            && Same(t.TableName, tableName)
        );

    // A version is made when there is none, never assumed: one the store dropped after a write must not bring back the
    // results read before it.
    private async Task<string> VersionAsync(string key, CancellationToken cancellationToken)
    {
        if (await _store.GetAsync(key, cancellationToken).ConfigureAwait(false) is { Length: > 0 } version)
        {
            return Convert.ToHexStringLower(version);
        }

        var made = Guid.NewGuid().ToByteArray();
        await _store.SetAsync(key, made, new DistributedCacheEntryOptions(), cancellationToken).ConfigureAwait(false);
        return Convert.ToHexStringLower(made);
    }

    private Task BumpAsync(string key, CancellationToken cancellationToken) =>
        _store.SetAsync(key, Guid.NewGuid().ToByteArray(), new DistributedCacheEntryOptions(), cancellationToken);
}
