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
    /// Checks if a primary key constraint exists on the specified table.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the primary key constraint exists, otherwise false.</returns>
    public virtual async Task<bool> DoesPrimaryKeyConstraintExistAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await GetPrimaryKeyConstraintAsync(db, schemaName, tableName, tx, cancellationToken)
                .ConfigureAwait(false) != null;
    }

    /// <summary>
    /// Creates a primary key constraint if it does not already exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="constraint">The primary key constraint details.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the primary key constraint was created, otherwise false.</returns>
    public virtual async Task<bool> CreatePrimaryKeyConstraintIfNotExistsAsync(
        IDbConnection db,
        DmPrimaryKeyConstraint constraint,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await CreatePrimaryKeyConstraintIfNotExistsAsync(
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
    /// Creates a primary key constraint if it does not already exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="columns">The columns that make up the primary key.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the primary key constraint was created, otherwise false.</returns>
    public virtual async Task<bool> CreatePrimaryKeyConstraintIfNotExistsAsync(
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
            await DoesPrimaryKeyConstraintExistAsync(
                    db,
                    schemaName,
                    tableName,
                    tx,
                    cancellationToken
                )
                .ConfigureAwait(false)
        )
        {
            return false;
        }

        var supportsOrderedKeysInConstraints = await SupportsOrderedKeysInConstraintsAsync(
                db,
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);

        var sql = SqlAlterTableAddPrimaryKeyConstraint(
            schemaName,
            tableName,
            constraintName,
            columns,
            supportsOrderedKeysInConstraints
        );

        await ExecuteAsync(db, sql, tx: tx, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Gets the primary key constraint for the specified table.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The primary key constraint, or null if it does not exist.</returns>
    public virtual async Task<DmPrimaryKeyConstraint?> GetPrimaryKeyConstraintAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var table = await GetTableAsync(db, schemaName, tableName, tx, cancellationToken)
            .ConfigureAwait(false);

        if (table?.PrimaryKeyConstraint is null)
        {
            return null;
        }

        return table.PrimaryKeyConstraint;
    }

    /// <summary>
    /// Drops the primary key constraint if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the primary key constraint was dropped, otherwise false.</returns>
    public virtual async Task<bool> DropPrimaryKeyConstraintIfExistsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var primaryKeyConstraint = await GetPrimaryKeyConstraintAsync(
                db,
                schemaName,
                tableName,
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(primaryKeyConstraint?.ConstraintName))
        {
            return false;
        }

        var sql = SqlDropPrimaryKeyConstraint(
            schemaName,
            tableName,
            primaryKeyConstraint.ConstraintName
        );

        await ExecuteAsync(db, sql, tx: tx, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return true;
    }
}
