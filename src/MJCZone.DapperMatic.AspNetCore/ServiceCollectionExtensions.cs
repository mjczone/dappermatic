// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Auditing;
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
        // Configure Dapper type handlers
        Dapper.SqlMapper.RemoveTypeMap(typeof(DateTimeOffset));
        Dapper.SqlMapper.RemoveTypeMap(typeof(Guid));
        Dapper.SqlMapper.RemoveTypeMap(typeof(TimeSpan));
        Dapper.SqlMapper.AddTypeHandler(new DateTimeOffsetHandler());
        Dapper.SqlMapper.AddTypeHandler(new GuidHandler());
        Dapper.SqlMapper.AddTypeHandler(new TimeSpanHandler());

        services.AddScoped<IDapperMaticService, DapperMaticService>();

        // Register operation context services
        services.TryAddScoped<IOperationContext, OperationContext>();
        services.TryAddScoped<IOperationContextInitializer, OperationContextInitializer>();

        // Apply fluent configuration
        if (configure != null)
        {
            var builder = new DapperMaticConfigurationBuilder(services);
            configure.Invoke(builder);
        }

        // Register defaults (do this AFTER the fluent configuration to allow overrides by the user)
        services.TryAddSingleton<IDbConnectionFactory, DbConnectionFactory>();
        services.TryAddSingleton<IDatasourceIdFactory, GuidDatasourceIdFactory>();
        services.TryAddSingleton<IDapperMaticPermissions, DefaultDapperMaticPermissions>();
        services.TryAddSingleton<IDapperMaticAuditLogger, DefaultDapperMaticAuditLogger>();

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

/// <summary>
/// Base class for Dapper type handlers.
/// </summary>
/// <typeparam name="T">The type to handle.</typeparam>
internal abstract class DapperTypeHandlerBase<T> : Dapper.SqlMapper.TypeHandler<T>
{
    /// <summary>
    /// Sets the value of a parameter before it is sent to the database.
    /// </summary>
    /// <param name="parameter">The database parameter to set the value for.</param>
    /// <param name="value">The value to set.</param>
    public override void SetValue(System.Data.IDbDataParameter parameter, T? value) => parameter.Value = value;
}

/// <summary>
/// Handles DateTimeOffset values for Dapper.
/// </summary>
internal class DateTimeOffsetHandler : DapperTypeHandlerBase<DateTimeOffset>
{
    /// <summary>
    /// Parses a database value into a DateTimeOffset.
    /// </summary>
    /// <param name="value">The database value to parse.</param>
    /// <returns>The parsed DateTimeOffset.</returns>
    public override DateTimeOffset Parse(object value)
    {
        return value switch
        {
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(dt),
            string s => DateTimeOffset.Parse(s),
            _ => throw new InvalidCastException($"Unable to convert {value.GetType()} to DateTimeOffset")
        };
    }
}

/// <summary>
/// Handles Guid values for Dapper.
/// </summary>
internal class GuidHandler : DapperTypeHandlerBase<Guid>
{
    /// <summary>
    /// Parses a database value into a Guid.
    /// </summary>
    /// <param name="value">The database value to parse.</param>
    /// <returns>The parsed Guid.</returns>
    public override Guid Parse(object value)
    {
        return value switch
        {
            Guid guid => guid,
            string s => Guid.Parse(s),
            byte[] bytes when bytes.Length == 16 => new Guid(bytes),
            _ => throw new InvalidCastException($"Unable to convert {value.GetType()} to Guid")
        };
    }
}

/// <summary>
/// Handles TimeSpan values for Dapper.
/// </summary>
internal class TimeSpanHandler : DapperTypeHandlerBase<TimeSpan>
{
    /// <summary>
    /// Parses a database value into a TimeSpan.
    /// </summary>
    /// <param name="value">The database value to parse.</param>
    /// <returns>The parsed TimeSpan.</returns>
    public override TimeSpan Parse(object value)
    {
        return value switch
        {
            TimeSpan ts => ts,
            string s => TimeSpan.Parse(s),
            long ticks => TimeSpan.FromTicks(ticks),
            _ => throw new InvalidCastException($"Unable to convert {value.GetType()} to TimeSpan")
        };
    }
}
