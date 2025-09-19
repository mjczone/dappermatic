# Configuration

DapperMatic is designed to work with minimal configuration, following convention-over-configuration principles. However, there are several ways to customize its behavior when needed.

## Table Factory Configuration

The `DmTableFactory` provides methods to customize how classes are mapped to `DmTable` instances.

### Global Configuration

Configure mapping behavior for all types at application startup:

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

    // Add a computed column
    var computedColumn = new DmColumn("full_name", typeof(string))
    {
        IsNullable = true,
        IsComputed = true,
        ComputedExpression = "first_name + ' ' + last_name"
    };
    table.Columns = table.Columns.Concat(new[] { computedColumn }).ToArray();

    // Add additional constraints
    table.CheckConstraints = table.CheckConstraints.Concat(new[]
    {
        new DmCheckConstraint("CK_Customer_Email_Valid", "email LIKE '%@%'")
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
                MaxLength = 100,
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

DapperMatic extension methods accept standard Dapper parameters for customization:

### Command Timeout

Control how long database operations wait before timing out:

```csharp
// Set timeout for individual operations
await connection.CreateTableIfNotExistsAsync(schema, table, commandTimeout: 120); // 2 minutes

// For complex operations like large table creation
await connection.CreateTableIfNotExistsAsync("dbo", complexTable, commandTimeout: 300); // 5 minutes

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
    await connection.CreateTableIfNotExistsAsync("dbo", usersTable, tx: transaction);
    await connection.CreateTableIfNotExistsAsync("dbo", ordersTable, tx: transaction);
    await connection.CreateTableIfNotExistsAsync("dbo", orderItemsTable, tx: transaction);

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
        await connection.CreateTableIfNotExistsAsync(
            "dbo",
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
                MaxLength = 1000,
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
        if (table.PrimaryKey == null || table.PrimaryKey.Columns.Length == 0)
        {
            throw new InvalidOperationException($"Table {table.TableName} must have a primary key");
        }

        // Validate foreign key references exist
        foreach (var fk in table.ForeignKeys ?? Array.Empty<DmForeignKeyConstraint>())
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
        var foreignKeyColumns = table.ForeignKeys?
            .SelectMany(fk => fk.Columns.Select(c => c.ColumnName))
            .ToHashSet() ?? new HashSet<string>();

        var indexedColumns = table.Indexes?
            .SelectMany(idx => idx.Columns.Select(c => c.ColumnName))
            .ToHashSet() ?? new HashSet<string>();

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
