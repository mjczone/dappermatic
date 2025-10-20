// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Providers.SqlServer;

public partial class SqlServerMethods
{
    #region Schema Strings
    #endregion // Schema Strings

    #region Table Strings

    /// <summary>
    /// Generates the SQL to rename a table.
    /// </summary>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The current table name.</param>
    /// <param name="newTableName">The new table name.</param>
    /// <returns>The SQL string to rename the table.</returns>
    protected override string SqlRenameTable(string? schemaName, string tableName, string newTableName)
    {
        return $"EXEC sp_rename '{GetSchemaQualifiedIdentifierName(schemaName, tableName)}', '{NormalizeName(newTableName)}'";
    }
    #endregion // Table Strings

    #region Column Strings
    #endregion // Column Strings

    #region Check Constraint Strings
    #endregion // Check Constraint Strings

    #region Default Constraint Strings
    #endregion // Default Constraint Strings

    #region Primary Key Strings
    #endregion // Primary Key Strings

    #region Unique Constraint Strings
    #endregion // Unique Constraint Strings

    #region Foreign Key Constraint Strings
    #endregion // Foreign Key Constraint Strings

    #region Index Strings
    #endregion // Index Strings

    #region View Strings

    /// <summary>
    /// Generates the SQL to get view names.
    /// </summary>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="viewNameFilter">The view name filter.</param>
    /// <returns>A tuple containing the SQL string and parameters.</returns>
    protected override (string sql, object parameters) SqlGetViewNames(
        string? schemaName,
        string? viewNameFilter = null
    )
    {
        var where = string.IsNullOrWhiteSpace(viewNameFilter) ? string.Empty : ToLikeString(viewNameFilter);

        var sql = $"""

                        SELECT
                            v.[name] AS ViewName
                        FROM sys.objects v
                            INNER JOIN sys.sql_modules m ON v.object_id = m.object_id
                        WHERE
                            v.[type] = 'V'
                            AND v.is_ms_shipped = 0
                            AND SCHEMA_NAME(v.schema_id) = @schemaName
                            {(
                string.IsNullOrWhiteSpace(where) ? string.Empty : " AND v.[name] LIKE @where"
            )}
                        ORDER BY
                            SCHEMA_NAME(v.schema_id),
                            v.[name]
            """;

        return (sql, new { schemaName = NormalizeSchemaName(schemaName), where });
    }

    /// <summary>
    /// Generates the SQL to get views.
    /// </summary>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="viewNameFilter">The view name filter.</param>
    /// <returns>A tuple containing the SQL string and parameters.</returns>
    protected override (string sql, object parameters) SqlGetViews(string? schemaName, string? viewNameFilter)
    {
        var where = string.IsNullOrWhiteSpace(viewNameFilter) ? string.Empty : ToLikeString(viewNameFilter);

        var sql = $"""

                        SELECT
                            SCHEMA_NAME(v.schema_id) AS SchemaName,
                            v.[name] AS ViewName,
                            m.definition AS Definition
                        FROM sys.objects v
                            INNER JOIN sys.sql_modules m ON v.object_id = m.object_id
                        WHERE
                            v.[type] = 'V'
                            AND v.is_ms_shipped = 0
                            AND SCHEMA_NAME(v.schema_id) = @schemaName
                            {(
                string.IsNullOrWhiteSpace(where) ? string.Empty : " AND v.[name] LIKE @where"
            )}
                        ORDER BY
                            SCHEMA_NAME(v.schema_id),
                            v.[name]
            """;

        return (sql, new { schemaName = NormalizeSchemaName(schemaName), where });
    }

#pragma warning disable SA1201 // Elements should appear in the correct order
    private static readonly char[] WhiteSpaceCharacters = [' ', '\t', '\n', '\r'];
#pragma warning restore SA1201 // Elements should appear in the correct order

    /// <summary>
    /// Normalizes the view definition by stripping off the CREATE VIEW statement.
    /// </summary>
    /// <param name="definition">The view definition.</param>
    /// <returns>The normalized view definition.</returns>
    /// <exception cref="Exception">Thrown when the view definition cannot be parsed.</exception>
    protected override string NormalizeViewDefinition(string definition)
    {
        definition = definition.Trim();

        // strip off the CREATE VIEW statement ending with the AS
        var indexOfAs = -1;
        for (var i = 0; i < definition.Length; i++)
        {
            if (i == 0)
            {
                continue;
            }

            if (i == definition.Length - 2)
            {
                break;
            }

            if (
                !WhiteSpaceCharacters.Contains(definition[i - 1])
                || char.ToUpperInvariant(definition[i]) != 'A'
                || char.ToUpperInvariant(definition[i + 1]) != 'S'
                || !WhiteSpaceCharacters.Contains(definition[i + 2])
            )
            {
                continue;
            }

            indexOfAs = i;
            break;
        }
        if (indexOfAs == -1)
        {
            // throw an exception if the view definition cannot be parsed
            throw new InvalidDataException("Could not parse view definition: " + definition);
        }

        return definition[(indexOfAs + 3)..].Trim();
    }
    #endregion // View Strings
}
