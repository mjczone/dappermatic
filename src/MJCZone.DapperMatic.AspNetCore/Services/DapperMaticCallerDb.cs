// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MJCZone.DapperMatic.AspNetCore.Caching;
using MJCZone.DapperMatic.AspNetCore.Changes;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.Dml;

namespace MJCZone.DapperMatic.AspNetCore.Services;

/// <summary>
/// Typed DML for the current request's caller: the request's tenant and values, and the policies of the web API, so
/// that application code reads and writes as the caller would through the API.
/// </summary>
/// <example>
/// <code>
/// app.MapGet("/my-invoices", async (DapperMaticCallerDb callerDb) =>
/// {
///     var db = await callerDb.OpenAsync("main");
///     return await db.SelectAsync&lt;Invoice&gt;(i => i.Status == "open");
/// });
/// </code>
/// </example>
public sealed class DapperMaticCallerDb
{
    private readonly IHttpContextAccessor _http;
    private readonly DapperMaticAccess _access;
    private readonly DapperMaticPolicySet _policies;
    private readonly IDapperMaticService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticCallerDb"/> class.
    /// </summary>
    /// <param name="http">The current request.</param>
    /// <param name="access">The request's caller, tenant and values.</param>
    /// <param name="policies">The policies in force.</param>
    /// <param name="service">The service, which opens datasources' connections with their session values.</param>
    public DapperMaticCallerDb(IHttpContextAccessor http, DapperMaticAccess access, DapperMaticPolicySet policies, IDapperMaticService service)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(service);
        _http = http;
        _access = access;
        _policies = policies;
        _service = service;
    }

    /// <summary>
    /// Opens a datasource for the current request's caller: a scope whose statements are held to the policies, in the
    /// tenant's schema when it has one, on a connection with the session values the database's own row-level security
    /// reads. The connection is closed when the request ends.
    /// </summary>
    /// <param name="datasourceId">The datasource.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The scope.</returns>
    /// <exception cref="InvalidOperationException">There is no current request.</exception>
    /// <exception cref="UnauthorizedAccessException">The datasource is not the request's tenant's.</exception>
    public async Task<DmScope> OpenAsync(string datasourceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(datasourceId);
        var http = _http.HttpContext ?? throw new InvalidOperationException("DapperMaticCallerDb opens datasources for a request, and there is none.");
        var caller = (DapperMaticCallerSession.Current ?? await _access.GetSessionAsync(http, cancellationToken).ConfigureAwait(false)).Access;
        DapperMaticAccess.Hold(caller, datasourceId, null);
        if (_service is not DapperMaticService service)
        {
            throw new InvalidOperationException("DapperMaticCallerDb needs DapperMatic's own service, which opens datasources' connections.");
        }

        var connection = await service.OpenForCallerAsync(datasourceId, caller).ConfigureAwait(false);
        http.Response.RegisterForDispose(connection);
        var scope = await new DmScope(connection).AsCallerAsync(caller, _policies.Policies, datasourceId, cancellationToken).ConfigureAwait(false);
        var provider = connection.GetDbProviderType();

        // The changes webhooks and subscriptions hear are noted in the writes' transactions: the change tables are made
        // now, before the application begins one.
        if (http.RequestServices.GetService<DapperMaticChangeLog>() is { } changes && changes.SinkFor(datasourceId, provider) is { } sink)
        {
            await changes.EnsureAsync(datasourceId, connection, cancellationToken).ConfigureAwait(false);
            scope.OnChanges(sink);
        }

        var results = http.RequestServices.GetService<DapperMaticResultCache>();
        var hub = http.RequestServices.GetService<DapperMaticRealtimeHub>();
        if (results is not null || hub is not null)
        {
            Committing(http, results, hub, scope, datasourceId, provider);
        }

        return caller.Tenant?.SchemaName is { Length: > 0 } schema ? scope.InSchema(schema) : scope;
    }

    // The tables the scope writes to make the results that read them stale, and this server's subscriptions to them are
    // told, once the writes are committed: when the request's response starts (its handler has returned), or when it
    // ends, for writes made after that.
    private static void Committing(HttpContext http, DapperMaticResultCache? results, DapperMaticRealtimeHub? hub, DmScope scope, string datasourceId, DbProviderType provider)
    {
        var written = new HashSet<(string?, string)>();
        var gate = new object();
        Task InvalidateAsync()
        {
            List<(string?, string)> tables;
            lock (gate)
            {
                tables = [.. written];
                written.Clear();
            }

            if (tables.Count == 0)
            {
                return Task.CompletedTask;
            }

            hub?.Notify(datasourceId, tables);
            return results?.WrittenAsync(datasourceId, provider, tables, CancellationToken.None) ?? Task.CompletedTask;
        }

        scope.OnWriting((schema, table) =>
        {
            lock (gate)
            {
                written.Add((schema, table));
            }
        });
        if (!http.Response.HasStarted)
        {
            http.Response.OnStarting(InvalidateAsync);
        }

        http.Response.OnCompleted(InvalidateAsync);
    }
}
