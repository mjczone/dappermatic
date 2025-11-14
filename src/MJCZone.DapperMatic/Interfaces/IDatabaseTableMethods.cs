// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Interfaces;

/// <summary>
/// Provides database table methods for database operations.
/// </summary>
public interface IDatabaseTableMethods
{
    /// <summary>
    /// Checks if a table exists in the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name of the table.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether the table exists.</returns>
    Task<bool> DoesTableExistAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Creates tables if they do not exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="tables">The table definitions.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task CreateTablesIfNotExistsAsync(
        IDbConnection db,
        DmTable[] tables,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Creates a table if it does not exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="table">The table definition.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether the table was created.</returns>
    Task<bool> CreateTableIfNotExistsAsync(
        IDbConnection db,
        DmTable table,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Creates a table with specified columns and constraints if it does not exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name of the table.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="columns">The columns of the table.</param>
    /// <param name="primaryKey">The primary key constraint.</param>
    /// <param name="checkConstraints">The check constraints.</param>
    /// <param name="defaultConstraints">The default constraints.</param>
    /// <param name="uniqueConstraints">The unique constraints.</param>
    /// <param name="foreignKeyConstraints">The foreign key constraints.</param>
    /// <param name="indexes">The indexes.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether the table was created.</returns>
    Task<bool> CreateTableIfNotExistsAsync(
        IDbConnection db,
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
    );

    /// <summary>
    /// Retrieves a table definition from the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name of the table.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the table definition.</returns>
    Task<DmTable?> GetTableAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Retrieves a list of table definitions from the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name of the tables.</param>
    /// <param name="tableNameFilter">The table name filter.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of table definitions.</returns>
    Task<List<DmTable>> GetTablesAsync(
        IDbConnection db,
        string? schemaName,
        string? tableNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Retrieves a list of table names from the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name of the tables.</param>
    /// <param name="tableNameFilter">The table name filter.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of table names.</returns>
    Task<List<string>> GetTableNamesAsync(
        IDbConnection db,
        string? schemaName,
        string? tableNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Drops a table if it exists in the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name of the table.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether the table was dropped.</returns>
    Task<bool> DropTableIfExistsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Renames a table if it exists in the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name of the table.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="newTableName">The new name of the table.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether the table was renamed.</returns>
    Task<bool> RenameTableIfExistsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string newTableName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Truncates a table if it exists in the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name of the table.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether the table was truncated.</returns>
    Task<bool> TruncateTableIfExistsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    );
}
