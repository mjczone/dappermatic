// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.Models;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.Providers.Sqlite;

public partial class SqliteMethods
{
    #region Schema Strings
    #endregion // Schema Strings

    #region Table Strings

    /// <inheritdoc/>
    protected override string SqlInlineColumnNameAndType(DmColumn column, Version dbVersion)
    {
        // IF the column is an autoincrement column, the type MUST be INTEGER
        // https://www.sqlite.org/autoinc.html
        if (column.IsAutoIncrement)
        {
            column.SetProviderDataType(ProviderType, SqliteTypes.sql_integer);
        }

        return base.SqlInlineColumnNameAndType(column, dbVersion);
    }

    /// <inheritdoc/>
    protected override string SqlInlinePrimaryKeyAutoIncrementColumnConstraint(DmColumn column)
    {
        return "AUTOINCREMENT";
    }

    /// <inheritdoc/>
    protected override (string sql, object parameters) SqlDoesTableExist(string? schemaName, string tableName)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE
                type = 'table'
                AND name = @tableName
            """;

        return (sql, new { schemaName = NormalizeSchemaName(schemaName), tableName = NormalizeName(tableName) });
    }

    /// <inheritdoc/>
    protected override (string sql, object parameters) SqlGetTableNames(
        string? schemaName,
        string? tableNameFilter = null
    )
    {
        var where = string.IsNullOrWhiteSpace(tableNameFilter) ? string.Empty : ToLikeString(tableNameFilter);

        var sql = $"""

                            SELECT name
                            FROM sqlite_master
                            WHERE
                                type = 'table'
                                AND name NOT LIKE 'sqlite\_%' ESCAPE '\'
                                {(
                string.IsNullOrWhiteSpace(where) ? null : " AND name LIKE @where"
            )}
                            ORDER BY name
            """;

        return (sql, new { schemaName = NormalizeSchemaName(schemaName), where });
    }
    #endregion // Table Strings

    #region Column Strings
    #endregion // Column Strings

    #region Check Constraint Strings
    #endregion // Check Constraint Strings

    #region Default Constraint Strings

    /// <summary>
    /// Generates the SQL string for inline default column constraint.
    /// SQLite doesn't support named default constraints, so this returns only the DEFAULT clause.
    /// </summary>
    /// <param name="constraintName">The constraint name (ignored in SQLite).</param>
    /// <param name="defaultExpression">The default expression.</param>
    /// <returns>The SQL string for inline default column constraint.</returns>
    protected override string SqlInlineDefaultColumnConstraint(string constraintName, string defaultExpression)
    {
        SqlExpressionValidator.ValidateDefaultExpression(defaultExpression, nameof(defaultExpression));

        defaultExpression = defaultExpression.Trim();

        // SQLite requires parentheses around function calls in DEFAULT clauses
        var addParentheses =
            (defaultExpression.Contains(' ', StringComparison.OrdinalIgnoreCase) || IsFunctionCall(defaultExpression))
            && !(defaultExpression.StartsWith('(') && defaultExpression.EndsWith(')'))
            && !(defaultExpression.StartsWith('"') && defaultExpression.EndsWith('"'))
            && !(defaultExpression.StartsWith('\'') && defaultExpression.EndsWith('\''));

        return $"DEFAULT {(addParentheses ? $"({defaultExpression})" : defaultExpression)}";
    }
    #endregion // Default Constraint Strings

    #region Primary Key Strings
    #endregion // Primary Key Strings

    #region Unique Constraint Strings
    #endregion // Unique Constraint Strings

    #region Foreign Key Constraint Strings
    #endregion // Foreign Key Constraint Strings

    #region Index Strings

    /// <inheritdoc/>
    protected override string SqlDropIndex(string? schemaName, string tableName, string indexName)
    {
        return $"DROP INDEX {GetSchemaQualifiedIdentifierName(schemaName, indexName)}";
    }
    #endregion // Index Strings

    #region View Strings

    /// <inheritdoc/>
    protected override (string sql, object parameters) SqlGetViewNames(
        string? schemaName,
        string? viewNameFilter = null
    )
    {
        var where = string.IsNullOrWhiteSpace(viewNameFilter) ? string.Empty : ToLikeString(viewNameFilter);

        var sql = $"""

                            SELECT
                                m.name AS ViewName
                            FROM sqlite_master AS m
                            WHERE
                                m.TYPE = 'view'
                                AND m.name NOT LIKE 'sqlite\_%' ESCAPE '\'
                                {(
                string.IsNullOrWhiteSpace(where) ? string.Empty : " AND m.name LIKE @where"
            )}
                            ORDER BY
                                m.name
            """;

        return (sql, new { schemaName = NormalizeSchemaName(schemaName), where });
    }

    /// <inheritdoc/>
    protected override (string sql, object parameters) SqlGetViews(string? schemaName, string? viewNameFilter)
    {
        var where = string.IsNullOrWhiteSpace(viewNameFilter) ? string.Empty : ToLikeString(viewNameFilter);

        var sql = $"""

                            SELECT
                                NULL as SchemaName,
                                m.name AS ViewName,
                                m.SQL AS Definition
                            FROM sqlite_master AS m
                            WHERE
                                m.TYPE = 'view'
                                AND m.name NOT LIKE 'sqlite\_%' ESCAPE '\'
                                {(
                string.IsNullOrWhiteSpace(where) ? string.Empty : " AND m.name LIKE @where"
            )}
                            ORDER BY
                                m.name
            """;

        return (sql, new { schemaName = NormalizeSchemaName(schemaName), where });
    }

#pragma warning disable SA1201 // Elements should appear in the correct order
    private static readonly char[] WhiteSpaceCharacters = [' ', '\t', '\n', '\r'];
#pragma warning restore SA1201 // Elements should appear in the correct order

    /// <inheritdoc/>
    protected override string NormalizeViewDefinition(string definition)
    {
        definition = definition.Trim();

        // split the view by the first AS keyword surrounded by whitespace
        string? viewDefinition = null;
        for (var i = 0; i < definition.Length; i++)
        {
            if (
                i <= 0
                || definition[i] != 'A'
                || definition[i + 1] != 'S'
                || !WhiteSpaceCharacters.Contains(definition[i - 1])
                || !WhiteSpaceCharacters.Contains(definition[i + 2])
            )
            {
                continue;
            }

            viewDefinition = definition[(i + 3)..].Trim();
            break;
        }

        if (string.IsNullOrWhiteSpace(viewDefinition))
        {
            throw new InvalidDataException("Could not parse view definition: " + definition);
        }

        return viewDefinition;
    }
    #endregion // View Strings
}
