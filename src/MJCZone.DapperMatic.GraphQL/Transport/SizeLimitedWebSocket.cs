// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net.WebSockets;

namespace MJCZone.DapperMatic.GraphQL.Transport;

/// <summary>
/// A socket whose messages are this long at most: one longer is not read on (a message is held whole before it is
/// read, whoever sends it, signed in or not), and the socket is closed, 1009. Everything else is the socket's own.
/// </summary>
/// <param name="inner">The socket.</param>
/// <param name="maxMessageBytes">The longest message, in bytes.</param>
internal sealed class SizeLimitedWebSocket(WebSocket inner, long maxMessageBytes) : WebSocket
{
    private long _received;
    private bool _refused;

    /// <inheritdoc />
    public override WebSocketCloseStatus? CloseStatus => _refused ? WebSocketCloseStatus.MessageTooBig : inner.CloseStatus;

    /// <inheritdoc />
    public override string? CloseStatusDescription => inner.CloseStatusDescription;

    /// <inheritdoc />
    public override WebSocketState State => inner.State;

    /// <inheritdoc />
    public override string? SubProtocol => inner.SubProtocol;

    /// <inheritdoc />
    public override void Abort() => inner.Abort();

    /// <inheritdoc />
    public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) =>
        _refused ? Task.CompletedTask : inner.CloseAsync(closeStatus, statusDescription, cancellationToken);

    /// <inheritdoc />
    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) =>
        _refused ? Task.CompletedTask : inner.CloseOutputAsync(closeStatus, statusDescription, cancellationToken);

    /// <inheritdoc />
    public override void Dispose() => inner.Dispose();

    /// <inheritdoc />
    public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
    {
        var result = await inner.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (result.MessageType == WebSocketMessageType.Close)
        {
            return result;
        }

        _received += result.Count;
        if (_received > maxMessageBytes)
        {
            // Read as the client closing, so that nothing more is read; the client is told why.
            _refused = true;
            var why = $"A message is at most {maxMessageBytes} bytes.";
            await inner.CloseOutputAsync(WebSocketCloseStatus.MessageTooBig, why, cancellationToken).ConfigureAwait(false);
            return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true, WebSocketCloseStatus.MessageTooBig, why);
        }

        if (result.EndOfMessage)
        {
            _received = 0;
        }

        return result;
    }

    /// <inheritdoc />
    public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) =>
        inner.SendAsync(buffer, messageType, endOfMessage, cancellationToken);
}
