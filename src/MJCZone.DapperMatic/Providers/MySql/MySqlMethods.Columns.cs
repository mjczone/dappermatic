// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Text;
using System.Text.RegularExpressions;

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

        var schemaQualifiedTableName = GetSchemaQualifiedIdentifierName(schemaName, tableName);

        // Check MySQL version to determine syntax
        var version = await GetDatabaseVersionAsync(db, tx, cancellationToken).ConfigureAwait(false);

        if (version >= new Version(8, 0, 0))
        {
            // MySQL refuses to rename a column that a check constraint uses (error 3959), so those checks are
            // dropped and added back over the new name, in the same ALTER TABLE statement as the rename. MySQL
            // stores check expressions with backquoted column names, so the column is found -- and replaced --
            // as `name`. MariaDB renames such a column itself and rewrites the check, so it needs none of this.
            var versionText =
                await ExecuteScalarAsync<string>(db, "SELECT VERSION()", tx: tx, cancellationToken: cancellationToken)
                    .ConfigureAwait(false) ?? string.Empty;
            var isMariaDb = versionText.Contains("MariaDB", StringComparison.OrdinalIgnoreCase);

            var checks =
                !isMariaDb && version >= new Version(8, 0, 16)
                    ? (await GetCheckConstraintClausesAsync(db, tableName, tx, cancellationToken).ConfigureAwait(false))
                        .Where(c => c.Clause.Contains($"`{columnName}`", StringComparison.OrdinalIgnoreCase))
                        .ToList()
                    : [];

            var clauses = new List<string>();
            clauses.AddRange(checks.Select(c => $"DROP CHECK {GetQuotedIdentifier(c.Name)}"));
            clauses.Add($"RENAME COLUMN {GetQuotedIdentifier(columnName)} TO {GetQuotedIdentifier(newColumnName)}");
            clauses.AddRange(
                checks.Select(c =>
                    $"ADD CONSTRAINT {GetQuotedIdentifier(c.Name)} CHECK {c.Clause.Replace($"`{columnName}`", $"`{newColumnName}`", StringComparison.OrdinalIgnoreCase)}"
                )
            );

            await ExecuteAsync(
                    db,
                    $"ALTER TABLE {schemaQualifiedTableName} {string.Join(", ", clauses)}",
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
                                        CHANGE {GetQuotedIdentifier(
                        columnName
                    )} {GetQuotedIdentifier(newColumnName)} {columnDefinition}
                    """,
                    tx: tx,
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>
    /// Changes the data type and nullability of a column if it exists, keeping the rest of its definition.
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
    /// MODIFY COLUMN replaces the whole column definition, so anything it does not restate is lost. The new
    /// type and nullability are therefore spliced into the column's own definition from SHOW CREATE TABLE,
    /// which keeps its character set, collation, default, ON UPDATE, comment and generated expression.
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

        var exists = await ExecuteScalarAsync<long>(
                db,
                """
                SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName AND COLUMN_NAME = @columnName
                """,
                new { tableName, columnName },
                tx: tx,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);

        if (exists == 0)
        {
            return false;
        }

        var definition = await GetCompleteColumnDefinitionAsync(db, tableName, columnName, tx, cancellationToken)
            .ConfigureAwait(false);

        await ExecuteAsync(
                db,
                $"""
                ALTER TABLE {GetSchemaQualifiedIdentifierName(schemaName, tableName)}
                    MODIFY COLUMN {GetQuotedIdentifier(columnName)} {RestateColumnDefinition(definition, sqlType, isNullable)}
                """,
                tx: tx,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Replaces the data type and nullability in a column definition as SHOW CREATE TABLE renders it, and
    /// leaves every other clause exactly as it was.
    /// </summary>
    /// <param name="definition">The column definition, without the column name.</param>
    /// <param name="sqlType">The new SQL data type.</param>
    /// <param name="isNullable">Whether the column allows nulls.</param>
    /// <returns>The restated definition.</returns>
    /// <remarks>
    /// Clauses are found in a copy with every quoted string and parenthesised expression blanked out, so a
    /// comment, default or generated expression that happens to contain NULL is never mistaken for the
    /// column's nullability. Positions in that copy line up with the original.
    /// </remarks>
    internal static string RestateColumnDefinition(string definition, string sqlType, bool isNullable)
    {
        var text = definition.Trim();

        // The type runs from the start through any length and numeric modifiers:
        // "varchar(50)", "decimal(10,2) unsigned zerofill".
        var type = Regex.Match(
            MaskQuotedAndParenthesised(text),
            @"^[A-Za-z][A-Za-z0-9_]*(\s*\([^)]*\))?(\s+(unsigned|signed|zerofill)\b)*(?=\s|$)",
            RegexOptions.IgnoreCase
        );
        if (!type.Success)
        {
            throw new InvalidOperationException($"Cannot find the data type in the column definition '{definition}'.");
        }

        // Drop the existing nullability. DEFAULT NULL is a default, not nullability, so it stays -- unless the
        // column becomes NOT NULL, where that default is invalid.
        var nullability = Regex.Match(
            MaskQuotedAndParenthesised(text),
            @"(?<!\bDEFAULT)\s+(NOT\s+)?NULL\b",
            RegexOptions.IgnoreCase
        );
        if (nullability.Success)
        {
            text = text.Remove(nullability.Index, nullability.Length);
        }

        if (!isNullable)
        {
            var defaultNull = Regex.Match(MaskQuotedAndParenthesised(text), @"\s+DEFAULT\s+NULL\b", RegexOptions.IgnoreCase);
            if (defaultNull.Success)
            {
                text = text.Remove(defaultNull.Index, defaultNull.Length);
            }
        }

        // A column with no nullability clause already allows nulls, so NULL is only written where the
        // definition had one; MariaDB does not accept it on every generated column.
        if (!isNullable || nullability.Success)
        {
            // Nullability goes after the data type and its character set and collation, or after the storage
            // keyword of a generated column, which has to come first.
            var masked = MaskQuotedAndParenthesised(text);
            var generated = Regex.Match(masked, @"\b(VIRTUAL|STORED|PERSISTENT)\b", RegexOptions.IgnoreCase);
            var insertAt = generated.Success
                ? generated.Index + generated.Length
                : type.Length
                    + Regex
                        .Match(
                            masked[type.Length..],
                            @"^(\s+CHARACTER\s+SET\s+\S+)?(\s+COLLATE\s+\S+)?",
                            RegexOptions.IgnoreCase
                        )
                        .Length;

            text = text.Insert(insertAt, isNullable ? " NULL" : " NOT NULL");
        }

        // Nothing before the end of the type has moved, so it can be replaced last.
        return (sqlType.Trim() + text[type.Length..]).Trim();
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
    /// <remarks>
    /// The definition is taken verbatim from SHOW CREATE TABLE, which is MySQL's own rendering of the column.
    /// Rebuilding it from INFORMATION_SCHEMA instead loses whatever is not spelled out there -- character set
    /// and collation, a generated column's expression, ON UPDATE -- and INFORMATION_SCHEMA reports string
    /// defaults unquoted, so a rebuilt <c>DEFAULT abc</c> is not even valid SQL.
    /// </remarks>
    private async Task<string> GetCompleteColumnDefinitionAsync(
        IDbConnection db,
        string tableName,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var results = await QueryAsync<(string Table, string CreateTable)>(
                db,
                $"SHOW CREATE TABLE {GetQuotedIdentifier(tableName)}",
                tx: tx,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);

        var createTable = results.FirstOrDefault().CreateTable;
        if (string.IsNullOrWhiteSpace(createTable))
        {
            throw new InvalidOperationException($"Table '{tableName}' not found");
        }

        // Each column sits on its own line: "  `name` definition," -- with backticks in a name doubled.
        var quotedColumnName = $"`{columnName.Replace("`", "``", StringComparison.Ordinal)}`";

        foreach (var line in createTable.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith(quotedColumnName + " ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return trimmed[(quotedColumnName.Length + 1)..].TrimEnd(',').Trim();
        }

        throw new InvalidOperationException($"Column '{columnName}' not found in table '{tableName}'");
    }

    /// <summary>
    /// Returns a copy of a definition with the contents of quoted strings, quoted identifiers and parentheses
    /// replaced by underscores, keeping every character's position.
    /// </summary>
    /// <param name="definition">The column definition.</param>
    /// <returns>The masked copy.</returns>
    private static string MaskQuotedAndParenthesised(string definition)
    {
        var masked = definition.ToCharArray();
        char? quote = null;
        var depth = 0;

        for (var i = 0; i < definition.Length; i++)
        {
            var c = definition[i];

            if (quote is { } q)
            {
                masked[i] = '_';
                if (c == '\\' && q != '`' && i + 1 < definition.Length)
                {
                    masked[++i] = '_';
                }
                else if (c == q && i + 1 < definition.Length && definition[i + 1] == q)
                {
                    masked[++i] = '_';
                }
                else if (c == q)
                {
                    masked[i] = c;
                    quote = null;
                }

                continue;
            }

            if (c is '\'' or '"' or '`')
            {
                quote = c;
            }
            else if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth--;
            }
            else if (depth > 0)
            {
                masked[i] = '_';
            }
        }

        return new string(masked);
    }
}
