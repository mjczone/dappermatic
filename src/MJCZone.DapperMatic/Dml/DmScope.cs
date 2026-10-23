// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dapper;
using MJCZone.DapperMatic.Dml.Internal;
using MJCZone.DapperMatic.Security;
using MJCZone.DapperMatic.TypeMapping;

namespace MJCZone.DapperMatic.Dml;

/// <summary>
/// Reads and writes the rows of model tables through a connection, in the model's own schema and table or in ones
/// the scope substitutes, such as a tenant's schema.
/// </summary>
/// <remarks>
/// The extension methods on <see cref="IDbConnection"/> in this namespace use a scope with no substitutions. A scope is
/// immutable and holds nothing beyond a connection and the names it substitutes, so one can be made per request.
/// </remarks>
public sealed partial class DmScope
{
    private static readonly ConcurrentDictionary<(Type, string, DbProviderType, string), string> Statements = new();

    private readonly DmlScopeState _state;

    /// <summary>
    /// Initializes a new instance of the <see cref="DmScope"/> class with no substitutions.
    /// </summary>
    /// <param name="connection">The connection.</param>
    public DmScope(IDbConnection connection)
        : this(connection, new DmlScopeState()) { }

    private DmScope(IDbConnection connection, DmlScopeState state)
    {
        ArgumentNullException.ThrowIfNull(connection);
        Connection = connection;
        _state = state;

        // Model properties such as List<OrderLine> need DapperMatic's type handlers.
        if (!DapperMaticTypeMapping.IsInitialized)
        {
            DapperMaticTypeMapping.Initialize();
        }
    }

    /// <summary>
    /// Gets the connection.
    /// </summary>
    public IDbConnection Connection { get; }

    /// <summary>
    /// Gets the schema the scope uses for every model, or null for each model's own.
    /// </summary>
    public string? SchemaName => _state.SchemaName;

    /// <summary>
    /// Uses a schema for every model, such as a tenant's, instead of each model's own.
    /// </summary>
    /// <param name="schemaName">The schema.</param>
    /// <returns>A scope using it.</returns>
    /// <exception cref="NotSupportedException">The provider has no schemas.</exception>
    public DmScope InSchema(string schemaName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);
        if (!Connection.SupportsSchemas())
        {
            throw new NotSupportedException(
                $"{Connection.GetDbProviderType()} has no schemas, so a schema cannot be substituted."
            );
        }

        return new DmScope(Connection, _state with { SchemaName = schemaName });
    }

    /// <summary>
    /// Uses a table for a model, such as an archive table, instead of the model's own.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="tableName">The table.</param>
    /// <returns>A scope using it.</returns>
    public DmScope WithTableName<T>(string tableName) => WithTableName(typeof(T), tableName);

    /// <summary>
    /// Uses a table for a model, such as an archive table, instead of the model's own.
    /// </summary>
    /// <param name="type">The model type.</param>
    /// <param name="tableName">The table.</param>
    /// <returns>A scope using it.</returns>
    public DmScope WithTableName(Type type, string tableName)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        return new DmScope(Connection, _state with { TableNames = new Dictionary<Type, string>(_state.TableNames) { [type] = tableName } });
    }

    /// <summary>
    /// Inserts a row.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="row">The row.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The row's key: the value the database generated for an auto-increment key, which is also set on the row; the
    /// key the row carries otherwise; or null for a composite key or no key.
    /// </returns>
    /// <remarks>An auto-increment key column is left for the database to fill. Every other column is written.</remarks>
    public Task<object?> InsertAsync<T>(T row, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
        where T : class => InsertAsync(ModelType(row), row, tx, cancellationToken);

    /// <summary>
    /// Inserts a row.
    /// </summary>
    /// <param name="type">The model type.</param>
    /// <param name="row">The row.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The row's key: the value the database generated for an auto-increment key, which is also set on the row; the
    /// key the row carries otherwise; or null for a composite key or no key.
    /// </returns>
    /// <remarks>An auto-increment key column is left for the database to fill. Every other column is written.</remarks>
    public async Task<object?> InsertAsync(
        Type type,
        object row,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(row);
        var (model, dialect, table) = Target(type);
        model.RequireWritable("InsertAsync");

        // A change heard is told in the insert's transaction: one of its own when none is given.
        if (tx is null && Heard(model) is not null)
        {
            return await InTransactionAsync(null, needsOne: true, t => InsertAsync(type, row, t, cancellationToken)).ConfigureAwait(false);
        }

        PrepareInsert(model, row);

        // Run as a caller, the row is given the policies' values, checked, and written without the columns the
        // caller may not write.
        List<DmlColumn>? held = null;
        if (RunsAsCaller)
        {
            held = PrepareCallerInsert(model, dialect, [row]);
            await RequireInsertCheckAsync(model, dialect, table, [row], held, tx, cancellationToken).ConfigureAwait(false);
        }

        var generated = model.Generated;
        string Build()
        {
            var insert = InsertStatement(model, dialect, table, held);
            return generated is null
                ? insert
                : dialect.ReturningGeneratedKey(insert, dialect.Column(generated.Column.ColumnName));
        }

        var sql = held is null ? Statement(type, "insert", dialect, table, Build) : Build();

        if (generated is null)
        {
            await Connection.ExecuteAsync(Command(sql, row, tx, cancellationToken)).ConfigureAwait(false);
            await ReadTokenAsync(model, dialect, table, row, tx, cancellationToken).ConfigureAwait(false);
            await TellRowsAsync(model, DmChangeAction.Insert, [row], tx, cancellationToken).ConfigureAwait(false);
            return model.Keys.Count == 1 ? model.Keys[0].Property.GetValue(row) : null;
        }

        var raw = await Connection.ExecuteScalarAsync<object>(Command(sql, row, tx, cancellationToken)).ConfigureAwait(false);
        var key = ConvertTo(raw, generated.Property.PropertyType, generated.Property.Name);
        if (generated.Property.CanWrite)
        {
            generated.Property.SetValue(row, key);
        }

        await ReadTokenAsync(model, dialect, table, row, tx, cancellationToken).ConfigureAwait(false);
        if (Heard(model) is { } wanted && tx is not null)
        {
            // The key the database generated, whether or not the row can take it.
            var told = ChangeColumns(model, wanted).ToDictionary(c => c.Column.ColumnName, c => c == generated ? key : c.Property.GetValue(row), StringComparer.OrdinalIgnoreCase);
            await TellAsync(model, DmChangeAction.Insert, [told], 1, tx, cancellationToken).ConfigureAwait(false);
        }

        return key;
    }

    /// <summary>
    /// Inserts rows, several to a statement.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="rows">The rows.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows inserted.</returns>
    /// <remarks>
    /// Statements are kept under the provider's limits on parameters and rows. With no transaction given, rows that
    /// take more than one statement are inserted in a transaction of the method's own, so either all are inserted or
    /// none. Keys the database generates are not set on the rows.
    /// </remarks>
    public Task<int> InsertManyAsync<T>(
        IEnumerable<T> rows,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        ArgumentNullException.ThrowIfNull(rows);
        var list = rows as IReadOnlyList<T> ?? [.. rows];
        return InsertManyAsync(typeof(T) == typeof(object) && list.Count > 0 ? list[0].GetType() : typeof(T), list, tx, cancellationToken);
    }

    /// <summary>
    /// Inserts rows, several to a statement.
    /// </summary>
    /// <param name="type">The model type.</param>
    /// <param name="rows">The rows.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows inserted.</returns>
    /// <remarks>
    /// Statements are kept under the provider's limits on parameters and rows. With no transaction given, rows that
    /// take more than one statement are inserted in a transaction of the method's own, so either all are inserted or
    /// none. Keys the database generates are not set on the rows.
    /// </remarks>
    public async Task<int> InsertManyAsync(
        Type type,
        IEnumerable<object> rows,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(rows);
        var list = rows as IReadOnlyList<object> ?? [.. rows];
        if (list.Count == 0)
        {
            return 0;
        }

        var (model, dialect, table) = Target(type);
        model.RequireWritable("InsertManyAsync");

        // A change heard is told with the rows' keys: those the database generates are read row by row.
        var heard = Heard(model) is not null;
        if (heard && model.Generated is not null)
        {
            return await InTransactionAsync(
                    tx,
                    needsOne: true,
                    async t =>
                    {
                        foreach (var row in list)
                        {
                            await InsertAsync(type, row, t, cancellationToken).ConfigureAwait(false);
                        }

                        return list.Count;
                    }
                )
                .ConfigureAwait(false);
        }

        foreach (var row in list)
        {
            PrepareInsert(model, row);
        }

        var columns = RunsAsCaller ? PrepareCallerInsert(model, dialect, list) : Written(model, dialect);
        if (RunsAsCaller)
        {
            await RequireInsertCheckAsync(model, dialect, table, list, columns, tx, cancellationToken).ConfigureAwait(false);
        }

        if (columns.Count == 0)
        {
            var inserted = 0;
            for (var i = 0; i < list.Count; i++)
            {
                inserted += await Connection
                    .ExecuteAsync(Command(dialect.InsertDefaults(table), null, tx, cancellationToken))
                    .ConfigureAwait(false);
            }

            return inserted;
        }

        var rowsPerStatement = Math.Max(1, Math.Min(dialect.MaxRowsPerInsert, dialect.MaxParameters / columns.Count));
        var columnList = string.Join(", ", columns.Select(c => dialect.Column(c.Column.ColumnName)));

        return await InTransactionAsync(
                tx,
                heard || list.Count > rowsPerStatement,
                async transaction =>
                {
                    var affected = 0;
                    for (var start = 0; start < list.Count; start += rowsPerStatement)
                    {
                        var parameters = new DmlParameters();
                        var values = new List<string>();
                        for (var i = start; i < Math.Min(start + rowsPerStatement, list.Count); i++)
                        {
                            var names = new List<string>();
                            foreach (var column in columns)
                            {
                                var name = $"r{i - start}_{names.Count}";
                                parameters.Add(name, column.Property.PropertyType, column.Property.GetValue(list[i]));
                                names.Add("@" + name);
                            }

                            values.Add($"({string.Join(", ", names)})");
                        }

                        var sql = $"INSERT INTO {table} ({columnList}) VALUES {string.Join(", ", values)}";
                        affected += await Connection
                            .ExecuteAsync(Command(sql, parameters, transaction, cancellationToken))
                            .ConfigureAwait(false);
                    }

                    await TellRowsAsync(model, DmChangeAction.Insert, list, transaction, cancellationToken).ConfigureAwait(false);
                    return affected;
                }
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Updates every column of a row but its key, by its key.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="row">The row.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows updated: 1, or 0 if no row has the key.</returns>
    public Task<int> UpdateAsync<T>(T row, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
        where T : class => UpdateAsync(ModelType(row), row, tx, cancellationToken);

    /// <summary>
    /// Updates every column of a row but its key, by its key.
    /// </summary>
    /// <param name="type">The model type.</param>
    /// <param name="row">The row.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows updated: 1, or 0 if no row has the key.</returns>
    public async Task<int> UpdateAsync(
        Type type,
        object row,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(row);
        var (model, dialect, table) = Target(type);
        model.RequireWritable("UpdateAsync");
        model.RequireKey("UpdateAsync");

        if (model.Behaviours.Any || RunsAsCaller)
        {
            return await WriteRowAsync(model, dialect, table, row, DmlRowWrite.Update, tx, cancellationToken).ConfigureAwait(false);
        }

        var set = model.Columns.Where(c => !model.Keys.Contains(c)).ToList();
        if (set.Count == 0)
        {
            return 0;
        }

        var sql = Statement(
            type,
            "update",
            dialect,
            table,
            () =>
                $"UPDATE {table} SET {string.Join(", ", set.Select(c => $"{dialect.Column(c.Column.ColumnName)} = @{c.ParameterName}"))}"
                + $" WHERE {KeyCondition(model, dialect, byParameterName: true)}"
        );

        return await Connection.ExecuteAsync(Command(sql, row, tx, cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes a row, by its key.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="row">The row.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows deleted: 1, or 0 if no row has the key.</returns>
    public Task<int> DeleteAsync<T>(T row, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
        where T : class => DeleteAsync(ModelType(row), row, tx, cancellationToken);

    /// <summary>
    /// Deletes a row, by its key.
    /// </summary>
    /// <param name="type">The model type.</param>
    /// <param name="row">The row.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows deleted: 1, or 0 if no row has the key.</returns>
    public async Task<int> DeleteAsync(
        Type type,
        object row,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(row);
        var (model, dialect, table) = Target(type);
        model.RequireWritable("DeleteAsync");
        model.RequireKey("DeleteAsync");
        if (model.Behaviours.Any || RunsAsCaller)
        {
            return await WriteRowAsync(model, dialect, table, row, DmlRowWrite.Delete, tx, cancellationToken).ConfigureAwait(false);
        }

        var sql = Statement(
            type,
            "delete",
            dialect,
            table,
            () => $"DELETE FROM {table} WHERE {KeyCondition(model, dialect, byParameterName: true)}"
        );

        return await Connection.ExecuteAsync(Command(sql, row, tx, cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes the row with a key.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="key">
    /// The key: its value, or for a composite key an object or tuple holding each key property, such as
    /// <c>new { OrderId = 1, LineNo = 2 }</c> or <c>(1, 2)</c>.
    /// </param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows deleted: 1, or 0 if no row has the key.</returns>
    public Task<int> DeleteByIdAsync<T>(object key, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
        where T : class => DeleteByIdAsync(typeof(T), key, tx, cancellationToken);

    /// <summary>
    /// Deletes the row with a key.
    /// </summary>
    /// <param name="type">The model type.</param>
    /// <param name="key">
    /// The key: its value, or for a composite key an object or tuple holding each key property, such as
    /// <c>new { OrderId = 1, LineNo = 2 }</c> or <c>(1, 2)</c>.
    /// </param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows deleted: 1, or 0 if no row has the key.</returns>
    public async Task<int> DeleteByIdAsync(
        Type type,
        object key,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(key);
        var (model, dialect, table) = Target(type);
        model.RequireWritable("DeleteByIdAsync");
        model.RequireKey("DeleteByIdAsync");
        if (model.Behaviours.Any || RunsAsCaller)
        {
            return await DeleteWhereAsync(model, dialect, table, null, key, hard: false, tx, cancellationToken).ConfigureAwait(false);
        }

        var sql = Statement(
            type,
            "delete-by-key",
            dialect,
            table,
            () => $"DELETE FROM {table} WHERE {KeyCondition(model, dialect, byParameterName: false)}"
        );

        return await Connection
            .ExecuteAsync(Command(sql, KeyParameters(model, key), tx, cancellationToken))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the row with a key.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="key">
    /// The key: its value, or for a composite key an object or tuple holding each key property, such as
    /// <c>new { OrderId = 1, LineNo = 2 }</c> or <c>(1, 2)</c>.
    /// </param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The row, or null if there is none.</returns>
    public async Task<T?> SingleByIdAsync<T>(object key, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
        where T : class
        => (T?)await SingleByIdAsync(typeof(T), key, tx, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Reads the row with a key.
    /// </summary>
    /// <param name="type">The model type.</param>
    /// <param name="key">
    /// The key: its value, or for a composite key an object or tuple holding each key property, such as
    /// <c>new { OrderId = 1, LineNo = 2 }</c> or <c>(1, 2)</c>.
    /// </param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The row, or null if there is none.</returns>
    public async Task<object?> SingleByIdAsync(
        Type type,
        object key,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(key);
        var (model, dialect, table) = Target(type);
        model.RequireKey("SingleByIdAsync");
        if (model.Behaviours.Any || RunsAsCaller)
        {
            var source = SourceOf(model, table);
            var scope = SqlScope.Select(dialect, TableOf, source, FiltersFor, HiddenFor);
            var filtered = new SqlSelect
            {
                From = source,
                Items = Items(source),
                Where =
                [
                    .. scope.Filters(source),
                    .. KeyMatch(
                        model,
                        source,
                        (k, i) => ConvertTo(model.Keys.Count == 1 && IsKeyValue(key) ? key : KeyPart(key, k, i), k.Property.PropertyType, k.Property.Name)
                    ),
                ],
            };
            var keyed = new DmlParameters();
            return await Connection
                .QuerySingleOrDefaultAsync(type, Command(Render(filtered, dialect, keyed), keyed, tx, cancellationToken))
                .ConfigureAwait(false);
        }

        var sql = Statement(
            type,
            "select-by-key",
            dialect,
            table,
            () => $"SELECT {SelectList(model, dialect)} FROM {table} WHERE {KeyCondition(model, dialect, byParameterName: false)}"
        );

        return await Connection
            .QuerySingleOrDefaultAsync(type, Command(sql, KeyParameters(model, key), tx, cancellationToken))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the rows with any of the given keys, in no particular order.
    /// </summary>
    /// <typeparam name="T">The model type, which must have a single-column key.</typeparam>
    /// <param name="keys">The keys.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rows found.</returns>
    public async Task<List<T>> SelectByIdsAsync<T>(
        IEnumerable<object> keys,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class
        => [.. (await SelectByIdsAsync(typeof(T), keys, tx, cancellationToken).ConfigureAwait(false)).Cast<T>()];

    /// <summary>
    /// Reads the rows with any of the given keys, in no particular order.
    /// </summary>
    /// <param name="type">The model type, which must have a single-column key.</param>
    /// <param name="keys">The keys.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rows found.</returns>
    public async Task<List<object>> SelectByIdsAsync(
        Type type,
        IEnumerable<object> keys,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(keys);
        var (model, dialect, table) = Target(type);
        model.RequireKey("SelectByIdsAsync");
        if (model.Keys.Count != 1)
        {
            throw new NotSupportedException(
                $"SelectByIdsAsync needs a single-column key, and {type.Name}'s has {model.Keys.Count} columns."
            );
        }

        var key = model.Keys[0];
        var values = keys.Select(k => ConvertTo(k, key.Property.PropertyType, key.Property.Name)).ToList();
        var results = new List<object>();
        var perStatement = Math.Min(dialect.MaxParameters, 1_000);
        for (var start = 0; start < values.Count; start += perStatement)
        {
            var parameters = new DmlParameters();
            var names = new List<string>();
            foreach (var value in values.Skip(start).Take(perStatement))
            {
                var name = $"k{names.Count}";
                parameters.Add(name, key.Property.PropertyType, value);
                names.Add("@" + name);
            }

            var sql =
                $"SELECT {SelectList(model, dialect, hidden: HiddenFor(model))} FROM {table} WHERE {dialect.Column(key.Column.ColumnName)} IN ({string.Join(", ", names)})";
            if (model.Behaviours.Any || RunsAsCaller)
            {
                var source = SourceOf(model, table);
                var filters = SqlScope.Select(dialect, TableOf, source, FiltersFor, HiddenFor).Filters(source).ToList();
                var renderer = new SqlRenderer(dialect, parameters).For(filters);
                sql += string.Concat(filters.Select(f => $" AND {renderer.Render(f)}"));
            }

            results.AddRange(
                await Connection.QueryAsync(type, Command(sql, parameters, tx, cancellationToken)).ConfigureAwait(false)
            );
        }

        return results;
    }

    /// <summary>
    /// Reads the rows that match a condition, or every row.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="where">The condition, such as <c>o =&gt; o.Status == OrderStatus.Open</c>, or null for every row.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rows, in no particular order.</returns>
    /// <exception cref="DmTranslationException">The condition has a part that cannot be written as SQL.</exception>
    public async Task<List<T>> SelectAsync<T>(
        Expression<Func<T, bool>>? where = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        var (model, dialect, table) = Target<T>();
        var parameters = new DmlParameters();
        var sql = Render(SelectOf(model, dialect, table, where), dialect, parameters);
        var rows = await Connection.QueryAsync<T>(Command(sql, parameters, tx, cancellationToken)).ConfigureAwait(false);
        return rows.AsList();
    }

    /// <summary>
    /// Reads the one row that matches a condition.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="where">The condition.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The row, or null if no row matches.</returns>
    /// <exception cref="InvalidOperationException">More than one row matches.</exception>
    /// <exception cref="DmTranslationException">The condition has a part that cannot be written as SQL.</exception>
    public async Task<T?> SingleAsync<T>(
        Expression<Func<T, bool>> where,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        ArgumentNullException.ThrowIfNull(where);
        var (model, dialect, table) = Target<T>();
        var parameters = new DmlParameters();
        var sql = Render(SelectOf(model, dialect, table, where), dialect, parameters);
        return await Connection
            .QuerySingleOrDefaultAsync<T>(Command(sql, parameters, tx, cancellationToken))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the first row that matches a condition, in key order.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="where">The condition, or null for any row.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The row with the lowest key that matches, any matching row for a model with no key, or null.</returns>
    /// <exception cref="DmTranslationException">The condition has a part that cannot be written as SQL.</exception>
    public async Task<T?> FirstAsync<T>(
        Expression<Func<T, bool>>? where = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        var (model, dialect, table) = Target<T>();
        var parameters = new DmlParameters();
        var select = SelectOf(model, dialect, table, where);
        var sql = Render(select with { OrderBy = KeyOrder(select.From), Take = 1 }, dialect, parameters);
        return await Connection
            .QueryFirstOrDefaultAsync<T>(Command(sql, parameters, tx, cancellationToken))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Counts the rows that match a condition, or every row.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="where">The condition, or null for every row.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows.</returns>
    /// <exception cref="DmTranslationException">The condition has a part that cannot be written as SQL.</exception>
    public async Task<long> CountAsync<T>(
        Expression<Func<T, bool>>? where = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        var (model, dialect, table) = Target<T>();
        var parameters = new DmlParameters();
        var sql = Render(SelectOf(model, dialect, table, where, [new SqlSelectItem(new SqlAggregate("COUNT", null), null)]), dialect, parameters);
        return await Connection.ExecuteScalarAsync<long>(Command(sql, parameters, tx, cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// Tells whether any row matches a condition, or whether the table has any rows.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="where">The condition, or null for any row.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if a row matches.</returns>
    /// <exception cref="DmTranslationException">The condition has a part that cannot be written as SQL.</exception>
    public async Task<bool> ExistsAsync<T>(
        Expression<Func<T, bool>>? where = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        var (model, dialect, table) = Target<T>();
        var parameters = new DmlParameters();
        var exists = new SqlExists(SelectOf(model, dialect, table, where, []));
        var sql = $"SELECT CASE WHEN {new SqlRenderer(dialect, parameters).For(exists).Render(exists)} THEN 1 ELSE 0 END";
        return await Connection.ExecuteScalarAsync<long>(Command(sql, parameters, tx, cancellationToken)).ConfigureAwait(false)
            != 0;
    }

    /// <summary>
    /// Sets columns of the rows that match a condition.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="set">
    /// The columns to set and their values, as an object initializer: <c>o =&gt; new Order { Status = s, Note = null }</c>.
    /// </param>
    /// <param name="where">The condition. To update every row, pass <c>o =&gt; true</c>.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows updated; 0 without a round trip if <paramref name="set"/> sets nothing.</returns>
    /// <exception cref="DmTranslationException">An expression has a part that cannot be written as SQL.</exception>
    public async Task<int> UpdateAsync<T>(
        Expression<Func<T, T>> set,
        Expression<Func<T, bool>> where,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(where);
        var (model, dialect, table) = Target<T>();
        model.RequireWritable("UpdateAsync");

        var parameters = new DmlParameters();
        var source = SourceOf(model, table);
        var scope = SqlScope.Write(dialect, TableOf, source, FiltersFor, HiddenFor);
        var assignments = DmlTranslator.Assignments(set, source, scope);
        if (assignments.Count == 0)
        {
            return 0;
        }

        if (model.Behaviours.TenantKey is { } tenantKey && TenantOf(model) is not null
            && assignments.Any(a => string.Equals(a.Column.ColumnName, tenantKey.Column.ColumnName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"UpdateAsync does not move {model.Type.Name} rows to another tenant: set {tenantKey.Property.Name} for every tenant's rows, with AllTenants().");
        }

        // Run as a caller: the columns set must be ones the caller may write; the policies write theirs; the rows
        // reached are those the policies' filters for updating (and reading) let through, and they must pass the check.
        if (Require(model, DmAction.Update) is { } rules)
        {
            RequireWritable(model, DmAction.Update, assignments.Select(a => a.Column.ColumnName));
            foreach (var (column, value) in Presets(model, rules))
            {
                assignments.RemoveAll(a => string.Equals(a.Column.ColumnName, column.Column.ColumnName, StringComparison.OrdinalIgnoreCase));
                assignments.Add((new SqlColumn(source, column.Column.ColumnName), new SqlValue(column.Property.PropertyType, value)));
            }
        }

        assignments.AddRange(UpdateAssignments(model, source, dialect, [.. assignments.Select(a => a.Column.ColumnName)]));

        // The columns set are named alone: PostgreSQL refuses a qualified one.
        List<SqlNode> conditions = [.. scope.Filters(source), DmlTranslator.Condition(where, source, scope)];
        conditions.AddRange(new[] { PolicyCondition(source, DmAction.Update) }.OfType<SqlNode>());
        var renderer = new SqlRenderer(dialect, parameters).For(assignments.ConvertAll(a => a.Value), conditions);
        var setList = string.Join(", ", assignments.Select(a => $"{dialect.Column(a.Column.ColumnName)} = {renderer.Render(a.Value)}"));
        var check = CheckOver(source, DmAction.Update, assignments.ToDictionary(a => a.Column.ColumnName, a => a.Value, StringComparer.OrdinalIgnoreCase));
        return await UpdateCheckedAsync(model, dialect, table, setList, string.Join(" AND ", conditions.Select(renderer.Render)), parameters, check, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes the rows that match a condition.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="where">The condition. To delete every row, use <see cref="DeleteAllAsync{T}"/>.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows deleted.</returns>
    /// <exception cref="DmTranslationException">The condition has a part that cannot be written as SQL.</exception>
    public async Task<int> DeleteAsync<T>(
        Expression<Func<T, bool>> where,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        ArgumentNullException.ThrowIfNull(where);
        var (model, dialect, table) = Target<T>();
        model.RequireWritable("DeleteAsync");
        if (model.Behaviours.Any || RunsAsCaller)
        {
            return await DeleteWhereAsync(model, dialect, table, where, hard: false, tx, cancellationToken).ConfigureAwait(false);
        }

        var parameters = new DmlParameters();
        var source = SourceOf(model, table);
        var condition = DmlTranslator.Condition(where, source, SqlScope.Write(dialect, TableOf, source, FiltersFor, HiddenFor));
        var sql = $"DELETE FROM {table} WHERE {new SqlRenderer(dialect, parameters).For(condition).Render(condition)}";
        return await Connection.ExecuteAsync(Command(sql, parameters, tx, cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes every row.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows deleted.</returns>
    public Task<int> DeleteAllAsync<T>(IDbTransaction? tx = null, CancellationToken cancellationToken = default)
        where T : class => DeleteAllAsync(typeof(T), tx, cancellationToken);

    /// <summary>
    /// Deletes every row.
    /// </summary>
    /// <param name="type">The model type.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows deleted.</returns>
    public async Task<int> DeleteAllAsync(Type type, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(type);
        var (model, dialect, table) = Target(type);
        model.RequireWritable("DeleteAllAsync");
        if (model.Behaviours.Any || RunsAsCaller)
        {
            return await DeleteWhereAsync(model, dialect, table, null, hard: false, tx, cancellationToken).ConfigureAwait(false);
        }

        return await Connection.ExecuteAsync(Command($"DELETE FROM {table}", null, tx, cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads every row of a model.
    /// </summary>
    /// <param name="type">The model type.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rows, in no particular order.</returns>
    public async Task<List<object>> SelectAsync(Type type, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(type);
        var (model, dialect, table) = Target(type);
        var parameters = new DmlParameters();
        var sql = Render(SelectOf(model, dialect, table, null), dialect, parameters);
        var rows = await Connection.QueryAsync(type, Command(sql, parameters, tx, cancellationToken)).ConfigureAwait(false);
        return rows.AsList();
    }

    /// <summary>
    /// Reads the row of a model with the lowest key.
    /// </summary>
    /// <param name="type">The model type.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The row with the lowest key, any row for a model with no key, or null if there are none.</returns>
    public async Task<object?> FirstAsync(Type type, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(type);
        var (model, dialect, table) = Target(type);
        var parameters = new DmlParameters();
        var select = SelectOf(model, dialect, table, null);
        var sql = Render(select with { OrderBy = KeyOrder(select.From), Take = 1 }, dialect, parameters);
        return await Connection.QueryFirstOrDefaultAsync(type, Command(sql, parameters, tx, cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// Counts the rows of a model.
    /// </summary>
    /// <param name="type">The model type.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows.</returns>
    public async Task<long> CountAsync(Type type, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(type);
        var (model, dialect, table) = Target(type);
        var parameters = new DmlParameters();
        var sql = Render(SelectOf(model, dialect, table, null, [new SqlSelectItem(new SqlAggregate("COUNT", null), null)]), dialect, parameters);
        return await Connection.ExecuteScalarAsync<long>(Command(sql, parameters, tx, cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// Tells whether a model's table has any rows.
    /// </summary>
    /// <param name="type">The model type.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if it does.</returns>
    public async Task<bool> ExistsAsync(Type type, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(type);
        var (model, dialect, table) = Target(type);
        var parameters = new DmlParameters();
        var exists = new SqlExists(SelectOf(model, dialect, table, null, []));
        var sql = $"SELECT CASE WHEN {new SqlRenderer(dialect, parameters).For(exists).Render(exists)} THEN 1 ELSE 0 END";
        return await Connection.ExecuteScalarAsync<long>(Command(sql, parameters, tx, cancellationToken)).ConfigureAwait(false) != 0;
    }

    /// <summary>
    /// Starts a query of a model's rows, refined with <c>Where</c>, <c>OrderBy</c>, <c>Skip</c>, <c>Take</c> and
    /// <c>Select</c>.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <returns>A query of every row, which runs only when one of its <c>...Async</c> methods is called.</returns>
    public DmQuery<T> From<T>()
        where T : class => new(this);

    /// <summary>
    /// Lists columns for a <c>SELECT</c>, each aliased to its property's name unless asked not to.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="dialect">The dialect.</param>
    /// <param name="columns">The columns, or null for every column.</param>
    /// <param name="aliased">Whether to alias each column to its property's name.</param>
    /// <param name="hidden">The columns the caller may not read, written as NULL; null for none.</param>
    /// <returns>The list.</returns>
    internal static string SelectList(
        DmlModel model,
        DmlDialect dialect,
        IEnumerable<DmlColumn>? columns = null,
        bool aliased = true,
        IReadOnlySet<string>? hidden = null
    ) =>
        string.Join(
            ", ",
            (columns ?? model.Columns).Select(c =>
            {
                // A column the caller may not read is read as empty.
                var read = hidden?.Contains(c.Column.ColumnName) == true ? "NULL" : dialect.Column(c.Column.ColumnName);
                return aliased ? $"{read} AS {dialect.Alias(c.Property.Name)}" : read;
            })
        );

    /// <summary>
    /// Lists a source's columns for a <c>SELECT</c>, each aliased to its property's name unless asked not to.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="columns">The columns, or null for every column of the model.</param>
    /// <param name="aliased">Whether to alias each column to its property's name.</param>
    /// <returns>The items.</returns>
    internal static List<SqlSelectItem> Items(SqlSource source, IEnumerable<DmlColumn>? columns = null, bool aliased = true) =>
        [
            .. (columns ?? source.Model!.Columns).Select(c =>
                new SqlSelectItem(new SqlColumn(source, c.Column.ColumnName), aliased ? c.Property.Name : null)
            ),
        ];

    /// <summary>
    /// Makes a Dapper command.
    /// </summary>
    /// <param name="sql">The SQL.</param>
    /// <param name="parameters">The parameters, or null.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The command.</returns>
    internal static CommandDefinition Command(
        string sql,
        object? parameters,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    ) => new(sql, parameters, tx, cancellationToken: cancellationToken);

    /// <summary>
    /// Names the table the scope reads and writes a model's rows in.
    /// </summary>
    /// <param name="type">The model type.</param>
    /// <returns>The quoted, qualified table.</returns>
    internal string TableOf(Type type) => Target(type).Table;

    /// <summary>
    /// Resolves a model, the connection's dialect, and the table the scope reads and writes the model's rows in.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <returns>The model, the dialect, and the quoted, qualified table.</returns>
    internal (DmlModel Model, DmlDialect Dialect, string Table) Target<T>() => Target(typeof(T));

    /// <summary>
    /// Resolves a model, the connection's dialect, and the table the scope reads and writes the model's rows in.
    /// </summary>
    /// <param name="type">The model type.</param>
    /// <returns>The model, the dialect, and the quoted, qualified table.</returns>
    internal (DmlModel Model, DmlDialect Dialect, string Table) Target(Type type)
    {
        var model = DmlModel.For(type);
        var dialect = DmlDialect.For(Connection);
        var tableName = _state.TableNames.TryGetValue(type, out var substituted) ? substituted : model.Name;
        return (model, dialect, dialect.Table(SchemaFor(type, model), tableName));
    }

    // A shared model's schema is the layout's; another's is the scope's, the ambient tenant schema, or its own.
    private string? SchemaFor(Type type, DmlModel model)
    {
        if (DmSchemaLayout.IsShared(type))
        {
            return Connection.SupportsSchemas() ? DmSchemaLayout.SharedSchemaName ?? model.SchemaName : model.SchemaName;
        }

        if (SchemaName is not null)
        {
            return SchemaName;
        }

        if (DmDefaults.TenantSchema?.Invoke() is { } schema)
        {
            return Connection.SupportsSchemas()
                ? schema
                : throw new NotSupportedException(
                    $"{Connection.GetDbProviderType()} has no schemas, and DmDefaults.TenantSchema gives {schema}."
                );
        }

        return model.SchemaName;
    }

    private static string Render(SqlSelect select, DmlDialect dialect, DmlParameters parameters) =>
        new SqlRenderer(dialect, parameters).For(select).Render(select);

    private static List<SqlOrder> KeyOrder(SqlSource source) =>
        [.. source.Model!.Keys.Select(k => new SqlOrder(new SqlColumn(source, k.Column.ColumnName), Descending: false, Nullable: false))];

    // A SELECT of a model's rows that match a condition, joining the related rows the condition reads.
    private SqlSelect SelectOf(DmlModel model, DmlDialect dialect, string table, LambdaExpression? where, List<SqlSelectItem>? items = null)
    {
        var source = SourceOf(model, table);
        var scope = SqlScope.Select(dialect, TableOf, source, FiltersFor, HiddenFor);
        return new SqlSelect
        {
            From = source,
            Items = items ?? Items(source),
            Where = [.. scope.Filters(source), .. where is null ? [] : new[] { DmlTranslator.Condition(where, source, scope) }],
            Joins = scope.Joins,
        };
    }

    // The columns an insert writes: all but a generated key and a token the database writes.
    private static List<DmlColumn> Written(DmlModel model, DmlDialect dialect)
    {
        var token = model.Behaviours.DatabaseWritesToken(dialect) ? model.Behaviours.Token : null;
        return [.. model.Columns.Where(c => c != model.Generated && c != token)];
    }

    private static string InsertStatement(DmlModel model, DmlDialect dialect, string table, IReadOnlyList<DmlColumn>? written = null)
    {
        var columns = written ?? Written(model, dialect);
        return columns.Count == 0
            ? dialect.InsertDefaults(table)
            : $"INSERT INTO {table} ({string.Join(", ", columns.Select(c => dialect.Column(c.Column.ColumnName)))})"
                + $" VALUES ({string.Join(", ", columns.Select(c => "@" + c.ParameterName))})";
    }

    private static string KeyCondition(DmlModel model, DmlDialect dialect, bool byParameterName) =>
        string.Join(
            " AND ",
            model.Keys.Select((k, i) => $"{dialect.Column(k.Column.ColumnName)} = @{(byParameterName ? k.ParameterName : $"k{i}")}")
        );

    private static DmlParameters KeyParameters(DmlModel model, object key)
    {
        var parameters = new DmlParameters();
        for (var i = 0; i < model.Keys.Count; i++)
        {
            var column = model.Keys[i];
            var value = model.Keys.Count == 1 && IsKeyValue(key) ? key : KeyPart(key, column, i);
            parameters.Add($"k{i}", column.Property.PropertyType, ConvertTo(value, column.Property.PropertyType, column.Property.Name));
        }

        return parameters;
    }

    private static bool IsKeyValue(object key)
    {
        var type = key.GetType();
        return type.IsPrimitive
            || type.IsEnum
            || type == typeof(string)
            || type == typeof(decimal)
            || type == typeof(Guid)
            || type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(DateOnly)
            || type == typeof(TimeOnly)
            || type == typeof(TimeSpan);
    }

    private static object? KeyPart(object key, DmlColumn column, int index)
    {
        if (key is ITuple tuple)
        {
            return index < tuple.Length
                ? tuple[index]
                : throw new ArgumentException($"The key has {tuple.Length} values, and the key has more columns.", nameof(key));
        }

        var property = key.GetType()
            .GetProperty(column.Property.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        return property is not null
            ? property.GetValue(key)
            : throw new ArgumentException($"The key has no {column.Property.Name}.", nameof(key));
    }

    private static object ConvertTo(object? value, Type type, string name)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;
        return value switch
        {
            null or DBNull => throw new ArgumentException($"The value for {name} is null.", nameof(value)),
            _ when target.IsInstanceOfType(value) => value,
            string text when target.IsEnum => Enum.Parse(target, text, ignoreCase: true),
            _ when target.IsEnum => Enum.ToObject(target, value),
            string text when target == typeof(Guid) => Guid.Parse(text),
            _ => Convert.ChangeType(value, target, CultureInfo.InvariantCulture),
        };
    }

    private static string Statement(Type type, string operation, DmlDialect dialect, string table, Func<string> build) =>
        Statements.GetOrAdd((type, operation, dialect.ProviderType, table), _ => build());

    // A row passed as object is a row of its own type: object itself is not a model.
    private static Type ModelType<T>(T row)
        where T : class => typeof(T) == typeof(object) && row is not null ? row.GetType() : typeof(T);

    private async Task<TResult> InTransactionAsync<TResult>(
        IDbTransaction? tx,
        bool needsOne,
        Func<IDbTransaction?, Task<TResult>> work
    )
    {
        if (tx is not null || !needsOne)
        {
            return await work(tx).ConfigureAwait(false);
        }

        var opened = false;
        if (Connection.State != ConnectionState.Open)
        {
            if (Connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync().ConfigureAwait(false);
            }
            else
            {
                Connection.Open();
            }

            opened = true;
        }

        try
        {
            using var own = Connection.BeginTransaction();
            var result = await work(own).ConfigureAwait(false);
            own.Commit();
            return result;
        }
        finally
        {
            if (opened)
            {
                Connection.Close();
            }
        }
    }
}
