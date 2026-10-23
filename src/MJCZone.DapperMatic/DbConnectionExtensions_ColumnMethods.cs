// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic;

public static partial class DbConnectionExtensions
{
    #region IDatabaseColumnMethods

    /// <summary>
    /// Checks if a column exists in the specified table.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the column exists, otherwise false.</returns>
    public static Task<bool> DoesColumnExistAsync<T>(
        this IDbConnection db,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => db.DoesColumnExistAsync(
            typeof(T),
            columnName,
            tx,
            cancellationToken
        );

    /// <summary>
    /// Checks if a column exists in the specified table.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="type">The type representing the table.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the column exists, otherwise false.</returns>
    public static async Task<bool> DoesColumnExistAsync(
        this IDbConnection db,
        Type type,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(type);
        var (schemaName, tableName) = DmTableFactory.GetTableName(type);
        return await Database(db)
            .DoesColumnExistAsync(db, schemaName, tableName, columnName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Checks if a column exists in the specified table.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="columnExpression">The column expression.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the column exists, otherwise false.</returns>
    public static async Task<bool> DoesColumnExistAsync<T>(
        this IDbConnection db,
        Expression<Func<T, object>> columnExpression,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var (schemaName, tableName) = DmTableFactory.GetTableName(typeof(T));
        return await Database(db)
            .DoesColumnExistAsync(
                db,
                schemaName,
                tableName,
                DmTableFactory.GetColumnName(columnExpression),
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Checks if a column exists in the specified table.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the column exists, otherwise false.</returns>
    public static async Task<bool> DoesColumnExistAsync(
        this IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .DoesColumnExistAsync(db, schemaName, tableName, columnName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the details of a column in the specified table.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The column details, or null if the column does not exist.</returns>
    public static Task<DmColumn?> GetColumnAsync<T>(
        this IDbConnection db,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => db.GetColumnAsync(
            typeof(T),
            columnName,
            tx,
            cancellationToken
        );

    /// <summary>
    /// Gets the details of a column in the specified table.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="type">The type representing the table.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The column details, or null if the column does not exist.</returns>
    public static async Task<DmColumn?> GetColumnAsync(
        this IDbConnection db,
        Type type,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(type);
        var (schemaName, tableName) = DmTableFactory.GetTableName(type);
        return await Database(db)
            .GetColumnAsync(db, schemaName, tableName, columnName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the details of a column in the specified table.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="columnExpression">An expression representing the column.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The column details, or null if the column does not exist.</returns>
    public static async Task<DmColumn?> GetColumnAsync<T>(
        this IDbConnection db,
        Expression<Func<T, object>> columnExpression,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var (schemaName, tableName) = DmTableFactory.GetTableName(typeof(T));
        return await Database(db)
            .GetColumnAsync(
                db,
                schemaName,
                tableName,
                DmTableFactory.GetColumnName(columnExpression),
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the details of a column in the specified table.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The column details, or null if the column does not exist.</returns>
    public static async Task<DmColumn?> GetColumnAsync(
        this IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .GetColumnAsync(db, schemaName, tableName, columnName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the names of columns in the specified table.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="columnNameFilter">An optional filter for column names.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of column names.</returns>
    public static Task<List<string>> GetColumnNamesAsync<T>(
        this IDbConnection db,
        string? columnNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => db.GetColumnNamesAsync(
            typeof(T),
            columnNameFilter,
            tx,
            cancellationToken
        );

    /// <summary>
    /// Gets the names of columns in the specified table.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="type">The type representing the table.</param>
    /// <param name="columnNameFilter">An optional filter for column names.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of column names.</returns>
    public static async Task<List<string>> GetColumnNamesAsync(
        this IDbConnection db,
        Type type,
        string? columnNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(type);
        var (schemaName, tableName) = DmTableFactory.GetTableName(type);
        return await Database(db)
            .GetColumnNamesAsync(db, schemaName, tableName, columnNameFilter, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the names of columns in the specified table.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnNameFilter">An optional filter for column names.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of column names.</returns>
    // Ahead of the (Type, filter) overload, so that GetColumnNamesAsync(null, "table") stays this one.
    [OverloadResolutionPriority(1)]
    public static async Task<List<string>> GetColumnNamesAsync(
        this IDbConnection db,
        string? schemaName,
        string tableName,
        string? columnNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .GetColumnNamesAsync(db, schemaName, tableName, columnNameFilter, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the details of columns in the specified table.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="columnNameFilter">An optional filter for column names.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of column details.</returns>
    public static Task<List<DmColumn>> GetColumnsAsync<T>(
        this IDbConnection db,
        string? columnNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => db.GetColumnsAsync(
            typeof(T),
            columnNameFilter,
            tx,
            cancellationToken
        );

    /// <summary>
    /// Gets the details of columns in the specified table.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="type">The type representing the table.</param>
    /// <param name="columnNameFilter">An optional filter for column names.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of column details.</returns>
    public static async Task<List<DmColumn>> GetColumnsAsync(
        this IDbConnection db,
        Type type,
        string? columnNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(type);
        var (schemaName, tableName) = DmTableFactory.GetTableName(type);
        return await Database(db)
            .GetColumnsAsync(db, schemaName, tableName, columnNameFilter, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the details of columns in the specified table.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnNameFilter">An optional filter for column names.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of column details.</returns>
    // Ahead of the (Type, filter) overload, so that GetColumnsAsync(null, "table") stays this one.
    [OverloadResolutionPriority(1)]
    public static async Task<List<DmColumn>> GetColumnsAsync(
        this IDbConnection db,
        string? schemaName,
        string tableName,
        string? columnNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .GetColumnsAsync(db, schemaName, tableName, columnNameFilter, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a column if it does not exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="column">The column details.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the column was created, otherwise false.</returns>
    public static async Task<bool> CreateColumnIfNotExistsAsync(
        this IDbConnection db,
        DmColumn column,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db).CreateColumnIfNotExistsAsync(db, column, tx, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a column if it does not exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="dotnetType">The .NET type of the column.</param>
    /// <param name="configureColumn">An optional action to configure the column.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the column was created, otherwise false.</returns>
    public static async Task<bool> CreateColumnIfNotExistsAsync(
        this IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        Type dotnetType,
        Action<DmColumn>? configureColumn = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var column = new DmColumn(schemaName, tableName, columnName, dotnetType);
        configureColumn?.Invoke(column);
        return await Database(db).CreateColumnIfNotExistsAsync(db, column, tx, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a column if it does not exist.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="dotnetType">The .NET type of the column.</param>
    /// <param name="configureColumn">An optional action to configure the column.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the column was created, otherwise false.</returns>
    public static Task<bool> CreateColumnIfNotExistsAsync<T>(
        this IDbConnection db,
        string columnName,
        Type dotnetType,
        Action<DmColumn>? configureColumn = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => db.CreateColumnIfNotExistsAsync(
            typeof(T),
            columnName,
            dotnetType,
            configureColumn,
            tx,
            cancellationToken
        );

    /// <summary>
    /// Creates a column if it does not exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="type">The type representing the table.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="dotnetType">The .NET type of the column.</param>
    /// <param name="configureColumn">An optional action to configure the column.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the column was created, otherwise false.</returns>
    public static async Task<bool> CreateColumnIfNotExistsAsync(
        this IDbConnection db,
        Type type,
        string columnName,
        Type dotnetType,
        Action<DmColumn>? configureColumn = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(type);
        var (schemaName, tableName) = DmTableFactory.GetTableName(type);
        var column = new DmColumn(schemaName, tableName, columnName, dotnetType);
        configureColumn?.Invoke(column);
        return await Database(db).CreateColumnIfNotExistsAsync(db, column, tx, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a column if it does not exist.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="dotnetType">The .NET type of the column.</param>
    /// <param name="providerDataType">The provider-specific data type of the column.</param>
    /// <param name="length">The length of the column.</param>
    /// <param name="precision">The precision of the column.</param>
    /// <param name="scale">The scale of the column.</param>
    /// <param name="checkExpression">The check expression for the column.</param>
    /// <param name="defaultExpression">The default expression for the column.</param>
    /// <param name="isNullable">Whether the column is nullable.</param>
    /// <param name="isPrimaryKey">Whether the column is a primary key.</param>
    /// <param name="isAutoIncrement">Whether the column is auto-incremented.</param>
    /// <param name="isUnique">Whether the column is unique.</param>
    /// <param name="isUnicode">Whether the column supports unicode characters.</param>
    /// <param name="isIndexed">Whether the column is indexed.</param>
    /// <param name="isForeignKey">Whether the column is a foreign key.</param>
    /// <param name="referencedTableName">The referenced table name for the foreign key.</param>
    /// <param name="referencedColumnName">The referenced column name for the foreign key.</param>
    /// <param name="onDelete">The action to take on delete.</param>
    /// <param name="onUpdate">The action to take on update.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the column was created, otherwise false.</returns>
    public static Task<bool> CreateColumnIfNotExistsAsync<T>(
        this IDbConnection db,
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
    ) => db.CreateColumnIfNotExistsAsync(
            typeof(T),
            columnName,
            dotnetType,
            providerDataType,
            length,
            precision,
            scale,
            checkExpression,
            defaultExpression,
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
            tx,
            cancellationToken
        );

    /// <summary>
    /// Creates a column if it does not exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="type">The type representing the table.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="dotnetType">The .NET type of the column.</param>
    /// <param name="providerDataType">The provider-specific data type of the column.</param>
    /// <param name="length">The length of the column.</param>
    /// <param name="precision">The precision of the column.</param>
    /// <param name="scale">The scale of the column.</param>
    /// <param name="checkExpression">The check expression for the column.</param>
    /// <param name="defaultExpression">The default expression for the column.</param>
    /// <param name="isNullable">Whether the column is nullable.</param>
    /// <param name="isPrimaryKey">Whether the column is a primary key.</param>
    /// <param name="isAutoIncrement">Whether the column is auto-incremented.</param>
    /// <param name="isUnique">Whether the column is unique.</param>
    /// <param name="isUnicode">Whether the column supports unicode characters.</param>
    /// <param name="isIndexed">Whether the column is indexed.</param>
    /// <param name="isForeignKey">Whether the column is a foreign key.</param>
    /// <param name="referencedTableName">The referenced table name for the foreign key.</param>
    /// <param name="referencedColumnName">The referenced column name for the foreign key.</param>
    /// <param name="onDelete">The action to take on delete.</param>
    /// <param name="onUpdate">The action to take on update.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the column was created, otherwise false.</returns>
    public static async Task<bool> CreateColumnIfNotExistsAsync(
        this IDbConnection db,
        Type type,
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
        ArgumentNullException.ThrowIfNull(type);
        var (schemaName, tableName) = DmTableFactory.GetTableName(type);
        return await Database(db)
            .CreateColumnIfNotExistsAsync(
                db,
                schemaName,
                tableName,
                columnName,
                dotnetType,
                providerDataType,
                length,
                precision,
                scale,
                checkExpression,
                defaultExpression,
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
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a column if it does not exist.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="columnExpression">The column expression.</param>
    /// <param name="configureColumn">An optional action to configure the column.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the column was created, otherwise false.</returns>
    public static async Task<bool> CreateColumnIfNotExistsAsync<T>(
        this IDbConnection db,
        Expression<Func<T, object>> columnExpression,
        Action<DmColumn>? configureColumn = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var table = DmTableFactory.GetTable(typeof(T));

        // The model is shared process-wide, so the callback configures a copy of its column.
        var column = table
            .Columns.FirstOrDefault(c => c.ColumnName == DmTableFactory.GetColumnName(columnExpression))
            ?.Copy();
        if (column == null)
        {
            return false;
        }

        column.SchemaName = table.SchemaName;
        column.TableName = table.TableName;
        configureColumn?.Invoke(column);

        return await db.CreateColumnIfNotExistsAsync(column, tx, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a column if it does not exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="dotnetType">The .NET type of the column.</param>
    /// <param name="providerDataType">The provider-specific data type of the column.</param>
    /// <param name="length">The length of the column.</param>
    /// <param name="precision">The precision of the column.</param>
    /// <param name="scale">The scale of the column.</param>
    /// <param name="checkExpression">The check expression for the column.</param>
    /// <param name="defaultExpression">The default expression for the column.</param>
    /// <param name="isNullable">Whether the column is nullable.</param>
    /// <param name="isPrimaryKey">Whether the column is a primary key.</param>
    /// <param name="isAutoIncrement">Whether the column is auto-incremented.</param>
    /// <param name="isUnique">Whether the column is unique.</param>
    /// <param name="isUnicode">Whether the column supports unicode characters.</param>
    /// <param name="isIndexed">Whether the column is indexed.</param>
    /// <param name="isForeignKey">Whether the column is a foreign key.</param>
    /// <param name="referencedTableName">The referenced table name for the foreign key.</param>
    /// <param name="referencedColumnName">The referenced column name for the foreign key.</param>
    /// <param name="onDelete">The action to take on delete.</param>
    /// <param name="onUpdate">The action to take on update.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the column was created, otherwise false.</returns>
    public static async Task<bool> CreateColumnIfNotExistsAsync(
        this IDbConnection db,
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
        return await Database(db)
            .CreateColumnIfNotExistsAsync(
                db,
                schemaName,
                tableName,
                columnName,
                dotnetType,
                providerDataType,
                length,
                precision,
                scale,
                checkExpression,
                defaultExpression,
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
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Drops a column if it exists.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the column was dropped, otherwise false.</returns>
    public static Task<bool> DropColumnIfExistsAsync<T>(
        this IDbConnection db,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => db.DropColumnIfExistsAsync(
            typeof(T),
            columnName,
            tx,
            cancellationToken
        );

    /// <summary>
    /// Drops a column if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="type">The type representing the table.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the column was dropped, otherwise false.</returns>
    public static async Task<bool> DropColumnIfExistsAsync(
        this IDbConnection db,
        Type type,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(type);
        var (schemaName, tableName) = DmTableFactory.GetTableName(type);
        return await Database(db)
            .DropColumnIfExistsAsync(db, schemaName, tableName, columnName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Drops a column if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the column was dropped, otherwise false.</returns>
    public static async Task<bool> DropColumnIfExistsAsync(
        this IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .DropColumnIfExistsAsync(db, schemaName, tableName, columnName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Renames a column if it exists.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="newColumnName">The new column name.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the column was renamed, otherwise false.</returns>
    public static Task<bool> RenameColumnIfExistsAsync<T>(
        this IDbConnection db,
        string columnName,
        string newColumnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => db.RenameColumnIfExistsAsync(
            typeof(T),
            columnName,
            newColumnName,
            tx,
            cancellationToken
        );

    /// <summary>
    /// Renames a column if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="type">The type representing the table.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="newColumnName">The new column name.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the column was renamed, otherwise false.</returns>
    public static async Task<bool> RenameColumnIfExistsAsync(
        this IDbConnection db,
        Type type,
        string columnName,
        string newColumnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(type);
        var (schemaName, tableName) = DmTableFactory.GetTableName(type);
        return await Database(db)
            .RenameColumnIfExistsAsync(db, schemaName, tableName, columnName, newColumnName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Renames a column if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="newColumnName">The new column name.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the column was renamed, otherwise false.</returns>
    public static async Task<bool> RenameColumnIfExistsAsync(
        this IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        string newColumnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .RenameColumnIfExistsAsync(db, schemaName, tableName, columnName, newColumnName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Renames a column if it exists.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="columnExpression">An expression representing the column that will be renamed.</param>
    /// <param name="oldColumnName">The existing column name that will be renamed.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the column was renamed, otherwise false.</returns>
    public static async Task<bool> RenameColumnIfExistsAsync<T>(
        this IDbConnection db,
        Expression<Func<T, object>> columnExpression,
        string oldColumnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var (schemaName, tableName) = DmTableFactory.GetTableName(typeof(T));
        return await Database(db)
            .RenameColumnIfExistsAsync(
                db,
                schemaName,
                tableName,
                oldColumnName,
                DmTableFactory.GetColumnName(columnExpression),
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Changes the data type and nullability of a column if it exists, keeping the rest of its definition.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="sqlType">The new SQL data type, including any length, precision and scale, for example <c>varchar(200)</c>.</param>
    /// <param name="isNullable">Whether the column allows nulls afterwards.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the column exists and now has the given type and nullability, otherwise false.</returns>
    /// <remarks>
    /// The column's collation, default and comment are kept, as is its generated expression on MySQL and
    /// MariaDB. Nothing checks that existing values fit: shortening a column or making it NOT NULL fails on
    /// data that does not fit, and some providers refuse to alter a column that an index, constraint or view
    /// depends on.
    /// </remarks>
    public static async Task<bool> AlterColumnIfExistsAsync(
        this IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        string sqlType,
        bool isNullable,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .AlterColumnIfExistsAsync(db, schemaName, tableName, columnName, sqlType, isNullable, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Changes the data type and nullability of a column if it exists to match a column definition, with the
    /// type rendered the same way CREATE TABLE renders it.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="column">The column definition to match.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the column exists and now has the definition's type and nullability, otherwise false.</returns>
    public static async Task<bool> AlterColumnIfExistsAsync(
        this IDbConnection db,
        DmColumn column,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db).AlterColumnIfExistsAsync(db, column, tx, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Changes the data type and nullability of a column if it exists, keeping the rest of its definition.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="sqlType">The new SQL data type, including any length, precision and scale, for example <c>varchar(200)</c>.</param>
    /// <param name="isNullable">Whether the column allows nulls afterwards.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the column exists and now has the given type and nullability, otherwise false.</returns>
    public static Task<bool> AlterColumnIfExistsAsync<T>(
        this IDbConnection db,
        string columnName,
        string sqlType,
        bool isNullable,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => db.AlterColumnIfExistsAsync(
            typeof(T),
            columnName,
            sqlType,
            isNullable,
            tx,
            cancellationToken
        );

    /// <summary>
    /// Changes the data type and nullability of a column if it exists, keeping the rest of its definition.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="type">The type representing the table.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="sqlType">The new SQL data type, including any length, precision and scale, for example <c>varchar(200)</c>.</param>
    /// <param name="isNullable">Whether the column allows nulls afterwards.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the column exists and now has the given type and nullability, otherwise false.</returns>
    public static async Task<bool> AlterColumnIfExistsAsync(
        this IDbConnection db,
        Type type,
        string columnName,
        string sqlType,
        bool isNullable,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(type);
        var (schemaName, tableName) = DmTableFactory.GetTableName(type);
        return await Database(db)
            .AlterColumnIfExistsAsync(db, schemaName, tableName, columnName, sqlType, isNullable, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Changes the data type and nullability of a column if it exists to match the model's definition of it,
    /// with the type rendered the same way CREATE TABLE renders it.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="columnExpression">The property the column is mapped from.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// True if the column exists and now has the model's type and nullability; false if it does not exist, or
    /// the property is not mapped to a column.
    /// </returns>
    public static async Task<bool> AlterColumnIfExistsAsync<T>(
        this IDbConnection db,
        Expression<Func<T, object>> columnExpression,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var table = DmTableFactory.GetTable(typeof(T));

        // The model is shared process-wide, so the table name is set on a copy of its column.
        var column = table
            .Columns.FirstOrDefault(c => c.ColumnName == DmTableFactory.GetColumnName(columnExpression))
            ?.Copy();
        if (column == null)
        {
            return false;
        }

        column.SchemaName = table.SchemaName;
        column.TableName = table.TableName;

        return await Database(db).AlterColumnIfExistsAsync(db, column, tx, cancellationToken).ConfigureAwait(false);
    }
    #endregion // IDatabaseColumnMethods
}
