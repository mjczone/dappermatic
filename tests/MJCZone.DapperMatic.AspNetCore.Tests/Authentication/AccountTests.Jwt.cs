// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Buffers.Text;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Accounts;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Authentication;

/// <summary>
/// The accounts' access tokens as JWTs: what they carry, the published keys, and the tokens refused.
/// </summary>
public partial class AccountTests
{
    private const string Issuer = "https://dm.example.com/api/dm/auth";
    private const string Audience = "orders-api";

    // A login gives a JWT that signs in, which another service checks with the published keys; a refresh gives another.
    [Fact]
    public async Task Accounts_sign_in_with_jwts_checked_by_the_published_keys_Async()
    {
        using var rsa = RSA.Create(2048);
        using var host = await HostAsync(accounts: a => a.UseJwt(j => Jwt(j).AddKey("k1", rsa.ExportPkcs8PrivateKeyPem())));
        await AdminAsync(host);
        var (token, refresh) = await LoginAsync(host, "ops@example.com");
        var (header, payload) = Parts(token);
        header["alg"]!.GetValue<string>().Should().Be("RS256");
        header["kid"]!.GetValue<string>().Should().Be("k1");
        payload["iss"]!.GetValue<string>().Should().Be(Issuer);
        payload["aud"]!.GetValue<string>().Should().Be(Audience);
        payload["email"]!.GetValue<string>().Should().Be("ops@example.com");
        payload["role"]!.AsArray().Select(r => r!.GetValue<string>()).Should().Equal("admin");
        payload["sid"].Should().NotBeNull();
        (payload["exp"]!.GetValue<long>() - payload["iat"]!.GetValue<long>()).Should().Be(15 * 60);
        (await Bearer(host, token).GetFromJsonAsync<JsonElement>("/api/dm/auth/me")).GetProperty("email").GetString().Should().Be("ops@example.com");

        // Another service's check: the key published under the token's kid verifies its signature.
        var jwks = await host.Factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/dm/auth/jwks.json");
        var jwk = jwks.GetProperty("keys").EnumerateArray().Single();
        (jwk.GetProperty("kid").GetString(), jwk.GetProperty("kty").GetString(), jwk.GetProperty("alg").GetString()).Should().Be(("k1", "RSA", "RS256"));
        jwk.TryGetProperty("d", out _).Should().BeFalse();
        using var published = RSA.Create(new RSAParameters { Modulus = Base64Url.DecodeFromChars(jwk.GetProperty("n").GetString()), Exponent = Base64Url.DecodeFromChars(jwk.GetProperty("e").GetString()) });
        var parts = token.Split('.');
        published.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), Base64Url.DecodeFromChars(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1).Should().BeTrue();

        // The discovery document other services' JWT handlers read: the issuer, and where the keys are.
        // It names the keys' address from the issuer alone: a request's Host header changes nothing.
        using var forged = new HttpRequestMessage(HttpMethod.Get, "/api/dm/auth/.well-known/openid-configuration");
        forged.Headers.Host = "keys.attacker.example";
        using var answer = await host.Factory.CreateClient().SendAsync(forged);
        var discovery = await answer.Content.ReadFromJsonAsync<JsonElement>();
        discovery.GetProperty("issuer").GetString().Should().Be(Issuer);
        discovery.GetProperty("jwks_uri").GetString().Should().Be(Issuer + "/jwks.json");
        discovery.GetProperty("id_token_signing_alg_values_supported").EnumerateArray().Select(a => a.GetString()).Should().Equal("RS256");

        using var refreshed = await host.Factory.CreateClient().PostAsJsonAsync("/api/dm/auth/refresh", new { refreshToken = refresh });
        var again = (await refreshed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        Parts(again).Header["kid"]!.GetValue<string>().Should().Be("k1");
        (await Bearer(host, again).GetAsync("/api/dm/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // Without JWTs, access tokens are Identity's own, and no keys are published.
    [Fact]
    public async Task Without_jwts_tokens_are_opaque_and_no_keys_are_published_Async()
    {
        using var host = await HostAsync();
        await AdminAsync(host);
        var (token, _) = await LoginAsync(host, "ops@example.com");
        token.Split('.').Should().HaveCount(1);
        (await host.Factory.CreateClient().GetAsync("/api/dm/auth/jwks.json")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // Only tokens DapperMatic signed, as it signs them: its key by the kid, that key's algorithm, its issuer and
    // audience, in their time.
    [Fact]
    public async Task Jwts_not_signed_and_made_as_DapperMatic_makes_them_are_refused_Async()
    {
        using var rsa = RSA.Create(2048);
        using var other = RSA.Create(2048);
        using var host = await HostAsync(accounts: a => a.UseJwt(j => Jwt(j).AddKey("k1", rsa.ExportPkcs8PrivateKeyPem())));
        await AdminAsync(host);
        var (token, _) = await LoginAsync(host, "ops@example.com");
        var (header, payload) = Parts(token);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        async Task<HttpStatusCode> StatusAsync(string jwt) => (await Bearer(host, jwt).GetAsync("/api/dm/auth/me")).StatusCode;
        string Signed(Action<JsonObject, JsonObject> change, RSA? key = null)
        {
            var (h, p) = ((JsonObject)header.DeepClone(), (JsonObject)payload.DeepClone());
            change(h, p);
            var signed = Encode(h) + "." + Encode(p);
            return signed + "." + Base64Url.EncodeToString((key ?? rsa).SignData(Encoding.ASCII.GetBytes(signed), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        }

        // Signed again as it was: accepted (what follows changes one thing each).
        (await StatusAsync(Signed((h, p) => { }))).Should().Be(HttpStatusCode.OK);

        var parts = token.Split('.');
        var raised = (JsonObject)payload.DeepClone();
        raised["role"] = new JsonArray("root");
        (await StatusAsync(parts[0] + "." + Encode(raised) + "." + parts[2])).Should().Be(HttpStatusCode.Unauthorized);
        (await StatusAsync(Signed((h, p) => { }, other))).Should().Be(HttpStatusCode.Unauthorized);
        (await StatusAsync(Signed((h, p) => p["iss"] = "https://elsewhere.example.com"))).Should().Be(HttpStatusCode.Unauthorized);
        (await StatusAsync(Signed((h, p) => p["aud"] = "another-api"))).Should().Be(HttpStatusCode.Unauthorized);
        (await StatusAsync(Signed((h, p) => p["exp"] = now - 1))).Should().Be(HttpStatusCode.Unauthorized);
        (await StatusAsync(Signed((h, p) => p["nbf"] = now + 600))).Should().Be(HttpStatusCode.Unauthorized);
        (await StatusAsync(Signed((h, p) => p.Remove("exp")))).Should().Be(HttpStatusCode.Unauthorized);
        (await StatusAsync(Signed((h, p) => p.Remove("sid")))).Should().Be(HttpStatusCode.Unauthorized);
        (await StatusAsync(Signed((h, p) => h["kid"] = "k2"))).Should().Be(HttpStatusCode.Unauthorized);
        (await StatusAsync(Signed((h, p) => h.Remove("kid")))).Should().Be(HttpStatusCode.Unauthorized);
        (await StatusAsync(Signed((h, p) => h["alg"] = "PS256"))).Should().Be(HttpStatusCode.Unauthorized);

        // An audience among several is the token's.
        (await StatusAsync(Signed((h, p) => p["aud"] = new JsonArray("another-api", Audience)))).Should().Be(HttpStatusCode.OK);

        // No signature (alg none), and the public key used as an HMAC secret (algorithm confusion).
        var unsigned = Encode(new JsonObject { ["alg"] = "none", ["typ"] = "JWT", ["kid"] = "k1" }) + "." + parts[1] + ".";
        (await StatusAsync(unsigned)).Should().Be(HttpStatusCode.Unauthorized);
        var hs = Encode(new JsonObject { ["alg"] = "HS256", ["typ"] = "JWT", ["kid"] = "k1" }) + "." + parts[1];
        var confused = hs + "." + Base64Url.EncodeToString(HMACSHA256.HashData(Encoding.ASCII.GetBytes(rsa.ExportSubjectPublicKeyInfoPem()), Encoding.ASCII.GetBytes(hs)));
        (await StatusAsync(confused)).Should().Be(HttpStatusCode.Unauthorized);
    }

    // ES256, and a change of keys: the new key signs, the old one's tokens are accepted until it is taken out.
    [Fact]
    public async Task Jwt_keys_change_with_the_new_one_first_Async()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var old = RSA.Create(2048);
        using var host = await HostAsync(accounts: a => a.UseJwt(j => Jwt(j).AddKey("new", ec.ExportPkcs8PrivateKeyPem()).AddKey("old", old.ExportSubjectPublicKeyInfoPem())));
        await AdminAsync(host);
        var (token, _) = await LoginAsync(host, "ops@example.com");
        var (header, payload) = Parts(token);
        header["alg"]!.GetValue<string>().Should().Be("ES256");
        header["kid"]!.GetValue<string>().Should().Be("new");
        (await Bearer(host, token).GetAsync("/api/dm/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        var jwks = await host.Factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/dm/auth/jwks.json");
        jwks.GetProperty("keys").EnumerateArray().Select(k => (k.GetProperty("kid").GetString(), k.GetProperty("alg").GetString())).Should().Equal(("new", "ES256"), ("old", "RS256"));

        // A token the old key signed (before the change) is accepted; one naming it with another algorithm is not.
        string OldSigned(string alg)
        {
            var signed = Encode(new JsonObject { ["alg"] = alg, ["typ"] = "JWT", ["kid"] = "old" }) + "." + Encode(payload);
            return signed + "." + Base64Url.EncodeToString(old.SignData(Encoding.ASCII.GetBytes(signed), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        }

        (await Bearer(host, OldSigned("RS256")).GetAsync("/api/dm/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Bearer(host, OldSigned("ES256")).GetAsync("/api/dm/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Settings refused: no issuer or audience, weak or unknown keys, a first key that cannot sign, two keys of one id.
    [Fact]
    public void Jwt_settings_that_would_sign_badly_are_refused()
    {
        using var rsa = RSA.Create(2048);
        using var small = RSA.Create(1024);
        using var p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        var good = rsa.ExportPkcs8PrivateKeyPem();
        static bool Accepted(Action<DapperMaticJwtOptions> configure)
        {
            var options = new DapperMaticOptions();
            options.Accounts.UseJwt(configure);
            using var jwt = new DapperMaticJwt(Options.Create(options), Options.Create(new IdentityOptions()));
            try
            {
                return jwt.Enabled;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        Accepted(j => Jwt(j).AddKey("k", good)).Should().BeTrue();
        Accepted(j => j.AddKey("k", good)).Should().BeFalse();
        Accepted(j => Jwt(j)).Should().BeFalse();
        Accepted(j => Jwt(j).AddKey("k", small.ExportPkcs8PrivateKeyPem())).Should().BeFalse();
        Accepted(j => Jwt(j).AddKey("k", p384.ExportPkcs8PrivateKeyPem())).Should().BeFalse();
        Accepted(j => Jwt(j).AddKey("k", rsa.ExportSubjectPublicKeyInfoPem())).Should().BeFalse();
        Accepted(j => Jwt(j).AddKey("k", "not a key")).Should().BeFalse();
        Accepted(j => Jwt(j).AddKey("k", good).AddKey("k", good)).Should().BeFalse();
        Accepted(j => Jwt(j).AddKey("k", good).AddKey("o", rsa.ExportSubjectPublicKeyInfoPem())).Should().BeTrue();
    }

    private static DapperMaticJwtOptions Jwt(DapperMaticJwtOptions options)
    {
        options.Issuer = Issuer;
        options.Audience = Audience;
        return options;
    }

    private static (JsonObject Header, JsonObject Payload) Parts(string jwt)
    {
        var parts = jwt.Split('.');
        parts.Should().HaveCount(3);
        return (JsonNode.Parse(Base64Url.DecodeFromChars(parts[0]))!.AsObject(), JsonNode.Parse(Base64Url.DecodeFromChars(parts[1]))!.AsObject());
    }

    private static string Encode(JsonNode value) => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(value.ToJsonString()));
}
