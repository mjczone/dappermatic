# Getting Started

Welcome to DapperMatic! This guide will help you get up and running with database DDL operations in your .NET applications.

## What is DapperMatic?

DapperMatic is a C# library that provides IDbConnection extension methods for DDL (Data Definition Language) operations across multiple database providers. It simplifies database schema management with a clean, model-first approach.

## Key Features

- **Multi-Provider Support**: Works with SQL Server, MySQL/MariaDB, PostgreSQL, and SQLite
- **Model-First Approach**: Define your schema using intuitive C# models
- **Type-Safe Operations**: Strongly-typed API prevents runtime errors
- **Comprehensive DDL Support**: Tables, columns, indexes, constraints, views, and more
- **SQL Injection Prevention**: Built-in security measures protect against malicious input

## Quick Example

```csharp
using MJCZone.DapperMatic;
using MJCZone.DapperMatic.Models;

// Define a table model
var table = new DmTable("Users")
{
    Columns = new[]
    {
        new DmColumn("Id", typeof(int)) { IsNullable = false, IsAutoIncrement = true },
        new DmColumn("Username", typeof(string)) { MaxLength = 50, IsNullable = false },
        new DmColumn("Email", typeof(string)) { MaxLength = 100, IsNullable = false },
        new DmColumn("CreatedAt", typeof(DateTime)) { IsNullable = false }
    },
    PrimaryKey = new DmPrimaryKeyConstraint("PK_Users", "Id")
};

// Create the table
using var connection = new SqlConnection(connectionString);
await connection.CreateTableIfNotExistsAsync("dbo", table);
```

## Next Steps

- [Installation](./installation) - Add DapperMatic to your project
- [Providers](./providers) - Learn about database provider support
- [Models](./models) - Understand the model-first approach
- [Extension Methods](/guide/extension-methods/) - Explore available operations