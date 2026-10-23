# Integration Examples

Real-world examples of how to integrate DapperMatic Web API into your applications.

## Example 1: Admin Dashboard

Create an admin panel for database management:

```csharp
// Program.cs
using MJCZone.DapperMatic.AspNetCore;
using MJCZone.DapperMatic.Security;

var builder = WebApplication.CreateBuilder(args);

// Add your existing services
builder.Services.AddRazorPages();
builder.Services.AddAuthentication("Identity.Application");

// Add DapperMatic
builder.Services.Configure<DapperMaticOptions>(options => {
    options.ConnectionStringEncryptionKey = builder.Configuration["DapperMatic:EncryptionKey"];
});
builder.Services.AddDapperMatic(config => config
    .AddProvider(SqlClientFactory.Instance)
    .UseFileDatasourceRepository("datasources.json")
    .Policy(DmPolicy.Allow(DmAction.All, "DatabaseAdmin"))
    .Policy(DmPolicy.Allow(DmAction.Read | DmAction.ReadSchema, "authenticated")));

var app = builder.Build();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

// Add DapperMatic endpoints
app.UseDapperMatic();

app.Run();
```

## Example 2: Multi-Tenant SaaS

Hold each tenant to its own rows in shared tables, with a database of their own for the tenants that have one:

```csharp
builder.Services.AddDapperMatic(config => config
    .AddProvider(SqlClientFactory.Instance)
    .UseDatabaseDatasourceRepository("SqlServer", connectionString)
    .Tenancy(t => t
        .FromClaim("tenant_id")
        .Map("globex", datasource: "globex-db"))
    .Policy(DmPolicy.Allow(DmAction.ReadWrite, "authenticated")
        .OnTablesWithColumns("tenant_id")
        .Where("tenant_id eq @tenant.id")
        .Preset("tenant_id", "@tenant.id")));
```

See [Tenants](/guide/web-api/tenancy) for every way of finding a tenant and keeping its data.

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
    for (const datasource of datasources.result) {
        await createMigrationTable(datasource.id);
        await applyPendingMigrations(datasource.id);
    }
}

async function createMigrationTable(datasourceName) {
    await fetch(`/api/dm/d/${datasourceName}/t/`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            schemaName: null, // default schema
            tableName: 'migrations',
            columns: [
                { columnName: 'id', providerDataType: 'bigint', isPrimaryKey: true, isAutoIncrement: true },
                { columnName: 'version', providerDataType: 'varchar', length: 255 },
                { columnName: 'applied_at', providerDataType: 'datetime' }
            ]
        })
    });
}
```

## Example 4: Database Backup Service

Automate database schema backups through the API, as a caller the policies let read definitions (here, an API key's
service account):

```csharp
public class SchemaBackupService(IHttpClientFactory clients) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await BackupAllSchemas(stoppingToken);
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task BackupAllSchemas(CancellationToken cancellationToken)
    {
        var api = clients.CreateClient("dappermatic"); // BaseAddress and X-Api-Key set where it is registered
        var datasources = await api.GetFromJsonAsync<DatasourceListResponse>("/api/dm/d/", cancellationToken);

        foreach (var datasource in datasources!.Result)
        {
            var tables = await api.GetFromJsonAsync<TableListResponse>($"/api/dm/d/{datasource.Id}/t/?include=columns,indexes", cancellationToken);
            var backup = new SchemaBackup
            {
                DatasourceId = datasource.Id,
                Tables = tables!.Result,
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

        var result = await response.Content.ReadFromJsonAsync<DatasourceListResponse>();
        return result.Result;
    }

    public async Task<bool> CreateTableAsync(string datasource, TableDto table)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(table),
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