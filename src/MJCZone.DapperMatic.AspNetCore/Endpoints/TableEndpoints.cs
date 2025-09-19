// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Claims;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.OpenApi.Any;

using MJCZone.DapperMatic.AspNetCore.Extensions;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Models.Requests;
using MJCZone.DapperMatic.AspNetCore.Models.Responses;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.AspNetCore.Services;
using MJCZone.DapperMatic.AspNetCore.Utilities;
using MJCZone.DapperMatic.Models;

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

        // Register default schema endpoints (for single-schema scenarios)
        var defaultGroup = app.MapGroup($"{basePath}/d/{{datasourceId}}/t")
            .WithTags(OperationTags.DatasourceTables)
            .WithOpenApi();

        RegisterTableEndpoints(defaultGroup, "DefaultSchema", isSchemaSpecific: false);

        // Register schema-specific endpoints (for multi-tenant scenarios)
        var schemaGroup = app.MapGroup($"{basePath}/d/{{datasourceId}}/s/{{schemaName}}/t")
            .WithTags(OperationTags.DatasourceTables)
            .WithOpenApi();

        RegisterTableEndpoints(schemaGroup, "Schema", isSchemaSpecific: true);

        return app;
    }

    private static void RegisterTableEndpoints(
        RouteGroupBuilder group,
        string namePrefix,
        bool isSchemaSpecific
    )
    {
        var schemaText = isSchemaSpecific ? "a specific schema" : "the default schema";
        var schemaInText = isSchemaSpecific ? "in a specific schema" : "in the default schema";

        // List all tables
        group
            .MapGet(
                "/",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    [Microsoft.AspNetCore.Mvc.FromQuery] string? include,
                    CancellationToken ct
                ) => ListTablesAsync(ctx, service, user, include, isSchemaSpecific, ct)
            )
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
            .MapGet(
                "/{tableName}",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string tableName,
                    [Microsoft.AspNetCore.Mvc.FromQuery] string? include,
                    CancellationToken ct
                ) => GetTableAsync(ctx, service, user, tableName, include, isSchemaSpecific, ct)
            )
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
            .MapPost(
                "/",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    CreateTableRequest request,
                    CancellationToken ct
                ) => CreateTableAsync(ctx, service, user, request, isSchemaSpecific, ct)
            )
            .WithName($"Create{namePrefix}Table")
            .WithSummary($"Creates a new table {schemaInText}")
            .Produces<TableResponse>((int)HttpStatusCode.Created)
            .Produces((int)HttpStatusCode.Conflict)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Drop table
        group
            .MapDelete(
                "/{tableName}",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string tableName,
                    CancellationToken ct
                ) => DropTableAsync(ctx, service, user, tableName, isSchemaSpecific, ct)
            )
            .WithName($"Drop{namePrefix}Table")
            .WithSummary($"Drops a table from {schemaText}")
            .Produces<TableResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Rename table
        group
            .MapPut(
                "/{tableName}",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string tableName,
                    RenameTableRequest request,
                    CancellationToken ct
                ) => RenameTableAsync(ctx, service, user, tableName, request, isSchemaSpecific, ct)
            )
            .WithName($"Rename{namePrefix}Table")
            .WithSummary($"Renames a table {schemaInText}")
            .Produces<TableResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Check if table exists
        group
            .MapGet(
                "/{tableName}/exists",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string tableName,
                    CancellationToken ct
                ) => TableExistsAsync(ctx, service, user, tableName, isSchemaSpecific, ct)
            )
            .WithName($"{namePrefix}TableExists")
            .WithSummary($"Checks if a table exists {schemaInText}")
            .Produces<TableExistsResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Query table via GET with URL parameters
        group
            .MapGet(
                "/{tableName}/query",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string tableName,
                    CancellationToken ct
                ) => QueryTableViaGetAsync(ctx, service, user, tableName, isSchemaSpecific, ct)
            )
            .WithName($"Query{namePrefix}TableViaGet")
            .WithSummary($"Queries a table {schemaInText} using URL parameters")
            .Produces<QueryResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Query table with filtering, sorting, and pagination
        group
            .MapPost(
                "/{tableName}/query",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string tableName,
                    QueryRequest request,
                    CancellationToken ct
                ) => QueryTableAsync(ctx, service, user, tableName, request, isSchemaSpecific, ct)
            )
            .WithName($"Query{namePrefix}Table")
            .WithSummary($"Queries a table {schemaInText} with filtering, sorting, and pagination")
            .Produces<QueryResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Column management endpoints
        group
            .MapGet(
                "/{tableName}/columns",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string tableName,
                    CancellationToken ct
                ) => ListColumnsAsync(ctx, service, user, tableName, isSchemaSpecific, ct)
            )
            .WithName($"List{namePrefix}Columns")
            .WithSummary($"Gets all columns for a table {schemaInText}")
            .Produces<ColumnListResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapGet(
                "/{tableName}/columns/{columnName}",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string tableName,
                    string columnName,
                    CancellationToken ct
                ) => GetColumnAsync(ctx, service, user, tableName, columnName, isSchemaSpecific, ct)
            )
            .WithName($"Get{namePrefix}Column")
            .WithSummary($"Gets a specific column from a table {schemaInText}")
            .Produces<ColumnResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapPost(
                "/{tableName}/columns",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string tableName,
                    CreateTableColumnRequest request,
                    CancellationToken ct
                ) => AddColumnAsync(ctx, service, user, tableName, request, isSchemaSpecific, ct)
            )
            .WithName($"Add{namePrefix}Column")
            .WithSummary($"Adds a new column to a table {schemaInText}")
            .Produces<ColumnResponse>((int)HttpStatusCode.Created)
            .Produces((int)HttpStatusCode.Conflict)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapPut(
                "/{tableName}/columns/{columnName}",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string tableName,
                    string columnName,
                    RenameColumnRequest request,
                    CancellationToken ct
                ) =>
                    RenameColumnAsync(
                        ctx,
                        service,
                        user,
                        tableName,
                        columnName,
                        request,
                        isSchemaSpecific,
                        ct
                    )
            )
            .WithName($"Update{namePrefix}Column")
            .WithSummary($"Updates a column in a table {schemaInText}")
            .Produces<ColumnResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapDelete(
                "/{tableName}/columns/{columnName}",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string tableName,
                    string columnName,
                    CancellationToken ct
                ) =>
                    DropColumnAsync(ctx, service, user, tableName, columnName, isSchemaSpecific, ct)
            )
            .WithName($"Drop{namePrefix}Column")
            .WithSummary($"Drops a column from a table {schemaInText}")
            .Produces<ColumnResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Index management endpoints
        group
            .MapGet(
                "/{tableName}/indexes",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string tableName,
                    CancellationToken ct
                ) => ListIndexesAsync(ctx, service, user, tableName, isSchemaSpecific, ct)
            )
            .WithName($"List{namePrefix}Indexes")
            .WithSummary($"Gets all indexes for a table {schemaInText}")
            .Produces<IndexListResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapGet(
                "/{tableName}/indexes/{indexName}",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string tableName,
                    string indexName,
                    CancellationToken ct
                ) => GetIndexAsync(ctx, service, user, tableName, indexName, isSchemaSpecific, ct)
            )
            .WithName($"Get{namePrefix}Index")
            .WithSummary($"Gets a specific index from a table {schemaInText}")
            .Produces<IndexResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapPost(
                "/{tableName}/indexes",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string tableName,
                    CreateIndexRequest request,
                    CancellationToken ct
                ) => CreateIndexAsync(ctx, service, user, tableName, request, isSchemaSpecific, ct)
            )
            .WithName($"Create{namePrefix}Index")
            .WithSummary($"Creates a new index on a table {schemaInText}")
            .Produces<IndexResponse>((int)HttpStatusCode.Created)
            .Produces((int)HttpStatusCode.Conflict)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapDelete(
                "/{tableName}/indexes/{indexName}",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string tableName,
                    string indexName,
                    CancellationToken ct
                ) => DropIndexAsync(ctx, service, user, tableName, indexName, isSchemaSpecific, ct)
            )
            .WithName($"Drop{namePrefix}Index")
            .WithSummary($"Drops an index from a table {schemaInText}")
            .Produces<IndexResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Primary key constraint endpoints
        group
            .MapGet(
                "/{tableName}/primarykey",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string tableName,
                    CancellationToken ct
                ) => GetPrimaryKeyAsync(ctx, service, user, tableName, isSchemaSpecific, ct)
            )
            .WithName($"Get{namePrefix}PrimaryKey")
            .WithSummary($"Gets the primary key constraint for a table {schemaInText}")
            .Produces<PrimaryKeyResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapPost(
                "/{tableName}/primarykey",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string tableName,
                    CreatePrimaryKeyRequest request,
                    CancellationToken ct
                ) =>
                    CreatePrimaryKeyAsync(
                        ctx,
                        service,
                        user,
                        tableName,
                        request,
                        isSchemaSpecific,
                        ct
                    )
            )
            .WithName($"Create{namePrefix}PrimaryKey")
            .WithSummary($"Creates a primary key constraint on a table {schemaInText}")
            .Produces<PrimaryKeyResponse>((int)HttpStatusCode.Created)
            .Produces((int)HttpStatusCode.Conflict)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapDelete(
                "/{tableName}/primarykey",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string tableName,
                    CancellationToken ct
                ) => DropPrimaryKeyAsync(ctx, service, user, tableName, isSchemaSpecific, ct)
            )
            .WithName($"Drop{namePrefix}PrimaryKey")
            .WithSummary($"Drops the primary key constraint from a table {schemaInText}")
            .Produces<PrimaryKeyResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Foreign key constraint endpoints
        group
            .MapGet(
                "/{tableName}/foreignkeys",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string tableName,
                    CancellationToken ct
                ) => ListForeignKeysAsync(ctx, service, user, tableName, isSchemaSpecific, ct)
            )
            .WithName($"List{namePrefix}ForeignKeys")
            .WithSummary($"Gets all foreign key constraints for a table {schemaInText}")
            .Produces<ForeignKeyListResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapGet(
                "/{tableName}/foreignkeys/{constraintName}",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string tableName,
                    string constraintName,
                    CancellationToken ct
                ) =>
                    GetForeignKeyAsync(
                        ctx,
                        service,
                        user,
                        tableName,
                        constraintName,
                        isSchemaSpecific,
                        ct
                    )
            )
            .WithName($"Get{namePrefix}ForeignKey")
            .WithSummary($"Gets a specific foreign key constraint from a table {schemaInText}")
            .Produces<ForeignKeyResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapPost(
                "/{tableName}/foreignkeys",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string tableName,
                    CreateForeignKeyRequest request,
                    CancellationToken ct
                ) =>
                    CreateForeignKeyAsync(
                        ctx,
                        service,
                        user,
                        tableName,
                        request,
                        isSchemaSpecific,
                        ct
                    )
            )
            .WithName($"Create{namePrefix}ForeignKey")
            .WithSummary($"Creates a foreign key constraint on a table {schemaInText}")
            .Produces<ForeignKeyResponse>((int)HttpStatusCode.Created)
            .Produces((int)HttpStatusCode.Conflict)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapDelete(
                "/{tableName}/foreignkeys/{constraintName}",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string tableName,
                    string constraintName,
                    CancellationToken ct
                ) =>
                    DropForeignKeyAsync(
                        ctx,
                        service,
                        user,
                        tableName,
                        constraintName,
                        isSchemaSpecific,
                        ct
                    )
            )
            .WithName($"Drop{namePrefix}ForeignKey")
            .WithSummary($"Drops a foreign key constraint from a table {schemaInText}")
            .Produces<ForeignKeyResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Check constraint endpoints
        group
            .MapGet(
                "/{tableName}/checkconstraints",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string tableName,
                    CancellationToken ct
                ) => ListCheckConstraintsAsync(ctx, service, user, tableName, isSchemaSpecific, ct)
            )
            .WithName($"List{namePrefix}CheckConstraints")
            .WithSummary($"Gets all check constraints for a table {schemaInText}")
            .Produces<CheckConstraintListResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapGet(
                "/{tableName}/checkconstraints/{constraintName}",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string tableName,
                    string constraintName,
                    CancellationToken ct
                ) =>
                    GetCheckConstraintAsync(
                        ctx,
                        service,
                        user,
                        tableName,
                        constraintName,
                        isSchemaSpecific,
                        ct
                    )
            )
            .WithName($"Get{namePrefix}CheckConstraint")
            .WithSummary($"Gets a specific check constraint from a table {schemaInText}")
            .Produces<CheckConstraintResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapPost(
                "/{tableName}/checkconstraints",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string tableName,
                    CreateCheckConstraintRequest request,
                    CancellationToken ct
                ) =>
                    CreateCheckConstraintAsync(
                        ctx,
                        service,
                        user,
                        tableName,
                        request,
                        isSchemaSpecific,
                        ct
                    )
            )
            .WithName($"Create{namePrefix}CheckConstraint")
            .WithSummary($"Creates a check constraint on a table {schemaInText}")
            .Produces<CheckConstraintResponse>((int)HttpStatusCode.Created)
            .Produces((int)HttpStatusCode.Conflict)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapDelete(
                "/{tableName}/checkconstraints/{constraintName}",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string tableName,
                    string constraintName,
                    CancellationToken ct
                ) =>
                    DropCheckConstraintAsync(
                        ctx,
                        service,
                        user,
                        tableName,
                        constraintName,
                        isSchemaSpecific,
                        ct
                    )
            )
            .WithName($"Drop{namePrefix}CheckConstraint")
            .WithSummary($"Drops a check constraint from a table {schemaInText}")
            .Produces<CheckConstraintResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Unique constraint endpoints
        group
            .MapGet(
                "/{tableName}/uniqueconstraints",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string tableName,
                    CancellationToken ct
                ) => ListUniqueConstraintsAsync(ctx, service, user, tableName, isSchemaSpecific, ct)
            )
            .WithName($"List{namePrefix}UniqueConstraints")
            .WithSummary($"Gets all unique constraints for a table {schemaInText}")
            .Produces<UniqueConstraintListResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapGet(
                "/{tableName}/uniqueconstraints/{constraintName}",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string tableName,
                    string constraintName,
                    CancellationToken ct
                ) =>
                    GetUniqueConstraintAsync(
                        ctx,
                        service,
                        user,
                        tableName,
                        constraintName,
                        isSchemaSpecific,
                        ct
                    )
            )
            .WithName($"Get{namePrefix}UniqueConstraint")
            .WithSummary($"Gets a specific unique constraint from a table {schemaInText}")
            .Produces<UniqueConstraintResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapPost(
                "/{tableName}/uniqueconstraints",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string tableName,
                    CreateUniqueConstraintRequest request,
                    CancellationToken ct
                ) =>
                    CreateUniqueConstraintAsync(
                        ctx,
                        service,
                        user,
                        tableName,
                        request,
                        isSchemaSpecific,
                        ct
                    )
            )
            .WithName($"Create{namePrefix}UniqueConstraint")
            .WithSummary($"Creates a unique constraint on a table {schemaInText}")
            .Produces<UniqueConstraintResponse>((int)HttpStatusCode.Created)
            .Produces((int)HttpStatusCode.Conflict)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapDelete(
                "/{tableName}/uniqueconstraints/{constraintName}",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string tableName,
                    string constraintName,
                    CancellationToken ct
                ) =>
                    DropUniqueConstraintAsync(
                        ctx,
                        service,
                        user,
                        tableName,
                        constraintName,
                        isSchemaSpecific,
                        ct
                    )
            )
            .WithName($"Drop{namePrefix}UniqueConstraint")
            .WithSummary($"Drops a unique constraint from a table {schemaInText}")
            .Produces<UniqueConstraintResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Default constraint endpoints
        group
            .MapGet(
                "/{tableName}/defaultconstraints",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string tableName,
                    CancellationToken ct
                ) =>
                    ListDefaultConstraintsAsync(ctx, service, user, tableName, isSchemaSpecific, ct)
            )
            .WithName($"List{namePrefix}DefaultConstraints")
            .WithSummary($"Gets all default constraints for a table {schemaInText}")
            .Produces<DefaultConstraintListResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapGet(
                "/{tableName}/defaultconstraints/{constraintName}",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string tableName,
                    string constraintName,
                    CancellationToken ct
                ) =>
                    GetDefaultConstraintAsync(
                        ctx,
                        service,
                        user,
                        tableName,
                        constraintName,
                        isSchemaSpecific,
                        ct
                    )
            )
            .WithName($"Get{namePrefix}DefaultConstraint")
            .WithSummary($"Gets a specific default constraint from a table {schemaInText}")
            .Produces<DefaultConstraintResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapPost(
                "/{tableName}/defaultconstraints",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string tableName,
                    CreateDefaultConstraintRequest request,
                    CancellationToken ct
                ) =>
                    CreateDefaultConstraintAsync(
                        ctx,
                        service,
                        user,
                        tableName,
                        request,
                        isSchemaSpecific,
                        ct
                    )
            )
            .WithName($"Create{namePrefix}DefaultConstraint")
            .WithSummary($"Creates a default constraint on a table {schemaInText}")
            .Produces<DefaultConstraintResponse>((int)HttpStatusCode.Created)
            .Produces((int)HttpStatusCode.Conflict)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group
            .MapDelete(
                "/{tableName}/defaultconstraints/{constraintName}",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string tableName,
                    string constraintName,
                    CancellationToken ct
                ) =>
                    DropDefaultConstraintAsync(
                        ctx,
                        service,
                        user,
                        tableName,
                        constraintName,
                        isSchemaSpecific,
                        ct
                    )
            )
            .WithName($"Drop{namePrefix}DefaultConstraint")
            .WithSummary($"Drops a default constraint from a table {schemaInText}")
            .Produces<DefaultConstraintResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);
    }

    private static async Task<IResult> ListTablesAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string? include,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : httpContext.Request.Query["schemaName"].FirstOrDefault();
        var filter = httpContext.Request.Query["filter"].FirstOrDefault();

        try
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
                    datasourceId,
                    schemaName,
                    includeColumns,
                    includeIndexes,
                    includeConstraints,
                    user,
                    cancellationToken
                )
                .ConfigureAwait(false);

            // Apply filter if provided
            if (!string.IsNullOrWhiteSpace(filter))
            {
                tables = tables.Where(t =>
                    t.TableName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                );
            }

            return Results.Ok(
                new TableListResponse
                {
                    Success = true,
                    Message = $"Found {tables.Count()} tables",
                    Result = tables.ToList(),
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
                detail: ex.Message,
                title: "Internal server error",
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> GetTableAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string tableName,
        string? include,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : httpContext.Request.Query["schemaName"].FirstOrDefault();

        try
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
                    datasourceId,
                    tableName,
                    schemaName,
                    includeColumns,
                    includeIndexes,
                    includeConstraints,
                    user,
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (table == null)
            {
                return Results.NotFound(
                    isSchemaSpecific
                        ? $"Table '{tableName}' not found in schema '{schemaName}' of datasource '{datasourceId}'"
                        : $"Table '{tableName}' not found in datasource '{datasourceId}'"
                );
            }

            return Results.Ok(
                new TableResponse
                {
                    Success = true,
                    Message = "Table retrieved successfully",
                    Result = table,
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
                detail: ex.Message,
                title: "Internal server error",
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> CreateTableAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        CreateTableRequest request,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : httpContext.Request.Query["schemaName"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(schemaName))
        {
            // Set it on the request to pass it down
            request.SchemaName = schemaName;
        }

        // Validate request
        if (string.IsNullOrWhiteSpace(request.TableName))
        {
            return Results.BadRequest(
                new TableResponse
                {
                    Success = false,
                    Message = "Table name is required and cannot be empty",
                }
            );
        }

        if (request.Columns == null || request.Columns.Count == 0)
        {
            return Results.BadRequest(
                new TableResponse
                {
                    Success = false,
                    Message = "At least one column is required to create a table",
                }
            );
        }

        try
        {
            var created = await service
                .CreateTableAsync(datasourceId, request, user, cancellationToken)
                .ConfigureAwait(false);

            if (created == null)
            {
                return Results.Conflict(
                    new TableResponse
                    {
                        Success = false,
                        Message = $"Table '{request.TableName}' already exists or creation failed",
                    }
                );
            }

            return Results.Created(
                $"{httpContext.Request.Path}/{request.TableName}",
                new TableResponse
                {
                    Success = true,
                    Message = $"Table '{request.TableName}' created successfully",
                    Result = created,
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
                detail: $"Failed to create table: {ex.Message}",
                title: "Internal server error",
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> DropTableAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string tableName,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : httpContext.Request.Query["schemaName"].FirstOrDefault();

        try
        {
            var success = await service
                .DropTableAsync(datasourceId, tableName, schemaName, user, cancellationToken)
                .ConfigureAwait(false);

            if (success)
            {
                return Results.Ok(
                    new TableResponse
                    {
                        Success = true,
                        Message = $"Table '{tableName}' dropped successfully",
                    }
                );
            }
            else
            {
                return Results.NotFound(
                    new TableResponse
                    {
                        Success = false,
                        Message = $"Table '{tableName}' not found",
                    }
                );
            }
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
                detail: $"Failed to drop table: {ex.Message}",
                title: "Internal server error",
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> RenameTableAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string tableName,
        RenameTableRequest request,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : httpContext.Request.Query["schemaName"].FirstOrDefault();

        // Validate request
        if (string.IsNullOrWhiteSpace(request.NewTableName))
        {
            return Results.BadRequest(
                new TableResponse
                {
                    Success = false,
                    Message = "New table name is required and cannot be empty",
                }
            );
        }

        if (request.NewTableName.Equals(tableName, StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(
                new TableResponse
                {
                    Success = false,
                    Message = "New table name must be different from the current table name",
                }
            );
        }

        try
        {
            var success = await service
                .RenameTableAsync(
                    datasourceId,
                    tableName,
                    request.NewTableName,
                    schemaName,
                    user,
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (success)
            {
                return Results.Ok(
                    new TableResponse
                    {
                        Success = true,
                        Message =
                            $"Table '{tableName}' renamed to '{request.NewTableName}' successfully",
                    }
                );
            }
            else
            {
                return Results.NotFound(
                    new TableResponse
                    {
                        Success = false,
                        Message = $"Table '{tableName}' not found",
                    }
                );
            }
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
                detail: $"Failed to rename table: {ex.Message}",
                title: "Internal server error",
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> TableExistsAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string tableName,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : httpContext.Request.Query["schemaName"].FirstOrDefault();

        try
        {
            var exists = await service
                .TableExistsAsync(datasourceId, tableName, schemaName, user, cancellationToken)
                .ConfigureAwait(false);

            return Results.Ok(
                new TableExistsResponse
                {
                    Result = exists,
                    Success = true,
                    Message = exists ? "Table exists" : "Table does not exist",
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
                detail: ex.Message,
                title: "Internal server error",
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> QueryTableAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string tableName,
        QueryRequest request,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : httpContext.Request.Query["schemaName"].FirstOrDefault();

        try
        {
            var result = await service
                .QueryTableAsync(
                    datasourceId,
                    tableName,
                    request,
                    schemaName,
                    user,
                    cancellationToken
                )
                .ConfigureAwait(false);

            return Results.Ok(
                new QueryResponse(result)
                {
                    Success = true,
                    Message = isSchemaSpecific
                        ? $"Query executed successfully on table '{tableName}' in schema '{schemaName}'. Returned {result.Data.Count()} records."
                        : $"Query executed successfully. Returned {result.Data.Count()} records.",
                }
            );
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (ArgumentException ex)
            when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
            )
        {
            var message = isSchemaSpecific
                ? $"Table '{tableName}' not found in schema '{schemaName}' of datasource '{datasourceId}'"
                : $"Table '{tableName}' not found in datasource '{datasourceId}'";
            return Results.NotFound(message);
        }
        catch (Exception ex)
        {
            var detail = isSchemaSpecific
                ? $"Failed to query table in schema: {ex.Message}"
                : $"Failed to query table: {ex.Message}";
            return Results.Problem(detail: detail, title: "Internal server error", statusCode: 500);
        }
    }

    private static async Task<IResult> QueryTableViaGetAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string tableName,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : null;

        try
        {
            var request = QueryRequest.FromQueryParameters(httpContext.Request.Query);

            var result = await service
                .QueryTableAsync(
                    datasourceId,
                    tableName,
                    request,
                    schemaName,
                    user,
                    cancellationToken
                )
                .ConfigureAwait(false);

            return Results.Ok(
                new QueryResponse(result)
                {
                    Success = true,
                    Message = isSchemaSpecific
                        ? $"Query executed successfully on table '{tableName}' in schema '{schemaName}'. Returned {result.Data.Count()} records."
                        : $"Query executed successfully. Returned {result.Data.Count()} records.",
                }
            );
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (ArgumentException ex)
            when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
            )
        {
            var message = isSchemaSpecific
                ? $"Table '{tableName}' not found in schema '{schemaName}' of datasource '{datasourceId}'"
                : $"Table '{tableName}' not found in datasource '{datasourceId}'";
            return Results.NotFound(message);
        }
        catch (Exception ex)
        {
            var detail = isSchemaSpecific
                ? $"Failed to query table in schema: {ex.Message}"
                : $"Failed to query table: {ex.Message}";
            return Results.Problem(detail: detail, title: "Internal server error", statusCode: 500);
        }
    }

    // Column management endpoint implementations
    private static async Task<IResult> ListColumnsAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string tableName,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : httpContext.Request.Query["schemaName"].FirstOrDefault();

        try
        {
            var columns = await service
                .GetColumnsAsync(datasourceId, tableName, schemaName, user, cancellationToken)
                .ConfigureAwait(false);

            return Results.Ok(
                new ColumnListResponse
                {
                    Success = true,
                    Message = $"Found {columns.Count()} columns",
                    Result = columns.ToList(),
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
            return Results.NotFound(
                $"Datasource '{datasourceId}' or table '{tableName}' not found"
            );
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

    private static async Task<IResult> GetColumnAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string tableName,
        string columnName,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : httpContext.Request.Query["schemaName"].FirstOrDefault();

        try
        {
            var column = await service
                .GetColumnAsync(
                    datasourceId,
                    tableName,
                    columnName,
                    schemaName,
                    user,
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (column == null)
            {
                return Results.NotFound(
                    isSchemaSpecific
                        ? $"Column '{columnName}' not found in table '{tableName}' in schema '{schemaName}' of datasource '{datasourceId}'"
                        : $"Column '{columnName}' not found in table '{tableName}' of datasource '{datasourceId}'"
                );
            }

            return Results.Ok(
                new ColumnResponse
                {
                    Success = true,
                    Message = "Column retrieved successfully",
                    Result = column,
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
            return Results.NotFound(
                $"Datasource '{datasourceId}' or table '{tableName}' not found"
            );
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

    private static async Task<IResult> AddColumnAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string tableName,
        CreateTableColumnRequest request,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : httpContext.Request.Query["schemaName"].FirstOrDefault();

        try
        {
            var result = await service
                .AddColumnAsync(
                    datasourceId,
                    tableName,
                    request,
                    schemaName,
                    user,
                    cancellationToken
                )
                .ConfigureAwait(false);

            return Results.Created(
                $"{httpContext.Request.Path}/{request.ColumnName}",
                new ColumnResponse
                {
                    Success = true,
                    Message = $"Column '{request.ColumnName}' added successfully",
                    Result = result,
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
            return Results.NotFound(
                $"Datasource '{datasourceId}' or table '{tableName}' not found"
            );
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: $"Failed to add column: {ex.Message}",
                title: "Internal server error",
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> RenameColumnAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string tableName,
        string columnName,
        RenameColumnRequest request,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : httpContext.Request.Query["schemaName"].FirstOrDefault();

        // Validate request
        if (string.IsNullOrWhiteSpace(request.NewColumnName))
        {
            return Results.BadRequest(
                new ColumnResponse
                {
                    Success = false,
                    Message = "New column name is required and cannot be empty",
                }
            );
        }

        try
        {
            var result = await service
                .RenameColumnAsync(
                    datasourceId,
                    tableName,
                    columnName,
                    request.NewColumnName,
                    schemaName,
                    user,
                    cancellationToken
                )
                .ConfigureAwait(false);

            return Results.Ok(
                new ColumnResponse
                {
                    Success = true,
                    Message = $"Column '{columnName}' updated successfully",
                    Result = result,
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
            return Results.NotFound(
                $"Datasource '{datasourceId}', table '{tableName}', or column '{columnName}' not found"
            );
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: $"Failed to update column: {ex.Message}",
                title: "Internal server error",
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> DropColumnAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string tableName,
        string columnName,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : httpContext.Request.Query["schemaName"].FirstOrDefault();

        try
        {
            var success = await service
                .DropColumnAsync(
                    datasourceId,
                    tableName,
                    columnName,
                    schemaName,
                    user,
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (success)
            {
                return Results.Ok(
                    new ColumnResponse
                    {
                        Success = true,
                        Message = $"Column '{columnName}' dropped successfully",
                        Result = null,
                    }
                );
            }
            else
            {
                return Results.NotFound(
                    new ColumnResponse
                    {
                        Success = false,
                        Message = $"Column '{columnName}' not found",
                        Result = null,
                    }
                );
            }
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (ArgumentException ex)
            when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return Results.NotFound(
                $"Datasource '{datasourceId}', table '{tableName}', or column '{columnName}' not found"
            );
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: $"Failed to drop column: {ex.Message}",
                title: "Internal server error",
                statusCode: 500
            );
        }
    }

    // Index management endpoint implementations
    private static async Task<IResult> ListIndexesAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string tableName,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : httpContext.Request.Query["schemaName"].FirstOrDefault();

        try
        {
            var indexes = await service
                .GetIndexesAsync(datasourceId, tableName, schemaName, user, cancellationToken)
                .ConfigureAwait(false);

            return Results.Ok(
                new IndexListResponse
                {
                    Success = true,
                    Message = $"Found {indexes.Count()} indexes",
                    Result = indexes.ToList(),
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
            return Results.NotFound(
                $"Datasource '{datasourceId}' or table '{tableName}' not found"
            );
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

    private static async Task<IResult> GetIndexAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string tableName,
        string indexName,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : httpContext.Request.Query["schemaName"].FirstOrDefault();

        try
        {
            var index = await service
                .GetIndexAsync(
                    datasourceId,
                    tableName,
                    indexName,
                    schemaName,
                    user,
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (index == null)
            {
                return Results.NotFound(
                    isSchemaSpecific
                        ? $"Index '{indexName}' not found in table '{tableName}' in schema '{schemaName}' of datasource '{datasourceId}'"
                        : $"Index '{indexName}' not found in table '{tableName}' of datasource '{datasourceId}'"
                );
            }

            return Results.Ok(
                new IndexResponse
                {
                    Success = true,
                    Message = "Index retrieved successfully",
                    Result = index,
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
            return Results.NotFound(
                $"Datasource '{datasourceId}' or table '{tableName}' not found"
            );
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

    private static async Task<IResult> CreateIndexAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string tableName,
        CreateIndexRequest request,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : httpContext.Request.Query["schemaName"].FirstOrDefault();

        try
        {
            var result = await service
                .CreateIndexAsync(
                    datasourceId,
                    tableName,
                    request,
                    schemaName,
                    user,
                    cancellationToken
                )
                .ConfigureAwait(false);

            return Results.Created(
                $"{httpContext.Request.Path}/{request.IndexName}",
                new IndexResponse
                {
                    Success = true,
                    Message = $"Index '{request.IndexName}' created successfully",
                    Result = result,
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
            return Results.NotFound(
                $"Datasource '{datasourceId}' or table '{tableName}' not found"
            );
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: $"Failed to create index: {ex.Message}",
                title: "Internal server error",
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> DropIndexAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string tableName,
        string indexName,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : httpContext.Request.Query["schemaName"].FirstOrDefault();

        try
        {
            var success = await service
                .DropIndexAsync(
                    datasourceId,
                    tableName,
                    indexName,
                    schemaName,
                    user,
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (success)
            {
                return Results.Ok(
                    new IndexResponse
                    {
                        Success = true,
                        Message = $"Index '{indexName}' dropped successfully",
                        Result = null,
                    }
                );
            }
            else
            {
                return Results.NotFound(
                    new IndexResponse
                    {
                        Success = false,
                        Message = $"Index '{indexName}' not found",
                        Result = null,
                    }
                );
            }
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (ArgumentException ex)
            when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return Results.NotFound(
                $"Datasource '{datasourceId}', table '{tableName}', or index '{indexName}' not found"
            );
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: $"Failed to drop index: {ex.Message}",
                title: "Internal server error",
                statusCode: 500
            );
        }
    }

    // Primary key constraint endpoint implementations
    private static async Task<IResult> GetPrimaryKeyAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string tableName,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : httpContext.Request.Query["schemaName"].FirstOrDefault();

        try
        {
            var result = await service
                .GetPrimaryKeyAsync(datasourceId, tableName, schemaName, user, cancellationToken)
                .ConfigureAwait(false);

            if (result == null)
            {
                return Results.NotFound(
                    isSchemaSpecific
                        ? $"Primary key not found for table '{tableName}' in schema '{schemaName}' of datasource '{datasourceId}'"
                        : $"Primary key not found for table '{tableName}' of datasource '{datasourceId}'"
                );
            }

            return Results.Ok(new PrimaryKeyResponse(result));
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (ArgumentException ex)
            when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return Results.NotFound(
                $"Datasource '{datasourceId}' or table '{tableName}' not found"
            );
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

    private static async Task<IResult> CreatePrimaryKeyAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string tableName,
        CreatePrimaryKeyRequest request,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : httpContext.Request.Query["schemaName"].FirstOrDefault();

        try
        {
            var result = await service
                .CreatePrimaryKeyAsync(
                    datasourceId,
                    tableName,
                    request,
                    schemaName,
                    user,
                    cancellationToken
                )
                .ConfigureAwait(false);

            return Results.Created(httpContext.Request.Path!, new PrimaryKeyResponse(result));
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (ArgumentException ex)
            when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return Results.NotFound(
                $"Datasource '{datasourceId}' or table '{tableName}' not found"
            );
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: $"Failed to create primary key: {ex.Message}",
                title: "Internal server error",
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> DropPrimaryKeyAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string tableName,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : httpContext.Request.Query["schemaName"].FirstOrDefault();

        try
        {
            var success = await service
                .DropPrimaryKeyAsync(datasourceId, tableName, schemaName, user, cancellationToken)
                .ConfigureAwait(false);

            if (success)
            {
                return Results.Ok(
                    new PrimaryKeyResponse
                    {
                        Success = true,
                        Message = "Primary key dropped successfully",
                        Result = null,
                    }
                );
            }
            else
            {
                return Results.NotFound(
                    new PrimaryKeyResponse
                    {
                        Success = false,
                        Message = "Primary key not found",
                        Result = null,
                    }
                );
            }
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (ArgumentException ex)
            when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return Results.NotFound(
                $"Datasource '{datasourceId}', table '{tableName}', or primary key not found"
            );
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: $"Failed to drop primary key: {ex.Message}",
                title: "Internal server error",
                statusCode: 500
            );
        }
    }

    // Foreign key constraint endpoint implementations
    private static async Task<IResult> ListForeignKeysAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string tableName,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : httpContext.Request.Query["schemaName"].FirstOrDefault();

        try
        {
            var foreignKeys = await service
                .GetForeignKeysAsync(datasourceId, tableName, schemaName, user, cancellationToken)
                .ConfigureAwait(false);

            return Results.Ok(
                new ForeignKeyListResponse
                {
                    Success = true,
                    Message = $"Found {foreignKeys.Count()} foreign keys",
                    Result = foreignKeys.ToList(),
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
            return Results.NotFound(
                $"Datasource '{datasourceId}' or table '{tableName}' not found"
            );
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

    private static async Task<IResult> GetForeignKeyAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string tableName,
        string constraintName,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : httpContext.Request.Query["schemaName"].FirstOrDefault();

        try
        {
            var foreignKey = await service
                .GetForeignKeyAsync(
                    datasourceId,
                    tableName,
                    constraintName,
                    schemaName,
                    user,
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (foreignKey == null)
            {
                return Results.NotFound(
                    isSchemaSpecific
                        ? $"Foreign key '{constraintName}' not found in table '{tableName}' in schema '{schemaName}' of datasource '{datasourceId}'"
                        : $"Foreign key '{constraintName}' not found in table '{tableName}' of datasource '{datasourceId}'"
                );
            }

            return Results.Ok(
                new ForeignKeyResponse
                {
                    Success = true,
                    Message = "Foreign key retrieved successfully",
                    Result = foreignKey,
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
            return Results.NotFound(
                $"Datasource '{datasourceId}' or table '{tableName}' not found"
            );
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

    private static async Task<IResult> CreateForeignKeyAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string tableName,
        CreateForeignKeyRequest request,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : httpContext.Request.Query["schemaName"].FirstOrDefault();

        try
        {
            var result = await service
                .CreateForeignKeyAsync(
                    datasourceId,
                    tableName,
                    request,
                    schemaName,
                    user,
                    cancellationToken
                )
                .ConfigureAwait(false);

            return Results.Created(
                $"{httpContext.Request.Path}/{request.ConstraintName}",
                new ForeignKeyResponse
                {
                    Success = true,
                    Message = $"Foreign key '{request.ConstraintName}' created successfully",
                    Result = result,
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
            return Results.NotFound(
                $"Datasource '{datasourceId}' or table '{tableName}' not found"
            );
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: $"Failed to create foreign key: {ex.Message}",
                title: "Internal server error",
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> DropForeignKeyAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string tableName,
        string constraintName,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : httpContext.Request.Query["schemaName"].FirstOrDefault();

        try
        {
            var success = await service
                .DropForeignKeyAsync(
                    datasourceId,
                    tableName,
                    constraintName,
                    schemaName,
                    user,
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (success)
            {
                return Results.Ok(
                    new ForeignKeyResponse
                    {
                        Success = true,
                        Message = $"Foreign key '{constraintName}' dropped successfully",
                        Result = null,
                    }
                );
            }
            else
            {
                return Results.NotFound(
                    new ForeignKeyResponse
                    {
                        Success = false,
                        Message = $"Foreign key '{constraintName}' not found",
                        Result = null,
                    }
                );
            }
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (ArgumentException ex)
            when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return Results.NotFound(
                $"Datasource '{datasourceId}', table '{tableName}', or foreign key '{constraintName}' not found"
            );
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: $"Failed to drop foreign key: {ex.Message}",
                title: "Internal server error",
                statusCode: 500
            );
        }
    }

    // Check constraint endpoint implementations
    private static async Task<IResult> ListCheckConstraintsAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string tableName,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : httpContext.Request.Query["schemaName"].FirstOrDefault();

        try
        {
            var checkConstraints = await service
                .GetCheckConstraintsAsync(
                    datasourceId,
                    tableName,
                    schemaName,
                    user,
                    cancellationToken
                )
                .ConfigureAwait(false);

            return Results.Ok(
                new CheckConstraintListResponse
                {
                    Success = true,
                    Message = $"Found {checkConstraints.Count()} check constraints",
                    Result = checkConstraints.ToList(),
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
            return Results.NotFound(
                $"Datasource '{datasourceId}' or table '{tableName}' not found"
            );
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

    private static async Task<IResult> GetCheckConstraintAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string tableName,
        string constraintName,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : httpContext.Request.Query["schemaName"].FirstOrDefault();

        try
        {
            var checkConstraint = await service
                .GetCheckConstraintAsync(
                    datasourceId,
                    tableName,
                    constraintName,
                    schemaName,
                    user,
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (checkConstraint == null)
            {
                return Results.NotFound(
                    isSchemaSpecific
                        ? $"Check constraint '{constraintName}' not found in table '{tableName}' in schema '{schemaName}' of datasource '{datasourceId}'"
                        : $"Check constraint '{constraintName}' not found in table '{tableName}' of datasource '{datasourceId}'"
                );
            }

            return Results.Ok(
                new CheckConstraintResponse
                {
                    Success = true,
                    Message = "Check constraint retrieved successfully",
                    Result = checkConstraint,
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
            return Results.NotFound(
                $"Datasource '{datasourceId}' or table '{tableName}' not found"
            );
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

    private static async Task<IResult> CreateCheckConstraintAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string tableName,
        CreateCheckConstraintRequest request,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : httpContext.Request.Query["schemaName"].FirstOrDefault();

        try
        {
            var result = await service
                .CreateCheckConstraintAsync(
                    datasourceId,
                    tableName,
                    request,
                    schemaName,
                    user,
                    cancellationToken
                )
                .ConfigureAwait(false);

            return Results.Created(
                $"{httpContext.Request.Path}/{request.ConstraintName}",
                new CheckConstraintResponse
                {
                    Success = true,
                    Message = $"Check constraint '{request.ConstraintName}' created successfully",
                    Result = result,
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
            return Results.NotFound(
                $"Datasource '{datasourceId}' or table '{tableName}' not found"
            );
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: $"Failed to create check constraint: {ex.Message}",
                title: "Internal server error",
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> DropCheckConstraintAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string tableName,
        string constraintName,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : httpContext.Request.Query["schemaName"].FirstOrDefault();

        try
        {
            var success = await service
                .DropCheckConstraintAsync(
                    datasourceId,
                    tableName,
                    constraintName,
                    schemaName,
                    user,
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (success)
            {
                return Results.Ok(
                    new CheckConstraintResponse
                    {
                        Success = true,
                        Message = $"Check constraint '{constraintName}' dropped successfully",
                        Result = null,
                    }
                );
            }
            else
            {
                return Results.NotFound(
                    new CheckConstraintResponse
                    {
                        Success = false,
                        Message = $"Check constraint '{constraintName}' not found",
                        Result = null,
                    }
                );
            }
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (ArgumentException ex)
            when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return Results.NotFound(
                $"Datasource '{datasourceId}', table '{tableName}', or check constraint '{constraintName}' not found"
            );
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: $"Failed to drop check constraint: {ex.Message}",
                title: "Internal server error",
                statusCode: 500
            );
        }
    }

    // Unique constraint endpoint implementations
    private static async Task<IResult> ListUniqueConstraintsAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string tableName,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : httpContext.Request.Query["schemaName"].FirstOrDefault();

        try
        {
            var uniqueConstraints = await service
                .GetUniqueConstraintsAsync(
                    datasourceId,
                    tableName,
                    schemaName,
                    user,
                    cancellationToken
                )
                .ConfigureAwait(false);

            return Results.Ok(
                new UniqueConstraintListResponse
                {
                    Success = true,
                    Message = $"Found {uniqueConstraints.Count()} unique constraints",
                    Result = uniqueConstraints.ToList(),
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
            return Results.NotFound(
                $"Datasource '{datasourceId}' or table '{tableName}' not found"
            );
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

    private static async Task<IResult> GetUniqueConstraintAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string tableName,
        string constraintName,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : httpContext.Request.Query["schemaName"].FirstOrDefault();

        try
        {
            var uniqueConstraint = await service
                .GetUniqueConstraintAsync(
                    datasourceId,
                    tableName,
                    constraintName,
                    schemaName,
                    user,
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (uniqueConstraint == null)
            {
                return Results.NotFound(
                    isSchemaSpecific
                        ? $"Unique constraint '{constraintName}' not found in table '{tableName}' in schema '{schemaName}' of datasource '{datasourceId}'"
                        : $"Unique constraint '{constraintName}' not found in table '{tableName}' of datasource '{datasourceId}'"
                );
            }

            return Results.Ok(
                new UniqueConstraintResponse
                {
                    Success = true,
                    Message = "Unique constraint retrieved successfully",
                    Result = uniqueConstraint,
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
            return Results.NotFound(
                $"Datasource '{datasourceId}' or table '{tableName}' not found"
            );
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

    private static async Task<IResult> CreateUniqueConstraintAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string tableName,
        CreateUniqueConstraintRequest request,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : httpContext.Request.Query["schemaName"].FirstOrDefault();

        try
        {
            var result = await service
                .CreateUniqueConstraintAsync(
                    datasourceId,
                    tableName,
                    request,
                    schemaName,
                    user,
                    cancellationToken
                )
                .ConfigureAwait(false);

            return Results.Created(
                $"{httpContext.Request.Path}/{request.ConstraintName}",
                new UniqueConstraintResponse
                {
                    Success = true,
                    Message = $"Unique constraint '{request.ConstraintName}' created successfully",
                    Result = result,
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
            return Results.NotFound(
                $"Datasource '{datasourceId}' or table '{tableName}' not found"
            );
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: $"Failed to create unique constraint: {ex.Message}",
                title: "Internal server error",
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> DropUniqueConstraintAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string tableName,
        string constraintName,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : httpContext.Request.Query["schemaName"].FirstOrDefault();

        try
        {
            var success = await service
                .DropUniqueConstraintAsync(
                    datasourceId,
                    tableName,
                    constraintName,
                    schemaName,
                    user,
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (success)
            {
                return Results.Ok(
                    new UniqueConstraintResponse
                    {
                        Success = true,
                        Message = $"Unique constraint '{constraintName}' dropped successfully",
                        Result = null,
                    }
                );
            }
            else
            {
                return Results.NotFound(
                    new UniqueConstraintResponse
                    {
                        Success = false,
                        Message = $"Unique constraint '{constraintName}' not found",
                        Result = null,
                    }
                );
            }
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (ArgumentException ex)
            when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return Results.NotFound(
                $"Datasource '{datasourceId}', table '{tableName}', or unique constraint '{constraintName}' not found"
            );
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: $"Failed to drop unique constraint: {ex.Message}",
                title: "Internal server error",
                statusCode: 500
            );
        }
    }

    // Default constraint endpoint implementations
    private static async Task<IResult> ListDefaultConstraintsAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string tableName,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : httpContext.Request.Query["schemaName"].FirstOrDefault();

        try
        {
            var defaultConstraints = await service
                .GetDefaultConstraintsAsync(
                    datasourceId,
                    tableName,
                    schemaName,
                    user,
                    cancellationToken
                )
                .ConfigureAwait(false);

            return Results.Ok(
                new DefaultConstraintListResponse
                {
                    Success = true,
                    Message = $"Found {defaultConstraints.Count()} default constraints",
                    Result = defaultConstraints.ToList(),
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
            return Results.NotFound(
                $"Datasource '{datasourceId}' or table '{tableName}' not found"
            );
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

    private static async Task<IResult> GetDefaultConstraintAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string tableName,
        string constraintName,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : httpContext.Request.Query["schemaName"].FirstOrDefault();

        try
        {
            var defaultConstraint = await service
                .GetDefaultConstraintAsync(
                    datasourceId,
                    tableName,
                    constraintName,
                    schemaName,
                    user,
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (defaultConstraint == null)
            {
                return Results.NotFound(
                    isSchemaSpecific
                        ? $"Default constraint '{constraintName}' not found in table '{tableName}' in schema '{schemaName}' of datasource '{datasourceId}'"
                        : $"Default constraint '{constraintName}' not found in table '{tableName}' of datasource '{datasourceId}'"
                );
            }

            return Results.Ok(
                new DefaultConstraintResponse
                {
                    Success = true,
                    Message = "Default constraint retrieved successfully",
                    Result = defaultConstraint,
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
            return Results.NotFound(
                $"Datasource '{datasourceId}' or table '{tableName}' not found"
            );
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

    private static async Task<IResult> CreateDefaultConstraintAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string tableName,
        CreateDefaultConstraintRequest request,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : httpContext.Request.Query["schemaName"].FirstOrDefault();

        try
        {
            var result = await service
                .CreateDefaultConstraintAsync(
                    datasourceId,
                    tableName,
                    request,
                    schemaName,
                    user,
                    cancellationToken
                )
                .ConfigureAwait(false);

            return Results.Created(
                $"{httpContext.Request.Path}/{request.ConstraintName}",
                new DefaultConstraintResponse
                {
                    Success = true,
                    Message = $"Default constraint '{request.ConstraintName}' created successfully",
                    Result = result,
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
            return Results.NotFound(
                $"Datasource '{datasourceId}' or table '{tableName}' not found"
            );
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: $"Failed to create default constraint: {ex.Message}",
                title: "Internal server error",
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> DropDefaultConstraintAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string tableName,
        string constraintName,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : httpContext.Request.Query["schemaName"].FirstOrDefault();

        try
        {
            var success = await service
                .DropDefaultConstraintAsync(
                    datasourceId,
                    tableName,
                    constraintName,
                    schemaName,
                    user,
                    cancellationToken
                )
                .ConfigureAwait(false);

            return success
                ? Results.Ok(
                    new DefaultConstraintResponse
                    {
                        Success = true,
                        Message = $"Default constraint '{constraintName}' dropped successfully",
                        Result = null,
                    }
                )
                : Results.NotFound(
                    new DefaultConstraintResponse
                    {
                        Success = false,
                        Message = $"Default constraint '{constraintName}' not found",
                        Result = null,
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
            return Results.NotFound(
                $"Datasource '{datasourceId}', table '{tableName}', or default constraint '{constraintName}' not found"
            );
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: $"Failed to drop default constraint: {ex.Message}",
                title: "Internal server error",
                statusCode: 500
            );
        }
    }
}
