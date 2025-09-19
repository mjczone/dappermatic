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
    /// Checks if a foreign key constraint exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the constraint exists, otherwise false.</returns>
    public virtual async Task<bool> DoesForeignKeyConstraintExistAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string constraintName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await GetForeignKeyConstraintAsync(
                    db,
                    schemaName,
                    tableName,
                    constraintName,
                    tx,
                    cancellationToken
                )
                .ConfigureAwait(false) != null;
    }

    /// <summary>
    /// Checks if a foreign key constraint exists on a specific column.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the constraint exists on the column, otherwise false.</returns>
    public virtual async Task<bool> DoesForeignKeyConstraintExistOnColumnAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await GetForeignKeyConstraintOnColumnAsync(
                    db,
                    schemaName,
                    tableName,
                    columnName,
                    tx,
                    cancellationToken
                )
                .ConfigureAwait(false) != null;
    }

    /// <summary>
    /// Creates a foreign key constraint if it does not exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="constraint">The foreign key constraint.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the constraint was created, otherwise false.</returns>
    public virtual async Task<bool> CreateForeignKeyConstraintIfNotExistsAsync(
        IDbConnection db,
        DmForeignKeyConstraint constraint,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await CreateForeignKeyConstraintIfNotExistsAsync(
                db,
                constraint.SchemaName,
                constraint.TableName,
                constraint.ConstraintName,
                [.. constraint.SourceColumns],
                constraint.ReferencedTableName,
                [.. constraint.ReferencedColumns],
                constraint.OnDelete,
                constraint.OnUpdate,
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a foreign key constraint if it does not exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="sourceColumns">The source columns.</param>
    /// <param name="referencedTableName">The referenced table name.</param>
    /// <param name="referencedColumns">The referenced columns.</param>
    /// <param name="onDelete">The delete action.</param>
    /// <param name="onUpdate">The update action.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the constraint was created, otherwise false.</returns>
    public virtual async Task<bool> CreateForeignKeyConstraintIfNotExistsAsync(
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
    )
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        if (string.IsNullOrWhiteSpace(constraintName))
        {
            throw new ArgumentException("Constraint name is required.", nameof(constraintName));
        }

        if (sourceColumns.Length == 0)
        {
            throw new ArgumentException(
                "At least one column must be specified.",
                nameof(sourceColumns)
            );
        }

        if (string.IsNullOrWhiteSpace(referencedTableName))
        {
            throw new ArgumentException(
                "Referenced table name is required.",
                nameof(referencedTableName)
            );
        }

        if (referencedColumns.Length == 0)
        {
            throw new ArgumentException(
                "At least one column must be specified.",
                nameof(referencedColumns)
            );
        }

        if (sourceColumns.Length != referencedColumns.Length)
        {
            throw new ArgumentException(
                "The number of source columns must match the number of referenced columns.",
                nameof(referencedColumns)
            );
        }

        if (
            await DoesForeignKeyConstraintExistAsync(
                    db,
                    schemaName,
                    tableName,
                    constraintName,
                    tx,
                    cancellationToken
                )
                .ConfigureAwait(false)
        )
        {
            return false;
        }

        var sql = SqlAlterTableAddForeignKeyConstraint(
            schemaName,
            constraintName,
            tableName,
            sourceColumns,
            referencedTableName,
            referencedColumns,
            onDelete,
            onUpdate
        );

        await ExecuteAsync(db, sql, tx: tx, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Gets a foreign key constraint.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The foreign key constraint if found, otherwise null.</returns>
    public virtual async Task<DmForeignKeyConstraint?> GetForeignKeyConstraintAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string constraintName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(constraintName))
        {
            throw new ArgumentException("Constraint name is required.", nameof(constraintName));
        }

        var foreignKeyConstraints = await GetForeignKeyConstraintsAsync(
                db,
                schemaName,
                tableName,
                constraintName,
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
        return foreignKeyConstraints.SingleOrDefault();
    }

    /// <summary>
    /// Gets the name of the foreign key constraint on a specific column.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The name of the foreign key constraint if found, otherwise null.</returns>
    public virtual async Task<string?> GetForeignKeyConstraintNameOnColumnAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return (
            await GetForeignKeyConstraintOnColumnAsync(
                    db,
                    schemaName,
                    tableName,
                    columnName,
                    tx,
                    cancellationToken
                )
                .ConfigureAwait(false)
        )?.ConstraintName;
    }

    /// <summary>
    /// Gets the names of the foreign key constraints.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintNameFilter">The constraint name filter.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of foreign key constraint names.</returns>
    public virtual async Task<List<string>> GetForeignKeyConstraintNamesAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string? constraintNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var foreignKeyConstraints = await GetForeignKeyConstraintsAsync(
                db,
                schemaName,
                tableName,
                constraintNameFilter,
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
        return foreignKeyConstraints.Select(c => c.ConstraintName).ToList();
    }

    /// <summary>
    /// Gets the foreign key constraint on a specific column.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The foreign key constraint if found, otherwise null.</returns>
    public virtual async Task<DmForeignKeyConstraint?> GetForeignKeyConstraintOnColumnAsync(
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

        var foreignKeyConstraints = await GetForeignKeyConstraintsAsync(
                db,
                schemaName,
                tableName,
                null,
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
        return foreignKeyConstraints.FirstOrDefault(c =>
            c.SourceColumns.Any(sc =>
                sc.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase)
            )
        );
    }

    /// <summary>
    /// Gets the foreign key constraints.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintNameFilter">The constraint name filter.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of foreign key constraints.</returns>
    public virtual async Task<List<DmForeignKeyConstraint>> GetForeignKeyConstraintsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string? constraintNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        var table = await GetTableAsync(db, schemaName, tableName, tx, cancellationToken)
            .ConfigureAwait(false);

        if (table == null)
        {
            return new List<DmForeignKeyConstraint>();
        }

        var filter = string.IsNullOrWhiteSpace(constraintNameFilter)
            ? null
            : ToSafeString(constraintNameFilter);

        return string.IsNullOrWhiteSpace(filter)
            ? table.ForeignKeyConstraints
            : table
                .ForeignKeyConstraints.Where(c => c.ConstraintName.IsWildcardPatternMatch(filter))
                .ToList();
    }

    /// <summary>
    /// Drops the foreign key constraint on a specific column if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the constraint was dropped, otherwise false.</returns>
    public virtual async Task<bool> DropForeignKeyConstraintOnColumnIfExistsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var constraintName = await GetForeignKeyConstraintNameOnColumnAsync(
                db,
                schemaName,
                tableName,
                columnName,
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
        return constraintName != null
            && await DropForeignKeyConstraintIfExistsAsync(
                    db,
                    schemaName,
                    tableName,
                    constraintName,
                    tx,
                    cancellationToken
                )
                .ConfigureAwait(false);
    }

    /// <summary>
    /// Drops the foreign key constraint if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the constraint was dropped, otherwise false.</returns>
    public virtual async Task<bool> DropForeignKeyConstraintIfExistsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string constraintName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        if (
            !await DoesForeignKeyConstraintExistAsync(
                    db,
                    schemaName,
                    tableName,
                    constraintName,
                    tx,
                    cancellationToken
                )
                .ConfigureAwait(false)
        )
        {
            return false;
        }

        var sql = SqlDropForeignKeyConstraint(schemaName, tableName, constraintName);

        await ExecuteAsync(db, sql, tx: tx, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return true;
    }
}
