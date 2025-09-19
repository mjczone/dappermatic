# Extension Methods

DapperMatic provides a comprehensive set of extension methods for `IDbConnection` that enable DDL (Data Definition Language) operations across multiple database providers. These methods allow you to create, modify, and introspect database schemas programmatically.

## Overview

All extension methods follow consistent patterns:

- **Async operations** - All methods are asynchronous and return `Task` or `Task<T>`
- **Provider agnostic** - Same API works across SQL Server, MySQL, PostgreSQL, and SQLite
- **Transaction support** - Optional `IDbTransaction` parameter for atomic operations
- **Timeout control** - Optional `commandTimeout` parameter for long-running operations
- **Cancellation support** - `CancellationToken` parameter for graceful cancellation

## Method Categories

The extension methods are organized into logical groups based on the database objects they operate on:

### [General Methods](./general-methods)

Core utility methods for connection testing and provider detection.

### [Schema Methods](./schema-methods)

Methods for creating, checking, and managing database schemas.

### [Table Methods](./table-methods)

Complete table lifecycle management including creation, modification, and introspection.

### [Column Methods](./column-methods)

Column-specific operations for adding, modifying, and removing table columns.

### [Primary Key Methods](./primary-key-constraint-methods)

Primary key constraint creation, modification, and removal.

### [Check Constraint Methods](./check-constraint-methods)

Business rule enforcement through check constraints.

### [Default Constraint Methods](./default-constraint-methods)

Default value constraint management.

### [Foreign Key Methods](./foreign-key-constraint-methods)

Relationship management through foreign key constraints.

### [Unique Constraint Methods](./unique-constraint-methods)

Uniqueness enforcement through unique constraints.

### [Index Methods](./index-methods)

Performance optimization through database indexes.

### [View Methods](./view-methods)

Database view creation, modification, and management.

## Common Usage Patterns

### Basic Connection Usage

```csharp
using var connection = new SqlConnection(connectionString);

// Test connection
if (await connection.TestConnectionAsync())
{
    // Perform DDL operations
    await connection.CreateSchemaIfNotExistsAsync("app");
    await connection.CreateTableIfNotExistsAsync("app", myTable);
}
```

### Transaction-Based Operations

```csharp
using var transaction = connection.BeginTransaction();
try
{
    await connection.CreateTableIfNotExistsAsync("dbo", usersTable, tx: transaction);
    await connection.CreateTableIfNotExistsAsync("dbo", ordersTable, tx: transaction);

    transaction.Commit();
}
catch
{
    transaction.Rollback();
    throw;
}
```

### Introspection and Schema Discovery

```csharp
// Discover existing schema
var tables = await connection.GetTablesAsync("dbo");
var columns = await connection.GetColumnsAsync("dbo", "Users");
var constraints = await connection.GetConstraintsAsync("dbo", "Users");

// Check for existence before creating
if (!await connection.DoesTableExistAsync("dbo", "Users"))
{
    await connection.CreateTableIfNotExistsAsync("dbo", userTable);
}
```

## Provider Support Matrix

| Method Category    | SQL Server | MySQL | PostgreSQL | SQLite   |
| ------------------ | ---------- | ----- | ---------- | -------- |
| General Methods    | ✅         | ✅    | ✅         | ✅       |
| Schema Methods     | ✅         | ✅    | ✅         | ⚠️\*     |
| Table Methods      | ✅         | ✅    | ✅         | ✅       |
| Column Methods     | ✅         | ✅    | ✅         | ⚠️\*\*   |
| Constraint Methods | ✅         | ✅    | ✅         | ⚠️\*\*\* |
| Index Methods      | ✅         | ✅    | ✅         | ✅       |
| View Methods       | ✅         | ✅    | ✅         | ✅       |

**Notes:**

- \*SQLite doesn't have schemas but methods work with default schema
- \*\*SQLite has limited column modification capabilities - DapperMatic overcomes these limitations by automatically recreating tables with data preservation when needed
- \*\*\*SQLite has limited constraint support (no foreign keys by default)

## Error Handling

DapperMatic extension methods throw standard .NET exceptions:

```csharp
try
{
    await connection.CreateTableIfNotExistsAsync("dbo", invalidTable);
}
catch (InvalidOperationException ex)
{
    // Handle DapperMatic-specific errors
    Console.WriteLine($"Configuration error: {ex.Message}");
}
catch (SqlException ex)
{
    // Handle database-specific errors
    Console.WriteLine($"Database error: {ex.Message}");
}
catch (TimeoutException ex)
{
    // Handle timeout errors
    Console.WriteLine($"Operation timed out: {ex.Message}");
}
```

## Best Practices

1. **Use transactions** for related operations
2. **Check existence** before creating objects
3. **Set appropriate timeouts** for complex operations
4. **Use cancellation tokens** for long-running tasks
5. **Handle provider differences** gracefully
6. **Test thoroughly** across your target providers
7. **Use introspection** to understand existing schemas

Explore the specific method categories to learn about the detailed capabilities available for each type of database object.
