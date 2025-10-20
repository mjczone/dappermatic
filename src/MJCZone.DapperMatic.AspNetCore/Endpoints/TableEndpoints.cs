// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.OpenApi.Any;
using MJCZone.DapperMatic.AspNetCore.Extensions;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Models.Responses;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.AspNetCore.Services;
using MJCZone.DapperMatic.AspNetCore.Validation;

namespace MJCZone.DapperMatic.AspNetCore.Endpoints;

/// <summary>
/// Extension methods for registering DapperMatic table endpoints.
/// </summary>
/// <remarks>
/// Registers both default schema endpoints (/d/{datasourceId}/t) and
/// schema-specific endpoints (/d/{datasourceId}/s/{schemaName}/t).
/// </remarks>
public static class TableEndpoints
{
    /// <summary>
    /// Maps all DapperMatic table endpoints to the specified route builder.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <param name="basePath">The base path for the API endpoints. Defaults to "/api/dm".</param>
    /// <returns>The route builder for method chaining.</returns>
    /// <remarks>
    /// This method registers two sets of endpoints:
    /// 1. Default schema endpoints at /d/{datasourceId}/t for single-schema scenarios.
    /// 2. Schema-specific endpoints at /d/{datasourceId}/s/{schemaName}/t for multi-tenant scenarios.
    /// </remarks>
    public static IEndpointRouteBuilder MapDapperMaticTableEndpoints(
        this IEndpointRouteBuilder app,
        string? basePath = null
    )
    {
        // Register default schema endpoints (for single-schema scenarios)
        var defaultGroup = app.MapDapperMaticEndpointGroup(
            basePath,
            "/d/{datasourceId}/t",
            OperationTags.DatasourceTables
        );

        RegisterTableEndpoints(defaultGroup, "DefaultSchema", isSchemaSpecific: false);

        // Register schema-specific endpoints (for multi-tenant scenarios)
        var schemaGroup = app.MapDapperMaticEndpointGroup(
            basePath,
            "/d/{datasourceId}/s/{schemaName}/t",
            OperationTags.DatasourceTables
        );

        RegisterTableEndpoints(schemaGroup, "Schema", isSchemaSpecific: true);

        return app;
    }

    private static void RegisterTableEndpoints(RouteGroupBuilder group, string namePrefix, bool isSchemaSpecific)
    {
        var schemaText = isSchemaSpecific ? "a specific schema" : "the default schema";
        var schemaInText = isSchemaSpecific ? "in a specific schema" : "in the default schema";

        // List all tables
        group
            .MapGet("/", isSchemaSpecific ? ListSchemaTablesAsync : ListTablesAsync)
            .WithName($"List{namePrefix}Tables")
            .WithSummary($"Gets all tables for {schemaText}")
            .WithOpenApi(operation =>
            {
                var includeParam = operation.Parameters.FirstOrDefault(p => p.Name == "include");
                if (includeParam != null)
                {
                    includeParam.Description =
                        "Comma-separated list of fields to include in the response. Use 'columns' to include column definitions, 'indexes' for indexes, 'constraints' for constraints, or '*' to include all fields. By default, only basic table information is returned.";
                    includeParam.Example = new OpenApiString("columns,indexes");
                }
                return operation;
            })
            .Produces<TableListResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Get specific table
        group
            .MapGet("/{tableName}", isSchemaSpecific ? GetSchemaTableAsync : GetTableAsync)
            .WithName($"Get{namePrefix}Table")
            .WithSummary($"Gets a table by name from {schemaText}")
            .WithOpenApi(operation =>
            {
                var includeParam = operation.Parameters.FirstOrDefault(p => p.Name == "include");
                if (includeParam != null)
                {
                    includeParam.Description =
                        "Comma-separated list of fields to include in the response. Use 'columns' to include column definitions, 'indexes' for indexes, 'constraints' for constraints, or '*' to include all fields.";
                    includeParam.Example = new OpenApiString("columns,indexes,constraints");
                }
                return operation;
            })
            .Produces<TableResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Create new table
        group
            .MapPost("/", isSchemaSpecific ? CreateSchemaTableAsync : CreateTableAsync)
            .WithName($"Create{namePrefix}Table")
            .WithSummary($"Creates a new table {schemaInText}")
            .Produces<TableResponse>((int)HttpStatusCode.Created)
            .Produces((int)HttpStatusCode.Conflict)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Update table
        group
            .MapPut("/{tableName}", isSchemaSpecific ? UpdateSchemaTableAsync : UpdateTableAsync)
            .WithName($"Update{namePrefix}Table")
            .WithSummary($"Updates a table {schemaInText}")
            .Produces<TableResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Drop table
        group
            .MapDelete("/{tableName}", isSchemaSpecific ? DropSchemaTableAsync : DropTableAsync)
            .WithName($"Drop{namePrefix}Table")
            .WithSummary($"Drops a table from {schemaText}")
            .Produces<TableResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Check if table exists
        group
            .MapGet("/{tableName}/exists", isSchemaSpecific ? SchemaTableExistsAsync : TableExistsAsync)
            .WithName($"{namePrefix}TableExists")
            .WithSummary($"Checks if a table exists {schemaInText}")
            .Produces<TableExistsResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Query table via GET with URL parameters
        group
            .MapGet("/{tableName}/query", isSchemaSpecific ? QuerySchemaTableViaGetAsync : QueryTableViaGetAsync)
            .WithName($"Query{namePrefix}TableViaGet")
            .WithSummary($"Queries a table {schemaInText} using URL parameters")
            .Produces<QueryResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Query table with filtering, sorting, and pagination
        group
            .MapPost("/{tableName}/query", isSchemaSpecific ? QuerySchemaTableAsync : QueryTableAsync)
            .WithName($"Query{namePrefix}Table")
            .WithSummary($"Queries a table {schemaInText} with filtering, sorting, and pagination")
            .Produces<QueryResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);
    }

    private static Task<IResult> ListTablesAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromQuery] string? include,
        [FromQuery] string? filter,
        CancellationToken cancellationToken = default
    ) => ListSchemaTablesAsync(operationContext, service, datasourceId, null, include, filter, cancellationToken);

    private static async Task<IResult> ListSchemaTablesAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromQuery] string? include,
        [FromQuery] string? filter,
        CancellationToken cancellationToken = default
    )
    {
        // Parse include parameter to determine what to include
        var includeColumns =
            !string.IsNullOrWhiteSpace(include)
            && (
                include.Contains("columns", StringComparison.OrdinalIgnoreCase)
                || include.Contains('*', StringComparison.OrdinalIgnoreCase)
            );
        var includeIndexes =
            !string.IsNullOrWhiteSpace(include)
            && (
                include.Contains("indexes", StringComparison.OrdinalIgnoreCase)
                || include.Contains('*', StringComparison.OrdinalIgnoreCase)
            );
        var includeConstraints =
            !string.IsNullOrWhiteSpace(include)
            && (
                include.Contains("constraints", StringComparison.OrdinalIgnoreCase)
                || include.Contains('*', StringComparison.OrdinalIgnoreCase)
            );

        var tables = await service
            .GetTablesAsync(
                operationContext,
                datasourceId,
                schemaName,
                includeColumns,
                includeIndexes,
                includeConstraints,
                cancellationToken
            )
            .ConfigureAwait(false);

        // Apply filter if provided
        if (!string.IsNullOrWhiteSpace(filter))
        {
            tables = tables.Where(t =>
                t.TableName != null && t.TableName.Contains(filter, StringComparison.OrdinalIgnoreCase)
            );
        }

        return Results.Ok(new TableListResponse(tables));
    }

    private static Task<IResult> GetTableAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string tableName,
        [FromQuery] string? include,
        CancellationToken cancellationToken = default
    ) => GetSchemaTableAsync(operationContext, service, datasourceId, null, tableName, include, cancellationToken);

    private static async Task<IResult> GetSchemaTableAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string tableName,
        [FromQuery] string? include,
        CancellationToken cancellationToken = default
    )
    {
        // Parse include parameter to determine what to include
        var includeColumns =
            string.IsNullOrWhiteSpace(include)
            || include.Contains("columns", StringComparison.OrdinalIgnoreCase)
            || include.Contains('*', StringComparison.OrdinalIgnoreCase);
        var includeIndexes =
            string.IsNullOrWhiteSpace(include)
            || include.Contains("indexes", StringComparison.OrdinalIgnoreCase)
            || include.Contains('*', StringComparison.OrdinalIgnoreCase);
        var includeConstraints =
            string.IsNullOrWhiteSpace(include)
            || include.Contains("constraints", StringComparison.OrdinalIgnoreCase)
            || include.Contains('*', StringComparison.OrdinalIgnoreCase);

        var table = await service
            .GetTableAsync(
                operationContext,
                datasourceId,
                tableName,
                schemaName,
                includeColumns,
                includeIndexes,
                includeConstraints,
                cancellationToken
            )
            .ConfigureAwait(false);

        return Results.Ok(new TableResponse(table));
    }

    private static Task<IResult> CreateTableAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromBody] TableDto table,
        CancellationToken cancellationToken = default
    ) => CreateSchemaTableAsync(operationContext, service, datasourceId, null, table, cancellationToken);

    private static async Task<IResult> CreateSchemaTableAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromBody] TableDto table,
        CancellationToken cancellationToken = default
    )
    {
        // API layer validation
        Validate
            .Object(table)
            .NotNullOrWhiteSpace(t => t.TableName, nameof(TableDto.TableName))
            .MaxLength(v => v.TableName, 128, nameof(TableDto.TableName), inclusive: true)
            .MinLength(v => v.TableName, 1, nameof(TableDto.TableName), inclusive: true)
            .NotNull(v => v.Columns, nameof(TableDto.Columns))
            .Custom(v => v.Columns!.Count > 0, nameof(TableDto.Columns), $"At least one column is required")
            .Assert();

        // Ensure schema name from DTO matches route parameter
        table.SchemaName = schemaName;

        operationContext.RequestBody = table;
        operationContext.TableName = table.TableName!.Trim();
        operationContext.ColumnNames =
            table.Columns != null && table.Columns.Count >= 1 ? table.Columns.Select(c => c.ColumnName).ToList() : null;

        var created = await service
            .CreateTableAsync(operationContext, datasourceId, table, cancellationToken)
            .ConfigureAwait(false);

        return Results.Created(
            $"{operationContext.EndpointPath?.TrimEnd('/') ?? string.Empty}/{created.TableName}",
            new TableResponse(created)
        );
    }

    private static Task<IResult> UpdateTableAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string tableName,
        [FromBody] TableDto updates,
        CancellationToken cancellationToken = default
    ) => UpdateSchemaTableAsync(operationContext, service, datasourceId, null, tableName, updates, cancellationToken);

    private static async Task<IResult> UpdateSchemaTableAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string tableName,
        [FromBody] TableDto updates,
        CancellationToken cancellationToken = default
    )
    {
        updates.SchemaName = schemaName;
        if (string.IsNullOrWhiteSpace(updates.TableName))
        {
            updates.TableName = tableName;
        }

        // API layer validation
        Validate
            .Object(updates)
            .MaxLength(u => u.TableName, 128, nameof(TableDto.TableName), inclusive: true)
            .Assert();

        // Ensure schema name from DTO matches route parameter
        updates.SchemaName = schemaName;

        operationContext.RequestBody = updates;

        // Check if this is a rename (TableName in body differs from route parameter)
        var isRename = !string.IsNullOrWhiteSpace(updates.TableName) && updates.TableName != tableName;
        var hasPropertyUpdates =
            (updates.Columns != null && updates.Columns.Count > 0)
            || (updates.Indexes != null && updates.Indexes.Count > 0)
            || (updates.ForeignKeyConstraints != null && updates.ForeignKeyConstraints.Count > 0)
            || (updates.CheckConstraints != null && updates.CheckConstraints.Count > 0)
            || (updates.UniqueConstraints != null && updates.UniqueConstraints.Count > 0)
            || (updates.DefaultConstraints != null && updates.DefaultConstraints.Count > 0);

        if (!isRename && !hasPropertyUpdates)
        {
            throw new InvalidOperationException("No changes provided - TableName or other additions must be specified");
        }

        var currentTableName = tableName;

        TableDto? updated = null;

        // Handle property updates first
        if (hasPropertyUpdates)
        {
            updated = await service
                .UpdateTableAsync(operationContext, datasourceId, currentTableName, updates, cancellationToken)
                .ConfigureAwait(false);
        }

        // Handle rename separately if needed
        if (isRename)
        {
            operationContext.Properties ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            operationContext.Properties["NewTableName"] = updates.TableName!;
            var renamed = await service
                .RenameTableAsync(
                    operationContext,
                    datasourceId,
                    currentTableName,
                    updates.TableName!,
                    schemaName,
                    cancellationToken
                )
                .ConfigureAwait(false);

            return Results.Ok(new TableResponse(renamed));
        }

        // Get the updated table if only properties were changed
        updated ??= await service
            .GetTableAsync(
                operationContext,
                datasourceId,
                currentTableName,
                schemaName,
                includeColumns: true,
                includeIndexes: true,
                includeConstraints: true,
                cancellationToken
            )
            .ConfigureAwait(false);

        return Results.Ok(new TableResponse(updated));
    }

    private static Task<IResult> DropTableAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string tableName,
        CancellationToken cancellationToken = default
    ) => DropSchemaTableAsync(operationContext, service, datasourceId, null, tableName, cancellationToken);

    private static async Task<IResult> DropSchemaTableAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string tableName,
        CancellationToken cancellationToken = default
    )
    {
        await service
            .DropTableAsync(operationContext, datasourceId, tableName, schemaName, cancellationToken)
            .ConfigureAwait(false);

        return Results.NoContent();
    }

    private static Task<IResult> TableExistsAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string tableName,
        CancellationToken cancellationToken = default
    ) => SchemaTableExistsAsync(operationContext, service, datasourceId, null, tableName, cancellationToken);

    private static async Task<IResult> SchemaTableExistsAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string tableName,
        CancellationToken cancellationToken = default
    )
    {
        var exists = await service
            .TableExistsAsync(operationContext, datasourceId, tableName, schemaName, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(new TableExistsResponse(exists));
    }

    private static Task<IResult> QueryTableAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string tableName,
        [FromBody] QueryDto request,
        CancellationToken cancellationToken = default
    ) => QuerySchemaTableAsync(operationContext, service, datasourceId, null, tableName, request, cancellationToken);

    private static async Task<IResult> QuerySchemaTableAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string tableName,
        [FromBody] QueryDto request,
        CancellationToken cancellationToken = default
    )
    {
        operationContext.RequestBody = request;

        var queryResult = await service
            .QueryTableAsync(operationContext, datasourceId, tableName, request, schemaName, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(
            new QueryResponse(queryResult.Data) { Pagination = queryResult.Pagination, Fields = queryResult.Fields }
        );
    }

    private static Task<IResult> QueryTableViaGetAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string tableName,
        CancellationToken cancellationToken = default
    ) => QuerySchemaTableViaGetAsync(operationContext, service, datasourceId, null, tableName, cancellationToken);

    private static async Task<IResult> QuerySchemaTableViaGetAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string tableName,
        CancellationToken cancellationToken = default
    )
    {
        var request = QueryDto.FromQueryParameters(operationContext.QueryParameters ?? []);
        if (!string.IsNullOrWhiteSpace(schemaName))
        {
            // Set it on the request to pass it down
            request.SchemaName = schemaName;
        }
        operationContext.RequestBody = request;

        var queryResult = await service
            .QueryTableAsync(operationContext, datasourceId, tableName, request, schemaName, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(
            new QueryResponse(queryResult.Data) { Pagination = queryResult.Pagination, Fields = queryResult.Fields }
        );
    }
}
