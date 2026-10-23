// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Changes;
using MJCZone.DapperMatic.AspNetCore.Services;

namespace MJCZone.DapperMatic.GraphQL.Schema;

/// <summary>
/// The schemas built, one for each shape of caller model (its fingerprint) in a datasource and schema: callers who may
/// use the same share one, built on first use.
/// </summary>
internal sealed class GraphQLSchemaCache : IDisposable
{
    private readonly MemoryCache _schemas;
    private readonly DapperMaticGraphQLOptions _options;
    private readonly DapperMaticChangeLog? _changes;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphQLSchemaCache"/> class.
    /// </summary>
    /// <param name="options">The GraphQL options.</param>
    /// <param name="changes">Which tables callers may subscribe to, or null for none.</param>
    /// <param name="logger">Where renamed names are told, or null.</param>
    public GraphQLSchemaCache(IOptions<DapperMaticGraphQLOptions> options, DapperMaticChangeLog? changes = null, ILogger<GraphQLSchemaCache>? logger = null)
    {
        _options = options.Value;
        _changes = changes;
        _logger = (ILogger?)logger ?? NullLogger.Instance;
        _schemas = new MemoryCache(new MemoryCacheOptions { SizeLimit = Math.Max(1, _options.SchemaCacheSize) });
    }

    /// <summary>
    /// Gets the schema of a caller model, built if no caller of its shape has used one yet.
    /// </summary>
    /// <param name="model">The caller model.</param>
    /// <returns>The schema, initialized.</returns>
    public GraphQLModel Get(DapperMaticCallerModel model)
    {
        var key = $"{model.DatasourceId}\u001f{model.ResolvedSchema}\u001f{model.Fingerprint}";
        if (_schemas.TryGetValue(key, out GraphQLModel? built) && built is not null)
        {
            return built;
        }

        built = GraphQLSchemaBuilder.Build(model, _options, t => _changes?.RealtimeTable(model.DatasourceId, t.SchemaName ?? model.ResolvedSchema, t.Name) is not null);
        built.Schema.Initialize();
        foreach (var (wanted, given) in built.Renamed)
        {
            _logger.LogWarning("DapperMatic's GraphQL schema for {Datasource} names {Wanted} {Given}: the name was taken.", model.DatasourceId, wanted, given);
        }

        _schemas.Set(key, built, new MemoryCacheEntryOptions { Size = 1, SlidingExpiration = TimeSpan.FromHours(1) });
        return built;
    }

    /// <inheritdoc />
    public void Dispose() => _schemas.Dispose();
}
