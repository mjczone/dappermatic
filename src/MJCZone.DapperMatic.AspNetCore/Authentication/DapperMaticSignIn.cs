// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Security;

namespace MJCZone.DapperMatic.AspNetCore.Authentication;

/// <summary>The names of DapperMatic's own sign-in method, which runs the methods its endpoints accept.</summary>
public static class DapperMaticAuthenticationDefaults
{
    /// <summary>
    /// The sign-in method (authentication scheme) that signs callers in as DapperMatic's endpoints do: name it in the
    /// application's own authorization policies (<c>policy.AddAuthenticationSchemes("DapperMatic")</c>) to authorize
    /// DapperMatic's group with the same callers.
    /// </summary>
    public const string Scheme = "DapperMatic";
}

/// <summary>
/// Signs in the caller of a DapperMatic endpoint with the sign-in methods DapperMatic's endpoints accept: DapperMatic's
/// own (API keys, built-in accounts, its JWTs), once turned on, then the methods <c>SignInWith</c> lists, in order;
/// with no list, the application's default method. The first that succeeds is the caller. With no list, a caller the
/// application's own authentication already signed in is kept.
/// </summary>
/// <param name="options">The DapperMatic options.</param>
/// <param name="schemes">The application's sign-in methods.</param>
internal sealed class DapperMaticSignIn(IOptions<DapperMaticOptions> options, IAuthenticationSchemeProvider schemes)
{
    private readonly DapperMaticOptions _options = options.Value;

    /// <summary>
    /// Finds the caller of a request.
    /// </summary>
    /// <param name="http">The request.</param>
    /// <param name="keepSignedIn">Whether a caller the application's authentication already signed in is kept (when no methods are listed).</param>
    /// <returns>The caller (signed in, or no one), when their sign-in ends, and why a method refused them, if one did.</returns>
    public async Task<(ClaimsPrincipal User, DateTimeOffset? Until, string? Failure)> AuthenticateAsync(HttpContext http, bool keepSignedIn = true)
    {
        var listed = _options.SignInSchemes;
        if (listed.Count == 0 && keepSignedIn && http.User.Identity?.IsAuthenticated == true)
        {
            return (http.User, null, null);
        }

        var candidates = new List<string>(_options.OwnSignInSchemes);
        if (listed.Count > 0)
        {
            candidates.AddRange(listed);
        }
        else if (await schemes.GetDefaultAuthenticateSchemeAsync().ConfigureAwait(false) is { } fallback)
        {
            candidates.Add(fallback.Name);
        }

        string? failure = null;
        foreach (var scheme in candidates.Where(s => s != DapperMaticAuthenticationDefaults.Scheme).Distinct(StringComparer.Ordinal))
        {
            var result = await http.AuthenticateAsync(scheme).ConfigureAwait(false);
            if (result.Succeeded && result.Principal is { } principal)
            {
                return (principal, result.Properties?.ExpiresUtc, null);
            }

            // A method that refused the caller does not stop the next being tried.
            failure ??= result.Failure?.Message;
        }

        // No method signed the caller in: with a list, they are no one, whatever the application's default method
        // read; without one, as the application's authentication left them.
        return (listed.Count > 0 ? new ClaimsPrincipal(new ClaimsIdentity()) : http.User, null, failure);
    }

    /// <summary>
    /// Refuses to start with a listed method the application has not registered.
    /// </summary>
    /// <exception cref="InvalidOperationException">A listed method is not registered.</exception>
    public void AssertRegistered()
    {
        foreach (var scheme in _options.SignInSchemes.Concat(_options.OwnSignInSchemes))
        {
            if (schemes.GetSchemeAsync(scheme).GetAwaiter().GetResult() is null)
            {
                throw new InvalidOperationException(
                    $"DapperMatic signs callers in with '{scheme}', which is not a registered authentication scheme: register it (services.AddAuthentication().Add…) or remove it from SignInWith."
                );
            }
        }
    }

    /// <summary>
    /// Tells whether a request carries credentials of its own (an <c>Authorization</c> header, an API key header): a
    /// method's refusal of those is the caller's answer (401), where a stale cookie's is not.
    /// </summary>
    /// <param name="http">The request.</param>
    /// <returns>True if it does.</returns>
    public bool CarriesCredentials(HttpContext http) =>
        http.Request.Headers.Authorization.Count > 0 || (_options.ApiKeys.Enabled && http.Request.Headers.ContainsKey(_options.ApiKeys.HeaderName));
}

/// <summary>
/// The <see cref="DapperMaticAuthenticationDefaults.Scheme"/> method: the caller as DapperMatic's endpoints sign them in.
/// </summary>
internal sealed class DapperMaticAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly DapperMaticSignIn _signIn;

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticAuthenticationHandler"/> class.
    /// </summary>
    /// <param name="options">The scheme's options.</param>
    /// <param name="logger">The logger factory.</param>
    /// <param name="encoder">The URL encoder.</param>
    /// <param name="signIn">How DapperMatic's endpoints sign callers in.</param>
    public DapperMaticAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, DapperMaticSignIn signIn)
        : base(options, logger, encoder)
    {
        _signIn = signIn;
    }

    /// <inheritdoc />
    /// <remarks>
    /// A sign-in that has ended (a key revoked, an account turned off, a membership gone) is refused here too, for the
    /// application's own endpoints. A key's limits are DapperMatic's: those endpoints see its owner's roles.
    /// </remarks>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var (user, until, failure) = await _signIn.AuthenticateAsync(Context).ConfigureAwait(false);
        if (user.Identity?.IsAuthenticated == true)
        {
            foreach (var check in Context.RequestServices.GetServices<IDapperMaticCallerCheck>())
            {
                try
                {
                    await check.CheckAsync(user, Context, again: false, Context.RequestAborted).ConfigureAwait(false);
                }
                catch (DapperMaticSignInEndedException ex)
                {
                    return AuthenticateResult.Fail(ex.Message);
                }
            }

            return AuthenticateResult.Success(new AuthenticationTicket(user, new AuthenticationProperties { ExpiresUtc = until }, Scheme.Name));
        }

        return failure is null ? AuthenticateResult.NoResult() : AuthenticateResult.Fail(failure);
    }
}

/// <summary>
/// How a request's caller signed in, kept with the request: when that sign-in ends (a cookie's expiry, a ticket's
/// caller's), which bounds their session.
/// </summary>
/// <param name="Until">When it ends, or null when the sign-in does not say.</param>
#pragma warning disable SA1313 // Positional record parameters are properties.
internal sealed record DapperMaticSignedIn(DateTimeOffset? Until);
#pragma warning restore SA1313
