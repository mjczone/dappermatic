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
    /// Checks if a check constraint exists in the specified table.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the check constraint exists, otherwise false.</returns>
    public virtual async Task<bool> DoesCheckConstraintExistAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string constraintName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        if (!await SupportsCheckConstraintsAsync(db, tx, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        return await GetCheckConstraintAsync(db, schemaName, tableName, constraintName, tx, cancellationToken)
                .ConfigureAwait(false) != null;
    }

    /// <summary>
    /// Checks if a check constraint exists on a specific column in the specified table.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the check constraint exists on the column, otherwise false.</returns>
    public virtual async Task<bool> DoesCheckConstraintExistOnColumnAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        if (!await SupportsCheckConstraintsAsync(db, tx, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        return await GetCheckConstraintOnColumnAsync(db, schemaName, tableName, columnName, tx, cancellationToken)
                .ConfigureAwait(false) != null;
    }

    /// <summary>
    /// Creates a check constraint if it does not already exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="constraint">The check constraint details.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the check constraint was created, otherwise false.</returns>
    public virtual async Task<bool> CreateCheckConstraintIfNotExistsAsync(
        IDbConnection db,
        DmCheckConstraint constraint,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        if (!await SupportsCheckConstraintsAsync(db, tx, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        return await CreateCheckConstraintIfNotExistsAsync(
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
    /// Creates a check constraint if it does not already exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="expression">The check constraint expression.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the check constraint was created, otherwise false.</returns>
    public virtual async Task<bool> CreateCheckConstraintIfNotExistsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string? columnName,
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

        if (!await SupportsCheckConstraintsAsync(db, tx, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        if (
            await DoesCheckConstraintExistAsync(db, schemaName, tableName, constraintName, tx, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            return false;
        }

        var sql = SqlAlterTableAddCheckConstraint(schemaName, tableName, constraintName, expression);

        await ExecuteAsync(db, sql, tx: tx, cancellationToken: cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Gets the details of a check constraint.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The check constraint details, or null if not found.</returns>
    public virtual async Task<DmCheckConstraint?> GetCheckConstraintAsync(
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

        var checkConstraints = await GetCheckConstraintsAsync(
                db,
                schemaName,
                tableName,
                constraintName,
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);

        return checkConstraints.SingleOrDefault();
    }

    /// <summary>
    /// Gets the name of a check constraint on a specific column.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The check constraint name, or null if not found.</returns>
    public virtual async Task<string?> GetCheckConstraintNameOnColumnAsync(
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

        var checkConstraints = await GetCheckConstraintsAsync(db, schemaName, tableName, null, tx, cancellationToken)
            .ConfigureAwait(false);

        return checkConstraints
            .FirstOrDefault(c =>
                !string.IsNullOrWhiteSpace(c.ColumnName)
                && c.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase)
            )
            ?.ConstraintName;
    }

    /// <summary>
    /// Gets the names of all check constraints in the specified table.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintNameFilter">The constraint name filter.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of check constraint names.</returns>
    public virtual async Task<List<string>> GetCheckConstraintNamesAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string? constraintNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var checkConstraints = await GetCheckConstraintsAsync(
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
    /// Gets the details of a check constraint on a specific column.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The check constraint details, or null if not found.</returns>
    public virtual async Task<DmCheckConstraint?> GetCheckConstraintOnColumnAsync(
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

        var checkConstraints = await GetCheckConstraintsAsync(db, schemaName, tableName, null, tx, cancellationToken)
            .ConfigureAwait(false);

        return checkConstraints.FirstOrDefault(c =>
            !string.IsNullOrWhiteSpace(c.ColumnName)
            && c.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <summary>
    /// Gets the details of all check constraints in the specified table.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintNameFilter">The constraint name filter.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of check constraint details.</returns>
    public virtual async Task<List<DmCheckConstraint>> GetCheckConstraintsAsync(
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

        if (!await SupportsCheckConstraintsAsync(db, tx, cancellationToken).ConfigureAwait(false))
        {
            return [];
        }

        var table = await GetTableAsync(db, schemaName, tableName, tx, cancellationToken).ConfigureAwait(false);

        if (table == null)
        {
            return [];
        }

        var filter = string.IsNullOrWhiteSpace(constraintNameFilter) ? null : ToSafeString(constraintNameFilter);

        return string.IsNullOrWhiteSpace(filter)
            ? table.CheckConstraints
            : table.CheckConstraints.Where(c => c.ConstraintName.IsWildcardPatternMatch(filter)).ToList();
    }

    /// <summary>
    /// Drops a check constraint on a specific column if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the check constraint was dropped, otherwise false.</returns>
    public virtual async Task<bool> DropCheckConstraintOnColumnIfExistsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        if (string.IsNullOrWhiteSpace(columnName))
        {
            throw new ArgumentException("Column name is required.", nameof(columnName));
        }

        if (!await SupportsCheckConstraintsAsync(db, tx, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var constraintName = await GetCheckConstraintNameOnColumnAsync(
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

        var sql = SqlDropCheckConstraint(schemaName, tableName, constraintName);

        await ExecuteAsync(db, sql, tx: tx, cancellationToken: cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Drops a check constraint if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the check constraint was dropped, otherwise false.</returns>
    public virtual async Task<bool> DropCheckConstraintIfExistsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string constraintName,
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

        if (!await SupportsCheckConstraintsAsync(db, tx, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        if (
            !await DoesCheckConstraintExistAsync(db, schemaName, tableName, constraintName, tx, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            return false;
        }

        var sql = SqlDropCheckConstraint(schemaName, tableName, constraintName);

        await ExecuteAsync(db, sql, tx: tx, cancellationToken: cancellationToken).ConfigureAwait(false);

        return true;
    }
}
