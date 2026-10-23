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
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Models.Responses;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.AspNetCore.Services;

namespace MJCZone.DapperMatic.AspNetCore.Endpoints;

/// <summary>
/// Writes of rows of several tables in one request, all or none.
/// </summary>
internal static class BatchEndpoints
{
    /// <summary>
    /// Maps the batch endpoint: <c>POST /d/{datasourceId}/batch</c>.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <param name="basePath">The base path for the API endpoints. Defaults to "/api/dm".</param>
    /// <returns>The route builder for method chaining.</returns>
    public static IEndpointRouteBuilder MapDapperMaticBatchEndpoints(this IEndpointRouteBuilder app, string? basePath = null)
    {
        var group = app.MapDapperMaticEndpointGroup(basePath, "/d/{datasourceId}/batch", OperationTags.DatasourceTables);
        group.MapPost("/", BatchAsync)
            .WithName("RunBatch")
            .WithSummary("Runs writes of rows of the datasource's tables in order, in one transaction, all or none")
            .Produces<BatchResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.BadRequest)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);
        return app;
    }

    private static async Task<IResult> BatchAsync(
        DapperMaticOperation operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromBody] BatchDto request,
        CancellationToken cancellationToken = default
    )
    {
        operationContext = operationContext with { RequestBody = request };
        return Results.Ok(await service.BatchAsync(operationContext, datasourceId, request, cancellationToken).ConfigureAwait(false));
    }
}
