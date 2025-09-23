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
/// Extension methods for registering DapperMatic primary key constraint endpoints.
/// </summary>
public static class PrimaryKeyConstraintEndpoints
{
    /// <summary>
    /// Maps all DapperMatic primary key constraint endpoints to the specified route builder.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <param name="basePath">The base path for the API endpoints. Defaults to "/api/dm".</param>
    /// <returns>The route builder for method chaining.</returns>
    public static IEndpointRouteBuilder MapDapperMaticPrimaryKeyConstraintEndpoints(
        this IEndpointRouteBuilder app,
        string? basePath = null
    )
    {
        // Register default schema endpoints (for single-schema scenarios)
        var defaultGroup = app.MapDapperMaticEndpointGroup(
            basePath,
            "/d/{datasourceId}/t/{tableName}/primary-key-constraint",
            OperationTags.DatasourceTables
        );

        RegisterPrimaryKeyConstraintEndpoints(
            defaultGroup,
            "DefaultSchema",
            isSchemaSpecific: false
        );

        // Register schema-specific endpoints (for multi-tenant scenarios)
        var schemaGroup = app.MapDapperMaticEndpointGroup(
            basePath,
            "/d/{datasourceId}/s/{schemaName}/t/{tableName}/primary-key-constraint",
            OperationTags.DatasourceTables
        );

        RegisterPrimaryKeyConstraintEndpoints(schemaGroup, "Schema", isSchemaSpecific: true);

        return app;
    }

    private static void RegisterPrimaryKeyConstraintEndpoints(
        RouteGroupBuilder group,
        string namePrefix,
        bool isSchemaSpecific
    )
    {
        var schemaText = isSchemaSpecific ? "a specific schema" : "the default schema";
        var schemaInText = isSchemaSpecific ? "in a specific schema" : "in the default schema";

        // Primary key constraint endpoints
        group
            .MapGet("/", GetPrimaryKeyAsync)
            .WithName($"Get{namePrefix}PrimaryKey")
            .WithSummary($"Gets the primary key constraint for a table {schemaInText}")
            .Produces<PrimaryKeyResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapPost("/", CreatePrimaryKeyAsync)
            .WithName($"Create{namePrefix}PrimaryKey")
            .WithSummary($"Creates a primary key constraint on a table {schemaInText}")
            .Produces<PrimaryKeyResponse>((int)HttpStatusCode.Created)
            .Produces((int)HttpStatusCode.Conflict)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapDelete("/", DropPrimaryKeyAsync)
            .WithName($"Drop{namePrefix}PrimaryKey")
            .WithSummary($"Drops the primary key constraint from a table {schemaInText}")
            .Produces<PrimaryKeyResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);
    }

    // Primary key constraint endpoint implementations
    private static async Task<IResult> GetPrimaryKeyAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string tableName,
        CancellationToken cancellationToken = default
    )
    {
        var result = await service
            .GetPrimaryKeyAsync(
                operationContext,
                datasourceId,
                tableName,
                schemaName,
                cancellationToken
            )
            .ConfigureAwait(false);

        return Results.Ok(new PrimaryKeyResponse(result));
    }

    private static async Task<IResult> CreatePrimaryKeyAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string tableName,
        [FromBody] PrimaryKeyConstraintDto primaryKey,
        CancellationToken cancellationToken = default
    )
    {
        // API layer validation
        Validate
            .Object(primaryKey)
            .NotNull(v => v.ColumnNames, nameof(PrimaryKeyConstraintDto.ColumnNames))
            .Custom(
                v => v.ColumnNames!.Count > 0,
                nameof(PrimaryKeyConstraintDto.ColumnNames),
                $"At least one column is required"
            )
            .Assert();

        operationContext.RequestBody = primaryKey;

        var result = await service
            .CreatePrimaryKeyAsync(
                operationContext,
                datasourceId,
                tableName,
                primaryKey,
                schemaName,
                cancellationToken
            )
            .ConfigureAwait(false);

        return Results.Created(
            operationContext.EndpointPath?.TrimEnd('/'),
            new PrimaryKeyResponse(result)
        );
    }

    private static async Task<IResult> DropPrimaryKeyAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string tableName,
        CancellationToken cancellationToken = default
    )
    {
        await service
            .DropPrimaryKeyAsync(
                operationContext,
                datasourceId,
                tableName,
                schemaName,
                cancellationToken
            )
            .ConfigureAwait(false);

        return Results.NoContent();
    }
}
