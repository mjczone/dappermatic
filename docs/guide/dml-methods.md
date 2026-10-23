# Reading and Writing Rows

The same classes DapperMatic builds tables from read and write their rows: inserts, updates, deletes and queries, written as C# expressions and sent as parameterized SQL for each provider, run by Dapper.

```csharp
using MJCZone.DapperMatic.Dml;

var id = await db.InsertAsync(new Order { CustomerName = "Ada", Total = 12.50m });
var order = await db.SingleByIdAsync<Order>(id);

var open = await db.SelectAsync<Order>(o => o.Status == OrderStatus.Open && o.Total > 100);

var page = await db.From<Order>()
    .Where(o => o.PlacedAt >= since)
    .OrderByDescending(o => o.PlacedAt)
    .Skip(20)
    .Take(10)
    .ToListAsync();
```

The methods are in their own namespace, `MJCZone.DapperMatic.Dml`, and are part of the `MJCZone.DapperMatic` package.

## Quick Navigation

- [Models](#models) - What the methods read and write
- [By key](#by-key) - Insert, update, delete and read single rows
- [Conditions](#conditions) - What a `Where` expression can say
- [Relations](#relations) - Conditions through related rows: `o.Customer!.Country`, `o.Lines!.Any(...)`
- [Loading related rows](#loading-related-rows) - `Include`, `LoadAsync`, and `DmMerge` for rows in memory
- [Updating and deleting by condition](#updating-and-deleting-by-condition)
- [Patching](#patching) - Set only the columns a patch holds
- [Upserting](#upserting) - Insert, or update the conflicting row, with conditions
- [Mass loading](#mass-loading) - Native bulk paths, and bulk upserts
- [Queries](#queries) - `From<T>()`: ordering, paging, projections, aggregates
- [Grouping](#grouping) - `GroupBy`, `Having`, and each group's key and aggregates
- [Joins written out](#joins-written-out) - `Join` and `LeftJoin` between any models
- [Columns the methods fill and filter](#columns-the-methods-fill-and-filter) - Soft delete, concurrency tokens, audit, tenant keys
- [Tenant schemas and other tables](#tenant-schemas-and-other-tables)
- [Running as a caller](#running-as-a-caller) - Held to the policies the web API holds requests to
- [Models known at run time](#models-known-at-run-time) - The `Type` overloads
- [Differences between providers](#differences-between-providers)
- [What the methods do not do](#what-the-methods-do-not-do)

## Models

A model is any class [`DmTableFactory`](./models) can build a table from. The methods use its table and schema, its mapped properties (`[DmColumn]`, `[Column]`, and unattributed properties; never `[DmIgnore]` or `[NotMapped]` ones), and its primary key. A class with `[DmView]` is read like a table, and every method that writes refuses it.

Values are always parameters. Table and column names come only from the model and are quoted the way the DDL methods name them, so a model whose table was created by `CreateTableIfNotExistsAsync<T>()` or [migrations](./migrations) reads and writes the same columns on every provider.

Every property type that [maps out of the box](./dml-query-support#what-maps-out-of-the-box) works here too: enums, `Guid`, dates and times, JSON-stored lists and dictionaries, and the rest. The first call initializes `DapperMaticTypeMapping` with its defaults if the application has not.

## By key

```csharp
var id = await db.InsertAsync(order);                    // the generated key, also set on order.Id
await db.InsertManyAsync(orders);                        // several rows to a statement, all or none
await db.UpdateAsync(order);                             // every column but the key, by the key
await db.DeleteAsync(order);                             // by the key
await db.DeleteByIdAsync<Order>(id);

var one = await db.SingleByIdAsync<Order>(id);           // null if there is none
var some = await db.SelectByIdsAsync<Order>([1, 2, 3]);

// A composite key, as an object or a tuple in key order.
var line = await db.SingleByIdAsync<OrderLine>(new { OrderId = 1, LineNumber = 2 });
var same = await db.SingleByIdAsync<OrderLine>((1, 2));
```

| Method | Returns |
|--------|---------|
| `InsertAsync` | The generated value for a single auto-increment key, which is also set on the row; the row's own key otherwise; null for a composite key or no key |
| `InsertManyAsync` | The number of rows inserted. Generated keys are not set on the rows. |
| `UpdateAsync(row)`, `DeleteAsync(row)`, `DeleteByIdAsync` | The number of rows affected: 1, or 0 if no row has the key |

`InsertManyAsync` writes multi-row `VALUES` statements kept under each provider's limits: 2,100 parameters and 1,000 rows on SQL Server, 999 parameters on SQLite. Without a transaction of yours, rows that take more than one statement are inserted in a transaction of the method's own, so either every row is inserted or none is.

A key given as another type is converted to the key's own: `SingleByIdAsync<Order>(42L)` works for an `int` key. A model without a primary key cannot be used with the by-key methods, and says so.

## Conditions

```csharp
await db.SelectAsync<Order>(o => o.Status == OrderStatus.Open);
await db.SingleAsync<Order>(o => o.Number == number);      // null if none, throws if more than one
await db.FirstAsync<Order>(o => o.Total > 100);            // the match with the lowest key
await db.CountAsync<Order>(o => o.Note != null);
await db.ExistsAsync<Order>(o => ids.Contains(o.Id));
```

A condition is translated to SQL, never evaluated in memory: anything that cannot be translated throws `DmTranslationException`, which names the part, before anything is sent.

| C# | SQL |
|----|-----|
| `==`, `!=`, `<`, `<=`, `>`, `>=`, `&&`, `\|\|`, `!` | the same, parenthesized |
| `o.Note == null`, `o.Total.HasValue` | `IS NULL`, `IS NOT NULL`, also for a variable that is null |
| `o.Shipped`, `!o.Shipped` | `shipped = @p` with true or false |
| Variables, fields, properties, method calls that do not use the row | evaluated once, then a parameter |
| `ids.Contains(o.Id)` (lists, arrays, sets) | `IN (@p0, @p1, ...)`; an empty list is false. On SQL Server, a list too long for its 2,100 parameters is one JSON parameter, `IN (SELECT value FROM OPENJSON(@p))` (SQL Server 2016 and later) |
| `o.Name.Contains(s)`, `StartsWith`, `EndsWith` | `LIKE @p ESCAPE '…'`, with `%` and `_` in the value matched as themselves |
| The same with `StringComparison.OrdinalIgnoreCase` (or another `IgnoreCase`), `string.Equals(a, b, …IgnoreCase)` | `LOWER(...)` on both sides |
| `string.IsNullOrEmpty(o.Note)` | `IS NULL` or exactly empty |
| `+ - * / %`, `??`, `?:` | arithmetic, `COALESCE`, `CASE` |
| `a + b` on strings, `string.Concat` | `\|\|`, `+` or `CONCAT`, per provider |
| `ToUpper`, `ToLower`, `Trim`, `TrimStart`, `TrimEnd`, `Substring`, `Replace`, `Length` | the provider's function |
| `.Year`, `.Month`, `.Day`, `.Hour`, `.Minute`, `.Second` of a `DateTime`, `DateOnly` or `TimeOnly` | the provider's date part, as an integer |

**Conditions keep their C# meaning where a column can be NULL.** `o.Note != "x"` is true in C# for a row whose note is NULL, so it is written `(note <> @p OR note IS NULL)`. Negations are pushed down to the comparisons they apply to, so `!(o.Total > 5)` keeps the rows with no total, and `!ids.Contains(o.Id)` keeps NULLs unless the list holds a null. Comparing two nullable columns with `==` matches two NULLs.

Divisions follow C#: `o.Qty / 2` drops the remainder on every provider (MySQL's `/` would not), and `(double)o.Qty / o.Count` keeps it (SQL Server, PostgreSQL and SQLite would not, dividing two integer columns). `Length` counts trailing spaces, as `string.Length` does and SQL Server's `LEN` does not.

A `StringComparison` that keeps case is refused: whether the database keeps case follows the column's collation. So is a date part of a `DateTimeOffset`, whose parts depend on an offset providers store differently.

## Relations

A property marked `[DmRelation]` is a relation to another model's rows, never a column: one related row, or a collection of them. Conditions can follow relations, to filter on the related rows' columns:

```csharp
public class Order
{
    [DmColumn("customer_id", isNullable: true, isForeignKey: true, referencedTableName: "customers", referencedColumnName: "id")]
    public int? CustomerId { get; set; }

    [DmRelation] public Customer? Customer { get; set; }          // keys from the foreign key
    [DmRelation] public List<OrderLine>? Lines { get; set; }      // the lines' foreign key to orders
}

// The orders of customers in France or Germany, or with a product made in England or Spain.
var orders = await db.From<Order>()
    .Where(o => countries.Contains(o.Customer!.Country) || o.Lines!.Any(l => origins.Contains(l.Product!.Origin)))
    .ToListAsync();
```

Rows are read with their relations null unless a query [includes](#loading-related-rows) them.

### Declaring a relation

| Declaration | Joins on |
|-------------|----------|
| `[DmRelation]` | The one foreign key between the two models: for one row, this model's foreign key to the other (or else the other's to this one); for many, the other's to this one. None, or several, is an error that says to name the keys. |
| `[DmRelation(nameof(ShipToId))]` | That foreign key property (this model's for one row, the other model's for many) and the referenced model's primary key. |
| `[DmRelation(thisKeys: [...], otherKeys: [...])]` | Those properties, in pairs; a side left out is its primary key. No foreign key is needed. |
| `otherValues: [nameof(Attribute.ObjectType), "order"]` | Fixed values the related rows have, as property and value pairs, converted to the property's type. `thisValues:` is the same for this model's rows. |
| `condition: nameof(NotDeleted)` | A condition the related rows must meet: a static property, field or method of this model returning `Expression<Func<TOther, bool>>`, or `Expression<Func<TThis, TOther, bool>>` to compare with this row. |
| `through: typeof(ProductTag)` | Many-to-many: the link model's foreign keys to the two models. |

Rows of many kinds of object that share one table, such as attribute (EAV) rows, need no foreign key:

```csharp
public class Document
{
    [DmColumn("id", isPrimaryKey: true)] public Guid Id { get; set; }

    [DmRelation(
        thisKeys: [nameof(Id)],
        otherKeys: [nameof(Attribute.ObjectId)],
        otherValues: [nameof(Attribute.ObjectType), "document"],
        condition: nameof(Live))]
    public List<Attribute>? Attributes { get; set; }

    // One attribute row: the keys and fixed values match one row of a unique constraint.
    [DmRelation(
        thisKeys: [nameof(Id)],
        otherKeys: [nameof(Attribute.ObjectId)],
        otherValues: [nameof(Attribute.ObjectType), "document", nameof(Attribute.Name), "color"])]
    public Attribute? Color { get; set; }

    private static Expression<Func<Attribute, bool>> Live => a => !a.IsDeleted;
}

var red = await db.From<Document>().Where(d => d.Color!.Value == "red").ToListAsync();
```

A relation to one row must match at most one: its keys and fixed values must cover the other model's primary key, a unique constraint or a unique index (a view model, which declares none, is not checked). Keys are compared as they are typed, so each pair must be of one type. A relation is resolved the first time it is used, and one that cannot be resolved throws `InvalidOperationException` saying why.

### What a condition can say through a relation

| C# | SQL |
|----|-----|
| `o.Customer!.Country == "France"`, `o.Line!.Product!.Origin` | The related row `LEFT JOIN`ed, once per relation however often it is used |
| `o.Customer == null`, `o.Customer != null` | `NOT EXISTS`, `EXISTS` |
| `o.Lines!.Any()`, `.Any(l => ...)`, `.All(l => ...)`, `.Where(l => ...).Any()` | `EXISTS` and `NOT EXISTS` subqueries, which can follow relations of their own |
| `o.Lines!.Count()`, `o.Lines!.Count`, `.Count(l => ...)` | `(SELECT COUNT(*) ...)` |
| `.Sum(l => l.Qty * l.Product!.Price)`, `Min`, `Max`, `Average` | `(SELECT SUM(...) ...)`; a sum of no rows is 0, as in C# |
| `p.Tags!.Any(t => t.Name == "sale")` through a link model | `EXISTS` over the link table joined to the tags |

**A missing related row reads as NULLs**, as `o.Customer?.Country` would in C#: `o.Customer!.Country != "France"` keeps the orders with no customer, and `o.Customer!.Country == null` finds them. A bool column of a missing row is neither true nor false. `All` is true for no rows. Inside a subquery's condition the query's own row is in reach, with its relations: `o.Lines!.Any(l => l.Product!.Origin == o.Customer!.Country)`.

Relations work in `Where`, ordering keys (`OrderBy(o => o.Customer!.Name)`), aggregate values (`SumAsync(l => l.Product!.Price)`), the conditions of `UpdateAsync`, `DeleteAsync` and `PatchAsync`, and `UpdateAsync`'s values, where a related row's column is read by a subquery rather than a join, the same way on every provider:

```csharp
await db.UpdateAsync<Order>(o => new Order { ShipCountry = o.Customer!.Country }, o => o.ShipCountry == null);
await db.DeleteAsync<OrderLine>(l => l.Order!.Status == "cancelled");
```

MySQL refuses a subquery of the table an `UPDATE` or `DELETE` writes (error 1093), so there a model's relation to its own table cannot be followed by those two. Upserts do not follow relations. Related tables follow the scope: `InSchema` and `WithTableName` apply to them as to the query's own table.

### Models with no relation property

`WhereExists` and `WhereNotExists` correlate rows of other models with a condition written out:

```csharp
var flagged = await db.From<Order>()
    .WhereExists<Note>((o, n) => n.OrderId == o.Id && n.Flagged)
    .ToListAsync();

var noSpanish = await db.From<Order>()
    .WhereNotExists<OrderLine, Product>((o, l, p) => l.OrderId == o.Id && p.Id == l.ProductId && p.Origin == "Spain")
    .ToListAsync();
```

## Loading related rows

A query loads relations only when it is told to, with `Include`:

```csharp
var orders = await db.From<Order>()
    .Where(o => o.PlacedAt >= since)
    .Include(o => o.Customer)                                   // one row: joined into the query
    .Include(o => o.Lines, lines => lines                       // many rows: one more query
        .Where(l => l.Qty > 0)
        .OrderBy(l => l.LineNumber)
        .Include(l => l.Product, p => p.Include(x => x.Tags)))  // to any depth
    .ToListAsync();

var customers = await db.From<Customer>()
    .Include(c => c.Orders, o => o.OrderByDescending(x => x.PlacedAt).Take(5))   // each customer's latest five
    .ToListAsync();
```

- **A relation of one row is joined** into the query that reads its owners, sharing the join a condition on the same relation uses. It is null where there is no related row, as it is when it is not loaded.
- **A collection is read by one query after its owners**, `WHERE key IN (...)` with up to 1,000 owners' keys per query, so the query count follows the number of collections, not of rows. A loaded collection is never null: empty when there are no rows. `Where`, `OrderBy`/`ThenBy` and `Take` choose among each owner's rows; `Take` is per owner, with `ROW_NUMBER()` (on MySQL before 8.0, which has none, the rows are trimmed as they are read).
- Includes go into `ToListAsync`, `FirstAsync`, `SingleAsync` and projections, whose selector then runs on the loaded rows: `.Include(o => o.Customer).Select(o => o.Customer!.Name)`. Counts, `ExistsAsync` and aggregates ignore them.
- A relation is included once; `Where`, `OrderBy` and `Take` on a relation of one row are refused. Each row read is its own object: two orders of one customer have two `Customer` objects.

**Rows already read** get their relations with `LoadAsync`, which keeps a relation that is already loaded (not null) unless told `reload: true`:

```csharp
var orders = await db.SelectAsync<Order>(o => o.Status == OrderStatus.Open);
await db.LoadAsync(orders, q => q.Include(o => o.Customer).Include(o => o.Lines, l => l.Include(x => x.Product)));
```

**Rows already in memory** merge into each other with `DmMerge.Into`, by a relation's keys, fixed values and condition, or by keys given explicitly; no query is run:

```csharp
DmMerge.Into(orders, o => o.Customer, customers);
DmMerge.Into(orders, o => o.Lines, lines);
DmMerge.Into(products, p => p.Tags, tags, productTags);                  // many-to-many: the link rows too
DmMerge.Into(orders, o => o.Lines, lines, o => o.Id, l => l.OrderId);    // any property, by explicit keys
DmMerge.Into(rows, r => r.Parts, parts, r => (r.A, r.B), p => (p.A, p.B)); // a key of several values
```

Keys are compared as they are typed, never as strings and never by the rows' order. A relation of one row that several rows match throws.

## Updating and deleting by condition

```csharp
await db.UpdateAsync<Order>(
    o => new Order { Status = OrderStatus.Shipped, ShippedAt = now, Note = null },
    o => o.Status == OrderStatus.Packed && o.PlacedAt < cutoff);

await db.UpdateAsync<Order>(o => new Order { Note = o.CustomerName }, o => o.Id == id);   // from another column

await db.DeleteAsync<Order>(o => o.Status == OrderStatus.Cancelled);
await db.DeleteAllAsync<Order>();
```

The columns to set are an object initializer of the model; only the properties it names are set, and each value is a parameter or another column of the row. A condition is required: to update every row, say so with `o => true`, and to delete every row use `DeleteAllAsync`. An initializer that sets nothing returns 0 without a round trip.

## Patching

A patch sets the columns it has values for and leaves the rest unchanged, which suits a form or an API that sends only what changed:

```csharp
public class OrderPatch                     // any class, or an anonymous object
{
    public int? Id { get; set; }
    public OrderStatus? Status { get; set; }
    public string? Note { get; set; }
    public decimal? Discount { get; set; }
}

await db.PatchAsync<Order>(new OrderPatch { Id = id, Status = OrderStatus.Shipped });       // by the key it holds
await db.PatchAsync<Order>(new { Note = "late" }, where: o => o.PlacedAt < cutoff);         // by condition
await db.PatchAsync<Order>(patch, setNull: o => new { o.Note, o.Discount });               // and clear columns
```

- A patch property is matched to the model's property of the same name, or to the column of that name. A property the model has no column for is an error, not ignored: a misspelled one would otherwise silently update nothing.
- A **null property leaves its column unchanged**. To set a column to NULL, name it in `setNull`; naming a column that cannot be NULL, or one the patch also sets, is an error. So give a patch class nullable properties: a non-nullable `int Qty` always has a value, and is always set.
- Without `where`, the patch's key properties say which row. **A patch never changes a key**, and with `where` its key properties are ignored.
- A patch that sets nothing returns 0 without a round trip. Every error is raised before anything is sent.

## Upserting

```csharp
await db.UpsertAsync(new Setting { Key = "theme", Value = "dark" });    // inserts it, or updates the row with that key
```

`UpsertAsync` is one statement, so two upserts of the same key at once leave one row: `ON CONFLICT` on PostgreSQL and SQLite, `ON DUPLICATE KEY UPDATE` on MySQL and MariaDB, and on SQL Server a check holding a range lock, then an `UPDATE` or an `INSERT`, in one transaction (yours, or one of its own). It returns the row's key, as `InsertAsync` does.

Options say which row conflicts, what to set on it, and when:

```csharp
await db.UpsertAsync(stat, new()
{
    OnConflict = s => new { s.Day, s.Page },          // a unique constraint or index; null for the primary key
    Update = (current, incoming) => new PageStat       // null: every other column from the incoming row
    {
        Views = current.Views + incoming.Views,
        LastSeen = incoming.LastSeen,
    },
    UpdateWhere = (current, incoming) => incoming.LastSeen > current.LastSeen,   // otherwise left as it is
});

bool inserted = await db.InsertIfAbsentAsync(user, u => u.Email);   // DO NOTHING on a conflict
```

| Option | Meaning |
|--------|---------|
| `OnConflict` | The columns a conflicting row shares: the primary key (when null) or exactly the columns of a unique constraint or index, which PostgreSQL and SQLite require and which is checked before anything is sent |
| `OnConflictConstraint` | The same, by the constraint's or index's name (PostgreSQL writes `ON CONFLICT ON CONSTRAINT`) |
| `Update` | The columns to set, from `current` (the row as it is) and `incoming` (the row being upserted), with the same expressions as conditions: `current.Views + incoming.Views` |
| `UpdateWhere` | A condition the update needs; when it does not hold the row is left as it is, and nothing is inserted |

- With a key the database generates and a conflict on another unique key (upsert a user by email), the key of the inserted or the conflicting row is returned and set on the row, conditional or not.
- **A key the database generates is never inserted as given.** On the primary key itself, a row whose generated key is unset (`0`) is inserted and given one; a row whose key is set is updated, and if no row has that key, `UpsertAsync` throws rather than inserting a row with a key the database did not choose.
- On MySQL and MariaDB a duplicate of **any** unique key updates the row, not only the conflict columns named; and their assignments run left to right, so DapperMatic assigns the columns a condition reads last.
- `InsertIfAbsentAsync` returns whether it inserted; on MySQL a concurrent insert of the same key that wins the race is read as "present".

## Mass loading

```csharp
long loaded = await db.BulkInsertAsync(rows);                      // all or none
await db.BulkInsertAsync(rows, new DmBulkOptions { BatchSize = 50_000, Timeout = TimeSpan.FromMinutes(10) });

await db.BulkUpsertAsync(rows, new DmUpsertOptions<Product>
{
    OnConflict = p => p.Sku,
    Update = (current, incoming) => new Product { Price = incoming.Price },
});
```

`BulkInsertAsync` goes through each provider's own bulk path, reached through the driver the application already uses, so DapperMatic depends on none of them:

| Provider | Path | Notes |
|----------|------|-------|
| PostgreSQL (Npgsql) | binary `COPY ... FROM STDIN` | Each value written as its column's type, read from the catalog |
| SQL Server (Microsoft.Data.SqlClient, System.Data.SqlClient) | `SqlBulkCopy` | Told to check constraints, keep NULLs and fire triggers, as an `INSERT` would |
| MySQL, MariaDB (MySqlConnector) | `MySqlBulkCopy` | Needs `AllowLoadLocalInfile=true` and the server's `local_infile` (off by default from MySQL 8). `LOAD DATA LOCAL` reports errors as warnings, so any warning fails the load |
| SQLite | one prepared `INSERT`, run per row | SQLite's fastest way in |
| Without one (MySql.Data, MySQL without local infile) | multi-row `VALUES`, as `InsertManyAsync` | `Fallback = DmBulkFallback.Throw` refuses instead |

- Rows are read once, as they are loaded, so they can be streamed from a file or another query without being held in memory.
- The load runs in your transaction, or one of its own: either every row is inserted or none is.
- Keys the database generates are not set on the rows.
- Connection wrappers such as MiniProfiler's are looked through (`WrappedConnection`, `InnerConnection`, `Inner`).

::: warning AllowLoadLocalInfile lets the server ask for client files
`AllowLoadLocalInfile` is off by default because it lets the MySQL server request files from the client, which is how `LOAD DATA LOCAL` works. DapperMatic never turns it on. Turn it on only against servers you trust, ideally on a connection string used for bulk loads alone; without it, `BulkInsertAsync` falls back to multi-row `VALUES`.
:::

`BulkUpsertAsync` loads the rows into a temporary table with the same path, then merges them into the table in one statement (on SQL Server an `UPDATE` then an `INSERT`, both holding range locks), with the same options as `UpsertAsync`. The rows must not conflict with each other: PostgreSQL refuses to update a row twice in one statement. It cannot merge on a generated key, which new rows do not have.

## Queries

`From<T>()` starts a query, which runs only when one of its `...Async` methods is called:

```csharp
var query = db.From<Order>()
    .Where(o => o.PlacedAt >= since)
    .Where(o => statuses.Contains(o.Status))       // conditions are ANDed
    .OrderByDescending(o => o.PlacedAt)
    .ThenBy(o => o.Id);

var page    = await query.Skip(20).Take(10).ToListAsync();
var first   = await query.FirstAsync();             // null if there is none
var total   = await query.CountAsync();
var any     = await query.ExistsAsync();
var sum     = await query.SumAsync(o => o.Total);   // 0 for no rows, as in LINQ
var largest = await query.MaxAsync(o => o.Total);
var average = await query.AverageAsync(o => o.Total);
DmStatement sql = query.ToSql();                    // the SQL and parameters, without running it

// Pages by cursor: fast at any depth, never skipping or repeating a row; the cursors are the web API's too.
var page1 = await db.From<Order>().OrderByDescending(o => o.PlacedAt).ToPageAsync(50);
var page2 = await db.From<Order>().OrderByDescending(o => o.PlacedAt).After(page1.NextCursor).ToPageAsync(50);
```

A query is immutable: each call returns a new one, so a base query can be shared and refined. The calls compose into one `SELECT` whatever their order: `Distinct` applies before `Skip` and `Take`, and counts and aggregates of a paged query count and aggregate that page.

**Ordering** puts NULLs first ascending and last descending, as LINQ does, on every provider (PostgreSQL alone needs telling). Ordering keys can use the same functions as conditions: `OrderBy(o => o.CustomerName.ToLower())`. A paged query on SQL Server with no order is given one, as SQL Server requires.

**Projections** read each row as something else:

```csharp
var rows = await db.From<Order>()
    .Where(o => o.Shipped)
    .Select(o => new { o.Id, Customer = o.CustomerName.ToUpper(), Lines = o.Lines!.Count })
    .ToListAsync();

var statuses = await db.From<Order>().Select(o => o.Status).Distinct().ToListAsync();
```

Only the columns a selector uses are read; the rest of it runs in .NET on each row, so it can call any method. A selector that uses the row itself, or a property that is not a column (one computed from others, for example), reads every column. `Distinct` after `Select` needs a selector of columns only, since the database is what compares them.

`Min` and `Max` of a non-nullable value throw for no rows, as in LINQ, and return null for a nullable one. `AverageAsync` returns a `double?` with a fraction even for integers, or a `decimal?` for decimals, and null for no rows.

**Values of related rows** in a selector are read by the database, in the same `SELECT`, with no row loaded: a related row's column (joined), a count, sum, minimum, maximum or average of related rows, and `Any`, `All` and `== null` tests of them.

```csharp
var rows = await db.From<Customer>()
    .Select(c => new
    {
        c.Name,
        Orders = c.Orders!.Count(),
        Spent = c.Orders!.Sum(o => o.Total),
        Region = c.Region!.Name,
        Late = c.Orders!.Any(o => o.ShippedAt > o.DueAt),
    })
    .ToListAsync();
```

The rest of the selector runs in .NET on the values, where a missing related row's column is null: `(o.Customer!.Name ?? "none").ToUpper()`. A value that cannot be null in .NET throws when the database returns NULL for it; read it as nullable, `(int?)o.Customer!.Id`. An aggregate of a value that itself reads related rows (`c.Orders!.Sum(o => o.Lines!.Count())`) is refused: SQL Server refuses it, so every provider does.

## Grouping

`GroupBy` makes one `GROUP BY`. Each group is an `IGrouping<TKey, TElement>`, read through its `Key` and LINQ's aggregates, which the database computes:

```csharp
var byCountry = await db.From<Order>()
    .Where(o => o.PlacedAt >= since)                          // rows first
    .GroupBy(o => o.Customer!.Country)                        // a key that follows a relation is joined
    .Having(g => g.Count() > 10)
    .OrderByDescending(g => g.Sum(o => o.Total))
    .Take(5)
    .Select(g => new
    {
        Country = g.Key,
        Orders = g.Count(),
        Open = g.Count(o => o.Status == OrderStatus.Open),     // COUNT(CASE WHEN ... THEN 1 END)
        Total = g.Sum(o => o.Total),
        Largest = g.Max(o => o.Total),
        Average = g.Average(o => o.Total),
        AnyLate = g.Any(o => o.ShippedAt > o.DueAt),
    })
    .ToListAsync();

var lines = await db.From<OrderLine>()
    .GroupBy(l => new { l.OrderId, l.Product!.Category }, l => new { l.Qty, l.Product!.Price })   // a key of two values, an element of two
    .Select(g => new { g.Key.OrderId, g.Key.Category, Value = g.Sum(x => x.Qty * x.Price) })
    .ToListAsync();
```

- A group reads its `Key` (or the key's members), and `Count`, `LongCount`, `Sum`, `Min`, `Max`, `Average`, `Any` and `All`, each after any `Where` (`g.Where(o => o.Paid).Sum(o => o.Total)`), which becomes a `CASE WHEN` inside the aggregate. Anything else of a group, such as `g.First()`, is refused.
- `Sum` is 0 when no value counts, as in C#; `All` is true when no row fails.
- `Having`, `OrderBy` and `ThenBy` (key or aggregates), `Skip` and `Take` apply to the groups, and `CountAsync` counts them. `GroupBy` follows `Where`: a query already ordered, paged, distinct or with includes is refused.
- The key's values are written once and reused in the select list, `HAVING` and `ORDER BY`, so a key computed with a value (`o.Total > limit`) has one parameter everywhere, as PostgreSQL requires.

## Joins written out

For models with no relation property between them, or a join a relation does not describe, `Join` and `LeftJoin` take the condition written out; `Where`, `OrderBy` and `Select` then see every joined row:

```csharp
var rows = await db.From<Order>()
    .Where(o => o.PlacedAt >= since)                              // the query's own conditions stay
    .Join<Shipment>((o, s) => s.OrderNumber == o.Number)
    .LeftJoin<Carrier>((o, s, c) => c.Code == s.CarrierCode)      // up to four models
    .Where((o, s, c) => c.Name != "Internal")
    .OrderBy((o, s, c) => s.ShippedAt)
    .Select((o, s, c) => new { o.Number, s.ShippedAt, Carrier = c.Name, Lines = o.Lines!.Count() })
    .ToListAsync();

List<(Order, Shipment?)> pairs = await db.From<Order>().LeftJoin<Shipment>((o, s) => s.OrderNumber == o.Number).ToListAsync();
```

- A `LeftJoin`'s row is null where none matched, and its columns compare as C# would compare a null row's: `c.Name != "Internal"` keeps the orders with no carrier. `c == null` tests whether one matched.
- A join's condition is over the joined rows' own columns; conditions after it can follow relations.
- `ToListAsync` and `FirstAsync` read tuples of the rows; `Select` reads only what the selector uses (whole rows only where it uses a row itself), with `Distinct` compared by the database. `CountAsync`, `ExistsAsync`, `Skip`, `Take` and `Distinct` work as on a query, and `GroupBy((o, s) => key, (o, s) => element)` groups the joined rows.

## Columns the methods fill and filter

Five attributes mark columns the methods look after themselves, on every method that reads or writes the model:

```csharp
public class Note
{
    [DmColumn("id", isPrimaryKey: true, isAutoIncrement: true)] public int Id { get; set; }

    [DmTenantKey] [DmColumn("tenant_id")] public string TenantId { get; set; } = "";
    [DmConcurrencyToken] [DmColumn("version")] public int Version { get; set; }
    [DmCreatedAt] [DmColumn("created_at")] public DateTime CreatedAt { get; set; }
    [DmUpdatedAt] [DmColumn("updated_at")] public DateTime UpdatedAt { get; set; }
    [DmCreatedBy] [DmColumn("created_by", isNullable: true)] public string? CreatedBy { get; set; }
    [DmUpdatedBy] [DmColumn("updated_by", isNullable: true)] public string? UpdatedBy { get; set; }
    [DmSoftDelete] [DmColumn("deleted_at", isNullable: true)] public DateTime? DeletedAt { get; set; }
}

var tenant = db.ForTenant(tenantId).WithUser(userName);
await tenant.InsertAsync(note);          // tenant, times, users and token set on the row
await tenant.UpdateAsync(note);          // checked against the token it was read with
await tenant.DeleteAsync(note);          // sets DeletedAt; reads leave the row out from then on
```

**`[DmSoftDelete]`**, on a `bool` or a nullable `DateTime`/`DateTimeOffset`: every delete (`DeleteAsync`, `DeleteByIdAsync`, by condition, `DeleteAllAsync`) sets it instead of removing the row, and every read leaves deleted rows out: queries, by-key reads, counts, relation conditions, includes and joins. `IncludeDeleted()` on a query or a scope reads them too; `OnlyDeleted()` reads only the query's deleted rows (with its relations' rows that are not deleted). Updates by condition skip deleted rows; writes of a row by its key do not. `HardDeleteAsync`, `HardDeleteByIdAsync` and `HardDeleteAllAsync` remove rows, deleted or not.

**`[DmConcurrencyToken]`**, on an `int` or `long` (incremented), a `Guid` (new on each write) or a `byte[]` (new random bytes; on SQL Server a `rowversion` column, `providerDataType: "{sqlserver:rowversion}"`, which the database writes and the methods read back). A row written by its key (`UpdateAsync(row)`, `DeleteAsync(row)`, `HardDeleteAsync(row)`, and `PatchAsync` with a patch that holds the token) is written only if its token is still the one it was read with, else `DmConcurrencyException` is thrown and nothing is written. On success the new token is set on the row. Writes by condition, and patches without the token, change it unchecked. A row never read, whose token is its type's default, is refused by an existing row's token: read it first.

**`[DmCreatedAt]` and `[DmUpdatedAt]`**, on a `DateTime` or `DateTimeOffset`, are set from `DmDefaults.Clock` (a `TimeProvider`, the system clock unless changed) in UTC; a `DateTime` is of kind `Unspecified`, which every provider stores as it is. **`[DmCreatedBy]` and `[DmUpdatedBy]`** take the scope's `WithUser(user)`, or `DmDefaults.User`, converted to the column's type; with no user they are left as they are. Inserts set all four; every update sets the `Updated` ones (updates by condition and upserts too), and none changes the `Created` ones.

**`[DmTenantKey]`**: every method reads and writes only the scope's tenant's rows, `ForTenant(id)` or `DmDefaults.TenantId`, and sets it on inserted rows. A row of another tenant cannot be inserted or updated by key, an update by condition cannot move rows to another tenant, and an upsert does not update another tenant's conflicting row. With no tenant, every method refuses the model with `InvalidOperationException`; `AllTenants()` reads and writes every tenant's rows, for administration and background work.

`DmDefaults` holds the providers a scope does not override, set once at startup and called on every statement that needs them:

```csharp
DmDefaults.TenantId = () => currentTenant.Id;          // null for none
DmDefaults.User = () => currentUser.Name;
DmDefaults.Clock = TimeProvider.System;
```

In an ASP.NET Core application, `app.UseDapperMaticRequestValues(tenant: http => ..., user: http => ...)` sets them from the current request, and `app.UseDapperMaticCallerValues()` from the caller DapperMatic's tenancy finds: see [The same tenant in typed code](./web-api/tenancy#the-same-tenant-in-typed-code).

## Tenant schemas and other tables

Many SaaS applications keep each tenant in a schema of its own. A scope runs the same calls against another schema, or another table:

```csharp
var tenant = db.InSchema(tenantSchema);                                   // every model, in that schema
var order = await tenant.SingleByIdAsync<Order>(id);
var open = await tenant.From<Order>().Where(o => o.Status == OrderStatus.Open).ToListAsync();

var archive = db.InSchema("archive").WithTableName<Order>("orders_2024");   // one model, another table
await archive.InsertAsync(order);
```

A scope (`DmScope`) has every method the connection has. It holds only the connection and the names it substitutes, so one can be made per request. Substituted names are quoted like every other name, keeping only letters, digits and underscores, so a tenant id taken from a request cannot carry SQL; checking that it is a real tenant is still the application's job. `InSchema` throws on a provider without schemas (SQLite, and MySQL, where a schema is a database) rather than ignoring the schema.

**Shared models.** Some models every tenant shares (plans, settings, the tenants themselves). Declare them once at startup, and no scope's schema moves them:

```csharp
DmSchemaLayout.Configure(layout => layout
    .SharedSchema("public")
    .Shared<Plan>()
    .Shared(typeof(Tenant), typeof(Setting)));

var tenant = db.InSchema("tenant_42");
await tenant.SelectAsync<Order>();       // tenant_42.orders
await tenant.SelectAsync<Plan>();        // public.plans
await tenant.From<Order>().Where(o => o.Plan!.Name == "pro").ToListAsync();   // joins across the two
```

`DmDefaults.TenantSchema` gives the schema a scope does not, such as the current request's tenant's (`UseDapperMaticRequestValues(schema: ...)` in ASP.NET Core); look it up from a registry rather than building it from input. The layout is inert in a deployment with one schema, so an application can start single-tenant and later move tenants into schemas of their own with configuration alone, the first tenant staying in the shared schema. The migrator creates the schemas: see [A schema per tenant](./migrations#a-schema-per-tenant).

## Running as a caller

By default the methods run as trusted code: they read and write every row. Code that acts for a user can run as that user instead, held to the same [policies](/guide/web-api/security) the web API holds requests to:

```csharp
var access = new DmAccessContext(user) { Tenant = new DmTenant(42) };
var db = await connection.AsCallerAsync(access, policies, "main");   // "main": the datasource the policies name

var invoices = await db.SelectAsync<Invoice>();          // the tenant's invoices only
await db.InsertAsync(new Invoice { Amount = 10 });       // tenant_id written by the policy
```

In ASP.NET Core, `DapperMaticCallerDb` opens a datasource for the current request's caller, with the web API's policies, the tenant's schema and the database's session values:

```csharp
app.MapGet("/my-invoices", async (DapperMaticCallerDb callerDb) =>
{
    var db = await callerDb.OpenAsync("main");
    return await db.SelectAsync<Invoice>(i => i.Status == "open");
});
```

What changes:

- **Every table needs a policy granting the action.** Reading or writing one no policy grants throws `UnauthorizedAccessException`.
- **Reads see the rows the policies' filters let through**, on every table a statement reads: the model's, those its relations, includes and joins reach.
- **A column the caller may not read comes back empty** (null, 0) in the objects read, and cannot be compared, ordered, grouped or summed: a condition naming it throws.
- **Writes are held as the web API holds them.** Inserts get the policies' values (`Preset`) and must pass their check; updates and deletes reach only the rows the filters let through, and updates must leave rows that pass the check, or nothing is changed. A row the policies hide is not there: `UpdateAsync(row)` on it updates nothing. A column the caller may not write is left as it is by writes of whole objects; a patch or an assignment naming it is refused.
- **Bulk inserts** load into a temporary table, check every row there, and copy them all or none. **Upserts** update the row with the same key when the caller may change it, insert when there is none, and are refused when a row exists that the caller may not change. Run as a caller, an upsert's own `Update` and `UpdateWhere` are not supported, and a bulk upsert writes row by row in one transaction.
- **A value the caller does not have** (no tenant) reads as PostgreSQL reads a null: `tenant_id eq @tenant.id` matches no row, and a preset of it refuses the write.

The request's values the policies read are read once, when the scope is made. A policy's condition may read only columns the model maps. Dapper's own `QueryAsync` and `ExecuteAsync` with SQL are not held to the policies.

## Models known at run time

Every method that takes a model as a type argument has an overload that takes it as a `Type`, for code that finds models at run time: scanning an assembly, or serving a generic endpoint.

```csharp
foreach (var type in modelTypes)
{
    var count = await db.CountAsync(type);
    var rows = await db.SelectAsync(type);              // List<object>, each a row of that type
}

await db.InsertAsync(type, row);
var one = await db.SingleByIdAsync(type, key);         // object?
```

The same holds for the [DDL methods](./extension-methods/): `CreateTableIfNotExistsAsync(type)`, `ReconcileTableAsync(type)`, `GetColumnNamesAsync(type)` and the rest. The exceptions are the methods that take expressions over the model, such as conditions and `From<T>()`, which need the model at compile time.

A row passed as `object` to a generic method is a row of its own type: `db.InsertAsync((object)order)` inserts an `Order`.

## Differences between providers

Some behaviour is the database's, and is documented rather than emulated:

- **Case.** `==` and `LIKE` follow the column's collation: case-insensitive by default on SQL Server and MySQL, case-sensitive on PostgreSQL, and on SQLite case-sensitive `=` but case-insensitive ASCII `LIKE`. Use the `IgnoreCase` overloads for a match that ignores case everywhere.
- **Trailing spaces.** SQL Server, and MySQL with a PAD SPACE collation, compare `'a '` equal to `'a'`. (`string.IsNullOrEmpty` and `Length` are written to be exact.)
- **Trimming** removes spaces only, where C# also removes tabs and line breaks.
- **`Replace`** follows the collation on SQL Server, so it may replace regardless of case.
- **Floating-point** equality is whatever the column type gives.

## What the methods do not do

- **Unions, window functions, common table expressions and joins of more than four models.** Use Dapper's own `QueryAsync` with SQL for those; the models and type mapping work there too.
- **Change tracking, identity maps, lazy loading.** This is not an ORM session: every call is one statement or a few.
- **An `IQueryable` provider.** Queries are built with `From<T>()`, not LINQ query syntax.

A file that imports both `MJCZone.DapperMatic.Dml` and Dapper.Contrib's `Dapper.Contrib.Extensions` gets ambiguous calls to `InsertAsync`, `UpdateAsync` and `DeleteAsync`, which both define on `IDbConnection`. Import one of them in a given file.
