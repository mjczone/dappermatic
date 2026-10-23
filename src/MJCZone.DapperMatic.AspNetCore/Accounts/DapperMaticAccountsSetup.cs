// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Authentication;
using MJCZone.DapperMatic.AspNetCore.Security;

namespace MJCZone.DapperMatic.AspNetCore.Accounts;

/// <summary>
/// Registers the built-in accounts: Identity (its sign-in, tokens and security), DapperMatic's store and places,
/// the bearer and cookie sign-in methods (without changing the application's default), Data Protection's keys kept
/// encrypted in the accounts' database, and their startup.
/// </summary>
internal static class DapperMaticAccountsSetup
{
    /// <summary>
    /// Adds the built-in accounts, once.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="configure">Settings, or null.</param>
    public static void Add(IServiceCollection services, Action<DapperMaticAccountOptions>? configure)
    {
        services.Configure<DapperMaticOptions>(options =>
        {
            options.Accounts.Enabled = true;
            configure?.Invoke(options.Accounts);
            foreach (var scheme in new[] { IdentityConstants.BearerScheme, IdentityConstants.ApplicationScheme })
            {
                if (!options.OwnSignInSchemes.Contains(scheme))
                {
                    options.OwnSignInSchemes.Add(scheme);
                }
            }
        });

        if (services.Any(d => d.ServiceType == typeof(DapperMaticAccountFlows)))
        {
            return;
        }

        if (services.Any(d => d.ServiceType.IsGenericType && d.ServiceType.GetGenericTypeDefinition() == typeof(UserManager<>)))
        {
            throw new InvalidOperationException("The application uses ASP.NET Core Identity itself: DapperMatic's built-in accounts take its place, or are left off.");
        }

        services.AddIdentityCore<DapperMaticUser>(o => o.User.RequireUniqueEmail = true)
            .AddSignInManager<DapperMaticSignInManager>()
            .AddDefaultTokenProviders()
            .AddUserStore<DapperMaticUserStore>();

        // Open sign-up asks for a confirmed email: an address not confirmed is anyone's claim. The application's
        // changes come last.
        services.AddOptions<IdentityOptions>().Configure<IOptions<DapperMaticOptions>>((identity, dm) =>
        {
            var accounts = dm.Value.Accounts;
            if (accounts.SignUp.Open || accounts.SignUp.EmailDomains.Count > 0)
            {
                identity.SignIn.RequireConfirmedEmail = true;
            }

            accounts.Identity?.Invoke(identity);
        });

        services.AddSingleton<DapperMaticAccountDb>();
        services.AddSingleton<DapperMaticAccountStore>();
        services.AddScoped<DapperMaticAccountPlace>();
        services.AddScoped<DapperMaticAccountPrincipals>();
        services.AddScoped<IUserClaimsPrincipalFactory<DapperMaticUser>>(sp => sp.GetRequiredService<DapperMaticAccountPrincipals>());
        services.AddSingleton<DapperMaticAccountCheck>();
        services.AddSingleton<IDapperMaticCallerCheck>(sp => sp.GetRequiredService<DapperMaticAccountCheck>());
        services.AddScoped<IDapperMaticKeyOwners, DapperMaticAccountOwners>();
        services.AddScoped<DapperMaticInvitations>();
        services.AddScoped<DapperMaticAccountFlows>();
        services.TryAddSingleton<IDapperMaticEmailSender, DapperMaticLoggedEmails>();
        services.AddSingleton<DapperMaticDataProtectionKeys>();
        services.AddSingleton<DapperMaticJwt>();
        services.AddHostedService<DapperMaticAccountsStartup>();

        // The accounts' sign-in methods, beside the application's (whose default stays its own). Cookies answer an API:
        // 401 and 403, never a redirect to a login page.
        services.AddAuthentication().AddBearerToken(IdentityConstants.BearerScheme).AddIdentityCookies(cookies =>
            cookies.ApplicationCookie?.Configure(o =>
            {
                o.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                o.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            })
        );
        services.AddOptions<BearerTokenOptions>(IdentityConstants.BearerScheme).Configure<IOptions<DapperMaticOptions>>((bearer, dm) => dm.Value.Accounts.BearerToken?.Invoke(bearer));

        // JWT access tokens, when turned on: written and read in place of the bearer handler's own format, lasting as
        // long as their settings say (refresh tokens stay its own).
        services.AddOptions<BearerTokenOptions>(IdentityConstants.BearerScheme).PostConfigure<IServiceProvider>((bearer, root) =>
        {
            var jwt = root.GetRequiredService<DapperMaticJwt>();
            if (jwt.Enabled)
            {
                bearer.BearerTokenProtector = jwt;
                bearer.BearerTokenExpiration = root.GetRequiredService<IOptions<DapperMaticOptions>>().Value.Accounts.Jwt!.AccessTokenLifetime;
            }
        });

        // A cookie's principal, made again when its security stamp is checked, stays in its tenant (or is refused).
        // Checked as often as DapperMatic reads access again, so that the application's own endpoints on the cookie follow too.
        services.AddOptions<SecurityStampValidatorOptions>().Configure<IServiceProvider>((validator, root) =>
        {
            validator.OnRefreshingPrincipal = context => RefreshAsync(context, root);
            validator.ValidationInterval = root.GetRequiredService<IOptions<DapperMaticOptions>>().Value.AccessRefreshInterval;
        });

        // Data Protection's keys (which sign the accounts' tokens and cookies, and the application's own): kept in the
        // accounts' database, encrypted, unless the application keeps them itself.
        services.AddOptions<KeyManagementOptions>().PostConfigure<IServiceProvider>((keys, root) =>
        {
            var dm = root.GetRequiredService<IOptions<DapperMaticOptions>>().Value;
            if (!dm.Accounts.KeepDataProtectionKeys)
            {
                return;
            }

            keys.XmlRepository = root.GetRequiredService<DapperMaticDataProtectionKeys>();
            if (dm.ConnectionStringEncryptionKey is { Length: > 0 } key)
            {
                keys.XmlEncryptor = new DapperMaticKeyEncryptor(key);
            }
        });

        DapperMaticAuthenticationSetup.AddSignIn(services);
    }

    // The cookie's principal for the same tenant and sign-in session; with the account or its membership gone, none: the
    // cookie is refused (by the application's own endpoints too), never renewed with roles it no longer has, nor with
    // the account's platform roles in their place.
    private static async Task RefreshAsync(SecurityStampRefreshingPrincipalContext context, IServiceProvider root)
    {
        var account = context.CurrentPrincipal?.Identities.FirstOrDefault(i => i.AuthenticationType == DapperMaticClaims.AccountAuthenticationType);
        var http = root.GetService<IHttpContextAccessor>()?.HttpContext;
        if (account is null || http is null)
        {
            return;
        }

        var users = http.RequestServices.GetRequiredService<UserManager<DapperMaticUser>>();
        var id = account.FindFirst(users.Options.ClaimsIdentity.UserIdClaimType)?.Value;
        if (id is null || await users.FindByIdAsync(id).ConfigureAwait(false) is not { } user)
        {
            context.NewPrincipal = null;
            return;
        }

        try
        {
            context.NewPrincipal = await http.RequestServices.GetRequiredService<DapperMaticAccountPrincipals>()
                .CreateAsync(user, account.FindFirst(DapperMaticClaims.Tenant)?.Value, chosen: true, account.FindFirst(DapperMaticClaims.Session)?.Value, http.RequestAborted)
                .ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException)
        {
            context.NewPrincipal = null;
        }
    }
}
