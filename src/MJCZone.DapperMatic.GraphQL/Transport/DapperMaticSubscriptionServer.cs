// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using GraphQL;
using GraphQL.Server.Transports.AspNetCore;
using GraphQL.Server.Transports.AspNetCore.WebSockets;
using GraphQL.Transport;
using Microsoft.Extensions.DependencyInjection;
using MJCZone.DapperMatic.GraphQL.Execution;
using GraphQLWsServer = GraphQL.Server.Transports.AspNetCore.WebSockets.GraphQLWs.SubscriptionServer;

namespace MJCZone.DapperMatic.GraphQL.Transport;

/// <summary>
/// A GraphQL socket of DapperMatic's endpoint (graphql-transport-ws): its caller signs in on <c>connection_init</c> and
/// their session is built then (a refusal closes the socket, 4403); it closes when their session ends (4401), and holds
/// so many subscriptions at most.
/// </summary>
internal sealed class DapperMaticSubscriptionServer : GraphQLWsServer
{
    private readonly DapperMaticGraphQLOptions _graphql;
    private readonly HashSet<string> _active = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticSubscriptionServer"/> class.
    /// </summary>
    /// <param name="connection">The socket.</param>
    /// <param name="options">The socket options.</param>
    /// <param name="authorizationOptions">The authorization options.</param>
    /// <param name="executer">The document executer.</param>
    /// <param name="serializer">The JSON serializer.</param>
    /// <param name="scopes">Makes each operation's scope.</param>
    /// <param name="userContexts">Builds the socket's user context: the caller's session.</param>
    /// <param name="signIn">Signs the caller in from <c>connection_init</c>.</param>
    /// <param name="graphql">DapperMatic's GraphQL options.</param>
    public DapperMaticSubscriptionServer(
        IWebSocketConnection connection,
        GraphQLWebSocketOptions options,
        IAuthorizationOptions authorizationOptions,
        IDocumentExecuter executer,
        IGraphQLSerializer serializer,
        IServiceScopeFactory scopes,
        IUserContextBuilder userContexts,
        IWebSocketAuthenticationService signIn,
        DapperMaticGraphQLOptions graphql
    )
        : base(connection, options, authorizationOptions, executer, serializer, scopes, userContexts, signIn)
    {
        _graphql = graphql;
    }

    /// <inheritdoc />
    protected override async ValueTask<bool> AuthorizeAsync(OperationMessage message)
    {
        try
        {
            if (!await base.AuthorizeAsync(message).ConfigureAwait(false))
            {
                return false;
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Signed in, and refused: no tenant, or another tenant's datasource or schema.
            await Connection.CloseAsync(4403, "Forbidden").ConfigureAwait(false);
            return false;
        }

        // The socket lasts as long as its caller's session.
        if (UserContext is DapperMaticGraphQLContext { Session.ExpiresAt: { } ends })
        {
            _ = CloseAtAsync(ends);
        }

        return true;
    }

    /// <inheritdoc />
    protected override Task SubscribeAsync(OperationMessage message, bool overwrite)
    {
        lock (_active)
        {
            if (message.Id is { } id && !_active.Contains(id) && _active.Count >= _graphql.MaxSubscriptionsPerSocket)
            {
                // Refused before it is held: told directly, as the server tells only of subscriptions it holds.
                return Connection.SendMessageAsync(
                    new OperationMessage
                    {
                        Id = id,
                        Type = "error",
                        Payload = new[] { new ExecutionError($"A socket holds at most {_graphql.MaxSubscriptionsPerSocket} subscriptions.") { Code = "TOO_MANY_SUBSCRIPTIONS" } },
                    }
                );
            }

            if (message.Id is { } added)
            {
                _active.Add(added);
            }
        }

        return base.SubscribeAsync(message, overwrite);
    }

    /// <inheritdoc />
    protected override Task UnsubscribeAsync(string? id)
    {
        Ended(id);
        return base.UnsubscribeAsync(id);
    }

    /// <inheritdoc />
    protected override Task SendCompletedAsync(string id)
    {
        Ended(id);
        return base.SendCompletedAsync(id);
    }

    /// <inheritdoc />
    protected override Task SendErrorResultAsync(string id, ExecutionResult result)
    {
        Ended(id);
        return base.SendErrorResultAsync(id, result);
    }

    private void Ended(string? id)
    {
        lock (_active)
        {
            if (id is not null)
            {
                _active.Remove(id);
            }
        }
    }

    private async Task CloseAtAsync(DateTimeOffset ends)
    {
        try
        {
            // A day at a time: a wait longer than a timer takes (about 49 days) would throw.
            for (var left = ends - DateTimeOffset.UtcNow; left > TimeSpan.Zero; left = ends - DateTimeOffset.UtcNow)
            {
                await Task.Delay(left < TimeSpan.FromDays(1) ? left : TimeSpan.FromDays(1), CancellationToken).ConfigureAwait(false);
            }

            await Connection.CloseAsync(4401, "The caller's session has ended: sign in again.").ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The socket closed first.
        }
    }
}
