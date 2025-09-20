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
    /// <summary>
    /// Asynchronously retrieves a list of tables from the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableNameFilter">The table name filter.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of tables.</returns>
    public override async Task<List<DmTable>> GetTablesAsync(
        IDbConnection db,
        string? schemaName,
        string? tableNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        schemaName = NormalizeSchemaName(schemaName);

        var where = string.IsNullOrWhiteSpace(tableNameFilter)
            ? null
            : ToLikeString(tableNameFilter);

        // columns
        var columnsSql = $"""
                        SELECT
                            t.TABLE_SCHEMA AS schema_name,
                            t.TABLE_NAME AS table_name,
                            c.COLUMN_NAME AS column_name,
                            t.TABLE_COLLATION AS table_collation,
                            c.ORDINAL_POSITION AS column_ordinal,
                            c.COLUMN_DEFAULT AS column_default,
                            case when (c.COLUMN_KEY = 'PRI') then 1 else 0 end AS is_primary_key,
                            case
                                when (c.COLUMN_KEY = 'UNI') then 1 else 0 end AS is_unique,
                            case
                                when (c.COLUMN_KEY = 'UNI') then 1
                                when (c.COLUMN_KEY = 'MUL') then 1
                                else 0
                            end AS is_indexed,
                            case when (c.IS_NULLABLE = 'YES') then 1 else 0 end AS is_nullable,
                            c.DATA_TYPE AS data_type,
                            c.COLUMN_TYPE AS data_type_complete,
                            c.CHARACTER_MAXIMUM_LENGTH AS max_length,
                            c.NUMERIC_PRECISION AS numeric_precision,
                            c.NUMERIC_SCALE AS numeric_scale,
                            c.EXTRA as extra
                        FROM INFORMATION_SCHEMA.TABLES t
                            LEFT OUTER JOIN INFORMATION_SCHEMA.COLUMNS c ON t.TABLE_SCHEMA = c.TABLE_SCHEMA and t.TABLE_NAME = c.TABLE_NAME
                        WHERE t.TABLE_TYPE = 'BASE TABLE'
                            AND t.TABLE_SCHEMA = DATABASE()
                            {(
                string.IsNullOrWhiteSpace(where) ? null : " AND t.TABLE_NAME LIKE @where"
            )}
                        ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME, c.ORDINAL_POSITION

            """;

        var columnResults = await QueryAsync<(
            string schema_name,
            string table_name,
            string column_name,
            string table_collation,
            int column_ordinal,
            string column_default,
            bool is_primary_key,
            bool is_unique,
            bool is_indexed,
            bool is_nullable,
            string data_type,
            string data_type_complete,
            long? max_length,
            int? numeric_precision,
            int? numeric_scale,
            string? extra
        )>(db, columnsSql, new { schemaName, where }, tx: tx, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        // get primary key, unique key in a single query
        var constraintsSql = $"""
                            SELECT
                                tc.table_schema AS schema_name,
                                tc.table_name AS table_name,
                                tc.constraint_type AS constraint_type,
                                tc.constraint_name AS constraint_name,
                                GROUP_CONCAT(kcu.column_name ORDER BY kcu.ordinal_position ASC SEPARATOR ', ') AS columns_csv,
                                GROUP_CONCAT(CASE isc.collation
                                            WHEN 'A' THEN 'ASC'
                                            WHEN 'D' THEN 'DESC'
                                            ELSE 'ASC'
                                            END ORDER BY kcu.ordinal_position ASC SEPARATOR ', ') AS columns_desc_csv
                            FROM
                                information_schema.table_constraints tc
                            JOIN
                                information_schema.key_column_usage kcu
                                ON tc.constraint_name = kcu.constraint_name
                                AND tc.table_schema = kcu.table_schema
                                AND tc.table_name = kcu.table_name
                            LEFT JOIN
                                information_schema.statistics isc
                                ON kcu.table_schema = isc.table_schema
                                AND kcu.table_name = isc.table_name
                                AND kcu.column_name = isc.column_name
                                AND kcu.constraint_name = isc.index_name
                            WHERE
                                tc.table_schema = DATABASE()
                                and tc.constraint_type in ('UNIQUE', 'PRIMARY KEY')
                                {(
                string.IsNullOrWhiteSpace(where) ? null : " AND tc.table_name LIKE @where"
            )}
                            GROUP BY
                                tc.table_name,
                                tc.constraint_type,
                                tc.constraint_name
                            ORDER BY
                                tc.table_name,
                                tc.constraint_type,
                                tc.constraint_name

            """;
        var constraintResults = await QueryAsync<(
            string schema_name,
            string table_name,
            string constraint_type,
            string constraint_name,
            string columns_csv,
            string columns_desc_csv
        )>(
                db,
                constraintsSql,
                new { schemaName, where },
                tx: tx,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);

        var allDefaultConstraints = columnResults
            .Where(t =>
                !string.IsNullOrWhiteSpace(t.column_default)
                &&
                // MariaDB adds NULL as a default constraint, let's ignore it
                !t.column_default.Equals("NULL", StringComparison.OrdinalIgnoreCase)
            )
            .Select(c =>
            {
                return new DmDefaultConstraint(
                    DefaultSchema,
                    c.table_name,
                    c.column_name,
                    DbProviderUtils.GenerateDefaultConstraintName(c.table_name, c.column_name),
                    c.column_default.Trim('(', ')')
                );
            })
            .ToArray();

        var allPrimaryKeyConstraints = constraintResults
            .Where(t => t.constraint_type == "PRIMARY KEY")
            .Select(t =>
            {
                var columnNames = t.columns_csv.Split(", ");
                var columnDescs = t.columns_desc_csv.Split(", ");
                return new DmPrimaryKeyConstraint(
                    DefaultSchema,
                    t.table_name,
                    DbProviderUtils.GeneratePrimaryKeyConstraintName(t.table_name, columnNames),
                    columnNames
                        .Select(
                            (c, i) =>
                                new DmOrderedColumn(
                                    c,
                                    columnDescs[i]
                                        .Equals("DESC", StringComparison.OrdinalIgnoreCase)
                                        ? DmColumnOrder.Descending
                                        : DmColumnOrder.Ascending
                                )
                        )
                        .ToArray()
                );
            })
            .ToArray();
        var allUniqueConstraints = constraintResults
            .Where(t => t.constraint_type == "UNIQUE")
            .Select(t =>
            {
                var columnNames = t.columns_csv.Split(", ");
                var columnDescs = t.columns_desc_csv.Split(", ");
                return new DmUniqueConstraint(
                    DefaultSchema,
                    t.table_name,
                    t.constraint_name,
                    columnNames
                        .Select(
                            (c, i) =>
                                new DmOrderedColumn(
                                    c,
                                    columnDescs[i]
                                        .Equals("DESC", StringComparison.OrdinalIgnoreCase)
                                        ? DmColumnOrder.Descending
                                        : DmColumnOrder.Ascending
                                )
                        )
                        .ToArray()
                );
            })
            .ToArray();

        var foreignKeysSql = $"""

                        select distinct
                            kcu.TABLE_SCHEMA as schema_name,
                            kcu.TABLE_NAME as table_name,
                            kcu.CONSTRAINT_NAME as constraint_name,
                            kcu.REFERENCED_TABLE_SCHEMA as referenced_schema_name,
                            kcu.REFERENCED_TABLE_NAME as referenced_table_name,
                            rc.DELETE_RULE as delete_rule,
                            rc.UPDATE_RULE as update_rule,
                            kcu.ORDINAL_POSITION as key_ordinal,
                            kcu.COLUMN_NAME as column_name,
                            kcu.REFERENCED_COLUMN_NAME as referenced_column_name
                        from INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                            INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc on kcu.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
                            INNER JOIN INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc on kcu.CONSTRAINT_NAME = rc.CONSTRAINT_NAME
                        where kcu.CONSTRAINT_SCHEMA = DATABASE()
                            and tc.CONSTRAINT_SCHEMA = DATABASE()
                            and tc.CONSTRAINT_TYPE = 'FOREIGN KEY'
                            {(
                string.IsNullOrWhiteSpace(where) ? null : " AND kcu.TABLE_NAME LIKE @where"
            )}
                        order by schema_name, table_name, key_ordinal

            """;
        var foreignKeyResults = await QueryAsync<(
            string schema_name,
            string table_name,
            string constraint_name,
            string referenced_schema_name,
            string referenced_table_name,
            string delete_rule,
            string update_rule,
            string key_ordinal,
            string column_name,
            string referenced_column_name
        )>(
                db,
                foreignKeysSql,
                new { schemaName, where },
                tx: tx,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
        var allForeignKeyConstraints = foreignKeyResults
            .GroupBy(t => new
            {
                t.schema_name,
                t.table_name,
                t.constraint_name,
                t.referenced_schema_name,
                t.referenced_table_name,
                t.update_rule,
                t.delete_rule,
            })
            .Select(gb =>
            {
                return new DmForeignKeyConstraint(
                    DefaultSchema,
                    gb.Key.table_name,
                    gb.Key.constraint_name,
                    gb.Select(c => new DmOrderedColumn(c.column_name)).ToArray(),
                    gb.Key.referenced_table_name,
                    gb.Select(c => new DmOrderedColumn(c.referenced_column_name)).ToArray(),
                    gb.Key.delete_rule.ToForeignKeyAction(),
                    gb.Key.update_rule.ToForeignKeyAction()
                );
            })
            .ToArray();

        // the table CHECK_CONSTRAINTS only exists starting MySQL 8.0.16 and MariaDB 10.2.1
        // resolve issue for MySQL 5.0.12+
        DmCheckConstraint[] allCheckConstraints = [];
        if (await SupportsCheckConstraintsAsync(db, tx, cancellationToken).ConfigureAwait(false))
        {
            var checkConstraintsSql = $"""

                            SELECT
                                tc.TABLE_SCHEMA as schema_name,
                                tc.TABLE_NAME as table_name,
                                kcu.COLUMN_NAME as column_name,
                                tc.CONSTRAINT_NAME as constraint_name,
                                cc.CHECK_CLAUSE AS check_expression
                            FROM
                                INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS tc
                            JOIN
                                INFORMATION_SCHEMA.CHECK_CONSTRAINTS AS cc
                                ON tc.CONSTRAINT_NAME = cc.CONSTRAINT_NAME
                            LEFT JOIN
                                INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS kcu
                                ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
                            WHERE
                                tc.TABLE_SCHEMA = DATABASE()
                                and tc.CONSTRAINT_TYPE = 'CHECK'
                                {(
                    string.IsNullOrWhiteSpace(where) ? null : " AND tc.TABLE_NAME LIKE @where"
                )}
                            order by schema_name, table_name, column_name, constraint_name

                """;

            var checkConstraintResults = await QueryAsync<(
                string schema_name,
                string table_name,
                string? column_name,
                string constraint_name,
                string check_expression
            )>(
                    db,
                    checkConstraintsSql,
                    new { schemaName, where },
                    tx: tx,
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);
            allCheckConstraints = checkConstraintResults
                .Select(t =>
                {
                    if (string.IsNullOrWhiteSpace(t.column_name))
                    {
                        // try to associate the check constraint with a column
                        var columnCount = 0;
                        var columnName = string.Empty;
                        foreach (var column in columnResults)
                        {
                            var pattern = $@"\b{Regex.Escape(column.column_name)}\b";
                            if (
                                column.table_name.Equals(
                                    t.table_name,
                                    StringComparison.OrdinalIgnoreCase
                                )
                                && Regex.IsMatch(
                                    t.check_expression,
                                    pattern,
                                    RegexOptions.IgnoreCase
                                )
                            )
                            {
                                columnName = column.column_name;
                                columnCount++;
                            }
                        }
                        if (columnCount == 1)
                        {
                            t.column_name = columnName;
                        }
                    }
                    return new DmCheckConstraint(
                        DefaultSchema,
                        t.table_name,
                        t.column_name,
                        t.constraint_name,
                        t.check_expression
                    );
                })
                .ToArray();
        }

        var allIndexes = await GetIndexesInternalAsync(
                db,
                schemaName,
                tableNameFilter,
                tx: tx,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);

        var tables = new List<DmTable>();

        foreach (
            var tableColumns in columnResults.GroupBy(r => new { r.schema_name, r.table_name })
        )
        {
            var tableName = tableColumns.Key.table_name;

            var foreignKeyConstraints = allForeignKeyConstraints
                .Where(t => t.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var checkConstraints = allCheckConstraints
                .Where(t => t.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var defaultConstraints = allDefaultConstraints
                .Where(t => t.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var uniqueConstraints = allUniqueConstraints
                .Where(t => t.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var primaryKeyConstraint = allPrimaryKeyConstraints.SingleOrDefault(t =>
                t.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase)
            );
            var indexes = allIndexes
                .Where(t => t.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var columns = new List<DmColumn>();
            foreach (var tableColumn in tableColumns)
            {
                var columnIsUniqueViaUniqueConstraintOrIndex =
                    uniqueConstraints.Any(c =>
                        c.Columns.Count == 1
                        && c.Columns.Any(col =>
                            col.ColumnName.Equals(
                                tableColumn.column_name,
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                    )
                    || indexes.Any(i =>
                        i is { IsUnique: true, Columns.Count: 1 }
                        && i.Columns.Any(c =>
                            c.ColumnName.Equals(
                                tableColumn.column_name,
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                    );

                var columnIsPartOfIndex = indexes.Any(i =>
                    i.Columns.Any(c =>
                        c.ColumnName.Equals(
                            tableColumn.column_name,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                );

                var foreignKeyConstraint = foreignKeyConstraints.FirstOrDefault(c =>
                    c.SourceColumns.Any(scol =>
                        scol.ColumnName.Equals(
                            tableColumn.column_name,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                );

                var foreignKeyColumnIndex = foreignKeyConstraint
                    ?.SourceColumns.Select((scol, i) => new { c = scol, i })
                    .FirstOrDefault(c =>
                        c.c.ColumnName.Equals(
                            tableColumn.column_name,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    ?.i;

                var dotnetTypeDescriptor = GetDotnetTypeFromSqlType(tableColumn.data_type_complete ?? tableColumn.data_type);

                var isUnicode =
                    dotnetTypeDescriptor.IsUnicode == true
                    || tableColumn.data_type.StartsWith(
                        "varchar",
                        StringComparison.OrdinalIgnoreCase
                    )
                    || tableColumn.data_type.StartsWith("char", StringComparison.OrdinalIgnoreCase)
                    || tableColumn.data_type.StartsWith("text", StringComparison.OrdinalIgnoreCase);

                var column = new DmColumn(
                    tableColumn.schema_name,
                    tableColumn.table_name,
                    tableColumn.column_name,
                    dotnetTypeDescriptor.DotnetType,
                    new Dictionary<DbProviderType, string>
                    {
                        { ProviderType, tableColumn.data_type_complete ?? tableColumn.data_type },
                    },
                    tableColumn.max_length.HasValue
                        ? (
                            tableColumn.max_length.Value > int.MaxValue
                                ? int.MaxValue
                                : (int)tableColumn.max_length.Value
                        )
                        : null,
                    tableColumn.numeric_precision,
                    tableColumn.numeric_scale,
                    tableColumn.is_nullable,
                    primaryKeyConstraint?.Columns.Any(c =>
                        c.ColumnName.Equals(
                            tableColumn.column_name,
                            StringComparison.OrdinalIgnoreCase
                        )
                    ) == true,
                    false, // Set to false initially, will be determined by DetermineIsAutoIncrement
                    columnIsUniqueViaUniqueConstraintOrIndex,
                    isUnicode,
                    columnIsPartOfIndex,
                    foreignKeyConstraint != null,
                    foreignKeyConstraint?.ReferencedTableName,
                    foreignKeyConstraint
                        ?.ReferencedColumns.ElementAtOrDefault(foreignKeyColumnIndex ?? 0)
                        ?.ColumnName,
                    foreignKeyConstraint?.OnDelete,
                    foreignKeyConstraint?.OnUpdate,
                    checkExpression: checkConstraints
                        .FirstOrDefault(c =>
                            !string.IsNullOrWhiteSpace(c.ColumnName)
                            && c.ColumnName.Equals(
                                tableColumn.column_name,
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                        ?.Expression,
                    defaultExpression: defaultConstraints
                        .FirstOrDefault(c =>
                            !string.IsNullOrWhiteSpace(c.ColumnName)
                            && c.ColumnName.Equals(
                                tableColumn.column_name,
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                        ?.Expression
                );

                // Apply standardized auto-increment detection
                column.IsAutoIncrement = DetermineIsAutoIncrement(
                    column,
                    tableColumn.extra,
                    tableColumn.data_type_complete ?? tableColumn.data_type);

                columns.Add(column);
            }

            var table = new DmTable(
                schemaName,
                tableName,
                [.. columns],
                primaryKeyConstraint,
                checkConstraints,
                defaultConstraints,
                uniqueConstraints,
                foreignKeyConstraints,
                indexes
            );
            tables.Add(table);
        }

        return tables;
    }

    /// <summary>
    /// Asynchronously retrieves a list of indexes from the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableNameFilter">The table name filter.</param>
    /// <param name="indexNameFilter">The index name filter.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of indexes.</returns>
    protected override async Task<List<DmIndex>> GetIndexesInternalAsync(
        IDbConnection db,
        string? schemaName,
        string? tableNameFilter = null,
        string? indexNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var whereTableLike = string.IsNullOrWhiteSpace(tableNameFilter)
            ? null
            : ToLikeString(tableNameFilter);

        var whereIndexLike = string.IsNullOrWhiteSpace(indexNameFilter)
            ? null
            : ToLikeString(indexNameFilter);

        var sql = $"""

                        SELECT
                            TABLE_SCHEMA as schema_name,
                            TABLE_NAME as table_name,
                            INDEX_NAME as index_name,
                            IF(NON_UNIQUE = 1, 0, 1) AS is_unique,
                            GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX ASC) AS columns_csv,
                            GROUP_CONCAT(CASE
                                WHEN COLLATION = 'A' THEN 'ASC'
                                WHEN COLLATION = 'D' THEN 'DESC'
                                ELSE 'N/A'
                            END ORDER BY SEQ_IN_INDEX ASC) AS columns_desc_csv
                        FROM
                            INFORMATION_SCHEMA.STATISTICS stats
                        WHERE
                            TABLE_SCHEMA = DATABASE()
                            and INDEX_NAME != 'PRIMARY'
                            and INDEX_NAME NOT IN (select CONSTRAINT_NAME from INFORMATION_SCHEMA.TABLE_CONSTRAINTS
                                                where TABLE_SCHEMA = DATABASE() and
                                                        TABLE_NAME = stats.TABLE_NAME and
                                                        CONSTRAINT_TYPE in ('PRIMARY KEY', 'FOREIGN KEY', 'CHECK'))
                            {(
                !string.IsNullOrWhiteSpace(whereTableLike)
                    ? "and TABLE_NAME LIKE @whereTableLike"
                    : string.Empty
            )}
                            {(
                !string.IsNullOrWhiteSpace(whereIndexLike)
                    ? "and INDEX_NAME LIKE @whereIndexLike"
                    : string.Empty
            )}
                        GROUP BY
                            TABLE_NAME, INDEX_NAME, NON_UNIQUE
                        order by schema_name, table_name, index_name

            """;

        var indexResults = await QueryAsync<(
            string schema_name,
            string table_name,
            string index_name,
            bool is_unique,
            string columns_csv,
            string columns_desc_csv
        )>(
                db,
                sql,
                new { whereTableLike, whereIndexLike },
                tx: tx,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);

        var indexes = new List<DmIndex>();

        foreach (var indexResult in indexResults)
        {
            var columnNames = indexResult.columns_csv.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            );
            var columnDirections = indexResult.columns_desc_csv.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            );

            var columns = columnNames
                .Select(
                    (c, i) =>
                        new DmOrderedColumn(
                            c,
                            columnDirections[i].Equals("desc", StringComparison.OrdinalIgnoreCase)
                                ? DmColumnOrder.Descending
                                : DmColumnOrder.Ascending
                        )
                )
                .ToArray();

            indexes.Add(
                new DmIndex(
                    DefaultSchema,
                    indexResult.table_name,
                    indexResult.index_name,
                    columns,
                    indexResult.is_unique
                )
            );
        }

        return indexes;
    }
}
