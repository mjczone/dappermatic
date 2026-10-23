// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using GraphQL;
using GraphQL.Server.Transports.AspNetCore;
using GraphQL.Server.Transports.AspNetCore.WebSockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore;
using MJCZone.DapperMatic.AspNetCore.Services;
using MJCZone.DapperMatic.GraphQL.Schema;
using MJCZone.DapperMatic.GraphQL.Transport;
using GraphQLWsServer = GraphQL.Server.Transports.AspNetCore.WebSockets.GraphQLWs.SubscriptionServer;

namespace MJCZone.DapperMatic.GraphQL;

/// <summary>
/// Maps DapperMatic's GraphQL endpoint into its route group: <c>{BasePath}/d/{datasourceId}/graphql</c> and
/// <c>{BasePath}/d/{datasourceId}/s/{schemaName}/graphql</c>, over HTTP (GET for queries, POST) and WebSockets.
/// </summary>
/// <param name="options">The GraphQL options.</param>
internal sealed class DapperMaticGraphQLContributor(IOptions<DapperMaticGraphQLOptions> options) : IDapperMaticEndpointContributor
{
    private const string WebSocketsAdded = "MJCZone.DapperMatic.GraphQL.WebSockets";

    /// <inheritdoc />
    public void Configure(IApplicationBuilder app)
    {
        // Sockets need the WebSocket middleware before the endpoints.
        if (!app.Properties.ContainsKey(WebSocketsAdded))
        {
            app.Properties[WebSocketsAdded] = true;
            app.UseWebSockets();
        }
    }

    /// <inheritdoc />
    public void Map(RouteGroupBuilder group, DapperMaticOptions dapperMatic)
    {
        var graphql = options.Value;
        foreach (var pattern in new[] { "/d/{datasourceId}/graphql", "/d/{datasourceId}/s/{schemaName}/graphql" })
        {
            var middleware = new GraphQLHttpMiddlewareOptions
            {
                HandleGet = true,
                HandlePost = true,
                HandleWebSockets = true,
                ReadVariablesFromQueryString = true,
                ReadExtensionsFromQueryString = true,
                ReadFormOnPost = false,
                EnableBatchedRequests = false,
                CsrfProtectionEnabled = true,
                CsrfProtectionHeaders = ["GraphQL-Require-Preflight", "Apollo-Require-Preflight", "X-Apollo-Operation-Name"],
            };
            middleware.WebSockets.ConnectionInitWaitTimeout = graphql.ConnectionInitTimeout;
            middleware.WebSockets.KeepAliveTimeout = TimeSpan.FromSeconds(30);
            middleware.WebSockets.KeepAliveMode = KeepAliveMode.Timeout;
            middleware.WebSockets.SupportedWebSocketSubProtocols = [GraphQLWsServer.SubProtocol];
            group.MapGraphQL<DapperMaticGraphQLMiddleware>(pattern, middleware)
                .WithMetadata(new DapperMaticOperationMetadata("graphql") { SignsInOnSocket = true, AcceptsTickets = true })
                .ExcludeFromDescription();

            // The caller's own schema as SDL, for code generators: the operations of a role are generated from that
            // role's schema.
            group.MapGet(pattern + "/schema.graphql", (Func<HttpContext, Task<IResult>>)SchemaAsync)
                .WithMetadata(new DapperMaticOperationMetadata("graphql/schema"))
                .ExcludeFromDescription();
        }
    }

    private static async Task<IResult> SchemaAsync(HttpContext http)
    {
        if (!http.RequestServices.GetRequiredService<IOptions<DapperMaticGraphQLOptions>>().Value.Introspection)
        {
            return Results.NotFound();
        }

        var operation = DapperMaticOperation.Of(http);
        var service = http.RequestServices.GetRequiredService<IDapperMaticService>() as DapperMaticService
            ?? throw new InvalidOperationException("DapperMatic's GraphQL endpoint needs DapperMatic's own service.");
        var model = await service.DescribeForCallerAsync(operation, operation.DatasourceId!, operation.SchemaName, withFunctions: true, http.RequestAborted).ConfigureAwait(false);
        var schema = http.RequestServices.GetRequiredService<GraphQLSchemaCache>().Get(model).Schema;
        return Results.Text(schema.Print(), "application/graphql; charset=utf-8");
    }
}
