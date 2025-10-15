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
        var group = app.MapDapperMaticEndpointGroup(
            basePath,
            "/d/{datasourceId}/s",
            OperationTags.DatasourceSchemas
        );

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
            .Produces((int)HttpStatusCode.NotFound) // if the datasource is not found (NOT to be confused, as schema not found returns 200 with result: false)
            .Produces((int)HttpStatusCode.Forbidden);

        return app;
    }

    private static async Task<IResult> ListSchemasAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromQuery] string? filter = null,
        CancellationToken cancellationToken = default
    )
    {
        var schemas = await service
            .GetSchemasAsync(operationContext, datasourceId, cancellationToken)
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

    private static async Task<IResult> GetSchemaAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string schemaName,
        CancellationToken cancellationToken = default
    )
    {
        var schema = await service
            .GetSchemaAsync(operationContext, datasourceId, schemaName, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(new SchemaResponse(schema));
    }

    private static async Task<IResult> CreateSchemaAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromBody] SchemaDto schema,
        CancellationToken cancellationToken = default
    )
    {
        // API layer validation
        Validate
            .Object(schema)
            .NotNullOrWhiteSpace(r => r.SchemaName, nameof(SchemaDto.SchemaName))
            .MaxLength(r => r.SchemaName, 128, nameof(SchemaDto.SchemaName), inclusive: true)
            .MinLength(r => r.SchemaName, 1, nameof(SchemaDto.SchemaName), inclusive: true)
            .Assert();

        operationContext.RequestBody = schema;
        operationContext.SchemaName = schema.SchemaName!.Trim();

        var created = await service
            .CreateSchemaAsync(operationContext, datasourceId, schema, cancellationToken)
            .ConfigureAwait(false);

        return Results.Created(
            $"{operationContext.EndpointPath?.TrimEnd('/')}/{created.SchemaName}",
            new SchemaResponse(created)
        );
    }

    private static async Task<IResult> DropSchemaAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string schemaName,
        CancellationToken cancellationToken = default
    )
    {
        await service
            .DropSchemaAsync(operationContext, datasourceId, schemaName, cancellationToken)
            .ConfigureAwait(false);

        return Results.NoContent();
    }

    private static async Task<IResult> SchemaExistsAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string schemaName,
        CancellationToken cancellationToken = default
    )
    {
        var exists = await service
            .SchemaExistsAsync(operationContext, datasourceId, schemaName, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(new SchemaExistsResponse(exists));
    }
}
