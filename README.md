# DapperMatic

> ⚠️ **UNDER DEVELOPMENT - BREAKING CHANGES EXPECTED**
>
> This library is in active development (v0.x.x). The API is not yet stable and breaking changes may occur between releases. Do not use in production until v1.0.0 is released.

**Model-first database schema management and query compatibility for .NET applications**

DapperMatic extends `IDbConnection` with extension methods for **DDL operations** (create/modify/inspect schemas) and **DML query compatibility** (enhanced Dapper queries with attribute-based column mapping) across SQL Server, MySQL/MariaDB, PostgreSQL, and SQLite.

[![.github/workflows/build-and-test.yml](https://github.com/mjczone/dappermatic/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/mjczone/dappermatic/actions/workflows/build-and-test.yml)
[![.github/workflows/release.yml](https://github.com/mjczone/dappermatic/actions/workflows/release.yml/badge.svg)](https://github.com/mjczone/dappermatic/actions/workflows/release.yml)
[![NuGet](https://img.shields.io/nuget/v/MJCZone.DapperMatic.svg?label=NuGet)](https://www.nuget.org/packages/MJCZone.DapperMatic/)
[![.NET](https://img.shields.io/badge/.NET-8.0+-purple.svg)](https://dotnet.microsoft.com/download)
[![License: LGPL v3](https://img.shields.io/badge/License-LGPL_v3-blue.svg)](LICENSE)

📚 **[Full Documentation](https://dappermatic.mjczone.com/)**

---

## Why DapperMatic?

DapperMatic offers an **alternative approach** to database schema management and query mapping for .NET applications. 

How DapperMatic might fit your needs:

- **Lightweight & Flexible**: You want a lightweight library that extends Dapper without the overhead of a full ORM
- **Extremely Fast to get started**: You want to define your database schema using C# models with attributes and create/modify schemas with an intuitive API
- **Runtime Schema Management**: You need to create or modify database schemas at runtime (multi-tenant apps, dynamic schema generation, deployment-time setup)
- **Dapper + Attributes**: You prefer Dapper for queries but want attribute-based column mapping without writing repetitive property-to-column code
- **Cross-Provider Portability**: You need the same models and code to work across SQL Server, PostgreSQL, MySQL, and SQLite with provider-specific SQL generation

**Existing Tools**:
- **Entity Framework Core**: Full-featured ORM with change tracking, LINQ queries, and comprehensive migration tooling - could be your better choice for complex LINQ scenarios
- **FluentMigrator**: Version-controlled, code-based migrations with rollback support - better choice for team-based migration workflows

DapperMatic can coexist in the same application as your other data access tools.
The only DapperMatic dependency in the core library is Dapper.

---

## Installation

### Core Library
```bash
dotnet add package MJCZone.DapperMatic
```

### ASP.NET Core Integration
```bash
dotnet add package MJCZone.DapperMatic.AspNetCore
```

### Prerequisites

- **.NET 8.0** or later

---

## Supported Database Providers

| Provider | Versions Tested | Connection Types |
|----------|----------|------------------|
| **SQL Server** | 2017, 2019, 2022 | `Microsoft.Data.SqlClient`, `System.Data.SqlClient` |
| **PostgreSQL** | 15, 16, 17 | `Npgsql` |
| **MySQL** | 5.7, 8.4, 9.0+ | `MySqlConnector`, `MySQL.Data` |
| **MariaDB** | 10.11, 11.4, 11.8, 12.0 | `MySqlConnector`, `MySQL.Data` |
| **SQLite** | 3.x | `Microsoft.Data.Sqlite`, `System.Data.SQLite` |

---

## Quick Start

### 1. Define Your Models with Attributes

```csharp
using MJCZone.DapperMatic.DataAnnotations;
using System.Text.Json;

[DmTable("products")]
[DmIndex(false, new[] { "product_name" }, "IX_Product_Name")] // Non-unique index on product name
public class Product
{
    [DmColumn("product_id", isPrimaryKey: true, isAutoIncrement: true)]
    public int Id { get; set; }

    [DmColumn("product_name", length: 200, isNullable: false)]
    public string Name { get; set; } = string.Empty;

    [DmColumn("price", precision: 18, scale: 2, isNullable: false)]
    public decimal Price { get; set; }

    [DmColumn("category", isNullable: false)]
    public DayOfWeek Category { get; set; } // Enum stored as string

    [DmColumn("tags", isNullable: true)]
    public string[]? Tags { get; set; } // Smart array handling (native on PostgreSQL, JSON elsewhere)

    [DmColumn("metadata", isNullable: true)]
    public JsonDocument? Metadata { get; set; } // JSON storage

    [DmColumn("attributes", isNullable: true)]
    public Dictionary<string, object>? Attributes { get; set; } // Dictionary stored as JSON

    [DmColumn("created_at", isNullable: false)]
    public DateTime CreatedAt { get; set; }

    [DmColumn("shipping_time", isNullable: true)]
    public TimeSpan? ShippingTime { get; set; }

    // Navigation property - not mapped to database
    [DmIgnore]
    public List<ProductDetails>? Details { get; set; }
}

[DmTable("ProductDetails")]
public class ProductDetails
{
    [DmColumn("detail_id", isPrimaryKey: true, isAutoIncrement: true)]
    public int Id { get; set; }

    [DmColumn("product_id", isNullable: false)]
    [DmForeignKeyConstraint(
        referencedType: typeof(Product),
        referencedColumnNames: new[] { "product_id" }
    )] // Can be applied to Class as well
    public int ProductId { get; set; }

    [DmColumn("description", isNullable: false)]
    public string Description { get; set; } = string.Empty;

    [DmColumn("specs", isNullable: true)]
    public Dictionary<string, object>? Specifications { get; set; }
}
```

### 2. Create Tables from Models (DDL)

There are hundreds of extension methods available for schema management, using typed, untyped, and expression-based APIs.

Here are a few examples to get you started:

```csharp
using MJCZone.DapperMatic;
using Microsoft.Data.SqlClient;

// Connect to your database
using var connection = new SqlConnection("your-connection-string");
await connection.OpenAsync();

// Create a schema
await connection.CreateSchemaIfNotExistsAsync("public");

// Create tables from your models (automatically handles relationships, without
// needing to worry about the order of type creation)
await connection.CreateTablesIfNotExistsAsync([typeof(Product), typeof(ProductDetails)]);
// Or individually
await connection.CreateTableIfNotExistsAsync<Product>();
await connection.CreateTableIfNotExistsAsync(typeof(ProductDetails));

// Untyped version
await connection.CreateTableIfNotExistsAsync(
    /*schemaName or null for default*/ "public", 
    /*tableName*/ "products", 
    /*columns*/ new[]
    {
        new DmColumn("product_id", typeof(int), isPrimaryKey: true, isAutoIncrement: true),
        new DmColumn("product_name", typeof(string), length: 200, isNullable: false),
        new DmColumn("price", typeof(decimal), precision: 18, scale: 2, isNullable: false),
        // Additional columns...
    },
    /* Optional indexes, constraints, etc. */
    // , primaryKey:  new DmPrimaryKeyConstraint(...) { ...},
    // , checkConstraints:  [ new DmCheckConstraint(...) { ... }, ... ],
    // , defaultConstraints:  [ new DmDefaultConstraint(...) { ... }, ... ],
    // , uniqueConstraints:  [ new DmUniqueConstraint(...) { ... }, ... ],
    // , foreignKeyConstraints:  [ new DmForeignKeyConstraint(...) { ... }, ... ]
    // , indexes:  [ new DmIndex(...) { ... }, ... ]
);

// Verify tables exist
bool productTableExists = await connection.DoesTableExistAsync<Product>();
bool detailsTableExists = await connection.DoesTableExistAsync<ProductDetails>();

// Add a missing column
await connection.CreateColumnIfNotExistsAsync<Product>(
    new DmColumn("last_updated", typeof(DateTime), isNullable: true, /* additional optional arguments */)
);
// Or using expressions
await connection.CreateColumnIfNotExistsAsync<Product>(
    p => p.LastUpdated, 
    // Optional additional configuration
    col => 
    {
        col.IsNullable = true;
    }
);
// Or untyped version
await connection.CreateColumnIfNotExistsAsync(
    /*schemaName or null for default*/ "public", 
    /*tableName*/ "products", 
    /*columnName*/ "discount",
    /*dotnetType*/ typeof(decimal),
    configureColumn: col =>
    {
        /* Optional customizations */
        col.IsNullable = true;
        col.Precision = 5;
        col.Scale = 2;

        /* Additional provider-specific data types */
        col.ProviderDataTypes[DbProviderType.SqlServer] = "DECIMAL(5, 2)";
        col.ProviderDataTypes[DbProviderType.PostgreSql] = "NUMERIC(5, 2)";

        // Or use a custom types (e.g. with PostGIS extension)
        // See DML documentation for spatial type support
        //column.ProviderDataTypes[DbProviderType.PostgreSql] = "geometry(Point)";
    }
);

// Drop a column
await connection.DropColumnAsync<Product>("discount");
```

### 3. Use with Dapper Queries (DML)

```csharp
using MJCZone.DapperMatic.TypeMapping;
using Dapper;
using System.Text.Json;

// Initialize type mappings once at application startup
DapperMaticTypeMapping.Initialize();

// Insert with Dapper (column names are automatically mapped via DmColumn attributes)
var product = new Product
{
    Name = "Laptop",
    Price = 999.99m,
    Category = DayOfWeek.Monday,
    Tags = new[] { "electronics", "computers" },
    Metadata = JsonDocument.Parse(@"{""brand"": ""TechCorp""}"),
    Attributes = new Dictionary<string, object>
    {
        ["warranty"] = "2 years",
        ["inStock"] = true,
    },
    CreatedAt = DateTime.UtcNow,
    ShippingTime = TimeSpan.FromDays(3),
};

await connection.ExecuteAsync(
    @"INSERT INTO products (product_name, price, category, tags, metadata, attributes, created_at, shipping_time)
      VALUES (@Name, @Price, @Category, @Tags, @Metadata, @Attributes, @CreatedAt, @ShippingTime)",
    product
);

// Query with Dapper (DmColumn attributes automatically map database columns to properties)
var products = await connection.QueryAsync<Product>(
    @"SELECT product_id, product_name, price, category, tags, metadata, attributes, created_at, shipping_time
      FROM products
      WHERE price > @MinPrice",
    new { MinPrice = 500m }
);

foreach (var p in products)
{
    Console.WriteLine($"{p.Name}: ${p.Price}");
    Console.WriteLine($"  Tags: {string.Join(", ", p.Tags ?? Array.Empty<string>())}");
}
```

### 4. Cross-Provider Support (Same Code, Any Database)

```csharp
using Microsoft.Data.SqlClient;
using Npgsql;
using MySqlConnector;
using Microsoft.Data.Sqlite;

// Same API works across all supported databases
using var sqlServer = new SqlConnection(sqlServerConnectionString);
using var postgres = new NpgsqlConnection(postgresConnectionString);
using var mysql = new MySqlConnection(mysqlConnectionString);
using var sqlite = new SqliteConnection(sqliteConnectionString);

// Identical DDL operations across providers
await sqlServer.CreateTableIfNotExistsAsync<Product>();
await postgres.CreateTableIfNotExistsAsync<Product>();  // Arrays stored as native PostgreSQL arrays
await mysql.CreateTableIfNotExistsAsync<Product>();     // Arrays stored as JSON
await sqlite.CreateTableIfNotExistsAsync<Product>();    // Arrays stored as JSON
```

---

## Key Capabilities at a Glance

| DDL Operations | DML Operations | ASP.NET Core Integration |
|----------------|----------------|--------------------------|
| Create/Drop Tables | Smart Type Handlers | REST API Endpoints |
| Indexes & Constraints | Dapper Integration | Multi-Datasource Management |
| Schema Inspection | Array/JSON/XML Support | Authorization Hooks |
| Foreign Keys | Attribute-Based Mapping | Audit Logging |
| Type Mapping | Modern C# (Records) | Encrypted Connection Strings |

### DDL Operations - Complete Schema Management

```csharp
// Inspect existing schemas
var tableNames = await connection.GetTableNamesAsync("dbo");
var table = await connection.GetTableAsync("dbo", "products");
var columns = await connection.GetColumnsAsync("dbo", "products");
var indexes = await connection.GetIndexesAsync("dbo", "products");
var foreignKeys = await connection.GetForeignKeyConstraintsAsync("dbo", "products");

// Modify schemas
await connection.AddColumnAsync("dbo", "products", new DmColumn("discount", typeof(decimal)));
await connection.DropColumnAsync("dbo", "products", "discount");

// Drop tables
await connection.DropTableIfExistsAsync("dbo", "products");
```

### DML Operations - Advanced Type Support

DapperMatic provides smart type handlers for seamless Dapper query compatibility:

**Basic Types**: `int`, `long`, `string`, `decimal`, `DateTime`, `DateTimeOffset`, `TimeSpan`, `Guid`, `byte[]`

**Advanced Types**:
- **XML**: `XDocument` - Serialized across all providers
- **JSON**: `JsonDocument` - Native JSON on supported providers
- **Collections**: `Dictionary<TKey, TValue>`, `List<T>` - JSON serialization
- **Arrays**: `string[]`, `int[]`, `DateTime[]`, etc. - Native PostgreSQL arrays, JSON fallback
- **Network**: `IPAddress`, `PhysicalAddress`, `NpgsqlCidr` - PostgreSQL native, string elsewhere
- **Spatial**: NetTopologySuite types, PostgreSQL geometric types, MySQL geometry
- **Enums**: String-based storage with custom handlers

**Example with complex types**:
```csharp
[DmTable("articles")]
public class Article
{
    [DmColumn("article_id", isPrimaryKey: true, isAutoIncrement: true)]
    public int Id { get; set; }
    [DmColumn("content", isNullable: false)]
    public XDocument Content { get; set; } = null!;      // XML storage
    [DmColumn("settings", isNullable: false)]
    public JsonDocument Settings { get; set; } = null!;   // JSON storage
    [DmColumn("authors", isNullable: false)]
    public string[] Authors { get; set; } = null!;        // Array (native PG, JSON elsewhere)
    [DmColumn("source_ip", isNullable: true)]
    public IPAddress? SourceIp { get; set; }              // Network type
}

// After DapperMaticTypeMapping.Initialize(/* configureOptions */), Dapper queries just work
var articles = await connection.QueryAsync<Article>("SELECT * FROM articles");
```

---

## ASP.NET Core Integration

### Basic Setup

```csharp
using MJCZone.DapperMatic.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Register DapperMatic services with default in-memory datasource repositories
builder.Services.AddDapperMatic();

var app = builder.Build();

// Map DapperMatic API endpoints
app.UseDapperMatic();

app.Run();
```

This creates REST endpoints for managing database connections (datasources):
- `POST /api/dappermatic/datasources/list` - List all datasources
- `POST /api/dappermatic/datasources/get` - Get datasource by name
- `POST /api/dappermatic/datasources/add` - Add new datasource
- `POST /api/dappermatic/datasources/update` - Update existing datasource
- `POST /api/dappermatic/datasources/remove` - Remove datasource

### Custom Permission Handling

```csharp
using MJCZone.DapperMatic.AspNetCore.Interfaces;

// Implement custom permissions (e.g., check user roles from database)
public class CustomPermissions : IDapperMaticPermissions
{
    public CustomPermissions(/* Inject your dependencies here */)
    {
        // ...
    }

    public async Task<bool> IsAuthorizedAsync(IOperationContext context)
    {
        // Example: Check if user is authenticated
        if(context.User?.Identity?.IsAuthenticated != true)
            return false;

        //
        // Add additional checks as needed
        // 
        // The context has information about the current datasource,
        // operation, user, and access to critical request information.
        //

        return true;
    }
}

// Configure DapperMatic options from the appsettings.json file
builder.Services.Configure<DapperMaticOptions>(builder.Configuration.GetSection("DapperMatic"));

// Override the datasource repository
builder.Services.AddSingleton<IDapperMaticDatasourceRepository, CustomDatasourceRepository>();

// Register in DI
builder.Services.AddSingleton<IDapperMaticPermissions, CustomPermissions>();
builder.Services.AddDapperMatic();

// Or use the builder
builder.Services.AddDapperMatic(options =>
{
    // Optionally use static datasource definitions
    options.WithDatasources( ... );

    // Optionally customize a datasource ID factory
    options.UseCustomDatasourceIdFactory( ... );

    // Optional use a custom permissions implementation (by default is registered as Singleton)
    options.UseCustomPermissions<CustomPermissions>();

    // Optionally override the audity logger
    options.UseCustomAuditLogger<CustomAuditLogger>();

    // Optionally override the datasource repository
    options.UseCustomDatasourceRepository<CustomDatasourceRepository>();

    // Or use an existing datasource repository instance
    // ConnectionStrings are stored encrypted
    options.UseFileDatasourceRepository("path/to/datasources.json");
    options.UseDatabaseDatasourceRepository("postgresql" /*mysql,sqlite,sqlserver*/, "Host=...;Database=...;Username=...;Password=...;");
})
```

---

## Documentation & Resources

- 📚 **[Full Documentation](https://dappermatic.mjczone.com/)** - Comprehensive guides and API reference
- 📖 **[DML Query Support Guide](https://dappermatic.mjczone.com/guide/dml-query-support.html)** - Complete guide to Dapper integration
- 🗄️ **[Database Providers](https://dappermatic.mjczone.com/guide/providers.html)** - Supported databases and type mappings
- 💡 **[Getting Started](https://dappermatic.mjczone.com/guide/getting-started.html)** - Step-by-step tutorial
- 🧪 **[Test Examples](tests/)** - Extensive usage examples in the test suite

---

## License

This project is licensed under the GNU Lesser General Public License v3.0 or later (LGPL-3.0-or-later) - see the [LICENSE](LICENSE) file for details.

**What this means:**
- ✅ You can use DapperMatic in commercial applications
- ✅ You can modify and distribute DapperMatic
- ✅ Your application code remains under your chosen license
- ⚠️ Changes to DapperMatic itself must be contributed back under LGPL

---

## Support

- 🐛 **Bug Reports** - [GitHub Issues](https://github.com/mjczone/dappermatic/issues)
- 💬 **Discussions** - [GitHub Discussions](https://github.com/mjczone/dappermatic/discussions)
- 💻 **Contributing** - See [CONTRIBUTING.md](CONTRIBUTING.md) for current contribution guidelines

---

<div align="center">

**Built with ❤️ by MJCZone Inc.**

[Website](https://mjczone.com) • [GitHub](https://github.com/mjczone) • [NuGet](https://www.nuget.org/profiles/mjczone)

</div>
