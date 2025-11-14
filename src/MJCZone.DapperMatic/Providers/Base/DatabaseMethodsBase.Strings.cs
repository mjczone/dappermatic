// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Text;
using MJCZone.DapperMatic.Models;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.Providers.Base;

public abstract partial class DatabaseMethodsBase
{
    #region Schema Strings

    /// <summary>
    /// Gets the SQL to create a schema.
    /// </summary>
    /// <param name="schemaName">The schema name.</param>
    /// <returns>The SQL to create the schema.</returns>
    protected virtual string SqlCreateSchema(string schemaName)
    {
        return $"CREATE SCHEMA {NormalizeSchemaName(schemaName)}";
    }

    /// <summary>
    /// Gets the SQL to check if a schema exists.
    /// </summary>
    /// <param name="schemaNameFilter">The schema name.</param>
    /// <returns>The SQL to check if the schema exists.</returns>
    protected virtual (string sql, object parameters) SqlGetSchemaNames(string? schemaNameFilter = null)
    {
        var where = string.IsNullOrWhiteSpace(schemaNameFilter) ? string.Empty : ToLikeString(schemaNameFilter);

        var sql = $"""

                        SELECT SCHEMA_NAME
                        FROM INFORMATION_SCHEMA.SCHEMATA
                        {(
                string.IsNullOrWhiteSpace(where) ? string.Empty : "WHERE SCHEMA_NAME LIKE @where"
            )}
                        ORDER BY SCHEMA_NAME
            """;

        return (sql, new { where });
    }

    /// <summary>
    /// Gets the SQL to drop a schema.
    /// </summary>
    /// <param name="schemaName">The schema name.</param>
    /// <returns>The SQL to drop the schema.</returns>
    protected virtual string SqlDropSchema(string schemaName)
    {
        return $"DROP SCHEMA {NormalizeSchemaName(schemaName)}";
    }
    #endregion // Schema Strings

    #region Table Strings

    /// <summary>
    /// Gets the SQL to create a table.
    /// </summary>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <returns>The SQL to create the table.</returns>
    protected virtual (string sql, object parameters) SqlDoesTableExist(string? schemaName, string tableName)
    {
        var sql = $"""

                        SELECT COUNT(*)
                        FROM INFORMATION_SCHEMA.TABLES
                        WHERE
                            TABLE_TYPE='BASE TABLE'
                            {(
                string.IsNullOrWhiteSpace(schemaName) ? string.Empty : " AND TABLE_SCHEMA = @schemaName"
            )}
                            AND TABLE_NAME = @tableName
            """;

        return (sql, new { schemaName = NormalizeSchemaName(schemaName), tableName = NormalizeName(tableName) });
    }

    /// <summary>
    /// Generates an SQL query and parameters to retrieve table names based on the provided schema name and optional table name filter.
    /// </summary>
    /// <param name="schemaName">The name of the schema. If null, uses the default schema.</param>
    /// <param name="tableNameFilter">An optional filter for the table names. If not null, only tables with a name starting with this filter will be returned.</param>
    /// <returns>A tuple containing the SQL query string and an object representing the parameters to pass in the command.</returns>
    protected virtual (string sql, object parameters) SqlGetTableNames(
        string? schemaName,
        string? tableNameFilter = null
    )
    {
        var where = string.IsNullOrWhiteSpace(tableNameFilter) ? string.Empty : ToLikeString(tableNameFilter);

        var sql = $"""
                            SELECT TABLE_NAME
                            FROM INFORMATION_SCHEMA.TABLES
                            WHERE
                                TABLE_TYPE = 'BASE TABLE'
                                AND TABLE_SCHEMA = @schemaName
                                {(
                string.IsNullOrWhiteSpace(where) ? null : " AND TABLE_NAME LIKE @where"
            )}
                            ORDER BY TABLE_NAME
            """;

        return (sql, new { schemaName = NormalizeSchemaName(schemaName), where });
    }

    /// <summary>
    /// Generates an SQL query to drop (delete) a table in the specified schema.
    /// </summary>
    /// <param name="schemaName">The name of the schema containing the table. If null, uses the default schema.</param>
    /// <param name="tableName">The name of the table to drop.</param>
    /// <returns>The SQL query string for dropping the table.</returns>
    protected virtual string SqlDropTable(string? schemaName, string tableName)
    {
        return $"DROP TABLE {GetSchemaQualifiedIdentifierName(schemaName, tableName)}";
    }

    /// <summary>
    /// Generates an SQL query to rename a table in the specified schema.
    /// </summary>
    /// <param name="schemaName">The name of the schema containing the table. If null, uses the default schema.</param>
    /// <param name="tableName">The current name of the table to rename.</param>
    /// <param name="newTableName">The new name for the table.</param>
    /// <returns>The SQL query string for renaming the table.</returns>
    protected virtual string SqlRenameTable(string? schemaName, string tableName, string newTableName)
    {
        return $"ALTER TABLE {GetSchemaQualifiedIdentifierName(schemaName, tableName)} RENAME TO {NormalizeName(newTableName)}";
    }

    /// <summary>
    /// Generates an SQL query to truncate (empty) a table in the specified schema.
    /// </summary>
    /// <param name="schemaName">The name of the schema containing the table. If null, uses the default schema.</param>
    /// <param name="tableName">The name of the table to truncate.</param>
    /// <returns>The SQL query string for truncating the table.</returns>
    protected virtual string SqlTruncateTable(string? schemaName, string tableName)
    {
        return $"TRUNCATE TABLE {GetSchemaQualifiedIdentifierName(schemaName, tableName)}";
    }

    /// <summary>
    /// Anything inside of tableConstraints does NOT get added to the column definition.
    /// Anything added to the column definition should be added to the tableConstraints object.
    /// </summary>
    /// <param name="existingTable">The existing table WITHOUT the column being added.</param>
    /// <param name="column">The new column.</param>
    /// <param name="tableConstraints">Table constraints that will get added after the column definitions clauses in the CREATE TABLE or ALTER TABLE commands.</param>
    /// <param name="dbVersion">The database version.</param>
    /// <returns>A string representing the column definition for the new column.</returns>
    protected virtual string SqlInlineColumnDefinition(
        DmTable existingTable,
        DmColumn column,
        DmTable tableConstraints,
        Version dbVersion
    )
    {
        var (schemaName, tableName, columnName) = NormalizeNames(
            existingTable.SchemaName,
            existingTable.TableName,
            column.ColumnName
        );

        var sql = new StringBuilder();

        sql.Append($"{SqlInlineColumnNameAndType(column, dbVersion)}");

        sql.Append($" {SqlInlineColumnNullable(column)}");

        // Only add the primary key here if the primary key is a single column key
        // and doesn't already exist in the existing table constraints
        var tpkc = tableConstraints.PrimaryKeyConstraint;
        if (
            column.IsPrimaryKey
            && (
                tpkc == null
                || (
                    tpkc.Columns.Count == 1
                    && tpkc.Columns[0].ColumnName.Equals(column.ColumnName, StringComparison.OrdinalIgnoreCase)
                )
            )
        )
        {
            var pkConstraintName = DbProviderUtils.GeneratePrimaryKeyConstraintName(tableName, columnName);
            var pkInlineSql = SqlInlinePrimaryKeyColumnConstraint(column, pkConstraintName, out var useTableConstraint);
            if (!string.IsNullOrWhiteSpace(pkInlineSql))
            {
                sql.Append($" {pkInlineSql}");
            }

            if (useTableConstraint)
            {
                tableConstraints.PrimaryKeyConstraint = new DmPrimaryKeyConstraint(
                    schemaName,
                    tableName,
                    pkConstraintName,
                    [new DmOrderedColumn(columnName)]
                );
            }
            else
            { // since we added the PK inline, we're going to remove it from the table constraints
                tableConstraints.PrimaryKeyConstraint = null;
            }
        }
#if DEBUG
        else if (column.IsPrimaryKey)
        {
            // PROVIDED FOR BREAKPOINT PURPOSES WHILE DEBUGGING: Primary key will be added as a table constraint
            sql.Append(string.Empty);
        }
#endif

        var defaultExpression = column.GetDefaultExpression(ProviderType);
        if (
            !string.IsNullOrWhiteSpace(defaultExpression)
            && tableConstraints.DefaultConstraints.All(dc =>
                !dc.ColumnName.Equals(column.ColumnName, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            var defConstraintName = DbProviderUtils.GenerateDefaultConstraintName(tableName, columnName);
            sql.Append($" {SqlInlineDefaultColumnConstraint(defConstraintName, defaultExpression)}");
        }
        else
        {
            // DEFAULT EXPRESSIONS ARE A LITTLE DIFFERENT
            // In our case, we're always going to add them via the column definition.
            // SQLite ONLY allows default expressions to be added via the column definition.
            // Other providers also allow it, so let's just do them all here
            var defaultConstraint = tableConstraints.DefaultConstraints.FirstOrDefault(dc =>
                dc.ColumnName.Equals(column.ColumnName, StringComparison.OrdinalIgnoreCase)
            );
            if (defaultConstraint != null)
            {
                sql.Append(
                    $" {SqlInlineDefaultColumnConstraint(defaultConstraint.ConstraintName, defaultConstraint.Expression)}"
                );
            }
        }

        var checkExpression = column.GetCheckExpression(ProviderType);
        if (
            !string.IsNullOrWhiteSpace(checkExpression)
            && tableConstraints.CheckConstraints.All(ck =>
                string.IsNullOrWhiteSpace(ck.ColumnName)
                || !ck.ColumnName.Equals(column.ColumnName, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            var ckConstraintName = DbProviderUtils.GenerateCheckConstraintName(tableName, columnName);
            var ckInlineSql = SqlInlineCheckColumnConstraint(
                ckConstraintName,
                checkExpression,
                out var useTableConstraint
            );

            if (!string.IsNullOrWhiteSpace(ckInlineSql))
            {
                sql.Append($" {ckInlineSql}");
            }

            if (useTableConstraint)
            {
                tableConstraints.CheckConstraints.Add(
                    new DmCheckConstraint(schemaName, tableName, columnName, ckConstraintName, checkExpression)
                );
            }
        }

        if (
            column.IsUnique
            && !column.IsIndexed
            && tableConstraints.UniqueConstraints.All(uc =>
                !uc.Columns.Any(c => c.ColumnName.Equals(column.ColumnName, StringComparison.OrdinalIgnoreCase))
            )
        )
        {
            var ucConstraintName = DbProviderUtils.GenerateUniqueConstraintName(tableName, columnName);
            var ucInlineSql = SqlInlineUniqueColumnConstraint(ucConstraintName, out var useTableConstraint);

            if (!string.IsNullOrWhiteSpace(ucInlineSql))
            {
                sql.Append($" {ucInlineSql}");
            }

            if (useTableConstraint)
            {
                tableConstraints.UniqueConstraints.Add(
                    new DmUniqueConstraint(schemaName, tableName, ucConstraintName, [new DmOrderedColumn(columnName)])
                );
            }
        }

        if (
            column.IsForeignKey
            && !string.IsNullOrWhiteSpace(column.ReferencedTableName)
            && !string.IsNullOrWhiteSpace(column.ReferencedColumnName)
            && tableConstraints.ForeignKeyConstraints.All(fk =>
                !fk.SourceColumns.Any(c => c.ColumnName.Equals(column.ColumnName, StringComparison.OrdinalIgnoreCase))
            )
        )
        {
            var fkConstraintName = DbProviderUtils.GenerateForeignKeyConstraintName(
                tableName,
                columnName,
                NormalizeName(column.ReferencedTableName),
                NormalizeName(column.ReferencedColumnName)
            );
            var fkInlineSql = SqlInlineForeignKeyColumnConstraint(
                schemaName,
                fkConstraintName,
                column.ReferencedTableName,
                new DmOrderedColumn(column.ReferencedColumnName),
                column.OnDelete,
                column.OnUpdate,
                out var useTableConstraint
            );

            if (!string.IsNullOrWhiteSpace(fkInlineSql))
            {
                sql.Append($" {fkInlineSql}");
            }

            if (useTableConstraint)
            {
                tableConstraints.ForeignKeyConstraints.Add(
                    new DmForeignKeyConstraint(
                        schemaName,
                        tableName,
                        fkConstraintName,
                        [new DmOrderedColumn(columnName)],
                        column.ReferencedTableName,
                        [new DmOrderedColumn(column.ReferencedColumnName)],
                        column.OnDelete ?? DmForeignKeyAction.NoAction,
                        column.OnUpdate ?? DmForeignKeyAction.NoAction
                    )
                );
            }
        }

        if (
            column.IsIndexed
            && tableConstraints.Indexes.All(i =>
                !i.Columns.Any(c => c.ColumnName.Equals(column.ColumnName, StringComparison.OrdinalIgnoreCase))
            )
        )
        {
            var indexName = DbProviderUtils.GenerateIndexName(tableName, columnName);
            tableConstraints.Indexes.Add(
                new DmIndex(schemaName, tableName, indexName, [new DmOrderedColumn(columnName)], column.IsUnique)
            );
        }

        var columnSqlStatement = sql.ToString();
        return columnSqlStatement;
    }

    /// <summary>
    /// Generates a string representing a column name and its data type for use in SQL inline statements (e.g., CREATE TABLE or ALTER TABLE).
    /// </summary>
    /// <param name="column">The DmColumn object containing the column details.</param>
    /// <param name="dbVersion">The database version. Used to determine the data type syntax for compatibility with different DBMS versions.</param>
    /// <returns>A string representing the column name and its data type, suitable for use in SQL inline statements.</returns>
    protected virtual string SqlInlineColumnNameAndType(DmColumn column, Version dbVersion)
    {
        var columnType = column.GetProviderDataType(ProviderType);
        if (string.IsNullOrWhiteSpace(columnType))
        {
            // if no provider type is set, we need to infer it from the .NET type
            ArgumentNullException.ThrowIfNull(column.DotnetType, nameof(column.DotnetType));

            var descriptor = new DotnetTypeDescriptor(
                column.DotnetType.OrUnderlyingTypeIfNullable(),
                column.Length,
                column.Precision,
                column.Scale,
                column.IsAutoIncrement,
                column.IsUnicode,
                column.IsFixedLength
            );
            columnType = GetSqlTypeFromDotnetType(descriptor);
        }
        else
        {
            // If provider type is set but doesn't already include parameters (no parentheses),
            // append length/precision/scale if they're specified on the column
            if (!columnType.Contains('(', StringComparison.Ordinal))
            {
                // Check if the type supports parameters by looking it up in the registry
                var typeInfo = GetDataTypeRegistry().GetDataTypeByName(columnType);
                string? parametersString = null;

                // Determine what parameters to add, but only if the type supports them
                if (
                    column.Precision.HasValue
                    && column.Scale.HasValue
                    && typeInfo?.SupportsPrecision == true
                    && typeInfo?.SupportsScale == true
                )
                {
                    parametersString = $"({column.Precision},{column.Scale})";
                }
                else if (column.Precision.HasValue && typeInfo?.SupportsPrecision == true)
                {
                    parametersString = $"({column.Precision})";
                }
                else if (column.Length.HasValue && typeInfo?.SupportsLength == true)
                {
                    parametersString = $"({column.Length})";
                }

                if (!string.IsNullOrEmpty(parametersString))
                {
                    // Check for multi-word type modifiers (e.g., PostgreSQL "time with time zone")
                    // Parameters must be inserted BEFORE these modifiers, not at the end
                    string[] modifiers = [" with time zone", " without time zone"];

                    var inserted = false;
                    foreach (var modifier in modifiers)
                    {
                        var modifierIndex = columnType.IndexOf(modifier, StringComparison.OrdinalIgnoreCase);
                        if (modifierIndex > 0)
                        {
                            // Insert parameters before the modifier
                            columnType = string.Concat(
                                columnType.AsSpan(0, modifierIndex),
                                parametersString,
                                columnType.AsSpan(modifierIndex)
                            );
                            inserted = true;
                            break;
                        }
                    }

                    if (!inserted)
                    {
                        // No modifier found, append to end
                        columnType += parametersString;
                    }
                }
            }
        }

        if (string.IsNullOrWhiteSpace(columnType))
        {
            throw new InvalidOperationException(
                $"Could not determine the SQL type for column {column.ColumnName} of type {column.DotnetType?.Name ?? "unknown"}."
            );
        }

        // set the type on the column so that it can be used in other methods
        column.SetProviderDataType(ProviderType, columnType);

        return $"{NormalizeName(column.ColumnName)} {columnType}";
    }

    /// <summary>
    /// Generates a string representing the NULLability clause for a column (e.g., NOT NULL or NULL).
    /// </summary>
    /// <param name="column">The DmColumn object containing the column details, including its Nullable property.</param>
    /// <returns>A string representing the NULLability clause for use in SQL inline statements, or an empty string if not specified.</returns>
    protected virtual string SqlInlineColumnNullable(DmColumn column)
    {
        return column.IsNullable && !column.IsUnique && !column.IsPrimaryKey ? " NULL" : " NOT NULL";
    }

    /// <summary>
    /// Generates a string representing a PRIMARY KEY constraint clause for a specific column in SQL inline statements.
    /// </summary>
    /// <param name="column">The DmColumn object containing the column details.</param>
    /// <param name="constraintName">The desired name for the PRIMARY KEY constraint. If null, a default name will be generated.</param>
    /// <param name="useTableConstraint">
    ///     Output parameter indicating whether to use TABLE CONSTRAINT syntax (true) or inline the constraint within the column definition (false).
    ///     The method determines which syntax to use based on the database provider.
    /// </param>
    /// <returns>A string representing the PRIMARY KEY constraint clause for use in SQL inline statements.</returns>
    protected virtual string SqlInlinePrimaryKeyColumnConstraint(
        DmColumn column,
        string constraintName,
        out bool useTableConstraint
    )
    {
        useTableConstraint = false;
        return $"CONSTRAINT {NormalizeName(constraintName)} PRIMARY KEY {(column.IsAutoIncrement ? SqlInlinePrimaryKeyAutoIncrementColumnConstraint(column) : string.Empty)}".Trim();
    }

    /// <summary>
    /// Generates a string representing both a PRIMARY KEY and AUTOINCREMENT constraint clauses for a specific column in SQL inline statements.
    /// </summary>
    /// <param name="column">The DmColumn object containing the column details, which must have an Identity property set to true.</param>
    /// <returns>A string representing both the PRIMARY KEY and AUTOINCREMENT constraint clauses for use in SQL inline statements. If the column does not support identity or autoincrement, an empty string is returned.</returns>
    protected virtual string SqlInlinePrimaryKeyAutoIncrementColumnConstraint(DmColumn column)
    {
        return "IDENTITY(1,1)";
    }

    /// <summary>
    /// Generates a string representing a DEFAULT constraint clause with a specified expression.
    /// </summary>
    /// <param name="constraintName">The desired name for the DEFAULT constraint. If null, no constraint name will be included.</param>
    /// <param name="defaultExpression">The default value or expression to use when inserting null values into the column.</param>
    /// <returns>A string representing the DEFAULT constraint clause for use in SQL inline statements, or an empty string if no constraint name is provided and the default expression is not set.</returns>
    protected virtual string SqlInlineDefaultColumnConstraint(string constraintName, string defaultExpression)
    {
        SqlExpressionValidator.ValidateDefaultExpression(defaultExpression, nameof(defaultExpression));

        defaultExpression = defaultExpression.Trim();
        var addParentheses =
            defaultExpression.Contains(' ', StringComparison.OrdinalIgnoreCase)
            && !(defaultExpression.StartsWith('(') && defaultExpression.EndsWith(')'))
            && !(defaultExpression.StartsWith('"') && defaultExpression.EndsWith('"'))
            && !(defaultExpression.StartsWith('\'') && defaultExpression.EndsWith('\''))
            && !IsFunctionCall(defaultExpression);

        return $"CONSTRAINT {NormalizeName(constraintName)} DEFAULT {(addParentheses ? $"({defaultExpression})" : defaultExpression)}";
    }

    /// <summary>
    /// Generates a string representing a CHECK constraint clause with a specified expression.
    /// </summary>
    /// <param name="constraintName">The desired name for the CHECK constraint. If null, no constraint name will be included.</param>
    /// <param name="checkExpression">The check condition that enforces data integrity on the column.</param>
    /// <param name="useTableConstraint">
    ///     Output parameter indicating whether to use TABLE CONSTRAINT syntax (true) or inline the constraint within the column definition (false).
    ///     The method determines which syntax to use based on the database provider.
    /// </param>
    /// <returns>A string representing the CHECK constraint clause for use in SQL inline statements, or an empty string if no constraint name is provided and the check expression is not set.</returns>
    protected virtual string SqlInlineCheckColumnConstraint(
        string constraintName,
        string checkExpression,
        out bool useTableConstraint
    )
    {
        SqlExpressionValidator.ValidateCheckExpression(checkExpression, nameof(checkExpression));
        useTableConstraint = false;
        return $"CONSTRAINT {NormalizeName(constraintName)} CHECK ({checkExpression})";
    }

    /// <summary>
    /// Generates a string representing a UNIQUE constraint clause.
    /// </summary>
    /// <param name="constraintName">The desired name for the UNIQUE constraint. If null, no constraint name will be included.</param>
    /// <param name="useTableConstraint">
    ///     Output parameter indicating whether to use TABLE CONSTRAINT syntax (true) or inline the constraint within the column definition (false).
    ///     The method determines which syntax to use based on the database provider.
    /// </param>
    /// <returns>A string representing the UNIQUE constraint clause for use in SQL inline statements, or an empty string if no constraint name is provided.</returns>
    protected virtual string SqlInlineUniqueColumnConstraint(string constraintName, out bool useTableConstraint)
    {
        useTableConstraint = false;
        return $"CONSTRAINT {NormalizeName(constraintName)} UNIQUE";
    }

    /// <summary>
    /// Generates a string representing a FOREIGN KEY constraint clause.
    /// </summary>
    /// <param name="schemaName">The name of the schema containing the referenced table, if not in the same schema as the current table.</param>
    /// <param name="constraintName">The desired name for the FOREIGN KEY constraint. If null, no constraint name will be included.</param>
    /// <param name="referencedTableName">The name of the referenced (parent) table.</param>
    /// <param name="referencedColumn">The DmOrderedColumn object representing the referenced column in the parent table.</param>
    /// <param name="onDelete">The ON DELETE action to take when a referenced row is deleted. Can be null if not specified.</param>
    /// <param name="onUpdate">The ON UPDATE action to take when a referenced row is updated. Can be null if not specified.</param>
    /// <param name="useTableConstraint">
    ///     Output parameter indicating whether to use TABLE CONSTRAINT syntax (true) or inline the constraint within the column definition (false).
    ///     The method determines which syntax to use based on the database provider.
    /// </param>
    /// <returns>A string representing the FOREIGN KEY constraint clause for use in SQL inline statements, or an empty string if no constraint name is provided.</returns>
    protected virtual string SqlInlineForeignKeyColumnConstraint(
        string? schemaName,
        string constraintName,
        string referencedTableName,
        DmOrderedColumn referencedColumn,
        DmForeignKeyAction? onDelete,
        DmForeignKeyAction? onUpdate,
        out bool useTableConstraint
    )
    {
        useTableConstraint = false;
        return $"CONSTRAINT {NormalizeName(constraintName)} REFERENCES {GetSchemaQualifiedIdentifierName(schemaName, referencedTableName)} ({NormalizeName(referencedColumn.ColumnName)})"
            + (onDelete.HasValue ? $" ON DELETE {onDelete.Value.ToSql()}" : string.Empty)
            + (onUpdate.HasValue ? $" ON UPDATE {onUpdate.Value.ToSql()}" : string.Empty);
    }

    /// <summary>
    /// Generates a string representing a PRIMARY KEY constraint clause for the specified table.
    /// </summary>
    /// <param name="table">The DmTable object representing the table to which the PRIMARY KEY constraint is applied.</param>
    /// <param name="primaryKeyConstraint">The DmPrimaryKeyConstraint object containing the list of columns that form the primary key.</param>
    /// <returns>A string representing the PRIMARY KEY constraint clause for use in SQL inline statements, or an empty string if no columns are specified in the primary key constraint.</returns>
    protected virtual string SqlInlinePrimaryKeyTableConstraint(
        DmTable table,
        DmPrimaryKeyConstraint primaryKeyConstraint
    )
    {
        var pkColumnNames = primaryKeyConstraint.Columns.Select(c => c.ColumnName).ToArray();
        var pkConstrainName = !string.IsNullOrWhiteSpace(primaryKeyConstraint.ConstraintName)
            ? primaryKeyConstraint.ConstraintName
            : DbProviderUtils.GeneratePrimaryKeyConstraintName(table.TableName, pkColumnNames.ToArray());
        var pkColumnsCsv = string.Join(", ", pkColumnNames);
        return $"CONSTRAINT {NormalizeName(pkConstrainName)} PRIMARY KEY ({pkColumnsCsv})";
    }

    /// <summary>
    /// Generates a string representing a CHECK constraint clause for the specified table using the provided DmCheckConstraint object.
    /// </summary>
    /// <param name="table">The DmTable object representing the table to which the CHECK constraint is applied.</param>
    /// <param name="check">
    ///     The DmCheckConstraint object containing the check condition that enforces data integrity on one or more columns in the table.
    ///     The check condition should be a valid SQL expression that evaluates to TRUE or FALSE.
    /// </param>
    /// <returns>A string representing the CHECK constraint clause for use in SQL inline statements, or an empty string if no check condition is specified.</returns>
    protected virtual string SqlInlineCheckTableConstraint(DmTable table, DmCheckConstraint check)
    {
        SqlExpressionValidator.ValidateCheckExpression(check.Expression, nameof(check.Expression));

        var ckConstraintName =
            !string.IsNullOrWhiteSpace(check.ConstraintName) ? check.ConstraintName
            : string.IsNullOrWhiteSpace(check.ColumnName)
                ? DbProviderUtils.GenerateCheckConstraintName(table.TableName, DateTime.Now.Ticks.ToString())
            : DbProviderUtils.GenerateCheckConstraintName(table.TableName, check.ColumnName);

        return $"CONSTRAINT {NormalizeName(ckConstraintName)} CHECK ({check.Expression})";
    }

    /// <summary>
    /// Generates a string representing a UNIQUE constraint clause for the specified table using the provided DmUniqueConstraint object.
    /// </summary>
    /// <param name="table">The DmTable object representing the table to which the UNIQUE constraint is applied.</param>
    /// <param name="uc">
    ///     The DmUniqueConstraint object containing the list of columns that form the unique key and, optionally, the constraint name.
    ///     If no constraint name is provided, none will be included in the generated clause.
    /// </param>
    /// <param name="supportsOrderedKeysInConstraints">
    ///     Indicates whether the database provider supports ordered keys within constraints. This flag affects how the unique key columns are listed in the output clause.
    ///     Set to true for providers like SQL Server that support ordered keys; set to false for providers like MySQL that treat all unique key columns as unordered.
    /// </param>
    /// <returns>A string representing the UNIQUE constraint clause for use in SQL inline statements, or an empty string if no columns are specified in the DmUniqueConstraint object.</returns>
    protected virtual string SqlInlineUniqueTableConstraint(
        DmTable table,
        DmUniqueConstraint uc,
        bool supportsOrderedKeysInConstraints
    )
    {
        var ucConstraintName = !string.IsNullOrWhiteSpace(uc.ConstraintName)
            ? uc.ConstraintName
            : DbProviderUtils.GenerateUniqueConstraintName(
                table.TableName,
                uc.Columns.Select(c => NormalizeName(c.ColumnName)).ToArray()
            );

        var uniqueColumns = uc.Columns.Select(c =>
            supportsOrderedKeysInConstraints
                ? new DmOrderedColumn(NormalizeName(c.ColumnName), c.Order).ToString()
                : new DmOrderedColumn(NormalizeName(c.ColumnName)).ToString()
        );
        return $"CONSTRAINT {NormalizeName(ucConstraintName)} UNIQUE ({string.Join(", ", uniqueColumns)})";
    }

    /// <summary>
    /// Generates a string representing a FOREIGN KEY constraint clause for the specified table using the provided DmForeignKeyConstraint object.
    /// </summary>
    /// <param name="table">The DmTable object representing the table to which the FOREIGN KEY constraint is applied.</param>
    /// <param name="fk">
    ///     The DmForeignKeyConstraint object containing the list of child columns, referenced table and column names,
    ///     as well as the ON DELETE and ON UPDATE actions. If no constraint name is provided, none will be included in the generated clause.
    /// </param>
    /// <returns>A string representing the FOREIGN KEY constraint clause for use in SQL inline statements, or an empty string if no columns are specified in the DmForeignKeyConstraint object.</returns>
    protected virtual string SqlInlineForeignKeyTableConstraint(DmTable table, DmForeignKeyConstraint fk)
    {
        return $"""

                        CONSTRAINT {NormalizeName(fk.ConstraintName)}
                            FOREIGN KEY ({string.Join(
                ", ",
                fk.SourceColumns.Select(c => NormalizeName(c.ColumnName))
            )})
                                REFERENCES {GetSchemaQualifiedIdentifierName(
                table.SchemaName,
                fk.ReferencedTableName
            )} ({string.Join(", ", fk.ReferencedColumns.Select(c => NormalizeName(c.ColumnName)))})
                                    ON DELETE {fk.OnDelete.ToSql()}
                                    ON UPDATE {fk.OnUpdate.ToSql()}
            """.Trim();
    }

    #endregion // Table Strings

    #region Column Strings

    /// <summary>
    /// Generates a SQL statement to drop (remove) the specified column from the given table.
    /// </summary>
    /// <param name="schemaName">The name of the schema containing the table, if not in the default schema. Can be null if not specified.</param>
    /// <param name="tableName">The name of the table from which to remove the column.</param>
    /// <param name="columnName">The name of the column to drop from the table.</param>
    /// <returns>A SQL statement that drops the specified column, or an empty string if no column name is provided.</returns>
    protected virtual string SqlDropColumn(string? schemaName, string tableName, string columnName)
    {
        return $"ALTER TABLE {GetSchemaQualifiedIdentifierName(schemaName, tableName)} DROP COLUMN {NormalizeName(columnName)}";
    }
    #endregion // Column Strings

    #region Check Constraint Strings

    /// <summary>
    /// Generates a SQL statement to add a CHECK constraint to the specified table with the given name and expression.
    /// </summary>
    /// <param name="schemaName">The name of the schema containing the table, if not in the default schema. Can be null if not specified.</param>
    /// <param name="tableName">The name of the table to which the CHECK constraint is added.</param>
    /// <param name="constraintName">The desired name for the CHECK constraint. If null or empty, no constraint name will be included in the generated SQL statement.</param>
    /// <param name="expression">The check condition that enforces data integrity on one or more columns in the table. Must be a valid SQL expression that evaluates to TRUE or FALSE.</param>
    /// <returns>A SQL statement that adds the specified CHECK constraint to the given table, or an empty string if no constraint name or expression is provided.</returns>
    protected virtual string SqlAlterTableAddCheckConstraint(
        string? schemaName,
        string tableName,
        string constraintName,
        string expression
    )
    {
        SqlExpressionValidator.ValidateCheckExpression(expression, nameof(expression));

        if (expression.Trim().StartsWith('(') && expression.Trim().EndsWith(')'))
        {
            expression = expression.Trim().Substring(1, expression.Length - 2);
        }

        return $"ALTER TABLE {GetSchemaQualifiedIdentifierName(schemaName, tableName)} ADD CONSTRAINT {NormalizeName(constraintName)} CHECK ({expression})";
    }

    /// <summary>
    /// Generates a SQL statement to drop (remove) the specified CHECK constraint from the given table in its schema.
    /// </summary>
    /// <param name="schemaName">The name of the schema containing the table and the CHECK constraint. Can be null if not specified, indicating the default schema.</param>
    /// <param name="tableName">The name of the table that contains the CHECK constraint to remove.</param>
    /// <param name="constraintName">The name of the CHECK constraint to drop from the table.</param>
    /// <returns>A SQL statement that drops (removes) the specified CHECK constraint from the given table in its schema.</returns>
    protected virtual string SqlDropCheckConstraint(string? schemaName, string tableName, string constraintName)
    {
        return $"ALTER TABLE {GetSchemaQualifiedIdentifierName(schemaName, tableName)} DROP CONSTRAINT {NormalizeName(constraintName)}";
    }
    #endregion // Check Constraint Strings

    #region Default Constraint Strings

    /// <summary>
    /// Generates a SQL statement to add a new DEFAULT constraint with the given name, column, and expression to the specified table in its schema.
    /// </summary>
    /// <param name="schemaName">The name of the schema containing the table. Can be null if not specified, indicating the default schema.</param>
    /// <param name="tableName">The name of the table to add the DEFAULT constraint to.</param>
    /// <param name="columnName">The name of the column that the DEFAULT constraint should apply to.</param>
    /// <param name="constraintName">The desired name for the new DEFAULT constraint.</param>
    /// <param name="expression">A string representing the default value expression for the specified column.</param>
    /// <returns>A SQL statement that adds a new DEFAULT constraint with the given name, column, and expression to the specified table in its schema.</returns>
    protected virtual string SqlAlterTableAddDefaultConstraint(
        string? schemaName,
        string tableName,
        string columnName,
        string constraintName,
        string expression
    )
    {
        var schemaQualifiedTableName = GetSchemaQualifiedIdentifierName(schemaName, tableName);

        return $"""

                        ALTER TABLE {schemaQualifiedTableName}
                            ADD CONSTRAINT {NormalizeName(
                constraintName
            )} DEFAULT {expression} FOR {NormalizeName(columnName)}

            """;
    }

    /// <summary>
    /// Generates a SQL statement to drop (remove) the specified DEFAULT constraint from the given column in its table and schema.
    /// </summary>
    /// <param name="schemaName">The name of the schema containing the table and the DEFAULT constraint. Can be null if not specified, indicating the default schema.</param>
    /// <param name="tableName">The name of the table that contains the DEFAULT constraint to remove.</param>
    /// <param name="columnName">The name of the column associated with the DEFAULT constraint to remove.</param>
    /// <param name="constraintName">The name of the DEFAULT constraint to drop from the specified column in its table.</param>
    /// <returns>A SQL statement that drops (removes) the specified DEFAULT constraint from the given column in its table and schema.</returns>
    protected virtual string SqlDropDefaultConstraint(
        string? schemaName,
        string tableName,
        string columnName,
        string constraintName
    )
    {
        return $"ALTER TABLE {GetSchemaQualifiedIdentifierName(schemaName, tableName)} DROP CONSTRAINT {NormalizeName(constraintName)}";
    }
    #endregion // Default Constraint Strings

    #region Primary Key Strings

    /// <summary>
    /// Generates a SQL statement to add a new PRIMARY KEY constraint with the given name and columns (including ordering, if supported) to the specified table in its schema.
    /// </summary>
    /// <param name="schemaName">The name of the schema containing the table. Can be null if not specified, indicating the default schema.</param>
    /// <param name="tableName">The name of the table to add the PRIMARY KEY constraint to.</param>
    /// <param name="constraintName">The desired name for the new PRIMARY KEY constraint.</param>
    /// <param name="columns">An array of DmOrderedColumn objects representing the columns that should serve as the primary key in the table, along with their ordering (if any).</param>
    /// <param name="supportsOrderedKeysInConstraints">
    ///     A boolean value indicating whether the database system supports ordered keys (column ordering) in constraints. If true, column order will be included in the generated SQL statement.
    /// </param>
    /// <returns>A SQL statement that adds a new PRIMARY KEY constraint with the given name and columns (including ordering, if supported) to the specified table in its schema.</returns>
    protected virtual string SqlAlterTableAddPrimaryKeyConstraint(
        string? schemaName,
        string tableName,
        string constraintName,
        DmOrderedColumn[] columns,
        bool supportsOrderedKeysInConstraints
    )
    {
        var primaryKeyColumns = string.Join(
            ", ",
            columns.Select(c =>
            {
                var columnName = NormalizeName(c.ColumnName);
                return c.Order == DmColumnOrder.Ascending ? columnName : $"{columnName} DESC";
            })
        );
        return $"""
            ALTER TABLE {GetSchemaQualifiedIdentifierName(schemaName, tableName)}
                                ADD CONSTRAINT {NormalizeName(constraintName)}
                                    PRIMARY KEY ({primaryKeyColumns})
            """;
    }

    /// <summary>
    /// Generates a SQL statement to drop (remove) the specified PRIMARY KEY constraint from the given table in its schema.
    /// </summary>
    /// <param name="schemaName">The name of the schema containing the table and the PRIMARY KEY constraint. Can be null if not specified, indicating the default schema.</param>
    /// <param name="tableName">The name of the table that contains the PRIMARY KEY constraint to remove.</param>
    /// <param name="constraintName">The name of the PRIMARY KEY constraint to drop from the table.</param>
    /// <returns>A SQL statement that drops (removes) the specified PRIMARY KEY constraint from the given table in its schema.</returns>
    protected virtual string SqlDropPrimaryKeyConstraint(string? schemaName, string tableName, string constraintName)
    {
        return $"ALTER TABLE {GetSchemaQualifiedIdentifierName(schemaName, tableName)} DROP CONSTRAINT {NormalizeName(constraintName)}";
    }
    #endregion // Primary Key Strings

    #region Unique Constraint Strings

    /// <summary>
    /// Generates a SQL statement to add a new UNIQUE constraint with the given name and columns to the specified table in its schema.
    /// </summary>
    /// <param name="schemaName">The name of the schema containing the table. Can be null if not specified, indicating the default schema.</param>
    /// <param name="tableName">The name of the table to add the UNIQUE constraint to.</param>
    /// <param name="constraintName">The desired name for the new UNIQUE constraint.</param>
    /// <param name="columns">An array of DmOrderedColumn objects representing the columns that should be unique together in the table.</param>
    /// <param name="supportsOrderedKeysInConstraints">
    ///     A boolean value indicating whether the database system supports ordered keys (column ordering) in constraints. If true, column order will be included in the generated SQL statement.
    /// </param>
    /// <returns>A SQL statement that adds a new UNIQUE constraint with the given name and columns to the specified table in its schema.</returns>
    protected virtual string SqlAlterTableAddUniqueConstraint(
        string? schemaName,
        string tableName,
        string constraintName,
        DmOrderedColumn[] columns,
        bool supportsOrderedKeysInConstraints
    )
    {
        var uniqueColumns = columns.Select(c =>
            supportsOrderedKeysInConstraints
                ? new DmOrderedColumn(NormalizeName(c.ColumnName), c.Order).ToString()
                : new DmOrderedColumn(NormalizeName(c.ColumnName)).ToString()
        );
        return $"""
            ALTER TABLE {GetSchemaQualifiedIdentifierName(schemaName, tableName)}
                                ADD CONSTRAINT {NormalizeName(constraintName)} UNIQUE ({string.Join(
                ", ",
                uniqueColumns
            )})
            """;
    }

    /// <summary>
    /// Generates a SQL statement to drop (remove) the specified UNIQUE constraint from the given table in its schema.
    /// </summary>
    /// <param name="schemaName">The name of the schema containing the table and the UNIQUE constraint. Can be null if not specified, indicating the default schema.</param>
    /// <param name="tableName">The name of the table that contains the UNIQUE constraint to remove.</param>
    /// <param name="constraintName">The name of the UNIQUE constraint to drop from the table.</param>
    /// <returns>A SQL statement that drops (removes) the specified UNIQUE constraint from the given table in its schema.</returns>
    protected virtual string SqlDropUniqueConstraint(string? schemaName, string tableName, string constraintName)
    {
        return $"ALTER TABLE {GetSchemaQualifiedIdentifierName(schemaName, tableName)} DROP CONSTRAINT {NormalizeName(constraintName)}";
    }
    #endregion // Unique Constraint Strings

    #region Foreign Key Constraint Strings

    /// <summary>
    /// Generates a SQL statement to add a new foreign key constraint to the specified table in its schema, referencing another table with the given columns and actions.
    /// </summary>
    /// <param name="schemaName">The name of the schema containing the table. Can be null if not specified, indicating the default schema.</param>
    /// <param name="constraintName">The desired name for the new foreign key constraint.</param>
    /// <param name="tableName">The name of the table to add the foreign key constraint to.</param>
    /// <param name="columns">An array of DmOrderedColumn objects representing the columns in the current (referencing) table that participate in the foreign key constraint.</param>
    /// <param name="referencedTableName">The name of the referenced table where the related data resides.</param>
    /// <param name="referencedColumns">
    ///     An array of DmOrderedColumn objects representing the columns in the referenced table that correspond to the columns in the current (referencing) table.
    /// </param>
    /// <param name="onDelete">The action to take when a referenced row is deleted. Can be one of the DmForeignKeyAction enum values: Cascade, SetNull, NoAction, or SetDefault.</param>
    /// <param name="onUpdate">The action to take when a referenced row is updated. Can be one of the DmForeignKeyAction enum values: Cascade, SetNull, NoAction, or SetDefault.</param>
    /// <returns>A SQL statement that adds a new foreign key constraint with the given name, columns, referencing table, and actions to the specified table in its schema.</returns>
    protected virtual string SqlAlterTableAddForeignKeyConstraint(
        string? schemaName,
        string constraintName,
        string tableName,
        DmOrderedColumn[] columns,
        string referencedTableName,
        DmOrderedColumn[] referencedColumns,
        DmForeignKeyAction onDelete,
        DmForeignKeyAction onUpdate
    )
    {
        var schemaQualifiedTableName = GetSchemaQualifiedIdentifierName(schemaName, tableName);
        var schemaQualifiedReferencedTableName = GetSchemaQualifiedIdentifierName(schemaName, referencedTableName);
        var columnNames = columns.Select(c => NormalizeName(c.ColumnName));
        var referencedColumnNames = referencedColumns.Select(c => NormalizeName(c.ColumnName));

        return $"""

                        ALTER TABLE {schemaQualifiedTableName}
                            ADD CONSTRAINT {NormalizeName(constraintName)}
                                FOREIGN KEY ({string.Join(", ", columnNames)})
                                    REFERENCES {schemaQualifiedReferencedTableName} ({string.Join(
                ", ",
                referencedColumnNames
            )})
                                        ON DELETE {onDelete.ToSql()}
                                        ON UPDATE {onUpdate.ToSql()}

            """;
    }

    /// <summary>
    /// Generates a SQL statement to drop (remove) the specified foreign key constraint from the given table in its schema.
    /// </summary>
    /// <param name="schemaName">The name of the schema containing the table and the foreign key constraint. Can be null if not specified, indicating the default schema.</param>
    /// <param name="tableName">The name of the table that contains the foreign key constraint to remove.</param>
    /// <param name="constraintName">The name of the foreign key constraint to drop from the table.</param>
    /// <returns>A SQL statement that drops (removes) the specified foreign key constraint from the given table in its schema.</returns>
    protected virtual string SqlDropForeignKeyConstraint(string? schemaName, string tableName, string constraintName)
    {
        return $"ALTER TABLE {GetSchemaQualifiedIdentifierName(schemaName, tableName)} DROP CONSTRAINT {NormalizeName(constraintName)}";
    }
    #endregion // Foreign Key Constraint Strings

    #region Index Strings

    /// <summary>
    /// Generates a SQL statement to create an index on the specified table with the given name, column list, and uniqueness option.
    /// </summary>
    /// <param name="schemaName">The name of the schema containing the table. Can be null if not specified, indicating the default schema.</param>
    /// <param name="tableName">The name of the table to create an index on.</param>
    /// <param name="indexName">The desired name for the new index.</param>
    /// <param name="columns">An array of DmOrderedColumn objects representing the columns to include in the index.</param>
    /// <param name="isUnique">
    ///     A boolean value indicating whether the created index should be a UNIQUE index. The default is false.
    /// </param>
    /// <returns>A SQL statement that creates an index with the given name, column list, and uniqueness option on the specified table in the provided schema.</returns>
    protected virtual string SqlCreateIndex(
        string? schemaName,
        string tableName,
        string indexName,
        DmOrderedColumn[] columns,
        bool isUnique = false
    )
    {
        return $"CREATE {(isUnique ? "UNIQUE " : string.Empty)}INDEX {NormalizeName(indexName)} ON {GetSchemaQualifiedIdentifierName(schemaName, tableName)} ({string.Join(", ", columns.Select(c => c.ToString()))})";
    }

    /// <summary>
    /// Generates a SQL statement to drop (remove) the specified index from the given table in its schema.
    /// </summary>
    /// <param name="schemaName">The name of the schema containing the table and index. Can be null if not specified, indicating the default schema.</param>
    /// <param name="tableName">The name of the table that contains the index to remove.</param>
    /// <param name="indexName">The name of the index to drop from the table.</param>
    /// <returns>A SQL statement that drops (removes) the specified index from the given table in its schema.</returns>
    protected virtual string SqlDropIndex(string? schemaName, string tableName, string indexName)
    {
        return $"DROP INDEX {NormalizeName(indexName)} ON {GetSchemaQualifiedIdentifierName(schemaName, tableName)}";
    }
    #endregion // Index Strings

    #region View Strings

    /// <summary>
    /// Generates a SQL statement to create a new view in the specified schema with the given name and definition.
    /// </summary>
    /// <param name="schemaName">The name of the schema to contain the new view. Can be null if not specified, indicating the default schema.</param>
    /// <param name="viewName">The desired name for the new view.</param>
    /// <param name="definition">The SQL statement or expression defining the structure and data of the view.</param>
    /// <returns>A SQL statement that creates a new view with the given name and definition in the specified schema.</returns>
    protected virtual string SqlCreateView(string? schemaName, string viewName, string definition)
    {
        SqlExpressionValidator.ValidateViewDefinition(definition, nameof(definition));
        return $"CREATE VIEW {GetSchemaQualifiedIdentifierName(schemaName, viewName)} AS {definition}";
    }

    /// <summary>
    /// Generates a SQL statement and any required parameters to retrieve a list of view names from the specified schema that match the given filter.
    /// </summary>
    /// <param name="schemaName">The name of the schema containing the views. If null, view names from all schemas will be retrieved.</param>
    /// <param name="viewNameFilter">
    ///     An optional view name filter to narrow down the list of retrieved view names. If provided, only views with names matching this filter will be included in the result set.
    ///     To match any view name, use a wildcard ('*'); e.g., '*' matches all view names, while 'MyView*' matches any view starting with 'MyView'.
    /// </param>
    /// <returns>A tuple containing:
    ///     - The SQL statement to retrieve view names from the specified schema and filter.
    ///     - An object representing any parameters required for executing the generated SQL statement (e.g., a parameterized query or stored procedure input parameters).</returns>
    protected virtual (string sql, object parameters) SqlGetViewNames(string? schemaName, string? viewNameFilter = null)
    {
        var where = string.IsNullOrWhiteSpace(viewNameFilter) ? string.Empty : ToLikeString(viewNameFilter);

        var sql = $"""
            SELECT
                                TABLE_NAME AS ViewName
                            FROM
                                INFORMATION_SCHEMA.VIEWS
                            WHERE
                                TABLE_NAME IS NOT NULL
                                {(
                string.IsNullOrWhiteSpace(schemaName) ? string.Empty : " AND TABLE_SCHEMA = @schemaName"
            )}
                                {(
                string.IsNullOrWhiteSpace(where) ? string.Empty : " AND TABLE_NAME LIKE @where"
            )}
                            ORDER BY
                                TABLE_NAME
            """;

        return (sql, new { schemaName = NormalizeSchemaName(schemaName), where });
    }

    /// <summary>
    /// Generates a SQL statement and any required parameters to retrieve a list of views from the specified schema that match the given filter.
    /// </summary>
    /// <param name="schemaName">The name of the schema containing the views. If null, views from all schemas will be retrieved.</param>
    /// <param name="viewNameFilter">
    ///     An optional view name filter to narrow down the list of retrieved views. If provided, only views with names matching this filter will be included in the result set.
    ///     To match any view name, use a wildcard ('*'); e.g., '*' matches all view names, while 'MyView*' matches any view starting with 'MyView'.
    /// </param>
    /// <returns>A tuple containing:
    ///     - The SQL statement to retrieve views from the specified schema and filter.
    ///     - An object representing any parameters required for executing the generated SQL statement (e.g., a parameterized query or stored procedure input parameters).</returns>
    protected virtual (string sql, object parameters) SqlGetViews(string? schemaName, string? viewNameFilter)
    {
        var where = string.IsNullOrWhiteSpace(viewNameFilter) ? string.Empty : ToLikeString(viewNameFilter);

        var sql = $"""
            SELECT
                                TABLE_SCHEMA AS SchemaName
                                TABLE_NAME AS ViewName,
                                VIEW_DEFINITION AS Definition
                            FROM
                                INFORMATION_SCHEMA.VIEWS
                            WHERE
                                TABLE_NAME IS NOT NULL
                                {(
                string.IsNullOrWhiteSpace(schemaName) ? string.Empty : " AND TABLE_SCHEMA = @schemaName"
            )}
                                {(
                string.IsNullOrWhiteSpace(where) ? string.Empty : " AND TABLE_NAME LIKE @where"
            )}
                            ORDER BY
                                TABLE_NAME
            """;

        return (sql, new { schemaName = NormalizeSchemaName(schemaName), where });
    }

    /// <summary>
    /// Normalizes the given view definition by removing any leading or trailing whitespace, and applying consistent indentation.
    /// This method is designed to clean up and standardize view definitions for better readability and consistency across different sources.
    /// </summary>
    /// <param name="definition">The original view definition as a string.</param>
    /// <returns>The normalized view definition with removed leading/trailing whitespace and consistent indentation.</returns>
    protected virtual string NormalizeViewDefinition(string definition)
    {
        return definition;
    }

    /// <summary>
    /// Generates a SQL statement to drop (remove) the specified view from the given schema.
    /// </summary>
    /// <param name="schemaName">The name of the schema containing the view, if not in the default schema. Can be null if not specified.</param>
    /// <param name="viewName">The name of the view to remove.</param>
    /// <returns>A SQL statement that drops the specified view from the given schema, or an empty string if no view name is provided.</returns>
    protected virtual string SqlDropView(string? schemaName, string viewName)
    {
        return $"DROP VIEW {GetSchemaQualifiedIdentifierName(schemaName, viewName)}";
    }

    /// <summary>
    /// Determines if the given expression appears to be a function call.
    /// </summary>
    /// <param name="expression">The expression to analyze.</param>
    /// <returns>True if the expression appears to be a function call, false otherwise.</returns>
    protected static bool IsFunctionCall(string expression)
    {
        // Check if the expression contains parentheses and looks like a function call
        var parenIndex = expression.IndexOf('(', StringComparison.Ordinal);
        if (parenIndex == -1 || !expression.EndsWith(')'))
        {
            return false;
        }

        // Get the part before the opening parenthesis
        var functionPart = expression[..parenIndex].Trim();

        // Check if it's a valid function name (contains only letters, numbers, underscores)
        return !string.IsNullOrEmpty(functionPart) && functionPart.All(c => char.IsLetterOrDigit(c) || c == '_');
    }
    #endregion // View Strings
}
