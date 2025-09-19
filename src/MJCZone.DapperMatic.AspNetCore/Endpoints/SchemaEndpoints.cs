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
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Models.Requests;
using MJCZone.DapperMatic.AspNetCore.Models.Responses;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.AspNetCore.Services;

namespace MJCZone.DapperMatic.AspNetCore.Endpoints;

/// <summary>
/// Extension methods for registering DapperMatic schema endpoints.
/// </summary>
public static class SchemaEndpoints
{
    /// <summary>
    /// Maps all DapperMatic schema endpoints to the specified route builder.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <param name="basePath">The base path for the API endpoints. Defaults to "/api/dm".</param>
    /// <returns>The route builder for method chaining.</returns>
    public static IEndpointRouteBuilder MapDapperMaticSchemaEndpoints(
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

        var group = app.MapGroup($"{basePath}/d/{{datasourceId}}/s")
            .WithTags(OperationTags.DatasourceSchemas)
            .WithOpenApi();

        // List all schemas for a datasource
        group
            .MapGet("/", ListSchemasAsync)
            .WithName("ListSchemas")
            .WithSummary("Gets all schemas for a datasource")
            .Produces<SchemaListResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Get specific schema
        group
            .MapGet("/{schemaName}", GetSchemaAsync)
            .WithName("GetSchema")
            .WithSummary("Gets a schema by name")
            .Produces<SchemaResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Create new schema
        group
            .MapPost("/", CreateSchemaAsync)
            .WithName("CreateSchema")
            .WithSummary("Creates a new schema")
            .Produces<SchemaResponse>((int)HttpStatusCode.Created)
            .Produces((int)HttpStatusCode.Conflict)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Drop schema
        group
            .MapDelete("/{schemaName}", DropSchemaAsync)
            .WithName("DropSchema")
            .WithSummary("Drops a schema")
            .Produces<SchemaResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Check if schema exists
        group
            .MapGet("/{schemaName}/exists", SchemaExistsAsync)
            .WithName("SchemaExists")
            .WithSummary("Checks if a schema exists")
            .Produces<SchemaExistsResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        return app;
    }

    private static async Task<IResult> ListSchemasAsync(
        IDapperMaticService service,
        ClaimsPrincipal user,
        [FromRoute] string datasourceId,
        [FromQuery] string? filter = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var schemas = await service
                .GetSchemasAsync(datasourceId, user, cancellationToken)
                .ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(filter))
            {
                schemas = schemas.Where(s =>
                    s.SchemaName != null
                    && s.SchemaName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                );
            }

            return Results.Ok(new SchemaListResponse(schemas));
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (ArgumentException ex)
            when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return Results.NotFound($"Datasource '{datasourceId}' not found");
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

    private static async Task<IResult> GetSchemaAsync(
        IDapperMaticService service,
        ClaimsPrincipal user,
        [FromRoute] string datasourceId,
        [FromRoute] string schemaName,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var schema = await service
                .GetSchemaAsync(datasourceId, schemaName, user, cancellationToken)
                .ConfigureAwait(false);

            return schema == null
                ? Results.NotFound(
                    $"Schema '{schemaName}' not found in datasource '{datasourceId}'"
                )
                : Results.Ok(new SchemaResponse(schema));
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (ArgumentException ex)
            when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return Results.NotFound($"Datasource '{datasourceId}' not found");
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

    private static async Task<IResult> CreateSchemaAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        [FromRoute] string datasourceId,
        CreateSchemaRequest request,
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

        // Validate request
        if (string.IsNullOrWhiteSpace(request.SchemaName))
        {
            return Results.BadRequest(
                new SchemaResponse(null)
                {
                    Success = false,
                    Message = "Schema name is required and cannot be empty",
                }
            );
        }

        try
        {
            var schema = new SchemaDto { SchemaName = request.SchemaName };

            var created = await service
                .CreateSchemaAsync(datasourceId, schema, user, cancellationToken)
                .ConfigureAwait(false);

            return created != null
                ? Results.Created(
                    $"{basePath.TrimEnd('/')}/{created.SchemaName}",
                    new SchemaResponse(created)
                    {
                        Success = true,
                        Message = $"Schema '{created.SchemaName}' created successfully",
                    }
                )
                : Results.Conflict(
                    new SchemaResponse(null)
                    {
                        Success = false,
                        Message = $"Schema '{schema.SchemaName}' already exists",
                    }
                );
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (ArgumentException ex)
            when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return Results.NotFound($"Datasource '{datasourceId}' not found");
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: $"Failed to create schema: {ex.Message}",
                title: "Internal server error",
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> DropSchemaAsync(
        IDapperMaticService service,
        ClaimsPrincipal user,
        [FromRoute] string datasourceId,
        [FromRoute] string schemaName,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var success = await service
                .DropSchemaAsync(datasourceId, schemaName, user, cancellationToken)
                .ConfigureAwait(false);

            return success
                ? Results.Ok(
                    new SchemaResponse(null)
                    {
                        Success = true,
                        Message = $"Schema '{schemaName}' dropped successfully",
                    }
                )
                : Results.NotFound(
                    new SchemaResponse(null)
                    {
                        Success = false,
                        Message = $"Schema '{schemaName}' not found",
                    }
                );
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (ArgumentException ex)
            when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return Results.NotFound($"Datasource '{datasourceId}' not found");
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: $"Failed to drop schema: {ex.Message}",
                title: "Internal server error",
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> SchemaExistsAsync(
        IDapperMaticService service,
        ClaimsPrincipal user,
        [FromRoute] string datasourceId,
        [FromRoute] string schemaName,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var exists = await service
                .SchemaExistsAsync(datasourceId, schemaName, user, cancellationToken)
                .ConfigureAwait(false);

            return Results.Ok(new SchemaExistsResponse(exists) { Success = true });
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (ArgumentException ex)
            when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return Results.NotFound($"Datasource '{datasourceId}' not found");
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
}
