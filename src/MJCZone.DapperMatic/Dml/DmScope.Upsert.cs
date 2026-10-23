// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Dapper;
using MJCZone.DapperMatic.Dml.Internal;

namespace MJCZone.DapperMatic.Dml;

/// <summary>
/// Upserts, which insert a row or update the row it conflicts with, and inserts of rows only if absent.
/// </summary>
public sealed partial class DmScope
{
    /// <summary>
    /// Inserts a row, or updates every column of the row with its primary key if there is one.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="row">The row.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The row's key, as <see cref="InsertAsync{T}"/> returns it.</returns>
    /// <remarks>
    /// <para>
    /// One statement, so two upserts of the same key at once leave one row: <c>ON CONFLICT</c> on PostgreSQL and
    /// SQLite, <c>ON DUPLICATE KEY UPDATE</c> on MySQL and MariaDB, and on SQL Server a check holding a range lock,
    /// then an <c>UPDATE</c> or an <c>INSERT</c>, in one transaction.
    /// </para>
    /// <para>
    /// A key the database generates is never inserted as given: a row whose generated key is unset is inserted and
    /// given one, and a row whose key is set is updated, which is an error if no row has the key.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">The generated key is set and no row has it.</exception>
    public Task<object?> UpsertAsync<T>(T row, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
        where T : class => UpsertAsync(ModelType(row), row, tx, cancellationToken);

    /// <summary>
    /// Inserts a row, or updates the row it conflicts with as the options say.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="row">The row.</param>
    /// <param name="options">The conflict columns, what to set, and when.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The row's key: for a key the database generates, the inserted row's new key or the conflicting row's key,
    /// which is also set on the row.
    /// </returns>
    /// <remarks>See <see cref="UpsertAsync{T}(T, IDbTransaction?, CancellationToken)"/>.</remarks>
    /// <exception cref="ArgumentException">The conflict columns are not the primary key or a unique constraint or index.</exception>
    /// <exception cref="DmTranslationException">An expression has a part that cannot be written as SQL.</exception>
    [OverloadResolutionPriority(1)] // `new() { ... }` also converts to the transaction: this overload is the one meant.
    public Task<object?> UpsertAsync<T>(
        T row,
        DmUpsertOptions<T> options,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        ArgumentNullException.ThrowIfNull(options);
        return UpsertCoreAsync(
            ModelType(row),
            row,
            options.OnConflict,
            options.OnConflictConstraint,
            options.Update,
            options.UpdateWhere,
            tx,
            cancellationToken
        );
    }

    /// <summary>
    /// Inserts a row, or updates every column of the row with its primary key if there is one.
    /// </summary>
    /// <param name="type">The model type.</param>
    /// <param name="row">The row.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The row's key, as <see cref="InsertAsync(Type, object, IDbTransaction?, CancellationToken)"/> returns it.</returns>
    /// <remarks>See <see cref="UpsertAsync{T}(T, IDbTransaction?, CancellationToken)"/>.</remarks>
    /// <exception cref="InvalidOperationException">The generated key is set and no row has it.</exception>
    public Task<object?> UpsertAsync(
        Type type,
        object row,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(type);
        return UpsertCoreAsync(type, row, null, null, null, null, tx, cancellationToken);
    }

    /// <summary>
    /// Inserts a row unless a row with its primary key exists.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="row">The row.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the row was inserted; a key the database generated is then set on it.</returns>
    public Task<bool> InsertIfAbsentAsync<T>(T row, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
        where T : class => InsertIfAbsentCoreAsync(ModelType(row), row, null, tx, cancellationToken);

    /// <summary>
    /// Inserts a row unless a row with the same values in some columns exists.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="row">The row.</param>
    /// <param name="onConflict">
    /// The columns, <c>u =&gt; u.Email</c> or <c>s =&gt; new { s.Day, s.Page }</c>: the primary key or the columns of a
    /// unique constraint or index.
    /// </param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the row was inserted; a key the database generated is then set on it.</returns>
    /// <exception cref="ArgumentException">The columns are not the primary key or a unique constraint or index.</exception>
    public Task<bool> InsertIfAbsentAsync<T>(
        T row,
        Expression<Func<T, object?>> onConflict,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        ArgumentNullException.ThrowIfNull(onConflict);
        return InsertIfAbsentCoreAsync(ModelType(row), row, onConflict, tx, cancellationToken);
    }

    /// <summary>
    /// Inserts a row unless a row with its primary key exists.
    /// </summary>
    /// <param name="type">The model type.</param>
    /// <param name="row">The row.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the row was inserted; a key the database generated is then set on it.</returns>
    public Task<bool> InsertIfAbsentAsync(Type type, object row, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(type);
        return InsertIfAbsentCoreAsync(type, row, null, tx, cancellationToken);
    }

    // MySqlConnector and MySql.Data both report a duplicate key as error number 1062.
    private static bool IsDuplicateKey(DbException exception) =>
        exception.GetType().GetProperty("Number")?.GetValue(exception) is 1062
        || exception.Message.StartsWith("Duplicate entry", StringComparison.Ordinal);

    private async Task<object?> UpsertCoreAsync(
        Type type,
        object row,
        LambdaExpression? onConflict,
        string? constraint,
        LambdaExpression? update,
        LambdaExpression? updateWhere,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(row);
        var (model, dialect, table) = Target(type);
        model.RequireWritable("UpsertAsync");
        PrepareInsert(model, row);
        var (target, constraintName) = DmlUpsert.Target(model, onConflict, constraint);
        var generated = model.Generated;
        if (RunsAsCaller)
        {
            return update is null && updateWhere is null
                ? await UpsertAsCallerAsync(type, model, dialect, table, row, target, tx, cancellationToken).ConfigureAwait(false)
                : throw new NotSupportedException(
                    "Run as a caller, an upsert updates the columns the caller may write from the row: for other assignments, "
                        + "UpdateAsync with a condition, then InsertAsync."
                );
        }

        // On the generated key itself: unset, nothing can conflict; set, the row must exist.
        if (generated is not null && target.Count == 1 && target[0] == generated)
        {
            var existingKey = generated.Property.GetValue(row);
            if (IsDefault(existingKey))
            {
                return await InsertAsync(type, row, tx, cancellationToken).ConfigureAwait(false);
            }

            await UpdateByGeneratedKeyAsync(model, dialect, table, row, update, updateWhere, tx, cancellationToken)
                .ConfigureAwait(false);
            await ReadTokenAsync(model, dialect, table, row, tx, cancellationToken, upserted: true).ConfigureAwait(false);
            return existingKey;
        }

        var statement = WithBehaviours(DmlUpsert.Statement(model, dialect, table, row, target, constraintName) with
        {
            Update = update,
            UpdateWhere = updateWhere,
        });
        var parameters = new DmlParameters();
        var sql = DmlUpsert.Upsert(statement, parameters);

        // SQL Server's check holds its lock only within a transaction.
        var sqlServer = dialect.ProviderType == DbProviderType.SqlServer;
        if (generated is null)
        {
            await InTransactionAsync(tx, sqlServer, t => Connection.ExecuteAsync(Command(sql, parameters, t, cancellationToken)))
                .ConfigureAwait(false);
            await ReadTokenAsync(model, dialect, table, row, tx, cancellationToken, upserted: true).ConfigureAwait(false);
            return model.Keys.Count == 1 ? model.Keys[0].Property.GetValue(row) : null;
        }

        var raw = await InTransactionAsync(
                tx,
                sqlServer,
                t => Connection.ExecuteScalarAsync<object>(Command(sql, parameters, t, cancellationToken))
            )
            .ConfigureAwait(false);

        // PostgreSQL and SQLite return no row when a condition left the conflicting row as it was: its key is read.
        raw ??= await KeyOfConflictingRowAsync(statement, tx, cancellationToken).ConfigureAwait(false);
        if (raw is null or DBNull)
        {
            return null;
        }

        // MySQL's LAST_INSERT_ID is the conflicting row's key even when the guard (its tenant) left it alone.
        if (statement.Guard is not null && dialect.ProviderType == DbProviderType.MySql)
        {
            raw = await GuardedKeyAsync(statement, raw, tx, cancellationToken).ConfigureAwait(false);
            if (raw is null or DBNull)
            {
                return null;
            }
        }

        var key = ConvertTo(raw, generated.Property.PropertyType, generated.Property.Name);
        if (generated.Property.CanWrite)
        {
            generated.Property.SetValue(row, key);
            await ReadTokenAsync(model, dialect, table, row, tx, cancellationToken, upserted: true).ConfigureAwait(false);
        }

        return key;
    }

    // Run as a caller, an upsert updates the row with the same values in its conflict columns, as UpdateAsync updates a
    // row; with none, it inserts as InsertAsync does. A row that exists and that the policies keep the caller from
    // changing refuses it: the database would refuse the insert anyway.
    private async Task<object?> UpsertAsCallerAsync(
        Type type,
        DmlModel model,
        DmlDialect dialect,
        string table,
        object row,
        IReadOnlyList<DmlColumn> target,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    )
    {
        var generated = model.Generated;
        var byGeneratedKey = generated is not null && target.Count == 1 && target[0] == generated;
        if (byGeneratedKey && IsDefault(generated!.Property.GetValue(row)))
        {
            return await InsertAsync(type, row, tx, cancellationToken).ConfigureAwait(false);
        }

        RequireConflict(model, target, update: true);
        return await InTransactionAsync(
                tx,
                needsOne: true,
                async transaction =>
                {
                    var token = model.Behaviours.Token;
                    var given = token?.Property.GetValue(row);
                    var existing = await ExistingAsync(model, dialect, table, row, target, token, transaction, cancellationToken).ConfigureAwait(false);
                    if (existing is null)
                    {
                        return byGeneratedKey
                            ? throw new InvalidOperationException(
                                $"No {model.Type.Name} row has the key {generated!.Property.GetValue(row)}, and a key the database generates is not inserted as given."
                            )
                            : await InsertAsync(type, row, transaction, cancellationToken).ConfigureAwait(false);
                    }

                    // The row is updated as read now: its token is the stored one.
                    if (token is not null && existing is not DBNull)
                    {
                        token.Property.SetValue(row, DmlValues.Convert(existing, token.Property.PropertyType));
                    }

                    int written;
                    try
                    {
                        written = await WriteRowAsync(model, dialect, table, row, DmlRowWrite.Update, transaction, cancellationToken, matchBy: target).ConfigureAwait(false);
                    }
                    catch (DmConcurrencyException)
                    {
                        written = 0;
                    }

                    if (written == 0)
                    {
                        // The row keeps the token it was given, not the one of a row the caller may not change.
                        token?.Property.SetValue(row, given);
                        throw new UnauthorizedAccessException(
                            $"A {model.Type.Name} row with the same {string.Join(", ", target.Select(c => c.Property.Name))} exists, and the policies do not let this caller change it."
                        );
                    }

                    if (model.Keys.Count != 1)
                    {
                        return null;
                    }

                    var key = model.Keys[0];
                    if (key == generated && IsDefault(key.Property.GetValue(row)))
                    {
                        var stored = await ExistingAsync(model, dialect, table, row, target, key, transaction, cancellationToken).ConfigureAwait(false);
                        if (key.Property.CanWrite && stored is not null and not DBNull)
                        {
                            key.Property.SetValue(row, ConvertTo(stored, key.Property.PropertyType, key.Property.Name));
                        }
                    }

                    return key.Property.GetValue(row);
                }
            )
            .ConfigureAwait(false);
    }

    // Reads a column of the row with the same values in some columns, whoever's it is (as a unique constraint sees
    // it): the column's value, DBNull for NULL, or null when there is no such row.
    private async Task<object?> ExistingAsync(
        DmlModel model,
        DmlDialect dialect,
        string table,
        object row,
        IReadOnlyList<DmlColumn> columns,
        DmlColumn? read,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    )
    {
        var source = new SqlSource(table, "t0", model.Table, model);
        var match = KeyMatch(columns, source, (c, _) => c.Property.GetValue(row));
        var parameters = new DmlParameters();
        var renderer = new SqlRenderer(dialect, parameters).For(match);
        var sql = $"SELECT {(read is null ? "1" : dialect.Column(read.Column.ColumnName))} FROM {table} WHERE {string.Join(" AND ", match.Select(renderer.Render))}";
        var rows = (await Connection.QueryAsync(Command(sql, parameters, tx, cancellationToken)).ConfigureAwait(false)).ToList();
        return rows.Count == 0 ? null : ((IDictionary<string, object?>)rows[0]).Values.First() ?? DBNull.Value;
    }

    private async Task<object?> KeyOfConflictingRowAsync(DmlUpsertStatement statement, IDbTransaction? tx, CancellationToken cancellationToken)
    {
        var dialect = statement.Dialect;
        var parameters = new DmlParameters();
        var renderer = new SqlRenderer(dialect, parameters);
        var match = string.Join(
            " AND ",
            statement.Target.Select(c =>
                $"{dialect.Column(c.Column.ColumnName)} = {renderer.Render(statement.Values[statement.Columns.ToList().IndexOf(c)])}"
            )
        );
        if (statement.Guard?.Invoke(new SqlSource(statement.Table, "t", statement.Model.Table, statement.Model)) is { } guard)
        {
            match += $" AND {renderer.Render(guard)}";
        }

        var sql = $"SELECT {dialect.Column(statement.ReturnKey!.Column.ColumnName)} FROM {statement.Table} WHERE {match}";
        return await Connection.ExecuteScalarAsync<object>(Command(sql, parameters, tx, cancellationToken)).ConfigureAwait(false);
    }

    // The key, if the row with it passes the statement's guard.
    private async Task<object?> GuardedKeyAsync(DmlUpsertStatement statement, object key, IDbTransaction? tx, CancellationToken cancellationToken)
    {
        var dialect = statement.Dialect;
        var parameters = new DmlParameters();
        var renderer = new SqlRenderer(dialect, parameters);
        var keyColumn = statement.ReturnKey!;
        var value = new SqlValue(keyColumn.Property.PropertyType, ConvertTo(key, keyColumn.Property.PropertyType, keyColumn.Property.Name));
        var guard = statement.Guard!.Invoke(new SqlSource(statement.Table, "t", statement.Model.Table, statement.Model));
        var column = dialect.Column(keyColumn.Column.ColumnName);
        var sql = $"SELECT {column} FROM {statement.Table} WHERE {column} = {renderer.Render(value)}"
            + (guard is null ? string.Empty : $" AND {renderer.Render(guard)}");
        return await Connection.ExecuteScalarAsync<object>(Command(sql, parameters, tx, cancellationToken)).ConfigureAwait(false);
    }

    // An upsert on a generated key that is set: an UPDATE of the row with it, which must exist.
    private async Task UpdateByGeneratedKeyAsync(
        DmlModel model,
        DmlDialect dialect,
        string table,
        object row,
        LambdaExpression? update,
        LambdaExpression? updateWhere,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    )
    {
        var generated = model.Generated!;
        var current = new SqlSource(table, "t", model.Table, model);
        var incoming = new SqlSource("incoming", "incoming", model.Table, model);
        var valueOf = model.Columns.ToDictionary(
            c => c.Column.ColumnName,
            c => new SqlValue(c.Property.PropertyType, c.Property.GetValue(row)),
            StringComparer.OrdinalIgnoreCase
        );

        var written = Written(model, dialect);
        List<(SqlColumn Column, SqlNode Value)> assignments = update is not null
            ? DmlTranslator.Assignments(update, current, [current, incoming], dialect)
            : written
                .Where(c => !model.Keys.Contains(c))
                .Select(c => (new SqlColumn(current, c.Column.ColumnName), (SqlNode)new SqlColumn(incoming, c.Column.ColumnName)))
                .ToList();
        var statement = WithBehaviours(DmlUpsert.Statement(model, dialect, table, row, [], null) with { Update = update, UpdateWhere = updateWhere });
        var guard = statement.Guard?.Invoke(current);
        var condition = statement.Finish(
            assignments,
            updateWhere is null ? null : DmlTranslator.Condition(updateWhere, [current, incoming], dialect),
            current,
            incoming
        );

        var parameters = new DmlParameters();
        SqlRenderer renderer = null!;
        renderer = new SqlRenderer(dialect, parameters)
        {
            ColumnWriter = c => ReferenceEquals(c.Source, incoming) ? renderer.Render(valueOf[c.ColumnName]) : null,
        }.For(assignments.ConvertAll(a => a.Value), condition);

        var keyMatch = $"{dialect.Column(generated.Column.ColumnName)} = {renderer.Render(valueOf[generated.Column.ColumnName])}"
            + (guard is null ? string.Empty : $" AND {renderer.Render(guard)}");
        var updated = 0;
        if (assignments.Count > 0)
        {
            var sql =
                $"UPDATE {table} SET {string.Join(", ", assignments.Select(a => $"{dialect.Column(a.Column.ColumnName)} = {renderer.Render(a.Value)}"))}"
                + $" WHERE {keyMatch}"
                + (condition is null ? string.Empty : $" AND {renderer.Render(condition)}");
            updated = await Connection.ExecuteAsync(Command(sql, parameters, tx, cancellationToken)).ConfigureAwait(false);
        }

        // No row updated: either the condition left it as it was, or there is none.
        if (updated == 0
            && await Connection
                    .ExecuteScalarAsync<long>(Command($"SELECT COUNT(*) FROM {table} WHERE {keyMatch}", parameters, tx, cancellationToken))
                    .ConfigureAwait(false) == 0)
        {
            throw new InvalidOperationException(
                $"No {model.Type.Name} row has the key {generated.Property.GetValue(row)}, and a key the database "
                    + "generates is not inserted as given: insert the row with its key unset."
            );
        }
    }

    private async Task<bool> InsertIfAbsentCoreAsync(
        Type type,
        object row,
        LambdaExpression? onConflict,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(row);
        var (model, dialect, table) = Target(type);
        model.RequireWritable("InsertIfAbsentAsync");
        PrepareInsert(model, row);
        var (target, constraint) = DmlUpsert.Target(model, onConflict, null);
        var generated = model.Generated;
        if (RunsAsCaller)
        {
            // Run as a caller: inserted as InsertAsync inserts, unless a row with the same values exists, whoever's.
            RequireConflict(model, target, update: false);
            if (generated is not null && target.Count == 1 && target[0] == generated && !IsDefault(generated.Property.GetValue(row)))
            {
                return await ExistingAsync(model, dialect, table, row, target, null, tx, cancellationToken).ConfigureAwait(false) is not null
                    ? false
                    : throw new InvalidOperationException(
                        $"No {model.Type.Name} row has the key {generated.Property.GetValue(row)}, and a key the database generates is not inserted as given."
                    );
            }

            return await InTransactionAsync(
                    tx,
                    needsOne: true,
                    async transaction =>
                    {
                        if ((generated is null || target.Count != 1 || target[0] != generated)
                            && await ExistingAsync(model, dialect, table, row, target, null, transaction, cancellationToken).ConfigureAwait(false) is not null)
                        {
                            return false;
                        }

                        await InsertAsync(type, row, transaction, cancellationToken).ConfigureAwait(false);
                        return true;
                    }
                )
                .ConfigureAwait(false);
        }

        if (generated is not null && target.Count == 1 && target[0] == generated)
        {
            var existingKey = generated.Property.GetValue(row);
            if (IsDefault(existingKey))
            {
                await InsertAsync(type, row, tx, cancellationToken).ConfigureAwait(false);
                return true;
            }

            return await SingleByIdAsync(type, existingKey!, tx, cancellationToken).ConfigureAwait(false) is null
                ? throw new InvalidOperationException(
                    $"No {model.Type.Name} row has the key {existingKey}, and a key the database generates is not "
                        + "inserted as given: insert the row with its key unset."
                )
                : false;
        }

        var statement = DmlUpsert.Statement(model, dialect, table, row, target, constraint);
        var parameters = new DmlParameters();
        var sql = DmlUpsert.InsertIfAbsent(statement, parameters);

        object? inserted;
        if (dialect.ProviderType == DbProviderType.MySql)
        {
            try
            {
                var first = await Connection.QueryFirstAsync(Command(sql, parameters, tx, cancellationToken)).ConfigureAwait(false);
                var result = (IDictionary<string, object?>)first;
                var values = result.Values.ToList();
                inserted = Convert.ToInt64(values[0], System.Globalization.CultureInfo.InvariantCulture) == 0 ? null : values[1];
            }
            catch (DbException exception) when (IsDuplicateKey(exception))
            {
                return false;
            }
        }
        else
        {
            inserted = await InTransactionAsync(
                    tx,
                    dialect.ProviderType == DbProviderType.SqlServer,
                    t => Connection.ExecuteScalarAsync<object>(Command(sql, parameters, t, cancellationToken))
                )
                .ConfigureAwait(false);
        }

        if (inserted is null or DBNull)
        {
            return false;
        }

        if (generated is not null && generated.Property.CanWrite)
        {
            generated.Property.SetValue(row, ConvertTo(inserted, generated.Property.PropertyType, generated.Property.Name));
        }

        return true;
    }
}
