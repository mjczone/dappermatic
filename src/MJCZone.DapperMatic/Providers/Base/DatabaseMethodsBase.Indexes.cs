// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Providers.Base;

public abstract partial class DatabaseMethodsBase
{
    /// <summary>
    /// Checks if an index exists in the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="indexName">The index name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the index exists, otherwise false.</returns>
    public virtual async Task<bool> DoesIndexExistAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string indexName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await GetIndexAsync(db, schemaName, tableName, indexName, tx, cancellationToken)
                .ConfigureAwait(false) != null;
    }

    /// <summary>
    /// Checks if an index exists on a specific column in the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the index exists on the column, otherwise false.</returns>
    public virtual async Task<bool> DoesIndexExistOnColumnAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return (
                await GetIndexesOnColumnAsync(
                        db,
                        schemaName,
                        tableName,
                        columnName,
                        tx,
                        cancellationToken
                    )
                    .ConfigureAwait(false)
            ).Count > 0;
    }

    /// <summary>
    /// Creates an index if it does not already exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="index">The index details.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the index was created, otherwise false.</returns>
    public virtual async Task<bool> CreateIndexIfNotExistsAsync(
        IDbConnection db,
        DmIndex index,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await CreateIndexIfNotExistsAsync(
                db,
                index.SchemaName,
                index.TableName,
                index.IndexName,
                [.. index.Columns],
                index.IsUnique,
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Creates an index if it does not already exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="indexName">The index name.</param>
    /// <param name="columns">The columns to include in the index.</param>
    /// <param name="isUnique">Whether the index is unique.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the index was created, otherwise false.</returns>
    public virtual async Task<bool> CreateIndexIfNotExistsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string indexName,
        DmOrderedColumn[] columns,
        bool isUnique = false,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(indexName))
        {
            throw new ArgumentException("Index name is required.", nameof(indexName));
        }

        if (
            await DoesIndexExistAsync(db, schemaName, tableName, indexName, tx, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            return false;
        }

        var sql = SqlCreateIndex(schemaName, tableName, indexName, columns, isUnique);

        await ExecuteAsync(db, sql, tx: tx, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Retrieves an index from the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="indexName">The index name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The index details, or null if the index does not exist.</returns>
    public virtual async Task<DmIndex?> GetIndexAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string indexName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(indexName))
        {
            throw new ArgumentException("Index name is required.", nameof(indexName));
        }

        var indexes = await GetIndexesAsync(
                db,
                schemaName,
                tableName,
                indexName,
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
        return indexes.SingleOrDefault();
    }

    /// <summary>
    /// Retrieves a list of indexes from the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="indexNameFilter">An optional filter for the index name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of indexes.</returns>
    public virtual async Task<List<DmIndex>> GetIndexesAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string? indexNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        (schemaName, tableName, _) = NormalizeNames(schemaName, tableName);

        return await GetIndexesInternalAsync(
                db,
                schemaName,
                tableName,
                string.IsNullOrWhiteSpace(indexNameFilter) ? null : indexNameFilter,
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves a list of index names on a specific column from the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of index names.</returns>
    public virtual async Task<List<string>> GetIndexNamesOnColumnAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return (
            await GetIndexesOnColumnAsync(
                    db,
                    schemaName,
                    tableName,
                    columnName,
                    tx,
                    cancellationToken
                )
                .ConfigureAwait(false)
        )
            .Select(x => x.IndexName)
            .ToList();
    }

    /// <summary>
    /// Retrieves a list of index names from the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="indexNameFilter">An optional filter for the index name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of index names.</returns>
    public virtual async Task<List<string>> GetIndexNamesAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string? indexNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return (
            await GetIndexesAsync(db, schemaName, tableName, indexNameFilter, tx, cancellationToken)
                .ConfigureAwait(false)
        )
            .Select(x => x.IndexName)
            .ToList();
    }

    /// <summary>
    /// Retrieves a list of indexes on a specific column from the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of indexes.</returns>
    public virtual async Task<List<DmIndex>> GetIndexesOnColumnAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(columnName))
        {
            throw new ArgumentException("Column name is required.", nameof(columnName));
        }

        var indexes = await GetIndexesAsync(db, schemaName, tableName, null, tx, cancellationToken)
            .ConfigureAwait(false);

        return indexes
            .Where(c =>
                c.Columns.Any(x =>
                    x.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase)
                )
            )
            .ToList();
    }

    /// <summary>
    /// Drops an index if it exists in the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="indexName">The index name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the index was dropped, otherwise false.</returns>
    public virtual async Task<bool> DropIndexIfExistsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string indexName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        if (
            !await DoesIndexExistAsync(db, schemaName, tableName, indexName, tx, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            return false;
        }

        var sql = SqlDropIndex(schemaName, tableName, indexName);

        await ExecuteAsync(db, sql, tx: tx, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Drops indexes on a specific column if they exist in the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if any indexes were dropped, otherwise false.</returns>
    public virtual async Task<bool> DropIndexesOnColumnIfExistsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var indexNames = await GetIndexNamesOnColumnAsync(
                db,
                schemaName,
                tableName,
                columnName,
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (indexNames.Count == 0)
        {
            return false;
        }

        foreach (var indexName in indexNames)
        {
            var sql = SqlDropIndex(schemaName, tableName, indexName);
            await ExecuteAsync(db, sql, tx: tx, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        return true;
    }
}
