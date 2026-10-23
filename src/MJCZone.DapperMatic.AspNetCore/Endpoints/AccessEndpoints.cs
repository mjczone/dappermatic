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
using MJCZone.DapperMatic.AspNetCore.Models.Responses;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.AspNetCore.Services;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Endpoints;

/// <summary>
/// Endpoints for the policies and tenants DapperMatic keeps, which need <see cref="DmAction.ManageAccess"/>, and for a
/// caller's effective permissions.
/// </summary>
internal static class AccessEndpoints
{
    /// <summary>
    /// Maps the access endpoints.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <param name="basePath">The base path for the API endpoints. Defaults to "/api/dm".</param>
    /// <returns>The route builder for method chaining.</returns>
    public static IEndpointRouteBuilder MapDapperMaticAccessEndpoints(this IEndpointRouteBuilder app, string? basePath = null)
    {
        var group = app.MapDapperMaticEndpointGroup(basePath, "/access", OperationTags.Access);

        group.MapGet("/policies", ListPoliciesAsync)
            .WithName("ListPolicies")
            .WithSummary("Lists the policies in force that the caller manages")
            .Produces<AccessResponse<IReadOnlyList<DmPolicy>>>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.Forbidden);
        group.MapGet("/policies/{policyName}", GetPolicyAsync)
            .WithName("GetPolicy")
            .WithSummary("Gets a policy in force")
            .Produces<AccessResponse<DmPolicy>>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);
        group.MapPut("/policies/{policyName}", SavePolicyAsync)
            .WithName("SavePolicy")
            .WithSummary("Adds or replaces a policy kept by DapperMatic; it applies to the next request")
            .Produces<AccessResponse<DmPolicy>>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.BadRequest)
            .Produces((int)HttpStatusCode.Conflict)
            .Produces((int)HttpStatusCode.Forbidden);
        group.MapDelete("/policies/{policyName}", DeletePolicyAsync)
            .WithName("DeletePolicy")
            .WithSummary("Removes a policy kept by DapperMatic")
            .Produces((int)HttpStatusCode.NoContent)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Conflict)
            .Produces((int)HttpStatusCode.Forbidden);

        group.MapGet("/tenants", ListTenantsAsync)
            .WithName("ListTenants")
            .WithSummary("Lists the tenants the caller manages")
            .Produces<AccessResponse<IReadOnlyDictionary<string, DapperMaticTenantSettings>>>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.Forbidden);
        group.MapGet("/tenants/{tenantId}", GetTenantAsync)
            .WithName("GetTenant")
            .WithSummary("Gets a tenant")
            .Produces<AccessResponse<DapperMaticTenantSettings>>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);
        group.MapPut("/tenants/{tenantId}", SaveTenantAsync)
            .WithName("SaveTenant")
            .WithSummary("Adds or replaces a tenant kept by DapperMatic: its datasource, schema and values")
            .Produces<AccessResponse<DapperMaticTenantSettings>>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.Forbidden);
        group.MapDelete("/tenants/{tenantId}", DeleteTenantAsync)
            .WithName("DeleteTenant")
            .WithSummary("Removes a tenant kept by DapperMatic")
            .Produces((int)HttpStatusCode.NoContent)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);

        group.MapGet("/webhooks", ListWebhooksAsync)
            .WithName("ListWebhooks")
            .WithSummary("Lists the webhooks the caller manages, their secrets masked")
            .Produces<AccessResponse<IReadOnlyList<DapperMaticWebhook>>>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.Forbidden);
        group.MapGet("/webhooks/{webhookName}", GetWebhookAsync)
            .WithName("GetWebhook")
            .WithSummary("Gets a webhook, its secret masked")
            .Produces<AccessResponse<DapperMaticWebhook>>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);
        group.MapGet("/webhooks/{webhookName}/status", GetWebhookStatusAsync)
            .WithName("GetWebhookStatus")
            .WithSummary("Where a webhook's messages have got to in each datasource")
            .Produces<AccessResponse<IReadOnlyDictionary<string, DapperMaticWebhookStatus>>>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Forbidden);
        group.MapPut("/webhooks/{webhookName}", SaveWebhookAsync)
            .WithName("SaveWebhook")
            .WithSummary("Adds or replaces a webhook kept by DapperMatic; a secret left out keeps the one kept")
            .Produces<AccessResponse<DapperMaticWebhook>>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.BadRequest)
            .Produces((int)HttpStatusCode.Conflict)
            .Produces((int)HttpStatusCode.Forbidden);
        group.MapDelete("/webhooks/{webhookName}", DeleteWebhookAsync)
            .WithName("DeleteWebhook")
            .WithSummary("Removes a webhook kept by DapperMatic")
            .Produces((int)HttpStatusCode.NoContent)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Conflict)
            .Produces((int)HttpStatusCode.Forbidden);

        group.MapGet("/export", ExportAsync)
            .WithName("ExportAccess")
            .WithSummary("Exports the policies and tenants kept by DapperMatic that the caller manages")
            .Produces<DapperMaticAccessDocument>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.Forbidden);
        group.MapPost("/import", ImportAsync)
            .WithName("ImportAccess")
            .WithSummary("Imports policies and tenants, all or none; replace=true replaces everything kept")
            .Produces((int)HttpStatusCode.NoContent)
            .Produces((int)HttpStatusCode.BadRequest)
            .Produces((int)HttpStatusCode.Forbidden);

        group.MapGet("/effective", GetEffectiveAsync)
            .WithMetadata(new DapperMaticOperationMetadata("access/effective"))
            .WithName("GetEffectiveAccess")
            .WithSummary("What the policies let the caller do to a datasource, schema, table or view")
            .Produces<AccessResponse<DapperMaticEffectiveAccess>>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.Forbidden);

        return app;
    }

    // ?tenant= names a tenant whose policies a call is about; a tenant's administrator's are always their tenant's.
    private static async Task<IResult> ListPoliciesAsync(
        DapperMaticOperation operationContext,
        DapperMaticAccessManager manager,
        [FromQuery] string? tenant,
        CancellationToken cancellationToken
    ) => Results.Ok(new AccessResponse<IReadOnlyList<DmPolicy>>(await manager.GetPoliciesAsync(operationContext, tenant, cancellationToken).ConfigureAwait(false)));

    private static async Task<IResult> GetPolicyAsync(
        DapperMaticOperation operationContext,
        DapperMaticAccessManager manager,
        [FromRoute] string policyName,
        [FromQuery] string? tenant,
        CancellationToken cancellationToken
    ) => Results.Ok(new AccessResponse<DmPolicy>(await manager.GetPolicyAsync(operationContext, policyName, tenant, cancellationToken).ConfigureAwait(false)));

    private static async Task<IResult> SavePolicyAsync(
        DapperMaticOperation operationContext,
        DapperMaticAccessManager manager,
        [FromRoute] string policyName,
        [FromBody] DmPolicy policy,
        [FromQuery] string? tenant,
        CancellationToken cancellationToken
    )
    {
        operationContext = operationContext with { RequestBody = policy };
        return Results.Ok(
            new AccessResponse<DmPolicy>(await manager.SavePolicyAsync(operationContext, policyName, policy, tenant, cancellationToken).ConfigureAwait(false))
        );
    }

    private static async Task<IResult> DeletePolicyAsync(
        DapperMaticOperation operationContext,
        DapperMaticAccessManager manager,
        [FromRoute] string policyName,
        [FromQuery] string? tenant,
        CancellationToken cancellationToken
    )
    {
        await manager.DeletePolicyAsync(operationContext, policyName, tenant, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> ListWebhooksAsync(
        DapperMaticOperation operationContext,
        DapperMaticAccessManager manager,
        [FromQuery] string? tenant,
        CancellationToken cancellationToken
    ) => Results.Ok(new AccessResponse<IReadOnlyList<DapperMaticWebhook>>(await manager.GetWebhooksAsync(operationContext, tenant, cancellationToken).ConfigureAwait(false)));

    private static async Task<IResult> GetWebhookAsync(
        DapperMaticOperation operationContext,
        DapperMaticAccessManager manager,
        [FromRoute] string webhookName,
        [FromQuery] string? tenant,
        CancellationToken cancellationToken
    ) => Results.Ok(new AccessResponse<DapperMaticWebhook>(await manager.GetWebhookAsync(operationContext, webhookName, tenant, cancellationToken).ConfigureAwait(false)));

    private static async Task<IResult> GetWebhookStatusAsync(
        DapperMaticOperation operationContext,
        DapperMaticAccessManager manager,
        [FromRoute] string webhookName,
        [FromQuery] string? tenant,
        CancellationToken cancellationToken
    ) =>
        Results.Ok(
            new AccessResponse<IReadOnlyDictionary<string, DapperMaticWebhookStatus>>(
                await manager.GetWebhookStatusAsync(operationContext, webhookName, tenant, cancellationToken).ConfigureAwait(false)
            )
        );

    private static async Task<IResult> SaveWebhookAsync(
        DapperMaticOperation operationContext,
        DapperMaticAccessManager manager,
        [FromRoute] string webhookName,
        [FromBody] DapperMaticWebhook webhook,
        [FromQuery] string? tenant,
        CancellationToken cancellationToken
    )
    {
        // The secret is not audited.
        operationContext = operationContext with
        {
            RequestBody = new { webhook.Url, webhook.DatasourceId, webhook.Schemas, webhook.Tables, webhook.Actions, webhook.Enabled },
        };
        return Results.Ok(
            new AccessResponse<DapperMaticWebhook>(await manager.SaveWebhookAsync(operationContext, webhookName, webhook, tenant, cancellationToken).ConfigureAwait(false))
        );
    }

    private static async Task<IResult> DeleteWebhookAsync(
        DapperMaticOperation operationContext,
        DapperMaticAccessManager manager,
        [FromRoute] string webhookName,
        [FromQuery] string? tenant,
        CancellationToken cancellationToken
    )
    {
        await manager.DeleteWebhookAsync(operationContext, webhookName, tenant, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> ListTenantsAsync(DapperMaticOperation operationContext, DapperMaticAccessManager manager, CancellationToken cancellationToken) =>
        Results.Ok(
            new AccessResponse<IReadOnlyDictionary<string, DapperMaticTenantSettings>>(await manager.GetTenantsAsync(operationContext, cancellationToken).ConfigureAwait(false))
        );

    private static async Task<IResult> GetTenantAsync(
        DapperMaticOperation operationContext,
        DapperMaticAccessManager manager,
        [FromRoute] string tenantId,
        CancellationToken cancellationToken
    ) => Results.Ok(new AccessResponse<DapperMaticTenantSettings>(await manager.GetTenantAsync(operationContext, tenantId, cancellationToken).ConfigureAwait(false)));

    private static async Task<IResult> SaveTenantAsync(
        DapperMaticOperation operationContext,
        DapperMaticAccessManager manager,
        [FromRoute] string tenantId,
        [FromBody] DapperMaticTenantSettings tenant,
        CancellationToken cancellationToken
    )
    {
        operationContext = operationContext with { RequestBody = tenant };
        return Results.Ok(
            new AccessResponse<DapperMaticTenantSettings>(await manager.SaveTenantAsync(operationContext, tenantId, tenant, cancellationToken).ConfigureAwait(false))
        );
    }

    private static async Task<IResult> DeleteTenantAsync(
        DapperMaticOperation operationContext,
        DapperMaticAccessManager manager,
        [FromRoute] string tenantId,
        CancellationToken cancellationToken
    )
    {
        await manager.DeleteTenantAsync(operationContext, tenantId, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> ExportAsync(DapperMaticOperation operationContext, DapperMaticAccessManager manager, CancellationToken cancellationToken) =>
        Results.Ok(await manager.ExportAsync(operationContext, cancellationToken).ConfigureAwait(false));

    private static async Task<IResult> ImportAsync(
        DapperMaticOperation operationContext,
        DapperMaticAccessManager manager,
        [FromBody] DapperMaticAccessDocument document,
        [FromQuery] bool replace = false,
        CancellationToken cancellationToken = default
    )
    {
        operationContext = operationContext with { RequestBody = document };
        await manager.ImportAsync(operationContext, document, replace, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> GetEffectiveAsync(
        DapperMaticOperation operationContext,
        DapperMaticAccessManager manager,
        [FromQuery] string? datasourceId = null,
        [FromQuery] string? schemaName = null,
        [FromQuery] string? tableName = null,
        CancellationToken cancellationToken = default
    )
    {
        // The resource is named in the query: the operation is about it.
        operationContext = operationContext with
        {
            DatasourceId = datasourceId,
            SchemaName = schemaName,
            TableName = tableName,
        };
        return Results.Ok(new AccessResponse<DapperMaticEffectiveAccess>(await manager.GetEffectiveAsync(operationContext, cancellationToken).ConfigureAwait(false)));
    }
}
