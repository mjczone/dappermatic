// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;

namespace MJCZone.DapperMatic.Providers.SqlServer;

public partial class SqlServerMethods
{
    /// <summary>
    /// Renames a column if it exists in the specified table using SQL Server's sp_rename stored procedure.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The current column name.</param>
    /// <param name="newColumnName">The new column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the column was renamed, otherwise false.</returns>
    public override async Task<bool> RenameColumnIfExistsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        string newColumnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        // Check if the column exists
        if (
            !await DoesColumnExistAsync(
                    db,
                    schemaName,
                    tableName,
                    columnName,
                    tx,
                    cancellationToken
                )
                .ConfigureAwait(false)
        )
        {
            return false;
        }

        // Check if a column with the new name already exists
        if (
            await DoesColumnExistAsync(
                    db,
                    schemaName,
                    tableName,
                    newColumnName,
                    tx,
                    cancellationToken
                )
                .ConfigureAwait(false)
        )
        {
            return false;
        }

        // Normalize names
        (schemaName, tableName, columnName) = NormalizeNames(schemaName, tableName, columnName);
        newColumnName = NormalizeName(newColumnName);

        // Build the full object name for sp_rename
        var schemaQualifiedTableName = GetSchemaQualifiedIdentifierName(schemaName, tableName);
        var fullColumnName = $"{schemaQualifiedTableName}.{GetQuotedIdentifier(columnName)}";

        // Use sp_rename to rename the column
        // sp_rename 'schema.table.oldcolumn', 'newcolumn', 'COLUMN'
        await ExecuteAsync(
                db,
                "sp_rename @objname, @newname, @objtype",
                new
                {
                    objname = fullColumnName,
                    newname = newColumnName,
                    objtype = "COLUMN",
                },
                tx: tx,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);

        return true;
    }
}
