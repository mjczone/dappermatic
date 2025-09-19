// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Data.Common;
using System.Text;
using MJCZone.DapperMatic.Models;

// ReSharper disable LoopCanBeConvertedToQuery
// ReSharper disable ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator

namespace MJCZone.DapperMatic.Providers.Sqlite;

public partial class SqliteMethods
{
    /// <inheritdoc/>
    public override async Task<List<DmTable>> GetTablesAsync(
        IDbConnection db,
        string? schemaName,
        string? tableNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var where = string.IsNullOrWhiteSpace(tableNameFilter)
            ? null
            : ToLikeString(tableNameFilter);

        var sql = new StringBuilder();
        sql.AppendLine(
            """
            SELECT name as table_name, sql as table_sql
                            FROM sqlite_master
                            WHERE type = 'table' AND name NOT LIKE 'sqlite_%'
            """
        );
        if (!string.IsNullOrWhiteSpace(where))
        {
            sql.AppendLine(" AND name LIKE @where");
        }

        sql.AppendLine("ORDER BY name");

        var results = await QueryAsync<(string table_name, string table_sql)>(
                db,
                sql.ToString(),
                new { where },
                tx: tx,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);

        var tables = new List<DmTable>();
        foreach (var result in results)
        {
            var table = SqliteSqlParser.ParseCreateTableStatement(
                result.table_sql,
                ProviderTypeMap
            );
            if (table == null)
            {
                continue;
            }

            tables.Add(table);
        }

        // attach indexes to tables
        var indexes = await GetIndexesInternalAsync(
                db,
                schemaName,
                tableNameFilter,
                null,
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (indexes.Count <= 0)
        {
            return tables;
        }

        foreach (var table in tables)
        {
            table.Indexes = indexes
                .Where(i => i.TableName.Equals(table.TableName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (table.Indexes.Count <= 0)
            {
                continue;
            }

            foreach (var column in table.Columns)
            {
                var providerDataType = column.GetProviderDataType(DbProviderType.Sqlite);

                column.IsUnicode =
                    providerDataType != null
                    && (
                        providerDataType.StartsWith("nvarchar", StringComparison.OrdinalIgnoreCase)
                        || providerDataType.StartsWith("nchar", StringComparison.OrdinalIgnoreCase)
                        || providerDataType.StartsWith("ntext", StringComparison.OrdinalIgnoreCase)
                    );

                // Apply standardized auto-increment detection
                // Note: SQLite parser already sets IsAutoIncrement, but we run this for consistency
                // and to catch any edge cases (e.g., INTEGER PRIMARY KEY implicit ROWID)
                column.IsAutoIncrement = DetermineIsAutoIncrement(
                    column,
                    column.IsAutoIncrement,
                    providerDataType
                );

                column.IsIndexed = table.Indexes.Any(i =>
                    i.Columns.Any(c =>
                        c.ColumnName.Equals(column.ColumnName, StringComparison.OrdinalIgnoreCase)
                    )
                );
                if (column is { IsIndexed: true, IsUnique: false })
                {
                    column.IsUnique = table
                        .Indexes.Where(i => i.IsUnique)
                        .Any(i =>
                            i.Columns.Any(c =>
                                c.ColumnName.Equals(
                                    column.ColumnName,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                        );
                }
            }
        }

        return tables;
    }

    /// <summary>
    /// Truncates the table if it exists asynchronously.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the table was truncated, otherwise false.</returns>
    public override async Task<bool> TruncateTableIfExistsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        if (
            !await DoesTableExistAsync(db, schemaName, tableName, tx, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            return false;
        }

        (_, tableName, _) = NormalizeNames(schemaName, tableName);

        // in SQLite, you could either delete all the records and reset the index (this could take a while if it's a big table)
        // - DELETE FROM table_name;
        // - DELETE FROM sqlite_sequence WHERE name = 'table_name';

        // or just drop the table (this is faster) and recreate it
        var createTableSql = await ExecuteScalarAsync<string>(
                db,
                "select sql FROM sqlite_master WHERE type = 'table' AND name = @tableName",
                new { tableName },
                tx: tx,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(createTableSql))
        {
            return false;
        }

        await DropTableIfExistsAsync(db, schemaName, tableName, tx, cancellationToken)
            .ConfigureAwait(false);

        await ExecuteAsync(db, createTableSql, tx: tx, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Gets the indexes internally asynchronously.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableNameFilter">The table name filter.</param>
    /// <param name="indexNameFilter">The index name filter.</param>
    /// <param name="tx">The transaction.</param>
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

                            SELECT DISTINCT
                                m.name AS table_name,
                                il.name AS index_name,
                                il."unique" AS is_unique,
                                ii.name AS column_name,
                                ii.DESC AS is_descending
                            FROM sqlite_schema AS m,
                                pragma_index_list(m.name) AS il,
                                pragma_index_xinfo(il.name) AS ii
                            WHERE m.type='table'
                                and ii.name IS NOT NULL
                                AND il.origin = 'c'
                                {(
                string.IsNullOrWhiteSpace(whereTableLike) ? string.Empty : " AND m.name LIKE @whereTableLike"
            )}
                                {(
                string.IsNullOrWhiteSpace(whereIndexLike) ? string.Empty : " AND il.name LIKE @whereIndexLike"
            )}
                            ORDER BY m.name, il.name, ii.seqno
            """;

        var results = await QueryAsync<(
            string table_name,
            string index_name,
            bool is_unique,
            string column_name,
            bool is_descending
        )>(
                db,
                sql,
                new { whereTableLike, whereIndexLike },
                tx: tx,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);

        var indexes = new List<DmIndex>();

        foreach (
            var group in results.GroupBy(r => new
            {
                r.table_name,
                r.index_name,
                r.is_unique,
            })
        )
        {
            var index = new DmIndex
            {
                SchemaName = null,
                TableName = group.Key.table_name,
                IndexName = group.Key.index_name,
                IsUnique = group.Key.is_unique,
                Columns = group
                    .Select(r => new DmOrderedColumn(
                        r.column_name,
                        r.is_descending ? DmColumnOrder.Descending : DmColumnOrder.Ascending
                    ))
                    .ToList(),
            };
            indexes.Add(index);
        }

        return indexes;
    }

    /// <summary>
    /// Alters the table using recreate table strategy asynchronously.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="validateTable">The validate table function.</param>
    /// <param name="updateTable">The update table function.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the table was altered, otherwise false.</returns>
    private async Task<bool> AlterTableUsingRecreateTableStrategyAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        Func<DmTable, bool>? validateTable,
        Func<DmTable, DmTable> updateTable,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    )
    {
        var table = await GetTableAsync(db, schemaName, tableName, tx, cancellationToken)
            .ConfigureAwait(false);

        if (table == null)
        {
            return false;
        }

        if (validateTable != null && !validateTable(table))
        {
            return false;
        }

        // create a temporary table with the updated schema
        var tmpTable = new DmTable(
            table.SchemaName,
            table.TableName,
            [.. table.Columns],
            table.PrimaryKeyConstraint,
            [.. table.CheckConstraints],
            [.. table.DefaultConstraints],
            [.. table.UniqueConstraints],
            [.. table.ForeignKeyConstraints],
            [.. table.Indexes]
        );
        var newTable = updateTable(tmpTable);

        await AlterTableUsingRecreateTableStrategyAsync(db, table, newTable, tx, cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Alters the table using recreate table strategy asynchronously.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="existingTable">The existing table.</param>
    /// <param name="updatedTable">The updated table.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private async Task AlterTableUsingRecreateTableStrategyAsync(
        IDbConnection db,
        DmTable existingTable,
        DmTable updatedTable,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    )
    {
        var tableName = existingTable.TableName;
        var tempTableName = $"{tableName}_tmp_{Guid.NewGuid():N}";

        if (db is DbConnection dbc)
        {
            if (db.State != ConnectionState.Open)
            {
                await dbc.OpenAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        // get the create index sql statements for the existing table
        // var createIndexStatements = await GetCreateIndexSqlStatementsForTable(
        //         db,
        //         schemaName,
        //         tableName,
        //         tx,
        //         cancellationToken
        //     )
        //     .ConfigureAwait(false);

        // disable foreign key constraints temporarily
        await ExecuteAsync(
                db,
                "PRAGMA foreign_keys = 0",
                tx: tx,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);

        // Only create a new transaction if one wasn't provided
        DbTransaction? innerTx = null;
        bool shouldCommit = false;

        if (tx != null)
        {
            // Use the existing transaction
            innerTx = (DbTransaction)tx;
        }
        else if (db is DbConnection dbcc)
        {
            // Create a new transaction only if none exists
            innerTx = await dbcc.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            shouldCommit = true;
        }

        try
        {
            // create a temporary table from the existing table's data
            await ExecuteAsync(
                    db,
                    $"CREATE TEMP TABLE {tempTableName} AS SELECT * FROM {tableName}",
                    tx: innerTx,
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);

            // drop the old table
            await ExecuteAsync(
                    db,
                    $"DROP TABLE {tableName}",
                    tx: innerTx,
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);

            var created = await CreateTableIfNotExistsAsync(
                    db,
                    updatedTable,
                    innerTx,
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (created)
            {
                // populate the new table with the data from the old table
                var previousColumnNames = existingTable.Columns.Select(c => c.ColumnName);

                // make sure to only copy columns that exist in both tables
                var columnNamesInBothTables = previousColumnNames
                    .Where(c =>
                        updatedTable.Columns.Any(x =>
                            x.ColumnName.Equals(c, StringComparison.OrdinalIgnoreCase)
                        )
                    )
                    .ToArray();

                if (columnNamesInBothTables.Length > 0)
                {
                    var columnsToCopyString = string.Join(", ", columnNamesInBothTables);
                    await ExecuteAsync(
                            db,
                            $"INSERT INTO {updatedTable.TableName} ({columnsToCopyString}) SELECT {columnsToCopyString} FROM {tempTableName}",
                            tx: innerTx,
                            cancellationToken: cancellationToken
                        )
                        .ConfigureAwait(false);
                }

                // drop the temp table
                await ExecuteAsync(
                        db,
                        $"DROP TABLE {tempTableName}",
                        tx: innerTx,
                        cancellationToken: cancellationToken
                    )
                    .ConfigureAwait(false);

                // commit the transaction only if we created it
                if (shouldCommit)
                {
                    await innerTx!.CommitAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch
        {
            // rollback only if we created the transaction
            if (shouldCommit && innerTx != null)
            {
                await innerTx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }
            throw;
        }
        finally
        {
            // dispose only if we created the transaction
            if (shouldCommit && innerTx != null)
            {
                await innerTx.DisposeAsync().ConfigureAwait(false);
            }
            // re-enable foreign key constraints
            await ExecuteAsync(
                    db,
                    "PRAGMA foreign_keys = 1",
                    tx: tx,
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);
        }
    }
}
