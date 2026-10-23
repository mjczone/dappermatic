// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace MJCZone.DapperMatic.AspNetCore.Authentication;

/// <summary>
/// Where a caller's roles are found besides ASP.NET Core's role claim: a claim of another type (each value a role, or
/// one value holding a JSON array of them), a path into a claim holding JSON (Keycloak's <c>realm_access</c>), or code.
/// The roles of every source add up; policies' roles are matched against them as against the role claim.
/// </summary>
public sealed class DapperMaticRoleSource
{
    /// <summary>Gets the claim type the roles are in, or null for a source in code.</summary>
    public string? ClaimType { get; init; }

    /// <summary>
    /// Gets the path into the claim's JSON value the roles are at (<c>roles</c> for Keycloak's <c>realm_access</c>,
    /// dots between properties), or null when the claim's values are the roles.
    /// </summary>
    public string? JsonPath { get; init; }

    /// <summary>Gets the roles read by code: the request and its caller in, the roles out.</summary>
    public Func<HttpContext, ClaimsPrincipal, CancellationToken, ValueTask<IEnumerable<string>?>>? Code { get; init; }

    /// <summary>
    /// Reads the roles this source finds for a caller.
    /// </summary>
    /// <param name="http">The request.</param>
    /// <param name="user">The caller.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The roles.</returns>
    internal async ValueTask<IEnumerable<string>> ReadAsync(HttpContext http, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (Code is { } code)
        {
            return await code(http, user, cancellationToken).ConfigureAwait(false) ?? [];
        }

        var roles = new List<string>();
        foreach (var claim in user.FindAll(ClaimType!))
        {
            if (JsonPath is { Length: > 0 } path)
            {
                roles.AddRange(Strings(Read(claim.Value, path)));
            }
            else if (claim.Value.TrimStart().StartsWith('['))
            {
                roles.AddRange(Strings(Parse(claim.Value)));
            }
            else
            {
                roles.Add(claim.Value);
            }
        }

        return roles.Where(r => !string.IsNullOrWhiteSpace(r));
    }

    // The value at a path into a claim holding JSON, or nothing when the claim is not JSON or has no such property.
    private static JsonElement? Read(string json, string path)
    {
        if (Parse(json) is not { } value)
        {
            return null;
        }

        foreach (var name in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(name, out var inner))
            {
                return null;
            }

            value = inner;
        }

        return value;
    }

    private static JsonElement? Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // The roles a JSON value holds: its strings, or the strings of an array.
    private static IEnumerable<string> Strings(JsonElement? value) =>
        value switch
        {
            { ValueKind: JsonValueKind.String } text => [text.GetString()!],
            { ValueKind: JsonValueKind.Array } array => array.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!),
            _ => [],
        };
}
