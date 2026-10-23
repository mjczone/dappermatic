// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using MJCZone.DapperMatic.AspNetCore.Auditing;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore;

/// <summary>
/// One operation a caller asks of DapperMatic: its name (<c>tables/query</c>, <c>datasources/post</c>), what it acts on,
/// and who asks. What permissions, policies and audit events are about.
/// </summary>
/// <remarks>
/// An operation never changes once made: checking a request as another operation (a table it reads besides its own, a
/// rename's new name) makes a copy, <c>operation with { TableName = "other" }</c>. Operations running side by side
/// for one caller (a socket's subscriptions, a GraphQL request's fields) never see each other's.
/// </remarks>
public sealed record DapperMaticOperation
{
    // The request headers an operation keeps: those useful to permissions and audit, not every header.
    private static readonly string[] KeptHeaders =
    [
        HeaderNames.UserAgent,
        HeaderNames.Accept,
        HeaderNames.AcceptEncoding,
        HeaderNames.AcceptLanguage,
        HeaderNames.Cookie,
        HeaderNames.Referer,
        HeaderNames.Authorization,
        HeaderNames.ContentType,
        "True-Client-IP", // Cloudflare: the original client's address (Enterprise)
        "CF-Connecting-IP", // Cloudflare: the address connecting to it
        "CF-Ray", // Cloudflare: the data center and request
        "CF-IPCountry", // Cloudflare: the visitor's country
    ];

    /// <summary>Gets the operation's name, as <c>tables/query</c> or <c>datasources/post</c>.</summary>
    public string? Operation { get; init; }

    /// <summary>Gets the caller.</summary>
    public ClaimsPrincipal? User { get; init; }

    /// <summary>
    /// Gets the caller's access context: the tenant and the values policies read. Null outside a request, where the
    /// operation's <see cref="User"/> stands alone.
    /// </summary>
    public DmAccessContext? Access { get; init; }

    /// <summary>Gets the datasource the operation acts on, if any.</summary>
    public string? DatasourceId { get; init; }

    /// <summary>Gets the schema the operation acts in, if any: the one named, else the caller's tenant's.</summary>
    public string? SchemaName { get; init; }

    /// <summary>Gets the table the operation acts on, if any.</summary>
    public string? TableName { get; init; }

    /// <summary>Gets the view the operation acts on, if any.</summary>
    public string? ViewName { get; init; }

    /// <summary>Gets the columns the operation acts on, if any.</summary>
    public IReadOnlyList<string>? ColumnNames { get; init; }

    /// <summary>Gets the index the operation acts on, if any.</summary>
    public string? IndexName { get; init; }

    /// <summary>Gets the constraint the operation acts on, if any.</summary>
    public string? ConstraintName { get; init; }

    /// <summary>Gets the request's HTTP method.</summary>
    public string? HttpMethod { get; init; }

    /// <summary>Gets the request's path.</summary>
    public string? EndpointPath { get; init; }

    /// <summary>Gets the request's body, once read, if any.</summary>
    public object? RequestBody { get; init; }

    /// <summary>Gets the request's query parameters, by case-insensitive name.</summary>
    public IReadOnlyDictionary<string, StringValues> QueryParameters { get; init; } =
        new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the request's route values, by case-insensitive name.</summary>
    public IReadOnlyDictionary<string, string> RouteValues { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the request headers useful to permissions and audit, by case-insensitive name.</summary>
    public IReadOnlyDictionary<string, string> HeaderValues { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the caller's address.</summary>
    public string? IpAddress { get; init; }

    /// <summary>Gets the request's id, for correlating logs.</summary>
    public string? RequestId { get; init; }

    /// <summary>
    /// Gets more about the operation, for permissions of the application's own: <c>NewTableName</c> for a rename,
    /// <c>CurrentMethod</c> for the service method authorizing it.
    /// </summary>
    public IReadOnlyDictionary<string, object> Properties { get; init; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets what the operation records as it runs, shared by its copies: one per operation made from a request, a
    /// socket's subscription or a GraphQL field.
    /// </summary>
    internal DapperMaticOperationState State { get; init; } = new();

    /// <summary>
    /// Gets the operation a DapperMatic endpoint runs: how its handlers receive it.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <returns>The operation.</returns>
    public static ValueTask<DapperMaticOperation?> BindAsync(HttpContext context) => ValueTask.FromResult<DapperMaticOperation?>(Of(context));

    /// <summary>
    /// Gets the operation of a request to a DapperMatic endpoint: as its endpoint names it, with what its route names.
    /// </summary>
    /// <param name="http">The request.</param>
    /// <returns>The operation.</returns>
    public static DapperMaticOperation Of(HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);
        if (http.Features.Get<DapperMaticOperation>() is { } operation)
        {
            return operation;
        }

        operation = FromRequest(http);
        http.Features.Set(operation);
        return operation;
    }

    /// <summary>Gets the request body as a type.</summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <returns>The body, or null if it is not one.</returns>
    public T? GetRequest<T>()
        where T : class => RequestBody as T;

    /// <summary>Gets a query parameter.</summary>
    /// <param name="key">Its name.</param>
    /// <returns>Its value, or null if the request has none.</returns>
    public string? GetQueryParameter(string key) => QueryParameters.TryGetValue(key, out var value) ? value.ToString() : null;

    /// <summary>
    /// Makes a copy of the operation with one more property.
    /// </summary>
    /// <param name="name">The property's name.</param>
    /// <param name="value">Its value.</param>
    /// <returns>The copy.</returns>
    public DapperMaticOperation WithProperty(string name, object value) =>
        this with { Properties = new Dictionary<string, object>(Properties, StringComparer.OrdinalIgnoreCase) { [name] = value } };

    /// <summary>
    /// Describes the operation as an audit event.
    /// </summary>
    /// <param name="success">Whether it succeeded.</param>
    /// <param name="message">What happened, or the error.</param>
    /// <param name="claimTypes">The caller's claims to record, by type.</param>
    /// <returns>The event.</returns>
    public DapperMaticAuditEvent ToAuditEvent(bool success, string? message = null, IEnumerable<string>? claimTypes = null)
    {
        // Which key, grant and membership a caller acted by are always recorded, with the claims the application names.
        var claims = new Dictionary<string, string>(StringComparer.Ordinal);
        string[] always = [Authentication.DapperMaticClaims.Key, Authentication.DapperMaticClaims.Grant, Authentication.DapperMaticClaims.GrantedBy, Authentication.DapperMaticClaims.Membership];
        foreach (var type in always.Concat(claimTypes ?? []).Distinct(StringComparer.Ordinal))
        {
            var values = User?.FindAll(type).Select(c => c.Value).ToList() ?? [];
            if (values.Count > 0)
            {
                claims[type] = string.Join(",", values);
            }
        }

        return new DapperMaticAuditEvent
        {
            TenantId = Access?.Tenant?.Id.ToString(),
            Claims = claims,
            UserIdentifier = User?.Identity?.Name ?? User?.FindFirst(ClaimTypes.Name)?.Value ?? User?.FindFirst("sub")?.Value ?? "Anonymous",
            Operation = Operation ?? string.Empty,
            DatasourceId = DatasourceId,
            SchemaName = SchemaName,
            TableName = TableName,
            ViewName = ViewName,
            ColumnNames = ColumnNames?.ToList(),
            IndexName = IndexName,
            ConstraintName = ConstraintName,
            Success = success,
            Message = message,
            IpAddress = IpAddress,
            RequestId = RequestId,
            Timestamp = DateTimeOffset.UtcNow,
        };
    }

    // The operation a request asks for: named as its endpoint was when mapped (an endpoint answering several methods is
    // named by the request's), acting on what its route names.
    private static DapperMaticOperation FromRequest(HttpContext http)
    {
        var endpoint = http.GetEndpoint();
        var query = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in http.Request.Query)
        {
            query.TryAdd(key, value);
        }

        var route = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in http.Request.RouteValues)
        {
            if (value?.ToString() is { Length: > 0 } text && !string.IsNullOrWhiteSpace(text))
            {
                route.TryAdd(key, text);
            }
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in KeptHeaders)
        {
            if (http.Request.Headers.TryGetValue(name, out var value))
            {
                headers.TryAdd(name, value.ToString());
            }
        }

        string? Route(string key) => route.GetValueOrDefault(key) is { Length: > 0 } value ? value : null;
        return new DapperMaticOperation
        {
            Operation = endpoint?.Metadata.GetMetadata<DapperMaticOperationMetadata>()?.Name
                ?? DapperMaticOperationMetadata.NameOf((endpoint as RouteEndpoint)?.RoutePattern.RawText, http.Request.Method),
            User = http.User,
            RequestId = http.TraceIdentifier,
            IpAddress = http.Connection.RemoteIpAddress?.ToString(),
            HttpMethod = http.Request.Method,
            EndpointPath = http.Request.Path.Value ?? string.Empty,
            QueryParameters = query,
            RouteValues = route,
            HeaderValues = headers,
            DatasourceId = Route("datasourceId"),
            SchemaName = Route("schemaName"),
            TableName = Route("tableName"),
            ViewName = Route("viewName"),
            ColumnNames = Route("columnName") is { } column ? [column] : null,
            IndexName = Route("indexName"),
            ConstraintName = Route("constraintName"),
        };
    }
}

/// <summary>
/// What an operation records as it runs, shared by the copies made of it.
/// </summary>
internal sealed class DapperMaticOperationState
{
    /// <summary>Gets or sets the operation as the service received it: what a failure is audited as.</summary>
    public DapperMaticOperation? Received { get; set; }

    /// <summary>Gets or sets the tables and views a query read, by schema and name.</summary>
    public List<(string? Schema, string Name, bool View)>? Reads { get; set; }
}
