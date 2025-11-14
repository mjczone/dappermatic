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
    /// Checks if a unique constraint exists in the specified table.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the unique constraint exists, otherwise false.</returns>
    public virtual async Task<bool> DoesUniqueConstraintExistAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string constraintName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await GetUniqueConstraintAsync(db, schemaName, tableName, constraintName, tx, cancellationToken)
                .ConfigureAwait(false) != null;
    }

    /// <summary>
    /// Checks if a unique constraint exists on the specified column.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the unique constraint exists on the column, otherwise false.</returns>
    public virtual async Task<bool> DoesUniqueConstraintExistOnColumnAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await GetUniqueConstraintOnColumnAsync(db, schemaName, tableName, columnName, tx, cancellationToken)
                .ConfigureAwait(false) != null;
    }

    /// <summary>
    /// Creates a unique constraint if it does not already exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="constraint">The unique constraint to create.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the unique constraint was created, otherwise false.</returns>
    public virtual async Task<bool> CreateUniqueConstraintIfNotExistsAsync(
        IDbConnection db,
        DmUniqueConstraint constraint,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await CreateUniqueConstraintIfNotExistsAsync(
                db,
                constraint.SchemaName,
                constraint.TableName,
                constraint.ConstraintName,
                [.. constraint.Columns],
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a unique constraint if it does not already exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="columns">The columns to include in the constraint.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the unique constraint was created, otherwise false.</returns>
    public virtual async Task<bool> CreateUniqueConstraintIfNotExistsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string constraintName,
        DmOrderedColumn[] columns,
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

        if (columns.Length == 0)
        {
            throw new ArgumentException("At least one column must be specified.", nameof(columns));
        }

        if (
            await DoesUniqueConstraintExistAsync(db, schemaName, tableName, constraintName, tx, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            return false;
        }

        var supportsOrderedKeysInConstraints = await SupportsOrderedKeysInConstraintsAsync(db, tx, cancellationToken)
            .ConfigureAwait(false);

        var sql = SqlAlterTableAddUniqueConstraint(
            schemaName,
            tableName,
            constraintName,
            columns,
            supportsOrderedKeysInConstraints
        );

        await ExecuteAsync(db, sql, tx: tx, cancellationToken: cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Gets a unique constraint by name.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The unique constraint if found, otherwise null.</returns>
    public virtual async Task<DmUniqueConstraint?> GetUniqueConstraintAsync(
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

        var uniqueConstraints = await GetUniqueConstraintsAsync(
                db,
                schemaName,
                tableName,
                constraintName,
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
        return uniqueConstraints.SingleOrDefault();
    }

    /// <summary>
    /// Gets the name of the unique constraint on the specified column.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The name of the unique constraint if found, otherwise null.</returns>
    public virtual async Task<string?> GetUniqueConstraintNameOnColumnAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return (
            await GetUniqueConstraintOnColumnAsync(db, schemaName, tableName, columnName, tx, cancellationToken)
                .ConfigureAwait(false)
        )?.ConstraintName;
    }

    /// <summary>
    /// Gets the names of all unique constraints in the specified table.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintNameFilter">An optional filter for constraint names.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of unique constraint names.</returns>
    public virtual async Task<List<string>> GetUniqueConstraintNamesAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string? constraintNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var uniqueConstraints = await GetUniqueConstraintsAsync(
                db,
                schemaName,
                tableName,
                constraintNameFilter,
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
        return uniqueConstraints.Select(c => c.ConstraintName).ToList();
    }

    /// <summary>
    /// Gets the unique constraint on the specified column.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The unique constraint if found, otherwise null.</returns>
    public virtual async Task<DmUniqueConstraint?> GetUniqueConstraintOnColumnAsync(
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

        var uniqueConstraints = await GetUniqueConstraintsAsync(db, schemaName, tableName, null, tx, cancellationToken)
            .ConfigureAwait(false);
        return uniqueConstraints.FirstOrDefault(c =>
            c.Columns.Any(sc => sc.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase))
        );
    }

    /// <summary>
    /// Gets all unique constraints in the specified table.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintNameFilter">An optional filter for constraint names.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of unique constraints.</returns>
    public virtual async Task<List<DmUniqueConstraint>> GetUniqueConstraintsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string? constraintNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var table = await GetTableAsync(db, schemaName, tableName, tx, cancellationToken).ConfigureAwait(false);
        if (table == null)
        {
            return [];
        }

        var filter = string.IsNullOrWhiteSpace(constraintNameFilter) ? null : ToSafeString(constraintNameFilter);

        return string.IsNullOrWhiteSpace(filter)
            ? table.UniqueConstraints
            : table.UniqueConstraints.Where(c => c.ConstraintName.IsWildcardPatternMatch(filter)).ToList();
    }

    /// <summary>
    /// Drops a unique constraint if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the unique constraint was dropped, otherwise false.</returns>
    public virtual async Task<bool> DropUniqueConstraintIfExistsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string constraintName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        if (
            !await DoesUniqueConstraintExistAsync(db, schemaName, tableName, constraintName, tx, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            return false;
        }

        var sql = SqlDropUniqueConstraint(schemaName, tableName, constraintName);

        await ExecuteAsync(db, sql, tx: tx, cancellationToken: cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Drops a unique constraint on the specified column if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the unique constraint was dropped, otherwise false.</returns>
    public virtual async Task<bool> DropUniqueConstraintOnColumnIfExistsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var constraintName = await GetUniqueConstraintNameOnColumnAsync(
                db,
                schemaName,
                tableName,
                columnName,
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(constraintName))
        {
            return false;
        }

        var sql = SqlDropUniqueConstraint(schemaName, tableName, constraintName);

        await ExecuteAsync(db, sql, tx: tx, cancellationToken: cancellationToken).ConfigureAwait(false);

        return true;
    }
}
