// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore.Models.Dtos;

/// <summary>
/// Asks for a ticket that opens a stream: a REST subscription (Server-Sent Events) or a GraphQL socket.
/// </summary>
public sealed class TicketRequest
{
    /// <summary>
    /// Gets or sets the path of the stream the ticket opens, as the browser requests it, without the query:
    /// <c>/api/dm/d/main/t/orders/subscribe</c>.
    /// </summary>
    public string? Path { get; set; }
}
