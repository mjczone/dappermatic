// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;

namespace MJCZone.DapperMatic.Providers.PostgreSql;

public partial class PostgreSqlMethods
{
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
    /// PostgreSQL alters the type and the nullability with separate clauses, so only the parts that change are
    /// issued. That matters beyond tidiness: ALTER COLUMN ... TYPE is refused while a view depends on the
    /// column, even when the type is unchanged, whereas changing only the nullability is not.
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

        // The collation is reported only when it differs from the type's default, which is the only case where
        // it has to be restated: ALTER COLUMN ... TYPE without COLLATE gives the column the new type's default.
        var current = await QueryAsync<(string data_type, bool not_null, string? collation)>(
                db,
                """
                SELECT
                    format_type(a.atttypid, a.atttypmod) AS data_type,
                    a.attnotnull AS not_null,
                    CASE WHEN a.attcollation <> 0 AND a.attcollation <> t.typcollation
                         THEN quote_ident(n.nspname) || '.' || quote_ident(c.collname)
                    END AS collation
                FROM pg_attribute a
                JOIN pg_type t ON t.oid = a.atttypid
                LEFT JOIN pg_collation c ON c.oid = a.attcollation
                LEFT JOIN pg_namespace n ON n.oid = c.collnamespace
                WHERE a.attrelid = to_regclass(@table)
                  AND a.attname = @column
                  AND a.attnum > 0
                  AND NOT a.attisdropped
                """,
                new { table = schemaQualifiedTableName, column = columnName },
                tx: tx,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);

        if (current.Count == 0)
        {
            return false;
        }

        var quotedColumnName = GetQuotedIdentifier(columnName);
        var clauses = new List<string>();

        if (!IsSameSqlType(current[0].data_type, sqlType))
        {
            var baseType = sqlType.DiscardLengthPrecisionAndScaleFromSqlTypeName();
            var isCharacterType =
                baseType.Contains("char", StringComparison.OrdinalIgnoreCase)
                || baseType.Contains("text", StringComparison.OrdinalIgnoreCase);

            var collateClause =
                isCharacterType && !string.IsNullOrWhiteSpace(current[0].collation)
                    ? $" COLLATE {current[0].collation}"
                    : string.Empty;

            clauses.Add($"ALTER COLUMN {quotedColumnName} TYPE {sqlType}{collateClause}");
        }

        if (current[0].not_null == isNullable)
        {
            clauses.Add($"ALTER COLUMN {quotedColumnName} {(isNullable ? "DROP" : "SET")} NOT NULL");
        }

        if (clauses.Count > 0)
        {
            await ExecuteAsync(
                    db,
                    $"ALTER TABLE {schemaQualifiedTableName} {string.Join(", ", clauses)}",
                    tx: tx,
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>
    /// Compares two type names as PostgreSQL's format_type renders them, ignoring case and spacing.
    /// </summary>
    /// <param name="left">The first type name.</param>
    /// <param name="right">The second type name.</param>
    /// <returns>True if the names are the same.</returns>
    private static bool IsSameSqlType(string left, string right)
    {
        static string Normalize(string type) =>
            string.Join(' ', type.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Replace(" (", "(", StringComparison.Ordinal)
                .Replace(", ", ",", StringComparison.Ordinal);

        return Normalize(left).Equals(Normalize(right), StringComparison.OrdinalIgnoreCase);
    }
}
