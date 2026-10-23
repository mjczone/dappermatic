// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Endpoints;
using MJCZone.DapperMatic.AspNetCore.Extensions;
using MJCZone.DapperMatic.AspNetCore.Security;

namespace MJCZone.DapperMatic.AspNetCore;

/// <summary>
/// Extension methods for configuring DapperMatic minimal API endpoints.
/// </summary>
public static class WebApplicationExtensions
{
    // The key UseRouting stores its route builder under in IApplicationBuilder.Properties, and UseEndpoints
    // requires.
    private const string EndpointRouteBuilderKey = "__EndpointRouteBuilder";

    /// <summary>
    /// Configures DapperMatic middleware and maps API endpoints.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="configureEndpoints">
    /// Optional conventions for every DapperMatic endpoint, on the group that holds them:
    /// <c>group =&gt; group.RequireAuthorization().RequireRateLimiting("api").RequireCors("admin")</c>.
    /// </param>
    /// <param name="configureMiddleware">Optional action to configure custom middleware.</param>
    /// <returns>The application builder for method chaining.</returns>
    /// <remarks>
    /// <c>UseRouting</c> does not need to be called first. On a <c>WebApplication</c> the endpoints are mapped
    /// onto the application itself, which routes them; on any other application builder, routing is added
    /// unless <c>UseRouting</c> has already been called.
    /// </remarks>
    public static IApplicationBuilder UseDapperMatic(
        this IApplicationBuilder app,
        Action<RouteGroupBuilder>? configureEndpoints = null,
        Action<IApplicationBuilder>? configureMiddleware = null
    )
    {
        ArgumentNullException.ThrowIfNull(app);

        // Routing first, where the application has not added it: UseEndpoints needs it. A WebApplication routes before
        // its pipeline on its own.
        if (app is not IEndpointRouteBuilder && !app.Properties.ContainsKey(EndpointRouteBuilderKey))
        {
            app.UseRouting();
        }

        // Allow users to add custom middleware
        configureMiddleware?.Invoke(app);

        // What packages built on DapperMatic need in the pipeline before their endpoints (WebSockets, say).
        foreach (var contributor in app.ApplicationServices.GetServices<IDapperMaticEndpointContributor>())
        {
            contributor.Configure(app);
        }

        // Map endpoints
        if (app is IEndpointRouteBuilder endpoints)
        {
            var group = endpoints.MapDapperMaticEndpoints();
            configureEndpoints?.Invoke(group);
        }
        else
        {
            app.UseEndpoints(e =>
            {
                var group = e.MapDapperMaticEndpoints();
                configureEndpoints?.Invoke(group);
            });
        }

        return app;
    }

    /// <summary>
    /// Maps DapperMatic API endpoints under <see cref="DapperMaticOptions.BasePath"/>, the sets
    /// <see cref="DapperMaticOptions.Endpoints"/> names.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <returns>
    /// The group holding every DapperMatic endpoint, for conventions such as <c>RequireAuthorization()</c>,
    /// <c>RequireRateLimiting(...)</c> or <c>RequireCors(...)</c>.
    /// </returns>
    /// <remarks>
    /// Dapper type mapping is initialized here if the application has not already done so: to configure it, call
    /// <c>DapperMaticTypeMapping.Initialize(options)</c> before this.
    /// </remarks>
    public static RouteGroupBuilder MapDapperMaticEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        DapperMatic.TypeMapping.DapperMaticTypeMapping.Initialize();

        var options = app.ServiceProvider.GetService<IOptions<DapperMaticOptions>>()?.Value ?? new DapperMaticOptions();

        // The policies are validated now, at startup; with none and the default permissions, every request is refused.
        if (app.ServiceProvider.GetService<DapperMaticPolicySet>() is { Policies.Count: 0 }
            && app.ServiceProvider.GetService<IDapperMaticPermissions>() is DefaultDapperMaticPermissions)
        {
            app.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger("MJCZone.DapperMatic.AspNetCore")
                .LogWarning(
                    "DapperMatic has no policies, so it refuses every request. Grant access with dm.Policy(DmPolicy.Allow(...)), "
                        + "the DapperMatic:Policies configuration section, or dm.UseCustomPermissions(...)."
                );
        }
        var sets = options.Endpoints;
        var group = app.MapGroup("/" + (options.BasePath ?? "/api/dm").Trim('/'));

        // Every endpoint is named once, from its route and method, unless it declared its name itself.
        var basePath = "/" + (options.BasePath ?? "/api/dm").Trim('/');
        ((IEndpointConventionBuilder)group).Finally(builder => NameOperation(builder, basePath));
        ((IEndpointConventionBuilder)group).Finally(DapperMaticRequestPipeline.Wrap);
        if (sets.HasFlag(DapperMaticEndpoints.Datasources))
        {
            group.MapDapperMaticDatasourceEndpoints(string.Empty);
        }

        if (sets.HasFlag(DapperMaticEndpoints.DataTypes))
        {
            group.MapDapperMaticDataTypeEndpoints(string.Empty);
        }

        if (sets.HasFlag(DapperMaticEndpoints.Schemas))
        {
            group.MapDapperMaticSchemaEndpoints(string.Empty);
            group.MapDapperMaticColumnEndpoints(string.Empty);
            group.MapDapperMaticCheckConstraintEndpoints(string.Empty);
            group.MapDapperMaticDefaultConstraintEndpoints(string.Empty);
            group.MapDapperMaticForeignKeyConstraintEndpoints(string.Empty);
            group.MapDapperMaticPrimaryKeyConstraintEndpoints(string.Empty);
            group.MapDapperMaticUniqueConstraintEndpoints(string.Empty);
            group.MapDapperMaticIndexEndpoints(string.Empty);
        }

        if (sets.HasFlag(DapperMaticEndpoints.Access))
        {
            group.MapDapperMaticAccessEndpoints(string.Empty);
        }

        // Tables and views hold definitions, queries and row writes: each set maps its own.
        group.MapDapperMaticViewEndpoints(string.Empty, sets);
        group.MapDapperMaticTableEndpoints(string.Empty, sets);
        if (sets.HasFlag(DapperMaticEndpoints.Rows))
        {
            group.MapDapperMaticBatchEndpoints(string.Empty);
        }

        if ((sets & (DapperMaticEndpoints.Query | DapperMaticEndpoints.Rows)) != 0)
        {
            group.MapDapperMaticOpenApiEndpoints(string.Empty);
        }

        if (sets.HasFlag(DapperMaticEndpoints.Functions))
        {
            group.MapDapperMaticFunctionEndpoints(string.Empty);
        }

        if (sets.HasFlag(DapperMaticEndpoints.Realtime))
        {
            group.MapDapperMaticRealtimeEndpoints(string.Empty);
        }

        if (sets.HasFlag(DapperMaticEndpoints.Auth))
        {
            group.MapDapperMaticAuthEndpoints(string.Empty);
            if (options.ApiKeys.Enabled)
            {
                group.MapDapperMaticOwnKeyEndpoints(string.Empty);
            }

            if (options.Accounts.Enabled)
            {
                group.MapDapperMaticAccountEndpoints(string.Empty);
            }
        }

        if (sets.HasFlag(DapperMaticEndpoints.Access) && options.ApiKeys.Enabled)
        {
            group.MapDapperMaticServiceAccountEndpoints(string.Empty);
        }

        if (sets.HasFlag(DapperMaticEndpoints.Access) && options.Accounts.Enabled)
        {
            group.MapDapperMaticAccountAdminEndpoints(string.Empty);
        }

        // Every method DapperMatic signs callers in with is registered, or it refuses to start.
        app.ServiceProvider.GetService<Authentication.DapperMaticSignIn>()?.AssertRegistered();

        if (sets.HasFlag(DapperMaticEndpoints.Mcp))
        {
            // Each tool call runs as the caller of the request that makes it. A session's execution context (an obsolete,
            // stateful mode) would run every call as the caller who opened the session.
#pragma warning disable MCP9006
            var perSession = app.ServiceProvider.GetService<IOptions<ModelContextProtocol.AspNetCore.HttpServerTransportOptions>>()?.Value.PerSessionExecutionContext;
#pragma warning restore MCP9006
            if (perSession == true)
            {
                throw new InvalidOperationException(
                    "DapperMatic's MCP tools run as each request's caller: HttpServerTransportOptions.PerSessionExecutionContext must be false."
                );
            }

            group.MapMcp("/mcp");
        }

        // The endpoints of packages built on DapperMatic, in the same group.
        foreach (var contributor in app.ServiceProvider.GetServices<IDapperMaticEndpointContributor>())
        {
            contributor.Map(group, options);
        }

        return group;
    }

    // Names an endpoint's operation, once: from its route and method; an endpoint answering several methods is named per
    // request, from the request's.
    private static void NameOperation(EndpointBuilder builder, string basePath)
    {
        var declared = builder.Metadata.OfType<DapperMaticOperationMetadata>().LastOrDefault();
        if (builder is not RouteEndpointBuilder route || declared?.Name is not null)
        {
            return;
        }

        // The base path is the application's, and its segments name nothing.
        var pattern = route.RoutePattern.RawText ?? string.Empty;
        if (pattern.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
        {
            pattern = pattern[basePath.Length..];
        }

        var methods = route.Metadata.OfType<HttpMethodMetadata>().LastOrDefault()?.HttpMethods;
        route.Metadata.Add(
            new DapperMaticOperationMetadata(methods is { Count: 1 } ? DapperMaticOperationMetadata.NameOf(pattern, methods[0]) : null)
            {
                SignsInOnSocket = declared?.SignsInOnSocket ?? false,
                AcceptsTickets = declared?.AcceptsTickets ?? false,
            }
        );
    }
}
