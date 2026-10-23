// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MJCZone.DapperMatic.AspNetCore.Authentication;

/// <summary>
/// API keys: how programs sign in. Off until <c>dm.AddApiKeys()</c>. A key is shown once, when made, and kept only as
/// a hash; it is sent as <c>Authorization: Bearer {key}</c> or in <see cref="HeaderName"/>, never in a URL.
/// </summary>
public sealed class DapperMaticApiKeyOptions
{
    /// <summary>
    /// Gets or sets what keys begin with, so that they are recognised when they leak (secret scanners, logs): 1 to 16
    /// lowercase letters and digits. Default is <c>dm</c>.
    /// </summary>
    public string Prefix { get; set; } = "dm";

    /// <summary>Gets or sets the header a key may be sent in, besides <c>Authorization: Bearer</c>. Default is <c>X-Api-Key</c>.</summary>
    public string HeaderName { get; set; } = "X-Api-Key";

    /// <summary>Gets or sets how long a personal key may last at most, or null (the default) for as long as its maker wants.</summary>
    public TimeSpan? MaxPersonalKeyLifetime { get; set; }

    /// <summary>Gets or sets how long a service account's key may last at most, or null (the default) for as long as its maker wants.</summary>
    public TimeSpan? MaxServiceKeyLifetime { get; set; }

    /// <summary>
    /// Gets the roles that may make their own personal keys; a caller with any of them may. Default is
    /// <c>authenticated</c> (anyone signed in); clear it to turn self-service off. Keys never make keys.
    /// </summary>
    public List<string> PersonalKeyRoles { get; } = ["authenticated"];

    /// <summary>
    /// Gets or sets how a personal key's owner is found as they are now, each time the key signs in: their id and the
    /// key's tenant in, their principal out, or null when they are gone or no longer in that tenant. DapperMatic's
    /// built-in accounts find their own; with neither, personal keys cannot be made.
    /// </summary>
    public Func<string, string?, IServiceProvider, CancellationToken, ValueTask<ClaimsPrincipal?>>? ResolveOwner { get; set; }

    /// <summary>Gets or sets a value indicating whether API keys are turned on.</summary>
    internal bool Enabled { get; set; }
}

/// <summary>
/// The form of API keys: <c>{prefix}_{id}_{secret}</c>, the id 16 and the secret 52 base32 characters (32 random
/// bytes). The id is public (lists, logs and audits name a key by it); the secret is kept only as its SHA-256.
/// </summary>
internal static partial class DapperMaticApiKeyFormat
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz234567";

    /// <summary>
    /// Makes a key.
    /// </summary>
    /// <param name="prefix">What it begins with.</param>
    /// <returns>Its id, the hash of its secret, and the key to show once.</returns>
    public static (string Id, string SecretHash, string Key) Create(string prefix)
    {
        var id = Base32(RandomNumberGenerator.GetBytes(10));
        var secret = Base32(RandomNumberGenerator.GetBytes(32));
        return (id, Hash(secret), $"{prefix}_{id}_{secret}");
    }

    /// <summary>
    /// Reads a key's id and secret.
    /// </summary>
    /// <param name="key">What was sent.</param>
    /// <param name="prefix">What keys begin with.</param>
    /// <param name="id">The id.</param>
    /// <param name="secret">The secret.</param>
    /// <returns>True if it has the form of a key.</returns>
    public static bool TryParse(string key, string prefix, out string id, out string secret)
    {
        id = secret = string.Empty;
        var parts = key.Split('_');
        if (parts.Length != 3 || !string.Equals(parts[0], prefix, StringComparison.Ordinal) || !Shape().IsMatch(parts[1] + parts[2]) || parts[1].Length != 16 || parts[2].Length != 52)
        {
            return false;
        }

        (id, secret) = (parts[1], parts[2]);
        return true;
    }

    /// <summary>
    /// Hashes a secret: a key's secret is 256 random bits, so a fast hash keeps it as safe as a slow one would.
    /// </summary>
    /// <param name="secret">The secret.</param>
    /// <returns>Its SHA-256, as hexadecimal.</returns>
    public static string Hash(string secret) => Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(secret)));

    /// <summary>
    /// Tells whether a secret is a key's, in constant time.
    /// </summary>
    /// <param name="secret">The secret sent.</param>
    /// <param name="hash">The hash kept.</param>
    /// <returns>True if they match.</returns>
    public static bool Matches(string secret, string hash) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(secret)), Encoding.ASCII.GetBytes(hash));

    /// <summary>
    /// Refuses a prefix that is not 1 to 16 lowercase letters and digits.
    /// </summary>
    /// <param name="prefix">The prefix.</param>
    /// <exception cref="ArgumentException">It is not.</exception>
    public static void AssertPrefix(string prefix)
    {
        if (!PrefixShape().IsMatch(prefix ?? string.Empty))
        {
            throw new ArgumentException($"An API key prefix is 1 to 16 lowercase letters and digits, not '{prefix}'.");
        }
    }

    private static string Base32(byte[] bytes)
    {
        var text = new StringBuilder(((bytes.Length * 8) + 4) / 5);
        int buffer = 0, bits = 0;
        foreach (var b in bytes)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                text.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }

        if (bits > 0)
        {
            text.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        }

        return text.ToString();
    }

    [GeneratedRegex("^[a-z2-7]+$")]
    private static partial Regex Shape();

    [GeneratedRegex("^[a-z0-9]{1,16}$")]
    private static partial Regex PrefixShape();
}
