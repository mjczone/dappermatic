// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Security;

/// <summary>
/// A check of a caller DapperMatic signed in, made each time their session is built or used again (a stream's re-run,
/// a socket's operation): whether they may still sign in (a key not revoked, an account not turned off), the most they
/// may do, and when their session ends. Derived from the caller's claims each time, never from how a request signed
/// them in, so that a ticket's caller is held as a key's or an account's own request is.
/// </summary>
internal interface IDapperMaticCallerCheck
{
    /// <summary>
    /// Checks a caller.
    /// </summary>
    /// <param name="user">The caller.</param>
    /// <param name="http">The request their session is of.</param>
    /// <param name="again">Whether the session is being used again (a stream's re-run): nothing checked by its request counts.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The terms of their session, or null for a caller this check is not about.</returns>
    /// <exception cref="UnauthorizedAccessException">The caller may no longer sign in.</exception>
    ValueTask<DapperMaticCallerTerms?> CheckAsync(ClaimsPrincipal user, HttpContext http, bool again, CancellationToken cancellationToken);
}

/// <summary>
/// A caller who may no longer sign in: their key revoked or expired, their account turned off. Answered 401, as
/// credentials refused are: the client signs in again.
/// </summary>
/// <param name="message">Why.</param>
internal sealed class DapperMaticSignInEndedException(string message) : UnauthorizedAccessException(message)
{
}

/// <summary>
/// The terms a caller's session is held to.
/// </summary>
/// <param name="Limits">The most they may do (an API key's), or null for no limits.</param>
/// <param name="EndsAt">When their session ends at the latest, or null.</param>
#pragma warning disable SA1313 // Positional record parameters are properties.
internal sealed record DapperMaticCallerTerms(IReadOnlyList<DmPolicy>? Limits, DateTimeOffset? EndsAt);
#pragma warning restore SA1313
