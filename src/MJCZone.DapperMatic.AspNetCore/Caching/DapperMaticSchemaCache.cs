// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using MJCZone.DapperMatic.Dml.Rest;

namespace MJCZone.DapperMatic.AspNetCore.Caching;

/// <summary>
/// The descriptions of tables and views that query and row requests read from each datasource's catalog, kept for
/// <see cref="DapperMaticOptions.SchemaCacheDuration"/>. DapperMatic's own schema endpoints forget a datasource's
/// descriptions when they change it; a change made elsewhere is seen when the duration runs out, or at once after
/// <see cref="Invalidate"/>.
/// </summary>
public sealed class DapperMaticSchemaCache
{
    private readonly ConcurrentDictionary<(string Datasource, string Schema, string Name, bool View), (RestTable Table, DateTimeOffset Expires)> _entries = new();
    private readonly ConcurrentDictionary<string, long> _generations = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _duration;
    private readonly TimeProvider _clock;
    private long _cleared;

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticSchemaCache"/> class.
    /// </summary>
    /// <param name="duration">How long a description is kept; zero keeps none.</param>
    /// <param name="clock">The clock, or null for the system's.</param>
    public DapperMaticSchemaCache(TimeSpan duration, TimeProvider? clock = null)
    {
        _duration = duration;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>Gets how long descriptions are kept.</summary>
    internal TimeSpan Duration => _duration;

    /// <summary>
    /// Forgets every description read from a datasource.
    /// </summary>
    /// <param name="datasourceId">The datasource.</param>
    public void Invalidate(string datasourceId)
    {
        _generations.AddOrUpdate(datasourceId, 1, (_, generation) => generation + 1);
        foreach (var key in _entries.Keys.Where(k => string.Equals(k.Datasource, datasourceId, StringComparison.OrdinalIgnoreCase)))
        {
            _entries.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Forgets every description.
    /// </summary>
    public void Clear()
    {
        Interlocked.Increment(ref _cleared);
        _entries.Clear();
    }

    /// <summary>
    /// Gets the generation of a datasource's descriptions: it changes each time they are forgotten, so that what was
    /// built from them is known to be stale.
    /// </summary>
    /// <param name="datasourceId">The datasource.</param>
    /// <returns>The generation.</returns>
    internal string GenerationOf(string datasourceId) => $"{Interlocked.Read(ref _cleared)}.{_generations.GetValueOrDefault(datasourceId)}";

    /// <summary>
    /// Reads a table's or a view's description, if one is kept and current.
    /// </summary>
    /// <param name="datasourceId">The datasource, or null for none, which is never cached.</param>
    /// <param name="schemaName">The schema, or null.</param>
    /// <param name="name">The table's name, or the view's qualified name.</param>
    /// <param name="view">Whether it is a view.</param>
    /// <returns>The description, or null.</returns>
    internal RestTable? Get(string? datasourceId, string? schemaName, string name, bool view)
    {
        if (datasourceId is null || _duration <= TimeSpan.Zero)
        {
            return null;
        }

        var key = Key(datasourceId, schemaName, name, view);
        if (_entries.TryGetValue(key, out var entry))
        {
            if (entry.Expires > _clock.GetUtcNow())
            {
                return entry.Table;
            }

            _entries.TryRemove(key, out _);
        }

        return null;
    }

    /// <summary>
    /// Keeps a table's or a view's description.
    /// </summary>
    /// <param name="datasourceId">The datasource, or null for none, which is never cached.</param>
    /// <param name="schemaName">The schema, or null.</param>
    /// <param name="name">The table's name, or the view's qualified name.</param>
    /// <param name="view">Whether it is a view.</param>
    /// <param name="table">The description.</param>
    internal void Set(string? datasourceId, string? schemaName, string name, bool view, RestTable table)
    {
        if (datasourceId is not null && _duration > TimeSpan.Zero)
        {
            _entries[Key(datasourceId, schemaName, name, view)] = (table, _clock.GetUtcNow() + _duration);
        }
    }

    private static (string, string, string, bool) Key(string datasourceId, string? schemaName, string name, bool view) =>
        (datasourceId.ToLowerInvariant(), schemaName?.ToLowerInvariant() ?? string.Empty, name.ToLowerInvariant(), view);
}
