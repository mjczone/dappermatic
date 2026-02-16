// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Text;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Providers.Base;

public abstract partial class DatabaseMethodsBase
{
    /// <summary>
    /// Checks if a table exists in the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the table exists, otherwise false.</returns>
    public virtual async Task<bool> DoesTableExistAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var (sql, parameters) = SqlDoesTableExist(schemaName, tableName);

        var result = await ExecuteScalarAsync<int>(db, sql, parameters, tx: tx, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result > 0;
    }

    /// <summary>
    /// Creates tables if they do not exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="tables">The tables to create.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task CreateTablesIfNotExistsAsync(
        IDbConnection db,
        DmTable[] tables,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var afterAllTablesConstraints = new List<DmTable>();

        foreach (var table in tables)
        {
            // CreateTableIfNotExistsAsync returns false if table already exists - that's OK, continue to next table
            await CreateTableIfNotExistsAsync(db, table, afterAllTablesConstraints, tx, cancellationToken)
                .ConfigureAwait(false);
        }

        // Add foreign keys AFTER all tables are created
        foreach (var foreignKeyConstraint in afterAllTablesConstraints.SelectMany(x => x.ForeignKeyConstraints))
        {
            await CreateForeignKeyConstraintIfNotExistsAsync(
                    db,
                    foreignKeyConstraint,
                    tx: tx,
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Creates a table if it does not exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="table">The table to create.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the table was created, otherwise false.</returns>
    public virtual async Task<bool> CreateTableIfNotExistsAsync(
        IDbConnection db,
        DmTable table,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await CreateTableIfNotExistsAsync(db, table, null, tx, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a table if it does not exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="table">The table to create.</param>
    /// <param name="afterAllTablesConstraints">If NULL, then the foreign keys will get added inline, or as table constraints, otherwise, if a list is passed, they'll get processed outside this function.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the table was created, otherwise false.</returns>
    protected virtual async Task<bool> CreateTableIfNotExistsAsync(
        IDbConnection db,
        DmTable table,
        List<DmTable>? afterAllTablesConstraints,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(table, nameof(table));
        ArgumentException.ThrowIfNullOrWhiteSpace(table.TableName, nameof(table.TableName));

        if (table.Columns == null || table.Columns.Count == 0)
        {
            throw new ArgumentException("At least one column is required.", nameof(table));
        }

        if (
            await DoesTableExistAsync(db, table.SchemaName, table.TableName, tx, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            return false;
        }

        var dbVersion = await GetDatabaseVersionAsync(db, tx, cancellationToken).ConfigureAwait(false);

        var supportsOrderedKeysInConstraints = await SupportsOrderedKeysInConstraintsAsync(
                db,
                tx: tx,
                cancellationToken
            )
            .ConfigureAwait(false);

        var (schemaName, tableName, _) = NormalizeNames(table.SchemaName, table.TableName);

        var sql = new StringBuilder();
        sql.Append($"CREATE TABLE {GetSchemaQualifiedIdentifierName(schemaName, tableName)} (");

        var tableConstraints = new DmTable(
            schemaName,
            tableName,
            [],
            table.PrimaryKeyConstraint,
            [.. table.CheckConstraints],
            [.. table.DefaultConstraints],
            [.. table.UniqueConstraints],
            [.. table.ForeignKeyConstraints],
            [.. table.Indexes]
        );

        afterAllTablesConstraints?.Add(tableConstraints);

        // if there are multiple columns with a primary key,
        // we need to add the primary key as a constraint, and not inline
        // with the column definition.
        if (table.PrimaryKeyConstraint == null && table.Columns.Count(c => c.IsPrimaryKey) > 1)
        {
            var pkColumns = table.Columns.Where(c => c.IsPrimaryKey).ToArray();
            var pkConstraintName = DbProviderUtils.GeneratePrimaryKeyConstraintName(
                tableName,
                pkColumns.Select(c => c.ColumnName).ToArray()
            );

            // The column definition builder will detect the primary key constraint is
            // already added and disregard adding it again.
            tableConstraints.PrimaryKeyConstraint = new DmPrimaryKeyConstraint(
                schemaName,
                tableName,
                pkConstraintName,
                [.. pkColumns.Select(c => new DmOrderedColumn(c.ColumnName))]
            );
        }

        for (var i = 0; i < table.Columns.Count; i++)
        {
            sql.AppendLine();
            sql.Append(i == 0 ? "  " : "  , ");

            var column = table.Columns[i];
            column.SchemaName = schemaName;
            column.TableName = tableName;

            if (afterAllTablesConstraints != null)
            {
                // the caller of this function wants to process the foreign keys
                // outside this function.
                if (
                    column.IsForeignKey
                    && !string.IsNullOrWhiteSpace(column.ReferencedTableName)
                    && !string.IsNullOrWhiteSpace(column.ReferencedColumnName)
                    && tableConstraints.ForeignKeyConstraints.All(fk =>
                        !fk.SourceColumns.Any(c =>
                            c.ColumnName.Equals(column.ColumnName, StringComparison.OrdinalIgnoreCase)
                        )
                    )
                )
                {
                    var fkConstraintName = DbProviderUtils.GenerateForeignKeyConstraintName(
                        tableName,
                        column.ColumnName,
                        column.ReferencedTableName,
                        column.ReferencedColumnName
                    );
                    tableConstraints.ForeignKeyConstraints.Add(
                        new DmForeignKeyConstraint(
                            schemaName,
                            tableName,
                            NormalizeName(fkConstraintName),
                            [new DmOrderedColumn(column.ColumnName)],
                            column.ReferencedTableName,
                            [new DmOrderedColumn(column.ReferencedColumnName)]
                        )
                    );
                }
            }

            var columnDefinitionSql = SqlInlineColumnDefinition(table, column, tableConstraints, dbVersion);
            sql.Append(columnDefinitionSql);
        }

        if (tableConstraints.PrimaryKeyConstraint != null)
        {
            sql.AppendLine();
            sql.Append("  ,");
            sql.Append(SqlInlinePrimaryKeyTableConstraint(table, tableConstraints.PrimaryKeyConstraint));
        }

        foreach (var check in tableConstraints.CheckConstraints)
        {
            check.SchemaName = schemaName;
            check.TableName = tableName;

            sql.AppendLine();
            sql.Append("  ,");
            sql.Append(SqlInlineCheckTableConstraint(table, check));
        }

        // Default constraints are added inline with the column definition always during CREATE TABLE and ADD COLUMN
        // foreach (var def in tableConstraints.DefaultConstraints)
        // {
        //     def.SchemaName = schemaName;
        //     def.TableName = tableName;
        //
        //     sql.AppendLine();
        //     sql.Append("  ,");
        //     sql.Append(SqlInlineDefaultTableConstraint(table, def));
        // }

        foreach (var uc in tableConstraints.UniqueConstraints)
        {
            uc.SchemaName = schemaName;
            uc.TableName = tableName;

            sql.AppendLine();
            sql.Append("  ,");
            sql.Append(SqlInlineUniqueTableConstraint(table, uc, supportsOrderedKeysInConstraints));
        }

        // When creating a single table, we can add the foreign keys inline.
        // We assume that the referenced table already exists.
        if (afterAllTablesConstraints == null && table.ForeignKeyConstraints.Count > 0)
        {
            foreach (var fk in table.ForeignKeyConstraints)
            {
                fk.SchemaName = schemaName;
                fk.TableName = tableName;

                sql.AppendLine();
                sql.Append("  ,");
                sql.Append(SqlInlineForeignKeyTableConstraint(table, fk));
            }
        }

        sql.AppendLine();
        sql.Append(')');

        // TODO: for MySQL, we need to add the ENGINE=InnoDB; at the end of the CREATE TABLE statement
        if (ProviderType == DbProviderType.MySql)
        {
            sql.Append(" DEFAULT CHARACTER SET utf8mb4 COLLATE `utf8mb4_unicode_ci` ENGINE = InnoDB");
        }

        sql.Append(';');

        var createTableSqlStatement = sql.ToString();

        await ExecuteAsync(db, createTableSqlStatement, tx: tx, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        // Add indexes AFTER the table is created
        foreach (var index in tableConstraints.Indexes)
        {
            index.SchemaName = schemaName;
            index.TableName = tableName;

            await CreateIndexIfNotExistsAsync(db, index, tx: tx, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>
    /// Creates a table if it does not exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columns">The columns of the table.</param>
    /// <param name="primaryKey">The primary key constraint.</param>
    /// <param name="checkConstraints">The check constraints.</param>
    /// <param name="defaultConstraints">The default constraints.</param>
    /// <param name="uniqueConstraints">The unique constraints.</param>
    /// <param name="foreignKeyConstraints">The foreign key constraints.</param>
    /// <param name="indexes">The indexes.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the table was created, otherwise false.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "StyleCop.CSharp.OrderingRules",
        "SA1202:Elements should be ordered by access",
        Justification = "Reviewed."
    )]
    public virtual async Task<bool> CreateTableIfNotExistsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        DmColumn[] columns,
        DmPrimaryKeyConstraint? primaryKey = null,
        DmCheckConstraint[]? checkConstraints = null,
        DmDefaultConstraint[]? defaultConstraints = null,
        DmUniqueConstraint[]? uniqueConstraints = null,
        DmForeignKeyConstraint[]? foreignKeyConstraints = null,
        DmIndex[]? indexes = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        if (columns == null || columns.Length == 0)
        {
            throw new ArgumentException("At least one column is required.", nameof(columns));
        }

        foreach (var column in columns)
        {
            column.SchemaName = schemaName;
            column.TableName = tableName;
        }

        return await CreateTableIfNotExistsAsync(
                db,
                new DmTable(
                    schemaName,
                    tableName,
                    columns,
                    primaryKey,
                    checkConstraints,
                    defaultConstraints,
                    uniqueConstraints,
                    foreignKeyConstraints,
                    indexes
                ),
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a table from the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The table if found, otherwise null.</returns>
    public virtual async Task<DmTable?> GetTableAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrEmpty(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        return (
            await GetTablesAsync(db, schemaName, tableName, tx, cancellationToken).ConfigureAwait(false)
        ).SingleOrDefault();
    }

    /// <summary>
    /// Gets the names of tables in the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableNameFilter">The table name filter.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of table names.</returns>
    public virtual async Task<List<string>> GetTableNamesAsync(
        IDbConnection db,
        string? schemaName,
        string? tableNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var (sql, parameters) = SqlGetTableNames(schemaName, tableNameFilter);
        return await QueryAsync<string>(db, sql, parameters, tx: tx, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets tables from the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableNameFilter">The table name filter.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of tables.</returns>
    public abstract Task<List<DmTable>> GetTablesAsync(
        IDbConnection db,
        string? schemaName,
        string? tableNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Drops a table if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the table was dropped, otherwise false.</returns>
    public virtual async Task<bool> DropTableIfExistsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var table = await GetTableAsync(db, schemaName, tableName, tx, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(table?.TableName))
        {
            return false;
        }

        schemaName = table.SchemaName;
        tableName = table.TableName;

        // drop all related objects
        // IMPORTANT: Drop foreign keys BEFORE indexes because MySQL creates implicit indexes for FKs
        foreach (var fk in table.ForeignKeyConstraints)
        {
            await DropForeignKeyConstraintIfExistsAsync(
                    db,
                    schemaName,
                    tableName,
                    fk.ConstraintName,
                    tx,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        foreach (var index in table.Indexes)
        {
            await DropIndexIfExistsAsync(db, schemaName, tableName, index.IndexName, tx, cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var uc in table.UniqueConstraints)
        {
            await DropUniqueConstraintIfExistsAsync(db, schemaName, tableName, uc.ConstraintName, tx, cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var dc in table.DefaultConstraints)
        {
            await DropDefaultConstraintIfExistsAsync(
                    db,
                    schemaName,
                    tableName,
                    dc.ConstraintName,
                    tx,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        // USUALLY, this is done by the database provider, and
        // it's not necessary to do it here.
        // foreach (var cc in table.CheckConstraints)
        // {
        //     await DropCheckConstraintIfExistsAsync(
        //             db,
        //             schemaName,
        //             tableName,
        //             cc.ConstraintName,
        //             tx,
        //             cancellationToken
        //         )
        //         .ConfigureAwait(false);
        // }

        // USUALLY, this is done by the database provider, and
        // it's not necessary to do it here.
        // await DropPrimaryKeyConstraintIfExistsAsync(
        //         db,
        //         schemaName,
        //         tableName,
        //         tx,
        //         cancellationToken
        //     )
        //     .ConfigureAwait(false);

        var sql = SqlDropTable(schemaName, tableName);

        await ExecuteAsync(db, sql, tx: tx, cancellationToken: cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Renames a table if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="newTableName">The new table name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the table was renamed, otherwise false.</returns>
    public virtual async Task<bool> RenameTableIfExistsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string newTableName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        if (string.IsNullOrWhiteSpace(newTableName))
        {
            throw new ArgumentException("New table name is required.", nameof(newTableName));
        }

        if (!await DoesTableExistAsync(db, schemaName, tableName, tx, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var sql = SqlRenameTable(schemaName, tableName, newTableName);

        await ExecuteAsync(db, sql, tx: tx, cancellationToken: cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Truncates a table if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the table was truncated, otherwise false.</returns>
    public virtual async Task<bool> TruncateTableIfExistsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        if (!await DoesTableExistAsync(db, schemaName, tableName, tx, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var sql = SqlTruncateTable(schemaName, tableName);

        await ExecuteAsync(db, sql, tx: tx, cancellationToken: cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Gets the indexes from the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableNameFilter">The table name filter.</param>
    /// <param name="indexNameFilter">The index name filter.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of indexes.</returns>
    protected abstract Task<List<DmIndex>> GetIndexesInternalAsync(
        IDbConnection db,
        string? schemaName,
        string? tableNameFilter = null,
        string? indexNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    );
}
