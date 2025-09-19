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
using MJCZone.DapperMatic.AspNetCore.Models.Requests;
using MJCZone.DapperMatic.AspNetCore.Models.Responses;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.AspNetCore.Services;
using MJCZone.DapperMatic.AspNetCore.Utilities;
using MJCZone.DapperMatic.Models;

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
        var defaultGroup = app.MapGroup($"{basePath}/d/{{datasourceId}}/v")
            .WithTags(OperationTags.DatasourceViews)
            .WithOpenApi();

        RegisterViewEndpoints(defaultGroup, "DefaultSchema", isSchemaSpecific: false);

        // Register schema-specific endpoints (for multi-tenant scenarios)
        var schemaGroup = app.MapGroup($"{basePath}/d/{{datasourceId}}/s/{{schemaName}}/v")
            .WithTags(OperationTags.DatasourceViews)
            .WithOpenApi();

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
            .MapGet(
                "/",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    [Microsoft.AspNetCore.Mvc.FromQuery] string? include,
                    CancellationToken ct
                ) => ListViewsAsync(ctx, service, user, include, isSchemaSpecific, ct)
            )
            .WithName($"List{namePrefix}Views")
            .WithSummary($"Gets all views for {schemaText}")
            .WithOpenApi(operation =>
            {
                var includeParam = operation.Parameters.FirstOrDefault(p => p.Name == "include");
                if (includeParam != null)
                {
                    includeParam.Description = "Comma-separated list of fields to include in the response. Use 'definition' to include view definitions, or '*' to include all fields. By default, definitions are excluded.";
                    includeParam.Example = new OpenApiString("definition");
                }
                return operation;
            })
            .Produces<ViewListResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Get specific view
        group
            .MapGet(
                "/{viewName}",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string viewName,
                    [Microsoft.AspNetCore.Mvc.FromQuery] string? include,
                    CancellationToken ct
                ) => GetViewAsync(ctx, service, user, viewName, include, isSchemaSpecific, ct)
            )
            .WithName($"Get{namePrefix}View")
            .WithSummary($"Gets a view by name from {schemaText}")
            .WithOpenApi(operation =>
            {
                var includeParam = operation.Parameters.FirstOrDefault(p => p.Name == "include");
                if (includeParam != null)
                {
                    includeParam.Description = "Comma-separated list of fields to include in the response. Use 'definition' to include the view definition, or '*' to include all fields. By default, the definition is excluded.";
                    includeParam.Example = new Microsoft.OpenApi.Any.OpenApiString("definition");
                }
                return operation;
            })
            .Produces<ViewResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Query view via GET with URL parameters
        group
            .MapGet(
                "/{viewName}/query",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string viewName,
                    CancellationToken ct
                ) => QueryViewViaGetAsync(ctx, service, user, viewName, isSchemaSpecific, ct)
            )
            .WithName($"Query{namePrefix}ViewViaGet")
            .WithSummary($"Queries a view {schemaInText} using URL parameters")
            .Produces<QueryResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Create new view
        group
            .MapPost(
                "/",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    CreateViewRequest request,
                    CancellationToken ct
                ) => CreateViewAsync(ctx, service, user, request, isSchemaSpecific, ct)
            )
            .WithName($"Create{namePrefix}View")
            .WithSummary($"Creates a new view {schemaInText}")
            .Produces<ViewResponse>((int)HttpStatusCode.Created)
            .Produces((int)HttpStatusCode.Conflict)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Update existing view
        group
            .MapPut(
                "/{viewName}",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string viewName,
                    UpdateViewRequest request,
                    CancellationToken ct
                ) => UpdateViewAsync(ctx, service, user, viewName, request, isSchemaSpecific, ct)
            )
            .WithName($"Update{namePrefix}View")
            .WithSummary($"Updates an existing view {schemaInText}")
            .Produces<ViewResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Drop view
        group
            .MapDelete(
                "/{viewName}",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string viewName,
                    CancellationToken ct
                ) => DropViewAsync(ctx, service, user, viewName, isSchemaSpecific, ct)
            )
            .WithName($"Drop{namePrefix}View")
            .WithSummary($"Drops a view from {schemaText}")
            .Produces<ViewResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Check if view exists
        group
            .MapGet(
                "/{viewName}/exists",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string viewName,
                    CancellationToken ct
                ) => ViewExistsAsync(ctx, service, user, viewName, isSchemaSpecific, ct)
            )
            .WithName($"{namePrefix}ViewExists")
            .WithSummary($"Checks if a view exists {schemaInText}")
            .Produces<ViewExistsResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        // Query view with filtering, sorting, and pagination
        group
            .MapPost(
                "/{viewName}/query",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string viewName,
                    QueryRequest request,
                    CancellationToken ct
                ) => QueryViewAsync(ctx, service, user, viewName, request, isSchemaSpecific, ct)
            )
            .WithName($"Query{namePrefix}View")
            .WithSummary($"Queries a view {schemaInText} with filtering, sorting, and pagination")
            .Produces<QueryResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);
    }

    private static async Task<IResult> ListViewsAsync(
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
            var views = await service
                .GetViewsAsync(datasourceId, schemaName, user, cancellationToken)
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
                views = views.Select(v => new DmView(v.SchemaName, v.ViewName, string.Empty));
            }

            return Results.Ok(new ViewListResponse(views.ToViewDtos()));
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

    private static async Task<IResult> GetViewAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string viewName,
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
            var view = await service
                .GetViewAsync(datasourceId, viewName, schemaName, user, cancellationToken)
                .ConfigureAwait(false);

            if (view == null)
            {
                var message = isSchemaSpecific
                    ? $"View '{viewName}' not found in schema '{schemaName}' of datasource '{datasourceId}'"
                    : $"View '{viewName}' not found in datasource '{datasourceId}'";
                return Results.NotFound(message);
            }

            // Parse the include parameter
            var includes = IncludeParameterHelper.ParseIncludeParameter(include);

            // Shape the response based on the include parameter
            if (IncludeParameterHelper.ShouldInclude(includes, "definition"))
            {
                // Return full view with definition
                return Results.Ok(new ViewResponse(view.ToViewDto()));
            }
            else
            {
                // Return view without definition (default behavior)
                var viewWithoutDefinition = new DmView(view.SchemaName, view.ViewName, string.Empty);
                return Results.Ok(new ViewResponse(viewWithoutDefinition.ToViewDto()));
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
                detail: ex.Message,
                title: "Internal server error",
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> CreateViewAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        CreateViewRequest request,
        bool isSchemaSpecific,
        CancellationToken cancellationToken = default
    )
    {
        var datasourceId =
            httpContext.Request.RouteValues["datasourceId"]?.ToString() ?? string.Empty;
        var schemaName = isSchemaSpecific
            ? httpContext.Request.RouteValues["schemaName"]?.ToString()
            : request.SchemaName;

        // Get the base path of the request to construct the location URL
        var basePath = httpContext.Request.PathBase.HasValue
            ? httpContext.Request.PathBase.Value
            : string.Empty;
        basePath += httpContext.Request.Path.HasValue
            ? httpContext.Request.Path.Value
            : string.Empty;

        // Validate request
        if (string.IsNullOrWhiteSpace(request.ViewName))
        {
            return Results.BadRequest(
                new ViewResponse(null)
                {
                    Success = false,
                    Message = "View name is required and cannot be empty",
                }
            );
        }

        if (string.IsNullOrWhiteSpace(request.ViewDefinition))
        {
            return Results.BadRequest(
                new ViewResponse(null)
                {
                    Success = false,
                    Message = "View definition is required and cannot be empty",
                }
            );
        }

        try
        {
            var view = new DmView
            {
                SchemaName = schemaName,
                ViewName = request.ViewName,
                Definition = request.ViewDefinition,
            };

            var created = await service
                .CreateViewAsync(datasourceId, view, user, cancellationToken)
                .ConfigureAwait(false);

            return created != null
                ? Results.Created(
                    $"{basePath.TrimEnd('/')}/{created.ViewName}",
                    new ViewResponse(created.ToViewDto())
                    {
                        Success = true,
                        Message = isSchemaSpecific
                            ? $"View '{created.ViewName}' created successfully in schema '{schemaName}'"
                            : $"View '{created.ViewName}' created successfully",
                    }
                )
                : Results.Conflict(
                    new ViewResponse(null)
                    {
                        Success = false,
                        Message = isSchemaSpecific
                            ? $"View '{view.ViewName}' already exists in schema '{schemaName}'"
                            : $"View '{view.ViewName}' already exists",
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
                detail: $"Failed to create view: {ex.Message}",
                title: "Internal server error",
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> UpdateViewAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string viewName,
        UpdateViewRequest request,
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
        if (string.IsNullOrWhiteSpace(request.ViewDefinition))
        {
            return Results.BadRequest(
                new ViewResponse(null)
                {
                    Success = false,
                    Message = "View definition is required and cannot be empty",
                }
            );
        }

        try
        {
            var updated = await service
                .UpdateViewAsync(
                    datasourceId,
                    viewName,
                    request.NewViewName,
                    request.ViewDefinition,
                    schemaName,
                    user,
                    cancellationToken
                )
                .ConfigureAwait(false);

            return updated != null
                ? Results.Ok(
                    new ViewResponse(updated.ToViewDto())
                    {
                        Success = true,
                        Message = isSchemaSpecific
                            ? $"View '{viewName}' updated successfully in schema '{schemaName}'"
                            : $"View '{viewName}' updated successfully",
                    }
                )
                : Results.NotFound(
                    new ViewResponse(null)
                    {
                        Success = false,
                        Message = isSchemaSpecific
                            ? $"View '{viewName}' not found in schema '{schemaName}'"
                            : $"View '{viewName}' not found",
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
                detail: $"Failed to update view: {ex.Message}",
                title: "Internal server error",
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> DropViewAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string viewName,
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
                .DropViewAsync(datasourceId, viewName, schemaName, user, cancellationToken)
                .ConfigureAwait(false);

            return success
                ? Results.Ok(
                    new ViewResponse(null)
                    {
                        Success = true,
                        Message = isSchemaSpecific
                            ? $"View '{viewName}' dropped successfully from schema '{schemaName}'"
                            : $"View '{viewName}' dropped successfully",
                    }
                )
                : Results.NotFound(
                    new ViewResponse(null)
                    {
                        Success = false,
                        Message = isSchemaSpecific
                            ? $"View '{viewName}' not found in schema '{schemaName}'"
                            : $"View '{viewName}' not found",
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
                detail: $"Failed to drop view: {ex.Message}",
                title: "Internal server error",
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> ViewExistsAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string viewName,
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
                .ViewExistsAsync(datasourceId, viewName, schemaName, user, cancellationToken)
                .ConfigureAwait(false);

            return Results.Ok(new ViewExistsResponse(exists) { Success = true });
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

    private static async Task<IResult> QueryViewAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string viewName,
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
                .QueryViewAsync(
                    datasourceId,
                    viewName,
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
                        ? $"Query executed successfully on view '{viewName}' in schema '{schemaName}'. Returned {result.Data.Count()} records."
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
                ? $"View '{viewName}' not found in schema '{schemaName}' of datasource '{datasourceId}'"
                : $"View '{viewName}' not found in datasource '{datasourceId}'";
            return Results.NotFound(message);
        }
        catch (Exception ex)
        {
            var detail = isSchemaSpecific
                ? $"Failed to query view in schema: {ex.Message}"
                : $"Failed to query view: {ex.Message}";
            return Results.Problem(detail: detail, title: "Internal server error", statusCode: 500);
        }
    }

    private static async Task<IResult> QueryViewViaGetAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string viewName,
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
                .QueryViewAsync(
                    datasourceId,
                    viewName,
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
                        ? $"Query executed successfully on view '{viewName}' in schema '{schemaName}'. Returned {result.Data.Count()} records."
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
                ? $"View '{viewName}' not found in schema '{schemaName}' of datasource '{datasourceId}'"
                : $"View '{viewName}' not found in datasource '{datasourceId}'";
            return Results.NotFound(message);
        }
        catch (Exception ex)
        {
            var detail = isSchemaSpecific
                ? $"Failed to query view in schema: {ex.Message}"
                : $"Failed to query view: {ex.Message}";
            return Results.Problem(detail: detail, title: "Internal server error", statusCode: 500);
        }
    }
}
