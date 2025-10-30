# Getting Started

Welcome to DapperMatic! This guide will help you get up and running with database DDL operations in your .NET applications.

## What is DapperMatic?

DapperMatic is a comprehensive C# library that provides:
- **DDL Operations**: IDbConnection extension methods for database schema management (tables, indexes, constraints, views, etc.)
- **DML Query Support**: Attribute-based column mapping for Dapper's QueryAsync/ExecuteAsync operations

It simplifies both database schema management and query operations with a clean, model-first approach.

## Key Features

### DDL (Schema Management)
- **Multi-Provider Support**: Works with SQL Server, MySQL/MariaDB, PostgreSQL, and SQLite
- **Model-First Approach**: Define your schema using intuitive C# models
- **Type-Safe Operations**: Strongly-typed API prevents runtime errors
- **Comprehensive DDL Support**: Tables, columns, indexes, constraints, views, and more
- **SQL Injection Prevention**: Built-in security measures protect against malicious input

### DML (Query Operations)
- **Attribute Mapping**: Use `DmColumn`, `DmIgnore`, EF Core, or ServiceStack.OrmLite attributes in Dapper queries
- **Advanced Type Handlers**: XML, JSON, Collections, and Smart Arrays (15 types)
- **Modern C# Support**: Full support for records with parameterized constructors
- **Cross-Database Compatibility**: Type handlers work across all supported providers
- **PostgreSQL Optimizations**: Native arrays (10-50x faster), jsonb, and xml types

## Quick Examples

### DDL Example (Schema Management)

```csharp
using MJCZone.DapperMatic;
using MJCZone.DapperMatic.Models;
using System.Data.SqlClient;

// Define a table model
var table = new DmTable("dbo", "Users")
{
    Columns = new[]
    {
        new DmColumn("Id", typeof(int)) { IsNullable = false, IsAutoIncrement = true },
        new DmColumn("Username", typeof(string)) { MaxLength = 50, IsNullable = false },
        new DmColumn("Email", typeof(string)) { MaxLength = 100, IsNullable = false },
        new DmColumn("CreatedAt", typeof(DateTime)) { IsNullable = false }
    },
    PrimaryKeyConstraint = new DmPrimaryKeyConstraint("PK_Users", "Id")
};

// Create the table
using var connection = new SqlConnection(connectionString);
await connection.CreateTableIfNotExistsAsync(table);
```

### DML Example (Query Operations)

```csharp
using Dapper;
using MJCZone.DapperMatic.DataAnnotations;
using MJCZone.DapperMatic.TypeMapping;

// Initialize DapperMatic type mapping once at startup
DapperMaticTypeMapping.Initialize();

// Define a class with attribute mappings
public class User
{
    [DmColumn("user_id")]
    public int UserId { get; set; }

    [DmColumn("username")]
    public string Username { get; set; } = string.Empty;

    [DmColumn("email")]
    public string Email { get; set; } = string.Empty;

    [DmColumn("tags")]
    public string[]? Tags { get; set; } // Smart array handler - PostgreSQL uses native arrays!
}

// Now Dapper queries work with attribute mappings
var users = await connection.QueryAsync<User>(
    "SELECT user_id, username, email, tags FROM users WHERE user_id = @id",
    new { id = 123 }
);
```

## Next Steps

### For DDL (Schema Management)
- [Installation](./installation) - Add DapperMatic to your project
- [Providers](./providers) - Learn about database provider support
- [Models](./models) - Understand the model-first approach
- [Extension Methods](/guide/extension-methods/) - Explore available DDL operations

### For DML (Query Operations)
- [DML Query Support](./dml-query-support) - Complete guide to using Dapper queries with DapperMatic attributes
- [Data Annotations](./data-annotations) - Learn about attribute mapping options