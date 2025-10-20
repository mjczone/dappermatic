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
    public override async Task<bool> CreatePrimaryKeyConstraintIfNotExistsAsync(
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
            await DoesPrimaryKeyConstraintExistAsync(db, schemaName, tableName, tx, cancellationToken)
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
                table => table.PrimaryKeyConstraint is null,
                table =>
                {
                    table.PrimaryKeyConstraint = new DmPrimaryKeyConstraint(
                        schemaName,
                        tableName,
                        constraintName,
                        columns
                    );

                    return table;
                },
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task<bool> DropPrimaryKeyConstraintIfExistsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        if (
            !await DoesPrimaryKeyConstraintExistAsync(db, schemaName, tableName, tx, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            return false;
        }

        return await AlterTableUsingRecreateTableStrategyAsync(
                db,
                schemaName,
                tableName,
                table => table.PrimaryKeyConstraint is not null,
                table =>
                {
                    table.PrimaryKeyConstraint = null;
                    foreach (var column in table.Columns)
                    {
                        column.IsPrimaryKey = false;
                    }
                    return table;
                },
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
    }
}
