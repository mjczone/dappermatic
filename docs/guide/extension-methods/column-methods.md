# Column Methods

Column methods provide comprehensive functionality for managing table columns across all supported database providers. These methods handle column creation, modification, introspection, and deletion operations.

## Quick Navigation

- [Column Existence Checking](#column-existence-checking)
  - [DoesColumnExistAsync](#doescolumnexistasync) - Check if column exists in table
- [Column Creation](#column-creation)
  - [CreateColumnIfNotExistsAsync (DmColumn)](#createcolumnifnotexistsasync-dmcolumn) - Add column from DmColumn model
  - [CreateColumnIfNotExistsAsync (Parameters)](#createcolumnifnotexistsasync-individual-parameters) - Create column with individual parameters
- [Column Discovery](#column-discovery)
  - [GetColumnNamesAsync](#getcolumnnamesasync) - Get list of column names with filtering
  - [GetColumnsAsync](#getcolumnsasync) - Get complete column models with structure
  - [GetColumnAsync](#getcolumnasync) - Get single column model by name
- [Column Modification](#column-modification)
  - [RenameColumnIfExistsAsync](#renamecolumnifexistsasync) - Rename existing column
- [Column Deletion](#column-deletion)
  - [DropColumnIfExistsAsync](#dropcolumnifexistsasync) - Drop column permanently

## Column Existence Checking

### DoesColumnExistAsync

Check if a column exists in a specific table.

```csharp
// Check if column exists
bool exists = await connection.DoesColumnExistAsync("app", "app_employees", "title");

if (exists)
{
    Console.WriteLine("Column 'title' already exists");
}
else
{
    Console.WriteLine("Column 'title' does not exist");
    await connection.CreateColumnIfNotExistsAsync(titleColumn);
}


// With transaction and cancellation
using var transaction = connection.BeginTransaction();
bool exists = await connection.DoesColumnExistAsync(
    "app", 
    "app_employees", 
    "title",
    tx: transaction,
    cancellationToken: cancellationToken
);
```

**Parameters:**
- `schemaName` - Schema containing the table
- `tableName` - Name of the table containing the column
- `columnName` - Name of the column to check
- `tx` (optional) - Database transaction
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if column exists, `false` otherwise

## Column Creation

### CreateColumnIfNotExistsAsync (DmColumn)

Add a column only if it doesn't already exist using a DmColumn model.

```csharp
// Create column if it doesn't exist
bool created = await connection.CreateColumnIfNotExistsAsync(column);

if (created)
{
    Console.WriteLine("Column 'Title' was created");
}
else
{
    Console.WriteLine("Column 'Title' already existed");
}
```

**Parameters:**
- `column` - DmColumn model defining the column structure (includes SchemaName and TableName)
- `tx` (optional) - Database transaction
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if column was created, `false` if it already existed

### CreateColumnIfNotExistsAsync (Individual Parameters)

Create a column using individual parameters for maximum flexibility and convenience.

```csharp
// Simple column with basic parameters
bool created = await connection.CreateColumnIfNotExistsAsync(
    schemaName: "app",
    tableName: "app_employees", 
    columnName: "manager_id",
    dotnetType: typeof(Guid),
    isNullable: false
);

// Advanced column with all options
bool created = await connection.CreateColumnIfNotExistsAsync(
    schemaName: "app",
    tableName: "app_employees",
    columnName: "manager_id",
    dotnetType: typeof(Guid),
    providerDataType: null, // Let DapperMatic determine optimal SQL type
    length: null,
    precision: null,
    scale: null,
    checkExpression: null,
    defaultExpression: "NEWID()", // SQL Server default
    isNullable: false,
    isPrimaryKey: false,
    isAutoIncrement: false,
    isUnique: false,
    isIndexed: true, // Create index on this column
    isForeignKey: true,
    referencedTableName: "app_managers",
    referencedColumnName: "id",
    onDelete: DmForeignKeyAction.Cascade,
    onUpdate: DmForeignKeyAction.NoAction,
    tx: transaction,
    cancellationToken: cancellationToken
);

// Decimal column with precision and scale
bool salaryCreated = await connection.CreateColumnIfNotExistsAsync(
    "hr", 
    "employees", 
    "salary",
    typeof(decimal),
    precision: 10,
    scale: 2,
    isNullable: true,
    defaultExpression: "0.00"
);

// String column with length constraint
bool emailCreated = await connection.CreateColumnIfNotExistsAsync(
    "app", 
    "users", 
    "email",
    typeof(string),
    length: 255,
    isNullable: false,
    isUnique: true,
    checkExpression: "email LIKE '%@%'" // Basic email validation
);
```

**Parameters:**
- `schemaName` - Schema containing the table
- `tableName` - Name of the table to add column to
- `columnName` - Name of the column to create
- `dotnetType` - .NET type for the column
- `providerDataType` (optional) - Provider-specific SQL type (auto-detected if null)
- `length` (optional) - Maximum length for string/binary types
- `precision` (optional) - Precision for decimal types
- `scale` (optional) - Scale for decimal types
- `checkExpression` (optional) - Check constraint expression
- `defaultExpression` (optional) - Default value expression
- `isNullable` (optional) - Whether column allows NULL values (default: true)
- `isPrimaryKey` (optional) - Whether column is part of primary key (default: false)
- `isAutoIncrement` (optional) - Whether column auto-increments (default: false)
- `isUnique` (optional) - Whether column has unique constraint (default: false)
- `isUnicode` (optional) - Whether column supports unicode characters (default: false)
- `isIndexed` (optional) - Whether to create index on column (default: false)
- `isForeignKey` (optional) - Whether column is a foreign key (default: false)
- `referencedTableName` (optional) - Referenced table for foreign key
- `referencedColumnName` (optional) - Referenced column for foreign key
- `onDelete` (optional) - Foreign key delete action
- `onUpdate` (optional) - Foreign key update action
- `tx` (optional) - Database transaction
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if column was created, `false` if it already existed

## Column Discovery

### GetColumnNamesAsync

Retrieve a list of column names from a table, with optional filtering.

```csharp
// Get all column names
List<string> allColumns = await connection.GetColumnNamesAsync("app", "app_employees");
foreach (string columnName in allColumns)
{
    Console.WriteLine($"Found column: {columnName}");
}

// Get column names with wildcard filter
List<string> titleColumns = await connection.GetColumnNamesAsync("app", "app_employees", columnNameFilter: "*title*");
// Finds: title, job_title, title_code, etc.

// Get columns with pattern matching
List<string> idColumns = await connection.GetColumnNamesAsync("app", "app_employees", columnNameFilter: "*_id");
// Finds: employee_id, manager_id, department_id, etc.
```

**Parameters:**
- `schemaName` - Schema containing the table
- `tableName` - Name of the table to search
- `columnNameFilter` (optional) - Wildcard pattern to filter column names (`*` = any characters, `?` = single character)
- `tx` (optional) - Database transaction
- `cancellationToken` (optional) - Cancellation token

**Returns:** `List<string>` - List of matching column names

### GetColumnsAsync

Retrieve complete DmColumn models for table columns.

```csharp
// Get all columns with full structure information
List<DmColumn> columns = await connection.GetColumnsAsync("app", "app_employees");

foreach (var column in columns)
{
    Console.WriteLine($"Column: {column.ColumnName}");
    Console.WriteLine($"  Type: {column.DataType.Name}");
    Console.WriteLine($"  Nullable: {column.IsNullable}");
    Console.WriteLine($"  Max Length: {column.MaxLength?.ToString() ?? "N/A"}");
    Console.WriteLine($"  Auto Increment: {column.IsAutoIncrement}");
    Console.WriteLine($"  Default: {column.DefaultExpression ?? "None"}");
}

// Get specific columns with pattern
List<DmColumn> auditColumns = await connection.GetColumnsAsync("app", "app_employees", columnNameFilter: "*_date");

// With transaction
using var transaction = connection.BeginTransaction();
List<DmColumn> columns = await connection.GetColumnsAsync("app", "app_employees", tx: transaction);
```

**Parameters:**
- `schemaName` - Schema containing the table
- `tableName` - Name of the table to search
- `columnNameFilter` (optional) - Wildcard pattern to filter column names
- `tx` (optional) - Database transaction
- `cancellationToken` (optional) - Cancellation token

**Returns:** `List<DmColumn>` - List of complete DmColumn models

### GetColumnAsync

Retrieve a single DmColumn model for a specific column.

```csharp
// Get specific column structure
DmColumn? column = await connection.GetColumnAsync("app", "app_employees", "email");

if (column != null)
{
    Console.WriteLine($"Column '{column.ColumnName}' details:");
    Console.WriteLine($"  Type: {column.DataType.Name}");
    Console.WriteLine($"  Max Length: {column.MaxLength}");
    Console.WriteLine($"  Nullable: {column.IsNullable}");
    Console.WriteLine($"  Unique: {column.IsUnique}");
    
    if (!string.IsNullOrEmpty(column.DefaultExpression))
    {
        Console.WriteLine($"  Default: {column.DefaultExpression}");
    }
    
    if (!string.IsNullOrEmpty(column.CheckExpression))
    {
        Console.WriteLine($"  Check: {column.CheckExpression}");
    }
}
else
{
    Console.WriteLine("Column not found");
}
```

**Parameters:**
- `schemaName` - Schema containing the table
- `tableName` - Name of the table containing the column
- `columnName` - Name of the column to retrieve
- `tx` (optional) - Database transaction
- `cancellationToken` (optional) - Cancellation token

**Returns:** `DmColumn?` - Complete column model, or `null` if column doesn't exist

## Column Modification

### RenameColumnIfExistsAsync

Rename an existing column.

```csharp
// Rename column only if it exists
bool renamed = await connection.RenameColumnIfExistsAsync("app", "app_employees", "title", "job_title");

if (renamed)
{
    Console.WriteLine("Column was renamed successfully");
}
else
{
    Console.WriteLine("Column did not exist");
}

// With transaction
using var transaction = connection.BeginTransaction();
bool renamed = await connection.RenameColumnIfExistsAsync(
    "app", 
    "app_employees", 
    "old_column_name", 
    "new_column_name", 
    tx: transaction
);
```

**Parameters:**
- `schemaName` - Schema containing the table
- `tableName` - Name of the table containing the column
- `columnName` - Current name of the column
- `newColumnName` - New name for the column
- `tx` (optional) - Database transaction
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if column was renamed, `false` if it didn't exist


## Column Deletion

### DropColumnIfExistsAsync

Remove a column from a table permanently.

```csharp
// Drop column only if it exists
bool dropped = await connection.DropColumnIfExistsAsync("app", "app_employees", "old_column");

if (dropped)
{
    Console.WriteLine("Column was dropped successfully");
}
else
{
    Console.WriteLine("Column did not exist");
}

// Batch drop multiple columns
var columnsToRemove = new[] { "temp_field", "legacy_status", "unused_flag" };
foreach (var columnName in columnsToRemove)
{
    bool wasDropped = await connection.DropColumnIfExistsAsync("app", "app_employees", columnName);
    Console.WriteLine($"Column '{columnName}': {(wasDropped ? "Dropped" : "Not found")}");
}
```

**Parameters:**
- `schemaName` - Schema containing the table
- `tableName` - Name of the table containing the column
- `columnName` - Name of the column to drop
- `tx` (optional) - Database transaction
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if column was dropped, `false` if it didn't exist

## Practical Examples

### Column Migration Pattern

```csharp
public async Task MigrateColumnDataTypeAsync(IDbConnection connection, 
    string schema, string tableName, string columnName, Type newType)
{
    using var transaction = connection.BeginTransaction();
    try
    {
        // Get current column structure
        var currentColumn = await connection.GetColumnAsync(schema, tableName, columnName, tx: transaction);
        if (currentColumn == null)
        {
            throw new InvalidOperationException($"Column {columnName} does not exist");
        }
        
        // Create temporary column with new type
        var tempColumnName = $"{columnName}_temp_{DateTime.UtcNow:yyyyMMddHHmmss}";
        var tempColumn = new DmColumn(tempColumnName, newType)
        {
            SchemaName = schema,
            TableName = tableName,
            IsNullable = true // Allow nulls during migration
        };
        
        await connection.CreateColumnIfNotExistsAsync(tempColumn, tx: transaction);
        
        // Copy and convert data (implementation depends on data types)
        await CopyAndConvertColumnDataAsync(connection, schema, tableName, 
            columnName, tempColumnName, currentColumn.DataType, newType, transaction);
        
        // Drop old column
        await connection.DropColumnIfExistsAsync(schema, tableName, columnName, tx: transaction);
        
        // Rename temp column to original name
        await connection.RenameColumnIfExistsAsync(schema, tableName, tempColumnName, columnName, tx: transaction);
        
        // Update column nullability if needed
        // Note: AlterColumnAsync does not exist in DapperMatic
        // You would need to drop and recreate the column or table to change nullability
        
        transaction.Commit();
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
}
```

### Bulk Column Operations

```csharp
public async Task AddAuditColumnsAsync(IDbConnection connection, string schema, string tableName)
{
    var auditColumns = new[]
    {
        new DmColumn("CreatedAt", typeof(DateTime))
        {
            SchemaName = schema,
            TableName = tableName,
            IsNullable = false,
            DefaultExpression = "GETUTCDATE()"
        },
        new DmColumn("CreatedBy", typeof(string))
        {
            SchemaName = schema,
            TableName = tableName,
            MaxLength = 100,
            IsNullable = false,
            DefaultExpression = "SYSTEM_USER"
        },
        new DmColumn("ModifiedAt", typeof(DateTime))
        {
            SchemaName = schema,
            TableName = tableName,
            IsNullable = true
        },
        new DmColumn("ModifiedBy", typeof(string))
        {
            SchemaName = schema,
            TableName = tableName,
            MaxLength = 100,
            IsNullable = true
        }
    };
    
    using var transaction = connection.BeginTransaction();
    try
    {
        foreach (var column in auditColumns)
        {
            bool created = await connection.CreateColumnIfNotExistsAsync(
                column, tx: transaction);
                
            Console.WriteLine($"Audit column '{column.ColumnName}': {(created ? "Added" : "Already exists")}");
        }
        
        transaction.Commit();
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
}

// Clean up temporary columns
public async Task CleanupTempColumnsAsync(IDbConnection connection, string schema, string tableName)
{
    var allColumns = await connection.GetColumnNamesAsync(schema, tableName);
    var tempColumns = allColumns.Where(c => 
        c.Contains("temp_") || 
        c.StartsWith("tmp_") || 
        c.EndsWith("_backup")).ToList();
    
    Console.WriteLine($"Found {tempColumns.Count} temporary columns to clean up");
    
    foreach (var columnName in tempColumns)
    {
        bool dropped = await connection.DropColumnIfExistsAsync(schema, tableName, columnName);
        if (dropped)
        {
            Console.WriteLine($"Cleaned up column: {columnName}");
        }
    }
}
```

### Column Validation and Analysis

```csharp
public async Task AnalyzeColumnUsageAsync(IDbConnection connection, string schema, string tableName)
{
    var columns = await connection.GetColumnsAsync(schema, tableName);
    
    Console.WriteLine($"Column analysis for {schema}.{tableName}:");
    Console.WriteLine($"Total columns: {columns.Count}");
    
    var nullableColumns = columns.Where(c => c.IsNullable).ToList();
    var uniqueColumns = columns.Where(c => c.IsUnique).ToList();
    var autoIncrementColumns = columns.Where(c => c.IsAutoIncrement).ToList();
    var columnsWithDefaults = columns.Where(c => !string.IsNullOrEmpty(c.DefaultExpression)).ToList();
    
    Console.WriteLine($"Nullable columns: {nullableColumns.Count}");
    Console.WriteLine($"Unique columns: {uniqueColumns.Count}");
    Console.WriteLine($"Auto-increment columns: {autoIncrementColumns.Count}");
    Console.WriteLine($"Columns with defaults: {columnsWithDefaults.Count}");
    
    // Group by data type
    var typeGroups = columns.GroupBy(c => c.DataType.Name).OrderBy(g => g.Key);
    Console.WriteLine("\nColumns by data type:");
    foreach (var group in typeGroups)
    {
        Console.WriteLine($"  {group.Key}: {group.Count()} columns");
        foreach (var column in group.Take(3))
        {
            Console.WriteLine($"    - {column.ColumnName}");
        }
        if (group.Count() > 3)
        {
            Console.WriteLine($"    ... and {group.Count() - 3} more");
        }
    }
}

// Validate column constraints
public async Task ValidateColumnConstraintsAsync(IDbConnection connection, 
    string schema, string tableName)
{
    var columns = await connection.GetColumnsAsync(schema, tableName);
    var issues = new List<string>();
    
    foreach (var column in columns)
    {
        // Check for potential issues
        if (column.DataType == typeof(string) && column.MaxLength == null)
        {
            issues.Add($"Column '{column.ColumnName}' has no length limit");
        }
        
        if (column.DataType == typeof(decimal) && (column.Precision == null || column.Scale == null))
        {
            issues.Add($"Column '{column.ColumnName}' has no precision/scale specified");
        }
        
        if (column.ColumnName.Contains(" ") || column.ColumnName.Contains("-"))
        {
            issues.Add($"Column '{column.ColumnName}' contains spaces or hyphens");
        }
        
        if (column.IsNullable && column.IsAutoIncrement)
        {
            issues.Add($"Column '{column.ColumnName}' is auto-increment but nullable");
        }
    }
    
    if (issues.Any())
    {
        Console.WriteLine($"Found {issues.Count} potential issues:");
        foreach (var issue in issues)
        {
            Console.WriteLine($"  - {issue}");
        }
    }
    else
    {
        Console.WriteLine("No column constraint issues found");
    }
}
```

### Dynamic Column Creation

```csharp
public async Task CreateColumnsFromMetadataAsync(IDbConnection connection, 
    string schema, string tableName, Dictionary<string, Type> columnMetadata)
{
    using var transaction = connection.BeginTransaction();
    try
    {
        foreach (var (columnName, dataType) in columnMetadata)
        {
            // Determine optimal column properties based on type
            var column = CreateOptimalColumn(columnName, dataType, schema, tableName);

            bool created = await connection.CreateColumnIfNotExistsAsync(
                column, tx: transaction);
                
            Console.WriteLine($"Column '{columnName}': {(created ? "Created" : "Already exists")}");
        }
        
        transaction.Commit();
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
}

private DmColumn CreateOptimalColumn(string columnName, Type dataType, string schema, string tableName)
{
    var column = new DmColumn(columnName, dataType)
    {
        SchemaName = schema,
        TableName = tableName
    };
    
    // Apply sensible defaults based on naming conventions and types
    if (columnName.ToLower().EndsWith("id"))
    {
        column.IsNullable = false;
        if (dataType == typeof(int))
        {
            column.IsAutoIncrement = columnName.ToLower() == "id";
        }
    }
    
    if (columnName.ToLower().Contains("email"))
    {
        column.MaxLength = 255;
        column.IsNullable = false;
    }
    
    if (columnName.ToLower().Contains("name"))
    {
        column.MaxLength = dataType == typeof(string) ? 100 : null;
        column.IsNullable = false;
    }
    
    if (columnName.ToLower().EndsWith("date") || columnName.ToLower().EndsWith("time"))
    {
        column.IsNullable = true;
    }
    
    if (dataType == typeof(decimal))
    {
        column.Precision = 18;
        column.Scale = 2;
    }
    
    return column;
}
```

Column methods provide essential functionality for evolving your database schema over time, supporting everything from simple column additions to complex data type migrations across all supported database providers.