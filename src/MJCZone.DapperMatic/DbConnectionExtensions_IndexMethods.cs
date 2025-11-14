// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Linq.Expressions;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic;

public static partial class DbConnectionExtensions
{
    #region IDatabaseIndexMethods

    /// <summary>
    /// Checks if an index exists on a specified column.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the index exists, otherwise false.</returns>
    public static async Task<bool> DoesIndexExistOnColumnAsync<T>(
        this IDbConnection db,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var (schemaName, tableName) = DmTableFactory.GetTableName(typeof(T));
        return await Database(db)
            .DoesIndexExistOnColumnAsync(db, schemaName, tableName, columnName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Checks if an index exists on a specified column.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="columnExpression">An expression representing the column.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the index exists, otherwise false.</returns>
    public static async Task<bool> DoesIndexExistOnColumnAsync<T>(
        this IDbConnection db,
        Expression<Func<T, object>> columnExpression,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var (schemaName, tableName) = DmTableFactory.GetTableName(typeof(T));
        return await Database(db)
            .DoesIndexExistOnColumnAsync(
                db,
                schemaName,
                tableName,
                DmTableFactory.GetColumnName(columnExpression),
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Checks if an index exists on a specified column.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the index exists, otherwise false.</returns>
    public static async Task<bool> DoesIndexExistOnColumnAsync(
        this IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .DoesIndexExistOnColumnAsync(db, schemaName, tableName, columnName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Checks if an index exists.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="indexName">The index name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the index exists, otherwise false.</returns>
    public static async Task<bool> DoesIndexExistAsync<T>(
        this IDbConnection db,
        string indexName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var (schemaName, tableName) = DmTableFactory.GetTableName(typeof(T));
        return await Database(db)
            .DoesIndexExistAsync(db, schemaName, tableName, indexName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Checks if an index exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="indexName">The index name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the index exists, otherwise false.</returns>
    public static async Task<bool> DoesIndexExistAsync(
        this IDbConnection db,
        string? schemaName,
        string tableName,
        string indexName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .DoesIndexExistAsync(db, schemaName, tableName, indexName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Creates an index if it does not exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="index">The index constraint.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the index was created, otherwise false.</returns>
    public static async Task<bool> CreateIndexIfNotExistsAsync(
        this IDbConnection db,
        DmIndex index,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db).CreateIndexIfNotExistsAsync(db, index, tx, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates an index if it does not exist.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="indexName">The index name.</param>
    /// <param name="columns">The columns in the index.</param>
    /// <param name="isUnique">Whether the index is unique.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the index was created, otherwise false.</returns>
    public static async Task<bool> CreateIndexIfNotExistsAsync<T>(
        this IDbConnection db,
        string indexName,
        DmOrderedColumn[] columns,
        bool isUnique = false,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var (schemaName, tableName) = DmTableFactory.GetTableName(typeof(T));
        return await Database(db)
            .CreateIndexIfNotExistsAsync(db, schemaName, tableName, indexName, columns, isUnique, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Creates an index if it does not exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="indexName">The index name.</param>
    /// <param name="columns">The columns in the index.</param>
    /// <param name="isUnique">Whether the index is unique.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the index was created, otherwise false.</returns>
    public static async Task<bool> CreateIndexIfNotExistsAsync(
        this IDbConnection db,
        string? schemaName,
        string tableName,
        string indexName,
        DmOrderedColumn[] columns,
        bool isUnique = false,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .CreateIndexIfNotExistsAsync(db, schemaName, tableName, indexName, columns, isUnique, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the indexes on a specified column.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of indexes on the column.</returns>
    public static async Task<List<DmIndex>> GetIndexesOnColumnAsync<T>(
        this IDbConnection db,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var (schemaName, tableName) = DmTableFactory.GetTableName(typeof(T));
        return await Database(db)
            .GetIndexesOnColumnAsync(db, schemaName, tableName, columnName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the indexes on a specified column.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="columnExpression">An expression representing the column.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of indexes on the column.</returns>
    public static async Task<List<DmIndex>> GetIndexesOnColumnAsync<T>(
        this IDbConnection db,
        Expression<Func<T, object>> columnExpression,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var (schemaName, tableName) = DmTableFactory.GetTableName(typeof(T));
        return await Database(db)
            .GetIndexesOnColumnAsync(
                db,
                schemaName,
                tableName,
                DmTableFactory.GetColumnName(columnExpression),
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the indexes on a specified column.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of indexes on the column.</returns>
    public static async Task<List<DmIndex>> GetIndexesOnColumnAsync(
        this IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .GetIndexesOnColumnAsync(db, schemaName, tableName, columnName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a specified index.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="indexName">The index name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The index if found, otherwise null.</returns>
    public static async Task<DmIndex?> GetIndexAsync<T>(
        this IDbConnection db,
        string indexName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var (schemaName, tableName) = DmTableFactory.GetTableName(typeof(T));
        return await Database(db)
            .GetIndexAsync(db, schemaName, tableName, indexName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a specified index.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="indexName">The index name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The index if found, otherwise null.</returns>
    public static async Task<DmIndex?> GetIndexAsync(
        this IDbConnection db,
        string? schemaName,
        string tableName,
        string indexName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .GetIndexAsync(db, schemaName, tableName, indexName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the indexes on a specified table.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="indexNameFilter">The index name filter.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of indexes on the table.</returns>
    public static async Task<List<DmIndex>> GetIndexesAsync<T>(
        this IDbConnection db,
        string? indexNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var (schemaName, tableName) = DmTableFactory.GetTableName(typeof(T));
        return await Database(db)
            .GetIndexesAsync(db, schemaName, tableName, indexNameFilter, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the indexes on a specified table.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="indexNameFilter">The index name filter.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of indexes on the table.</returns>
    public static async Task<List<DmIndex>> GetIndexesAsync(
        this IDbConnection db,
        string? schemaName,
        string tableName,
        string? indexNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .GetIndexesAsync(db, schemaName, tableName, indexNameFilter, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the names of indexes on a specified column.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of index names on the column.</returns>
    public static async Task<List<string>> GetIndexNamesOnColumnAsync<T>(
        this IDbConnection db,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var (schemaName, tableName) = DmTableFactory.GetTableName(typeof(T));
        return await Database(db)
            .GetIndexNamesOnColumnAsync(db, schemaName, tableName, columnName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the names of indexes on a specified column.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="columnExpression">An expression representing the column.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of index names on the column.</returns>
    public static async Task<List<string>> GetIndexNamesOnColumnAsync<T>(
        this IDbConnection db,
        Expression<Func<T, object>> columnExpression,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var (schemaName, tableName) = DmTableFactory.GetTableName(typeof(T));
        return await Database(db)
            .GetIndexNamesOnColumnAsync(
                db,
                schemaName,
                tableName,
                DmTableFactory.GetColumnName(columnExpression),
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the names of indexes on a specified column.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of index names on the column.</returns>
    public static async Task<List<string>> GetIndexNamesOnColumnAsync(
        this IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .GetIndexNamesOnColumnAsync(db, schemaName, tableName, columnName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the names of indexes on a specified table.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="indexNameFilter">The index name filter.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of index names on the table.</returns>
    public static async Task<List<string>> GetIndexNamesAsync<T>(
        this IDbConnection db,
        string? indexNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var (schemaName, tableName) = DmTableFactory.GetTableName(typeof(T));
        return await Database(db)
            .GetIndexNamesAsync(db, schemaName, tableName, indexNameFilter, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the names of indexes on a specified table.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="indexNameFilter">The index name filter.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of index names on the table.</returns>
    public static async Task<List<string>> GetIndexNamesAsync(
        this IDbConnection db,
        string? schemaName,
        string tableName,
        string? indexNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .GetIndexNamesAsync(db, schemaName, tableName, indexNameFilter, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Drops indexes on a specified column if they exist.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the indexes were dropped, otherwise false.</returns>
    public static async Task<bool> DropIndexesOnColumnIfExistsAsync<T>(
        this IDbConnection db,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var (schemaName, tableName) = DmTableFactory.GetTableName(typeof(T));
        return await Database(db)
            .DropIndexesOnColumnIfExistsAsync(db, schemaName, tableName, columnName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Drops indexes on a specified column if they exist.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="columnExpression">An expression representing the column.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the indexes were dropped, otherwise false.</returns>
    public static async Task<bool> DropIndexesOnColumnIfExistsAsync<T>(
        this IDbConnection db,
        Expression<Func<T, object>> columnExpression,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var (schemaName, tableName) = DmTableFactory.GetTableName(typeof(T));
        return await Database(db)
            .DropIndexesOnColumnIfExistsAsync(
                db,
                schemaName,
                tableName,
                DmTableFactory.GetColumnName(columnExpression),
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Drops indexes on a specified column if they exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the indexes were dropped, otherwise false.</returns>
    public static async Task<bool> DropIndexesOnColumnIfExistsAsync(
        this IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .DropIndexesOnColumnIfExistsAsync(db, schemaName, tableName, columnName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Drops a specified index if it exists.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="indexName">The index name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the index was dropped, otherwise false.</returns>
    public static async Task<bool> DropIndexIfExistsAsync<T>(
        this IDbConnection db,
        string indexName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var (schemaName, tableName) = DmTableFactory.GetTableName(typeof(T));
        return await Database(db)
            .DropIndexIfExistsAsync(db, schemaName, tableName, indexName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Drops a specified index if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="indexName">The index name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the index was dropped, otherwise false.</returns>
    public static async Task<bool> DropIndexIfExistsAsync(
        this IDbConnection db,
        string? schemaName,
        string tableName,
        string indexName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .DropIndexIfExistsAsync(db, schemaName, tableName, indexName, tx, cancellationToken)
            .ConfigureAwait(false);
    }
    #endregion // IDatabaseIndexMethods
}
