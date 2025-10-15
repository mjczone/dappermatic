// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace MJCZone.DapperMatic.AspNetCore;

/// <summary>
/// Default implementation of IOperationContextInitializer that populates basic HTTP context information.
/// </summary>
public partial class OperationContextInitializer : IOperationContextInitializer
{
    /// <summary>
    /// List of HTTP headers to include in the operation context for potential custom initialization.
    /// </summary>
    /// <remarkes>
    /// This list can be modified at application startup to include additional headers as needed.
    /// This list is NOT thread-safe to modify at runtime. It's here for convenience.
    /// </remarkes>
#pragma warning disable CA2211 // Non-constant fields should not be visible
#pragma warning disable SA1401 // Fields should be private
    public static string[] HeadersToInclude =
    [
        HeaderNames.UserAgent,
        HeaderNames.Accept,
        HeaderNames.AcceptEncoding,
        HeaderNames.AcceptLanguage,
        HeaderNames.Cookie,
        HeaderNames.Referer,
        HeaderNames.Authorization,
        HeaderNames.ContentType,
        // Add cloudflare headers
        "True-Client-IP", // the original client IP address (Enterprise customers only)
        "CF-Connecting-IP", // the client IP address connecting to Cloudflare
        "CF-Ray", // hashed value that encodes information about the data center and the visitor's request
        "CF-IPCountry", // two-character country code of the originating visitor's country (see https://www.iso.org/iso-3166-country-codes.html)
    ];
#pragma warning restore SA1401 // Fields should be private
#pragma warning restore CA2211 // Non-constant fields should not be visible

    /// <summary>
    /// Initializes a new instance of the <see cref="OperationContextInitializer"/> class.
    /// </summary>
    /// <remarks>
    /// This class has no dependencies and can be used as-is or as a base class for custom initializers.
    /// </remarks>
    /// <param name="context">The operation context to initialize.</param>
    /// <param name="httpContext">The HTTP context for the current request.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task InitializeAsync(IOperationContext context, HttpContext httpContext)
    {
        var basePathFromOptions =
            httpContext
                .RequestServices.GetService<IOptions<DapperMaticOptions>>()
                ?.Value.BasePath?.TrimEnd('/') ?? string.Empty;
        var endpoint = httpContext.GetEndpoint() as RouteEndpoint;
        var routePattern = endpoint?.RoutePattern.RawText; // e.g., "/api/d/{datasourceId}"

        string? routePath = null;
        if (routePattern != null)
        {
            // If base path is set, strip it from the route pattern
            if (
                !string.IsNullOrWhiteSpace(basePathFromOptions)
                && routePattern.StartsWith(basePathFromOptions, StringComparison.OrdinalIgnoreCase)
            )
            {
                routePattern = routePattern[basePathFromOptions.Length..];
            }

            // Extract the route path by removing route parameters
            if (string.IsNullOrWhiteSpace(routePattern))
            {
                routePath = string.Empty;
            }
            else
            {
                routePath = RouteValueStripperRegex().Replace(routePattern, string.Empty);
                routePath = DoubleForwardSlashReducerRegex().Replace(routePath, "/").Trim('/');
            }
        }

        // If not a DapperMatic route, skip initialization
        if (routePath == null)
        {
            return Task.CompletedTask;
        }

        // There's always the possibility that the context has already been initialized.
        // Don't overwrite existing values, if a calling code has already set them.
        // This can happen if an initializer implements this one and calls base.InitializeAsync after setting some values.
        // The recommendation for custom initializers is to call this base method first, then set/override any custom values.

        // Populate user information
        context.User ??= httpContext.User;

        // Set request ID for correlation
        context.RequestId ??= httpContext.TraceIdentifier;

        // Extract IP address (handle proxies with X-Forwarded-For)
        context.IpAddress ??= GetClientIpAddress(httpContext);

        // Populate HTTP context information in Properties for potential endpoint inference
        context.HttpMethod ??= httpContext.Request.Method;
        context.EndpointPath ??= httpContext.Request.Path.Value ?? string.Empty;
        context.QueryParameters ??= new Dictionary<string, StringValues>(
            StringComparer.OrdinalIgnoreCase
        );
        context.RouteValues ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        context.HeaderValues ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Now populate query parameters and route values
        foreach (var kvp in httpContext.Request.Query)
        {
            // Do not overwrite existing values
            context.QueryParameters.TryAdd(kvp.Key, kvp.Value);
        }

        // Now populate route values
        foreach (var kvp in httpContext.Request.RouteValues)
        {
            var valueStr = kvp.Value?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(valueStr))
            {
                // Do not overwrite existing values
                context.RouteValues.TryAdd(kvp.Key, valueStr);
            }
        }

        // Build a human-readable operation name from the route path and HTTP method
        // This must be done AFTER populating route values since it depends on them
        context.Operation ??= BuildOperationName(
            routePath,
            httpContext.Request.Method,
            context.RouteValues
        );

        // Now populate header values
        if (httpContext.Request.Headers.Count > 0)
        {
            // Only include commonly useful headers to avoid bloating the context
            foreach (var headerName in HeadersToInclude)
            {
                if (httpContext.Request.Headers.TryGetValue(headerName, out var headerValue))
                {
                    context.HeaderValues.TryAdd(headerName, headerValue.ToString());
                }
            }
        }

        // Add route values if available (useful for endpoint inference)
        if (context.RouteValues.Count > 0)
        {
            // If known route values exist, set them in the context
            if (
                string.IsNullOrWhiteSpace(context.DatasourceId)
                && TryGetRouteValue(
                    context.RouteValues,
                    nameof(context.DatasourceId),
                    out var dsIdStr
                )
            )
            {
                context.DatasourceId = dsIdStr;
            }

            if (
                string.IsNullOrWhiteSpace(context.SchemaName)
                && TryGetRouteValue(
                    context.RouteValues,
                    nameof(context.SchemaName),
                    out var schemaStr
                )
            )
            {
                context.SchemaName = schemaStr;
            }

            if (
                string.IsNullOrWhiteSpace(context.TableName)
                && TryGetRouteValue(
                    context.RouteValues,
                    nameof(context.TableName),
                    out var tableStr
                )
            )
            {
                context.TableName = tableStr;
            }

            if (
                string.IsNullOrWhiteSpace(context.ViewName)
                && TryGetRouteValue(context.RouteValues, nameof(context.ViewName), out var viewStr)
            )
            {
                context.ViewName = viewStr;
            }

            if (
                string.IsNullOrWhiteSpace(context.ColumnName)
                && TryGetRouteValue(
                    context.RouteValues,
                    nameof(context.ColumnName),
                    out var columnStr
                )
            )
            {
                context.ColumnName = columnStr;
            }

            if (
                string.IsNullOrWhiteSpace(context.IndexName)
                && TryGetRouteValue(
                    context.RouteValues,
                    nameof(context.IndexName),
                    out var indexStr
                )
            )
            {
                context.IndexName = indexStr;
            }

            if (
                string.IsNullOrWhiteSpace(context.ConstraintName)
                && TryGetRouteValue(
                    context.RouteValues,
                    nameof(context.ConstraintName),
                    out var constraintStr
                )
            )
            {
                context.ConstraintName = constraintStr;
            }
        }

        return Task.CompletedTask;
    }

    private bool TryGetRouteValue(
        Dictionary<string, string> routeValues,
        string key,
        out string dsIdStr
    )
    {
        if (
            // TryGetValue but ignore case
            routeValues.TryGetValue(key, out var dsIdStrVal)
            && !string.IsNullOrWhiteSpace(dsIdStrVal)
        )
        {
            dsIdStr = dsIdStrVal;
            return true;
        }
        dsIdStr = null!;
        return false;
    }

    /// <summary>
    /// Extracts the client IP address from the HTTP context, handling proxy scenarios.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    /// <returns>The client IP address, or null if it cannot be determined.</returns>
    private string? GetClientIpAddress(HttpContext httpContext)
    {
        // Check Cloudflare headers first (for proxy scenarios)
        var trueClientIp = httpContext.Request.Headers["True-Client-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(trueClientIp))
        {
            return trueClientIp.Trim();
        }

        var cloudflareIp = httpContext.Request.Headers["CF-Connecting-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(cloudflareIp))
        {
            return cloudflareIp.Trim();
        }

        // Check X-Forwarded-For header first (for proxy scenarios)
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // X-Forwarded-For can contain multiple IPs (client, proxy1, proxy2, etc.)
            // The first one is typically the original client IP
            return forwardedFor.Split(',')[0].Trim();
        }

        // Check X-Real-IP header (used by some proxies)
        var realIp = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp.Trim();
        }

        // Fall back to direct connection IP
        return httpContext.Connection.RemoteIpAddress?.ToString();
    }

    /// <summary>
    /// Builds a human-readable operation name from the route path and HTTP method.
    /// </summary>
    /// <param name="routePath">The route path with parameters stripped (e.g., "d", "d/t/columns").</param>
    /// <param name="httpMethod">The HTTP method (GET, POST, PUT, PATCH, DELETE).</param>
    /// <param name="routeValues">The route values containing parameter names and values.</param>
    /// <returns>A human-readable operation name (e.g., "datasources/add", "tables/list").</returns>
    private string BuildOperationName(
        string routePath,
        string httpMethod,
        IDictionary<string, string>? routeValues
    )
    {
        if (string.IsNullOrWhiteSpace(routePath))
        {
            return $"unknown/{httpMethod.ToLowerInvariant()}";
        }

        // Normalize the route path by removing schema-specific segments
        // e.g., "d/s/t" becomes "d/t", "d/s/t/columns" becomes "d/t/columns"
        var normalizedPath = routePath.Replace("/s/", "/", StringComparison.Ordinal);

        // Split path into segments
        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        // Determine resource name and operation verb
        string resourceName;
        string operationVerb;

        // Check for special sub-paths first (test, exists, query, columns/{columnName})
        if (routeValues != null)
        {
            // Check for special datasource operations
            if (normalizedPath == "d/test")
            {
                return "datasources/test";
            }

            // Check for table/view operations with special paths
            if (
                normalizedPath.StartsWith("d/t/", StringComparison.Ordinal)
                || normalizedPath.StartsWith("d/v/", StringComparison.Ordinal)
            )
            {
                var isTable = normalizedPath.StartsWith("d/t/", StringComparison.Ordinal);
                var resourceType = isTable ? "tables" : "views";

                if (normalizedPath.EndsWith("/exists", StringComparison.Ordinal))
                {
                    return $"{resourceType}/exists";
                }
                if (normalizedPath.EndsWith("/query", StringComparison.Ordinal))
                {
                    return $"{resourceType}/query";
                }
            }

            // Check for default constraint column-specific operations
            if (normalizedPath.Contains("/default-constraints/columns/", StringComparison.Ordinal))
            {
                if (httpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
                {
                    return "default-constraints/get-on-column";
                }
                if (httpMethod.Equals("DELETE", StringComparison.OrdinalIgnoreCase))
                {
                    return "default-constraints/drop-on-column";
                }
            }
        }

        // Map route path segments to resource names
        switch (normalizedPath)
        {
            case "d":
                resourceName = "datasources";
                break;
            case "d/datatypes":
                resourceName = "datatypes";
                break;
            case "d/s":
                resourceName = "schemas";
                break;
            case "d/t":
                resourceName = "tables";
                break;
            case "d/v":
                resourceName = "views";
                break;
            case "d/t/columns":
                resourceName = "columns";
                break;
            case "d/t/indexes":
                resourceName = "indexes";
                break;
            case "d/t/check-constraints":
                resourceName = "check-constraints";
                break;
            case "d/t/default-constraints":
                resourceName = "default-constraints";
                break;
            case "d/t/foreign-key-constraints":
                resourceName = "foreign-key-constraints";
                break;
            case "d/t/primary-key-constraint":
                resourceName = "primary-key-constraint";
                break;
            case "d/t/unique-constraints":
                resourceName = "unique-constraints";
                break;
            default:
                // Fallback: use the last segment as resource name
                resourceName = segments.Length > 0 ? segments[^1] : "unknown";
                break;
        }

        // Determine if the operation targets a specific resource (singular) or a collection (plural)
        bool isSingularOperation = routeValues != null && DetermineIsSingularOperation(normalizedPath, routeValues);

        // Map HTTP method to operation verb
        operationVerb = MapHttpMethodToVerb(httpMethod, resourceName, isSingularOperation);

        return $"{resourceName}/{operationVerb}";
    }

    /// <summary>
    /// Determines if the operation targets a specific resource instance (singular) or a collection (plural).
    /// </summary>
    private bool DetermineIsSingularOperation(string normalizedPath, IDictionary<string, string> routeValues)
    {
        // Check for resource-specific route parameters
        if (normalizedPath.StartsWith("d/", StringComparison.Ordinal))
        {
            // Datasources
            if (normalizedPath == "d" && routeValues.ContainsKey("datasourceId"))
            {
                return true;
            }

            // Schemas
            if (normalizedPath == "d/s" && routeValues.ContainsKey("schemaName"))
            {
                return true;
            }

            // Tables
            if (normalizedPath == "d/t" && routeValues.ContainsKey("tableName"))
            {
                return true;
            }

            // Views
            if (normalizedPath == "d/v" && routeValues.ContainsKey("viewName"))
            {
                return true;
            }

            // Columns
            if (normalizedPath == "d/t/columns" && routeValues.ContainsKey("columnName"))
            {
                return true;
            }

            // Indexes
            if (normalizedPath == "d/t/indexes" && routeValues.ContainsKey("indexName"))
            {
                return true;
            }

            // Constraints (check, default, foreign-key, unique)
            if (
                (
                    normalizedPath == "d/t/check-constraints"
                    || normalizedPath == "d/t/default-constraints"
                    || normalizedPath == "d/t/foreign-key-constraints"
                    || normalizedPath == "d/t/unique-constraints"
                ) && routeValues.ContainsKey("constraintName")
            )
            {
                return true;
            }

            // Primary key constraint doesn't use constraintName in route, but has special handling
            if (normalizedPath == "d/t/primary-key-constraint")
            {
                // Primary key operations are always singular (get/create/drop the PK for a table)
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Maps HTTP method and singularity to an operation verb.
    /// </summary>
    private string MapHttpMethodToVerb(string httpMethod, string resourceName, bool isSingular)
    {
        return httpMethod.ToUpperInvariant() switch
        {
            "GET" => isSingular ? "get" : "list",
            "POST"
                => resourceName switch
                {
                    "datasources" => "add",
                    "columns" => "add",
                    "schemas" or "tables" or "views" or "indexes" => "create",
                    _
                        when resourceName.EndsWith("-constraints", StringComparison.Ordinal)
                            || resourceName.EndsWith("-constraint", StringComparison.Ordinal)
                        => "create",
                    _ => "create",
                },
            "PUT" or "PATCH"
                => resourceName switch
                {
                    "datasources" => "update",
                    "tables" or "views" => "rename",
                    "columns" => "rename",
                    _ => "update",
                },
            "DELETE"
                => resourceName switch
                {
                    "datasources" => "remove",
                    "schemas" or "tables" or "views" or "indexes" => "drop",
                    "columns" => "drop",
                    _
                        when resourceName.EndsWith("-constraints", StringComparison.Ordinal)
                            || resourceName.EndsWith("-constraint", StringComparison.Ordinal)
                        => "drop",
                    _ => "delete",
                },
            _ => httpMethod.ToLowerInvariant(),
        };
    }

    [GeneratedRegex(@"/+")]
    private static partial Regex DoubleForwardSlashReducerRegex();

    [GeneratedRegex(@"\{[^}]*\}")]
    private static partial Regex RouteValueStripperRegex();
}
