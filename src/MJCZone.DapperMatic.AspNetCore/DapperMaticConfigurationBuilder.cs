// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Auditing;
using MJCZone.DapperMatic.AspNetCore.Factories;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Repositories;
using MJCZone.DapperMatic.AspNetCore.Security;

namespace MJCZone.DapperMatic.AspNetCore;

/// <summary>
/// Fluent configuration builder for DapperMatic services.
/// </summary>
public sealed class DapperMaticConfigurationBuilder
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
