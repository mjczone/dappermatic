# Unique Constraint Methods

Unique constraint methods provide comprehensive functionality for managing uniqueness constraints across all supported database providers. Unique constraints ensure that no duplicate values exist in specified columns, enforcing data integrity at the database level.

## Quick Navigation

- [Unique Constraint Existence Checking](#unique-constraint-existence-checking)
  - [DoesUniqueConstraintExistAsync](#doesuniqueconstraintexistasync) - Check if named unique constraint exists
  - [DoesUniqueConstraintExistOnColumnAsync](#doesuniqueconstraintexistoncolumnasync) - Check if any unique constraint exists on column
- [Unique Constraint Creation](#unique-constraint-creation)
  - [CreateUniqueConstraintIfNotExistsAsync (Model)](#createuniqueconstraintifnotexistsasync-dmuniqueconstraint) - Create from DmUniqueConstraint model
  - [CreateUniqueConstraintIfNotExistsAsync (Parameters)](#createuniqueconstraintifnotexistsasync-parameters) - Create with individual parameters
- [Unique Constraint Discovery](#unique-constraint-discovery)
  - [GetUniqueConstraintNamesAsync](#getuniqueconstraintnamesasync) - Get list of unique constraint names with filtering
  - [GetUniqueConstraintNameOnColumnAsync](#getuniqueconstraintnameoncolumnasync) - Get unique constraint name on specific column
  - [GetUniqueConstraintsAsync](#getuniqueconstraintsasync) - Get complete unique constraint models
  - [GetUniqueConstraintAsync](#getuniqueconstraintasync) - Get single unique constraint by name
  - [GetUniqueConstraintOnColumnAsync](#getuniqueconstraintoncolumnasync) - Get unique constraint model on specific column
- [Unique Constraint Deletion](#unique-constraint-deletion)
  - [DropUniqueConstraintIfExistsAsync](#dropuniqueconstraintifexistsasync) - Drop unique constraint by name
  - [DropUniqueConstraintOnColumnIfExistsAsync](#dropuniqueconstraintoncolumnifexistsasync) - Drop unique constraint on specific column

## Unique Constraint Existence Checking

### DoesUniqueConstraintExistAsync

Check if a specific named unique constraint exists.

```csharp
// Check if named unique constraint exists
var constraintName = "UQ_Users_Email";
bool exists = await connection.DoesUniqueConstraintExistAsync("app", "users", constraintName);

if (exists)
{
    Console.WriteLine($"Unique constraint '{constraintName}' exists");

    // Get the unique constraint details
    var uniqueConstraint = await connection.GetUniqueConstraintAsync("app", "users", constraintName);
    if (uniqueConstraint != null)
    {
        Console.WriteLine($"Columns: {string.Join(", ", uniqueConstraint.KeyColumnNames)}");
    }
}
else
{
    Console.WriteLine($"Unique constraint '{constraintName}' does not exist");
}

// With transaction and cancellation
using var transaction = connection.BeginTransaction();
bool exists = await connection.DoesUniqueConstraintExistAsync(
    "app",
    "users",
    constraintName,
    tx: transaction,
    cancellationToken: cancellationToken
);
```

**Parameters:**

- `schemaName` - Schema containing the table
- `tableName` - Name of the table containing the unique constraint
- `constraintName` - Name of the unique constraint to check
- `tx` (optional) - Database transaction
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if unique constraint exists, `false` otherwise

### DoesUniqueConstraintExistOnColumnAsync

Check if any unique constraint exists on a specific column.

```csharp
// Check if any unique constraint exists on a column
bool hasUnique = await connection.DoesUniqueConstraintExistOnColumnAsync("app", "users", "email");

if (hasUnique)
{
    Console.WriteLine("Column 'email' has a unique constraint");

    // Get the unique constraint on this column
    var constraint = await connection.GetUniqueConstraintOnColumnAsync("app", "users", "email");
    if (constraint != null)
    {
        Console.WriteLine($"Unique constraint '{constraint.KeyName}' includes columns: {string.Join(", ", constraint.KeyColumnNames)}");
    }
}
else
{
    Console.WriteLine("Column 'email' has no unique constraint");
}

// With transaction
using var transaction = connection.BeginTransaction();
bool exists = await connection.DoesUniqueConstraintExistOnColumnAsync(
    "inventory", "products", "sku", tx: transaction);
```

**Parameters:**

- `schemaName` - Schema containing the table
- `tableName` - Name of the table containing the column
- `columnName` - Name of the column to check for unique constraints
- `tx` (optional) - Database transaction
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if any unique constraint exists on the column, `false` otherwise

## Unique Constraint Creation

### CreateUniqueConstraintIfNotExistsAsync (DmUniqueConstraint)

Create a unique constraint only if it doesn't already exist using a DmUniqueConstraint model.

```csharp
// Create unique constraint if it doesn't exist
bool created = await connection.CreateUniqueConstraintIfNotExistsAsync(uniqueConstraint);

if (created)
{
    Console.WriteLine($"Unique constraint '{uniqueConstraint.KeyName}' was created");
}
else
{
    Console.WriteLine($"Unique constraint '{uniqueConstraint.KeyName}' already existed");
}
```

**Parameters:**

- `constraint` - DmUniqueConstraint model defining the constraint (includes SchemaName and TableName)
- `tx` (optional) - Database transaction
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if unique constraint was created, `false` if it already existed

### CreateUniqueConstraintIfNotExistsAsync (Parameters)

Create a unique constraint using individual parameters for convenience.

```csharp
// Single column unique constraint
bool created = await connection.CreateUniqueConstraintIfNotExistsAsync(
    schemaName: "app",
    tableName: "users",
    constraintName: "UQ_Users_Email",
    columns: new[] { new DmOrderedColumn("Email") }
);

// Multi-column composite unique constraint
bool created = await connection.CreateUniqueConstraintIfNotExistsAsync(
    "inventory",
    "products",
    "UQ_Products_Code_Supplier",
    new[]
    {
        new DmOrderedColumn("ProductCode"),
        new DmOrderedColumn("SupplierId")
    },
    tx: transaction,
    cancellationToken: cancellationToken
);

// Business key uniqueness
bool created = await connection.CreateUniqueConstraintIfNotExistsAsync(
    "sales",
    "orders",
    "UQ_Orders_OrderNumber",
    new[] { new DmOrderedColumn("OrderNumber") }
);

// Natural key constraint
bool created = await connection.CreateUniqueConstraintIfNotExistsAsync(
    "hr",
    "employees",
    "UQ_Employees_SSN",
    new[] { new DmOrderedColumn("SSN") }
);
```

**Parameters:**

- `schemaName` - Schema containing the table
- `tableName` - Name of the table to add constraint to
- `constraintName` - Name of the unique constraint
- `columns` - Array of DmOrderedColumn defining the unique columns
- `tx` (optional) - Database transaction
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if unique constraint was created, `false` if it already existed

## Unique Constraint Discovery

### GetUniqueConstraintNamesAsync

Retrieve a list of unique constraint names from a table, with optional filtering.

```csharp
// Get all unique constraint names on a table
List<string> allConstraints = await connection.GetUniqueConstraintNamesAsync("app", "users");
foreach (string constraintName in allConstraints)
{
    Console.WriteLine($"Found unique constraint: {constraintName}");
}

// Get unique constraint names with wildcard filter
List<string> emailConstraints = await connection.GetUniqueConstraintNamesAsync("app", "users", "UQ_*_Email*");
// Finds: UQ_Users_Email, UQ_Contacts_EmailAddress, etc.

// Get constraints with pattern matching
List<string> businessKeys = await connection.GetUniqueConstraintNamesAsync("sales", "orders", "*_OrderNumber*");
// Finds: UQ_Orders_OrderNumber, UQ_Returns_OrderNumber, etc.
```

**Parameters:**

- `schemaName` - Schema containing the table
- `tableName` - Name of the table to search
- `nameFilter` (optional) - Wildcard pattern to filter constraint names (`*` = any characters, `?` = single character)
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `List<string>` - List of matching unique constraint names

### GetUniqueConstraintNameOnColumnAsync

Get the unique constraint name that includes a specific column.

```csharp
// Get unique constraint name on a specific column
string? constraintName = await connection.GetUniqueConstraintNameOnColumnAsync("app", "users", "email");

if (constraintName != null)
{
    Console.WriteLine($"Unique constraint on 'email': {constraintName}");

    // Get full details of the unique constraint
    var constraint = await connection.GetUniqueConstraintAsync("app", "users", constraintName);
    if (constraint != null)
    {
        Console.WriteLine($"All columns in constraint: {string.Join(", ", constraint.KeyColumnNames)}");
    }
}
else
{
    Console.WriteLine("No unique constraint found on 'email' column");
}
```

**Parameters:**

- `schemaName` - Schema containing the table
- `tableName` - Name of the table containing the column
- `columnName` - Name of the column to find unique constraint for
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `string?` - Name of the unique constraint, or `null` if none exists on the column

### GetUniqueConstraintsAsync

Retrieve complete DmUniqueConstraint models for table unique constraints.

```csharp
// Get all unique constraints with full structure information
List<DmUniqueConstraint> uniqueConstraints = await connection.GetUniqueConstraintsAsync("app", "users");

foreach (var constraint in uniqueConstraints)
{
    Console.WriteLine($"Unique Constraint: {constraint.KeyName}");
    Console.WriteLine($"  Columns: {string.Join(", ", constraint.KeyColumnNames)}");
    Console.WriteLine($"  Schema: {constraint.SchemaName}");
    Console.WriteLine($"  Table: {constraint.TableName}");
    Console.WriteLine($"  Column Count: {constraint.KeyColumnNames.Count}");

    if (constraint.KeyColumnNames.Count > 1)
    {
        Console.WriteLine($"  This is a composite unique constraint");
    }
}

// Get specific unique constraints with pattern
List<DmUniqueConstraint> emailConstraints = await connection.GetUniqueConstraintsAsync("app", "users", "UQ_*_Email*");

// With transaction
using var transaction = connection.BeginTransaction();
List<DmUniqueConstraint> constraints = await connection.GetUniqueConstraintsAsync("inventory", "products", tx: transaction);
```

**Parameters:**

- `schemaName` - Schema containing the table
- `tableName` - Name of the table to search
- `nameFilter` (optional) - Wildcard pattern to filter constraint names
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `List<DmUniqueConstraint>` - List of complete DmUniqueConstraint models

### GetUniqueConstraintAsync

Retrieve a single DmUniqueConstraint model for a specific unique constraint.

```csharp
// Get specific unique constraint structure
DmUniqueConstraint? constraint = await connection.GetUniqueConstraintAsync("app", "users", "UQ_Users_Email");

if (constraint != null)
{
    Console.WriteLine($"Unique Constraint '{constraint.KeyName}' details:");
    Console.WriteLine($"  Table: {constraint.SchemaName}.{constraint.TableName}");
    Console.WriteLine($"  Columns: {string.Join(", ", constraint.KeyColumnNames)}");

    // Analyze the constraint
    if (constraint.KeyColumnNames.Count == 1)
    {
        Console.WriteLine("  This is a single-column unique constraint");
        var columnName = constraint.KeyColumnNames[0];
        if (columnName.ToLower().Contains("email"))
        {
            Console.WriteLine("  Ensures email uniqueness");
        }
        else if (columnName.ToLower().Contains("code") || columnName.ToLower().Contains("number"))
        {
            Console.WriteLine("  Ensures business identifier uniqueness");
        }
    }
    else
    {
        Console.WriteLine($"  This is a composite unique constraint with {constraint.KeyColumnNames.Count} columns");
    }
}
else
{
    Console.WriteLine("Unique constraint not found");
}
```

**Parameters:**

- `schemaName` - Schema containing the table
- `tableName` - Name of the table containing the constraint
- `constraintName` - Name of the unique constraint to retrieve
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `DmUniqueConstraint?` - Complete unique constraint model, or `null` if not found

### GetUniqueConstraintOnColumnAsync

Retrieve the unique constraint that includes a specific column.

```csharp
// Get unique constraint on a specific column
DmUniqueConstraint? constraint = await connection.GetUniqueConstraintOnColumnAsync("app", "users", "email");

if (constraint != null)
{
    Console.WriteLine($"Unique constraint on 'email': {constraint.KeyName}");
    Console.WriteLine($"All columns: {string.Join(", ", constraint.KeyColumnNames)}");

    // Check if it's part of a composite constraint
    if (constraint.KeyColumnNames.Count > 1)
    {
        var otherColumns = constraint.KeyColumnNames.Where(c => c.ToLower() != "email").ToList();
        Console.WriteLine($"✅ Email uniqueness combined with: {string.Join(", ", otherColumns)}");
    }
    else
    {
        Console.WriteLine("✅ Standalone email uniqueness constraint");
    }
}
else
{
    Console.WriteLine("No unique constraint found on 'email' column");
}
```

**Parameters:**

- `schemaName` - Schema containing the table
- `tableName` - Name of the table containing the column
- `columnName` - Name of the column to find unique constraint for
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `DmUniqueConstraint?` - Unique constraint model, or `null` if none exists on the column

## Unique Constraint Deletion

### DropUniqueConstraintIfExistsAsync

Remove a unique constraint from a table if it exists.

```csharp
// Drop unique constraint if it exists
bool dropped = await connection.DropUniqueConstraintIfExistsAsync("app", "users", "UQ_Users_Email");

if (dropped)
{
    Console.WriteLine("Unique constraint was dropped");
}
else
{
    Console.WriteLine("Unique constraint did not exist");
}

// Batch drop multiple unique constraints
var constraintsToRemove = new[] { "UQ_Users_TempEmail", "UQ_Users_OldUsername", "UQ_Users_BackupCode" };
foreach (var constraintName in constraintsToRemove)
{
    bool wasDropped = await connection.DropUniqueConstraintIfExistsAsync("app", "users", constraintName);
    Console.WriteLine($"Unique constraint '{constraintName}': {(wasDropped ? "Dropped" : "Not found")}");
}

// With transaction
using var transaction = connection.BeginTransaction();
try
{
    bool dropped = await connection.DropUniqueConstraintIfExistsAsync("app", "users", "UQ_Users_Email", tx: transaction);
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
- `constraintName` - Name of the unique constraint to drop
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if unique constraint was dropped, `false` if it didn't exist

### DropUniqueConstraintOnColumnIfExistsAsync

Remove the unique constraint that includes a specific column.

```csharp
// Drop unique constraint on a specific column
bool dropped = await connection.DropUniqueConstraintOnColumnIfExistsAsync("app", "users", "legacy_username");

if (dropped)
{
    Console.WriteLine("Unique constraint on 'legacy_username' was dropped");
    // Now safe to modify or drop the column
    await connection.DropColumnIfExistsAsync("app", "users", "legacy_username");
}
else
{
    Console.WriteLine("No unique constraint found on 'legacy_username'");
}

// Useful before column modifications
await connection.DropUniqueConstraintOnColumnIfExistsAsync("inventory", "products", "old_sku");
// Now safe to modify the column
await connection.CreateUniqueConstraintIfNotExistsAsync(
    "inventory", "products", "UQ_Products_NewSKU",
    new[] { new DmOrderedColumn("new_sku") });
```

**Parameters:**

- `schemaName` - Schema containing the table
- `tableName` - Name of the table containing the constraint
- `columnName` - Name of the column to drop unique constraint for
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if unique constraint was dropped, `false` if none existed on the column

## Practical Examples

### Business Key Uniqueness Setup

```csharp
public async Task SetupBusinessKeyConstraintsAsync(IDbConnection connection)
{
    using var transaction = connection.BeginTransaction();
    try
    {
        // User uniqueness constraints
        await connection.CreateUniqueConstraintIfNotExistsAsync(
            "app", "users", "UQ_Users_Email",
            new[] { new DmOrderedColumn("Email") }, tx: transaction);

        await connection.CreateUniqueConstraintIfNotExistsAsync(
            "app", "users", "UQ_Users_Username",
            new[] { new DmOrderedColumn("Username") }, tx: transaction);

        // Product uniqueness constraints
        await connection.CreateUniqueConstraintIfNotExistsAsync(
            "inventory", "products", "UQ_Products_SKU",
            new[] { new DmOrderedColumn("SKU") }, tx: transaction);

        await connection.CreateUniqueConstraintIfNotExistsAsync(
            "inventory", "products", "UQ_Products_Barcode",
            new[] { new DmOrderedColumn("Barcode") }, tx: transaction);

        // Order business key
        await connection.CreateUniqueConstraintIfNotExistsAsync(
            "sales", "orders", "UQ_Orders_OrderNumber",
            new[] { new DmOrderedColumn("OrderNumber") }, tx: transaction);

        // Customer business identifiers
        await connection.CreateUniqueConstraintIfNotExistsAsync(
            "sales", "customers", "UQ_Customers_CustomerNumber",
            new[] { new DmOrderedColumn("CustomerNumber") }, tx: transaction);

        // Employee identification
        await connection.CreateUniqueConstraintIfNotExistsAsync(
            "hr", "employees", "UQ_Employees_EmployeeNumber",
            new[] { new DmOrderedColumn("EmployeeNumber") }, tx: transaction);

        await connection.CreateUniqueConstraintIfNotExistsAsync(
            "hr", "employees", "UQ_Employees_SSN",
            new[] { new DmOrderedColumn("SSN") }, tx: transaction);

        transaction.Commit();
        Console.WriteLine("Business key unique constraints created successfully");
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
}
```

### Composite Unique Constraints

```csharp
public async Task SetupCompositeUniqueConstraintsAsync(IDbConnection connection)
{
    using var transaction = connection.BeginTransaction();
    try
    {
        // Product-Supplier combination must be unique
        await connection.CreateUniqueConstraintIfNotExistsAsync(
            "inventory", "product_suppliers", "UQ_ProductSuppliers_Product_Supplier",
            new[]
            {
                new DmOrderedColumn("ProductId"),
                new DmOrderedColumn("SupplierId")
            }, tx: transaction);

        // User-Role combination per tenant
        await connection.CreateUniqueConstraintIfNotExistsAsync(
            "security", "user_roles", "UQ_UserRoles_User_Role_Tenant",
            new[]
            {
                new DmOrderedColumn("UserId"),
                new DmOrderedColumn("RoleId"),
                new DmOrderedColumn("TenantId")
            }, tx: transaction);

        // Order items: one line item per product per order
        await connection.CreateUniqueConstraintIfNotExistsAsync(
            "sales", "order_items", "UQ_OrderItems_Order_Product",
            new[]
            {
                new DmOrderedColumn("OrderId"),
                new DmOrderedColumn("ProductId")
            }, tx: transaction);

        // Category-Parent hierarchy uniqueness (prevent duplicate subcategories)
        await connection.CreateUniqueConstraintIfNotExistsAsync(
            "catalog", "categories", "UQ_Categories_Name_Parent",
            new[]
            {
                new DmOrderedColumn("CategoryName"),
                new DmOrderedColumn("ParentCategoryId")
            }, tx: transaction);

        // Time-based uniqueness (one record per user per day)
        await connection.CreateUniqueConstraintIfNotExistsAsync(
            "timekeeping", "daily_attendance", "UQ_Attendance_Employee_Date",
            new[]
            {
                new DmOrderedColumn("EmployeeId"),
                new DmOrderedColumn("AttendanceDate")
            }, tx: transaction);

        transaction.Commit();
        Console.WriteLine("Composite unique constraints created successfully");
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
}
```

### Unique Constraint Analysis

```csharp
public async Task AnalyzeUniqueConstraintsAsync(IDbConnection connection, string schema)
{
    var tables = await connection.GetTableNamesAsync(schema);

    Console.WriteLine($"Unique Constraint Analysis for schema '{schema}':");

    var totalConstraints = 0;
    var tablesWithUnique = new List<string>();
    var singleColumnConstraints = 0;
    var compositeConstraints = 0;
    var constraintsByColumn = new Dictionary<string, List<string>>();

    foreach (var tableName in tables)
    {
        var constraints = await connection.GetUniqueConstraintsAsync(schema, tableName);

        if (constraints.Any())
        {
            tablesWithUnique.Add(tableName);
            totalConstraints += constraints.Count;

            foreach (var constraint in constraints)
            {
                Console.WriteLine($"  {tableName}.{constraint.KeyName}:");
                Console.WriteLine($"    Columns: {string.Join(", ", constraint.KeyColumnNames)}");

                if (constraint.KeyColumnNames.Count == 1)
                {
                    singleColumnConstraints++;
                    var column = constraint.KeyColumnNames[0];
                    if (!constraintsByColumn.ContainsKey(column))
                        constraintsByColumn[column] = new List<string>();
                    constraintsByColumn[column].Add($"{tableName}.{constraint.KeyName}");
                }
                else
                {
                    compositeConstraints++;
                }

                // Analyze constraint purpose
                var purpose = AnalyzeConstraintPurpose(constraint.KeyName, constraint.KeyColumnNames);
                Console.WriteLine($"    Purpose: {purpose}");
            }
        }
    }

    Console.WriteLine($"\nSummary:");
    Console.WriteLine($"Total tables: {tables.Count}");
    Console.WriteLine($"Tables with unique constraints: {tablesWithUnique.Count}");
    Console.WriteLine($"Total unique constraints: {totalConstraints}");
    Console.WriteLine($"Single-column constraints: {singleColumnConstraints}");
    Console.WriteLine($"Composite constraints: {compositeConstraints}");

    // Show most common unique column names
    var topColumns = constraintsByColumn
        .Where(kvp => kvp.Value.Count > 1)
        .OrderByDescending(kvp => kvp.Value.Count)
        .Take(5);

    if (topColumns.Any())
    {
        Console.WriteLine("\nMost frequently constrained columns:");
        foreach (var (column, constraints) in topColumns)
        {
            Console.WriteLine($"  {column}: {constraints.Count} tables ({string.Join(", ", constraints.Take(3))}{(constraints.Count > 3 ? "..." : "")})");
        }
    }
}

private string AnalyzeConstraintPurpose(string constraintName, List<string> columns)
{
    var name = constraintName.ToLower();
    var columnList = string.Join(", ", columns.Select(c => c.ToLower()));

    if (columnList.Contains("email")) return "Email uniqueness";
    if (columnList.Contains("username")) return "Username uniqueness";
    if (columnList.Contains("sku") || columnList.Contains("code")) return "Business identifier";
    if (columnList.Contains("number")) return "Business number uniqueness";
    if (columns.Count > 2) return "Complex business rule";
    if (columns.Count == 2) return "Composite business key";

    return "Data integrity constraint";
}
```

### Unique Constraint Maintenance

```csharp
public async Task ValidateUniqueConstraintsAsync(IDbConnection connection, string schema)
{
    Console.WriteLine($"Validating unique constraints in schema '{schema}'...");

    var tables = await connection.GetTableNamesAsync(schema);
    var issues = new List<string>();

    foreach (var tableName in tables)
    {
        var constraints = await connection.GetUniqueConstraintsAsync(schema, tableName);

        foreach (var constraint in constraints)
        {
            // Check for potential issues
            if (constraint.KeyColumnNames.Count > 5)
            {
                issues.Add($"Constraint '{constraint.KeyName}' on {tableName} has too many columns ({constraint.KeyColumnNames.Count}) - may impact performance");
            }

            // Check for duplicate constraint definitions
            var otherConstraints = constraints.Where(c => c.KeyName != constraint.KeyName).ToList();
            foreach (var other in otherConstraints)
            {
                if (constraint.KeyColumnNames.SequenceEqual(other.KeyColumnNames))
                {
                    issues.Add($"Duplicate unique constraints on {tableName}: '{constraint.KeyName}' and '{other.KeyName}' have same columns");
                }

                // Check for redundant constraints (subset)
                if (constraint.KeyColumnNames.All(c => other.KeyColumnNames.Contains(c)) &&
                    constraint.KeyColumnNames.Count < other.KeyColumnNames.Count)
                {
                    issues.Add($"Redundant constraint '{constraint.KeyName}' on {tableName} - covered by '{other.KeyName}'");
                }
            }

            Console.WriteLine($"✅ Constraint '{constraint.KeyName}' on {tableName} validated");
        }
    }

    if (issues.Any())
    {
        Console.WriteLine($"\n⚠️  Found {issues.Count} potential issues:");
        foreach (var issue in issues)
        {
            Console.WriteLine($"  - {issue}");
        }
    }
    else
    {
        Console.WriteLine("\n✅ All unique constraints appear to be optimal");
    }
}

public async Task StandardizeUniqueConstraintNamesAsync(IDbConnection connection, string schema)
{
    var tables = await connection.GetTableNamesAsync(schema);

    foreach (var tableName in tables)
    {
        var constraints = await connection.GetUniqueConstraintsAsync(schema, tableName);

        foreach (var constraint in constraints)
        {
            // Generate standard name: UQ_TableName_Column1_Column2
            var standardName = $"UQ_{tableName}_{string.Join("_", constraint.KeyColumnNames)}";

            if (constraint.KeyName != standardName)
            {
                Console.WriteLine($"Non-standard constraint name: {constraint.KeyName}");
                Console.WriteLine($"Suggested name: {standardName}");
                Console.WriteLine($"Columns: {string.Join(", ", constraint.KeyColumnNames)}");

                // Note: Renaming would require dropping and recreating
                // This is database-specific and requires careful handling of data
            }
        }
    }
}

public async Task CleanupTemporaryUniqueConstraintsAsync(IDbConnection connection, string schema)
{
    var tables = await connection.GetTableNamesAsync(schema);
    var cleanedCount = 0;

    foreach (var tableName in tables)
    {
        var constraintNames = await connection.GetUniqueConstraintNamesAsync(schema, tableName);
        var tempConstraints = constraintNames.Where(name =>
            name.Contains("temp_") ||
            name.Contains("_temp") ||
            name.StartsWith("tmp_") ||
            name.EndsWith("_backup") ||
            name.Contains("_old_") ||
            name.Contains("_test_")).ToList();

        foreach (var constraintName in tempConstraints)
        {
            bool dropped = await connection.DropUniqueConstraintIfExistsAsync(schema, tableName, constraintName);
            if (dropped)
            {
                Console.WriteLine($"Cleaned up temporary unique constraint: {constraintName}");
                cleanedCount++;
            }
        }
    }

    Console.WriteLine($"Cleaned up {cleanedCount} temporary unique constraints");
}
```

### Advanced Unique Constraint Scenarios

```csharp
public async Task MigrateToCompositeUniqueKeyAsync(IDbConnection connection, string schema, string tableName,
    string oldColumn, string[] newColumns)
{
    using var transaction = connection.BeginTransaction();
    try
    {
        // Find existing unique constraint on old column
        var oldConstraintName = await connection.GetUniqueConstraintNameOnColumnAsync(
            schema, tableName, oldColumn, tx: transaction);

        if (oldConstraintName != null)
        {
            Console.WriteLine($"Found existing constraint: {oldConstraintName}");

            // Drop old single-column constraint
            bool dropped = await connection.DropUniqueConstraintIfExistsAsync(
                schema, tableName, oldConstraintName, tx: transaction);

            if (dropped)
            {
                Console.WriteLine($"Dropped old constraint: {oldConstraintName}");

                // Create new composite constraint
                var newConstraintName = $"UQ_{tableName}_{string.Join("_", newColumns)}";
                var columns = newColumns.Select(c => new DmOrderedColumn(c)).ToArray();

                bool created = await connection.CreateUniqueConstraintIfNotExistsAsync(
                    schema, tableName, newConstraintName, columns, tx: transaction);

                if (created)
                {
                    Console.WriteLine($"Created new composite constraint: {newConstraintName}");
                    Console.WriteLine($"Columns: {string.Join(", ", newColumns)}");
                }
            }
        }
        else
        {
            Console.WriteLine($"No existing unique constraint found on column '{oldColumn}'");
        }

        transaction.Commit();
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
}

public async Task CreateConditionalUniqueConstraintsAsync(IDbConnection connection)
{
    // Note: This example shows the concept, but actual implementation
    // would be database-specific as not all providers support filtered unique indexes

    using var transaction = connection.BeginTransaction();
    try
    {
        // Example: Email must be unique only for active users
        // (This would typically be implemented as a filtered/partial unique index)

        Console.WriteLine("Creating conditional uniqueness constraints...");

        // For providers that support it, you could create filtered indexes
        // For others, you might need application-level validation or triggers

        // Standard approach: ensure active users have unique emails
        await connection.CreateUniqueConstraintIfNotExistsAsync(
            "app", "active_users_view", "UQ_ActiveUsers_Email",
            new[] { new DmOrderedColumn("Email") }, tx: transaction);

        transaction.Commit();
        Console.WriteLine("Conditional uniqueness setup completed");
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
}

public async Task AnalyzeUniqueConstraintPerformanceAsync(IDbConnection connection, string schema, string tableName)
{
    var constraints = await connection.GetUniqueConstraintsAsync(schema, tableName);

    Console.WriteLine($"Performance analysis for unique constraints on {schema}.{tableName}:");

    foreach (var constraint in constraints)
    {
        Console.WriteLine($"\nConstraint: {constraint.KeyName}");
        Console.WriteLine($"Columns: {string.Join(", ", constraint.KeyColumnNames)}");

        // Analyze potential performance impact
        if (constraint.KeyColumnNames.Count == 1)
        {
            var column = constraint.KeyColumnNames[0];
            Console.WriteLine($"✅ Single-column constraint on '{column}' - optimal for lookups");
        }
        else if (constraint.KeyColumnNames.Count <= 3)
        {
            Console.WriteLine($"⚡ Composite constraint with {constraint.KeyColumnNames.Count} columns - good for multi-column lookups");
        }
        else
        {
            Console.WriteLine($"⚠️  Wide constraint with {constraint.KeyColumnNames.Count} columns - may impact INSERT performance");
        }

        // Check if an index already exists for this constraint
        // (Most databases automatically create an index for unique constraints)
        Console.WriteLine($"📊 Automatic index created for uniqueness enforcement");

        // Suggest optimization opportunities
        var firstColumn = constraint.KeyColumnNames[0];
        if (firstColumn.ToLower().Contains("id"))
        {
            Console.WriteLine($"💡 First column '{firstColumn}' is likely high-cardinality - good for performance");
        }

        if (constraint.KeyColumnNames.Any(c => c.ToLower().Contains("date")))
        {
            Console.WriteLine($"📅 Contains date column - consider partitioning for very large tables");
        }
    }
}
```

Unique constraint methods provide essential functionality for maintaining data integrity by preventing duplicate values in specified columns, supporting both simple single-column uniqueness and complex composite business key scenarios across all supported database providers.
