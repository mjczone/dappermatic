// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.Dml;

/// <summary>
/// Reads and writes the rows of model tables: the classes DapperMatic builds tables from.
/// </summary>
/// <remarks>
/// Every value is a parameter, and table and column names come only from the model, quoted the way DDL names them.
/// Each method is the same as on a <see cref="DmScope"/> with no substitutions; <see cref="InSchema"/> and
/// <see cref="WithTableName"/> make a scope that substitutes a schema or a table.
/// </remarks>
public static class DmlConnectionExtensions
{
    /// <summary>
    /// Runs statements on the connection as a caller, held to the policies as the web API holds requests: see
    /// <see cref="DmScope.AsCallerAsync(DmAccessContext, IEnumerable{DmPolicy}, string, CancellationToken)"/>.
    /// </summary>
    /// <param name="db">The connection.</param>
    /// <param name="access">The caller: who they are, their tenant and values.</param>
    /// <param name="policies">The policies, such as those of the web API.</param>
    /// <param name="datasourceId">The datasource the policies know the connection by.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A scope run as the caller.</returns>
    public static Task<DmScope> AsCallerAsync(
        this IDbConnection db,
        DmAccessContext access,
        IEnumerable<DmPolicy> policies,
        string datasourceId,
        CancellationToken cancellationToken = default
    ) => new DmScope(db).AsCallerAsync(access, policies, datasourceId, cancellationToken);

    /// <summary>
    /// Uses a schema for every model, such as a tenant's, instead of each model's own.
    /// </summary>
    /// <param name="db">The connection.</param>
    /// <param name="schemaName">The schema.</param>
    /// <returns>A scope using it, with the same methods as the connection.</returns>
    /// <exception cref="NotSupportedException">The provider has no schemas.</exception>
    public static DmScope InSchema(this IDbConnection db, string schemaName) => new DmScope(db).InSchema(schemaName);

    /// <summary>
    /// Uses a table for a model, such as an archive table, instead of the model's own.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="db">The connection.</param>
    /// <param name="tableName">The table.</param>
    /// <returns>A scope using it, with the same methods as the connection.</returns>
    public static DmScope WithTableName<T>(this IDbConnection db, string tableName) =>
        new DmScope(db).WithTableName<T>(tableName);

    /// <summary>
    /// Starts a query of a model's rows, refined with <c>Where</c>, <c>OrderBy</c>, <c>Skip</c>, <c>Take</c> and
    /// <c>Select</c>.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="db">The connection.</param>
    /// <returns>A query of every row, which runs only when one of its <c>...Async</c> methods is called.</returns>
    public static DmQuery<T> From<T>(this IDbConnection db)
        where T : class => new DmScope(db).From<T>();

    /// <summary>
    /// Inserts a row.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="db">The connection.</param>
    /// <param name="row">The row.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The row's key: the value the database generated for an auto-increment key, which is also set on the row; the
    /// key the row carries otherwise; or null for a composite key or no key.
    /// </returns>
    public static Task<object?> InsertAsync<T>(
        this IDbConnection db,
        T row,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class => new DmScope(db).InsertAsync(row, tx, cancellationToken);

    /// <summary>
    /// Inserts rows, several to a statement, all or none.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="db">The connection.</param>
    /// <param name="rows">The rows.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows inserted.</returns>
    public static Task<int> InsertManyAsync<T>(
        this IDbConnection db,
        IEnumerable<T> rows,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class => new DmScope(db).InsertManyAsync(rows, tx, cancellationToken);

    /// <summary>
    /// Updates every column of a row but its key, by its key.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="db">The connection.</param>
    /// <param name="row">The row.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows updated: 1, or 0 if no row has the key.</returns>
    public static Task<int> UpdateAsync<T>(
        this IDbConnection db,
        T row,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class => new DmScope(db).UpdateAsync(row, tx, cancellationToken);

    /// <summary>
    /// Deletes a row, by its key.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="db">The connection.</param>
    /// <param name="row">The row.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows deleted: 1, or 0 if no row has the key.</returns>
    public static Task<int> DeleteAsync<T>(
        this IDbConnection db,
        T row,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class => new DmScope(db).DeleteAsync(row, tx, cancellationToken);

    /// <summary>
    /// Deletes the row with a key.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="db">The connection.</param>
    /// <param name="key">The key: its value, or for a composite key an object or tuple holding each key property.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows deleted: 1, or 0 if no row has the key.</returns>
    public static Task<int> DeleteByIdAsync<T>(
        this IDbConnection db,
        object key,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class => new DmScope(db).DeleteByIdAsync<T>(key, tx, cancellationToken);

    /// <summary>
    /// Reads the row with a key.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="db">The connection.</param>
    /// <param name="key">The key: its value, or for a composite key an object or tuple holding each key property.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The row, or null if there is none.</returns>
    public static Task<T?> SingleByIdAsync<T>(
        this IDbConnection db,
        object key,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class => new DmScope(db).SingleByIdAsync<T>(key, tx, cancellationToken);

    /// <summary>
    /// Reads the rows with any of the given keys, in no particular order.
    /// </summary>
    /// <typeparam name="T">The model type, which must have a single-column key.</typeparam>
    /// <param name="db">The connection.</param>
    /// <param name="keys">The keys.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rows found.</returns>
    public static Task<List<T>> SelectByIdsAsync<T>(
        this IDbConnection db,
        IEnumerable<object> keys,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class => new DmScope(db).SelectByIdsAsync<T>(keys, tx, cancellationToken);

    /// <summary>
    /// Reads the rows that match a condition, or every row.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="db">The connection.</param>
    /// <param name="where">The condition, such as <c>o =&gt; o.Status == OrderStatus.Open</c>, or null for every row.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rows, in no particular order.</returns>
    public static Task<List<T>> SelectAsync<T>(
        this IDbConnection db,
        Expression<Func<T, bool>>? where = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class => new DmScope(db).SelectAsync(where, tx, cancellationToken);

    /// <summary>
    /// Reads the one row that matches a condition.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="db">The connection.</param>
    /// <param name="where">The condition.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The row, or null if no row matches.</returns>
    /// <exception cref="InvalidOperationException">More than one row matches.</exception>
    public static Task<T?> SingleAsync<T>(
        this IDbConnection db,
        Expression<Func<T, bool>> where,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class => new DmScope(db).SingleAsync(where, tx, cancellationToken);

    /// <summary>
    /// Reads the first row that matches a condition, in key order.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="db">The connection.</param>
    /// <param name="where">The condition, or null for any row.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The row with the lowest key that matches, any matching row for a model with no key, or null.</returns>
    public static Task<T?> FirstAsync<T>(
        this IDbConnection db,
        Expression<Func<T, bool>>? where = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class => new DmScope(db).FirstAsync(where, tx, cancellationToken);

    /// <summary>
    /// Counts the rows that match a condition, or every row.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="db">The connection.</param>
    /// <param name="where">The condition, or null for every row.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows.</returns>
    public static Task<long> CountAsync<T>(
        this IDbConnection db,
        Expression<Func<T, bool>>? where = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class => new DmScope(db).CountAsync(where, tx, cancellationToken);

    /// <summary>
    /// Tells whether any row matches a condition, or whether the table has any rows.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="db">The connection.</param>
    /// <param name="where">The condition, or null for any row.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if a row matches.</returns>
    public static Task<bool> ExistsAsync<T>(
        this IDbConnection db,
        Expression<Func<T, bool>>? where = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class => new DmScope(db).ExistsAsync(where, tx, cancellationToken);

    /// <summary>
    /// Sets columns of the rows that match a condition.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="db">The connection.</param>
    /// <param name="set">
    /// The columns to set and their values, as an object initializer: <c>o =&gt; new Order { Status = s, Note = null }</c>.
    /// </param>
    /// <param name="where">The condition. To update every row, pass <c>o =&gt; true</c>.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows updated.</returns>
    public static Task<int> UpdateAsync<T>(
        this IDbConnection db,
        Expression<Func<T, T>> set,
        Expression<Func<T, bool>> where,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class => new DmScope(db).UpdateAsync(set, where, tx, cancellationToken);

    /// <summary>
    /// Deletes the rows that match a condition.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="db">The connection.</param>
    /// <param name="where">The condition. To delete every row, use <see cref="DeleteAllAsync{T}"/>.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows deleted.</returns>
    public static Task<int> DeleteAsync<T>(
        this IDbConnection db,
        Expression<Func<T, bool>> where,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class => new DmScope(db).DeleteAsync(where, tx, cancellationToken);

    /// <summary>
    /// Deletes every row.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="db">The connection.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows deleted.</returns>
    public static Task<int> DeleteAllAsync<T>(
        this IDbConnection db,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class => new DmScope(db).DeleteAllAsync<T>(tx, cancellationToken);

    /// <summary>
    /// Uses a table for a model, such as an archive table, instead of the model's own.
    /// </summary>
    /// <param name="db">The connection.</param>
    /// <param name="type">The model type.</param>
    /// <param name="tableName">The table.</param>
    /// <returns>A scope using it, with the same methods as the connection.</returns>
    public static DmScope WithTableName(
        this IDbConnection db,
        Type type,
        string tableName
    ) => new DmScope(db).WithTableName(type, tableName);

    /// <summary>
    /// Inserts a row.
    /// </summary>
    /// <param name="db">The connection.</param>
    /// <param name="type">The model type.</param>
    /// <param name="row">The row.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The row's key: the value the database generated for an auto-increment key, which is also set on the row; the key the row carries otherwise; or null for a composite key or no key.</returns>
    public static Task<object?> InsertAsync(
        this IDbConnection db,
        Type type,
        object row,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => new DmScope(db).InsertAsync(type, row, tx, cancellationToken);

    /// <summary>
    /// Inserts rows, several to a statement, all or none.
    /// </summary>
    /// <param name="db">The connection.</param>
    /// <param name="type">The model type.</param>
    /// <param name="rows">The rows.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows inserted.</returns>
    public static Task<int> InsertManyAsync(
        this IDbConnection db,
        Type type,
        IEnumerable<object> rows,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => new DmScope(db).InsertManyAsync(type, rows, tx, cancellationToken);

    /// <summary>
    /// Updates every column of a row but its key, by its key.
    /// </summary>
    /// <param name="db">The connection.</param>
    /// <param name="type">The model type.</param>
    /// <param name="row">The row.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows updated: 1, or 0 if no row has the key.</returns>
    public static Task<int> UpdateAsync(
        this IDbConnection db,
        Type type,
        object row,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => new DmScope(db).UpdateAsync(type, row, tx, cancellationToken);

    /// <summary>
    /// Deletes a row, by its key.
    /// </summary>
    /// <param name="db">The connection.</param>
    /// <param name="type">The model type.</param>
    /// <param name="row">The row.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows deleted: 1, or 0 if no row has the key.</returns>
    public static Task<int> DeleteAsync(
        this IDbConnection db,
        Type type,
        object row,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => new DmScope(db).DeleteAsync(type, row, tx, cancellationToken);

    /// <summary>
    /// Deletes the row with a key.
    /// </summary>
    /// <param name="db">The connection.</param>
    /// <param name="type">The model type.</param>
    /// <param name="key">The key: its value, or for a composite key an object or tuple holding each key property.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows deleted: 1, or 0 if no row has the key.</returns>
    public static Task<int> DeleteByIdAsync(
        this IDbConnection db,
        Type type,
        object key,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => new DmScope(db).DeleteByIdAsync(type, key, tx, cancellationToken);

    /// <summary>
    /// Reads the row with a key.
    /// </summary>
    /// <param name="db">The connection.</param>
    /// <param name="type">The model type.</param>
    /// <param name="key">The key: its value, or for a composite key an object or tuple holding each key property.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The row, or null if there is none.</returns>
    public static Task<object?> SingleByIdAsync(
        this IDbConnection db,
        Type type,
        object key,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => new DmScope(db).SingleByIdAsync(type, key, tx, cancellationToken);

    /// <summary>
    /// Reads the rows with any of the given keys, in no particular order.
    /// </summary>
    /// <param name="db">The connection.</param>
    /// <param name="type">The model type.</param>
    /// <param name="keys">The keys.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rows found.</returns>
    public static Task<List<object>> SelectByIdsAsync(
        this IDbConnection db,
        Type type,
        IEnumerable<object> keys,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => new DmScope(db).SelectByIdsAsync(type, keys, tx, cancellationToken);

    /// <summary>
    /// Reads every row of a model.
    /// </summary>
    /// <param name="db">The connection.</param>
    /// <param name="type">The model type.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rows, in no particular order.</returns>
    public static Task<List<object>> SelectAsync(
        this IDbConnection db,
        Type type,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => new DmScope(db).SelectAsync(type, tx, cancellationToken);

    /// <summary>
    /// Reads the row of a model with the lowest key.
    /// </summary>
    /// <param name="db">The connection.</param>
    /// <param name="type">The model type.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The row with the lowest key, any row for a model with no key, or null if there are none.</returns>
    public static Task<object?> FirstAsync(
        this IDbConnection db,
        Type type,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => new DmScope(db).FirstAsync(type, tx, cancellationToken);

    /// <summary>
    /// Counts the rows of a model.
    /// </summary>
    /// <param name="db">The connection.</param>
    /// <param name="type">The model type.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows.</returns>
    public static Task<long> CountAsync(
        this IDbConnection db,
        Type type,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => new DmScope(db).CountAsync(type, tx, cancellationToken);

    /// <summary>
    /// Tells whether a model's table has any rows.
    /// </summary>
    /// <param name="db">The connection.</param>
    /// <param name="type">The model type.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if it does.</returns>
    public static Task<bool> ExistsAsync(
        this IDbConnection db,
        Type type,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => new DmScope(db).ExistsAsync(type, tx, cancellationToken);

    /// <summary>
    /// Deletes every row.
    /// </summary>
    /// <param name="db">The connection.</param>
    /// <param name="type">The model type.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows deleted.</returns>
    public static Task<int> DeleteAllAsync(
        this IDbConnection db,
        Type type,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => new DmScope(db).DeleteAllAsync(type, tx, cancellationToken);

    /// <summary>
    /// Sets the columns a patch has values for, leaving the rest unchanged.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="db">The connection.</param>
    /// <param name="patch">
    /// Any object whose properties are named as the model's are: a null property leaves its column unchanged, and a
    /// property the model has no column for is an error.
    /// </param>
    /// <param name="where">The rows to patch; or null for the row with the key the patch holds.</param>
    /// <param name="setNull">Columns to set to NULL: <c>o =&gt; new { o.Note, o.Discount }</c>.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows patched; 0 without a round trip if the patch sets nothing.</returns>
    public static Task<int> PatchAsync<T>(
        this IDbConnection db,
        object patch,
        Expression<Func<T, bool>>? where = null,
        Expression<Func<T, object?>>? setNull = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class => new DmScope(db).PatchAsync(patch, where, setNull, tx, cancellationToken);

    /// <summary>
    /// Sets the columns a patch has values for in the row with the key the patch holds, leaving the rest unchanged.
    /// </summary>
    /// <param name="db">The connection.</param>
    /// <param name="type">The model type.</param>
    /// <param name="patch">Any object whose properties are named as the model's are.</param>
    /// <param name="setNull">The names of the properties whose columns to set to NULL, or null.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows patched: 1, or 0 if no row has the key or the patch sets nothing.</returns>
    public static Task<int> PatchAsync(
        this IDbConnection db,
        Type type,
        object patch,
        IEnumerable<string>? setNull = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => new DmScope(db).PatchAsync(type, patch, setNull, tx, cancellationToken);

    /// <summary>
    /// Inserts a row, or updates every column of the row with its key if there is one.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="db">The connection.</param>
    /// <param name="row">The row.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The row's key, as InsertAsync returns it.</returns>
    public static Task<object?> UpsertAsync<T>(
        this IDbConnection db,
        T row,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class => new DmScope(db).UpsertAsync(row, tx, cancellationToken);

    /// <summary>
    /// Inserts a row, or updates every column of the row with its key if there is one.
    /// </summary>
    /// <param name="db">The connection.</param>
    /// <param name="type">The model type.</param>
    /// <param name="row">The row.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The row's key, as InsertAsync returns it.</returns>
    public static Task<object?> UpsertAsync(
        this IDbConnection db,
        Type type,
        object row,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => new DmScope(db).UpsertAsync(type, row, tx, cancellationToken);

    /// <summary>
    /// Inserts a row, or updates the row it conflicts with as the options say.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="db">The connection.</param>
    /// <param name="row">The row.</param>
    /// <param name="options">The conflict columns, what to set, and when.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The row's key: for a key the database generates, the inserted or conflicting row's, also set on the row.</returns>
    [OverloadResolutionPriority(1)] // `new() { ... }` also converts to the transaction: this overload is the one meant.
    public static Task<object?> UpsertAsync<T>(
        this IDbConnection db,
        T row,
        DmUpsertOptions<T> options,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class => new DmScope(db).UpsertAsync(row, options, tx, cancellationToken);

    /// <summary>
    /// Inserts a row unless a row with its primary key exists.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="db">The connection.</param>
    /// <param name="row">The row.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the row was inserted; a key the database generated is then set on it.</returns>
    public static Task<bool> InsertIfAbsentAsync<T>(
        this IDbConnection db,
        T row,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class => new DmScope(db).InsertIfAbsentAsync(row, tx, cancellationToken);

    /// <summary>
    /// Inserts a row unless a row with the same values in some columns exists.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="db">The connection.</param>
    /// <param name="row">The row.</param>
    /// <param name="onConflict">The columns: the primary key or the columns of a unique constraint or index.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the row was inserted; a key the database generated is then set on it.</returns>
    public static Task<bool> InsertIfAbsentAsync<T>(
        this IDbConnection db,
        T row,
        Expression<Func<T, object?>> onConflict,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class => new DmScope(db).InsertIfAbsentAsync(row, onConflict, tx, cancellationToken);

    /// <summary>
    /// Inserts a row unless a row with its primary key exists.
    /// </summary>
    /// <param name="db">The connection.</param>
    /// <param name="type">The model type.</param>
    /// <param name="row">The row.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the row was inserted; a key the database generated is then set on it.</returns>
    public static Task<bool> InsertIfAbsentAsync(
        this IDbConnection db,
        Type type,
        object row,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => new DmScope(db).InsertIfAbsentAsync(type, row, tx, cancellationToken);

    /// <summary>
    /// Inserts rows in bulk, all or none, through the provider's native bulk path, or multi-row VALUES without one.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="db">The connection.</param>
    /// <param name="rows">The rows, read once, as they are loaded.</param>
    /// <param name="tx">The transaction to use, or null for one of the method's own.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows inserted.</returns>
    public static Task<long> BulkInsertAsync<T>(
        this IDbConnection db,
        IEnumerable<T> rows,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class => new DmScope(db).BulkInsertAsync(rows, tx, cancellationToken);

    /// <summary>
    /// Inserts rows in bulk, all or none, with options.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="db">The connection.</param>
    /// <param name="rows">The rows, read once, as they are loaded.</param>
    /// <param name="options">The batch size, the timeout, and what to do without a native path.</param>
    /// <param name="tx">The transaction to use, or null for one of the method's own.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows inserted.</returns>
    [OverloadResolutionPriority(1)] // `new() { ... }` also converts to the transaction: this overload is the one meant.
    public static Task<long> BulkInsertAsync<T>(
        this IDbConnection db,
        IEnumerable<T> rows,
        DmBulkOptions options,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class => new DmScope(db).BulkInsertAsync(rows, options, tx, cancellationToken);

    /// <summary>
    /// Inserts rows of a model known at run time in bulk, all or none.
    /// </summary>
    /// <param name="db">The connection.</param>
    /// <param name="type">The model type.</param>
    /// <param name="rows">The rows, read once, as they are loaded.</param>
    /// <param name="tx">The transaction to use, or null for one of the method's own.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows inserted.</returns>
    public static Task<long> BulkInsertAsync(
        this IDbConnection db,
        Type type,
        IEnumerable<object> rows,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => new DmScope(db).BulkInsertAsync(type, rows, tx, cancellationToken);

    /// <summary>
    /// Inserts rows of a model known at run time in bulk, all or none, with options.
    /// </summary>
    /// <param name="db">The connection.</param>
    /// <param name="type">The model type.</param>
    /// <param name="rows">The rows, read once, as they are loaded.</param>
    /// <param name="options">The batch size, the timeout, and what to do without a native path.</param>
    /// <param name="tx">The transaction to use, or null for one of the method's own.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows inserted.</returns>
    [OverloadResolutionPriority(1)] // `new() { ... }` also converts to the transaction: this overload is the one meant.
    public static Task<long> BulkInsertAsync(
        this IDbConnection db,
        Type type,
        IEnumerable<object> rows,
        DmBulkOptions options,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => new DmScope(db).BulkInsertAsync(type, rows, options, tx, cancellationToken);

    /// <summary>
    /// Inserts rows in bulk, or updates the rows with their primary keys, all or none, through a staging table.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="db">The connection.</param>
    /// <param name="rows">The rows, read once; no two may have the same key.</param>
    /// <param name="tx">The transaction to use, or null for one of the method's own.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows loaded.</returns>
    public static Task<long> BulkUpsertAsync<T>(
        this IDbConnection db,
        IEnumerable<T> rows,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class => new DmScope(db).BulkUpsertAsync(rows, tx, cancellationToken);

    /// <summary>
    /// Inserts rows in bulk, or updates the rows they conflict with as the options say, all or none.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="db">The connection.</param>
    /// <param name="rows">The rows, read once; no two may conflict with each other.</param>
    /// <param name="upsert">The conflict columns, what to set, and when.</param>
    /// <param name="bulk">The batch size, the timeout, and what to do without a native path; null for the defaults.</param>
    /// <param name="tx">The transaction to use, or null for one of the method's own.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows loaded.</returns>
    [OverloadResolutionPriority(1)] // `new() { ... }` also converts to the transaction: this overload is the one meant.
    public static Task<long> BulkUpsertAsync<T>(
        this IDbConnection db,
        IEnumerable<T> rows,
        DmUpsertOptions<T> upsert,
        DmBulkOptions? bulk = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class => new DmScope(db).BulkUpsertAsync(rows, upsert, bulk, tx, cancellationToken);

    /// <summary>
    /// Inserts rows of a model known at run time in bulk, or updates the rows with their primary keys, all or none.
    /// </summary>
    /// <param name="db">The connection.</param>
    /// <param name="type">The model type.</param>
    /// <param name="rows">The rows, read once; no two may have the same key.</param>
    /// <param name="tx">The transaction to use, or null for one of the method's own.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows loaded.</returns>
    public static Task<long> BulkUpsertAsync(
        this IDbConnection db,
        Type type,
        IEnumerable<object> rows,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => new DmScope(db).BulkUpsertAsync(type, rows, tx, cancellationToken);

    /// <summary>
    /// Loads relations onto rows already read, by one query per relation and batch of the rows' keys.
    /// </summary>
    /// <typeparam name="T">The rows' model.</typeparam>
    /// <param name="db">The connection.</param>
    /// <param name="rows">The rows.</param>
    /// <param name="include">The relations to load, as for a query's <c>Include</c>.</param>
    /// <param name="reload">Whether to load a relation onto a row that has it already.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public static Task LoadAsync<T>(
        this IDbConnection db,
        IEnumerable<T> rows,
        Func<DmInclude<T>, DmInclude<T>> include,
        bool reload = false,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class => new DmScope(db).LoadAsync(rows, include, reload, tx, cancellationToken);

    /// <summary>
    /// Reads and writes only a tenant's rows of models with a <c>[DmTenantKey]</c>, and sets it on rows inserted.
    /// </summary>
    /// <param name="db">The connection.</param>
    /// <param name="tenantId">The tenant's id.</param>
    /// <returns>A scope for the tenant, with the same methods as the connection.</returns>
    public static DmScope ForTenant(this IDbConnection db, object tenantId) => new DmScope(db).ForTenant(tenantId);

    /// <summary>
    /// Reads and writes every tenant's rows of models with a <c>[DmTenantKey]</c>.
    /// </summary>
    /// <param name="db">The connection.</param>
    /// <returns>A scope for every tenant.</returns>
    public static DmScope AllTenants(this IDbConnection db) => new DmScope(db).AllTenants();

    /// <summary>
    /// Sets the user <c>[DmCreatedBy]</c> and <c>[DmUpdatedBy]</c> record.
    /// </summary>
    /// <param name="db">The connection.</param>
    /// <param name="user">The user, or null for none.</param>
    /// <returns>A scope for the user.</returns>
    public static DmScope WithUser(this IDbConnection db, object? user) => new DmScope(db).WithUser(user);

    /// <summary>
    /// Reads deleted rows of models with a <c>[DmSoftDelete]</c> too.
    /// </summary>
    /// <param name="db">The connection.</param>
    /// <returns>A scope that sees deleted rows.</returns>
    public static DmScope IncludeDeleted(this IDbConnection db) => new DmScope(db).IncludeDeleted();

    /// <summary>
    /// Reads only the deleted rows of the models a statement reads.
    /// </summary>
    /// <param name="db">The connection.</param>
    /// <returns>A scope that sees only deleted rows.</returns>
    public static DmScope OnlyDeleted(this IDbConnection db) => new DmScope(db).OnlyDeleted();

    /// <summary>
    /// Removes a row by its key, even of a model with a <c>[DmSoftDelete]</c>.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="db">The connection.</param>
    /// <param name="row">The row.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows removed.</returns>
    public static Task<int> HardDeleteAsync<T>(this IDbConnection db, T row, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
        where T : class => new DmScope(db).HardDeleteAsync(row, tx, cancellationToken);

    /// <summary>
    /// Removes a row of a model known at run time by its key, even of a model with a <c>[DmSoftDelete]</c>.
    /// </summary>
    /// <param name="db">The connection.</param>
    /// <param name="type">The model type.</param>
    /// <param name="row">The row.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows removed.</returns>
    public static Task<int> HardDeleteAsync(this IDbConnection db, Type type, object row, IDbTransaction? tx = null, CancellationToken cancellationToken = default) =>
        new DmScope(db).HardDeleteAsync(type, row, tx, cancellationToken);

    /// <summary>
    /// Removes the row with a key, even of a model with a <c>[DmSoftDelete]</c>.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="db">The connection.</param>
    /// <param name="key">The key, as <c>DeleteByIdAsync</c> takes it.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows removed.</returns>
    public static Task<int> HardDeleteByIdAsync<T>(this IDbConnection db, object key, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
        where T : class => new DmScope(db).HardDeleteByIdAsync<T>(key, tx, cancellationToken);

    /// <summary>
    /// Removes the row of a model known at run time with a key, even of a model with a <c>[DmSoftDelete]</c>.
    /// </summary>
    /// <param name="db">The connection.</param>
    /// <param name="type">The model type.</param>
    /// <param name="key">The key.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows removed.</returns>
    public static Task<int> HardDeleteByIdAsync(this IDbConnection db, Type type, object key, IDbTransaction? tx = null, CancellationToken cancellationToken = default) =>
        new DmScope(db).HardDeleteByIdAsync(type, key, tx, cancellationToken);

    /// <summary>
    /// Removes the rows that match a condition, deleted or not.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="db">The connection.</param>
    /// <param name="where">The condition.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows removed.</returns>
    public static Task<int> HardDeleteAsync<T>(
        this IDbConnection db,
        Expression<Func<T, bool>> where,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
        where T : class => new DmScope(db).HardDeleteAsync(where, tx, cancellationToken);

    /// <summary>
    /// Removes every row, deleted or not.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="db">The connection.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows removed.</returns>
    public static Task<int> HardDeleteAllAsync<T>(this IDbConnection db, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
        where T : class => new DmScope(db).HardDeleteAllAsync<T>(tx, cancellationToken);

    /// <summary>
    /// Removes every row of a model known at run time, deleted or not.
    /// </summary>
    /// <param name="db">The connection.</param>
    /// <param name="type">The model type.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows removed.</returns>
    public static Task<int> HardDeleteAllAsync(this IDbConnection db, Type type, IDbTransaction? tx = null, CancellationToken cancellationToken = default) =>
        new DmScope(db).HardDeleteAllAsync(type, tx, cancellationToken);
}
