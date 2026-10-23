// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.Dml;

namespace MJCZone.DapperMatic.AspNetCore;

/// <summary>
/// Extension methods setting the values the DML methods take from the current request.
/// </summary>
public static class DmRequestValuesExtensions
{
    /// <summary>
    /// Sets <see cref="DmDefaults.TenantId"/> and <see cref="DmDefaults.User"/> to read the current request, through
    /// <see cref="IHttpContextAccessor"/>: the tenant of models with a <c>[DmTenantKey]</c>, and the user of
    /// <c>[DmCreatedBy]</c> and <c>[DmUpdatedBy]</c>. A scope's own <c>ForTenant</c> and <c>WithUser</c> still win.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="tenant">The request's tenant, as <c>http =&gt; http.User.FindFirst("tenant_id")?.Value</c>; or null to leave it.</param>
    /// <param name="user">The request's user, as <c>http =&gt; http.User.Identity?.Name</c>; or null to leave it.</param>
    /// <param name="schema">
    /// The request's tenant's schema, for a schema per tenant (see <c>DmSchemaLayout</c>), looked up rather than built
    /// from input; or null to leave it.
    /// </param>
    /// <returns>The same application builder, for chaining.</returns>
    /// <exception cref="InvalidOperationException">No <see cref="IHttpContextAccessor"/> is registered.</exception>
    /// <remarks>
    /// Outside a request, such as in a background job, both return null: a tenant-keyed model is then refused unless the
    /// scope gives a tenant or says <c>AllTenants()</c>.
    /// </remarks>
    public static IApplicationBuilder UseDapperMaticRequestValues(
        this IApplicationBuilder app,
        Func<HttpContext, object?>? tenant = null,
        Func<HttpContext, object?>? user = null,
        Func<HttpContext, string?>? schema = null
    )
    {
        ArgumentNullException.ThrowIfNull(app);
        var accessor = app.ApplicationServices.GetService<IHttpContextAccessor>()
            ?? throw new InvalidOperationException(
                "UseDapperMaticRequestValues reads the current request through IHttpContextAccessor: register it with "
                    + "services.AddHttpContextAccessor()."
            );

        if (tenant is not null)
        {
            DmDefaults.TenantId = () => accessor.HttpContext is { } http ? tenant(http) : null;
        }

        if (user is not null)
        {
            DmDefaults.User = () => accessor.HttpContext is { } http ? user(http) : null;
        }

        if (schema is not null)
        {
            DmDefaults.TenantSchema = () => accessor.HttpContext is { } http ? schema(http) : null;
        }

        return app;
    }

    /// <summary>
    /// Sets <see cref="DmDefaults.TenantId"/>, <see cref="DmDefaults.User"/> and <see cref="DmDefaults.TenantSchema"/>
    /// to the current caller's, as DapperMatic's tenancy finds them: the tenant its sources find (a claim, a header, a
    /// lookup), the user's id (<c>@user.id</c>: the name identifier or <c>sub</c> claim), and the tenant's schema. The
    /// tenancy is configured once, for the web API and typed code alike. A scope's own <c>ForTenant</c>,
    /// <c>WithUser</c> and <c>InSchema</c> still win.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same application builder, for chaining.</returns>
    /// <exception cref="InvalidOperationException">No <see cref="IHttpContextAccessor"/> is registered, or DapperMatic is not.</exception>
    /// <remarks>
    /// Adds a step building each request's caller session before the application's endpoints run: place it after
    /// authentication, and after routing where tenants are read from routes. A WebSocket's caller signs in on the
    /// socket, so a socket's opening request gets none. Outside a request there is no caller, and every value is null.
    /// </remarks>
    public static IApplicationBuilder UseDapperMaticCallerValues(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var accessor = app.ApplicationServices.GetService<IHttpContextAccessor>()
            ?? throw new InvalidOperationException(
                "UseDapperMaticCallerValues reads the current request through IHttpContextAccessor: register it with "
                    + "services.AddHttpContextAccessor()."
            );
        var access = app.ApplicationServices.GetService<DapperMaticAccess>()
            ?? throw new InvalidOperationException("UseDapperMaticCallerValues reads DapperMatic's tenancy: register it with services.AddDapperMatic().");

        app.Use(async (http, next) =>
        {
            // DapperMatic's endpoints sign their callers in and build their sessions themselves; a socket's caller signs in
            // on the socket. Any other request is its caller's session now.
            if (http.GetEndpoint()?.Metadata.GetMetadata<DapperMaticOperationMetadata>() is null)
            {
                await access.GetSessionAsync(http, http.RequestAborted).ConfigureAwait(false);
            }

            await next(http).ConfigureAwait(false);
        });

        DapperMaticCallerSession? Caller() => DapperMaticCallerSession.Current ?? (accessor.HttpContext is { } http ? DapperMaticCallerSession.Of(http) : null);
        DmDefaults.TenantId = () => Caller()?.Tenant?.Id;
        DmDefaults.User = () => Caller()?.User is { } user ? (user.FindFirst(ClaimTypes.NameIdentifier) ?? user.FindFirst("sub"))?.Value : null;
        DmDefaults.TenantSchema = () => Caller()?.Tenant?.SchemaName;
        return app;
    }
}
