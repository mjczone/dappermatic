// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic;

public static partial class DbConnectionExtensions
{
    #region IDatabaseViewMethods

    /// <summary>
    /// Checks if a view exists in the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="viewName">The view name.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the view exists, otherwise false.</returns>
    public static async Task<bool> DoesViewExistAsync(
        this IDbConnection db,
        string? schemaName,
        string viewName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .DoesViewExistAsync(db, schemaName, viewName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a view if it does not exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="view">The view definition.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the view was created, otherwise false.</returns>
    public static async Task<bool> CreateViewIfNotExistsAsync(
        this IDbConnection db,
        DmView view,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .CreateViewIfNotExistsAsync(db, view, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a view if it does not exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="viewName">The view name.</param>
    /// <param name="viewDefinition">The view definition.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the view was created, otherwise false.</returns>
    public static async Task<bool> CreateViewIfNotExistsAsync(
        this IDbConnection db,
        string? schemaName,
        string viewName,
        string viewDefinition,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .CreateViewIfNotExistsAsync(
                db,
                schemaName,
                viewName,
                viewDefinition,
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Updates a view if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="viewName">The view name.</param>
    /// <param name="viewDefinition">The view definition.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the view was updated, otherwise false.</returns>
    public static async Task<bool> UpdateViewIfExistsAsync(
        this IDbConnection db,
        string? schemaName,
        string viewName,
        string viewDefinition,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        if (
            !await db.DropViewIfExistsAsync(schemaName, viewName, tx, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            return false;
        }

        return await db.CreateViewIfNotExistsAsync(
                schemaName,
                viewName,
                viewDefinition,
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a view from the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="viewName">The view name.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The view if found, otherwise null.</returns>
    public static async Task<DmView?> GetViewAsync(
        this IDbConnection db,
        string? schemaName,
        string viewName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .GetViewAsync(db, schemaName, viewName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a list of views from the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="viewNameFilter">The view name filter.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of views.</returns>
    public static async Task<List<DmView>> GetViewsAsync(
        this IDbConnection db,
        string? schemaName,
        string? viewNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .GetViewsAsync(db, schemaName, viewNameFilter, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a list of view names from the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="viewNameFilter">The view name filter.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of view names.</returns>
    public static async Task<List<string>> GetViewNamesAsync(
        this IDbConnection db,
        string? schemaName,
        string? viewNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .GetViewNamesAsync(db, schemaName, viewNameFilter, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Drops a view if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="viewName">The view name.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the view was dropped, otherwise false.</returns>
    public static async Task<bool> DropViewIfExistsAsync(
        this IDbConnection db,
        string? schemaName,
        string viewName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .DropViewIfExistsAsync(db, schemaName, viewName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Renames a view if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="viewName">The current view name.</param>
    /// <param name="newViewName">The new view name.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the view was renamed, otherwise false.</returns>
    public static async Task<bool> RenameViewIfExistsAsync(
        this IDbConnection db,
        string? schemaName,
        string viewName,
        string newViewName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .RenameViewIfExistsAsync(db, schemaName, viewName, newViewName, tx, cancellationToken)
            .ConfigureAwait(false);
    }
    #endregion // IDatabaseViewMethods
}
