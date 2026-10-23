// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Text.Json;
using GraphQL.Server.Transports.AspNetCore.WebSockets;
using GraphQL.Transport;
using Microsoft.Extensions.DependencyInjection;
using MJCZone.DapperMatic.AspNetCore.Authentication;

namespace MJCZone.DapperMatic.GraphQL.Transport;

/// <summary>
/// Signs a socket's caller in from its <c>connection_init</c> payload: by the application's hook
/// (<see cref="DapperMaticGraphQLOptions.SignIn"/>); else a socket opened by a signed-in request is that caller's; else
/// the payload's <c>Authorization</c> is checked as a bearer token by the application's default authentication scheme.
/// A refusal (the hook's null, a token not accepted) closes the socket, 4403.
/// </summary>
/// <param name="options">The GraphQL options.</param>
internal sealed class DapperMaticSocketSignIn(DapperMaticGraphQLOptions options) : IWebSocketAuthenticationService
{
    /// <inheritdoc />
    public async Task AuthenticateAsync(IWebSocketConnection connection, string subProtocol, OperationMessage operationMessage)
    {
        var http = connection.HttpContext;
        // The payload as JSON, whatever shape the serializer read it in.
        var payload = operationMessage.Payload switch
        {
            null => (JsonElement?)null,
            JsonElement json => json,
            var other => JsonSerializer.SerializeToElement(other),
        };
        if (options.SignIn is { } signIn)
        {
            // The application's answer is the caller, or null to refuse: the socket is closed (4403), not left to the
            // request that opened it.
            http.User = await signIn(payload, http).ConfigureAwait(false)
                ?? throw new UnauthorizedAccessException("The application refused this socket's sign-in.");
            return;
        }

        if (http.User.Identity?.IsAuthenticated == true || payload is not { ValueKind: JsonValueKind.Object } values)
        {
            return;
        }

        var token = values.EnumerateObject().FirstOrDefault(p => string.Equals(p.Name, "Authorization", StringComparison.OrdinalIgnoreCase)).Value;
        if (token.ValueKind != JsonValueKind.String || token.GetString() is not { Length: > 0 } authorization)
        {
            return;
        }

        // The token as DapperMatic's endpoints read tokens (their sign-in methods): in the request's Authorization header,
        // read by handlers of a scope of their own (a request's handler keeps the result it read when the request came in).
        var before = (http.Request.Headers.Authorization, http.RequestServices);
        using var scope = http.RequestServices.GetRequiredService<IServiceScopeFactory>().CreateScope();
        http.Request.Headers.Authorization = authorization.Contains(' ', StringComparison.Ordinal) ? authorization : "Bearer " + authorization;
        http.RequestServices = scope.ServiceProvider;
        try
        {
            // A token given and not accepted refuses the socket: its client would otherwise go on as no one, unaware.
            var (user, _, _) = await scope.ServiceProvider.GetRequiredService<DapperMaticSignIn>().AuthenticateAsync(http, keepSignedIn: false).ConfigureAwait(false);
            http.User = user.Identity?.IsAuthenticated == true
                ? user
                : throw new UnauthorizedAccessException("The token given on connection_init was not accepted.");
        }
        finally
        {
            (http.Request.Headers.Authorization, http.RequestServices) = before;
        }
    }
}
