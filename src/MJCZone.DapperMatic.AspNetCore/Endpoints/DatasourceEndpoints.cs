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
using MJCZone.DapperMatic.AspNetCore.Validation;

namespace MJCZone.DapperMatic.AspNetCore.Endpoints;

/// <summary>
/// Extension methods for registering DapperMatic datasource endpoints.
/// </summary>
public static class DatasourceEndpoints
{
    /// <summary>
    /// Maps all DapperMatic datasource endpoints to the specified route builder.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <param name="basePath">The base path for the API endpoints. Defaults to "/api/dm".</param>
    /// <returns>The route builder for method chaining.</returns>
    public static IEndpointRouteBuilder MapDapperMaticDatasourceEndpoints(
        this IEndpointRouteBuilder app,
        string? basePath = null
    )
    {
        var group = app.MapDapperMaticEndpointGroup(basePath, "/d", OperationTags.Datasources);

        // List all datasources - GET only since no parameters needed
        group
            .MapGet("/", ListDatasourcesAsync)
            .WithName("ListDatasources")
            .WithSummary("Gets all registered datasources")
            .Produces<DatasourceListResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.Forbidden);

        // Get specific datasource - POST with request body
        group
            .MapGet("/{datasourceId}", GetDatasourceAsync)
            .WithName("GetDatasource")
            .WithSummary("Gets a datasource by ID")
            .Produces<DatasourceResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Add new datasource - POST only
        group
            .MapPost("/", CreateDatasourceAsync)
            .WithName("AddDatasource")
            .WithSummary("Adds a new datasource")
            .Produces<DatasourceResponse>((int)HttpStatusCode.Created)
            .Produces((int)HttpStatusCode.Conflict)
            .Produces((int)HttpStatusCode.Forbidden);

        // Update existing datasource - PUT only
        group
            .MapPut("/{datasourceId}", UpdateDatasourceAsync)
            .WithName("UpdateDatasource")
            .WithSummary("Updates an existing datasource")
            .Produces<DatasourceResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Patch existing datasource - PATCH only
        group
            .MapPatch("/{datasourceId}", UpdateDatasourceAsync)
            .WithName("PatchDatasource")
            .WithSummary("Patches an existing datasource")
            .Produces<DatasourceResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Remove datasource - DELETE only
        group
            .MapDelete("/{datasourceId}", DeleteDatasourceAsync)
            .WithName("RemoveDatasource")
            .WithSummary("Removes a datasource")
            .Produces<DatasourceResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Test datasource connection - GET
        group
            .MapGet("/{datasourceId}/exists", DatasourceExistsAsync)
            .WithName("DatasourceExists")
            .WithSummary("Checks if a datasource exists")
            .Produces<DatasourceTestResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.Forbidden);

        // Test datasource connection - GET
        group
            .MapGet("/{datasourceId}/test", TestDatasourceAsync)
            .WithName("TestDatasource")
            .WithSummary("Tests datasource connectivity")
            .Produces<DatasourceTestResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        return app;
    }

    private static async Task<IResult> ListDatasourcesAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromQuery] string? filter = null,
        CancellationToken cancellationToken = default
    )
    {
        var datasources = await service.GetDatasourcesAsync(operationContext, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(filter))
        {
            datasources = datasources.Where(d =>
                (d.Id != null && d.Id.Contains(filter, StringComparison.OrdinalIgnoreCase))
                || (d.DisplayName != null && d.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                || (d.Description != null && d.Description.Contains(filter, StringComparison.OrdinalIgnoreCase))
                || (d.Provider != null && d.Provider.Contains(filter, StringComparison.OrdinalIgnoreCase))
                || (d.Tags != null && d.Tags.Any(t => t.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            );
        }
        return Results.Ok(new DatasourceListResponse(datasources));
    }

    private static async Task<IResult> GetDatasourceAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        CancellationToken cancellationToken = default
    )
    {
        var datasource = await service
            .GetDatasourceAsync(operationContext, datasourceId, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(new DatasourceResponse(datasource));
    }

    private static async Task<IResult> CreateDatasourceAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromBody] DatasourceDto datasource,
        CancellationToken cancellationToken = default
    )
    {
        // API layer validation
        Validate
            .Object(datasource)
            .MaxLength(r => r.Id, 64, nameof(DatasourceDto.Id), inclusive: true)
            .NotNullOrWhiteSpace(r => r.Provider, nameof(DatasourceDto.Provider))
            .MaxLength(r => r.Provider, 10, nameof(DatasourceDto.Provider), inclusive: true)
            .MinLength(r => r.Provider, 2, nameof(DatasourceDto.Provider), inclusive: true)
            .NotNullOrWhiteSpace(r => r.ConnectionString, nameof(DatasourceDto.ConnectionString))
            .MaxLength(r => r.ConnectionString, 2000, nameof(DatasourceDto.ConnectionString), inclusive: false)
            .NotNullOrWhiteSpace(r => r.DisplayName, nameof(DatasourceDto.DisplayName))
            .MaxLength(r => r.DisplayName, 128, nameof(DatasourceDto.DisplayName), inclusive: true)
            .MaxLength(r => r.Description, 1000, nameof(DatasourceDto.Description), inclusive: true)
            .Assert();

        operationContext.RequestBody = datasource;
        operationContext.DatasourceId = datasource.Id;

        var created = await service
            .AddDatasourceAsync(operationContext, datasource, cancellationToken)
            .ConfigureAwait(false);

        return Results.Created(
            $"{operationContext.EndpointPath?.TrimEnd('/')}/{created.Id}",
            new DatasourceResponse(created)
        );
    }

    private static async Task<IResult> UpdateDatasourceAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromBody] DatasourceDto datasource,
        CancellationToken cancellationToken = default
    )
    {
        datasource.Id = datasourceId;

        // API layer validation
        Validate
            .Object(datasource)
            .MaxLength(r => r.Provider, 10, nameof(DatasourceDto.Provider), inclusive: true) // pgsql
            .MinLength(r => r.Provider, 2, nameof(DatasourceDto.Provider), inclusive: true) // pg
            .MaxLength(r => r.ConnectionString, 2000, nameof(DatasourceDto.ConnectionString), inclusive: false)
            .MaxLength(r => r.DisplayName, 128, nameof(DatasourceDto.DisplayName), inclusive: true)
            .MaxLength(r => r.Description, 1000, nameof(DatasourceDto.Description), inclusive: true)
            .Assert();

        operationContext.RequestBody = datasource;

        var updated = await service
            .UpdateDatasourceAsync(operationContext, datasource, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(new DatasourceResponse(updated));
    }

    private static async Task<IResult> DeleteDatasourceAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        CancellationToken cancellationToken = default
    )
    {
        await service.RemoveDatasourceAsync(operationContext, datasourceId, cancellationToken).ConfigureAwait(false);

        return Results.NoContent();
    }

    private static async Task<IResult> DatasourceExistsAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        CancellationToken cancellationToken = default
    )
    {
        var exists = await service
            .DatasourceExistsAsync(operationContext, datasourceId, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(new DatasourceExistsResponse(exists));
    }

    private static async Task<IResult> TestDatasourceAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceConnectivityTest = await service
            .TestDatasourceAsync(operationContext, datasourceId, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(new DatasourceTestResponse(datasourceConnectivityTest));
    }
}
