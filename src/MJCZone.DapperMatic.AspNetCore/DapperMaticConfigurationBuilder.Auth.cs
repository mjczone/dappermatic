// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MJCZone.DapperMatic.AspNetCore.Authentication;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Repositories;
using MJCZone.DapperMatic.AspNetCore.Security;

namespace MJCZone.DapperMatic.AspNetCore;

/// <content>
/// Signing in: the methods DapperMatic's endpoints accept, where roles are found, API keys.
/// </content>
public sealed partial class DapperMaticConfigurationBuilder
{
    /// <summary>
    /// Names the sign-in methods (authentication schemes) DapperMatic's endpoints accept, tried in order after
    /// DapperMatic's own (API keys, built-in accounts) once those are on; the first that signs the caller in is the
    /// caller, and a caller none signs in is no one (the application's default method is not read unless listed).
    /// Without it, the application's default method, and a caller its authentication already signed in.
    /// </summary>
    /// <param name="schemes">The methods, as registered with <c>AddAuthentication()</c>.</param>
    /// <returns>The builder.</returns>
    public DapperMaticConfigurationBuilder SignInWith(params string[] schemes)
    {
        ArgumentNullException.ThrowIfNull(schemes);
        _services.Configure<DapperMaticOptions>(options => options.SignInSchemes.AddRange(schemes.Where(s => !options.SignInSchemes.Contains(s))));
        DapperMaticAuthenticationSetup.AddSignIn(_services);
        return this;
    }

    /// <summary>
    /// Finds callers' roles in another claim too: each of its values a role, or one value holding a JSON array of them.
    /// Several sources add up, with ASP.NET Core's role claim; only DapperMatic's checks read them.
    /// </summary>
    /// <param name="claimType">The claim type, exactly (Auth0's <c>https://acme.com/roles</c>, Entra's <c>roles</c>).</param>
    /// <returns>The builder.</returns>
    public DapperMaticConfigurationBuilder RolesFrom(string claimType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(claimType);
        _services.Configure<DapperMaticOptions>(options => options.RoleSources.Add(new DapperMaticRoleSource { ClaimType = claimType }));
        return this;
    }

    /// <summary>
    /// Finds callers' roles inside a claim holding JSON: Keycloak's <c>RolesFrom("realm_access", "roles")</c>.
    /// </summary>
    /// <param name="claimType">The claim type.</param>
    /// <param name="jsonPath">The path into its JSON value, properties separated by dots.</param>
    /// <returns>The builder.</returns>
    public DapperMaticConfigurationBuilder RolesFrom(string claimType, string jsonPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(claimType);
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonPath);
        _services.Configure<DapperMaticOptions>(options => options.RoleSources.Add(new DapperMaticRoleSource { ClaimType = claimType, JsonPath = jsonPath }));
        return this;
    }

    /// <summary>
    /// Finds callers' roles by code: the request (and through it any service) and the caller in, roles out. Read once,
    /// when the caller's session is built.
    /// </summary>
    /// <param name="roles">The code.</param>
    /// <returns>The builder.</returns>
    public DapperMaticConfigurationBuilder RolesFrom(Func<HttpContext, ClaimsPrincipal, CancellationToken, ValueTask<IEnumerable<string>?>> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);
        _services.Configure<DapperMaticOptions>(options => options.RoleSources.Add(new DapperMaticRoleSource { Code = roles }));
        return this;
    }

    /// <summary>
    /// Gives a service account in code: an account for a program, whose keys sign in as it.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <returns>The builder.</returns>
    public DapperMaticConfigurationBuilder ServiceAccount(DapperMaticServiceAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentException.ThrowIfNullOrWhiteSpace(account.Name);
        _services.Configure<DapperMaticOptions>(options => options.ServiceAccounts.Add(account));
        return this;
    }

    /// <summary>
    /// Turns API keys on: service accounts' keys and personal keys, sent as <c>Authorization: Bearer {key}</c> or in the
    /// key header, kept where DapperMatic keeps access (its database, a file beside its file, or memory) unless
    /// <see cref="UseKeyRepository{TRepository}"/> says otherwise.
    /// </summary>
    /// <param name="configure">Settings, or null for the defaults.</param>
    /// <returns>The builder.</returns>
    public DapperMaticConfigurationBuilder AddApiKeys(Action<DapperMaticApiKeyOptions>? configure = null)
    {
        _services.Configure<DapperMaticOptions>(options =>
        {
            options.ApiKeys.Enabled = true;
            configure?.Invoke(options.ApiKeys);
            DapperMaticApiKeyFormat.AssertPrefix(options.ApiKeys.Prefix);
            if (!options.OwnSignInSchemes.Contains(DapperMaticApiKeyDefaults.Scheme))
            {
                options.OwnSignInSchemes.Insert(0, DapperMaticApiKeyDefaults.Scheme);
            }
        });

        if (!_services.Any(d => d.ServiceType == typeof(DapperMaticApiKeyService)))
        {
            _services.TryAddSingleton<IDapperMaticKeyRepository>(DapperMaticStores.Keys);
            _services.AddSingleton<DapperMaticApiKeyService>();
            _services.TryAddEnumerable(ServiceDescriptor.Singleton<IDapperMaticCallerCheck, DapperMaticApiKeyCheck>());
            _services.AddSingleton<DapperMaticKeyUseWorker>();
            _services.AddHostedService(sp => sp.GetRequiredService<DapperMaticKeyUseWorker>());
            _services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, DapperMaticApiKeyHandler>(DapperMaticApiKeyDefaults.Scheme, null);
            DapperMaticAuthenticationSetup.AddSignIn(_services);
        }

        return this;
    }

    /// <summary>
    /// Turns the built-in accounts on: people who sign in with an email and a password, on ASP.NET Core Identity, kept
    /// in DapperMatic's tables (in the datasource or database named, or where DapperMatic keeps access). Invitations by
    /// default; memberships of tenants, one active per sign-in; grants; bearer tokens or cookies.
    /// </summary>
    /// <param name="configure">Settings, or null for the defaults.</param>
    /// <returns>The builder.</returns>
    /// <exception cref="InvalidOperationException">The application uses ASP.NET Core Identity itself.</exception>
    public DapperMaticConfigurationBuilder AddUserAccounts(Action<Accounts.DapperMaticAccountOptions>? configure = null)
    {
        Accounts.DapperMaticAccountsSetup.Add(_services, configure);
        return this;
    }

    /// <summary>
    /// Keeps API keys in the application's own repository.
    /// </summary>
    /// <typeparam name="TRepository">The repository.</typeparam>
    /// <returns>The builder.</returns>
    public DapperMaticConfigurationBuilder UseKeyRepository<TRepository>()
        where TRepository : class, IDapperMaticKeyRepository
    {
        _services.AddSingleton<IDapperMaticKeyRepository, TRepository>();
        return this;
    }
}
