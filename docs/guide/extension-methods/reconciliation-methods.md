# Reconciliation Methods

Reconciliation brings an existing table in line with the model that describes it, by creating whatever the model has and the database is missing — columns, indexes, and constraints — in a single call.

It is **additive by default**. Nothing is ever dropped, and nothing is altered unless you ask. A column that exists but no longer matches the model is reported to you and left exactly as it is. If you ask for [safe column alterations](#safe-column-alterations), reconciliation also makes the changes that cannot lose data, such as lengthening a column or allowing nulls, and still reports the rest.

## Quick Navigation

- [Reconciling a table](#reconciling-a-table)
  - [ReconcileTableAsync](#reconciletableasync) - Add whatever the model describes and the database lacks
- [Previewing the work](#previewing-the-work)
  - [GetTableReconciliationPlanAsync](#gettablereconciliationplanasync) - Compute the diff without touching the database
- [Drift](#drift) - What is reported but deliberately not changed
- [Safe column alterations](#safe-column-alterations) - The changes that cannot lose data, when you ask for them
- [Renames](#renames) - Elements renamed in place instead of created again
- [Blocked elements](#blocked-elements) - What could not be created or altered, and why
- [Narrowing the scope](#narrowing-the-scope) - The `DmReconcileElements` filter
- [Fingerprints](#fingerprints) - Skipping tables whose model has not changed
- [What reconciliation does not do](#what-reconciliation-does-not-do) - Known limits

## Reconciling a table

### ReconcileTableAsync

```csharp
var result = await connection.ReconcileTableAsync<User>();

if (result.TableCreated)
{
    Console.WriteLine("Created the table from scratch.");
}

foreach (var addition in result.Applied)
{
    Console.WriteLine($"Added {addition}");
    // Added Column 'email'
    // Added Index 'ix_users_email'
}
```

If the table does not exist, reconciliation creates it, along with everything the model describes, and sets `TableCreated`. A schema the model names that does not exist yet is created first. The [element filter](#narrowing-the-scope) does not apply in that case — it narrows what may be added to a table that is already there, rather than carving up a `CREATE TABLE` into something the model does not describe.

If the table does exist, reconciliation adds only what is missing, in this order:

1. Columns
2. Primary key — only when the table has none at all
3. Unique constraints
4. Check constraints
5. Default constraints
6. Indexes
7. Foreign keys — **last**, so that columns added in the same call are available to reference

Reconciliation is idempotent. Running it twice against an unchanged model does nothing the second time, so it is safe to call on every application start.

You can also pass a `DmTable` directly rather than a type:

```csharp
var table = DmTableFactory.GetTable(typeof(User));
var result = await connection.ReconcileTableAsync(table);
```

## Previewing the work

### GetTableReconciliationPlanAsync

Computing a plan **never modifies the database**. Use it to log, approve, or gate the change before it happens.

```csharp
var plan = await connection.GetTableReconciliationPlanAsync<User>();

if (!plan.HasChanges)
{
    logger.LogInformation("Schema is current.");
    return;
}

foreach (var addition in plan.Additions)
{
    logger.LogInformation("Will add {Element}", addition);
}

await connection.ReconcileTableAsync<User>();
```

The plan exposes both a flat `Additions` list for logging and the typed collections behind it — `MissingColumns`, `MissingIndexes`, `MissingUniqueConstraints`, and so on — when you need the detail.

::: tip A plan is a snapshot
A plan reflects the schema as it was when the plan was computed. `ReconcileTableAsync` recomputes rather than accepting a plan you hand it, so a stale plan can never be applied against a database that has moved on.
:::

## Drift

Drift is an element that exists on **both** sides but does not match. It is reported and left alone. The column differences that cannot lose data can be applied instead, with [safe column alterations](#safe-column-alterations).

```csharp
var result = await connection.ReconcileTableAsync<User>();

foreach (var d in result.Drift)
{
    logger.LogWarning("Schema drift: {Drift}", d);
    // Schema drift: Column 'email': expected length 320, found length 100
}
```

Use it as a CI guard:

```csharp
var plan = await connection.GetTableReconciliationPlanAsync<User>();
if (plan.HasDrift)
{
    throw new InvalidOperationException(
        $"Database has drifted from the model: {string.Join("; ", plan.Drift)}");
}
```

What is compared, and what deliberately is not:

| Element | Compared | Not compared |
|---------|----------|--------------|
| Column | Nullability, length, precision, scale | The SQL type name |
| Primary key | Columns covered | The constraint name |
| Unique constraint | Columns covered, and whether an equivalent exists under another name | — |
| Index | Columns covered and their order, uniqueness | — |
| Check constraint | Presence by name | The expression |
| Default constraint | Presence by column | The expression |
| Foreign key | Source columns, referenced table and columns, `ON DELETE` / `ON UPDATE` actions, and whether an equivalent exists under another name | The difference between `NO ACTION` and `RESTRICT` |

The omissions are deliberate. Providers rewrite what you give them: a check expression comes back with added casts and parentheses, a default comes back as `'x'::text` or `((0))`, and type names are normalised (`character varying` for `varchar`). Comparing those textually reports drift on objects that are in fact identical, and a drift report nobody trusts is worse than one that says less.

Column facets are only compared when **both** sides state a positive value. A column declared as plain `text` reports no length at all, so comparing a model's length against it would flag every such column.

A column that exists in the database but not in the model is reported as drift too, and is never dropped.

## Safe column alterations

After adding a column, the most common change to a schema is lengthening one or allowing nulls. Pass `applySafeAlterations: true` and reconciliation makes those changes too: the ones that cannot lose data and cannot fail on the rows already there. It is off by default, so nothing existing is altered unless you ask.

```csharp
var result = await connection.ReconcileTableAsync<User>(applySafeAlterations: true);

foreach (var a in result.Altered)
{
    Console.WriteLine($"Altered {a}");
    // Altered Column 'email': length 100 to 320
    // Altered Column 'nickname': NOT NULL to NULL
}
```

| Difference | With `applySafeAlterations` |
|------------|------------------------|
| A string or binary column is longer in the model | Lengthened |
| A decimal has more precision in the model, and no digit is lost | Widened. `decimal(10,2)` to `(12,2)` qualifies; `(10,2)` to `(10,4)` does not, because it leaves two fewer digits before the point |
| The model allows nulls and the column does not | Nulls allowed (never on a primary key column) |
| Shorter, less precise, or `NOT NULL` in the model | Drift, left alone: these can lose data or fail on existing rows |
| A different type, including `int` to `bigint` | Drift, left alone |

Only the differing facet changes. The alteration restates the column's type exactly as the database reports it, so `nvarchar(50)` becomes `nvarchar(200)` and never `varchar(200)`. The column keeps its collation, default and comment; see [AlterColumnIfExistsAsync](./column-methods#altercolumnifexistsasync) for how each provider does that.

A plan lists these in `plan.Alterations`, separately from drift. Without `applySafeAlterations`, the same differences appear in `plan.Drift` instead.

Some providers refuse an alteration that is safe for the data. PostgreSQL will not change the type of a column a view depends on, and SQL Server will not change the precision of a column an index covers. Those are reported as [blocked](#blocked-elements) with the reason, and the rest of the reconciliation goes ahead. Inside a transaction, a refused alteration is undone to a savepoint so the transaction stays usable. SQL Server ends the whole transaction when it refuses, so the cases it is known to refuse are blocked in the plan instead of attempted.

## Renames

Declare an element's earlier names with `PreviousNames` on its [annotation](../data-annotations#renaming-with-previousnames), and reconciliation renames it rather than creating a new, empty one beside it.

```csharp
[DmTable("app", "customers", PreviousNames = new[] { "users" })]
public class Customer
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }

    [DmColumn("email_address", length: 320, PreviousNames = new[] { "email" })]
    public string EmailAddress { get; set; } = default!;
}

var result = await connection.ReconcileTableAsync<Customer>();

foreach (var r in result.Renamed)
{
    Console.WriteLine($"Renamed {r}");
    // Renamed Table 'users' to 'customers'
    // Renamed Column 'email' to 'email_address'
}
```

Renames are made first, in the order table, columns, then indexes and constraints. Everything else in the plan is worked out against the table as it will be once renamed. A plan lists them in `plan.Renames`.

The rule is the same for every element:

| In the database | What happens |
|-----------------|--------------|
| The current name exists | Nothing |
| Exactly one previous name exists | Renamed |
| None of the names exists | Created, as usual |
| The current name and a previous one both exist | Nothing. The old one is reported as drift |
| Two or more previous names exist | [Blocked](#blocked-elements). Nothing is renamed or created, because picking one would be a guess |

**Generated names follow on their own.** An index or constraint without an explicit name gets one built from its table and columns, such as `ix_users_email`. That name changes when the table or column is renamed, and there is nowhere to declare its old form. Reconciliation works the old forms out from the table's and the columns' previous names, and matches them, so nothing is created twice. It renames them where the provider can do that in place: PostgreSQL and SQL Server for every kind, MySQL and MariaDB for indexes and unique constraints, and SQLite for indexes. Elsewhere the element keeps its old generated name, because rebuilding a table or re-adding a foreign key costs more than a generated name is worth.

Renames follow the element filter like everything else: column renames need `DmReconcileElements.Columns`, index renames need `Indexes`, and so on. The exception is a table found under a previous name, which is renamed for any filter except `None`, because creating it afresh would leave the data behind under the old name.

**Undeclared renames are not guessed.** If exactly one model column is missing and one database column is unaccounted for, and they have the same type, the drift for the database column says it may have been renamed and suggests `PreviousNames`. Nothing is renamed on a guess, because renaming the wrong column is worse than adding an empty one.

::: warning Renames and overlapping deploys
While an old and a new version of an application share a database, as in a rolling deploy, the old version's model still has the old name. If it reconciles, it adds the old column back, empty. Deploy renames to a database that only the new version is using.
:::

## Blocked elements

An index or constraint can only be created once the column it names exists. When a column is missing and is not being added — because you narrowed the filter — the element that depends on it is reported as **blocked** rather than attempted, since issuing that DDL would fail.

```csharp
var result = await connection.ReconcileTableAsync<User>(DmReconcileElements.Indexes);

foreach (var b in result.Blocked)
{
    logger.LogWarning("Blocked: {Blocked}", b);
    // Blocked: Index 'ix_users_email' (requires column(s) email, which are not
    // present and not being added)
}
```

Widening the filter, or reconciling columns first, clears the block.

An element is also blocked when the database cannot hold it, or declines to create it. Check constraints on MySQL before 8.0.16 are the common case: the server has none, so they are reported as blocked rather than planned as work that would never happen, with `Unsupported` set. The [migrator](../migrations) records a table whose only blocked elements are unsupported ones, since no later run would add them.

```csharp
// Blocked: CheckConstraint 'ck_orders_qty' (this database server does not support check constraints)
```

Every element in a plan ends up in exactly one place after reconciling: `Applied`, or `Blocked` with a reason.

## Narrowing the scope

`DmReconcileElements` is a `[Flags]` enum that limits which kinds of element reconciliation may create. It never enables a destructive operation — there isn't one to enable.

```csharp
// Add missing columns and nothing else.
await connection.ReconcileTableAsync<User>(DmReconcileElements.Columns);

// Columns and indexes, but leave constraints alone.
await connection.ReconcileTableAsync<User>(
    DmReconcileElements.Columns | DmReconcileElements.Indexes);

// Report drift without changing anything at all.
var plan = await connection.GetTableReconciliationPlanAsync<User>(DmReconcileElements.None);
```

| Flag | Effect |
|------|--------|
| `None` | Report drift only |
| `Columns` | Missing columns |
| `PrimaryKey` | A primary key, only when the table has none |
| `UniqueConstraints` | Missing unique constraints |
| `CheckConstraints` | Missing check constraints |
| `DefaultConstraints` | Missing default constraints |
| `Indexes` | Missing indexes |
| `ForeignKeyConstraints` | Missing foreign keys, applied last |
| `All` | Every kind (the default) |

Drift is reported for every kind of element whatever the filter, which only decides what is planned.

::: warning Creating a column creates its index
A column marked as indexed — `[DmIndex]` on the property, or `[DmColumn(isIndexed: true)]` — has its index created along with the column. Filtering to `DmReconcileElements.Columns` will therefore still produce that index, under a name generated from the table and column. When indexes are within the filter, the index is created separately under the name the model gives it. The same applies to a column's unique, check, default and foreign key constraints.
:::

## Fingerprints

Reconciling a table reads it back from the database first, which costs several catalog queries per table. When the model has not changed since the table was last reconciled there is nothing to find, so `DmTableFingerprint` gives each model a fingerprint you can store and compare instead:

```csharp
var model = DmTableFactory.GetTable(typeof(User));
var fingerprint = DmTableFingerprint.Compute(model);   // 64 hex characters, SHA-256

if (fingerprint != storedFingerprint)
{
    var result = await connection.ReconcileTableAsync(model);
    if (result.Blocked.Count == 0)
    {
        storedFingerprint = fingerprint;   // record it only once nothing is outstanding
    }
}
```

The [migrator](../migrations) does exactly this for a whole set of tables, with a ledger table, a lock and rename history, so most applications never call `DmTableFingerprint` themselves.

The fingerprint covers everything reconciliation acts on: the table's schema, name and previous names, each column in order with every facet, the primary key, and every unique, check and default constraint, index and foreign key. It is the same in every process, on every machine, in every culture and on every runtime version, and it ignores order where order means nothing: the order constraints and indexes are declared in, and the order of a column's provider data types.

It errs towards changing. Editing the expression of a check constraint that already exists changes it, although reconciliation leaves existing expressions alone; that costs one reconcile that finds nothing to do. Missing a change would skip it, which is the mistake that matters. A test mutates every facet of every model type in turn and fails if the fingerprint stays the same.

A few things to know:

- **Expressions given as functions** (`DefaultExpressionFunc`, `CheckExpressionFunc`) are recorded as present or absent, because a delegate cannot be compared.
- **`DmTableFactory.Configure`** runs your code on each model. It must configure the model identically in every instance that shares a database, or each instance will see the others' fingerprints as changes.
- **Upgrading DapperMatic** can change what `DmTableFactory` produces from the same class. Every table is then reconciled once, finds nothing, and is recorded again.
- **Out-of-band changes are invisible to it.** A fingerprint describes the model, not the database, so an index dropped by hand is not noticed until something reconciles the table anyway.
- To find out why a fingerprint changed, compare two `DmTableFingerprint.GetCanonicalForm(model)` strings. They have one line per element.

## What reconciliation does not do

These are limits of the primitive, not oversights:

- **It never drops, and alters only what cannot lose data.** Removing a column, shortening one, changing its type, or replacing a constraint is your call, using the `Drop*`, `Rename*` and `AlterColumnIfExistsAsync` methods directly.
- **It does not guess renames.** A rename has to be declared with `PreviousNames`; see [Renames](#renames). An undeclared one is reported as drift, with a hint.
- **It does not track versions.** The primitive keeps no bookkeeping table: it compares the model to the database every time, which is what makes it idempotent. Skipping unchanged tables is the job of [fingerprints](#fingerprints) and the [migrator](../migrations).
- **It handles one table.** Reconciling several tables that reference each other means calling it per table; a foreign key to a table that does not exist yet will fail, so create tables before wiring up references, or reconcile in dependency order.
- **It cannot verify referenced columns.** For a foreign key, only the source columns are checked before the attempt. The referenced table belongs to another model, so the database validates that half.

::: warning Transactional DDL differs by provider
Pass a transaction if you want all-or-nothing behaviour, and note that PostgreSQL, SQL Server and SQLite roll DDL back cleanly while MySQL and MariaDB commit implicitly on most DDL statements — a failure part-way through will leave earlier statements applied there. On SQLite, adding a column or constraint recreates the table, and inside a transaction that is refused for a table other tables reference while foreign keys are on; see [SQLite](../providers#sqlite).
:::
