// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Authentication;
using MJCZone.DapperMatic.AspNetCore.Extensions;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Models.Responses;
using MJCZone.DapperMatic.AspNetCore.Security;

namespace MJCZone.DapperMatic.AspNetCore.Endpoints;

/// <summary>
/// Endpoints for signing in: tickets that open streams.
/// </summary>
internal static class AuthEndpoints
{
    /// <summary>
    /// Maps the sign-in endpoints.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <param name="basePath">The base path for the API endpoints. Defaults to "/api/dm".</param>
    /// <returns>The route builder for method chaining.</returns>
    public static IEndpointRouteBuilder MapDapperMaticAuthEndpoints(this IEndpointRouteBuilder app, string? basePath = null)
    {
        var group = app.MapDapperMaticEndpointGroup(basePath, "/auth", OperationTags.Auth);

        group.MapPost("/ticket", IssueTicketAsync)
            .WithMetadata(new DapperMaticOperationMetadata("auth/ticket"))
            .WithName("IssueStreamTicket")
            .WithSummary("A ticket that opens one stream (a subscription, a GraphQL socket) as the caller, once, within seconds")
            .Produces<TicketResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.BadRequest)
            .Produces((int)HttpStatusCode.Unauthorized);

        return app;
    }

    // A signed-in caller's ticket for one of DapperMatic's streams, in the tenant their request is in.
    private static async Task<IResult> IssueTicketAsync(
        HttpContext http,
        DapperMaticTickets tickets,
        IOptions<DapperMaticOptions> options,
        [FromBody] TicketRequest request,
        CancellationToken cancellationToken
    )
    {
        if (http.User.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedAccessException("A ticket opens a stream as its caller: sign in first.");
        }

        if (DapperMaticClaims.ByTicket(http.User))
        {
            throw new UnauthorizedAccessException("A ticket's caller cannot get another.");
        }

        var basePath = "/" + (options.Value.BasePath ?? "/api/dm").Trim('/') + "/";
        if (request.Path is not { Length: > 0 } path || !("/" + path.TrimStart('/')).StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"A ticket opens one of DapperMatic's streams: give its path, under {basePath}.");
        }

        var tenant = DapperMaticCallerSession.Of(http)?.Tenant?.Id.ToString();
        var until = http.Features.Get<DapperMaticSignedIn>()?.Until;
        var ticket = await tickets.IssueAsync(http.User, until, tenant, path, cancellationToken).ConfigureAwait(false);
        return Results.Ok(new TicketResponse(ticket, (int)tickets.Lifetime.TotalSeconds));
    }
}
