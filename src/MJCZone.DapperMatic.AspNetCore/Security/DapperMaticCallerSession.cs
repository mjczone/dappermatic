// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Security;

/// <summary>
/// One caller's session with DapperMatic: who is calling, for which tenant, with which values. An HTTP request has one,
/// built before its endpoint runs; so does a stream for as long as it is open, and a socket once its caller has signed
/// in on it. Built by <see cref="DapperMaticAccess"/>.
/// </summary>
public sealed class DapperMaticCallerSession
{
    private static readonly AsyncLocal<DapperMaticCallerSession?> Ambient = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticCallerSession"/> class.
    /// </summary>
    /// <param name="user">The caller.</param>
    /// <param name="access">The caller's access context: the tenant and values.</param>
    /// <param name="request">The request the session was built from.</param>
    /// <param name="builder">What built it, and builds it again when what DapperMatic keeps changes.</param>
    /// <param name="storedTenant">The tenant's settings DapperMatic kept when the session was built, or null.</param>
    /// <param name="startedAt">When the session began: now, or when the session it was built again from began.</param>
    /// <param name="maxAge">How long a session lasts at most, or null for as long as its sign-in.</param>
    /// <param name="signedIn">The request's caller the session was built for, as their sign-in gave them.</param>
    /// <param name="endsAt">When the caller's sign-in (a cookie, a key) ends, or null.</param>
    internal DapperMaticCallerSession(
        ClaimsPrincipal user,
        DmAccessContext access,
        HttpContext request,
        DapperMaticAccess builder,
        DapperMaticTenantSettings? storedTenant,
        DateTimeOffset startedAt,
        TimeSpan? maxAge,
        ClaimsPrincipal signedIn,
        DateTimeOffset? endsAt
    )
    {
        User = user;
        SignedIn = signedIn;
        Access = access;
        Request = request;
        Builder = builder;
        StoredTenant = storedTenant;
        StartedAt = startedAt;

        // A signed-in token says when it expires (exp, in seconds); the session ends then, or at its maximum age. An exp
        // no date can have (milliseconds, say) is not read, as one that is not a number.
        DateTimeOffset? expires = maxAge is { } age ? startedAt + age : null;
        if (
            long.TryParse(user.FindFirst("exp")?.Value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var exp)
            && exp >= DateTimeOffset.MinValue.ToUnixTimeSeconds()
            && exp <= DateTimeOffset.MaxValue.ToUnixTimeSeconds()
        )
        {
            var token = DateTimeOffset.FromUnixTimeSeconds(exp);
            expires = expires is { } at && at < token ? at : token;
        }

        if (endsAt is { } ends)
        {
            expires = expires is { } at && at < ends ? at : ends;
        }

        ExpiresAt = expires;
    }

    /// <summary>
    /// Gets the session the current code runs in: the request's, within a DapperMatic endpoint; null elsewhere.
    /// </summary>
    public static DapperMaticCallerSession? Current
    {
        get => Ambient.Value;
        internal set => Ambient.Value = value;
    }

    /// <summary>
    /// Gets the caller, as DapperMatic's checks see them: their identities, and the roles found in other claims or by
    /// code (<c>RolesFrom</c>).
    /// </summary>
    public ClaimsPrincipal User { get; }

    /// <summary>Gets the caller's access context: the tenant and the values policies and lookups read.</summary>
    public DmAccessContext Access { get; }

    /// <summary>Gets the caller's tenant, or null for none.</summary>
    public DmTenant? Tenant => Access.Tenant;

    /// <summary>Gets when the session began.</summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>
    /// Gets when the session ends: when the caller's token expires, or at <see cref="DapperMaticOptions.MaxSessionAge"/>,
    /// whichever comes first; null for never. A stream or socket ends with it, and its client signs in again.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; }

    /// <summary>Gets the request's caller the session was built for, as their sign-in gave them.</summary>
    internal ClaimsPrincipal SignedIn { get; }

    /// <summary>Gets the request the session was built from.</summary>
    internal HttpContext Request { get; }

    /// <summary>Gets what built the session.</summary>
    internal DapperMaticAccess Builder { get; }

    /// <summary>Gets the tenant's settings DapperMatic kept when the session was built, or null.</summary>
    internal DapperMaticTenantSettings? StoredTenant { get; }

    /// <summary>
    /// Gets a request's session, once built: by a DapperMatic endpoint, or by
    /// <see cref="DapperMaticAccess.GetSessionAsync"/>.
    /// </summary>
    /// <param name="http">The request.</param>
    /// <returns>The session, or null if none was built for it.</returns>
    public static DapperMaticCallerSession? Of(HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);
        return http.Features.Get<DapperMaticCallerSession>();
    }

    /// <summary>
    /// Checks the session before it is used again, as a stream does before each re-run and a socket before each
    /// operation: the policies and tenants DapperMatic keeps are read again if they are due, and a session whose tenant
    /// they now describe differently is built again from the same request.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The session to use: this one, or one built again.</returns>
    /// <exception cref="UnauthorizedAccessException">The session has ended (<see cref="ExpiresAt"/>).</exception>
    public ValueTask<DapperMaticCallerSession> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (ExpiresAt is { } at && DateTimeOffset.UtcNow >= at)
        {
            throw new UnauthorizedAccessException("The caller's session has ended: sign in again.");
        }

        return Builder.CheckAsync(this, cancellationToken);
    }
}
