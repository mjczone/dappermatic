// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Accounts;
using MJCZone.DapperMatic.AspNetCore.Extensions;
using MJCZone.DapperMatic.AspNetCore.Models.Responses;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.AspNetCore.Services;

namespace MJCZone.DapperMatic.AspNetCore.Endpoints;

/// <summary>
/// The built-in accounts' endpoints: signing in and one's own account (<c>/auth</c>); accounts, memberships, grants and
/// invitations for those who manage access (<c>/access</c>).
/// </summary>
internal static class AccountEndpoints
{
    /// <summary>
    /// Maps the sign-in and own-account endpoints.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <param name="basePath">The base path for the API endpoints.</param>
    /// <returns>The route builder for method chaining.</returns>
    public static IEndpointRouteBuilder MapDapperMaticAccountEndpoints(this IEndpointRouteBuilder app, string? basePath = null)
    {
        var group = app.MapDapperMaticEndpointGroup(basePath, "/auth", OperationTags.Auth);
        static void Name(RouteHandlerBuilder route, string operation, string name, string summary) =>
            route.WithMetadata(new DapperMaticOperationMetadata("auth/" + operation)).WithName(name).WithSummary(summary);

        Name(
            group.MapPost(
                "/login",
                (DapperMaticOperation operation, DapperMaticAccountFlows flows, [FromBody] DapperMaticLoginRequest request, [FromQuery] bool? useCookies, [FromQuery] bool? useSessionCookies) =>
                    flows.LoginAsync(operation, request, useCookies == true, useSessionCookies == true)
            ).AllowAnonymous(),
            "login",
            "AccountLogin",
            "Signs in to a built-in account, in a tenant or none: bearer tokens, or a cookie"
        );
        Name(group.MapPost("/refresh", (DapperMaticAccountFlows flows, [FromBody] RefreshRequest request) => flows.RefreshAsync(request)).AllowAnonymous(), "refresh", "AccountRefresh", "New bearer tokens for a sign-in, in its tenant");
        Name(
            group.MapPost("/switch", (HttpContext http, DapperMaticAccountFlows flows, [FromBody] DapperMaticSwitchRequest request, [FromQuery] bool? useCookies) => flows.SwitchAsync(http, request, useCookies == true)),
            "switch",
            "AccountSwitch",
            "Moves the signed-in account to another of its tenants, or the platform"
        );
        Name(group.MapPost("/logout", (HttpContext http, DapperMaticAccountFlows flows) => flows.LogoutAsync(http)), "logout", "AccountLogout", "Signs out: the sign-in and its refresh tokens end");
        Name(group.MapGet("/me", (HttpContext http, DapperMaticAccountFlows flows) => flows.MeAsync(http)), "me", "AccountMe", "The signed-in account: its tenant, roles and memberships");
        Name(group.MapPost("/register", (HttpContext http, DapperMaticAccountFlows flows, [FromBody] DapperMaticRegisterRequest request) => flows.RegisterAsync(http, request)).AllowAnonymous(), "register", "AccountRegister", "Signs an email up, where sign-up is open: an email to it makes the account");
        Name(
            group.MapPost("/invitations/accept", (HttpContext http, DapperMaticAccountFlows flows, [FromBody] DapperMaticAcceptRequest request) => flows.AcceptAsync(http, request)).AllowAnonymous(),
            "invitations-accept",
            "AccountAcceptInvitation",
            "Accepts an invitation: signed in, or as a new account"
        );
        Name(group.MapPost("/setup", (DapperMaticAccountFlows flows, [FromBody] DapperMaticSetupRequest request) => flows.SetupAsync(request)).AllowAnonymous(), "setup", "AccountSetup", "Makes the first administrator, with the setup code from the log");
        Name(
            group.MapGet("/confirm-email", (DapperMaticAccountFlows flows, [FromQuery] string userId, [FromQuery] string code, [FromQuery] string? changedEmail) => flows.ConfirmEmailAsync(userId, code, changedEmail)).AllowAnonymous(),
            "confirm-email",
            "AccountConfirmEmail",
            "Confirms an email, from its link"
        );
        Name(
            group.MapPost("/resend-confirmation", (HttpContext http, DapperMaticAccountFlows flows, [FromBody] ResendConfirmationEmailRequest request) => flows.ResendConfirmationAsync(http, request)).AllowAnonymous(),
            "resend-confirmation",
            "AccountResendConfirmation",
            "Sends an email's confirmation again"
        );
        Name(group.MapPost("/forgot-password", (DapperMaticAccountFlows flows, [FromBody] ForgotPasswordRequest request) => flows.ForgotPasswordAsync(request)).AllowAnonymous(), "forgot-password", "AccountForgotPassword", "Sends a password reset code");
        Name(group.MapPost("/reset-password", (DapperMaticAccountFlows flows, [FromBody] ResetPasswordRequest request) => flows.ResetPasswordAsync(request)).AllowAnonymous(), "reset-password", "AccountResetPassword", "Resets a password with its code");
        Name(group.MapGet("/manage/info", (HttpContext http, DapperMaticAccountFlows flows) => flows.InfoAsync(http)), "manage-info", "AccountInfo", "The signed-in account's email");
        Name(group.MapPost("/manage/info", (HttpContext http, DapperMaticAccountFlows flows, [FromBody] InfoRequest request) => flows.UpdateInfoAsync(http, request)), "manage-info-update", "AccountUpdateInfo", "Changes the signed-in account's password or email");
        Name(group.MapPost("/manage/2fa", (HttpContext http, DapperMaticAccountFlows flows, [FromBody] TwoFactorRequest request) => flows.TwoFactorAsync(http, request)), "manage-2fa", "AccountTwoFactor", "Sets up, turns on or off, or resets two-factor sign-in");
        Name(group.MapPost("/manage/sign-out-everywhere", (HttpContext http, DapperMaticAccountFlows flows) => flows.SignOutEverywhereAsync(http)), "manage-sign-out-everywhere", "AccountSignOutEverywhere", "Ends every sign-in of the account");

        // JWTs' keys, for other services: as a key set, and in a discovery document naming the issuer. The document says
        // where the keys are from the issuer alone (an absolute URL, this group's own), never from the request's Host,
        // which a client chooses and a shared cache would keep.
        var jwtOptions = app.ServiceProvider.GetRequiredService<IOptions<DapperMaticOptions>>().Value.Accounts.Jwt;
        if (jwtOptions is not null)
        {
            Name(group.MapGet("/jwks.json", (HttpContext http, DapperMaticJwt jwt) => Published(http, jwt.Jwks())).AllowAnonymous(), "jwks", "AccountJwks", "The public keys of the accounts' JWTs, as a JSON Web Key Set");
        }

        if (jwtOptions is not null && Uri.TryCreate(jwtOptions.Issuer, UriKind.Absolute, out var issuer) && issuer.Scheme is "https" or "http")
        {
            var keysAt = jwtOptions.Issuer!.TrimEnd('/') + "/jwks.json";
            Name(
                group.MapGet(
                    "/.well-known/openid-configuration",
                    (HttpContext http, DapperMaticJwt jwt) =>
                        Published(
                            http,
                            new JsonObject
                            {
                                ["issuer"] = jwtOptions.Issuer,
                                ["jwks_uri"] = keysAt,
                                ["response_types_supported"] = new JsonArray("token"),
                                ["subject_types_supported"] = new JsonArray("public"),
                                ["id_token_signing_alg_values_supported"] = new JsonArray([.. (jwt.Jwks()?["keys"]?.AsArray() ?? []).Select(k => k?["alg"]?.GetValue<string>()).Distinct().Select(a => (JsonNode?)a)]),
                            }
                        )
                ).AllowAnonymous(),
                "openid-configuration",
                "AccountOpenIdConfiguration",
                "Where the accounts' JWTs are checked: their issuer and keys"
            );
        }

        return app;
    }

    /// <summary>
    /// Maps the administration of accounts, memberships, grants and invitations.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <param name="basePath">The base path for the API endpoints.</param>
    /// <returns>The route builder for method chaining.</returns>
    public static IEndpointRouteBuilder MapDapperMaticAccountAdminEndpoints(this IEndpointRouteBuilder app, string? basePath = null)
    {
        var group = app.MapDapperMaticEndpointGroup(basePath, "/access", OperationTags.Access);

        group.MapGet(
                "/accounts",
                async (DapperMaticOperation context, DapperMaticAccessManager manager, [FromQuery] string? tenant, [FromQuery] string? after, [FromQuery] int? take, CancellationToken cancellationToken) =>
                    Results.Ok(new AccessResponse<IReadOnlyList<DapperMaticAccountSummary>>(await manager.GetAccountsAsync(context, tenant, after, take ?? 100, cancellationToken).ConfigureAwait(false)))
            )
            .WithName("ListAccounts")
            .WithSummary("Lists built-in accounts: every one, or a tenant's members")
            .Produces<AccessResponse<IReadOnlyList<DapperMaticAccountSummary>>>((int)HttpStatusCode.OK);

        group.MapPut(
                "/accounts/{userId}/roles",
                async (DapperMaticOperation context, DapperMaticAccessManager manager, [FromRoute] string userId, [FromBody] DapperMaticMembershipRequest request, CancellationToken cancellationToken) =>
                {
                    await manager.SetPlatformRolesAsync(context, userId, request.Roles, cancellationToken).ConfigureAwait(false);
                    return Results.NoContent();
                }
            )
            .WithName("SetAccountPlatformRoles")
            .WithSummary("Sets an account's platform roles")
            .Produces((int)HttpStatusCode.NoContent);

        foreach (var (verb, off) in new[] { ("disable", true), ("enable", false) })
        {
            group.MapPost(
                    $"/accounts/{{userId}}/{verb}",
                    async (DapperMaticOperation context, DapperMaticAccessManager manager, [FromRoute] string userId, CancellationToken cancellationToken) =>
                    {
                        await manager.SetDisabledAsync(context, userId, off, cancellationToken).ConfigureAwait(false);
                        return Results.NoContent();
                    }
                )
                .WithName(off ? "DisableAccount" : "EnableAccount")
                .WithSummary(off ? "Turns an account off: its sign-ins, sessions and keys end" : "Turns an account back on")
                .Produces((int)HttpStatusCode.NoContent);
        }

        group.MapPut(
                "/memberships/{tenantId}/{userId}",
                async (DapperMaticOperation context, DapperMaticAccessManager manager, [FromRoute] string tenantId, [FromRoute] string userId, [FromBody] DapperMaticMembershipRequest request, CancellationToken cancellationToken) =>
                    Results.Ok(new AccessResponse<DapperMaticMembershipInfo>(await manager.SaveMembershipAsync(context, tenantId, userId, request, cancellationToken).ConfigureAwait(false)))
            )
            .WithName("SaveMembership")
            .WithSummary("Gives an account a membership of a tenant, or changes its roles there")
            .Produces<AccessResponse<DapperMaticMembershipInfo>>((int)HttpStatusCode.OK);

        group.MapDelete(
                "/memberships/{tenantId}/{userId}",
                async (DapperMaticOperation context, DapperMaticAccessManager manager, [FromRoute] string tenantId, [FromRoute] string userId, CancellationToken cancellationToken) =>
                {
                    await manager.DeleteMembershipAsync(context, tenantId, userId, cancellationToken).ConfigureAwait(false);
                    return Results.NoContent();
                }
            )
            .WithName("DeleteMembership")
            .WithSummary("Removes an account's membership of a tenant")
            .Produces((int)HttpStatusCode.NoContent);

        group.MapGet(
                "/grants",
                async (DapperMaticOperation context, DapperMaticAccessManager manager, [FromQuery] string? tenant, CancellationToken cancellationToken) =>
                    Results.Ok(new AccessResponse<IReadOnlyList<DapperMaticMembershipInfo>>(await manager.GetGrantsAsync(context, tenant, cancellationToken).ConfigureAwait(false)))
            )
            .WithName("ListGrants")
            .WithSummary("Lists grants: platform users' memberships of tenants, for a while")
            .Produces<AccessResponse<IReadOnlyList<DapperMaticMembershipInfo>>>((int)HttpStatusCode.OK);

        group.MapPost(
                "/grants",
                async (DapperMaticOperation context, DapperMaticAccessManager manager, [FromBody] DapperMaticGrantRequest request, CancellationToken cancellationToken) =>
                    Results.Json(new AccessResponse<DapperMaticMembershipInfo>(await manager.GrantAsync(context, request, cancellationToken).ConfigureAwait(false)), statusCode: (int)HttpStatusCode.Created)
            )
            .WithName("CreateGrant")
            .WithSummary("Grants a platform user a tenant's roles until a time, for a reason")
            .Produces<AccessResponse<DapperMaticMembershipInfo>>((int)HttpStatusCode.Created);

        group.MapDelete(
                "/grants/{grantId}",
                async (DapperMaticOperation context, DapperMaticAccessManager manager, [FromRoute] string grantId, CancellationToken cancellationToken) =>
                {
                    await manager.RevokeGrantAsync(context, grantId, cancellationToken).ConfigureAwait(false);
                    return Results.NoContent();
                }
            )
            .WithName("RevokeGrant")
            .WithSummary("Revokes a grant")
            .Produces((int)HttpStatusCode.NoContent);

        group.MapGet(
                "/invitations",
                async (DapperMaticOperation context, DapperMaticAccessManager manager, [FromQuery] string? tenant, CancellationToken cancellationToken) =>
                    Results.Ok(new AccessResponse<IReadOnlyList<DapperMaticInvitationInfo>>(await manager.GetInvitationsAsync(context, tenant, cancellationToken).ConfigureAwait(false)))
            )
            .WithName("ListInvitations")
            .WithSummary("Lists invitations not yet accepted")
            .Produces<AccessResponse<IReadOnlyList<DapperMaticInvitationInfo>>>((int)HttpStatusCode.OK);

        group.MapPost(
                "/invitations",
                async (DapperMaticOperation context, DapperMaticAccessManager manager, [FromBody] DapperMaticInvitationRequest request, CancellationToken cancellationToken) =>
                    Results.Json(new AccessResponse<DapperMaticInvitationCreated>(await manager.InviteAsync(context, request, cancellationToken).ConfigureAwait(false)), statusCode: (int)HttpStatusCode.Created)
            )
            .WithName("CreateInvitation")
            .WithSummary("Invites an email to a tenant, or to the platform; the token is shown once and sent")
            .Produces<AccessResponse<DapperMaticInvitationCreated>>((int)HttpStatusCode.Created);

        group.MapDelete(
                "/invitations/{invitationId}",
                async (DapperMaticOperation context, DapperMaticAccessManager manager, [FromRoute] string invitationId, CancellationToken cancellationToken) =>
                {
                    await manager.DeleteInvitationAsync(context, invitationId, cancellationToken).ConfigureAwait(false);
                    return Results.NoContent();
                }
            )
            .WithName("DeleteInvitation")
            .WithSummary("Withdraws an invitation")
            .Produces((int)HttpStatusCode.NoContent);

        return app;
    }

    // A public document other services read and keep for a while.
    private static IResult Published(HttpContext http, JsonObject? document)
    {
        if (document is null)
        {
            return Results.NotFound();
        }

        http.Response.Headers.CacheControl = "public, max-age=300";
        return Results.Content(document.ToJsonString(), "application/json");
    }
}
