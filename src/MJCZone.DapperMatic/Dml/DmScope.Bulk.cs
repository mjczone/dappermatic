// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Dapper;
using MJCZone.DapperMatic.Dml.Internal;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.Dml;

/// <summary>
/// Mass loading: inserts and upserts of many rows through each provider's native bulk path.
/// </summary>
public sealed partial class DmScope
{
    /// <summary>
    /// Inserts rows in bulk, all or none: PostgreSQL's binary <c>COPY</c>, SQL Server's <c>SqlBulkCopy</c>,
    /// MySqlConnector's <c>MySqlBulkCopy</c>, or on SQLite one prepared statement per row, reached through the
    /// connection's own driver; multi-row <c>VALUES</c> where none is available.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="rows">The rows, read once, as they are loaded.</param>
    /// <param name="tx">The transaction to use, or null for one of the method's own.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows inserted. Keys the database generates are not set on the rows.</returns>
    /// <remarks>
    /// SQL Server's bulk copy is told to check constraints, keep NULLs and fire triggers, as an <c>INSERT</c> would.
    /// </remarks>
    public Task<long> BulkInsertAsync<T>(IEnumerable<T> rows, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
        where T : class => BulkInsertAsync(rows, new DmBulkOptions(), tx, cancellationToken);

    /// <summary>
    /// Inserts rows in bulk, all or none, as <see cref="BulkInsertAsync{T}(IEnumerable{T}, IDbTransaction?, CancellationToken)"/>
    /// does.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="rows">The rows, read once, as they are loaded.</param>
    /// <param name="options">The batch size, the timeout, and what to do without a native path.</param>
    /// <param name="tx">The transaction to use, or null for one of the method's own.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows inserted.</returns>
    /// <exception cref="NotSupportedException">There is no native path and the options say not to fall back.</exception>
    [OverloadResolutionPriority(1)] // `new() { ... }` also converts to the transaction: this overload is the one meant.
    public Task<long> BulkInsertAsync<T>(
        IEnumerable<T> rows,
        DmBulkOptions options,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        ArgumentNullException.ThrowIfNull(rows);
        var (type, replay) = ModelTypeOf(rows);
        return BulkInsertAsync(type, replay, options, tx, cancellationToken);
    }

    /// <summary>
    /// Inserts rows of a model known at run time in bulk, all or none.
    /// </summary>
    /// <param name="type">The model type.</param>
    /// <param name="rows">The rows, read once, as they are loaded.</param>
    /// <param name="tx">The transaction to use, or null for one of the method's own.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows inserted.</returns>
    public Task<long> BulkInsertAsync(Type type, IEnumerable<object> rows, IDbTransaction? tx = null, CancellationToken cancellationToken = default) =>
        BulkInsertAsync(type, rows, new DmBulkOptions(), tx, cancellationToken);

    /// <summary>
    /// Inserts rows of a model known at run time in bulk, all or none.
    /// </summary>
    /// <param name="type">The model type.</param>
    /// <param name="rows">The rows, read once, as they are loaded.</param>
    /// <param name="options">The batch size, the timeout, and what to do without a native path.</param>
    /// <param name="tx">The transaction to use, or null for one of the method's own.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows inserted.</returns>
    /// <exception cref="NotSupportedException">There is no native path and the options say not to fall back.</exception>
    [OverloadResolutionPriority(1)] // `new() { ... }` also converts to the transaction: this overload is the one meant.
    public async Task<long> BulkInsertAsync(
        Type type,
        IEnumerable<object> rows,
        DmBulkOptions options,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(options);
        var (model, dialect, table) = Target(type);
        model.RequireWritable("BulkInsertAsync");
        if (RunsAsCaller)
        {
            return await BulkInsertAsCallerAsync(model, dialect, table, rows, options, tx, cancellationToken).ConfigureAwait(false);
        }

        var columns = Written(model, dialect);
        var prepared = rows.Select(row =>
        {
            PrepareInsert(model, row);
            return row;
        });

        return await InTransactionAsync(
                tx,
                needsOne: true,
                transaction => DmlBulk.InsertAsync(Connection, dialect, table, columns, prepared, options, transaction!, cancellationToken)
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Inserts rows in bulk, or updates the rows with their primary keys, all or none.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="rows">The rows, read once; no two may have the same key.</param>
    /// <param name="tx">The transaction to use, or null for one of the method's own.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows loaded.</returns>
    /// <remarks>
    /// The rows are loaded into a temporary table with the native bulk path, then merged into the table in one
    /// statement (two on SQL Server).
    /// </remarks>
    public Task<long> BulkUpsertAsync<T>(IEnumerable<T> rows, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(rows);
        var (type, replay) = ModelTypeOf(rows);
        return BulkUpsertCoreAsync(type, replay, null, null, null, null, new DmBulkOptions(), tx, cancellationToken);
    }

    /// <summary>
    /// Inserts rows in bulk, or updates the rows they conflict with as the options say, all or none.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="rows">The rows, read once; no two may conflict with each other.</param>
    /// <param name="upsert">The conflict columns, what to set, and when, as for a single row.</param>
    /// <param name="bulk">The batch size, the timeout, and what to do without a native path; null for the defaults.</param>
    /// <param name="tx">The transaction to use, or null for one of the method's own.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows loaded.</returns>
    /// <remarks>See <see cref="BulkUpsertAsync{T}(IEnumerable{T}, IDbTransaction?, CancellationToken)"/>.</remarks>
    [OverloadResolutionPriority(1)] // `new() { ... }` also converts to the transaction: this overload is the one meant.
    public Task<long> BulkUpsertAsync<T>(
        IEnumerable<T> rows,
        DmUpsertOptions<T> upsert,
        DmBulkOptions? bulk = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(upsert);
        var (type, replay) = ModelTypeOf(rows);
        return BulkUpsertCoreAsync(
            type,
            replay,
            upsert.OnConflict,
            upsert.OnConflictConstraint,
            upsert.Update,
            upsert.UpdateWhere,
            bulk ?? new DmBulkOptions(),
            tx,
            cancellationToken
        );
    }

    /// <summary>
    /// Inserts rows of a model known at run time in bulk, or updates the rows with their primary keys, all or none.
    /// </summary>
    /// <param name="type">The model type.</param>
    /// <param name="rows">The rows, read once; no two may have the same key.</param>
    /// <param name="tx">The transaction to use, or null for one of the method's own.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows loaded.</returns>
    public Task<long> BulkUpsertAsync(Type type, IEnumerable<object> rows, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(rows);
        return BulkUpsertCoreAsync(type, rows, null, null, null, null, new DmBulkOptions(), tx, cancellationToken);
    }

    // A temporary table rows are loaded into first, with the statements that create it as a table's empty copy and drop it.
    private static (string Stage, string Create, string? Drop) StagingTable(DmlDialect dialect)
    {
        var name = $"dm_stage_{Guid.NewGuid():N}"[..17];
        return dialect.ProviderType switch
        {
            DbProviderType.SqlServer => ($"[#{name}]", "SELECT TOP 0 {0} INTO {1} FROM {2}", "DROP TABLE {0}"),
            DbProviderType.PostgreSql => ($"\"{name}\"", "CREATE TEMP TABLE {1} ON COMMIT DROP AS SELECT {0} FROM {2} WITH NO DATA", null),
            DbProviderType.MySql => ($"`{name}`", "CREATE TEMPORARY TABLE {1} AS SELECT {0} FROM {2} LIMIT 0", "DROP TEMPORARY TABLE {0}"),
            _ => ($"\"{name}\"", "CREATE TEMP TABLE {1} AS SELECT {0} FROM {2} WHERE 0", "DROP TABLE {0}"),
        };
    }

    // Run as a caller, rows are loaded into a temporary table first, checked there all at once, and copied into the
    // table only if every one passes the policies' check: the columns the caller may write, and the policies' values.
    private async Task<long> BulkInsertAsCallerAsync(
        DmlModel model,
        DmlDialect dialect,
        string table,
        IEnumerable<object> rows,
        DmBulkOptions options,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    )
    {
        // A change heard is told with the rows' keys: keys the database generates are read row by row.
        var heard = Heard(model) is not null;
        if (heard && model.Generated is not null)
        {
            return await InsertManyAsync(model.Type, rows, tx, cancellationToken).ConfigureAwait(false);
        }

        var rules = Require(model, DmAction.Create)!;
        var presets = Presets(model, rules);
        var hidden = Hidden(model, DmAction.Create);
        var columns = Written(model, dialect).Where(c => hidden is null || !hidden.Contains(c.Column.ColumnName) || rules.Presets.ContainsKey(c.Column.ColumnName)).ToList();
        var prepared = rows.Select(row =>
        {
            PrepareInsert(model, row);
            foreach (var (column, value) in presets)
            {
                column.Property.SetValue(row, value);
            }

            return row;
        });
        if (heard)
        {
            prepared = prepared.ToList();
        }

        var (stage, create, drop) = StagingTable(dialect);
        var columnList = string.Join(", ", columns.Select(c => dialect.Column(c.Column.ColumnName)));
        var written = columns.Select(c => c.Column.ColumnName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var staged = new SqlSource(stage, "s", model.Table, model);
        var check = PolicyCondition(SourceOf(model, table), DmAction.Create, check: true) is { } condition
            ? SqlNode.ReplaceColumns(
                condition,
                c =>
                {
                    if (written.Contains(c.ColumnName))
                    {
                        return new SqlColumn(staged, c.ColumnName);
                    }

                    return model.Columns.FirstOrDefault(m => string.Equals(m.Column.ColumnName, c.ColumnName, StringComparison.OrdinalIgnoreCase)) is { } filled
                        && (filled.Column.IsAutoIncrement || !string.IsNullOrWhiteSpace(filled.Column.DefaultExpression))
                        ? throw new ArgumentException($"The policies' check on {model.Name} reads {c.ColumnName}, which the database fills when a row does not give it: give its value.")
                        : new SqlText("NULL");
                }
            )
            : null;

        return await InTransactionAsync(
                tx,
                needsOne: true,
                async transaction =>
                {
                    await Connection
                        .ExecuteAsync(Command(string.Format(System.Globalization.CultureInfo.InvariantCulture, create, columnList, stage, table), null, transaction, cancellationToken))
                        .ConfigureAwait(false);
                    var loaded = await DmlBulk.InsertAsync(Connection, dialect, stage, columns, prepared, options, transaction!, cancellationToken)
                        .ConfigureAwait(false);

                    if (check is not null)
                    {
                        var parameters = new DmlParameters();
                        var failing = new SqlNot(check);
                        var sql = $"SELECT COUNT(*) FROM {stage} WHERE {new SqlRenderer(dialect, parameters).For(failing).Render(failing)}";
                        if (await Connection.ExecuteScalarAsync<long>(Command(sql, parameters, transaction, cancellationToken)).ConfigureAwait(false) > 0)
                        {
                            throw new UnauthorizedAccessException($"A row breaks the policies' check on {model.Name}; nothing was inserted.");
                        }
                    }

                    await Connection
                        .ExecuteAsync(Command($"INSERT INTO {table} ({columnList}) SELECT {columnList} FROM {stage}", null, transaction, cancellationToken))
                        .ConfigureAwait(false);
                    await TellRowsAsync(model, DmChangeAction.Insert, prepared, transaction, cancellationToken).ConfigureAwait(false);
                    if (drop is not null)
                    {
                        await Connection
                            .ExecuteAsync(Command(string.Format(System.Globalization.CultureInfo.InvariantCulture, drop, stage), null, transaction, cancellationToken))
                            .ConfigureAwait(false);
                    }

                    return loaded;
                }
            )
            .ConfigureAwait(false);
    }

    // Rows typed object are rows of the first one's type; the rows are read once, so that one is replayed.
    private static (Type Type, IEnumerable<object> Rows) ModelTypeOf<T>(IEnumerable<T> rows)
        where T : class
    {
        if (typeof(T) != typeof(object))
        {
            return (typeof(T), rows);
        }

        var enumerator = rows.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            enumerator.Dispose();
            throw new ArgumentException("No rows typed object were given, so their model is unknown: name its type.", nameof(rows));
        }

        static IEnumerable<object> Replay(T first, IEnumerator<T> rest)
        {
            using (rest)
            {
                yield return first;
                while (rest.MoveNext())
                {
                    yield return rest.Current;
                }
            }
        }

        var first = enumerator.Current;
        return (first.GetType(), Replay(first, enumerator));
    }

    private async Task<long> BulkUpsertCoreAsync(
        Type type,
        IEnumerable<object> rows,
        LambdaExpression? onConflict,
        string? constraint,
        LambdaExpression? update,
        LambdaExpression? updateWhere,
        DmBulkOptions options,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    )
    {
        var (model, dialect, table) = Target(type);
        model.RequireWritable("BulkUpsertAsync");
        if (RunsAsCaller)
        {
            // Run as a caller, each row is upserted as UpsertAsync does it, in one transaction.
            return await InTransactionAsync(
                    tx,
                    needsOne: true,
                    async transaction =>
                    {
                        long upserted = 0;
                        foreach (var row in rows)
                        {
                            await UpsertCoreAsync(type, row, onConflict, constraint, update, updateWhere, transaction, cancellationToken).ConfigureAwait(false);
                            upserted++;
                        }

                        return upserted;
                    }
                )
                .ConfigureAwait(false);
        }

        var (target, constraintName) = DmlUpsert.Target(model, onConflict, constraint);
        if (model.Generated is { } generated && target.Contains(generated))
        {
            throw new ArgumentException(
                $"A bulk upsert cannot merge on {model.Type.Name}'s generated key, which new rows do not have: insert "
                    + "those with BulkInsertAsync, or merge on another unique key.",
                nameof(onConflict)
            );
        }

        var columns = Written(model, dialect);
        rows = rows.Select(row =>
        {
            PrepareInsert(model, row);
            return row;
        });
        var statement = WithBehaviours(new DmlUpsertStatement
        {
            Model = model,
            Dialect = dialect,
            Table = table,
            Columns = columns,
            Values = [],
            Target = target,
            Constraint = constraintName,
            Update = update,
            UpdateWhere = updateWhere,
        });

        var (stage, create, drop) = StagingTable(dialect);
        var columnList = string.Join(", ", columns.Select(c => dialect.Column(c.Column.ColumnName)));

        return await InTransactionAsync(
                tx,
                needsOne: true,
                async transaction =>
                {
                    await Connection
                        .ExecuteAsync(Command(string.Format(System.Globalization.CultureInfo.InvariantCulture, create, columnList, stage, table), null, transaction, cancellationToken))
                        .ConfigureAwait(false);

                    var loaded = await DmlBulk.InsertAsync(Connection, dialect, stage, columns, rows, options, transaction!, cancellationToken)
                        .ConfigureAwait(false);

                    var parameters = new DmlParameters();
                    var merge = DmlUpsert.Merge(statement, stage, parameters);
                    await Connection.ExecuteAsync(Command(merge, parameters, transaction, cancellationToken)).ConfigureAwait(false);

                    if (drop is not null)
                    {
                        await Connection
                            .ExecuteAsync(Command(string.Format(System.Globalization.CultureInfo.InvariantCulture, drop, stage), null, transaction, cancellationToken))
                            .ConfigureAwait(false);
                    }

                    return loaded;
                }
            )
            .ConfigureAwait(false);
    }
}
