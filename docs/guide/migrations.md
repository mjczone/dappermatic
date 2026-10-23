# Migrations

`DmMigrator` keeps a database's tables in line with their models, typically when an application starts. It [reconciles](./extension-methods/reconciliation-methods) every table whose model changed since the last run, and only those: an application whose models have not changed starts with a single ledger query, without reading a single table back from the database.

```csharp
using MJCZone.DapperMatic.Migrations;

var migrator = new DmMigrator(new DmMigrationOptions { AppliedBy = Environment.MachineName })
    .AddTable<Customer>()
    .AddTable<Order>()
    .AddTable<OrderLine>();

await using var connection = new NpgsqlConnection(connectionString);
var result = await migrator.MigrateAsync(connection);
```

The migrator is part of the `MJCZone.DapperMatic` package and supports every provider it does, so there is nothing more to install.

## Quick Navigation

- [What a run does](#what-a-run-does) - Fingerprints, the fast path, and the two passes
- [Registering tables](#registering-tables)
- [The ledger](#the-ledger) - The table that records each model's fingerprint
- [Results and logging](#results-and-logging)
- [Blocked tables](#blocked-tables) - What is not recorded, and is tried again
- [Renames during a deploy](#renames-during-a-deploy) - Why an older model is left alone
- [Forcing a full check](#forcing-a-full-check)
- [Several instances at once](#several-instances-at-once) - The lock
- [Transactions](#transactions) - What happens when a run fails part-way
- [Options](#options)
- [A schema per tenant](#a-schema-per-tenant) - `MigrateSchemasAsync`, shared models migrated once
- [What the migrator does not do](#what-the-migrator-does-not-do)

## What a run does

Each model gets a [fingerprint](./extension-methods/reconciliation-methods#fingerprints): a SHA-256 hash of everything reconciliation acts on, which is the same in every process and on every machine. The ledger records, per table, the fingerprint of the model the table was last reconciled to.

1. **The fast path.** The migrator reads the ledger. If every model's fingerprint matches the one recorded, it returns: no lock, no transaction, no catalog queries. Every instance of an unchanged application pays for a check that the ledger exists and one read of it, however many tables it has.
2. **The lock.** Otherwise it takes the [lock](#several-instances-at-once), and reads the ledger again under it, because another instance may have migrated in the meantime. A missing ledger is created here.
3. **Reconciling, in two passes.** Each table that needs it is reconciled with everything except foreign keys, then each is reconciled again for its foreign keys. By the second pass every table and column a foreign key could reference exists, so tables can reference each other in any registration order, and in cycles.
4. **Recording.** A table whose reconcile left nothing blocked has its new fingerprint recorded. [Drift](./extension-methods/reconciliation-methods#drift) does not prevent recording: it is reported and never acted on, so checking it on every start would only keep the table from ever being skipped.

| Recorded for the table | Status | What happens |
|------------------------|--------|--------------|
| Nothing | `NotRecorded` | Reconciled. The first run against an existing database adopts it this way; there is no baseline step. |
| The model's fingerprint | `Unchanged` | Skipped |
| A different fingerprint | `ModelChanged` | Reconciled |
| A fingerprint a rename replaced | `Superseded` | Skipped, with a warning. See [Renames during a deploy](#renames-during-a-deploy). |
| Anything, in a run with `force: true` | `Forced` | Reconciled |

Reconciling uses safe column alterations by default: a column whose model has a greater length, more precision or allows nulls is altered to match. See [Safe column alterations](./extension-methods/reconciliation-methods#safe-column-alterations); set `ApplySafeAlterations = false` to leave existing columns alone. The setting is not part of the fingerprint: turned on later, it reaches only tables whose model changes, unless the run is [forced](#forcing-a-full-check).

## Registering tables

```csharp
migrator.AddTable<Customer>();                          // a class, as DmTableFactory reads it
migrator.AddTable(customersTable);                      // a DmTable built in code
migrator.AddTablesFromAssembly(typeof(Program).Assembly);                       // every [DmTable] class
migrator.AddTablesFromAssembly(typeof(Program).Assembly, t => t.Namespace == "Shop.Data");
```

Classes found by scanning are registered in order of their full name. Registration order does not matter for foreign keys. Registering two models for the same table is an error. In a trimmed application, prefer `AddTable<T>()`, which scanning cannot miss.

## The ledger

The ledger is a table named `dm_table_state` in the provider's default schema, created on the first run. `SchemaName` and `TableStateTableName` change where it lives; both must be plain identifiers (letters, digits and underscores), since they go into SQL the migrator writes.

| Column | Holds |
|--------|-------|
| `table_key` | The table's schema-qualified name, with the schema resolved, in lowercase. The primary key. |
| `schema_name`, `table_name` | The same, split out for whoever reads the ledger |
| `model_hash` | The fingerprint of the model the table was last reconciled to |
| `rename_history` | The renames reconciling the table made, as JSON. See [Renames during a deploy](#renames-during-a-deploy). |
| `reconciled_at_utc` | When |
| `applied_by` | `DmMigrationOptions.AppliedBy`, such as a machine name |
| `library_version` | The DapperMatic version that reconciled it |

Nothing in it needs editing by hand. To have every table checked again, run with [`force`](#forcing-a-full-check); to have one table checked again, delete its row.

## Results and logging

`MigrateAsync` returns a `DmMigrationResult` with one entry per registered table, in registration order:

```csharp
var result = await migrator.MigrateAsync(connection);

foreach (var table in result.Reconciled)
{
    var r = table.Reconciliation!;      // the same DmTableReconciliationResult ReconcileTableAsync returns
    Console.WriteLine($"{table.State.TableKey}: {r.Applied.Count} added, {r.Altered.Count} altered");
}

if (!result.IsComplete)
{
    // result.Pending: tables left with blocked elements, reconciled again on the next run
}
```

| Outcome | Meaning |
|---------|---------|
| `Unchanged` | Skipped; `Reconciliation` is null |
| `Superseded` | Skipped, because a newer model renamed things away from this one |
| `Reconciled` | Reconciled, and the new fingerprint recorded |
| `Pending` | Reconciled, but something was blocked, so not recorded |

`DmMigrationOptions.Log` receives each step as it happens. With the [ASP.NET Core package](./web-api/getting-started), `UseLogger` sends the steps to an `ILogger`:

```csharp
using MJCZone.DapperMatic.AspNetCore;
using MJCZone.DapperMatic.Migrations;

var app = builder.Build();

var options = new DmMigrationOptions { AppliedBy = Environment.MachineName }
    .UseLogger(app.Services.GetRequiredService<ILogger<DmMigrator>>());

var migrator = new DmMigrator(options).AddTablesFromAssembly(typeof(Program).Assembly);
```

Each step is logged at its own level (`Debug`, `Information` or `Warning`), with an event id named after its `DmMigrationEventKind`. A step about one table carries the table's ledger key as the `TableKey` property, so structured logging can filter on it. Unchanged tables are logged at `Debug`, so at the usual `Information` level a run with nothing to do writes only its `Completed` line.

Without that package, map the callback onto any logger yourself:

```csharp
var options = new DmMigrationOptions
{
    Log = e => logger.Log(
        e.Level switch
        {
            DmMigrationEventLevel.Debug => LogLevel.Debug,
            DmMigrationEventLevel.Warning => LogLevel.Warning,
            _ => LogLevel.Information,
        },
        "{Message}",
        e.Message),
};
```

`GetPlanAsync` works out what a run would do from the ledger and the models alone, without changing anything or taking the lock. Its `ChangedTables` are the tables a run would reconcile; what reconciling them would do takes the catalog queries the ledger exists to avoid, so it is not part of the plan.

## Blocked tables

A table is recorded only when its reconcile left nothing [blocked](./extension-methods/reconciliation-methods#blocked-elements): an element the database would not create, an alteration it refused, a table found under more than one previous name. Otherwise it is reported as `Pending` and its fingerprint is not recorded, so every run reconciles it again and reports it again until the cause is fixed. The rest of the run carries on. An element the server cannot hold at all, such as a check constraint on MySQL before 8.0.16, does not keep a table pending: it is reported in the reconciliation's `Blocked` with `Unsupported` set, and the table is recorded.

Set `ThrowOnBlocked = true` to make a run that leaves tables pending throw a `DmMigrationException` instead, once its work is saved. The exception's `Result` says what the run did.

## Renames during a deploy

A rename is declared with `PreviousNames` and made by reconciliation; see [Renames](./extension-methods/reconciliation-methods#renames). During a rolling or blue/green deploy, the previous version of the application keeps running against the same database for a while, and its model still has the old name. Reconciling that model would add the old column back, empty, beside the renamed one — and then the new version would see both and report the table blocked on every start.

So the migrator records each rename in the table's `rename_history`, along with the fingerprint of the model it replaced. A model with that fingerprint is `Superseded`: the table is left alone and a warning is logged. For a renamed table, the ledger row under the old name is kept for the same reason, so the previous version does not create the table again under its old name.

The previous version's own queries still use the old name and will fail; the history only stops it from damaging the schema. A rename that must not break the running version needs expand and contract instead: add the new column, write to both, backfill, and drop the old one in a later release.

## Forcing a full check

The fingerprint describes the model, not the database, so a change made by hand — an index dropped, a column altered, a table restored from an old backup — goes unnoticed while the model stays the same. `force` reconciles every registered table whatever was recorded:

```csharp
await migrator.MigrateAsync(connection, force: true);
```

A forced run also reconciles models that a rename superseded, so run it from the current version of the application.

## Several instances at once

When several instances start together, one migrates and the others wait for it, then read the ledger again under the lock and find nothing left to do. The lock is each database's own, and is released by the database when the connection closes, so an instance that dies mid-run cannot hold it forever.

| Provider | Lock |
|----------|------|
| SQL Server | `sp_getapplock`, owned by the run's transaction |
| PostgreSQL | `pg_try_advisory_xact_lock`, owned by the run's transaction. This also keeps it correct with Npgsql multiplexing. |
| MySQL / MariaDB | `GET_LOCK`, released at the end of the run |
| SQLite | The run's write transaction: only one connection can hold one |

`LockTimeout` (30 seconds by default) is how long to wait before giving up with a `TimeoutException`. On SQLite the wait is also bounded by the driver's own timeout. The lock name is derived from the database and the ledger, so applications with different ledgers never wait for each other; `LockName` overrides it. Turn `UseLock` off only if a single instance ever migrates.

Runs with different ledgers, such as runs in different schemas, hold no lock in common, and their schema changes can deadlock in the database. A run the database chooses as the victim was rolled back whole, so the migrator runs it again after a short pause, up to `DeadlockRetries` times (3 by default; 0 throws the first deadlock), logging each retry as a `Retrying` warning. On SQL Server a run also reads the catalog without shared locks: its reads are of its own schema and of a ledger the lock guards, and with them, runs in different schemas deadlocked on each other's catalog rows on most runs. The connection's isolation level is put back afterwards.

## Transactions

| Provider | A run is | If it fails part-way |
|----------|----------|----------------------|
| PostgreSQL, SQL Server, SQLite | One transaction, ledger writes included | Nothing is changed and nothing is recorded |
| MySQL, MariaDB | One statement at a time: DDL commits implicitly | What was done stays done; tables not yet recorded are reconciled again next run |

On MySQL a table is recorded only after its changes are made, never before, so a failure can only cause a repeat — and reconciling is idempotent.

On SQLite, most schema changes rebuild the table, and a rebuild inside a transaction is [refused](./providers#sqlite) for a table other tables reference while foreign keys are on. So the migrator turns foreign keys off for the run, before its transaction begins, and puts the setting back afterwards. Before committing, it runs `PRAGMA foreign_key_check` on each table it reconciled, and rolls back if a foreign key it added does not hold for the existing rows.

The connection passed to the migrator must not have a transaction open. If it is closed, the migrator opens it and closes it again.

## Options

| Option | Default | |
|--------|---------|-|
| `SchemaName` | provider default | The ledger's schema |
| `TableStateTableName` | `dm_table_state` | The ledger's name |
| `CreateLedgerIfMissing` | `true` | When false, a missing ledger is an error |
| `ApplySafeAlterations` | `true` | Widen columns and relax `NOT NULL` to match the model |
| `ThrowOnBlocked` | `false` | Throw when tables are left pending |
| `UseLock` | `true` | Take turns with other instances |
| `LockName` | derived | What the lock name is derived from |
| `LockTimeout` | 30 seconds | How long to wait for the lock |
| `DeadlockRetries` | 3 | How many times a run chosen as a deadlock victim is run again |
| `AppliedBy` | null | Recorded with each table |
| `Log` | null | Receives each step. `UseLogger` adds an `ILogger`. |

## A schema per tenant

`MigrateSchemasAsync` migrates the registered models into several schemas: the models [`DmSchemaLayout`](./dml-methods#tenant-schemas-and-other-tables) declares shared once, into the shared schema, and every other model into each schema listed.

```csharp
DmSchemaLayout.Configure(layout => layout.SharedSchema("public").Shared<Plan>().Shared<Tenant>());

var migrator = new DmMigrator().AddTablesFromAssembly(typeof(Order).Assembly);

// One tenant, beside the shared models.
await migrator.MigrateSchemasAsync(() => new NpgsqlConnection(connectionString), ["public"]);

// Later: every tenant, the first still in public.
var schemas = await registry.GetTenantSchemasAsync();      // ["public", "tenant_2", "tenant_3", ...]
var result = await migrator.MigrateSchemasAsync(() => new NpgsqlConnection(connectionString), schemas, maxParallelism: 4);
foreach (var failed in result.Failed)
{
    logger.LogError(failed.Exception, "Schema {Schema} was not migrated", failed.SchemaName);
}
```

- The shared schema runs first, then the others, up to `maxParallelism` at once, each on a connection of its own from the factory.
- Each schema has its own ledger, lock and transaction, so one that fails does not stop the others, and records nothing: the next run migrates it again. `MigrateAsync` is unchanged for a deployment with one schema.
- A schema that does not exist is created.
- A model migrated into every schema cannot have a foreign key to a shared one, since a foreign key names its table without a schema: keep the column, drop the foreign key, and use a [relation](./dml-methods#relations) for the join. This is refused before anything runs.
- Providers without schemas (SQLite, MySQL and MariaDB) refuse `MigrateSchemasAsync`.

## What the migrator does not do

- **Data changes.** Backfills, data-transforming type changes, splitting a column and seed data are not part of a model, so they are not part of a run. Run them yourself after `MigrateAsync`, checking the database's state first so that running them twice does no harm.
- **Drops.** A column or table removed from the model stays in the database, reported as drift. Drop it yourself, in the same release as the model change.
- **Views.** Only tables are registered.
- **Changes made by hand** are invisible until a [forced run](#forcing-a-full-check).
- **Non-deterministic models.** `DmTableFactory.Configure` must configure a model the same way in every instance, or each instance will see the others' fingerprints as changes.
