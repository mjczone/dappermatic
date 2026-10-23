# Setup & Configuration

This guide covers how to configure the DapperMatic Web API integration: drivers, where datasources are kept, which endpoints are served, errors, limits and caching. Who may do what is covered in [Security & Policies](/guide/web-api/security) and [Tenants](/guide/web-api/tenancy).

## Database drivers

The package bundles no database driver. Register the drivers your datasources use, each from its own package:

```csharp
using Microsoft.Data.SqlClient;   // Microsoft.Data.SqlClient
using Microsoft.Data.Sqlite;      // Microsoft.Data.Sqlite
using MySqlConnector;             // MySqlConnector
using Npgsql;                     // Npgsql

builder.Services.AddDapperMatic(dm => dm
    .AddProvider(SqlClientFactory.Instance)
    .AddProvider(NpgsqlFactory.Instance)
    .AddProvider(MySqlConnectorFactory.Instance)
    .AddProvider(SqliteFactory.Instance));
```

The provider each driver serves is read from its type. A datasource whose provider has no registered driver cannot connect, and adding one through the API is refused with a message naming the package to install. Another driver for a provider can be registered with `AddProvider(DbProviderType.MySql, factory)`.

::: tip Why no bundled drivers
An application installs only the drivers it uses, and no driver's licence is imposed on it: Oracle's MySql.Data is GPL, while MySqlConnector, Npgsql and both Microsoft drivers are MIT or similar.
:::

## Repository Configuration

DapperMatic supports multiple datasource repository implementations:

### In-Memory Repository
Perfect for development and testing, and for datasources that come from configuration:

```csharp
builder.Services.AddDapperMatic(); // Uses in-memory repository by default
```

It needs no encryption key: connection strings are encrypted with a key made for the process, which nothing outlives.

### File-Based Repository
Stores datasources in a JSON file, with their connection strings encrypted:

```csharp
builder.Services.AddDapperMatic(config =>
    config.UseFileDatasourceRepository("/path/to/datasources.json"));
```

### Database Repository
Stores datasources in a database table, with their connection strings encrypted:

```csharp
builder.Services.AddDapperMatic(config =>
    config.UseDatabaseDatasourceRepository("SqlServer", connectionString));
```

The file and database repositories refuse to start without `ConnectionStringEncryptionKey`.

### Encryption keys and rotation

The key is a base64-encoded 256-bit key, such as `CryptoUtils.GenerateEncryptionKey()` returns. Each encrypted connection string is prefixed with the id of the key that encrypted it. To rotate the key, make the new one current and keep the old one in `PreviousConnectionStringEncryptionKeys`: connection strings it encrypted are still read, and each is encrypted with the current key the next time it is written.

```json
{
  "DapperMatic": {
    "ConnectionStringEncryptionKey": "new-base64-key",
    "PreviousConnectionStringEncryptionKeys": [ "old-base64-key" ]
  }
}
```

### Configuration-Based Datasources
Configure datasources directly in `appsettings.json` for easy startup:

```json
{
  "DapperMatic": {
    "BasePath": "/api/dm",
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
builder.Services.AddDapperMatic(dm => dm.AddProvider(SqlClientFactory.Instance).AddProvider(NpgsqlFactory.Instance));
builder.Services.Configure<DapperMaticOptions>(builder.Configuration.GetSection("DapperMatic"));
```

With datasources from configuration, the datasource endpoints can be left out altogether: see [Choosing the endpoints](#choosing-the-endpoints).

## Datasources added through the API

A connection string is code that runs on your server: it can name any host, ask the driver to read local files, or point SQLite at any path. Datasources added or changed through the API are checked against `ConnectionPolicy`; datasources from code or configuration are trusted.

```csharp
builder.Services.Configure<DapperMaticOptions>(options =>
{
    options.ConnectionPolicy.AllowedProviders.AddRange(["PostgreSql", "SqlServer"]); // empty: any registered driver
    options.ConnectionPolicy.AllowedHosts.Add("*.db.internal.example.com");         // empty: any host
    options.ConnectionPolicy.SqliteDirectory = "/var/data/sqlite";                   // null: in-memory SQLite only
});
```

Always refused unless allowed: MySQL's `AllowLoadLocalInfile`, which lets the database server ask for files on your machine (`AllowLoadLocalInfile = true` in the policy allows it), and SQL Server's `AttachDbFilename`. A relative SQLite path is taken inside `SqliteDirectory`, and one that leads outside it is refused.

## Choosing the endpoints

`Endpoints` says which sets are served. The default, `DapperMaticEndpoints.Default`, is every set but `Mcp`, which you turn on yourself; `All` is every set.

| Set | Endpoints |
|-----|-----------|
| `Datasources` | Listing, adding, changing and removing datasources, and testing connections |
| `DataTypes` | Each provider's data types |
| `Schemas` | Schemas, tables, columns, indexes, constraints and views: reading their definitions and changing them |
| `Query` | Querying rows of tables and views; each caller's OpenAPI document |
| `Rows` | Inserting, updating and deleting rows; batches |
| `Access` | The [policies, tenants and webhooks DapperMatic keeps](/guide/web-api/security#policies-and-tenants-kept-by-dappermatic), and effective permissions |
| `Functions` | Running [declared functions](./endpoints.md#functions) |
| `Realtime` | [Subscriptions](./changes.md#realtime-subscriptions) to queries |
| `Mcp` | The [MCP endpoint](./mcp) for AI assistants |

```csharp
// A data API over datasources from configuration: no datasource management, no schema changes.
builder.Services.Configure<DapperMaticOptions>(options =>
    options.Endpoints = DapperMaticEndpoints.Query | DapperMaticEndpoints.Rows);
```

[GraphQL](./graphql) is not one of these sets: it comes with its own package, and is served when `AddDapperMaticGraphQL()` adds it.

## Authorization, rate limiting and CORS

Every DapperMatic endpoint is mapped into one route group, which `UseDapperMatic` hands to you. Apply ASP.NET Core's own conventions to it:

```csharp
app.UseDapperMatic(group => group
    .RequireAuthorization("DatabaseAdmins")
    .RequireRateLimiting("api")
    .RequireCors("admin-ui"));

// Or map the endpoints yourself:
app.MapDapperMaticEndpoints().RequireAuthorization();
```

These run before DapperMatic's own [permissions and policies](/guide/web-api/security), which decide what an authorized caller may do with each table.

With [API keys or built-in accounts](/guide/web-api/authentication) turned on, an authorization policy on the group must accept their callers: name DapperMatic's scheme in it (`policy.AddAuthenticationSchemes(DapperMaticAuthenticationDefaults.Scheme)`), which signs callers in the same way DapperMatic's endpoints do. The endpoints that exist to sign in (`auth/login`, `auth/refresh`, `auth/register`, `auth/setup`, the email links, the published JWT keys) allow anonymous callers whatever the group requires.

## Errors

Errors are [problem details](https://www.rfc-editor.org/rfc/rfc9457) with a `code` and the request's `traceId`. Database errors are recognised by their codes on every provider and answered with a message that names no table, column, constraint or value:

| Database error | Status | `code` |
|----------------|--------|--------|
| Duplicate key or unique value | 409 | `unique_violation` |
| Missing or referenced row | 409 | `foreign_key_violation` |
| Missing required value | 400 | `not_null_violation` |
| Check constraint | 400 | `check_violation` |
| Value too long or too large | 400 | `value_out_of_range` |
| Value not of the column's type | 400 | `invalid_value` |
| Deadlock, timeout | 503, with `Retry-After` | `deadlock`, `timeout` |
| Database unreachable | 503 | `database_unavailable` |
| Anything else | 500 | `database_error` |

The database's own text is included only when `IncludeErrorDetails` is true, which by default it is in the Development environment only. A caller who is not signed in and is refused gets 401; one who is signed in gets 403.

The same classification is available in code, for any driver: `DmDatabaseErrors.Classify(exception)` returns a `DmDatabaseErrorKind`.

## Limits on writes

```csharp
builder.Services.Configure<DapperMaticOptions>(options =>
{
    options.QueryLimits.MaxInsertRows = 1_000;     // rows in one insert request (default 1,000)
    options.QueryLimits.MaxAffectedRows = 500;     // rows one update or delete may change (default: no limit)
});
```

An update or delete that would change more rows than `MaxAffectedRows` is rolled back and refused with 400. Any write can be tried first with `"dryRun": true`, which runs it, reports how many rows it changed, and rolls it back.

## Table descriptions are cached

Query and row requests read each table's description (columns, types, keys) from the database's catalog. It is kept for `SchemaCacheDuration`, five minutes by default; zero turns the cache off. DapperMatic's own schema endpoints clear a datasource's descriptions as soon as they change it. A change made outside DapperMatic is seen once the duration runs out, or at once after:

```csharp
app.Services.GetRequiredService<DapperMaticSchemaCache>().Invalidate("ProductionDB");
```

## Callers' models are kept

Each caller's OpenAPI document (and GraphQL schema) is built from what they may use of a datasource: its tables, the actions they may take and the columns they may read and write. That model is kept, shared by every caller alike in what the policies read: their roles, their values of the claims policies name, and their tenant. It is built again when the policies, the tenants kept through the API, or the schema change, and kept no longer than table descriptions (`SchemaCacheDuration`).

```csharp
options.CallerModelCacheSize = 1000;   // models kept; 0 keeps none
```

When code decides what callers may do (a policy's `When` predicate, or permissions of the application's own), DapperMatic cannot tell which callers are alike, and builds each model for every request, unless the application says:

```csharp
options.CallerModelCacheKey = operation => operation.User?.FindFirst("plan")?.Value;   // null: not kept
```

## ETags

Every successful `GET` response carries an `ETag` (a hash of its body) and `Cache-Control: private, no-cache`. A client that sends the tag back in `If-None-Match` gets `304 Not Modified` with no body while the response is unchanged. The body is still computed, so this saves bandwidth, not database work; for that, cache the results (below). Turn it off with `options.ETags = false`.

## Caching query results

Query results of the tables you list are kept, each table for its own time. Nothing is cached unless listed.

```csharp
builder.Services.Configure<DapperMaticOptions>(options =>
{
    options.CachedTables.Add(new DapperMaticCachedTable { TableName = "products", Duration = TimeSpan.FromMinutes(10) });
    options.CachedTables.Add(new DapperMaticCachedTable { TableName = "categories", Duration = TimeSpan.FromHours(1) });

    // A view: name the tables it reads, so that writes to them make its results stale.
    options.CachedTables.Add(new DapperMaticCachedTable { TableName = "product_sales", Duration = TimeSpan.FromMinutes(1), Reads = ["products", "sales"] });
});
```

`DatasourceId` and `SchemaName` narrow an entry to one datasource or schema; left out, it covers the table in all of them.

**Where results are kept.** Results go in the application's `IDistributedCache` when it registers one, else in memory in this process. To share them between instances, register Redis or Valkey:

```csharp
builder.Services.AddStackExchangeRedisCache(o => o.Configuration = "localhost:6379"); // Redis or Valkey
// or name a store for DapperMatic alone:
builder.Services.Configure<DapperMaticOptions>(o => o.ResultCacheStore = myCache);
```

**What a cached result is kept under.** Each statement has its own entry. The key is built from:

- the statement's SQL and its parameter values (these include the values the policies read);
- the session values the database's own row-level security reads;
- the datasource.

Callers with different policies, tenants or hidden columns therefore never share results. If `OnConnectionOpened` steps are configured, nothing is cached: DapperMatic cannot know what they set on the session.

A query is cached only when **every** table it reads is listed: its own table, plus the tables of the relations it follows in `$filter`, `$orderby` or `$expand`. Its time is the shortest of theirs. Statements inside a batch or a write are never cached.

**When kept results become stale.** Every listed table has a version in the store, and every write DapperMatic makes changes it, so results read before the write are never found again:

| Write | Results made stale |
|-------|--------------------|
| Row endpoints (`rows`, `rows/save`, `rows/update`, `rows/delete`) and batches | those reading the tables written, after the commit |
| Application code through `DapperMaticCallerDb` | those reading the tables written, when the request's response starts |
| Schema and datasource endpoints (tables, columns, views, ...) | every result of the datasource |

The versions live in the same store as the results, so a write on one instance is seen by every instance sharing a distributed store. A write made **outside** DapperMatic (another application, a SQL script, a database trigger) is seen when the table's time runs out, or at once after:

```csharp
var results = app.Services.GetRequiredService<DapperMaticResultCache>();
await results.InvalidateAsync("ProductionDB", "products", schemaName: null); // the table, in every schema
await results.InvalidateAsync("ProductionDB");                               // everything of the datasource
```

The `dappermatic.cache.requests` counter (OpenTelemetry, below) counts hits and misses.

## Webhooks, realtime and MCP

See [Webhooks & Realtime](./changes) and [AI Assistants (MCP)](./mcp).

## Tracing and metrics (OpenTelemetry)

DapperMatic reports through .NET's own `ActivitySource` and `Meter`, both named `DmTelemetry.Name` (`MJCZone.DapperMatic`), so it needs no OpenTelemetry package of its own. Collect them with the SDK the application already uses:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation().AddSource(DmTelemetry.Name).AddOtlpExporter())
    .WithMetrics(m => m.AddAspNetCoreInstrumentation().AddMeter(DmTelemetry.Name).AddOtlpExporter());
```

| What | Name | Contains |
|------|------|----------|
| Span per request | `DapperMatic {operation}`, e.g. `DapperMatic tables/query` | `dappermatic.operation`, `.datasource`, `.schema`, `.table`, `.tenant`, `http.response.status_code` |
| Span per statement, inside the request's | `{SELECT\|INSERT\|UPDATE\|DELETE} {table}` | `db.system.name`, `db.operation.name`, `db.collection.name`, `db.query.text`, rows returned or affected |
| Histogram | `dappermatic.request.duration` (ms) | by operation and status code |
| Histogram | `dappermatic.statement.duration` (ms) | by database and operation |
| Counter | `dappermatic.cache.requests` | statements looked for in the result cache, by `dappermatic.cache.result` (`hit`, `miss`) |

`db.query.text` is the SQL with its parameters by name (`@p0`), **never their values**: filter values, row contents and session values do not leave the process through telemetry.

## Client IP addresses in audit events

Audit events record the connection's remote address. Behind a proxy or load balancer, add ASP.NET Core's forwarded headers middleware with the proxies you trust; DapperMatic does not read `X-Forwarded-For` itself, since any client can send it.

```csharp
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
    o.KnownProxies.Add(IPAddress.Parse("10.0.0.10"));
});
app.UseForwardedHeaders();
```

## Custom Services

```csharp
builder.Services.AddDapperMatic(dm => dm
    .UseCustomPermissions<MyPermissions>()
    .UseCustomAuditLogger<MyAuditLogger>());
```

## Base path and type mapping

```csharp
builder.Services.Configure<DapperMaticOptions>(options => options.BasePath = "/api/database"); // default: /api/dm

// To configure Dapper type mapping, initialize it before the endpoints are mapped; otherwise the defaults are used.
DapperMaticTypeMapping.Initialize(new DapperMaticMappingOptions { StoreSimpleCollectionsAsJson = true });
app.UseDapperMatic(); // No UseRouting() call is needed first.
```
