# Tenants

Products keep tenants' data in many ways:
- a tenant key on shared tables, sometimes with global rows every tenant reads;
- a schema per tenant;
- a database per tenant;
- a tree of tenants, where each one sees the tenants below it;
- any mix of these, such as dozens of shared schemas and one that is not.

DapperMatic assumes none of them. It finds the request's tenant, learns what the application knows of it, and holds the request to it. [Policies](/guide/web-api/security) and the database's own row-level security can then read the tenant as values.

## Finding the tenant

```csharp
builder.Services.AddDapperMatic(dm => dm.Tenancy(t => t
    .FromClaim("tenant_id")                       // a claim of the signed-in caller
    .FromSubdomain("{tenant}.app.example.com")    // or the host
    .FromRoute("tenant")                          // or a route value
    .FromHeader("X-Tenant")                       // or a header
    .FromQuery("tenant")));                       // or a query value
```

The sources are tried in order, and the first that has a value wins. A client chooses its own headers, query values, routes and host name (the subdomain comes from the `Host` header): use those only where something else checks that the caller belongs to the tenant, such as a policy, a gateway or the database.

Where a column marks a tenant's rows in shared tables, `RowsMarkedBy("tenant_id")` (with `RowsMarkedBy("invoices", "org_id")` for a table using another) lets a tenant's own administrators keep policies held to their tenant's rows; see [Tenant administrators](/guide/web-api/security#tenant-administrators).

A request without a tenant has none: `RequireTenant()` refuses it. Without that, a policy reading `@tenant.id` holds for no row, and a preset of it refuses the write (see [Rows and columns](/guide/web-api/security#rows-and-columns)).

Any other source is written in code. The application's services are available, and so is the HTTP context:

```csharp
dm.Tenancy(t => t.From(async (http, cancellationToken) =>
{
    var sessions = http.RequestServices.GetRequiredService<ISessionStore>();
    var session = await sessions.FindAsync(http.Request.Cookies["sid"], cancellationToken);
    return session?.TenantId;
}));
```

Sources in code are tried before the others. `RequireTenant()` refuses any request for a datasource that has no tenant.

## What is known of a tenant

A tenant can have its own datasource, its own schema, and values:

```csharp
dm.Tenancy(t => t
    .FromClaim("tenant_id")
    .Map("acme", schema: "tenant_acme")                    // a schema of its own
    .Map("globex", datasource: "globex-db")                // a database of its own
    .Lookup(async (tenant, http, cancellationToken) =>     // or found in code, from anywhere
    {
        var directory = http.RequestServices.GetRequiredService<ITenantDirectory>();
        var entry = await directory.GetAsync(tenant.ToString(), cancellationToken);
        tenant.SchemaName = entry?.Schema;
        tenant.Values["plan"] = entry?.Plan;
    })
    .ValueFromSql("below", "SELECT descendant_id FROM tenant_tree WHERE ancestor_id = @tenant.id")
    .Value("regions", async (context, cancellationToken) =>
        await context.Services!.GetRequiredService<IRegions>().ForTenantAsync(context.Tenant!.Id, cancellationToken)));
```

- **A datasource or schema** of the tenant's holds the request to it. A request naming another datasource or schema, in its URL, body (a new schema) or query (`/access/effective`), is refused. A URL naming no schema uses the tenant's.
- **Values** are read by policies and session values as `@tenant.name`. `ValueFromSql` runs its query once per request, when the value is first read, with the request's values as parameters, in the datasource it names or else the one the request is for (once for each, if one caller reads several). One row gives a value; several give a list. `Value` computes one in code.

The same in configuration:

```json
{
  "DapperMatic": {
    "Tenancy": {
      "Sources": [ { "Claim": "tenant_id" }, { "Subdomain": "{tenant}.app.example.com" } ],
      "Tenants": {
        "acme": { "Schema": "tenant_acme" },
        "globex": { "Datasource": "globex-db", "Values": { "region": "eu" } }
      },
      "Values": {
        "below": { "Sql": "SELECT descendant_id FROM tenant_tree WHERE ancestor_id = @tenant.id" }
      },
      "RequireTenant": false
    }
  }
}
```

## Values of the request

Besides the tenant's, a policy can read values of the request itself, computed once when first read:

```csharp
dm.Value("teams", async (context, cancellationToken) =>
    await context.Services!.GetRequiredService<ITeams>().OfUserAsync(context.User, cancellationToken));

dm.Policy(DmPolicy.Allow(DmAction.Read, "authenticated").OnTables("projects").Where("team_id in @values.teams"));
```

## The same tenant in typed code

The application's own code, using the typed DML methods, can take the tenant, user and schema from the same tenancy, configured once:

```csharp
builder.Services.AddHttpContextAccessor();
builder.Services.AddDapperMatic(dm => dm.Tenancy(t => t.FromClaim("tenant_id").Map("acme", schema: "ten_acme")));

app.UseAuthentication();
app.UseDapperMaticCallerValues();   // DmDefaults.TenantId, User and TenantSchema read each request's caller
```

Each request's caller is worked out before the application's endpoints run: place it after authentication, and after routing when tenants come from the route. `DmDefaults.User` is the caller's id (`@user.id`). A scope's own `ForTenant`, `WithUser` and `InSchema` still win. `UseDapperMaticRequestValues(tenant: http => ...)` remains for values worked out some other way.

## Patterns

| How the data is kept | What to configure |
|----------------------|-------------------|
| A tenant key on shared tables | A policy `OnTablesWithColumns("tenant_id")` with `.Where("tenant_id eq @tenant.id")` and `.Preset("tenant_id", "@tenant.id")` |
| Global rows every tenant reads | `.Where("tenant_id eq null or tenant_id eq @tenant.id")` |
| A tree of tenants | A value listing the tenant and those below it (`ValueFromSql` over a closure table, a path, or a recursive query), and `.Where("tenant_id in @tenant.below")` |
| A schema per tenant | `Map(id, schema: ...)`, or `Lookup` to set `tenant.SchemaName` |
| A database per tenant | `Map(id, datasource: ...)`, or `Lookup` to set `tenant.DatasourceId` |
| Shared schemas and tenant schemas together | Policies narrowed with `InSchemas` and `ExceptSchemas` |

## The database's own row-level security

PostgreSQL and SQL Server enforce row-level security themselves. MySQL can, through views and functions. DapperMatic sets values on each connection's session before a request uses it, so the database's policies can read the request's tenant and caller:

```csharp
dm.SessionValue("app.tenant_id", "@tenant.id")
  .SessionValue("app.below", "@tenant.below")      // a list is joined by commas
  .SessionValue("app.user_id", "@user.id");
```

| Provider | How it is set | How a policy reads it |
|----------|---------------|-----------------------|
| PostgreSQL | `set_config` (names need a prefix, as `app.tenant_id`) | `current_setting('app.tenant_id', true)` |
| SQL Server | `sp_set_session_context`, read-only | `SESSION_CONTEXT(N'app.tenant_id')` |
| MySQL, MariaDB | User variables; the connection string needs `AllowUserVariables=True` | `@tenant_id`, from a function (views cannot read variables) |
| SQLite | Not supported: SQLite has no sessions | |

For example, on PostgreSQL:

```sql
ALTER TABLE notes ENABLE ROW LEVEL SECURITY;
CREATE POLICY notes_tenant ON notes USING (
    tenant_id IS NULL
    OR tenant_id = ANY (string_to_array(current_setting('app.below', true), ',')::int[]));
```

[Cached query results](./setup.md#caching-query-results) are kept per session values, so callers the database filters differently never share them.

The application must connect as a role the database's policies apply to: a PostgreSQL superuser or table owner bypasses them unless the table forces row-level security. Each driver's pool resets the session when a connection is returned, unless the connection string turns that off.

Anything else to run on a connection before the request uses it goes in `OnConnectionOpened`:

```csharp
dm.OnConnectionOpened(async (connection, access, cancellationToken) =>
{
    if (access.Tenant is { } tenant)
    {
        await connection.ExecuteAsync("SET ROLE tenant_app");
    }
});
```

DapperMatic's policies and the database's own can be used together.
