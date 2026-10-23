// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using GraphQL;
using GraphQL.Execution;
using GraphQL.Server.Transports.AspNetCore;
using GraphQL.Server.Transports.AspNetCore.Errors;
using GraphQL.Server.Transports.AspNetCore.WebSockets;
using GraphQL.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.GraphQL.Execution;
using SubscriptionServer = GraphQL.Server.Transports.AspNetCore.WebSockets.GraphQLWs.SubscriptionServer;

namespace MJCZone.DapperMatic.GraphQL.Transport;

/// <summary>
/// DapperMatic's GraphQL endpoint, over GraphQL.NET's server: every request runs as the caller of DapperMatic's request
/// pipeline (a socket's once they sign in on it); responses follow the GraphQL-over-HTTP status codes; a browser's socket
/// is opened only from the endpoint's own origin or one allowed.
/// </summary>
internal sealed class DapperMaticGraphQLMiddleware : GraphQLHttpMiddleware
{
    private readonly GraphQLHttpMiddlewareOptions _options;
    private readonly IDocumentExecuter _executer;
    private readonly IGraphQLTextSerializer _serializer;
    private readonly IServiceScopeFactory _scopes;
    private readonly DapperMaticGraphQLOptions _graphql;

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticGraphQLMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware.</param>
    /// <param name="serializer">The JSON serializer.</param>
    /// <param name="executer">The document executer.</param>
    /// <param name="scopes">Makes each request's scope.</param>
    /// <param name="options">The middleware's options.</param>
    /// <param name="lifetime">The application's lifetime.</param>
    /// <param name="graphql">DapperMatic's GraphQL options.</param>
    public DapperMaticGraphQLMiddleware(
        RequestDelegate next,
        IGraphQLTextSerializer serializer,
        IDocumentExecuter executer,
        IServiceScopeFactory scopes,
        GraphQLHttpMiddlewareOptions options,
        IHostApplicationLifetime lifetime,
        IOptions<DapperMaticGraphQLOptions> graphql
    )
        : base(next, serializer, executer, scopes, options, lifetime)
    {
        _options = options;
        _executer = executer;
        _serializer = serializer;
        _scopes = scopes;
        _graphql = graphql.Value;
    }

    /// <inheritdoc />
    protected override async ValueTask<IDictionary<string, object?>?> BuildUserContextAsync(HttpContext context, object? payload)
    {
        var access = context.RequestServices.GetRequiredService<DapperMaticAccess>();
        var operation = DapperMaticOperation.Of(context);
        var namedSchema = context.GetRouteValue("schemaName") as string;
        if (context.WebSockets.IsWebSocketRequest)
        {
            // The socket's caller has signed in on it: their session, the endpoint's datasource and schema held to it.
            var session = await access.BuildAsync(context, context.RequestAborted).ConfigureAwait(false);
            return new DapperMaticGraphQLContext(session, access.Hold(session, operation with { SchemaName = namedSchema }), namedSchema);
        }

        // A request is its caller's session, built and held by DapperMatic's request pipeline.
        return new DapperMaticGraphQLContext(await access.GetSessionAsync(context, context.RequestAborted).ConfigureAwait(false), operation, namedSchema);
    }

    /// <inheritdoc />
    protected override IWebSocketConnection CreateWebSocketConnection(HttpContext httpContext, System.Net.WebSockets.WebSocket webSocket, CancellationToken cancellationToken) =>
        base.CreateWebSocketConnection(httpContext, new SizeLimitedWebSocket(webSocket, _graphql.MaxSocketMessageBytes), cancellationToken);

    /// <inheritdoc />
    protected override Task WriteJsonResponseAsync(HttpContext context, ExecutionResult result)
    {
        // With application/graphql-response+json: a document that does not parse is 400, one that does not validate 422
        // (GraphQL over HTTP); an error that prefers a status (a mutation over GET: 405) has it. application/json: 200.
        var type = SelectResponseContentType(context);
        if (result.Executed || !string.Equals(type.MediaType.Value, "application/graphql-response+json", StringComparison.OrdinalIgnoreCase))
        {
            return base.WriteJsonResponseAsync(context, result);
        }

        var errors = result.Errors ?? [];
        var preferred = errors.Count > 0 && errors.All(e => e is IHasPreferredStatusCode) ? ((IHasPreferredStatusCode)errors[0]).PreferredStatusCode : (HttpStatusCode?)null;
        var status = preferred is { } code && errors.All(e => ((IHasPreferredStatusCode)e).PreferredStatusCode == code)
            ? code
            : errors.Any(e => e is SyntaxError) ? HttpStatusCode.BadRequest : HttpStatusCode.UnprocessableEntity;
        return WriteJsonResponseAsync(context, status, result);
    }

    /// <inheritdoc />
    protected override Task WriteJsonResponseAsync<TResult>(HttpContext context, HttpStatusCode httpStatusCode, TResult result)
    {
        // A mutation over GET: POST is the method that takes it.
        if (httpStatusCode == HttpStatusCode.MethodNotAllowed)
        {
            context.Response.Headers.Allow = "POST";
        }

        return base.WriteJsonResponseAsync(context, httpStatusCode, result);
    }

    /// <inheritdoc />
    protected override async ValueTask<bool> HandleAuthorizeWebSocketConnectionAsync(HttpContext context, RequestDelegate next)
    {
        // A browser sends its page's origin: a page elsewhere could open the socket with the caller's cookies. True
        // says the request was answered (refused) here.
        var origin = context.Request.Headers.Origin.ToString();
        if (origin.Length > 0 && !Allowed(origin, context))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return true;
        }

        return await base.HandleAuthorizeWebSocketConnectionAsync(context, next).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override IOperationMessageProcessor CreateMessageProcessor(IWebSocketConnection webSocketConnection, string subProtocol) =>
        subProtocol == SubscriptionServer.SubProtocol
            ? new DapperMaticSubscriptionServer(
                webSocketConnection,
                _options.WebSockets,
                _options,
                _executer,
                (IGraphQLSerializer)_serializer,
                _scopes,
                this,
                new DapperMaticSocketSignIn(_graphql),
                _graphql
            )
            : throw new ArgumentOutOfRangeException(nameof(subProtocol), subProtocol, "DapperMatic's GraphQL sockets speak graphql-transport-ws.");

    private bool Allowed(string origin, HttpContext context)
    {
        if (_graphql.AllowedOrigins.Contains("*") || _graphql.AllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return Uri.TryCreate(origin, UriKind.Absolute, out var page)
            && string.Equals(page.Authority, context.Request.Host.Value, StringComparison.OrdinalIgnoreCase)
            && string.Equals(page.Scheme, context.Request.Scheme, StringComparison.OrdinalIgnoreCase);
    }
}
