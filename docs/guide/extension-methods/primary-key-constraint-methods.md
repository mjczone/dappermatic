# Primary Key Constraint Methods

Primary key constraint methods provide functionality for managing primary key constraints across all supported database providers. Primary keys ensure entity uniqueness and provide the foundation for referential integrity.

## Quick Navigation

- [Primary Key Existence Checking](#primary-key-existence-checking)
  - [DoesPrimaryKeyConstraintExistAsync](#doesprimarykeyconstraintexistasync) - Check if table has a primary key
- [Primary Key Creation](#primary-key-creation)
  - [CreatePrimaryKeyConstraintIfNotExistsAsync (Model)](#createprimarykeyconstraintifnotexistsasync-dmprimarykeyconstraint) - Create from DmPrimaryKeyConstraint model
  - [CreatePrimaryKeyConstraintIfNotExistsAsync (Parameters)](#createprimarykeyconstraintifnotexistsasync-parameters) - Create with individual parameters
- [Primary Key Discovery](#primary-key-discovery)
  - [GetPrimaryKeyConstraintAsync](#getprimarykeyconstraintasync) - Get table's primary key constraint
- [Primary Key Deletion](#primary-key-deletion)
  - [DropPrimaryKeyConstraintIfExistsAsync](#dropprimarykeyconstraintifexistsasync) - Drop primary key constraint

## Primary Key Existence Checking

### DoesPrimaryKeyConstraintExistAsync

Check if a primary key constraint exists on a table.

```csharp
// Check if table has a primary key
bool hasPrimaryKey = await connection.DoesPrimaryKeyConstraintExistAsync("app", "employees");

if (hasPrimaryKey)
{
    Console.WriteLine("Table 'employees' has a primary key constraint");
    
    // Get the primary key details
    var pk = await connection.GetPrimaryKeyConstraintAsync("app", "employees");
    if (pk != null)
    {
        Console.WriteLine($"Primary key '{pk.KeyName}' on columns: {string.Join(", ", pk.KeyColumnNames)}");
    }
}
else
{
    Console.WriteLine("Table 'employees' has no primary key constraint");
    // Consider adding a primary key for referential integrity
}

// With transaction and cancellation
using var transaction = connection.BeginTransaction();
bool exists = await connection.DoesPrimaryKeyConstraintExistAsync(
    "app", 
    "employees", 
    tx: transaction,
    cancellationToken: cancellationToken
);
```

**Parameters:**
- `schemaName` - Schema containing the table
- `tableName` - Name of the table to check for primary key
- `tx` (optional) - Database transaction
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if table has a primary key constraint, `false` otherwise

## Primary Key Creation

### CreatePrimaryKeyConstraintIfNotExistsAsync (DmPrimaryKeyConstraint)

Create a primary key constraint only if the table doesn't already have one.

```csharp
// Create primary key if table doesn't have one
bool created = await connection.CreatePrimaryKeyConstraintIfNotExistsAsync(primaryKey);

if (created)
{
    Console.WriteLine($"Primary key constraint '{primaryKey.KeyName}' was created");
}
else
{
    Console.WriteLine("Table already has a primary key constraint");
}
```

**Parameters:**
- `constraint` - DmPrimaryKeyConstraint model defining the constraint (includes SchemaName and TableName)
- `tx` (optional) - Database transaction
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if primary key was created, `false` if table already had a primary key

### CreatePrimaryKeyConstraintIfNotExistsAsync (Parameters)

Create a primary key constraint using individual parameters for convenience.

```csharp
// Simple single-column primary key
bool created = await connection.CreatePrimaryKeyConstraintIfNotExistsAsync(
    schemaName: "app",
    tableName: "employees",
    constraintName: "PK_Employees_Id",
    columns: new[] { new DmOrderedColumn("EmployeeId", DmColumnOrder.Ascending) }
);

// Multi-column composite primary key
bool created = await connection.CreatePrimaryKeyConstraintIfNotExistsAsync(
    "inventory",
    "product_warehouses", 
    "PK_ProductWarehouses_Composite",
    new[]
    {
        new DmOrderedColumn("ProductId", DmColumnOrder.Ascending),
        new DmOrderedColumn("WarehouseId", DmColumnOrder.Ascending)
    },
    tx: transaction,
    cancellationToken: cancellationToken
);

// Named constraint
bool created = await connection.CreatePrimaryKeyConstraintIfNotExistsAsync(
    "app",
    "categories",
    "PK_Categories_CategoryId",
    new[] { new DmOrderedColumn("CategoryId") }
);
```

**Parameters:**
- `schemaName` - Schema containing the table
- `tableName` - Name of the table to add primary key to
- `constraintName` - Name of the primary key constraint
- `columns` - Array of DmOrderedColumn defining key columns and order
- `tx` (optional) - Database transaction
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if primary key was created, `false` if table already had a primary key

## Primary Key Discovery

### GetPrimaryKeyConstraintAsync

Retrieve the primary key constraint for a table.

```csharp
// Get primary key constraint for table
DmPrimaryKeyConstraint? primaryKey = await connection.GetPrimaryKeyConstraintAsync("app", "employees");

if (primaryKey != null)
{
    Console.WriteLine($"Primary key constraint details:");
    Console.WriteLine($"  Name: {primaryKey.KeyName}");
    Console.WriteLine($"  Columns: {string.Join(", ", primaryKey.KeyColumnNames)}");
    Console.WriteLine($"  Column count: {primaryKey.KeyColumnNames.Count}");
    
    // Check if it's a composite key
    if (primaryKey.KeyColumnNames.Count > 1)
    {
        Console.WriteLine("  This is a composite primary key");
    }
    
    // Show column order if available
    if (primaryKey.KeyColumns.Any())
    {
        Console.WriteLine("  Column order:");
        foreach (var column in primaryKey.KeyColumns)
        {
            Console.WriteLine($"    {column.ColumnName} ({column.ColumnOrder})");
        }
    }
}
else
{
    Console.WriteLine("Table has no primary key constraint");
}

// With transaction
using var transaction = connection.BeginTransaction();
var pk = await connection.GetPrimaryKeyConstraintAsync("app", "employees", tx: transaction);
```

**Parameters:**
- `schemaName` - Schema containing the table
- `tableName` - Name of the table to get primary key from
- `tx` (optional) - Database transaction
- `cancellationToken` (optional) - Cancellation token

**Returns:** `DmPrimaryKeyConstraint?` - Primary key constraint model, or `null` if table has no primary key


## Primary Key Deletion

### DropPrimaryKeyConstraintIfExistsAsync

Remove a primary key constraint from a table if it exists.

```csharp
// Drop primary key constraint if it exists
bool dropped = await connection.DropPrimaryKeyConstraintIfExistsAsync("app", "employees");

if (dropped)
{
    Console.WriteLine("Primary key constraint was dropped");
}
else
{
    Console.WriteLine("Table had no primary key constraint");
}

// With transaction
using var transaction = connection.BeginTransaction();
try
{
    bool dropped = await connection.DropPrimaryKeyConstraintIfExistsAsync("app", "employees", tx: transaction);
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
- `tableName` - Name of the table containing the primary key
- `constraintName` (optional) - Name of the primary key constraint to drop
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:**
- `DropPrimaryKeyConstraintAsync`: `Task` - Completes when primary key is dropped
- `DropPrimaryKeyConstraintIfExistsAsync`: `bool` - `true` if primary key was dropped, `false` if none existed

## Practical Examples

### Primary Key Migration Patterns

```csharp
public async Task MigrateToPrimaryKeyAsync(IDbConnection connection, string schema, string tableName)
{
    using var transaction = connection.BeginTransaction();
    try
    {
        // Check if table already has a primary key
        bool hasPK = await connection.DoesPrimaryKeyConstraintExistAsync(schema, tableName, tx: transaction);
        
        if (!hasPK)
        {
            // Add an identity column as primary key
            var idColumn = new DmColumn("Id", typeof(long))
            {
                SchemaName = schema,
                TableName = tableName,
                IsAutoIncrement = true,
                IsNullable = false
            };
            
            await connection.CreateColumnIfNotExistsAsync(idColumn, tx: transaction);
            
            // Create primary key constraint
            var primaryKey = new DmPrimaryKeyConstraint($"PK_{tableName}_Id", new[] { "Id" })
            {
                SchemaName = schema,
                TableName = tableName
            };
            await connection.CreatePrimaryKeyConstraintIfNotExistsAsync(primaryKey, tx: transaction);
            
            Console.WriteLine($"Added identity primary key to {tableName}");
        }
        else
        {
            Console.WriteLine($"Table {tableName} already has a primary key");
        }
        
        transaction.Commit();
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
}
```

### Composite Key Management

```csharp
public async Task CreateJunctionTableAsync(IDbConnection connection, string schema, 
    string tableName, string leftTable, string leftColumn, string rightTable, string rightColumn)
{
    using var transaction = connection.BeginTransaction();
    try
    {
        // Create the junction table columns
        var leftCol = new DmColumn($"{leftTable}Id", typeof(int)) { IsNullable = false };
        var rightCol = new DmColumn($"{rightTable}Id", typeof(int)) { IsNullable = false };
        
        var columns = new[] { leftCol, rightCol };
        
        // Create composite primary key
        var compositePK = new DmPrimaryKeyConstraint(
            $"PK_{tableName}_Composite", 
            new[] { $"{leftTable}Id", $"{rightTable}Id" }
        );
        
        // Create foreign key constraints
        var leftFK = new DmForeignKeyConstraint(
            $"FK_{tableName}_{leftTable}",
            new[] { $"{leftTable}Id" },
            schema,
            leftTable,
            new[] { leftColumn }
        );
        
        var rightFK = new DmForeignKeyConstraint(
            $"FK_{tableName}_{rightTable}",
            new[] { $"{rightTable}Id" },
            schema,
            rightTable,
            new[] { rightColumn }
        );
        
        // Create the table with all constraints
        bool created = await connection.CreateTableIfNotExistsAsync(
            schema,
            tableName,
            columns,
            compositePK,
            checkConstraints: null,
            defaultConstraints: null,
            uniqueConstraints: null,
            foreignKeyConstraints: new[] { leftFK, rightFK },
            indexes: null,
            tx: transaction
        );
        
        if (created)
        {
            Console.WriteLine($"Junction table '{tableName}' created with composite primary key");
        }
        
        transaction.Commit();
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
}
```

### Primary Key Analysis

```csharp
public async Task AnalyzePrimaryKeysAsync(IDbConnection connection, string schema)
{
    var tables = await connection.GetTableNamesAsync(schema);
    
    Console.WriteLine($"Primary Key Analysis for schema '{schema}':");
    Console.WriteLine($"Total tables: {tables.Count}");
    
    var tablesWithPK = new List<string>();
    var tablesWithoutPK = new List<string>();
    var compositePKTables = new List<(string tableName, int columnCount)>();
    
    foreach (var tableName in tables)
    {
        bool hasPK = await connection.DoesPrimaryKeyConstraintExistAsync(schema, tableName);
        
        if (hasPK)
        {
            tablesWithPK.Add(tableName);
            
            var pk = await connection.GetPrimaryKeyConstraintAsync(schema, tableName);
            if (pk != null && pk.KeyColumnNames.Count > 1)
            {
                compositePKTables.Add((tableName, pk.KeyColumnNames.Count));
            }
        }
        else
        {
            tablesWithoutPK.Add(tableName);
        }
    }
    
    Console.WriteLine($"Tables with primary keys: {tablesWithPK.Count}");
    Console.WriteLine($"Tables without primary keys: {tablesWithoutPK.Count}");
    Console.WriteLine($"Tables with composite primary keys: {compositePKTables.Count}");
    
    if (tablesWithoutPK.Any())
    {
        Console.WriteLine("\nTables missing primary keys:");
        foreach (var table in tablesWithoutPK)
        {
            Console.WriteLine($"  ⚠️  {table}");
        }
    }
    
    if (compositePKTables.Any())
    {
        Console.WriteLine("\nTables with composite primary keys:");
        foreach (var (tableName, columnCount) in compositePKTables)
        {
            Console.WriteLine($"  📋 {tableName} ({columnCount} columns)");
        }
    }
    
    // Calculate primary key coverage
    var coverage = tables.Count > 0 ? (double)tablesWithPK.Count / tables.Count * 100 : 0;
    Console.WriteLine($"\nPrimary key coverage: {coverage:F1}%");
    
    if (coverage < 100)
    {
        Console.WriteLine("💡 Consider adding primary keys to tables without them for referential integrity");
    }
}
```

### Primary Key Cleanup and Standardization

```csharp
public async Task StandardizePrimaryKeyNamesAsync(IDbConnection connection, string schema)
{
    var tables = await connection.GetTableNamesAsync(schema);
    
    foreach (var tableName in tables)
    {
        var pk = await connection.GetPrimaryKeyConstraintAsync(schema, tableName);
        if (pk == null) continue;
        
        // Check if primary key name follows standard convention
        var expectedName = $"PK_{tableName}";
        if (pk.KeyName != expectedName)
        {
            Console.WriteLine($"Standardizing primary key name for {tableName}: '{pk.KeyName}' -> '{expectedName}'");
            
            using var transaction = connection.BeginTransaction();
            try
            {
                // Drop existing primary key
                await connection.DropPrimaryKeyConstraintIfExistsAsync(schema, tableName, tx: transaction);
                
                // Create new primary key with standard name
                var standardPK = new DmPrimaryKeyConstraint(expectedName, pk.KeyColumnNames.ToArray())
                {
                    SchemaName = schema,
                    TableName = tableName
                };
                await connection.CreatePrimaryKeyConstraintIfNotExistsAsync(standardPK, tx: transaction);
                
                transaction.Commit();
                Console.WriteLine($"  ✅ Renamed to '{expectedName}'");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                Console.WriteLine($"  ❌ Failed to rename: {ex.Message}");
            }
        }
    }
}

public async Task ValidatePrimaryKeyIntegrityAsync(IDbConnection connection, string schema)
{
    var tables = await connection.GetTablesAsync(schema);
    
    foreach (var table in tables)
    {
        var pk = table.PrimaryKeyConstraint;
        if (pk == null)
        {
            Console.WriteLine($"⚠️  Table '{table.TableName}' has no primary key");
            continue;
        }
        
        // Validate that all primary key columns exist and have correct properties
        foreach (var columnName in pk.KeyColumnNames)
        {
            var column = table.Columns.FirstOrDefault(c => c.ColumnName == columnName);
            if (column == null)
            {
                Console.WriteLine($"❌ Primary key column '{columnName}' not found in table '{table.TableName}'");
                continue;
            }
            
            if (column.IsNullable)
            {
                Console.WriteLine($"⚠️  Primary key column '{columnName}' in table '{table.TableName}' allows NULL values");
            }
        }
        
        Console.WriteLine($"✅ Primary key '{pk.KeyName}' on table '{table.TableName}' is valid");
    }
}
```

Primary key constraint methods provide essential functionality for maintaining data integrity and establishing the foundation for relational database design across all supported database providers.