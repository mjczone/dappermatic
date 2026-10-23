// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MJCZone.DapperMatic.AspNetCore.Extensions;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.AspNetCore.Services;

namespace MJCZone.DapperMatic.AspNetCore.Endpoints;

/// <summary>
/// The OpenAPI document of a datasource's rows as the calling user may use them.
/// </summary>
internal static class OpenApiEndpoints
{
    /// <summary>
    /// Maps <c>GET /d/{datasourceId}/openapi</c> and <c>GET /d/{datasourceId}/s/{schemaName}/openapi</c>.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <param name="basePath">The base path for the API endpoints. Defaults to "/api/dm".</param>
    /// <returns>The route builder for method chaining.</returns>
    public static IEndpointRouteBuilder MapDapperMaticOpenApiEndpoints(this IEndpointRouteBuilder app, string? basePath = null)
    {
        foreach (var (prefix, name, handler) in new (string, string, Delegate)[]
        {
            ("/d/{datasourceId}/openapi", "GetDatasourceOpenApi", GetAsync),
            ("/d/{datasourceId}/s/{schemaName}/openapi", "GetSchemaOpenApi", GetSchemaAsync),
        })
        {
            app.MapDapperMaticEndpointGroup(basePath, prefix, OperationTags.DatasourceTables)
                .MapGet("/", handler)
                .WithName(name)
                .WithSummary("Describes the datasource's rows as the caller may use them: an OpenAPI 3.1 document of its tables, actions and columns")
                .Produces((int)HttpStatusCode.OK, contentType: "application/json")
                .Produces((int)HttpStatusCode.NotFound)
                .Produces((int)HttpStatusCode.Forbidden);
        }

        return app;
    }

    private static Task<IResult> GetAsync(
        HttpContext http,
        DapperMaticOperation operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        CancellationToken cancellationToken = default
    ) => DocumentAsync(http, operationContext, service, datasourceId, null, cancellationToken);

    private static Task<IResult> GetSchemaAsync(
        HttpContext http,
        DapperMaticOperation operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string schemaName,
        CancellationToken cancellationToken = default
    ) => DocumentAsync(http, operationContext, service, datasourceId, schemaName, cancellationToken);

    private static async Task<IResult> DocumentAsync(
        HttpContext http,
        DapperMaticOperation operationContext,
        IDapperMaticService service,
        string datasourceId,
        string? schemaName,
        CancellationToken cancellationToken
    )
    {
        // The paths are relative to where this document is served: the request's path without "/openapi".
        var path = http.Request.Path.Value!.TrimEnd('/');
        var server = http.Request.PathBase + path[..path.LastIndexOf('/')];
        return Results.Ok(await service.GetOpenApiDocumentAsync(operationContext, datasourceId, schemaName, server, cancellationToken).ConfigureAwait(false));
    }
}
