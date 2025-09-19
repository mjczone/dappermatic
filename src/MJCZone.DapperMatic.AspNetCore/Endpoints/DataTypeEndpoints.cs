// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MJCZone.DapperMatic.AspNetCore.Extensions;
using MJCZone.DapperMatic.AspNetCore.Models.Responses;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.AspNetCore.Services;

namespace MJCZone.DapperMatic.AspNetCore.Endpoints;

/// <summary>
/// Extension methods for registering DapperMatic data type endpoints.
/// </summary>
public static class DataTypeEndpoints
{
    /// <summary>
    /// Maps all DapperMatic data type endpoints to the specified route builder.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <param name="basePath">The base path for the API endpoints. Defaults to "/api/dm".</param>
    /// <returns>The route builder for method chaining.</returns>
    public static IEndpointRouteBuilder MapDapperMaticDataTypeEndpoints(
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

        var group = app.MapGroup($"{basePath}/d/{{datasourceId}}/datatypes")
            .WithTags(OperationTags.DatasourceDataTypes)
            .WithOpenApi();

        // Get available data types for a specific datasource
        group
            .MapGet(
                "/",
                (
                    HttpContext ctx,
                    IDapperMaticService service,
                    ClaimsPrincipal user,
                    string datasourceId,
                    [Microsoft.AspNetCore.Mvc.FromQuery] string? include,
                    CancellationToken ct
                ) => GetDatasourceDataTypesAsync(ctx, service, user, datasourceId, include, ct)
            )
            .WithName("GetDatasourceDataTypes")
            .WithSummary("Gets all available data types for a specific datasource")
            .WithDescription(
                "Returns a list of data types available in the specified datasource, including provider-specific types, extensions, and custom types. Use include=customTypes to discover user-defined types from the database."
            )
            .WithOpenApi(operation =>
            {
                var includeParam = operation.Parameters.FirstOrDefault(p => p.Name == "include");
                if (includeParam != null)
                {
                    includeParam.Description = "Optional parameter to include additional data. Use 'customTypes' to discover user-defined types from the database (PostgreSQL domains, enums, composite types).";
                    includeParam.Example = new Microsoft.OpenApi.Any.OpenApiString("customTypes");
                }
                return operation;
            })
            .Produces<DataTypesResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        return app;
    }

    private static async Task<IResult> GetDatasourceDataTypesAsync(
        HttpContext httpContext,
        IDapperMaticService service,
        ClaimsPrincipal user,
        string datasourceId,
        string? include,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            // Parse include parameter to determine if custom types should be included
            var includeCustomTypes = !string.IsNullOrWhiteSpace(include) &&
                include.Contains("customTypes", StringComparison.OrdinalIgnoreCase);

            var (providerName, dataTypes) = await service
                .GetDatasourceDataTypesAsync(datasourceId, user, includeCustomTypes, cancellationToken)
                .ConfigureAwait(false);

            var dataTypeDtos = dataTypes.ToDataTypeDtos()
                .OrderBy(dt => dt.Category)
                .ThenBy(dt => dt.DataType)
                .ToList();
            return Results.Ok(new DataTypesResponse(providerName, dataTypeDtos));
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
            return Results.Problem(detail: ex.Message, title: "Internal server error", statusCode: 500);
        }
    }
}
