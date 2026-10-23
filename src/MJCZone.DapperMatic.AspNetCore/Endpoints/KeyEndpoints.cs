// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MJCZone.DapperMatic.AspNetCore.Extensions;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Models.Responses;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.AspNetCore.Services;

namespace MJCZone.DapperMatic.AspNetCore.Endpoints;

/// <summary>
/// Endpoints for API keys: service accounts and every key, for those who manage access (<c>/access</c>); one's own
/// personal keys (<c>/auth/keys</c>).
/// </summary>
internal static class KeyEndpoints
{
    /// <summary>
    /// Maps the service account and key endpoints of those who manage access.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <param name="basePath">The base path for the API endpoints.</param>
    /// <returns>The route builder for method chaining.</returns>
    public static IEndpointRouteBuilder MapDapperMaticServiceAccountEndpoints(this IEndpointRouteBuilder app, string? basePath = null)
    {
        var group = app.MapDapperMaticEndpointGroup(basePath, "/access", OperationTags.Access);

        group.MapGet("/service-accounts", ListServiceAccountsAsync)
            .WithName("ListServiceAccounts")
            .WithSummary("Lists the service accounts: accounts for programs, whose keys sign in as them")
            .Produces<AccessResponse<IReadOnlyList<DapperMaticServiceAccount>>>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.Forbidden);

        group.MapPut("/service-accounts/{accountName}", SaveServiceAccountAsync)
            .WithName("SaveServiceAccount")
            .WithSummary("Adds or replaces a service account: its roles, claims and tenant")
            .Produces<AccessResponse<DapperMaticServiceAccount>>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.BadRequest)
            .Produces((int)HttpStatusCode.Forbidden);

        group.MapDelete("/service-accounts/{accountName}", DeleteServiceAccountAsync)
            .WithName("DeleteServiceAccount")
            .WithSummary("Removes a service account and revokes its keys")
            .Produces((int)HttpStatusCode.NoContent)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group.MapPost("/service-accounts/{accountName}/keys", CreateServiceKeyAsync)
            .WithName("CreateServiceAccountKey")
            .WithSummary("Makes a key for a service account, shown once: its expiry and the most it may do")
            .Produces<AccessResponse<ApiKeyCreated>>((int)HttpStatusCode.Created)
            .Produces((int)HttpStatusCode.BadRequest)
            .Produces((int)HttpStatusCode.Forbidden);

        group.MapGet("/keys", ListKeysAsync)
            .WithName("ListApiKeys")
            .WithSummary("Lists API keys, without their secrets: service accounts' and people's")
            .Produces<AccessResponse<IReadOnlyList<ApiKeyInfo>>>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.Forbidden);

        group.MapDelete("/keys/{keyId}", RevokeKeyAsync)
            .WithName("RevokeApiKey")
            .WithSummary("Revokes an API key: it no longer signs in")
            .Produces((int)HttpStatusCode.NoContent)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        return app;
    }

    /// <summary>
    /// Maps the endpoints of one's own personal keys.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <param name="basePath">The base path for the API endpoints.</param>
    /// <returns>The route builder for method chaining.</returns>
    public static IEndpointRouteBuilder MapDapperMaticOwnKeyEndpoints(this IEndpointRouteBuilder app, string? basePath = null)
    {
        var group = app.MapDapperMaticEndpointGroup(basePath, "/auth", OperationTags.Auth);

        group.MapGet("/keys", ListOwnKeysAsync)
            .WithMetadata(new DapperMaticOperationMetadata("auth/keys-list"))
            .WithName("ListOwnApiKeys")
            .WithSummary("Lists the caller's own personal keys")
            .Produces<AccessResponse<IReadOnlyList<ApiKeyInfo>>>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.Unauthorized);

        group.MapPost("/keys", CreateOwnKeyAsync)
            .WithMetadata(new DapperMaticOperationMetadata("auth/keys-create"))
            .WithName("CreateOwnApiKey")
            .WithSummary("Makes a personal key for the caller, shown once: it acts as them, never more")
            .Produces<AccessResponse<ApiKeyCreated>>((int)HttpStatusCode.Created)
            .Produces((int)HttpStatusCode.BadRequest)
            .Produces((int)HttpStatusCode.Forbidden);

        group.MapDelete("/keys/{keyId}", RevokeOwnKeyAsync)
            .WithMetadata(new DapperMaticOperationMetadata("auth/keys-revoke"))
            .WithName("RevokeOwnApiKey")
            .WithSummary("Revokes one of the caller's personal keys")
            .Produces((int)HttpStatusCode.NoContent)
            .Produces((int)HttpStatusCode.NotFound);

        group.MapDelete("/keys", RevokeOwnKeysAsync)
            .WithMetadata(new DapperMaticOperationMetadata("auth/keys-revoke-all"))
            .WithName("RevokeOwnApiKeys")
            .WithSummary("Revokes every one of the caller's personal keys")
            .Produces<AccessResponse<int>>((int)HttpStatusCode.OK);

        return app;
    }

    private static async Task<IResult> ListServiceAccountsAsync(DapperMaticOperation context, DapperMaticAccessManager manager, [FromQuery] string? tenant, CancellationToken cancellationToken) =>
        Results.Ok(new AccessResponse<IReadOnlyList<DapperMaticServiceAccount>>(await manager.GetServiceAccountsAsync(context, tenant, cancellationToken).ConfigureAwait(false)));

    private static async Task<IResult> SaveServiceAccountAsync(
        DapperMaticOperation context,
        DapperMaticAccessManager manager,
        [FromRoute] string accountName,
        [FromBody] DapperMaticServiceAccount account,
        [FromQuery] string? tenant,
        CancellationToken cancellationToken
    )
    {
        context = context with { RequestBody = account };
        return Results.Ok(new AccessResponse<DapperMaticServiceAccount>(await manager.SaveServiceAccountAsync(context, accountName, account, tenant, cancellationToken).ConfigureAwait(false)));
    }

    private static async Task<IResult> DeleteServiceAccountAsync(
        DapperMaticOperation context,
        DapperMaticAccessManager manager,
        [FromRoute] string accountName,
        [FromQuery] string? tenant,
        CancellationToken cancellationToken
    )
    {
        await manager.DeleteServiceAccountAsync(context, accountName, tenant, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> CreateServiceKeyAsync(
        DapperMaticOperation context,
        DapperMaticAccessManager manager,
        [FromRoute] string accountName,
        [FromBody] ApiKeyRequest request,
        [FromQuery] string? tenant,
        CancellationToken cancellationToken
    )
    {
        var created = await manager.CreateServiceKeyAsync(context, accountName, request, tenant, cancellationToken).ConfigureAwait(false);
        return Results.Json(new AccessResponse<ApiKeyCreated>(created), statusCode: (int)HttpStatusCode.Created);
    }

    private static async Task<IResult> ListKeysAsync(
        DapperMaticOperation context,
        DapperMaticAccessManager manager,
        [FromQuery] string? owner,
        [FromQuery] string? tenant,
        [FromQuery] DapperMaticKeyKind? kind,
        CancellationToken cancellationToken
    ) =>
        Results.Ok(
            new AccessResponse<IReadOnlyList<ApiKeyInfo>>(
                await manager.GetKeysAsync(context, new DapperMaticKeyFilter { Owner = owner, Tenant = tenant, Kind = kind }, cancellationToken).ConfigureAwait(false)
            )
        );

    private static async Task<IResult> RevokeKeyAsync(DapperMaticOperation context, DapperMaticAccessManager manager, [FromRoute] string keyId, CancellationToken cancellationToken)
    {
        await manager.RevokeKeyAsync(context, keyId, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> ListOwnKeysAsync(DapperMaticOperation context, DapperMaticAccessManager manager, CancellationToken cancellationToken) =>
        Results.Ok(new AccessResponse<IReadOnlyList<ApiKeyInfo>>(await manager.GetOwnKeysAsync(context, cancellationToken).ConfigureAwait(false)));

    private static async Task<IResult> CreateOwnKeyAsync(DapperMaticOperation context, DapperMaticAccessManager manager, [FromBody] ApiKeyRequest request, CancellationToken cancellationToken)
    {
        var created = await manager.CreateOwnKeyAsync(context, request, cancellationToken).ConfigureAwait(false);
        return Results.Json(new AccessResponse<ApiKeyCreated>(created), statusCode: (int)HttpStatusCode.Created);
    }

    private static async Task<IResult> RevokeOwnKeyAsync(DapperMaticOperation context, DapperMaticAccessManager manager, [FromRoute] string keyId, CancellationToken cancellationToken)
    {
        await manager.RevokeOwnKeysAsync(context, keyId, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> RevokeOwnKeysAsync(DapperMaticOperation context, DapperMaticAccessManager manager, CancellationToken cancellationToken) =>
        Results.Ok(new AccessResponse<int>(await manager.RevokeOwnKeysAsync(context, null, cancellationToken).ConfigureAwait(false)));
}
