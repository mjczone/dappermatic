# Check Constraint Methods

Check constraint methods provide comprehensive functionality for managing check constraints across all supported database providers. Check constraints enforce data integrity by ensuring that values in columns meet specific conditions.

## Quick Navigation

- [Check Constraint Existence Checking](#check-constraint-existence-checking)
  - [DoesCheckConstraintExistAsync](#doescheckconstraintexistasync) - Check if named check constraint exists
  - [DoesCheckConstraintExistOnColumnAsync](#doescheckconstraintexistoncolumnasync) - Check if any check constraint exists on column
- [Check Constraint Creation](#check-constraint-creation)
  - [CreateCheckConstraintIfNotExistsAsync (Model)](#createcheckconstraintifnotexistsasync-dmcheckconstraint) - Create from DmCheckConstraint model
  - [CreateCheckConstraintIfNotExistsAsync (Parameters)](#createcheckconstraintifnotexistsasync-parameters) - Create with individual parameters
- [Check Constraint Discovery](#check-constraint-discovery)
  - [GetCheckConstraintNamesAsync](#getcheckconstraintnamesasync) - Get list of check constraint names with filtering
  - [GetCheckConstraintNameOnColumnAsync](#getcheckconstraintnameoncolumnasync) - Get check constraint name on specific column
  - [GetCheckConstraintsAsync](#getcheckconstraintsasync) - Get complete check constraint models
  - [GetCheckConstraintAsync](#getcheckconstraintasync) - Get single check constraint by name
  - [GetCheckConstraintOnColumnAsync](#getcheckconstraintoncolumnasync) - Get check constraint model on specific column
- [Check Constraint Deletion](#check-constraint-deletion)
  - [DropCheckConstraintIfExistsAsync](#dropcheckconstraintifexistsasync) - Drop check constraint by name
  - [DropCheckConstraintOnColumnIfExistsAsync](#dropcheckconstraintoncolumnifexistsasync) - Drop check constraint on specific column

## Check Constraint Existence Checking

### DoesCheckConstraintExistAsync

Check if a specific named check constraint exists.

```csharp
// Check if named check constraint exists
var constraintName = "CK_Orders_PositiveAmount";
bool exists = await connection.DoesCheckConstraintExistAsync("sales", "orders", constraintName);

if (exists)
{
    Console.WriteLine($"Check constraint '{constraintName}' exists");

    // Get the check constraint details
    var checkConstraint = await connection.GetCheckConstraintAsync("sales", "orders", constraintName);
    if (checkConstraint != null)
    {
        Console.WriteLine($"Expression: {checkConstraint.Expression}");
    }
}
else
{
    Console.WriteLine($"Check constraint '{constraintName}' does not exist");
}

// With transaction and cancellation
using var transaction = connection.BeginTransaction();
bool exists = await connection.DoesCheckConstraintExistAsync(
    "sales",
    "orders",
    constraintName,
    tx: transaction,
    cancellationToken: cancellationToken
);
```

**Parameters:**

- `schemaName` - Schema containing the table
- `tableName` - Name of the table containing the check constraint
- `constraintName` - Name of the check constraint to check
- `tx` (optional) - Database transaction
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if check constraint exists, `false` otherwise

### DoesCheckConstraintExistOnColumnAsync

Check if any check constraint exists on a specific column.

```csharp
// Check if any check constraint exists on a column
bool hasCheck = await connection.DoesCheckConstraintExistOnColumnAsync("sales", "orders", "amount");

if (hasCheck)
{
    Console.WriteLine("Column 'amount' has at least one check constraint");

    // Get the check constraint on this column
    var constraint = await connection.GetCheckConstraintOnColumnAsync("sales", "orders", "amount");
    if (constraint != null)
    {
        Console.WriteLine($"Check constraint '{constraint.ConstraintName}' expression: {constraint.Expression}");
    }
}
else
{
    Console.WriteLine("Column 'amount' has no check constraints");
}

// With transaction
using var transaction = connection.BeginTransaction();
bool exists = await connection.DoesCheckConstraintExistOnColumnAsync(
    "hr", "employees", "hire_date", tx: transaction);
```

**Parameters:**

- `schemaName` - Schema containing the table
- `tableName` - Name of the table containing the column
- `columnName` - Name of the column to check for constraints
- `tx` (optional) - Database transaction
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if any check constraint exists on the column, `false` otherwise

## Check Constraint Creation

### CreateCheckConstraintIfNotExistsAsync (DmCheckConstraint)

Create a check constraint only if it doesn't already exist using a DmCheckConstraint model.

```csharp
// Create check constraint if it doesn't exist
bool created = await connection.CreateCheckConstraintIfNotExistsAsync("sales", "orders", checkConstraint);

if (created)
{
    Console.WriteLine($"Check constraint '{checkConstraint.ConstraintName}' was created");
}
else
{
    Console.WriteLine($"Check constraint '{checkConstraint.ConstraintName}' already existed");
}
```

**Parameters:**

- `schemaName` - Schema containing the table
- `tableName` - Name of the table to add check constraint to
- `checkConstraint` - DmCheckConstraint model defining the constraint
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if check constraint was created, `false` if it already existed

### CreateCheckConstraintIfNotExistsAsync (Parameters)

Create a check constraint using individual parameters for convenience.

```csharp
// Simple check constraint for positive values
bool created = await connection.CreateCheckConstraintIfNotExistsAsync(
    schemaName: "sales",
    tableName: "orders",
    constraintName: "CK_Orders_PositiveAmount",
    expression: "amount > 0"
);

// Check constraint with multiple conditions
bool created = await connection.CreateCheckConstraintIfNotExistsAsync(
    "hr",
    "employees",
    "CK_Employees_ValidHireDate",
    "hire_date >= '1900-01-01' AND hire_date <= GETDATE()",
    tx: transaction,
    commandTimeout: 60,
    cancellationToken: cancellationToken
);

// Email validation check constraint
bool created = await connection.CreateCheckConstraintIfNotExistsAsync(
    "app",
    "users",
    "CK_Users_ValidEmail",
    "email LIKE '%@%.%' AND LEN(email) >= 5"
);

// Status enumeration check constraint
bool created = await connection.CreateCheckConstraintIfNotExistsAsync(
    "orders",
    "order_status",
    "CK_Orders_ValidStatus",
    "status IN ('pending', 'processing', 'shipped', 'delivered', 'cancelled')"
);
```

**Parameters:**

- `schemaName` - Schema containing the table
- `tableName` - Name of the table to add constraint to
- `constraintName` - Name of the check constraint
- `expression` - SQL expression that must evaluate to true
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if check constraint was created, `false` if it already existed

## Check Constraint Discovery

### GetCheckConstraintNamesAsync

Retrieve a list of check constraint names from a table, with optional filtering.

```csharp
// Get all check constraint names on a table
List<string> allConstraints = await connection.GetCheckConstraintNamesAsync("sales", "orders");
foreach (string constraintName in allConstraints)
{
    Console.WriteLine($"Found check constraint: {constraintName}");
}

// Get check constraint names with wildcard filter
List<string> positiveConstraints = await connection.GetCheckConstraintNamesAsync("sales", "orders", "CK_*_Positive*");
// Finds: CK_Orders_PositiveAmount, CK_Items_PositiveQuantity, etc.

// Get constraints with pattern matching
List<string> validationConstraints = await connection.GetCheckConstraintNamesAsync("app", "users", "*_Valid*");
// Finds: CK_Users_ValidEmail, CK_Users_ValidAge, etc.
```

**Parameters:**

- `schemaName` - Schema containing the table
- `tableName` - Name of the table to search
- `nameFilter` (optional) - Wildcard pattern to filter constraint names (`*` = any characters, `?` = single character)
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `List<string>` - List of matching check constraint names

### GetCheckConstraintNameOnColumnAsync

Get the check constraint name that includes a specific column.

```csharp
// Get check constraint name on a specific column
string? constraintName = await connection.GetCheckConstraintNameOnColumnAsync("sales", "orders", "amount");

if (constraintName != null)
{
    Console.WriteLine($"Check constraint on 'amount': {constraintName}");

    // Get full details of the check constraint
    var constraint = await connection.GetCheckConstraintAsync("sales", "orders", constraintName);
    if (constraint != null)
    {
        Console.WriteLine($"Expression: {constraint.Expression}");
    }
}
else
{
    Console.WriteLine("No check constraint found on 'amount' column");
}
```

**Parameters:**

- `schemaName` - Schema containing the table
- `tableName` - Name of the table containing the column
- `columnName` - Name of the column to find check constraint for
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `string?` - Name of the check constraint, or `null` if none exists on the column

### GetCheckConstraintsAsync

Retrieve complete DmCheckConstraint models for table check constraints.

```csharp
// Get all check constraints with full structure information
List<DmCheckConstraint> checkConstraints = await connection.GetCheckConstraintsAsync("sales", "orders");

foreach (var constraint in checkConstraints)
{
    Console.WriteLine($"Check Constraint: {constraint.ConstraintName}");
    Console.WriteLine($"  Expression: {constraint.Expression}");
    Console.WriteLine($"  Schema: {constraint.SchemaName}");
    Console.WriteLine($"  Table: {constraint.TableName}");
}

// Get specific check constraints with pattern
List<DmCheckConstraint> validationConstraints = await connection.GetCheckConstraintsAsync("app", "users", "CK_*_Valid*");

// With transaction
using var transaction = connection.BeginTransaction();
List<DmCheckConstraint> constraints = await connection.GetCheckConstraintsAsync("hr", "employees", tx: transaction);
```

**Parameters:**

- `schemaName` - Schema containing the table
- `tableName` - Name of the table to search
- `nameFilter` (optional) - Wildcard pattern to filter constraint names
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `List<DmCheckConstraint>` - List of complete DmCheckConstraint models

### GetCheckConstraintAsync

Retrieve a single DmCheckConstraint model for a specific check constraint.

```csharp
// Get specific check constraint structure
DmCheckConstraint? constraint = await connection.GetCheckConstraintAsync("sales", "orders", "CK_Orders_PositiveAmount");

if (constraint != null)
{
    Console.WriteLine($"Check Constraint '{constraint.ConstraintName}' details:");
    Console.WriteLine($"  Table: {constraint.SchemaName}.{constraint.TableName}");
    Console.WriteLine($"  Expression: {constraint.Expression}");

    // Analyze the constraint expression
    if (constraint.Expression.Contains(">"))
    {
        Console.WriteLine("  This is a range/comparison constraint");
    }
    else if (constraint.Expression.Contains("IN ("))
    {
        Console.WriteLine("  This is an enumeration constraint");
    }
    else if (constraint.Expression.Contains("LIKE"))
    {
        Console.WriteLine("  This is a pattern matching constraint");
    }
}
else
{
    Console.WriteLine("Check constraint not found");
}
```

**Parameters:**

- `schemaName` - Schema containing the table
- `tableName` - Name of the table containing the constraint
- `constraintName` - Name of the check constraint to retrieve
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `DmCheckConstraint?` - Complete check constraint model, or `null` if not found

### GetCheckConstraintOnColumnAsync

Retrieve the check constraint that includes a specific column.

```csharp
// Get check constraint on a specific column
DmCheckConstraint? constraint = await connection.GetCheckConstraintOnColumnAsync("sales", "orders", "amount");

if (constraint != null)
{
    Console.WriteLine($"Check constraint on 'amount': {constraint.ConstraintName}");
    Console.WriteLine($"Expression: {constraint.Expression}");

    // Check constraint type
    if (constraint.Expression.Contains("amount > 0"))
    {
        Console.WriteLine("✅ Ensures positive amounts");
    }
    else if (constraint.Expression.Contains("amount"))
    {
        Console.WriteLine("💡 Amount-related validation constraint");
    }
}
else
{
    Console.WriteLine("No check constraint found on 'amount' column");
}
```

**Parameters:**

- `schemaName` - Schema containing the table
- `tableName` - Name of the table containing the column
- `columnName` - Name of the column to find check constraint for
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `DmCheckConstraint?` - Check constraint model, or `null` if none exists on the column

## Check Constraint Deletion

### DropCheckConstraintIfExistsAsync

Remove a check constraint from a table if it exists.

```csharp
// Drop check constraint if it exists
bool dropped = await connection.DropCheckConstraintIfExistsAsync("sales", "orders", "CK_Orders_PositiveAmount");

if (dropped)
{
    Console.WriteLine("Check constraint was dropped");
}
else
{
    Console.WriteLine("Check constraint did not exist");
}

// Batch drop multiple check constraints
var constraintsToRemove = new[] { "CK_Orders_TempValidation", "CK_Orders_OldRule", "CK_Orders_BackupCheck" };
foreach (var constraintName in constraintsToRemove)
{
    bool wasDropped = await connection.DropCheckConstraintIfExistsAsync("sales", "orders", constraintName);
    Console.WriteLine($"Check constraint '{constraintName}': {(wasDropped ? "Dropped" : "Not found")}");
}

// With transaction
using var transaction = connection.BeginTransaction();
try
{
    bool dropped = await connection.DropCheckConstraintIfExistsAsync("sales", "orders", "CK_Orders_PositiveAmount", tx: transaction);
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
- `constraintName` - Name of the check constraint to drop
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if check constraint was dropped, `false` if it didn't exist

### DropCheckConstraintOnColumnIfExistsAsync

Remove the check constraint that includes a specific column.

```csharp
// Drop check constraint on a specific column
bool dropped = await connection.DropCheckConstraintOnColumnIfExistsAsync("sales", "orders", "legacy_status");

if (dropped)
{
    Console.WriteLine("Check constraint on 'legacy_status' was dropped");
    // Now safe to modify or drop the column
    await connection.DropColumnIfExistsAsync("sales", "orders", "legacy_status");
}
else
{
    Console.WriteLine("No check constraint found on 'legacy_status'");
}

// Useful before column modifications
await connection.DropCheckConstraintOnColumnIfExistsAsync("hr", "employees", "salary");
// Now safe to modify the salary column constraints
await connection.CreateCheckConstraintIfNotExistsAsync(
    "hr", "employees", "CK_Employees_ReasonableSalary", "salary BETWEEN 20000 AND 500000");
```

**Parameters:**

- `schemaName` - Schema containing the table
- `tableName` - Name of the table containing the constraint
- `columnName` - Name of the column to drop check constraint for
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if check constraint was dropped, `false` if none existed on the column

## Practical Examples

### Data Validation Setup

```csharp
public async Task SetupDataValidationAsync(IDbConnection connection)
{
    using var transaction = connection.BeginTransaction();
    try
    {
        // User table validations
        await connection.CreateCheckConstraintIfNotExistsAsync(
            "app", "users", "CK_Users_ValidEmail",
            "email LIKE '%@%.%' AND LEN(email) >= 5", tx: transaction);

        await connection.CreateCheckConstraintIfNotExistsAsync(
            "app", "users", "CK_Users_ValidAge",
            "age >= 13 AND age <= 120", tx: transaction);

        // Order table validations
        await connection.CreateCheckConstraintIfNotExistsAsync(
            "sales", "orders", "CK_Orders_PositiveAmount",
            "total_amount > 0", tx: transaction);

        await connection.CreateCheckConstraintIfNotExistsAsync(
            "sales", "orders", "CK_Orders_ValidStatus",
            "status IN ('pending', 'processing', 'shipped', 'delivered', 'cancelled')", tx: transaction);

        // Employee table validations
        await connection.CreateCheckConstraintIfNotExistsAsync(
            "hr", "employees", "CK_Employees_ValidHireDate",
            "hire_date >= '1900-01-01' AND hire_date <= GETDATE()", tx: transaction);

        await connection.CreateCheckConstraintIfNotExistsAsync(
            "hr", "employees", "CK_Employees_ReasonableSalary",
            "salary BETWEEN 20000 AND 1000000", tx: transaction);

        transaction.Commit();
        Console.WriteLine("Data validation constraints created successfully");
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
}
```

### Check Constraint Analysis

```csharp
public async Task AnalyzeCheckConstraintsAsync(IDbConnection connection, string schema)
{
    var tables = await connection.GetTableNamesAsync(schema);

    Console.WriteLine($"Check Constraint Analysis for schema '{schema}':");

    var totalConstraints = 0;
    var tablesWithConstraints = new List<string>();
    var constraintTypes = new Dictionary<string, int>();

    foreach (var tableName in tables)
    {
        var constraints = await connection.GetCheckConstraintsAsync(schema, tableName);

        if (constraints.Any())
        {
            tablesWithConstraints.Add(tableName);
            totalConstraints += constraints.Count;

            foreach (var constraint in constraints)
            {
                Console.WriteLine($"  {tableName}.{constraint.ConstraintName}:");
                Console.WriteLine($"    Expression: {constraint.Expression}");

                // Categorize constraint types
                var type = CategorizeConstraint(constraint.Expression);
                constraintTypes[type] = constraintTypes.GetValueOrDefault(type, 0) + 1;
            }
        }
    }

    Console.WriteLine($"\nSummary:");
    Console.WriteLine($"Total tables: {tables.Count}");
    Console.WriteLine($"Tables with check constraints: {tablesWithConstraints.Count}");
    Console.WriteLine($"Total check constraints: {totalConstraints}");

    Console.WriteLine("\nConstraint Types:");
    foreach (var (type, count) in constraintTypes.OrderByDescending(kvp => kvp.Value))
    {
        Console.WriteLine($"  {type}: {count}");
    }
}

private string CategorizeConstraint(string expression)
{
    var expr = expression.ToLower();
    if (expr.Contains(" in (")) return "Enumeration";
    if (expr.Contains("between")) return "Range";
    if (expr.Contains(" > ") || expr.Contains(" < ") || expr.Contains(">=") || expr.Contains("<=")) return "Comparison";
    if (expr.Contains("like")) return "Pattern";
    if (expr.Contains("len(") || expr.Contains("length(")) return "Length";
    if (expr.Contains("@")) return "Email";
    return "Other";
}
```

### Constraint Validation and Cleanup

```csharp
public async Task ValidateCheckConstraintsAsync(IDbConnection connection, string schema)
{
    Console.WriteLine($"Validating check constraints in schema '{schema}'...");

    var tables = await connection.GetTableNamesAsync(schema);
    var validationErrors = new List<string>();

    foreach (var tableName in tables)
    {
        var constraints = await connection.GetCheckConstraintsAsync(schema, tableName);

        foreach (var constraint in constraints)
        {
            try
            {
                // Check if constraint expression is syntactically valid
                // This would require database-specific validation
                Console.WriteLine($"✅ Constraint '{constraint.ConstraintName}' on {tableName} appears valid");

                // Check for common issues
                if (string.IsNullOrWhiteSpace(constraint.Expression))
                {
                    validationErrors.Add($"Constraint '{constraint.ConstraintName}' has empty expression");
                }

                if (constraint.Expression.Length > 4000)
                {
                    validationErrors.Add($"Constraint '{constraint.ConstraintName}' has very long expression (>4000 chars)");
                }

                if (constraint.Expression.Contains("--") || constraint.Expression.Contains("/*"))
                {
                    validationErrors.Add($"Constraint '{constraint.ConstraintName}' contains SQL comments - potential security risk");
                }
            }
            catch (Exception ex)
            {
                validationErrors.Add($"Error validating constraint '{constraint.ConstraintName}' on {tableName}: {ex.Message}");
            }
        }
    }

    if (validationErrors.Any())
    {
        Console.WriteLine($"\n❌ Found {validationErrors.Count} validation errors:");
        foreach (var error in validationErrors)
        {
            Console.WriteLine($"  - {error}");
        }
    }
    else
    {
        Console.WriteLine("\n✅ All check constraints appear to be valid");
    }
}

public async Task CleanupTemporaryConstraintsAsync(IDbConnection connection, string schema)
{
    var tables = await connection.GetTableNamesAsync(schema);
    var cleanedCount = 0;

    foreach (var tableName in tables)
    {
        var constraintNames = await connection.GetCheckConstraintNamesAsync(schema, tableName);
        var tempConstraints = constraintNames.Where(name =>
            name.Contains("temp_") ||
            name.Contains("_temp") ||
            name.StartsWith("tmp_") ||
            name.EndsWith("_backup")).ToList();

        foreach (var constraintName in tempConstraints)
        {
            bool dropped = await connection.DropCheckConstraintIfExistsAsync(schema, tableName, constraintName);
            if (dropped)
            {
                Console.WriteLine($"Cleaned up temporary constraint: {constraintName}");
                cleanedCount++;
            }
        }
    }

    Console.WriteLine($"Cleaned up {cleanedCount} temporary check constraints");
}
```

### Advanced Constraint Management

```csharp
public async Task StandardizeConstraintNamesAsync(IDbConnection connection, string schema)
{
    var tables = await connection.GetTableNamesAsync(schema);

    foreach (var tableName in tables)
    {
        var constraints = await connection.GetCheckConstraintsAsync(schema, tableName);

        foreach (var constraint in constraints)
        {
            // Check if constraint name follows standard convention: CK_TableName_Description
            var expectedPrefix = $"CK_{tableName}_";
            if (!constraint.ConstraintName.StartsWith(expectedPrefix))
            {
                Console.WriteLine($"Non-standard constraint name: {constraint.ConstraintName}");

                // Generate standard name based on expression content
                var standardName = GenerateStandardConstraintName(tableName, constraint.Expression);

                Console.WriteLine($"Suggested name: {standardName}");

                // Note: Renaming constraints would require dropping and recreating
                // This is left as an exercise since it's database-specific
            }
        }
    }
}

private string GenerateStandardConstraintName(string tableName, string expression)
{
    var expr = expression.ToLower();

    if (expr.Contains("email") && expr.Contains("like")) return $"CK_{tableName}_ValidEmail";
    if (expr.Contains("age") && expr.Contains("between")) return $"CK_{tableName}_ValidAge";
    if (expr.Contains("amount") && expr.Contains(" > 0")) return $"CK_{tableName}_PositiveAmount";
    if (expr.Contains("date") && expr.Contains("getdate")) return $"CK_{tableName}_ValidDate";
    if (expr.Contains(" in (")) return $"CK_{tableName}_ValidStatus";

    return $"CK_{tableName}_Custom";
}
```

Check constraint methods provide essential functionality for maintaining data integrity and enforcing business rules at the database level across all supported database providers.
