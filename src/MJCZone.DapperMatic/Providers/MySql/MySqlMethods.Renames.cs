// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Text.RegularExpressions;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Providers.MySql;

public partial class MySqlMethods
{
    /// <inheritdoc/>
    protected override async Task RenameIndexAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string indexName,
        string newIndexName,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    )
    {
        await ExecuteAsync(
                db,
                $"ALTER TABLE {GetSchemaQualifiedIdentifierName(schemaName, tableName)} RENAME INDEX {QuoteName(indexName)} TO {QuoteName(newIndexName)}",
                tx: tx,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <remarks>A unique constraint is a unique index on MySQL and MariaDB, so it is renamed as one.</remarks>
    protected override Task RenameUniqueConstraintAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string constraintName,
        string newConstraintName,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    ) => RenameIndexAsync(db, schemaName, tableName, constraintName, newConstraintName, tx, cancellationToken);

    /// <inheritdoc/>
    /// <remarks>
    /// Neither MySQL nor MariaDB can rename a check constraint, so it is dropped and added back with the
    /// expression from SHOW CREATE TABLE, in one ALTER TABLE statement.
    /// </remarks>
    protected override async Task RenameCheckConstraintAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string constraintName,
        string newConstraintName,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    )
    {
        var expression = (
            await GetCheckConstraintClausesAsync(db, tableName, tx, cancellationToken).ConfigureAwait(false)
        )
            .FirstOrDefault(c => c.Name.Equals(NormalizeName(constraintName), StringComparison.OrdinalIgnoreCase))
            .Clause;

        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new InvalidOperationException(
                $"The expression of check constraint '{constraintName}' on '{tableName}' could not be read."
            );
        }

        await ExecuteAsync(
                db,
                $"""
                ALTER TABLE {GetSchemaQualifiedIdentifierName(schemaName, tableName)}
                    DROP CONSTRAINT {QuoteName(constraintName)},
                    ADD CONSTRAINT {QuoteName(newConstraintName)} CHECK {expression}
                """,
                tx: tx,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Neither MySQL nor MariaDB can rename a foreign key, so it is dropped and added back with the same
    /// definition, in one ALTER TABLE statement. The index that backs it is left as it is.
    /// </remarks>
    protected override async Task RenameForeignKeyConstraintAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string constraintName,
        string newConstraintName,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    )
    {
        var fk =
            await GetForeignKeyConstraintAsync(db, schemaName, tableName, constraintName, tx, cancellationToken)
                .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Foreign key '{constraintName}' on '{tableName}' could not be read."
            );

        var columns = string.Join(", ", fk.SourceColumns.Select(c => QuoteName(c.ColumnName)));
        var referencedColumns = string.Join(", ", fk.ReferencedColumns.Select(c => QuoteName(c.ColumnName)));

        await ExecuteAsync(
                db,
                $"""
                ALTER TABLE {GetSchemaQualifiedIdentifierName(schemaName, tableName)}
                    DROP FOREIGN KEY {QuoteName(constraintName)},
                    ADD CONSTRAINT {QuoteName(newConstraintName)}
                        FOREIGN KEY ({columns})
                        REFERENCES {GetSchemaQualifiedIdentifierName(schemaName, fk.ReferencedTableName)} ({referencedColumns})
                        ON DELETE {fk.OnDelete.ToSql()}
                        ON UPDATE {fk.OnUpdate.ToSql()}
                """,
                tx: tx,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Returns each check constraint on a table with its expression, exactly as SHOW CREATE TABLE renders it.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Each constraint's name and its parenthesised expression.</returns>
    /// <remarks>
    /// INFORMATION_SCHEMA.CHECK_CONSTRAINTS is no use for re-creating a check: MySQL reports string literals in it
    /// with backslash-escaped quotes (<c>_utf8mb4\'\'</c>), which is not valid SQL to execute. SHOW CREATE TABLE
    /// renders the check as it would have to be written.
    /// </remarks>
    private async Task<List<(string Name, string Clause)>> GetCheckConstraintClausesAsync(
        IDbConnection db,
        string tableName,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    )
    {
        var createTable = (
            await QueryAsync<(string Table, string CreateTable)>(
                    db,
                    $"SHOW CREATE TABLE {GetQuotedIdentifier(NormalizeName(tableName))}",
                    tx: tx,
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false)
        )
            .FirstOrDefault()
            .CreateTable;

        var checks = new List<(string Name, string Clause)>();
        foreach (var line in (createTable ?? string.Empty).Split('\n'))
        {
            var match = CheckConstraintLine().Match(line.Trim().TrimEnd(','));
            if (match.Success)
            {
                checks.Add((match.Groups["name"].Value.Replace("``", "`", StringComparison.Ordinal), match.Groups["clause"].Value.Trim()));
            }
        }

        return checks;
    }

    [GeneratedRegex(@"^CONSTRAINT\s+`(?<name>(?:[^`]|``)+)`\s+CHECK\s+(?<clause>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex CheckConstraintLine();
}
