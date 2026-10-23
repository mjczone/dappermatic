// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MJCZone.DapperMatic.AspNetCore.Factories;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Repositories;
using MJCZone.DapperMatic.AspNetCore.Security;

namespace MJCZone.DapperMatic.AspNetCore.Authentication;

/// <summary>
/// Where DapperMatic keeps its sign-in state (keys, tickets) unless told otherwise: where it keeps access. The same
/// database; a file beside the access file (tickets in memory); memory.
/// </summary>
internal static class DapperMaticStores
{
    /// <summary>
    /// Makes the key repository kept where access is.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The repository.</returns>
    public static IDapperMaticKeyRepository Keys(IServiceProvider services) =>
        services.GetRequiredService<IDapperMaticAccessRepository>() switch
        {
            DatabaseDapperMaticAccessRepository database => new DatabaseDapperMaticKeyRepository(
                database.Provider,
                database.ConnectionString,
                services.GetRequiredService<IDbConnectionFactory>(),
                database.SchemaName
            ),
            FileDapperMaticAccessRepository file => new FileDapperMaticKeyRepository(Beside(file.FilePath, "keys")),
            _ => Warned(services, new InMemoryDapperMaticKeyRepository()),
        };

    /// <summary>
    /// Makes the ticket store kept where access is: its database, or memory.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The store.</returns>
    public static IDapperMaticTicketStore Tickets(IServiceProvider services) =>
        services.GetRequiredService<IDapperMaticAccessRepository>() is DatabaseDapperMaticAccessRepository database
            ? new DatabaseDapperMaticTicketStore(database.Provider, database.ConnectionString, services.GetRequiredService<IDbConnectionFactory>(), database.SchemaName)
            : new InMemoryDapperMaticTicketStore();

    // A file beside another: access.json → access.keys.json.
    private static string Beside(string path, string kind) =>
        Path.Combine(Path.GetDirectoryName(path)!, $"{Path.GetFileNameWithoutExtension(path)}.{kind}{Path.GetExtension(path)}");

    private static InMemoryDapperMaticKeyRepository Warned(IServiceProvider services, InMemoryDapperMaticKeyRepository keys)
    {
        services.GetService<ILoggerFactory>()?.CreateLogger("MJCZone.DapperMatic.AspNetCore")
            .LogWarning("DapperMatic keeps API keys in memory: they are lost when the application stops. Keep access in a database (UseDatabaseAccessRepository) or a file, or give a key repository (UseKeyRepository).");
        return keys;
    }
}

/// <summary>
/// The check of a key's caller, each time their session is built or used again: the key still live (not revoked, not
/// expired, its service account there and on), a personal key's owner still able to sign in; the key's limits and
/// expiry for the session.
/// </summary>
internal sealed class DapperMaticApiKeyCheck(DapperMaticApiKeyService keys) : IDapperMaticCallerCheck
{
    /// <summary>The request item a key's handler sets when it has just checked the key and its owner.</summary>
    internal const string Checked = "dm:key-checked";

    /// <inheritdoc />
    public async ValueTask<DapperMaticCallerTerms?> CheckAsync(ClaimsPrincipal user, HttpContext http, bool again, CancellationToken cancellationToken)
    {
        if (user.Identities.FirstOrDefault(i => i.AuthenticationType == DapperMaticClaims.ApiKeyAuthenticationType)?.FindFirst(DapperMaticClaims.Key)?.Value is not { } id)
        {
            return null;
        }

        var key = await keys.LiveAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new DapperMaticSignInEndedException("The API key has been revoked or has expired, or its account turned off.");
        var justChecked = !again && http.Items.TryGetValue(Checked, out var checkedId) && Equals(checkedId, id);
        if (key.Kind == DapperMaticKeyKind.Personal && !justChecked)
        {
            var owner = await keys.OwnerAsync(key.Owner, key.Tenant, http.RequestServices, cancellationToken).ConfigureAwait(false);
            if (owner?.Identity?.IsAuthenticated != true)
            {
                throw new DapperMaticSignInEndedException("The API key's owner can no longer sign in.");
            }
        }

        return new DapperMaticCallerTerms(key.Limits.Count > 0 ? key.Limits : null, key.ExpiresAt);
    }
}
