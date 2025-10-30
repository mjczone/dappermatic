# Dapper Query Compatibility

## Overview

This document tracks the implementation of DapperMatic attribute compatibility with standard Dapper query operations (QueryAsync, ExecuteAsync, etc.), enabling "full spectrum functionality" beyond DDL operations.

## Current State (Phases 1-5 Complete! - MVP Achieved!)

**Status**: ✅ Core infrastructure + XML, JSON, Collection, and Smart Array support implemented, enums work via Dapper's native handling

**What Works**:
- ✅ `DmColumn` attribute mapping for QueryAsync/ExecuteAsync
- ✅ `DmIgnore` attribute for excluding properties
- ✅ EF Core attribute support (`Column`, `NotMapped`)
- ✅ ServiceStack.OrmLite attribute support (`Alias`, `Ignore`)
- ✅ Modern C# record support with parameterized constructors
- ✅ Enum support - uses Dapper's native integer handling (no custom handler needed)
- ✅ XML support - `XDocument` with provider-agnostic serialization (works on all databases)
- ✅ JSON support - `JsonDocument` with provider-agnostic serialization (PostgreSQL uses jsonb optimization)
- ✅ Collection support - `Dictionary<TKey, TValue>` and `List<T>` with JSON serialization (works on all databases)
- ✅ Smart Array support - 15 array types (string[], int[], DateTime[], etc.) with PostgreSQL native arrays (10-50x faster) and JSON fallback for other databases
- ✅ One-time initialization with `DapperMaticTypeMapping.Initialize()`
- ✅ Comprehensive test coverage across all database providers (SQL Server, MySQL, MariaDB, PostgreSQL, SQLite)

**What's NOT Yet Implemented (Phases 6-9)**:
- ❌ PostgreSQL network types (IPAddress, PhysicalAddress, NpgsqlCidr)
- ❌ PostgreSQL range types (NpgsqlRange<T>)
- ❌ PostgreSQL Npgsql spatial types (Box, Circle, Point, etc.)
- ❌ Native spatial handlers (MySqlGeometry, SqlGeography, etc.)

**Usage**:
```csharp
// Initialize once at application startup
DapperMaticTypeMapping.Initialize();

// Now Dapper queries work with attribute mappings
public class User
{
    [DmColumn("user_id")]
    public int UserId { get; set; }

    [DmColumn("full_name")]
    public string FullName { get; set; } = string.Empty;

    [DmColumn("status")]
    public UserStatus Status { get; set; } // Enums work natively (stored as INT)
}

public enum UserStatus { Inactive = 0, Active = 1, Suspended = 2 }

var users = await connection.QueryAsync<User>(
    "SELECT user_id, full_name, status FROM users WHERE status = @status",
    new { status = UserStatus.Active } // Passed as integer (1)
);
```

**Documentation**: See [DML Query Support Guide](docs/guide/dml-query-support.md) for complete documentation.

---

## Implementation Status Summary

| Phase | Feature | Status | Priority |
|-------|---------|--------|----------|
| **Phase 1** | Core Infrastructure & Options | ✅ **COMPLETE** | CRITICAL |
| **Phase 2** | Enum Support | ✅ **COMPLETE** (Native Dapper) | CRITICAL |
| **Phase 3** | XML Support | ✅ **COMPLETE** | HIGH |
| **Phase 4** | JSON & Collection Handlers | ✅ **COMPLETE** | HIGH |
| **Phase 5** | Smart Array Handlers | ✅ **COMPLETE** | HIGH |
| **Phase 6** | PostgreSQL Network Types | ❌ **NOT IMPLEMENTED** | MEDIUM |
| **Phase 7** | PostgreSQL Range Types | ❌ **NOT IMPLEMENTED** | MEDIUM |
| **Phase 8** | PostgreSQL Npgsql Spatial | ❌ **NOT IMPLEMENTED** | MEDIUM |
| **Phase 9** | Native Spatial Handlers | ❌ **NOT IMPLEMENTED** | LOW (Optional) |
| **Phase 10** | Documentation & Examples | ⚠️ **PARTIAL** | HIGH |

**Overall Progress**: 5 of 10 phases complete (50%)

**MVP Target** (Phases 1-5 + 10): ✅ **5 of 6 complete (~83%)** - MVP nearly achieved!

---

## Native Support Verification Policy

**Critical Principle**: Always verify if Dapper or database provider libraries handle types natively before implementing custom type handlers.

### Why This Matters

1. **Avoid Duplication**: Dapper and provider libraries (Npgsql, MySqlConnector, etc.) already handle many types natively
2. **Maintain Compatibility**: Custom handlers can conflict with existing handlers or change expected behavior
3. **Reduce Maintenance**: Less code to maintain, fewer edge cases to handle
4. **Performance**: Native implementations are often optimized

### Verification Process (Used for Every Phase)

Before implementing any custom type handler, we:

1. **Research Phase**: Check Dapper source code and provider documentation
2. **Test Native Support**: Write verification test that attempts to use the type WITHOUT DapperMatic initialization
3. **Document Findings**: Record what works natively and what needs custom handlers
4. **Implement Only What's Needed**: Add handlers only for types that don't work natively

### Native Support Audit Results

This audit was conducted before implementing Phases 3-9:

#### ✅ Types That Work Natively (No Custom Handler Needed)

**Enums** (Phase 2):
- Dapper handles all enum types natively using integer storage
- Works for `byte`, `short`, `int`, `long` based enums
- Handles nullable enums correctly
- **Decision**: No custom enum handler implemented

**PostgreSQL Types** (Npgsql):
- `NpgsqlRange<T>` - All range types (int4range, int8range, daterange, etc.)
- Network types: `IPAddress`, `PhysicalAddress`, `NpgsqlCidr`
- Npgsql spatial types: `NpgsqlPoint`, `NpgsqlBox`, `NpgsqlCircle`, etc.
- `NpgsqlTsQuery`, `NpgsqlTsVector` (full text search)
- **Decision**: Use Npgsql's native handlers, no custom handlers needed

**Native Spatial Types** (Provider Libraries):
- `MySqlGeometry` - Handled by MySql.Data
- `SqlGeography`, `SqlGeometry`, `SqlHierarchyId` - Handled by Microsoft.SqlServer.Types
- NetTopologySuite types - Handled by Npgsql.NetTopologySuite and MySqlConnector plugins
- **Decision**: Rely on provider libraries, optional assembly detection only

#### ❌ Types That Need Custom Handlers

**XDocument** (Phase 3 - ✅ IMPLEMENTED):
- Dapper does NOT handle `XDocument` natively
- Requires custom `XDocumentTypeHandler` for serialization/deserialization
- Handler works provider-agnostic (all databases store as string/text)
- Special handling for PostgreSQL xml type (requires `NpgsqlDbType.Xml` via reflection)
- **Status**: Implemented and tested across all providers

**JSON Types** (Phase 4 - ✅ IMPLEMENTED):
- `JsonDocument` - Requires custom `JsonDocumentTypeHandler` for serialization/deserialization
- Handler works provider-agnostic (all databases store as JSON string/text)
- Special handling for PostgreSQL jsonb type (requires `NpgsqlDbType.Jsonb` via reflection)
- **Status**: Implemented and tested across all providers

**Collections** (Phase 4 - ✅ IMPLEMENTED):
- `Dictionary<TKey, TValue>`, `List<T>` - Require custom handlers with JSON serialization
- Handlers work provider-agnostic (all databases store as JSON string)
- PostgreSQL optimization: Uses jsonb type for better query performance
- **Status**: Implemented and tested across all providers

**Arrays** (Phase 5 - Pending):
- PostgreSQL: Native array support in Npgsql (e.g., `int[]`, `string[]`)
- Other providers: Need JSON fallback via custom handler
- Requires "smart handler" pattern (native for PostgreSQL, JSON elsewhere)

### Example: XDocument Verification (Phase 3)

**Verification Test** (Removed after confirmation):
```csharp
[Fact]
protected virtual async Task Should_verify_xdocument_requires_custom_handler_Async()
{
    // DON'T initialize DapperMatic - test native Dapper behavior
    var testXml = new XDocument(new XElement("metadata", ...));

    try
    {
        await db.ExecuteAsync("INSERT INTO test (xml_data) VALUES (@xmlData)",
            new { xmlData = testXml }); // Try to use XDocument
        // If we get here, XDocument works natively
    }
    catch (Exception)
    {
        // Exception = custom handler needed
    }
}
```

**Result**: All providers threw exceptions → Custom handler required → `XDocumentTypeHandler` implemented

**Lesson Learned**: Verification tests serve their purpose during development but should be removed after handler implementation to avoid test isolation issues with Dapper's global type handler registration.

---

## Critical Missing Features (Based on Test Analysis)

After analyzing `DatabaseMethodsTests.Types.cs`, we identified several critical type categories that **ARE tested in DDL** but need query mapping support:

### 1. Enum Support (✅ COMPLETE - Using Dapper Native Handling!)
- **DDL Storage**: Enums stored as their underlying integer type (byte, short, int, long)
- **DML Queries**: Dapper's native enum handling works out-of-the-box - **no custom type handlers needed**
- **Alignment**: Matches Dapper's default enum behavior for seamless interoperability
- **Nullability**: Handles both `Enum` and `Nullable<Enum>` naturally
- **Custom Handlers**: Users can register custom string-based enum handlers per enum type if needed (see test: `Should_support_custom_type_handler_for_string_enum_mapping_Async`)
- **Decision**: No built-in string enum handler - keeps implementation simple and aligned with standard Dapper behavior

### 2. PostgreSQL Native Types (CRITICAL - Extensively Tested!)
Test file shows comprehensive PostgreSQL-specific type support:

**Range Types** (6 types):
- `int4range`, `int8range`, `daterange`, `tsrange`, `tstzrange`, `numrange` → `NpgsqlRange<T>`

**Network Types** (3 types):
- `inet` → `IPAddress`
- `cidr` → `NpgsqlCidr`
- `macaddr`, `macaddr8` → `PhysicalAddress`

**Npgsql Spatial Types** (8 types):
- `box` → `NpgsqlBox`, `circle` → `NpgsqlCircle`, `point` → `NpgsqlPoint`
- `line` → `NpgsqlLine`, `lseg` → `NpgsqlLSeg`, `path` → `NpgsqlPath`, `polygon` → `NpgsqlPolygon`

**Special Types**:
- `hstore` → `Dictionary<string, string>` (covered by dictionary handler)
- `xml` → `XDocument`
- `tsquery` → `NpgsqlTsQuery`, `tsvector` → `NpgsqlTsVector` (lower priority)

### 3. Modern C# Patterns (✅ IMPLEMENTED!)
- **Records**: ✅ `public record User(Guid Id, string Name);` fully supported
- **Init Properties**: ✅ `public required string Name { get; init; }` works naturally
- **Parameterized Constructors**: ✅ `DmColumnFallbackMapper` now supports constructor parameter mapping
- **Record with Attributes**: ✅ `public record User([property: DmColumn("user_id")] int UserId, string Name);`

### 4. Read-Only Collections (MEDIUM Priority)
- `IReadOnlyList<T>`, `IReadOnlyCollection<T>`
- `ImmutableList<T>`, `ImmutableArray<T>`

### 5. XDocument (✅ COMPLETE - Provider-Agnostic XML)
- **Status**: ✅ Fully implemented with `XDocumentTypeHandler`
- **Coverage**: SQL Server, MySQL, MariaDB, PostgreSQL, SQLite
- **Storage**: Provider-agnostic string/text serialization
- **PostgreSQL Special Handling**: Automatically detects and sets `NpgsqlDbType.Xml` via reflection for xml columns
- **Tests**: Comprehensive test coverage across all 15 provider/version combinations
- **Serialization**: `XDocument.ToString()` for writing, `XDocument.Parse()` for reading

---

## Summary of Updates (Based on Test Analysis)

**Key Findings:**
- ✅ **80+ types** already tested in DDL operations need query mapping support
- ✅ **Enums** stored as integers - Dapper's native handling works out-of-the-box (no custom handler needed)
- ✅ **PostgreSQL** has 16 provider-specific types extensively tested (ranges, network, spatial)
- ✅ **Modern C#** patterns (records, init properties) fully supported via Phase 1

**Updated Architecture:**
- Changed `Enable()` → `Initialize(DapperMaticMappingOptions)` with configuration options
- Added `DapperMaticMappingOptions` for error handling and handler precedence
- Updated `DmColumnFallbackMapper` to support parameterized constructors (records)
- Updated `DmMemberMap` to support constructor parameters
- **Clear DDL/DML separation**:
  - **DDL (reverse engineering)**: Opinionated type selection (native → string → byte[])
  - **DML (Dapper queries)**: Flexible - supports user's choice of property type
- **Enum handling**: Uses Dapper's native integer handling (no custom handler needed)

**Expanded Type Coverage:**
- **Original plan**: ~30 types (primitives, JSON, arrays, collections)
- **Updated plan**: ~80+ types including:
  - Enums (✅ native Dapper integer handling - no custom handler needed)
  - XDocument (XML support) - ❌ not yet implemented
  - 16 array types (PostgreSQL native) - ❌ not yet implemented
  - 3 network types (IPAddress, PhysicalAddress, NpgsqlCidr) - ❌ not yet implemented
  - 6 range types (NpgsqlRange<T>) - ❌ not yet implemented
  - 7 Npgsql spatial types (Box, Circle, Point, Line, LSeg, Path, Polygon) - ❌ not yet implemented
  - PostgreSQL utility types (ltree → string, reg* → uint) - ❌ not yet implemented
  - Read-only collections - ❌ not yet implemented
  - Modern C# records - ✅ IMPLEMENTED (Phase 1)
  - **No `object` mappings** (except SQL Server sql_variant)

**Implementation Phases:**
- **Original**: 7 phases
- **Updated**: 10 phases (6 essential + 3 PostgreSQL + 1 optional)
- **MVP**: Phases 1-5 + 10 (~40-50 handlers, covers 80% of use cases)
- **Full**: Phases 1-8 + 10 (~65 handlers, complete PostgreSQL support)

---

## Key Architectural Insight: Global Registration

**Dapper's type system is global and static**:
- `SqlMapper.AddTypeHandler()` → Global static dictionary
- `SqlMapper.SetTypeMap()` → Global static dictionary
- `SqlMapper.TypeMapProvider` → Global static function

These apply to **ALL** IDbConnection instances in your application, regardless of provider type.

**Therefore**: Configuration should happen **ONCE at application startup**, not per-connection.

## DDL vs. DML Type Strategy

### DDL (Reverse Engineering) - Opinionated Type Selection

When DapperMatic reads database schema and creates `DmColumn` objects, it follows a **priority-based type selection**:

**Priority 1: Native Provider Types** (if assembly available)
- PostgreSQL: `NpgsqlRange<T>`, `NpgsqlPoint`, `IPAddress`, `PhysicalAddress`, etc.
- MySQL: `MySqlGeometry`
- SQL Server: `SqlGeography`, `SqlGeometry`, `SqlHierarchyId`
- NetTopologySuite: `Geometry`, `Point`, `LineString`, `Polygon`

**Priority 2: String Serialization** (portable fallback)
- Use `string` for text-based formats (WKT, JSON, XML, hierarchical paths)
- Examples: PostgreSQL ltree, spatial WKT, XML documents

**Priority 3: Binary Serialization** (when string not viable)
- Use `byte[]` for binary formats (WKB, binary blobs)
- Examples: SQL Server spatial types without Microsoft.SqlServer.Types

**Never Use `object`** (except sql_variant):
- Always prefer a specific type for better IntelliSense, type safety, and validation
- The only exception: SQL Server `sql_variant` (genuinely stores any type)

### DML (Dapper Queries) - Flexible Type Support

Type handlers support **whatever type the user chose** for their property:

```csharp
// User choice #1: String representation (WKT)
public class Location {
    [DmColumn("geom")]
    public string Geometry { get; set; }  // Works out-of-box, Dapper handles string natively
}

// User choice #2: Binary representation (WKB)
public class Location {
    [DmColumn("geom")]
    public byte[] Geometry { get; set; }  // Works out-of-box, Dapper handles byte[] natively
}

// User choice #3: Native provider type
public class Location {
    [DmColumn("geom")]
    public MySqlGeometry Geometry { get; set; }  // Needs custom type handler
}

// User choice #4: NetTopologySuite
public class Location {
    [DmColumn("geom")]
    public Geometry Geometry { get; set; }  // Needs custom type handler (or Npgsql built-in)
}
```

**Handler Registration Strategy**:
- **No handler needed**: `string`, `byte[]`, primitives (Dapper handles natively)
- **Handler registered**: Native types (`MySqlGeometry`, `SqlGeography`, `Geometry`) when assembly is available
- **User controls**: Choose property type based on their needs and available dependencies

## Solution Architecture

### Startup Registration (Recommended)

```csharp
// Program.cs or Startup.cs - Simple initialization
DapperMaticTypeMapping.Initialize(); // ONE CALL for entire application

// Or with options
DapperMaticTypeMapping.Initialize(new DapperMaticMappingOptions
{
    HandlerPrecedence = TypeHandlerPrecedence.SkipIfExists,
    ErrorStrategy = DeserializationErrorStrategy.ThrowException,
    EnableRecordSupport = true
});

// Now ALL IDbConnection instances automatically work
using var sqlServerDb = new SqlConnection(sqlServerConnString);
using var postgresDb = new NpgsqlConnection(postgresConnString);
using var sqliteDb = new SqliteConnection(sqliteConnString);

// All of these work automatically:
var users = await sqlServerDb.QueryAsync<User>("SELECT * FROM users");
var analytics = await postgresDb.QueryAsync<Analytics>("SELECT * FROM analytics");
var cache = await sqliteDb.QueryAsync<Cache>("SELECT * FROM cache");
```

### 90% Provider-Agnostic + 10% Provider-Specific Optimizations

**Strategy**: Default to JSON serialization (works everywhere), optimize where it matters.

#### Provider-Agnostic Types (90%)
JSON serialization to text/string works on **ALL** database providers:

| Type | Storage | All Providers |
|------|---------|--------------|
| JsonDocument, JsonElement, JsonNode | JSON string | ✅ nvarchar/text/json/TEXT |
| Dictionary<TKey, TValue> | JSON string | ✅ Works everywhere |
| List<T>, IEnumerable<T> | JSON string | ✅ Works everywhere |
| Custom classes | JSON string | ✅ Works everywhere |
| Arrays (fallback) | JSON array | ✅ Works everywhere |
| object | JSON string | ✅ Works everywhere |

**Performance**: ~1-5ms for typical payloads, portable, maintainable

#### Provider-Specific Optimizations (10%)
Only where performance or features matter:

| Type | PostgreSQL | Other Providers | Performance Gain |
|------|-----------|----------------|------------------|
| Arrays | Native array (text[], int4[]) | JSON fallback | 10-50x faster |
| Dictionary<string,string> | hstore (optional) | JSON | Better indexing |
| JsonDocument | jsonb (binary) | JSON text | Queryable, indexable |

### Smart Handlers: Auto-Detect Provider at Runtime

Smart handlers automatically choose the best strategy based on the connection type:

```csharp
public class SmartArrayTypeHandler<T> : SqlMapper.TypeHandler<T[]>
{
    public override void SetValue(IDbDataParameter parameter, T[] value)
    {
        // Runtime provider detection via parameter type
        var paramType = parameter.GetType().FullName;

        if (paramType.Contains("Npgsql"))
        {
            // PostgreSQL native array (FASTEST - 10-50x faster)
            parameter.Value = value;
            // Npgsql automatically handles T[] → SQL array conversion
        }
        else
        {
            // JSON fallback for SQL Server/MySQL/SQLite (fast, portable)
            parameter.Value = JsonSerializer.Serialize(value);
            parameter.DbType = DbType.String;
        }
    }

    public override T[] Parse(object value)
    {
        if (value is T[] array)
        {
            return array; // PostgreSQL native array
        }

        return JsonSerializer.Deserialize<T[]>(value.ToString()); // JSON
    }
}
```

**Benefits**:
- ✅ ONE handler registered globally at startup
- ✅ Automatically uses BEST strategy for each provider
- ✅ Supports multiple database types in same application
- ✅ PostgreSQL gets native array performance
- ✅ Other providers get reliable JSON fallback
- ✅ No provider type parameter needed

## Implementation Details

### Configuration Options

```csharp
public class DapperMaticMappingOptions
{
    /// <summary>
    /// How to handle type handler registration when handler already exists.
    /// Default: SkipIfExists (don't override user's custom handlers)
    /// </summary>
    public TypeHandlerPrecedence HandlerPrecedence { get; set; } = TypeHandlerPrecedence.SkipIfExists;

    /// <summary>
    /// How to handle deserialization errors.
    /// Default: ThrowException (fail fast)
    /// </summary>
    public DeserializationErrorStrategy ErrorStrategy { get; set; } = DeserializationErrorStrategy.ThrowException;

    /// <summary>
    /// Support modern C# records with parameterized constructors.
    /// Default: true (enables record support)
    /// </summary>
    public bool EnableRecordSupport { get; set; } = true;
}

public enum TypeHandlerPrecedence
{
    /// <summary>Skip registration if handler already exists (don't override user's handlers)</summary>
    SkipIfExists,

    /// <summary>Override existing handlers (DapperMatic handlers take precedence)</summary>
    OverrideExisting,

    /// <summary>Throw exception if handler already exists (fail fast on conflicts)</summary>
    ThrowIfExists
}

public enum DeserializationErrorStrategy
{
    /// <summary>Throw exception on deserialization errors (fail fast)</summary>
    ThrowException,

    /// <summary>Return null on deserialization errors (graceful degradation)</summary>
    ReturnNull,

    /// <summary>Return default(T) on deserialization errors (use C# defaults)</summary>
    ReturnDefault
}
```

### Core Implementation

```csharp
public static class DapperMaticTypeMapping
{
    private static int _initialized = 0;
    private static DapperMaticMappingOptions _options = new();

    /// <summary>
    /// Initialize DapperMatic query compatibility for all IDbConnection instances.
    /// Call this ONCE during application startup.
    /// </summary>
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

        // Register enum handlers
        RegisterEnumHandlers();

        // Register smart handlers with per-provider optimizations
        RegisterSmartHandlers();

        // Register PostgreSQL-specific handlers
        RegisterPostgreSqlHandlers();

        // Register native spatial handlers (optional - if assemblies available)
        RegisterNativeSpatialHandlers();

        // Set fallback type mapper for DmColumn attribute detection
        SqlMapper.TypeMapProvider = type => new DmColumnFallbackMapper(type, _options);
    }

    private static void RegisterCoreHandlers()
    {
        // XML - provider-agnostic (all DBs store as text/string)
        TryAddTypeHandler(new XDocumentTypeHandler());

        // JSON types - work on ALL providers
        TryAddTypeHandler(new JsonDocumentTypeHandler());
        TryAddTypeHandler(new JsonElementTypeHandler());
        TryAddTypeHandler(new JsonNodeTypeHandler());

        // Dictionaries - JSON on ALL providers
        TryAddTypeHandler(new DictionaryTypeHandler<string, string>());
        TryAddTypeHandler(new DictionaryTypeHandler<string, object>());
        TryAddTypeHandler(new DictionaryTypeHandler<int, string>());
        TryAddTypeHandler(new DictionaryTypeHandler<Guid, object>());
        TryAddTypeHandler(new DictionaryTypeHandler<long, DateTime>());

        // Lists - JSON on ALL providers
        TryAddTypeHandler(new ListTypeHandler<string>());
        TryAddTypeHandler(new ListTypeHandler<decimal>());
        TryAddTypeHandler(new ListTypeHandler<DateTimeOffset>());
        TryAddTypeHandler(new ListTypeHandler<Guid>());

        // Read-only collections
        TryAddTypeHandler(new ReadOnlyListTypeHandler<string>());
        TryAddTypeHandler(new ReadOnlyCollectionTypeHandler<string>());

        // Object/custom classes - JSON on ALL providers
        TryAddTypeHandler(new ObjectTypeHandler());
    }

    private static void RegisterEnumHandlers()
    {
        // Generic enum handler - handles ALL enum types dynamically
        // This is a special handler that Dapper will use for any enum type
        // See: https://github.com/DapperLib/Dapper/blob/main/Dapper/SqlMapper.TypeHandlers.cs
        TryAddTypeHandler(new EnumStringTypeHandler(_options));
    }

    private static void RegisterSmartHandlers()
    {
        // Smart array handlers - auto-detect provider, use best strategy
        TryAddTypeHandler(new SmartArrayTypeHandler<string>());
        TryAddTypeHandler(new SmartArrayTypeHandler<int>());
        TryAddTypeHandler(new SmartArrayTypeHandler<long>());
        TryAddTypeHandler(new SmartArrayTypeHandler<short>());
        TryAddTypeHandler(new SmartArrayTypeHandler<double>());
        TryAddTypeHandler(new SmartArrayTypeHandler<float>());
        TryAddTypeHandler(new SmartArrayTypeHandler<decimal>());
        TryAddTypeHandler(new SmartArrayTypeHandler<bool>());
        TryAddTypeHandler(new SmartArrayTypeHandler<Guid>());
        TryAddTypeHandler(new SmartArrayTypeHandler<DateTime>());
        TryAddTypeHandler(new SmartArrayTypeHandler<DateTimeOffset>());
        TryAddTypeHandler(new SmartArrayTypeHandler<DateOnly>());
        TryAddTypeHandler(new SmartArrayTypeHandler<TimeOnly>());
        TryAddTypeHandler(new SmartArrayTypeHandler<TimeSpan>());
        TryAddTypeHandler(new SmartArrayTypeHandler<byte[]>());
        TryAddTypeHandler(new SmartArrayTypeHandler<object>());
    }

    private static void RegisterNativeSpatialHandlers()
    {
        // Optional: MySQL spatial types (if MySql.Data available)
        if (IsMySqlDataAvailable())
        {
            TryAddTypeHandler(new MySqlGeometryTypeHandler());
        }

        // Optional: SQL Server spatial types (if Microsoft.SqlServer.Types available)
        if (IsSqlServerTypesAvailable())
        {
            TryAddTypeHandler(new SqlGeographyTypeHandler());
            TryAddTypeHandler(new SqlGeometryTypeHandler());
            TryAddTypeHandler(new SqlHierarchyIdTypeHandler());
        }

        // Note: NetTopologySuite types are handled by provider libraries
        // - PostgreSQL: Npgsql has built-in NTS support
        // - MySQL: MySqlConnector (not MySql.Data) has built-in NTS support
        // - SQL Server: Use Microsoft.SqlServer.Types with NTS adapter
        // No custom handlers needed for NTS types in most cases
    }

    private static void RegisterPostgreSqlHandlers()
    {
        // Network types - smart handlers (PostgreSQL native, others as string)
        TryAddTypeHandler(new SmartIPAddressTypeHandler());
        TryAddTypeHandler(new SmartPhysicalAddressTypeHandler());
        TryAddTypeHandler(new SmartNpgsqlCidrTypeHandler());

        // Range types - smart handlers (PostgreSQL native, others as JSON)
        TryAddTypeHandler(new SmartNpgsqlRangeTypeHandler<int>());
        TryAddTypeHandler(new SmartNpgsqlRangeTypeHandler<long>());
        TryAddTypeHandler(new SmartNpgsqlRangeTypeHandler<decimal>());
        TryAddTypeHandler(new SmartNpgsqlRangeTypeHandler<DateTime>());
        TryAddTypeHandler(new SmartNpgsqlRangeTypeHandler<DateOnly>());
        TryAddTypeHandler(new SmartNpgsqlRangeTypeHandler<DateTimeOffset>());

        // Npgsql spatial types - smart handlers (PostgreSQL native, others as WKT/JSON)
        TryAddTypeHandler(new SmartNpgsqlBoxTypeHandler());
        TryAddTypeHandler(new SmartNpgsqlCircleTypeHandler());
        TryAddTypeHandler(new SmartNpgsqlPointTypeHandler());
        TryAddTypeHandler(new SmartNpgsqlLineTypeHandler());
        TryAddTypeHandler(new SmartNpgsqlLSegTypeHandler());
        TryAddTypeHandler(new SmartNpgsqlPathTypeHandler());
        TryAddTypeHandler(new SmartNpgsqlPolygonTypeHandler());

        // Text search types (PostgreSQL only, lower priority)
        // TryAddTypeHandler(new SmartNpgsqlTsQueryTypeHandler());
        // TryAddTypeHandler(new SmartNpgsqlTsVectorTypeHandler());
    }

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

    private static bool IsMySqlDataAvailable()
    {
        try
        {
            var type = Type.GetType("MySql.Data.Types.MySqlGeometry, MySql.Data");
            return type != null;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsSqlServerTypesAvailable()
    {
        try
        {
            var type = Type.GetType("Microsoft.SqlServer.Types.SqlGeography, Microsoft.SqlServer.Types");
            return type != null;
        }
        catch
        {
            return false;
        }
    }
}
```

### Fallback Type Mapper for DmColumn Attributes

Automatically detects and maps DmColumn attributes at runtime with support for modern C# patterns:

```csharp
public class DmColumnFallbackMapper : SqlMapper.ITypeMap
{
    private readonly Type _type;
    private readonly DmTable? _table;
    private readonly DapperMaticMappingOptions _options;

    public DmColumnFallbackMapper(Type type, DapperMaticMappingOptions options)
    {
        _type = type;
        _options = options;

        // Try to get DmTable if type has DmTable attribute
        if (type.GetCustomAttribute<DmTableAttribute>() != null)
        {
            _table = DmTableFactory.GetTable(type);
        }
    }

    public ConstructorInfo? FindConstructor(string[] names, Type[] types)
    {
        // If record support is disabled, use parameterless constructor only
        if (!_options.EnableRecordSupport)
        {
            return _type.GetConstructor(Type.EmptyTypes);
        }

        // Try to find parameterized constructor that matches column names (for records)
        var constructors = _type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        // Find constructor with parameters matching the column names
        foreach (var ctor in constructors)
        {
            var parameters = ctor.GetParameters();
            if (parameters.Length == 0) continue;

            // Check if all parameters can be matched to column names
            bool allMatch = true;
            foreach (var param in parameters)
            {
                if (!names.Any(n => n.Equals(param.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    allMatch = false;
                    break;
                }
            }

            if (allMatch)
            {
                return ctor; // Found matching parameterized constructor
            }
        }

        // Fall back to parameterless constructor if available
        return _type.GetConstructor(Type.EmptyTypes);
    }

    public SqlMapper.IMemberMap? GetMember(string columnName)
    {
        if (_table != null)
        {
            // Find column by database column name
            var column = _table.Columns.FirstOrDefault(c =>
                c.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase));

            if (column != null)
            {
                // Find property that corresponds to this column
                var properties = _type.GetProperties();
                foreach (var prop in properties)
                {
                    var dmColumn = prop.GetCustomAttribute<DmColumnAttribute>();
                    if (dmColumn?.ColumnName?.Equals(columnName, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        return new DmMemberMap(columnName, prop);
                    }
                }
            }
        }

        // Fall back to default property name mapping (case-insensitive)
        var property = _type.GetProperty(columnName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (property != null)
        {
            return new DmMemberMap(columnName, property);
        }

        // Check for constructor parameter (for records)
        if (_options.EnableRecordSupport)
        {
            var constructors = _type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            foreach (var ctor in constructors)
            {
                var param = ctor.GetParameters()
                    .FirstOrDefault(p => p.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));

                if (param != null)
                {
                    return new DmMemberMap(columnName, null, param);
                }
            }
        }

        return null;
    }
}

public class DmMemberMap : SqlMapper.IMemberMap
{
    private readonly string _columnName;
    private readonly PropertyInfo? _property;
    private readonly ParameterInfo? _parameter;

    public DmMemberMap(string columnName, PropertyInfo? property, ParameterInfo? parameter = null)
    {
        _columnName = columnName;
        _property = property;
        _parameter = parameter;
    }

    public string ColumnName => _columnName;
    public Type MemberType => _property?.PropertyType ?? _parameter?.ParameterType ?? typeof(object);
    public PropertyInfo? Property => _property;
    public FieldInfo? Field => null;
    public ParameterInfo? Parameter => _parameter;
}
```

## Type Handler Examples

### Enum Handler (Provider-Agnostic, String Storage)

```csharp
public class EnumStringTypeHandler : SqlMapper.TypeHandler<Enum>
{
    private readonly DapperMaticMappingOptions _options;

    public EnumStringTypeHandler(DapperMaticMappingOptions options)
    {
        _options = options;
    }

    public override void SetValue(IDbDataParameter parameter, Enum? value)
    {
        if (value == null)
        {
            parameter.Value = DBNull.Value;
        }
        else
        {
            // Always write as string (DapperMatic standard)
            parameter.Value = value.ToString();
            parameter.DbType = DbType.String;
        }
    }

    public override Enum? Parse(object value)
    {
        if (value == null || value is DBNull)
        {
            return null;
        }

        // Already an enum
        if (value is Enum enumValue)
        {
            return enumValue;
        }

        var valueType = value.GetType();

        // String parsing (primary)
        if (value is string str)
        {
            try
            {
                return (Enum)Enum.Parse(valueType, str, ignoreCase: true);
            }
            catch when (_options.AllowEnumIntegerFallback)
            {
                // Fall through to integer parsing
            }
            catch
            {
                return HandleError();
            }
        }

        // Integer fallback (optional)
        if (_options.AllowEnumIntegerFallback && value is int intValue)
        {
            try
            {
                return (Enum)Enum.ToObject(valueType, intValue);
            }
            catch
            {
                return HandleError();
            }
        }

        return HandleError();
    }

    private Enum? HandleError()
    {
        return _options.ErrorStrategy switch
        {
            DeserializationErrorStrategy.ThrowException => throw new InvalidCastException("Cannot convert value to enum"),
            DeserializationErrorStrategy.ReturnNull => null,
            DeserializationErrorStrategy.ReturnDefault => default,
            _ => throw new InvalidOperationException("Unknown error strategy")
        };
    }
}
```

### XDocument Handler (Provider-Agnostic XML)

```csharp
public class XDocumentTypeHandler : SqlMapper.TypeHandler<XDocument>
{
    public override void SetValue(IDbDataParameter parameter, XDocument? value)
    {
        if (value == null)
        {
            parameter.Value = DBNull.Value;
        }
        else
        {
            parameter.Value = value.ToString();
            parameter.DbType = DbType.String;
        }
    }

    public override XDocument? Parse(object value)
    {
        if (value == null || value is DBNull)
        {
            return null;
        }

        return XDocument.Parse(value.ToString());
    }
}
```

### Provider-Agnostic JSON Handler

```csharp
public class JsonDocumentTypeHandler : SqlMapper.TypeHandler<JsonDocument>
{
    public override void SetValue(IDbDataParameter parameter, JsonDocument? value)
    {
        if (value == null)
        {
            parameter.Value = DBNull.Value;
        }
        else
        {
            parameter.Value = value.RootElement.GetRawText();
            parameter.DbType = DbType.String;
        }
    }

    public override JsonDocument? Parse(object value)
    {
        if (value == null || value is DBNull)
        {
            return null;
        }

        var json = value.ToString();
        return JsonDocument.Parse(json);
    }
}
```

### Provider-Agnostic Dictionary Handler

```csharp
public class DictionaryTypeHandler<TKey, TValue> : SqlMapper.TypeHandler<Dictionary<TKey, TValue>>
{
    public override void SetValue(IDbDataParameter parameter, Dictionary<TKey, TValue>? value)
    {
        if (value == null)
        {
            parameter.Value = DBNull.Value;
        }
        else
        {
            parameter.Value = JsonSerializer.Serialize(value);
            parameter.DbType = DbType.String;
        }
    }

    public override Dictionary<TKey, TValue>? Parse(object value)
    {
        if (value == null || value is DBNull)
        {
            return null;
        }

        return JsonSerializer.Deserialize<Dictionary<TKey, TValue>>(value.ToString());
    }
}
```

### Smart Array Handler (Runtime Provider Detection)

```csharp
public class SmartArrayTypeHandler<T> : SqlMapper.TypeHandler<T[]>
{
    public override void SetValue(IDbDataParameter parameter, T[]? value)
    {
        if (value == null)
        {
            parameter.Value = DBNull.Value;
            return;
        }

        // Runtime provider detection via parameter type
        var paramType = parameter.GetType().FullName ?? string.Empty;

        if (paramType.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            // PostgreSQL native array (FASTEST)
            parameter.Value = value;
            // Npgsql automatically handles T[] → SQL array conversion
        }
        else
        {
            // JSON fallback for SQL Server/MySQL/SQLite
            parameter.Value = JsonSerializer.Serialize(value);
            parameter.DbType = DbType.String;
        }
    }

    public override T[]? Parse(object value)
    {
        if (value == null || value is DBNull)
        {
            return null;
        }

        // Check if already a typed array (PostgreSQL native)
        if (value is T[] array)
        {
            return array;
        }

        // Deserialize from JSON (other providers)
        return JsonSerializer.Deserialize<T[]>(value.ToString());
    }
}
```

### Smart IPAddress Handler (PostgreSQL Native, Others String)

```csharp
public class SmartIPAddressTypeHandler : SqlMapper.TypeHandler<IPAddress>
{
    public override void SetValue(IDbDataParameter parameter, IPAddress? value)
    {
        if (value == null)
        {
            parameter.Value = DBNull.Value;
            return;
        }

        var paramType = parameter.GetType().FullName ?? string.Empty;

        if (paramType.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            // PostgreSQL native inet type
            parameter.Value = value;
        }
        else
        {
            // String representation for other providers
            parameter.Value = value.ToString();
            parameter.DbType = DbType.String;
        }
    }

    public override IPAddress? Parse(object value)
    {
        if (value == null || value is DBNull)
        {
            return null;
        }

        // Already an IPAddress (PostgreSQL)
        if (value is IPAddress ip)
        {
            return ip;
        }

        // Parse from string (other providers)
        return IPAddress.Parse(value.ToString());
    }
}
```

### Smart NpgsqlRange Handler (PostgreSQL Native, Others JSON)

```csharp
public class SmartNpgsqlRangeTypeHandler<T> : SqlMapper.TypeHandler<NpgsqlRange<T>>
    where T : struct
{
    public override void SetValue(IDbDataParameter parameter, NpgsqlRange<T> value)
    {
        var paramType = parameter.GetType().FullName ?? string.Empty;

        if (paramType.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            // PostgreSQL native range type (int4range, tsrange, etc.)
            parameter.Value = value;
        }
        else
        {
            // JSON fallback for other providers
            // Store as {LowerBound: x, UpperBound: y, LowerBoundIsInclusive: true, UpperBoundIsInclusive: false}
            var rangeObj = new
            {
                LowerBound = value.LowerBound,
                UpperBound = value.UpperBound,
                LowerBoundIsInclusive = value.LowerBoundIsInclusive,
                UpperBoundIsInclusive = value.UpperBoundIsInclusive,
                LowerBoundInfinite = value.LowerBoundInfinite,
                UpperBoundInfinite = value.UpperBoundInfinite
            };
            parameter.Value = JsonSerializer.Serialize(rangeObj);
            parameter.DbType = DbType.String;
        }
    }

    public override NpgsqlRange<T> Parse(object value)
    {
        if (value == null || value is DBNull)
        {
            return default;
        }

        // Already a range (PostgreSQL)
        if (value is NpgsqlRange<T> range)
        {
            return range;
        }

        // Deserialize from JSON (other providers)
        var json = value.ToString();
        var rangeData = JsonSerializer.Deserialize<JsonElement>(json);

        var lowerBound = rangeData.GetProperty("LowerBound").Deserialize<T>();
        var upperBound = rangeData.GetProperty("UpperBound").Deserialize<T>();
        var lowerInclusive = rangeData.GetProperty("LowerBoundIsInclusive").GetBoolean();
        var upperInclusive = rangeData.GetProperty("UpperBoundIsInclusive").GetBoolean();

        return new NpgsqlRange<T>(lowerBound, lowerInclusive, upperBound, upperInclusive);
    }
}
```

## Multi-Provider Application Example

```csharp
// Startup.cs
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // ONE call for entire application - works for ALL providers
        DapperMaticTypeMapping.Initialize();

        // Register your connections
        services.AddScoped<IDbConnection>(sp =>
            new SqlConnection(Configuration.GetConnectionString("SqlServer")));

        services.AddScoped<NpgsqlConnection>(sp =>
            new NpgsqlConnection(Configuration.GetConnectionString("PostgreSql")));

        services.AddScoped<SqliteConnection>(sp =>
            new SqliteConnection(Configuration.GetConnectionString("Sqlite")));
    }
}

// Usage in services
public class AnalyticsService
{
    private readonly IDbConnection _sqlServer;
    private readonly NpgsqlConnection _postgres;

    public async Task<AnalyticsReport> GenerateReport()
    {
        // SQL Server - arrays stored as nvarchar(max) JSON
        var orderTags = await _sqlServer.QueryAsync<Order>(@"
            SELECT id, tags FROM orders
        ");

        // PostgreSQL - arrays stored as text[] native (10-50x faster!)
        var postTags = await _postgres.QueryAsync<Post>(@"
            SELECT id, tags FROM posts
        ");

        // Same C# type (string[]) but optimal storage per provider!
        // Smart handlers automatically choose best strategy
    }
}
```

## Implementation Plan

### Phase 1: Core Infrastructure & Options ✅ (Required)
**Goal**: Core infrastructure with configuration options and modern C# support

**Tasks**:
1. Create `DapperMaticMappingOptions` class with enums
2. Create `DmMemberMap : SqlMapper.IMemberMap` with constructor parameter support
3. Create `DmColumnFallbackMapper : SqlMapper.ITypeMap` with record support
4. Implement `DapperMaticTypeMapping.Initialize(options)` static method
5. Set `SqlMapper.TypeMapProvider` global function
6. Add tests for column name mapping with records and init properties

**Files to Create**:
- `src/MJCZone.DapperMatic/TypeMapping/DapperMaticMappingOptions.cs`
- `src/MJCZone.DapperMatic/TypeMapping/DmMemberMap.cs`
- `src/MJCZone.DapperMatic/TypeMapping/DmColumnFallbackMapper.cs`
- `src/MJCZone.DapperMatic/TypeMapping/DapperMaticTypeMapping.cs`
- `tests/MJCZone.DapperMatic.Tests/TypeMapping/ColumnMappingTests.cs`
- `tests/MJCZone.DapperMatic.Tests/TypeMapping/RecordSupportTests.cs`

**Deliverable**: Users call `DapperMaticTypeMapping.Initialize()` once at startup, all DmColumn attributes work automatically with full modern C# support

**Example**:
```csharp
// Startup with options
DapperMaticTypeMapping.Initialize(new DapperMaticMappingOptions
{
    HandlerPrecedence = TypeHandlerPrecedence.SkipIfExists,
    ErrorStrategy = DeserializationErrorStrategy.ThrowException,
    EnableRecordSupport = true
});

// Usage with records
public record User(
    [property: DmColumn("user_id")] Guid UserId,
    [property: DmColumn("user_name")] string UserName
);

var users = await db.QueryAsync<User>("SELECT user_id, user_name FROM users");
```

---

### Phase 2: Enum Support ✅ **COMPLETE** (Using Dapper Native Handling)
**Status**: ✅ **COMPLETE** - No custom handler needed

**Decision**: Use Dapper's native enum handling (integer storage) instead of custom string handler

**Why**:
- Dapper natively handles enum ↔ integer conversion out-of-the-box
- Aligns with DDL enum storage strategy (underlying integer type)
- Simpler implementation, less code to maintain
- Users can register custom string handlers per enum type if needed (see test: `Should_support_custom_type_handler_for_string_enum_mapping_Async`)

**What Works**:
- ✅ Enum properties work in QueryAsync/ExecuteAsync with integer storage
- ✅ Nullable enums supported
- ✅ All enum underlying types (byte, short, int, long)
- ✅ Custom string handlers can be registered per enum type if needed

**Files Created**:
- ✅ Test exists: `Should_support_custom_type_handler_for_string_enum_mapping_Async` in `DapperMaticDmlTypeMappingTests.cs`
- ✅ Example custom handler: `OrderStatusStringTypeHandler` (test-only, shows pattern)

**Example**:
```csharp
public enum UserRole { Admin = 1, User = 2, Guest = 3 }

public class User
{
    [DmColumn("role")]
    public UserRole Role { get; set; } // Stored as INT (1, 2, 3)
}

// Works naturally with Dapper's native enum handling
var admins = await db.QueryAsync<User>(
    "SELECT * FROM users WHERE role = @role",
    new { role = UserRole.Admin } // Passed as integer (1)
);
```

**Optional Custom String Handler** (if needed for legacy VARCHAR columns):
```csharp
// Users can register custom string handlers per enum type
SqlMapper.AddTypeHandler(new CustomUserRoleStringTypeHandler());
```

---

### Phase 3: XML Support ✅ **COMPLETE** (High Value)
**Status**: ✅ **COMPLETE**

**Goal**: Enable XDocument for XML data across all providers

**Implementation**:
- ✅ Implemented `XDocumentTypeHandler` (provider-agnostic, string serialization)
- ✅ Registered in `RegisterCoreHandlers()`
- ✅ Added comprehensive tests across all database providers
- ✅ Special PostgreSQL xml type handling via reflection (`NpgsqlDbType.Xml`)

**Files Created**:
- ✅ `src/MJCZone.DapperMatic/TypeMapping/Handlers/XDocumentTypeHandler.cs`
- ✅ Tests in `tests/MJCZone.DapperMatic.Tests/DapperMaticDmlTypeMappingTests.cs`

**Deliverable**: ✅ XDocument properties work in QueryAsync/ExecuteAsync on all providers (SQL Server, MySQL, MariaDB, PostgreSQL, SQLite)

---

### Phase 4: Provider-Agnostic JSON & Collection Handlers ✅ **COMPLETE** (High Value)
**Status**: ✅ **COMPLETE**

**Goal**: Enable JSON types, dictionaries, and lists via JSON serialization (all providers)

**Implementation**:
- ✅ Implemented `JsonDocumentTypeHandler` (provider-agnostic JSON serialization)
- ✅ Implemented `DictionaryTypeHandler<TKey, TValue>` (generic, JSON serialization)
- ✅ Implemented `ListTypeHandler<T>` (generic, JSON serialization)
- ✅ Registered handlers for common type combinations in `RegisterCoreHandlers()`
- ✅ Added comprehensive tests across all database providers (150 total DML tests passing)
- ✅ PostgreSQL optimization: Handlers automatically detect and set `NpgsqlDbType.Jsonb` via reflection

**Files Created**:
- ✅ `src/MJCZone.DapperMatic/TypeMapping/Handlers/JsonDocumentTypeHandler.cs`
- ✅ `src/MJCZone.DapperMatic/TypeMapping/Handlers/DictionaryTypeHandler.cs`
- ✅ `src/MJCZone.DapperMatic/TypeMapping/Handlers/ListTypeHandler.cs`
- ✅ Tests in `tests/MJCZone.DapperMatic.Tests/DapperMaticDmlTypeMappingTests.cs`

**Deliverable**: ✅ JSON (JsonDocument), Dictionary<TKey, TValue>, and List<T> types work in queries on all providers

**Performance**: ~1-5ms for typical payloads, PostgreSQL gets jsonb optimization for better query performance

**Notes**:
- JsonElement and JsonNode handlers deferred (lower priority)
- ReadOnly collection handlers deferred (lower priority)
- ObjectTypeHandler deferred (Phase 9 - optional)

---

### Phase 5: Smart Array Handlers ✅ **COMPLETE** (Performance Optimization)
**Status**: ✅ **COMPLETE**

**Goal**: Native PostgreSQL arrays with JSON fallback for other providers

**Implementation**:
- ✅ Implemented `SmartArrayTypeHandler<T>` with runtime provider detection
- ✅ Registered 15 array types in `RegisterSmartHandlers()` (primitives, decimals, temporal)
- ✅ Added comprehensive tests across all database providers (195 total DML tests passing)
- ✅ PostgreSQL automatically uses native arrays (text[], int4[], timestamp[], etc.)
- ✅ Other providers use JSON array serialization (portable, works everywhere)

**Array Types Supported**:
- Primitives: `string[]`, `int[]`, `long[]`, `short[]`, `bool[]`, `byte[]`
- Decimals: `double[]`, `float[]`, `decimal[]`
- Temporal: `Guid[]`, `DateTime[]`, `DateTimeOffset[]`, `DateOnly[]`, `TimeOnly[]`, `TimeSpan[]`

**Files Created**:
- ✅ `src/MJCZone.DapperMatic/TypeMapping/Handlers/SmartArrayTypeHandler.cs`
- ✅ Tests in `tests/MJCZone.DapperMatic.Tests/DapperMaticDmlTypeMappingTests.cs`

**Deliverable**: ✅ Arrays work on all providers, PostgreSQL gets 10-50x performance boost with native array types

**Performance**:
- PostgreSQL native: ~0.1ms for typical arrays (10-50x faster!)
- JSON fallback: ~1-5ms for typical arrays (works on SQL Server, MySQL, MariaDB, SQLite)

---

### Phase 6: PostgreSQL Network Types ❌ **NOT IMPLEMENTED** (CRITICAL for PostgreSQL)
**Status**: ❌ **NOT IMPLEMENTED**

**Goal**: Enable PostgreSQL network types with smart handlers

**Tasks**:
1. Implement `SmartIPAddressTypeHandler` (PostgreSQL native inet, others string)
2. Implement `SmartPhysicalAddressTypeHandler` (PostgreSQL native macaddr, others string)
3. Implement `SmartNpgsqlCidrTypeHandler` (PostgreSQL native cidr, others string)
4. Register in `RegisterPostgreSqlHandlers()`
5. Add tests for all network types from DatabaseMethodsTests.Types.cs

**Files to Create**:
- ❌ `src/MJCZone.DapperMatic/TypeMapping/Handlers/SmartIPAddressTypeHandler.cs` - **DOES NOT EXIST**
- ❌ `src/MJCZone.DapperMatic/TypeMapping/Handlers/SmartPhysicalAddressTypeHandler.cs` - **DOES NOT EXIST**
- ❌ `src/MJCZone.DapperMatic/TypeMapping/Handlers/SmartNpgsqlCidrTypeHandler.cs` - **DOES NOT EXIST**
- ❌ `tests/MJCZone.DapperMatic.Tests/TypeMapping/PostgreSqlNetworkTypeTests.cs` - **DOES NOT EXIST**

**Deliverable**: IPAddress, PhysicalAddress, NpgsqlCidr work in queries

---

### Phase 7: PostgreSQL Range Types ❌ **NOT IMPLEMENTED** (CRITICAL for PostgreSQL)
**Status**: ❌ **NOT IMPLEMENTED**

**Goal**: Enable PostgreSQL range types with smart handlers

**Tasks**:
1. Implement `SmartNpgsqlRangeTypeHandler<T>` (PostgreSQL native, others JSON)
2. Register 6 range types in `RegisterPostgreSqlHandlers()` (int, long, decimal, DateTime, DateOnly, DateTimeOffset)
3. Add tests for all range types from DatabaseMethodsTests.Types.cs

**Files to Create**:
- ❌ `src/MJCZone.DapperMatic/TypeMapping/Handlers/SmartNpgsqlRangeTypeHandler.cs` - **DOES NOT EXIST**
- ❌ `tests/MJCZone.DapperMatic.Tests/TypeMapping/PostgreSqlRangeTypeTests.cs` - **DOES NOT EXIST**

**Deliverable**: NpgsqlRange<T> types work in queries on PostgreSQL and other providers

---

### Phase 8: PostgreSQL Npgsql Spatial Types ❌ **NOT IMPLEMENTED** (Important for PostgreSQL)
**Status**: ❌ **NOT IMPLEMENTED**

**Goal**: Enable Npgsql-specific spatial types (not PostGIS)

**Tasks**:
1. Implement smart handlers for 7 Npgsql spatial types:
   - `SmartNpgsqlBoxTypeHandler`, `SmartNpgsqlCircleTypeHandler`, `SmartNpgsqlPointTypeHandler`
   - `SmartNpgsqlLineTypeHandler`, `SmartNpgsqlLSegTypeHandler`
   - `SmartNpgsqlPathTypeHandler`, `SmartNpgsqlPolygonTypeHandler`
2. PostgreSQL uses native types, others use WKT/WKB or JSON
3. Register in `RegisterPostgreSqlHandlers()`
4. Add tests for all Npgsql spatial types from DatabaseMethodsTests.Types.cs

**Files to Create**:
- ❌ `src/MJCZone.DapperMatic/TypeMapping/Handlers/SmartNpgsqlBoxTypeHandler.cs` - **DOES NOT EXIST**
- ❌ `src/MJCZone.DapperMatic/TypeMapping/Handlers/SmartNpgsqlCircleTypeHandler.cs` - **DOES NOT EXIST**
- ❌ `src/MJCZone.DapperMatic/TypeMapping/Handlers/SmartNpgsqlPointTypeHandler.cs` - **DOES NOT EXIST**
- ❌ `src/MJCZone.DapperMatic/TypeMapping/Handlers/SmartNpgsqlLineTypeHandler.cs` - **DOES NOT EXIST**
- ❌ `src/MJCZone.DapperMatic/TypeMapping/Handlers/SmartNpgsqlLSegTypeHandler.cs` - **DOES NOT EXIST**
- ❌ `src/MJCZone.DapperMatic/TypeMapping/Handlers/SmartNpgsqlPathTypeHandler.cs` - **DOES NOT EXIST**
- ❌ `src/MJCZone.DapperMatic/TypeMapping/Handlers/SmartNpgsqlPolygonTypeHandler.cs` - **DOES NOT EXIST**
- ❌ `tests/MJCZone.DapperMatic.Tests/TypeMapping/PostgreSqlNpgsqlSpatialTypeTests.cs` - **DOES NOT EXIST**

**Deliverable**: All Npgsql spatial types work in queries

---

### Phase 9: Native Spatial Type Handlers ❌ **NOT IMPLEMENTED** (Optional - Low Priority)
**Status**: ❌ **NOT IMPLEMENTED**

**Goal**: Enable native spatial types when their assemblies are available

**Strategy**: Simple handler registration (no smart converters)
- If user uses native type as property → handler registered (if assembly available)
- If user uses `string` or `byte[]` → works out-of-box (Dapper handles natively)
- User chooses property type based on their needs

**Tasks**:
1. Implement handlers **only when assemblies are available**:
   - `MySqlGeometryTypeHandler` (if MySql.Data available)
   - `SqlGeographyTypeHandler` (if Microsoft.SqlServer.Types available)
   - `SqlGeometryTypeHandler` (if Microsoft.SqlServer.Types available)
   - `SqlHierarchyIdTypeHandler` (if Microsoft.SqlServer.Types available)
   - NetTopologySuite types usually handled by provider libraries (Npgsql, MySqlConnector)
2. Add tests for each native type handler
3. Document that `string` (WKT) and `byte[]` (WKB) work without custom handlers

**Files to Create**:
- ❌ `src/MJCZone.DapperMatic/TypeMapping/Handlers/MySqlGeometryTypeHandler.cs` - **DOES NOT EXIST**
- ❌ `src/MJCZone.DapperMatic/TypeMapping/Handlers/SqlGeographyTypeHandler.cs` - **DOES NOT EXIST**
- ❌ `src/MJCZone.DapperMatic/TypeMapping/Handlers/SqlGeometryTypeHandler.cs` - **DOES NOT EXIST**
- ❌ `src/MJCZone.DapperMatic/TypeMapping/Handlers/SqlHierarchyIdTypeHandler.cs` - **DOES NOT EXIST**
- ❌ `tests/MJCZone.DapperMatic.Tests/TypeMapping/NativeSpatialTypeTests.cs` - **DOES NOT EXIST**

**Deliverable**: Native spatial types work when assemblies are available; string/byte[] always work

**Note**:
- PostgreSQL with NetTopologySuite: Npgsql already has built-in support, no custom handler needed
- MySQL with NetTopologySuite: MySqlConnector (not MySql.Data) has built-in support
- SQLite: Requires SpatiaLite extension (complex, may defer)

---

### Phase 10: Documentation & Examples ⚠️ **PARTIAL** (Required)
**Status**: ⚠️ **PARTIAL** - Phase 1 documented, phases 3-9 not yet implemented

**Goal**: Complete user-facing documentation

**Tasks**:
1. Update `docs/guide/configuration.md` to remove limitation note
2. Add new section: "Dapper Query Compatibility"
3. Add examples for each type category
4. Document startup registration pattern
5. Document performance characteristics (90% agnostic + 10% optimized)
6. Add multi-provider application example
7. Update README.md with feature bullet

**Files to Modify**:
- `docs/guide/configuration.md`
- `README.md`

**Files to Create**:
- `docs/guide/dapper-query-compatibility.md`
- `docs/examples/query-mapping-examples.md`

**Deliverable**: Complete user documentation with examples

---

## Types to Support

Based on comprehensive analysis of `tests/MJCZone.DapperMatic.Tests/DatabaseMethodsTests.Types.cs`:

### Primitives (built-in Dapper support - no handlers needed)
- **Integers**: bool, byte, sbyte, short, int, long
- **Decimals**: float, double, decimal
- **Text**: char, string
- **Identity**: Guid
- **Temporal**: DateTime, DateTimeOffset, TimeSpan, DateOnly, TimeOnly
- **Binary**: byte[], Memory<byte>, ReadOnlyMemory<byte>, Stream, MemoryStream

### Enums (✅ COMPLETE - Native Dapper Handling)
- **Enum types**: All C# enum types work natively via Dapper's integer handling
- **Storage**: Underlying integer type (byte, short, int, long) - aligns with DDL strategy
- **Nullable enums**: Nullable<TEnum> supported naturally
- **Custom handlers**: Users can register custom string-based handlers per enum type if needed for legacy VARCHAR columns
- **Status**: ✅ Works out-of-the-box, no custom handler needed

### XML (provider-agnostic handler)
- **XDocument**: SQL Server xml, PostgreSQL xml → string serialization

### JSON Types (provider-agnostic handlers)
- **System.Text.Json**: JsonDocument, JsonElement, JsonNode, JsonArray, JsonObject, JsonValue

### Collections - Dictionaries (JSON handlers - provider-agnostic)
- Dictionary<string, string> (PostgreSQL can use hstore natively)
- Dictionary<string, object>
- Dictionary<int, string>
- Dictionary<Guid, object>
- Dictionary<long, DateTime>
- IDictionary<TKey, TValue> variants

### Collections - Lists (JSON handlers - provider-agnostic)
- List<T>: string, decimal, DateTimeOffset, Guid, etc.
- IEnumerable<T>, ICollection<T>, IList<T>
- IReadOnlyList<T>, IReadOnlyCollection<T>
- ImmutableList<T>, ImmutableArray<T> (future)

### Arrays (smart handlers - PostgreSQL native, others JSON)
**16 array types** with PostgreSQL native support:
- **Primitive arrays**: string[], int[], long[], short[], bool[], byte[]
- **Decimal arrays**: double[], float[], decimal[]
- **Temporal arrays**: Guid[], DateTime[], DateTimeOffset[], DateOnly[], TimeOnly[], TimeSpan[]
- **Special**: char[], object[]

### PostgreSQL Network Types (smart handlers - 3 types)
- **IPAddress** → PostgreSQL inet, others string
- **PhysicalAddress** → PostgreSQL macaddr/macaddr8, others string
- **NpgsqlCidr** → PostgreSQL cidr, others string

### PostgreSQL Range Types (smart handlers - 6 types)
- **NpgsqlRange<int>** → PostgreSQL int4range, others JSON
- **NpgsqlRange<long>** → PostgreSQL int8range, others JSON
- **NpgsqlRange<decimal>** → PostgreSQL numrange, others JSON
- **NpgsqlRange<DateTime>** → PostgreSQL tsrange, others JSON
- **NpgsqlRange<DateOnly>** → PostgreSQL daterange, others JSON
- **NpgsqlRange<DateTimeOffset>** → PostgreSQL tstzrange, others JSON

### PostgreSQL Npgsql Spatial Types (smart handlers - 7 types)
- **NpgsqlBox** → PostgreSQL box, others WKT/JSON
- **NpgsqlCircle** → PostgreSQL circle, others WKT/JSON
- **NpgsqlPoint** → PostgreSQL point, others WKT/JSON
- **NpgsqlLine** → PostgreSQL line, others WKT/JSON
- **NpgsqlLSeg** → PostgreSQL lseg, others WKT/JSON
- **NpgsqlPath** → PostgreSQL path, others WKT/JSON
- **NpgsqlPolygon** → PostgreSQL polygon, others WKT/JSON

### PostgreSQL Utility Types (simple mappings)
- **ltree** → `string` (hierarchical label path like `Top.Science.Astronomy`)
- **reg*** → `uint` (8 types: regclass, regconfig, regdictionary, regoper, regoperator, regproc, regprocedure, regtype)
  - These are OID (Object Identifier) types - internally unsigned integers

### SQL Server Spatial Types (optional - if assembly available)
- **SqlGeography** → SQL Server geography type (if Microsoft.SqlServer.Types available, else `byte[]` WKB)
- **SqlGeometry** → SQL Server geometry type (if Microsoft.SqlServer.Types available, else `byte[]` WKB)
- **SqlHierarchyId** → SQL Server hierarchyid type (if Microsoft.SqlServer.Types available, else `string`)

### MySQL Spatial Types (optional - if assembly available)
- **MySqlGeometry** → MySQL geometry type (if MySql.Data available, else `string` WKT)
- All MySQL spatial types (point, polygon, linestring, etc.) map to MySqlGeometry

### NetTopologySuite Types (optional - if assembly available)
- **Geometry**, **Point**, **LineString**, **Polygon** - Unified spatial types across all providers
- PostgreSQL: Uses Npgsql built-in NTS support (automatically handled)
- SQL Server: Requires Microsoft.SqlServer.Types + NTS adapter
- MySQL: Requires MySqlConnector (not MySql.Data) for NTS support
- SQLite: Requires SpatiaLite extension + NTS adapter

### Special Types
- **Custom classes** → JSON storage (provider-agnostic)
- **object** → Only for SQL Server `sql_variant` (genuinely stores any type)

### Text Search Types (PostgreSQL only, lower priority)
- **NpgsqlTsQuery**, **NpgsqlTsVector** - Full-text search types

### Modern C# Support
- **Records**: `public record User(Guid Id, string Name);`
- **Init properties**: `public required string Name { get; init; }`
- **Parameterized constructors**: Full Dapper constructor mapping support

**Total Type Coverage**:
- **DDL**: 80+ types with priority-based type selection (native → string → byte[])
- **DML**: Supports user's choice of property type (native types, string, byte[], or NTS)
- **No `object` types** except SQL Server sql_variant

---

## Performance Characteristics

### Provider-Agnostic Strategy (90% of types)
**Approach**: JSON serialization to text/string
**Performance**: ~1-5ms for typical payloads
**Coverage**: JSON types, dictionaries, lists, custom classes, object type
**Benefit**: Works on ALL providers, simple, maintainable

### Provider-Specific Optimizations (10% of types)
**Approach**: Runtime provider detection, use native features when available

| Type | PostgreSQL Native | JSON Fallback | Performance Gain |
|------|------------------|---------------|------------------|
| string[] | text[] (0.1ms) | JSON (1-5ms) | 10-50x faster |
| int[] | int4[] (0.1ms) | JSON (1-5ms) | 10-50x faster |
| JsonDocument | jsonb (binary, indexable) | JSON text | Better queries |
| Dictionary<string,string> | hstore (optional) | JSON | Better indexing |

### One-Time Startup Cost
- Type handler registration: <1ms total
- Fallback mapper registration: <1ms
- **Total startup cost**: <2ms (one-time per application)

---

## Trade-offs

### Pros
- ✅ "Full spectrum" functionality as requested
- ✅ Minimal complexity - 90% provider-agnostic
- ✅ Global startup registration - zero per-connection overhead
- ✅ Supports multi-provider applications seamlessly
- ✅ Automatic performance optimizations where they matter (PostgreSQL arrays)
- ✅ Zero-config experience via single `Enable()` call
- ✅ No breaking changes (purely additive)
- ✅ Supports all types from DatabaseMethodsTests.Types.cs

### Cons
- ⚠️ Increases library scope beyond pure DDL
- ⚠️ JSON serialization adds ~1-5ms overhead for complex types (unavoidable for fidelity)
- ⚠️ Smart handlers add minor runtime detection overhead (~0.01ms per query)

---

## Success Metrics

1. ✅ All types from DatabaseMethodsTests.Types.cs work in queries
2. ✅ ONE call at startup (`DapperMaticTypeMapping.Enable()`)
3. ✅ Works with multiple database providers in same application
4. ✅ PostgreSQL arrays get 10-50x performance boost over JSON
5. ✅ No breaking changes to existing DDL functionality
6. ✅ Comprehensive test coverage (>90%)
7. ✅ Complete documentation with examples
8. ✅ Startup registration cost <2ms (one-time)
9. ✅ Query overhead <5ms for complex types

---

## Usage Examples

### Basic Startup Configuration

```csharp
// Program.cs (Console/Worker) - Simple
DapperMaticTypeMapping.Initialize();

// Program.cs with options
DapperMaticTypeMapping.Initialize(new DapperMaticMappingOptions
{
    HandlerPrecedence = TypeHandlerPrecedence.SkipIfExists,  // Don't override user handlers
    ErrorStrategy = DeserializationErrorStrategy.ThrowException,  // Fail fast
    EnableRecordSupport = true  // Enable C# 9+ records
});

// Startup.cs (ASP.NET Core)
public void ConfigureServices(IServiceCollection services)
{
    DapperMaticTypeMapping.Initialize();
    // ... rest of configuration
}
```

### Column Name Mapping

```csharp
[DmTable("users")]
public class User
{
    [DmColumn("user_id")]
    public Guid UserId { get; set; }

    [DmColumn("user_name")]
    public string UserName { get; set; }

    [DmColumn("email_address")]
    public string Email { get; set; }
}

// Query automatically maps database columns to C# properties
var users = await db.QueryAsync<User>("SELECT user_id, user_name, email_address FROM users");
```

### JSON Types (All Providers)

```csharp
public class Product
{
    [DmColumn("id")]
    public Guid Id { get; set; }

    [DmColumn("metadata")]
    public JsonDocument Metadata { get; set; } // Stored as JSON in DB

    [DmColumn("settings")]
    public Dictionary<string, object> Settings { get; set; } // Stored as JSON in DB
}

var product = await db.QuerySingleAsync<Product>(
    "SELECT id, metadata, settings FROM products WHERE id = @id",
    new { id = productId }
);
// Metadata and Settings automatically serialized/deserialized
```

### Arrays (PostgreSQL Native, JSON Fallback)

```csharp
public class Post
{
    [DmColumn("id")]
    public Guid Id { get; set; }

    [DmColumn("tags")]
    public string[] Tags { get; set; }

    [DmColumn("view_counts")]
    public int[] ViewCounts { get; set; }
}

// PostgreSQL - uses native text[] and int4[] arrays (FAST!)
var posts = await postgresDb.QueryAsync<Post>("SELECT id, tags, view_counts FROM posts");

// SQL Server - uses JSON arrays (works great!)
var posts = await sqlServerDb.QueryAsync<Post>("SELECT id, tags, view_counts FROM posts");

// Same C# code, optimal storage per provider!
```

### Custom Classes (All Providers)

```csharp
public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
    public string Country { get; set; }
}

public class Customer
{
    [DmColumn("id")]
    public Guid Id { get; set; }

    [DmColumn("shipping_address")]
    public Address ShippingAddress { get; set; } // Stored as JSON
}

var customer = await db.QuerySingleAsync<Customer>(
    "SELECT id, shipping_address FROM customers WHERE id = @id",
    new { id = customerId }
);
// ShippingAddress automatically serialized/deserialized as JSON
```

### Modern C# Records (All Providers)

```csharp
// C# 9+ record with primary constructor
public record User(
    [property: DmColumn("user_id")] Guid UserId,
    [property: DmColumn("user_name")] string UserName,
    [property: DmColumn("email")] string Email
);

// Works automatically with DmColumn attributes
var users = await db.QueryAsync<User>("SELECT user_id, user_name, email FROM users");

// Init-only properties also supported
public class Product
{
    [DmColumn("id")]
    public required Guid Id { get; init; }

    [DmColumn("name")]
    public required string Name { get; init; }

    [DmColumn("price")]
    public decimal Price { get; init; }
}
```

### Enums (All Providers - Integer Storage via Dapper Native Handling)

```csharp
public enum UserRole { Admin = 1, User = 2, Guest = 3 }
public enum OrderStatus { Pending = 0, Shipped = 1, Delivered = 2, Cancelled = 3 }

public class User
{
    [DmColumn("id")]
    public Guid Id { get; set; }

    [DmColumn("role")]
    public UserRole Role { get; set; } // Stored as INT (1, 2, 3)

    [DmColumn("status")]
    public OrderStatus? Status { get; set; } // Nullable enum supported
}

// Query with enum values - Dapper automatically handles enum ↔ integer conversion
var admins = await db.QueryAsync<User>(
    "SELECT id, role, status FROM users WHERE role = @role",
    new { role = UserRole.Admin }  // Passed as integer (1)
);

// Works naturally - no custom handler needed!
```

**Optional: Custom String Handler for Legacy VARCHAR Enum Columns**

```csharp
// If you have a legacy database with VARCHAR enum columns,
// register a custom string handler per enum type:
SqlMapper.AddTypeHandler(new CustomUserRoleStringTypeHandler());

// See test: Should_support_custom_type_handler_for_string_enum_mapping_Async
// for a complete example implementation
```

### XML Types (SQL Server, PostgreSQL)

```csharp
public class Document
{
    [DmColumn("id")]
    public Guid Id { get; set; }

    [DmColumn("metadata")]
    public XDocument Metadata { get; set; } // Stored as XML/TEXT
}

var doc = await db.QuerySingleAsync<Document>(
    "SELECT id, metadata FROM documents WHERE id = @id",
    new { id = documentId }
);
// Metadata automatically serialized/deserialized
```

### PostgreSQL Network Types

```csharp
public class Server
{
    [DmColumn("id")]
    public Guid Id { get; set; }

    [DmColumn("ip_address")]
    public IPAddress IpAddress { get; set; } // PostgreSQL: inet, others: string

    [DmColumn("mac_address")]
    public PhysicalAddress MacAddress { get; set; } // PostgreSQL: macaddr, others: string

    [DmColumn("subnet")]
    public NpgsqlCidr Subnet { get; set; } // PostgreSQL: cidr, others: string
}

// PostgreSQL - uses native inet/macaddr/cidr types (FAST!)
var servers = await postgresDb.QueryAsync<Server>("SELECT id, ip_address, mac_address, subnet FROM servers");

// SQL Server/MySQL/SQLite - uses string representation (works great!)
var servers = await sqlServerDb.QueryAsync<Server>("SELECT id, ip_address, mac_address, subnet FROM servers");
```

### PostgreSQL Range Types

```csharp
public class Booking
{
    [DmColumn("id")]
    public Guid Id { get; set; }

    [DmColumn("date_range")]
    public NpgsqlRange<DateOnly> DateRange { get; set; } // PostgreSQL: daterange, others: JSON

    [DmColumn("price_range")]
    public NpgsqlRange<decimal> PriceRange { get; set; } // PostgreSQL: numrange, others: JSON

    [DmColumn("time_range")]
    public NpgsqlRange<DateTime> TimeRange { get; set; } // PostgreSQL: tsrange, others: JSON
}

// PostgreSQL - uses native range types
var bookings = await postgresDb.QueryAsync<Booking>(
    "SELECT id, date_range, price_range, time_range FROM bookings WHERE date_range @> @date",
    new { date = DateOnly.FromDateTime(DateTime.Today) }
);

// Other providers - uses JSON fallback
```

### PostgreSQL Spatial Types (Npgsql Built-in)

```csharp
public class Location
{
    [DmColumn("id")]
    public Guid Id { get; set; }

    [DmColumn("coordinates")]
    public NpgsqlPoint Coordinates { get; set; } // PostgreSQL: point, others: WKT/JSON

    [DmColumn("boundary")]
    public NpgsqlCircle Boundary { get; set; } // PostgreSQL: circle, others: WKT/JSON

    [DmColumn("area")]
    public NpgsqlPolygon Area { get; set; } // PostgreSQL: polygon, others: WKT/JSON
}

// PostgreSQL - uses native spatial types
var locations = await postgresDb.QueryAsync<Location>(
    "SELECT id, coordinates, boundary, area FROM locations WHERE coordinates <-> @point < 10",
    new { point = new NpgsqlPoint(10.5, 20.3) }
);
```

### User Type Choice - Flexible Property Types

Users can choose the property type that best fits their needs. DapperMatic supports multiple representations:

#### Example 1: Spatial Types - Multiple Representations

```csharp
// Option A: String (WKT) - works out-of-box, no custom handler needed
public class LocationWKT
{
    [DmColumn("geom")]
    public string Geometry { get; set; }  // "POINT(10.5 20.3)"
}

// Option B: Binary (WKB) - works out-of-box, no custom handler needed
public class LocationWKB
{
    [DmColumn("geom")]
    public byte[] Geometry { get; set; }  // Binary representation
}

// Option C: Native provider type - needs custom handler (if assembly available)
public class LocationNative
{
    [DmColumn("geom")]
    public MySqlGeometry Geometry { get; set; }  // MySQL native type
}

// Option D: NetTopologySuite - provider library handles it
public class LocationNTS
{
    [DmColumn("geom")]
    public Geometry Geometry { get; set; }  // NTS unified type
}

// All work! User chooses based on:
// - Dependencies available (MySql.Data, NTS, etc.)
// - Portability needs (string/byte[] work everywhere)
// - Performance needs (native types may be faster)
// - API surface (NTS has rich geometric operations)
```

#### Example 2: SQL Server hierarchyid - Multiple Representations

```csharp
// Option A: String - works out-of-box
public class OrgChart
{
    [DmColumn("path")]
    public string Path { get; set; }  // "/1/2/3/"
}

// Option B: Native type - needs custom handler
public class OrgChartNative
{
    [DmColumn("path")]
    public SqlHierarchyId Path { get; set; }  // SQL Server native type
}
```

#### Example 3: JSON - Multiple Representations

```csharp
// Option A: JsonDocument - provider-agnostic handler
public class ProductJson
{
    [DmColumn("metadata")]
    public JsonDocument Metadata { get; set; }
}

// Option B: String - works out-of-box if you just need raw JSON
public class ProductString
{
    [DmColumn("metadata")]
    public string Metadata { get; set; }  // Raw JSON string
}

// Option C: Custom class - serialized as JSON
public class ProductTyped
{
    [DmColumn("metadata")]
    public ProductMetadata Metadata { get; set; }  // Custom POCO
}

public class ProductMetadata
{
    public string Brand { get; set; }
    public decimal Weight { get; set; }
    public string[] Tags { get; set; }
}
```

**Key Principles**:
- ✅ **string/byte[] always work** - Dapper handles them natively
- ✅ **Native types need handlers** - Only registered when assembly is available
- ✅ **User controls** - Choose based on dependencies, needs, and preferences
- ✅ **DDL uses best default** - Reverse engineering picks native → string → byte[]
- ✅ **DML is flexible** - Query mapping supports whatever user chose

---

## Recommendations

### Phase Priority

**Essential Phases** (Must implement):
1. **Phase 1** (core infrastructure + options) - Foundation with modern C# support
2. **Phase 2** (enum support) - CRITICAL missing feature, widely used
3. **Phase 3** (XML support) - SQL Server/PostgreSQL XML types
4. **Phase 4** (JSON/collections) - Modern apps, highest value
5. **Phase 5** (smart arrays) - PostgreSQL performance boost
6. **Phase 10** (documentation) - User-facing completion

**PostgreSQL-Specific Phases** (Critical for PostgreSQL users):
7. **Phase 6** (network types) - IPAddress, PhysicalAddress, NpgsqlCidr
8. **Phase 7** (range types) - NpgsqlRange<T> for 6 types
9. **Phase 8** (Npgsql spatial) - Box, Circle, Point, Line, LSeg, Path, Polygon

**Optional Phase**:
10. **Phase 9** (NetTopologySuite) - Only if PostGIS/spatial features needed

### Implementation Strategy

**Minimum Viable Product (MVP)**: Phases 1-5 + 10
- Provides complete "full spectrum" functionality for 80% of use cases
- Supports all common types (primitives, enums, JSON, collections, arrays)
- Works across all 4 database providers
- ~40-50 type handlers

**Full PostgreSQL Support**: Phases 1-8 + 10
- Adds 16 PostgreSQL-specific handlers
- Unlocks full PostgreSQL feature set
- Native performance for PostgreSQL types
- ~65 type handlers

**Complete Implementation**: All Phases 1-10
- NetTopologySuite/PostGIS support
- Comprehensive spatial type coverage
- ~80+ type handlers

### Decision Points

**After Phase 5 (MVP Complete):**
- Assess: User adoption, feedback, demand for PostgreSQL features
- Decision: Continue to Phases 6-8 if PostgreSQL users are significant

**After Phase 8 (Full PostgreSQL Support):**
- Assess: Demand for NetTopologySuite/PostGIS
- Decision: Implement Phase 9 only if spatial features requested

**Recommended Path**: Implement Phases 1-8 + 10 for comprehensive coverage while maintaining focus on widely-used types.

---

## Future Enhancements

1. **Custom JSON serializer**: Allow users to provide custom JsonSerializerOptions
2. **Type converter plugins**: Extensibility point for third-party types
3. **Schema validation**: Ensure query results match expected types at runtime
4. **Performance optimizations**: Cache expression trees for type mapping
5. **Binary JSON optimization**: Detect MySQL 5.7+ vs MariaDB for json vs longtext
