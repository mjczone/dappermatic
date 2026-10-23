# Security & Policies

DapperMatic's endpoints refuse everything until a policy grants it. Policies say who may do what to which datasources, schemas, tables, rows and columns. They are given in code, in configuration, or both, and DapperMatic enforces them on every provider, the same way.

If the application starts with no policies and the default permissions, DapperMatic logs a warning: every request will be refused.

## Authentication

DapperMatic reads the caller from ASP.NET Core's authentication, from API keys, or from its built-in accounts: see [Sign-in](./authentication.md). With your own authentication, configure it as usual:

```csharp
builder.Services.AddAuthentication("Bearer").AddJwtBearer("Bearer", options =>
{
    options.Authority = "https://your-identity-server.com";
    options.Audience = "your-api";
});

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.UseDapperMatic();
```

Policies name roles. Two roles are built in: `anonymous` (any caller who is not signed in) and `authenticated` (any caller who is). Any other role is read from the caller's role claims (and from [other claims or code](./authentication.md#roles) you name). `*` means every caller.

A refused caller who is not signed in gets **401**. A refused caller who is signed in gets **403**.

## A first policy

```csharp
builder.Services.AddDapperMatic(dm => dm
    .Policy(DmPolicy.Allow(DmAction.Read, "*").OnTables("products", "categories"))
    .Policy(DmPolicy.Allow(DmAction.ReadWrite | DmAction.ReadSchema, "staff"))
    .Policy(DmPolicy.Allow(DmAction.All, "admin")));
```

Everyone may read the rows of `products` and `categories`. Staff may read and write rows, and read definitions, of every table. Admins may do everything. Nothing else is allowed.

## Actions

| Action | What it allows | Operations |
|--------|----------------|------------|
| `Read` | Read rows | `tables/query`, `views/query`; `openapi/list` (the [caller's own document](./endpoints.md#each-caller-s-own-openapi-document), narrowed table by table to every row action) |
| `Create` | Insert rows | `tables/insert-rows` |
| `Update` | Update rows | `tables/update-rows` |
| `Delete` | Delete rows | `tables/delete-rows` |
| `ReadSchema` | Read definitions: datasources, schemas, tables, views, columns, indexes, constraints, data types | every `get` and `list` |
| `ChangeSchema` | Create, change and drop schemas, tables, columns, indexes, constraints and views | every `post`, `put` and `delete` but datasources' |
| `ManageDatasources` | Add, change, remove and test datasources | `datasources/post`, `put`, `delete`, `test`; a grant confined to some schemas does not cover these, which act on the whole datasource |
| `ManageAccess` | Read and change the policies and tenants DapperMatic keeps | the policy and tenant endpoints |
| `DefineViews` | Write a view's SQL: create a view or change its definition, with `ChangeSchema` on the view | `views/post`, `views/put` with a definition; a grant confined to some schemas or tables does not cover it |
| `Execute` | Run a [declared function](./endpoints.md#functions) | `functions/post`; covered by a grant naming the function (`.OnFunctions("report_*")`), or one naming none that is confined to no schema or table. A policy naming functions grants `Execute` only |

`Write` is `Create | Update | Delete`, `ReadWrite` is `Read | Write`, and `All` is everything. An operation DapperMatic does not know is refused.

Some changes to a schema read what other tables hold, and need more than `ChangeSchema` on their own table:

- **A view's definition** is SQL that can read any table the connection can. Creating a view, or changing its definition, also needs `DefineViews` for the whole datasource: grant it only to callers you trust to read everything there.
- **A foreign key** tells whether a key exists in the table it references, and keeps that table's rows from being deleted. Creating one also needs `ReadSchema` on the referenced table, and `Read` of its key columns.
- **A rename** is authorized as the new name too, so a policy for `dev_*` tables cannot rename one out of its reach or into it.
- **A datasource added without an id** is covered only by a grant that names no datasources, or `*`.

## What a policy covers

| Narrowed by | Method | Meaning |
|-------------|--------|---------|
| Datasources | `OnDatasources("tenant-*")` | Ids or patterns with `*` and `?` |
| Schemas | `InSchemas("sales")`, `ExceptSchemas("admin")` | A request that names no schema is about its tenant's, or else the datasource's default (`dbo`, `public`). `_` is the schema of providers without schemas (MySQL, SQLite) |
| Tables and views | `OnTables("order*")`, `ExceptTables("audit*")` | |
| Tables having columns | `OnTablesWithColumns("tenant_id")` | Row actions only: one policy covers every table with a `tenant_id`, in whatever schema |
| Roles | `DmPolicy.Allow(actions, "staff", "admin")` | `anonymous`, `authenticated`, `*`, or role names |
| Claims | `.WithClaim("plan", "pro")` | The caller must have the claim; `*` for any value |
| Code | `.When(ctx => ...)` | A predicate over the caller, the tenant and the request's services |

A policy that names nothing covers everything. A policy that grants must say who it grants to: roles, claims or a predicate. A grant to nobody in particular is refused at startup.

Listing datasources, schemas, tables or views returns only those the caller may read the definition of.

## Rows and columns

```csharp
DmPolicy.Allow(DmAction.ReadWrite, "authenticated")
    .OnTablesWithColumns("tenant_id")
    .Where("tenant_id eq @tenant.id")         // the rows the caller reads, updates and deletes
    .Check("status ne 'archived'")            // what every row written must satisfy
    .Preset("tenant_id", "@tenant.id")        // written by the policy, whatever the request says
    .ExceptColumns("password_hash");          // kept from the caller: not read, not written
```

- **`Where`** is a condition in the [query syntax](/guide/web-api/endpoints#querying-and-writing-rows). It limits every read, update and delete, and it also applies to every table a request reads through a relation or an `$expand`.
- **`Check`** must hold for every row an insert or update leaves. A policy with no check, or a blank one, uses its filter, as PostgreSQL does, so a write cannot move a row out of the caller's reach; `Check("true")` checks nothing. An insert's check reads the row's given values, and a column it does not give reads as null. A column the database fills (a default, an identity) is not null in the row inserted, so a row must give it when the check reads it. A write that breaks a check is refused whole, with 403.
- **`Preset`** writes a column on insert and update: a value of the request (`@tenant.id`, `@user.id`) or a fixed text. When the request does not have the value (no tenant), the write is refused, rather than writing null.
- **`OnlyColumns`** and **`ExceptColumns`** limit the columns a caller may select, compare, order by and write. A query that selects nothing in particular returns only the columns the caller may read. A request naming any other column is refused with 403.

Conditions read the table's own columns and the request's values:

| Value | Is |
|-------|----|
| `@tenant.id` | The request's tenant, or null. See [Tenants](/guide/web-api/tenancy) |
| `@tenant.name` | A value of the tenant's, such as one read with SQL |
| `@user.id`, `@user.name` | The caller's name identifier (or `sub`) claim, and name |
| `@claims.type` | A claim of the caller; a list when the caller has several |
| `@values.name` | A value the application computes per request: `dm.Value("name", provider)` |

A list value is tested with `in`: `tenant_id in @tenant.below`. An empty list matches nothing. A request's own conditions cannot read these values.

A value the request does not have reads as PostgreSQL reads a null: a test of it holds for no row, negated or not. With no tenant, `tenant_id eq @tenant.id` matches no row (not the rows whose `tenant_id` is null), and so does `not (tenant_id eq @tenant.id)`. To test whether there is a value, compare it with null: `@tenant.id eq null`.

A request reaches rows through its own conditions too, and they are held as reads are:

- An update or delete whose condition compares columns reads rows. As in PostgreSQL, the caller must be allowed to read them: it reaches only the rows the read policies show, and compares only columns the caller may read. Otherwise the number of rows it changed would tell what reading would not.
- Following a relation compares its key columns, which must be ones the caller may read on both tables.

A policy's condition cannot follow a relation to another table. A rule that depends on other rows is written as a value computed from them, such as the ids of the tenants below the caller's, or as the database's own row-level security.

## How policies combine

Policies combine as PostgreSQL's row-level security does:

- **Permissive** policies grant: `DmPolicy.Allow(...)`. An action is allowed if any of them grants it to the caller. Their filters are ORed.
- **Restrictive** policies fence: `DmPolicy.Restrict(...)`. Every one that applies must hold too. Their filters are ANDed on top. A restrictive policy with no roles applies to every caller.

The caller may use the columns any grant allows, less those any applying policy excludes, within every fence's own column list.

```csharp
// Nobody, whatever else they are granted, sees or writes rows of a closed account.
dm.Policy(DmPolicy.Restrict(DmAction.ReadWrite).OnTablesWithColumns("account_id").Where("account_closed eq false"));
```

## A full example

This is a product with these rules:
- rows keyed by tenant;
- global rows (no tenant) that every tenant reads;
- a tree of tenants, where each tenant reads its own rows and those of the tenants below it, never above;
- 35 schemas, all but one shared by every tenant.

```csharp
builder.Services.AddDapperMatic(dm => dm
    .Tenancy(t => t
        .FromClaim("tenant_id")
        // The ids of the tenant and every tenant below it, read once per request.
        .ValueFromSql("below", "SELECT descendant_id FROM tenant_tree WHERE ancestor_id = @tenant.id"))
    .Policies(
        DmPolicy.Allow(DmAction.Read, "authenticated")
            .Named("tenant rows: global, own, and below")
            .OnTablesWithColumns("tenant_id")
            .ExceptSchemas("platform")
            .Where("tenant_id eq null or tenant_id in @tenant.below"),
        DmPolicy.Allow(DmAction.Write, "editor")
            .Named("tenant rows: write own")
            .OnTablesWithColumns("tenant_id")
            .ExceptSchemas("platform")
            .Where("tenant_id eq @tenant.id")
            .Preset("tenant_id", "@tenant.id"),
        DmPolicy.Allow(DmAction.All, "platform-admin").InSchemas("platform"),
        DmPolicy.Restrict(DmAction.ReadWrite).ExceptColumns("internal_notes")));
```

## Policies in configuration

Policies bind from configuration. A predicate (`.When(...)`) can be given only in code.

```json
{
  "DapperMatic": {
    "Policies": [
      {
        "Name": "tenant rows",
        "Actions": "Read, Create, Update",
        "Roles": [ "authenticated" ],
        "WithColumns": [ "tenant_id" ],
        "Filter": "tenant_id eq @tenant.id",
        "Presets": { "tenant_id": "@tenant.id" }
      },
      {
        "Kind": "Restrictive",
        "Actions": "ReadWrite",
        "ExcludeColumns": [ "password_hash" ]
      }
    ]
  }
}
```

```csharp
builder.Services.Configure<DapperMaticOptions>(builder.Configuration.GetSection("DapperMatic"));
```

Every policy is validated at startup, from code or configuration. A malformed condition, a condition reading another table, or a grant to nobody stops the application with a message naming the policy.

`RequireRole` and `ReadOnlyRole` are shorthand: `RequireRole = "admin"` is a policy granting `All` to `admin`, and `ReadOnlyRole = "reader"` grants `Read` and `ReadSchema` to `reader`.

## Policies and tenants kept by DapperMatic

Policies and tenants can also be kept by DapperMatic and edited through its access endpoints, for an application whose administrators manage access at runtime, such as a CMS or a multi-tenant product. They apply from the next request, alongside those of code and configuration.

```csharp
builder.Services.AddDapperMatic(dm => dm
    .UseDatabaseAccessRepository("PostgreSql", connectionString)   // or UseFileAccessRepository(path); in memory by default
    .Policy(DmPolicy.Allow(DmAction.All, "admin")));               // admins manage access, among everything else
```

| Endpoint | What it does |
|----------|--------------|
| `GET /api/dm/access/policies` | Lists the policies in force the caller manages, each with its `source`: `code`, `configuration` or `stored` |
| `GET /api/dm/access/policies/{name}` | Gets one |
| `PUT /api/dm/access/policies/{name}` | Adds or replaces a stored policy; refused with 400 if it cannot work, 409 if code or configuration has the name |
| `DELETE /api/dm/access/policies/{name}` | Removes a stored policy |
| `GET`, `PUT`, `DELETE /api/dm/access/tenants/{id}` | The same for tenants: a stored tenant wins over one of configuration |
| `GET /api/dm/access/export` | The stored policies and tenants the caller manages, as one document |
| `POST /api/dm/access/import?replace=true` | Imports a document, all or none; `replace` replaces everything stored |
| `GET /api/dm/access/effective?datasourceId=&schemaName=&tableName=` | What the policies let the caller do there, action by action |

```http
PUT /api/dm/access/policies/support-reads
Content-Type: application/json

{ "actions": "Read, ReadSchema", "roles": ["support"], "tables": ["tickets", "customers"], "excludeColumns": ["card_last4"] }
```

These endpoints need `ManageAccess`. A caller granted it for some datasources or schemas manages only policies and tenants confined to those. Such a caller can save a policy that names those datasources, and schemas when its grant names schemas, without patterns. It cannot save one that reaches further, and it does not see the others. A tenant's administrator can therefore manage the tenant's access without reaching any other tenant:

```csharp
dm.Policy(DmPolicy.Allow(DmAction.ManageAccess, "tenant-admin")
    .When(ctx => ctx.Tenant is not null)
    .OnDatasources("tenant-a"));
```

Each instance of the application reads the stored policies and tenants again every `AccessRefreshInterval` (30 seconds by default). It sees its own changes at once. A stored policy that no longer validates is left out, with a warning in the log. A connection kept open (a subscription) reads them again before each use, so access taken away ends it; it also ends when the caller's token expires, or after `MaxSessionAge` (one hour by default).

`effective` answers what a user interface needs to show or hide: whether each action is allowed, and which columns. It also shows the conditions and the names of the policies that apply, to a caller who manages access there.

## Tenant administrators

Where tenants share tables and a column marks whose row it is, a tenant's own administrator can keep the rules for their tenant: "our clerks may read and edit invoices, but not delete them". DapperMatic holds every such policy to the tenant.

```csharp
builder.Services.AddDapperMatic(dm => dm
    .Tenancy(t => t
        .FromClaim("tenant_id")
        .RowsMarkedBy("tenant_id")              // the column marking a tenant's rows
        .RowsMarkedBy("invoices", "org_id"))    // a table where another column does
    .Policies(
        DmPolicy.Allow(DmAction.ReadWrite | DmAction.ReadSchema, "tenant-admin")
            .OnTablesWithColumns("tenant_id")
            .Where("tenant_id eq @tenant.id"),
        DmPolicy.Allow(DmAction.ManageAccess, "tenant-admin").WithinOwnTenant()));
```

A tenant's administrator then uses the same `/access/policies` endpoints, from a request in their tenant:

```http
PUT /api/dm/access/policies/clerks
{ "actions": "Read, Update", "roles": ["clerk"], "datasources": ["main"], "tables": ["invoices"],
  "columns": ["id", "amount", "status"] }
```

What DapperMatic does with it:

- **It belongs to the tenant.** It applies only to requests in that tenant, and its name is the tenant's own: another tenant's `clerks` is another policy.
- **It is held to the tenant's rows.** On each table it names, the column marking a tenant's rows must be the tenant's, whatever its own filter says; the check on written rows holds that too, and a row its callers add or change has the column set to the tenant. Where tenants have a datasource or schema of their own, the policy is held to those as well. With `TenantRulesReach("@tenant.below")`, it reaches the tenants a list value names, such as those below the tenant in a tree.
- **It gives no more than its author may do.** Each action on each table it names must be one the administrator may do, and where the administrator may use only some columns, the policy names its columns among those. It grants to callers who sign in (not `*` or `anonymous`), names its datasources, schemas and tables without patterns, and never manages datasources, writes views' SQL, or changes schemas tenants share. A policy granting `ManageAccess` makes sub-administrators of the same tenant.
- **It sees nothing else.** A tenant's administrator lists, changes and removes their tenant's policies only, and none of the application's, other tenants' or the tenants' settings.

Platform administrators (a grant of `ManageAccess` not confined to a tenant) see every tenant's policies, each marked with its `tenant`, and change one by naming it: `PUT /api/dm/access/policies/clerks?tenant=42`.

The administrator's own limits on rows beyond the tenant are not passed on: if they may edit only draft invoices, a policy they keep can still let clerks edit any of the tenant's invoices. A limit that must hold for everyone in a tenant is a restrictive policy of the application's: `DmPolicy.Restrict(DmAction.Update).OnTables("invoices").Where("status eq 'draft'")`.

## Application code

Code of the application's own, reading and writing for the current user, can be held to the same policies through the typed DML methods: `DapperMaticCallerDb.OpenAsync("main")` gives a scope run as the request's caller. See [Running as a caller](/guide/dml-methods#running-as-a-caller).

## Custom permissions

`IDapperMaticPermissions` decides each operation in code. It replaces the default decision of whether an operation may run at all:

```csharp
public sealed class MyPermissions(IMyAccessService access) : IDapperMaticPermissions
{
    public async Task<bool> IsAuthorizedAsync(DapperMaticOperation operation)
    {
        var action = DapperMaticPolicySet.ActionOf(operation.Operation); // DmAction.Read, ChangeSchema, ...
        var resource = DapperMaticPolicySet.ResourceOf(operation);       // datasource, schema, table
        var tenant = operation.Access?.Tenant;                           // the caller's tenant, if any
        return await access.MayAsync(operation.User, tenant?.Id, action, resource);
    }
}

builder.Services.AddDapperMatic(dm => dm.UseCustomPermissions<MyPermissions>());
```

Rows are still held to the policies of every table a policy is about. With custom permissions, a table no policy is about is the application's to decide, and its permissions have decided. A table whose rows any policy is about, for reading, inserting, updating or deleting, is held for all four, as PostgreSQL holds a table with row security on: an action no policy grants there is refused. With the default permissions, a table no policy is about is refused.

`DapperMaticOperation` carries the operation's name (`tables/query`, `datasources/post`), its datasource, schema, table, view, columns, index and constraint, the HTTP method and path, the request body, `Properties` (`NewTableName` for a rename, `CurrentMethod`), and `Access`: the caller, the tenant, and the request's values. It never changes once made: checking a request as another operation, such as a table it reads besides its own, makes a copy (`operation with { TableName = "customers" }`), so operations running side by side for one caller never see each other's.

## Audit logging

Every operation, successful or not, is passed to `IDapperMaticAuditLogger`. The default writes to `ILogger`. Replace it to keep an audit trail elsewhere:

```csharp
public sealed class DatabaseAuditLogger(IDbConnection auditDb) : IDapperMaticAuditLogger
{
    public Task LogOperationAsync(DapperMaticAuditEvent e) =>
        auditDb.ExecuteAsync(
            "INSERT INTO audit_log (user_id, operation, datasource_id, schema_name, table_name, success, message, at, ip) "
                + "VALUES (@UserIdentifier, @Operation, @DatasourceId, @SchemaName, @TableName, @Success, @Message, @Timestamp, @IpAddress)",
            e);
}

builder.Services.AddDapperMatic(dm => dm.UseCustomAuditLogger<DatabaseAuditLogger>());
```

Each event names the tenant the operation was performed in (`TenantId`). To record more about who acted, name claims to copy into every event's `Claims`, such as the grant a platform administrator works in a tenant under:

```csharp
dm.AuditClaims("grant_id", "act");
// "tables/put by bob in tenant 42 ... {grant_id: 17}"
```

## Connection strings

Connection strings kept in a file or database repository are encrypted with AES-256, using `ConnectionStringEncryptionKey`. Keys can be rotated, and datasources added through the API are held to a connection policy: see [Setup & Configuration](/guide/web-api/setup#encryption-keys-and-rotation).

## Defense in depth

- **ASP.NET Core** conventions on the endpoint group: `RequireAuthorization`, `RequireRateLimiting`, `RequireCors`. See [Setup](/guide/web-api/setup#authorization-rate-limiting-and-cors).
- **DapperMatic's policies**, on every provider.
- **The database's own row-level security**, where the provider has it: DapperMatic can set the tenant and the caller on each connection's session for the database's policies to read. See [Tenants](/guide/web-api/tenancy#the-database-s-own-row-level-security).
- **Database permissions**: connect as a database user that can do no more than the application needs.
