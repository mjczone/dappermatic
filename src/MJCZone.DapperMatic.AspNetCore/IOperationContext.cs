// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace MJCZone.DapperMatic.AspNetCore;

/// <summary>
/// Interface for DapperMatic operation context.
/// </summary>
public interface IOperationContext
{
    /// <summary>
    /// Gets or sets the user's claims principal.
    /// </summary>
    ClaimsPrincipal? User { get; set; }

    /// <summary>
    /// Gets or sets the operation being performed (e.g., "datasources/get", "datasources/add").
    /// </summary>
    string? Operation { get; set; }

    /// <summary>
    /// Gets or sets the name of the datasource being accessed, if applicable.
    /// </summary>
    string? DatasourceId { get; set; }

    /// <summary>
    /// Gets or sets the name of the schema being accessed, if applicable.
    /// </summary>
    string? SchemaName { get; set; }

    /// <summary>
    /// Gets or sets the name of the table being accessed, if applicable.
    /// </summary>
    string? TableName { get; set; }

    /// <summary>
    /// Gets or sets the name of the view being accessed, if applicable.
    /// </summary>
    string? ViewName { get; set; }

    /// <summary>
    /// Gets or sets the name of the column being accessed, if applicable.
    /// </summary>
    string? ColumnName { get; set; }

    /// <summary>
    /// Gets or sets the name of the index being accessed, if applicable.
    /// </summary>
    string? IndexName { get; set; }

    /// <summary>
    /// Gets or sets the name of the constraint being accessed, if applicable.
    /// </summary>
    string? ConstraintName { get; set; }

    /// <summary>
    /// Gets or sets the HTTP method (GET, POST, etc.).
    /// </summary>
    string? HttpMethod { get; set; }

    /// <summary>
    /// Gets or sets the endpoint path.
    /// </summary>
    string? EndpointPath { get; set; }

    /// <summary>
    /// Gets or sets the request payload, if any.
    /// </summary>
    object? RequestBody { get; set; }

    /// <summary>
    /// Gets or sets the query parameters as a case-insensitive dictionary, if any.
    /// </summary>
    Dictionary<string, StringValues>? QueryParameters { get; set; }

    /// <summary>
    /// Gets or sets the route values as a case-insensitive dictionary, if any.
    /// </summary>
    Dictionary<string, string>? RouteValues { get; set; }

    /// <summary>
    /// Gets or sets the header values as a case-insensitive dictionary, if any.
    /// </summary>
    Dictionary<string, string>? HeaderValues { get; set; }

    /// <summary>
    /// Gets or sets the IP address of the client.
    /// </summary>
    string? IpAddress { get; set; }

    /// <summary>
    /// Gets or sets the request ID for correlating logs.
    /// </summary>
    string? RequestId { get; set; }

    /// <summary>
    /// Gets or sets additional properties for custom authorization logic.
    /// </summary>
    Dictionary<string, object>? Properties { get; set; }

    /// <summary>
    /// Gets the request body as a specific type.
    /// </summary>
    /// <typeparam name="T">The type to cast the request body to.</typeparam>
    /// <returns>The request body as type T, or default if the cast fails.</returns>
    T? GetRequest<T>()
        where T : class;

    /// <summary>
    /// Gets a query parameter value by key.
    /// </summary>
    /// <param name="key">The query parameter key.</param>
    /// <returns>The query parameter value, or null if not found.</returns>
    string? GetQueryParameter(string key);
}