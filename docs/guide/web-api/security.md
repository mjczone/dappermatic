# Security & Authentication

DapperMatic Web API includes comprehensive security features to protect your database management endpoints with fine-grained authorization, complete audit trails, and enterprise-grade encryption.

## Built-in Security Features

- **🔐 Authentication Integration** - Seamless integration with ASP.NET Core authentication systems
- **🛡️ Fine-Grained Authorization** - Resource-level permission control with context-aware decisions
- **📋 Comprehensive Audit Logging** - Complete audit trail of all operations with detailed context
- **🔒 Connection String Encryption** - AES-256 encryption for stored credentials and sensitive data
- **🚫 SQL Injection Prevention** - Built-in protection against malicious input and code injection
- **👥 Role-Based Access Control** - Support for role-based permissions with read-only role separation

## Authentication

DapperMatic integrates seamlessly with your existing ASP.NET Core authentication system. Configure authentication first, then DapperMatic will automatically respect the authentication context:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Configure your authentication system
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options => {
        options.Authority = "https://your-identity-server.com";
        options.Audience = "your-api-audience";
    });

// Configure DapperMatic with authentication requirements
builder.Services.Configure<DapperMaticOptions>(options =>
{
    options.RequireAuthentication = true;  // Require authentication for all endpoints
    options.RequireRole = "DatabaseAdmin"; // Require specific role for all operations
    options.ReadOnlyRole = "DatabaseUser"; // Allow read-only operations for this role
});

builder.Services.AddDapperMatic();

var app = builder.Build();

// Order matters: Authentication must come before DapperMatic endpoints
app.UseAuthentication();
app.UseAuthorization();
app.UseDapperMatic();
```

## Authorization with IDapperMaticPermissions

The `IDapperMaticPermissions` interface provides powerful, context-aware authorization for all DapperMatic operations. This interface allows you to implement sophisticated authorization logic based on the complete operation context.

### Understanding the Authorization Interface

```csharp
public interface IDapperMaticPermissions
{
    /// <summary>
    /// Determines whether a user is authorized to perform a specific operation.
    /// </summary>
    /// <param name="context">The authorization context containing operation and resource details.</param>
    /// <returns>True if the user is authorized; otherwise, false.</returns>
    Task<bool> IsAuthorizedAsync(OperationContext context);
}
```

### The OperationContext: Complete Operation Information

The `OperationContext` provides rich information about the operation being performed, enabling fine-grained authorization decisions:

#### Core User and Request Information
```csharp
public class OperationContext
{
    // User authentication context
    public ClaimsPrincipal? User { get; set; }

    // HTTP request information
    public string HttpMethod { get; set; }           // GET, POST, etc.
    public string EndpointPath { get; set; }         // /api/dm/d/get
    public IQueryCollection? QueryParameters { get; set; }
    public object? RequestBody { get; set; }

    // Operation identification
    public string Operation { get; set; }            // e.g., "datasources/post", "tables/put"
}
```

#### Database Resource Context
The context includes detailed information about the specific database resources being accessed:

```csharp
// Resource hierarchy - from database to specific object
public string? DatasourceId { get; set; }      // Database/datasource name
public string? SchemaName { get; set; }        // Database schema
public string? TableName { get; set; }         // Table name
public string? ViewName { get; set; }          // View name
public string? ColumnName { get; set; }        // Column name
public string? IndexName { get; set; }         // Index name
public string? ConstraintName { get; set; }    // Constraint name
```

#### Extensibility and Utility Methods
```csharp
// Custom properties for additional authorization data
public Dictionary<string, object> Properties { get; set; }

// Utility methods for easy access
public T? GetRequest<T>() where T : class           // Get typed request body
public string? GetQueryParameter(string key)       // Get query parameter value
```

### Available Operations

DapperMatic defines specific operation identifiers for all supported operations:

#### Datasource Operations
- `datasources/list` - List all datasources
- `datasources/get` - Get specific datasource details
- `datasources/post` - Create new datasource
- `datasources/put` - Modify existing datasource
- `datasources/delete` - Delete datasource
- `datasources/test` - Test datasource connection

#### Database Schema Operations
- `schemas/list` - List schemas in datasource
- `schemas/get` - Get schema details
- `schemas/post` - Create new schema
- `schemas/delete` - Delete schema
- `schemas/exists` - Check schema existence

#### Table and View Operations
- `tables/list`, `tables/get`, `tables/post`, `tables/put`, `tables/delete`, `tables/exists`, `tables/query`
- `views/list`, `views/get`, `views/post`, `views/put`, `views/delete`, `views/exists`, `views/query`

#### Column and Index Operations
- `columns/list`, `columns/get`, `columns/post`, `columns/put`, `columns/delete`
- `indexes/list`, `indexes/get`, `indexes/post`, `indexes/delete`

#### Constraint Operations
- `constraints/primarykey/*` - Primary key operations
- `constraints/foreignkeys/*` - Foreign key operations
- `constraints/checks/*` - Check constraint operations
- `constraints/uniques/*` - Unique constraint operations
- `constraints/defaults/*` - Default constraint operations

### Advanced Authorization Examples

#### 1. Resource-Based Authorization
```csharp
public class ResourceBasedPermissions : IDapperMaticPermissions
{
    private readonly IUserPermissionService _permissionService;

    public async Task<bool> IsAuthorizedAsync(OperationContext context)
    {
        var user = context.User;
        if (user?.Identity?.IsAuthenticated != true)
            return false;

        // Check datasource-level permissions
        if (!string.IsNullOrEmpty(context.DatasourceId))
        {
            var canAccessDatasource = await _permissionService
                .CanUserAccessDatasourceAsync(user.Identity.Name, context.DatasourceId);
            if (!canAccessDatasource)
                return false;
        }

        // Different permissions for different operations
        return context.Operation switch
        {
            // Read operations - require read permission
            var op when OperationIdentifiers.IsGetOperation(op) =>
                await _permissionService.HasPermissionAsync(user.Identity.Name, "database:read"),

            // Schema modifications - require admin permission
            var op when op.StartsWith("schemas/") && !OperationIdentifiers.IsGetOperation(op) =>
                await _permissionService.HasPermissionAsync(user.Identity.Name, "database:admin"),

            // Table operations - require write permission
            var op when op.StartsWith("tables/") && !OperationIdentifiers.IsGetOperation(op) =>
                await _permissionService.HasPermissionAsync(user.Identity.Name, "database:write"),

            // Datasource management - require full admin
            var op when op.StartsWith("datasources/") && !OperationIdentifiers.IsGetOperation(op) =>
                user.IsInRole("SuperAdmin"),

            _ => false
        };
    }
}
```

#### 2. Multi-Tenant Authorization
```csharp
public class MultiTenantPermissions : IDapperMaticPermissions
{
    public async Task<bool> IsAuthorizedAsync(OperationContext context)
    {
        var userTenantId = context.User?.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrEmpty(userTenantId))
            return false;

        // Extract tenant from datasource name (e.g., "tenant123_production")
        if (!string.IsNullOrEmpty(context.DatasourceId))
        {
            var datasourceTenantId = context.DatasourceId.Split('_')[0];
            if (datasourceTenantId != userTenantId)
                return false; // Cross-tenant access denied
        }

        // Additional business logic...
        return await CheckTenantSpecificPermissions(context);
    }
}
```

#### 3. Environment and Time-Based Authorization
```csharp
public class EnvironmentAwarePermissions : IDapperMaticPermissions
{
    public async Task<bool> IsAuthorizedAsync(OperationContext context)
    {
        // Deny production modifications during business hours
        if (context.DatasourceId?.Contains("production") == true &&
            !OperationIdentifiers.IsGetOperation(context.Operation))
        {
            var currentTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));

            if (currentTime.Hour >= 9 && currentTime.Hour <= 17)
                return context.User?.IsInRole("ProductionAdmin") == true;
        }

        // Check table-specific permissions
        if (!string.IsNullOrEmpty(context.TableName))
        {
            // Deny access to sensitive tables
            var sensitiveTables = new[] { "users", "payments", "audit_logs" };
            if (sensitiveTables.Contains(context.TableName.ToLower()) &&
                !context.User?.IsInRole("DataProtectionOfficer") == true)
                return false;
        }

        return true;
    }
}
```

### Default Permission Implementation

DapperMatic includes a flexible default implementation that supports common authorization patterns:

```csharp
public class DefaultDapperMaticPermissions : IDapperMaticPermissions
{
    public async Task<bool> IsAuthorizedAsync(OperationContext context)
    {
        // Supports three modes:
        // 1. AllowAll - No restrictions (development/testing)
        // 2. RequireAuthentication - Must be authenticated
        // 3. RequireRole - Must have specific role

        // Automatic read-only role support:
        // - Users with ReadOnlyRole can perform get/list operations
        // - Users with RequireRole can perform all operations
    }
}
```

Configure the default implementation through options:

```csharp
builder.Services.Configure<DapperMaticOptions>(options =>
{
    options.RequireAuthentication = true;
    options.RequireRole = "DatabaseAdmin";      // Full access role
    options.ReadOnlyRole = "DatabaseReader";    // Read-only role
});
```

## Custom Authorization Registration

Register your custom authorization implementation:

```csharp
// Replace the default implementation
builder.Services.AddScoped<IDapperMaticPermissions, MyCustomPermissions>();

// Or configure DapperMatic after registration
builder.Services.AddDapperMatic();
builder.Services.AddScoped<IDapperMaticPermissions, MyCustomPermissions>();
```

## Audit Logging with IDapperMaticAuditLogger

DapperMatic provides comprehensive audit logging for compliance and security monitoring:

### Audit Event Information

Every operation generates a detailed audit event:

```csharp
public class DapperMaticAuditEvent
{
    // User and operation identification
    public string UserIdentifier { get; set; }        // User ID/name
    public string Operation { get; set; }             // Operation performed
    public bool Success { get; set; }                 // Operation result
    public string? ErrorMessage { get; set; }         // Error details if failed
    public DateTimeOffset Timestamp { get; set; }     // When operation occurred

    // Resource hierarchy
    public string? DatasourceId { get; set; }
    public string? SchemaName { get; set; }
    public string? TableName { get; set; }
    public string? ViewName { get; set; }
    public string? ColumnName { get; set; }
    public string? IndexName { get; set; }
    public string? ConstraintName { get; set; }

    // Request correlation
    public string? RequestId { get; set; }            // Request correlation ID
    public string? IpAddress { get; set; }            // Client IP address

    // Extensibility
    public Dictionary<string, object> Properties { get; set; }
}
```

### Custom Audit Implementation

```csharp
public class DatabaseAuditLogger : IDapperMaticAuditLogger
{
    private readonly IDbConnection _auditDb;
    private readonly ILogger<DatabaseAuditLogger> _logger;

    public async Task LogOperationAsync(DapperMaticAuditEvent auditEvent)
    {
        try
        {
            // Log to database for compliance
            await _auditDb.ExecuteAsync(@"
                INSERT INTO AuditLogs
                (UserIdentifier, Operation, DatasourceId, SchemaName, TableName,
                 Success, ErrorMessage, Timestamp, IpAddress, RequestId)
                VALUES
                (@UserIdentifier, @Operation, @DatasourceId, @SchemaName, @TableName,
                 @Success, @ErrorMessage, @Timestamp, @IpAddress, @RequestId)",
                auditEvent);

            // Also log to application logs for monitoring
            if (auditEvent.Success)
            {
                _logger.LogInformation(
                    "User {User} successfully performed {Operation} on {Resource}",
                    auditEvent.UserIdentifier,
                    auditEvent.Operation,
                    FormatResourcePath(auditEvent));
            }
            else
            {
                _logger.LogWarning(
                    "User {User} failed to perform {Operation} on {Resource}: {Error}",
                    auditEvent.UserIdentifier,
                    auditEvent.Operation,
                    FormatResourcePath(auditEvent),
                    auditEvent.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log audit event for operation {Operation}",
                auditEvent.Operation);
        }
    }

    private static string FormatResourcePath(DapperMaticAuditEvent evt)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(evt.DatasourceId)) parts.Add($"datasource:{evt.DatasourceId}");
        if (!string.IsNullOrEmpty(evt.SchemaName)) parts.Add($"schema:{evt.SchemaName}");
        if (!string.IsNullOrEmpty(evt.TableName)) parts.Add($"table:{evt.TableName}");
        if (!string.IsNullOrEmpty(evt.ViewName)) parts.Add($"view:{evt.ViewName}");
        return string.Join("/", parts);
    }
}
```

### Register Custom Audit Logger

```csharp
builder.Services.AddScoped<IDapperMaticAuditLogger, DatabaseAuditLogger>();
```

## Connection String Encryption

DapperMatic automatically encrypts connection strings using AES-256 encryption when stored in persistent repositories:

```csharp
builder.Services.Configure<DapperMaticOptions>(options =>
{
    // Use environment variable for production
    options.ConnectionStringEncryptionKey = Environment.GetEnvironmentVariable("DAPPERMATIC_ENCRYPTION_KEY");

    // Or configure directly (base64-encoded 256-bit key)
    // options.ConnectionStringEncryptionKey = "your-base64-encoded-256-bit-key";
});
```

**Security Best Practices:**
- Generate encryption keys using cryptographically secure random generators
- Store encryption keys in secure configuration (Azure Key Vault, AWS Secrets Manager, etc.)
- Rotate encryption keys periodically
- Never commit encryption keys to source control

## Security Best Practices

### 1. Principle of Least Privilege
```csharp
public async Task<bool> IsAuthorizedAsync(OperationContext context)
{
    // Start with deny-by-default
    if (context.User?.Identity?.IsAuthenticated != true)
        return false;

    // Grant minimum necessary permissions
    return context.Operation switch
    {
        var op when OperationIdentifiers.IsGetOperation(op) => HasReadPermission(context),
        var op when IsDataModification(op) => HasWritePermission(context),
        var op when IsSchemaModification(op) => HasAdminPermission(context),
        _ => false // Deny unknown operations
    };
}
```

### 2. Defense in Depth
- **Application-level**: DapperMatic authorization
- **Network-level**: API gateway, firewall rules
- **Database-level**: Database user permissions
- **Infrastructure-level**: VPC, security groups

### 3. Monitoring and Alerting
```csharp
public async Task LogOperationAsync(DapperMaticAuditEvent auditEvent)
{
    await base.LogOperationAsync(auditEvent);

    // Alert on suspicious activities
    if (IsHighRiskOperation(auditEvent))
    {
        await _alertingService.SendSecurityAlertAsync(
            $"High-risk operation {auditEvent.Operation} by {auditEvent.UserIdentifier}");
    }
}

private bool IsHighRiskOperation(DapperMaticAuditEvent evt) =>
    evt.Operation.Contains("drop") ||
    evt.Operation.Contains("remove") ||
    (evt.DatasourceId?.Contains("production") == true && !evt.Success);
```

### 4. Rate Limiting and Throttling
Implement rate limiting to prevent abuse:

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("DapperMatic", limiterOptions =>
    {
        limiterOptions.PermitLimit = 100;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
    });
});

// Apply to DapperMatic endpoints
app.MapGroup("/api/dm").RequireRateLimiting("DapperMatic");
```

## Complete Security Configuration Example

```csharp
var builder = WebApplication.CreateBuilder(args);

// Authentication
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options => {
        options.Authority = "https://your-identity-server.com";
        options.Audience = "dappermatic-api";
    });

// DapperMatic with security
builder.Services.Configure<DapperMaticOptions>(options =>
{
    options.RequireAuthentication = true;
    options.RequireRole = "DatabaseAdmin";
    options.ReadOnlyRole = "DatabaseReader";
    options.ConnectionStringEncryptionKey = Environment.GetEnvironmentVariable("ENCRYPTION_KEY");
    options.BasePath = "/api/database";
});

builder.Services.AddDapperMatic(config =>
    config.UseFileDatasourceRepository("/secure/path/datasources.json"));

// Custom security implementations
builder.Services.AddScoped<IDapperMaticPermissions, MyCustomPermissions>();
builder.Services.AddScoped<IDapperMaticAuditLogger, MyAuditLogger>();

// Rate limiting
builder.Services.AddRateLimiter(options => {
    options.AddFixedWindowLimiter("DapperMatic", limiterOptions => {
        limiterOptions.PermitLimit = 100;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
    });
});

var app = builder.Build();

// Security middleware order is critical
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// DapperMatic endpoints
app.UseDapperMatic();

app.Run();
```

This comprehensive security configuration provides enterprise-grade protection for your database management endpoints while maintaining flexibility for custom authorization logic.