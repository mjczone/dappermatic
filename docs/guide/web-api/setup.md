# Setup & Configuration

This guide covers how to configure the DapperMatic Web API integration for your specific needs.

## Repository Configuration

DapperMatic supports multiple datasource repository implementations:

### In-Memory Repository
Perfect for development and testing:

```csharp
builder.Services.AddDapperMatic(); // Uses in-memory repository by default
```

### File-Based Repository
Stores datasources in JSON files:

```csharp
builder.Services.AddDapperMatic(config =>
    config.UseFileDatasourceRepository("/path/to/datasources.json"));
```

### Database Repository
Stores datasources in a database:

```csharp
builder.Services.AddDapperMatic(config =>
    config.UseDatabaseDatasourceRepository("SqlServer", connectionString));
```

### Configuration-Based Datasources
Configure datasources directly in `appsettings.json` for easy startup:

```json
{
  "DapperMatic": {
    "BasePath": "/api/dm",
    "RequireAuthentication": false,
    "ConnectionStringEncryptionKey": "your-base64-encoded-256-bit-key",
    "Datasources": [
      {
        "Id": "ProductionDB",
        "Provider": "SqlServer",
        "ConnectionString": "Server=prod-server;Database=MyApp;Trusted_Connection=true;",
        "DisplayName": "Production Database",
        "Description": "Main production database",
        "Tags": ["production", "primary"],
        "IsEnabled": true
      },
      {
        "Id": "AnalyticsDB",
        "Provider": "PostgreSql",
        "ConnectionString": "Host=analytics-server;Database=analytics;Username=user;Password=pass;",
        "DisplayName": "Analytics Database",
        "Description": "Analytics and reporting database",
        "Tags": ["analytics", "readonly"],
        "IsEnabled": true
      }
    ]
  }
}
```

```csharp
builder.Services.AddDapperMatic(); // Automatically loads datasources from configuration
```

This approach is perfect for:
- **Pre-configured environments** - Deploy with datasources already set up
- **Read-only scenarios** - Disable datasource management endpoints for security
- **Multi-tenant applications** - Different configurations per environment
- **Simplified deployment** - No need for runtime datasource registration

## Advanced Configuration

### Custom Options

```csharp
builder.Services.Configure<DapperMaticOptions>(options =>
{
    options.ConnectionStringEncryptionKey = "your-base64-encoded-256-bit-key";
    options.RequireAuthentication = true;
    options.RequireRole = "Admin";
    options.BasePath = "/api/database";
});
```

### Custom Services

```csharp
builder.Services.AddDapperMatic(); // Uses in-memory repository by default

// Register custom services separately
builder.Services.AddScoped<IDapperMaticPermissions, MyPermissionsService>();
builder.Services.AddScoped<IDapperMaticAuditLogger, MyAuditLogger>();
```

## Endpoint Configuration

The Web API endpoints can be customized through options:

```csharp
// Configure options before registering endpoints
builder.Services.Configure<DapperMaticOptions>(options =>
{
    options.BasePath = "/api/database"; // Default: /api/dm
    options.RequireAuthentication = true;
    options.EnableCors = true;
});

// IMPORTANT: UseRouting must be called before UseDapperMatic
app.UseRouting();

// Register endpoints
app.UseDapperMatic();
```

*More detailed configuration options coming soon...*