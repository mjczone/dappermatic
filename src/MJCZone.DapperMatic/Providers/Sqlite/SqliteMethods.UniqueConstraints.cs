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
    public override async Task<bool> CreateUniqueConstraintIfNotExistsAsync(
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
            await DoesUniqueConstraintExistAsync(
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

        (_, tableName, constraintName) = NormalizeNames(schemaName, tableName, constraintName);

        return await AlterTableUsingRecreateTableStrategyAsync(
                db,
                schemaName,
                tableName,
                table =>
                {
                    return table.UniqueConstraints.All(uc =>
                        !uc.ConstraintName.Equals(
                            constraintName,
                            StringComparison.OrdinalIgnoreCase
                        )
                    );
                },
                table =>
                {
                    table.UniqueConstraints.Add(
                        new DmUniqueConstraint(schemaName, tableName, constraintName, columns)
                    );

                    return table;
                },
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task<bool> DropUniqueConstraintIfExistsAsync(
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

        if (
            !await DoesUniqueConstraintExistAsync(
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

        return await AlterTableUsingRecreateTableStrategyAsync(
                db,
                schemaName,
                tableName,
                table =>
                {
                    return table.UniqueConstraints.Any(uc =>
                        uc.ConstraintName.Equals(constraintName, StringComparison.OrdinalIgnoreCase)
                    );
                },
                table =>
                {
                    var uc = table.UniqueConstraints.FirstOrDefault(uc =>
                        uc.ConstraintName.Equals(constraintName, StringComparison.OrdinalIgnoreCase)
                    );
                    if (uc is not null)
                    {
                        if (uc.Columns.Count == 1)
                        {
                            var tableColumn = table.Columns.First(x =>
                                x.ColumnName.Equals(
                                    uc.Columns[0].ColumnName,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            );
                            if (!tableColumn.IsIndexed)
                            {
                                tableColumn.IsUnique = false;
                            }
                        }
                        table.UniqueConstraints.Remove(uc);
                    }
                    table.UniqueConstraints.RemoveAll(x =>
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
