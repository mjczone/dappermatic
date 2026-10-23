// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// A ticket that opens one stream, once: sent as <c>?ticket=</c> on the stream's URL.
/// </summary>
/// <param name="Ticket">The ticket.</param>
/// <param name="ExpiresIn">How many seconds it is good for.</param>
#pragma warning disable SA1313 // Positional record parameters are properties.
public sealed record TicketResponse(string Ticket, int ExpiresIn);
#pragma warning restore SA1313
