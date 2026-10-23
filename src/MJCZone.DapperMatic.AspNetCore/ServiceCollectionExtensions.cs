// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Auditing;
using MJCZone.DapperMatic.AspNetCore.Caching;
using MJCZone.DapperMatic.AspNetCore.Factories;
using MJCZone.DapperMatic.AspNetCore.Repositories;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.AspNetCore.Services;

namespace MJCZone.DapperMatic.AspNetCore;

/// <summary>
/// Extension methods for registering DapperMatic services with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds DapperMatic ASP.NET Core services with configuration to the specified IServiceCollection.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <param name="configure">Configuration action for the DapperMatic configuration builder.</param>
    /// <returns>The service collection for further configuration.</returns>
    public static IServiceCollection AddDapperMatic(
        this IServiceCollection services,
        Action<DapperMaticConfigurationBuilder>? configure = null
    )
    {
        services.AddScoped<IDapperMaticService, DapperMaticService>();

        services.TryAddSingleton<DapperMaticAccess>();

        // Apply fluent configuration
        if (configure != null)
        {
            var builder = new DapperMaticConfigurationBuilder(services);
            configure.Invoke(builder);
        }

        // Register defaults (do this AFTER the fluent configuration to allow overrides by the user)
        services.TryAddSingleton<IDbConnectionFactory, DbConnectionFactory>();
        services.TryAddSingleton<IDatasourceIdFactory, GuidDatasourceIdFactory>();
        services.TryAddSingleton<IDapperMaticAccessRepository, InMemoryDapperMaticAccessRepository>();
        services.TryAddSingleton<DapperMaticStoredAccess>();
        services.TryAddSingleton<DapperMaticPolicySet>();
        services.TryAddScoped<DapperMaticAccessManager>();
        services.AddHttpContextAccessor();
        services.TryAddScoped<DapperMaticCallerDb>();
        services.TryAddSingleton<IDapperMaticPermissions, DefaultDapperMaticPermissions>();
        services.TryAddSingleton<IDapperMaticAuditLogger, DefaultDapperMaticAuditLogger>();
        services.TryAddSingleton(sp => new DapperMaticSchemaCache(sp.GetRequiredService<IOptions<DapperMaticOptions>>().Value.SchemaCacheDuration));
        services.TryAddSingleton(sp => new DapperMaticCallerModelCache(sp.GetRequiredService<IOptions<DapperMaticOptions>>()));
        services.TryAddSingleton<DapperMaticResultCache>();
        services.TryAddSingleton<Changes.DapperMaticChangeLog>();
        services.TryAddSingleton<Changes.DapperMaticRealtimeHub>();

        // Signing in: the methods DapperMatic's endpoints accept, and tickets for browsers' streams (kept where access
        // is). Authentication's services are there even for an application without sign-in.
        services.AddAuthenticationCore();
        services.AddDataProtection();
        services.TryAddSingleton<Authentication.DapperMaticSignIn>();
        services.TryAddSingleton<Authentication.IDapperMaticTicketStore>(Authentication.DapperMaticStores.Tickets);
        services.TryAddSingleton<Authentication.DapperMaticTickets>();

        // Webhooks are sent, and other servers' changes read for subscriptions, by workers of their own: once, however
        // often AddDapperMatic is called.
        if (!services.Any(d => d.ServiceType == typeof(Changes.DapperMaticWebhookWorker)))
        {
            services.AddHttpClient(Changes.DapperMaticWebhookWorker.ClientName);
            services.AddHttpClient(Changes.DapperMaticWebhookWorker.GuardedClientName).ConfigurePrimaryHttpMessageHandler(Changes.DapperMaticWebhookWorker.GuardedHandler);
            services.AddSingleton<Changes.DapperMaticWebhookWorker>();
            services.AddHostedService(sp => sp.GetRequiredService<Changes.DapperMaticWebhookWorker>());
            services.AddSingleton<Changes.DapperMaticRealtimePoller>();
            services.AddHostedService(sp => sp.GetRequiredService<Changes.DapperMaticRealtimePoller>());
        }

        // DapperMatic's tools for AI assistants, served at {BasePath}/mcp when the options turn the Mcp endpoints on. The
        // server is registered either way (stateless, it runs nothing in the background); the tools join it only when
        // turned on, read once the options are configured, so that an application's own MCP server never serves them
        // unasked.
        if (!services.Any(d => d.ServiceType == typeof(Mcp.DapperMaticMcpTools)))
        {
            services.AddScoped<Mcp.DapperMaticMcpTools>();
            services.AddMcpServer().WithHttpTransport();
            services
                .AddOptions<ModelContextProtocol.Server.McpServerOptions>()
                .PostConfigure<IOptions<DapperMaticOptions>, IServiceProvider>((mcp, dapperMatic, provider) =>
                {
                    if (dapperMatic.Value.Endpoints.HasFlag(DapperMaticEndpoints.Mcp))
                    {
                        Mcp.DapperMaticMcpTools.AddTo(mcp, provider);
                    }
                });
        }

        // Register default in-memory repository if no repository was explicitly configured
        // This ensures that datasources added via configuration or fluent API are captured
        RegisterDatasourceRepositoryIfNotRegistered(services);

        return services;
    }

    /// <summary>
    /// Registers the default in-memory datasource repository only if no repository was explicitly registered.
    /// </summary>
    /// <param name="services">The service collection.</param>
    private static void RegisterDatasourceRepositoryIfNotRegistered(IServiceCollection services)
    {
        // Check if repository is already registered
        var hasRepository = services.Any(sd => sd.ServiceType == typeof(IDapperMaticDatasourceRepository));

        if (!hasRepository)
        {
            RegisterDatasourceRepository(services);
        }
    }

    /// <summary>
    /// Registers the datasource repository with deferred initialization to capture all configured datasources.
    /// </summary>
    /// <param name="services">The service collection.</param>
    private static void RegisterDatasourceRepository(IServiceCollection services)
    {
        services.TryAddSingleton<IDapperMaticDatasourceRepository>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<DapperMaticOptions>>();
            var datasourceIdFactory = serviceProvider.GetRequiredService<IDatasourceIdFactory>();
            var logger = serviceProvider.GetRequiredService<ILogger<InMemoryDapperMaticDatasourceRepository>>();
            var repository = new InMemoryDapperMaticDatasourceRepository(options, datasourceIdFactory, logger);

            // Get final options configuration (includes both direct config and fluent API additions)
            if (options?.Value?.Datasources?.Count > 0)
            {
                foreach (var datasource in options.Value.Datasources)
                {
                    _ = repository.AddDatasourceAsync(datasource).GetAwaiter().GetResult();
                }
            }

            return repository;
        });
    }
}
