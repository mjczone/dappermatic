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
    /// Checks if a column exists in the specified table.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the column exists, otherwise false.</returns>
    public virtual async Task<bool> DoesColumnExistAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await GetColumnAsync(db, schemaName, tableName, columnName, tx, cancellationToken).ConfigureAwait(false)
            != null;
    }

    /// <summary>
    /// Creates a column if it does not already exist in the specified table.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="column">The column definition.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the column was created, otherwise false.</returns>
    public virtual async Task<bool> CreateColumnIfNotExistsAsync(
        IDbConnection db,
        DmColumn column,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(column.TableName))
        {
            throw new ArgumentException("Table name is required", nameof(column));
        }

        if (string.IsNullOrWhiteSpace(column.ColumnName))
        {
            throw new ArgumentException("Column name is required", nameof(column));
        }

        var table = await GetTableAsync(db, column.SchemaName, column.TableName, tx, cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(table?.TableName))
        {
            return false;
        }

        if (table.Columns.Any(c => c.ColumnName.Equals(column.ColumnName, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var dbVersion = await GetDatabaseVersionAsync(db, tx, cancellationToken).ConfigureAwait(false);

        var tableConstraints = new DmTable(table.SchemaName, table.TableName);

        // attach the existing primary key constraint if it exists to ensure that it doesn't get recreated
        if (table.PrimaryKeyConstraint != null)
        {
            tableConstraints.PrimaryKeyConstraint = table.PrimaryKeyConstraint;
        }

        var columnDefinitionSql = SqlInlineColumnDefinition(table, column, tableConstraints, dbVersion);

        var sql = new StringBuilder();
        sql.Append(
            $"ALTER TABLE {GetSchemaQualifiedIdentifierName(column.SchemaName, column.TableName)} ADD {columnDefinitionSql}"
        );

        await ExecuteAsync(db, sql.ToString(), tx: tx, cancellationToken: cancellationToken).ConfigureAwait(false);

        // ONLY add the primary key constraint if it didn't exist before and if it wasn't
        // already added as part of the column definition (in which case that tableConstraints.PrimaryKeyConstraint will be null)
        // will be null.
        if (tableConstraints.PrimaryKeyConstraint != null)
        {
            await CreatePrimaryKeyConstraintIfNotExistsAsync(
                    db,
                    tableConstraints.PrimaryKeyConstraint,
                    tx: tx,
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);
        }

        foreach (var checkConstraint in tableConstraints.CheckConstraints)
        {
            await CreateCheckConstraintIfNotExistsAsync(
                    db,
                    checkConstraint,
                    tx: tx,
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);
        }

        foreach (var defaultConstraint in tableConstraints.DefaultConstraints)
        {
            await CreateDefaultConstraintIfNotExistsAsync(
                    db,
                    defaultConstraint,
                    tx: tx,
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);
        }

        foreach (var uniqueConstraint in tableConstraints.UniqueConstraints)
        {
            await CreateUniqueConstraintIfNotExistsAsync(
                    db,
                    uniqueConstraint,
                    tx: tx,
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);
        }

        foreach (var foreignKeyConstraint in tableConstraints.ForeignKeyConstraints)
        {
            await CreateForeignKeyConstraintIfNotExistsAsync(
                    db,
                    foreignKeyConstraint,
                    tx: tx,
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);
        }

        foreach (var index in tableConstraints.Indexes)
        {
            await CreateIndexIfNotExistsAsync(db, index, tx: tx, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>
    /// Creates a column with the specified properties if it does not already exist in the specified table.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="dotnetType">The .NET type of the column.</param>
    /// <param name="providerDataType">The provider-specific data type.</param>
    /// <param name="length">The length of the column.</param>
    /// <param name="precision">The precision of the column.</param>
    /// <param name="scale">The scale of the column.</param>
    /// <param name="checkExpression">The check constraint expression.</param>
    /// <param name="defaultExpression">The default value expression.</param>
    /// <param name="isNullable">Indicates if the column is nullable.</param>
    /// <param name="isPrimaryKey">Indicates if the column is a primary key.</param>
    /// <param name="isAutoIncrement">Indicates if the column is auto-incremented.</param>
    /// <param name="isUnique">Indicates if the column is unique.</param>
    /// <param name="isUnicode">Indicates if the column supports unicode characters.</param>
    /// <param name="isIndexed">Indicates if the column is indexed.</param>
    /// <param name="isForeignKey">Indicates if the column is a foreign key.</param>
    /// <param name="referencedTableName">The referenced table name for foreign key.</param>
    /// <param name="referencedColumnName">The referenced column name for foreign key.</param>
    /// <param name="onDelete">The action on delete for foreign key.</param>
    /// <param name="onUpdate">The action on update for foreign key.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the column was created, otherwise false.</returns>
    public virtual async Task<bool> CreateColumnIfNotExistsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        Type dotnetType,
        string? providerDataType = null,
        int? length = null,
        int? precision = null,
        int? scale = null,
        string? checkExpression = null,
        string? defaultExpression = null,
        bool isNullable = true,
        bool isPrimaryKey = false,
        bool isAutoIncrement = false,
        bool isUnique = false,
        bool isUnicode = false,
        bool isIndexed = false,
        bool isForeignKey = false,
        string? referencedTableName = null,
        string? referencedColumnName = null,
        DmForeignKeyAction? onDelete = null,
        DmForeignKeyAction? onUpdate = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await CreateColumnIfNotExistsAsync(
                db,
                new DmColumn(
                    schemaName,
                    tableName,
                    columnName,
                    dotnetType,
                    providerDataType == null
                        ? null
                        : new Dictionary<DbProviderType, string> { { ProviderType, providerDataType } },
                    length,
                    precision,
                    scale,
                    isNullable,
                    isPrimaryKey,
                    isAutoIncrement,
                    isUnique,
                    isUnicode,
                    isIndexed,
                    isForeignKey,
                    referencedTableName,
                    referencedColumnName,
                    onDelete,
                    onUpdate,
                    checkExpression: checkExpression,
                    defaultExpression: defaultExpression
                ),
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves a column from the specified table.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The column definition if found, otherwise null.</returns>
    public virtual async Task<DmColumn?> GetColumnAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return (
            await GetColumnsAsync(db, schemaName, tableName, columnName, tx, cancellationToken).ConfigureAwait(false)
        ).FirstOrDefault();
    }

    /// <summary>
    /// Retrieves the names of columns from the specified table.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnNameFilter">The column name filter.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of column names.</returns>
    public virtual async Task<List<string>> GetColumnNamesAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string? columnNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var columns = await GetColumnsAsync(db, schemaName, tableName, columnNameFilter, tx, cancellationToken)
            .ConfigureAwait(false);
        return columns.Select(x => x.ColumnName).ToList();
    }

    /// <summary>
    /// Retrieves columns from the specified table.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnNameFilter">The column name filter.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of column definitions.</returns>
    public virtual async Task<List<DmColumn>> GetColumnsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string? columnNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        var table = await GetTableAsync(db, schemaName, tableName, tx, cancellationToken).ConfigureAwait(false);

        if (table == null)
        {
            return [];
        }

        var filter = string.IsNullOrWhiteSpace(columnNameFilter) ? null : ToSafeString(columnNameFilter);

        return string.IsNullOrWhiteSpace(filter)
            ? table.Columns
            : table.Columns.Where(c => c.ColumnName.IsWildcardPatternMatch(filter)).ToList();
    }

    /// <summary>
    /// Drops a column if it exists in the specified table.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the column was dropped, otherwise false.</returns>
    public virtual async Task<bool> DropColumnIfExistsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var table = await GetTableAsync(db, schemaName, tableName, tx, cancellationToken).ConfigureAwait(false);

        if (table == null)
        {
            return false;
        }

        (schemaName, tableName, columnName) = NormalizeNames(schemaName, tableName, columnName);

        var column = table.Columns.FirstOrDefault(c =>
            c.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase)
        );

        if (column == null)
        {
            return false;
        }

        // drop any related constraints
        if (column.IsPrimaryKey)
        {
            await DropPrimaryKeyConstraintIfExistsAsync(db, schemaName, tableName, tx, cancellationToken)
                .ConfigureAwait(false);
        }

        if (column.IsForeignKey)
        {
            await DropForeignKeyConstraintOnColumnIfExistsAsync(
                    db,
                    schemaName,
                    tableName,
                    column.ColumnName,
                    tx,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        if (column.IsUnique)
        {
            await DropUniqueConstraintOnColumnIfExistsAsync(
                    db,
                    schemaName,
                    tableName,
                    column.ColumnName,
                    tx,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        if (column.IsIndexed)
        {
            await DropIndexesOnColumnIfExistsAsync(db, schemaName, tableName, column.ColumnName, tx, cancellationToken)
                .ConfigureAwait(false);
        }

        await DropCheckConstraintOnColumnIfExistsAsync(db, schemaName, tableName, columnName, tx, cancellationToken)
            .ConfigureAwait(false);

        await DropDefaultConstraintOnColumnIfExistsAsync(db, schemaName, tableName, columnName, tx, cancellationToken)
            .ConfigureAwait(false);

        var sql = SqlDropColumn(schemaName, tableName, columnName);

        await ExecuteAsync(db, sql, tx: tx, cancellationToken: cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Renames a column if it exists in the specified table.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="newColumnName">The new column name.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the column was renamed, otherwise false.</returns>
    public virtual async Task<bool> RenameColumnIfExistsAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        string newColumnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        if (
            !await DoesColumnExistAsync(db, schemaName, tableName, columnName, tx, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            return false;
        }

        if (
            await DoesColumnExistAsync(db, schemaName, tableName, newColumnName, tx, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            return false;
        }

        (schemaName, tableName, columnName) = NormalizeNames(schemaName, tableName, columnName);

        var schemaQualifiedTableName = GetSchemaQualifiedIdentifierName(schemaName, tableName);

        // As of version 3.25.0 released September 2018, SQLite supports renaming columns
        await ExecuteAsync(
                db,
                $"""
                ALTER TABLE {schemaQualifiedTableName}
                                    RENAME COLUMN {columnName}
                                            TO {newColumnName}
                """,
                tx: tx,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);

        return true;
    }
}
