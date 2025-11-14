# DML Query Support

DapperMatic enhances Dapper's query capabilities by providing seamless attribute-based column mapping for DML (Data Manipulation Language) operations. This means your `QueryAsync` and `ExecuteAsync` operations work naturally with the same attributes used for DDL schema management.

## Overview

While DapperMatic's primary focus is DDL (schema management), it also provides **DML query compatibility** through global type mapping initialization. This allows you to use Dapper's query methods (`QueryAsync`, `ExecuteAsync`, etc.) with classes annotated using DapperMatic, EF Core, or ServiceStack.OrmLite attributes.

::: tip Key Benefits
- **Seamless Integration**: Initialize once, use everywhere with Dapper queries
- **Attribute Flexibility**: Supports DapperMatic, EF Core, and ServiceStack.OrmLite attributes
- **Zero Configuration**: Works automatically after initialization
- **Modern C# Support**: Full support for records with parameterized constructors
- **Performance**: No reflection overhead at query time (mapping cached)
:::

## Quick Start

### 1. Initialize Type Mapping

Call `DapperMaticTypeMapping.Initialize()` once during application startup:

```csharp
using MJCZone.DapperMatic.TypeMapping;

// In Program.cs or Startup.cs
DapperMaticTypeMapping.Initialize();

// That's it! Now Dapper queries work with mapped attributes
```

###2. Use Dapper Queries with Mapped Classes

```csharp
using MJCZone.DapperMatic.DataAnnotations;
using Dapper;

// Class with database column mapping
public class User
{
    [DmColumn("user_id")]
    public int UserId { get; set; }

    [DmColumn("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [DmColumn("last_name")]
    public string LastName { get; set; } = string.Empty;

    [DmColumn("email_address")]
    public string EmailAddress { get; set; } = string.Empty;

    [DmIgnore] // Not mapped from database
    public string FullName => $"{FirstName} {LastName}";
}

// Query works automatically - column names mapped via attributes
var users = await connection.QueryAsync<User>(
    "SELECT user_id, first_name, last_name, email_address FROM users WHERE user_id = @id",
    new { id = 123 }
);
```

## Supported Attributes

DapperMatic supports attributes from three frameworks for column name mapping and property exclusion:

### DapperMatic Attributes

```csharp
using MJCZone.DapperMatic.DataAnnotations;

public class Product
{
    [DmColumn("product_id")]
    public int ProductId { get; set; }

    [DmColumn("product_name")]
    public string Name { get; set; } = string.Empty;

    [DmIgnore] // Excluded from mapping
    public decimal CalculatedTotal { get; set; }
}
```

### EF Core Attributes

```csharp
using System.ComponentModel.DataAnnotations.Schema;

public class Customer
{
    [Column("customer_id")]
    public int CustomerId { get; set; }

    [Column("full_name")]
    public string FullName { get; set; } = string.Empty;

    [NotMapped] // Excluded from mapping
    public string DisplayName { get; set; } = string.Empty;
}
```

### ServiceStack.OrmLite Attributes

```csharp
using ServiceStack.DataAnnotations;

public class Order
{
    [Alias("order_id")]
    public int OrderId { get; set; }

    [Alias("order_date")]
    public DateTime OrderDate { get; set; }

    [Ignore] // Excluded from mapping
    public string StatusText { get; set; } = string.Empty;
}
```

::: info Attribute Priority
When multiple mapping attributes are present, DapperMatic uses the first one found in this order:
1. `DmColumn` / `DmIgnore`
2. EF Core `Column` / `NotMapped`
3. ServiceStack.OrmLite `Alias` / `Ignore`
:::

## Modern C# Support

### Records with Parameterized Constructors

DapperMatic fully supports modern C# records:

```csharp
// Simple record with positional parameters
public record Product(int Id, string Name, decimal Price);

// Query works automatically
var products = await connection.QueryAsync<Product>(
    "SELECT id, name, price FROM products"
);
```

### Records with Column Mapping

```csharp
using MJCZone.DapperMatic.DataAnnotations;

public record OrderItem(
    [property: DmColumn("item_id")] int ItemId,
    [property: DmColumn("order_id")] int OrderId,
    [property: DmColumn("product_name")] string ProductName,
    [property: DmColumn("unit_price")] decimal UnitPrice,
    [property: DmColumn("quantity")] int Quantity
);

// Maps database columns to record properties
var items = await connection.QueryAsync<OrderItem>(
    "SELECT item_id, order_id, product_name, unit_price, quantity FROM order_items WHERE order_id = @orderId",
    new { orderId = 456 }
);
```

## Configuration Options

### Basic Initialization

```csharp
// Use default settings
DapperMaticTypeMapping.Initialize();
```

### Custom Configuration

```csharp
using MJCZone.DapperMatic.TypeMapping;

DapperMaticTypeMapping.Initialize(new DapperMaticMappingOptions
{
    // Control how type handlers are registered
    HandlerPrecedence = TypeHandlerPrecedence.OverrideExisting,

    // Enable/disable record constructor support
    EnableRecordSupport = true
});
```

### Handler Precedence Options

- **`SkipIfExists`** (default): Don't register if handler already exists
- **`OverrideExisting`**: Replace existing handlers with DapperMatic handlers
- **`ThrowIfExists`**: Throw exception if handler already registered

### Record Support

Enable or disable support for records with parameterized constructors:

```csharp
new DapperMaticMappingOptions
{
    EnableRecordSupport = true // default: true
}
```

When enabled, Dapper will map query results to record constructors. When disabled, only parameterless constructors are used.

## Type Mapping Behavior

### Enum Storage

Enums are stored as their underlying integer type (byte, short, int, or long). This aligns with Dapper's default enum handling:

```csharp
public enum OrderStatus
{
    Pending = 0,
    Processing = 1,
    Shipped = 2,
    Delivered = 3
}

public class Order
{
    public int OrderId { get; set; }
    public OrderStatus Status { get; set; } // Stored as INT
}

// Queries work naturally - Dapper handles enum conversion
var orders = await connection.QueryAsync<Order>(
    "SELECT order_id, status FROM orders WHERE status = @status",
    new { status = OrderStatus.Shipped } // Passed as integer (2)
);
```

### Property Name Fallback

If no mapping attribute is found, DapperMatic falls back to case-insensitive property name matching:

```csharp
public class SimpleUser
{
    // Maps to "Id" or "id" or "ID" column
    public int Id { get; set; }

    // Maps to "Name" or "name" or "NAME" column
    public string Name { get; set; } = string.Empty;
}
```

## Advanced Type Support

DapperMatic provides type handlers for complex data types, enabling seamless serialization and deserialization across all database providers.

### XML Support

Store and query XML data using `XDocument`:

```csharp
using System.Xml.Linq;

public class Document
{
    [DmColumn("document_id")]
    public int DocumentId { get; set; }

    [DmColumn("title")]
    public string Title { get; set; } = string.Empty;

    [DmColumn("metadata")]
    public XDocument? Metadata { get; set; } // Stored as XML/TEXT
}

// Insert with XML data
var doc = new Document
{
    Title = "Product Catalog",
    Metadata = new XDocument(
        new XElement("metadata",
            new XElement("version", "1.0"),
            new XElement("author", "John Doe"),
            new XElement("tags",
                new XElement("tag", "electronics"),
                new XElement("tag", "gadgets")
            )
        )
    )
};

await connection.ExecuteAsync(
    "INSERT INTO documents (title, metadata) VALUES (@title, @metadata)",
    new { title = doc.Title, metadata = doc.Metadata }
);

// Query with XML data
var documents = await connection.QueryAsync<Document>(
    "SELECT document_id, title, metadata FROM documents"
);

// Access XML data
foreach (var document in documents)
{
    var version = document.Metadata?.Root?.Element("version")?.Value;
    Console.WriteLine($"{document.Title} - Version: {version}");
}
```

::: tip Cross-Database Support
XML type handlers work across **all database providers** (SQL Server, MySQL, MariaDB, PostgreSQL, SQLite). PostgreSQL automatically uses the native `xml` type for optimal performance.
:::

### JSON Support

Store and query JSON data using `JsonDocument`:

```csharp
using System.Text.Json;

public class Product
{
    [DmColumn("product_id")]
    public int ProductId { get; set; }

    [DmColumn("product_name")]
    public string ProductName { get; set; } = string.Empty;

    [DmColumn("specifications")]
    public JsonDocument? Specifications { get; set; } // Stored as JSON
}

// Insert with JSON data
var product = new Product
{
    ProductName = "Laptop",
    Specifications = JsonDocument.Parse(@"{
        ""brand"": ""TechCorp"",
        ""model"": ""Pro 15"",
        ""cpu"": ""Intel i7"",
        ""ram"": ""16GB"",
        ""storage"": ""512GB SSD""
    }")
};

await connection.ExecuteAsync(
    "INSERT INTO products (product_name, specifications) VALUES (@name, @specs)",
    new { name = product.ProductName, specs = product.Specifications }
);

// Query with JSON data
var products = await connection.QueryAsync<Product>(
    "SELECT product_id, product_name, specifications FROM products"
);

// Access JSON data
foreach (var prod in products)
{
    var brand = prod.Specifications?.RootElement.GetProperty("brand").GetString();
    var model = prod.Specifications?.RootElement.GetProperty("model").GetString();
    Console.WriteLine($"{prod.ProductName}: {brand} {model}");
}
```

::: tip PostgreSQL Optimization
On PostgreSQL, JSON type handlers automatically use the native `jsonb` type for better query performance and indexing capabilities. Other databases store JSON as text.
:::

### Dictionary Support

Store and query key-value pairs using `Dictionary<TKey, TValue>`:

```csharp
public class UserSettings
{
    [DmColumn("user_id")]
    public int UserId { get; set; }

    [DmColumn("username")]
    public string Username { get; set; } = string.Empty;

    [DmColumn("preferences")]
    public Dictionary<string, string>? Preferences { get; set; } // Stored as JSON
}

// Insert with dictionary
var settings = new UserSettings
{
    UserId = 1,
    Username = "jdoe",
    Preferences = new Dictionary<string, string>
    {
        ["theme"] = "dark",
        ["language"] = "en-US",
        ["timezone"] = "America/New_York",
        ["notifications"] = "enabled"
    }
};

await connection.ExecuteAsync(
    "INSERT INTO user_settings (user_id, username, preferences) VALUES (@userId, @username, @prefs)",
    new { userId = settings.UserId, username = settings.Username, prefs = settings.Preferences }
);

// Query with dictionary
var userSettings = await connection.QueryAsync<UserSettings>(
    "SELECT user_id, username, preferences FROM user_settings WHERE user_id = @userId",
    new { userId = 1 }
);

// Access dictionary data
foreach (var setting in userSettings)
{
    Console.WriteLine($"User: {setting.Username}");
    if (setting.Preferences != null)
    {
        foreach (var (key, value) in setting.Preferences)
        {
            Console.WriteLine($"  {key}: {value}");
        }
    }
}
```

### List Support

Store and query collections using `List<T>`:

```csharp
public class BlogPost
{
    [DmColumn("post_id")]
    public int PostId { get; set; }

    [DmColumn("title")]
    public string Title { get; set; } = string.Empty;

    [DmColumn("tags")]
    public List<string>? Tags { get; set; } // Stored as JSON array
}

// Insert with list
var post = new BlogPost
{
    Title = "Getting Started with DapperMatic",
    Tags = new List<string> { "dapper", "orm", "database", "tutorial" }
};

await connection.ExecuteAsync(
    "INSERT INTO blog_posts (title, tags) VALUES (@title, @tags)",
    new { title = post.Title, tags = post.Tags }
);

// Query with list
var posts = await connection.QueryAsync<BlogPost>(
    "SELECT post_id, title, tags FROM blog_posts"
);

// Access list data
foreach (var blogPost in posts)
{
    Console.WriteLine($"{blogPost.Title}");
    Console.WriteLine($"  Tags: {string.Join(", ", blogPost.Tags ?? new List<string>())}");
}
```

::: info Supported Collection Types
DapperMatic provides handlers for the following generic collection types:
- `Dictionary<string, string>`
- `Dictionary<string, object>`
- `List<string>`

Additional type combinations can be registered by adding more handler instances in your initialization code.
:::

### Performance Characteristics

| Type | Storage Method | Performance | Cross-Database |
|------|---------------|-------------|----------------|
| `XDocument` | XML/TEXT serialization | ~1-5ms | ✅ All providers |
| `JsonDocument` | JSON serialization | ~1-5ms | ✅ All providers |
| `Dictionary<TKey, TValue>` | JSON serialization | ~1-5ms | ✅ All providers |
| `List<T>` | JSON array serialization | ~1-5ms | ✅ All providers |
| Arrays (`T[]`) | PostgreSQL: Native arrays<br/>Others: JSON | PostgreSQL: **~0.1ms**<br/>Others: ~1-5ms | ✅ All providers |
| `IPAddress` | PostgreSQL: Native inet<br/>Others: String | PostgreSQL: **~0.1ms**<br/>Others: ~1-2ms | ✅ All providers |
| `PhysicalAddress` | PostgreSQL: Native macaddr<br/>Others: String | PostgreSQL: **~0.1ms**<br/>Others: ~1-2ms | ✅ All providers |
| `NpgsqlCidr` | PostgreSQL: Native cidr<br/>Others: String | PostgreSQL: **~0.1ms**<br/>Others: ~1-2ms | ✅ All providers |
| `NpgsqlRange<T>` | PostgreSQL: Native ranges<br/>Others: JSON | PostgreSQL: **~0.1ms**<br/>Others: ~1-5ms | ✅ All providers |

::: tip PostgreSQL Performance
PostgreSQL automatically benefits from native types for optimal performance:
- **Native types** (`xml`, `jsonb`, `inet`, `macaddr`, `cidr`, range types): Fast, indexable, with specialized operators
- **JSON serialization** (other providers): Reliable cross-database compatibility
- **String serialization** (network types on non-PostgreSQL): Simple and portable
:::

### Array Support

Store and query arrays with automatic provider optimization:

```csharp
public class Product
{
    [DmColumn("product_id")]
    public int ProductId { get; set; }

    [DmColumn("product_name")]
    public string ProductName { get; set; } = string.Empty;

    [DmColumn("tags")]
    public string[]? Tags { get; set; } // Smart array handler

    [DmColumn("related_ids")]
    public int[]? RelatedIds { get; set; } // Smart array handler
}

// Insert with arrays
var product = new Product
{
    ProductName = "Laptop",
    Tags = new[] { "electronics", "computers", "portable" },
    RelatedIds = new[] { 101, 102, 105 }
};

await connection.ExecuteAsync(
    "INSERT INTO products (product_name, tags, related_ids) VALUES (@name, @tags, @ids)",
    new { name = product.ProductName, tags = product.Tags, ids = product.RelatedIds }
);

// Query with arrays
var products = await connection.QueryAsync<Product>(
    "SELECT product_id, product_name, tags, related_ids FROM products"
);

// Access array data
foreach (var prod in products)
{
    Console.WriteLine($"{prod.ProductName}:");
    Console.WriteLine($"  Tags: {string.Join(", ", prod.Tags ?? Array.Empty<string>())}");
    Console.WriteLine($"  Related: {string.Join(", ", prod.RelatedIds ?? Array.Empty<int>())}");
}
```

::: tip Smart Array Performance
DapperMatic uses **runtime provider detection** for optimal performance:
- **PostgreSQL**: Native arrays (text[], int4[], etc.) - **10-50x faster** than JSON!
- **Other providers**: JSON array serialization - works reliably everywhere
:::

**Supported Array Types** (15 total):
- **Primitives**: `string[]`, `int[]`, `long[]`, `short[]`, `bool[]`, `byte[]`
- **Decimals**: `double[]`, `float[]`, `decimal[]`
- **Temporal**: `Guid[]`, `DateTime[]`, `DateTimeOffset[]`, `DateOnly[]`, `TimeOnly[]`, `TimeSpan[]`

### Network Types Support

Store and query network addresses using standard .NET types:

```csharp
using System.Net;
using System.Net.NetworkInformation;
using NpgsqlTypes; // For NpgsqlCidr

// Approach 1: Using typed properties (recommended for DDL)
public class NetworkDevice
{
    [DmColumn("device_id")]
    public int DeviceId { get; set; }

    [DmColumn("device_name")]
    public string DeviceName { get; set; } = string.Empty;

    [DmColumn("ip_address")]
    public IPAddress? IPAddress { get; set; } // PostgreSQL: inet, Others: string

    [DmColumn("mac_address")]
    public PhysicalAddress? MacAddress { get; set; } // PostgreSQL: macaddr, Others: string

    [DmColumn("subnet")]
    public NpgsqlCidr? Subnet { get; set; } // PostgreSQL: cidr, Others: string
}

// Approach 2: Using object with providerDataType (for DDL with object types)
public class NetworkDeviceAlt
{
    [DmColumn("device_id")]
    public int DeviceId { get; set; }

    [DmColumn("device_name")]
    public string DeviceName { get; set; } = string.Empty;

    [DmColumn("ip_address")]
    public IPAddress? IPAddress { get; set; }

    [DmColumn("mac_address")]
    public PhysicalAddress? MacAddress { get; set; }

    [DmColumn("subnet", providerDataType: "{postgresql:cidr}")]
    public object? Subnet { get; set; } // Explicit provider type for DDL
}

// Insert with network data
var device = new NetworkDevice
{
    DeviceName = "Main Router",
    IPAddress = IPAddress.Parse("192.168.1.1"),
    MacAddress = PhysicalAddress.Parse("00-11-22-33-44-55"),
    Subnet = NpgsqlCidr.Parse("192.168.1.0/24") // Parse CIDR notation
};

await connection.ExecuteAsync(
    "INSERT INTO network_devices (device_name, ip_address, mac_address, subnet) VALUES (@name, @ip, @mac, @subnet)",
    new { name = device.DeviceName, ip = device.IPAddress, mac = device.MacAddress, subnet = device.Subnet }
);

// Query with network data
var devices = await connection.QueryAsync<NetworkDevice>(
    "SELECT device_id, device_name, ip_address, mac_address, subnet FROM network_devices"
);

// Access network data
foreach (var dev in devices)
{
    Console.WriteLine($"{dev.DeviceName}:");
    Console.WriteLine($"  IP: {dev.IPAddress}");
    Console.WriteLine($"  MAC: {dev.MacAddress}");
    Console.WriteLine($"  Subnet: {dev.Subnet}");
}
```

::: tip PostgreSQL Native Types
On PostgreSQL, network types use native `inet`, `macaddr`, and `cidr` types for:
- Validation at the database level
- Specialized operators and functions
- Efficient storage and indexing

Other databases use string serialization with the same API.
:::

::: warning Property Type Matters for Both DDL and DML
The property type and/or `providerDataType` affects **both table creation (DDL) and querying (DML)**:

**Option 1 (Recommended):** Use the typed property:
```csharp
[DmColumn("subnet")]
public NpgsqlCidr? Subnet { get; set; }
```
- ✅ **DDL:** Creates `cidr` column in PostgreSQL
- ✅ **DML:** Returns as `NpgsqlCidr` with full API (`.Address`, `.Netmask`, etc.)

**Option 2:** Use `object?` with explicit provider data type:
```csharp
[DmColumn("subnet", providerDataType: "{postgresql:cidr}")]
public object? Subnet { get; set; }
```
- ✅ **DDL:** Creates `cidr` column in PostgreSQL
- ✅ **DML:** Returns as `NpgsqlCidr` with full API (`.Address`, `.Netmask`, etc.)

**Option 3 (Limited):** Use `object?` alone without `providerDataType`:
```csharp
[DmColumn("subnet")]
public object? Subnet { get; set; }
```
- ⚠️ **DDL:** Creates generic text column (cannot infer `cidr` from `object`)
- ⚠️ **DML:** Returns as **string** (only `.ToString()` available, no `.Address`/`.Netmask`)

**Recommendation:** Always use Option 1 or Option 2 for full functionality in both DDL and DML scenarios.
:::

**Supported Network Types** (3 total):
- `IPAddress` - IPv4 and IPv6 addresses
- `PhysicalAddress` - MAC addresses (Ethernet hardware addresses)
- `NpgsqlCidr` - Network ranges in CIDR notation (PostgreSQL-specific, requires Npgsql package)

### Range Types Support

Store and query range values with bounds using PostgreSQL's powerful range types:

```csharp
using NpgsqlTypes; // For NpgsqlRange<T>

// Approach 1: Using typed properties (recommended for DDL)
public class PriceHistory
{
    [DmColumn("history_id")]
    public int HistoryId { get; set; }

    [DmColumn("product_name")]
    public string ProductName { get; set; } = string.Empty;

    [DmColumn("price_range")]
    public NpgsqlRange<decimal>? PriceRange { get; set; } // PostgreSQL: numrange

    [DmColumn("date_range")]
    public NpgsqlRange<DateOnly>? DateRange { get; set; } // PostgreSQL: daterange
}

// Approach 2: Using object with providerDataType (for DDL with object types)
public class PriceHistoryAlt
{
    [DmColumn("history_id")]
    public int HistoryId { get; set; }

    [DmColumn("product_name")]
    public string ProductName { get; set; } = string.Empty;

    [DmColumn("price_range", providerDataType: "{postgresql:numrange}")]
    public object? PriceRange { get; set; } // Explicit provider type for DDL

    [DmColumn("date_range", providerDataType: "{postgresql:daterange}")]
    public object? DateRange { get; set; } // Explicit provider type for DDL
}

// Create range values (requires Npgsql package)
var priceRange = new NpgsqlRange<decimal>(19.99m, true, false, 99.99m, true, false); // [19.99, 99.99)
var dateRange = new NpgsqlRange<DateOnly>(
    new DateOnly(2024, 1, 1), true, false,
    new DateOnly(2024, 12, 31), true, false
); // [2024-01-01, 2024-12-31)

// Insert with range data
await connection.ExecuteAsync(
    "INSERT INTO price_history (product_name, price_range, date_range) VALUES (@name, @price, @date)",
    new { name = "Laptop", price = priceRange, date = dateRange }
);

// Query with range data
var history = await connection.QueryAsync<PriceHistory>(
    "SELECT history_id, product_name, price_range, date_range FROM price_history"
);

// Access range data
foreach (var record in history)
{
    Console.WriteLine($"{record.ProductName}:");
    if (record.PriceRange != null)
    {
        Console.WriteLine($"  Price Range: [{record.PriceRange.Value.LowerBound}, {record.PriceRange.Value.UpperBound})");
    }
    if (record.DateRange != null)
    {
        Console.WriteLine($"  Date Range: [{record.DateRange.Value.LowerBound}, {record.DateRange.Value.UpperBound})");
    }
}
```

::: tip PostgreSQL Range Type Advantages
PostgreSQL's native range types provide:
- **Containment operators**: Check if a value is within a range (`@>`, `<@`)
- **Overlap detection**: Test if ranges overlap (`&&`)
- **Adjacency testing**: Check if ranges are adjacent (`-|-`)
- **Union and intersection**: Combine ranges (`+`, `*`)
- **GiST indexing**: Fast range queries with specialized indexes

Other databases use JSON serialization with the same API but without native database operators.
:::

::: warning Property Type Matters for Both DDL and DML
The property type and/or `providerDataType` affects **both table creation (DDL) and querying (DML)**:

**Option 1 (Recommended):** Use the typed property:
```csharp
[DmColumn("price_range")]
public NpgsqlRange<decimal>? PriceRange { get; set; }
```
- ✅ **DDL:** Creates `numrange` column in PostgreSQL
- ✅ **DML:** Returns as `NpgsqlRange<decimal>` with full API (`.LowerBound`, `.UpperBound`, etc.)

**Option 2:** Use `object?` with explicit provider data type:
```csharp
[DmColumn("price_range", providerDataType: "{postgresql:numrange}")]
public object? PriceRange { get; set; }
```
- ✅ **DDL:** Creates `numrange` column in PostgreSQL
- ✅ **DML:** Returns as `NpgsqlRange<decimal>` with full API (`.LowerBound`, `.UpperBound`, etc.)

**Option 3 (Limited):** Use `object?` alone without `providerDataType`:
```csharp
[DmColumn("price_range")]
public object? PriceRange { get; set; }
```
- ⚠️ **DDL:** Creates generic text column (cannot infer `numrange` from `object`)
- ⚠️ **DML:** Returns as **string** (only `.ToString()` available, no `.LowerBound`/`.UpperBound`)

**Recommendation:** Always use Option 1 or Option 2 for full functionality in both DDL and DML scenarios.
:::

**Supported Range Types** (6 total):
- `NpgsqlRange<int>` - Integer ranges (int4range) for whole numbers
- `NpgsqlRange<long>` - Long integer ranges (int8range) for large numbers
- `NpgsqlRange<decimal>` - **Numeric ranges (numrange)** for exact precision decimals
- `NpgsqlRange<DateOnly>` - Date ranges (daterange) for calendar dates
- `NpgsqlRange<DateTime>` - Timestamp ranges (tsrange) without timezone
- `NpgsqlRange<DateTimeOffset>` - Timestamp ranges (tstzrange) with timezone

::: warning Exact vs Approximate Precision
Range types use **exact precision** semantics:
- ✅ `NpgsqlRange<decimal>` → `numrange` (exact decimal arithmetic)
- ❌ `NpgsqlRange<double>` - **Not supported** (PostgreSQL has no float8range type)
- ❌ `NpgsqlRange<float>` - **Not supported** (PostgreSQL has no float4range type)

For floating-point ranges, use `decimal` to maintain exact precision and avoid rounding errors.
:::

### PostgreSQL Geometric Types Support

Store and query geometric shapes using PostgreSQL's native geometric types with WKT (Well-Known Text) fallback for other databases:

```csharp
using NpgsqlTypes; // For NpgsqlPoint, NpgsqlPolygon, etc.

// Approach 1: Using typed properties (recommended for DDL)
public class MapLocation
{
    [DmColumn("location_id")]
    public int LocationId { get; set; }

    [DmColumn("name")]
    public string Name { get; set; } = string.Empty;

    [DmColumn("coordinates")]
    public NpgsqlPoint? Coordinates { get; set; } // PostgreSQL: point, Others: WKT string

    [DmColumn("service_area")]
    public NpgsqlPolygon? ServiceArea { get; set; } // PostgreSQL: polygon, Others: WKT string
}

// Approach 2: Using object with providerDataType (for DDL with object types)
public class MapLocationAlt
{
    [DmColumn("location_id")]
    public int LocationId { get; set; }

    [DmColumn("name")]
    public string Name { get; set; } = string.Empty;

    [DmColumn("coordinates", providerDataType: "{postgresql:point}")]
    public object? Coordinates { get; set; } // Explicit provider type for DDL

    [DmColumn("service_area", providerDataType: "{postgresql:polygon}")]
    public object? ServiceArea { get; set; } // Explicit provider type for DDL
}

// Create geometric values (requires Npgsql package)
var location = new NpgsqlPoint(40.7128, -74.0060); // New York City

// Insert with geometric data
await connection.ExecuteAsync(
    "INSERT INTO map_locations (name, coordinates) VALUES (@name, @coords)",
    new { name = "NYC Office", coords = location }
);

// Query with geometric data
var locations = await connection.QueryAsync<MapLocation>(
    "SELECT location_id, name, coordinates FROM map_locations"
);

// Access geometric data
foreach (var loc in locations)
{
    if (loc.Coordinates != null)
    {
        Console.WriteLine($"{loc.Name}: ({loc.Coordinates.Value.X}, {loc.Coordinates.Value.Y})");
    }
}
```

::: warning Property Type Matters for Both DDL and DML
The property type and/or `providerDataType` affects **both table creation (DDL) and querying (DML)**:

**Option 1 (Recommended):** Use the typed property:
```csharp
[DmColumn("coordinates")]
public NpgsqlPoint? Coordinates { get; set; }
```
- ✅ **DDL:** Creates `point` column in PostgreSQL
- ✅ **DML:** Returns as `NpgsqlPoint` with full API (`.X`, `.Y`, etc.)

**Option 2:** Use `object?` with explicit provider data type:
```csharp
[DmColumn("coordinates", providerDataType: "{postgresql:point}")]
public object? Coordinates { get; set; }
```
- ✅ **DDL:** Creates `point` column in PostgreSQL
- ✅ **DML:** Returns as `NpgsqlPoint` with full API (`.X`, `.Y`, etc.)

**Option 3 (Limited):** Use `object?` alone without `providerDataType`:
```csharp
[DmColumn("coordinates")]
public object? Coordinates { get; set; }
```
- ⚠️ **DDL:** Creates generic text column (cannot infer `point` from `object`)
- ⚠️ **DML:** Returns as **string** (WKT format only, no `.X`/`.Y` properties)

**Recommendation:** Always use Option 1 or Option 2 for full functionality in both DDL and DML scenarios.
:::

**Supported PostgreSQL Geometric Types:**

| .NET Type | PostgreSQL Type | WKT Format (Other DBs) | Description |
|-----------|-----------------|------------------------|-------------|
| `NpgsqlPoint` | `point` | `POINT(x y)` | 2D point coordinate |
| `NpgsqlBox` | `box` | `POLYGON((x1 y1,x2 y1,x2 y2,x1 y2,x1 y1))` | Rectangle (opposite corners) |
| `NpgsqlCircle` | `circle` | `CIRCLE(x y, radius)` | Circle with center and radius |
| `NpgsqlLine` | `line` | `LINE(a b c)` | Infinite line (Ax + By + C = 0) |
| `NpgsqlLSeg` | `lseg` | `LINESTRING(x1 y1, x2 y2)` | Line segment (2 points) |
| `NpgsqlPath` | `path` | `LINESTRING(...)` or `POLYGON((...))` | Open or closed path |
| `NpgsqlPolygon` | `polygon` | `POLYGON((x1 y1, x2 y2, ..., x1 y1))` | Closed polygon |

::: tip WKT Format
**WKT (Well-Known Text)** is an ISO standard text format for representing geometric objects. On PostgreSQL, DapperMatic uses native geometric types for 10-50x better performance. On other databases (SQL Server, MySQL, SQLite), geometric data is automatically converted to WKT strings for storage and retrieval.

Learn more: [WKT on Wikipedia](https://en.wikipedia.org/wiki/Well-known_text_representation_of_geometry)
:::

## Complete Example

Here's a complete example showing DDL schema creation and DML query usage together:

```csharp
using Dapper;
using MJCZone.DapperMatic;
using MJCZone.DapperMatic.DataAnnotations;
using MJCZone.DapperMatic.Models;
using MJCZone.DapperMatic.TypeMapping;
using Microsoft.Data.SqlClient;

// Initialize DapperMatic type mapping once at startup
DapperMaticTypeMapping.Initialize();

using var connection = new SqlConnection(connectionString);
await connection.OpenAsync();

// 1. DDL: Create table using DapperMatic
var table = new DmTable
{
    TableName = "products",
    Columns =
    [
        new DmColumn("product_id", typeof(int), isPrimaryKey: true, isAutoIncrement: true),
        new DmColumn("product_name", typeof(string), length: 255),
        new DmColumn("unit_price", typeof(decimal), precision: 10, scale: 2),
        new DmColumn("stock_quantity", typeof(int)),
    ]
};

await connection.CreateTableIfNotExistsAsync(table);

// 2. DML: Insert data using Dapper
await connection.ExecuteAsync(
    @"INSERT INTO products (product_name, unit_price, stock_quantity)
      VALUES (@name, @price, @quantity)",
    new { name = "Widget", price = 19.99m, quantity = 100 }
);

// 3. DML: Query with mapped class
public class Product
{
    [DmColumn("product_id")]
    public int ProductId { get; set; }

    [DmColumn("product_name")]
    public string ProductName { get; set; } = string.Empty;

    [DmColumn("unit_price")]
    public decimal UnitPrice { get; set; }

    [DmColumn("stock_quantity")]
    public int StockQuantity { get; set; }

    [DmIgnore]
    public decimal TotalValue => UnitPrice * StockQuantity;
}

var products = await connection.QueryAsync<Product>(
    "SELECT product_id, product_name, unit_price, stock_quantity FROM products"
);

foreach (var product in products)
{
    Console.WriteLine($"{product.ProductName}: ${product.UnitPrice} x {product.StockQuantity} = ${product.TotalValue}");
}
```

## Framework Interoperability

DapperMatic's attribute support enables seamless migration from other frameworks:

### Migrating from EF Core

If you're migrating from Entity Framework Core, your existing entity classes work without changes:

```csharp
// Existing EF Core entity - works immediately with DapperMatic
[Table("users")]
public class User
{
    [Key]
    [Column("user_id")]
    public int UserId { get; set; }

    [Column("username")]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [NotMapped]
    public string DisplayName { get; set; } = string.Empty;
}

// After DapperMaticTypeMapping.Initialize(), Dapper queries work:
var users = await connection.QueryAsync<User>("SELECT user_id, username FROM users");
```

### Using ServiceStack.OrmLite Models

ServiceStack.OrmLite models are also supported:

```csharp
// ServiceStack.OrmLite model
[Alias("customers")]
public class Customer
{
    [PrimaryKey]
    [Alias("customer_id")]
    public int CustomerId { get; set; }

    [Alias("company_name")]
    public string CompanyName { get; set; } = string.Empty;

    [Ignore]
    public string Notes { get; set; } = string.Empty;
}

// Works with Dapper after initialization
var customers = await connection.QueryAsync<Customer>(
    "SELECT customer_id, company_name FROM customers"
);
```

## Best Practices

### 1. Initialize Once, Early

Initialize DapperMatic type mapping as early as possible in your application lifecycle:

```csharp
// ASP.NET Core
var builder = WebApplication.CreateBuilder(args);

// Initialize before registering other services
DapperMaticTypeMapping.Initialize();

builder.Services.AddControllers();
// ... other services

var app = builder.Build();
```

### 2. Use Consistent Attribute Style

Choose one attribute style and use it consistently across your application:

```csharp
// Good: Consistent use of DmColumn
public class Order
{
    [DmColumn("order_id")]
    public int OrderId { get; set; }

    [DmColumn("customer_id")]
    public int CustomerId { get; set; }
}

// Avoid: Mixing attribute types unnecessarily
public class Order
{
    [DmColumn("order_id")]     // DapperMatic
    public int OrderId { get; set; }

    [Column("customer_id")]    // EF Core
    public int CustomerId { get; set; }
}
```

### 3. Document Ignored Properties

Make it clear when properties are not mapped from the database:

```csharp
public class Product
{
    [DmColumn("product_id")]
    public int ProductId { get; set; }

    [DmColumn("unit_price")]
    public decimal UnitPrice { get; set; }

    [DmColumn("quantity")]
    public int Quantity { get; set; }

    /// <summary>
    /// Calculated property - not stored in database
    /// </summary>
    [DmIgnore]
    public decimal TotalValue => UnitPrice * Quantity;
}
```

### 4. Leverage Records for Immutability

Use records for read-only query results:

```csharp
// Immutable query result
public record ProductSummary(
    [property: DmColumn("product_id")] int ProductId,
    [property: DmColumn("product_name")] string ProductName,
    [property: DmColumn("total_sales")] decimal TotalSales
);

var summary = await connection.QueryAsync<ProductSummary>(
    @"SELECT p.product_id, p.product_name, SUM(oi.unit_price * oi.quantity) as total_sales
      FROM products p
      JOIN order_items oi ON p.product_id = oi.product_id
      GROUP BY p.product_id, p.product_name"
);
```

## Troubleshooting

### Column Not Mapping

**Problem**: Query returns data but properties are not populated.

**Solution**: Ensure column names in SQL match either:
1. The attribute name: `[DmColumn("user_id")]`
2. The property name (case-insensitive): `public int UserId` matches `user_id`

```csharp
// Won't work - column name mismatch
[DmColumn("user_id")]
public int UserId { get; set; }

var users = await connection.QueryAsync<User>("SELECT id FROM users"); // "id" != "user_id"

// Fix: Use correct column name in query
var users = await connection.QueryAsync<User>("SELECT user_id FROM users");
```

### Record Constructor Not Called

**Problem**: Record properties are null/default after query.

**Solution**: Ensure `EnableRecordSupport = true` (default) and column names match constructor parameters:

```csharp
// Record constructor parameter names must match columns (case-insensitive)
public record Product(int Id, string Name, decimal Price);

// Query column names should match constructor parameter names
var products = await connection.QueryAsync<Product>(
    "SELECT id, name, price FROM products" // Matches: Id, Name, Price
);
```

### Initialization Not Applied

**Problem**: Attributes are ignored, columns not mapped.

**Solution**: Ensure `DapperMaticTypeMapping.Initialize()` is called before any Dapper queries:

```csharp
// Wrong order
var users = await connection.QueryAsync<User>("SELECT * FROM users");
DapperMaticTypeMapping.Initialize(); // Too late!

// Correct order
DapperMaticTypeMapping.Initialize();
var users = await connection.QueryAsync<User>("SELECT * FROM users"); // Works!
```

## Performance Considerations

- **Initialization**: Type mapping initialization happens once - there's no per-query overhead
- **Caching**: Property mappings are cached after first use
- **No Reflection at Query Time**: Mapping decisions are made once and reused
- **Same Performance as Dapper**: After initialization, query performance is identical to standard Dapper

## Limitations

1. **Global State**: Type mapping affects all Dapper queries in the application after initialization
2. **One Mapping Per Type**: Each property can only have one active column mapping
3. **No Dynamic Mapping**: Column mappings are determined at initialization, not at query time
4. **Constructor Matching**: For records, ALL constructor parameters must match available columns

## What's Next?

- [Data Annotations Reference](/guide/data-annotations) - Complete list of supported attributes
- [Type Mapping Reference](/guide/providers#data-type-mapping) - .NET to SQL type conversions
- [Extension Methods](/guide/extension-methods/) - DDL operations reference

## Related Topics

- [Database Providers](/guide/providers) - Supported databases and type mappings
- [Data Annotations](/guide/data-annotations) - Attribute reference guide
- [Models](/guide/models) - Working with DmTable and DmColumn
