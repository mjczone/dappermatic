// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Auditing;
using MJCZone.DapperMatic.AspNetCore.Authentication;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Validation;

namespace MJCZone.DapperMatic.AspNetCore.Accounts;

/// <summary>Signs an email up: an email to it carries the token that makes the account, with its password.</summary>
public sealed class DapperMaticRegisterRequest
{
    /// <summary>Gets or sets the email.</summary>
    public string Email { get; set; } = string.Empty;
}

/// <summary>Signs in to a built-in account: an email and password (and a second factor when it has one), in a tenant or none.</summary>
public sealed class DapperMaticLoginRequest
{
    /// <summary>Gets or sets the email.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Gets or sets the password.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Gets or sets the code of an authenticator app, when the account has two-factor sign-in.</summary>
    public string? TwoFactorCode { get; set; }

    /// <summary>Gets or sets a recovery code, instead of an authenticator's.</summary>
    public string? TwoFactorRecoveryCode { get; set; }

    /// <summary>Gets or sets the tenant to sign in to, or null: the account's only tenant (with no platform roles), or the platform.</summary>
    public string? Tenant { get; set; }
}

/// <summary>Moves a signed-in account to another of its tenants, or to the platform (null).</summary>
public sealed class DapperMaticSwitchRequest
{
    /// <summary>Gets or sets the tenant, or null for the platform.</summary>
    public string? Tenant { get; set; }
}

/// <summary>Accepts an invitation: signed in (the invitation's email, confirmed), or with the new account's password.</summary>
public sealed class DapperMaticAcceptRequest
{
    /// <summary>Gets or sets the invitation's token.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Gets or sets the new account's password, when not signed in.</summary>
    public string? Password { get; set; }
}

/// <summary>Makes the first administrator, with the setup code from the log.</summary>
public sealed class DapperMaticSetupRequest
{
    /// <summary>Gets or sets the setup code.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the administrator's email.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Gets or sets their password.</summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>A built-in account as it sees itself: where it is signed in, with which roles, and its places.</summary>
/// <param name="Id">The account's id.</param>
/// <param name="Email">Its email.</param>
/// <param name="EmailConfirmed">Whether its email is confirmed.</param>
/// <param name="Tenant">The tenant it is signed in to, or null for the platform.</param>
/// <param name="Roles">Its roles there.</param>
/// <param name="PlatformRoles">Its platform roles.</param>
/// <param name="Memberships">Its live memberships and grants.</param>
#pragma warning disable SA1313 // Positional record parameters are properties.
public sealed record DapperMaticAccountInfo(
    string Id,
    string? Email,
    bool EmailConfirmed,
    string? Tenant,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> PlatformRoles,
    IReadOnlyList<DapperMaticMembershipInfo> Memberships
);

/// <summary>A membership as it is shown.</summary>
/// <param name="Id">The membership's id.</param>
/// <param name="UserId">The account's id.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="Roles">The roles there.</param>
/// <param name="IsGrant">Whether it is a platform user's grant.</param>
/// <param name="GrantedBy">Who made it.</param>
/// <param name="Reason">Why (a grant's).</param>
/// <param name="ExpiresAt">When it ends, or null.</param>
public sealed record DapperMaticMembershipInfo(string Id, string UserId, string Tenant, IReadOnlyList<string> Roles, bool IsGrant, string? GrantedBy, string? Reason, DateTimeOffset? ExpiresAt)
{
    /// <summary>
    /// Shows a membership.
    /// </summary>
    /// <param name="membership">The membership.</param>
    /// <returns>What is shown.</returns>
    public static DapperMaticMembershipInfo Of(DapperMaticMembership membership)
    {
        ArgumentNullException.ThrowIfNull(membership);
        return new(membership.Id, membership.UserId, membership.TenantId, DapperMaticAccountStore.RolesOf(membership.Roles), membership.IsGrant, membership.GrantedBy, membership.Reason, membership.ExpiresAt);
    }
}
#pragma warning restore SA1313

/// <summary>
/// The built-in accounts' sign-in flows: login (bearer tokens or a cookie), refresh, switching tenant, logout,
/// sign-up, invitations, setup, email confirmation, password reset and the account's own settings. Identity does the
/// security (passwords, lockout, two-factor sign-in, tokens); these keep DapperMatic's places and sessions, and never
/// say whether an email has an account.
/// </summary>
internal sealed class DapperMaticAccountFlows
{
    private readonly UserManager<DapperMaticUser> _users;
    private readonly SignInManager<DapperMaticUser> _signIn;
    private readonly DapperMaticAccountPrincipals _principals;
    private readonly DapperMaticAccountPlace _place;
    private readonly DapperMaticAccountStore _store;
    private readonly DapperMaticInvitations _invitations;
    private readonly DapperMaticAccountCheck _check;
    private readonly IDapperMaticEmailSender _emails;
    private readonly IOptionsMonitor<BearerTokenOptions> _bearer;
    private readonly IDapperMaticAuditLogger _audit;
    private readonly DapperMaticOptions _options;
    private readonly IServiceProvider _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticAccountFlows"/> class.
    /// </summary>
    /// <param name="users">Identity's user manager.</param>
    /// <param name="signIn">Identity's sign-in manager.</param>
    /// <param name="principals">The account principal factory.</param>
    /// <param name="place">The request's place.</param>
    /// <param name="store">The accounts' memberships, sessions and settings.</param>
    /// <param name="invitations">The invitations.</param>
    /// <param name="check">The account check, forgotten after a change.</param>
    /// <param name="emails">The email sender.</param>
    /// <param name="bearer">The bearer tokens' options.</param>
    /// <param name="audit">The audit logger.</param>
    /// <param name="options">The DapperMatic options.</param>
    /// <param name="services">The services: API keys', when they are on.</param>
    public DapperMaticAccountFlows(
        UserManager<DapperMaticUser> users,
        SignInManager<DapperMaticUser> signIn,
        DapperMaticAccountPrincipals principals,
        DapperMaticAccountPlace place,
        DapperMaticAccountStore store,
        DapperMaticInvitations invitations,
        DapperMaticAccountCheck check,
        IDapperMaticEmailSender emails,
        IOptionsMonitor<BearerTokenOptions> bearer,
        IDapperMaticAuditLogger audit,
        IOptions<DapperMaticOptions> options,
        IServiceProvider services
    )
    {
        _services = services;
        _users = users;
        _signIn = signIn;
        _principals = principals;
        _place = place;
        _store = store;
        _invitations = invitations;
        _check = check;
        _emails = emails;
        _bearer = bearer;
        _audit = audit;
        _options = options.Value;
    }

    private DapperMaticAccountOptions Accounts => _options.Accounts;

    /// <summary>
    /// Signs in: bearer tokens (written by Identity's bearer handler) or a cookie.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="request">The email, password, second factor and tenant.</param>
    /// <param name="useCookies">Whether to sign in with a cookie.</param>
    /// <param name="useSessionCookies">Whether the cookie ends with the browser session.</param>
    /// <returns>The result.</returns>
    public async Task<IResult> LoginAsync(DapperMaticOperation operation, DapperMaticLoginRequest request, bool useCookies, bool useSessionCookies)
    {
        _signIn.AuthenticationScheme = useCookies ? IdentityConstants.ApplicationScheme : IdentityConstants.BearerScheme;
        _place.Tenant = request.Tenant;
        _place.Chosen = request.Tenant is not null;
        _place.Session = Guid.NewGuid().ToString("N");
        var persistent = useCookies && !useSessionCookies;
        var result = await _signIn.PasswordSignInAsync(request.Email, request.Password, persistent, lockoutOnFailure: true).ConfigureAwait(false);
        if (result.RequiresTwoFactor)
        {
            if (!string.IsNullOrEmpty(request.TwoFactorCode))
            {
                result = await _signIn.TwoFactorAuthenticatorSignInAsync(request.TwoFactorCode, persistent, rememberClient: persistent).ConfigureAwait(false);
            }
            else if (!string.IsNullOrEmpty(request.TwoFactorRecoveryCode))
            {
                result = await _signIn.TwoFactorRecoveryCodeSignInAsync(request.TwoFactorRecoveryCode).ConfigureAwait(false);
            }
        }

        if (!result.Succeeded)
        {
            await AuditAsync(operation, false, $"Sign-in refused for {request.Email}: {result}").ConfigureAwait(false);
            return TypedResults.Problem(result.ToString(), statusCode: StatusCodes.Status401Unauthorized);
        }

        var user = await _users.FindByEmailAsync(request.Email).ConfigureAwait(false);
        await _store.StartSessionAsync(new DapperMaticUserSession { Id = _place.Session, UserId = user!.Id, Stamp = user.SecurityStamp }, CancellationToken.None).ConfigureAwait(false);
        await AuditAsync(operation, true, $"Signed in {request.Email}" + (request.Tenant is null ? string.Empty : $" to the tenant '{request.Tenant}'")).ConfigureAwait(false);
        return TypedResults.Empty;
    }

    /// <summary>
    /// Refreshes bearer tokens: the same sign-in session and tenant, the account and its membership checked again.
    /// </summary>
    /// <param name="request">The refresh token.</param>
    /// <returns>New tokens, or 401.</returns>
    public async Task<IResult> RefreshAsync(RefreshRequest request)
    {
        var ticket = _bearer.Get(IdentityConstants.BearerScheme).RefreshTokenProtector.Unprotect(request.RefreshToken);
        if (ticket?.Properties.ExpiresUtc is not { } expires || DateTimeOffset.UtcNow >= expires
            || await _signIn.ValidateSecurityStampAsync(ticket.Principal).ConfigureAwait(false) is not { Disabled: false } user)
        {
            return TypedResults.Challenge();
        }

        var account = ticket.Principal.Identities.FirstOrDefault(i => i.AuthenticationType == DapperMaticClaims.AccountAuthenticationType);
        var session = account?.FindFirst(DapperMaticClaims.Session)?.Value;
        if (session is null || await _store.SessionAsync(session, CancellationToken.None).ConfigureAwait(false) is not { RevokedAt: null } live || live.UserId != user.Id)
        {
            return TypedResults.Challenge();
        }

        ClaimsPrincipal principal;
        try
        {
            principal = await _principals.CreateAsync(user, account!.FindFirst(DapperMaticClaims.Tenant)?.Value, chosen: true, session, CancellationToken.None).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException)
        {
            return TypedResults.Challenge();
        }

        await _store.TouchSessionAsync(session, DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);
        return TypedResults.SignIn(principal, authenticationScheme: IdentityConstants.BearerScheme);
    }

    /// <summary>
    /// Moves the signed-in account to another of its tenants (or the platform): new bearer tokens, or a new cookie.
    /// </summary>
    /// <param name="http">The request.</param>
    /// <param name="request">The tenant.</param>
    /// <param name="useCookies">Whether the account signed in with a cookie.</param>
    /// <returns>The result.</returns>
    public async Task<IResult> SwitchAsync(HttpContext http, DapperMaticSwitchRequest request, bool useCookies)
    {
        var (user, session) = await SignedInAsync(http).ConfigureAwait(false);
        var principal = await _principals.CreateAsync(user, request.Tenant, chosen: true, session, http.RequestAborted).ConfigureAwait(false);
        if (useCookies)
        {
            await http.SignInAsync(IdentityConstants.ApplicationScheme, principal).ConfigureAwait(false);
            return TypedResults.Ok();
        }

        return TypedResults.SignIn(principal, authenticationScheme: IdentityConstants.BearerScheme);
    }

    /// <summary>
    /// Signs out: the sign-in session ends (its refresh tokens with it), the cookie is removed.
    /// </summary>
    /// <param name="http">The request.</param>
    /// <returns>The result.</returns>
    public async Task<IResult> LogoutAsync(HttpContext http)
    {
        var (user, session) = await SignedInAsync(http).ConfigureAwait(false);
        if (session is not null)
        {
            await _store.EndSessionsAsync(user.Id, session, DateTimeOffset.UtcNow, http.RequestAborted).ConfigureAwait(false);
        }

        await http.SignOutAsync(IdentityConstants.ApplicationScheme).ConfigureAwait(false);
        _check.Forget(user.Id);
        return TypedResults.Ok();
    }

    /// <summary>
    /// Ends every sign-in of the account: its security stamp changed, its sessions ended, its personal keys revoked.
    /// </summary>
    /// <param name="http">The request.</param>
    /// <returns>The result.</returns>
    public async Task<IResult> SignOutEverywhereAsync(HttpContext http)
    {
        var (user, _) = await SignedInAsync(http).ConfigureAwait(false);
        await _users.UpdateSecurityStampAsync(user).ConfigureAwait(false);
        await _store.EndSessionsAsync(user.Id, null, DateTimeOffset.UtcNow, http.RequestAborted).ConfigureAwait(false);
        await RevokePersonalKeysAsync(user.Id).ConfigureAwait(false);
        await http.SignOutAsync(IdentityConstants.ApplicationScheme).ConfigureAwait(false);
        _check.Forget(user.Id);
        return TypedResults.Ok();
    }

    /// <summary>
    /// Describes the signed-in account: where it is signed in, with which roles, and its places.
    /// </summary>
    /// <param name="http">The request.</param>
    /// <returns>The account.</returns>
    public async Task<IResult> MeAsync(HttpContext http)
    {
        var (user, _) = await SignedInAsync(http, allowKey: true).ConfigureAwait(false);
        var account = AccountOf(http.User)!;
        var now = DateTimeOffset.UtcNow;
        var memberships = (await _store.MembershipsAsync(user.Id, http.RequestAborted).ConfigureAwait(false)).Where(m => m.IsLive(now)).Select(DapperMaticMembershipInfo.Of).ToList();
        var platform = await _store.PlatformRolesAsync(user.Id, http.RequestAborted).ConfigureAwait(false);
        return TypedResults.Ok(
            new DapperMaticAccountInfo(
                user.Id,
                user.Email,
                user.EmailConfirmed,
                account.FindFirst(DapperMaticClaims.Tenant)?.Value,
                [.. account.FindAll(account.RoleClaimType).Select(c => c.Value)],
                platform,
                memberships
            )
        );
    }

    /// <summary>
    /// Signs an email up, when sign-up is open to it: an invitation to no tenant is sent to it, and accepting it (with a
    /// password) makes the account. So an account is never made with a password by someone who does not read the email.
    /// Never says whether the email has an account already.
    /// </summary>
    /// <param name="http">The request.</param>
    /// <param name="request">The email.</param>
    /// <returns>The result.</returns>
    public async Task<IResult> RegisterAsync(HttpContext http, DapperMaticRegisterRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Email);
        var signUp = Accounts.SignUp;
        var domain = request.Email.Split('@') is [_, var d] ? d : null;
        if ((!signUp.Open && signUp.EmailDomains.Count == 0)
            || (signUp.EmailDomains.Count > 0 && !signUp.EmailDomains.Contains(domain ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            || (signUp.Check is { } check && !await check(request.Email, http, http.RequestAborted).ConfigureAwait(false)))
        {
            return ErrorHandler.Forbidden("Accounts are made by invitation here.", http);
        }

        if (await _users.FindByEmailAsync(request.Email).ConfigureAwait(false) is null)
        {
            await _invitations.InviteAsync(request.Email, null, [], null, http.RequestAborted, DapperMaticEmailKind.SignUp).ConfigureAwait(false);
        }

        return TypedResults.Ok();
    }

    /// <summary>
    /// Accepts an invitation: by the signed-in account of its email (confirmed), or as a new account with the password.
    /// </summary>
    /// <param name="http">The request.</param>
    /// <param name="request">The token, and the password of a new account.</param>
    /// <returns>The result.</returns>
    public async Task<IResult> AcceptAsync(HttpContext http, DapperMaticAcceptRequest request) =>
        await _invitations.AcceptAsync(http, request).ConfigureAwait(false);

    /// <summary>
    /// Makes the first administrator with the setup code: while no account exists, once.
    /// </summary>
    /// <param name="request">The code, email and password.</param>
    /// <returns>The result.</returns>
    public async Task<IResult> SetupAsync(DapperMaticSetupRequest request)
    {
        var kept = await _store.SettingAsync(DapperMaticAccountsStartup.SetupCode, CancellationToken.None).ConfigureAwait(false);
        if (kept is null || !kept.StartsWith(DapperMaticAccountsStartup.Hash(request.Code) + ":", StringComparison.Ordinal) || await _store.AnyUserAsync(CancellationToken.None).ConfigureAwait(false))
        {
            return TypedResults.NotFound();
        }

        var user = new DapperMaticUser { UserName = request.Email, Email = request.Email, EmailConfirmed = true };
        if (await PasswordProblemAsync(user, request.Password).ConfigureAwait(false) is { } problem)
        {
            return problem;
        }

        // Whoever takes the code makes the administrator: a second request finds no code.
        if (!await _store.TakeSettingAsync(DapperMaticAccountsStartup.SetupCode, kept, CancellationToken.None).ConfigureAwait(false))
        {
            return TypedResults.NotFound();
        }

        var created = await _users.CreateAsync(user, request.Password).ConfigureAwait(false);
        if (!created.Succeeded)
        {
            await _store.AddSettingAsync(DapperMaticAccountsStartup.SetupCode, kept, CancellationToken.None).ConfigureAwait(false);
            return ValidationProblem(created);
        }

        await _store.SetPlatformRolesAsync(user.Id, (Accounts.FirstAdmin?.Roles is { Count: > 0 } roles) ? roles : ["admin"], CancellationToken.None).ConfigureAwait(false);
        return TypedResults.Ok();
    }

    /// <summary>
    /// Confirms an email (or a change of email), from its link.
    /// </summary>
    /// <param name="userId">The account's id.</param>
    /// <param name="code">The code.</param>
    /// <param name="changedEmail">The new email, for a change.</param>
    /// <returns>The result.</returns>
    public async Task<IResult> ConfirmEmailAsync(string userId, string code, string? changedEmail)
    {
        if (await _users.FindByIdAsync(userId).ConfigureAwait(false) is not { } user)
        {
            return TypedResults.Unauthorized();
        }

        IdentityResult result;
        try
        {
            code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            result = string.IsNullOrEmpty(changedEmail)
                ? await _users.ConfirmEmailAsync(user, code).ConfigureAwait(false)
                : await _users.ChangeEmailAsync(user, changedEmail, code).ConfigureAwait(false);
            if (result.Succeeded && !string.IsNullOrEmpty(changedEmail))
            {
                result = await _users.SetUserNameAsync(user, changedEmail).ConfigureAwait(false);
            }
        }
        catch (FormatException)
        {
            return TypedResults.Unauthorized();
        }

        return result.Succeeded ? TypedResults.Text("Thank you for confirming your email.") : TypedResults.Unauthorized();
    }

    /// <summary>
    /// Sends a confirmation again, to an account's unconfirmed email; never saying whether there is one.
    /// </summary>
    /// <param name="http">The request.</param>
    /// <param name="request">The email.</param>
    /// <returns>The result.</returns>
    public async Task<IResult> ResendConfirmationAsync(HttpContext http, ResendConfirmationEmailRequest request)
    {
        // Sent to the account's own address, never the request's: emails are found case-folded ("ı" is found as "i").
        if (await _users.FindByEmailAsync(request.Email).ConfigureAwait(false) is { EmailConfirmed: false, Email: { } email } user)
        {
            await SendConfirmationAsync(http, user, email).ConfigureAwait(false);
        }

        return TypedResults.Ok();
    }

    /// <summary>
    /// Sends a password reset code to an account's confirmed email; never saying whether there is one.
    /// </summary>
    /// <param name="request">The email.</param>
    /// <returns>The result.</returns>
    public async Task<IResult> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        // Sent to the account's own address, never the request's: emails are found case-folded, so "alice@gmaıl.com"
        // finds alice@gmail.com, and a code sent to the address as written would reach whoever owns that domain.
        if (await _users.FindByEmailAsync(request.Email).ConfigureAwait(false) is { EmailConfirmed: true, Email: { } email } user)
        {
            var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(await _users.GeneratePasswordResetTokenAsync(user).ConfigureAwait(false)));
            await _emails.SendAsync(new DapperMaticEmail(email, DapperMaticEmailKind.ResetPassword, Link(Accounts.Links.ResetPassword, ("email", email), ("code", code)), code), CancellationToken.None)
                .ConfigureAwait(false);
        }

        return TypedResults.Ok();
    }

    /// <summary>
    /// Resets a password with its code: every sign-in of the account ends, and its personal keys are revoked.
    /// </summary>
    /// <param name="request">The email, code and new password.</param>
    /// <returns>The result.</returns>
    public async Task<IResult> ResetPasswordAsync(ResetPasswordRequest request)
    {
        if (await _users.FindByEmailAsync(request.Email).ConfigureAwait(false) is not { EmailConfirmed: true } user)
        {
            return ValidationProblem(IdentityResult.Failed(_users.ErrorDescriber.InvalidToken()));
        }

        IdentityResult result;
        try
        {
            result = await _users.ResetPasswordAsync(user, Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.ResetCode)), request.NewPassword).ConfigureAwait(false);
        }
        catch (FormatException)
        {
            result = IdentityResult.Failed(_users.ErrorDescriber.InvalidToken());
        }

        if (!result.Succeeded)
        {
            return ValidationProblem(result);
        }

        await _store.EndSessionsAsync(user.Id, null, DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);
        await RevokePersonalKeysAsync(user.Id).ConfigureAwait(false);
        _check.Forget(user.Id);
        return TypedResults.Ok();
    }

    /// <summary>
    /// Reads the signed-in account's email.
    /// </summary>
    /// <param name="http">The request.</param>
    /// <returns>The email, and whether it is confirmed.</returns>
    public async Task<IResult> InfoAsync(HttpContext http)
    {
        var (user, _) = await SignedInAsync(http).ConfigureAwait(false);
        return TypedResults.Ok(new InfoResponse { Email = user.Email ?? string.Empty, IsEmailConfirmed = user.EmailConfirmed });
    }

    /// <summary>
    /// Changes the signed-in account's password (with the old one), or its email (confirmed by a link to the new one).
    /// </summary>
    /// <param name="http">The request.</param>
    /// <param name="request">The changes.</param>
    /// <returns>The result.</returns>
    public async Task<IResult> UpdateInfoAsync(HttpContext http, InfoRequest request)
    {
        var (user, session) = await SignedInAsync(http).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(request.NewPassword))
        {
            if (string.IsNullOrEmpty(request.OldPassword))
            {
                return ValidationProblem(IdentityResult.Failed(new IdentityError { Code = "OldPasswordRequired", Description = "The old password is required to set a new one." }));
            }

            // A wrong old password counts as a failed sign-in: a sign-in taken over cannot guess it without the lockout.
            if (!await _users.CheckPasswordAsync(user, request.OldPassword).ConfigureAwait(false))
            {
                await _users.AccessFailedAsync(user).ConfigureAwait(false);
                _check.Forget(user.Id);
                return ValidationProblem(IdentityResult.Failed(_users.ErrorDescriber.PasswordMismatch()));
            }

            var before = user.SecurityStamp;
            var changed = await _users.ChangePasswordAsync(user, request.OldPassword, request.NewPassword).ConfigureAwait(false);
            if (!changed.Succeeded)
            {
                return ValidationProblem(changed);
            }

            await KeepAsync(session, before, user).ConfigureAwait(false);
        }

        if (!string.IsNullOrEmpty(request.NewEmail) && !string.Equals(request.NewEmail, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(await _users.GenerateChangeEmailTokenAsync(user, request.NewEmail).ConfigureAwait(false)));
            await _emails.SendAsync(new DapperMaticEmail(request.NewEmail, DapperMaticEmailKind.ConfirmEmail, ConfirmLink(http, user.Id, code, request.NewEmail), code), CancellationToken.None).ConfigureAwait(false);
        }

        _check.Forget(user.Id);
        return TypedResults.Ok(new InfoResponse { Email = user.Email ?? string.Empty, IsEmailConfirmed = user.EmailConfirmed });
    }

    /// <summary>
    /// Sets up, turns on or off, or resets the signed-in account's two-factor sign-in, as Identity's endpoint does.
    /// </summary>
    /// <param name="http">The request.</param>
    /// <param name="request">What to change.</param>
    /// <returns>The account's two-factor state.</returns>
    public async Task<IResult> TwoFactorAsync(HttpContext http, TwoFactorRequest request)
    {
        var (user, session) = await SignedInAsync(http).ConfigureAwait(false);
        var before = user.SecurityStamp;
        if (request.Enable == true)
        {
            if (request.ResetSharedKey)
            {
                return ValidationProblem(IdentityResult.Failed(new IdentityError { Code = "CannotResetSharedKeyAndEnable", Description = "Resetting the shared key turns two-factor sign-in off until a code of the new key is given." }));
            }

            if (string.IsNullOrEmpty(request.TwoFactorCode) || !await _users.VerifyTwoFactorTokenAsync(user, _users.Options.Tokens.AuthenticatorTokenProvider, request.TwoFactorCode).ConfigureAwait(false))
            {
                return ValidationProblem(IdentityResult.Failed(new IdentityError { Code = "InvalidTwoFactorCode", Description = "A valid code of the authenticator is needed to turn two-factor sign-in on." }));
            }

            await _users.SetTwoFactorEnabledAsync(user, true).ConfigureAwait(false);
        }
        else if (request.Enable == false || request.ResetSharedKey)
        {
            await _users.SetTwoFactorEnabledAsync(user, false).ConfigureAwait(false);
        }

        if (request.ResetSharedKey)
        {
            await _users.ResetAuthenticatorKeyAsync(user).ConfigureAwait(false);
        }

        string[]? recoveryCodes = null;
        if (request.ResetRecoveryCodes || (request.Enable == true && await _users.CountRecoveryCodesAsync(user).ConfigureAwait(false) == 0))
        {
            recoveryCodes = (await _users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10).ConfigureAwait(false))?.ToArray();
        }

        if (request.ForgetMachine)
        {
            await _signIn.ForgetTwoFactorClientAsync().ConfigureAwait(false);
        }

        // An account's first key replaces none, so it ends no sign-in (a reset changes the stamp).
        var key = await _users.GetAuthenticatorKeyAsync(user).ConfigureAwait(false);
        if (string.IsNullOrEmpty(key))
        {
            user.AuthenticatorKey = key = _users.GenerateNewAuthenticatorKey();
            var updated = await _users.UpdateAsync(user).ConfigureAwait(false);
            if (!updated.Succeeded)
            {
                return ValidationProblem(updated);
            }
        }

        await KeepAsync(session, before, user).ConfigureAwait(false);

        return TypedResults.Ok(
            new TwoFactorResponse
            {
                SharedKey = key!,
                RecoveryCodes = recoveryCodes,
                RecoveryCodesLeft = recoveryCodes?.Length ?? await _users.CountRecoveryCodesAsync(user).ConfigureAwait(false),
                IsTwoFactorEnabled = await _users.GetTwoFactorEnabledAsync(user).ConfigureAwait(false),
                IsMachineRemembered = await _signIn.IsTwoFactorClientRememberedAsync(user).ConfigureAwait(false),
            }
        );
    }

    /// <summary>
    /// Sends an account's email confirmation.
    /// </summary>
    /// <param name="http">The request (for a link to DapperMatic's endpoint).</param>
    /// <param name="user">The account.</param>
    /// <param name="email">The email.</param>
    /// <returns>A task.</returns>
    internal async Task SendConfirmationAsync(HttpContext http, DapperMaticUser user, string email)
    {
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(await _users.GenerateEmailConfirmationTokenAsync(user).ConfigureAwait(false)));
        await _emails.SendAsync(new DapperMaticEmail(email, DapperMaticEmailKind.ConfirmEmail, ConfirmLink(http, user.Id, code, null), code), CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Fills a link template, its values URL-encoded.
    /// </summary>
    /// <param name="template">The template, or null for none.</param>
    /// <param name="values">The values.</param>
    /// <returns>The link, or null.</returns>
    internal static string? Link(string? template, params (string Name, string Value)[] values)
    {
        if (template is null)
        {
            return null;
        }

        foreach (var (name, value) in values)
        {
            template = template.Replace("{" + name + "}", Uri.EscapeDataString(value), StringComparison.Ordinal);
        }

        return template;
    }

    private static ClaimsIdentity? AccountOf(ClaimsPrincipal user) =>
        user.Identities.FirstOrDefault(i => i.AuthenticationType == DapperMaticClaims.AccountAuthenticationType);

    private static ValidationProblem ValidationProblem(IdentityResult result) =>
        TypedResults.ValidationProblem(result.Errors.GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));

    // The account's personal keys, revoked: its sign-ins ended because it may have been taken over, and a key made then
    // would outlast them.
    private async Task RevokePersonalKeysAsync(string userId)
    {
        if (_services.GetService<DapperMaticApiKeyService>() is not { } keys)
        {
            return;
        }

        foreach (var key in await keys.Repository.ListAsync(new DapperMaticKeyFilter { Kind = DapperMaticKeyKind.Personal, Owner = userId }, CancellationToken.None).ConfigureAwait(false))
        {
            if (key.RevokedAt is null)
            {
                await keys.Repository.RevokeAsync(key.Id, DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);
                keys.Forget(key.Id);
            }
        }
    }

    // After the caller changed their own account's security stamp (a password, a second factor): their own sign-in
    // goes on, every other one ends.
    private async Task KeepAsync(string? session, string? before, DapperMaticUser user)
    {
        if (session is not null && before != user.SecurityStamp)
        {
            await _store.KeepSessionAsync(session, before, user.SecurityStamp, CancellationToken.None).ConfigureAwait(false);
        }

        _check.Forget(user.Id);
    }

    // The signed-in account (of a token or cookie, not a key unless allowed) and its sign-in session.
    private async Task<(DapperMaticUser User, string? Session)> SignedInAsync(HttpContext http, bool allowKey = false)
    {
        if (!allowKey && DapperMaticClaims.ByKey(http.User))
        {
            throw new UnauthorizedAccessException("A caller signed in with an API key may not change its owner's account.");
        }

        var account = AccountOf(http.User) ?? throw new UnauthorizedAccessException("This is for a built-in account: sign in to one.");
        var id = account.FindFirst(_users.Options.ClaimsIdentity.UserIdClaimType)?.Value;
        var user = id is null ? null : await _users.FindByIdAsync(id).ConfigureAwait(false);
        return (user ?? throw new UnauthorizedAccessException("This account no longer exists."), account.FindFirst(DapperMaticClaims.Session)?.Value);
    }

    // A password Identity's rules refuse, as a validation problem; null when it is good.
    private async Task<IResult?> PasswordProblemAsync(DapperMaticUser user, string password)
    {
        var errors = new List<IdentityError>();
        foreach (var validator in _users.PasswordValidators)
        {
            var result = await validator.ValidateAsync(_users, user, password).ConfigureAwait(false);
            errors.AddRange(result.Errors);
        }

        return errors.Count == 0 ? null : ValidationProblem(IdentityResult.Failed([.. errors]));
    }

    private string ConfirmLink(HttpContext http, string userId, string code, string? changedEmail)
    {
        if (Link(Accounts.Links.ConfirmEmail, ("userId", userId), ("code", code), ("email", changedEmail ?? string.Empty)) is { } link)
        {
            return link + (changedEmail is null ? string.Empty : $"&changedEmail={Uri.EscapeDataString(changedEmail)}");
        }

        var basePath = "/" + (_options.BasePath ?? "/api/dm").Trim('/');
        var query = $"?userId={Uri.EscapeDataString(userId)}&code={Uri.EscapeDataString(code)}" + (changedEmail is null ? string.Empty : $"&changedEmail={Uri.EscapeDataString(changedEmail)}");
        return $"{http.Request.Scheme}://{http.Request.Host}{http.Request.PathBase}{basePath}/auth/confirm-email{query}";
    }

    private Task AuditAsync(DapperMaticOperation operation, bool success, string message) =>
        _audit.LogOperationAsync(operation.ToAuditEvent(success, message, _options.AuditClaims));
}

/// <summary>
/// Invitations: made (a token shown once, sent to the email), and accepted once, before they expire.
/// </summary>
internal sealed class DapperMaticInvitations(
    UserManager<DapperMaticUser> users,
    DapperMaticAccountStore store,
    DapperMaticAccountCheck check,
    IDapperMaticEmailSender emails,
    IOptions<DapperMaticOptions> options
)
{
    /// <summary>
    /// Invites an email: to a tenant with roles there, or to the platform with platform roles.
    /// </summary>
    /// <param name="email">The email.</param>
    /// <param name="tenant">The tenant, or null for the platform.</param>
    /// <param name="roles">The roles.</param>
    /// <param name="invitedBy">Who invites.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The invitation, and its token (shown once).</returns>
    /// <param name="kind">The email's kind: an invitation, or a sign-up's.</param>
    public async Task<(DapperMaticInvitation Invitation, string Token)> InviteAsync(
        string email,
        string? tenant,
        IEnumerable<string> roles,
        string? invitedBy,
        CancellationToken cancellationToken,
        DapperMaticEmailKind kind = DapperMaticEmailKind.Invitation
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var invitation = new DapperMaticInvitation
        {
            Email = email,
            NormalizedEmail = users.NormalizeEmail(email),
            TenantId = tenant,
            Roles = DapperMaticAccountStore.RolesText(roles),
            InvitedBy = invitedBy,
            TokenHash = DapperMaticAccountsStartup.Hash(token),
            ExpiresAt = DateTimeOffset.UtcNow + options.Value.Accounts.InvitationLifetime,
        };
        await store.SaveInvitationAsync(invitation, cancellationToken).ConfigureAwait(false);
        var links = options.Value.Accounts.Links;
        await emails.SendAsync(new DapperMaticEmail(email, kind, DapperMaticAccountFlows.Link(kind == DapperMaticEmailKind.SignUp ? links.SignUp ?? links.Invitation : links.Invitation, ("token", token)), token), cancellationToken)
            .ConfigureAwait(false);
        return (invitation, token);
    }

    /// <summary>
    /// Accepts an invitation.
    /// </summary>
    /// <param name="http">The request.</param>
    /// <param name="request">The token, and a new account's password.</param>
    /// <returns>The result.</returns>
    public async Task<IResult> AcceptAsync(HttpContext http, DapperMaticAcceptRequest request)
    {
        var now = DateTimeOffset.UtcNow;
        if (await store.InvitationAsync(DapperMaticAccountsStartup.Hash(request.Token), http.RequestAborted).ConfigureAwait(false) is not { AcceptedAt: null } invitation || invitation.ExpiresAt <= now)
        {
            return TypedResults.NotFound();
        }

        DapperMaticUser user;
        var account = http.User.Identities.FirstOrDefault(i => i.AuthenticationType == DapperMaticClaims.AccountAuthenticationType);
        if (account is not null && !DapperMaticClaims.ByKey(http.User))
        {
            // The signed-in account of the invitation's email, confirmed.
            var id = account.FindFirst(users.Options.ClaimsIdentity.UserIdClaimType)?.Value;
            if (id is null || await users.FindByIdAsync(id).ConfigureAwait(false) is not { } signedIn)
            {
                return TypedResults.Unauthorized();
            }

            if (!signedIn.EmailConfirmed || signedIn.NormalizedEmail != invitation.NormalizedEmail)
            {
                throw new UnauthorizedAccessException("This invitation is for another email, or this account's email is not confirmed.");
            }

            if (!await store.ClaimInvitationAsync(invitation.Id, now, http.RequestAborted).ConfigureAwait(false))
            {
                return TypedResults.NotFound();
            }

            user = signedIn;
        }
        else
        {
            if (await users.FindByEmailAsync(invitation.Email).ConfigureAwait(false) is not null)
            {
                throw new UnauthorizedAccessException("This email has an account: sign in to accept the invitation.");
            }

            var created = new DapperMaticUser { UserName = invitation.Email, Email = invitation.Email, EmailConfirmed = true };
            if (string.IsNullOrEmpty(request.Password))
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["PasswordRequired"] = ["A new account needs a password."] });
            }

            if (!await store.ClaimInvitationAsync(invitation.Id, now, http.RequestAborted).ConfigureAwait(false))
            {
                return TypedResults.NotFound();
            }

            var result = await users.CreateAsync(created, request.Password).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                await store.UnclaimInvitationAsync(invitation.Id, CancellationToken.None).ConfigureAwait(false);
                return TypedResults.ValidationProblem(result.Errors.GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));
            }

            user = created;
        }

        var roles = DapperMaticAccountStore.RolesOf(invitation.Roles);
        if (invitation.TenantId is { } tenant)
        {
            await store.SaveMembershipAsync(new DapperMaticMembership { UserId = user.Id, TenantId = tenant, Roles = DapperMaticAccountStore.RolesText(roles), GrantedBy = invitation.InvitedBy }, CancellationToken.None)
                .ConfigureAwait(false);
        }
        else
        {
            var platform = await store.PlatformRolesAsync(user.Id, CancellationToken.None).ConfigureAwait(false);
            await store.SetPlatformRolesAsync(user.Id, platform.Union(roles, StringComparer.Ordinal), CancellationToken.None).ConfigureAwait(false);
        }

        check.Forget(user.Id);
        return TypedResults.Ok();
    }
}
