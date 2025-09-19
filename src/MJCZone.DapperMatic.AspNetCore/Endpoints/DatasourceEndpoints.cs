// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MJCZone.DapperMatic.AspNetCore.Extensions;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Models.Requests;
using MJCZone.DapperMatic.AspNetCore.Models.Responses;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.AspNetCore.Services;

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
        basePath ??= "/api/dm";

        // make sure basePath starts with a slash and does not end with a slash
        if (!basePath.StartsWith('/'))
        {
            basePath = "/" + basePath;
        }
        if (basePath.EndsWith('/'))
        {
            basePath = basePath.TrimEnd('/');
        }

        var group = app.MapGroup($"{basePath}/datasources")
            .WithTags(OperationTags.Datasources)
            .WithOpenApi();

        // List all datasources - GET only since no parameters needed
        group
            .MapGet("/", ListDatasourcesAsync)
            .WithName("ListDatasources")
            .WithSummary("Gets all registered datasources")
            .Produces<DatasourceListResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.Forbidden);

        // Get specific datasource - POST with request body
        group
            .MapGet("/{id}", GetDatasourceAsync)
            .WithName("GetDatasource")
            .WithSummary("Gets a datasource by ID")
            .Produces<DatasourceResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Add new datasource - POST only
        group
            .MapPost("/", AddDatasourceAsync)
            .WithName("AddDatasource")
            .WithSummary("Adds a new datasource")
            .Produces<DatasourceResponse>((int)HttpStatusCode.Created)
            .Produces((int)HttpStatusCode.Conflict)
            .Produces((int)HttpStatusCode.Forbidden);

        // Update existing datasource - PUT only
        group
            .MapPut("/{id}", UpdateDatasourceAsync)
            .WithName("UpdateDatasource")
            .WithSummary("Updates an existing datasource")
            .Produces<DatasourceResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Patch existing datasource - PATCH only
        group
            .MapPatch("/{id}", UpdateDatasourceAsync)
            .WithName("PatchDatasource")
            .WithSummary("Patches an existing datasource")
            .Produces<DatasourceResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Remove datasource - DELETE only
        group
            .MapDelete("/{id}", RemoveDatasourceAsync)
            .WithName("RemoveDatasource")
            .WithSummary("Removes a datasource")
            .Produces<DatasourceResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Test datasource connection - GET
        group
            .MapGet("/{id}/test", TestDatasourceAsync)
            .WithName("TestDatasource")
            .WithSummary("Tests datasource connectivity")
            .Produces<DatasourceTestResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        return app;
    }

    private static async Task<IResult> ListDatasourcesAsync(
        IDapperMaticService service,
        ClaimsPrincipal user,
        [FromQuery] string? filter = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var datasources = await service
                .GetDatasourcesAsync(user, cancellationToken)
                .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(filter))
            {
                datasources = datasources.Where(d =>
                    (d.Id != null && d.Id.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    || (
                        d.DisplayName != null
                        && d.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                    )
                    || (
                        d.Description != null
                        && d.Description.Contains(filter, StringComparison.OrdinalIgnoreCase)
                    )
                    || (
                        d.Provider != null
                        && d.Provider.Contains(filter, StringComparison.OrdinalIgnoreCase)
                    )
                    || (
                        d.Tags != null
                        && d.Tags.Any(t => t.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    )
                );
            }
            return Results.Ok(new DatasourceListResponse(datasources));
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: ex.Message,
                title: "Internal server error",
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> GetDatasourceAsync(
        IDapperMaticService service,
        ClaimsPrincipal user,
        [FromRoute] string id,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var datasource = await service
                .GetDatasourceAsync(id, user, cancellationToken)
                .ConfigureAwait(false);

            return datasource == null
                ? Results.NotFound($"Datasource '{id}' not found")
                : Results.Ok(new DatasourceResponse(datasource));
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: ex.Message,
                title: "Internal server error",
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> AddDatasourceAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        AddDatasourceRequest request,
        CancellationToken cancellationToken = default
    )
    {
        // Get the base path of the request to construct the location URL
        var basePath = httpContext.Request.PathBase.HasValue
            ? httpContext.Request.PathBase.Value
            : string.Empty;
        basePath += httpContext.Request.Path.HasValue
            ? httpContext.Request.Path.Value
            : string.Empty;

        try
        {
            var datasource = new DatasourceDto
            {
                Id = request.Id,
                Provider = request.Provider,
                ConnectionString = request.ConnectionString,
                DisplayName = request.DisplayName,
                Description = request.Description,
                Tags = request.Tags,
                IsEnabled = request.IsEnabled,
            };

            var created = await service
                .AddDatasourceAsync(datasource, user, cancellationToken)
                .ConfigureAwait(false);

            return created != null
                ? Results.Created(
                    $"{basePath.TrimEnd('/')}/{created.Id}",
                    new DatasourceResponse(created)
                    {
                        Success = true,
                        Message = $"Datasource '{created.Id}' added successfully",
                    }
                )
                : Results.Conflict(
                    new DatasourceResponse(null)
                    {
                        Success = false,
                        Message = $"Datasource '{datasource.Id}' already exists",
                    }
                );
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: $"Failed to add datasource: {ex.Message}",
                title: "Internal server error",
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> UpdateDatasourceAsync(
        IDapperMaticService service,
        ClaimsPrincipal user,
        [FromRoute] string id,
        [FromBody] UpdateDatasourceRequest request,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var datasource = new DatasourceDto
            {
                Id = id,
                Provider = request.Provider,
                ConnectionString = request.ConnectionString,
                DisplayName = request.DisplayName,
                Description = request.Description,
                Tags = request.Tags,
                IsEnabled = request.IsEnabled,
            };

            var updated = await service
                .UpdateDatasourceAsync(datasource, user, cancellationToken)
                .ConfigureAwait(false);

            return updated != null
                ? Results.Ok(
                    new DatasourceResponse(updated)
                    {
                        Success = true,
                        Message = $"Datasource '{id}' updated successfully",
                    }
                )
                : Results.NotFound(
                    new DatasourceResponse(null)
                    {
                        Success = false,
                        Message = $"Datasource '{id}' not found",
                    }
                );
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: $"Failed to update datasource: {ex.Message}",
                title: "Internal server error",
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> RemoveDatasourceAsync(
        IDapperMaticService service,
        ClaimsPrincipal user,
        [FromRoute] string id,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var success = await service
                .RemoveDatasourceAsync(id, user, cancellationToken)
                .ConfigureAwait(false);

            return success
                ? Results.Ok(
                    new DatasourceResponse(null)
                    {
                        Success = true,
                        Message = $"Datasource '{id}' removed successfully",
                    }
                )
                : Results.NotFound(
                    new DatasourceResponse(null)
                    {
                        Success = false,
                        Message = $"Datasource '{id}' not found",
                    }
                );
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: $"Failed to remove datasource: {ex.Message}",
                title: "Internal server error",
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> TestDatasourceAsync(
        IDapperMaticService service,
        ClaimsPrincipal user,
        [FromRoute] string id,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var result = await service
                .TestDatasourceAsync(id, user, cancellationToken)
                .ConfigureAwait(false);

            return result.IsConnected
                ? Results.Ok(
                    new DatasourceTestResponse(result)
                    {
                        Success = true,
                        Message = $"Successfully connected to datasource '{id}'",
                    }
                )
                : Results.Ok(
                    new DatasourceTestResponse(result)
                    {
                        Success = false,
                        Message = result.ErrorMessage ?? $"Failed to connect to datasource '{id}'",
                    }
                );
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: $"Failed to test datasource: {ex.Message}",
                title: "Internal server error",
                statusCode: 500
            );
        }
    }
}
