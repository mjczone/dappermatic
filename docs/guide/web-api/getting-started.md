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

### 2. Add to Your ASP.NET Core Application

```csharp
using MJCZone.DapperMatic.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add DapperMatic Web API services
builder.Services.AddDapperMatic(); // Uses in-memory repository by default

// Optional: Configure a specific datasource repository type
// Datasource repositories store encrypted connection string information for datasources
// (databases with metadata like tags, display names, etc.) in the system
// builder.Services.AddDapperMatic(config =>
//     config.UseFileDatasourceRepository("path/to/datasources.json"));
// OR
// builder.Services.AddDapperMatic(config =>
//     config.UseDatabaseDatasourceRepository("SqlServer", connectionString));

var app = builder.Build();

// Register DapperMatic endpoints
app.UseDapperMatic();

app.Run();
```

### 3. Start Your Application

Your application now has DapperMatic REST endpoints available at:

- `GET /api/dm/datasources/` - List all datasources
- `POST /api/dm/datasources/` - Add a new datasource
- `GET /api/dm/datasources/{id}` - Get datasource details
- And many more...

### 4. Explore the API

Navigate to `/swagger` in your application to see the interactive OpenAPI documentation, or use the [REST API browser](/api-browser/) to explore all available endpoints.

## Example Usage

Once configured, you can manage your database schemas via HTTP requests:

```bash
# Add a datasource
curl -X POST "/api/dm/datasources/" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "MyDatabase",
    "connectionString": "Server=localhost;Database=MyDB;Trusted_Connection=true;",
    "provider": "SqlServer"
  }'

# List all tables in the datasource (default schema)
curl -X GET "/api/dm/d/MyDatabase/t/"
```

## Next Steps

- [Setup & Configuration](/guide/web-api/setup) - Configure repositories, security, and options
- [Security & Authentication](/guide/web-api/security) - Secure your endpoints with authentication and authorization
- [Endpoints Overview](/guide/web-api/endpoints) - Explore all available REST endpoints
- [Integration Examples](/guide/web-api/examples) - See real-world integration scenarios

## When to Use Web API Integration

Choose the Web API Integration when you need:

- **Web-based database management** - Admin panels, dashboards, or management tools
- **Remote schema operations** - External systems need to modify your database structure
- **Multi-tenant applications** - Different clients need to manage their own schemas
- **Database-as-a-Service** - Providing database management capabilities to other applications

For direct .NET library usage, see the [Library Usage guide](/guide/getting-started).