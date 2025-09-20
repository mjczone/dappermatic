# Foreign Key Constraint Methods

Foreign key constraint methods provide comprehensive functionality for managing referential integrity relationships across all supported database providers. Foreign keys ensure data consistency and establish relationships between tables.

## Quick Navigation

- [Foreign Key Existence Checking](#foreign-key-existence-checking)
  - [DoesForeignKeyConstraintExistAsync](#doesforeignkeyconstraintexistasync) - Check if named foreign key exists
  - [DoesForeignKeyConstraintExistOnColumnAsync](#doesforeignkeyconstraintexistoncolumnasync) - Check if any foreign key exists on column
- [Foreign Key Creation](#foreign-key-creation)
  - [CreateForeignKeyConstraintIfNotExistsAsync (Model)](#createforeignkeyconstraintifnotexistsasync-dmforeignkeyconstraint) - Create from DmForeignKeyConstraint model
  - [CreateForeignKeyConstraintIfNotExistsAsync (Parameters)](#createforeignkeyconstraintifnotexistsasync-parameters) - Create with individual parameters
- [Foreign Key Discovery](#foreign-key-discovery)
  - [GetForeignKeyConstraintNamesAsync](#getforeignkeyconstraintnamesasync) - Get list of foreign key names with filtering
  - [GetForeignKeyConstraintNameOnColumnAsync](#getforeignkeyconstraintnamenoncolumnasync) - Get foreign key name on specific column
  - [GetForeignKeyConstraintsAsync](#getforeignkeyconstraintsasync) - Get complete foreign key models
  - [GetForeignKeyConstraintAsync](#getforeignkeyconstraintasync) - Get single foreign key by name
  - [GetForeignKeyConstraintOnColumnAsync](#getforeignkeyconstraintoncolumnasync) - Get foreign key model on specific column
- [Foreign Key Deletion](#foreign-key-deletion)
  - [DropForeignKeyConstraintIfExistsAsync](#dropforeignkeyconstraintifexistsasync) - Drop foreign key by name
  - [DropForeignKeyConstraintOnColumnIfExistsAsync](#dropforeignkeyconstraintoncolumnifexistsasync) - Drop foreign key on specific column

## Foreign Key Existence Checking

### DoesForeignKeyConstraintExistAsync

Check if a specific named foreign key constraint exists.

```csharp
// Check if named foreign key constraint exists
var fkName = "FK_Orders_Customer";
bool exists = await connection.DoesForeignKeyConstraintExistAsync("sales", "orders", fkName);

if (exists)
{
    Console.WriteLine($"Foreign key constraint '{fkName}' exists");
    
    // Get the foreign key details
    var fk = await connection.GetForeignKeyConstraintAsync("sales", "orders", fkName);
    if (fk != null)
    {
        Console.WriteLine($"References: {fk.ReferencedSchemaName}.{fk.ReferencedTableName}");
    }
}
else
{
    Console.WriteLine($"Foreign key constraint '{fkName}' does not exist");
}

// With transaction and cancellation
using var transaction = connection.BeginTransaction();
bool exists = await connection.DoesForeignKeyConstraintExistAsync(
    "sales", 
    "orders", 
    fkName,
    tx: transaction,
    cancellationToken: cancellationToken
);
```

**Parameters:**
- `schemaName` - Schema containing the table
- `tableName` - Name of the table containing the foreign key
- `constraintName` - Name of the foreign key constraint to check
- `tx` (optional) - Database transaction
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if foreign key constraint exists, `false` otherwise

### DoesForeignKeyConstraintExistOnColumnAsync

Check if any foreign key constraint exists on a specific column.

```csharp
// Check if any foreign key exists on a column
bool hasFK = await connection.DoesForeignKeyConstraintExistOnColumnAsync("sales", "orders", "customer_id");

if (hasFK)
{
    Console.WriteLine("Column 'customer_id' has at least one foreign key constraint");
    
    // Get the foreign key constraint on this column
    var fk = await connection.GetForeignKeyConstraintOnColumnAsync("sales", "orders", "customer_id");
    if (fk != null)
    {
        Console.WriteLine($"FK '{fk.KeyName}' references {fk.ReferencedSchemaName}.{fk.ReferencedTableName}");
    }
}
else
{
    Console.WriteLine("Column 'customer_id' has no foreign key constraints");
}

// With transaction
using var transaction = connection.BeginTransaction();
bool exists = await connection.DoesForeignKeyConstraintExistOnColumnAsync(
    "hr", "employees", "department_id", tx: transaction);
```

**Parameters:**
- `schemaName` - Schema containing the table
- `tableName` - Name of the table containing the column
- `columnName` - Name of the column to check for foreign keys
- `tx` (optional) - Database transaction
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if any foreign key constraint exists on the column, `false` otherwise

## Foreign Key Creation

### CreateForeignKeyConstraintIfNotExistsAsync (DmForeignKeyConstraint)

Create a foreign key constraint only if it doesn't already exist using a DmForeignKeyConstraint model.

```csharp
// Create foreign key if it doesn't exist
bool created = await connection.CreateForeignKeyConstraintIfNotExistsAsync(foreignKey);

if (created)
{
    Console.WriteLine($"Foreign key constraint '{foreignKey.KeyName}' was created");
}
else
{
    Console.WriteLine($"Foreign key constraint '{foreignKey.KeyName}' already existed");
}
```

**Parameters:**
- `constraint` - DmForeignKeyConstraint model defining the constraint (includes SchemaName and TableName)
- `tx` (optional) - Database transaction
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if foreign key was created, `false` if it already existed

### CreateForeignKeyConstraintIfNotExistsAsync (Parameters)

Create a foreign key constraint using individual parameters for convenience.

```csharp
// Simple single-column foreign key
bool created = await connection.CreateForeignKeyConstraintIfNotExistsAsync(
    schemaName: "sales",
    tableName: "orders",
    constraintName: "FK_Orders_Customer",
    sourceColumns: new[] { new DmOrderedColumn("CustomerId") },
    referencedTableName: "customers",
    referencedColumns: new[] { new DmOrderedColumn("CustomerId") },
    onDelete: DmForeignKeyAction.Cascade,
    onUpdate: DmForeignKeyAction.NoAction
);

// Multi-column foreign key with cross-schema reference
bool created = await connection.CreateForeignKeyConstraintIfNotExistsAsync(
    "sales",
    "order_items",
    "FK_OrderItems_Product_Warehouse",
    sourceColumns: new[]
    {
        new DmOrderedColumn("ProductId"),
        new DmOrderedColumn("WarehouseId")
    },
    referencedTableName: "product_warehouses",
    referencedColumns: new[]
    {
        new DmOrderedColumn("ProductId"),
        new DmOrderedColumn("WarehouseId")
    },
    onDelete: DmForeignKeyAction.Restrict,
    onUpdate: DmForeignKeyAction.Cascade,
    tx: transaction,
    cancellationToken: cancellationToken
);

// Employee self-referencing foreign key (manager relationship)
bool created = await connection.CreateForeignKeyConstraintIfNotExistsAsync(
    "hr",
    "employees",
    "FK_Employees_Manager",
    sourceColumns: new[] { new DmOrderedColumn("ManagerId") },
    referencedTableName: "employees", // Self-reference
    referencedColumns: new[] { new DmOrderedColumn("EmployeeId") },
    onDelete: DmForeignKeyAction.SetNull, // Manager can be deleted
    onUpdate: DmForeignKeyAction.Cascade
);
```

**Parameters:**
- `schemaName` - Schema containing the source table
- `tableName` - Name of the source table
- `constraintName` - Name of the foreign key constraint
- `sourceColumns` - Array of DmOrderedColumn in the source table
- `referencedTableName` - Name of the referenced table
- `referencedColumns` - Array of DmOrderedColumn in the referenced table
- `onDelete` (optional) - Action when referenced row is deleted (default: NoAction)
- `onUpdate` (optional) - Action when referenced row is updated (default: NoAction)
- `tx` (optional) - Database transaction
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if foreign key was created, `false` if it already existed

## Foreign Key Discovery

### GetForeignKeyConstraintNamesAsync

Retrieve a list of foreign key constraint names from a table, with optional filtering.

```csharp
// Get all foreign key constraint names on a table
List<string> allForeignKeys = await connection.GetForeignKeyConstraintNamesAsync("sales", "orders");
foreach (string fkName in allForeignKeys)
{
    Console.WriteLine($"Found foreign key: {fkName}");
}

// Get foreign key names with wildcard filter
List<string> customerFKs = await connection.GetForeignKeyConstraintNamesAsync("sales", "orders", "FK_*_Customer*");
// Finds: FK_Orders_Customer, FK_OrderItems_Customer, etc.

// Get foreign keys with pattern matching
List<string> referenceFKs = await connection.GetForeignKeyConstraintNamesAsync("sales", "orders", "*_Reference");
```

**Parameters:**
- `schemaName` - Schema containing the table
- `tableName` - Name of the table to search
- `nameFilter` (optional) - Wildcard pattern to filter foreign key names (`*` = any characters, `?` = single character)
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `List<string>` - List of matching foreign key constraint names

### GetForeignKeyConstraintNameOnColumnAsync

Get the foreign key constraint name that includes a specific column.

```csharp
// Get foreign key constraint name on a specific column
string? fkName = await connection.GetForeignKeyConstraintNameOnColumnAsync("sales", "orders", "customer_id");

if (fkName != null)
{
    Console.WriteLine($"Foreign key constraint on 'customer_id': {fkName}");
    
    // Get full details of the foreign key
    var fk = await connection.GetForeignKeyConstraintAsync("sales", "orders", fkName);
    if (fk != null)
    {
        Console.WriteLine($"References: {fk.ReferencedSchemaName}.{fk.ReferencedTableName}");
    }
}
else
{
    Console.WriteLine("No foreign key constraint found on 'customer_id' column");
}
```

**Parameters:**
- `schemaName` - Schema containing the table
- `tableName` - Name of the table containing the column
- `columnName` - Name of the column to find foreign key for
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `string?` - Name of the foreign key constraint, or `null` if none exists on the column

### GetForeignKeyConstraintsAsync

Retrieve complete DmForeignKeyConstraint models for table foreign keys.

```csharp
// Get all foreign keys with full structure information
List<DmForeignKeyConstraint> foreignKeys = await connection.GetForeignKeyConstraintsAsync("sales", "orders");

foreach (var fk in foreignKeys)
{
    Console.WriteLine($"Foreign Key: {fk.KeyName}");
    Console.WriteLine($"  Source columns: {string.Join(", ", fk.KeyColumnNames)}");
    Console.WriteLine($"  References: {fk.ReferencedSchemaName}.{fk.ReferencedTableName}");
    Console.WriteLine($"  Referenced columns: {string.Join(", ", fk.ReferencedKeyColumnNames)}");
    Console.WriteLine($"  On Delete: {fk.OnDelete}");
    Console.WriteLine($"  On Update: {fk.OnUpdate}");
}

// Get specific foreign keys with pattern
List<DmForeignKeyConstraint> customerFKs = await connection.GetForeignKeyConstraintsAsync("sales", "orders", "FK_*_Customer");

// With transaction
using var transaction = connection.BeginTransaction();
List<DmForeignKeyConstraint> foreignKeys = await connection.GetForeignKeyConstraintsAsync("hr", "employees", tx: transaction);
```

**Parameters:**
- `schemaName` - Schema containing the table
- `tableName` - Name of the table to search
- `nameFilter` (optional) - Wildcard pattern to filter foreign key names
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `List<DmForeignKeyConstraint>` - List of complete DmForeignKeyConstraint models

### GetForeignKeyConstraintAsync

Retrieve a single DmForeignKeyConstraint model for a specific foreign key.

```csharp
// Get specific foreign key constraint structure
DmForeignKeyConstraint? fk = await connection.GetForeignKeyConstraintAsync("sales", "orders", "FK_Orders_Customer");

if (fk != null)
{
    Console.WriteLine($"Foreign Key '{fk.KeyName}' details:");
    Console.WriteLine($"  Source Table: {fk.SchemaName}.{fk.TableName}");
    Console.WriteLine($"  Source Columns: {string.Join(", ", fk.KeyColumnNames)}");
    Console.WriteLine($"  Referenced Table: {fk.ReferencedSchemaName}.{fk.ReferencedTableName}");
    Console.WriteLine($"  Referenced Columns: {string.Join(", ", fk.ReferencedKeyColumnNames)}");
    Console.WriteLine($"  Delete Action: {fk.OnDelete}");
    Console.WriteLine($"  Update Action: {fk.OnUpdate}");
    
    // Check if it's a self-referencing foreign key
    if (fk.SchemaName == fk.ReferencedSchemaName && fk.TableName == fk.ReferencedTableName)
    {
        Console.WriteLine("  This is a self-referencing foreign key");
    }
    
    // Check if it's a multi-column foreign key
    if (fk.KeyColumnNames.Count > 1)
    {
        Console.WriteLine($"  This is a composite foreign key with {fk.KeyColumnNames.Count} columns");
    }
}
else
{
    Console.WriteLine("Foreign key constraint not found");
}
```

**Parameters:**
- `schemaName` - Schema containing the table
- `tableName` - Name of the table containing the foreign key
- `constraintName` - Name of the foreign key constraint to retrieve
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `DmForeignKeyConstraint?` - Complete foreign key constraint model, or `null` if not found

### GetForeignKeyConstraintOnColumnAsync

Retrieve the foreign key constraint that includes a specific column.

```csharp
// Get foreign key constraint on a specific column
DmForeignKeyConstraint? fk = await connection.GetForeignKeyConstraintOnColumnAsync("sales", "orders", "customer_id");

if (fk != null)
{
    Console.WriteLine($"Foreign key on 'customer_id': {fk.KeyName}");
    Console.WriteLine($"References: {fk.ReferencedSchemaName}.{fk.ReferencedTableName}.{string.Join(", ", fk.ReferencedKeyColumnNames)}");
    
    // Check cascade behavior
    if (fk.OnDelete == DmForeignKeyAction.Cascade)
    {
        Console.WriteLine("⚠️  Deleting referenced records will cascade delete dependent records");
    }
    else if (fk.OnDelete == DmForeignKeyAction.Restrict)
    {
        Console.WriteLine("🔒 Referenced records cannot be deleted while dependent records exist");
    }
}
else
{
    Console.WriteLine("No foreign key constraint found on 'customer_id' column");
}
```

**Parameters:**
- `schemaName` - Schema containing the table
- `tableName` - Name of the table containing the column
- `columnName` - Name of the column to find foreign key for
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `DmForeignKeyConstraint?` - Foreign key constraint model, or `null` if none exists on the column

## Foreign Key Deletion

### DropForeignKeyConstraintIfExistsAsync

Remove a foreign key constraint from a table if it exists.

```csharp
// Drop foreign key constraint if it exists
bool dropped = await connection.DropForeignKeyConstraintIfExistsAsync("sales", "orders", "FK_Orders_Customer");

if (dropped)
{
    Console.WriteLine("Foreign key constraint was dropped");
}
else
{
    Console.WriteLine("Foreign key constraint did not exist");
}

// Batch drop multiple foreign keys
var foreignKeysToRemove = new[] { "FK_Orders_TempCustomer", "FK_Orders_OldReference", "FK_Orders_BackupLink" };
foreach (var fkName in foreignKeysToRemove)
{
    bool wasDropped = await connection.DropForeignKeyConstraintIfExistsAsync("sales", "orders", fkName);
    Console.WriteLine($"Foreign key '{fkName}': {(wasDropped ? "Dropped" : "Not found")}");
}

// With transaction
using var transaction = connection.BeginTransaction();
try
{
    bool dropped = await connection.DropForeignKeyConstraintIfExistsAsync("sales", "orders", "FK_Orders_Customer", tx: transaction);
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
- `tableName` - Name of the table containing the foreign key
- `constraintName` - Name of the foreign key constraint to drop
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if foreign key was dropped, `false` if it didn't exist

### DropForeignKeyConstraintOnColumnIfExistsAsync

Remove the foreign key constraint that includes a specific column.

```csharp
// Drop foreign key constraint on a specific column
bool dropped = await connection.DropForeignKeyConstraintOnColumnIfExistsAsync("sales", "orders", "legacy_customer_id");

if (dropped)
{
    Console.WriteLine("Foreign key constraint on 'legacy_customer_id' was dropped");
    // Now safe to modify or drop the column
    await connection.DropColumnIfExistsAsync("sales", "orders", "legacy_customer_id");
}
else
{
    Console.WriteLine("No foreign key constraint found on 'legacy_customer_id'");
}

// Useful before column modifications
await connection.DropForeignKeyConstraintOnColumnIfExistsAsync("hr", "employees", "department_id");
// Note: AlterColumnAsync does not exist in DapperMatic
// You would need to drop and recreate the column or table to change column properties
// Then recreate foreign key with new structure
await connection.CreateForeignKeyConstraintIfNotExistsAsync("hr", "employees", newDepartmentFK);
```

**Parameters:**
- `schemaName` - Schema containing the table
- `tableName` - Name of the table containing the foreign key
- `columnName` - Name of the column to drop foreign key for
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if foreign key constraint was dropped, `false` if none existed on the column

## Practical Examples

### Referential Integrity Setup

```csharp
public async Task SetupOrderSystemForeignKeysAsync(IDbConnection connection)
{
    using var transaction = connection.BeginTransaction();
    try
    {
        // Customer -> Orders relationship
        var customerFK = new DmForeignKeyConstraint(
            "FK_Orders_Customer",
            new[] { "CustomerId" },
            "sales",
            "customers",
            new[] { "CustomerId" }
        )
        {
            OnDelete = DmForeignKeyAction.Restrict, // Prevent deleting customers with orders
            OnUpdate = DmForeignKeyAction.Cascade   // Update cascades to orders
        };
        
        bool created = await connection.CreateForeignKeyConstraintIfNotExistsAsync(
            "sales", "orders", customerFK, tx: transaction);
        Console.WriteLine($"Customer FK: {(created ? "Created" : "Already exists")}");
        
        // Orders -> OrderItems relationship
        var orderItemsFK = new DmForeignKeyConstraint(
            "FK_OrderItems_Order",
            new[] { "OrderId" },
            "sales",
            "orders",
            new[] { "OrderId" }
        )
        {
            OnDelete = DmForeignKeyAction.Cascade, // Delete order items when order is deleted
            OnUpdate = DmForeignKeyAction.Cascade
        };
        
        created = await connection.CreateForeignKeyConstraintIfNotExistsAsync(
            "sales", "order_items", orderItemsFK, tx: transaction);
        Console.WriteLine($"Order Items FK: {(created ? "Created" : "Already exists")}");
        
        // Products -> OrderItems relationship
        var productFK = new DmForeignKeyConstraint(
            "FK_OrderItems_Product",
            new[] { "ProductId" },
            "inventory",
            "products",
            new[] { "ProductId" }
        )
        {
            OnDelete = DmForeignKeyAction.Restrict, // Prevent deleting products in orders
            OnUpdate = DmForeignKeyAction.Cascade
        };
        
        created = await connection.CreateForeignKeyConstraintIfNotExistsAsync(
            "sales", "order_items", productFK, tx: transaction);
        Console.WriteLine($"Product FK: {(created ? "Created" : "Already exists")}");
        
        transaction.Commit();
        Console.WriteLine("Order system foreign keys setup completed");
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
}
```

### Foreign Key Analysis and Validation

```csharp
public async Task AnalyzeForeignKeyIntegrityAsync(IDbConnection connection, string schema)
{
    var tables = await connection.GetTableNamesAsync(schema);
    
    Console.WriteLine($"Foreign Key Analysis for schema '{schema}':");
    
    var totalForeignKeys = 0;
    var tablesWithForeignKeys = new List<string>();
    var foreignKeyActions = new Dictionary<string, int>();
    var selfReferencingTables = new List<string>();
    
    foreach (var tableName in tables)
    {
        var foreignKeys = await connection.GetForeignKeyConstraintsAsync(schema, tableName);
        
        if (foreignKeys.Any())
        {
            tablesWithForeignKeys.Add(tableName);
            totalForeignKeys += foreignKeys.Count;
            
            foreach (var fk in foreignKeys)
            {
                // Track delete actions
                var deleteAction = fk.OnDelete.ToString();
                foreignKeyActions[deleteAction] = foreignKeyActions.GetValueOrDefault(deleteAction, 0) + 1;
                
                // Check for self-referencing foreign keys
                if (fk.SchemaName == fk.ReferencedSchemaName && fk.TableName == fk.ReferencedTableName)
                {
                    if (!selfReferencingTables.Contains(tableName))
                    {
                        selfReferencingTables.Add(tableName);
                    }
                }
                
                Console.WriteLine($"  {tableName}.{fk.KeyName}:");
                Console.WriteLine($"    Columns: {string.Join(", ", fk.KeyColumnNames)}");
                Console.WriteLine($"    References: {fk.ReferencedSchemaName}.{fk.ReferencedTableName}({string.Join(", ", fk.ReferencedKeyColumnNames)})");
                Console.WriteLine($"    Delete Action: {fk.OnDelete}");
                Console.WriteLine($"    Update Action: {fk.OnUpdate}");
            }
        }
    }
    
    Console.WriteLine($"\nSummary:");
    Console.WriteLine($"Total tables: {tables.Count}");
    Console.WriteLine($"Tables with foreign keys: {tablesWithForeignKeys.Count}");
    Console.WriteLine($"Total foreign key constraints: {totalForeignKeys}");
    Console.WriteLine($"Self-referencing tables: {selfReferencingTables.Count}");
    
    if (selfReferencingTables.Any())
    {
        Console.WriteLine($"Self-referencing tables: {string.Join(", ", selfReferencingTables)}");
    }
    
    Console.WriteLine("\nDelete Actions:");
    foreach (var (action, count) in foreignKeyActions.OrderByDescending(kvp => kvp.Value))
    {
        Console.WriteLine($"  {action}: {count}");
    }
    
    // Check for potential integrity issues
    var cascadeDeletes = foreignKeyActions.GetValueOrDefault("Cascade", 0);
    if (cascadeDeletes > 0)
    {
        Console.WriteLine($"\n⚠️  {cascadeDeletes} foreign keys use CASCADE delete - ensure this is intentional");
    }
}
```

### Foreign Key Dependency Management

```csharp
public async Task AnalyzeTableDependenciesAsync(IDbConnection connection, string schema)
{
    var tables = await connection.GetTableNamesAsync(schema);
    var dependencies = new Dictionary<string, List<string>>();
    var dependents = new Dictionary<string, List<string>>();
    
    // Build dependency maps
    foreach (var tableName in tables)
    {
        dependencies[tableName] = new List<string>();
        dependents[tableName] = new List<string>();
    }
    
    foreach (var tableName in tables)
    {
        var foreignKeys = await connection.GetForeignKeyConstraintsAsync(schema, tableName);
        
        foreach (var fk in foreignKeys)
        {
            var referencedTable = fk.ReferencedTableName;
            
            // This table depends on the referenced table
            dependencies[tableName].Add(referencedTable);
            
            // The referenced table has dependents
            if (dependents.ContainsKey(referencedTable))
            {
                dependents[referencedTable].Add(tableName);
            }
        }
    }
    
    Console.WriteLine("Table Dependency Analysis:");
    
    // Find root tables (no dependencies)
    var rootTables = dependencies.Where(kvp => !kvp.Value.Any()).Select(kvp => kvp.Key).ToList();
    Console.WriteLine($"\nRoot tables (no foreign key dependencies): {rootTables.Count}");
    foreach (var table in rootTables.OrderBy(t => t))
    {
        Console.WriteLine($"  📋 {table}");
        ShowDependents(dependents, table, 1);
    }
    
    // Find tables with most dependencies
    var mostDependent = dependencies.Where(kvp => kvp.Value.Any())
        .OrderByDescending(kvp => kvp.Value.Count)
        .Take(5)
        .ToList();
        
    if (mostDependent.Any())
    {
        Console.WriteLine($"\nTables with most dependencies:");
        foreach (var (table, deps) in mostDependent)
        {
            Console.WriteLine($"  {table} depends on {deps.Count} tables: {string.Join(", ", deps.Distinct())}");
        }
    }
    
    // Find tables that are most referenced
    var mostReferenced = dependents.Where(kvp => kvp.Value.Any())
        .OrderByDescending(kvp => kvp.Value.Count)
        .Take(5)
        .ToList();
        
    if (mostReferenced.Any())
    {
        Console.WriteLine($"\nMost referenced tables:");
        foreach (var (table, deps) in mostReferenced)
        {
            Console.WriteLine($"  {table} is referenced by {deps.Count} tables: {string.Join(", ", deps.Distinct())}");
        }
    }
}

private void ShowDependents(Dictionary<string, List<string>> dependents, string table, int level, int maxLevel = 3)
{
    if (level > maxLevel || !dependents.ContainsKey(table)) return;
    
    var indent = new string(' ', level * 2);
    var tableDependents = dependents[table].Distinct().ToList();
    
    foreach (var dependent in tableDependents)
    {
        Console.WriteLine($"{indent}└── {dependent}");
        ShowDependents(dependents, dependent, level + 1, maxLevel);
    }
}
```

### Foreign Key Maintenance Operations

```csharp
public async Task ValidateForeignKeyConstraintsAsync(IDbConnection connection, string schema)
{
    Console.WriteLine($"Validating foreign key constraints in schema '{schema}'...");
    
    var tables = await connection.GetTableNamesAsync(schema);
    var validationErrors = new List<string>();
    
    foreach (var tableName in tables)
    {
        var foreignKeys = await connection.GetForeignKeyConstraintsAsync(schema, tableName);
        
        foreach (var fk in foreignKeys)
        {
            try
            {
                // Check if referenced table exists
                bool referencedTableExists = await connection.DoesTableExistAsync(
                    fk.ReferencedSchemaName, fk.ReferencedTableName);
                    
                if (!referencedTableExists)
                {
                    validationErrors.Add($"FK '{fk.KeyName}' on {tableName} references non-existent table {fk.ReferencedSchemaName}.{fk.ReferencedTableName}");
                    continue;
                }
                
                // Check if referenced columns exist
                foreach (var referencedColumn in fk.ReferencedKeyColumnNames)
                {
                    bool columnExists = await connection.DoesColumnExistAsync(
                        fk.ReferencedSchemaName, fk.ReferencedTableName, referencedColumn);
                        
                    if (!columnExists)
                    {
                        validationErrors.Add($"FK '{fk.KeyName}' on {tableName} references non-existent column {referencedColumn} in {fk.ReferencedSchemaName}.{fk.ReferencedTableName}");
                    }
                }
                
                // Check if source columns exist
                foreach (var sourceColumn in fk.KeyColumnNames)
                {
                    bool columnExists = await connection.DoesColumnExistAsync(schema, tableName, sourceColumn);
                    
                    if (!columnExists)
                    {
                        validationErrors.Add($"FK '{fk.KeyName}' on {tableName} uses non-existent source column {sourceColumn}");
                    }
                }
                
                Console.WriteLine($"✅ FK '{fk.KeyName}' on {tableName} is valid");
            }
            catch (Exception ex)
            {
                validationErrors.Add($"Error validating FK '{fk.KeyName}' on {tableName}: {ex.Message}");
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
        Console.WriteLine("\n✅ All foreign key constraints are valid");
    }
}

public async Task RepairOrphanedForeignKeysAsync(IDbConnection connection, string schema)
{
    Console.WriteLine("Checking for orphaned foreign key constraints...");
    
    var tables = await connection.GetTableNamesAsync(schema);
    var orphanedForeignKeys = new List<(string tableName, string fkName, string reason)>();
    
    foreach (var tableName in tables)
    {
        var foreignKeys = await connection.GetForeignKeyConstraintsAsync(schema, tableName);
        
        foreach (var fk in foreignKeys)
        {
            // Check if referenced table still exists
            bool referencedTableExists = await connection.DoesTableExistAsync(
                fk.ReferencedSchemaName, fk.ReferencedTableName);
                
            if (!referencedTableExists)
            {
                orphanedForeignKeys.Add((tableName, fk.KeyName, 
                    $"Referenced table {fk.ReferencedSchemaName}.{fk.ReferencedTableName} does not exist"));
            }
        }
    }
    
    if (orphanedForeignKeys.Any())
    {
        Console.WriteLine($"Found {orphanedForeignKeys.Count} orphaned foreign key constraints:");
        
        foreach (var (tableName, fkName, reason) in orphanedForeignKeys)
        {
            Console.WriteLine($"  ⚠️  {tableName}.{fkName}: {reason}");
            
            // Option to remove orphaned foreign keys
            Console.Write($"Remove orphaned foreign key {fkName}? (y/n): ");
            var response = Console.ReadLine();
            
            if (response?.ToLower() == "y")
            {
                try
                {
                    bool dropped = await connection.DropForeignKeyConstraintIfExistsAsync(schema, tableName, fkName);
                    if (dropped)
                    {
                        Console.WriteLine($"    ✅ Removed {fkName}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    ❌ Failed to remove {fkName}: {ex.Message}");
                }
            }
        }
    }
    else
    {
        Console.WriteLine("✅ No orphaned foreign key constraints found");
    }
}
```

Foreign key constraint methods provide essential functionality for maintaining referential integrity and establishing robust data relationships across all supported database providers.