// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Data.Common;
using Dapper;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic;

public static partial class DbConnectionExtensions
{
    #region Reconciliation

    /// <summary>
    /// Computes the additive work needed to bring a table in the database in line with the model,
    /// without modifying the database.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="elements">The element kinds to consider. Defaults to every kind.</param>
    /// <param name="applySafeAlterations">
    /// Whether to plan changes to existing columns that cannot lose data: a longer length, more precision, or
    /// allowing nulls. Otherwise those differences are reported as drift.
    /// </param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A plan describing what would be created and what has drifted.</returns>
    public static Task<DmTableReconciliationPlan> GetTableReconciliationPlanAsync<T>(
        this IDbConnection db,
        DmReconcileElements elements = DmReconcileElements.All,
        bool applySafeAlterations = false,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => db.GetTableReconciliationPlanAsync(
            typeof(T),
            elements,
            applySafeAlterations,
            tx,
            cancellationToken
        );

    /// <summary>
    /// Computes the additive work needed to bring a table in the database in line with the model,
    /// without modifying the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="type">The type representing the table.</param>
    /// <param name="elements">The element kinds to consider. Defaults to every kind.</param>
    /// <param name="applySafeAlterations">
    /// Whether to plan changes to existing columns that cannot lose data: a longer length, more precision, or
    /// allowing nulls. Otherwise those differences are reported as drift.
    /// </param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A plan describing what would be created and what has drifted.</returns>
    public static async Task<DmTableReconciliationPlan> GetTableReconciliationPlanAsync(
        this IDbConnection db,
        Type type,
        DmReconcileElements elements = DmReconcileElements.All,
        bool applySafeAlterations = false,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(type);
        return await db.GetTableReconciliationPlanAsync(
                DmTableFactory.GetTable(type),
                elements,
                applySafeAlterations,
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Computes the additive work needed to bring a table in the database in line with the model,
    /// without modifying the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="table">The table definition the database should match.</param>
    /// <param name="elements">The element kinds to consider. Defaults to every kind.</param>
    /// <param name="applySafeAlterations">
    /// Whether to plan changes to existing columns that cannot lose data: a longer length, more precision, or
    /// allowing nulls. Otherwise those differences are reported as drift.
    /// </param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A plan describing what would be created and what has drifted.</returns>
    public static async Task<DmTableReconciliationPlan> GetTableReconciliationPlanAsync(
        this IDbConnection db,
        DmTable table,
        DmReconcileElements elements = DmReconcileElements.All,
        bool applySafeAlterations = false,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(table);

        var actual = await db.GetTableAsync(table.SchemaName, table.TableName, tx, cancellationToken)
            .ConfigureAwait(false);

        var renames = new List<DmSchemaRename>();
        var drift = new List<DmSchemaDrift>();
        var blocked = new List<DmSchemaAddition>();
        var doNotAdd = new HashSet<(DmSchemaElementType, string)>();

        if (table.PreviousNames.Count > 0)
        {
            var found = new List<string>();
            foreach (var previous in table.PreviousNames.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (
                    !previous.Equals(table.TableName, StringComparison.OrdinalIgnoreCase)
                    && await db.DoesTableExistAsync(table.SchemaName, previous, tx, cancellationToken)
                        .ConfigureAwait(false)
                )
                {
                    found.Add(previous);
                }
            }

            if (actual is not null)
            {
                foreach (var previous in found)
                {
                    drift.Add(
                        new DmSchemaDrift(
                            DmSchemaElementType.Table,
                            previous,
                            "absent",
                            "present",
                            $"Table '{previous}' is a previous name of '{table.TableName}', which also exists, so nothing was renamed."
                        )
                    );
                }
            }
            else if (found.Count > 1)
            {
                // Creating the table would make a third copy, and picking one to rename would be a guess.
                return new DmTableReconciliationPlan
                {
                    SchemaName = table.SchemaName,
                    TableName = table.TableName,
                    Elements = elements,
                    Blocked =
                    [
                        new DmSchemaAddition(
                            DmSchemaElementType.Table,
                            table.TableName,
                            $"it exists under more than one previous name ({string.Join(", ", found)}); rename one by hand"
                        ),
                    ],
                };
            }
            else if (found.Count == 1)
            {
                actual = await db.GetTableAsync(table.SchemaName, found[0], tx, cancellationToken).ConfigureAwait(false);

                // The rename is planned for any filter but None: the alternative, creating the table afresh,
                // would leave the data behind under the old name.
                if (elements != DmReconcileElements.None)
                {
                    renames.Add(new DmSchemaRename(DmSchemaElementType.Table, found[0], table.TableName));
                }
            }
        }

        if (actual is null)
        {
            return new DmTableReconciliationPlan
            {
                SchemaName = table.SchemaName,
                TableName = table.TableName,
                TableMissing = true,
                Elements = elements,
            };
        }

        actual.TableName = table.TableName;
        ResolveRenames(table, actual, elements, db.GetDbProviderType(), renames, blocked, doNotAdd);

        // Check constraints the server cannot hold are blocked up front rather than planned. The provider
        // declines to create them (MySQL before 8.0.16 has none), so planning them would report the same
        // work as outstanding on every run.
        var supportsCheckConstraints =
            !elements.HasFlag(DmReconcileElements.CheckConstraints)
            || table.CheckConstraints.Count == 0
            || await db.SupportsCheckConstraintsAsync(tx, cancellationToken).ConfigureAwait(false);

        return BuildPlan(
            table,
            actual,
            elements,
            applySafeAlterations,
            supportsCheckConstraints,
            db.GetDbProviderType(),
            renames,
            drift,
            blocked,
            doNotAdd
        );
    }

    /// <summary>
    /// Brings a table in the database in line with the model by creating whatever the model describes
    /// and the database is missing.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="elements">The element kinds to reconcile. Defaults to every kind.</param>
    /// <param name="applySafeAlterations">
    /// Whether to alter existing columns in the ways that cannot lose data: a longer length, more precision, or
    /// allowing nulls. Otherwise those differences are reported as drift and left alone.
    /// </param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A result describing what was created and what has drifted.</returns>
    /// <remarks>
    /// <para>
    /// Reconciliation is additive and idempotent: nothing is ever dropped, and running it twice against an
    /// unchanged model is a no-op the second time. Columns, constraints and indexes that exist but do not match
    /// the model are reported as drift and left untouched, except for the column changes
    /// <paramref name="applySafeAlterations"/> allows.
    /// </para>
    /// <para>
    /// When the table does not exist it is created in full, from the whole model. <paramref name="elements"/>
    /// narrows which elements may be added to a table that is already there; it does not carve up a
    /// CREATE TABLE, which would produce a table the model does not describe.
    /// </para>
    /// </remarks>
    public static Task<DmTableReconciliationResult> ReconcileTableAsync<T>(
        this IDbConnection db,
        DmReconcileElements elements = DmReconcileElements.All,
        bool applySafeAlterations = false,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => db.ReconcileTableAsync(
            typeof(T),
            elements,
            applySafeAlterations,
            tx,
            cancellationToken
        );

    /// <summary>
    /// Brings a table in the database in line with the model by creating whatever the model describes
    /// and the database is missing.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="type">The type representing the table.</param>
    /// <param name="elements">The element kinds to reconcile. Defaults to every kind.</param>
    /// <param name="applySafeAlterations">
    /// Whether to alter existing columns in the ways that cannot lose data: a longer length, more precision, or
    /// allowing nulls. Otherwise those differences are reported as drift and left alone.
    /// </param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A result describing what was created and what has drifted.</returns>
    /// <remarks>
    /// <para>
    /// Reconciliation is additive and idempotent: nothing is ever dropped, and running it twice against an
    /// unchanged model is a no-op the second time. Columns, constraints and indexes that exist but do not match
    /// the model are reported as drift and left untouched, except for the column changes
    /// <paramref name="applySafeAlterations"/> allows.
    /// </para>
    /// <para>
    /// When the table does not exist it is created in full, from the whole model. <paramref name="elements"/>
    /// narrows which elements may be added to a table that is already there; it does not carve up a
    /// CREATE TABLE, which would produce a table the model does not describe.
    /// </para>
    /// </remarks>
    public static async Task<DmTableReconciliationResult> ReconcileTableAsync(
        this IDbConnection db,
        Type type,
        DmReconcileElements elements = DmReconcileElements.All,
        bool applySafeAlterations = false,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(type);
        return await db.ReconcileTableAsync(
                DmTableFactory.GetTable(type),
                elements,
                applySafeAlterations,
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Brings a table in the database in line with the model by creating whatever the model describes
    /// and the database is missing.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="table">The table definition the database should match.</param>
    /// <param name="elements">The element kinds to reconcile. Defaults to every kind.</param>
    /// <param name="applySafeAlterations">
    /// Whether to alter existing columns in the ways that cannot lose data: a longer length, more precision, or
    /// allowing nulls. Otherwise those differences are reported as drift and left alone.
    /// </param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A result describing what was created and what has drifted.</returns>
    /// <remarks>
    /// <para>
    /// Reconciliation is additive and idempotent: nothing is ever dropped, and running it twice against an
    /// unchanged model is a no-op the second time. Columns, constraints and indexes that exist but do not match
    /// the model are reported as drift and left untouched, except for the column changes
    /// <paramref name="applySafeAlterations"/> allows.
    /// </para>
    /// <para>
    /// When the table does not exist it is created in full, from the whole model. <paramref name="elements"/>
    /// narrows which elements may be added to a table that is already there; it does not carve up a
    /// CREATE TABLE, which would produce a table the model does not describe.
    /// </para>
    /// </remarks>
    public static async Task<DmTableReconciliationResult> ReconcileTableAsync(
        this IDbConnection db,
        DmTable table,
        DmReconcileElements elements = DmReconcileElements.All,
        bool applySafeAlterations = false,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(table);

        var plan = await db.GetTableReconciliationPlanAsync(table, elements, applySafeAlterations, tx, cancellationToken)
            .ConfigureAwait(false);

        if (plan.TableMissing)
        {
            // CREATE TABLE fails outright when the table's schema does not exist yet.
            if (!string.IsNullOrWhiteSpace(table.SchemaName) && db.SupportsSchemas())
            {
                await db.CreateSchemaIfNotExistsAsync(table.SchemaName, tx, cancellationToken).ConfigureAwait(false);
            }

            var created = await db.CreateTableIfNotExistsAsync(table, tx, cancellationToken).ConfigureAwait(false);

            return new DmTableReconciliationResult
            {
                SchemaName = table.SchemaName,
                TableName = table.TableName,
                TableCreated = created,
                Applied = created ? [new DmSchemaAddition(DmSchemaElementType.Table, table.TableName)] : [],
                Drift = plan.Drift,
            };
        }

        // Renames come first: everything after them was planned against the table as it is once renamed. A
        // rename that fails here means the database moved since the plan was made, so nothing more is attempted.
        var renamed = new List<DmSchemaRename>();
        foreach (var rename in plan.Renames)
        {
            if (!await RenameAsync(db, table, rename, tx, cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException(
                    $"Could not rename {rename}: the database no longer matches the plan. "
                        + (renamed.Count > 0 ? $"Already renamed: {string.Join(", ", renamed)}. " : string.Empty)
                        + "Nothing else was changed."
                );
            }

            renamed.Add(rename);
        }

        var applied = new List<DmSchemaAddition>();

        // A Create*IfNotExistsAsync call returns false both when the element turned out to exist already
        // and when the database declined to create it. Which of the two it was is settled after the loop.
        var notCreated = new List<DmSchemaAddition>();

        async Task<bool> CreateAsync(DmSchemaAddition addition, Func<Task<bool>> create)
        {
            var created = await create().ConfigureAwait(false);
            (created ? applied : notCreated).Add(addition);
            return created;
        }

        var providerType = db.GetDbProviderType();
        var columnsAddedWithDefault = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var column in plan.MissingColumns)
        {
            var columnToAdd = WithoutSeparatelyPlannedConstraints(column, plan);

            var added = await CreateAsync(
                    new DmSchemaAddition(DmSchemaElementType.Column, column.ColumnName),
                    () => db.CreateColumnIfNotExistsAsync(columnToAdd, tx, cancellationToken)
                )
                .ConfigureAwait(false);

            if (added && !string.IsNullOrWhiteSpace(columnToAdd.GetDefaultExpression(providerType)))
            {
                columnsAddedWithDefault.Add(column.ColumnName);
            }
        }

        // Alterations come straight after the columns, so constraints and indexes created below see each
        // column at its final size.
        var altered = new List<DmColumnAlteration>();
        var refused = new List<DmSchemaAddition>();

        foreach (var alteration in plan.Alterations)
        {
            var refusal = await TryAlterColumnAsync(db, table, alteration, tx, cancellationToken).ConfigureAwait(false);
            if (refusal is null)
            {
                altered.Add(alteration);
            }
            else
            {
                refused.Add(
                    new DmSchemaAddition(
                        DmSchemaElementType.Column,
                        alteration.ColumnName,
                        $"{string.Join("; ", alteration.Changes)} refused by the database: {refusal}"
                    )
                );
            }
        }

        if (plan.MissingPrimaryKeyConstraint is { } pk)
        {
            await CreateAsync(
                    new DmSchemaAddition(DmSchemaElementType.PrimaryKeyConstraint, pk.ConstraintName),
                    () => db.CreatePrimaryKeyConstraintIfNotExistsAsync(pk, tx, cancellationToken)
                )
                .ConfigureAwait(false);
        }

        foreach (var uc in plan.MissingUniqueConstraints)
        {
            await CreateAsync(
                    new DmSchemaAddition(DmSchemaElementType.UniqueConstraint, uc.ConstraintName),
                    () => db.CreateUniqueConstraintIfNotExistsAsync(uc, tx, cancellationToken)
                )
                .ConfigureAwait(false);
        }

        foreach (var cc in plan.MissingCheckConstraints)
        {
            await CreateAsync(
                    new DmSchemaAddition(DmSchemaElementType.CheckConstraint, cc.ConstraintName),
                    () => db.CreateCheckConstraintIfNotExistsAsync(cc, tx, cancellationToken)
                )
                .ConfigureAwait(false);
        }

        foreach (var dc in plan.MissingDefaultConstraints)
        {
            if (columnsAddedWithDefault.Contains(dc.ColumnName))
            {
                // The column was added with its DEFAULT inline (see WithoutSeparatelyPlannedConstraints).
                // Adding the model's default as well would fail on SQL Server, where a column carries one.
                applied.Add(
                    new DmSchemaAddition(DmSchemaElementType.DefaultConstraint, dc.ConstraintName, "added with its column")
                );
                continue;
            }

            await CreateAsync(
                    new DmSchemaAddition(DmSchemaElementType.DefaultConstraint, dc.ConstraintName),
                    () => db.CreateDefaultConstraintIfNotExistsAsync(dc, tx, cancellationToken)
                )
                .ConfigureAwait(false);
        }

        foreach (var ix in plan.MissingIndexes)
        {
            await CreateAsync(
                    new DmSchemaAddition(DmSchemaElementType.Index, ix.IndexName),
                    () => db.CreateIndexIfNotExistsAsync(ix, tx, cancellationToken)
                )
                .ConfigureAwait(false);
        }

        // Foreign keys go last so that columns added earlier in this same call are available to
        // reference, and so a failure to add one does not strand the rest of the work.
        foreach (var fk in plan.MissingForeignKeyConstraints)
        {
            await CreateAsync(
                    new DmSchemaAddition(DmSchemaElementType.ForeignKeyConstraint, fk.ConstraintName),
                    () => db.CreateForeignKeyConstraintIfNotExistsAsync(fk, tx, cancellationToken)
                )
                .ConfigureAwait(false);
        }

        var blocked = new List<DmSchemaAddition>(plan.Blocked);
        blocked.AddRange(refused);

        if (notCreated.Count > 0)
        {
            // Re-read once to tell an element that exists by now -- created concurrently, or as a side
            // effect of an earlier step -- from one the database declined. Only the latter is reported;
            // otherwise planned work would vanish from the result without a trace.
            var stillMissing = (
                await db.GetTableReconciliationPlanAsync(table, elements, applySafeAlterations, tx, cancellationToken)
                    .ConfigureAwait(false)
            ).Additions;

            foreach (var item in notCreated)
            {
                if (
                    stillMissing.Any(m =>
                        m.ElementType == item.ElementType && m.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase)
                    )
                )
                {
                    blocked.Add(
                        new DmSchemaAddition(
                            item.ElementType,
                            item.Name,
                            "the database did not create it; this server may not support it"
                        )
                    );
                }
            }
        }

        return new DmTableReconciliationResult
        {
            SchemaName = table.SchemaName,
            TableName = table.TableName,
            TableCreated = false,
            Applied = applied,
            Altered = altered,
            Renamed = renamed,
            Drift = plan.Drift,
            Blocked = blocked,
        };
    }

    /// <summary>
    /// Tells whether an error, or one it wraps, is a deadlock as its provider reports it: SQL Server 1205 and MySQL
    /// 1213 as the error's number (both drivers of each have the property), PostgreSQL as SQLSTATE 40P01, or 40001
    /// when it is serialization that failed.
    /// </summary>
    /// <param name="db">The connection the error came from.</param>
    /// <param name="exception">The error.</param>
    /// <returns>True for a deadlock, which running the transaction again may get past.</returns>
    internal static bool IsDeadlock(IDbConnection db, Exception exception)
    {
        for (var inner = exception; inner is not null; inner = inner.InnerException)
        {
            if (inner is not DbException error)
            {
                continue;
            }

            var number = error.GetType().GetProperty("Number")?.GetValue(error);
            var deadlocked = db.GetDbProviderType() switch
            {
                DbProviderType.SqlServer => number is 1205,
                DbProviderType.MySql => number is 1213,
                DbProviderType.PostgreSql => error.SqlState is "40P01" or "40001",
                _ => false,
            };

            if (deadlocked)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Makes one planned rename.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="table">The table being reconciled. A table rename is planned first, so by the time a column
    /// or constraint is renamed the table already has this name.</param>
    /// <param name="rename">The rename to make.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the element was renamed; false if it was not found under its old name, or the new name was taken.</returns>
    private static Task<bool> RenameAsync(
        IDbConnection db,
        DmTable table,
        DmSchemaRename rename,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    ) =>
        rename.ElementType switch
        {
            DmSchemaElementType.Table => db.RenameTableIfExistsAsync(
                table.SchemaName,
                rename.OldName,
                rename.NewName,
                tx,
                cancellationToken
            ),
            DmSchemaElementType.Column => db.RenameColumnIfExistsAsync(
                table.SchemaName,
                table.TableName,
                rename.OldName,
                rename.NewName,
                tx,
                cancellationToken
            ),
            DmSchemaElementType.Index => db.RenameIndexIfExistsAsync(
                table.SchemaName,
                table.TableName,
                rename.OldName,
                rename.NewName,
                tx,
                cancellationToken
            ),
            DmSchemaElementType.UniqueConstraint => db.RenameUniqueConstraintIfExistsAsync(
                table.SchemaName,
                table.TableName,
                rename.OldName,
                rename.NewName,
                tx,
                cancellationToken
            ),
            DmSchemaElementType.CheckConstraint => db.RenameCheckConstraintIfExistsAsync(
                table.SchemaName,
                table.TableName,
                rename.OldName,
                rename.NewName,
                tx,
                cancellationToken
            ),
            DmSchemaElementType.ForeignKeyConstraint => db.RenameForeignKeyConstraintIfExistsAsync(
                table.SchemaName,
                table.TableName,
                rename.OldName,
                rename.NewName,
                tx,
                cancellationToken
            ),
            _ => throw new InvalidOperationException($"{rename.ElementType} cannot be renamed."),
        };

    /// <summary>
    /// Applies one column alteration, turning a refusal by the database into a reason instead of an exception.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="table">The table being reconciled.</param>
    /// <param name="alteration">The alteration to apply.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Null when the column was altered, otherwise the database's reason for refusing.</returns>
    /// <remarks>
    /// <para>
    /// Providers refuse some alterations that are safe for the data: PostgreSQL will not change the type of a
    /// column a view depends on, and SQL Server will not resize an indexed decimal column. Those are reported
    /// as blocked, like any other element the database will not take, rather than failing the whole run.
    /// </para>
    /// <para>
    /// A failed statement can leave a transaction unusable -- PostgreSQL aborts it outright -- so inside a
    /// transaction the attempt runs under a savepoint and is rolled back to it on refusal. Savepoints are
    /// issued as SQL rather than through <see cref="DbTransaction"/>, which older drivers such as
    /// System.Data.SqlClient do not implement. MySQL needs none: its DDL commits implicitly and a failed
    /// statement leaves nothing behind. For a provider without a known savepoint statement the error is not
    /// caught inside a transaction, because the transaction may not survive it.
    /// </para>
    /// </remarks>
    private static async Task<string?> TryAlterColumnAsync(
        IDbConnection db,
        DmTable table,
        DmColumnAlteration alteration,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    )
    {
        var (save, rollback, release) = tx is null
            ? (null, null, null)
            : db.GetDbProviderType() switch
            {
                DbProviderType.SqlServer => (
                    "SAVE TRANSACTION dm_reconcile_alter",
                    "ROLLBACK TRANSACTION dm_reconcile_alter",
                    (string?)null
                ),
                DbProviderType.PostgreSql => (
                    "SAVEPOINT dm_reconcile_alter",
                    "ROLLBACK TO SAVEPOINT dm_reconcile_alter",
                    "RELEASE SAVEPOINT dm_reconcile_alter"
                ),
                DbProviderType.Sqlite => (
                    "SAVEPOINT dm_reconcile_alter",
                    "ROLLBACK TO dm_reconcile_alter",
                    "RELEASE dm_reconcile_alter"
                ),
                _ => ((string?)null, (string?)null, (string?)null),
            };

        var canRecover = tx is null || save is not null || db.GetDbProviderType() == DbProviderType.MySql;

        async Task RunAsync(string? sql)
        {
            if (sql is not null)
            {
                await db.ExecuteAsync(new CommandDefinition(sql, transaction: tx, cancellationToken: cancellationToken))
                    .ConfigureAwait(false);
            }
        }

        await RunAsync(save).ConfigureAwait(false);

        try
        {
            await db.AlterColumnIfExistsAsync(
                    table.SchemaName,
                    table.TableName,
                    alteration.ColumnName,
                    alteration.SqlType,
                    alteration.IsNullable,
                    tx,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        catch (DbException ex) when (canRecover && !IsDeadlock(db, ex))
        {
            // A deadlock is not the database refusing the change: it propagates, for the transaction to be run again.
            //
            // SQL Server ends the whole transaction on some ALTER COLUMN refusals. Reporting the column as
            // blocked would then let the caller carry on in a transaction that no longer exists, so the
            // original error is rethrown instead.
            if (!await IsTransactionStillOpenAsync(db, tx, cancellationToken).ConfigureAwait(false))
            {
                throw;
            }

            await RunAsync(rollback).ConfigureAwait(false);
            return ex.Message.Split('\n')[0].Trim();
        }

        await RunAsync(release).ConfigureAwait(false);
        return null;
    }

    /// <summary>
    /// Returns whether the caller's transaction survived a failed statement.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="tx">The caller's transaction, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True when there is no transaction, or it can still be committed.</returns>
    /// <remarks>
    /// Only SQL Server ends a transaction on its own here. A transaction it has ended can no longer even run a
    /// query, so a failure to ask counts as closed.
    /// </remarks>
    private static async Task<bool> IsTransactionStillOpenAsync(
        IDbConnection db,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    )
    {
        if (tx is null || db.GetDbProviderType() != DbProviderType.SqlServer)
        {
            return true;
        }

        try
        {
            return await db.ExecuteScalarAsync<int>(
                    new CommandDefinition("SELECT XACT_STATE()", transaction: tx, cancellationToken: cancellationToken)
                )
                .ConfigureAwait(false) == 1;
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// Returns the column to add, minus the column-level constraints this plan creates separately under
    /// the model's own names.
    /// </summary>
    /// <param name="column">The model's column.</param>
    /// <param name="plan">The plan being applied.</param>
    /// <returns>The column itself when nothing needs removing, otherwise a copy.</returns>
    /// <remarks>
    /// <para>
    /// Adding a column also creates the constraints its flags imply -- index, unique, check, foreign key,
    /// default -- under names generated from the table and column. When the model names one of those
    /// elements explicitly, the plan creates it a second time under that name, because existence is
    /// checked by name: two indexes or two foreign keys over one column. Leaving the flag off the column
    /// means each element is created once, under the model's name.
    /// </para>
    /// <para>
    /// A default stays on a NOT NULL column: the DEFAULT has to be part of the ADD COLUMN for the column to
    /// be added to a table that already has rows. The model's column itself is never modified, because
    /// <see cref="DmTableFactory"/> caches it and shares it process-wide.
    /// </para>
    /// </remarks>
    private static DmColumn WithoutSeparatelyPlannedConstraints(DmColumn column, DmTableReconciliationPlan plan)
    {
        bool IsThisColumn(string? name) =>
            name is not null && name.Equals(column.ColumnName, StringComparison.OrdinalIgnoreCase);

        bool IsOnlyThisColumn(List<DmOrderedColumn> columns) =>
            columns.Count == 1 && IsThisColumn(columns[0].ColumnName);

        var plannedIndex = plan.MissingIndexes.FirstOrDefault(ix => IsOnlyThisColumn(ix.Columns));
        var uniqueIsPlanned = plan.MissingUniqueConstraints.Any(uc => IsOnlyThisColumn(uc.Columns));
        var foreignKeyIsPlanned = plan.MissingForeignKeyConstraints.Any(fk => IsOnlyThisColumn(fk.SourceColumns));
        var checkIsPlanned = plan.MissingCheckConstraints.Any(cc => IsThisColumn(cc.ColumnName));
        var defaultIsPlanned =
            column.IsNullable && plan.MissingDefaultConstraints.Any(dc => IsThisColumn(dc.ColumnName));

        if (plannedIndex is null && !uniqueIsPlanned && !foreignKeyIsPlanned && !checkIsPlanned && !defaultIsPlanned)
        {
            return column;
        }

        var copy = column.Copy();

        if (plannedIndex is not null)
        {
            copy.IsIndexed = false;

            // A unique index also sets IsUnique, which without IsIndexed would add a unique constraint.
            if (plannedIndex.IsUnique)
            {
                copy.IsUnique = false;
            }
        }

        if (uniqueIsPlanned)
        {
            copy.IsUnique = false;
        }

        if (foreignKeyIsPlanned)
        {
            copy.IsForeignKey = false;
        }

        if (checkIsPlanned)
        {
            copy.CheckExpression = null;
            copy.CheckExpressionFunc = null;
        }

        if (defaultIsPlanned)
        {
            copy.DefaultExpression = null;
            copy.DefaultExpressionFunc = null;
        }

        return copy;
    }

    #endregion // Reconciliation
}
