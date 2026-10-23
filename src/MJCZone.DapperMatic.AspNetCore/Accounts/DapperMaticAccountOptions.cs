// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace MJCZone.DapperMatic.AspNetCore.Accounts;

/// <summary>
/// Built-in accounts: people who sign in to DapperMatic with an email and a password, on ASP.NET Core Identity, kept
/// in DapperMatic's tables. Off until <c>dm.AddUserAccounts()</c>.
/// </summary>
public sealed class DapperMaticAccountOptions
{
    /// <summary>Gets or sets the datasource the account tables are in, or null for <see cref="Provider"/> and <see cref="ConnectionString"/>.</summary>
    public string? Datasource { get; set; }

    /// <summary>Gets or sets the database provider of the account tables, when no datasource is named.</summary>
    public string? Provider { get; set; }

    /// <summary>Gets or sets the connection string of the account tables, when no datasource is named.</summary>
    public string? ConnectionString { get; set; }

    /// <summary>Gets or sets the schema of the account tables, or null for the database's default.</summary>
    public string? Schema { get; set; }

    /// <summary>Gets who may make an account themselves. Nobody, by default: people are invited.</summary>
    public DapperMaticSignUpOptions SignUp { get; } = new();

    /// <summary>
    /// Gets or sets the first administrator: invited at startup, when no such account exists yet. Without it (here
    /// or in the <c>DapperMatic:Accounts:FirstAdmin</c> configuration section), a one-time setup code is written to
    /// the log while no account exists.
    /// </summary>
    public DapperMaticFirstAdmin? FirstAdmin { get; set; }

    /// <summary>Gets or sets the longest a grant may last (a platform user's membership of a tenant). Default is 8 hours.</summary>
    public TimeSpan MaxGrantDuration { get; set; } = TimeSpan.FromHours(8);

    /// <summary>Gets or sets how long an invitation stays good. Default is 7 days.</summary>
    public TimeSpan InvitationLifetime { get; set; } = TimeSpan.FromDays(7);

    /// <summary>Gets where emails send people: the application's own pages, or DapperMatic's endpoints when not set.</summary>
    public DapperMaticAccountLinks Links { get; } = new();

    /// <summary>Gets or sets changes to Identity's options: password rules, lockout, ….</summary>
    public Action<IdentityOptions>? Identity { get; set; }

    /// <summary>Gets or sets changes to the bearer tokens' options: lifetimes.</summary>
    public Action<BearerTokenOptions>? BearerToken { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether DapperMatic keeps the application's Data Protection keys (which sign
    /// the accounts' tokens and cookies, and the application's own) in its database, encrypted with the connection
    /// string encryption key, so that every instance shares them. Default is true; false when the application keeps
    /// them itself.
    /// </summary>
    public bool KeepDataProtectionKeys { get; set; } = true;

    /// <summary>
    /// Gets or sets the settings of JWT access tokens, or null (the default) for Identity's own tokens, which only
    /// DapperMatic reads. See <see cref="UseJwt"/>.
    /// </summary>
    public DapperMaticJwtOptions? Jwt { get; set; }

    /// <summary>Gets or sets a value indicating whether accounts are turned on.</summary>
    internal bool Enabled { get; set; }

    /// <summary>
    /// Names the datasource the account tables are in.
    /// </summary>
    /// <param name="datasourceId">The datasource.</param>
    /// <param name="schema">The schema, or null for the database's default.</param>
    public void UseDatasource(string datasourceId, string? schema = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(datasourceId);
        (Datasource, Schema) = (datasourceId, schema);
    }

    /// <summary>
    /// Issues access tokens as JWTs (RS256 or ES256) that other services check with the published keys
    /// (<c>auth/jwks.json</c>); refresh tokens stay Identity's own.
    /// </summary>
    /// <param name="configure">The issuer, audience and keys.</param>
    public void UseJwt(Action<DapperMaticJwtOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        Jwt ??= new DapperMaticJwtOptions();
        configure(Jwt);
    }

    /// <summary>
    /// Names the database the account tables are in.
    /// </summary>
    /// <param name="provider">The database provider.</param>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="schema">The schema, or null for the database's default.</param>
    public void UseDatabase(string provider, string connectionString, string? schema = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        (Provider, ConnectionString, Schema) = (provider, connectionString, schema);
    }
}

/// <summary>Who may make an account themselves.</summary>
public sealed class DapperMaticSignUpOptions
{
    /// <summary>Gets or sets a value indicating whether anyone may (whose email's domain is allowed). Default is false.</summary>
    public bool Open { get; set; }

    /// <summary>Gets the only email domains that may sign up, or none for any (when open).</summary>
    public List<string> EmailDomains { get; } = [];

    /// <summary>Gets or sets a check of each sign-up: the email and the request in, whether it may out.</summary>
    public Func<string, HttpContext, CancellationToken, ValueTask<bool>>? Check { get; set; }
}

/// <summary>The first administrator.</summary>
public sealed class DapperMaticFirstAdmin
{
    /// <summary>Gets or sets the administrator's email.</summary>
    public string? Email { get; set; }

    /// <summary>Gets the platform roles they get. Default is <c>admin</c>; policies decide what it may do.</summary>
    public List<string> Roles { get; init; } = ["admin"];
}

/// <summary>
/// Where emails send people. Each is a template: <c>{token}</c>, <c>{userId}</c>, <c>{code}</c> and <c>{email}</c>
/// are replaced (URL-encoded).
/// </summary>
public sealed class DapperMaticAccountLinks
{
    /// <summary>Gets or sets an invitation's link: <c>https://app.example.com/join?token={token}</c>.</summary>
    public string? Invitation { get; set; }

    /// <summary>Gets or sets an email confirmation's link: <c>https://app.example.com/confirm?user={userId}&amp;code={code}</c>.</summary>
    public string? ConfirmEmail { get; set; }

    /// <summary>Gets or sets a password reset's link: <c>https://app.example.com/reset?email={email}&amp;code={code}</c>.</summary>
    public string? ResetPassword { get; set; }

    /// <summary>Gets or sets a sign-up's link, or null for the invitation's: <c>https://app.example.com/welcome?token={token}</c>.</summary>
    public string? SignUp { get; set; }
}
