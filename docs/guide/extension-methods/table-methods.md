# Table Methods

Table methods provide comprehensive functionality for managing database tables across all supported providers. These methods handle table creation, introspection, modification, and deletion operations.

## Quick Navigation

- [Table Existence Checking](#table-existence-checking)
  - [DoesTableExistAsync](#doestableexistasync) - Check if table exists
- [Table Creation](#table-creation)
  - [CreateTableIfNotExistsAsync](#createtableifnotexistsasync) - Create table from DmTable model
  - [CreateTableIfNotExistsAsync (Components)](#createtableifnotexistsasync-individual-components) - Create table from individual components
- [Table Discovery](#table-discovery)
  - [GetTableNamesAsync](#gettablenamesasync-names-only) - Get list of table names with filtering
  - [GetTablesAsync](#gettablesasync-full-models) - Get complete table models with structure
  - [GetTableAsync](#gettableasync) - Get single table model by name
- [Table Modification](#table-modification)
  - [RenameTableIfExistsAsync](#renametableifexistsasync) - Rename existing table
  - [TruncateTableIfExistsAsync](#truncatetableifexistsasync) - Remove all data from table
- [Table Deletion](#table-deletion)
  - [DropTableIfExistsAsync](#droptableifexistsasync) - Drop table permanently

## Table Existence Checking

### DoesTableExistAsync

Check if a table exists in the database.

```csharp
// Check if table exists
bool exists = await connection.DoesTableExistAsync("app", "app_employees");

if (exists)
{
    Console.WriteLine("Table 'app_employees' already exists");
}
else
{
    Console.WriteLine("Table 'app_employees' does not exist");
    await connection.CreateTableIfNotExistsAsync("app", table);
}


// With transaction and cancellation
using var transaction = connection.BeginTransaction();
bool exists = await connection.DoesTableExistAsync(
    "app",
    "app_employees",
    tx: transaction,
    cancellationToken: cancellationToken
);
```

**Parameters:**

- `schemaName` - Schema containing the table
- `tableName` - Name of the table to check
- `tx` (optional) - Database transaction
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if table exists, `false` otherwise

## Table Creation

### CreateTableIfNotExistsAsync

Create a table only if it doesn't already exist using a DmTable model.

```csharp
// Create table if it doesn't exist
bool created = await connection.CreateTableIfNotExistsAsync("app", table);

if (created)
{
    Console.WriteLine("Table 'app_employees' was created");
}
else
{
    Console.WriteLine("Table 'app_employees' already existed");
}
```

**Parameters:**

- `schemaName` - Schema to create the table in
- `table` - DmTable model defining the table structure
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if table was created, `false` if it already existed

### CreateTableIfNotExistsAsync (Individual Components)

Create a table using individual components for maximum flexibility.

```csharp
// Define table structure using individual components
var columns = new[]
{
    new DmColumn("EmployeeId", typeof(int)) { IsAutoIncrement = true },
    new DmColumn("FirstName", typeof(string)) { MaxLength = 50, IsNullable = false },
    new DmColumn("LastName", typeof(string)) { MaxLength = 50, IsNullable = false },
    new DmColumn("Email", typeof(string)) { MaxLength = 255, IsNullable = false },
    new DmColumn("DepartmentId", typeof(int)),
    new DmColumn("HireDate", typeof(DateTime)) { DefaultExpression = "GETDATE()" },
    new DmColumn("IsActive", typeof(bool)) { DefaultExpression = "1" }
};

var primaryKey = new DmPrimaryKeyConstraint("PK_Employees", new[] { "EmployeeId" });

var checkConstraints = new[]
{
    new DmCheckConstraint("CK_Employees_HireDate", "HireDate >= '1900-01-01'")
};

var defaultConstraints = new[]
{
    new DmDefaultConstraint("DF_Employees_IsActive", "IsActive", "1")
};

var uniqueConstraints = new[]
{
    new DmUniqueConstraint("UK_Employees_Email", new[] { "Email" })
};

var foreignKeyConstraints = new[]
{
    new DmForeignKeyConstraint("FK_Employees_Department",
        new[] { "DepartmentId" }, "hr", "Departments", new[] { "DepartmentId" })
};

var indexes = new[]
{
    new DmIndex("IX_Employees_LastName", new[] { "LastName" })
};

// Create table with all components
bool created = await connection.CreateTableIfNotExistsAsync(
    "app",
    "app_employees",
    columns,
    primaryKey,
    checkConstraints,
    defaultConstraints,
    uniqueConstraints,
    foreignKeyConstraints,
    indexes,
    tx: transaction,
    commandTimeout: 60,
    cancellationToken: cancellationToken
);
```

**Parameters:**

- `schemaName` - Schema to create the table in
- `tableName` - Name of the table to create
- `columns` - Array of DmColumn definitions
- `primaryKey` (optional) - Primary key constraint
- `checkConstraints` (optional) - Array of check constraints
- `defaultConstraints` (optional) - Array of default constraints
- `uniqueConstraints` (optional) - Array of unique constraints
- `foreignKeyConstraints` (optional) - Array of foreign key constraints
- `indexes` (optional) - Array of indexes
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if table was created, `false` if it already existed

## Table Discovery

### GetTableNamesAsync (Names Only)

Retrieve a list of table names, with optional filtering.

```csharp
// Get all table names in schema
List<string> allTables = await connection.GetTableNamesAsync("app");
foreach (string tableName in allTables)
{
    Console.WriteLine($"Found table: {tableName}");
}

// Get table names with wildcard filter
List<string> appTables = await connection.GetTableNamesAsync("app", "app_*");
// Finds: app_employees, app_departments, app_logs, etc.

// Get tables with pattern matching
List<string> logTables = await connection.GetTableNamesAsync("app", "*log*");
// Finds: app_logs, error_logs, audit_log_entries, etc.
```

**Parameters:**

- `schemaName` - Schema to search for tables
- `nameFilter` (optional) - Wildcard pattern to filter table names (`*` = any characters, `?` = single character)
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `List<string>` - List of matching table names

### GetTablesAsync (Full Models)

Retrieve complete DmTable models for existing tables.

```csharp
// Get all tables with full structure information
List<DmTable> tables = await connection.GetTablesAsync("app");

foreach (var table in tables)
{
    Console.WriteLine($"Table: {table.TableName}");
    Console.WriteLine($"  Columns: {table.Columns.Count}");
    Console.WriteLine($"  Primary Key: {table.PrimaryKeyConstraint?.KeyName ?? "None"}");
    Console.WriteLine($"  Foreign Keys: {table.ForeignKeyConstraints.Count}");
    Console.WriteLine($"  Indexes: {table.Indexes.Count}");

    foreach (var column in table.Columns)
    {
        Console.WriteLine($"    {column.ColumnName}: {column.DataType.Name}" +
                         $"{(column.IsNullable ? "" : " NOT NULL")}");
    }
}

// Get specific tables with pattern
List<DmTable> employeeTables = await connection.GetTablesAsync("hr", "employee*");

// With transaction
using var transaction = connection.BeginTransaction();
List<DmTable> tables = await connection.GetTablesAsync("app", tx: transaction);
```

**Parameters:**

- `schemaName` - Schema to search for tables
- `nameFilter` (optional) - Wildcard pattern to filter table names
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `List<DmTable>` - List of complete DmTable models

### GetTableAsync

Retrieve a single DmTable model for a specific table.

```csharp
// Get specific table structure
DmTable? table = await connection.GetTableAsync("app", "app_employees");

if (table != null)
{
    Console.WriteLine($"Table '{table.TableName}' has {table.Columns.Count} columns");

    // Access table structure
    var primaryKey = table.PrimaryKeyConstraint;
    var foreignKeys = table.ForeignKeyConstraints;
    var indexes = table.Indexes;

    // Find specific column
    var emailColumn = table.Columns.FirstOrDefault(c => c.ColumnName == "Email");
    if (emailColumn != null)
    {
        Console.WriteLine($"Email column max length: {emailColumn.MaxLength}");
    }
}
else
{
    Console.WriteLine("Table not found");
}
```

**Parameters:**

- `schemaName` - Schema containing the table
- `tableName` - Name of the table to retrieve
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `DmTable?` - Complete table model, or `null` if table doesn't exist

## Table Modification

### RenameTableIfExistsAsync

Rename an existing table.

```csharp
// Rename table only if it exists
bool renamed = await connection.RenameTableIfExistsAsync("app", "app_employees", "app_staff");

if (renamed)
{
    Console.WriteLine("Table was renamed successfully");
}
else
{
    Console.WriteLine("Table did not exist");
}

// With transaction
using var transaction = connection.BeginTransaction();
bool renamed = await connection.RenameTableIfExistsAsync(
    "app",
    "old_table_name",
    "new_table_name",
    tx: transaction
);
```

**Parameters:**

- `schemaName` - Schema containing the table
- `currentTableName` - Current name of the table
- `newTableName` - New name for the table
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if table was renamed, `false` if it didn't exist

### TruncateTableIfExistsAsync

Remove all data from a table while preserving its structure.

```csharp
// Truncate table only if it exists
bool truncated = await connection.TruncateTableIfExistsAsync("app", "app_logs");

if (truncated)
{
    Console.WriteLine("Table was truncated successfully");
}
else
{
    Console.WriteLine("Table did not exist");
}

// Batch truncate multiple tables
var tablesToTruncate = new[] { "app_logs", "app_temp_data", "app_cache" };
foreach (var tableName in tablesToTruncate)
{
    bool wasTruncated = await connection.TruncateTableIfExistsAsync("app", tableName);
    Console.WriteLine($"Table '{tableName}': {(wasTruncated ? "Truncated" : "Not found")}");
}
```

**Parameters:**

- `schemaName` - Schema containing the table
- `tableName` - Name of the table to truncate
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if table was truncated, `false` if it didn't exist

## Table Deletion

### DropTableIfExistsAsync

Delete a table and all its data permanently.

```csharp
// Drop table only if it exists
bool dropped = await connection.DropTableIfExistsAsync("app", "old_table");

if (dropped)
{
    Console.WriteLine("Table was dropped successfully");
}
else
{
    Console.WriteLine("Table did not exist");
}

// Batch drop multiple tables (order matters for foreign key dependencies)
var tablesToDrop = new[] { "app_order_items", "app_orders", "app_customers" };
foreach (var tableName in tablesToDrop)
{
    bool wasDropped = await connection.DropTableIfExistsAsync("app", tableName);
    Console.WriteLine($"Table '{tableName}': {(wasDropped ? "Dropped" : "Not found")}");
}
```

**Parameters:**

- `schemaName` - Schema containing the table
- `tableName` - Name of the table to drop
- `tx` (optional) - Database transaction
- `commandTimeout` (optional) - Command timeout in seconds
- `cancellationToken` (optional) - Cancellation token

**Returns:** `bool` - `true` if table was dropped, `false` if it didn't exist

## Practical Examples

### Table Migration Pattern

```csharp
public async Task MigrateTableStructureAsync(IDbConnection connection, string schema, string tableName)
{
    using var transaction = connection.BeginTransaction();
    try
    {
        // Get current table structure
        var currentTable = await connection.GetTableAsync(schema, tableName, tx: transaction);
        if (currentTable == null)
        {
            throw new InvalidOperationException($"Table {schema}.{tableName} does not exist");
        }

        // Create backup table
        var backupTableName = $"{tableName}_backup_{DateTime.UtcNow:yyyyMMddHHmmss}";
        await connection.RenameTableIfExistsAsync(schema, tableName, backupTableName, tx: transaction);

        // Create new table with updated structure
        var newTable = CreateUpdatedTableStructure(currentTable);
        await connection.CreateTableIfNotExistsAsync(schema, newTable, tx: transaction);

        // Migrate data (implementation depends on changes)
        await MigrateTableDataAsync(connection, schema, backupTableName, tableName, transaction);

        // Drop backup table if migration successful
        await connection.DropTableIfExistsAsync(schema, backupTableName, tx: transaction);

        transaction.Commit();
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
}
```

### Table Dependency Analysis

```csharp
public async Task<Dictionary<string, List<string>>> AnalyzeTableDependenciesAsync(
    IDbConnection connection, string schema)
{
    var dependencies = new Dictionary<string, List<string>>();
    var tables = await connection.GetTablesAsync(schema);

    foreach (var table in tables)
    {
        var referencedTables = new List<string>();

        foreach (var fk in table.ForeignKeyConstraints)
        {
            var referencedTable = $"{fk.ReferencedSchemaName}.{fk.ReferencedTableName}";
            referencedTables.Add(referencedTable);
        }

        dependencies[table.TableName] = referencedTables;
    }

    return dependencies;
}

// Usage
var dependencies = await AnalyzeTableDependenciesAsync(connection, "app");
foreach (var (table, deps) in dependencies)
{
    Console.WriteLine($"Table '{table}' depends on:");
    foreach (var dep in deps)
    {
        Console.WriteLine($"  - {dep}");
    }
}
```

### Bulk Table Operations

```csharp
public async Task CreateTablesFromModelsAsync(IDbConnection connection,
    string schema, params DmTable[] tables)
{
    using var transaction = connection.BeginTransaction();
    try
    {
        // Create tables in dependency order
        var sortedTables = SortTablesByDependencies(tables);

        foreach (var table in sortedTables)
        {
            bool created = await connection.CreateTableIfNotExistsAsync(
                schema, table, tx: transaction);

            Console.WriteLine($"Table '{table.TableName}': {(created ? "Created" : "Already exists")}");
        }

        transaction.Commit();
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
}

// Clean up test tables
public async Task CleanupTestTablesAsync(IDbConnection connection, string schema)
{
    var allTables = await connection.GetTableNamesAsync(schema);
    var testTables = allTables.Where(t =>
        t.StartsWith("test_") ||
        t.EndsWith("_test") ||
        t.Contains("temp")).ToList();

    Console.WriteLine($"Found {testTables.Count} test tables to clean up");

    foreach (var tableName in testTables.Reverse()) // Reverse for foreign key dependencies
    {
        bool dropped = await connection.DropTableIfExistsAsync(schema, tableName);
        if (dropped)
        {
            Console.WriteLine($"Cleaned up table: {tableName}");
        }
    }
}
```

### Table Structure Comparison

```csharp
public async Task<bool> CompareTableStructuresAsync(IDbConnection connection,
    string schema, string table1, string table2)
{
    var table1Model = await connection.GetTableAsync(schema, table1);
    var table2Model = await connection.GetTableAsync(schema, table2);

    if (table1Model == null || table2Model == null)
    {
        return false;
    }

    // Compare column count
    if (table1Model.Columns.Count != table2Model.Columns.Count)
    {
        Console.WriteLine($"Column count differs: {table1Model.Columns.Count} vs {table2Model.Columns.Count}");
        return false;
    }

    // Compare each column
    for (int i = 0; i < table1Model.Columns.Count; i++)
    {
        var col1 = table1Model.Columns[i];
        var col2 = table2Model.Columns[i];

        if (!CompareColumns(col1, col2))
        {
            Console.WriteLine($"Column '{col1.ColumnName}' differs from '{col2.ColumnName}'");
            return false;
        }
    }

    // Compare constraints and indexes
    return CompareConstraints(table1Model, table2Model) &&
           CompareIndexes(table1Model, table2Model);
}
```

Table methods provide the core functionality for managing table structures in your database, supporting everything from simple table creation to complex migration scenarios across all supported database providers.
