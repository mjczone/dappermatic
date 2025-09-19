# General Methods

General methods provide core utility functionality for database connections, including version detection, type mapping, and connection testing.

## Quick Navigation

- [Connection Testing](#connection-testing)
  - [TestConnectionAsync](#testconnectionasync) - Test database connection health
- [Database Version Information](#database-version-information)
  - [GetDatabaseVersionAsync](#getdatabaseversionasync) - Get database version information
- [Type Mapping Utilities](#type-mapping-utilities)
  - [GetDotnetTypeFromSqlType](#getdotnettypefromsqltype) - Convert SQL types to .NET types
  - [GetSqlTypeFromDotnetType](#getsqltypefromdotnettype) - Convert .NET types to SQL types
- [Alternative Type Mapping](#alternative-type-mapping-method)
  - [GetDotnetTypeFromSqlType (Tuple)](#getdotnettypefromsqltype-tuple-return) - Get type info as tuple
- [Provider Detection](#provider-detection)
  - [GetDbProviderType](#getdbprovidertype) - Identify database provider type

## Connection Testing

### TestConnectionAsync

Test if a database connection is working properly.

```csharp
using var connection = new SqlConnection(connectionString);

// Test the connection
bool isConnected = await connection.TestConnectionAsync();

if (isConnected)
{
    Console.WriteLine("Connection successful!");
    // Proceed with DDL operations
}
else
{
    Console.WriteLine("Connection failed!");
}

// With cancellation token
bool isConnected = await connection.TestConnectionAsync(cancellationToken: cancellationToken);
```

**Parameters:**
- `cancellationToken` (optional) - Cancellation token for the operation

**Returns:** `bool` - `true` if connection is successful, `false` otherwise

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
DbProviderDotnetTypeDescriptor descriptor = connection.GetDotnetTypeFromSqlType("nvarchar(255)");

Console.WriteLine($"Type: {descriptor.DotnetType}");           // typeof(string)
Console.WriteLine($"Length: {descriptor.Length}");             // 255
Console.WriteLine($"Precision: {descriptor.Precision}");       // null
Console.WriteLine($"Scale: {descriptor.Scale}");               // null
Console.WriteLine($"Unicode: {descriptor.Unicode}");           // True
Console.WriteLine($"AutoIncrement: {descriptor.AutoIncrement}"); // False

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
// varcharDescriptor.Unicode => False
```

**Parameters:**
- `sqlType` - Provider-specific SQL type string (e.g., "nvarchar(255)", "decimal(10,2)")

**Returns:** `DbProviderDotnetTypeDescriptor` - Detailed type information

### GetSqlTypeFromDotnetType

Convert .NET type information to provider-specific SQL types.

```csharp
// Create type descriptor
var stringDescriptor = new DbProviderDotnetTypeDescriptor(
    dotnetType: typeof(string),
    length: 100,
    unicode: true
);

// Get SQL type for current provider
string sqlType = connection.GetSqlTypeFromDotnetType(stringDescriptor);
// SQL Server: "nvarchar(100)"
// MySQL: "varchar(100)"
// PostgreSQL: "varchar(100)"
// SQLite: "TEXT"

// Decimal example
var decimalDescriptor = new DbProviderDotnetTypeDescriptor(
    dotnetType: typeof(decimal),
    precision: 15,
    scale: 4
);
string decimalSqlType = connection.GetSqlTypeFromDotnetType(decimalDescriptor);
// Result: "decimal(15,4)" (most providers)

// Boolean example  
var boolDescriptor = new DbProviderDotnetTypeDescriptor(typeof(bool));
string boolSqlType = connection.GetSqlTypeFromDotnetType(boolDescriptor);
// SQL Server: "bit"
// MySQL: "tinyint(1)" 
// PostgreSQL: "boolean"
// SQLite: "INTEGER"
```

**Parameters:**
- `typeDescriptor` - .NET type descriptor with optional length, precision, and scale

**Returns:** `string` - Provider-specific SQL type

## Alternative Type Mapping Method

### GetDotnetTypeFromSqlType (Tuple Return)

Alternative method that returns type information as a tuple.

```csharp
// Get type information as tuple
var (dotnetType, length, precision, scale) = connection.GetDotnetTypeFromSqlType("decimal(15,4)");

Console.WriteLine($"Type: {dotnetType}");       // typeof(decimal)
Console.WriteLine($"Length: {length}");         // null
Console.WriteLine($"Precision: {precision}");   // 15
Console.WriteLine($"Scale: {scale}");           // 4

// String type example
var (stringType, stringLength, _, _) = connection.GetDotnetTypeFromSqlType("varchar(255)");
// stringType => typeof(string)
// stringLength => 255
```

**Parameters:**
- `sqlType` - Provider-specific SQL type string

**Returns:** `(Type dotnetType, int? length, int? precision, int? scale)` - Type information tuple

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
    // Test connection first
    if (!await connection.TestConnectionAsync())
    {
        throw new InvalidOperationException("Cannot connect to database");
    }
    
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
    var descriptor = new DbProviderDotnetTypeDescriptor(dotnetType, length: length);
    
    // Get provider-specific SQL type
    string sqlType = connection.GetSqlTypeFromDotnetType(descriptor);
    
    // Create column with optimal type
    var column = new DmColumn(columnName, dotnetType)
    {
        MaxLength = length,
        IsNullable = dotnetType.IsNullable()
    };
    
    await connection.AddColumnAsync("dbo", tableName, column);
    
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
        var descriptor = new DbProviderDotnetTypeDescriptor(type);
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