# API Endpoints Reference

DapperMatic Web API provides comprehensive REST endpoints for database schema management. The complete, always up-to-date endpoint documentation is available through the interactive OpenAPI browser.

## 📚 Complete API Documentation

### Interactive API Browser
**The authoritative source for all endpoints is the [DapperMatic API Browser](/api-browser/)** which provides:

- ✅ **Complete endpoint listings** - Every single endpoint with accurate paths
- ✅ **Live API specification** - Generated directly from the OpenAPI/Swagger definition
- ✅ **Request/Response schemas** - Exact models and parameters
- ✅ **Interactive testing** - Try endpoints directly from the browser
- ✅ **Code generation** - Generate client code in multiple languages
- ✅ **Always accurate** - Automatically updated when the API changes

### Accessing API Documentation

1. **Built-in Documentation API Browser**: Navigate to [/api-browser/](/api-browser/) in this documentation

2. **Add Swagger to Your Application** (optional): Install the Swashbuckle NuGet package to enable interactive API documentation in your application:
   ```bash
   dotnet add package Swashbuckle.AspNetCore
   ```
   Then configure it in your application:
   ```csharp
   builder.Services.AddEndpointsApiExplorer();
   builder.Services.AddSwaggerGen();

   var app = builder.Build();

   app.UseSwagger();
   app.UseSwaggerUI();
   ```
   Once configured, you can access:
   - **Swagger UI**: Navigate to `/swagger` in your running application
   - **OpenAPI Specification**: Available at `/swagger/v1/swagger.json`

## Base URL Structure

All endpoints use the base path `/api/dm` by default (configurable via `DapperMaticOptions.BasePath`):

```
/api/dm/{resource-hierarchy}
```

## URL Hierarchy

DapperMatic uses a consistent hierarchical URL structure that mirrors database organization:

```
/api/dm/datasources                              # Datasource management
/api/dm/d/{datasourceId}/s                       # Schema management
/api/dm/d/{datasourceId}/t                       # Tables (default schema)
/api/dm/d/{datasourceId}/s/{schemaName}/t        # Tables (specific schema)
/api/dm/d/{datasourceId}/v                       # Views (default schema)
/api/dm/d/{datasourceId}/s/{schemaName}/v        # Views (specific schema)
/api/dm/d/{datasourceId}/datatypes               # Datasource-specific data types
```

### Path Abbreviations
- `d` = datasource
- `s` = schema
- `t` = table
- `v` = view

## Endpoint Categories

### Datasource Management
Manage database connections with metadata (display names, tags, etc.).

**Key Operations:**
- List, Get, Add, Update, Delete datasources
- Test datasource connectivity

**Example - Add Datasource:**
```json
POST /api/dm/datasources/
{
  "id": "ProductionDB",
  "provider": "SqlServer",
  "connectionString": "Server=prod-server;Database=MyApp;Trusted_Connection=true;",
  "displayName": "Production Database",
  "description": "Main production database",
  "tags": ["production", "primary"],
  "isEnabled": true
}
```

### Schema Operations
Manage database schemas within datasources.

**Key Operations:**
- List, Get, Create, Drop schemas
- Schema existence checking

### Table Operations
DapperMatic supports both default schema and schema-specific endpoints:

**Key Operations:**
- List, Get, Create, Drop, Update tables
- Check table existence
- Query table data
- **Column Management:** List, Get, Add, Update, Drop columns
- **Index Management:** List, Get, Create, Drop indexes
- **Constraint Management:** Primary keys, foreign keys, unique, check, default constraints

### View Operations
Manage database views with similar patterns to tables.

**Key Operations:**
- List, Get, Create, Update, Drop views
- Check view existence
- Query view data

### Data Type Operations
Get available data types for specific datasource providers.

**Key Operations:**
- Get datasource-specific data types (including custom types)

## Response Format

All endpoints return a consistent response format based on the `ResponseBase<T>` class:

```json
{
  "result": T,          // The data payload (object, array, or null)
  "message": string,    // Optional message providing additional information
  "success": boolean    // Whether the operation was successful
}
```

### Success Response Example
```json
{
  "result": {
    "id": "ProductionDB",
    "provider": "SqlServer",
    "displayName": "Production Database",
    "isConnected": true
  },
  "message": null,
  "success": true
}
```

### Error Response Example
```json
{
  "result": null,
  "message": "Datasource 'NonExistentDB' not found",
  "success": false
}
```

## HTTP Methods and Status Codes

DapperMatic follows RESTful conventions:

### HTTP Methods
- **GET** - Retrieve data (list, get details, check existence)
- **POST** - Create new resources, execute complex operations
- **PUT** - Update/replace entire resources
- **PATCH** - Partially update resources
- **DELETE** - Remove resources

### HTTP Status Codes
- **200 OK** - Successful GET, PUT, PATCH operations
- **201 Created** - Successful POST operations (resource created)
- **204 No Content** - Successful DELETE operations
- **400 Bad Request** - Invalid request data or parameters
- **401 Unauthorized** - Authentication required
- **403 Forbidden** - Insufficient permissions
- **404 Not Found** - Requested resource doesn't exist
- **409 Conflict** - Resource already exists (for create operations)
- **500 Internal Server Error** - Unexpected server error

## Authentication and Authorization

All endpoints respect the configured authentication and authorization settings:

- Endpoints require authentication if `RequireAuthentication = true`
- Role-based access control via `RequireRole` and `ReadOnlyRole` options
- Custom authorization logic via `IDapperMaticPermissions` implementations
- All operations are logged via `IDapperMaticAuditLogger`

See the [Security & Authentication](/guide/web-api/security) guide for detailed configuration.

## Why Use the API Browser?

The [API Browser](/api-browser/) is always the most accurate source because it:

1. **Generates from source** - Built directly from the OpenAPI specification
2. **Updates automatically** - Always reflects the current API implementation
3. **Shows all endpoints** - Including nested resources like columns, indexes, constraints
4. **Provides exact schemas** - Request/response models with all properties
5. **Includes validation rules** - Required fields, formats, constraints
6. **Offers interactive testing** - Try endpoints with real data

## Quick Start Examples

While the [API Browser](/api-browser/) has complete details, here are some common operations:

```bash
# List all datasources
GET /api/dm/datasources/

# Create a table
POST /api/dm/d/MyDB/t/
{
  "name": "Users",
  "columns": [...]
}

# Query a table
POST /api/dm/d/MyDB/t/Users/query
{
  "filter": "IsActive = true",
  "orderBy": "CreatedAt DESC",
  "limit": 100
}

# Add a column to a table
POST /api/dm/d/MyDB/t/Users/columns
{
  "name": "LastLoginDate",
  "dataType": "datetime2",
  "allowNull": true
}
```

For complete endpoint details, request/response schemas, and interactive testing, visit the [DapperMatic API Browser](/api-browser/).