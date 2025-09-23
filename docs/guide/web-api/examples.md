# Integration Examples

Real-world examples of how to integrate DapperMatic Web API into your applications.

## Example 1: Admin Dashboard

Create an admin panel for database management:

```csharp
// Program.cs
using MJCZone.DapperMatic.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add your existing services
builder.Services.AddRazorPages();
builder.Services.AddAuthentication("Identity.Application");

// Add DapperMatic
builder.Services.AddDapperMatic()
    .UseFileRepository(options => {
        options.FilePath = "datasources.json";
        options.EncryptionKey = builder.Configuration["DapperMatic:EncryptionKey"];
    });

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

// Add DapperMatic endpoints
app.UseDapperMatic();

app.Run();
```

## Example 2: Multi-Tenant SaaS

Manage separate databases for different tenants:

```csharp
public class TenantPermissionsService : IDapperMaticPermissions
{
    public async Task<bool> CanAccessDatasourceAsync(
        string datasourceName,
        ClaimsPrincipal user,
        string operation)
    {
        var tenantId = user.FindFirst("tenant_id")?.Value;

        // Only allow access to datasources that belong to the user's tenant
        return datasourceName.StartsWith($"tenant_{tenantId}_");
    }
}

// In Program.cs
builder.Services.AddDapperMatic()
    .UseDatabaseRepository(connectionString, "SqlServer")
    .AddCustomPermissions<TenantPermissionsService>();
```

## Example 3: Database Migration Tool

Build a tool for managing database migrations:

```javascript
// Frontend JavaScript
async function migrateDatabases() {
    // Get list of all datasources
    const response = await fetch('/api/dm/d/', {
        method: 'GET',
        headers: { 'Content-Type': 'application/json' }
    });

    const datasources = await response.json();

    // Apply schema changes to each datasource
    for (const datasource of datasources.data) {
        await createMigrationTable(datasource.name);
        await applyPendingMigrations(datasource.name);
    }
}

async function createMigrationTable(datasourceName) {
    await fetch(`/api/dm/d/${datasourceName}/t/`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            table: {
                schemaName: null, // default schema
                tableName: 'migrations',
                columns: [
                    { columnName: 'id', dataType: 'bigint', isAutoIncrement: true },
                    { columnName: 'version', dataType: 'varchar', maxLength: 255 },
                    { columnName: 'applied_at', dataType: 'datetime' }
                ]
            }
        })
    });
}
```

## Example 4: Database Backup Service

Automate database schema backups:

```csharp
public class SchemaBackupService : BackgroundService
{
    private readonly IDapperMaticService _dapperMaticService;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await BackupAllSchemas();
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task BackupAllSchemas()
    {
        var datasources = await _dapperMaticService.ListDatasourcesAsync();

        foreach (var datasource in datasources)
        {
            var tables = await _dapperMaticService.ListTablesAsync(datasource.Name);
            var backup = new SchemaBackup
            {
                DatasourceName = datasource.Name,
                Tables = tables,
                BackupDate = DateTime.UtcNow
            };

            await SaveBackupToStorage(backup);
        }
    }
}
```

## Example 5: API Client

Create a typed client for consuming the API:

```csharp
public class DapperMaticClient
{
    private readonly HttpClient _httpClient;

    public DapperMaticClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<DatasourceDto>> GetDatasourcesAsync()
    {
        var response = await _httpClient.GetAsync("/api/dm/d/");

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<IEnumerable<DatasourceDto>>>();
        return result.Data;
    }

    public async Task<bool> CreateTableAsync(string datasource, TableDto table)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(new { table }),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync(
            $"/api/dm/d/{datasource}/t/",
            content);

        return response.IsSuccessStatusCode;
    }
}

// Register the client
builder.Services.AddHttpClient<DapperMaticClient>(client => {
    client.BaseAddress = new Uri("https://your-api.com");
});
```

*More examples and detailed integration scenarios coming soon...*