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
/// Extension methods for registering DapperMatic unique constraint endpoints.
/// </summary>
public static class UniqueConstraintEndpoints
{
    /// <summary>
    /// Maps all DapperMatic unique constraint endpoints to the specified route builder.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <param name="basePath">The base path for the API endpoints. Defaults to "/api/dm".</param>
    /// <returns>The route builder for method chaining.</returns>
    public static IEndpointRouteBuilder MapDapperMaticUniqueConstraintEndpoints(
        this IEndpointRouteBuilder app,
        string? basePath = null
    )
    {
        // Register default schema endpoints (for single-schema scenarios)
        var defaultGroup = app.MapDapperMaticEndpointGroup(
            basePath,
            "/d/{datasourceId}/t/{tableName}/unique-constraints",
            OperationTags.DatasourceTables
        );

        RegisterUniqueConstraintEndpoints(defaultGroup, "DefaultSchema", isSchemaSpecific: false);

        // Register schema-specific endpoints (for multi-tenant scenarios)
        var schemaGroup = app.MapDapperMaticEndpointGroup(
            basePath,
            "/d/{datasourceId}/s/{schemaName}/t/{tableName}/unique-constraints",
            OperationTags.DatasourceTables
        );

        RegisterUniqueConstraintEndpoints(schemaGroup, "Schema", isSchemaSpecific: true);

        return app;
    }

    private static void RegisterUniqueConstraintEndpoints(
        RouteGroupBuilder group,
        string namePrefix,
        bool isSchemaSpecific
    )
    {
        var schemaText = isSchemaSpecific ? "a specific schema" : "the default schema";
        var schemaInText = isSchemaSpecific ? "in a specific schema" : "in the default schema";

        // Unique constraint endpoints
        group
            .MapGet("/", isSchemaSpecific ? ListSchemaUniqueConstraintsAsync : ListUniqueConstraintsAsync)
            .WithName($"List{namePrefix}UniqueConstraints")
            .WithSummary($"Gets all unique constraints for a table {schemaInText}")
            .Produces<UniqueConstraintListResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapGet("/{constraintName}", isSchemaSpecific ? GetSchemaUniqueConstraintAsync : GetUniqueConstraintAsync)
            .WithName($"Get{namePrefix}UniqueConstraint")
            .WithSummary($"Gets a specific unique constraint from a table {schemaInText}")
            .Produces<UniqueConstraintResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapPost("/", isSchemaSpecific ? CreateSchemaUniqueConstraintAsync : CreateUniqueConstraintAsync)
            .WithName($"Create{namePrefix}UniqueConstraint")
            .WithSummary($"Creates a unique constraint on a table {schemaInText}")
            .Produces<UniqueConstraintResponse>((int)HttpStatusCode.Created)
            .Produces((int)HttpStatusCode.Conflict)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapDelete(
                "/{constraintName}",
                isSchemaSpecific ? DropSchemaUniqueConstraintAsync : DropUniqueConstraintAsync
            )
            .WithName($"Drop{namePrefix}UniqueConstraint")
            .WithSummary($"Drops a unique constraint from a table {schemaInText}")
            .Produces<UniqueConstraintResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);
    }

    // Unique constraint endpoint implementations
    private static Task<IResult> ListUniqueConstraintsAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string tableName,
        CancellationToken cancellationToken = default
    ) => ListSchemaUniqueConstraintsAsync(operationContext, service, datasourceId, null, tableName, cancellationToken);

    private static async Task<IResult> ListSchemaUniqueConstraintsAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string tableName,
        CancellationToken cancellationToken = default
    )
    {
        var uniqueConstraints = await service
            .GetUniqueConstraintsAsync(operationContext, datasourceId, tableName, schemaName, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(new UniqueConstraintListResponse(uniqueConstraints));
    }

    private static Task<IResult> GetUniqueConstraintAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string tableName,
        [FromRoute] string constraintName,
        CancellationToken cancellationToken = default
    ) =>
        GetSchemaUniqueConstraintAsync(
            operationContext,
            service,
            datasourceId,
            null,
            tableName,
            constraintName,
            cancellationToken
        );

    private static async Task<IResult> GetSchemaUniqueConstraintAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string tableName,
        [FromRoute] string constraintName,
        CancellationToken cancellationToken = default
    )
    {
        var uniqueConstraint = await service
            .GetUniqueConstraintAsync(
                operationContext,
                datasourceId,
                tableName,
                constraintName,
                schemaName,
                cancellationToken
            )
            .ConfigureAwait(false);

        return Results.Ok(new UniqueConstraintResponse(uniqueConstraint));
    }

    private static Task<IResult> CreateUniqueConstraintAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string tableName,
        [FromBody] UniqueConstraintDto uniqueConstraint,
        CancellationToken cancellationToken = default
    ) =>
        CreateSchemaUniqueConstraintAsync(
            operationContext,
            service,
            datasourceId,
            null,
            tableName,
            uniqueConstraint,
            cancellationToken
        );

    private static async Task<IResult> CreateSchemaUniqueConstraintAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string tableName,
        [FromBody] UniqueConstraintDto uniqueConstraint,
        CancellationToken cancellationToken = default
    )
    {
        // API layer validation
        ValidationFactory
            .Object(uniqueConstraint)
            .NotNull(v => v.ColumnNames, nameof(UniqueConstraintDto.ColumnNames))
            .Custom(
                v => v.ColumnNames!.Count > 0,
                nameof(UniqueConstraintDto.ColumnNames),
                $"At least one column is required"
            )
            .Assert();

        operationContext.RequestBody = uniqueConstraint;
        operationContext.ColumnNames =
            uniqueConstraint.ColumnNames != null && uniqueConstraint.ColumnNames.Count >= 1
                ? uniqueConstraint.ColumnNames
                : null;

        var created = await service
            .CreateUniqueConstraintAsync(
                operationContext,
                datasourceId,
                tableName,
                uniqueConstraint,
                schemaName,
                cancellationToken
            )
            .ConfigureAwait(false);

        return Results.Created(
            $"{operationContext.EndpointPath?.TrimEnd('/')}/{uniqueConstraint.ConstraintName}",
            new UniqueConstraintResponse(created)
        );
    }

    private static Task<IResult> DropUniqueConstraintAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string tableName,
        [FromRoute] string constraintName,
        CancellationToken cancellationToken = default
    ) =>
        DropSchemaUniqueConstraintAsync(
            operationContext,
            service,
            datasourceId,
            null,
            tableName,
            constraintName,
            cancellationToken
        );

    private static async Task<IResult> DropSchemaUniqueConstraintAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string tableName,
        [FromRoute] string constraintName,
        CancellationToken cancellationToken = default
    )
    {
        await service
            .DropUniqueConstraintAsync(
                operationContext,
                datasourceId,
                tableName,
                constraintName,
                schemaName,
                cancellationToken
            )
            .ConfigureAwait(false);

        return Results.NoContent();
    }
}
