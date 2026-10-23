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
using MJCZone.DapperMatic.AspNetCore.Extensions;
using MJCZone.DapperMatic.AspNetCore.Models.Responses;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.AspNetCore.Services;

namespace MJCZone.DapperMatic.AspNetCore.Endpoints;

/// <summary>
/// Running the functions an application declares.
/// </summary>
internal static class FunctionEndpoints
{
    /// <summary>
    /// Maps <c>POST /d/{datasourceId}/f/{functionName}</c>.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <param name="basePath">The base path for the API endpoints. Defaults to "/api/dm".</param>
    /// <returns>The route builder for method chaining.</returns>
    public static IEndpointRouteBuilder MapDapperMaticFunctionEndpoints(this IEndpointRouteBuilder app, string? basePath = null)
    {
        app.MapDapperMaticEndpointGroup(basePath, "/d/{datasourceId}/f", OperationTags.DatasourceTables)
            .MapPost("/{functionName}", ExecuteAsync)
            .WithName("ExecuteFunction")
            .WithSummary("Runs a function the application declares, with arguments by name, and returns its rows")
            .Produces<FunctionResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.BadRequest)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);
        return app;
    }

    private static async Task<IResult> ExecuteAsync(
        HttpContext http,
        DapperMaticOperation operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string functionName,
        CancellationToken cancellationToken = default
    )
    {
        // The arguments are optional: a function without parameters is run with no body.
        JsonElement? arguments = null;
        if (http.Request.ContentLength is not 0 && http.Request.HasJsonContentType())
        {
            arguments = await http.Request.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);
        }

        operationContext = operationContext with { RequestBody = arguments };
        return Results.Ok(await service.ExecuteFunctionAsync(operationContext, datasourceId, functionName, arguments, cancellationToken).ConfigureAwait(false));
    }
}
