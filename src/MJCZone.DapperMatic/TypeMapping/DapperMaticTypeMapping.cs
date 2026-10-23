// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using System.Net.NetworkInformation;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Dapper;
using MJCZone.DapperMatic.TypeMapping.Handlers;

namespace MJCZone.DapperMatic.TypeMapping;

/// <summary>
/// Provides global initialization for DapperMatic query compatibility.
/// Call Initialize() once at application startup to enable DmColumn attribute mapping
/// and custom type handlers for all IDbConnection instances.
/// </summary>
public static partial class DapperMaticTypeMapping
{
    private static int _initialized;
    private static DapperMaticMappingOptions _options = new();

    /// <summary>
    /// Gets the options used for values stored as JSON.
    /// </summary>
    internal static JsonSerializerOptions JsonOptions { get; private set; } = new();

    /// <summary>
    /// Gets a value indicating whether <see cref="Initialize"/> has run.
    /// </summary>
    internal static bool IsInitialized => Volatile.Read(ref _initialized) == 1;

    /// <summary>
    /// Gets the options <see cref="Initialize"/> ran with.
    /// </summary>
    internal static DapperMaticMappingOptions Options => _options;

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
        JsonOptions = _options.JsonSerializerOptions ?? new JsonSerializerOptions();

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

        // Handlers for the property types of the application's models: those already built for DDL, and those in
        // the application's assemblies. Dapper never asks before writing a parameter, so they are registered now.
        RegisterKnownModels();
        if (_options.ScanEntryAssembly)
        {
            RegisterEntryAssemblies();
        }
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
        RegisterJsonHandler(typeof(JsonElement));
        RegisterJsonHandler(typeof(JsonNode));
        RegisterJsonHandler(typeof(JsonObject));
        RegisterJsonHandler(typeof(JsonArray));
        RegisterJsonHandler(typeof(JsonValue));

        // Collections and dictionaries of common element types - JSON on ALL providers. Those of the
        // application's own types are registered from its models.
        RegisterCommonCollectionHandlers();

        // Values no driver accepts, stored as their text
        TryAddTypeHandler(
            new StringValueTypeHandler<Uri>(u => u.OriginalString, s => new Uri(s, UriKind.RelativeOrAbsolute))
        );
        TryAddTypeHandler(new StringValueTypeHandler<Version>(v => v.ToString(), Version.Parse));
        TryAddTypeHandler(new StringValueTypeHandler<IPEndPoint>(e => e.ToString(), IPEndPoint.Parse));
        TryAddTypeHandler(new StringValueTypeHandler<char[]>(c => new string(c), s => s.ToCharArray()));

        // Numbers some drivers do not accept. Dapper writes the unsigned types itself unless its own mapping for
        // them is removed, so it is.
        RegisterNumericHandler<sbyte>();
        RegisterNumericHandler<ushort>();
        RegisterNumericHandler<uint>();
        RegisterNumericHandler<ulong>();
        RegisterNumericHandler<Half>();
        RegisterNumericHandler<Int128>();
        RegisterNumericHandler<UInt128>();
        RegisterNumericHandler<BigInteger>();

        // Values SQLite drivers return as another type. Dapper still writes these itself.
        var guidHandler = new SmartGuidTypeHandler();
        TryAddTypeHandler(guidHandler, typeof(Guid));
        TryAddTypeHandler(guidHandler, typeof(Guid?));
        var timeSpanHandler = new SmartTimeSpanTypeHandler();
        TryAddTypeHandler(timeSpanHandler, typeof(TimeSpan));
        TryAddTypeHandler(timeSpanHandler, typeof(TimeSpan?));
        var decimalHandler = new SmartDecimalTypeHandler();
        TryAddTypeHandler(decimalHandler, typeof(decimal));
        TryAddTypeHandler(decimalHandler, typeof(decimal?));
    }

    /// <summary>
    /// Registers a handler for a number that some drivers do not accept, for it and its nullable form.
    /// </summary>
    private static void RegisterNumericHandler<T>()
        where T : struct
    {
        var handler = new NumericConversionTypeHandler<T>();
        TryAddTypeHandler(handler, typeof(T), replaceDapperMapping: true);
        TryAddTypeHandler(handler, typeof(T?), replaceDapperMapping: true);
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

        // Arrays of simple values keep Dapper's own behaviour unless asked to be JSON: a handler would stop them
        // expanding for IN lists. PostgreSQL stores them natively either way. No handler for byte[] in any case:
        // every provider stores it natively as binary.
        if (_options.StoreSimpleCollectionsAsJson)
        {
            foreach (var element in SmartArrayElementTypes)
            {
                RegisterSmartArrayHandler(element);
            }
        }
        else
        {
            // The exception: Npgsql reads timestamptz[] as DateTime[], which Dapper cannot put in a
            // DateTimeOffset[] without a handler. An IN list of instants is rare enough to give up.
            RegisterSmartArrayHandler(typeof(DateTimeOffset));
        }

        // Scalar temporal type handlers with Npgsql 9.x compatibility
        // Register for both nullable and non-nullable versions
        var dateOnlyHandler = new SmartScalarDateTimeTypeHandler<DateOnly>();
#pragma warning disable CA2263 // Use generic overload - non-generic required for Dapper registration
        TryAddTypeHandler(dateOnlyHandler, typeof(DateOnly), replaceDapperMapping: true);
        TryAddTypeHandler(dateOnlyHandler, typeof(DateOnly?), replaceDapperMapping: true);

        var timeOnlyHandler = new SmartScalarDateTimeTypeHandler<TimeOnly>();
        TryAddTypeHandler(timeOnlyHandler, typeof(TimeOnly), replaceDapperMapping: true);
        TryAddTypeHandler(timeOnlyHandler, typeof(TimeOnly?), replaceDapperMapping: true);

        var dateTimeOffsetHandler = new SmartScalarDateTimeTypeHandler<DateTimeOffset>();
        TryAddTypeHandler(dateTimeOffsetHandler, typeof(DateTimeOffset), replaceDapperMapping: true);
        TryAddTypeHandler(dateTimeOffsetHandler, typeof(DateTimeOffset?), replaceDapperMapping: true);
#pragma warning restore CA2263
    }

    /// <summary>
    /// Registers PostgreSQL-specific type handlers for network, range, and spatial types.
    /// </summary>
    private static void RegisterPostgreSqlHandlers()
    {
        // Network types - smart handlers (PostgreSQL native, others as string)
        TryAddTypeHandler(new SmartIPAddressTypeHandler());
        TryAddTypeHandler(new SmartPhysicalAddressTypeHandler());
        TryAddTypeHandler(new SmartIPNetworkTypeHandler());

        // A handler for object, so it is registered for NpgsqlCidr only: registered generically, it would
        // receive every object-typed member.
        RegisterNpgsqlTypeHandler<SmartNpgsqlCidrTypeHandler>("NpgsqlTypes.NpgsqlCidr");

        // Range types - smart handlers (PostgreSQL native, others as JSON)
        RegisterNpgsqlRangeHandler<int>();
        RegisterNpgsqlRangeHandler<long>();
        RegisterNpgsqlRangeHandler<decimal>();
        RegisterNpgsqlRangeHandler<DateTime>();
        RegisterNpgsqlRangeHandler<DateOnly>();
        RegisterNpgsqlRangeHandler<DateTimeOffset>();

        // Npgsql geometric types - smart handlers (PostgreSQL native, others as WKT)
        RegisterNpgsqlTypeHandler<SmartNpgsqlPointTypeHandler>("NpgsqlTypes.NpgsqlPoint");
        RegisterNpgsqlTypeHandler<SmartNpgsqlBoxTypeHandler>("NpgsqlTypes.NpgsqlBox");
        RegisterNpgsqlTypeHandler<SmartNpgsqlCircleTypeHandler>("NpgsqlTypes.NpgsqlCircle");
        RegisterNpgsqlTypeHandler<SmartNpgsqlLineTypeHandler>("NpgsqlTypes.NpgsqlLine");
        RegisterNpgsqlTypeHandler<SmartNpgsqlLSegTypeHandler>("NpgsqlTypes.NpgsqlLSeg");
        RegisterNpgsqlTypeHandler<SmartNpgsqlPathTypeHandler>("NpgsqlTypes.NpgsqlPath");
        RegisterNpgsqlTypeHandler<SmartNpgsqlPolygonTypeHandler>("NpgsqlTypes.NpgsqlPolygon");
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
    /// Registers a handler for an Npgsql type, found by reflection to avoid a direct Npgsql dependency.
    /// </summary>
    private static void RegisterNpgsqlTypeHandler<THandler>(string npgsqlTypeName)
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
        // NetTopologySuite geometry types - smart handlers
        // PostgreSQL/MySQL (MySqlConnector): Native NTS support via provider libraries (pass through)
        // SQL Server/SQLite/MySQL (MySql.Data): WKB conversion via reflection
        // NOTE: SQL Server requires query modifications (use geometry::STGeomFromWKB for INSERT, .STAsBinary() for SELECT)
        RegisterNtsGeometryHandlers();

        // MySQL spatial types - smart handler (MySQL native, others as WKB byte array)
        RegisterMySqlGeometryHandler();
    }

    /// <summary>
    /// Registers MySqlGeometry type handler using reflection to avoid direct MySql.Data dependency.
    /// </summary>
    private static void RegisterMySqlGeometryHandler()
    {
        // Try MySql.Data.MySqlClient.MySqlGeometry
        var geometryType = Type.GetType("MySql.Data.MySqlClient.MySqlGeometry, MySql.Data");
        if (geometryType == null)
        {
            // Try MySqlConnector.MySqlGeometry
            geometryType = Type.GetType("MySqlConnector.MySqlGeometry, MySqlConnector");
        }

        if (geometryType != null)
        {
            var handler = new SmartMySqlGeometryTypeHandler();
            TryAddTypeHandler(handler, geometryType);
        }
    }

    /// <summary>
    /// Registers NetTopologySuite geometry type handlers using reflection to avoid direct NTS dependency.
    /// Supports all NTS geometry types with provider-specific optimizations.
    /// </summary>
    private static void RegisterNtsGeometryHandlers()
    {
        // Create a single handler instance for all NTS geometry types
        var handler = new SmartNtsGeometryTypeHandler();

        // Register for all NTS geometry types
        var ntsGeometryTypes = new[]
        {
            "NetTopologySuite.Geometries.Geometry, NetTopologySuite",
            "NetTopologySuite.Geometries.Point, NetTopologySuite",
            "NetTopologySuite.Geometries.LineString, NetTopologySuite",
            "NetTopologySuite.Geometries.Polygon, NetTopologySuite",
            "NetTopologySuite.Geometries.MultiPoint, NetTopologySuite",
            "NetTopologySuite.Geometries.MultiLineString, NetTopologySuite",
            "NetTopologySuite.Geometries.MultiPolygon, NetTopologySuite",
            "NetTopologySuite.Geometries.GeometryCollection, NetTopologySuite",
        };

        foreach (var typeName in ntsGeometryTypes)
        {
            var geometryType = Type.GetType(typeName, throwOnError: false);
            if (geometryType != null)
            {
                TryAddTypeHandler(handler, geometryType);
            }
        }
    }

    /// <summary>
    /// Adds a type handler, respecting the configured precedence strategy.
    /// </summary>
    private static void TryAddTypeHandler<T>(SqlMapper.TypeHandler<T> handler, bool replaceDapperMapping = false) =>
        TryAddTypeHandler(handler, typeof(T), replaceDapperMapping);

    /// <summary>
    /// Adds a type handler for a specific type, respecting the configured precedence strategy.
    /// </summary>
    /// <param name="handler">The handler.</param>
    /// <param name="type">The type it handles.</param>
    /// <param name="replaceDapperMapping">
    /// Whether to remove Dapper's own mapping for the type. Dapper writes the types it maps itself, such as
    /// <see cref="ushort"/>, without consulting a handler, and reads them through one; a handler that must also
    /// write needs the mapping gone.
    /// </param>
    private static void TryAddTypeHandler(SqlMapper.ITypeHandler handler, Type type, bool replaceDapperMapping = false)
    {
        if (SqlMapper.HasTypeHandler(type))
        {
            switch (_options.HandlerPrecedence)
            {
                case TypeHandlerPrecedence.SkipIfExists:
                    return;
                case TypeHandlerPrecedence.ThrowIfExists:
                    throw new InvalidOperationException(
                        $"A Dapper type handler for {type} is already registered, and HandlerPrecedence is ThrowIfExists."
                    );
            }
        }

        if (replaceDapperMapping)
        {
            SqlMapper.RemoveTypeMap(type);
        }

        SqlMapper.AddTypeHandler(type, handler);
    }
}
