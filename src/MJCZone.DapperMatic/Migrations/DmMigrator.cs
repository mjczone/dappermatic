// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using Dapper;
using MJCZone.DapperMatic.DataAnnotations;
using MJCZone.DapperMatic.Migrations.Internal;
using MJCZone.DapperMatic.Models;
using MJCZone.DapperMatic.Providers;

namespace MJCZone.DapperMatic.Migrations;

/// <summary>
/// Keeps a database's tables in line with their models, reconciling only the tables whose model changed since
/// they were last reconciled.
/// </summary>
/// <remarks>
/// <para>
/// Each table's model fingerprint (see <see cref="DmTableFingerprint"/>) is recorded in a ledger table once the
/// table has been reconciled with nothing left blocked. A later run compares fingerprints first and reconciles
/// only the tables whose model differs, so an application whose models have not changed starts with one ledger
/// query instead of reading every table back from the database.
/// </para>
/// <para>
/// Reconciling is what <see cref="DbConnectionExtensions.ReconcileTableAsync(IDbConnection, DmTable, DmReconcileElements, bool, IDbTransaction?, CancellationToken)"/>
/// does: additive, with renames declared through previous names, and with safe column alterations unless
/// <see cref="DmMigrationOptions.ApplySafeAlterations"/> is off. Across the tables of one run, foreign keys are
/// added after everything else, so tables can reference each other, in any order, and in cycles.
/// </para>
/// <para>
/// On SQL Server, PostgreSQL and SQLite a run is one transaction: it applies and records everything, or, if
/// anything throws, nothing. MySQL and MariaDB commit each DDL statement as it runs, so there a table is
/// recorded only after its changes are made, and a run that fails part-way leaves the unrecorded tables to be
/// reconciled again next time.
/// </para>
/// </remarks>
public sealed partial class DmMigrator
{
    private readonly List<DmTable> _tables = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="DmMigrator"/> class.
    /// </summary>
    /// <param name="options">The options, or null for the defaults.</param>
    public DmMigrator(DmMigrationOptions? options = null)
    {
        Options = options ?? new DmMigrationOptions();
    }

    /// <summary>
    /// Gets the options.
    /// </summary>
    public DmMigrationOptions Options { get; }

    /// <summary>
    /// Gets the registered table models, in the order registered.
    /// </summary>
    public IReadOnlyList<DmTable> Tables => _tables;

    /// <summary>
    /// Registers the table a class describes.
    /// </summary>
    /// <typeparam name="T">The class, as <see cref="DmTableFactory"/> reads it.</typeparam>
    /// <returns>This migrator.</returns>
    public DmMigrator AddTable<T>() => AddTable(typeof(T));

    /// <summary>
    /// Registers the table a class describes.
    /// </summary>
    /// <param name="type">The class, as <see cref="DmTableFactory"/> reads it.</param>
    /// <returns>This migrator.</returns>
    public DmMigrator AddTable(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return AddTable(DmTableFactory.GetTable(type));
    }

    /// <summary>
    /// Registers a table model.
    /// </summary>
    /// <param name="table">The model.</param>
    /// <returns>This migrator.</returns>
    /// <exception cref="ArgumentException">A model for the same table is registered already.</exception>
    public DmMigrator AddTable(DmTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentException.ThrowIfNullOrWhiteSpace(table.TableName, nameof(table));

        if (
            _tables.Any(t =>
                string.Equals(t.SchemaName ?? string.Empty, table.SchemaName ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                && t.TableName.Equals(table.TableName, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            throw new ArgumentException($"A model for table '{table.TableName}' is registered already.", nameof(table));
        }

        _tables.Add(table);
        return this;
    }

    /// <summary>
    /// Registers every class in an assembly marked with <see cref="DmTableAttribute"/>.
    /// </summary>
    /// <param name="assembly">The assembly.</param>
    /// <param name="predicate">Which of those classes to register, such as the ones in one namespace when an
    /// assembly holds the models of more than one database. Null registers them all.</param>
    /// <returns>This migrator.</returns>
    /// <remarks>
    /// Classes are registered in order of their full name, so the order does not depend on how the compiler laid
    /// the assembly out. Prefer <see cref="AddTable{T}"/> in a trimmed application, where scanning can miss types.
    /// </remarks>
    public DmMigrator AddTablesFromAssembly(Assembly assembly, Func<Type, bool>? predicate = null)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        foreach (
            var type in assembly
                .GetTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false } && t.GetCustomAttribute<DmTableAttribute>() is not null)
                .Where(t => predicate is null || predicate(t))
                .OrderBy(t => t.FullName, StringComparer.Ordinal)
        )
        {
            AddTable(type);
        }

        return this;
    }

    /// <summary>
    /// Works out which tables a run would reconcile, from the ledger and the models alone. Nothing is changed and
    /// no lock is taken.
    /// </summary>
    /// <param name="db">The database connection. It is opened if it is closed, and closed again afterwards.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The plan.</returns>
    public async Task<DmMigrationPlan> GetPlanAsync(IDbConnection db, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        var fingerprints = ComputeFingerprints();
        var ledger = CreateLedger();
        var opened = await EnsureOpenAsync(db, cancellationToken).ConfigureAwait(false);
        try
        {
            var exists = await ledger.ExistsAsync(db, null, cancellationToken).ConfigureAwait(false);
            var rows = exists
                ? await ledger.ReadAsync(db, null, cancellationToken).ConfigureAwait(false)
                : new Dictionary<string, LedgerRow>();

            return new DmMigrationPlan { LedgerExists = exists, Tables = Classify(db, fingerprints, rows, force: false) };
        }
        finally
        {
            if (opened)
            {
                db.Close();
            }
        }
    }

    /// <summary>
    /// Reconciles every registered table whose model changed since it was last reconciled, and records the new
    /// fingerprints.
    /// </summary>
    /// <param name="db">The database connection, with no transaction open on it. It is opened if it is closed, and
    /// closed again afterwards.</param>
    /// <param name="force">True to reconcile every table whatever was recorded: the way to repair a table changed
    /// by hand, which a fingerprint of the model cannot notice. A forced run also reconciles models a rename
    /// superseded, so run it from the current version of the application.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>What the run did.</returns>
    /// <exception cref="TimeoutException">Another instance held the lock for longer than
    /// <see cref="DmMigrationOptions.LockTimeout"/>.</exception>
    /// <exception cref="DmMigrationException"><see cref="DmMigrationOptions.ThrowOnBlocked"/> is set and tables were
    /// left pending.</exception>
    public async Task<DmMigrationResult> MigrateAsync(
        IDbConnection db,
        bool force = false,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(db);

        var fingerprints = ComputeFingerprints();
        var ledger = CreateLedger();
        var opened = await EnsureOpenAsync(db, cancellationToken).ConfigureAwait(false);
        try
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    // The fast path: read the ledger without the lock, and if no model changed, finish without taking
                    // it. Every instance of an unchanged application starts with this one query.
                    if (!force && await FastPathAsync(db, ledger, fingerprints, cancellationToken).ConfigureAwait(false) is { } unchanged)
                    {
                        return Finish(unchanged);
                    }

                    return Finish(
                        await MigrateUnderLockAsync(db, ledger, fingerprints, force, cancellationToken).ConfigureAwait(false)
                    );
                }
                catch (Exception exception) when (attempt < Options.DeadlockRetries && DbConnectionExtensions.IsDeadlock(db, exception))
                {
                    // The other transaction is finishing; a pause of each victim's own keeps them from meeting again.
                    var pause = RandomNumberGenerator.GetInt32(50, 250) * (attempt + 1);
                    Emit(
                        DmMigrationEventKind.Retrying,
                        DmMigrationEventLevel.Warning,
                        "The database chose the run as a deadlock victim and rolled it back; running it again in "
                            + $"{pause.ToString(CultureInfo.InvariantCulture)} ms (retry "
                            + $"{(attempt + 1).ToString(CultureInfo.InvariantCulture)} of "
                            + $"{Options.DeadlockRetries.ToString(CultureInfo.InvariantCulture)})."
                    );
                    await Task.Delay(pause, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            if (opened)
            {
                db.Close();
            }
        }
    }

    /// <summary>
    /// Returns a copy of a model whose foreign keys declared only on a column, as
    /// <c>[DmColumn(isForeignKey: true, referencedTableName: ...)]</c> declares them, are declared on the table too,
    /// under the name creating the column would give them, when they reference a table not created yet.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="notYetCreated">Whether a referenced table is one the run has still to reconcile.</param>
    /// <returns>The model itself when it has no such foreign keys, otherwise a copy with them.</returns>
    /// <remarks>
    /// Reconciliation adds a column's foreign key only along with the column. One that references a table the run
    /// reconciles later, as with tables registered in the other order or referencing each other, is declared on the
    /// table instead: left out of the first half of the run, which would otherwise create the column referencing a
    /// table that does not exist yet, and added by the second. The others are created with their columns, in one
    /// statement, as before: on SQL Server, adding them separately makes runs in parallel schemas deadlock on the
    /// catalog.
    /// </remarks>
    internal static DmTable WithColumnForeignKeysDeclared(DmTable model, Func<string, bool> notYetCreated)
    {
        var declared = model
            .ForeignKeyConstraints.SelectMany(fk => fk.SourceColumns)
            .Select(c => c.ColumnName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var columnOnly = model
            .Columns.Where(c =>
                c.IsForeignKey
                && !string.IsNullOrWhiteSpace(c.ReferencedTableName)
                && !string.IsNullOrWhiteSpace(c.ReferencedColumnName)
                && !declared.Contains(c.ColumnName)
                && !c.ReferencedTableName.Equals(model.TableName, StringComparison.OrdinalIgnoreCase)
                && notYetCreated(c.ReferencedTableName)
            )
            .ToList();
        if (columnOnly.Count == 0)
        {
            return model;
        }

        return new DmTable
        {
            SchemaName = model.SchemaName,
            TableName = model.TableName,
            PreviousNames = model.PreviousNames,
            Columns = model.Columns,
            PrimaryKeyConstraint = model.PrimaryKeyConstraint,
            CheckConstraints = model.CheckConstraints,
            DefaultConstraints = model.DefaultConstraints,
            UniqueConstraints = model.UniqueConstraints,
            ForeignKeyConstraints =
            [
                .. model.ForeignKeyConstraints,
                .. columnOnly.Select(c => new DmForeignKeyConstraint(
                    model.SchemaName,
                    model.TableName,
                    DbProviderUtils.GenerateForeignKeyConstraintName(model.TableName, c.ColumnName, c.ReferencedTableName!, c.ReferencedColumnName!),
                    [new DmOrderedColumn(c.ColumnName)],
                    c.ReferencedTableName!,
                    [new DmOrderedColumn(c.ReferencedColumnName!)],
                    c.OnDelete ?? DmForeignKeyAction.NoAction,
                    c.OnUpdate ?? DmForeignKeyAction.NoAction
                )),
            ],
            Indexes = model.Indexes,
        };
    }

    /// <summary>
    /// Returns a copy of a model without its foreign keys, for the first half of a run.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>The model itself when it has no foreign keys, otherwise a copy without them.</returns>
    /// <remarks>
    /// Columns that a foreign key uses lose their foreign key flag too, since adding such a column would create
    /// its foreign key along with it. A flagged column with no foreign key declared on the table keeps its flag:
    /// that foreign key exists only through the column, so it is created the way reconciliation creates it.
    /// </remarks>
    internal static DmTable WithoutForeignKeys(DmTable model)
    {
        if (model.ForeignKeyConstraints.Count == 0)
        {
            return model;
        }

        var foreignKeyColumns = model
            .ForeignKeyConstraints.SelectMany(fk => fk.SourceColumns)
            .Select(c => c.ColumnName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new DmTable
        {
            SchemaName = model.SchemaName,
            TableName = model.TableName,
            PreviousNames = model.PreviousNames,
            Columns =
            [
                .. model.Columns.Select(c =>
                {
                    if (!c.IsForeignKey || !foreignKeyColumns.Contains(c.ColumnName))
                    {
                        return c;
                    }

                    // A copy: the model's column is cached by DmTableFactory and shared process-wide.
                    var copy = c.Copy();
                    copy.IsForeignKey = false;
                    return copy;
                }),
            ],
            PrimaryKeyConstraint = model.PrimaryKeyConstraint,
            CheckConstraints = model.CheckConstraints,
            DefaultConstraints = model.DefaultConstraints,
            UniqueConstraints = model.UniqueConstraints,
            Indexes = model.Indexes,
        };
    }

    // The fast path's result when no model changed, or null to take the lock. On SQL Server under READ COMMITTED,
    // the read would wait behind another instance's uncommitted ledger rows until the command timed out: it waits for
    // none, and a row it cannot read at once sends the run to the locked path, which waits for the lock instead.
    private async Task<DmMigrationResult?> FastPathAsync(
        IDbConnection db,
        TableStateLedger ledger,
        List<(DmTable Table, string Fingerprint)> fingerprints,
        CancellationToken cancellationToken
    )
    {
        var sqlServer = db.GetDbProviderType() == DbProviderType.SqlServer;
        int? lockTimeout = null;
        if (sqlServer)
        {
            lockTimeout = await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT @@LOCK_TIMEOUT", cancellationToken: cancellationToken)).ConfigureAwait(false);
            await db.ExecuteAsync(new CommandDefinition("SET LOCK_TIMEOUT 0", cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        try
        {
            if (!await ledger.ExistsAsync(db, null, cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            var rows = await ledger.ReadAsync(db, null, cancellationToken).ConfigureAwait(false);
            var states = Classify(db, fingerprints, rows, force: false);
            return states.Any(s => s.NeedsReconcile) ? null : new DmMigrationResult { Tables = [.. states.Select(NotReconciled)] };
        }
        catch (DbException exception) when (sqlServer && DmDatabaseErrors.Classify(exception) == DmDatabaseErrorKind.Timeout)
        {
            return null;
        }
        finally
        {
            if (lockTimeout is { } previous)
            {
                await db.ExecuteAsync(
                        new CommandDefinition($"SET LOCK_TIMEOUT {previous.ToString(CultureInfo.InvariantCulture)}", cancellationToken: CancellationToken.None)
                    )
                    .ConfigureAwait(false);
            }
        }
    }

    private static async Task<bool> EnsureOpenAsync(IDbConnection db, CancellationToken cancellationToken)
    {
        if (db.State == ConnectionState.Open)
        {
            return false;
        }

        if (db is DbConnection connection)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            db.Open();
        }

        return true;
    }

    private static DmTableMigrationResult NotReconciled(DmTableState state) =>
        new()
        {
            State = state,
            Outcome =
                state.Status == DmTableStatus.Superseded
                    ? DmTableMigrationOutcome.Superseded
                    : DmTableMigrationOutcome.Unchanged,
        };

    /// <summary>
    /// Combines a table's two reconciles: everything but foreign keys from the first, and only foreign keys from
    /// the second, which reports the rest again.
    /// </summary>
    private static DmTableReconciliationResult Merge(DmTableReconciliationResult first, DmTableReconciliationResult second)
    {
        static bool IsForeignKey(DmSchemaElementType type) => type == DmSchemaElementType.ForeignKeyConstraint;

        return new DmTableReconciliationResult
        {
            SchemaName = first.SchemaName,
            TableName = first.TableName,
            TableCreated = first.TableCreated,
            Applied = [.. first.Applied, .. second.Applied.Where(a => IsForeignKey(a.ElementType))],
            Altered = first.Altered,
            Renamed = [.. first.Renamed, .. second.Renamed.Where(r => IsForeignKey(r.ElementType))],
            Drift = [.. first.Drift, .. second.Drift.Where(d => IsForeignKey(d.ElementType))],
            Blocked = [.. first.Blocked, .. second.Blocked.Where(b => IsForeignKey(b.ElementType))],
        };
    }

    private static async Task CheckSqliteForeignKeysAsync(
        IDbConnection db,
        IDbTransaction tx,
        IEnumerable<DmTableState> reconciled,
        CancellationToken cancellationToken
    )
    {
        // Foreign keys are off for the run, so nothing checked rows against a foreign key added to a table that
        // already had rows. SQLite's own check finds them before the transaction commits.
        foreach (var state in reconciled)
        {
            var violations = await db.QueryAsync(
                    new CommandDefinition(
                        $"PRAGMA foreign_key_check({db.GetSchemaQualifiedTableName(null, state.Model.TableName)})",
                        transaction: tx,
                        cancellationToken: cancellationToken
                    )
                )
                .ConfigureAwait(false);

            if (violations.Any())
            {
                throw new InvalidOperationException(
                    $"Table '{state.Model.TableName}' has rows that violate its foreign keys, so nothing was changed. "
                        + $"Run PRAGMA foreign_key_check(\"{state.Model.TableName}\") to find them."
                );
            }
        }
    }

    private static string Describe(DmTableReconciliationResult result)
    {
        var parts = new List<string>();
        if (result.TableCreated)
        {
            parts.Add("created");
        }
        else
        {
            if (result.Renamed.Count > 0)
            {
                parts.Add($"{Count(result.Renamed.Count)} renamed");
            }

            if (result.Applied.Count > 0)
            {
                parts.Add($"{Count(result.Applied.Count)} added");
            }

            if (result.Altered.Count > 0)
            {
                parts.Add($"{Count(result.Altered.Count)} altered");
            }
        }

        if (result.Drift.Count > 0)
        {
            parts.Add($"{Count(result.Drift.Count)} drifted");
        }

        return parts.Count == 0 ? "nothing to do" : string.Join(", ", parts);

        static string Count(int n) => n.ToString(CultureInfo.InvariantCulture);
    }

    private async Task<DmMigrationResult> MigrateUnderLockAsync(
        IDbConnection db,
        TableStateLedger ledger,
        List<(DmTable Table, string Fingerprint)> fingerprints,
        bool force,
        CancellationToken cancellationToken
    )
    {
        var provider = db.GetDbProviderType();

        // SQLite ignores PRAGMA foreign_keys inside a transaction, and with foreign keys on, the table rebuild behind
        // most of its schema changes is refused for a table that others reference. So they go off for the run,
        // before its transaction starts, and the run checks what it changed itself before committing.
        long? sqliteForeignKeys = null;
        long? sqliteBusyTimeout = null;
        if (provider == DbProviderType.Sqlite)
        {
            sqliteForeignKeys = await db.ExecuteScalarAsync<long>("PRAGMA foreign_keys").ConfigureAwait(false);
            sqliteBusyTimeout = await db.ExecuteScalarAsync<long>("PRAGMA busy_timeout").ConfigureAwait(false);
            await db.ExecuteAsync("PRAGMA foreign_keys = 0").ConfigureAwait(false);

            // How long BEGIN waits for another connection's write transaction: the lock timeout, on SQLite.
            var milliseconds = (long)Options.LockTimeout.TotalMilliseconds;
            await db.ExecuteAsync($"PRAGMA busy_timeout = {milliseconds.ToString(CultureInfo.InvariantCulture)}")
                .ConfigureAwait(false);
        }

        // SQL Server reads its catalog under shared locks, and a run holds exclusive locks on the catalog rows its
        // changes wrote until it commits. Runs in different schemas scan each other's rows, so two of them reading
        // after changing deadlock (MigrateSchemasAsync did, on most runs). A run reads only its own schema and a
        // ledger the migration lock guards, so it reads without those locks. SQL Server keeps a transaction's
        // isolation level on the connection after it ends, so the caller's is put back afterwards.
        int? sqlServerIsolation = null;
        if (provider == DbProviderType.SqlServer)
        {
            sqlServerIsolation = await db.ExecuteScalarAsync<int>(
                    "SELECT transaction_isolation_level FROM sys.dm_exec_sessions WHERE session_id = @@SPID"
                )
                .ConfigureAwait(false);
        }

        IDbTransaction? tx = null;
        Func<Task>? release = null;
        try
        {
            // MySQL commits DDL implicitly, so a transaction around the run would promise what it cannot keep. On
            // SQLite, beginning it is what waits for another instance's run to finish.
            tx = provider switch
            {
                DbProviderType.MySql => null,
                DbProviderType.SqlServer => db.BeginTransaction(IsolationLevel.ReadUncommitted),
                _ => db.BeginTransaction(),
            };

            if (Options.UseLock)
            {
                var name = MigrationLock.GetName(Options.LockName ?? $"{db.Database}\n{ledger.QualifiedName(db)}");
                release = await MigrationLock
                    .AcquireAsync(
                        db,
                        tx,
                        name,
                        Options.LockTimeout,
                        () =>
                            Emit(
                                DmMigrationEventKind.WaitingForLock,
                                DmMigrationEventLevel.Information,
                                $"Waiting up to {Options.LockTimeout} for another instance to finish migrating."
                            ),
                        cancellationToken
                    )
                    .ConfigureAwait(false);

                Emit(DmMigrationEventKind.LockAcquired, DmMigrationEventLevel.Debug, $"Took the migration lock '{name}'.");
            }

            // Everything is read again under the lock: another instance may have migrated in the meantime.
            var ledgerCreated = false;
            if (!await ledger.ExistsAsync(db, tx, cancellationToken).ConfigureAwait(false))
            {
                if (!Options.CreateLedgerIfMissing)
                {
                    throw new InvalidOperationException(
                        $"The migration ledger {ledger.QualifiedName(db)} does not exist, and CreateLedgerIfMissing is off."
                    );
                }

                ledgerCreated = await ledger.CreateIfMissingAsync(db, tx, cancellationToken).ConfigureAwait(false);
                if (ledgerCreated)
                {
                    Emit(
                        DmMigrationEventKind.LedgerCreated,
                        DmMigrationEventLevel.Information,
                        $"Created the migration ledger {ledger.QualifiedName(db)}."
                    );
                }
            }

            var rows = await ledger.ReadAsync(db, tx, cancellationToken).ConfigureAwait(false);
            var states = Classify(db, fingerprints, rows, force);
            var results = await ReconcileAsync(db, tx, ledger, states, rows, cancellationToken).ConfigureAwait(false);

            if (provider == DbProviderType.Sqlite && sqliteForeignKeys == 1)
            {
                // A table blocked outright, such as one found under two previous names, was never created.
                await CheckSqliteForeignKeysAsync(
                        db,
                        tx!,
                        results
                            .Where(r =>
                                r.Reconciliation is { } reconciliation
                                && !reconciliation.Blocked.Any(b => b.ElementType == DmSchemaElementType.Table)
                            )
                            .Select(r => r.State),
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }

            tx?.Commit();

            return new DmMigrationResult
            {
                LedgerCreated = ledgerCreated,
                LockTaken = Options.UseLock,
                Tables = results,
            };
        }
        catch
        {
            if (tx is not null)
            {
                try
                {
                    tx.Rollback();
                }
                catch (Exception ex) when (ex is DbException or InvalidOperationException)
                {
                    // The database may have ended the transaction itself, as SQL Server does on some errors. The
                    // error that caused it is the one worth reporting.
                }
            }

            throw;
        }
        finally
        {
            if (release is not null)
            {
                await release().ConfigureAwait(false);
            }

            tx?.Dispose();

            // A severe error closes the connection, and with it the level; an error here would hide that one.
            if (sqlServerIsolation is { } isolation && db.State == ConnectionState.Open)
            {
                var level = isolation switch
                {
                    1 => "READ UNCOMMITTED",
                    3 => "REPEATABLE READ",
                    4 => "SERIALIZABLE",
                    5 => "SNAPSHOT",
                    _ => "READ COMMITTED",
                };
                await db.ExecuteAsync($"SET TRANSACTION ISOLATION LEVEL {level}").ConfigureAwait(false);
            }

            if (sqliteForeignKeys is not null)
            {
                await db.ExecuteAsync($"PRAGMA foreign_keys = {sqliteForeignKeys.Value.ToString(CultureInfo.InvariantCulture)}")
                    .ConfigureAwait(false);
                await db.ExecuteAsync($"PRAGMA busy_timeout = {sqliteBusyTimeout!.Value.ToString(CultureInfo.InvariantCulture)}")
                    .ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Reconciles the tables that need it in two passes -- everything but foreign keys for every table, then the
    /// foreign keys -- and records each table that ends with nothing blocked.
    /// </summary>
    private async Task<List<DmTableMigrationResult>> ReconcileAsync(
        IDbConnection db,
        IDbTransaction? tx,
        TableStateLedger ledger,
        List<DmTableState> states,
        Dictionary<string, LedgerRow> rows,
        CancellationToken cancellationToken
    )
    {
        const DmReconcileElements firstPass = DmReconcileElements.All & ~DmReconcileElements.ForeignKeyConstraints;

        // The first pass leaves foreign keys out, so by the second every table and column a foreign key could
        // reference exists, whichever order the tables were registered in and even if they reference each other.
        var first = new Dictionary<string, DmTableReconciliationResult>(StringComparer.OrdinalIgnoreCase);
        var models = new Dictionary<string, DmTable>(StringComparer.OrdinalIgnoreCase);
        var pending = states.Where(s => s.NeedsReconcile).Select(s => s.Model.TableName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var state in states.Where(s => s.NeedsReconcile))
        {
            models[state.TableKey] = WithColumnForeignKeysDeclared(state.Model, pending.Contains);
            pending.Remove(state.Model.TableName);
            first[state.TableKey] = await db.ReconcileTableAsync(
                    WithoutForeignKeys(models[state.TableKey]),
                    firstPass,
                    Options.ApplySafeAlterations,
                    tx,
                    cancellationToken
                )
                .ConfigureAwait(false);
            await RecordRenamesAsync(db, tx, ledger, state, first[state.TableKey].Renamed, rows, cancellationToken).ConfigureAwait(false);
        }

        var results = new List<DmTableMigrationResult>();
        foreach (var state in states)
        {
            if (!state.NeedsReconcile)
            {
                results.Add(NotReconciled(state));
                continue;
            }

            var reconciliation = first[state.TableKey];
            var model = models[state.TableKey];
            if (
                model.ForeignKeyConstraints.Count > 0
                && !reconciliation.Blocked.Any(b => b.ElementType == DmSchemaElementType.Table)
            )
            {
                var second = await db.ReconcileTableAsync(
                        model,
                        DmReconcileElements.ForeignKeyConstraints,
                        applySafeAlterations: false,
                        tx,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                await RecordRenamesAsync(
                        db,
                        tx,
                        ledger,
                        state,
                        [.. second.Renamed.Where(r => r.ElementType == DmSchemaElementType.ForeignKeyConstraint)],
                        rows,
                        cancellationToken
                    )
                    .ConfigureAwait(false);

                reconciliation = Merge(reconciliation, second);
            }

            // Drift does not stop a table being recorded: it is reported, never acted on, so checking it again on
            // every run would only reopen the gate for good. Anything blocked does, unless the server cannot hold
            // it at all, which no later run would change.
            var complete = reconciliation.Blocked.All(b => b.Unsupported);
            if (complete)
            {
                await RecordAsync(db, tx, ledger, state, rows, cancellationToken).ConfigureAwait(false);
            }

            results.Add(
                new DmTableMigrationResult
                {
                    State = state,
                    Outcome = complete ? DmTableMigrationOutcome.Reconciled : DmTableMigrationOutcome.Pending,
                    Reconciliation = reconciliation,
                }
            );
        }

        return results;
    }

    /// <summary>
    /// Records a reconciled table's fingerprint, and the renames reconciling it made.
    /// </summary>
    private async Task RecordAsync(
        IDbConnection db,
        IDbTransaction? tx,
        TableStateLedger ledger,
        DmTableState state,
        Dictionary<string, LedgerRow> rows,
        CancellationToken cancellationToken
    )
    {
        rows.TryGetValue(state.TableKey, out var row);
        var history = RenameHistory.Parse(row?.RenameHistory);

        // A forced run can record a model the history says was superseded. Recorded, it no longer is.
        history.RemoveAll(e => string.Equals(e.Supersedes, state.Fingerprint, StringComparison.OrdinalIgnoreCase));

        var serialized = RenameHistory.Serialize(history);
        await ledger
            .WriteAsync(
                db,
                tx,
                state.Model.TableName,
                state.TableKey,
                state.Fingerprint,
                serialized,
                exists: row is not null,
                Options.AppliedBy,
                cancellationToken
            )
            .ConfigureAwait(false);

        rows[state.TableKey] = new LedgerRow(state.TableKey, state.Fingerprint, serialized);
    }

    /// <summary>
    /// Writes down the renames a pass made, as soon as it made them: whether or not the table is recorded in this
    /// run, the model they replace is superseded from now on.
    /// </summary>
    /// <remarks>
    /// Written only when the table is recorded, renames made in a run that leaves something blocked, or that fails
    /// later on MySQL, where they are not undone, would never be: the next run finds nothing left to rename, and an
    /// instance of the previous version, still running during a deploy, would see a changed model and add the old
    /// names back.
    /// </remarks>
    private async Task RecordRenamesAsync(
        IDbConnection db,
        IDbTransaction? tx,
        TableStateLedger ledger,
        DmTableState state,
        IReadOnlyList<DmSchemaRename> renamed,
        Dictionary<string, LedgerRow> rows,
        CancellationToken cancellationToken
    )
    {
        if (renamed.Count == 0)
        {
            return;
        }

        rows.TryGetValue(state.TableKey, out var row);
        var tableRename = renamed.FirstOrDefault(r => r.ElementType == DmSchemaElementType.Table);
        var oldKey = tableRename is null ? null : TableStateLedger.GetTableKey(db, state.Model.SchemaName, tableRename.OldName);
        var oldRow = oldKey is not null && rows.TryGetValue(oldKey, out var found) ? found : null;

        // The model these renames replace is the one last recorded, under the table's new name or its old one.
        var supersedes = row?.ModelHash ?? oldRow?.ModelHash;
        if (string.Equals(supersedes, state.Fingerprint, StringComparison.OrdinalIgnoreCase))
        {
            supersedes = null;
        }

        var entry = new RenameHistoryEntry(supersedes, DateTime.UtcNow, [.. renamed.Select(r => r.ToString())]);

        if (oldRow is not null)
        {
            // The old name's row is kept, not deleted, so that a model still using the old name -- the previous
            // version of the application, during a deploy -- is recognised as superseded instead of creating the
            // table again, empty, under its old name.
            var oldHistory = RenameHistory.Parse(oldRow.RenameHistory);
            oldHistory.Add(new RenameHistoryEntry(oldRow.ModelHash, entry.At, entry.Renames));
            var serializedOld = RenameHistory.Serialize(oldHistory);

            await ledger
                .WriteAsync(
                    db,
                    tx,
                    tableRename!.OldName,
                    oldRow.TableKey,
                    oldRow.ModelHash,
                    serializedOld,
                    exists: true,
                    Options.AppliedBy,
                    cancellationToken
                )
                .ConfigureAwait(false);

            rows[oldRow.TableKey] = new LedgerRow(oldRow.TableKey, oldRow.ModelHash, serializedOld);
        }

        // The table's own row keeps the model it records until the table is recorded again. Without a row there is
        // no model these renames replace under the new name.
        if (row is not null)
        {
            var history = RenameHistory.Parse(row.RenameHistory);
            history.Add(entry);
            var serialized = RenameHistory.Serialize(history);
            await ledger
                .WriteAsync(
                    db,
                    tx,
                    state.Model.TableName,
                    state.TableKey,
                    row.ModelHash,
                    serialized,
                    exists: true,
                    Options.AppliedBy,
                    cancellationToken
                )
                .ConfigureAwait(false);

            rows[state.TableKey] = new LedgerRow(state.TableKey, row.ModelHash, serialized);
        }
    }

    private List<(DmTable Table, string Fingerprint)> ComputeFingerprints() =>
        [.. _tables.Select(t => (t, DmTableFingerprint.Compute(t)))];

    private TableStateLedger CreateLedger() => new(Options.SchemaName, Options.TableStateTableName);

    private List<DmTableState> Classify(
        IDbConnection db,
        List<(DmTable Table, string Fingerprint)> fingerprints,
        Dictionary<string, LedgerRow> rows,
        bool force
    )
    {
        var states = new List<DmTableState>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (table, fingerprint) in fingerprints)
        {
            var key = TableStateLedger.GetTableKey(db, table.SchemaName, table.TableName);
            if (!keys.Add(key))
            {
                throw new InvalidOperationException($"More than one registered model is for table '{key}'.");
            }

            rows.TryGetValue(key, out var row);
            var supersededBy = row is null
                ? null
                : RenameHistory
                    .Parse(row.RenameHistory)
                    .LastOrDefault(e => string.Equals(e.Supersedes, fingerprint, StringComparison.OrdinalIgnoreCase));

            var status =
                force ? DmTableStatus.Forced
                : row is null ? DmTableStatus.NotRecorded
                : supersededBy is not null ? DmTableStatus.Superseded
                : row.ModelHash.Equals(fingerprint, StringComparison.OrdinalIgnoreCase) ? DmTableStatus.Unchanged
                : DmTableStatus.ModelChanged;

            states.Add(
                new DmTableState
                {
                    Model = table,
                    TableKey = key,
                    Fingerprint = fingerprint,
                    RecordedFingerprint = row?.ModelHash,
                    Status = status,
                    SupersededBy = supersededBy is null ? null : string.Join("; ", supersededBy.Renames),
                }
            );
        }

        return states;
    }

    private DmMigrationResult Finish(DmMigrationResult result)
    {
        foreach (var table in result.Tables)
        {
            var key = table.State.TableKey;
            switch (table.Outcome)
            {
                case DmTableMigrationOutcome.Unchanged:
                    Emit(DmMigrationEventKind.TableUnchanged, DmMigrationEventLevel.Debug, $"Table {key} is unchanged.", key);
                    break;

                case DmTableMigrationOutcome.Superseded:
                    Emit(
                        DmMigrationEventKind.TableSuperseded,
                        DmMigrationEventLevel.Warning,
                        $"Table {key} was not reconciled: a newer model renamed {table.State.SupersededBy}. This is "
                            + "expected while an older version of the application is still running.",
                        key
                    );
                    break;

                case DmTableMigrationOutcome.Reconciled:
                    Emit(
                        DmMigrationEventKind.TableReconciled,
                        DmMigrationEventLevel.Information,
                        $"Reconciled table {key} ({table.State.Status}): {Describe(table.Reconciliation!)}.",
                        key
                    );
                    break;

                case DmTableMigrationOutcome.Pending:
                    Emit(
                        DmMigrationEventKind.TablePending,
                        DmMigrationEventLevel.Warning,
                        $"Table {key} is reconciled again on the next run, because "
                            + string.Join("; ", table.Reconciliation!.Blocked.Select(b => b.ToString()))
                            + ".",
                        key
                    );
                    break;
            }
        }

        Emit(
            DmMigrationEventKind.Completed,
            DmMigrationEventLevel.Information,
            $"Migration finished: {result.Reconciled.Count} reconciled, {result.Pending.Count} pending, "
                + $"{result.Superseded.Count} superseded, "
                + $"{result.Tables.Count(t => t.Outcome == DmTableMigrationOutcome.Unchanged)} unchanged."
        );

        if (Options.ThrowOnBlocked && !result.IsComplete)
        {
            throw new DmMigrationException(
                "Tables were left with blocked elements: " + string.Join(", ", result.Pending.Select(t => t.State.TableKey)),
                result
            );
        }

        return result;
    }

    private void Emit(DmMigrationEventKind kind, DmMigrationEventLevel level, string message, string? tableKey = null) =>
        Options.Log?.Invoke(new DmMigrationEvent(kind, level, message, tableKey));
}
