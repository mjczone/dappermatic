# Index Methods

Index methods provide comprehensive functionality for managing database indexes across all supported providers. Indexes are critical for query performance optimization and can significantly impact database efficiency.

## Quick Navigation

- [Index Existence Checking](#index-existence-checking)
  - [DoesIndexExistAsync](#doesindexexistasync) - Check if specific index exists on table
  - [DoesIndexExistOnColumnAsync](#doesindexexistoncolumnasync) - Check if any index exists on column
- [Index Creation](#index-creation)
  - [CreateIndexIfNotExistsAsync (Model)](#createindexifnotexistsasync-dmindex) - Create from DmIndex model
  - [CreateIndexIfNotExistsAsync (Parameters)](#createindexifnotexistsasync-parameters) - Create with individual parameters
- [Index Discovery](#index-discovery)
  - [GetIndexNamesAsync](#getindexnamesasync) - Get list of index names with filtering
  - [GetIndexNamesOnColumnAsync](#getindexnamesoncolumnasync) - Get index names that include specific column
  - [GetIndexesAsync](#getindexesasync) - Get complete index models with structure
  - [GetIndexAsync](#getindexasync) - Get single index model by name
  - [GetIndexesOnColumnAsync](#getindexesoncolumnasync) - Get all indexes that include specific column
- [Index Deletion](#index-deletion)
  - [DropIndexIfExistsAsync](#dropindexifexistsasync) - Drop index by name
  - [DropIndexesOnColumnIfExistsAsync](#dropindexesoncolumnifexistsasync) - Drop all indexes on specific column

## Index Existence Checking

### DoesIndexExistAsync

Check if a specific index exists on a table.

```csharp
// Check if named index exists
var indexName = "IX_Employees_LastName";
bool exists = await connection.DoesIndexExistAsync("app", "employees", indexName);

if (exists)
{
    Console.WriteLine($"Index '{indexName}' already exists");
}
else
{
    Console.WriteLine($"Index '{indexName}' does not exist");
    await connection.CreateIndexIfNotExistsAsync("app", "employees", index);
}


// With transaction and cancellation
using var transaction = connection.BeginTransaction();
bool exists = await connection.DoesIndexExistAsync(
    "app", 
    "employees", 
    indexName,
    tx: transaction,
    cancellationToken: cancellationToken
);
```

**Parameters:**
- `schemaName` - Schema containing the table
- `tableName` - Name of the table containing the index
- `indexName` - Name of the index to check
- `tx` (optional) - Database transaction
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if index exists, `false` otherwise

### DoesIndexExistOnColumnAsync

Check if any index exists on a specific column.

```csharp
// Check if any index exists on a column
bool exists = await connection.DoesIndexExistOnColumnAsync("app", "employees", "email");

if (exists)
{
    Console.WriteLine("Column 'email' has at least one index");
    
    // Get all indexes on this column
    var indexes = await connection.GetIndexesOnColumnAsync("app", "employees", "email");
    Console.WriteLine($"Found {indexes.Count} indexes on 'email' column");
}
else
{
    Console.WriteLine("Column 'email' has no indexes");
    // Consider creating an index for performance
    await CreateEmailIndexAsync(connection);
}

// With transaction
using var transaction = connection.BeginTransaction();
bool exists = await connection.DoesIndexExistOnColumnAsync(
    "app", "employees", "department_id", tx: transaction);
```

**Parameters:**
- `schemaName` - Schema containing the table
- `tableName` - Name of the table containing the column
- `columnName` - Name of the column to check for indexes
- `tx` (optional) - Database transaction
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if any index exists on the column, `false` otherwise

## Index Creation

### CreateIndexIfNotExistsAsync (DmIndex)

Create an index only if it doesn't already exist using a DmIndex model.

```csharp
// Create index if it doesn't exist
bool created = await connection.CreateIndexIfNotExistsAsync("app", "employees", index);

if (created)
{
    Console.WriteLine($"Index '{index.IndexName}' was created");
}
else
{
    Console.WriteLine($"Index '{index.IndexName}' already existed");
}
```

**Parameters:**
- `schemaName` - Schema containing the table
- `tableName` - Name of the table to create index on
- `index` - DmIndex model defining the index structure
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if index was created, `false` if it already existed

### CreateIndexIfNotExistsAsync (Parameters)

Create an index using individual parameters for convenience.

```csharp
// Simple single-column index
var indexName = "IX_Employees_DepartmentId";
bool created = await connection.CreateIndexIfNotExistsAsync(
    schemaName: "app",
    tableName: "employees",
    indexName: indexName,
    columns: new[] { new DmOrderedColumn("DepartmentId", DmColumnOrder.Ascending) },
    isUnique: false
);

// Multi-column composite index
bool created = await connection.CreateIndexIfNotExistsAsync(
    "app",
    "orders",
    "IX_Orders_CustomerDate",
    new[]
    {
        new DmOrderedColumn("CustomerId", DmColumnOrder.Ascending),
        new DmOrderedColumn("OrderDate", DmColumnOrder.Descending) // Recent orders first
    },
    isUnique: false,
    tx: transaction,
    commandTimeout: 60,
    cancellationToken: cancellationToken
);

// Unique constraint via index
bool created = await connection.CreateIndexIfNotExistsAsync(
    "app",
    "users",
    "IX_Users_Email_Unique",
    new[] { new DmOrderedColumn("Email", DmColumnOrder.Ascending) },
    isUnique: true // Enforces uniqueness
);

// Performance index for frequent queries
bool created = await connection.CreateIndexIfNotExistsAsync(
    "app",
    "products",
    "IX_Products_CategoryPrice",
    new[]
    {
        new DmOrderedColumn("CategoryId", DmColumnOrder.Ascending),
        new DmOrderedColumn("Price", DmColumnOrder.Descending),
        new DmOrderedColumn("ProductName", DmColumnOrder.Ascending)
    },
    isUnique: false
);
```

**Parameters:**
- `schemaName` - Schema containing the table
- `tableName` - Name of the table to create index on
- `indexName` - Name of the index to create
- `columns` - Array of DmOrderedColumn defining indexed columns and sort order
- `isUnique` (optional) - Whether index enforces uniqueness (default: false)
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if index was created, `false` if it already existed

## Index Discovery

### GetIndexNamesAsync

Retrieve a list of index names from a table, with optional filtering.

```csharp
// Get all index names on a table
List<string> allIndexes = await connection.GetIndexNamesAsync("app", "employees");
foreach (string indexName in allIndexes)
{
    Console.WriteLine($"Found index: {indexName}");
}

// Get index names with wildcard filter
List<string> performanceIndexes = await connection.GetIndexNamesAsync("app", "employees", "IX_*");
// Finds: IX_Employees_LastName, IX_Employees_Email, etc.

// Get indexes with pattern matching
List<string> uniqueIndexes = await connection.GetIndexNamesAsync("app", "employees", "*_Unique");
// Finds: IX_Employees_Email_Unique, IX_Employees_SSN_Unique, etc.
```

**Parameters:**
- `schemaName` - Schema containing the table
- `tableName` - Name of the table to search
- `nameFilter` (optional) - Wildcard pattern to filter index names (`*` = any characters, `?` = single character)
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `List<string>` - List of matching index names

### GetIndexNamesOnColumnAsync

Retrieve index names that include a specific column.

```csharp
// Get all indexes that include the 'email' column
List<string> emailIndexes = await connection.GetIndexNamesOnColumnAsync("app", "users", "email");

foreach (string indexName in emailIndexes)
{
    Console.WriteLine($"Index '{indexName}' includes column 'email'");
}

// Useful for determining if a column is indexed before creating queries
if (emailIndexes.Any())
{
    Console.WriteLine("Email column is indexed - queries filtering by email should perform well");
}
else
{
    Console.WriteLine("Email column is not indexed - consider adding an index for better performance");
}
```

**Parameters:**
- `schemaName` - Schema containing the table
- `tableName` - Name of the table to search
- `columnName` - Name of the column to find indexes for
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `List<string>` - List of index names that include the specified column

### GetIndexesAsync

Retrieve complete DmIndex models for table indexes.

```csharp
// Get all indexes with full structure information
List<DmIndex> indexes = await connection.GetIndexesAsync("app", "employees");

foreach (var index in indexes)
{
    Console.WriteLine($"Index: {index.IndexName}");
    Console.WriteLine($"  Unique: {index.IsUnique}");
    Console.WriteLine($"  Columns: {string.Join(", ", index.Columns.Select(c => $"{c.ColumnName} {c.ColumnOrder}"))}");
    Console.WriteLine($"  Type: {(index.IsUnique ? "Unique" : "Non-unique")}");
}

// Get specific indexes with pattern
List<DmIndex> performanceIndexes = await connection.GetIndexesAsync("sales", "orders", "IX_Orders_*");

// With transaction
using var transaction = connection.BeginTransaction();
List<DmIndex> indexes = await connection.GetIndexesAsync("app", "products", tx: transaction);
```

**Parameters:**
- `schemaName` - Schema containing the table
- `tableName` - Name of the table to search
- `nameFilter` (optional) - Wildcard pattern to filter index names
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `List<DmIndex>` - List of complete DmIndex models

### GetIndexAsync

Retrieve a single DmIndex model for a specific index.

```csharp
// Get specific index structure
DmIndex? index = await connection.GetIndexAsync("app", "employees", "IX_Employees_LastName");

if (index != null)
{
    Console.WriteLine($"Index '{index.IndexName}' details:");
    Console.WriteLine($"  Unique: {index.IsUnique}");
    Console.WriteLine($"  Column count: {index.Columns.Count}");
    
    foreach (var column in index.Columns)
    {
        Console.WriteLine($"    {column.ColumnName} ({column.ColumnOrder})");
    }
    
    // Check if it's a covering index (multi-column)
    if (index.Columns.Count > 1)
    {
        Console.WriteLine("  This is a composite index");
    }
}
else
{
    Console.WriteLine("Index not found");
}
```

**Parameters:**
- `schemaName` - Schema containing the table
- `tableName` - Name of the table containing the index
- `indexName` - Name of the index to retrieve
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `DmIndex?` - Complete index model, or `null` if index doesn't exist

### GetIndexesOnColumnAsync

Retrieve all indexes that include a specific column.

```csharp
// Get all indexes on the 'customer_id' column
List<DmIndex> customerIdIndexes = await connection.GetIndexesOnColumnAsync("sales", "orders", "customer_id");

Console.WriteLine($"Found {customerIdIndexes.Count} indexes on 'customer_id' column:");

foreach (var index in customerIdIndexes)
{
    Console.WriteLine($"  - {index.IndexName}");
    Console.WriteLine($"    Unique: {index.IsUnique}");
    Console.WriteLine($"    Columns: {string.Join(", ", index.Columns.Select(c => c.ColumnName))}");
    
    // Check if this is the first column in the index (most efficient for filtering)
    if (index.Columns.First().ColumnName == "customer_id")
    {
        Console.WriteLine($"    ✅ Column is first in index - optimal for WHERE clauses");
    }
    else
    {
        Console.WriteLine($"    ⚠️  Column is not first - may not be optimal for single-column filtering");
    }
}
```

**Parameters:**
- `schemaName` - Schema containing the table
- `tableName` - Name of the table to search
- `columnName` - Name of the column to find indexes for
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `List<DmIndex>` - List of DmIndex models that include the specified column

## Index Modification


## Index Deletion

### DropIndexIfExistsAsync

Remove an index permanently.

```csharp
// Drop index only if it exists
bool dropped = await connection.DropIndexIfExistsAsync("app", "employees", "IX_Employees_Temporary");

if (dropped)
{
    Console.WriteLine("Index was dropped successfully");
}
else
{
    Console.WriteLine("Index did not exist");
}

// Batch drop multiple indexes
var indexesToRemove = new[] { "temp_index_1", "backup_index", "old_performance_index" };
foreach (var indexName in indexesToRemove)
{
    bool wasDropped = await connection.DropIndexIfExistsAsync("app", "employees", indexName);
    Console.WriteLine($"Index '{indexName}': {(wasDropped ? "Dropped" : "Not found")}");
}
```

**Parameters:**
- `schemaName` - Schema containing the table
- `tableName` - Name of the table containing the index
- `indexName` - Name of the index to drop
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if index was dropped, `false` if it didn't exist

### DropIndexesOnColumnIfExistsAsync

Remove all indexes that include a specific column.

```csharp
// Drop all indexes on the 'legacy_column' before dropping the column
bool dropped = await connection.DropIndexesOnColumnIfExistsAsync("app", "employees", "legacy_column");

if (dropped)
{
    Console.WriteLine("All indexes on 'legacy_column' were dropped");
    // Now safe to drop the column
    await connection.DropColumnAsync("app", "employees", "legacy_column");
}
else
{
    Console.WriteLine("No indexes found on 'legacy_column'");
}

// Useful before major column modifications
await connection.DropIndexesOnColumnIfExistsAsync("app", "users", "email");
// Modify the email column
// Note: AlterColumnAsync does not exist in DapperMatic
// You would need to drop and recreate the column or table
// Then recreate indexes with new structure
await connection.CreateIndexIfNotExistsAsync("app", "users", newEmailIndex);
```

**Parameters:**
- `schemaName` - Schema containing the table
- `tableName` - Name of the table containing the indexes
- `columnName` - Name of the column to drop indexes for
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if any indexes were dropped, `false` if no indexes existed on the column

## Practical Examples

### Performance Index Strategy

```csharp
public async Task CreatePerformanceIndexesAsync(IDbConnection connection)
{
    using var transaction = connection.BeginTransaction();
    try
    {
        // Common query patterns and their optimal indexes
        var performanceIndexes = new[]
        {
            // Index for user login queries (WHERE email = ? AND is_active = ?)
            new DmIndex("IX_Users_Email_Active", new[]
            {
                new DmOrderedColumn("Email", DmColumnOrder.Ascending),
                new DmOrderedColumn("IsActive", DmColumnOrder.Ascending)
            }),
            
            // Index for order history queries (WHERE customer_id = ? ORDER BY order_date DESC)
            new DmIndex("IX_Orders_Customer_Date", new[]
            {
                new DmOrderedColumn("CustomerId", DmColumnOrder.Ascending),
                new DmOrderedColumn("OrderDate", DmColumnOrder.Descending) // Recent first
            }),
            
            // Index for product search (WHERE category_id = ? AND price BETWEEN ? AND ?)
            new DmIndex("IX_Products_Category_Price", new[]
            {
                new DmOrderedColumn("CategoryId", DmColumnOrder.Ascending),
                new DmOrderedColumn("Price", DmColumnOrder.Ascending)
            }),
            
            // Covering index for order summary queries
            new DmIndex("IX_OrderItems_Order_Covering", new[]
            {
                new DmOrderedColumn("OrderId", DmColumnOrder.Ascending),
                new DmOrderedColumn("ProductId", DmColumnOrder.Ascending),
                new DmOrderedColumn("Quantity", DmColumnOrder.Ascending),
                new DmOrderedColumn("UnitPrice", DmColumnOrder.Ascending)
            })
        };
        
        foreach (var index in performanceIndexes)
        {
            var tableName = ExtractTableNameFromIndex(index.IndexName);
            bool created = await connection.CreateIndexIfNotExistsAsync(
                "app", tableName, index, tx: transaction);
                
            Console.WriteLine($"Performance index '{index.IndexName}': {(created ? "Created" : "Already exists")}");
        }
        
        transaction.Commit();
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
}

private string ExtractTableNameFromIndex(string indexName)
{
    // Extract table name from index naming convention IX_TableName_Columns
    var parts = indexName.Split('_');
    return parts.Length > 1 ? parts[1] : "unknown";
}
```

### Index Analysis and Optimization

```csharp
public async Task AnalyzeIndexUsageAsync(IDbConnection connection, string schema, string tableName)
{
    var indexes = await connection.GetIndexesAsync(schema, tableName);
    
    Console.WriteLine($"Index analysis for {schema}.{tableName}:");
    Console.WriteLine($"Total indexes: {indexes.Count}");
    
    // Categorize indexes
    var uniqueIndexes = indexes.Where(i => i.IsUnique).ToList();
    var compositeIndexes = indexes.Where(i => i.Columns.Count > 1).ToList();
    var singleColumnIndexes = indexes.Where(i => i.Columns.Count == 1).ToList();
    
    Console.WriteLine($"Unique indexes: {uniqueIndexes.Count}");
    Console.WriteLine($"Composite indexes: {compositeIndexes.Count}");
    Console.WriteLine($"Single-column indexes: {singleColumnIndexes.Count}");
    
    // Analyze potential issues
    var issues = new List<string>();
    
    // Check for redundant indexes
    var columnCombinations = indexes
        .GroupBy(i => string.Join(",", i.Columns.Select(c => c.ColumnName)))
        .Where(g => g.Count() > 1)
        .ToList();
        
    if (columnCombinations.Any())
    {
        issues.Add($"Found {columnCombinations.Count} potential redundant index groups");
        foreach (var group in columnCombinations)
        {
            var indexNames = string.Join(", ", group.Select(i => i.IndexName));
            issues.Add($"  Redundant: {indexNames} (same columns: {group.Key})");
        }
    }
    
    // Check for overly wide indexes
    var wideIndexes = indexes.Where(i => i.Columns.Count > 5).ToList();
    if (wideIndexes.Any())
    {
        issues.Add($"Found {wideIndexes.Count} potentially over-wide indexes (>5 columns)");
        foreach (var index in wideIndexes)
        {
            issues.Add($"  Wide index: {index.IndexName} ({index.Columns.Count} columns)");
        }
    }
    
    if (issues.Any())
    {
        Console.WriteLine("Potential optimization opportunities:");
        foreach (var issue in issues)
        {
            Console.WriteLine($"  - {issue}");
        }
    }
    else
    {
        Console.WriteLine("✅ No obvious indexing issues detected");
    }
    
    // Show column coverage
    var allColumns = await connection.GetColumnNamesAsync(schema, tableName);
    var indexedColumns = indexes.SelectMany(i => i.Columns.Select(c => c.ColumnName)).Distinct().ToList();
    var unindexedColumns = allColumns.Except(indexedColumns).ToList();
    
    Console.WriteLine($"Column coverage: {indexedColumns.Count}/{allColumns.Count} columns have indexes");
    
    if (unindexedColumns.Any() && unindexedColumns.Count <= 10)
    {
        Console.WriteLine($"Unindexed columns: {string.Join(", ", unindexedColumns)}");
    }
}
```

### Index Maintenance Operations

```csharp
public async Task OptimizeTableIndexesAsync(IDbConnection connection, string schema, string tableName)
{
    Console.WriteLine($"Optimizing indexes for {schema}.{tableName}");
    
    using var transaction = connection.BeginTransaction();
    try
    {
        // Get current indexes
        var currentIndexes = await connection.GetIndexesAsync(schema, tableName, tx: transaction);
        
        // Find and remove duplicate indexes
        await RemoveDuplicateIndexesAsync(connection, schema, tableName, currentIndexes, transaction);
        
        // Create missing foreign key indexes
        await CreateForeignKeyIndexesAsync(connection, schema, tableName, transaction);
        
        // Create indexes for common query patterns
        await CreateQueryOptimizationIndexesAsync(connection, schema, tableName, transaction);
        
        transaction.Commit();
        Console.WriteLine("Index optimization completed successfully");
    }
    catch (Exception ex)
    {
        transaction.Rollback();
        Console.WriteLine($"Index optimization failed: {ex.Message}");
        throw;
    }
}

private async Task RemoveDuplicateIndexesAsync(IDbConnection connection, string schema, string tableName, 
    List<DmIndex> indexes, IDbTransaction transaction)
{
    var duplicateGroups = indexes
        .GroupBy(i => string.Join(",", i.Columns.Select(c => $"{c.ColumnName}:{c.ColumnOrder}")))
        .Where(g => g.Count() > 1)
        .ToList();
    
    foreach (var group in duplicateGroups)
    {
        var indexesToKeep = group.OrderBy(i => i.IndexName.Length).Take(1); // Keep shortest name
        var indexesToRemove = group.Except(indexesToKeep);
        
        foreach (var indexToRemove in indexesToRemove)
        {
            await connection.DropIndexIfExistsAsync(schema, tableName, indexToRemove.IndexName, tx: transaction);
            Console.WriteLine($"  Removed duplicate index: {indexToRemove.IndexName}");
        }
    }
}

private async Task CreateForeignKeyIndexesAsync(IDbConnection connection, string schema, string tableName, 
    IDbTransaction transaction)
{
    // Get table structure to find foreign key columns
    var table = await connection.GetTableAsync(schema, tableName, tx: transaction);
    if (table == null) return;
    
    foreach (var fk in table.ForeignKeyConstraints)
    {
        foreach (var columnName in fk.KeyColumnNames)
        {
            // Check if column already has an index
            bool hasIndex = await connection.DoesIndexExistOnColumnAsync(
                schema, tableName, columnName, tx: transaction);
                
            if (!hasIndex)
            {
                var indexName = $"IX_{tableName}_{columnName}_FK";
                bool created = await connection.CreateIndexIfNotExistsAsync(
                    schema, tableName, indexName,
                    new[] { new DmOrderedColumn(columnName, DmColumnOrder.Ascending) },
                    isUnique: false,
                    tx: transaction
                );
                
                if (created)
                {
                    Console.WriteLine($"  Created foreign key index: {indexName}");
                }
            }
        }
    }
}

private async Task CreateQueryOptimizationIndexesAsync(IDbConnection connection, string schema, string tableName,
    IDbTransaction transaction)
{
    // Create indexes based on common patterns and table structure
    var columns = await connection.GetColumnsAsync(schema, tableName, tx: transaction);
    
    // Index date/datetime columns for range queries
    var dateColumns = columns.Where(c => 
        c.DataType == typeof(DateTime) || 
        c.DataType == typeof(DateOnly) ||
        c.ColumnName.ToLower().Contains("date") ||
        c.ColumnName.ToLower().Contains("time")
    ).ToList();
    
    foreach (var dateColumn in dateColumns)
    {
        bool hasIndex = await connection.DoesIndexExistOnColumnAsync(
            schema, tableName, dateColumn.ColumnName, tx: transaction);
            
        if (!hasIndex)
        {
            var indexName = $"IX_{tableName}_{dateColumn.ColumnName}";
            bool created = await connection.CreateIndexIfNotExistsAsync(
                schema, tableName, indexName,
                new[] { new DmOrderedColumn(dateColumn.ColumnName, DmColumnOrder.Descending) }, // Recent first
                isUnique: false,
                tx: transaction
            );
            
            if (created)
            {
                Console.WriteLine($"  Created date optimization index: {indexName}");
            }
        }
    }
    
    // Index status/flag columns for filtering
    var statusColumns = columns.Where(c =>
        c.DataType == typeof(bool) ||
        c.ColumnName.ToLower().Contains("status") ||
        c.ColumnName.ToLower().Contains("active") ||
        c.ColumnName.ToLower().Contains("enabled") ||
        c.ColumnName.ToLower().Contains("flag")
    ).ToList();
    
    foreach (var statusColumn in statusColumns.Take(3)) // Limit to avoid over-indexing
    {
        bool hasIndex = await connection.DoesIndexExistOnColumnAsync(
            schema, tableName, statusColumn.ColumnName, tx: transaction);
            
        if (!hasIndex)
        {
            var indexName = $"IX_{tableName}_{statusColumn.ColumnName}";
            bool created = await connection.CreateIndexIfNotExistsAsync(
                schema, tableName, indexName,
                new[] { new DmOrderedColumn(statusColumn.ColumnName, DmColumnOrder.Ascending) },
                isUnique: false,
                tx: transaction
            );
            
            if (created)
            {
                Console.WriteLine($"  Created status optimization index: {indexName}");
            }
        }
    }
}
```

### Bulk Index Operations

```csharp
public async Task CreateStandardIndexSetAsync(IDbConnection connection, string schema)
{
    // Define standard indexes that should exist on most tables
    var standardIndexPatterns = new Dictionary<string, Func<string, List<DmIndex>>>
    {
        ["users"] = tableName => new List<DmIndex>
        {
            new("IX_Users_Email", new[] { new DmOrderedColumn("Email", DmColumnOrder.Ascending) }) { IsUnique = true },
            new("IX_Users_LastLoginDate", new[] { new DmOrderedColumn("LastLoginDate", DmColumnOrder.Descending) }),
            new("IX_Users_IsActive", new[] { new DmOrderedColumn("IsActive", DmColumnOrder.Ascending) })
        },
        
        ["orders"] = tableName => new List<DmIndex>
        {
            new("IX_Orders_Customer_Date", new[]
            {
                new DmOrderedColumn("CustomerId", DmColumnOrder.Ascending),
                new DmOrderedColumn("OrderDate", DmColumnOrder.Descending)
            }),
            new("IX_Orders_Status", new[] { new DmOrderedColumn("OrderStatus", DmColumnOrder.Ascending) }),
            new("IX_Orders_OrderDate", new[] { new DmOrderedColumn("OrderDate", DmColumnOrder.Descending) })
        },
        
        ["products"] = tableName => new List<DmIndex>
        {
            new("IX_Products_Category_Name", new[]
            {
                new DmOrderedColumn("CategoryId", DmColumnOrder.Ascending),
                new DmOrderedColumn("ProductName", DmColumnOrder.Ascending)
            }),
            new("IX_Products_SKU", new[] { new DmOrderedColumn("SKU", DmColumnOrder.Ascending) }) { IsUnique = true },
            new("IX_Products_Price", new[] { new DmOrderedColumn("Price", DmColumnOrder.Ascending) })
        }
    };
    
    // Get all tables in schema
    var tables = await connection.GetTableNamesAsync(schema);
    
    foreach (var tableName in tables)
    {
        // Check if we have standard indexes for this table type
        var tablePattern = standardIndexPatterns.Keys.FirstOrDefault(pattern => 
            tableName.ToLower().Contains(pattern) || tableName.ToLower().EndsWith(pattern));
            
        if (tablePattern != null)
        {
            var standardIndexes = standardIndexPatterns[tablePattern](tableName);
            
            Console.WriteLine($"Creating standard indexes for {tableName}:");
            
            foreach (var index in standardIndexes)
            {
                // Check if all columns exist before creating index
                bool allColumnsExist = true;
                foreach (var column in index.Columns)
                {
                    bool exists = await connection.DoesColumnExistAsync(schema, tableName, column.ColumnName);
                    if (!exists)
                    {
                        allColumnsExist = false;
                        break;
                    }
                }
                
                if (allColumnsExist)
                {
                    bool created = await connection.CreateIndexIfNotExistsAsync(schema, tableName, index);
                    Console.WriteLine($"  {index.IndexName}: {(created ? "Created" : "Already exists")}");
                }
                else
                {
                    Console.WriteLine($"  {index.IndexName}: Skipped (missing columns)");
                }
            }
        }
    }
}
```

Index methods provide essential performance optimization capabilities for your database, enabling you to create efficient data access patterns and significantly improve query performance across all supported database providers.