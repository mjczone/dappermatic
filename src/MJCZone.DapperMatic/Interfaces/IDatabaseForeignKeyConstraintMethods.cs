// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Interfaces;

/// <summary>
/// Provides database default constraint methods for database operations.
/// </summary>
public interface IDatabaseForeignKeyConstraintMethods
{
    /// <summary>
    /// Creates a foreign key constraint if it does not already exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="constraint">The foreign key constraint to create.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the constraint was created, otherwise false.</returns>
    Task<bool> CreateForeignKeyConstraintIfNotExistsAsync(
        IDbConnection db,
        DmForeignKeyConstraint constraint,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Creates a foreign key constraint if it does not already exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="sourceColumns">The source columns.</param>
    /// <param name="referencedTableName">The referenced table name.</param>
    /// <param name="referencedColumns">The referenced columns.</param>
    /// <param name="onDelete">The action on delete.</param>
    /// <param name="onUpdate">The action on update.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the constraint was created, otherwise false.</returns>
    Task<bool> CreateForeignKeyConstraintIfNotExistsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string constraintName,
        DmOrderedColumn[] sourceColumns,
        string referencedTableName,
        DmOrderedColumn[] referencedColumns,
        DmForeignKeyAction onDelete = DmForeignKeyAction.NoAction,
        DmForeignKeyAction onUpdate = DmForeignKeyAction.NoAction,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Checks if a foreign key constraint exists on a column.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the constraint exists, otherwise false.</returns>
    Task<bool> DoesForeignKeyConstraintExistOnColumnAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Checks if a foreign key constraint exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the constraint exists, otherwise false.</returns>
    Task<bool> DoesForeignKeyConstraintExistAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string constraintName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets the foreign key constraint on a column.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The foreign key constraint if it exists, otherwise null.</returns>
    Task<DmForeignKeyConstraint?> GetForeignKeyConstraintOnColumnAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets the foreign key constraint.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The foreign key constraint if it exists, otherwise null.</returns>
    Task<DmForeignKeyConstraint?> GetForeignKeyConstraintAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string constraintName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets the foreign key constraints.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintNameFilter">The constraint name filter.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of foreign key constraints.</returns>
    Task<List<DmForeignKeyConstraint>> GetForeignKeyConstraintsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string? constraintNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets the foreign key constraint name on a column.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The foreign key constraint name if it exists, otherwise null.</returns>
    Task<string?> GetForeignKeyConstraintNameOnColumnAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets the foreign key constraint names.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintNameFilter">The constraint name filter.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of foreign key constraint names.</returns>
    Task<List<string>> GetForeignKeyConstraintNamesAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string? constraintNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Drops a foreign key constraint on a column if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the constraint was dropped, otherwise false.</returns>
    Task<bool> DropForeignKeyConstraintOnColumnIfExistsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Drops a foreign key constraint if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the constraint was dropped, otherwise false.</returns>
    Task<bool> DropForeignKeyConstraintIfExistsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string constraintName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    );
}
