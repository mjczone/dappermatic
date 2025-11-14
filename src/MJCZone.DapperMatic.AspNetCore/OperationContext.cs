// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using MJCZone.DapperMatic.AspNetCore.Auditing;

namespace MJCZone.DapperMatic.AspNetCore;

/// <summary>
/// Context for DapperMatic operations.
/// </summary>
public class OperationContext : IOperationContext
{
    /// <summary>
    /// Gets or sets the user's claims principal.
    /// </summary>
    public ClaimsPrincipal? User { get; set; }

    /// <summary>
    /// Gets or sets the operation being performed (e.g., "datasources/post", "tables/put").
    /// </summary>
    public string? Operation { get; set; }

    /// <summary>
    /// Gets or sets the name of the datasource being accessed, if applicable.
    /// </summary>
    public string? DatasourceId { get; set; }

    /// <summary>
    /// Gets or sets the name of the schema being accessed, if applicable.
    /// </summary>
    public string? SchemaName { get; set; }

    /// <summary>
    /// Gets or sets the name of the table being accessed, if applicable.
    /// </summary>
    public string? TableName { get; set; }

    /// <summary>
    /// Gets or sets the name of the view being accessed, if applicable.
    /// </summary>
    public string? ViewName { get; set; }

    /// <summary>
    /// Gets or sets the name of the columns being accessed, if applicable.
    /// </summary>
    public List<string>? ColumnNames { get; set; }

    /// <summary>
    /// Gets or sets the name of the index being accessed, if applicable.
    /// </summary>
    public string? IndexName { get; set; }

    /// <summary>
    /// Gets or sets the name of the constraint being accessed, if applicable.
    /// </summary>
    public string? ConstraintName { get; set; }

    /// <summary>
    /// Gets or sets the HTTP method (GET, POST, etc.).
    /// </summary>
    public string? HttpMethod { get; set; }

    /// <summary>
    /// Gets or sets the endpoint path.
    /// </summary>
    public string? EndpointPath { get; set; }

    /// <summary>
    /// Gets or sets the request payload, if any.
    /// </summary>
    public object? RequestBody { get; set; }

    /// <summary>
    /// Gets or sets the query parameters, if any.
    /// </summary>
    public Dictionary<string, StringValues>? QueryParameters { get; set; }

    /// <summary>
    /// Gets or sets the route values, if any.
    /// </summary>
    public Dictionary<string, string>? RouteValues { get; set; }

    /// <summary>
    /// Gets or sets the header values as a case-insensitive dictionary, if any.
    /// </summary>
    public Dictionary<string, string>? HeaderValues { get; set; }

    /// <summary>
    /// Gets or sets the IP address of the client.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Gets or sets the request ID for correlating logs.
    /// </summary>
    public string? RequestId { get; set; }

    /// <summary>
    /// Gets or sets additional properties for custom authorization logic.
    /// </summary>
    public Dictionary<string, object>? Properties { get; set; }

    /// <summary>
    /// Gets the request body as a specific type.
    /// </summary>
    /// <typeparam name="T">The type to cast the request body to.</typeparam>
    /// <returns>The request body as type T, or default if the cast fails.</returns>
    public T? GetRequest<T>()
        where T : class
    {
        return RequestBody as T;
    }

    /// <summary>
    /// Gets a query parameter value by key.
    /// </summary>
    /// <param name="key">The query parameter key.</param>
    /// <returns>The query parameter value, or null if not found.</returns>
    public string? GetQueryParameter(string key)
    {
        return QueryParameters?[key];
    }
}

/// <summary>
/// Extension methods for OperationContext.
/// </summary>
public static class OperationContextExtensions
{
    /// <summary>
    /// Converts the operation context to an audit event.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="success">Indicates if the operation was successful.</param>
    /// <param name="message">Optional message describing the operation outcome.</param>
    /// <returns>The corresponding audit event.</returns>
    public static DapperMaticAuditEvent ToAuditEvent(
        this IOperationContext context,
        bool success,
        string? message = null
    )
    {
        return new DapperMaticAuditEvent
        {
            UserIdentifier =
                context.User?.Identity?.Name
                ?? context.User?.FindFirst(ClaimTypes.Name)?.Value
                ?? context.User?.FindFirst("sub")?.Value
                ?? "Anonymous",
            Operation = context.Operation ?? string.Empty,
            DatasourceId = context.DatasourceId,
            SchemaName = context.SchemaName,
            TableName = context.TableName,
            ViewName = context.ViewName,
            ColumnNames = context.ColumnNames,
            IndexName = context.IndexName,
            ConstraintName = context.ConstraintName,
            Success = success,
            Message = message,
            IpAddress = context.IpAddress,
            RequestId = context.RequestId,
            Timestamp = DateTimeOffset.UtcNow,
        };
    }
}
