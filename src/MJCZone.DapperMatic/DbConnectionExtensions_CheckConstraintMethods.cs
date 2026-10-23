// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Runtime.CompilerServices;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic;

public static partial class DbConnectionExtensions
{
    #region IDatabaseCheckConstraintMethods

    /// <summary>
    /// Checks if a check constraint exists in the specified table.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the constraint exists, otherwise false.</returns>
    public static Task<bool> DoesCheckConstraintExistAsync<T>(
        this IDbConnection db,
        string constraintName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => db.DoesCheckConstraintExistAsync(
            typeof(T),
            constraintName,
            tx,
            cancellationToken
        );

    /// <summary>
    /// Checks if a check constraint exists in the specified table.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="type">The type representing the table.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the constraint exists, otherwise false.</returns>
    public static async Task<bool> DoesCheckConstraintExistAsync(
        this IDbConnection db,
        Type type,
        string constraintName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(type);
        var (schemaName, tableName) = DmTableFactory.GetTableName(type);
        return await Database(db)
            .DoesCheckConstraintExistAsync(db, schemaName, tableName, constraintName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Checks if a check constraint exists in the specified table.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the constraint exists, otherwise false.</returns>
    public static async Task<bool> DoesCheckConstraintExistAsync(
        this IDbConnection db,
        string? schemaName,
        string tableName,
        string constraintName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .DoesCheckConstraintExistAsync(db, schemaName, tableName, constraintName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Checks if a check constraint exists on the specified column.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the constraint exists, otherwise false.</returns>
    public static Task<bool> DoesCheckConstraintExistOnColumnAsync<T>(
        this IDbConnection db,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => db.DoesCheckConstraintExistOnColumnAsync(
            typeof(T),
            columnName,
            tx,
            cancellationToken
        );

    /// <summary>
    /// Checks if a check constraint exists on the specified column.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="type">The type representing the table.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the constraint exists, otherwise false.</returns>
    public static async Task<bool> DoesCheckConstraintExistOnColumnAsync(
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
            .DoesCheckConstraintExistOnColumnAsync(db, schemaName, tableName, columnName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Checks if a check constraint exists on the specified column.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the constraint exists, otherwise false.</returns>
    public static async Task<bool> DoesCheckConstraintExistOnColumnAsync(
        this IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .DoesCheckConstraintExistOnColumnAsync(db, schemaName, tableName, columnName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a check constraint if it does not exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="constraint">The check constraint.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the constraint was created, otherwise false.</returns>
    public static async Task<bool> CreateCheckConstraintIfNotExistsAsync(
        this IDbConnection db,
        DmCheckConstraint constraint,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .CreateCheckConstraintIfNotExistsAsync(db, constraint, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a check constraint if it does not exist.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="expression">The constraint expression.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the constraint was created, otherwise false.</returns>
    public static Task<bool> CreateCheckConstraintIfNotExistsAsync<T>(
        this IDbConnection db,
        string? columnName,
        string constraintName,
        string expression,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => db.CreateCheckConstraintIfNotExistsAsync(
            typeof(T),
            columnName,
            constraintName,
            expression,
            tx,
            cancellationToken
        );

    /// <summary>
    /// Creates a check constraint if it does not exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="type">The type representing the table.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="expression">The constraint expression.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the constraint was created, otherwise false.</returns>
    public static async Task<bool> CreateCheckConstraintIfNotExistsAsync(
        this IDbConnection db,
        Type type,
        string? columnName,
        string constraintName,
        string expression,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(type);
        var (schemaName, tableName) = DmTableFactory.GetTableName(type);
        return await Database(db)
            .CreateCheckConstraintIfNotExistsAsync(
                db,
                schemaName,
                tableName,
                columnName,
                constraintName,
                expression,
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a check constraint if it does not exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="expression">The constraint expression.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the constraint was created, otherwise false.</returns>
    public static async Task<bool> CreateCheckConstraintIfNotExistsAsync(
        this IDbConnection db,
        string? schemaName,
        string tableName,
        string? columnName,
        string constraintName,
        string expression,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .CreateCheckConstraintIfNotExistsAsync(
                db,
                schemaName,
                tableName,
                columnName,
                constraintName,
                expression,
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the check constraint with the specified name.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The check constraint, or null if not found.</returns>
    public static Task<DmCheckConstraint?> GetCheckConstraintAsync<T>(
        this IDbConnection db,
        string constraintName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => db.GetCheckConstraintAsync(
            typeof(T),
            constraintName,
            tx,
            cancellationToken
        );

    /// <summary>
    /// Gets the check constraint with the specified name.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="type">The type representing the table.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The check constraint, or null if not found.</returns>
    public static async Task<DmCheckConstraint?> GetCheckConstraintAsync(
        this IDbConnection db,
        Type type,
        string constraintName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(type);
        var (schemaName, tableName) = DmTableFactory.GetTableName(type);
        return await Database(db)
            .GetCheckConstraintAsync(db, schemaName, tableName, constraintName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the check constraint with the specified name.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The check constraint, or null if not found.</returns>
    public static async Task<DmCheckConstraint?> GetCheckConstraintAsync(
        this IDbConnection db,
        string? schemaName,
        string tableName,
        string constraintName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .GetCheckConstraintAsync(db, schemaName, tableName, constraintName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the name of the check constraint on the specified column.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The name of the check constraint, or null if not found.</returns>
    public static Task<string?> GetCheckConstraintNameOnColumnAsync<T>(
        this IDbConnection db,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => db.GetCheckConstraintNameOnColumnAsync(
            typeof(T),
            columnName,
            tx,
            cancellationToken
        );

    /// <summary>
    /// Gets the name of the check constraint on the specified column.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="type">The type representing the table.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The name of the check constraint, or null if not found.</returns>
    public static async Task<string?> GetCheckConstraintNameOnColumnAsync(
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
            .GetCheckConstraintNameOnColumnAsync(db, schemaName, tableName, columnName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the name of the check constraint on the specified column.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The name of the check constraint, or null if not found.</returns>
    public static async Task<string?> GetCheckConstraintNameOnColumnAsync(
        this IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .GetCheckConstraintNameOnColumnAsync(db, schemaName, tableName, columnName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the names of the check constraints in the specified table.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="constraintNameFilter">The constraint name filter.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of check constraint names.</returns>
    public static Task<List<string>> GetCheckConstraintNamesAsync<T>(
        this IDbConnection db,
        string? constraintNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => db.GetCheckConstraintNamesAsync(
            typeof(T),
            constraintNameFilter,
            tx,
            cancellationToken
        );

    /// <summary>
    /// Gets the names of the check constraints in the specified table.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="type">The type representing the table.</param>
    /// <param name="constraintNameFilter">The constraint name filter.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of check constraint names.</returns>
    public static async Task<List<string>> GetCheckConstraintNamesAsync(
        this IDbConnection db,
        Type type,
        string? constraintNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(type);
        var (schemaName, tableName) = DmTableFactory.GetTableName(type);
        return await Database(db)
            .GetCheckConstraintNamesAsync(db, schemaName, tableName, constraintNameFilter, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the names of the check constraints in the specified table.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintNameFilter">The constraint name filter.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of check constraint names.</returns>
    // Ahead of the (Type, filter) overload, so that GetCheckConstraintNamesAsync(null, "table") stays this one.
    [OverloadResolutionPriority(1)]
    public static async Task<List<string>> GetCheckConstraintNamesAsync(
        this IDbConnection db,
        string? schemaName,
        string tableName,
        string? constraintNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .GetCheckConstraintNamesAsync(db, schemaName, tableName, constraintNameFilter, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the check constraint on the specified column.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The check constraint, or null if not found.</returns>
    public static Task<DmCheckConstraint?> GetCheckConstraintOnColumnAsync<T>(
        this IDbConnection db,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => db.GetCheckConstraintOnColumnAsync(
            typeof(T),
            columnName,
            tx,
            cancellationToken
        );

    /// <summary>
    /// Gets the check constraint on the specified column.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="type">The type representing the table.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The check constraint, or null if not found.</returns>
    public static async Task<DmCheckConstraint?> GetCheckConstraintOnColumnAsync(
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
            .GetCheckConstraintOnColumnAsync(db, schemaName, tableName, columnName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the check constraint on the specified column.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The check constraint, or null if not found.</returns>
    public static async Task<DmCheckConstraint?> GetCheckConstraintOnColumnAsync(
        this IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .GetCheckConstraintOnColumnAsync(db, schemaName, tableName, columnName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the check constraints in the specified table.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="constraintNameFilter">The constraint name filter.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of check constraints.</returns>
    public static Task<List<DmCheckConstraint>> GetCheckConstraintsAsync<T>(
        this IDbConnection db,
        string? constraintNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => db.GetCheckConstraintsAsync(
            typeof(T),
            constraintNameFilter,
            tx,
            cancellationToken
        );

    /// <summary>
    /// Gets the check constraints in the specified table.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="type">The type representing the table.</param>
    /// <param name="constraintNameFilter">The constraint name filter.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of check constraints.</returns>
    public static async Task<List<DmCheckConstraint>> GetCheckConstraintsAsync(
        this IDbConnection db,
        Type type,
        string? constraintNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(type);
        var (schemaName, tableName) = DmTableFactory.GetTableName(type);
        return await Database(db)
            .GetCheckConstraintsAsync(db, schemaName, tableName, constraintNameFilter, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the check constraints in the specified table.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintNameFilter">The constraint name filter.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of check constraints.</returns>
    // Ahead of the (Type, filter) overload, so that GetCheckConstraintsAsync(null, "table") stays this one.
    [OverloadResolutionPriority(1)]
    public static async Task<List<DmCheckConstraint>> GetCheckConstraintsAsync(
        this IDbConnection db,
        string? schemaName,
        string tableName,
        string? constraintNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .GetCheckConstraintsAsync(db, schemaName, tableName, constraintNameFilter, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Drops the check constraint if it exists.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the constraint was dropped, otherwise false.</returns>
    public static Task<bool> DropCheckConstraintIfExistsAsync<T>(
        this IDbConnection db,
        string constraintName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => db.DropCheckConstraintIfExistsAsync(
            typeof(T),
            constraintName,
            tx,
            cancellationToken
        );

    /// <summary>
    /// Drops the check constraint if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="type">The type representing the table.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the constraint was dropped, otherwise false.</returns>
    public static async Task<bool> DropCheckConstraintIfExistsAsync(
        this IDbConnection db,
        Type type,
        string constraintName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(type);
        var (schemaName, tableName) = DmTableFactory.GetTableName(type);
        return await Database(db)
            .DropCheckConstraintIfExistsAsync(db, schemaName, tableName, constraintName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Drops the check constraint if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the constraint was dropped, otherwise false.</returns>
    public static async Task<bool> DropCheckConstraintIfExistsAsync(
        this IDbConnection db,
        string? schemaName,
        string tableName,
        string constraintName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .DropCheckConstraintIfExistsAsync(db, schemaName, tableName, constraintName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Drops the check constraint on the specified column if it exists.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the constraint was dropped, otherwise false.</returns>
    public static Task<bool> DropCheckConstraintOnColumnIfExistsAsync<T>(
        this IDbConnection db,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => db.DropCheckConstraintOnColumnIfExistsAsync(
            typeof(T),
            columnName,
            tx,
            cancellationToken
        );

    /// <summary>
    /// Drops the check constraint on the specified column if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="type">The type representing the table.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the constraint was dropped, otherwise false.</returns>
    public static async Task<bool> DropCheckConstraintOnColumnIfExistsAsync(
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
            .DropCheckConstraintOnColumnIfExistsAsync(db, schemaName, tableName, columnName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Drops the check constraint on the specified column if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the constraint was dropped, otherwise false.</returns>
    public static async Task<bool> DropCheckConstraintOnColumnIfExistsAsync(
        this IDbConnection db,
        string? schemaName,
        string tableName,
        string columnName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .DropCheckConstraintOnColumnIfExistsAsync(db, schemaName, tableName, columnName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Renames a check constraint if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The current name.</param>
    /// <param name="newConstraintName">The new name.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if it was renamed; false if it does not exist, or the new name is already taken.</returns>
    public static async Task<bool> RenameCheckConstraintIfExistsAsync(
        this IDbConnection db,
        string? schemaName,
        string tableName,
        string constraintName,
        string newConstraintName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .RenameCheckConstraintIfExistsAsync(db, schemaName, tableName, constraintName, newConstraintName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Renames a check constraint if it exists.
    /// </summary>
    /// <typeparam name="T">The type representing the table.</typeparam>
    /// <param name="db">The database connection.</param>
    /// <param name="constraintName">The current name.</param>
    /// <param name="newConstraintName">The new name.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if it was renamed; false if it does not exist, or the new name is already taken.</returns>
    public static Task<bool> RenameCheckConstraintIfExistsAsync<T>(
        this IDbConnection db,
        string constraintName,
        string newConstraintName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => db.RenameCheckConstraintIfExistsAsync(
            typeof(T),
            constraintName,
            newConstraintName,
            tx,
            cancellationToken
        );

    /// <summary>
    /// Renames a check constraint if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="type">The type representing the table.</param>
    /// <param name="constraintName">The current name.</param>
    /// <param name="newConstraintName">The new name.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if it was renamed; false if it does not exist, or the new name is already taken.</returns>
    public static async Task<bool> RenameCheckConstraintIfExistsAsync(
        this IDbConnection db,
        Type type,
        string constraintName,
        string newConstraintName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(type);
        var (schemaName, tableName) = DmTableFactory.GetTableName(type);
        return await Database(db)
            .RenameCheckConstraintIfExistsAsync(db, schemaName, tableName, constraintName, newConstraintName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    #endregion // IDatabaseCheckConstraintMethods
}
