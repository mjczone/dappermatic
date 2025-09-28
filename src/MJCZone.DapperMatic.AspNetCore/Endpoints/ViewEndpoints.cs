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
using MJCZone.DapperMatic.AspNetCore.Utilities;
using MJCZone.DapperMatic.AspNetCore.Validation;

namespace MJCZone.DapperMatic.AspNetCore.Endpoints;

/// <summary>
/// Extension methods for registering DapperMatic view endpoints.
/// </summary>
/// <remarks>
/// Registers both default schema endpoints (/d/{datasourceId}/v) and
/// schema-specific endpoints (/d/{datasourceId}/s/{schemaName}/v).
/// </remarks>
public static class ViewEndpoints
{
    /// <summary>
    /// Maps all DapperMatic view endpoints to the specified route builder.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <param name="basePath">The base path for the API endpoints. Defaults to "/api/dm".</param>
    /// <returns>The route builder for method chaining.</returns>
    /// <remarks>
    /// This method registers two sets of endpoints:
    /// 1. Default schema endpoints at /d/{datasourceId}/v for single-schema scenarios.
    /// 2. Schema-specific endpoints at /d/{datasourceId}/s/{schemaName}/v for multi-tenant scenarios.
    /// </remarks>
    public static IEndpointRouteBuilder MapDapperMaticViewEndpoints(
        this IEndpointRouteBuilder app,
        string? basePath = null
    )
    {
        // Register default schema endpoints (for single-schema scenarios)
        var defaultGroup = app.MapDapperMaticEndpointGroup(
            basePath,
            "/d/{datasourceId}/v",
            OperationTags.DatasourceViews
        );

        RegisterViewEndpoints(defaultGroup, "DefaultSchema", isSchemaSpecific: false);

        // Register schema-specific endpoints (for multi-tenant scenarios)
        var schemaGroup = app.MapDapperMaticEndpointGroup(
            basePath,
            "/d/{datasourceId}/s/{schemaName}/v",
            OperationTags.DatasourceViews
        );

        RegisterViewEndpoints(schemaGroup, "Schema", isSchemaSpecific: true);

        return app;
    }

    private static void RegisterViewEndpoints(
        RouteGroupBuilder group,
        string namePrefix,
        bool isSchemaSpecific
    )
    {
        var schemaText = isSchemaSpecific ? "a specific schema" : "the default schema";
        var schemaInText = isSchemaSpecific ? "in a specific schema" : "in the default schema";

        // List all views
        group
            .MapGet("/", isSchemaSpecific ? ListSchemaViewsAsync : ListViewsAsync)
            .WithName($"List{namePrefix}Views")
            .WithSummary($"Gets all views for {schemaText}")
            .WithOpenApi(operation =>
            {
                var includeParam = operation.Parameters.FirstOrDefault(p => p.Name == "include");
                if (includeParam != null)
                {
                    includeParam.Description =
                        "Comma-separated list of fields to include in the response. Use 'definition' to include view definitions, or '*' to include all fields. By default, definitions are excluded.";
                    includeParam.Example = new OpenApiString("definition");
                }
                return operation;
            })
            .Produces<ViewListResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Get specific view
        group
            .MapGet("/{viewName}", isSchemaSpecific ? GetSchemaViewAsync : GetViewAsync)
            .WithName($"Get{namePrefix}View")
            .WithSummary($"Gets a view by name from {schemaText}")
            .WithOpenApi(operation =>
            {
                var includeParam = operation.Parameters.FirstOrDefault(p => p.Name == "include");
                if (includeParam != null)
                {
                    includeParam.Description =
                        "Comma-separated list of fields to include in the response. Use 'definition' to include the view definition, or '*' to include all fields. By default, the definition is excluded.";
                    includeParam.Example = new OpenApiString("definition");
                }
                return operation;
            })
            .Produces<ViewResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Create new view
        group
            .MapPost("/", isSchemaSpecific ? CreateSchemaViewAsync : CreateViewAsync)
            .WithName($"Create{namePrefix}View")
            .WithSummary($"Creates a new view {schemaInText}")
            .Produces<ViewResponse>((int)HttpStatusCode.Created)
            .Produces((int)HttpStatusCode.Conflict)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Update existing view
        group
            .MapPut("/{viewName}", isSchemaSpecific ? UpdateSchemaViewAsync : UpdateViewAsync)
            .WithName($"Update{namePrefix}View")
            .WithSummary($"Updates an existing view {schemaInText}")
            .Produces<ViewResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Drop view
        group
            .MapDelete("/{viewName}", isSchemaSpecific ? DropSchemaViewAsync : DropViewAsync)
            .WithName($"Drop{namePrefix}View")
            .WithSummary($"Drops a view from {schemaText}")
            .Produces((int)HttpStatusCode.NoContent)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Check if view exists
        group
            .MapGet(
                "/{viewName}/exists",
                isSchemaSpecific ? SchemaViewExistsAsync : ViewExistsAsync
            )
            .WithName($"{namePrefix}ViewExists")
            .WithSummary($"Checks if a view exists {schemaInText}")
            .Produces<ViewExistsResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Query view via GET with URL parameters
        group
            .MapGet(
                "/{viewName}/query",
                isSchemaSpecific ? QuerySchemaViewViaGetAsync : QueryViewViaGetAsync
            )
            .WithName($"Query{namePrefix}ViewViaGet")
            .WithSummary($"Queries a view {schemaInText} using URL parameters")
            .Produces<QueryResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Query view with filtering, sorting, and pagination
        group
            .MapPost("/{viewName}/query", isSchemaSpecific ? QuerySchemaViewAsync : QueryViewAsync)
            .WithName($"Query{namePrefix}View")
            .WithSummary($"Queries a view {schemaInText} with filtering, sorting, and pagination")
            .Produces<QueryResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);
    }

    // Non-schema version delegates to schema version with null schemaName
    private static Task<IResult> ListViewsAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromQuery] string? include,
        [FromQuery] string? filter,
        CancellationToken cancellationToken = default
    ) =>
        ListSchemaViewsAsync(
            operationContext,
            service,
            datasourceId,
            null,
            include,
            filter,
            cancellationToken
        );

    private static async Task<IResult> ListSchemaViewsAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromQuery] string? include,
        [FromQuery] string? filter,
        CancellationToken cancellationToken = default
    )
    {
        var views = await service
            .GetViewsAsync(operationContext, datasourceId, schemaName, cancellationToken)
            .ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(filter))
        {
            views = views.Where(v =>
                v.ViewName != null
                && v.ViewName.Contains(filter, StringComparison.OrdinalIgnoreCase)
            );
        }

        // Parse the include parameter
        var includes = IncludeParameterHelper.ParseIncludeParameter(include);

        // Shape the response based on the include parameter
        if (!IncludeParameterHelper.ShouldInclude(includes, "definition"))
        {
            // Exclude definitions from all views (default behavior)
            views = views.Select(v => new ViewDto
            {
                SchemaName = v.SchemaName,
                ViewName = v.ViewName,
                Definition = null,
            });
        }

        return Results.Ok(new ViewListResponse(views));
    }

    private static Task<IResult> GetViewAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string viewName,
        [FromQuery] string? include,
        CancellationToken cancellationToken = default
    ) =>
        GetSchemaViewAsync(
            operationContext,
            service,
            datasourceId,
            null,
            viewName,
            include,
            cancellationToken
        );

    private static async Task<IResult> GetSchemaViewAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string viewName,
        [FromQuery] string? include,
        CancellationToken cancellationToken = default
    )
    {
        var view = await service
            .GetViewAsync(operationContext, datasourceId, viewName, schemaName, cancellationToken)
            .ConfigureAwait(false);

        // Parse the include parameter
        var includes = IncludeParameterHelper.ParseIncludeParameter(include);

        // Shape the response based on the include parameter
        if (IncludeParameterHelper.ShouldInclude(includes, "definition"))
        {
            // Return full view with definition
            return Results.Ok(new ViewResponse(view));
        }
        else
        {
            // Return view without definition (default behavior)
            var viewWithoutDefinition = new ViewDto
            {
                SchemaName = view.SchemaName,
                ViewName = view.ViewName,
                Definition = null,
            };
            return Results.Ok(new ViewResponse(viewWithoutDefinition));
        }
    }

    private static Task<IResult> CreateViewAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromBody] ViewDto view,
        CancellationToken cancellationToken = default
    ) =>
        CreateSchemaViewAsync(
            operationContext,
            service,
            datasourceId,
            null,
            view,
            cancellationToken
        );

    private static async Task<IResult> CreateSchemaViewAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromBody] ViewDto view,
        CancellationToken cancellationToken = default
    )
    {
        // API layer validation
        Validate
            .Object(view)
            .NotNullOrWhiteSpace(v => v.ViewName, nameof(ViewDto.ViewName))
            .MaxLength(v => v.ViewName, 128, nameof(ViewDto.ViewName), inclusive: true)
            .MinLength(v => v.ViewName, 1, nameof(ViewDto.ViewName), inclusive: true)
            .NotNullOrWhiteSpace(v => v.Definition, nameof(ViewDto.Definition))
            .Assert();

        // Route parameters take priority
        view.SchemaName = schemaName;

        operationContext.RequestBody = view;

        var created = await service
            .CreateViewAsync(operationContext, datasourceId, view, cancellationToken)
            .ConfigureAwait(false);

        return Results.Created(
            $"{operationContext.EndpointPath?.TrimEnd('/') ?? string.Empty}/{created.ViewName}",
            new ViewResponse(created)
        );
    }

    private static Task<IResult> UpdateViewAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string viewName,
        [FromBody] ViewDto updates,
        CancellationToken cancellationToken = default
    ) =>
        UpdateSchemaViewAsync(
            operationContext,
            service,
            datasourceId,
            null,
            viewName,
            updates,
            cancellationToken
        );

    private static async Task<IResult> UpdateSchemaViewAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string viewName,
        [FromBody] ViewDto updates,
        CancellationToken cancellationToken = default
    )
    {
        // API layer validation
        Validate
            .Object(updates)
            .MaxLength(u => u.ViewName, 128, nameof(ViewDto.ViewName), inclusive: true)
            .Assert();

        // Ensure schema name from DTO matches route parameter
        updates.SchemaName = schemaName;

        operationContext.RequestBody = updates;

        // Check if this is a rename (ViewName in body differs from route parameter)
        var isRename = !string.IsNullOrWhiteSpace(updates.ViewName) && updates.ViewName != viewName;
        var hasPropertyUpdates = !string.IsNullOrWhiteSpace(updates.Definition);

        if (!isRename && !hasPropertyUpdates)
        {
            throw new InvalidOperationException(
                "No changes provided - ViewName or Definition must be specified"
            );
        }

        var currentViewName = viewName;

        ViewDto? updated = null;

        // Handle property updates first
        if (hasPropertyUpdates)
        {
            updated = await service
                .UpdateViewAsync(
                    operationContext,
                    datasourceId,
                    currentViewName,
                    updates,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        // Handle rename separately if needed
        if (isRename)
        {
            var renamed = await service
                .RenameViewAsync(
                    operationContext,
                    datasourceId,
                    currentViewName,
                    updates.ViewName!,
                    schemaName,
                    cancellationToken
                )
                .ConfigureAwait(false);

            return Results.Ok(new ViewResponse(renamed));
        }

        // Get the updated view if only properties were changed
        updated ??= await service
            .GetViewAsync(
                operationContext,
                datasourceId,
                currentViewName,
                schemaName,
                cancellationToken
            )
            .ConfigureAwait(false);

        return Results.Ok(new ViewResponse(updated));
    }

    private static Task<IResult> DropViewAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string viewName,
        CancellationToken cancellationToken = default
    ) =>
        DropSchemaViewAsync(
            operationContext,
            service,
            datasourceId,
            null,
            viewName,
            cancellationToken
        );

    private static async Task<IResult> DropSchemaViewAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string viewName,
        CancellationToken cancellationToken = default
    )
    {
        await service
            .DropViewAsync(operationContext, datasourceId, viewName, schemaName, cancellationToken)
            .ConfigureAwait(false);

        return Results.NoContent();
    }

    private static Task<IResult> ViewExistsAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string viewName,
        CancellationToken cancellationToken = default
    ) =>
        SchemaViewExistsAsync(
            operationContext,
            service,
            datasourceId,
            null,
            viewName,
            cancellationToken
        );

    private static async Task<IResult> SchemaViewExistsAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string viewName,
        CancellationToken cancellationToken = default
    )
    {
        var exists = await service
            .ViewExistsAsync(
                operationContext,
                datasourceId,
                viewName,
                schemaName,
                cancellationToken
            )
            .ConfigureAwait(false);

        return Results.Ok(new ViewExistsResponse(exists));
    }

    private static Task<IResult> QueryViewAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string viewName,
        [FromBody] QueryDto query,
        CancellationToken cancellationToken = default
    ) =>
        QuerySchemaViewAsync(
            operationContext,
            service,
            datasourceId,
            null,
            viewName,
            query,
            cancellationToken
        );

    private static async Task<IResult> QuerySchemaViewAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string viewName,
        [FromBody] QueryDto query,
        CancellationToken cancellationToken = default
    )
    {
        if (!string.IsNullOrWhiteSpace(schemaName))
        {
            // Set it on the request to pass it down
            query.SchemaName = schemaName;
        }
        operationContext.RequestBody = query;

        var queryResult = await service
            .QueryViewAsync(
                operationContext,
                datasourceId,
                viewName,
                query,
                schemaName,
                cancellationToken
            )
            .ConfigureAwait(false);

        return Results.Ok(
            new QueryResponse(queryResult.Data)
            {
                Pagination = queryResult.Pagination,
                Fields = queryResult.Fields,
            }
        );
    }

    private static Task<IResult> QueryViewViaGetAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string viewName,
        CancellationToken cancellationToken = default
    ) =>
        QuerySchemaViewViaGetAsync(
            operationContext,
            service,
            datasourceId,
            null,
            viewName,
            cancellationToken
        );

    private static async Task<IResult> QuerySchemaViewViaGetAsync(
        IOperationContext operationContext,
        IDapperMaticService service,
        [FromRoute] string datasourceId,
        [FromRoute] string? schemaName,
        [FromRoute] string viewName,
        CancellationToken cancellationToken = default
    )
    {
        var query = QueryDto.FromQueryParameters(operationContext.QueryParameters ?? []);
        if (!string.IsNullOrWhiteSpace(schemaName))
        {
            // Set it on the request to pass it down
            query.SchemaName = schemaName;
        }
        operationContext.RequestBody = query;

        var queryResult = await service
            .QueryViewAsync(
                operationContext,
                datasourceId,
                viewName,
                query,
                schemaName,
                cancellationToken
            )
            .ConfigureAwait(false);

        return Results.Ok(
            new QueryResponse(queryResult.Data)
            {
                Pagination = queryResult.Pagination,
                Fields = queryResult.Fields,
            }
        );
    }
}
