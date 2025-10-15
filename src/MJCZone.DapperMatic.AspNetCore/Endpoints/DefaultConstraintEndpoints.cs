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
/// Extension methods for registering DapperMatic default constraint endpoints.
/// </summary>
public static class DefaultConstraintEndpoints
{
    /// <summary>
    /// Maps all DapperMatic default constraint endpoints to the specified route builder.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <param name="basePath">The base path for the API endpoints. Defaults to "/api/dm".</param>
    /// <returns>The route builder for method chaining.</returns>
    public static IEndpointRouteBuilder MapDapperMaticDefaultConstraintEndpoints(
        this IEndpointRouteBuilder app,
        string? basePath = null
    )
    {
        // Register default schema endpoints (for single-schema scenarios)
        var defaultGroup = app.MapDapperMaticEndpointGroup(
            basePath,
            "/d/{datasourceId}/t/{tableName}/default-constraints",
            OperationTags.DatasourceTables
        );

        RegisterDefaultConstraintEndpoints(defaultGroup, "DefaultSchema", isSchemaSpecific: false);

        // Register schema-specific endpoints (for multi-tenant scenarios)
        var schemaGroup = app.MapDapperMaticEndpointGroup(
            basePath,
            "/d/{datasourceId}/s/{schemaName}/t/{tableName}/default-constraints",
            OperationTags.DatasourceTables
        );

        RegisterDefaultConstraintEndpoints(schemaGroup, "Schema", isSchemaSpecific: true);

        return app;
    }

    private static void RegisterDefaultConstraintEndpoints(
        RouteGroupBuilder group,
        string namePrefix,
        bool isSchemaSpecific
    )
    {
        var schemaText = isSchemaSpecific ? "a specific schema" : "the default schema";
        var schemaInText = isSchemaSpecific ? "in a specific schema" : "in the default schema";

        // Default constraint endpoints
        group
            .MapGet(
                "/",
                isSchemaSpecific ? ListSchemaDefaultConstraintsAsync : ListDefaultConstraintsAsync
            )
            .WithName($"List{namePrefix}DefaultConstraints")
            .WithSummary($"Gets all default constraints for a table {schemaInText}")
            .Produces<DefaultConstraintListResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapGet(
                "/{constraintName}",
                isSchemaSpecific ? GetSchemaDefaultConstraintAsync : GetDefaultConstraintAsync
            )
            .WithName($"Get{namePrefix}DefaultConstraint")
            .WithSummary($"Gets a specific default constraint from a table {schemaInText}")
            .Produces<DefaultConstraintResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapGet(
                "/columns/{columnName}",
                isSchemaSpecific
                    ? GetSchemaDefaultConstraintOnColumnAsync
                    : GetDefaultConstraintOnColumnAsync
            )
            .WithName($"Get{namePrefix}DefaultConstraintOnColumn")
            .WithSummary(
                $"Gets a specific default constraint from a table {schemaInText} for a specific column"
            )
            .Produces<DefaultConstraintResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapPost(
                "/",
                isSchemaSpecific ? CreateSchemaDefaultConstraintAsync : CreateDefaultConstraintAsync
            )
            .WithName($"Create{namePrefix}DefaultConstraint")
            .WithSummary($"Creates a default constraint on a table {schemaInText}")
            .Produces<DefaultConstraintResponse>((int)HttpStatusCode.Created)
            .Produces((int)HttpStatusCode.Conflict)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapDelete(
                "/{constraintName}",
                isSchemaSpecific ? DropSchemaDefaultConstraintAsync : DropDefaultConstraintAsync
            )
            .WithName($"Drop{namePrefix}DefaultConstraint")
            .WithSummary($"Drops a default constraint from a table {schemaInText}")
            .Produces<DefaultConstraintResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapDelete(
                "/columns/{columnName}",
                isSchemaSpecific
                    ? DropSchemaDefaultConstraintOnColumnAsync
                    : DropDefaultConstraintOnColumnAsync
            )
            .WithName($"Drop{namePrefix}DefaultConstraintOnColumn")
            .WithSummary(
                $"Drops a default constraint from a table {schemaInText} for a specific column"
            )
            .Produces<DefaultConstraintResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);
    }

    // Default constraint endpoint implementations
    private static Task<IResult> ListDefaultConstraintsAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string tableName,
        CancellationToken cancellationToken = default
    ) =>
        ListSchemaDefaultConstraintsAsync(
            operationContext,
            service,
            datasourceId,
            null,
            tableName,
            cancellationToken
        );

    private static async Task<IResult> ListSchemaDefaultConstraintsAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string tableName,
        CancellationToken cancellationToken = default
    )
    {
        var defaultConstraints = await service
            .GetDefaultConstraintsAsync(
                operationContext,
                datasourceId,
                tableName,
                schemaName,
                cancellationToken
            )
            .ConfigureAwait(false);

        return Results.Ok(new DefaultConstraintListResponse(defaultConstraints));
    }

    private static Task<IResult> GetDefaultConstraintOnColumnAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string tableName,
        [FromRoute] string columnName,
        CancellationToken cancellationToken = default
    ) =>
        GetSchemaDefaultConstraintOnColumnAsync(
            operationContext,
            service,
            datasourceId,
            null,
            tableName,
            columnName,
            cancellationToken
        );

    private static async Task<IResult> GetSchemaDefaultConstraintOnColumnAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string tableName,
        [FromRoute] string columnName,
        CancellationToken cancellationToken = default
    )
    {
        var defaultConstraint = await service
            .GetDefaultConstraintOnColumnAsync(
                operationContext,
                datasourceId,
                tableName,
                columnName,
                schemaName,
                cancellationToken
            )
            .ConfigureAwait(false);

        return Results.Ok(new DefaultConstraintResponse(defaultConstraint));
    }

    private static Task<IResult> GetDefaultConstraintAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string tableName,
        [FromRoute] string constraintName,
        CancellationToken cancellationToken = default
    ) =>
        GetSchemaDefaultConstraintAsync(
            operationContext,
            service,
            datasourceId,
            null,
            tableName,
            constraintName,
            cancellationToken
        );

    private static async Task<IResult> GetSchemaDefaultConstraintAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string tableName,
        [FromRoute] string constraintName,
        CancellationToken cancellationToken = default
    )
    {
        var defaultConstraint = await service
            .GetDefaultConstraintAsync(
                operationContext,
                datasourceId,
                tableName,
                constraintName,
                schemaName,
                cancellationToken
            )
            .ConfigureAwait(false);

        return Results.Ok(new DefaultConstraintResponse(defaultConstraint));
    }

    private static Task<IResult> CreateDefaultConstraintAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string tableName,
        [FromBody] DefaultConstraintDto defaultConstraint,
        CancellationToken cancellationToken = default
    ) =>
        CreateSchemaDefaultConstraintAsync(
            operationContext,
            service,
            datasourceId,
            null,
            tableName,
            defaultConstraint,
            cancellationToken
        );

    private static async Task<IResult> CreateSchemaDefaultConstraintAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string tableName,
        [FromBody] DefaultConstraintDto defaultConstraint,
        CancellationToken cancellationToken = default
    )
    {
        // API layer validation
        Validate
            .Object(defaultConstraint)
            .NotNullOrWhiteSpace(v => v.ColumnName, nameof(DefaultConstraintDto.ColumnName))
            .Assert();

        operationContext.RequestBody = defaultConstraint;
        operationContext.ColumnNames = [defaultConstraint.ColumnName!];
        if (!string.IsNullOrWhiteSpace(defaultConstraint.ConstraintName))
        {
            operationContext.ConstraintName = defaultConstraint.ConstraintName;
        }

        var created = await service
            .CreateDefaultConstraintAsync(
                operationContext,
                datasourceId,
                tableName,
                defaultConstraint,
                schemaName,
                cancellationToken
            )
            .ConfigureAwait(false);

        return Results.Created(
            $"{operationContext.EndpointPath?.TrimEnd('/')}/{defaultConstraint.ConstraintName}",
            new DefaultConstraintResponse(created)
        );
    }

    private static Task<IResult> DropDefaultConstraintAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string tableName,
        [FromRoute] string constraintName,
        CancellationToken cancellationToken = default
    ) =>
        DropSchemaDefaultConstraintAsync(
            operationContext,
            service,
            datasourceId,
            null,
            tableName,
            constraintName,
            cancellationToken
        );

    private static async Task<IResult> DropSchemaDefaultConstraintAsync(
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
            .DropDefaultConstraintAsync(
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

    private static Task<IResult> DropDefaultConstraintOnColumnAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string tableName,
        [FromRoute] string columnName,
        CancellationToken cancellationToken = default
    ) =>
        DropSchemaDefaultConstraintOnColumnAsync(
            operationContext,
            service,
            datasourceId,
            null,
            tableName,
            columnName,
            cancellationToken
        );

    private static async Task<IResult> DropSchemaDefaultConstraintOnColumnAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string tableName,
        [FromRoute] string columnName,
        CancellationToken cancellationToken = default
    )
    {
        await service
            .DropDefaultConstraintOnColumnAsync(
                operationContext,
                datasourceId,
                tableName,
                columnName,
                schemaName,
                cancellationToken
            )
            .ConfigureAwait(false);

        return Results.NoContent();
    }
}
