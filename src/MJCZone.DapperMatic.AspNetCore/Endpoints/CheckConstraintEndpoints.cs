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
/// Extension methods for registering DapperMatic check constraint endpoints.
/// </summary>
public static class CheckConstraintEndpoints
{
    /// <summary>
    /// Maps all DapperMatic check constraint endpoints to the specified route builder.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <param name="basePath">The base path for the API endpoints. Defaults to "/api/dm".</param>
    /// <returns>The route builder for method chaining.</returns>
    public static IEndpointRouteBuilder MapDapperMaticCheckConstraintEndpoints(
        this IEndpointRouteBuilder app,
        string? basePath = null
    )
    {
        // Register default schema endpoints (for single-schema scenarios)
        var defaultGroup = app.MapDapperMaticEndpointGroup(
            basePath,
            "/d/{datasourceId}/t/{tableName}/check-constraints",
            OperationTags.DatasourceTables
        );

        RegisterCheckConstraintEndpoints(defaultGroup, "DefaultSchema", isSchemaSpecific: false);

        // Register schema-specific endpoints (for multi-tenant scenarios)
        var schemaGroup = app.MapDapperMaticEndpointGroup(
            basePath,
            "/d/{datasourceId}/s/{schemaName}/t/{tableName}/check-constraints",
            OperationTags.DatasourceTables
        );

        RegisterCheckConstraintEndpoints(schemaGroup, "Schema", isSchemaSpecific: true);

        return app;
    }

    private static void RegisterCheckConstraintEndpoints(
        RouteGroupBuilder group,
        string namePrefix,
        bool isSchemaSpecific
    )
    {
        var schemaText = isSchemaSpecific ? "a specific schema" : "the default schema";
        var schemaInText = isSchemaSpecific ? "in a specific schema" : "in the default schema";

        // Check constraint endpoints
        group
            .MapGet("/", isSchemaSpecific ? ListSchemaCheckConstraintsAsync : ListCheckConstraintsAsync)
            .WithName($"List{namePrefix}CheckConstraints")
            .WithSummary($"Gets all check constraints for a table {schemaInText}")
            .Produces<CheckConstraintListResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapGet("/{constraintName}", isSchemaSpecific ? GetSchemaCheckConstraintAsync : GetCheckConstraintAsync)
            .WithName($"Get{namePrefix}CheckConstraint")
            .WithSummary($"Gets a specific check constraint from a table {schemaInText}")
            .Produces<CheckConstraintResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapPost("/", isSchemaSpecific ? CreateSchemaCheckConstraintAsync : CreateCheckConstraintAsync)
            .WithName($"Create{namePrefix}CheckConstraint")
            .WithSummary($"Creates a check constraint on a table {schemaInText}")
            .Produces<CheckConstraintResponse>((int)HttpStatusCode.Created)
            .Produces((int)HttpStatusCode.Conflict)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapDelete(
                "/{constraintName}",
                isSchemaSpecific ? DropSchemaCheckConstraintAsync : DropCheckConstraintAsync
            )
            .WithName($"Drop{namePrefix}CheckConstraint")
            .WithSummary($"Drops a check constraint from a table {schemaInText}")
            .Produces<CheckConstraintResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);
    }

    // Check constraint endpoint implementations
    private static Task<IResult> ListCheckConstraintsAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string tableName,
        CancellationToken cancellationToken = default
    ) => ListSchemaCheckConstraintsAsync(operationContext, service, datasourceId, null, tableName, cancellationToken);

    private static async Task<IResult> ListSchemaCheckConstraintsAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string tableName,
        CancellationToken cancellationToken = default
    )
    {
        var checkConstraints = await service
            .GetCheckConstraintsAsync(operationContext, datasourceId, tableName, schemaName, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(new CheckConstraintListResponse(checkConstraints));
    }

    private static Task<IResult> GetCheckConstraintAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string tableName,
        [FromRoute] string constraintName,
        CancellationToken cancellationToken = default
    ) =>
        GetSchemaCheckConstraintAsync(
            operationContext,
            service,
            datasourceId,
            null,
            tableName,
            constraintName,
            cancellationToken
        );

    private static async Task<IResult> GetSchemaCheckConstraintAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string tableName,
        [FromRoute] string constraintName,
        CancellationToken cancellationToken = default
    )
    {
        var checkConstraint = await service
            .GetCheckConstraintAsync(
                operationContext,
                datasourceId,
                tableName,
                constraintName,
                schemaName,
                cancellationToken
            )
            .ConfigureAwait(false);

        return Results.Ok(new CheckConstraintResponse(checkConstraint));
    }

    private static Task<IResult> CreateCheckConstraintAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string tableName,
        [FromBody] CheckConstraintDto checkConstraint,
        CancellationToken cancellationToken = default
    ) =>
        CreateSchemaCheckConstraintAsync(
            operationContext,
            service,
            datasourceId,
            null,
            tableName,
            checkConstraint,
            cancellationToken
        );

    private static async Task<IResult> CreateSchemaCheckConstraintAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string tableName,
        [FromBody] CheckConstraintDto checkConstraint,
        CancellationToken cancellationToken = default
    )
    {
        // API layer validation
        ValidationFactory
            .Object(checkConstraint)
            .NotNullOrWhiteSpace(v => v.CheckExpression, nameof(CheckConstraintDto.CheckExpression))
            .Assert();

        operationContext.RequestBody = checkConstraint;
        if (!string.IsNullOrWhiteSpace(checkConstraint.ConstraintName))
        {
            operationContext.ConstraintName = checkConstraint.ConstraintName;
        }
        if (!string.IsNullOrWhiteSpace(checkConstraint.ColumnName))
        {
            operationContext.ColumnNames = [checkConstraint.ColumnName];
        }

        var created = await service
            .CreateCheckConstraintAsync(
                operationContext,
                datasourceId,
                tableName,
                checkConstraint,
                schemaName,
                cancellationToken
            )
            .ConfigureAwait(false);

        return Results.Created(
            $"{operationContext.EndpointPath?.TrimEnd('/')}/{checkConstraint.ConstraintName}",
            new CheckConstraintResponse(created)
        );
    }

    private static Task<IResult> DropCheckConstraintAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string tableName,
        [FromRoute] string constraintName,
        CancellationToken cancellationToken = default
    ) =>
        DropSchemaCheckConstraintAsync(
            operationContext,
            service,
            datasourceId,
            null,
            tableName,
            constraintName,
            cancellationToken
        );

    private static async Task<IResult> DropSchemaCheckConstraintAsync(
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
            .DropCheckConstraintAsync(
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
