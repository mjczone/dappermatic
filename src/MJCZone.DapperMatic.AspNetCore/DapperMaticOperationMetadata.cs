// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore;

/// <summary>
/// The name of the operation an endpoint performs (<c>tables/query</c>, <c>datasources/post</c>, ...), declared when the
/// endpoint is mapped: what permissions, policies, telemetry and audit events call it. DapperMatic names its own
/// endpoints from their routes once, at startup; an endpoint a package maps itself may declare its name.
/// </summary>
public sealed class DapperMaticOperationMetadata
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticOperationMetadata"/> class.
    /// </summary>
    /// <param name="name">The operation's name, or null for an endpoint answering several methods, named per request.</param>
    public DapperMaticOperationMetadata(string? name)
    {
        Name = name;
    }

    /// <summary>Gets the operation's name, or null when the request's method names it.</summary>
    public string? Name { get; }

    /// <summary>
    /// Gets a value indicating whether the endpoint takes WebSockets whose callers sign in on the socket, after the
    /// request that opened it, as GraphQL's does. A WebSocket request to such an endpoint gets no session from that
    /// request: the endpoint builds one when the caller signs in. Every other request, to any endpoint, is its caller's
    /// session, whatever headers it carries.
    /// </summary>
    public bool SignsInOnSocket { get; init; }

    /// <summary>
    /// Gets a value indicating whether the endpoint opens a stream a browser may sign in to with a ticket
    /// (<c>?ticket=</c>, from <c>POST …/auth/ticket</c>): a subscription, a socket. Elsewhere a ticket is not read.
    /// </summary>
    public bool AcceptsTickets { get; init; }

    /// <summary>
    /// Names the operation of a DapperMatic route and method: the resource the route is about, from its pattern (whose
    /// names and ids are placeholders, so a table named "columns" is not read as columns), and the verb.
    /// </summary>
    /// <param name="routePattern">The route pattern.</param>
    /// <param name="method">The HTTP method.</param>
    /// <returns>The name, <c>{resource}/{verb}</c>.</returns>
    internal static string NameOf(string? routePattern, string method)
    {
        var resource = ResourceOf(routePattern);
        return $"{resource}/{VerbOf(routePattern ?? string.Empty, resource, method)}";
    }

    // The resource type is read from the route pattern, whose names and ids are placeholders: from the request's own
    // path, a table named "columns" or "v" would read as columns or views, and be authorized as them.
    private static string ResourceOf(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "unknown";
        }

        // Split path into segments
        var segments = path.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );

        var normalizedSegments = new List<string>();
        foreach (var segment in segments)
        {
            // Skip base path segments
            var segmentLowerCase = segment.ToLowerInvariant();
            switch (segmentLowerCase)
            {
                case "d":
                    normalizedSegments.Add("datasources");
                    break;
                case "s":
                    normalizedSegments.Add("schemas");
                    break;
                case "t":
                    normalizedSegments.Add("tables");
                    break;
                case "v":
                    normalizedSegments.Add("views");
                    break;
                case "f":
                    normalizedSegments.Add("functions");
                    break;
                case "batch":
                case "openapi":
                case "access":
                case "policies":
                case "tenants":
                case "webhooks":
                case "datatypes":
                case "columns":
                case "indexes":
                case "check-constraints":
                case "default-constraints":
                case "foreign-key-constraints":
                case "primary-key-constraint":
                case "unique-constraints":
                    normalizedSegments.Add(segmentLowerCase);
                    break;
            }
        }

        return normalizedSegments.LastOrDefault() ?? "unknown";
    }

    private static string VerbOf(string routePattern, string resourceType, string method)
    {
        var methodLowerCase = method.ToLowerInvariant();
        if (methodLowerCase == "post")
        {
            if (routePattern.EndsWith("/test", StringComparison.OrdinalIgnoreCase) && resourceType == "datasources")
            {
                return "test";
            }
            if (
                routePattern.EndsWith("/query", StringComparison.OrdinalIgnoreCase)
                && (resourceType == "tables" || resourceType == "views")
            )
            {
                return "query";
            }

            // Writes of rows, which permissions tell apart from creating a table.
            if (resourceType == "tables")
            {
                if (routePattern.EndsWith("/rows", StringComparison.OrdinalIgnoreCase))
                {
                    return "insert-rows";
                }

                if (routePattern.EndsWith("/rows/save", StringComparison.OrdinalIgnoreCase))
                {
                    return "save-rows";
                }

                if (routePattern.EndsWith("/rows/update", StringComparison.OrdinalIgnoreCase))
                {
                    return "update-rows";
                }

                if (routePattern.EndsWith("/rows/delete", StringComparison.OrdinalIgnoreCase))
                {
                    return "delete-rows";
                }
            }

            return "post";
        }

        if (methodLowerCase == "put")
        {
            return "put";
        }

        if (methodLowerCase == "delete")
        {
            return "delete";
        }

        if (methodLowerCase == "patch")
        {
            return "patch";
        }

        if (methodLowerCase == "get")
        {
            if (routePattern.EndsWith("/test", StringComparison.OrdinalIgnoreCase) && resourceType == "datasources")
            {
                return "test";
            }
            // A subscription is a query, run again as its tables change.
            if (
                (routePattern.EndsWith("/query", StringComparison.OrdinalIgnoreCase) || routePattern.EndsWith("/subscribe", StringComparison.OrdinalIgnoreCase))
                && (resourceType == "tables" || resourceType == "views")
            )
            {
                return "query";
            }

            // If the route pattern ends with a parameter, it's a "get" operation
            return routePattern.EndsWith('}') || routePattern.EndsWith("/exists", StringComparison.OrdinalIgnoreCase)
                ? "get"
                : "list";
        }

        return "unknown";
    }
}
