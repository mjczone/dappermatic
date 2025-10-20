# Database Providers

DapperMatic supports multiple database providers, each with their own connection types and specific features. This page covers the supported providers and their capabilities.

## Quick Navigation

- [Supported Providers](#supported-providers)
  - [SQL Server](#sql-server)
  - [MySQL / MariaDB](#mysql-mariadb)
  - [PostgreSQL](#postgresql)
  - [SQLite](#sqlite)
- [Custom Providers](#custom-providers)
- [Connection String Examples](#connection-string-examples)
- [Data Type Mapping](#data-type-mapping)

## Supported Providers

### SQL Server

**Supported Versions:** SQL Server 2017+, 2019, 2022  
**Connection Packages:** `Microsoft.Data.SqlClient` or `System.Data.SqlClient`  
**Schema Support:** Full schema support with `dbo` as default

```csharp
using Microsoft.Data.SqlClient;

var connectionString = "Server=localhost;Database=MyApp;Integrated Security=true;";
using var connection = new SqlConnection(connectionString);

// SQL Server uses schemas extensively
var table = new DmTable("dbo", "TableName", columns);
await connection.CreateTableIfNotExistsAsync(table);
```

**Key Features:**

- Full DDL support including all constraint types
- Auto-increment columns via `IDENTITY`
- Rich data type support including spatial types
- Computed columns and check constraints
- Filtered indexes and included columns

**Limitations:**

- Schema name required for most operations
- Some DDL operations require elevated permissions

### MySQL / MariaDB

**Supported Versions:** MySQL 5.7+, 8.4, 9.0; MariaDB 10.11+, 11.1  
**Connection Packages:** `MySqlConnector` or `MySql.Data`  
**Schema Support:** Database-level organization (no schemas)

```csharp
using MySqlConnector;

var connectionString = "Server=localhost;Database=myapp;Uid=user;Pwd=password;";
using var connection = new MySqlConnection(connectionString);

// MySQL doesn't use schemas - pass null or database name
var table = new DmTable(null /* or databaseName */, "TableName", columns);
await connection.CreateTableIfNotExistsAsync(table);
```

**Key Features:**

- Auto-increment columns via `AUTO_INCREMENT`
- Full-text search indexes
- Spatial data types and functions
- JSON column type support
- Partitioning support

**Limitations:**

- No schema concept (database = schema)
- Limited check constraint support (MySQL 8.0.16+)
- Some DDL operations don't support transactions

### PostgreSQL

**Supported Versions:** PostgreSQL 15+, 16+ (with optional PostGIS extension support)  
**Connection Package:** `Npgsql`  
**Schema Support:** Full schema support with `public` as default

```csharp
using Npgsql;

var connectionString = "Host=localhost;Database=myapp;Username=user;Password=password;";
using var connection = new NpgsqlConnection(connectionString);

// PostgreSQL is case-sensitive for quoted identifiers
var table = new DmTable("public", "TableName", columns);
await connection.CreateTableIfNotExistsAsync(table);
```

**Key Features:**

- Advanced data types (arrays, JSON, UUID, etc.)
- Full ACID compliance
- Excellent performance with large datasets
- Rich indexing options (GiST, GIN, SP-GiST, BRIN)
- Native array support in DapperMatic

**Limitations:**

- Case-sensitive for quoted identifiers
- Some advanced features may not be portable

### SQLite

**Supported Versions:** SQLite 3.35+  
**Connection Package:** `Microsoft.Data.Sqlite` or `System.Data.SQLite`  
**Schema Support:** Single database file (no schemas)

```csharp
using Microsoft.Data.Sqlite;

var connectionString = "Data Source=myapp.db";
using var connection = new SqliteConnection(connectionString);

// SQLite doesn't use schemas
var table = new DmTable(null /* no schema support */, "TableName", columns);
await connection.CreateTableIfNotExistsAsync(table);
```

**Key Features:**

- Zero-configuration embedded database
- Full ACID compliance
- Cross-platform compatibility
- JSON support (SQLite 3.38+)
- Excellent for development and testing

**Limitations:**

- Limited ALTER TABLE support - **DapperMatic overcomes this automatically**
- No native date/time types (stored as TEXT/INTEGER)
- Single writer at a time
- No schemas or stored procedures

**How DapperMatic Handles SQLite Limitations:**

SQLite's `ALTER TABLE` statement has significant restrictions - it cannot modify existing columns, drop columns, or change column types. DapperMatic automatically works around these limitations using a sophisticated "table recreation strategy":

1. **Automatic Detection**: When a column operation would fail in SQLite, DapperMatic detects this and switches to recreation mode
2. **Data Preservation**: Creates a temporary table with existing data before making schema changes
3. **Schema Recreation**: Drops and recreates the table with the new schema structure
4. **Intelligent Data Migration**: Copies compatible data from the temporary table to the new table
5. **Transaction Safety**: The entire process runs within a transaction for atomicity
6. **Foreign Key Handling**: Temporarily disables foreign key constraints during recreation

This means operations like `DropColumnIfExistsAsync()`, `CreateColumnIfNotExistsAsync()`, and constraint modifications work seamlessly in SQLite, just like other providers - you don't need to worry about SQLite's native limitations.

## Custom Providers

DapperMatic's extensible architecture allows you to work with custom database providers and connection wrappers. Many third-party libraries provide `IDbConnection` implementations that wrap existing providers to add functionality like profiling, caching, logging, or custom connection pooling.

### Wrapper Providers (Easiest)

These are the most common custom providers - they wrap existing database connections to add functionality while delegating core database operations to the underlying provider.

#### MiniProfiler Integration

**Use Case**: Add SQL profiling and performance monitoring to your DDL operations.

```csharp
using StackExchange.Profiling.Data;
using Microsoft.Data.SqlClient;

// Create a factory that recognizes the wrapper
public class ProfiledSqlServerMethodsFactory : Providers.SqlServer.SqlServerMethodsFactory
{
    public override bool SupportsConnectionCustom(IDbConnection db) =>
        db is ProfiledDbConnection pdc && pdc.InnerConnection is SqlConnection;
}

// Register the factory
DatabaseMethodsProvider.RegisterFactory(
    // any unique name will do
    "ProfiledDbConnection.SqlServer",
    new ProfiledSqlServerMethodsFactory());

// Wrap your connection with MiniProfiler
var baseConnection = new SqlConnection(connectionString);
var profiledConnection = new ProfiledDbConnection(baseConnection, MiniProfiler.Current);

// Now your profiled connection works with DapperMatic
var table = new DmTable(null /* or schemaName */, "my_table", new[] {
    new DmColumn("id", typeof(int)) { IsPrimaryKey = true },
    new DmColumn("name", typeof(string)),
});
await profiledConnection.CreateTableIfNotExistsAsync(table);
```

#### ServiceStack OrmLite Integration

**Use Case**: Use DapperMatic DDL operations with ServiceStack's OrmLite connections.

```csharp
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.Sqlite;

// Create a factory for OrmLite SQLite connections
public class OrmLiteSqliteDialectMethodsFactory : SqliteMethodsFactory
{
	public override bool SupportsConnectionCustom(IDbConnection db)
	{
		// OrmLite connections implement IDbConnection directly
		return db is OrmLiteConnection odb && odb.DialectProvider is SqliteOrmLiteDialectProvider; // SQLite check
	}
}

// Register the factory
DatabaseMethodsProvider.RegisterFactory(
    nameof(OrmLiteSqliteDialectMethodsFactory),
    new OrmLiteSqliteDialectMethodsFactory());

// Create OrmLite connection
var sqliteFile = Path.GetTempFileName();
var factory = new OrmLiteConnectionFactory(sqliteFile, SqliteDialect.Provider);
using (var connection = factory.OpenDbConnection())
{
    var table = new DmTable(null /* or schemaName */, "my_table", new[] {
        new DmColumn("id", typeof(int)) { IsPrimaryKey = true },
        new DmColumn("name", typeof(string)),
    });
    await connection.CreateSchemaIfNotExistsAsync("reporting"); // returns false with SQLite
    await connection.CreateTableIfNotExistsAsync(table);

    var dbTable = await connection.GetTableAsync(null, "my_table");
    if (dbTable != null) Console.WriteLine("Table exists with columns: " + string.Join(", ", dbTable.Columns.Select(c => c.ColumnName)));

    // Prints: Table exists with columns: id, name
}

if (File.Exists(sqliteFile)) File.Delete(sqliteFile);
```

#### Resilience and Retry Logic

**Use Case**: Add automatic retry logic for transient database failures.

```csharp
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Polly;

public class ResilientDbConnection : IDbConnection
{
    private readonly IDbConnection _innerConnection;
    private readonly IAsyncPolicy _retryPolicy;

    public ResilientDbConnection(IDbConnection innerConnection, ILogger logger)
    {
        _innerConnection = innerConnection;
        _retryPolicy = Policy
            .Handle<SqlException>(ex => IsTransientError(ex.Number))
            .WaitAndRetryAsync(3, retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    logger.LogWarning("Retrying database operation. Attempt {RetryCount}", retryCount);
                });
    }

    // Implement IDbConnection by delegating to _innerConnection
    // with retry policy applied to operations
}

public class ResilientSqlServerMethodsFactory : SqlServerMethodsFactory
{
    public override bool SupportsConnection(IDbConnection db)
    {
        return db is ResilientDbConnection rdc && rdc.InnerConnection is SqlConnection;
    }
}

// Usage
var resilientConnection = new ResilientDbConnection(baseConnection, logger);
DatabaseMethodsProvider.RegisterFactory(
    "ResilientDbConnection.SqlServer",
    new ResilientSqlServerMethodsFactory());

// DDL operations now have automatic retry logic
var criticalTable = new DmTable("dbo", "CriticalTable", criticalColumns);
await resilientConnection.CreateTableIfNotExistsAsync(criticalTable);
```

### Native Custom Providers (Advanced)

For completely new database engines or significantly different connection implementations, you'll need to implement the full provider interface.

#### Custom Database Engine

**Use Case**: Supporting a proprietary or emerging database that doesn't have existing .NET providers.

```csharp
// Example: Fictional CentipedeDB with native C# driver
namespace PestControl.Database;

public class CentipedeDbConnection : DbConnection
{
    private readonly string _connectionString;
    private bool _isOpen;

    public CentipedeDbConnection(string connectionString)
    {
        _connectionString = connectionString;
    }

    public override string ConnectionString
    {
        get => _connectionString;
        set => throw new NotSupportedException();
    }

    public override void Open()
    {
        // Custom connection logic for CentipedeDB
        CentipedeNativeClient.Connect(_connectionString);
        _isOpen = true;
    }

    // Implement all other DbConnection members...
}

// Full provider implementation required
public class CentipedeDbMethods : IDatabaseMethods
{
    public async Task<bool> CreateTableIfNotExistsAsync(
        IDbConnection db,
        DmTable table,
        IDbTransaction tx = null,
        CancellationToken cancellationToken = default)
    {
        // Generate CentipedeDB-specific CREATE TABLE syntax
        var sql = GenerateCentipedeCreateTableSql(table);

        // Execute using CentipedeDB's native command execution
        return await ExecuteCentipedeCommandAsync(db, sql, tx, cancellationToken);
    }

    // Implement all IDatabaseMethods interface methods...
    // This is the most work-intensive approach as every method needs
    // custom implementation for your database's specific syntax and capabilities
}

public class CentipedeDbMethodsFactory : DatabaseMethodsFactoryBase
{
    public override bool SupportsConnection(IDbConnection db)
        => db is CentipedeDbConnection;

    protected override IDatabaseMethods CreateMethodsCore()
        => new CentipedeDbMethods();
}

// Registration and usage
DatabaseMethodsProvider.RegisterFactory("CentipedeDb", new CentipedeDbMethodsFactory());

using var connection = new CentipedeDbConnection("Host=centipede-cluster;Database=analytics");
var customTable = new DmTable(null /* or schemaName */, "CustomTable", customColumns);
await connection.CreateTableIfNotExistsAsync(customTable);
```

### Additional Custom Provider Ideas

#### Distributed Tracing Integration

```csharp
public class TracingDbConnection : IDbConnection
{
    private readonly IDbConnection _innerConnection;
    private readonly ActivitySource _activitySource;

    public TracingDbConnection(IDbConnection innerConnection)
    {
        _innerConnection = innerConnection;
        _activitySource = new ActivitySource("MyApp.Database");
    }

    // Wrap all operations with OpenTelemetry activities for observability
}
```

#### Connection Pooling Wrapper

```csharp
public class PooledDbConnection : IDbConnection
{
    private readonly IConnectionPool _pool;
    private IDbConnection _currentConnection;

    // Implement smart connection pooling with health checks
    // and automatic connection recycling
}
```

#### Audit Trail Provider

```csharp
public class AuditingDbConnection : IDbConnection
{
    private readonly IDbConnection _innerConnection;
    private readonly IAuditLogger _auditLogger;

    // Log all DDL operations for compliance and tracking
    // Include user context, timestamps, and operation details
}
```

### Registration Best Practices

1. **Register Early**: Register custom providers during application startup, before any DapperMatic operations.

```csharp
// In Program.cs or Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    // Register custom providers first
    DatabaseMethodsProvider.RegisterFactory("ProfiledDbConnection.SqlServer",
        new ProfiledSqlServerMethodsFactory());
    DatabaseMethodsProvider.RegisterFactory("ResilientDbConnection.PostgreSql",
        new ResilientPostgreSqlMethodsFactory());

    // Then configure other services
    services.AddScoped<IDbConnection>(provider =>
        new ProfiledDbConnection(baseConnection, MiniProfiler.Current));
}
```

2. **Use Descriptive Names**: Choose factory names that clearly indicate both the wrapper and underlying provider.

3. **Test Thoroughly**: Custom providers should be tested with the same rigor as built-in providers.

```csharp
[Test]
public async Task CustomProvider_CreateTable_ShouldWork()
{
    using var connection = new CustomDbConnection(connectionString);
    var table = new DmTable("schema", "TestTable", columns);
    var result = await connection.CreateTableIfNotExistsAsync(table);

    Assert.IsTrue(result);
    Assert.IsTrue(await connection.DoesTableExistAsync("schema", "TestTable"));
}
```

4. **Handle Provider-Specific Features**: Custom providers may have unique capabilities or limitations.

```csharp
public class CustomMethodsFactory : DatabaseMethodsFactoryBase
{
    protected override IDatabaseMethods CreateMethodsCore()
    {
        var methods = new CustomMethods();

        // Configure provider-specific settings
        methods.SupportsTransactions = true;
        methods.SupportsSchemas = false;
        methods.MaxIdentifierLength = 128;

        return methods;
    }
}
```

### When to Use Custom Providers

- **Wrapper Providers** when you need to add cross-cutting concerns (profiling, logging, resilience) to existing database connections
- **Native Providers** when working with databases not supported by DapperMatic out-of-the-box
- **Specialized Providers** when you need custom connection behavior, pooling strategies, or protocol implementations

Custom providers ensure DapperMatic can work with virtually any `IDbConnection` implementation while maintaining the same consistent API across all your database interactions.

## Connection String Examples

::: code-group

```csharp [SQL Server]
// Windows Authentication
"Server=localhost;Database=MyApp;Integrated Security=true;"

// SQL Authentication
"Server=localhost;Database=MyApp;User Id=user;Password=pass;"
```

```csharp [MySQL]
// Standard connection
"Server=localhost;Database=myapp;Uid=user;Pwd=password;"

// With SSL
"Server=localhost;Database=myapp;Uid=user;Pwd=password;SslMode=Required;"
```

```csharp [PostgreSQL]
// Standard connection
"Host=localhost;Database=myapp;Username=user;Password=password;"

// With connection pooling
"Host=localhost;Database=myapp;Username=user;Password=password;Pooling=true;Maximum Pool Size=20;"
```

```csharp [SQLite]
// File-based
"Data Source=myapp.db"

// In-memory (testing)
"Data Source=:memory:"
```

:::

## Data Type Mapping

DapperMatic automatically maps .NET types to appropriate database-specific types. The tables below show how each .NET type is represented across all supported database providers based on the comprehensive test suite in `DatabaseMethodsTests.Types.cs`.

::: tip Source of Truth
These mappings are verified by automated tests. For the most up-to-date and detailed type mappings, see [`tests/MJCZone.DapperMatic.Tests/DatabaseMethodsTests.Types.cs`](https://github.com/mjczone/MJCZone.DapperMatic/blob/develop/tests/MJCZone.DapperMatic.Tests/DatabaseMethodsTests.Types.cs).
:::

### Integer Types (Default Mappings)

| .NET Type | SQL Server    | MySQL        | PostgreSQL | SQLite     | Notes |
| --------- | ------------- | ------------ | ---------- | ---------- | ----- |
| `byte`    | `TINYINT(3)`  | `TINYINT(3)` | `INT2`     | `TINYINT`  | PostgreSQL: SMALLINT alias |
| `sbyte`   | `TINYINT(3)`  | `TINYINT(3)` | `INT2`     | `TINYINT`  | PostgreSQL: SMALLINT alias |
| `short`   | `SMALLINT(5)` | `SMALLINT(5)` | `INT2`    | `SMALLINT` | PostgreSQL: SMALLINT alias |
| `int`     | `INT(10)`     | `INT(10)`    | `INT4`     | `INT`      | PostgreSQL: INTEGER alias |
| `long`    | `BIGINT(19)`  | `BIGINT(19)` | `INT8`     | `BIGINT`   | PostgreSQL: BIGINT alias |

::: info Integer Type Notes
- **PostgreSQL Internal Names**: `INT2`, `INT4`, `INT8` are internal PostgreSQL type names (aliases for `SMALLINT`, `INTEGER`, `BIGINT`)
- **Display Width**: MySQL 8.0.19+ deprecated display widths except for `TINYINT(1)` (boolean). Older versions may show different widths.
- **SQLite Type Affinity**: SQLite maps all integer types to INTEGER storage class but preserves type names for compatibility
:::

### Floating Point Types

| .NET Type | SQL Server  | MySQL        | PostgreSQL | SQLite  | Notes |
| --------- | ----------- | ------------ | ---------- | ------- | ----- |
| `float`   | `REAL(24)`  | `DOUBLE(22)` | `FLOAT4`   | `REAL`  | MySQL uses DOUBLE; PostgreSQL: REAL alias |
| `double`  | `FLOAT(53)` | `FLOAT(12)`  | `FLOAT8`   | `DOUBLE` | PostgreSQL: DOUBLE PRECISION alias |

::: info Floating Point Notes
- **PostgreSQL Internal Names**: `FLOAT4`, `FLOAT8` are internal names for `REAL` and `DOUBLE PRECISION`
- **MySQL Behavior**: Maps `float` to DOUBLE instead of FLOAT for better precision
:::

### Decimal Types

| .NET Type | SQL Server       | MySQL           | PostgreSQL      | SQLite          | Precision | Scale | Notes |
| --------- | ---------------- | --------------- | --------------- | --------------- | --------- | ----- | ----- |
| `decimal` | `DECIMAL(16,4)`  | `DECIMAL(16,4)` | `NUMERIC(16,4)` | `NUMERIC(16,4)` | Default   | Default | **Default precision/scale** |
| `decimal` | `DECIMAL(12,8)`  | `DECIMAL(12,8)` | `NUMERIC(12,8)` | `NUMERIC(12,8)` | 12        | 8     | Custom precision/scale |
| `decimal` | `DECIMAL(12)`    | `DECIMAL(12)`   | `NUMERIC(12)`   | `NUMERIC(12)`   | 12        | 0     | Precision only (scale=0) |

::: tip Decimal Configuration
Default precision is **16**, default scale is **4**. Customize using `DmColumn` attribute:
```csharp
[DmColumn("Price", precision: 18, scale: 2)]
public decimal Price { get; set; }
```
:::

### Boolean Type

| .NET Type | SQL Server | MySQL        | PostgreSQL | SQLite    | Notes |
| --------- | ---------- | ------------ | ---------- | --------- | ----- |
| `bool`    | `BIT`      | `TINYINT(1)` | `BOOL`     | `BOOLEAN` | MySQL: TINYINT(1) convention |

::: info Boolean Notes
- **MySQL**: Uses `TINYINT(1)` as boolean convention (0=false, 1=true). `BOOLEAN` is an alias that becomes `TINYINT(1)`.
- **PostgreSQL**: `BOOL` is an alias for `BOOLEAN`
:::

### Character Type

| .NET Type | SQL Server | MySQL     | PostgreSQL | SQLite | Unicode | Notes |
| --------- | ---------- | --------- | ---------- | ------ | ------- | ----- |
| `char`    | `CHAR(1)`  | `CHAR(1)` | `BPCHAR(1)` | `TEXT` | No      | Default (non-unicode) |
| `char`    | `NCHAR(1)` | `CHAR(1)` | `BPCHAR(1)` | `TEXT` | Yes     | Unicode (SQL Server only) |

::: info Character Type Notes
- **PostgreSQL**: `BPCHAR` = "blank-padded char" (internal name for CHAR type)
- **Unicode**: Only SQL Server differentiates CHAR (ASCII) vs NCHAR (Unicode). Other providers ignore the unicode flag.
:::

### String Types (Variable Length)

| .NET Type | SQL Server      | MySQL          | PostgreSQL    | SQLite        | Unicode | Length | Notes |
| --------- | --------------- | -------------- | ------------- | ------------- | ------- | ------ | ----- |
| `string`  | `NVARCHAR(255)` | `VARCHAR(255)` | `VARCHAR(255)` | `VARCHAR(255)` | Yes     | Default | **Default: 255 characters** |
| `string`  | `VARCHAR(255)`  | `VARCHAR(255)` | `VARCHAR(255)` | `VARCHAR(255)` | No      | Default | Non-unicode |
| `string`  | `NVARCHAR(234)` | `VARCHAR(234)` | `VARCHAR(234)` | `NVARCHAR(234)` | Yes    | 234    | Custom length |
| `string`  | `VARCHAR(234)`  | `VARCHAR(234)` | `VARCHAR(234)` | `VARCHAR(234)` | No     | 234    | Custom length, non-unicode |
| `string`  | `NVARCHAR(MAX)` | `TEXT(65535)`  | `TEXT`        | `NVARCHAR`    | Yes     | -1 or `int.MaxValue` | **Unlimited length** |

### String Types (Fixed Length)

| .NET Type | SQL Server   | MySQL       | PostgreSQL  | SQLite      | Unicode | Length | IsFixedLength | Notes |
| --------- | ------------ | ----------- | ----------- | ----------- | ------- | ------ | ------------- | ----- |
| `string`  | `CHAR(234)`  | `CHAR(234)` | `BPCHAR(234)` | `CHAR(234)` | No    | 234    | Yes           | Fixed-length CHAR |
| `string`  | `NCHAR(234)` | `CHAR(234)` | `BPCHAR(234)` | `NCHAR(234)` | Yes  | 234    | Yes           | Fixed-length NCHAR |

::: tip String Type Configuration
```csharp
// Default (variable length, unicode, 255 chars)
public string Name { get; set; }

// Custom length
[DmColumn(length: 100)]
public string ShortName { get; set; }

// Unlimited length
[DmColumn(length: -1)]  // or int.MaxValue
public string LongText { get; set; }

// Fixed length (for codes, etc)
[DmColumn(length: 10, isFixedLength: true)]
public string Code { get; set; }

// Non-unicode (SQL Server only)
[DmColumn(isUnicode: false)]
public string AsciiOnly { get; set; }
```
:::

### GUID Type

| .NET Type | SQL Server         | MySQL      | PostgreSQL | SQLite       | Notes |
| --------- | ------------------ | ---------- | ---------- | ------------ | ----- |
| `Guid`    | `UNIQUEIDENTIFIER` | `CHAR(36)` | `UUID`     | `VARCHAR(36)` | |

### Date & Time Types

| .NET Type        | SQL Server       | MySQL       | PostgreSQL   | SQLite     | Notes |
| ---------------- | ---------------- | ----------- | ------------ | ---------- | ----- |
| `DateTime`       | `DATETIME`       | `DATETIME`  | `TIMESTAMP`  | `DATETIME` | |
| `DateTimeOffset` | `DATETIMEOFFSET` | `TIMESTAMP` | `TIMESTAMPTZ` | `DATETIME` | MySQL: TIMESTAMP type |
| `TimeSpan`       | `TIME`           | `TIME`      | `INTERVAL`   | `TIME`     | PostgreSQL: INTERVAL for durations |
| `DateOnly`       | `DATE`           | `DATE`      | `DATE`       | `DATE`     | .NET 6+ |
| `TimeOnly`       | `TIME`           | `TIME`      | `TIME`       | `TIME`     | .NET 6+ |

::: info Date & Time Notes
- **PostgreSQL**: `TIMESTAMPTZ` = timestamp with time zone, `INTERVAL` for time spans/durations
- **MySQL**: DateTime precision defaults can vary by version. Modern MySQL supports fractional seconds.
- **SQLite**: Stores date/time as TEXT, INTEGER, or REAL. DapperMatic uses TEXT format for compatibility.
:::

### Binary Types

| .NET Type              | SQL Server        | MySQL            | PostgreSQL | SQLite | Notes |
| ---------------------- | ----------------- | ---------------- | ---------- | ------ | ----- |
| `byte[]`               | `VARBINARY(255)`  | `VARBINARY(255)` | `BYTEA`    | `BLOB` | **Default: 255 bytes** |
| `Memory<byte>`         | `VARBINARY(255)`  | `VARBINARY(255)` | `BYTEA`    | `BLOB` | |
| `ReadOnlyMemory<byte>` | `VARBINARY(255)`  | `VARBINARY(255)` | `BYTEA`    | `BLOB` | |
| `Stream`               | `VARBINARY(MAX)`  | `LONGBLOB`       | `BYTEA`    | `BLOB` | **Unlimited (large binary)** |
| `MemoryStream`         | `VARBINARY(MAX)`  | `LONGBLOB`       | `BYTEA`    | `BLOB` | **Unlimited (large binary)** |

::: info Binary Type Notes
- **Default Length**: Binary types default to 255 bytes. Streams default to unlimited (MAX/LONGBLOB).
- **PostgreSQL**: `BYTEA` has no length limit - it's variable-length by nature
- **MySQL**: `LONGBLOB` can store up to 4GB
- **Custom Length**: Use `length` parameter for custom sizes: `[DmColumn(length: 1024)]`
:::

### JSON & Complex Types

| .NET Type                          | SQL Server      | MySQL  | PostgreSQL | SQLite | Unicode | Notes |
| ---------------------------------- | --------------- | ------ | ---------- | ------ | ------- | ----- |
| `System.Text.Json.JsonDocument`    | `VARCHAR(MAX)`  | `JSON` | `JSONB`    | `TEXT` | No      | Non-unicode |
| `System.Text.Json.JsonDocument`    | `NVARCHAR(MAX)` | `JSON` | `JSONB`    | `TEXT` | Yes     | Unicode (default) |
| `System.Text.Json.JsonElement`     | `VARCHAR(MAX)`  | `JSON` | `JSONB`    | `TEXT` | No      | Non-unicode |
| `System.Text.Json.JsonElement`     | `NVARCHAR(MAX)` | `JSON` | `JSONB`    | `TEXT` | Yes     | Unicode (default) |
| `System.Text.Json.Nodes.JsonArray` | `NVARCHAR(MAX)` | `JSON` | `JSONB`    | `TEXT` | Yes     | |
| `System.Text.Json.Nodes.JsonObject` | `NVARCHAR(MAX)` | `JSON` | `JSONB`    | `TEXT` | Yes    | |
| `System.Text.Json.Nodes.JsonValue` | `NVARCHAR(MAX)` | `JSON` | `JSONB`    | `TEXT` | Yes     | |
| `object`                           | `sql_variant`   | `JSON` | `JSONB`    | `CLOB` | N/A     | SQL Server: variant type |
| `DayOfWeek` (Enum example)         | `VARCHAR(128)`  | `VARCHAR(128)` | `VARCHAR(128)` | `VARCHAR(128)` | No | Enums as strings |

::: info JSON & Complex Type Notes
- **PostgreSQL**: `JSONB` = binary JSON (preferred over JSON for performance)
- **MySQL**: Native `JSON` type (MySQL 5.7+). **MariaDB 10.x**: `JSON` is an alias for `LONGTEXT` with JSON validation
- **SQL Server**: No native JSON type. Uses `VARCHAR(MAX)`/`NVARCHAR(MAX)` for JSON text. `sql_variant` for generic objects.
- **Enum Handling**: Enums are stored as strings by default (enum name, not value)
:::

### Array Types

| .NET Type  | SQL Server      | MySQL  | PostgreSQL | SQLite | Unicode | Length | Notes |
| ---------- | --------------- | ------ | ---------- | ------ | ------- | ------ | ----- |
| `string[]` | `VARCHAR(MAX)`  | `JSON` | `_TEXT`    | `TEXT` | No      | -1     | PostgreSQL: native array (internal notation) |
| `int[]`    | `VARCHAR(MAX)`  | `JSON` | `_INT4`    | `TEXT` | No      | -1     | PostgreSQL: native array (internal notation) |
| `long[]`   | `VARCHAR(MAX)`  | `JSON` | `_INT8`    | `TEXT` | No      | -1     | PostgreSQL: native array (internal notation) |
| `Guid[]`   | `VARCHAR(MAX)`  | `JSON` | `_UUID`    | `TEXT` | No      | -1     | PostgreSQL: native array (internal notation) |
| `char[]`   | `VARCHAR(255)`  | `VARCHAR(255)` | `VARCHAR(255)` | `VARCHAR(255)` | No | Default | Treated as string, not array |
| `char[]`   | `NVARCHAR(MAX)` | `TEXT(65535)` | `TEXT`    | `NVARCHAR` | Yes    | -1     | Unlimited (treated as string) |

::: info Array Type Notes
- **PostgreSQL Native Arrays**:
  - PostgreSQL has true native array support for primitive types
  - Internal notation uses underscore prefix: `_TEXT`, `_INT4`, `_INT8`, `_UUID`
  - Standard notation uses suffix: `text[]`, `integer[]`, `bigint[]`, `uuid[]`
  - DapperMatic recognizes both notations when reading schema
- **SQL Server, MySQL, SQLite**: No native array support. Arrays are serialized as JSON or TEXT.
- **char[]** is special: treated as a string (character array), not a typed array
- **MariaDB 10.x**: `JSON` is actually `LONGTEXT` with validation
:::

### Collection Types

All collection types are serialized as JSON:

| .NET Type                         | SQL Server     | MySQL  | PostgreSQL | SQLite | Notes |
| --------------------------------- | -------------- | ------ | ---------- | ------ | ----- |
| `List<string>`                    | `VARCHAR(MAX)` | `JSON` | `JSONB`    | `TEXT` | JSON array |
| `IList<string>`                   | `VARCHAR(MAX)` | `JSON` | `JSONB`    | `TEXT` | JSON array |
| `ICollection<string>`             | `VARCHAR(MAX)` | `JSON` | `JSONB`    | `TEXT` | JSON array |
| `IEnumerable<string>`             | `VARCHAR(MAX)` | `JSON` | `JSONB`    | `TEXT` | JSON array |
| `Dictionary<string, string>`      | `VARCHAR(MAX)` | `JSON` | `HSTORE`   | `TEXT` | PostgreSQL: HSTORE for string-string maps |
| `IDictionary<string, string>`     | `VARCHAR(MAX)` | `JSON` | `HSTORE`   | `TEXT` | PostgreSQL: HSTORE for string-string maps |

::: info Collection Type Notes
- All collections are serialized as JSON for storage
- **PostgreSQL HSTORE**: Special type for `Dictionary<string, string>` - efficient key-value storage
- **PostgreSQL JSONB**: Binary JSON format for other collections (faster indexing than JSON)
- **MariaDB 10.x**: `JSON` is actually `LONGTEXT` with JSON validation
- Generic types work the same: `List<T>`, `Dictionary<K,V>`, etc.
:::

### Auto-Increment Configuration

::: code-group

```csharp [SQL Server]
new DmColumn("Id", typeof(int))
{
    IsNullable = false,
    IsAutoIncrement = true // Creates IDENTITY(1,1)
}
```

```csharp [MySQL]
new DmColumn("Id", typeof(int))
{
    IsNullable = false,
    IsAutoIncrement = true // Creates AUTO_INCREMENT
}
```

```csharp [PostgreSQL]
new DmColumn("Id", typeof(int))
{
    IsNullable = false,
    IsAutoIncrement = true // Creates SERIAL/IDENTITY
}
```

```csharp [SQLite]
new DmColumn("Id", typeof(int))
{
    IsNullable = false,
    IsAutoIncrement = true // Uses INTEGER PRIMARY KEY
}
```

:::

### General Type Mapping Notes

::: tip Key Takeaways
- **Defaults are sensible**: String length defaults to 255, binary to 255, decimal to (16,4)
- **Unlimited values**: Use `length: -1` or `int.MaxValue` for VARCHAR(MAX), TEXT, LONGBLOB, etc.
- **PostgreSQL specifics**: Uses internal notation (INT2, INT4, FLOAT8, _TEXT, etc.) when reading schema
- **MariaDB 10.x JSON**: `JSON` type is actually `LONGTEXT` with validation (not binary JSON like MySQL 5.7+)
- **Source of truth**: See [`DatabaseMethodsTests.Types.cs`](https://github.com/mjczone/MJCZone.DapperMatic/blob/develop/tests/MJCZone.DapperMatic.Tests/DatabaseMethodsTests.Types.cs) for complete test coverage
:::

**Customizing Type Mappings:**

```csharp
// Custom length
[DmColumn(length: 500)]
public string LongName { get; set; }

// Custom precision and scale
[DmColumn(precision: 18, scale: 6)]
public decimal HighPrecisionValue { get; set; }

// Fixed-length strings
[DmColumn(length: 10, isFixedLength: true)]
public string CountryCode { get; set; }

// Explicit provider-specific types (multi-database support)
[DmColumn(providerDataType: "{sqlserver:money,mysql:decimal(19,4),postgresql:money,sqlite:real}")]
public decimal Price { get; set; }

// Simple provider type (single database)
[DmColumn(providerDataType: "money")]
public decimal Amount { get; set; }
```

## Getting Help

If you encounter provider-specific issues:

1. Check the [troubleshooting guide](/guide/troubleshooting)
2. Review the provider's documentation
3. File an issue on [GitHub](https://github.com/mjczone/dappermatic/issues)
