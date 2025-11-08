// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Providers.PostgreSql;

public partial class PostgreSqlMethods
{
    /// <summary>
    /// Retrieves a list of tables and their metadata from the PostgreSQL database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name to filter tables.</param>
    /// <param name="tableNameFilter">The table name filter.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of <see cref="DmTable"/> objects representing the tables and their metadata.</returns>
    public override async Task<List<DmTable>> GetTablesAsync(
        IDbConnection db,
        string? schemaName,
        string? tableNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        schemaName = NormalizeSchemaName(schemaName);

        var where = string.IsNullOrWhiteSpace(tableNameFilter) ? null : ToLikeString(tableNameFilter);

        // columns
        // we could use information_schema but it's SOOO SLOW! unbearable really,
        // so we will use pg_catalog instead
        var columnsSql = $"""

                        SELECT
                            schemas.nspname as schema_name,
                            tables.relname as table_name,
                            columns.attname as column_name,
                            columns.attnum as column_ordinal,
                            pg_get_expr(column_defs.adbin, column_defs.adrelid) as column_default,
                            case when (coalesce(primarykeys.conname, '') = '') then 0 else 1 end AS is_primary_key,
                            primarykeys.conname as pk_constraint_name,
                            case when columns.attnotnull then 0 else 1 end AS is_nullable,
                            case when (columns.attidentity = '') then 0 else 1 end as is_identity,
                            types.typname as data_type,
                        	format_type(columns.atttypid, columns.atttypmod) as data_type_ext
                        FROM pg_catalog.pg_attribute AS columns
                            join pg_catalog.pg_type as types on columns.atttypid = types.oid
                            JOIN pg_catalog.pg_class AS tables ON columns.attrelid = tables.oid and tables.relkind = 'r' and tables.relpersistence = 'p'
                            JOIN pg_catalog.pg_namespace AS schemas ON tables.relnamespace = schemas.oid
                            left outer join pg_catalog.pg_attrdef as column_defs on columns.attrelid = column_defs.adrelid and columns.attnum = column_defs.adnum
                            left outer join pg_catalog.pg_constraint as primarykeys on columns.attnum=ANY(primarykeys.conkey) AND primarykeys.conrelid = tables.oid and primarykeys.contype = 'p'
                        where
                            schemas.nspname not like 'pg_%' and schemas.nspname != 'information_schema' and columns.attnum > 0 and not columns.attisdropped
                            AND lower(schemas.nspname) = @schemaName
                            AND tables.relname NOT IN ('spatial_ref_sys', 'geometry_columns', 'geography_columns', 'raster_columns', 'raster_overviews')
                            {(
                string.IsNullOrWhiteSpace(where) ? null : " AND lower(tables.relname) LIKE @where"
            )}
                        order by schema_name, table_name, column_ordinal;

            """;
        var columnResults = await QueryAsync<(
            string schema_name,
            string table_name,
            string column_name,
            int column_ordinal,
            string column_default,
            bool is_primary_key,
            string pk_constraint_name,
            bool is_nullable,
            bool is_identity,
            string data_type,
            string data_type_ext
        )>(db, columnsSql, new { schemaName, where }, tx: tx, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        // get indexes
        var indexes = await GetIndexesInternalAsync(db, schemaName, tableNameFilter, null, tx, cancellationToken)
            .ConfigureAwait(false);

        // get primary key, unique key, foreign key and check constraints in a single query
        var constraintsSql = $"""

                        select
                            schemas.nspname as schema_name,
                            tables.relname as table_name,
                            r.conname as constraint_name,
                            indexes.relname as supporting_index_name,
                            case
                                when r.contype = 'c' then 'CHECK'
                                when r.contype = 'f' then 'FOREIGN KEY'
                                when r.contype = 'p' then 'PRIMARY KEY'
                                when r.contype = 'u' then 'UNIQUE'
                                else 'OTHER'
                            end as constraint_type,
                            pg_catalog.pg_get_constraintdef(r.oid, true) as constraint_definition,
                            referenced_tables.relname as referenced_table_name,
                            array_to_string(r.conkey, ',') as column_ordinals_csv,
                            array_to_string(r.confkey, ',') as referenced_column_ordinals_csv,
                            case
                                when r.confdeltype = 'a' then 'NO ACTION'
                                when r.confdeltype = 'r' then 'RESTRICT'
                                when r.confdeltype = 'c' then 'CASCADE'
                                when r.confdeltype = 'n' then 'SET NULL'
                                when r.confdeltype = 'd' then 'SET DEFAULT'
                                else null
                            end as delete_rule,
                            case
                                when r.confupdtype = 'a' then 'NO ACTION'
                                when r.confupdtype = 'r' then 'RESTRICT'
                                when r.confupdtype = 'c' then 'CASCADE'
                                when r.confupdtype = 'n' then 'SET NULL'
                                when r.confupdtype = 'd' then 'SET DEFAULT'
                                else null
                            end as update_rule
                        from pg_catalog.pg_constraint r
                            join pg_catalog.pg_namespace AS schemas ON r.connamespace = schemas.oid
                            join pg_class as tables on r.conrelid = tables.oid
                            left outer join pg_class as indexes on r.conindid = indexes.oid
                            left outer join pg_class as referenced_tables on r.confrelid = referenced_tables.oid
                        where
                            schemas.nspname not like 'pg_%'
                            and schemas.nspname != 'information_schema'
                            and r.contype in ('c', 'f', 'p', 'u')
                            and lower(schemas.nspname) = @schemaName
                            {(
                string.IsNullOrWhiteSpace(where) ? null : " AND lower(tables.relname) LIKE @where"
            )}
                        order by schema_name, table_name, constraint_type, constraint_name

            """;
        var constraintResults = await QueryAsync<(
            string schema_name,
            string table_name,
            string constraint_name,
            string supporting_index_name,
            /* CHECK, UNIQUE, FOREIGN KEY, PRIMARY KEY */
            string constraint_type,
            string constraint_definition,
            string referenced_table_name,
            string column_ordinals_csv,
            string referenced_column_ordinals_csv,
            string delete_rule,
            string update_rule
        )>(db, constraintsSql, new { schemaName, where }, tx: tx, cancellationToken: cancellationToken).ConfigureAwait(false);

        var referencedTableNames = constraintResults
            .Where(c => c.constraint_type == "FOREIGN KEY")
            .Select(c => c.referenced_table_name.ToLowerInvariant())
            .Distinct()
            .ToArray();
        var referencedColumnsSql = """

                        SELECT
                            schemas.nspname as schema_name,
                            tables.relname as table_name,
                            columns.attname as column_name,
                            columns.attnum as column_ordinal
                        FROM pg_catalog.pg_attribute AS columns
                            JOIN pg_catalog.pg_class AS tables ON columns.attrelid = tables.oid and tables.relkind = 'r' and tables.relpersistence = 'p'
                            JOIN pg_catalog.pg_namespace AS schemas ON tables.relnamespace = schemas.oid
                        where
                            schemas.nspname not like 'pg_%' and schemas.nspname != 'information_schema' and columns.attnum > 0 and not columns.attisdropped
                            AND lower(schemas.nspname) = @schemaName
                            AND lower(tables.relname) = ANY (@referencedTableNames)
                        order by schema_name, table_name, column_ordinal;

            """;
        var referencedColumnsResults =
            referencedTableNames.Length == 0
                ? []
                : await QueryAsync<(string schema_name, string table_name, string column_name, int column_ordinal)>(
                        db,
                        referencedColumnsSql,
                        new { schemaName, referencedTableNames },
                        tx: tx,
                        cancellationToken: cancellationToken
                    )
                    .ConfigureAwait(false);

        var tables = new List<DmTable>();

        foreach (var tableColumnResults in columnResults.GroupBy(r => new { r.schema_name, r.table_name }))
        {
            schemaName = tableColumnResults.Key.schema_name;
            var tableName = tableColumnResults.Key.table_name;
            var tableConstraintResults = constraintResults
                .Where(t =>
                    t.schema_name.Equals(schemaName, StringComparison.OrdinalIgnoreCase)
                    && t.table_name.Equals(tableName, StringComparison.OrdinalIgnoreCase)
                )
                .ToArray();

            var tableForeignKeyConstraints = tableConstraintResults
                .Where(t => t.constraint_type.Equals("FOREIGN KEY", StringComparison.OrdinalIgnoreCase))
                .Select(row =>
                {
                    var sourceColumns = row
                        .column_ordinals_csv.Split(',')
                        .Select(r =>
                        {
                            return new DmOrderedColumn(
                                tableColumnResults.First(c => c.column_ordinal == int.Parse(r)).column_name
                            );
                        })
                        .ToArray();
                    var referencedColumns = row
                        .referenced_column_ordinals_csv.Split(',')
                        .Select(r =>
                        {
                            return new DmOrderedColumn(
                                referencedColumnsResults
                                    .First(c =>
                                        c.table_name.Equals(
                                            row.referenced_table_name,
                                            StringComparison.OrdinalIgnoreCase
                                        )
                                        && c.column_ordinal == int.Parse(r)
                                    )
                                    .column_name
                            );
                        })
                        .ToArray();
                    return new DmForeignKeyConstraint(
                        row.schema_name,
                        row.table_name,
                        row.constraint_name,
                        sourceColumns,
                        row.referenced_table_name,
                        referencedColumns,
                        row.delete_rule.ToForeignKeyAction(),
                        row.update_rule.ToForeignKeyAction()
                    );
                })
                .ToArray();

            var tableCheckConstraints = tableConstraintResults
                .Where(t =>
                    t.constraint_type.Equals("CHECK", StringComparison.OrdinalIgnoreCase)
                    && t.constraint_definition.StartsWith("CHECK (", StringComparison.OrdinalIgnoreCase)
                )
                .Select(c =>
                {
                    var columns = c
                        .column_ordinals_csv.Split(',')
                        .Select(r =>
                        {
                            return tableColumnResults.First(tcr => tcr.column_ordinal == int.Parse(r)).column_name;
                        })
                        .ToArray();
                    return new DmCheckConstraint(
                        c.schema_name,
                        c.table_name,
                        columns.Length == 1 ? columns[0] : null,
                        c.constraint_name,
                        c.constraint_definition.Substring(7).TrimEnd(')')
                    );
                })
                .ToArray();

            var tableDefaultConstraints = tableColumnResults
                // ignore default values that are sequences (from SERIAL columns)
                .Where(t =>
                    !string.IsNullOrWhiteSpace(t.column_default)
                    && !t.column_default.StartsWith("nextval()", StringComparison.OrdinalIgnoreCase)
                )
                .Select(c =>
                {
                    return new DmDefaultConstraint(
                        c.schema_name,
                        c.table_name,
                        c.column_name,
                        $"df_{c.table_name}_{c.column_name}",
                        c.column_default
                    );
                })
                .ToArray();

            var tablePrimaryKeyConstraint = tableConstraintResults
                .Where(t => t.constraint_type.Equals("PRIMARY KEY", StringComparison.OrdinalIgnoreCase))
                .Select(row =>
                {
                    var columns = row
                        .column_ordinals_csv.Split(',')
                        .Select(r =>
                        {
                            return new DmOrderedColumn(
                                tableColumnResults.First(c => c.column_ordinal == int.Parse(r)).column_name
                            );
                        })
                        .ToArray();
                    return new DmPrimaryKeyConstraint(row.schema_name, row.table_name, row.constraint_name, columns);
                })
                .FirstOrDefault();

            var tableUniqueConstraints = tableConstraintResults
                .Where(t => t.constraint_type.Equals("UNIQUE", StringComparison.OrdinalIgnoreCase))
                .Select(row =>
                {
                    var columns = row
                        .column_ordinals_csv.Split(',')
                        .Select(r =>
                        {
                            return new DmOrderedColumn(
                                tableColumnResults.First(c => c.column_ordinal == int.Parse(r)).column_name
                            );
                        })
                        .ToArray();
                    return new DmUniqueConstraint(row.schema_name, row.table_name, row.constraint_name, columns);
                })
                .ToArray();

            var tableIndexes = indexes
                .Where(i =>
                    (i.SchemaName ?? string.Empty).Equals(schemaName, StringComparison.OrdinalIgnoreCase)
                    && i.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase)
                )
                .ToArray();

            var columns = new List<DmColumn>();
            foreach (var tableColumn in tableColumnResults)
            {
                var columnIsUniqueViaUniqueConstraintOrIndex =
                    tableUniqueConstraints.Any(c =>
                        c.Columns.Count == 1
                        && c.Columns.Any(col =>
                            col.ColumnName.Equals(tableColumn.column_name, StringComparison.OrdinalIgnoreCase)
                        )
                    )
                    || indexes.Any(i =>
                        i.IsUnique
                        && i.Columns.Count == 1
                        && i.Columns.Any(c =>
                            c.ColumnName.Equals(tableColumn.column_name, StringComparison.OrdinalIgnoreCase)
                        )
                    );

                var columnIsPartOfIndex = indexes.Any(i =>
                    i.Columns.Any(c => c.ColumnName.Equals(tableColumn.column_name, StringComparison.OrdinalIgnoreCase))
                );

                var foreignKeyConstraint = tableForeignKeyConstraints.FirstOrDefault(c =>
                    c.SourceColumns.Any(scol =>
                        scol.ColumnName.Equals(tableColumn.column_name, StringComparison.OrdinalIgnoreCase)
                    )
                );

                var foreignKeyColumnIndex = foreignKeyConstraint
                    ?.SourceColumns.Select((scol, i) => new { c = scol, i })
                    .FirstOrDefault(c =>
                        c.c.ColumnName.Equals(tableColumn.column_name, StringComparison.OrdinalIgnoreCase)
                    )
                    ?.i;

                var dotnetTypeDescriptor = GetDotnetTypeFromSqlType(
                    !string.IsNullOrWhiteSpace(tableColumn.data_type_ext)
                    && tableColumn.data_type.Length < tableColumn.data_type_ext.Length
                        ? tableColumn.data_type_ext
                        : tableColumn.data_type
                );

                var isUnicode =
                    dotnetTypeDescriptor.IsUnicode == true
                    || tableColumn.data_type.StartsWith("nvarchar", StringComparison.OrdinalIgnoreCase)
                    || tableColumn.data_type.StartsWith("nchar", StringComparison.OrdinalIgnoreCase)
                    || tableColumn.data_type.StartsWith("ntext", StringComparison.OrdinalIgnoreCase);

                // Normalize PostgreSQL TEXT type (unlimited) to -1
                int? normalizedLength = dotnetTypeDescriptor.Length;
                if (
                    normalizedLength == null
                    && tableColumn.data_type.Equals("text", StringComparison.OrdinalIgnoreCase)
                )
                {
                    normalizedLength = -1;
                }

                var column = new DmColumn(
                    tableColumn.schema_name,
                    tableColumn.table_name,
                    tableColumn.column_name,
                    dotnetTypeDescriptor.DotnetType,
                    new Dictionary<DbProviderType, string>
                    {
                        { ProviderType, tableColumn.data_type_ext ?? tableColumn.data_type },
                    },
                    normalizedLength,
                    dotnetTypeDescriptor.Precision,
                    dotnetTypeDescriptor.Scale,
                    tableColumn.is_nullable,
                    tablePrimaryKeyConstraint != null
                        && tablePrimaryKeyConstraint.Columns.Any(c =>
                            c.ColumnName.Equals(tableColumn.column_name, StringComparison.OrdinalIgnoreCase)
                        ),
                    tableColumn.is_identity,
                    columnIsUniqueViaUniqueConstraintOrIndex,
                    isUnicode,
                    columnIsPartOfIndex,
                    foreignKeyConstraint != null,
                    foreignKeyConstraint?.ReferencedTableName,
                    foreignKeyConstraint?.ReferencedColumns.ElementAtOrDefault(foreignKeyColumnIndex ?? 0)?.ColumnName,
                    foreignKeyConstraint?.OnDelete,
                    foreignKeyConstraint?.OnUpdate,
                    checkExpression: tableCheckConstraints
                        .FirstOrDefault(c =>
                            !string.IsNullOrWhiteSpace(c.ColumnName)
                            && c.ColumnName.Equals(tableColumn.column_name, StringComparison.OrdinalIgnoreCase)
                        )
                        ?.Expression,
                    defaultExpression: tableDefaultConstraints
                        .FirstOrDefault(c =>
                            !string.IsNullOrWhiteSpace(c.ColumnName)
                            && c.ColumnName.Equals(tableColumn.column_name, StringComparison.OrdinalIgnoreCase)
                        )
                        ?.Expression
                );

                // Apply standardized auto-increment detection
                column.IsAutoIncrement = DetermineIsAutoIncrement(
                    column,
                    tableColumn.is_identity,
                    tableColumn.data_type_ext ?? tableColumn.data_type
                );

                columns.Add(column);
            }

            var table = new DmTable(
                schemaName,
                tableName,
                [.. columns],
                tablePrimaryKeyConstraint,
                tableCheckConstraints,
                tableDefaultConstraints,
                tableUniqueConstraints,
                tableForeignKeyConstraints,
                tableIndexes
            );
            tables.Add(table);
        }

        return tables;
    }

    /// <summary>
    /// Retrieves a list of indexes and their metadata from the PostgreSQL database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name to filter indexes.</param>
    /// <param name="tableNameFilter">The table name filter.</param>
    /// <param name="indexNameFilter">The index name filter.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of <see cref="DmIndex"/> objects representing the indexes and their metadata.</returns>
    protected override async Task<List<DmIndex>> GetIndexesInternalAsync(
        IDbConnection db,
        string? schemaName,
        string? tableNameFilter = null,
        string? indexNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var whereSchemaLike = string.IsNullOrWhiteSpace(schemaName) ? null : ToLikeString(schemaName);
        var whereTableLike = string.IsNullOrWhiteSpace(tableNameFilter) ? null : ToLikeString(tableNameFilter);
        var whereIndexLike = string.IsNullOrWhiteSpace(indexNameFilter) ? null : ToLikeString(indexNameFilter);

        var indexesSql = $"""

                            select
                                schemas.nspname AS schema_name,
                                tables.relname AS table_name,
                                indexes.relname AS index_name,
                                case when i.indisunique then 1 else 0 end as is_unique,
                                array_to_string(array_agg (
                                    a.attname
                                    || ' ' || CASE o.option & 1 WHEN 1 THEN 'DESC' ELSE 'ASC' END
                                    || ' ' || CASE o.option & 2 WHEN 2 THEN 'NULLS FIRST' ELSE 'NULLS LAST' END
                                    ORDER BY c.ordinality
                                ),',') AS columns_csv
                            from
                                pg_index AS i
                                JOIN pg_class AS tables ON tables.oid = i.indrelid
                                JOIN pg_namespace AS schemas ON tables.relnamespace = schemas.oid
                                JOIN pg_class AS indexes ON indexes.oid = i.indexrelid
                                CROSS JOIN LATERAL unnest (i.indkey) WITH ORDINALITY AS c (colnum, ordinality)
                                LEFT JOIN LATERAL unnest (i.indoption) WITH ORDINALITY AS o (option, ordinality)
                                ON c.ordinality = o.ordinality
                                JOIN pg_attribute AS a ON tables.oid = a.attrelid AND a.attnum = c.colnum
                            where
                                schemas.nspname not like 'pg_%'
                                and schemas.nspname != 'information_schema'
                                and i.indislive
                                and not i.indisprimary
                                {(
                string.IsNullOrWhiteSpace(whereSchemaLike)
                    ? string.Empty
                    : " AND lower(schemas.nspname) LIKE @whereSchemaLike"
            )}
                                {(
                string.IsNullOrWhiteSpace(whereTableLike)
                    ? string.Empty
                    : " AND lower(tables.relname) LIKE @whereTableLike"
            )}
                                {(
                string.IsNullOrWhiteSpace(whereIndexLike)
                    ? string.Empty
                    : " AND lower(indexes.relname) LIKE @whereIndexLike"
            )}
                                -- postgresql creates an index for primary key and unique constraints, so we don't need to include them in the results
                                and indexes.relname not in (select x.conname from pg_catalog.pg_constraint x
                                            join pg_catalog.pg_namespace AS x2 ON x.connamespace = x2.oid
                                            join pg_class as x3 on x.conrelid = x3.oid
                                            where x2.nspname = schemas.nspname and x3.relname = tables.relname)
                            group by schemas.nspname, tables.relname, indexes.relname, i.indisunique
                            order by schema_name, table_name, index_name

            """;

        var indexResults = await QueryAsync<(
            string schema_name,
            string table_name,
            string index_name,
            bool is_unique,
            string columns_csv
        )>(
                db,
                indexesSql,
                new
                {
                    whereSchemaLike,
                    whereTableLike,
                    whereIndexLike,
                },
                tx: tx,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);

        var indexes = new List<DmIndex>();
        foreach (var ir in indexResults)
        {
            var columns = ir
                .columns_csv.Split(',')
                .Select(c =>
                {
                    var columnName = c.Trim()
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .First();
                    var isDescending = c.Contains("desc", StringComparison.OrdinalIgnoreCase);
                    return new DmOrderedColumn(
                        columnName,
                        isDescending ? DmColumnOrder.Descending : DmColumnOrder.Ascending
                    );
                })
                .ToArray();

            var index = new DmIndex(ir.schema_name, ir.table_name, ir.index_name, columns, ir.is_unique);
            indexes.Add(index);
        }

        return [.. indexes];
    }
}
