// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Auditing;
using MJCZone.DapperMatic.AspNetCore.Authentication;
using MJCZone.DapperMatic.AspNetCore.Caching;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.AspNetCore.Validation;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Extensions;

/// <summary>
/// What every DapperMatic endpoint goes through, whatever maps it (route handlers, MCP, GraphQL, streams): wrapped once,
/// after its handler is built, by a <c>Finally</c> convention on DapperMatic's route group. It names the operation,
/// builds the caller's session and holds the request to their tenant, traces and measures the request, turns an
/// exception into problem details while the response has not started, audits the failure, and forgets cached
/// descriptions and results after a change to a schema or datasource.
/// </summary>
internal static class DapperMaticRequestPipeline
{
    private static readonly Histogram<double> Duration =
        DmTelemetry.Meter.CreateHistogram<double>("dappermatic.request.duration", "ms", "How long DapperMatic's web API requests take.");

    /// <summary>
    /// Wraps an endpoint's request delegate in the pipeline.
    /// </summary>
    /// <param name="builder">The endpoint's builder, its handler already built.</param>
    public static void Wrap(EndpointBuilder builder)
    {
        if (builder.RequestDelegate is not { } next)
        {
            return;
        }

        // Whether the endpoint signs a socket's caller in on the socket, and takes tickets: decided by the endpoint, never
        // by the request, whose headers its sender chooses.
        var declared = builder.Metadata.OfType<DapperMaticOperationMetadata>().LastOrDefault();
        var signsInOnSocket = declared?.SignsInOnSocket == true;
        var acceptsTickets = declared?.AcceptsTickets == true;
        builder.RequestDelegate = http => InvokeAsync(http, next, signsInOnSocket, acceptsTickets);
    }

    /// <summary>
    /// Whether an operation may change a schema or a datasource: a write of a definition, as the policies class it.
    /// Reads, row writes, batches, functions, GraphQL, MCP and access management change no schema; a write of rows makes
    /// its own tables' results stale.
    /// </summary>
    /// <param name="operation">The operation's name.</param>
    /// <returns>Whether what DapperMatic keeps of the datasource is stale after it.</returns>
    internal static bool ChangesSchema(string operation)
    {
        var verb = operation[(operation.LastIndexOf('/') + 1)..];
        return verb.ToLowerInvariant() is "post" or "put" or "patch" or "delete"
            && !operation.StartsWith("batch/", StringComparison.OrdinalIgnoreCase)
            && DapperMaticPolicySet.ActionOf(operation) is DmAction.ChangeSchema or DmAction.ManageDatasources;
    }

    /// <summary>
    /// Audits a failure as the operation the service received, with what the endpoint read from the request (its body,
    /// the columns it names); else as the request's. Front ends that answer failures themselves (GraphQL's errors)
    /// audit them so too.
    /// </summary>
    /// <param name="services">The request's services.</param>
    /// <param name="operation">The operation that failed.</param>
    /// <param name="ex">What went wrong.</param>
    /// <returns>A task.</returns>
    internal static async Task AuditFailureAsync(IServiceProvider services, DapperMaticOperation operation, Exception ex)
    {
        if (services.GetService<IDapperMaticAuditLogger>() is { } audit)
        {
            var claims = services.GetService<IOptions<DapperMaticOptions>>()?.Value.AuditClaims ?? [];
            await audit.LogOperationAsync((operation.State.Received ?? operation).ToAuditEvent(false, ex.Message, claims)).ConfigureAwait(false);
        }
    }

    private static async Task InvokeAsync(HttpContext http, RequestDelegate next, bool signsInOnSocket, bool acceptsTickets)
    {
        var services = http.RequestServices;
        var operation = DapperMaticOperation.Of(http);
        using var activity = DmTelemetry.Source.StartActivity("DapperMatic request", ActivityKind.Internal);
        var started = Stopwatch.GetTimestamp();
        try
        {
            // Who is calling, as DapperMatic's endpoints sign callers in. A socket's caller signs in on the socket, after the
            // request that opened it: an endpoint that takes such sockets builds their session then. Every other request is
            // its caller's session, held to their tenant.
            await SignInAsync(http, services, acceptsTickets).ConfigureAwait(false);
            if (!(signsInOnSocket && http.WebSockets.IsWebSocketRequest) && services.GetService<DapperMaticAccess>() is { } access)
            {
                var session = await access.GetSessionAsync(http, http.RequestAborted).ConfigureAwait(false);
                DapperMaticCallerSession.Current = session;

                // A request refused for its tenant is audited as its caller's, with that tenant.
                operation = operation with { User = session.User, Access = session.Access.ForDatasource(operation.DatasourceId) };
                operation = access.Hold(session, operation);
                http.Features.Set(operation);
            }

            await next(http).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested)
        {
            // The client went away: nothing failed, and there is no one to answer.
        }
        catch (Exception ex) when (!http.Response.HasStarted)
        {
            await AuditFailureAsync(http.RequestServices, operation, ex).ConfigureAwait(false);
            if (ex is DapperMaticSignInEndedException)
            {
                // The caller's credentials no longer sign in: they are no one, and answered so (401).
                http.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity());
            }

            await ErrorHandler.HandleError(ex, http).ExecuteAsync(http).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Too late for problem details: the response has started (a stream, a socket). The failure is still audited.
            await AuditFailureAsync(http.RequestServices, operation, ex).ConfigureAwait(false);
            throw;
        }
        finally
        {
            var name = operation.Operation ?? "unknown";
            var status = http.Response.StatusCode;
            if (activity is not null)
            {
                activity.DisplayName = $"DapperMatic {name}";
                activity.SetTag("dappermatic.operation", name);
                activity.SetTag("dappermatic.datasource", operation.DatasourceId);
                activity.SetTag("dappermatic.schema", operation.SchemaName);
                activity.SetTag("dappermatic.table", operation.TableName ?? operation.ViewName);
                activity.SetTag("dappermatic.tenant", operation.Access?.Tenant?.Id.ToString());
                activity.SetTag("http.response.status_code", status);
                if (status >= 500)
                {
                    activity.SetStatus(ActivityStatusCode.Error);
                }
            }

            Duration.Record(
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                new KeyValuePair<string, object?>("dappermatic.operation", name),
                new KeyValuePair<string, object?>("http.response.status_code", status)
            );

            // A failed change may still have changed something: MySQL does not roll DDL back.
            await ForgetChangedSchemaAsync(http, operation).ConfigureAwait(false);
        }
    }

    // Who is calling: a stream's ticket, where the endpoint takes one; else the sign-in methods DapperMatic's endpoints
    // accept. Credentials sent and refused are refused (401), not taken for no one.
    private static async Task SignInAsync(HttpContext http, IServiceProvider services, bool acceptsTickets)
    {
        if (acceptsTickets && http.Request.Query.TryGetValue("ticket", out var ticket) && services.GetService<DapperMaticTickets>() is { } tickets)
        {
            var redeemed = await tickets.RedeemAsync(ticket.ToString(), http.Request.PathBase + http.Request.Path, http.RequestAborted).ConfigureAwait(false)
                ?? throw new UnauthorizedAccessException("The ticket does not open this stream: it was used, has expired, or is another stream's.");
            http.User = redeemed.User;
            http.Features.Set(new DapperMaticSignedIn(redeemed.SignedInUntil));
            return;
        }

        if (services.GetService<DapperMaticSignIn>() is { } signIn)
        {
            var (user, until, failure) = await signIn.AuthenticateAsync(http).ConfigureAwait(false);
            if (failure is not null && user.Identity?.IsAuthenticated != true && signIn.CarriesCredentials(http))
            {
                throw new UnauthorizedAccessException($"The credentials sent were not accepted: {failure}");
            }

            http.User = user;
            http.Features.Set(new DapperMaticSignedIn(until));
        }
    }

    // A schema or datasource changed through DapperMatic: the datasource's cached descriptions and results are
    // forgotten.
    private static async Task ForgetChangedSchemaAsync(HttpContext http, DapperMaticOperation operation)
    {
        if (operation.DatasourceId is not { Length: > 0 } datasourceId || operation.Operation is not { } name)
        {
            return;
        }

        if (ChangesSchema(name))
        {
            http.RequestServices.GetService<DapperMaticSchemaCache>()?.Invalidate(datasourceId);
            if (http.RequestServices.GetService<DapperMaticResultCache>() is { } results)
            {
                await results.InvalidateAsync(datasourceId, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }
}
