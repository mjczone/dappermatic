# Database Providers

DapperMatic supports multiple database providers, each with their own connection types and specific features. This page covers the supported providers and their capabilities.

## Quick Navigation

- [Supported Providers](#supported-providers)
  - [SQL Server](#sql-server)
  - [MySQL / MariaDB](#mysql-mariadb)
  - [PostgreSQL](#postgresql)
  - [SQLite](#sqlite)
- [Custom Providers](#custom-providers)
- [Provider-Specific Considerations](#provider-specific-considerations)
- [Provider Selection](#provider-selection)
- [Best Practices](#best-practices)
- [Migration Between Providers](#migration-between-providers)

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
await connection.CreateTableIfNotExistsAsync("dbo", table);
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
await connection.CreateTableIfNotExistsAsync(null, table);
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
await connection.CreateTableIfNotExistsAsync("public", table);
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

**Supported Versions:** SQLite 3.35+ (via `System.Data.SQLite` driver)  
**Connection Package:** `Microsoft.Data.Sqlite` or `System.Data.SQLite`  
**Schema Support:** Single database file (no schemas)

```csharp
using Microsoft.Data.Sqlite;

var connectionString = "Data Source=myapp.db";
using var connection = new SqliteConnection(connectionString);

// SQLite doesn't use schemas
await connection.CreateTableIfNotExistsAsync(null, table);
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
var table = new DmTable(null, "my_table", new[] {
    new DmColumn(null, "my_table", "id", typeof(int), isPrimaryKey: true),
    new DmColumn(null, "my_table", "name", typeof(string)),
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
		return db is OrmLiteConnection odb && odb.DialectProvider is SqliteOrmLiteDialectProvider; // SQL Server check
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
    var table = new DmTable(null, "my_table", new[] {
        new DmColumn(null, "my_table", "id", typeof(int), isPrimaryKey: true),
        new DmColumn(null, "my_table", "name", typeof(string)),
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
await resilientConnection.CreateTableIfNotExistsAsync("dbo", criticalTable);
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
        string schemaName,
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
await connection.CreateTableIfNotExistsAsync(null, customTable);
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
    var table = new DmTable("TestTable", columns);

    var result = await connection.CreateTableIfNotExistsAsync("schema", table);

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

## Provider-Specific Considerations

### Connection String Management

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

### Data Type Mapping

| .NET Type  | SQL Server         | MySQL      | PostgreSQL  | SQLite    |
| ---------- | ------------------ | ---------- | ----------- | --------- |
| `int`      | `INT`              | `INT`      | `INTEGER`   | `INTEGER` |
| `long`     | `BIGINT`           | `BIGINT`   | `BIGINT`    | `INTEGER` |
| `string`   | `NVARCHAR`         | `VARCHAR`  | `TEXT`      | `TEXT`    |
| `DateTime` | `DATETIME2`        | `DATETIME` | `TIMESTAMP` | `TEXT`    |
| `bool`     | `BIT`              | `BOOLEAN`  | `BOOLEAN`   | `INTEGER` |
| `decimal`  | `DECIMAL`          | `DECIMAL`  | `NUMERIC`   | `TEXT`    |
| `Guid`     | `UNIQUEIDENTIFIER` | `CHAR(36)` | `UUID`      | `TEXT`    |

### Auto-Increment Patterns

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

## Provider Selection

### When to Choose SQL Server

- Enterprise applications requiring high availability
- Windows-centric environments
- Need for advanced features like partitioning, replication
- Integration with Microsoft ecosystem

### When to Choose MySQL/MariaDB

- Web applications with high read loads
- Open-source preference
- Need for master-slave replication
- Cost-sensitive projects

### When to Choose PostgreSQL

- Applications requiring complex queries
- Need for advanced data types (JSON, arrays, spatial)
- ACID compliance is critical
- Open-source with enterprise features

### When to Choose SQLite

- Desktop applications
- Mobile applications
- Development and testing
- Small to medium datasets
- Zero-configuration requirements

## Best Practices

1. **Use connection factories** for better connection management
2. **Always specify timeouts** for long-running DDL operations
3. **Test DDL operations** against all target providers
4. **Use transactions** where supported for consistency
5. **Handle provider-specific exceptions** appropriately

## Migration Between Providers

DapperMatic's model-first approach makes it easier to migrate between providers, but consider:

- **Data type compatibility** - some types don't have direct equivalents
- **Schema differences** - SQL Server schemas vs MySQL databases
- **Feature availability** - not all features are available on all providers
- **Performance characteristics** - query patterns may need optimization

## Getting Help

If you encounter provider-specific issues:

1. Check the [troubleshooting guide](/guide/troubleshooting)
2. Review the provider's documentation
3. File an issue on [GitHub](https://github.com/mjczone/dappermatic/issues)
