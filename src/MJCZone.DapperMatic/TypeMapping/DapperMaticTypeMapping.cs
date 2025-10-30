// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using System.Net.NetworkInformation;
using System.Xml.Linq;
using Dapper;
using MJCZone.DapperMatic.TypeMapping.Handlers;

namespace MJCZone.DapperMatic.TypeMapping;

/// <summary>
/// Provides global initialization for DapperMatic query compatibility.
/// Call Initialize() once at application startup to enable DmColumn attribute mapping
/// and custom type handlers for all IDbConnection instances.
/// </summary>
public static class DapperMaticTypeMapping
{
    private static int _initialized;
    private static DapperMaticMappingOptions _options = new();

    /// <summary>
    /// Initialize DapperMatic query compatibility for all IDbConnection instances.
    /// Call this ONCE during application startup (Program.cs, Startup.cs, etc.).
    /// This registers type handlers and configures Dapper's type mapping system globally.
    /// </summary>
    /// <param name="options">Optional configuration options. If null, defaults are used.</param>
    /// <remarks>
    /// This method is thread-safe and can only execute once. Subsequent calls are ignored.
    /// After initialization:
    /// - DmColumn attributes will work in QueryAsync/ExecuteAsync
    /// - Modern C# records with parameterized constructors will be supported
    /// - Custom type handlers for enums, JSON, arrays, etc. will be registered
    /// </remarks>
    public static void Initialize(DapperMaticMappingOptions? options = null)
    {
        // Thread-safe, one-time initialization
        if (Interlocked.CompareExchange(ref _initialized, 1, 0) == 1)
        {
            return; // Already initialized
        }

        _options = options ?? new DapperMaticMappingOptions();

        // Register provider-agnostic handlers (90% of types)
        RegisterCoreHandlers();

        // Register smart handlers with per-provider optimizations
        RegisterSmartHandlers();

        // Register PostgreSQL-specific handlers
        RegisterPostgreSqlHandlers();

        // Register native spatial handlers (optional - if assemblies available)
        RegisterNativeSpatialHandlers();

        // Set fallback type mapper for DmColumn attribute detection
        SqlMapper.TypeMapProvider = type => new DmColumnFallbackMapper(type, _options);
    }

    /// <summary>
    /// Registers provider-agnostic type handlers that work on ALL database providers.
    /// Uses JSON serialization for portability across SQL Server, MySQL, PostgreSQL, SQLite.
    /// </summary>
    private static void RegisterCoreHandlers()
    {
        // XML - provider-agnostic (all DBs store as text/string)
        TryAddTypeHandler(new XDocumentTypeHandler());

        // JSON types - work on ALL providers
        TryAddTypeHandler(new JsonDocumentTypeHandler());
        // TryAddTypeHandler(new JsonElementTypeHandler());
        // TryAddTypeHandler(new JsonNodeTypeHandler());

        // Dictionaries - JSON on ALL providers
        TryAddTypeHandler(new DictionaryTypeHandler<string, string>());
        TryAddTypeHandler(new DictionaryTypeHandler<string, object>());

        // Lists - JSON on ALL providers
        TryAddTypeHandler(new ListTypeHandler<string>());

        // Read-only collections
        // TryAddTypeHandler(new ReadOnlyListTypeHandler<string>());

        // Object/custom classes - JSON on ALL providers
        // TryAddTypeHandler(new ObjectTypeHandler());
    }

    /// <summary>
    /// Registers smart handlers that auto-detect provider at runtime.
    /// Uses best strategy per provider: PostgreSQL gets native types, others get JSON.
    /// </summary>
    private static void RegisterSmartHandlers()
    {
        // Smart array handlers - auto-detect provider, use best strategy
        // PostgreSQL: Native arrays (text[], int4[], etc.) - 10-50x faster
        // Other providers: JSON arrays - portable, works everywhere

        // Primitive arrays
        TryAddTypeHandler(new SmartArrayTypeHandler<string>());
        TryAddTypeHandler(new SmartArrayTypeHandler<int>());
        TryAddTypeHandler(new SmartArrayTypeHandler<long>());
        TryAddTypeHandler(new SmartArrayTypeHandler<short>());
        TryAddTypeHandler(new SmartArrayTypeHandler<bool>());
        TryAddTypeHandler(new SmartArrayTypeHandler<byte>());

        // Decimal arrays
        TryAddTypeHandler(new SmartArrayTypeHandler<double>());
        TryAddTypeHandler(new SmartArrayTypeHandler<float>());
        TryAddTypeHandler(new SmartArrayTypeHandler<decimal>());

        // Temporal arrays
        TryAddTypeHandler(new SmartArrayTypeHandler<Guid>());
        TryAddTypeHandler(new SmartArrayTypeHandler<DateTime>());
        TryAddTypeHandler(new SmartArrayTypeHandler<DateTimeOffset>());
        TryAddTypeHandler(new SmartArrayTypeHandler<DateOnly>());
        TryAddTypeHandler(new SmartArrayTypeHandler<TimeOnly>());
        TryAddTypeHandler(new SmartArrayTypeHandler<TimeSpan>());
    }

    /// <summary>
    /// Registers PostgreSQL-specific type handlers for network, range, and spatial types.
    /// </summary>
    private static void RegisterPostgreSqlHandlers()
    {
        // Network types - smart handlers (PostgreSQL native, others as string)
        TryAddTypeHandler(new SmartIPAddressTypeHandler());
        TryAddTypeHandler(new SmartPhysicalAddressTypeHandler());
        TryAddTypeHandler(new SmartNpgsqlCidrTypeHandler());

        // Range types - smart handlers (PostgreSQL native, others as JSON)
        RegisterNpgsqlRangeHandler<int>();
        RegisterNpgsqlRangeHandler<long>();
        RegisterNpgsqlRangeHandler<decimal>();
        RegisterNpgsqlRangeHandler<DateTime>();
        RegisterNpgsqlRangeHandler<DateOnly>();
        RegisterNpgsqlRangeHandler<DateTimeOffset>();

        // Npgsql geometric types - smart handlers (PostgreSQL native, others as WKT)
        RegisterNpgsqlGeometricHandler<SmartNpgsqlPointTypeHandler>("NpgsqlTypes.NpgsqlPoint");
        RegisterNpgsqlGeometricHandler<SmartNpgsqlBoxTypeHandler>("NpgsqlTypes.NpgsqlBox");
        RegisterNpgsqlGeometricHandler<SmartNpgsqlCircleTypeHandler>("NpgsqlTypes.NpgsqlCircle");
        RegisterNpgsqlGeometricHandler<SmartNpgsqlLineTypeHandler>("NpgsqlTypes.NpgsqlLine");
        RegisterNpgsqlGeometricHandler<SmartNpgsqlLSegTypeHandler>("NpgsqlTypes.NpgsqlLSeg");
        RegisterNpgsqlGeometricHandler<SmartNpgsqlPathTypeHandler>("NpgsqlTypes.NpgsqlPath");
        RegisterNpgsqlGeometricHandler<SmartNpgsqlPolygonTypeHandler>("NpgsqlTypes.NpgsqlPolygon");
    }

    /// <summary>
    /// Registers a NpgsqlRange&lt;T&gt; type handler using reflection to avoid direct Npgsql dependency.
    /// </summary>
    private static void RegisterNpgsqlRangeHandler<T>()
        where T : struct
    {
        var rangeType = Type.GetType($"NpgsqlTypes.NpgsqlRange`1[[{typeof(T).AssemblyQualifiedName}]], Npgsql");
        if (rangeType != null)
        {
            // Create the generic handler type: SmartNpgsqlRangeTypeHandler<T>
            var handlerGenericType = typeof(SmartNpgsqlRangeTypeHandler<>);
            var handlerType = handlerGenericType.MakeGenericType(typeof(T));

            // Create an instance of the handler
            var handler = Activator.CreateInstance(handlerType) as SqlMapper.ITypeHandler;
            if (handler != null)
            {
                TryAddTypeHandler(handler, rangeType);
            }
        }
    }

    /// <summary>
    /// Registers a Npgsql geometric type handler using reflection to avoid direct Npgsql dependency.
    /// </summary>
    private static void RegisterNpgsqlGeometricHandler<THandler>(string npgsqlTypeName)
        where THandler : SqlMapper.ITypeHandler, new()
    {
        var geometricType = Type.GetType($"{npgsqlTypeName}, Npgsql");
        if (geometricType != null)
        {
            var handler = new THandler();
            TryAddTypeHandler(handler, geometricType);
        }
    }

    /// <summary>
    /// Registers native spatial type handlers when optional assemblies are available.
    /// </summary>
    private static void RegisterNativeSpatialHandlers()
    {
        // Optional: MySQL spatial types (if MySql.Data available)
        // if (IsMySqlDataAvailable())
        // {
        //     TryAddTypeHandler(new MySqlGeometryTypeHandler());
        // }

        // Optional: SQL Server spatial types (if Microsoft.SqlServer.Types available)
        // if (IsSqlServerTypesAvailable())
        // {
        //     TryAddTypeHandler(new SqlGeographyTypeHandler());
        //     TryAddTypeHandler(new SqlGeometryTypeHandler());
        //     TryAddTypeHandler(new SqlHierarchyIdTypeHandler());
        // }

        // Note: NetTopologySuite types are handled by provider libraries
        // - PostgreSQL: Npgsql has built-in NTS support
        // - MySQL: MySqlConnector (not MySql.Data) has built-in NTS support
        // - SQL Server: Use Microsoft.SqlServer.Types with NTS adapter
        // No custom handlers needed for NTS types in most cases
    }

    /// <summary>
    /// Tries to add a type handler, respecting the configured precedence strategy.
    /// </summary>
    private static void TryAddTypeHandler<T>(SqlMapper.TypeHandler<T> handler)
    {
        try
        {
            switch (_options.HandlerPrecedence)
            {
                case TypeHandlerPrecedence.SkipIfExists:
                    // Check if handler exists, skip if it does
                    // Note: Dapper doesn't expose a way to check, so we try-catch
                    SqlMapper.AddTypeHandler(handler);
                    break;

                case TypeHandlerPrecedence.OverrideExisting:
                    SqlMapper.RemoveTypeMap(typeof(T)); // Remove if exists
                    SqlMapper.AddTypeHandler(handler);
                    break;

                case TypeHandlerPrecedence.ThrowIfExists:
                    // This will throw if handler already exists
                    SqlMapper.AddTypeHandler(handler);
                    break;
            }
        }
        catch (ArgumentException)
        {
            // Handler already exists
            if (_options.HandlerPrecedence == TypeHandlerPrecedence.ThrowIfExists)
            {
                throw;
            }
            // Otherwise skip silently
        }
    }

    /// <summary>
    /// Tries to add a type handler for a specific type, respecting the configured precedence strategy.
    /// </summary>
    private static void TryAddTypeHandler(SqlMapper.ITypeHandler handler, Type type)
    {
        try
        {
            switch (_options.HandlerPrecedence)
            {
                case TypeHandlerPrecedence.SkipIfExists:
                    // Check if handler exists, skip if it does
                    // Note: Dapper doesn't expose a way to check, so we try-catch
                    SqlMapper.AddTypeHandler(type, handler);
                    break;

                case TypeHandlerPrecedence.OverrideExisting:
                    SqlMapper.RemoveTypeMap(type); // Remove if exists
                    SqlMapper.AddTypeHandler(type, handler);
                    break;

                case TypeHandlerPrecedence.ThrowIfExists:
                    // This will throw if handler already exists
                    SqlMapper.AddTypeHandler(type, handler);
                    break;
            }
        }
        catch (ArgumentException)
        {
            // Handler already exists
            if (_options.HandlerPrecedence == TypeHandlerPrecedence.ThrowIfExists)
            {
                throw;
            }
            // Otherwise skip silently
        }
    }
}
