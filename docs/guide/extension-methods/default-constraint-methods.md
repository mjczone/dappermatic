# Default Constraint Methods

Default constraint methods provide comprehensive functionality for managing default value constraints across all supported database providers. Default constraints automatically provide values for columns when no value is explicitly specified during INSERT operations.

## Quick Navigation

- [Default Constraint Existence Checking](#default-constraint-existence-checking)
  - [DoesDefaultConstraintExistAsync](#doesdefaultconstraintexistasync) - Check if named default constraint exists
  - [DoesDefaultConstraintExistOnColumnAsync](#doesdefaultconstraintexistoncolumnasync) - Check if any default constraint exists on column
- [Default Constraint Creation](#default-constraint-creation)
  - [CreateDefaultConstraintIfNotExistsAsync (Model)](#createdefaultconstraintifnotexistsasync-dmdefaultconstraint) - Create from DmDefaultConstraint model
  - [CreateDefaultConstraintIfNotExistsAsync (Parameters)](#createdefaultconstraintifnotexistsasync-parameters) - Create with individual parameters
- [Default Constraint Discovery](#default-constraint-discovery)
  - [GetDefaultConstraintNamesAsync](#getdefaultconstraintnamesasync) - Get list of default constraint names with filtering
  - [GetDefaultConstraintNameOnColumnAsync](#getdefaultconstraintnameoncolumnasync) - Get default constraint name on specific column
  - [GetDefaultConstraintsAsync](#getdefaultconstraintsasync) - Get complete default constraint models
  - [GetDefaultConstraintAsync](#getdefaultconstraintasync) - Get single default constraint by name
  - [GetDefaultConstraintOnColumnAsync](#getdefaultconstraintoncolumnasync) - Get default constraint model on specific column
- [Default Constraint Deletion](#default-constraint-deletion)
  - [DropDefaultConstraintIfExistsAsync](#dropdefaultconstraintifexistsasync) - Drop default constraint by name
  - [DropDefaultConstraintOnColumnIfExistsAsync](#dropdefaultconstraintoncolumnifexistsasync) - Drop default constraint on specific column

## Default Constraint Existence Checking

### DoesDefaultConstraintExistAsync

Check if a specific named default constraint exists.

```csharp
// Check if named default constraint exists
var constraintName = "DF_Orders_OrderDate";
bool exists = await connection.DoesDefaultConstraintExistAsync("sales", "orders", constraintName);

if (exists)
{
    Console.WriteLine($"Default constraint '{constraintName}' exists");

    // Get the default constraint details
    var defaultConstraint = await connection.GetDefaultConstraintAsync("sales", "orders", constraintName);
    if (defaultConstraint != null)
    {
        Console.WriteLine($"Expression: {defaultConstraint.Expression}");
        Console.WriteLine($"Column: {defaultConstraint.ColumnName}");
    }
}
else
{
    Console.WriteLine($"Default constraint '{constraintName}' does not exist");
}

// With transaction and cancellation
using var transaction = connection.BeginTransaction();
bool exists = await connection.DoesDefaultConstraintExistAsync(
    "sales",
    "orders",
    constraintName,
    tx: transaction,
    cancellationToken: cancellationToken
);
```

**Parameters:**

- `schemaName` - Schema containing the table
- `tableName` - Name of the table containing the default constraint
- `constraintName` - Name of the default constraint to check
- `tx` (optional) - Database transaction
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if default constraint exists, `false` otherwise

### DoesDefaultConstraintExistOnColumnAsync

Check if any default constraint exists on a specific column.

```csharp
// Check if any default constraint exists on a column
bool hasDefault = await connection.DoesDefaultConstraintExistOnColumnAsync("sales", "orders", "order_date");

if (hasDefault)
{
    Console.WriteLine("Column 'order_date' has a default constraint");

    // Get the default constraint on this column
    var constraint = await connection.GetDefaultConstraintOnColumnAsync("sales", "orders", "order_date");
    if (constraint != null)
    {
        Console.WriteLine($"Default constraint '{constraint.ConstraintName}' expression: {constraint.Expression}");
    }
}
else
{
    Console.WriteLine("Column 'order_date' has no default constraint");
}

// With transaction
using var transaction = connection.BeginTransaction();
bool exists = await connection.DoesDefaultConstraintExistOnColumnAsync(
    "hr", "employees", "hire_date", tx: transaction);
```

**Parameters:**

- `schemaName` - Schema containing the table
- `tableName` - Name of the table containing the column
- `columnName` - Name of the column to check for default constraints
- `tx` (optional) - Database transaction
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if any default constraint exists on the column, `false` otherwise

## Default Constraint Creation

### CreateDefaultConstraintIfNotExistsAsync (DmDefaultConstraint)

Create a default constraint only if it doesn't already exist using a DmDefaultConstraint model.

```csharp
// Create default constraint if it doesn't exist
bool created = await connection.CreateDefaultConstraintIfNotExistsAsync(defaultConstraint);

if (created)
{
    Console.WriteLine($"Default constraint '{defaultConstraint.ConstraintName}' was created");
}
else
{
    Console.WriteLine($"Default constraint '{defaultConstraint.ConstraintName}' already existed");
}
```

**Parameters:**

- `schemaName` - Schema containing the table
- `tableName` - Name of the table to add default constraint to
- `defaultConstraint` - DmDefaultConstraint model defining the constraint
- `tx` (optional) - Database transaction
- `` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if default constraint was created, `false` if it already existed

### CreateDefaultConstraintIfNotExistsAsync (Parameters)

Create a default constraint using individual parameters for convenience.

```csharp
// Current timestamp default for order date
bool created = await connection.CreateDefaultConstraintIfNotExistsAsync(
    schemaName: "sales",
    tableName: "orders",
    constraintName: "DF_Orders_OrderDate",
    columnName: "order_date",
    expression: "GETDATE()"
);

// GUID default for new IDs
bool created = await connection.CreateDefaultConstraintIfNotExistsAsync(
    "app",
    "users",
    "DF_Users_UserId",
    "user_id",
    "NEWID()",
    tx: transaction,
    : 60,
    cancellationToken: cancellationToken
);

// Boolean default for active status
bool created = await connection.CreateDefaultConstraintIfNotExistsAsync(
    "hr",
    "employees",
    "DF_Employees_IsActive",
    "is_active",
    "1"
);

// Numeric default for quantity
bool created = await connection.CreateDefaultConstraintIfNotExistsAsync(
    "inventory",
    "products",
    "DF_Products_MinQuantity",
    "min_quantity",
    "0"
);

// String default for status
bool created = await connection.CreateDefaultConstraintIfNotExistsAsync(
    "orders",
    "order_items",
    "DF_OrderItems_Status",
    "status",
    "'pending'"
);
```

**Parameters:**

- `schemaName` - Schema containing the table
- `tableName` - Name of the table to add constraint to
- `constraintName` - Name of the default constraint
- `columnName` - Name of the column to apply default to
- `expression` - SQL expression that provides the default value
- `tx` (optional) - Database transaction
- `` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if default constraint was created, `false` if it already existed

## Default Constraint Discovery

### GetDefaultConstraintNamesAsync

Retrieve a list of default constraint names from a table, with optional filtering.

```csharp
// Get all default constraint names on a table
List<string> allConstraints = await connection.GetDefaultConstraintNamesAsync("sales", "orders");
foreach (string constraintName in allConstraints)
{
    Console.WriteLine($"Found default constraint: {constraintName}");
}

// Get default constraint names with wildcard filter
List<string> dateDefaults = await connection.GetDefaultConstraintNamesAsync("sales", "orders", "DF_*_Date*");
// Finds: DF_Orders_OrderDate, DF_Orders_ShipDate, etc.

// Get constraints with pattern matching
List<string> idDefaults = await connection.GetDefaultConstraintNamesAsync("app", "users", "*_Id");
// Finds: DF_Users_UserId, DF_Users_TenantId, etc.
```

**Parameters:**

- `schemaName` - Schema containing the table
- `tableName` - Name of the table to search
- `nameFilter` (optional) - Wildcard pattern to filter constraint names (`*` = any characters, `?` = single character)
- `tx` (optional) - Database transaction
- `` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `List<string>` - List of matching default constraint names

### GetDefaultConstraintNameOnColumnAsync

Get the default constraint name that applies to a specific column.

```csharp
// Get default constraint name on a specific column
string? constraintName = await connection.GetDefaultConstraintNameOnColumnAsync("sales", "orders", "order_date");

if (constraintName != null)
{
    Console.WriteLine($"Default constraint on 'order_date': {constraintName}");

    // Get full details of the default constraint
    var constraint = await connection.GetDefaultConstraintAsync("sales", "orders", constraintName);
    if (constraint != null)
    {
        Console.WriteLine($"Expression: {constraint.Expression}");
    }
}
else
{
    Console.WriteLine("No default constraint found on 'order_date' column");
}
```

**Parameters:**

- `schemaName` - Schema containing the table
- `tableName` - Name of the table containing the column
- `columnName` - Name of the column to find default constraint for
- `tx` (optional) - Database transaction
- `` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `string?` - Name of the default constraint, or `null` if none exists on the column

### GetDefaultConstraintsAsync

Retrieve complete DmDefaultConstraint models for table default constraints.

```csharp
// Get all default constraints with full structure information
List<DmDefaultConstraint> defaultConstraints = await connection.GetDefaultConstraintsAsync("sales", "orders");

foreach (var constraint in defaultConstraints)
{
    Console.WriteLine($"Default Constraint: {constraint.ConstraintName}");
    Console.WriteLine($"  Column: {constraint.ColumnName}");
    Console.WriteLine($"  Expression: {constraint.Expression}");
    Console.WriteLine($"  Schema: {constraint.SchemaName}");
    Console.WriteLine($"  Table: {constraint.TableName}");
}

// Get specific default constraints with pattern
List<DmDefaultConstraint> timestampDefaults = await connection.GetDefaultConstraintsAsync("app", "audit_log", "DF_*_Date*");

// With transaction
using var transaction = connection.BeginTransaction();
List<DmDefaultConstraint> constraints = await connection.GetDefaultConstraintsAsync("hr", "employees", tx: transaction);
```

**Parameters:**

- `schemaName` - Schema containing the table
- `tableName` - Name of the table to search
- `nameFilter` (optional) - Wildcard pattern to filter constraint names
- `tx` (optional) - Database transaction
- `` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `List<DmDefaultConstraint>` - List of complete DmDefaultConstraint models

### GetDefaultConstraintAsync

Retrieve a single DmDefaultConstraint model for a specific default constraint.

```csharp
// Get specific default constraint structure
DmDefaultConstraint? constraint = await connection.GetDefaultConstraintAsync("sales", "orders", "DF_Orders_OrderDate");

if (constraint != null)
{
    Console.WriteLine($"Default Constraint '{constraint.ConstraintName}' details:");
    Console.WriteLine($"  Table: {constraint.SchemaName}.{constraint.TableName}");
    Console.WriteLine($"  Column: {constraint.ColumnName}");
    Console.WriteLine($"  Expression: {constraint.Expression}");

    // Analyze the constraint expression
    if (constraint.Expression.Contains("GETDATE()") || constraint.Expression.Contains("NOW()"))
    {
        Console.WriteLine("  This provides current timestamp as default");
    }
    else if (constraint.Expression.Contains("NEWID()") || constraint.Expression.Contains("UUID()"))
    {
        Console.WriteLine("  This provides unique identifier as default");
    }
    else if (constraint.Expression == "0" || constraint.Expression == "1")
    {
        Console.WriteLine("  This provides numeric/boolean default");
    }
}
else
{
    Console.WriteLine("Default constraint not found");
}
```

**Parameters:**

- `schemaName` - Schema containing the table
- `tableName` - Name of the table containing the constraint
- `constraintName` - Name of the default constraint to retrieve
- `tx` (optional) - Database transaction
- `` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `DmDefaultConstraint?` - Complete default constraint model, or `null` if not found

### GetDefaultConstraintOnColumnAsync

Retrieve the default constraint that applies to a specific column.

```csharp
// Get default constraint on a specific column
DmDefaultConstraint? constraint = await connection.GetDefaultConstraintOnColumnAsync("sales", "orders", "order_date");

if (constraint != null)
{
    Console.WriteLine($"Default constraint on 'order_date': {constraint.ConstraintName}");
    Console.WriteLine($"Expression: {constraint.Expression}");

    // Check constraint type
    if (constraint.Expression.Contains("GETDATE()"))
    {
        Console.WriteLine("✅ Uses current date/time as default");
    }
    else if (constraint.Expression.Contains("0"))
    {
        Console.WriteLine("📊 Uses zero as default value");
    }
}
else
{
    Console.WriteLine("No default constraint found on 'order_date' column");
}
```

**Parameters:**

- `schemaName` - Schema containing the table
- `tableName` - Name of the table containing the column
- `columnName` - Name of the column to find default constraint for
- `tx` (optional) - Database transaction
- `` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `DmDefaultConstraint?` - Default constraint model, or `null` if none exists on the column

## Default Constraint Deletion

### DropDefaultConstraintIfExistsAsync

Remove a default constraint from a table if it exists.

```csharp
// Drop default constraint if it exists
bool dropped = await connection.DropDefaultConstraintIfExistsAsync("sales", "orders", "DF_Orders_OrderDate");

if (dropped)
{
    Console.WriteLine("Default constraint was dropped");
}
else
{
    Console.WriteLine("Default constraint did not exist");
}

// Batch drop multiple default constraints
var constraintsToRemove = new[] { "DF_Orders_TempDefault", "DF_Orders_OldDefault", "DF_Orders_BackupDefault" };
foreach (var constraintName in constraintsToRemove)
{
    bool wasDropped = await connection.DropDefaultConstraintIfExistsAsync("sales", "orders", constraintName);
    Console.WriteLine($"Default constraint '{constraintName}': {(wasDropped ? "Dropped" : "Not found")}");
}

// With transaction
using var transaction = connection.BeginTransaction();
try
{
    bool dropped = await connection.DropDefaultConstraintIfExistsAsync("sales", "orders", "DF_Orders_OrderDate", tx: transaction);
    transaction.Commit();
}
catch
{
    transaction.Rollback();
    throw;
}
```

**Parameters:**

- `schemaName` - Schema containing the table
- `tableName` - Name of the table containing the constraint
- `constraintName` - Name of the default constraint to drop
- `tx` (optional) - Database transaction
- `` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if default constraint was dropped, `false` if it didn't exist

### DropDefaultConstraintOnColumnIfExistsAsync

Remove the default constraint that applies to a specific column.

```csharp
// Drop default constraint on a specific column
bool dropped = await connection.DropDefaultConstraintOnColumnIfExistsAsync("sales", "orders", "legacy_status");

if (dropped)
{
    Console.WriteLine("Default constraint on 'legacy_status' was dropped");
    // Now safe to modify or drop the column
    await connection.DropColumnIfExistsAsync("sales", "orders", "legacy_status");
}
else
{
    Console.WriteLine("No default constraint found on 'legacy_status'");
}

// Useful before column modifications
await connection.DropDefaultConstraintOnColumnIfExistsAsync("hr", "employees", "salary");
// Now safe to modify the salary column defaults
await connection.CreateDefaultConstraintIfNotExistsAsync(
    "hr", "employees", "DF_Employees_DefaultSalary", "salary", "0.00");
```

**Parameters:**

- `schemaName` - Schema containing the table
- `tableName` - Name of the table containing the constraint
- `columnName` - Name of the column to drop default constraint for
- `tx` (optional) - Database transaction
- `` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if default constraint was dropped, `false` if none existed on the column

## Practical Examples

### Standard Default Values Setup

```csharp
public async Task SetupStandardDefaultsAsync(IDbConnection connection)
{
    using var transaction = connection.BeginTransaction();
    try
    {
        // User table defaults
        await connection.CreateDefaultConstraintIfNotExistsAsync(
            "app", "users", "DF_Users_CreatedAt", "created_at", "GETDATE()", tx: transaction);

        await connection.CreateDefaultConstraintIfNotExistsAsync(
            "app", "users", "DF_Users_IsActive", "is_active", "1", tx: transaction);

        await connection.CreateDefaultConstraintIfNotExistsAsync(
            "app", "users", "DF_Users_UserId", "user_id", "NEWID()", tx: transaction);

        // Order table defaults
        await connection.CreateDefaultConstraintIfNotExistsAsync(
            "sales", "orders", "DF_Orders_OrderDate", "order_date", "GETDATE()", tx: transaction);

        await connection.CreateDefaultConstraintIfNotExistsAsync(
            "sales", "orders", "DF_Orders_Status", "status", "'pending'", tx: transaction);

        await connection.CreateDefaultConstraintIfNotExistsAsync(
            "sales", "orders", "DF_Orders_Total", "total_amount", "0.00", tx: transaction);

        // Product table defaults
        await connection.CreateDefaultConstraintIfNotExistsAsync(
            "inventory", "products", "DF_Products_CreatedAt", "created_at", "GETDATE()", tx: transaction);

        await connection.CreateDefaultConstraintIfNotExistsAsync(
            "inventory", "products", "DF_Products_IsActive", "is_active", "1", tx: transaction);

        await connection.CreateDefaultConstraintIfNotExistsAsync(
            "inventory", "products", "DF_Products_StockLevel", "stock_level", "0", tx: transaction);

        transaction.Commit();
        Console.WriteLine("Standard default constraints created successfully");
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
}
```

### Default Constraint Analysis

```csharp
public async Task AnalyzeDefaultConstraintsAsync(IDbConnection connection, string schema)
{
    var tables = await connection.GetTableNamesAsync(schema);

    Console.WriteLine($"Default Constraint Analysis for schema '{schema}':");

    var totalConstraints = 0;
    var tablesWithDefaults = new List<string>();
    var expressionTypes = new Dictionary<string, int>();
    var columnTypes = new Dictionary<string, int>();

    foreach (var tableName in tables)
    {
        var constraints = await connection.GetDefaultConstraintsAsync(schema, tableName);

        if (constraints.Any())
        {
            tablesWithDefaults.Add(tableName);
            totalConstraints += constraints.Count;

            foreach (var constraint in constraints)
            {
                Console.WriteLine($"  {tableName}.{constraint.ConstraintName}:");
                Console.WriteLine($"    Column: {constraint.ColumnName}");
                Console.WriteLine($"    Expression: {constraint.Expression}");

                // Categorize expression types
                var expressionType = CategorizeExpression(constraint.Expression);
                expressionTypes[expressionType] = expressionTypes.GetValueOrDefault(expressionType, 0) + 1;

                // Categorize column types
                var columnType = CategorizeColumnType(constraint.ColumnName);
                columnTypes[columnType] = columnTypes.GetValueOrDefault(columnType, 0) + 1;
            }
        }
    }

    Console.WriteLine($"\nSummary:");
    Console.WriteLine($"Total tables: {tables.Count}");
    Console.WriteLine($"Tables with default constraints: {tablesWithDefaults.Count}");
    Console.WriteLine($"Total default constraints: {totalConstraints}");

    Console.WriteLine("\nExpression Types:");
    foreach (var (type, count) in expressionTypes.OrderByDescending(kvp => kvp.Value))
    {
        Console.WriteLine($"  {type}: {count}");
    }

    Console.WriteLine("\nColumn Types:");
    foreach (var (type, count) in columnTypes.OrderByDescending(kvp => kvp.Value))
    {
        Console.WriteLine($"  {type}: {count}");
    }
}

private string CategorizeExpression(string expression)
{
    var expr = expression.ToUpper();
    if (expr.Contains("GETDATE()") || expr.Contains("NOW()")) return "Timestamp";
    if (expr.Contains("NEWID()") || expr.Contains("UUID()")) return "GUID";
    if (expr == "0" || expr == "1") return "Numeric";
    if (expr.StartsWith("'") && expr.EndsWith("'")) return "String Literal";
    if (expr.Contains("USER") || expr.Contains("SYSTEM")) return "System Function";
    return "Other";
}

private string CategorizeColumnType(string columnName)
{
    var name = columnName.ToLower();
    if (name.Contains("date") || name.Contains("time")) return "Date/Time";
    if (name.Contains("id") || name.Contains("key")) return "Identifier";
    if (name.Contains("active") || name.Contains("enabled") || name.Contains("flag")) return "Boolean";
    if (name.Contains("status") || name.Contains("state")) return "Status";
    if (name.Contains("count") || name.Contains("quantity") || name.Contains("amount")) return "Numeric";
    return "Other";
}
```

### Default Constraint Maintenance

```csharp
public async Task StandardizeDefaultConstraintNamesAsync(IDbConnection connection, string schema)
{
    var tables = await connection.GetTableNamesAsync(schema);

    foreach (var tableName in tables)
    {
        var constraints = await connection.GetDefaultConstraintsAsync(schema, tableName);

        foreach (var constraint in constraints)
        {
            // Check if constraint name follows standard convention: DF_TableName_ColumnName
            var expectedName = $"DF_{tableName}_{constraint.ColumnName}";
            if (constraint.ConstraintName != expectedName)
            {
                Console.WriteLine($"Non-standard constraint name: {constraint.ConstraintName}");
                Console.WriteLine($"Expected: {expectedName}");
                Console.WriteLine($"Column: {constraint.ColumnName}");
                Console.WriteLine($"Expression: {constraint.Expression}");

                // Note: Renaming constraints would require dropping and recreating
                // This is left as an exercise since it's database-specific
            }
        }
    }
}

public async Task ValidateDefaultExpressionsAsync(IDbConnection connection, string schema)
{
    Console.WriteLine($"Validating default constraint expressions in schema '{schema}'...");

    var tables = await connection.GetTableNamesAsync(schema);
    var validationIssues = new List<string>();

    foreach (var tableName in tables)
    {
        var constraints = await connection.GetDefaultConstraintsAsync(schema, tableName);

        foreach (var constraint in constraints)
        {
            // Check for common issues with default expressions
            if (string.IsNullOrWhiteSpace(constraint.Expression))
            {
                validationIssues.Add($"Constraint '{constraint.ConstraintName}' has empty expression");
            }

            if (constraint.Expression.Contains("--") || constraint.Expression.Contains("/*"))
            {
                validationIssues.Add($"Constraint '{constraint.ConstraintName}' contains SQL comments - potential issue");
            }

            if (constraint.Expression.Length > 1000)
            {
                validationIssues.Add($"Constraint '{constraint.ConstraintName}' has very long expression (>{constraint.Expression.Length} chars)");
            }

            // Check for potentially problematic expressions
            if (constraint.Expression.Contains("SELECT"))
            {
                validationIssues.Add($"Constraint '{constraint.ConstraintName}' contains SELECT - may cause performance issues");
            }

            Console.WriteLine($"✅ Constraint '{constraint.ConstraintName}' on {tableName}.{constraint.ColumnName} validated");
        }
    }

    if (validationIssues.Any())
    {
        Console.WriteLine($"\n⚠️  Found {validationIssues.Count} potential issues:");
        foreach (var issue in validationIssues)
        {
            Console.WriteLine($"  - {issue}");
        }
    }
    else
    {
        Console.WriteLine("\n✅ All default constraints appear to be valid");
    }
}

public async Task CleanupTemporaryDefaultsAsync(IDbConnection connection, string schema)
{
    var tables = await connection.GetTableNamesAsync(schema);
    var cleanedCount = 0;

    foreach (var tableName in tables)
    {
        var constraintNames = await connection.GetDefaultConstraintNamesAsync(schema, tableName);
        var tempConstraints = constraintNames.Where(name =>
            name.Contains("temp_") ||
            name.Contains("_temp") ||
            name.StartsWith("tmp_") ||
            name.EndsWith("_backup") ||
            name.Contains("_old_")).ToList();

        foreach (var constraintName in tempConstraints)
        {
            bool dropped = await connection.DropDefaultConstraintIfExistsAsync(schema, tableName, constraintName);
            if (dropped)
            {
                Console.WriteLine($"Cleaned up temporary default constraint: {constraintName}");
                cleanedCount++;
            }
        }
    }

    Console.WriteLine($"Cleaned up {cleanedCount} temporary default constraints");
}
```

### Advanced Default Management

```csharp
public async Task MigrateDefaultConstraintsAsync(IDbConnection connection, string schema, string tableName)
{
    using var transaction = connection.BeginTransaction();
    try
    {
        // Get current default constraints
        var currentDefaults = await connection.GetDefaultConstraintsAsync(schema, tableName, tx: transaction);
        var migrationActions = new List<string>();

        foreach (var constraint in currentDefaults)
        {
            // Example: Update old timestamp defaults to use newer functions
            if (constraint.Expression.Contains("GETDATE()"))
            {
                var newExpression = constraint.Expression.Replace("GETDATE()", "SYSDATETIME()");

                // Drop old constraint
                await connection.DropDefaultConstraintIfExistsAsync(
                    schema, tableName, constraint.ConstraintName, tx: transaction);

                // Create new constraint with updated expression
                await connection.CreateDefaultConstraintIfNotExistsAsync(
                    schema, tableName, constraint.ConstraintName, constraint.ColumnName,
                    newExpression, tx: transaction);

                migrationActions.Add($"Updated {constraint.ConstraintName}: GETDATE() → SYSDATETIME()");
            }

            // Example: Standardize string defaults
            if (constraint.Expression == "''")
            {
                var newExpression = "'N/A'";

                await connection.DropDefaultConstraintIfExistsAsync(
                    schema, tableName, constraint.ConstraintName, tx: transaction);

                await connection.CreateDefaultConstraintIfNotExistsAsync(
                    schema, tableName, constraint.ConstraintName, constraint.ColumnName,
                    newExpression, tx: transaction);

                migrationActions.Add($"Updated {constraint.ConstraintName}: empty string → 'N/A'");
            }
        }

        transaction.Commit();

        if (migrationActions.Any())
        {
            Console.WriteLine($"Migrated {migrationActions.Count} default constraints:");
            foreach (var action in migrationActions)
            {
                Console.WriteLine($"  ✅ {action}");
            }
        }
        else
        {
            Console.WriteLine("No default constraints required migration");
        }
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
}

public async Task CreateProviderOptimizedDefaultsAsync(IDbConnection connection, string schema, string tableName, string columnName, Type columnType)
{
    var provider = connection.GetDbProviderType();
    string expression;
    string constraintName = $"DF_{tableName}_{columnName}";

    // Create provider-optimized default expressions
    switch (columnType)
    {
        case Type t when t == typeof(DateTime):
            expression = provider switch
            {
                DbProviderType.SqlServer => "SYSDATETIME()",
                DbProviderType.MySql => "NOW(6)",
                DbProviderType.PostgreSql => "NOW()",
                DbProviderType.Sqlite => "datetime('now')",
                _ => "CURRENT_TIMESTAMP"
            };
            break;

        case Type t when t == typeof(Guid):
            expression = provider switch
            {
                DbProviderType.SqlServer => "NEWID()",
                DbProviderType.MySql => "UUID()",
                DbProviderType.PostgreSql => "gen_random_uuid()",
                _ => "NEWID()" // Fallback, may not work on all providers
            };
            break;

        case Type t when t == typeof(bool):
            expression = provider switch
            {
                DbProviderType.SqlServer => "0",
                DbProviderType.MySql => "0",
                DbProviderType.PostgreSql => "false",
                DbProviderType.Sqlite => "0",
                _ => "0"
            };
            break;

        default:
            expression = "NULL";
            break;
    }

    bool created = await connection.CreateDefaultConstraintIfNotExistsAsync(
        schema, tableName, constraintName, columnName, expression);

    if (created)
    {
        Console.WriteLine($"Created provider-optimized default for {tableName}.{columnName}: {expression}");
    }
}
```

Default constraint methods provide essential functionality for ensuring data consistency by automatically providing appropriate values when columns are not explicitly set during INSERT operations across all supported database providers.
