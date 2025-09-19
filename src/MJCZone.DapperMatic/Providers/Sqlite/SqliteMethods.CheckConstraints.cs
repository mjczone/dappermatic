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
    public override async Task<bool> CreateCheckConstraintIfNotExistsAsync(
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

        (_, tableName, constraintName) = NormalizeNames(schemaName, tableName, constraintName);

        return await AlterTableUsingRecreateTableStrategyAsync(
                db,
                schemaName,
                tableName,
                table =>
                {
                    if (!string.IsNullOrWhiteSpace(columnName))
                    {
                        return table.CheckConstraints.All(x =>
                                !x.ConstraintName.Equals(
                                    constraintName,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                            && table.CheckConstraints.All(x =>
                                string.IsNullOrWhiteSpace(x.ColumnName)
                                || !x.ColumnName.Equals(
                                    columnName,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            );
                    }
                    return table.CheckConstraints.All(x =>
                        !x.ConstraintName.Equals(constraintName, StringComparison.OrdinalIgnoreCase)
                    );
                },
                table =>
                {
                    table.CheckConstraints.Add(
                        new DmCheckConstraint(
                            schemaName,
                            tableName,
                            columnName,
                            constraintName,
                            expression
                        )
                    );
                    return table;
                },
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task<bool> DropCheckConstraintIfExistsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string constraintName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        (schemaName, tableName, constraintName) = NormalizeNames(
            schemaName,
            tableName,
            constraintName
        );

        return await AlterTableUsingRecreateTableStrategyAsync(
                db,
                schemaName,
                tableName,
                table =>
                {
                    return table.CheckConstraints.Any(x =>
                        x.ConstraintName.Equals(constraintName, StringComparison.OrdinalIgnoreCase)
                    );
                },
                table =>
                {
                    var checkConstraint = table.CheckConstraints.SingleOrDefault(x =>
                        x.ConstraintName.Equals(constraintName, StringComparison.OrdinalIgnoreCase)
                    );
                    if (!string.IsNullOrWhiteSpace(checkConstraint?.ColumnName))
                    {
                        var column = table.Columns.SingleOrDefault(x =>
                            x.ColumnName.Equals(
                                checkConstraint.ColumnName,
                                StringComparison.OrdinalIgnoreCase
                            )
                        );
                        if (column != null)
                        {
                            column.CheckExpression = null;
                        }
                    }
                    table.CheckConstraints.RemoveAll(x =>
                        x.ConstraintName.Equals(constraintName, StringComparison.OrdinalIgnoreCase)
                    );
                    return table;
                },
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
    }
}
