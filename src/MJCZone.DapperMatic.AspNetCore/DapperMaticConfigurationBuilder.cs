// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Auditing;
using MJCZone.DapperMatic.AspNetCore.Factories;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Repositories;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore;

/// <summary>
/// Fluent configuration builder for DapperMatic services.
/// </summary>
public sealed partial class DapperMaticConfigurationBuilder
{
    private readonly IServiceCollection _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticConfigurationBuilder"/> class.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    internal DapperMaticConfigurationBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Adds a single datasource to the configuration.
    /// </summary>
    /// <param name="datasource">The datasource to add.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public DapperMaticConfigurationBuilder WithDatasource(DatasourceDto datasource)
    {
        ArgumentNullException.ThrowIfNull(datasource);

        _services.Configure<DapperMaticOptions>(options => options.Datasources.Add(datasource));

        return this;
    }

    /// <summary>
    /// Adds multiple datasources to the configuration.
    /// </summary>
    /// <param name="datasources">The datasources to add.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public DapperMaticConfigurationBuilder WithDatasources(params DatasourceDto[] datasources)
    {
        ArgumentNullException.ThrowIfNull(datasources);

        _services.Configure<DapperMaticOptions>(options => options.Datasources.AddRange(datasources));

        return this;
    }

    /// <summary>
    /// Adds multiple datasources to the configuration.
    /// </summary>
    /// <param name="datasources">The datasources to add.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public DapperMaticConfigurationBuilder WithDatasources(IEnumerable<DatasourceDto> datasources)
    {
        ArgumentNullException.ThrowIfNull(datasources);

        _services.Configure<DapperMaticOptions>(options => options.Datasources.AddRange(datasources));

        return this;
    }

    #region Drivers

    /// <summary>
    /// Registers the driver for a database provider, such as <c>NpgsqlFactory.Instance</c> (Npgsql),
    /// <c>SqlClientFactory.Instance</c> (Microsoft.Data.SqlClient), <c>MySqlConnectorFactory.Instance</c>
    /// (MySqlConnector) or <c>SqliteFactory.Instance</c> (Microsoft.Data.Sqlite). DapperMatic bundles no driver:
    /// datasources of a provider without one cannot connect.
    /// </summary>
    /// <param name="factory">The driver's factory; its provider is read from its type.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <exception cref="ArgumentException">The factory is not a driver DapperMatic recognizes; name its provider instead.</exception>
    public DapperMaticConfigurationBuilder AddProvider(DbProviderFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        var provider = DapperMaticProviders.Of(factory)
            ?? throw new ArgumentException(
                $"{factory.GetType().FullName} is not a driver DapperMatic recognizes: name its provider with AddProvider(provider, factory).",
                nameof(factory)
            );
        return AddProvider(provider, factory);
    }

    /// <summary>
    /// Registers the driver for a database provider, replacing any registered before.
    /// </summary>
    /// <param name="provider">The provider the driver serves.</param>
    /// <param name="factory">The driver's factory.</param>
    /// <returns>The builder, for chaining.</returns>
    public DapperMaticConfigurationBuilder AddProvider(DbProviderType provider, DbProviderFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _services.Configure<DapperMaticOptions>(options => options.ProviderFactories[provider] = factory);
        return this;
    }

    #endregion // Drivers

    #region Tenancy and session

    /// <summary>
    /// Adds a policy: <c>dm.Policy(DmPolicy.Allow(DmAction.Read, "*").OnTables("products"))</c>. Nothing is allowed
    /// unless a policy grants it.
    /// </summary>
    /// <param name="policy">The policy, validated at startup.</param>
    /// <returns>The builder.</returns>
    public DapperMaticConfigurationBuilder Policy(DmPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        policy.Source ??= "code";
        _services.Configure<DapperMaticOptions>(options => options.Policies.Add(policy));
        return this;
    }

    /// <summary>
    /// Copies claims of the caller into every audit event, with the tenant: who acted, in which tenant, under what.
    /// </summary>
    /// <param name="claimTypes">The claim types, such as <c>grant_id</c> or <c>act</c>.</param>
    /// <returns>The builder.</returns>
    public DapperMaticConfigurationBuilder AuditClaims(params string[] claimTypes)
    {
        ArgumentNullException.ThrowIfNull(claimTypes);
        _services.Configure<DapperMaticOptions>(options => options.AuditClaims.AddRange(claimTypes));
        return this;
    }

    /// <summary>
    /// Adds policies. Nothing is allowed unless a policy grants it.
    /// </summary>
    /// <param name="policies">The policies, validated at startup.</param>
    /// <returns>The builder.</returns>
    public DapperMaticConfigurationBuilder Policies(params DmPolicy[] policies)
    {
        ArgumentNullException.ThrowIfNull(policies);
        foreach (var policy in policies)
        {
            Policy(policy);
        }

        return this;
    }

    /// <summary>
    /// Says how a request's tenant is found, and what is known of it: built-in sources, code, and lookups.
    /// </summary>
    /// <param name="configure">Configures the tenancy.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <example>
    /// <code>
    /// dm.Tenancy(t =&gt; t
    ///     .FromClaim("tenant_id")
    ///     .FromSubdomain("{tenant}.app.example.com")
    ///     .Value("below", async (ctx, ct) =&gt; await ctx.Services!.GetRequiredService&lt;ITenantTree&gt;().SelfAndBelowAsync(ctx.Tenant!.Id, ct)));
    /// </code>
    /// </example>
    public DapperMaticConfigurationBuilder Tenancy(Action<DapperMaticTenancyBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _services.Configure<DapperMaticOptions>(options => configure(new DapperMaticTenancyBuilder(options.Tenancy)));
        return this;
    }

    /// <summary>
    /// Adds a request value, read by policies as <c>@values.name</c>, computed when first read in a request.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="provider">Computes the value.</param>
    /// <returns>The builder, for chaining.</returns>
    public DapperMaticConfigurationBuilder Value(string name, DmValueProvider provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(provider);
        _services.Configure<DapperMaticOptions>(options => options.RequestValues[name] = provider);
        return this;
    }

    /// <summary>
    /// Sets a value on each connection's session for the database's own row-level security: on PostgreSQL
    /// <c>set_config</c>, on SQL Server <c>sp_set_session_context</c> (read-only), on MySQL a user variable.
    /// </summary>
    /// <param name="name">The name: <c>app.tenant_id</c> on PostgreSQL, a key on SQL Server, a variable on MySQL.</param>
    /// <param name="value">A path such as <c>@tenant.id</c>, or a fixed text.</param>
    /// <returns>The builder, for chaining.</returns>
    public DapperMaticConfigurationBuilder SessionValue(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _services.Configure<DapperMaticOptions>(options => options.Session.Add(new DapperMaticSessionValue { Name = name, Value = value }));
        return this;
    }

    /// <summary>
    /// Runs a step on each connection once it is opened for a request, after the session values are set: anything the
    /// database needs to know about the caller.
    /// </summary>
    /// <param name="step">The step, given the open connection and the request's access context.</param>
    /// <returns>The builder, for chaining.</returns>
    public DapperMaticConfigurationBuilder OnConnectionOpened(Func<IDbConnection, DmAccessContext, CancellationToken, Task> step)
    {
        ArgumentNullException.ThrowIfNull(step);
        _services.Configure<DapperMaticOptions>(options => options.SessionSteps.Add(step));
        return this;
    }

    #endregion // Tenancy and session

    #region DatasourceId Factory Configuration

    /// <summary>
    /// Configures DapperMatic to use a custom datasource ID factory implementation.
    /// </summary>
    /// <typeparam name="TFactory">The type of the custom factory implementation.</typeparam>
    /// <returns>The configuration builder for method chaining.</returns>
    public DapperMaticConfigurationBuilder UseCustomDatasourceIdFactory<TFactory>()
        where TFactory : class, IDatasourceIdFactory
    {
        _services.AddSingleton<IDatasourceIdFactory, TFactory>();
        return this;
    }

    /// <summary>
    /// Configures DapperMatic to use a custom datasource ID factory implementation.
    /// </summary>
    /// <param name="implementationFactory">A factory function to create the factory instance.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if implementationFactory is null.</exception>
    public DapperMaticConfigurationBuilder UseCustomDatasourceIdFactory(
        Func<IServiceProvider, IDatasourceIdFactory> implementationFactory
    )
    {
        ArgumentNullException.ThrowIfNull(implementationFactory);
        _services.AddSingleton(implementationFactory);
        return this;
    }
    #endregion

    #region Permissions Configuration

    /// <summary>
    /// Configures DapperMatic to use a custom permissions implementation.
    /// </summary>
    /// <typeparam name="TPermissions">The type of the custom permissions implementation.</typeparam>
    /// <returns>The configuration builder for method chaining.</returns>
    public DapperMaticConfigurationBuilder UseCustomPermissions<TPermissions>()
        where TPermissions : class, IDapperMaticPermissions
    {
        _services.AddSingleton<IDapperMaticPermissions, TPermissions>();
        return this;
    }

    /// <summary>
    /// Configures DapperMatic to use a custom permissions implementation.
    /// </summary>
    /// <param name="implementationFactory">A factory function to create the permissions instance.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if implementationFactory is null.</exception>
    public DapperMaticConfigurationBuilder UseCustomPermissions(
        Func<IServiceProvider, IDapperMaticPermissions> implementationFactory
    )
    {
        ArgumentNullException.ThrowIfNull(implementationFactory);
        _services.AddSingleton(implementationFactory);
        return this;
    }
    #endregion // Permissions Configuration

    #region Audit Logger Configuration

    /// <summary>
    /// Configures DapperMatic to use a custom audit logger implementation.
    /// </summary>
    /// <typeparam name="TAuditLogger">The type of the custom audit logger implementation.</typeparam>
    /// <returns>The configuration builder for method chaining.</returns>
    /// <remarks>
    /// The custom audit logger must implement <see cref="IDapperMaticAuditLogger"/>.
    /// </remarks>
    public DapperMaticConfigurationBuilder UseCustomAuditLogger<TAuditLogger>()
        where TAuditLogger : class, IDapperMaticAuditLogger
    {
        _services.AddSingleton<IDapperMaticAuditLogger, TAuditLogger>();
        return this;
    }

    /// <summary>
    /// Configures DapperMatic to use a custom audit logger implementation.
    /// </summary>
    /// <param name="implementationFactory">A factory function to create the audit logger instance.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if implementationFactory is null.</exception>
    /// <remarks>
    /// The custom audit logger must implement <see cref="IDapperMaticAuditLogger"/>.
    /// </remarks>
    public DapperMaticConfigurationBuilder UseCustomAuditLogger(
        Func<IServiceProvider, IDapperMaticAuditLogger> implementationFactory
    )
    {
        ArgumentNullException.ThrowIfNull(implementationFactory);
        _services.AddSingleton(implementationFactory);
        return this;
    }
    #endregion // Audit Logger Configuration

    #region Access Repository Configuration

    /// <summary>
    /// Keeps the policies and tenants edited through the access endpoints in a JSON file. By default they are kept in
    /// memory, for the life of the process.
    /// </summary>
    /// <param name="filePath">The file; created when first written.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public DapperMaticConfigurationBuilder UseFileAccessRepository(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _services.AddSingleton<IDapperMaticAccessRepository>(_ => new FileDapperMaticAccessRepository(filePath));
        return this;
    }

    /// <summary>
    /// Keeps the policies and tenants edited through the access endpoints in a database table, which every instance of
    /// the application reads; it is created when first used.
    /// </summary>
    /// <param name="provider">The database provider, whose driver is registered with <see cref="AddProvider(DbProviderFactory)"/>.</param>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="schemaName">The table's schema, or null for the default one.</param>
    /// <param name="tableName">The table's name.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public DapperMaticConfigurationBuilder UseDatabaseAccessRepository(
        string provider,
        string connectionString,
        string? schemaName = null,
        string tableName = "dm_access"
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _services.AddSingleton<IDapperMaticAccessRepository>(services =>
            new DatabaseDapperMaticAccessRepository(provider, connectionString, services.GetRequiredService<IDbConnectionFactory>(), schemaName, tableName)
        );
        _services.Configure<DapperMaticOptions>(options => options.PrivateTables.Add(tableName));
        return this;
    }

    /// <summary>
    /// Keeps the policies and tenants edited through the access endpoints wherever the application's own repository
    /// keeps them.
    /// </summary>
    /// <typeparam name="TRepository">The repository.</typeparam>
    /// <returns>The configuration builder for method chaining.</returns>
    public DapperMaticConfigurationBuilder UseCustomAccessRepository<TRepository>()
        where TRepository : class, IDapperMaticAccessRepository
    {
        _services.AddSingleton<IDapperMaticAccessRepository, TRepository>();
        return this;
    }

    #endregion // Access Repository Configuration

    #region Datasource Repository Configuration

    /// <summary>
    /// Configures DapperMatic to use a file-based datasource repository.
    /// </summary>
    /// <param name="filePath">The path to the JSON file where datasources will be stored.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public DapperMaticConfigurationBuilder UseFileDatasourceRepository(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        _services.AddSingleton<IDapperMaticDatasourceRepository>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<DapperMaticOptions>>();
            var datasourceIdFactory = provider.GetRequiredService<IDatasourceIdFactory>();
            var logger = provider.GetRequiredService<ILogger<FileDapperMaticDatasourceRepository>>();
            var repository = new FileDapperMaticDatasourceRepository(filePath, datasourceIdFactory, options, logger);

            // Initialize with configured datasources from options
            if (options.Value.Datasources?.Count > 0)
            {
                foreach (var datasource in options.Value.Datasources)
                {
                    _ = repository.AddDatasourceAsync(datasource).GetAwaiter().GetResult();
                }
            }

            return repository;
        });

        return this;
    }

    /// <summary>
    /// Configures DapperMatic to use a database-based datasource repository.
    /// </summary>
    /// <param name="provider">The database provider for the repository storage.</param>
    /// <param name="connectionString">The connection string for the repository database.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public DapperMaticConfigurationBuilder UseDatabaseDatasourceRepository(string provider, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        _services.AddSingleton<IDapperMaticDatasourceRepository>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<DapperMaticOptions>>();
            var connectionFactory = serviceProvider.GetRequiredService<IDbConnectionFactory>();
            var datasourceIdFactory = serviceProvider.GetRequiredService<IDatasourceIdFactory>();
            var logger = serviceProvider.GetRequiredService<ILogger<DatabaseDapperMaticDatasourceRepository>>();
            var repository = new DatabaseDapperMaticDatasourceRepository(
                provider,
                connectionString,
                connectionFactory,
                datasourceIdFactory,
                options,
                logger
            );
            repository.Initialize();

            // Initialize with configured datasources from options
            if (options.Value.Datasources?.Count > 0)
            {
                foreach (var datasource in options.Value.Datasources)
                {
                    _ = repository.AddDatasourceAsync(datasource).GetAwaiter().GetResult();
                }
            }

            return repository;
        });

        return this;
    }

    /// <summary>
    /// Configures DapperMatic to use a custom datasource repository implementation.
    /// </summary>
    /// <typeparam name="TRepository">The type of the custom repository implementation.</typeparam>
    /// <returns>The configuration builder for method chaining.</returns>
    public DapperMaticConfigurationBuilder UseCustomDatasourceRepository<TRepository>()
        where TRepository : class, IDapperMaticDatasourceRepository
    {
        _services.AddSingleton<IDapperMaticDatasourceRepository, TRepository>();
        return this;
    }

    /// <summary>
    /// Configures DapperMatic to use a custom datasource repository implementation.
    /// </summary>
    /// <param name="implementationFactory">A factory function to create the repository instance.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public DapperMaticConfigurationBuilder UseCustomDatasourceRepository(
        Func<IServiceProvider, IDapperMaticDatasourceRepository> implementationFactory
    )
    {
        ArgumentNullException.ThrowIfNull(implementationFactory);
        _services.AddSingleton(implementationFactory);
        return this;
    }

    #endregion // Datasource Repository Configuration
}
