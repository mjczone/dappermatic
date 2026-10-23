// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace MJCZone.DapperMatic.GraphQL;

/// <summary>
/// The options of DapperMatic's GraphQL endpoint. The engine's own limits (<c>DapperMaticOptions.QueryLimits</c>:
/// rows per page, expands, conditions, rows written) apply too.
/// </summary>
public sealed class DapperMaticGraphQLOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether callers may ask for their schema (introspection), which tools and clients
    /// generate code from. Each caller sees only their own part. Default is true.
    /// </summary>
    public bool Introspection { get; set; } = true;

    /// <summary>
    /// Gets or sets how deep an operation's selections may nest. Operations asking only for the schema are not held to
    /// it. Default is 10.
    /// </summary>
    public int MaxDepth { get; set; } = 10;

    /// <summary>
    /// Gets or sets how complex an operation may be: about one for each field it selects, times the rows a list may hold.
    /// Default is 10,000.
    /// </summary>
    public int MaxComplexity { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets how many root fields an operation may have: each is a query or a write of its own. Default is 20.
    /// </summary>
    public int MaxRootFields { get; set; } = 20;

    /// <summary>
    /// Gets or sets how many schemas are kept: one for each shape of caller model (what callers may use), shared by every
    /// caller of that shape. Default is 256.
    /// </summary>
    public int SchemaCacheSize { get; set; } = 256;

    /// <summary>
    /// Gets or sets a value indicating whether 64-bit integers and decimals are written as strings, <c>BigInt</c> and
    /// <c>BigDecimal</c>, which JavaScript reads without losing digits; else as JSON numbers, <c>Long</c> and
    /// <c>Decimal</c>. Default is false.
    /// </summary>
    public bool BigNumbersAsStrings { get; set; }

    /// <summary>
    /// Gets the origins a browser may open a GraphQL socket from, besides the endpoint's own: a page elsewhere could
    /// otherwise use the caller's cookies (cross-site WebSocket hijacking). <c>*</c> allows any.
    /// </summary>
    public List<string> AllowedOrigins { get; } = [];

    /// <summary>Gets or sets how many subscriptions one socket may hold at once. Default is 20.</summary>
    public int MaxSubscriptionsPerSocket { get; set; } = 20;

    /// <summary>
    /// Gets or sets the longest message a socket may send, in bytes: a message is held whole before it is read, so one
    /// longer closes the socket (1009). Default is 4 MiB. Requests over HTTP are held to the server's own limit on a
    /// request's body.
    /// </summary>
    public long MaxSocketMessageBytes { get; set; } = 4 * 1024 * 1024;

    /// <summary>
    /// Gets or sets how long a socket has to send <c>connection_init</c> after it opens. Default is 3 seconds.
    /// </summary>
    public TimeSpan ConnectionInitTimeout { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Gets or sets how a socket's caller signs in from its <c>connection_init</c> payload (browsers cannot set headers
    /// on a WebSocket, so clients send a token there): the payload and the request that opened the socket in, the
    /// caller out, or null to refuse. When not set, a socket opened by a signed-in request is that caller's, and
    /// otherwise the payload's <c>Authorization</c> is checked as a bearer token by the application's default
    /// authentication scheme.
    /// </summary>
    public Func<JsonElement?, HttpContext, Task<ClaimsPrincipal?>>? SignIn { get; set; }
}
