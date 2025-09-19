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
    /// Checks if a default constraint exists in the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the constraint exists, otherwise false.</returns>
    public virtual async Task<bool> DoesDefaultConstraintExistAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string constraintName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await GetDefaultConstraintAsync(
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
    /// Checks if a default constraint exists on a specific column in the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the constraint exists, otherwise false.</returns>
    public virtual async Task<bool> DoesDefaultConstraintExistOnColumnAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await GetDefaultConstraintOnColumnAsync(
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
    /// Creates a default constraint if it does not exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="constraint">The default constraint.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the constraint was created, otherwise false.</returns>
    public virtual async Task<bool> CreateDefaultConstraintIfNotExistsAsync(
        IDbConnection db,
        DmDefaultConstraint constraint,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await CreateDefaultConstraintIfNotExistsAsync(
                db,
                constraint.SchemaName,
                constraint.TableName,
                constraint.ColumnName,
                constraint.ConstraintName,
                constraint.Expression,
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a default constraint if it does not exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="expression">The expression.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the constraint was created, otherwise false.</returns>
    public virtual async Task<bool> CreateDefaultConstraintIfNotExistsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        string constraintName,
        string expression,
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

        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new ArgumentException("Expression is required.", nameof(expression));
        }

        if (
            await DoesDefaultConstraintExistAsync(
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

        var sql = SqlAlterTableAddDefaultConstraint(
            schemaName,
            tableName,
            columnName,
            constraintName,
            expression
        );

        await ExecuteAsync(db, sql, tx: tx, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Gets a default constraint from the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The default constraint if found, otherwise null.</returns>
    public virtual async Task<DmDefaultConstraint?> GetDefaultConstraintAsync(
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

        var defaultConstraints = await GetDefaultConstraintsAsync(
                db,
                schemaName,
                tableName,
                constraintName,
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);

        return defaultConstraints.SingleOrDefault();
    }

    /// <summary>
    /// Gets the name of the default constraint on a specific column.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The name of the default constraint if found, otherwise null.</returns>
    public virtual async Task<string?> GetDefaultConstraintNameOnColumnAsync(
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

        var defaultConstraints = await GetDefaultConstraintsAsync(
                db,
                schemaName,
                tableName,
                null,
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);

        return defaultConstraints
            .FirstOrDefault(c =>
                !string.IsNullOrWhiteSpace(c.ColumnName)
                && c.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase)
            )
            ?.ConstraintName;
    }

    /// <summary>
    /// Gets the names of all default constraints in the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintNameFilter">The constraint name filter.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of default constraint names.</returns>
    public virtual async Task<List<string>> GetDefaultConstraintNamesAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string? constraintNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var checkConstraints = await GetDefaultConstraintsAsync(
                db,
                schemaName,
                tableName,
                constraintNameFilter,
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
        return checkConstraints.Select(c => c.ConstraintName).ToList();
    }

    /// <summary>
    /// Gets the default constraint on a specific column.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The default constraint if found, otherwise null.</returns>
    public virtual async Task<DmDefaultConstraint?> GetDefaultConstraintOnColumnAsync(
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

        var defaultConstraints = await GetDefaultConstraintsAsync(
                db,
                schemaName,
                tableName,
                null,
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);

        return defaultConstraints.FirstOrDefault(c =>
            !string.IsNullOrWhiteSpace(c.ColumnName)
            && c.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <summary>
    /// Gets all default constraints in the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintNameFilter">The constraint name filter.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of default constraints.</returns>
    public virtual async Task<List<DmDefaultConstraint>> GetDefaultConstraintsAsync(
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
            return [];
        }

        var filter = string.IsNullOrWhiteSpace(constraintNameFilter)
            ? null
            : ToSafeString(constraintNameFilter);

        return string.IsNullOrWhiteSpace(filter)
            ? table.DefaultConstraints
            : table
                .DefaultConstraints.Where(c => c.ConstraintName.IsWildcardPatternMatch(filter))
                .ToList();
    }

    /// <summary>
    /// Drops the default constraint on a specific column if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the constraint was dropped, otherwise false.</returns>
    public virtual async Task<bool> DropDefaultConstraintOnColumnIfExistsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var constraintName = await GetDefaultConstraintNameOnColumnAsync(
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

        var sql = SqlDropDefaultConstraint(schemaName, tableName, columnName, constraintName);

        await ExecuteAsync(db, sql, tx: tx, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Drops a default constraint if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the constraint was dropped, otherwise false.</returns>
    public virtual async Task<bool> DropDefaultConstraintIfExistsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string constraintName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var defaultConstraint = await GetDefaultConstraintAsync(
                db,
                schemaName,
                tableName,
                constraintName,
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(defaultConstraint?.ColumnName))
        {
            return false;
        }

        var sql = SqlDropDefaultConstraint(
            schemaName,
            tableName,
            defaultConstraint.ColumnName,
            constraintName
        );

        await ExecuteAsync(db, sql, tx: tx, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return true;
    }
}
