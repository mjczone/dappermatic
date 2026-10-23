// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Services;

namespace MJCZone.DapperMatic.AspNetCore.Caching;

/// <summary>
/// The callers' models kept (what each may use of a datasource), by what decides them: callers alike in every way the
/// policies read share one, built once rather than for every request for an OpenAPI document or GraphQL schema.
/// </summary>
public sealed class DapperMaticCallerModelCache : IDisposable
{
    private readonly MemoryCache? _models;

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticCallerModelCache"/> class.
    /// </summary>
    /// <param name="options">The options, with how many models to keep.</param>
    internal DapperMaticCallerModelCache(IOptions<DapperMaticOptions> options)
    {
        var size = options.Value.CallerModelCacheSize;
        _models = size > 0 ? new MemoryCache(new MemoryCacheOptions { SizeLimit = size }) : null;
    }

    /// <summary>Gets how many models are kept.</summary>
    public int Count => _models?.Count ?? 0;

    /// <summary>Forgets every model kept: the next request for each caller builds theirs again.</summary>
    public void Clear() => _models?.Clear();

    /// <inheritdoc />
    public void Dispose() => _models?.Dispose();

    /// <summary>Gets a model kept under a key.</summary>
    /// <param name="key">The key.</param>
    /// <param name="model">The model, if kept.</param>
    /// <returns>True if it was.</returns>
    internal bool TryGet(string key, out DapperMaticCallerModel? model)
    {
        model = null;
        return _models?.TryGetValue(key, out model) == true;
    }

    /// <summary>Keeps a model under a key, for as long as the descriptions it was built from are kept.</summary>
    /// <param name="key">The key.</param>
    /// <param name="model">The model.</param>
    /// <param name="duration">How long to keep it.</param>
    internal void Set(string key, DapperMaticCallerModel model, TimeSpan duration) =>
        _models?.Set(key, model, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = duration });
}
