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
/// Extension methods for registering DapperMatic foreign key constraint endpoints.
/// </summary>
public static class ForeignKeyConstraintEndpoints
{
    /// <summary>
    /// Maps all DapperMatic foreign key constraint endpoints to the specified route builder.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <param name="basePath">The base path for the API endpoints. Defaults to "/api/dm".</param>
    /// <returns>The route builder for method chaining.</returns>
    public static IEndpointRouteBuilder MapDapperMaticForeignKeyConstraintEndpoints(
        this IEndpointRouteBuilder app,
        string? basePath = null
    )
    {
        // Register default schema endpoints (for single-schema scenarios)
        var defaultGroup = app.MapDapperMaticEndpointGroup(
            basePath,
            "/d/{datasourceId}/t/{tableName}/foreign-key-constraints",
            OperationTags.DatasourceTables
        );

        RegisterForeignKeyConstraintEndpoints(defaultGroup, "DefaultSchema", isSchemaSpecific: false);

        // Register schema-specific endpoints (for multi-tenant scenarios)
        var schemaGroup = app.MapDapperMaticEndpointGroup(
            basePath,
            "/d/{datasourceId}/s/{schemaName}/t/{tableName}/foreign-key-constraints",
            OperationTags.DatasourceTables
        );

        RegisterForeignKeyConstraintEndpoints(schemaGroup, "Schema", isSchemaSpecific: true);

        return app;
    }

    private static void RegisterForeignKeyConstraintEndpoints(
        RouteGroupBuilder group,
        string namePrefix,
        bool isSchemaSpecific
    )
    {
        var schemaText = isSchemaSpecific ? "a specific schema" : "the default schema";
        var schemaInText = isSchemaSpecific ? "in a specific schema" : "in the default schema";

        // Foreign key constraint endpoints
        group
            .MapGet("/", isSchemaSpecific ? ListSchemaForeignKeyConstraintsAsync : ListForeignKeyConstraintsAsync)
            .WithName($"List{namePrefix}ForeignKeyConstraints")
            .WithSummary($"Gets all foreign key constraints for a table {schemaInText}")
            .Produces<ForeignKeyListResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapGet(
                "/{constraintName}",
                isSchemaSpecific ? GetSchemaForeignKeyConstraintAsync : GetForeignKeyConstraintAsync
            )
            .WithName($"Get{namePrefix}ForeignKeyConstraint")
            .WithSummary($"Gets a specific foreign key constraint from a table {schemaInText}")
            .Produces<ForeignKeyResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapPost("/", isSchemaSpecific ? CreateSchemaForeignKeyConstraintAsync : CreateForeignKeyConstraintAsync)
            .WithName($"Create{namePrefix}ForeignKeyConstraint")
            .WithSummary($"Creates a foreign key constraint on a table {schemaInText}")
            .Produces<ForeignKeyResponse>((int)HttpStatusCode.Created)
            .Produces((int)HttpStatusCode.Conflict)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapDelete(
                "/{constraintName}",
                isSchemaSpecific ? DropSchemaForeignKeyConstraintAsync : DropForeignKeyConstraintAsync
            )
            .WithName($"Drop{namePrefix}ForeignKeyConstraint")
            .WithSummary($"Drops a foreign key constraint from a table {schemaInText}")
            .Produces<ForeignKeyResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);
    }

    // Foreign key constraint endpoint implementations
    private static Task<IResult> ListForeignKeyConstraintsAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string tableName,
        CancellationToken cancellationToken = default
    ) =>
        ListSchemaForeignKeyConstraintsAsync(
            operationContext,
            service,
            datasourceId,
            null,
            tableName,
            cancellationToken
        );

    private static async Task<IResult> ListSchemaForeignKeyConstraintsAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string tableName,
        CancellationToken cancellationToken = default
    )
    {
        var foreignKeys = await service
            .GetForeignKeyConstraintsAsync(operationContext, datasourceId, tableName, schemaName, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(new ForeignKeyListResponse(foreignKeys));
    }

    private static Task<IResult> GetForeignKeyConstraintAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string tableName,
        [FromRoute] string constraintName,
        CancellationToken cancellationToken = default
    ) =>
        GetSchemaForeignKeyConstraintAsync(
            operationContext,
            service,
            datasourceId,
            null,
            tableName,
            constraintName,
            cancellationToken
        );

    private static async Task<IResult> GetSchemaForeignKeyConstraintAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string tableName,
        [FromRoute] string constraintName,
        CancellationToken cancellationToken = default
    )
    {
        var foreignKey = await service
            .GetForeignKeyConstraintAsync(
                operationContext,
                datasourceId,
                tableName,
                constraintName,
                schemaName,
                cancellationToken
            )
            .ConfigureAwait(false);

        return Results.Ok(new ForeignKeyResponse(foreignKey));
    }

    private static Task<IResult> CreateForeignKeyConstraintAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string tableName,
        [FromBody] ForeignKeyConstraintDto foreignKeyConstraint,
        CancellationToken cancellationToken = default
    ) =>
        CreateSchemaForeignKeyConstraintAsync(
            operationContext,
            service,
            datasourceId,
            null,
            tableName,
            foreignKeyConstraint,
            cancellationToken
        );

    private static async Task<IResult> CreateSchemaForeignKeyConstraintAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string tableName,
        [FromBody] ForeignKeyConstraintDto foreignKeyConstraint,
        CancellationToken cancellationToken = default
    )
    {
        // API layer validation
        Validate
            .Object(foreignKeyConstraint)
            .NotNull(v => v.ColumnNames, nameof(ForeignKeyConstraintDto.ColumnNames))
            .Custom(
                v => v.ColumnNames!.Count > 0,
                nameof(ForeignKeyConstraintDto.ColumnNames),
                $"At least one column is required"
            );

        operationContext.RequestBody = foreignKeyConstraint;
        operationContext.ColumnNames =
            foreignKeyConstraint.ColumnNames != null && foreignKeyConstraint.ColumnNames.Count >= 1
                ? foreignKeyConstraint.ColumnNames
                : null;
        if (!string.IsNullOrWhiteSpace(foreignKeyConstraint.ConstraintName))
        {
            operationContext.ConstraintName = foreignKeyConstraint.ConstraintName;
        }

        var result = await service
            .CreateForeignKeyConstraintAsync(
                operationContext,
                datasourceId,
                tableName,
                foreignKeyConstraint,
                schemaName,
                cancellationToken
            )
            .ConfigureAwait(false);

        return Results.Created(
            $"{operationContext.EndpointPath?.TrimEnd('/')}/{foreignKeyConstraint.ConstraintName}",
            new ForeignKeyResponse(foreignKeyConstraint)
        );
    }

    private static Task<IResult> DropForeignKeyConstraintAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string tableName,
        [FromRoute] string constraintName,
        CancellationToken cancellationToken = default
    ) =>
        DropSchemaForeignKeyConstraintAsync(
            operationContext,
            service,
            datasourceId,
            null,
            tableName,
            constraintName,
            cancellationToken
        );

    private static async Task<IResult> DropSchemaForeignKeyConstraintAsync(
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
            .DropForeignKeyConstraintAsync(
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
