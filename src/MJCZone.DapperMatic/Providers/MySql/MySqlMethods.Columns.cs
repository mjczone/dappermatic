// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;

namespace MJCZone.DapperMatic.Providers.MySql;

public partial class MySqlMethods
{
    /// <summary>
    /// Renames a column if it exists in the specified table using version-appropriate MySQL syntax.
    /// MySQL 8.0+ uses RENAME COLUMN, MySQL 5.7 and earlier uses CHANGE syntax.
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

        var schemaQualifiedTableName = GetSchemaQualifiedIdentifierName(schemaName, tableName);

        // Check MySQL version to determine syntax
        var version = await GetDatabaseVersionAsync(db, tx, cancellationToken).ConfigureAwait(false);

        if (version >= new Version(8, 0, 0))
        {
            // Use modern RENAME COLUMN syntax for MySQL 8.0+
            await ExecuteAsync(
                    db,
                    $"""
                    ALTER TABLE {schemaQualifiedTableName}
                                        RENAME COLUMN {GetQuotedIdentifier(columnName)}
                                                TO {GetQuotedIdentifier(newColumnName)}
                    """,
                    tx: tx,
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);
        }
        else
        {
            // Use legacy CHANGE syntax for MySQL 5.7 and earlier
            var columnDefinition = await GetCompleteColumnDefinitionAsync(
                    db,
                    tableName,
                    columnName,
                    tx,
                    cancellationToken
                )
                .ConfigureAwait(false);

            await ExecuteAsync(
                    db,
                    $"""
                    ALTER TABLE {schemaQualifiedTableName}
                                        CHANGE {GetQuotedIdentifier(columnName)} {GetQuotedIdentifier(newColumnName)} {columnDefinition}
                    """,
                    tx: tx,
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>
    /// Retrieves the complete column definition for MySQL CHANGE syntax.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The complete column definition string.</returns>
    private async Task<string> GetCompleteColumnDefinitionAsync(
        IDbConnection db,
        string tableName,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        const string sql = """
            SELECT
                c.COLUMN_TYPE,
                c.IS_NULLABLE,
                c.COLUMN_DEFAULT,
                c.EXTRA,
                c.COLUMN_COMMENT
            FROM INFORMATION_SCHEMA.COLUMNS c
            WHERE c.TABLE_SCHEMA = DATABASE()
              AND c.TABLE_NAME = @tableName
              AND c.COLUMN_NAME = @columnName
            """;

        var results = await QueryAsync<(
            string ColumnType,
            string IsNullable,
            string? ColumnDefault,
            string? Extra,
            string? ColumnComment
        )>(db, sql, new { tableName, columnName }, tx: tx, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var result = results.FirstOrDefault();
        if (result == default)
        {
            throw new InvalidOperationException($"Column '{columnName}' not found in table '{tableName}'");
        }

        var definition = result.ColumnType; // e.g., "varchar(255)", "int(11)", etc.

        // Add NULL/NOT NULL
        if (result.IsNullable == "NO")
        {
            definition += " NOT NULL";
        }

        // Add DEFAULT value
        if (!string.IsNullOrEmpty(result.ColumnDefault))
        {
            definition += $" DEFAULT {result.ColumnDefault}";
        }

        // Add AUTO_INCREMENT
        if (result.Extra?.Contains("auto_increment", StringComparison.OrdinalIgnoreCase) == true)
        {
            definition += " AUTO_INCREMENT";
        }

        // Add COMMENT
        if (!string.IsNullOrEmpty(result.ColumnComment))
        {
            string escapedComment = result.ColumnComment.Replace("'", "''", StringComparison.Ordinal);
            definition += $" COMMENT '{escapedComment}'";
        }

        return definition;
    }
}