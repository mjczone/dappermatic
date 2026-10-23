// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace MJCZone.DapperMatic.AspNetCore.Authentication;

/// <summary>
/// Registers DapperMatic's own sign-in method (<see cref="DapperMaticAuthenticationDefaults.Scheme"/>), once some
/// sign-in feature is on, without changing the application's: a lone method of the application's stays its default
/// when DapperMatic's are added beside it.
/// </summary>
internal static class DapperMaticAuthenticationSetup
{
    /// <summary>
    /// Adds the method, once.
    /// </summary>
    /// <param name="services">The services.</param>
    public static void AddSignIn(IServiceCollection services)
    {
        if (services.Any(d => d.ServiceType == typeof(DapperMaticAuthenticationHandler)))
        {
            return;
        }

        services.AddTransient<DapperMaticAuthenticationHandler>();
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, DapperMaticAuthenticationHandler>(DapperMaticAuthenticationDefaults.Scheme, null);

        // ASP.NET Core takes a lone registered method as the default; with DapperMatic's beside it, it would have none.
        services.PostConfigure<AuthenticationOptions>(options =>
        {
            if (options.DefaultScheme is null && options.DefaultAuthenticateScheme is null && options.DefaultChallengeScheme is null)
            {
                var theirs = options.Schemes.Select(s => s.Name).Where(name => !IsOwn(name)).ToList();
                if (theirs.Count == 1)
                {
                    options.DefaultScheme = theirs[0];
                }
            }
        });
    }

    /// <summary>
    /// Tells whether a sign-in method is one DapperMatic registers: its own, or the built-in accounts' (Identity's).
    /// </summary>
    /// <param name="scheme">The method's name.</param>
    /// <returns>True if it is.</returns>
    public static bool IsOwn(string scheme) =>
        scheme.StartsWith(DapperMaticAuthenticationDefaults.Scheme, StringComparison.Ordinal)
        || scheme == IdentityConstants.ApplicationScheme
        || scheme == IdentityConstants.BearerScheme
        || scheme == IdentityConstants.ExternalScheme
        || scheme == IdentityConstants.TwoFactorRememberMeScheme
        || scheme == IdentityConstants.TwoFactorUserIdScheme;
}
