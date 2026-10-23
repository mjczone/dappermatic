// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Changes;
using MJCZone.DapperMatic.AspNetCore.Extensions;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Models.Responses;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.AspNetCore.Services;

namespace MJCZone.DapperMatic.AspNetCore.Endpoints;

/// <summary>
/// Subscriptions to queries, as Server-Sent Events: the query's results at once, then its new results each time a table
/// it reads changes, as the same caller.
/// </summary>
internal static class RealtimeEndpoints
{
    /// <summary>
    /// Maps <c>GET …/t/{tableName}/subscribe</c> and <c>GET …/v/{viewName}/subscribe</c>, with and without a schema.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <param name="basePath">The base path for the API endpoints. Defaults to "/api/dm".</param>
    /// <returns>The route builder for method chaining.</returns>
    public static IEndpointRouteBuilder MapDapperMaticRealtimeEndpoints(this IEndpointRouteBuilder app, string? basePath = null)
    {
        var group = app.MapDapperMaticEndpointGroup(basePath, "/d/{datasourceId}", OperationTags.DatasourceTables);
        foreach (var (route, name, handler) in new (string, string, Delegate)[]
        {
            ("/t/{tableName}/subscribe", "SubscribeTable", SubscribeTableAsync),
            ("/s/{schemaName}/t/{tableName}/subscribe", "SubscribeSchemaTable", SubscribeSchemaTableAsync),
            ("/v/{viewName}/subscribe", "SubscribeView", SubscribeViewAsync),
            ("/s/{schemaName}/v/{viewName}/subscribe", "SubscribeSchemaView", SubscribeSchemaViewAsync),
        })
        {
            // A browser's EventSource cannot send a header: it opens the stream with a ticket.
            group.MapGet(route, handler)
                .WithMetadata(new DapperMaticOperationMetadata(null) { AcceptsTickets = true })
                .WithName(name)
                .WithSummary("Subscribes to a query (the options of /query): its results, then its new results whenever a table it reads changes, as Server-Sent Events")
                .Produces((int)HttpStatusCode.OK, contentType: "text/event-stream")
                .Produces((int)HttpStatusCode.BadRequest)
                .Produces((int)HttpStatusCode.Forbidden);
        }

        return app;
    }

    private static Task<IResult> SubscribeTableAsync(HttpContext http, DapperMaticOperation context, IDapperMaticService service, [FromRoute] string datasourceId, [FromRoute] string tableName) =>
        SubscribeAsync(http, context, service, datasourceId, null, tableName, view: false);

    private static Task<IResult> SubscribeSchemaTableAsync(
        HttpContext http,
        DapperMaticOperation context,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string schemaName,
        [FromRoute] string tableName
    ) => SubscribeAsync(http, context, service, datasourceId, schemaName, tableName, view: false);

    private static Task<IResult> SubscribeViewAsync(HttpContext http, DapperMaticOperation context, IDapperMaticService service, [FromRoute] string datasourceId, [FromRoute] string viewName) =>
        SubscribeAsync(http, context, service, datasourceId, null, viewName, view: true);

    private static Task<IResult> SubscribeSchemaViewAsync(
        HttpContext http,
        DapperMaticOperation context,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string schemaName,
        [FromRoute] string viewName
    ) => SubscribeAsync(http, context, service, datasourceId, schemaName, viewName, view: true);

    private static async Task<IResult> SubscribeAsync(
        HttpContext http,
        DapperMaticOperation context,
        IDapperMaticService service,
        string datasourceId,
        string? schemaName,
        string name,
        bool view
    )
    {
        if (service is not DapperMaticService dapperMatic || http.RequestServices.GetService<DapperMaticRealtimeHub>() is not { } hub)
        {
            throw new InvalidOperationException("Subscriptions need DapperMatic's own service.");
        }

        var options = http.RequestServices.GetRequiredService<IOptions<DapperMaticOptions>>().Value.Realtime;
        var json = http.RequestServices.GetService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>()?.Value.SerializerOptions ?? JsonSerializerOptions.Web;
        var request = QueryDto.FromQueryParameters(context.QueryParameters);
        if (!string.IsNullOrWhiteSpace(schemaName))
        {
            request.SchemaName = schemaName;
        }

        context = context with { RequestBody = request };
        async Task<string> RunAsync(CancellationToken cancellationToken)
        {
            var result = view
                ? await service.QueryViewAsync(context, datasourceId, name, request, schemaName, cancellationToken).ConfigureAwait(false)
                : await service.QueryTableAsync(context, datasourceId, name, request, schemaName, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Serialize(new QueryResponse(result.Data) { Pagination = result.Pagination, Fields = result.Fields }, json);
        }

        // The query runs once before anything is sent: a request it refuses is answered as /query would answer it.
        var aborted = http.RequestAborted;
        var session = DapperMaticCallerSession.Of(http);
        var last = await RunAsync(aborted).ConfigureAwait(false);
        using var subscription = hub.Subscribe(datasourceId, dapperMatic.SubscribedTables(context, datasourceId));

        http.Response.StatusCode = StatusCodes.Status200OK;
        http.Response.ContentType = "text/event-stream";
        http.Response.Headers.CacheControl = "no-cache";
        http.Response.Headers["X-Accel-Buffering"] = "no";
        var sent = 0;
        async Task SendAsync(string kind, string data)
        {
            await http.Response.WriteAsync($"event: {kind}\nid: {++sent}\ndata: {data}\n\n", aborted).ConfigureAwait(false);
            await http.Response.Body.FlushAsync(aborted).ConfigureAwait(false);
        }

        try
        {
            await SendAsync("results", last).ConfigureAwait(false);
            while (!aborted.IsCancellationRequested)
            {
                var changed = await subscription.WaitAsync(options.Heartbeat, aborted).ConfigureAwait(false);
                string next;
                try
                {
                    // The caller's session is checked before each use: it may have ended, or their tenant be kept
                    // differently now. A policy changed meanwhile is read by the re-run itself.
                    if (session is not null)
                    {
                        var current = await session.CheckAsync(aborted).ConfigureAwait(false);
                        if (!ReferenceEquals(current, session))
                        {
                            session = current;
                            DapperMaticCallerSession.Current = session;
                            // Held again as the request named it: with no schema in its URL, it follows the tenant's.
                            context = session.Builder.Hold(session, context with { SchemaName = schemaName });
                        }
                    }

                    if (!changed)
                    {
                        await http.Response.WriteAsync(": ping\n\n", aborted).ConfigureAwait(false);
                        await http.Response.Body.FlushAsync(aborted).ConfigureAwait(false);
                        continue;
                    }

                    // Changes close together are run for once.
                    await Task.Delay(options.Debounce, aborted).ConfigureAwait(false);
                    subscription.Drain();
                    next = await RunAsync(aborted).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or ArgumentException or KeyNotFoundException or InvalidOperationException)
                {
                    // The query no longer runs: the session ended, the policies changed, or the table did. The
                    // subscription ends.
                    await SendAsync("error", JsonSerializer.Serialize(new { title = ex is UnauthorizedAccessException ? "Forbidden" : "The query no longer runs", detail = ex.Message }, json))
                        .ConfigureAwait(false);
                    break;
                }

                if (!string.Equals(next, last, StringComparison.Ordinal))
                {
                    last = next;
                    await SendAsync("results", next).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (aborted.IsCancellationRequested)
        {
            // The client went away.
        }

        return Results.Empty;
    }
}
