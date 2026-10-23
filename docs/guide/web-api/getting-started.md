# Web API Integration - Getting Started

Welcome to DapperMatic's Web API Integration! This guide will help you add REST endpoints to your ASP.NET Core applications for database schema management.

## What is the Web API Integration?

The `MJCZone.DapperMatic.AspNetCore` package extends DapperMatic with REST API capabilities, allowing you to:

- **Manage database schemas via HTTP** - Create, modify, and query database structures through REST endpoints
- **Build admin panels** - Provide web-based database management interfaces
- **Remote database operations** - Allow external systems to manage your database schema
- **Database management tools** - Create web applications for database administration

## Key Features

- **RESTful Endpoints**: Complete CRUD operations for database schema management
- **Security Built-in**: Authentication, authorization, and audit logging
- **OpenAPI Documentation**: Auto-generated Swagger/OpenAPI documentation
- **Repository Pattern**: Flexible datasource management with multiple storage options
- **Encryption Support**: Connection strings encrypted at rest using AES-256

## Quick Start

### 1. Install the Package

```bash
dotnet add package MJCZone.DapperMatic.AspNetCore
```

Add the driver for each database you use, too: DapperMatic bundles none.

```bash
dotnet add package Npgsql   # or Microsoft.Data.SqlClient, MySqlConnector, Microsoft.Data.Sqlite
```

### 2. Add to Your ASP.NET Core Application

```csharp
using MJCZone.DapperMatic.AspNetCore;
using MJCZone.DapperMatic.Security;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Add DapperMatic Web API services, with the drivers your datasources use, and who may do what:
// nothing is allowed until a policy grants it.
builder.Services.AddDapperMatic(dm => dm
    .AddProvider(NpgsqlFactory.Instance)                       // Uses in-memory repository by default
    .Policy(DmPolicy.Allow(DmAction.All, "admin")));           // see Security & Policies

// Optional: Configure a specific datasource repository type
// Datasource repositories store encrypted connection string information for datasources
// (databases with metadata like tags, display names, etc.) in the system
// builder.Services.AddDapperMatic(config =>
//     config.UseFileDatasourceRepository("path/to/datasources.json"));
// OR
// builder.Services.AddDapperMatic(config =>
//     config.UseDatabaseDatasourceRepository("SqlServer", connectionString));

var app = builder.Build();

// Register DapperMatic endpoints. No UseRouting() call is needed first.
app.UseDapperMatic();

app.Run();
```

### 3. Start Your Application

Your application now has DapperMatic REST endpoints available at:

- `GET /api/dm/d/` - List all datasources
- `POST /api/dm/d/` - Add a new datasource
- `GET /api/dm/d/{datasourceId}` - Get datasource details
- And many more...

### 4. Explore the API

Wire up an OpenAPI UI to explore the endpoints interactively — see [Accessing API Documentation](/guide/web-api/endpoints#accessing-api-documentation) for the two-line setup using ASP.NET Core's built-in generator and Scalar. You can also use the [REST API browser](/api-browser/) here in the docs to explore every endpoint without running anything.

## Example Usage

Once configured, you can manage your database schemas via HTTP requests:

```bash
# Add a datasource
curl -X POST "/api/dm/d/" \
  -H "Content-Type: application/json" \
  -d '{
    "id": "MyDatabase",
    "displayName": "My Database",
    "connectionString": "Host=localhost;Database=mydb;Username=app;Password=secret",
    "provider": "PostgreSql"
  }'

# List all tables in the datasource (default schema)
curl -X GET "/api/dm/d/MyDatabase/t/"
```

## Next Steps

- [Setup & Configuration](/guide/web-api/setup) - Configure repositories, security, and options
- [Security & Policies](/guide/web-api/security) - Grant access with policies: nothing is allowed until one does
- [Tenants](/guide/web-api/tenancy) - Find the request's tenant and hold it to its data
- [Endpoints Overview](/guide/web-api/endpoints) - Explore all available REST endpoints
- [Integration Examples](/guide/web-api/examples) - See real-world integration scenarios

## When to Use Web API Integration

Choose the Web API Integration when you need:

- **Web-based database management** - Admin panels, dashboards, or management tools
- **Remote schema operations** - External systems need to modify your database structure
- **Multi-tenant applications** - Different clients need to manage their own schemas
- **Database-as-a-Service** - Providing database management capabilities to other applications

For direct .NET library usage, see the [Library Usage guide](/guide/getting-started).