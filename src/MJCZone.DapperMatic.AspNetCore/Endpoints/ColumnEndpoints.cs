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
/// Extension methods for registering DapperMatic column endpoints.
/// </summary>
public static class ColumnEndpoints
{
    /// <summary>
    /// Maps all DapperMatic column endpoints to the specified route builder.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <param name="basePath">The base path for the API endpoints. Defaults to "/api/dm".</param>
    /// <returns>The route builder for method chaining.</returns>
    public static IEndpointRouteBuilder MapDapperMaticColumnEndpoints(
        this IEndpointRouteBuilder app,
        string? basePath = null
    )
    {
        // Register default schema endpoints (for single-schema scenarios)
        var defaultGroup = app.MapDapperMaticEndpointGroup(
            basePath,
            "/d/{datasourceId}/t/{tableName}/columns",
            OperationTags.DatasourceTables
        );

        RegisterColumnEndpoints(defaultGroup, "DefaultSchema", isSchemaSpecific: false);

        // Register schema-specific endpoints (for multi-tenant scenarios)
        var schemaGroup = app.MapDapperMaticEndpointGroup(
            basePath,
            "/d/{datasourceId}/s/{schemaName}/t/{tableName}/columns",
            OperationTags.DatasourceTables
        );

        RegisterColumnEndpoints(schemaGroup, "Schema", isSchemaSpecific: true);

        return app;
    }

    private static void RegisterColumnEndpoints(RouteGroupBuilder group, string namePrefix, bool isSchemaSpecific)
    {
        var schemaText = isSchemaSpecific ? "a specific schema" : "the default schema";
        var schemaInText = isSchemaSpecific ? "in a specific schema" : "in the default schema";

        // Column management endpoints
        group
            .MapGet("/", isSchemaSpecific ? ListSchemaColumnsAsync : ListColumnsAsync)
            .WithName($"List{namePrefix}Columns")
            .WithSummary($"Gets all columns for a table {schemaInText}")
            .Produces<ColumnListResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapGet("/{columnName}", isSchemaSpecific ? GetSchemaColumnAsync : GetColumnAsync)
            .WithName($"Get{namePrefix}Column")
            .WithSummary($"Gets a specific column from a table {schemaInText}")
            .Produces<ColumnResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapPost("/", isSchemaSpecific ? CreateSchemaColumnAsync : CreateColumnAsync)
            .WithName($"Add{namePrefix}Column")
            .WithSummary($"Adds a new column to a table {schemaInText}")
            .Produces<ColumnResponse>((int)HttpStatusCode.Created)
            .Produces((int)HttpStatusCode.Conflict)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapPut("/{columnName}", isSchemaSpecific ? UpdateSchemaColumnAsync : UpdateColumnAsync)
            .WithName($"Update{namePrefix}Column")
            .WithSummary($"Updates a column in a table {schemaInText}")
            .Produces<ColumnResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapDelete("/{columnName}", isSchemaSpecific ? DropSchemaColumnAsync : DropColumnAsync)
            .WithName($"Drop{namePrefix}Column")
            .WithSummary($"Drops a column from a table {schemaInText}")
            .Produces((int)HttpStatusCode.NoContent)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);
    }

    // Column management endpoint implementations
    private static Task<IResult> ListColumnsAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string tableName,
        CancellationToken cancellationToken = default
    ) => ListSchemaColumnsAsync(operationContext, service, datasourceId, null, tableName, cancellationToken);

    private static async Task<IResult> ListSchemaColumnsAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string tableName,
        CancellationToken cancellationToken = default
    )
    {
        var columns = await service
            .GetColumnsAsync(operationContext, datasourceId, tableName, schemaName, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(new ColumnListResponse(columns));
    }

    private static Task<IResult> GetColumnAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string tableName,
        [FromRoute] string columnName,
        CancellationToken cancellationToken = default
    ) => GetSchemaColumnAsync(operationContext, service, datasourceId, null, tableName, columnName, cancellationToken);

    private static async Task<IResult> GetSchemaColumnAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string tableName,
        [FromRoute] string columnName,
        CancellationToken cancellationToken = default
    )
    {
        var column = await service
            .GetColumnAsync(operationContext, datasourceId, tableName, columnName, schemaName, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(new ColumnResponse(column));
    }

    private static Task<IResult> CreateColumnAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string tableName,
        [FromBody] ColumnDto column,
        CancellationToken cancellationToken = default
    ) => CreateSchemaColumnAsync(operationContext, service, datasourceId, null, tableName, column, cancellationToken);

    private static async Task<IResult> CreateSchemaColumnAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string tableName,
        [FromBody] ColumnDto column,
        CancellationToken cancellationToken = default
    )
    {
        // API layer validation
        Validate
            .Object(column)
            .NotNullOrWhiteSpace(c => c.ColumnName, nameof(ColumnDto.ColumnName))
            .NotNullOrWhiteSpace(c => c.ProviderDataType, nameof(ColumnDto.ProviderDataType))
            .Assert();

        operationContext.RequestBody = column;
        operationContext.ColumnNames = [column.ColumnName];

        var result = await service
            .AddColumnAsync(operationContext, datasourceId, tableName, column, schemaName, cancellationToken)
            .ConfigureAwait(false);

        return Results.Created(
            $"{operationContext.EndpointPath?.TrimEnd('/')}/{column.ColumnName}",
            new ColumnResponse(result)
        );
    }

    private static Task<IResult> UpdateColumnAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string tableName,
        [FromRoute] string columnName,
        [FromBody] ColumnDto column,
        CancellationToken cancellationToken = default
    ) =>
        UpdateSchemaColumnAsync(
            operationContext,
            service,
            datasourceId,
            null,
            tableName,
            columnName,
            column,
            cancellationToken
        );

    private static async Task<IResult> UpdateSchemaColumnAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string tableName,
        [FromRoute] string columnName,
        [FromBody] ColumnDto column,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(column.ColumnName))
        {
            column.ColumnName = columnName;
        }

        // API layer validation
        Validate.Object(column).NotNullOrWhiteSpace(r => r.ColumnName, nameof(ColumnDto.ColumnName)).Assert();

        operationContext.RequestBody = column;

        if (!columnName.Equals(column.ColumnName, StringComparison.OrdinalIgnoreCase))
        {
            operationContext.Properties ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            operationContext.Properties["NewColumnName"] = column.ColumnName!;
            var renamedColumn = await service
                .RenameColumnAsync(
                    operationContext,
                    datasourceId,
                    tableName,
                    columnName,
                    column.ColumnName!,
                    schemaName,
                    cancellationToken
                )
                .ConfigureAwait(false);

            return Results.Ok(new ColumnResponse(renamedColumn));
        }
        else
        {
            var existingColumn = await service
                .GetColumnAsync(operationContext, datasourceId, tableName, columnName, schemaName, cancellationToken)
                .ConfigureAwait(false);

            return Results.Ok(new ColumnResponse(existingColumn));
        }
    }

    private static Task<IResult> DropColumnAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string tableName,
        [FromRoute] string columnName,
        CancellationToken cancellationToken = default
    ) => DropSchemaColumnAsync(operationContext, service, datasourceId, null, tableName, columnName, cancellationToken);

    private static async Task<IResult> DropSchemaColumnAsync(
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
            .DropColumnAsync(operationContext, datasourceId, tableName, columnName, schemaName, cancellationToken)
            .ConfigureAwait(false);

        return Results.NoContent();
    }
}
