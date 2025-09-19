// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Providers.Sqlite;

public partial class SqliteMethods
{
    /// <summary>
    /// Creates a column if it does not already exist in the specified table.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="column">The column to create.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether the column was created.</returns>
    /// <exception cref="ArgumentException">Thrown when the table name or column name is null or whitespace.</exception>
    public override async Task<bool> CreateColumnIfNotExistsAsync(
        IDbConnection db,
        DmColumn column,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(column.TableName))
        {
            throw new ArgumentException("Table name is required", nameof(column));
        }

        if (string.IsNullOrWhiteSpace(column.ColumnName))
        {
            throw new ArgumentException("Column name is required", nameof(column));
        }

        var (_, tableName, columnName) = NormalizeNames(
            column.SchemaName,
            column.TableName,
            column.ColumnName
        );

        return await AlterTableUsingRecreateTableStrategyAsync(
                db,
                DefaultSchema,
                tableName,
                table =>
                {
                    return table.Columns.All(x =>
                        !x.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase)
                    );
                },
                table =>
                {
                    table.Columns.Add(column);
                    return table;
                },
                tx: tx,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Drops a column if it exists in the specified table.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether the column was dropped.</returns>
    public override async Task<bool> DropColumnIfExistsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await AlterTableUsingRecreateTableStrategyAsync(
                db,
                schemaName,
                tableName,
                table =>
                {
                    return table.Columns.Any(x =>
                        x.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase)
                    );
                },
                table =>
                {
                    table.Columns.RemoveAll(c =>
                        c.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase)
                    );
                    return table;
                },
                tx: tx,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
    }
}
