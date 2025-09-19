# MJCZone.DapperMatic

[![License: LGPL v3](https://img.shields.io/badge/License-LGPL_v3-blue.svg)](https://www.gnu.org/licenses/lgpl-3.0)
[![.github/workflows/build-and-test.yml](https://github.com/mjczone/MJCZone.DapperMatic/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/mjczone/MJCZone.DapperMatic/actions/workflows/build-and-test.yml)
[![.github/workflows/release.yml](https://github.com/mjczone/MJCZone.DapperMatic/actions/workflows/release.yml/badge.svg)](https://github.com/mjczone/MJCZone.DapperMatic/actions/workflows/release.yml)

**Model-first database schema management for .NET applications**

DapperMatic extends `IDbConnection` with extension methods for DDL (Data Definition Language) operations. Define database schemas using strongly-typed C# models and execute them across SQL Server, MySQL/MariaDB, PostgreSQL, and SQLite.

Built on Dapper, DapperMatic provides schema management capabilities for applications that need to create, modify, or inspect database structures at runtime.

## Installation

### Core Library
```bash
dotnet add package MJCZone.DapperMatic
```

### ASP.NET Core Integration
```bash
dotnet add package MJCZone.DapperMatic.AspNetCore
```

## Quick Start

### Basic Table Operations

```csharp
using MJCZone.DapperMatic;
using MJCZone.DapperMatic.Models;
using System.Data.SqlClient;

// Connect to your database
using var connection = new SqlConnection("your-connection-string");
await connection.OpenAsync();

// Define a table structure
var table = new DmTable(
    "dbo",
    "Users",
    [
        new DmColumn("id", typeof(int), isPrimaryKey: true, isAutoIncrement: true),
        new DmColumn("name", typeof(string), isUnique: true),
        new DmColumn("email", typeof(string), length: 255),
        new DmColumn("created_at", typeof(DateTime))
    ]
);

// Create the table
await connection.CreateTableIfNotExistsAsync(table);

// Verify it exists
bool exists = await connection.DoesTableExistAsync("dbo", "Users");

// Retrieve the table definition
var existingTable = await connection.GetTableAsync("dbo", "Users");
```

### Schema Inspection

```csharp
// Get all table names in a schema
var tableNames = await connection.GetTableNamesAsync("dbo");

// Get complete table definition with columns, constraints, and indexes
var fullTable = await connection.GetTableAsync("dbo", "Users");

// Get specific column information
var columns = await connection.GetColumnsAsync("dbo", "Users");

// Check for constraints and indexes
var primaryKeys = await connection.GetPrimaryKeyConstraintsAsync("dbo", "Users");
var indexes = await connection.GetIndexesAsync("dbo", "Users");
```

### Working with Multiple Providers

```csharp
// Same API works across all supported databases
using var sqlServer = new SqlConnection(sqlServerConnectionString);
using var postgres = new NpgsqlConnection(postgresConnectionString);
using var mysql = new MySqlConnection(mysqlConnectionString);
using var sqlite = new SqliteConnection(sqliteConnectionString);

// Identical operations across providers
await sqlServer.CreateTableIfNotExistsAsync(table);
await postgres.CreateTableIfNotExistsAsync(table);
await mysql.CreateTableIfNotExistsAsync(table);
await sqlite.CreateTableIfNotExistsAsync(table);
```

## Key Capabilities

**Cross-Database Operations**
- SQL Server (2019+), MySQL (8.0+), MariaDB (10.5+), PostgreSQL (13+), SQLite (3.35+)
- Provider-specific SQL generation with consistent API
- Automatic type mapping between .NET types and database-specific types

**Schema Management**
- Create, modify, and drop tables, columns, indexes, and constraints
- Runtime schema inspection and validation
- Support for complex column types including JSON, arrays (PostgreSQL), and spatial data

**Model-First Approach**
- Define schemas using strongly-typed C# classes
- Data annotations support (`[Table]`, `[Key]`, `[Column]` attributes)
- Programmatic model building with `DmTable`, `DmColumn`, etc.

**Production-Ready**
- SQL injection protection with expression validation
- Comprehensive test coverage across all database providers
- Transaction support for atomic schema changes

## ASP.NET Core Integration

The `MJCZone.DapperMatic.AspNetCore` package provides REST API endpoints for database schema management:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register DapperMatic services
builder.Services.AddDapperMatic();

var app = builder.Build();

// Add DapperMatic endpoints
app.UseDapperMatic();

app.Run();
```

This creates REST endpoints for managing database schemas via HTTP API. Complete ASP.NET Core documentation will be available in future releases.

## Database Provider Support

| Provider | Versions | Connection Types |
|----------|----------|------------------|
| **SQL Server** | 2019+ | `Microsoft.Data.SqlClient`, `System.Data.SqlClient` |
| **PostgreSQL** | 13+ | `Npgsql` |
| **MySQL** | 8.0+ | `MySqlConnector`, `MySQL.Data` |
| **MariaDB** | 10.5+ | `MySqlConnector`, `MySQL.Data` |
| **SQLite** | 3.35+ | `Microsoft.Data.Sqlite`, `System.Data.SQLite` |

## Use Cases

**Application Deployment**
- Create database schemas during application startup
- Validate existing database structure against expected schema
- Handle schema migrations in applications that manage their own database structure

**Multi-Tenant Applications**
- Create tenant-specific schemas and tables at runtime
- Manage database structures for SaaS applications with dynamic schemas

**Database Administration Tools**
- Build tools for database schema management and inspection
- Create utilities for comparing and synchronizing database structures

**Testing and Development**
- Set up test databases with specific schema requirements
- Create development tools for rapid database prototyping

## Documentation

- **API Reference**: [Documentation site](https://mjczone.github.io/MJCZone.DapperMatic/) (comprehensive guides and examples)
- **Source Code**: Browse the codebase for implementation details
- **Test Examples**: The `tests/` directory contains extensive usage examples

## License

This project is licensed under the GNU Lesser General Public License v3.0 or later (LGPL-3.0-or-later) - see the [LICENSE](LICENSE) file for details.

**What this means:**
- You can use DapperMatic in commercial applications
- You can modify and distribute DapperMatic
- Changes to DapperMatic itself must be contributed back under LGPL
- Your application code remains under your chosen license

## Contributing

This library is in active development. While the API is stabilizing, please see [CONTRIBUTING.md](CONTRIBUTING.md) for current contribution guidelines and project status.

---

For questions and support, please use GitHub Issues or check the documentation site for detailed guides and examples.