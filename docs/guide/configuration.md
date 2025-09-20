# Configuration

DapperMatic is designed to work with minimal configuration, following convention-over-configuration principles. However, there are several ways to customize its behavior when needed.

## Table Factory Configuration

The `DmTableFactory` provides methods to customize how classes are mapped to `DmTable` instances.

### When Configuration is Applied

**Important**: Configuration in DapperMatic is applied **only when creating database schema objects (DDL operations)**, not during data queries. Specifically:

- ✅ **Applied during**: `CreateTableIfNotExistsAsync()`, `CreateViewIfNotExistsAsync()`, and other DDL operations
- ✅ **Applied when**: `DmTableFactory.GetTable(typeof(MyClass))` is called to generate table definitions
- ❌ **NOT applied during**: `QueryAsync()`, `ExecuteAsync()`, or any data access operations
- ❌ **NOT used for**: Query result mapping to C# objects

### What Adding Default Columns Does

When you add columns through configuration, you are **modifying the table schema definition** that DapperMatic will create in your database. This affects:

1. **Database Schema Creation**: Added columns become real database columns with constraints and defaults
2. **DDL Generation**: The columns are included in CREATE TABLE statements
3. **Schema Introspection**: DapperMatic will detect these columns when reading existing table structures
4. **Schema Validation**: The columns are considered part of the expected table structure

**Example Impact**:
```csharp
// Original User class (no created_at field)
[DmTable("dbo", "users")]
public class User
{
    [DmColumn("id", isPrimaryKey: true, isAutoIncrement: true)]
    public int Id { get; set; }

    [DmColumn("name", length: 100)]
    public string Name { get; set; }

    [DmColumn("email", length: 200)]
    public string Email { get; set; }
}

// Configure audit columns
DmTableFactory.Configure((type, table) => {
    // This adds REAL database columns
    table.Columns = table.Columns.Concat(new[] {
        new DmColumn("created_at", typeof(DateTime)) { DefaultExpression = "GETDATE()" }
    }).ToArray();
});

// When you call this, the created_at column will be physically created in the database
var userTable = DmTableFactory.GetTable(typeof(User));
await connection.CreateTableIfNotExistsAsync(userTable);

// The database now has: id, name, email, created_at (with GETDATE() default)
// Even though User class only has 3 properties, the table has 4 columns!
// Future INSERTs will automatically populate created_at if not specified
```

### Global Configuration

Configure mapping behavior for all types at application startup:

> **⚠️ Configuration Timing**: Configure DapperMatic **before** calling `DmTableFactory.GetTable(typeof(SomeClass))` for any type, as type-to-table mappings are cached after first generation. Ideally, set up configuration during application startup.

> **📝 What Gets Cached**: DapperMatic caches the **C# Type → DmTable mapping**, NOT database schema. The cache stores the result of analyzing your C# classes (attributes, properties, configuration) and converting them to `DmTable` objects. This is purely in-memory and has nothing to do with database introspection or schema caching.

> **🔄 Cache Management**: Currently, there is **no public API to clear or refresh the mapping cache**. Once a type is processed by `DmTableFactory.GetTable(typeof(SomeClass))`, its `DmTable` definition is permanently cached for the application lifetime. To apply new configuration to already-processed types, you need to restart the application.

```csharp
// Configure custom mapping logic for all types
DmTableFactory.Configure((type, table) =>
{
    // Apply custom logic to all generated tables
    if (type.Name.EndsWith("Entity"))
    {
        // Remove "Entity" suffix from table names
        table.TableName = table.TableName.Replace("Entity", "");
    }

    // Add audit columns to all tables
    if (!table.Columns.Any(c => c.ColumnName == "created_at"))
    {
        table.Columns = table.Columns.Concat(new[]
        {
            new DmColumn("created_at", typeof(DateTime))
            {
                IsNullable = false,
                DefaultExpression = "GETDATE()"
            },
            new DmColumn("updated_at", typeof(DateTime))
            {
                IsNullable = true
            }
        }).ToArray();
    }
});
```

### Type-Specific Configuration

Configure mapping for specific types:

```csharp
// Configure a specific type
DmTableFactory.Configure<Customer>(table =>
{
    // Customize the Customer table
    table.TableName = "app_customers";

    // Add a custom column for business logic
    var statusColumn = new DmColumn("status", typeof(string))
    {
        Length = 20,
        IsNullable = false,
        DefaultExpression = "'Active'"
    };
    table.Columns = table.Columns.Concat(new[] { statusColumn }).ToArray();

    // Add additional constraints
    table.CheckConstraints = table.CheckConstraints.Concat(new[]
    {
        new DmCheckConstraint("CK_Customer_Email_Valid", "email LIKE '%@%'"),
        new DmCheckConstraint("CK_Customer_Status_Valid", "status IN ('Active', 'Inactive', 'Suspended')")
    }).ToArray();
});

// Alternative syntax with Type parameter
DmTableFactory.Configure(typeof(Product), table =>
{
    table.SchemaName = "catalog";
    // Additional customizations...
});
```

### Configuration Best Practices

```csharp
public class DatabaseConfig
{
    public static void ConfigureDapperMatic()
    {
        DmTableFactory.Configure((type, table) =>
        {
            // 1. Standardize naming conventions
            if (string.IsNullOrEmpty(table.SchemaName))
            {
                table.SchemaName = GetSchemaForType(type);
            }

            // 2. Add common audit fields
            AddAuditFields(table);

            // 3. Apply organization-specific rules
            ApplyBusinessRules(type, table);
        });
    }

    private static string GetSchemaForType(Type type)
    {
        // Apply your organization's schema naming rules
        var namespaceParts = type.Namespace?.Split('.') ?? Array.Empty<string>();
        return namespaceParts.Length > 2 ? namespaceParts[2].ToLower() : "dbo";
    }

    private static void AddAuditFields(DmTable table)
    {
        var auditFields = new[]
        {
            new DmColumn("created_at", typeof(DateTime))
            {
                IsNullable = false,
                DefaultExpression = "GETDATE()"
            },
            new DmColumn("created_by", typeof(string))
            {
                Length = 100,
                IsNullable = false,
                DefaultExpression = "SYSTEM_USER"
            }
        };

        // Only add if they don't already exist
        var existingColumns = table.Columns.Select(c => c.ColumnName).ToHashSet();
        var newColumns = auditFields.Where(c => !existingColumns.Contains(c.ColumnName));
        table.Columns = table.Columns.Concat(newColumns).ToArray();
    }
}

// Call this at application startup
DatabaseConfig.ConfigureDapperMatic();
```

## Extension Method Parameters

DapperMatic extension methods accept standard Dapper method parameters (e.g.: QueryAsync, ExecuteAsync, ...) for customization:

### Command Timeout

Control how long database operations wait before timing out:

```csharp
// Set timeout for individual operations
var table = new DmTable(schema, "TableName", columns);
await connection.CreateTableIfNotExistsAsync(table, commandTimeout: 120); // 2 minutes

// For complex operations like large table creation
var complexTable = new DmTable("dbo", "ComplexTable", complexColumns);
await connection.CreateTableIfNotExistsAsync(complexTable, commandTimeout: 300); // 5 minutes

// Apply timeout to introspection operations
var tables = await connection.GetTablesAsync("dbo", commandTimeout: 60);
```

### Transaction Support

Use transactions for atomic operations:

```csharp
using var transaction = connection.BeginTransaction();

try
{
    // Create multiple related tables in a transaction
    var usersTable = new DmTable("dbo", "Users", userColumns);
    var ordersTable = new DmTable("dbo", "Orders", orderColumns);
    var orderItemsTable = new DmTable("dbo", "OrderItems", orderItemColumns);
    await connection.CreateTableIfNotExistsAsync(usersTable, tx: transaction);
    await connection.CreateTableIfNotExistsAsync(ordersTable, tx: transaction);
    await connection.CreateTableIfNotExistsAsync(orderItemsTable, tx: transaction);

    transaction.Commit();
}
catch
{
    transaction.Rollback();
    throw;
}
```

### Cancellation Token Support

Handle cancellation gracefully in async operations:

```csharp
public async Task CreateSchemaAsync(CancellationToken cancellationToken = default)
{
    var tables = GetRequiredTables();

    foreach (var table in tables)
    {
        var table = new DmTable("dbo", "TableName", tableColumns);
        await connection.CreateTableIfNotExistsAsync(
            table,
            cancellationToken: cancellationToken
        );

        // Check for cancellation between operations
        cancellationToken.ThrowIfCancellationRequested();
    }
}
```

## Provider-Specific Configuration

### Connection String Considerations

Different providers may require specific connection string parameters:

```csharp
// SQL Server - Enable Multiple Active Result Sets for complex operations
var sqlServerConnectionString = "Server=.;Database=MyApp;Trusted_Connection=true;MultipleActiveResultSets=true";

// PostgreSQL - Set command timeout and application name
var pgConnectionString = "Host=localhost;Database=myapp;Username=user;Password=pass;CommandTimeout=30;ApplicationName=MyApp";

// MySQL - Configure SSL and charset
var mysqlConnectionString = "Server=localhost;Database=myapp;Uid=user;Pwd=pass;SslMode=Required;CharSet=utf8mb4";

// SQLite - Enable foreign keys and set journal mode
var sqliteConnectionString = "Data Source=myapp.db;Foreign Keys=true;Journal Mode=WAL";
```

### Provider-Specific Data Types

Use provider-specific data types when needed:

```csharp
public class Document
{
    public int Id { get; set; }

    // Use provider-specific types for optimal performance
    [DmColumn("content",
              providerDataType: "{sqlserver:nvarchar(max),mysql:longtext,postgresql:text,sqlite:text}")]
    public string Content { get; set; }

    // JSON support varies by provider
    [DmColumn("metadata",
              providerDataType: "{sqlserver:nvarchar(max),mysql:json,postgresql:jsonb,sqlite:text}")]
    public string Metadata { get; set; }

    // UUID/GUID handling
    [DmColumn("document_id",
              providerDataType: "{sqlserver:uniqueidentifier,mysql:char(36),postgresql:uuid,sqlite:text}")]
    public Guid DocumentId { get; set; }
}
```

## Environment-Specific Configuration

### Development vs Production

Configure different behaviors based on environment:

```csharp
public static class DatabaseSetup
{
    public static void Configure(IConfiguration configuration)
    {
        var environment = configuration["Environment"];

        DmTableFactory.Configure((type, table) =>
        {
            if (environment == "Development")
            {
                // In development, add debug-friendly features
                AddDevelopmentFeatures(table);
            }
            else if (environment == "Production")
            {
                // In production, optimize for performance
                AddProductionOptimizations(table);
            }
        });
    }

    private static void AddDevelopmentFeatures(DmTable table)
    {
        // Add debug columns in development
        if (!table.Columns.Any(c => c.ColumnName == "debug_info"))
        {
            var debugColumn = new DmColumn("debug_info", typeof(string))
            {
                Length = 1000,
                IsNullable = true
            };
            table.Columns = table.Columns.Concat(new[] { debugColumn }).ToArray();
        }
    }

    private static void AddProductionOptimizations(DmTable table)
    {
        // Add performance-focused indexes
        var performanceIndexes = table.Columns
            .Where(c => c.ColumnName.EndsWith("_id") && !c.IsPrimaryKey)
            .Select(c => new DmIndex($"IX_{table.TableName}_{c.ColumnName}", new[] { c.ColumnName }));

        table.Indexes = table.Indexes.Concat(performanceIndexes).ToArray();
    }
}
```

## Configuration Validation

Validate your configuration to catch issues early:

```csharp
public static class ConfigurationValidator
{
    public static void ValidateTableConfiguration<T>()
    {
        var table = DmTableFactory.GetTable(typeof(T));

        // Validate table has primary key
        if (table.PrimaryKeyConstraint == null || table.PrimaryKeyConstraint.Columns.Count == 0)
        {
            throw new InvalidOperationException($"Table {table.TableName} must have a primary key");
        }

        // Validate foreign key references exist
        foreach (var fk in table.ForeignKeyConstraints ?? [])
        {
            if (string.IsNullOrEmpty(fk.ReferencedTableName))
            {
                throw new InvalidOperationException($"Foreign key {fk.ConstraintName} must reference a table");
            }
        }

        // Validate index coverage
        ValidateIndexCoverage(table);
    }

    private static void ValidateIndexCoverage(DmTable table)
    {
        var foreignKeyColumns = table.ForeignKeyConstraints
            .SelectMany(fk => fk.Columns.Select(c => c.ColumnName))
            .ToHashSet();

        var indexedColumns = table.Indexes
            .SelectMany(idx => idx.Columns.Select(c => c.ColumnName))
            .ToHashSet();

        var unindexedForeignKeys = foreignKeyColumns.Except(indexedColumns);

        if (unindexedForeignKeys.Any())
        {
            Console.WriteLine($"Warning: Foreign key columns without indexes in {table.TableName}: {string.Join(", ", unindexedForeignKeys)}");
        }
    }
}

// Usage
ConfigurationValidator.ValidateTableConfiguration<User>();
ConfigurationValidator.ValidateTableConfiguration<Order>();
```

## Best Practices

1. **Configure early** - Set up factory configuration at application startup
2. **Use transactions** - Group related DDL operations in transactions
3. **Set appropriate timeouts** - Complex operations may need longer timeouts
4. **Validate configuration** - Check your setup in tests or at startup
5. **Environment-specific settings** - Different configurations for dev/prod
6. **Provider considerations** - Use provider-specific features when beneficial
7. **Monitor performance** - Use cancellation tokens for long-running operations

Remember that DapperMatic is designed to work well with minimal configuration. Only customize when you have specific requirements that differ from the sensible defaults.
