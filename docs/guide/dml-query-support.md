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

### XML Support (Phase 3)

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

### JSON Support (Phase 4)

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

### Dictionary Support (Phase 4)

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

### List Support (Phase 4)

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

::: tip PostgreSQL Performance
PostgreSQL automatically benefits from native `xml` and `jsonb` types, which provide:
- Better query performance for filtering and searching
- Indexing capabilities for fast lookups
- Smaller storage footprint with jsonb's binary format
:::

### Array Support (Phase 5)

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
