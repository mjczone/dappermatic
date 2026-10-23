# General Methods

General methods provide core utility functionality for database connections, including version detection, type mapping, and connection testing.

## Quick Navigation

- [Connection Testing](#connection-testing)
- [Database Version Information](#database-version-information)
  - [GetDatabaseVersionAsync](#getdatabaseversionasync) - Get database version information
- [Type Mapping Utilities](#type-mapping-utilities)
  - [GetDotnetTypeFromSqlType](#getdotnettypefromsqltype) - Convert SQL types to .NET types
  - [GetSqlTypeFromDotnetType](#getsqltypefromdotnettype) - Convert .NET types to SQL types
- [Alternative Type Mapping](#alternative-type-mapping-method)
- [Provider Detection](#provider-detection)
  - [GetDbProviderType](#getdbprovidertype) - Identify database provider type

## Connection Testing

DapperMatic has no connection test of its own. Open the connection, or call `GetDatabaseVersionAsync`, which throws
when the database cannot be reached:

```csharp
using var connection = new SqlConnection(connectionString);
var version = await connection.GetDatabaseVersionAsync(); // throws if the database cannot be reached
```

## Database Version Information

### GetDatabaseVersionAsync

Retrieve the version of the connected database.

```csharp
using var connection = new SqlConnection(connectionString);

// Get database version
Version version = await connection.GetDatabaseVersionAsync();
Console.WriteLine($"Database version: {version}");

// With transaction and cancellation
using var transaction = connection.BeginTransaction();
Version version = await connection.GetDatabaseVersionAsync(
    tx: transaction,
    cancellationToken: cancellationToken
);
```

**Parameters:**
- `tx` (optional) - Database transaction
- `cancellationToken` (optional) - Cancellation token for the operation

**Returns:** `Version` - The database version information

**Examples by Provider:**
- **SQL Server**: `15.0.2000.5` (SQL Server 2019)
- **MySQL**: `8.0.33` 
- **PostgreSQL**: `15.4`
- **SQLite**: `3.46.1`

## Type Mapping Utilities

### GetDotnetTypeFromSqlType

Convert provider-specific SQL types to .NET type information.

```csharp
// Get .NET type information from SQL type
DotnetTypeDescriptor descriptor = connection.GetDotnetTypeFromSqlType("nvarchar(255)");

Console.WriteLine($"Type: {descriptor.DotnetType}");           // typeof(string)
Console.WriteLine($"Length: {descriptor.Length}");             // 255
Console.WriteLine($"Precision: {descriptor.Precision}");       // null
Console.WriteLine($"Scale: {descriptor.Scale}");               // null
Console.WriteLine($"Unicode: {descriptor.IsUnicode}");         // True
Console.WriteLine($"AutoIncrement: {descriptor.IsAutoIncrementing}"); // False

// Examples with different types
var intDescriptor = connection.GetDotnetTypeFromSqlType("int");
// intDescriptor.DotnetType => typeof(int)

var decimalDescriptor = connection.GetDotnetTypeFromSqlType("decimal(10,2)");
// decimalDescriptor.DotnetType => typeof(decimal)
// decimalDescriptor.Precision => 10
// decimalDescriptor.Scale => 2

var varcharDescriptor = connection.GetDotnetTypeFromSqlType("varchar(100)");
// varcharDescriptor.DotnetType => typeof(string)
// varcharDescriptor.Length => 100
// varcharDescriptor.IsUnicode => False
```

**Parameters:**
- `sqlType` - Provider-specific SQL type string (e.g., "nvarchar(255)", "decimal(10,2)")

**Returns:** `DotnetTypeDescriptor` - Detailed type information

### GetSqlTypeFromDotnetType

Convert .NET type information to provider-specific SQL types.

```csharp
// Create type descriptor
var stringDescriptor = new DotnetTypeDescriptor(
    dotnetType: typeof(string),
    length: 100,
    isUnicode: true
);

// Get SQL type for current provider
string sqlType = connection.GetSqlTypeFromDotnetType(stringDescriptor);
// SQL Server: "nvarchar(100)"
// MySQL: "varchar(100)"
// PostgreSQL: "varchar(100)"
// SQLite: "TEXT"

// Decimal example
var decimalDescriptor = new DotnetTypeDescriptor(
    dotnetType: typeof(decimal),
    precision: 15,
    scale: 4
);
string decimalSqlType = connection.GetSqlTypeFromDotnetType(decimalDescriptor);
// Result: "decimal(15,4)" (most providers)

// Boolean example  
var boolDescriptor = new DotnetTypeDescriptor(typeof(bool));
string boolSqlType = connection.GetSqlTypeFromDotnetType(boolDescriptor);
// SQL Server: "bit"
// MySQL: "tinyint(1)" 
// PostgreSQL: "boolean"
// SQLite: "INTEGER"
```

**Parameters:**
- `descriptor` - .NET type descriptor with optional length, precision, and scale

**Returns:** `string` - Provider-specific SQL type

## Provider Detection

### GetDbProviderType

Determine which database provider is being used.

```csharp
// Get the provider type for the connection
DbProviderType providerType = connection.GetDbProviderType();

switch (providerType)
{
    case DbProviderType.SqlServer:
        Console.WriteLine("Using SQL Server");
        break;
    case DbProviderType.MySql:
        Console.WriteLine("Using MySQL");
        break;
    case DbProviderType.PostgreSql:
        Console.WriteLine("Using PostgreSQL");
        break;
    case DbProviderType.Sqlite:
        Console.WriteLine("Using SQLite");
        break;
}

// Use provider type for conditional logic
if (providerType == DbProviderType.SqlServer)
{
    // SQL Server specific operations
    await connection.CreateSchemaIfNotExistsAsync("app");
}
```

**Returns:** `DbProviderType` - Enumeration indicating the database provider

## Practical Examples

### Connection Validation Before Operations

```csharp
public async Task<bool> InitializeDatabaseAsync(IDbConnection connection)
{
    // Throws if the database cannot be reached
    await connection.GetDatabaseVersionAsync();
    
    // Get version for compatibility checking
    var version = await connection.GetDatabaseVersionAsync();
    var provider = connection.GetDbProviderType();
    
    Console.WriteLine($"Connected to {provider} version {version}");
    
    // Check minimum version requirements
    return IsVersionSupported(provider, version);
}

private bool IsVersionSupported(DbProviderType provider, Version version)
{
    return provider switch
    {
        DbProviderType.SqlServer => version.Major >= 11, // SQL Server 2012+
        DbProviderType.MySql => version.Major >= 5,      // MySQL 5.0+
        DbProviderType.PostgreSql => version.Major >= 9, // PostgreSQL 9.0+
        DbProviderType.Sqlite => version.Major >= 3,     // SQLite 3.0+
        _ => false
    };
}
```

### Dynamic Type Mapping

```csharp
public async Task CreateColumnWithOptimalType(IDbConnection connection, string tableName, 
    string columnName, Type dotnetType, int? length = null)
{
    // Create type descriptor
    var descriptor = new DotnetTypeDescriptor(dotnetType, length: length);
    
    // Get provider-specific SQL type
    string sqlType = connection.GetSqlTypeFromDotnetType(descriptor);
    
    // Create column with optimal type
    var column = new DmColumn("dbo", tableName, columnName, dotnetType)
    {
        Length = length,
        IsNullable = dotnetType.IsNullable()
    };
    
    await connection.CreateColumnIfNotExistsAsync(column);
    
    Console.WriteLine($"Added column {columnName} as {sqlType}");
}

// Usage
await CreateColumnWithOptimalType(connection, "Users", "Bio", typeof(string), 500);
// SQL Server: Creates "Bio nvarchar(500)"
// MySQL: Creates "Bio varchar(500)"
```

### Type System Introspection

```csharp
public Dictionary<string, string> GetProviderTypeMappings(IDbConnection connection)
{
    var mappings = new Dictionary<string, string>();
    var commonTypes = new[]
    {
        typeof(int), typeof(long), typeof(string), typeof(decimal),
        typeof(DateTime), typeof(bool), typeof(Guid)
    };
    
    foreach (var type in commonTypes)
    {
        var descriptor = new DotnetTypeDescriptor(type);
        var sqlType = connection.GetSqlTypeFromDotnetType(descriptor);
        mappings[type.Name] = sqlType;
    }
    
    return mappings;
}

// Usage
var mappings = GetProviderTypeMappings(connection);
foreach (var (dotnetType, sqlType) in mappings)
{
    Console.WriteLine($"{dotnetType} -> {sqlType}");
}
```

## Error Handling

```csharp
try
{
    var version = await connection.GetDatabaseVersionAsync();
    var descriptor = connection.GetDotnetTypeFromSqlType("invalid_type");
}
catch (ArgumentException ex)
{
    // Invalid SQL type or parameter
    Console.WriteLine($"Invalid type: {ex.Message}");
}
catch (NotSupportedException ex)
{
    // Unsupported operation for this provider
    Console.WriteLine($"Unsupported: {ex.Message}");
}
catch (InvalidOperationException ex)
{
    // Connection or state issues
    Console.WriteLine($"Operation error: {ex.Message}");
}
```

General methods provide the foundation for all other DapperMatic operations, enabling robust connection management and cross-provider compatibility.