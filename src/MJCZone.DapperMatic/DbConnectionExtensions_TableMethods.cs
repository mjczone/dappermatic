// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic;

public static partial class DbConnectionExtensions
{
    #region IDatabaseTableMethods

    /// <summary>
    /// Checks if a table exists in the database.
    /// </summary>.
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the table exists, otherwise false.</returns>
    public static async Task<bool> DoesTableExistAsync<T>(
        this IDbConnection db,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var (schemaName, tableName) = DmTableFactory.GetTableName(typeof(T));
        return await Database(db)
            .DoesTableExistAsync(db, schemaName, tableName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Checks if a table exists in the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the table exists, otherwise false.</returns>
    public static async Task<bool> DoesTableExistAsync(
        this IDbConnection db,
        string? schemaName,
        string tableName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .DoesTableExistAsync(db, schemaName, tableName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Creates tables if they do not exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="tables">The table type definitions.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task CreateTablesIfNotExistsAsync(
        this IDbConnection db,
        IEnumerable<Type> tables,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var tableDefs = tables.Select(t => DmTableFactory.GetTable(t)).ToArray();
        await Database(db).CreateTablesIfNotExistsAsync(db, tableDefs, tx, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates tables if they do not exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="tables">The table definitions.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task CreateTablesIfNotExistsAsync(
        this IDbConnection db,
        IEnumerable<DmTable> tables,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        await Database(db).CreateTablesIfNotExistsAsync(db, [.. tables], tx, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a table if it does not exist.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the table was created, otherwise false.</returns>
    public static async Task<bool> CreateTableIfNotExistsAsync<T>(
        this IDbConnection db,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var table = DmTableFactory.GetTable(typeof(T));
        return await Database(db).CreateTableIfNotExistsAsync(db, table, tx, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a table if it does not exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="table">The table definition.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the table was created, otherwise false.</returns>
    public static async Task<bool> CreateTableIfNotExistsAsync(
        this IDbConnection db,
        DmTable table,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db).CreateTableIfNotExistsAsync(db, table, tx, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a table if it does not exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columns">The columns of the table.</param>
    /// <param name="primaryKey">The primary key constraint.</param>
    /// <param name="checkConstraints">The check constraints.</param>
    /// <param name="defaultConstraints">The default constraints.</param>
    /// <param name="uniqueConstraints">The unique constraints.</param>
    /// <param name="foreignKeyConstraints">The foreign key constraints.</param>
    /// <param name="indexes">The indexes.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the table was created, otherwise false.</returns>
    public static async Task<bool> CreateTableIfNotExistsAsync(
        this IDbConnection db,
        string? schemaName,
        string tableName,
        DmColumn[] columns,
        DmPrimaryKeyConstraint? primaryKey = null,
        DmCheckConstraint[]? checkConstraints = null,
        DmDefaultConstraint[]? defaultConstraints = null,
        DmUniqueConstraint[]? uniqueConstraints = null,
        DmForeignKeyConstraint[]? foreignKeyConstraints = null,
        DmIndex[]? indexes = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .CreateTableIfNotExistsAsync(
                db,
                schemaName,
                tableName,
                columns,
                primaryKey,
                checkConstraints,
                defaultConstraints,
                uniqueConstraints,
                foreignKeyConstraints,
                indexes,
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the table definition.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The table definition.</returns>
    public static async Task<DmTable?> GetTableAsync<T>(
        this IDbConnection db,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var (schemaName, tableName) = DmTableFactory.GetTableName(typeof(T));
        return await Database(db).GetTableAsync(db, schemaName, tableName, tx, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the table definition.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The table definition.</returns>
    public static async Task<DmTable?> GetTableAsync(
        this IDbConnection db,
        string? schemaName,
        string tableName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db).GetTableAsync(db, schemaName, tableName, tx, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the list of table definitions.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableNameFilter">The table name filter.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The list of table definitions.</returns>
    public static async Task<List<DmTable>> GetTablesAsync(
        this IDbConnection db,
        string? schemaName,
        string? tableNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .GetTablesAsync(db, schemaName, tableNameFilter, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the list of table names.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableNameFilter">The table name filter.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The list of table names.</returns>
    public static async Task<List<string>> GetTableNamesAsync(
        this IDbConnection db,
        string? schemaName,
        string? tableNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .GetTableNamesAsync(db, schemaName, tableNameFilter, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Drops a table if it exists.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the table was dropped, otherwise false.</returns>
    public static async Task<bool> DropTableIfExistsAsync<T>(
        this IDbConnection db,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var (schemaName, tableName) = DmTableFactory.GetTableName(typeof(T));
        return await Database(db)
            .DropTableIfExistsAsync(db, schemaName, tableName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Drops a table if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the table was dropped, otherwise false.</returns>
    public static async Task<bool> DropTableIfExistsAsync(
        this IDbConnection db,
        string? schemaName,
        string tableName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .DropTableIfExistsAsync(db, schemaName, tableName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Renames a table if it exists.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="oldTableName">The old table name.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the table was renamed, otherwise false.</returns>
    public static async Task<bool> RenameTableIfExistsAsync<T>(
        this IDbConnection db,
        string oldTableName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var (schemaName, tableName) = DmTableFactory.GetTableName(typeof(T));
        return await Database(db)
            .RenameTableIfExistsAsync(db, schemaName, oldTableName, tableName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Renames a table if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="newTableName">The new table name.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the table was renamed, otherwise false.</returns>
    public static async Task<bool> RenameTableIfExistsAsync(
        this IDbConnection db,
        string? schemaName,
        string tableName,
        string newTableName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .RenameTableIfExistsAsync(db, schemaName, tableName, newTableName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Truncates a table if it exists.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the table was truncated, otherwise false.</returns>
    public static async Task<bool> TruncateTableIfExistsAsync<T>(
        this IDbConnection db,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var (schemaName, tableName) = DmTableFactory.GetTableName(typeof(T));
        return await Database(db)
            .TruncateTableIfExistsAsync(db, schemaName, tableName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Truncates a table if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the table was truncated, otherwise false.</returns>
    public static async Task<bool> TruncateTableIfExistsAsync(
        this IDbConnection db,
        string? schemaName,
        string tableName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .TruncateTableIfExistsAsync(db, schemaName, tableName, tx, cancellationToken)
            .ConfigureAwait(false);
    }
    #endregion // IDatabaseTableMethods
}
