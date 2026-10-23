// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Authentication;

namespace MJCZone.DapperMatic.AspNetCore.Accounts;

/// <summary>
/// The built-in accounts' access tokens as JSON Web Tokens, which other services check with the keys DapperMatic
/// publishes (<c>auth/jwks.json</c>). Signed with RS256 (an RSA key of 2,048 bits or more) or ES256 (a P-256 key) only.
/// </summary>
public sealed class DapperMaticJwtOptions
{
    /// <summary>Gets or sets the issuer (<c>iss</c>): who made the token, as the services that check it expect. Required.</summary>
    public string? Issuer { get; set; }

    /// <summary>Gets or sets the audience (<c>aud</c>): who the token is for, as the services that check it expect. Required.</summary>
    public string? Audience { get; set; }

    /// <summary>
    /// Gets the signing keys: the first signs, every one is accepted and published. To change keys, put the new key
    /// first and keep the old one until the tokens it signed have expired.
    /// </summary>
    public List<DapperMaticJwtKey> Keys { get; } = [];

    /// <summary>Gets or sets how long an access token lasts. Default is 15 minutes: the refresh token gets another.</summary>
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Adds a signing key.
    /// </summary>
    /// <param name="keyId">The key's id (<c>kid</c>).</param>
    /// <param name="pem">The key in PEM: an RSA key of 2,048 bits or more, or a P-256 EC key; private for the first.</param>
    /// <returns>These options.</returns>
    public DapperMaticJwtOptions AddKey(string keyId, string pem)
    {
        Keys.Add(new DapperMaticJwtKey { KeyId = keyId, Pem = pem });
        return this;
    }
}

/// <summary>A JWT signing key.</summary>
public sealed class DapperMaticJwtKey
{
    /// <summary>Gets or sets the key's id (<c>kid</c>), which every token it signs names.</summary>
    public string KeyId { get; set; } = string.Empty;

    /// <summary>Gets or sets the key in PEM: an RSA key of 2,048 bits or more, or a P-256 EC key.</summary>
    public string Pem { get; set; } = string.Empty;
}

/// <summary>
/// Writes and reads the accounts' access tokens as JWTs, in place of the bearer handler's own format. It reads only the
/// tokens it writes: the header's <c>kid</c> picks one of the configured keys and its <c>alg</c> must be that key's;
/// <c>iss</c>, <c>aud</c> and <c>exp</c> are required. A token read back is an account's sign-in like any other, checked
/// at each request against its session, membership and roles.
/// </summary>
internal sealed class DapperMaticJwt : ISecureDataFormat<AuthenticationTicket>, IDisposable
{
    // An EC key's curve: P-256 (secp256r1, prime256v1), by its OID or the names platforms give it.
    private const string P256 = "1.2.840.10045.3.1.7";

    private static readonly TimeSpan Skew = TimeSpan.FromSeconds(30);
    private static readonly string[] P256Names = ["nistP256", "ECDSA_P256", "prime256v1", "secp256r1"];

    private readonly Lazy<Settings?> _settings;
    private readonly ClaimsIdentityOptions _types;
    private readonly TimeProvider _time;

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticJwt"/> class.
    /// </summary>
    /// <param name="options">DapperMatic's options: the accounts' JWT settings, if any.</param>
    /// <param name="identity">Identity's options: its claim types.</param>
    /// <param name="time">The clock, or null for the system's.</param>
    public DapperMaticJwt(IOptions<DapperMaticOptions> options, IOptions<IdentityOptions> identity, TimeProvider? time = null)
    {
        _settings = new Lazy<Settings?>(() => options.Value.Accounts.Jwt is { } jwt ? Settings.Of(jwt) : null);
        _types = identity.Value.ClaimsIdentity;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Gets a value indicating whether JWTs are turned on; reading it checks their settings (and throws on bad ones).</summary>
    public bool Enabled => _settings.Value is not null;

    /// <summary>
    /// Gets the public keys as a JSON Web Key Set, or null when JWTs are off.
    /// </summary>
    /// <returns>The key set.</returns>
    public JsonObject? Jwks()
    {
        if (_settings.Value is not { } settings)
        {
            return null;
        }

        var keys = new JsonArray();
        foreach (var key in settings.Keys)
        {
            var jwk = new JsonObject { ["kid"] = key.Id, ["use"] = "sig", ["alg"] = key.Algorithm };
            if (key.Value is RSA rsa)
            {
                var p = rsa.ExportParameters(false);
                jwk["kty"] = "RSA";
                jwk["n"] = Base64Url.EncodeToString(p.Modulus);
                jwk["e"] = Base64Url.EncodeToString(p.Exponent);
            }
            else
            {
                var p = ((ECDsa)key.Value).ExportParameters(false);
                jwk["kty"] = "EC";
                jwk["crv"] = "P-256";
                jwk["x"] = Base64Url.EncodeToString(p.Q.X);
                jwk["y"] = Base64Url.EncodeToString(p.Q.Y);
            }

            keys.Add(jwk);
        }

        return new JsonObject { ["keys"] = keys };
    }

    /// <inheritdoc />
    public string Protect(AuthenticationTicket data) => Protect(data, null);

    /// <inheritdoc />
    public string Protect(AuthenticationTicket data, string? purpose)
    {
        ArgumentNullException.ThrowIfNull(data);
        var settings = _settings.Value ?? throw new InvalidOperationException("JWTs are not turned on.");
        var account = data.Principal.Identities.FirstOrDefault(i => i.AuthenticationType == DapperMaticClaims.AccountAuthenticationType)
            ?? throw new InvalidOperationException("Only a built-in account's sign-in is written as a JWT.");
        var issued = data.Properties.IssuedUtc ?? _time.GetUtcNow();
        var expires = data.Properties.ExpiresUtc ?? throw new InvalidOperationException("A JWT needs its expiry.");
        var payload = new JsonObject
        {
            ["iss"] = settings.Issuer,
            ["aud"] = settings.Audience,
            ["sub"] = account.FindFirst(_types.UserIdClaimType)?.Value ?? throw new InvalidOperationException("The sign-in names no account."),
            ["iat"] = issued.ToUnixTimeSeconds(),
            ["nbf"] = issued.ToUnixTimeSeconds(),
            ["exp"] = expires.ToUnixTimeSeconds(),
            ["jti"] = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16)),
        };
        foreach (var (name, type) in Claims())
        {
            if (account.FindFirst(type)?.Value is { } value)
            {
                payload[name] = value;
            }
        }

        payload["role"] = new JsonArray([.. account.FindAll(account.RoleClaimType).Select(c => (JsonNode?)c.Value)]);
        var key = settings.Keys[0];
        var header = new JsonObject { ["alg"] = key.Algorithm, ["typ"] = "JWT", ["kid"] = key.Id };
        var signed = Encode(header) + "." + Encode(payload);
        return signed + "." + Base64Url.EncodeToString(key.Sign(Encoding.ASCII.GetBytes(signed)));
    }

    /// <inheritdoc />
    public AuthenticationTicket? Unprotect(string? protectedText) => Unprotect(protectedText, null);

    /// <inheritdoc />
    public AuthenticationTicket? Unprotect(string? protectedText, string? purpose)
    {
        if (_settings.Value is not { } settings || string.IsNullOrEmpty(protectedText) || protectedText.Length > 16_384 || protectedText.Split('.') is not [var head, var body, var signature])
        {
            return null;
        }

        try
        {
            // The key the header names, with its own algorithm: no other, and none from the token.
            using var header = JsonDocument.Parse(Base64Url.DecodeFromChars(head));
            if (header.RootElement.ValueKind != JsonValueKind.Object
                || header.RootElement.TryGetProperty("crit", out _)
                || Text(header.RootElement, "kid") is not { } kid
                || settings.Keys.FirstOrDefault(k => k.Id == kid) is not { } key
                || Text(header.RootElement, "alg") != key.Algorithm
                || !key.Verify(Encoding.ASCII.GetBytes(head + "." + body), Base64Url.DecodeFromChars(signature)))
            {
                return null;
            }

            using var document = JsonDocument.Parse(Base64Url.DecodeFromChars(body));
            var claims = document.RootElement;
            var now = _time.GetUtcNow();
            if (claims.ValueKind != JsonValueKind.Object
                || Text(claims, "iss") != settings.Issuer
                || !ForAudience(claims, settings.Audience)
                || Time(claims, "exp") is not { } expires
                || now >= expires
                || (claims.TryGetProperty("nbf", out _) && (Time(claims, "nbf") is not { } notBefore || now + Skew < notBefore))
                || Text(claims, "sub") is not { } subject
                || Text(claims, "sid") is null)
            {
                return null;
            }

            var read = new List<Claim> { new(_types.UserIdClaimType, subject) };
            foreach (var (name, type) in Claims())
            {
                if (claims.TryGetProperty(name, out var value))
                {
                    read.Add(new Claim(type, value.ValueKind == JsonValueKind.String ? value.GetString()! : throw new FormatException($"The claim '{name}' is not text.")));
                }
            }

            if (claims.TryGetProperty("role", out var roles))
            {
                foreach (var role in roles.ValueKind == JsonValueKind.Array ? roles.EnumerateArray() : Enumerable.Repeat(roles, 1))
                {
                    read.Add(new Claim(_types.RoleClaimType, role.ValueKind == JsonValueKind.String ? role.GetString()! : throw new FormatException("A role is not text.")));
                }
            }

            var principal = new ClaimsPrincipal(new ClaimsIdentity(read, DapperMaticClaims.AccountAuthenticationType, _types.UserNameClaimType, _types.RoleClaimType));
            return new AuthenticationTicket(principal, new AuthenticationProperties { ExpiresUtc = expires, IssuedUtc = Time(claims, "iat") }, IdentityConstants.BearerScheme);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or CryptographicException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_settings.IsValueCreated && _settings.Value is { } settings)
        {
            foreach (var key in settings.Keys)
            {
                key.Value.Dispose();
            }
        }
    }

    private static string Encode(JsonObject value) => Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(value));

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static DateTimeOffset? Time(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var seconds) && seconds is >= 0 and <= 253_402_300_799
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;

    private static bool ForAudience(JsonElement claims, string audience) =>
        claims.TryGetProperty("aud", out var aud)
        && (aud.ValueKind == JsonValueKind.String ? aud.GetString() == audience
            : aud.ValueKind == JsonValueKind.Array && aud.EnumerateArray().Any(a => a.ValueKind == JsonValueKind.String && a.GetString() == audience));

    // The claims a token carries besides sub and role, by their names in the token: Identity's, and DapperMatic's own
    // (the sign-in session as OpenID Connect's sid).
    private IEnumerable<(string Name, string Type)> Claims() =>
    [
        ("name", _types.UserNameClaimType),
        ("email", _types.EmailClaimType),
        ("sid", DapperMaticClaims.Session),
        (DapperMaticClaims.Tenant, DapperMaticClaims.Tenant),
        (DapperMaticClaims.Membership, DapperMaticClaims.Membership),
        (DapperMaticClaims.Grant, DapperMaticClaims.Grant),
        (DapperMaticClaims.GrantedBy, DapperMaticClaims.GrantedBy),
    ];

#pragma warning disable SA1313 // Positional record parameters are properties.

    // The settings, checked: an issuer, an audience, keys of the kinds allowed with distinct ids, the first private.
    private sealed record Settings(string Issuer, string Audience, IReadOnlyList<Key> Keys)
    {
        public static Settings Of(DapperMaticJwtOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.Issuer) || string.IsNullOrWhiteSpace(options.Audience))
            {
                throw new InvalidOperationException("JWTs need an issuer and an audience.");
            }

            if (options.Keys.Count == 0)
            {
                throw new InvalidOperationException("JWTs need a signing key.");
            }

            if (options.Keys.GroupBy(k => k.KeyId, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1) is { } twice)
            {
                throw new InvalidOperationException($"Two JWT keys have the id '{twice.Key}'.");
            }

            if (options.AccessTokenLifetime <= TimeSpan.Zero)
            {
                throw new InvalidOperationException("A JWT's lifetime is more than nothing.");
            }

            var keys = new List<Key>();
            try
            {
                keys.AddRange(options.Keys.Select(Key.Of));
            }
            catch
            {
                keys.ForEach(k => k.Value.Dispose());
                throw;
            }

            if (!keys[0].CanSign())
            {
                keys.ForEach(k => k.Value.Dispose());
                throw new InvalidOperationException($"The first JWT key ('{keys[0].Id}') signs: it needs its private part.");
            }

            return new Settings(options.Issuer, options.Audience, keys);
        }
    }

    // A signing key and its algorithm.
    private sealed record Key(string Id, string Algorithm, AsymmetricAlgorithm Value)
    {
        public static Key Of(DapperMaticJwtKey key)
        {
            if (string.IsNullOrWhiteSpace(key.KeyId))
            {
                throw new InvalidOperationException("A JWT key needs an id.");
            }

            var rsa = RSA.Create();
            try
            {
                rsa.ImportFromPem(key.Pem);
                if (rsa.KeySize < 2048)
                {
                    throw new InvalidOperationException($"The JWT key '{key.KeyId}' is an RSA key of {rsa.KeySize} bits: 2,048 or more are needed.");
                }

                return new Key(key.KeyId, "RS256", rsa);
            }
            catch (Exception ex) when (ex is ArgumentException or CryptographicException)
            {
                rsa.Dispose();
            }
            catch
            {
                rsa.Dispose();
                throw;
            }

            var ec = ECDsa.Create();
            try
            {
                ec.ImportFromPem(key.Pem);
                var curve = ec.ExportParameters(false).Curve;
                if (curve.Oid?.Value != P256 && !P256Names.Contains(curve.Oid?.FriendlyName, StringComparer.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"The JWT key '{key.KeyId}' is an EC key on another curve than P-256.");
                }

                return new Key(key.KeyId, "ES256", ec);
            }
            catch (Exception ex) when (ex is ArgumentException or CryptographicException)
            {
                ec.Dispose();
                throw new InvalidOperationException($"The JWT key '{key.KeyId}' is not an RSA or EC key in PEM.", ex);
            }
            catch
            {
                ec.Dispose();
                throw;
            }
        }

        public bool CanSign()
        {
            try
            {
                Sign([0]);
                return true;
            }
            catch (CryptographicException)
            {
                return false;
            }
        }

        public byte[] Sign(byte[] data) =>
            Value is RSA rsa ? rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1) : ((ECDsa)Value).SignData(data, HashAlgorithmName.SHA256);

        public bool Verify(byte[] data, byte[] signature) =>
            Value is RSA rsa ? rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
                : signature.Length == 64 && ((ECDsa)Value).VerifyData(data, signature, HashAlgorithmName.SHA256);
    }
#pragma warning restore SA1313
}
