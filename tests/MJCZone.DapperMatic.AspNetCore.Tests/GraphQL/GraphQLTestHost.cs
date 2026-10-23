// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MJCZone.DapperMatic.AspNetCore.Factories;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Tests.Factories;
using MJCZone.DapperMatic.GraphQL;
using MJCZone.DapperMatic.Models;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Tests.GraphQL;

/// <summary>
/// A host serving DapperMatic's GraphQL endpoint over tables of customers and their orders, with callers signed in by
/// headers: <c>X-Test-Role</c>, <c>X-Test-Name</c>, <c>X-Test-Tenant</c>.
/// </summary>
internal static class GraphQLTestHost
{
    /// <summary>The media type of GraphQL-over-HTTP responses whose status codes say what went wrong.</summary>
    public const string GraphQLResponse = "application/graphql-response+json";

    public static WebApplicationFactory<Program> Create(
        DatasourceDto datasource,
        Action<DapperMatic.AspNetCore.DapperMaticConfigurationBuilder>? configure = null,
        Action<DapperMaticOptions>? options = null,
        Action<DapperMaticGraphQLOptions>? graphql = null
    ) =>
        new WafWithInMemoryDatasourceRepository([datasource]).WithWebHostBuilder(web =>
            web.ConfigureServices(services =>
            {
                services.Configure<DapperMaticOptions>(o =>
                {
                    o.Policies.Clear();
                    o.Relations.Add(new DapperMaticRelation { TableName = "gq_orders", Name = "customer", OtherTableName = "gq_customers", Columns = ["customer_id"], OtherColumns = ["id"] });
                    o.Relations.Add(new DapperMaticRelation { TableName = "gq_customers", Name = "orders", OtherTableName = "gq_orders", Columns = ["id"], OtherColumns = ["customer_id"], Many = true });
                    options?.Invoke(o);
                });
                services.AddDapperMatic(dm => (configure ?? (b => b.Policy(DmPolicy.Allow(DmAction.All, "admin"))))(dm));
                services.AddDapperMaticGraphQL(graphql);
                services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, HeaderAuthentication>("Test", _ => { });
            })
        );

    /// <summary>Makes the tables: customers, their orders, each order's total and status.</summary>
    public static async Task CreateTablesAsync(WebApplicationFactory<Program> factory, DatasourceDto datasource, string? schema)
    {
        using var db = factory.Services.GetRequiredService<IDbConnectionFactory>().CreateConnection(datasource.Provider!, datasource.ConnectionString!);
        db.Open();
        await db.DropTableIfExistsAsync(schema, "gq_orders");
        await db.DropTableIfExistsAsync(schema, "gq_customers");
        await db.CreateTableIfNotExistsAsync(
            new DmTable(schema, "gq_customers", [new DmColumn("id", typeof(int), isPrimaryKey: true, isAutoIncrement: true), new DmColumn("name", typeof(string), length: 50), new DmColumn("country", typeof(string), length: 2, isNullable: true)])
        );
        await db.CreateTableIfNotExistsAsync(
            new DmTable(
                schema,
                "gq_orders",
                [
                    new DmColumn("id", typeof(int), isPrimaryKey: true, isAutoIncrement: true),
                    new DmColumn("customer_id", typeof(int), isNullable: true),
                    new DmColumn("total", typeof(decimal), precision: 10, scale: 2),
                    new DmColumn("status", typeof(string), length: 20),
                    new DmColumn("placed", typeof(DateTime), isNullable: true),
                    new DmColumn("ref", typeof(Guid), isNullable: true),
                ]
            )
        );
    }

    public static HttpClient Client(WebApplicationFactory<Program> factory, string role = "admin", string? tenant = null, string name = "tester")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        client.DefaultRequestHeaders.Add("X-Test-Name", name);
        if (tenant is not null)
        {
            client.DefaultRequestHeaders.Add("X-Test-Tenant", tenant);
        }

        return client;
    }

    /// <summary>Posts a GraphQL request; returns the status and the response.</summary>
    public static async Task<(HttpStatusCode Status, JsonElement Body)> PostAsync(HttpClient client, string path, string query, object? variables = null, string accept = GraphQLResponse)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { query, variables }), Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone());
    }

    /// <summary>The data of a response that must have no errors.</summary>
    public static JsonElement Data(this (HttpStatusCode Status, JsonElement Body) response)
    {
        if (response.Body.ValueKind != JsonValueKind.Object || response.Body.TryGetProperty("errors", out _) || response.Status != HttpStatusCode.OK)
        {
            throw new Xunit.Sdk.XunitException($"{(int)response.Status}: {response.Body}");
        }

        return response.Body.GetProperty("data");
    }

    /// <summary>The codes of a response's errors.</summary>
    public static List<string?> Codes(this (HttpStatusCode Status, JsonElement Body) response) =>
        response.Body.TryGetProperty("errors", out var errors)
            ? [.. errors.EnumerateArray().Select(e => e.TryGetProperty("extensions", out var x) && x.TryGetProperty("code", out var c) ? c.GetString() : null)]
            : [];

    // Signs requests in with the role, name and tenant their headers give.
    private sealed class HeaderAuthentication(
        Microsoft.Extensions.Options.IOptionsMonitor<AuthenticationSchemeOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder
    ) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Request.Headers.Authorization.ToString();
            var role = Request.Headers["X-Test-Role"].ToString();
            var name = Request.Headers["X-Test-Name"].ToString();
            var tenant = Request.Headers["X-Test-Tenant"].ToString();

            // An API key or an account's token is not this method's (as it is no JWT's): left to DapperMatic's.
            if (header.StartsWith("Bearer ", StringComparison.Ordinal) && (header.StartsWith("Bearer dm_", StringComparison.Ordinal) || !header.Contains(':', StringComparison.Ordinal)))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            // A socket's caller sends their token on connection_init: "Bearer role:name:tenant".
            if (header.StartsWith("Bearer ", StringComparison.Ordinal))
            {
                var parts = header[7..].Split(':');
                (role, name, tenant) = (parts[0], parts.ElementAtOrDefault(1) ?? "tester", parts.ElementAtOrDefault(2) ?? string.Empty);
            }

            if (role.Length == 0)
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            List<Claim> claims = [.. role.Split(',').Select(r => new Claim(ClaimTypes.Role, r)), new Claim(ClaimTypes.Name, name), new Claim(ClaimTypes.NameIdentifier, name)];
            if (tenant.Length > 0)
            {
                claims.Add(new Claim("tenant_id", tenant));
            }

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")), "Test")));
        }
    }
}
