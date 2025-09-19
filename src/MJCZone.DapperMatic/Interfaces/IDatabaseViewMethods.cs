// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Interfaces;

/// <summary>
/// Provides database view methods for database operations.
/// </summary>
public interface IDatabaseViewMethods
{
    /// <summary>
    /// Checks if a view exists in the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="viewName">The view name.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the view exists, otherwise false.</returns>
    Task<bool> DoesViewExistAsync(
        IDbConnection db,
        string? schemaName,
        string viewName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Creates a view if it does not exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="view">The view definition.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the view was created, otherwise false.</returns>
    Task<bool> CreateViewIfNotExistsAsync(
        IDbConnection db,
        DmView view,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Creates a view if it does not exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="viewName">The view name.</param>
    /// <param name="definition">The view definition.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the view was created, otherwise false.</returns>
    Task<bool> CreateViewIfNotExistsAsync(
        IDbConnection db,
        string? schemaName,
        string viewName,
        string definition,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets a view from the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="viewName">The view name.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The view if found, otherwise null.</returns>
    Task<DmView?> GetViewAsync(
        IDbConnection db,
        string? schemaName,
        string viewName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets a list of views from the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="viewNameFilter">The view name filter.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of views.</returns>
    Task<List<DmView>> GetViewsAsync(
        IDbConnection db,
        string? schemaName,
        string? viewNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets a list of view names from the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="viewNameFilter">The view name filter.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of view names.</returns>
    Task<List<string>> GetViewNamesAsync(
        IDbConnection db,
        string? schemaName,
        string? viewNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Drops a view if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="viewName">The view name.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the view was dropped, otherwise false.</returns>
    Task<bool> DropViewIfExistsAsync(
        IDbConnection db,
        string? schemaName,
        string viewName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Renames a view if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="viewName">The view name.</param>
    /// <param name="newViewName">The new view name.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the view was renamed, otherwise false.</returns>
    Task<bool> RenameViewIfExistsAsync(
        IDbConnection db,
        string? schemaName,
        string viewName,
        string newViewName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    );
}
