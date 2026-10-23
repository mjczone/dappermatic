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
            !await DoesColumnExistAsync(db, schemaName, tableName, columnName, tx, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            return false;
        }

        // Check if a column with the new name already exists
        if (
            await DoesColumnExistAsync(db, schemaName, tableName, newColumnName, tx, cancellationToken)
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

        // SQL Server refuses to rename a column that a CHECK constraint uses ("participates in enforced
        // dependencies"). Those checks are dropped, the column renamed, and the checks added back over the new
        // name, in one transaction. SQL Server stores check definitions with bracketed column names, so the
        // column is found -- and replaced -- as [name].
        var checks = await QueryAsync<(string name, string definition)>(
                db,
                """
                SELECT cc.name, cc.definition
                FROM sys.check_constraints cc
                WHERE cc.parent_object_id = OBJECT_ID(@table) AND CHARINDEX(QUOTENAME(@column), cc.definition) > 0
                """,
                new { table = schemaQualifiedTableName, column = columnName },
                tx: tx,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);

        if (checks.Count > 0 && db.State != ConnectionState.Open)
        {
            db.Open();
        }

        var ownTx = checks.Count > 0 && tx is null ? db.BeginTransaction() : null;
        var activeTx = tx ?? ownTx;

        try
        {
            foreach (var check in checks)
            {
                await ExecuteAsync(
                        db,
                        $"ALTER TABLE {schemaQualifiedTableName} DROP CONSTRAINT {GetQuotedIdentifier(check.name)}",
                        tx: activeTx,
                        cancellationToken: cancellationToken
                    )
                    .ConfigureAwait(false);
            }

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
                    tx: activeTx,
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);

            foreach (var check in checks)
            {
                var definition = check.definition.Replace(
                    $"[{columnName}]",
                    $"[{newColumnName}]",
                    StringComparison.OrdinalIgnoreCase
                );

                await ExecuteAsync(
                        db,
                        $"ALTER TABLE {schemaQualifiedTableName} WITH CHECK ADD CONSTRAINT {GetQuotedIdentifier(check.name)} CHECK {definition}",
                        tx: activeTx,
                        cancellationToken: cancellationToken
                    )
                    .ConfigureAwait(false);
            }

            ownTx?.Commit();
        }
        catch
        {
            ownTx?.Rollback();
            throw;
        }
        finally
        {
            ownTx?.Dispose();
        }

        return true;
    }

    /// <summary>
    /// Changes the data type and nullability of a column if it exists, keeping its collation.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="sqlType">The new SQL data type, including any length, precision and scale.</param>
    /// <param name="isNullable">Whether the column allows nulls afterwards.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the column exists and now has the given type and nullability, otherwise false.</returns>
    /// <remarks>
    /// ALTER COLUMN without a COLLATE clause gives a character column the database's default collation,
    /// silently, so the column's current collation is restated. Defaults survive ALTER COLUMN on their own.
    /// </remarks>
    public override async Task<bool> AlterColumnIfExistsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        string sqlType,
        bool isNullable,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sqlType);

        (schemaName, tableName, columnName) = NormalizeNames(schemaName, tableName, columnName);
        var schemaQualifiedTableName = GetSchemaQualifiedIdentifierName(schemaName, tableName);

        // One query answers both whether the column exists and which collation it carries.
        var collations = await QueryAsync<string?>(
                db,
                """
                SELECT c.collation_name
                FROM sys.columns c
                WHERE c.object_id = OBJECT_ID(@table) AND c.name = @column
                """,
                new { table = schemaQualifiedTableName, column = columnName },
                tx: tx,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);

        if (collations.Count == 0)
        {
            return false;
        }

        var collation = collations[0];
        var baseType = sqlType.DiscardLengthPrecisionAndScaleFromSqlTypeName();
        var isCharacterType =
            baseType.Contains("char", StringComparison.OrdinalIgnoreCase)
            || baseType.Contains("text", StringComparison.OrdinalIgnoreCase);

        var collateClause = isCharacterType && !string.IsNullOrWhiteSpace(collation) ? $" COLLATE {collation}" : string.Empty;

        await ExecuteAsync(
                db,
                $"ALTER TABLE {schemaQualifiedTableName} ALTER COLUMN {GetQuotedIdentifier(columnName)} {sqlType}{collateClause} {(isNullable ? "NULL" : "NOT NULL")}",
                tx: tx,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);

        return true;
    }
}
