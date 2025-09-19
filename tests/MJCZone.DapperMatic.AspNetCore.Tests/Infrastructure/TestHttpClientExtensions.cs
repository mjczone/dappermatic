// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Infrastructure;

/// <summary>
/// Extension methods for HttpClient in tests.
/// </summary>
public static class TestHttpClientExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Creates an authenticated HTTP client with the specified claims.
    /// </summary>
    /// <param name="factory">The web application factory.</param>
    /// <param name="claims">The claims for the authenticated user.</param>
    /// <returns>An authenticated HTTP client.</returns>
    public static HttpClient CreateAuthenticatedClient(
        this WebApplicationFactory<Program> factory,
        params Claim[] claims
    )
    {
        return factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services
                        .AddAuthentication("Test")
                        .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                            "Test",
                            options => { }
                        );
                    services.AddSingleton(new TestAuthenticationHandler.TestUser(claims));
                });
            })
            .CreateClient();
    }

    /// <summary>
    /// Gets JSON content from the response.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="response">The HTTP response.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The deserialized content.</returns>
    public static async Task<T?> ReadAsJsonAsync<T>(
        this HttpResponseMessage response,
        CancellationToken cancellationToken = default
    )
    {
        var json = await response
            .Content.ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    /// <summary>
    /// Ensures the response has the expected status code.
    /// </summary>
    /// <param name="response">The HTTP response.</param>
    /// <param name="expectedStatusCode">The expected status code.</param>
    /// <returns>The HTTP response for chaining.</returns>
    public static HttpResponseMessage EnsureStatusCode(
        this HttpResponseMessage response,
        System.Net.HttpStatusCode expectedStatusCode
    )
    {
        if (response.StatusCode != expectedStatusCode)
        {
            var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            throw new InvalidOperationException(
                $"Expected status code {expectedStatusCode} but got {response.StatusCode}. Content: {content}"
            );
        }

        return response;
    }
}

/// <summary>
/// Test authentication handler for creating authenticated test requests.
/// </summary>
public class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestAuthenticationHandler"/> class.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="encoder">The URL encoder.</param>
    /// <param name="user">The test user.</param>
    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder,
        TestUser user
    )
        : base(options, logger, encoder)
    {
        User = user;
    }

    /// <summary>
    /// Gets the test user.
    /// </summary>
    public TestUser User { get; }

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(User.Claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    /// <summary>
    /// Represents a test user with claims.
    /// </summary>
    /// <param name="Claims">The user's claims.</param>
    public record TestUser(Claim[] Claims);
}
