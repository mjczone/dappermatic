// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using MJCZone.DapperMatic.Models;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.Providers.MySql;

public partial class MySqlMethods
{
    #region Schema Strings
    #endregion // Schema Strings

    #region Table Strings

    /// <summary>
    /// Generates the SQL string for inline column name and type.
    /// </summary>
    /// <param name="column">The column definition.</param>
    /// <param name="dbVersion">The database version.</param>
    /// <returns>The SQL string for inline column name and type.</returns>
    protected override string SqlInlineColumnNameAndType(DmColumn column, Version dbVersion)
    {
        var nameAndType = base.SqlInlineColumnNameAndType(column, dbVersion);

        if (
            !nameAndType.Contains(" varchar", StringComparison.OrdinalIgnoreCase)
            && !nameAndType.Contains(" text", StringComparison.OrdinalIgnoreCase)
        )
        {
            return nameAndType;
        }

        var doNotAddUtf8Mb4 =
            dbVersion < new Version(5, 5, 3)
            // do not include MariaDb here
            || dbVersion.Major == 10
            || dbVersion.Major == 11;
        // || (dbVersion.Major == 10 && dbVersion < new Version(10, 5, 25));

        if (!doNotAddUtf8Mb4 && column.IsUnicode)
        {
            // make it unicode by default
            nameAndType += " CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";
        }

        return nameAndType;
    }

    /// <summary>
    /// Generates the SQL string for inline primary key column constraint.
    /// </summary>
    /// <param name="column">The column definition.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="useTableConstraint">Indicates whether to use table constraint.</param>
    /// <returns>The SQL string for inline primary key column constraint.</returns>
    protected override string SqlInlinePrimaryKeyColumnConstraint(
        DmColumn column,
        string constraintName,
        out bool useTableConstraint
    )
    {
        useTableConstraint = true;
        return column.IsAutoIncrement ? "AUTO_INCREMENT" : string.Empty;

        // the following code doesn't work because MySQL doesn't allow named constraints in the column definition
        // return $"CONSTRAINT {NormalizeName(constraintName)} {(column.IsAutoIncrement ? $"{SqlInlinePrimaryKeyAutoIncrementColumnConstraint(column)} " : string.Empty)}PRIMARY KEY".Trim();
    }

    /// <summary>
    /// Generates the SQL string for inline primary key auto-increment column constraint.
    /// </summary>
    /// <param name="column">The column definition.</param>
    /// <returns>The SQL string for inline primary key auto-increment column constraint.</returns>
    protected override string SqlInlinePrimaryKeyAutoIncrementColumnConstraint(DmColumn column)
    {
        return "AUTO_INCREMENT";
    }

    /// <summary>
    /// Generates the SQL string for inline default column constraint.
    /// </summary>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="defaultExpression">The default expression.</param>
    /// <returns>The SQL string for inline default column constraint.</returns>
    protected override string SqlInlineDefaultColumnConstraint(
        string constraintName,
        string defaultExpression
    )
    {
        SqlExpressionValidator.ValidateDefaultExpression(defaultExpression, nameof(defaultExpression));

        defaultExpression = defaultExpression.Trim();
        var addParentheses =
            defaultExpression.Contains(' ', StringComparison.OrdinalIgnoreCase)
            && !(defaultExpression.StartsWith('(') && defaultExpression.EndsWith(')'))
            && !(defaultExpression.StartsWith('"') && defaultExpression.EndsWith('"'))
            && !(defaultExpression.StartsWith('\'') && defaultExpression.EndsWith('\''));

        return $"DEFAULT {(addParentheses ? $"({defaultExpression})" : defaultExpression)}";
    }

    /// <summary>
    /// Generates the SQL string for inline check column constraint.
    /// </summary>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="checkExpression">The check expression.</param>
    /// <param name="useTableConstraint">Indicates whether to use table constraint.</param>
    /// <returns>The SQL string for inline check column constraint.</returns>
    protected override string SqlInlineCheckColumnConstraint(
        string constraintName,
        string checkExpression,
        out bool useTableConstraint
    )
    {
        useTableConstraint = true;
        return string.Empty;
    }

    /// <summary>
    /// Generates the SQL string for inline unique column constraint.
    /// </summary>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="useTableConstraint">Indicates whether to use table constraint.</param>
    /// <returns>The SQL string for inline unique column constraint.</returns>
    protected override string SqlInlineUniqueColumnConstraint(
        string constraintName,
        out bool useTableConstraint
    )
    {
        useTableConstraint = true;
        return string.Empty;
    }

    /// <summary>
    /// Generates the SQL string for inline foreign key column constraint.
    /// </summary>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="referencedTableName">The referenced table name.</param>
    /// <param name="referencedColumn">The referenced column.</param>
    /// <param name="onDelete">The action on delete.</param>
    /// <param name="onUpdate">The action on update.</param>
    /// <param name="useTableConstraint">Indicates whether to use table constraint.</param>
    /// <returns>The SQL string for inline foreign key column constraint.</returns>
    protected override string SqlInlineForeignKeyColumnConstraint(
        string? schemaName,
        string constraintName,
        string referencedTableName,
        DmOrderedColumn referencedColumn,
        DmForeignKeyAction? onDelete,
        DmForeignKeyAction? onUpdate,
        out bool useTableConstraint
    )
    {
        useTableConstraint = true;
        return string.Empty;
    }

    /// <summary>
    /// Generates the SQL string to check if a table exists.
    /// </summary>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <returns>The SQL string and parameters to check if a table exists.</returns>
    protected override (string sql, object parameters) SqlDoesTableExist(
        string? schemaName,
        string tableName
    )
    {
        const string sql = """
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE'
                and TABLE_SCHEMA = DATABASE()
                and TABLE_NAME = @tableName
            """;

        return (
            sql,
            new
            {
                schemaName = NormalizeSchemaName(schemaName),
                tableName = NormalizeName(tableName),
            }
        );
    }

    /// <summary>
    /// Generates the SQL string to get table names.
    /// </summary>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableNameFilter">The table name filter.</param>
    /// <returns>The SQL string and parameters to get table names.</returns>
    protected override (string sql, object parameters) SqlGetTableNames(
        string? schemaName,
        string? tableNameFilter = null
    )
    {
        var where = string.IsNullOrWhiteSpace(tableNameFilter)
            ? string.Empty
            : ToLikeString(tableNameFilter);

        var sql = $"""
            SELECT TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE
                TABLE_TYPE = 'BASE TABLE'
                AND TABLE_SCHEMA = DATABASE()
                {(string.IsNullOrWhiteSpace(where) ? null : " AND TABLE_NAME LIKE @where")}
            ORDER BY TABLE_NAME
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
    /// Generates the SQL string to add a default constraint to a table.
    /// </summary>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="expression">The default expression.</param>
    /// <returns>The SQL string to add a default constraint to a table.</returns>
    protected override string SqlAlterTableAddDefaultConstraint(
        string? schemaName,
        string tableName,
        string columnName,
        string constraintName,
        string expression
    )
    {
        var schemaQualifiedTableName = GetSchemaQualifiedIdentifierName(schemaName, tableName);

        var defaultExpression = expression.Trim();
        var addParentheses =
            defaultExpression.Contains(' ', StringComparison.OrdinalIgnoreCase)
            && !(defaultExpression.StartsWith('(') && defaultExpression.EndsWith(')'))
            && !(defaultExpression.StartsWith('"') && defaultExpression.EndsWith('"'))
            && !(defaultExpression.StartsWith('\'') && defaultExpression.EndsWith('\''));

        return $"""

                        ALTER TABLE {schemaQualifiedTableName}
                            ALTER COLUMN {NormalizeName(columnName)} SET DEFAULT {(
                                addParentheses ? $"({defaultExpression})" : defaultExpression
                            )}
            """;
    }

    /// <summary>
    /// Generates the SQL string to drop a default constraint from a table.
    /// </summary>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <returns>The SQL string to drop a default constraint from a table.</returns>
    protected override string SqlDropDefaultConstraint(
        string? schemaName,
        string tableName,
        string columnName,
        string constraintName
    )
    {
        return $"ALTER TABLE {GetSchemaQualifiedIdentifierName(schemaName, tableName)} ALTER COLUMN {NormalizeName(columnName)} DROP DEFAULT";
    }
    #endregion // Default Constraint Strings

    #region Primary Key Strings

    /// <summary>
    /// Generates the SQL string to drop a primary key constraint from a table.
    /// </summary>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <returns>The SQL string to drop a primary key constraint from a table.</returns>
    protected override string SqlDropPrimaryKeyConstraint(
        string? schemaName,
        string tableName,
        string constraintName
    )
    {
        return $"ALTER TABLE {GetSchemaQualifiedIdentifierName(schemaName, tableName)} DROP PRIMARY KEY";
    }
    #endregion // Primary Key Strings

    #region Unique Constraint Strings

    /// <summary>
    /// Generates the SQL string to drop a unique constraint from a table.
    /// </summary>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <returns>The SQL string to drop a unique constraint from a table.</returns>
    protected override string SqlDropUniqueConstraint(
        string? schemaName,
        string tableName,
        string constraintName
    )
    {
        return $"ALTER TABLE {GetSchemaQualifiedIdentifierName(schemaName, tableName)} DROP INDEX {NormalizeName(constraintName)}";
    }
    #endregion // Unique Constraint Strings

    #region Foreign Key Constraint Strings

    /// <summary>
    /// Generates the SQL string to drop a foreign key constraint from a table.
    /// </summary>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <returns>The SQL string to drop a foreign key constraint from a table.</returns>
    protected override string SqlDropForeignKeyConstraint(
        string? schemaName,
        string tableName,
        string constraintName
    )
    {
        return $"ALTER TABLE {GetSchemaQualifiedIdentifierName(schemaName, tableName)} DROP FOREIGN KEY {NormalizeName(constraintName)}";
    }
    #endregion // Foreign Key Constraint Strings

    #region Index Strings
    #endregion // Index Strings

    #region View Strings

    /// <summary>
    /// Generates the SQL string to get view names.
    /// </summary>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="viewNameFilter">The view name filter.</param>
    /// <returns>The SQL string and parameters to get view names.</returns>
    protected override (string sql, object parameters) SqlGetViewNames(
        string? schemaName,
        string? viewNameFilter = null
    )
    {
        var where = string.IsNullOrWhiteSpace(viewNameFilter)
            ? string.Empty
            : ToLikeString(viewNameFilter);

        var sql = $"""
            SELECT
                                TABLE_NAME AS ViewName
                            FROM
                                INFORMATION_SCHEMA.VIEWS
                            WHERE
                                VIEW_DEFINITION IS NOT NULL
                                AND TABLE_SCHEMA = DATABASE()
                                {(
                string.IsNullOrWhiteSpace(where) ? string.Empty : " AND TABLE_NAME LIKE @where"
            )}
                            ORDER BY
                                TABLE_SCHEMA, TABLE_NAME
            """;

        return (sql, new { schemaName = NormalizeSchemaName(schemaName), where });
    }

    /// <summary>
    /// Generates the SQL string to get views.
    /// </summary>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="viewNameFilter">The view name filter.</param>
    /// <returns>The SQL string and parameters to get views.</returns>
    protected override (string sql, object parameters) SqlGetViews(
        string? schemaName,
        string? viewNameFilter
    )
    {
        var where = string.IsNullOrWhiteSpace(viewNameFilter)
            ? string.Empty
            : ToLikeString(viewNameFilter);

        var sql = $"""
            SELECT
                                NULL AS SchemaName,
                                TABLE_NAME AS ViewName,
                                VIEW_DEFINITION AS Definition
                            FROM
                                INFORMATION_SCHEMA.VIEWS
                            WHERE
                                VIEW_DEFINITION IS NOT NULL
                                AND TABLE_SCHEMA = DATABASE()
                                {(
                string.IsNullOrWhiteSpace(where) ? string.Empty : "AND TABLE_NAME LIKE @where"
            )}
                            ORDER BY
                                TABLE_SCHEMA, TABLE_NAME
            """;

        return (sql, new { schemaName = NormalizeSchemaName(schemaName), where });
    }
    #endregion // View Strings
}
