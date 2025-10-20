// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Providers.Sqlite;

public partial class SqliteMethods
{
    /// <inheritdoc/>
    public override async Task<bool> CreateDefaultConstraintIfNotExistsAsync(
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

        if (string.IsNullOrWhiteSpace(columnName))
        {
            throw new ArgumentException("Column name is required.", nameof(columnName));
        }

        if (string.IsNullOrWhiteSpace(constraintName))
        {
            throw new ArgumentException("Constraint name is required.", nameof(constraintName));
        }

        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new ArgumentException("Expression is required.", nameof(expression));
        }

        (_, tableName, constraintName) = NormalizeNames(schemaName, tableName, constraintName);

        return await AlterTableUsingRecreateTableStrategyAsync(
                db,
                schemaName,
                tableName,
                table =>
                {
                    return table.DefaultConstraints.All(x =>
                        !x.ConstraintName.Equals(constraintName, StringComparison.OrdinalIgnoreCase)
                        && !x.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase)
                    );
                },
                table =>
                {
                    table.DefaultConstraints.Add(
                        new DmDefaultConstraint(schemaName, tableName, columnName, constraintName, expression)
                    );
                    return table;
                },
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task<bool> DropDefaultConstraintIfExistsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string constraintName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        (schemaName, tableName, constraintName) = NormalizeNames(schemaName, tableName, constraintName);

        return await AlterTableUsingRecreateTableStrategyAsync(
                db,
                schemaName,
                tableName,
                table =>
                {
                    return table.DefaultConstraints.Any(x =>
                        x.ConstraintName.Equals(constraintName, StringComparison.OrdinalIgnoreCase)
                    );
                },
                table =>
                {
                    var defaultConstraint = table.DefaultConstraints.SingleOrDefault(x =>
                        x.ConstraintName.Equals(constraintName, StringComparison.OrdinalIgnoreCase)
                    );
                    if (!string.IsNullOrWhiteSpace(defaultConstraint?.ColumnName))
                    {
                        var column = table.Columns.SingleOrDefault(x =>
                            x.ColumnName.Equals(defaultConstraint.ColumnName, StringComparison.OrdinalIgnoreCase)
                        );
                        if (column != null)
                        {
                            column.DefaultExpression = null;
                        }
                    }
                    table.DefaultConstraints.RemoveAll(x =>
                        x.ConstraintName.Equals(constraintName, StringComparison.OrdinalIgnoreCase)
                    );
                    return table;
                },
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task<bool> DropDefaultConstraintOnColumnIfExistsAsync(
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

        return await DropDefaultConstraintIfExistsAsync(
                db,
                schemaName,
                tableName,
                constraintName,
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
    }
}
