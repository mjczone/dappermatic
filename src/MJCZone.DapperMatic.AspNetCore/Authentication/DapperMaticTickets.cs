// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace MJCZone.DapperMatic.AspNetCore.Authentication;

/// <summary>
/// Tickets for browsers' streams, which cannot send an <c>Authorization</c> header (<c>EventSource</c>, WebSockets): a
/// signed-in caller asks for one, and opens the stream with <c>?ticket=</c>. The URL carries only a random id; the
/// caller (as they were signed in, with when that sign-in ends), the path and the tenant are kept, protected by Data
/// Protection, until the ticket is taken (once) or expires.
/// </summary>
internal sealed class DapperMaticTickets
{
    private const string Purpose = "DapperMatic.Ticket.v1";

    private readonly IDataProtector _protector;
    private readonly IDapperMaticTicketStore _store;
    private readonly TimeSpan _lifetime;

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticTickets"/> class.
    /// </summary>
    /// <param name="protection">The application's Data Protection.</param>
    /// <param name="store">Where tickets are kept until taken.</param>
    /// <param name="options">The DapperMatic options.</param>
    public DapperMaticTickets(IDataProtectionProvider protection, IDapperMaticTicketStore store, IOptions<DapperMaticOptions> options)
    {
        _protector = protection.CreateProtector(Purpose);
        _store = store;
        _lifetime = options.Value.Tickets.Lifetime;
    }

    /// <summary>Gets how long a ticket is good for.</summary>
    public TimeSpan Lifetime => _lifetime;

    /// <summary>
    /// Makes a ticket.
    /// </summary>
    /// <param name="user">The caller, as signed in.</param>
    /// <param name="signedInUntil">When the caller's sign-in ends (a cookie's expiry), or null.</param>
    /// <param name="tenant">The tenant the caller is in, or null.</param>
    /// <param name="path">The path of the stream it opens.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The ticket.</returns>
    public async Task<string> IssueAsync(ClaimsPrincipal user, DateTimeOffset? signedInUntil, string? tenant, string path, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Normalize(path));
            writer.Write(tenant is not null);
            writer.Write(tenant ?? string.Empty);
            var properties = new AuthenticationProperties { ExpiresUtc = signedInUntil };
            var serialized = TicketSerializer.Default.Serialize(new AuthenticationTicket(user, properties, DapperMaticClaims.TicketAuthenticationType));
            writer.Write(serialized.Length);
            writer.Write(serialized);
        }

        var id = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        await _store.SaveAsync(id, Convert.ToBase64String(_protector.Protect(buffer.ToArray())), DateTimeOffset.UtcNow + _lifetime, cancellationToken).ConfigureAwait(false);
        return id;
    }

    /// <summary>
    /// Takes a ticket for a request: once, before it expires, for its path.
    /// </summary>
    /// <param name="ticket">The ticket.</param>
    /// <param name="path">The request's path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The caller it signs in (with an identity naming their tenant) and when their sign-in ends; null for a ticket
    /// that is not good.
    /// </returns>
    public async Task<(ClaimsPrincipal User, DateTimeOffset? SignedInUntil)?> RedeemAsync(string ticket, string path, CancellationToken cancellationToken)
    {
        if (ticket.Length is 0 or > 64 || await _store.TakeAsync(ticket, cancellationToken).ConfigureAwait(false) is not { } kept)
        {
            return null;
        }

        byte[] payload;
        try
        {
            payload = _protector.Unprotect(Convert.FromBase64String(kept));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return null;
        }

        using var reader = new BinaryReader(new MemoryStream(payload), Encoding.UTF8);
        if (!string.Equals(reader.ReadString(), Normalize(path), StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var hasTenant = reader.ReadBoolean();
        var named = reader.ReadString();
        var signedIn = TicketSerializer.Default.Deserialize(reader.ReadBytes(reader.ReadInt32()));
        if (signedIn?.Principal is not { } user)
        {
            return null;
        }

        // The caller as they were, in the tenant they were in: a ticket's identity names it, so the stream's request
        // (whose headers a browser cannot set) is not read for another.
        var place = new ClaimsIdentity(DapperMaticClaims.TicketAuthenticationType);
        if (hasTenant)
        {
            place.AddClaim(new Claim(DapperMaticClaims.Tenant, named));
        }

        return (new ClaimsPrincipal(user.Identities.Append(place)), signedIn.Properties.ExpiresUtc);
    }

    private static string Normalize(string path) => "/" + path.Split('?', 2)[0].Trim('/');
}
