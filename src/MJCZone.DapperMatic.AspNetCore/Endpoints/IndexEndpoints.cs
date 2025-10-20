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
/// Extension methods for registering DapperMatic index endpoints.
/// </summary>
public static class IndexEndpoints
{
    /// <summary>
    /// Maps all DapperMatic index endpoints to the specified route builder.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <param name="basePath">The base path for the API endpoints. Defaults to "/api/dm".</param>
    /// <returns>The route builder for method chaining.</returns>
    public static IEndpointRouteBuilder MapDapperMaticIndexEndpoints(
        this IEndpointRouteBuilder app,
        string? basePath = null
    )
    {
        // Register default schema endpoints (for single-schema scenarios)
        var defaultGroup = app.MapDapperMaticEndpointGroup(
            basePath,
            "/d/{datasourceId}/t/{tableName}/indexes",
            OperationTags.DatasourceTables
        );

        RegisterIndexEndpoints(defaultGroup, "DefaultSchema", isSchemaSpecific: false);

        // Register schema-specific endpoints (for multi-tenant scenarios)
        var schemaGroup = app.MapDapperMaticEndpointGroup(
            basePath,
            "/d/{datasourceId}/s/{schemaName}/t/{tableName}/indexes",
            OperationTags.DatasourceTables
        );

        RegisterIndexEndpoints(schemaGroup, "Schema", isSchemaSpecific: true);

        return app;
    }

    private static void RegisterIndexEndpoints(RouteGroupBuilder group, string namePrefix, bool isSchemaSpecific)
    {
        var schemaText = isSchemaSpecific ? "a specific schema" : "the default schema";
        var schemaInText = isSchemaSpecific ? "in a specific schema" : "in the default schema";

        // Index management endpoints
        group
            .MapGet("/", isSchemaSpecific ? ListSchemaIndexesAsync : ListIndexesAsync)
            .WithName($"List{namePrefix}Indexes")
            .WithSummary($"Gets all indexes for a table {schemaInText}")
            .Produces<IndexListResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapGet("/{indexName}", isSchemaSpecific ? GetSchemaIndexAsync : GetIndexAsync)
            .WithName($"Get{namePrefix}Index")
            .WithSummary($"Gets a specific index from a table {schemaInText}")
            .Produces<IndexResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapPost("/", isSchemaSpecific ? CreateSchemaIndexAsync : CreateIndexAsync)
            .WithName($"Create{namePrefix}Index")
            .WithSummary($"Creates a new index on a table {schemaInText}")
            .Produces<IndexResponse>((int)HttpStatusCode.Created)
            .Produces((int)HttpStatusCode.Conflict)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapDelete("/{indexName}", isSchemaSpecific ? DropSchemaIndexAsync : DropIndexAsync)
            .WithName($"Drop{namePrefix}Index")
            .WithSummary($"Drops an index from a table {schemaInText}")
            .Produces<IndexResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);
    }

    // Index management endpoint implementations
    private static Task<IResult> ListIndexesAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string tableName,
        CancellationToken cancellationToken = default
    ) => ListSchemaIndexesAsync(operationContext, service, datasourceId, null, tableName, cancellationToken);

    private static async Task<IResult> ListSchemaIndexesAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string tableName,
        CancellationToken cancellationToken = default
    )
    {
        var indexes = await service
            .GetIndexesAsync(operationContext, datasourceId, tableName, schemaName, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(new IndexListResponse(indexes));
    }

    private static Task<IResult> GetIndexAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string tableName,
        [FromRoute] string indexName,
        CancellationToken cancellationToken = default
    ) => GetSchemaIndexAsync(operationContext, service, datasourceId, null, tableName, indexName, cancellationToken);

    private static async Task<IResult> GetSchemaIndexAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string tableName,
        [FromRoute] string indexName,
        CancellationToken cancellationToken = default
    )
    {
        var index = await service
            .GetIndexAsync(operationContext, datasourceId, tableName, indexName, schemaName, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(new IndexResponse(index));
    }

    private static Task<IResult> CreateIndexAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string tableName,
        [FromBody] IndexDto index,
        CancellationToken cancellationToken = default
    ) => CreateSchemaIndexAsync(operationContext, service, datasourceId, null, tableName, index, cancellationToken);

    private static async Task<IResult> CreateSchemaIndexAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string tableName,
        [FromBody] IndexDto index,
        CancellationToken cancellationToken = default
    )
    {
        // API layer validation
        Validate
            .Object(index)
            .NotNull(v => v.ColumnNames, nameof(IndexDto.ColumnNames))
            .Custom(v => v.ColumnNames!.Count > 0, nameof(IndexDto.ColumnNames), $"At least one column is required")
            .Assert();

        operationContext.RequestBody = index;
        operationContext.ColumnNames =
            index.ColumnNames != null && index.ColumnNames.Count >= 1 ? index.ColumnNames : null;
        if (!string.IsNullOrWhiteSpace(index.IndexName))
        {
            operationContext.IndexName = index.IndexName;
        }

        var created = await service
            .CreateIndexAsync(operationContext, datasourceId, tableName, index, schemaName, cancellationToken)
            .ConfigureAwait(false);

        return Results.Created(
            $"{operationContext.EndpointPath?.TrimEnd('/')}/{index.IndexName}",
            new IndexResponse(created)
        );
    }

    private static Task<IResult> DropIndexAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string tableName,
        [FromRoute] string indexName,
        CancellationToken cancellationToken = default
    ) => DropSchemaIndexAsync(operationContext, service, datasourceId, null, tableName, indexName, cancellationToken);

    private static async Task<IResult> DropSchemaIndexAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string tableName,
        [FromRoute] string indexName,
        CancellationToken cancellationToken = default
    )
    {
        await service
            .DropIndexAsync(operationContext, datasourceId, tableName, indexName, schemaName, cancellationToken)
            .ConfigureAwait(false);

        return Results.NoContent();
    }
}
