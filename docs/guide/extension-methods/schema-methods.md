# Schema Methods

Schema methods provide functionality for managing database schemas. Note that schema support varies by database provider - SQL Server and PostgreSQL have full schema support, while MySQL treats schemas as databases, and SQLite has no schema concept.

## Quick Navigation

- [Schema Support Detection](#schema-support-detection)
  - [SupportsSchemas](#supportsschemas) - Check if provider supports schemas
- [Schema Existence Checking](#schema-existence-checking)
  - [DoesSchemaExistAsync](#doesschemaexistasync) - Check if schema exists
- [Schema Creation](#schema-creation)
  - [CreateSchemaIfNotExistsAsync](#createschemaifnotexistsasync) - Create schema if not exists
- [Schema Discovery](#schema-discovery)
  - [GetSchemaNamesAsync](#getschemanamesasync) - Get list of existing schemas
- [Schema Deletion](#schema-deletion)
  - [DropSchemaIfExistsAsync](#dropschemaifexistsasync) - Drop schema if exists

## Graceful Provider Handling

**Important**: Schema methods are designed to be safe across all providers. They won't throw exceptions for unsupported operations:

- **SQLite**: Schema methods return `false` for create/drop operations and empty lists for discovery, since SQLite has no schema concept
- **MySQL**: Schema methods work but operate at the database level since MySQL schemas are databases  
- **SQL Server/PostgreSQL**: Full schema support with all methods working as expected

This design allows you to write provider-agnostic code that safely calls schema methods regardless of the underlying database's capabilities.

## Schema Support Detection

### SupportsSchemas

Check if the current database provider supports schemas.

```csharp
bool supportsSchemas = connection.SupportsSchemas();

if (supportsSchemas)
{
    Console.WriteLine("This provider supports schemas");
    await connection.CreateSchemaIfNotExistsAsync("app");
}
else
{
    Console.WriteLine("This provider uses a default schema only");
    // All objects will be created in the default schema
}
```

**Returns:** `bool` - `true` if the provider supports schemas, `false` otherwise

**Provider Support:**

- **SQL Server**: ✅ Full schema support
- **MySQL**: ⚠️ Limited support (schemas = databases, methods return `false`)
- **PostgreSQL**: ✅ Full schema support  
- **SQLite**: ❌ No schema support (methods return `false`)

## Schema Existence Checking

### DoesSchemaExistAsync

Check if a schema exists in the database.

**Note**: For providers without schema support (SQLite), this method always returns `false`. For MySQL, this checks if a database with the given name exists.

```csharp
// Check if schema exists
bool exists = await connection.DoesSchemaExistAsync("app");

if (exists)
{
    Console.WriteLine("Schema 'app' already exists");
}
else
{
    Console.WriteLine("Schema 'app' does not exist");
    await connection.CreateSchemaIfNotExistsAsync("app");
}


// With transaction and cancellation
using var transaction = connection.BeginTransaction();
bool exists = await connection.DoesSchemaExistAsync(
    "app",
    tx: transaction,
    cancellationToken: cancellationToken
);
```

**Parameters:**

- `schemaName` - Name of the schema to check
- `tx` (optional) - Database transaction
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if schema exists, `false` otherwise or if schemas are not supported

## Schema Creation

### CreateSchemaIfNotExistsAsync

Create a schema only if it doesn't already exist.

**Note**: For providers without schema support (SQLite, MySQL), this method always returns `false` since no schema is created.

```csharp
// Create schema if it doesn't exist
bool created = await connection.CreateSchemaIfNotExistsAsync("app");

if (created)
{
    Console.WriteLine("Schema 'app' was created");
}
else
{
    Console.WriteLine("Schema 'app' already existed");
}

// Create multiple schemas safely
var schemas = new[] { "app", "reporting", "audit" };
foreach (var schema in schemas)
{
    bool wasCreated = await connection.CreateSchemaIfNotExistsAsync(schema);
    Console.WriteLine($"Schema '{schema}': {(wasCreated ? "Created" : "Already exists")}");
}
```

**Parameters:**

- `schemaName` - Name of the schema to create
- `tx` (optional) - Database transaction
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if schema was created, `false` if it already existed or if schemas are not supported

## Schema Discovery

### GetSchemaNamesAsync

Retrieve a list of existing schema names, with optional filtering.

**Note**: For providers without schema support (SQLite), this method returns an empty list. For MySQL, this returns database names.

```csharp
// Get all schemas
List<string> allSchemas = await connection.GetSchemaNamesAsync();
foreach (string schema in allSchemas)
{
    Console.WriteLine($"Found schema: {schema}");
}

// Get schemas with wildcard filter
List<string> appSchemas = await connection.GetSchemaNamesAsync(schemaNameFilter: "app*");
// Finds: app, app_dev, app_test, etc.

// Get schemas with pattern matching
List<string> testSchemas = await connection.GetSchemaNamesAsync(schemaNameFilter: "*test*");
// Finds: test, app_test, test_reporting, etc.

```

**Parameters:**

- `schemaNameFilter` (optional) - Wildcard pattern to filter schema names (`*` = any characters, `?` = single character)
- `tx` (optional) - Database transaction
- `cancellationToken` (optional) - Cancellation token

**Returns:** `List<string>` - List of matching schema names (empty list for providers without schema support)

## Schema Deletion

### DropSchemaIfExistsAsync

Drop a schema only if it exists.

**Note**: For providers without schema support (SQLite, MySQL), this method always returns `false` since no schema exists to drop.

```csharp
// Drop schema if it exists
bool dropped = await connection.DropSchemaIfExistsAsync("old_app");

if (dropped)
{
    Console.WriteLine("Schema 'old_app' was dropped");
}
else
{
    Console.WriteLine("Schema 'old_app' did not exist");
}

// Clean up multiple schemas
var schemasToClean = new[] { "temp", "test", "backup" };
foreach (var schema in schemasToClean)
{
    bool wasDropped = await connection.DropSchemaIfExistsAsync(schema);
    Console.WriteLine($"Schema '{schema}': {(wasDropped ? "Dropped" : "Not found")}");
}
```

**Parameters:**

- `schemaName` - Name of the schema to drop
- `tx` (optional) - Database transaction
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if schema was dropped, `false` if it didn't exist or if schemas are not supported

## Practical Examples

### Application Schema Setup

```csharp
public async Task SetupApplicationSchemasAsync(IDbConnection connection)
{
    var requiredSchemas = new[] { "app", "reporting", "audit" };

    // Check if provider supports schemas
    if (!connection.SupportsSchemas())
    {
        Console.WriteLine("Provider doesn't support schemas - using default schema");
        return;
    }

    // Create all required schemas
    foreach (var schema in requiredSchemas)
    {
        bool created = await connection.CreateSchemaIfNotExistsAsync(schema);
        Console.WriteLine($"Schema '{schema}': {(created ? "Created" : "Already exists")}");
    }
}
```

### Cross-Provider Safe Schema Operations

```csharp
public async Task SafeSchemaOperationsAsync(IDbConnection connection)
{
    // This code works safely across all providers
    // SQLite: returns false, no exception
    // MySQL: operates at database level  
    // SQL Server/PostgreSQL: full schema support
    
    bool created = await connection.CreateSchemaIfNotExistsAsync("myapp");
    Console.WriteLine($"Schema created: {created}");
    
    // Safe to check existence regardless of provider
    bool exists = await connection.DoesSchemaExistAsync("myapp");
    Console.WriteLine($"Schema exists: {exists}");
    
    // Discovery works across providers (empty list for SQLite)
    var schemas = await connection.GetSchemaNamesAsync();
    Console.WriteLine($"Found {schemas.Count} schemas: {string.Join(", ", schemas)}");
    
    // Safe cleanup - no exception if schema doesn't exist or isn't supported
    bool dropped = await connection.DropSchemaIfExistsAsync("temp_schema");
    Console.WriteLine($"Schema dropped: {dropped}");
}
```

### Schema Migration

```csharp
public async Task MigrateSchemaAsync(IDbConnection connection, string oldSchema, string newSchema)
{
    using var transaction = connection.BeginTransaction();
    try
    {
        // Create new schema
        await connection.CreateSchemaIfNotExistsAsync(newSchema, tx: transaction);

        // Get all tables from old schema
        var tables = await connection.GetTablesAsync(oldSchema, tx: transaction);

        // Migrate each table (simplified - would need to handle foreign keys, etc.)
        foreach (var table in tables)
        {
            // Create table in new schema
            table.SchemaName = newSchema;
            await connection.CreateTableIfNotExistsAsync(table, tx: transaction);

            // Copy data (implementation would vary)
            await CopyTableDataAsync(connection, oldSchema, newSchema, table.TableName, transaction);
        }

        // Drop old tables
        foreach (var table in tables.Reverse())
        {
            await connection.DropTableIfExistsAsync(oldSchema, table.TableName, tx: transaction);
        }

        // Drop old schema
        await connection.DropSchemaIfExistsAsync(oldSchema, tx: transaction);

        transaction.Commit();
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
}
```

### Schema Cleanup

```csharp
public async Task CleanupTestSchemasAsync(IDbConnection connection)
{
    if (!connection.SupportsSchemas()) return;

    // Find all test schemas
    var allSchemas = await connection.GetSchemaNamesAsync();
    var testSchemas = allSchemas.Where(s =>
        s.StartsWith("test_") ||
        s.EndsWith("_test") ||
        s.Contains("temp")).ToList();

    Console.WriteLine($"Found {testSchemas.Count} test schemas to clean up");

    foreach (var schema in testSchemas)
    {
        try
        {
            // Drop all tables in schema first
            var tables = await connection.GetTablesAsync(schema);
            foreach (var table in tables.Reverse())
            {
                await connection.DropTableIfExistsAsync(schema, table.TableName);
            }

            // Drop the schema
            bool dropped = await connection.DropSchemaIfExistsAsync(schema);
            if (dropped)
            {
                Console.WriteLine($"Cleaned up schema: {schema}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to clean up schema {schema}: {ex.Message}");
        }
    }
}
```

### Schema Inspection

```csharp
public async Task InspectDatabaseSchemasAsync(IDbConnection connection)
{
    if (!connection.SupportsSchemas())
    {
        Console.WriteLine("Database uses single default schema");
        return;
    }

    var schemas = await connection.GetSchemaNamesAsync();
    Console.WriteLine($"Found {schemas.Count} schemas:");

    foreach (var schema in schemas.OrderBy(s => s))
    {
        var tables = await connection.GetTablesAsync(schema);
        var views = await connection.GetViewsAsync(schema);

        Console.WriteLine($"  {schema}:");
        Console.WriteLine($"    Tables: {tables.Count}");
        Console.WriteLine($"    Views: {views.Count}");

        if (tables.Any())
        {
            Console.WriteLine($"    Table names: {string.Join(", ", tables.Select(t => t.TableName))}");
        }
    }
}
```

## Provider-Specific Notes

### SQL Server

- Schemas are separate from databases
- Default schema is `dbo`
- Can set default schema per user
- Schema ownership can be transferred

### MySQL

- Schemas are synonymous with databases
- `CREATE SCHEMA` is equivalent to `CREATE DATABASE`
- Schema name must be a valid database name

### PostgreSQL

- Full schema support with ownership
- Default schema is `public`
- Search path determines schema resolution order
- Supports schema-level permissions

### SQLite

- **No schema support** - single default schema only
- All objects created in main database file
- **Schema method behavior**:
  - `SupportsSchemas()` returns `false`
  - `DoesSchemaExistAsync()` always returns `false`
  - `CreateSchemaIfNotExistsAsync()` always returns `false` (no-op)
  - `DropSchemaIfExistsAsync()` always returns `false` (no-op)
  - `GetSchemaNamesAsync()` returns empty list

### MySQL

- **Limited schema support** - schemas are synonymous with databases
- `CREATE SCHEMA` is equivalent to `CREATE DATABASE`
- **Schema method behavior**:
  - Methods work but operate at database level
  - Consider using database-specific methods instead
  - Creating schemas creates new databases

Schema methods provide the foundation for organizing database objects in a logical hierarchy, essential for multi-tenant applications and large-scale database designs.
