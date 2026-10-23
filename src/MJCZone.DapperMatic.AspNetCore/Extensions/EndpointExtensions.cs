// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MJCZone.DapperMatic.AspNetCore.Extensions;

/// <summary>
/// Extension methods for configuring endpoint conventions.
/// </summary>
internal static class EndpointExtensions
{
    /// <summary>
    /// Maps a DapperMatic route group with the specified base path and prefix.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="basePath">The base path for the API endpoints. Defaults to "/api/dm".</param>
    /// <param name="prefix">The prefix for the specific resource (e.g., "datasources", "tables").</param>
    /// <param name="tag">The OpenAPI tag to associate with the group.</param>
    /// <returns>The configured route group.</returns>
    public static RouteGroupBuilder MapDapperMaticEndpointGroup(
        this IEndpointRouteBuilder endpoints,
        string? basePath,
        [StringSyntax("Route")] string prefix,
        string tag
    )
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        basePath ??= "/api/dm";
        var path = string.Join('/', new[] { basePath.Trim('/'), prefix.Trim('/') }.Where(p => p.Length > 0));
        return endpoints.MapGroup("/" + path).WithDapperMaticConventions(tag);
    }

    /// <summary>
    /// Adds DapperMatic conventions to the specified route group.
    /// </summary>
    /// <param name="group">The route group to configure.</param>
    /// <param name="tag">The OpenAPI tag to associate with the group.</param>
    /// <returns>The configured route group.</returns>
    private static RouteGroupBuilder WithDapperMaticConventions(this RouteGroupBuilder group, string tag)
    {
        // The request pipeline (caller, span, errors, audit, cache invalidation) wraps every endpoint from DapperMatic's
        // own group; these route-handler groups add the ETag.
        return group.WithTags(tag).AddEndpointFilter<DapperMaticETagFilter>();
    }

    /// <summary>
    /// Gives each successful <c>GET</c> response an <c>ETag</c>, a hash of its body, and answers <c>304 Not Modified</c>
    /// when the request's <c>If-None-Match</c> names it: the client's copy is current, and the body is not sent again.
    /// The response is marked <c>private, no-cache</c>: it is the caller's own, and is checked again before reuse.
    /// </summary>
    public class DapperMaticETagFilter : IEndpointFilter
    {
        /// <inheritdoc />
        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            var result = await next(context).ConfigureAwait(false);
            var http = context.HttpContext;
            if (
                !HttpMethods.IsGet(http.Request.Method)
                || http.RequestServices.GetService<IOptions<DapperMaticOptions>>()?.Value.ETags == false
                || result is not IValueHttpResult { Value: { } value }
                || (result is IStatusCodeHttpResult { StatusCode: { } code } && code != StatusCodes.Status200OK)
            )
            {
                return result;
            }

            // The body as the endpoint would have written it, with the application's JSON options.
            var json =
                http.RequestServices.GetService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>()?.Value.SerializerOptions
                ?? System.Text.Json.JsonSerializerOptions.Web;
            var body = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value, value.GetType(), json);
            var tag = new Microsoft.Net.Http.Headers.EntityTagHeaderValue(
                $"\"{Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(body).AsSpan(0, 16))}\""
            );
            var headers = http.Response.GetTypedHeaders();
            headers.ETag = tag;
            headers.CacheControl = new Microsoft.Net.Http.Headers.CacheControlHeaderValue { Private = true, NoCache = true };
            if (http.Request.GetTypedHeaders().IfNoneMatch.Any(m => m.Equals(Microsoft.Net.Http.Headers.EntityTagHeaderValue.Any) || m.Compare(tag, useStrongComparison: false)))
            {
                return Results.StatusCode(StatusCodes.Status304NotModified);
            }

            return Results.Bytes(body, "application/json; charset=utf-8");
        }
    }
}
