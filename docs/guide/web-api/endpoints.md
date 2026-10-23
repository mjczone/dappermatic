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

2. **Add an API reference to your own application** (optional). DapperMatic describes its
   endpoints using standard ASP.NET Core metadata and ships **no OpenAPI dependency of its own**,
   so any OpenAPI stack picks them up.

   **Recommended — ASP.NET Core 10's built-in generator with the Scalar UI:**
   ```bash
   dotnet add package Microsoft.AspNetCore.OpenApi
   dotnet add package Scalar.AspNetCore
   ```
   ```csharp
   builder.Services.AddOpenApi();

   var app = builder.Build();

   app.MapOpenApi();
   app.MapScalarApiReference();

   app.UseRouting();
   app.UseDapperMatic();
   ```
   Once configured, you can access:
   - **Scalar UI**: Navigate to `/scalar` in your running application
   - **OpenAPI Specification**: Available at `/openapi/v1.json`

   Keep `Microsoft.AspNetCore.OpenApi` on the major version matching your target framework
   (`10.x` for `net10.0`). See [OpenAPI Integration](/guide/web-api/openapi-compatibility) for
   generating the spec at build time instead of serving it.

   **Already using Swashbuckle?** That keeps working — any version. Because DapperMatic
   references no OpenAPI package, there is no version for it to conflict with:
   ```csharp
   builder.Services.AddSwaggerGen();

   var app = builder.Build();

   app.UseSwagger();
   app.UseSwaggerUI();
   ```

## Base URL Structure

All endpoints use the base path `/api/dm` by default (configurable via `DapperMaticOptions.BasePath`):

```
/api/dm/{resource-hierarchy}
```

## URL Hierarchy

DapperMatic uses a consistent hierarchical URL structure that mirrors database organization:

```
/api/dm/d                              # Datasource management
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
POST /api/dm/d/
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
- Query table data, and insert, update and delete rows (see [Querying and writing rows](#querying-and-writing-rows))
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

A successful response carries its data in `result`, plus `pagination` for queries and `affected` / `rows` for row writes:

```json
{
  "result": {
    "id": "ProductionDB",
    "provider": "SqlServer",
    "displayName": "Production Database"
  }
}
```

An error is a problem details body (RFC 9457), with a `code` and the request's `traceId`; see [Errors](./setup#errors):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not found",
  "status": 404,
  "detail": "Datasource 'NonExistentDB' not found",
  "code": "not_found",
  "traceId": "0HN..."
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
- **304 Not Modified** - A GET whose `If-None-Match` names the response's current `ETag` ([ETags](./setup.md#etags))
- **400 Bad Request** - Invalid request data or parameters
- **401 Unauthorized** - Authentication required
- **403 Forbidden** - Insufficient permissions
- **404 Not Found** - Requested resource doesn't exist
- **409 Conflict** - Resource already exists (for create operations)
- **500 Internal Server Error** - Unexpected server error

## Authentication and Authorization

Nothing is allowed until a policy grants it:

- [Policies](/guide/web-api/security) say who may do what to which datasources, schemas, tables, rows and columns
- The request's [tenant](/guide/web-api/tenancy) is found and held to its datasource, schema and rows
- Custom authorization logic can replace the default decisions via `IDapperMaticPermissions`
- All operations are logged via `IDapperMaticAuditLogger`

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
GET /api/dm/d/

# Create a table
POST /api/dm/d/MyDB/t/
{
  "tableName": "Users",
  "columns": [...]
}

# Query a table
GET /api/dm/d/MyDB/t/Users/query?$filter=IsActive eq true&$orderby=CreatedAt desc&$top=100

# Add a column to a table
POST /api/dm/d/MyDB/t/Users/columns
{
  "columnName": "LastLoginDate",
  "providerDataType": "datetime2",
  "isNullable": true
}
```

For complete endpoint details, request/response schemas, and interactive testing, visit the [DapperMatic API Browser](/api-browser/).

## Querying and writing rows

`GET .../t/{table}/query` and `GET .../v/{view}/query` take OData-shaped options; `POST` to the same paths takes them in a JSON body. Every name is checked against the table's or view's columns, and every value is a parameter of the column's type.

```bash
GET /api/dm/d/Shop/t/orders/query
    ?$filter=status eq 'open' and (customer/country in ('FR', 'DE') or lines/any(l: l/qty gt 5))
    &$orderby=placed_at desc
    &$select=id,status,placed_at
    &$top=50&$skip=0&$count=true
    &$expand=customer,lines($filter=qty gt 0;$orderby=id;$top=10)
```

| Option | Meaning |
|--------|---------|
| `$filter` | `eq` `ne` `gt` `ge` `lt` `le`, `and` `or` `not`, `in (...)`, `add` `sub` `mul` `div` `mod`; `contains`, `startswith`, `endswith`, `tolower`, `toupper`, `trim`, `length`, `year` ... `second`; `null`, `true`, `false`, numbers and `'strings'` (a quote doubled). Dates, times and GUIDs are written as strings. |
| `rel/column` | A column of a related row, joined: `customer/country eq 'FR'`. |
| `rel/any(x: ...)`, `rel/all(x: ...)`, `rel/$count` | Tests and counts of related rows. |
| `$orderby`, `$select`, `$top`, `$skip`, `$count` | Order, columns, page (`$top` at most 1,000), and the total count, which ignores the page. |
| `$expand` | Related rows read with each row, with their own `$filter`, `$orderby`, `$top` (per row), `$select` and `$expand`. |
| `$after` | The rows after a cursor, in the same order: see *Paging by cursor* below. Not with `$skip`. |
| `$apply` | Groups and totals: see *Totals and groups* below. |
| `$search` | Rows holding every word in a searchable column: see *Search* below. |

A `POST` body has the same options as properties: `filter` (the `$filter` text), `where` (the same condition as a JSON tree), `order`, `select`, `take`, `skip`, `includeTotal`, `expand`. The JSON tree writes each node as an object of one property, and never guesses whether a string is a column or a value:

```json
{
  "where": { "and": [
    { "eq": [ { "path": "status" }, { "value": "open" } ] },
    { "any": { "path": "lines", "as": "l", "where": { "gt": [ { "path": "l/qty" }, { "value": 5 } ] } } }
  ] },
  "expand": "customer",
  "take": 50
}
```

**Totals and groups** come from the database with OData's `$apply`: `filter(...)` steps over the rows, then `groupby((columns),aggregate(...))`, or `aggregate(...)` for one total of every row. Aggregates are `column with sum|min|max|average|countdistinct as name`, and `$count as name` for the rows. With `$apply`, the results are the groups: `$filter`, `$orderby`, `$top`, `$skip` and `$count` apply to them, and read their keys and aggregates by name.

```bash
GET .../t/orders/query?$apply=filter(status ne 'void')/groupby((customer_id),aggregate(amount with sum as total,$count as orders))
    &$filter=total gt 1000&$orderby=total desc&$top=10
```

A `POST` body takes the same text as `apply`, or a JSON tree as `applyTree`: `{"filter": node, "groupBy": ["customer_id"], "aggregate": [{"path": "amount", "with": "sum", "as": "total"}, {"with": "count", "as": "orders"}]}`. Groups count only the rows the caller's [policies](./security) let them read, and a column they may not read cannot be grouped or aggregated. Groups are by the table's own columns.

**Search.** `$search=acme rockets` finds the rows that hold every word in one of a table's searchable columns (a phrase in double quotes is one word), ANDed with `$filter`. A table is searchable only when declared:

```csharp
options.Searchable.Add(new DapperMaticSearchable { TableName = "articles", Columns = ["title", "body"] });
```

Where the database has a full-text index of exactly those columns, it is used: words match words, with each database's stemming and ranking rules. An index over more columns than are declared is not used, since it would search columns a caller may not be able to read; the contains match runs instead.

| Database | Index used |
|----------|------------|
| PostgreSQL | A GIN or GiST index on `to_tsvector(...)` of the columns, queried with `websearch_to_tsquery` in the index's configuration |
| MySQL / MariaDB | A `FULLTEXT` index of exactly the columns, in boolean mode (each word required, as a prefix) |
| SQLite | An FTS5 table named `{table}_fts` whose rowids are the table's (`content='{table}', content_rowid='id'`) |
| SQL Server | Not yet: a contains match |

Without one, every word must be contained in one of the columns, ignoring case: slower on big tables, but the same on every database. The [policies](./security) apply as to any condition, and a searchable column the caller may not read refuses the search.

**Paging by cursor.** `$skip` gets slower the deeper it goes, and skips or repeats rows when others are added or removed meanwhile. A query ordered by the table's own columns is ordered by its key last, so that every row has one place, and each full page's `pagination.nextCursor` reads the rows after it:

```bash
GET .../t/orders/query?$orderby=placed_at desc&$top=50                    # pagination.nextCursor: "eyJrIjpb..."
GET .../t/orders/query?$orderby=placed_at desc&$top=50&$after=eyJrIjpb...
```

The cursor is opaque: pass it back as it is, with the same `$orderby`. It is null on the last page, and for a query ordered by a related row's column or of a view or table without a key. The typed DML methods read pages the same way, with the same cursors: `ToPageAsync`.

The simple filters (`?status.eq=open&total.gt=10&sort=-placed_at`) still work, ANDed with the others. Comparisons keep the meaning the [DML methods](../dml-methods#conditions) give them where a column can be NULL.

**Relations are reachable only when declared.** Nothing is inferred from foreign keys:

```csharp
builder.Services.Configure<DapperMaticOptions>(options =>
{
    options.Relations.Add(new DapperMaticRelation
    {
        TableName = "orders", Name = "customer",
        OtherTableName = "customers", Columns = ["customer_id"], OtherColumns = ["id"],
    });
    options.Relations.Add(new DapperMaticRelation
    {
        TableName = "orders", Name = "lines",
        OtherTableName = "order_lines", Columns = ["id"], OtherColumns = ["order_id"], Many = true,
    });
    options.QueryLimits.MaxExpandDepth = 2;    // also MaxTop, MaxLambdaDepth, MaxNodes
});
```

A request that names an undeclared relation, an unknown column or a value not of its column's type, or that asks for more than the limits, is refused with 400.

**Writing rows** of a table (not a view), each a `POST` with a JSON body; the response is `{ "affected": n }`:

```bash
POST /api/dm/d/Shop/t/orders/rows           { "rows": [ { "id": 1, "status": "open" }, { "id": 2, "status": "held" } ] }
POST /api/dm/d/Shop/t/orders/rows/update    { "set": { "status": "closed" }, "filter": "status eq 'open' and placed_at lt '2026-01-01'" }
POST /api/dm/d/Shop/t/orders/rows/delete    { "filter": "status eq 'void'" }
```

Inserts are all or none. An update or a delete needs a condition (`filter`, `where`, or both, ANDed); `"filter": "true"` says every row.

**Related rows** are written with a row, nested under a declared relation's name: a row the row refers to (`customer`) as an object, rows that refer to it (`lines`) as an array. The related row it refers to is written first and its key put in the row; then the row; then the rows that refer to it, with its key. A related row whose key is given is updated if it exists and inserted if not; one without a key is inserted; rows left out are left as they are. Each related table is written only as the caller may write it on its own: inserted into with its insert rights, updated with its update rights. `.../rows/save` does the same with the rows themselves: updated by their key where it exists, inserted where not. Inserts and saves return the rows as written, with the keys the database generated, holding the columns the request gave, the keys, and the columns the caller may read (a value a policy wrote into a column the caller may not read is not returned):

```bash
POST /api/dm/d/Shop/t/orders/rows
{ "rows": [ { "total": 10, "customer": { "name": "Acme" }, "lines": [ { "qty": 1 }, { "qty": 2 } ] } ] }

POST /api/dm/d/Shop/t/orders/rows/save
{ "rows": [ { "id": 7, "total": 11, "lines": [ { "id": 31, "qty": 5 }, { "qty": 3 } ] } ] }
```

Every row is held to its own table's [policies](./security), all or none. A key the database generates is never inserted as given: a save naming one no row has is refused. An object or array given for a column, rather than a relation, is written as JSON.

**Batches** run writes of several tables in order, in one transaction, all or none. A value `"$1.id"` is the `id` of the first row the first operation inserted (`"$1[2].id"` its third row; `"$$..."` a string that starts with one `$`), so a parent and its children go in one request. Each operation is authorized and held to its table's [policies](./security) as the row endpoints are, all of them before anything is written:

```bash
POST /api/dm/d/Shop/batch
{
  "operations": [
    { "op": "insert", "table": "customers", "rows": [ { "name": "Acme" } ] },
    { "op": "insert", "table": "orders", "rows": [ { "customer_id": "$1.id", "total": 10 } ] },
    { "op": "update", "table": "quotes", "set": { "status": "won" }, "filter": "customer_name eq 'Acme'" }
  ]
}
# { "results": [ { "affected": 1, "rows": [ { "name": "Acme", "id": 42 } ] }, { "affected": 1, "rows": [...] }, { "affected": 3 } ] }
```

Inserts return their rows as written, with the keys the database generated and the columns the caller may see, as `rows` does; a later operation can refer only to those. `"dryRun": true` rolls every write back. A batch holds at most `QueryLimits.MaxBatchOperations` operations (100). Their operations for [permissions](./security) are `tables/insert-rows`, `tables/update-rows` and `tables/delete-rows`.

## Functions

A function is SQL the application writes, or a routine of the database, that callers run by name through `POST /api/dm/d/{datasourceId}/f/{name}`. Nothing is callable unless declared. The SQL is the application's own and is never built from the request; the request only gives arguments by name, and each is bound as a parameter.

```csharp
builder.Services.Configure<DapperMaticOptions>(options =>
{
    options.Functions.Add(new DapperMaticFunction
    {
        Name = "top_customers",
        Description = "The customers with the largest totals, of the caller's tenant",
        Sql = "SELECT customer_id, SUM(total) AS total FROM orders WHERE tenant_id = @tenant AND total >= @min GROUP BY customer_id",
        Parameters =
        {
            new DapperMaticFunctionParameter { Name = "min", Type = typeof(decimal), Default = 0m },
            new DapperMaticFunctionParameter { Name = "tenant", Type = typeof(int), Value = "@tenant.id" }, // never from the request
        },
    });

    // A routine: PostgreSQL SELECT * FROM fn(...), SQL Server EXEC proc @p = ..., MySQL CALL proc(...).
    options.Functions.Add(new DapperMaticFunction
    {
        Name = "close_month",
        Routine = "accounting.close_month",
        Parameters = { new DapperMaticFunctionParameter { Name = "month", Type = typeof(DateOnly) } },
        Writes = ["ledger", "accounting.balances"], // their cached query results become stale after each run
    });
});

builder.Services.AddDapperMatic(dm => dm.Policies(
    DmPolicy.Allow(DmAction.Execute, "analyst").OnFunctions("top_*")));
```

```bash
curl -X POST .../api/dm/d/ProductionDB/f/top_customers -H "Content-Type: application/json" -d '{ "min": 1000 }'
# { "result": [ { "customer_id": 12, "total": 5400.00 }, ... ] }
```

- **Arguments** are a JSON object, read as each parameter's `Type`. An unknown, mistyped or missing one is a 400. A parameter with a `Default` may be left out; one marked `Nullable` may be `null`.
- **Values from the request's context** (`Value = "@tenant.id"`, `"@user.id"`, any value [tenancy](./tenancy) defines) are always set by DapperMatic, and a request that gives them is refused. This keeps a caller inside its own tenant. A value the request has several of (several claims of one type, a tenant's reach) is not one value: the request is refused (400).
- **Who may run it**: `DmAction.Execute`, from a grant that names the function (`OnFunctions`, patterns allowed) or names none and is not confined to schemas or tables. A function's SQL can read and write any table the connection can, so a grant on a few tables does not reach it. A caller not allowed to run a function gets 403, whether or not the function exists.
- **Rows**: the function returns at most `MaxRows`, by default `QueryLimits.MaxTop`. More is a 400, never a silent cut.
- **Session values** for the database's own row-level security are set on its connection, as for every request.
- Each caller's [OpenAPI document](#each-caller-s-own-openapi-document) lists the functions it may run, with the arguments it gives.

## Each caller's own OpenAPI document

`GET /api/dm/d/{datasourceId}/openapi` (or `.../d/{datasourceId}/s/{schemaName}/openapi`) returns an OpenAPI 3.1 document of the datasource's rows **as the calling user may use them**. A client generator (NSwag, Kiota, openapi-typescript, ...) then produces a typed client for exactly that caller.

- **Tables and views**: only those the caller may read or write. A table no policy grants it does not appear.
- **Actions**: only the ones granted. A table it may read and insert into gets `GET .../query` and `POST .../rows`. `rows/update`, `rows/delete` and `rows/save` appear only with `Update`, `Delete`, or both `Create` and `Update`.
- **Columns**: each row schema holds only the columns the caller may read. The insert (`{table}New`) and update (`{table}Set`) schemas hold only the columns it may write, without those its policies preset. Columns are typed (`integer`, `number`, `string` with `date-time` / `date` / `uuid`, ...) and nullable when the column is.
- **Relations**: a declared relation to a table the caller may read is a property of the row schema (a row, or an array), filled when `$expand`ed. Its name is listed under `$expand`.

```bash
curl -H "Authorization: Bearer $TOKEN" https://localhost:5001/api/dm/d/ProductionDB/openapi > my-api.json
npx openapi-typescript my-api.json -o my-api.d.ts
```

The document's `servers` URL is where it was served from, without `/openapi`, so its paths read `/t/orders/query`. Like every `GET`, it carries an [ETag](./setup.md#etags). The static API document (above) still describes every endpoint for everyone.
