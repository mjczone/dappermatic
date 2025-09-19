// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Providers.Base;

public abstract partial class DatabaseMethodsBase
{
    /// <summary>
    /// Checks if a view exists in the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="viewName">The view name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the view exists, otherwise false.</returns>
    public virtual async Task<bool> DoesViewExistAsync(
        IDbConnection db,
        string? schemaName,
        string viewName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return (
                await GetViewNamesAsync(db, schemaName, viewName, tx, cancellationToken)
                    .ConfigureAwait(false)
            ).Count == 1;
    }

    /// <summary>
    /// Creates a view if it does not exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="view">The view details.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the view was created, otherwise false.</returns>
    public virtual async Task<bool> CreateViewIfNotExistsAsync(
        IDbConnection db,
        DmView view,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await CreateViewIfNotExistsAsync(
                db,
                view.SchemaName,
                view.ViewName,
                view.Definition,
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a view if it does not exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="viewName">The view name.</param>
    /// <param name="definition">The view definition.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the view was created, otherwise false.</returns>
    public virtual async Task<bool> CreateViewIfNotExistsAsync(
        IDbConnection db,
        string? schemaName,
        string viewName,
        string definition,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrEmpty(definition))
        {
            throw new ArgumentException("View definition is required.", nameof(definition));
        }

        if (
            await DoesViewExistAsync(db, schemaName, viewName, tx, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            return false;
        }

        var sql = SqlCreateView(schemaName, viewName, definition);

        await ExecuteAsync(db, sql, tx: tx, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Gets a view from the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="viewName">The view name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The view details, or null if the view does not exist.</returns>
    public virtual async Task<DmView?> GetViewAsync(
        IDbConnection db,
        string? schemaName,
        string viewName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrEmpty(viewName))
        {
            throw new ArgumentException("View name is required.", nameof(viewName));
        }

        return (
            await GetViewsAsync(db, schemaName, viewName, tx, cancellationToken)
                .ConfigureAwait(false)
        ).SingleOrDefault();
    }

    /// <summary>
    /// Gets the names of views from the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="viewNameFilter">The view name filter.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of view names.</returns>
    public virtual async Task<List<string>> GetViewNamesAsync(
        IDbConnection db,
        string? schemaName,
        string? viewNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var (sql, parameters) = SqlGetViewNames(schemaName, viewNameFilter);
        return await QueryAsync<string>(
                db,
                sql,
                parameters,
                tx: tx,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets views from the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="viewNameFilter">The view name filter.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of views.</returns>
    public virtual async Task<List<DmView>> GetViewsAsync(
        IDbConnection db,
        string? schemaName,
        string? viewNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var (sql, parameters) = SqlGetViews(schemaName, viewNameFilter);
        var views = await QueryAsync<DmView>(
                db,
                sql,
                parameters,
                tx: tx,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
        foreach (var view in views)
        {
            view.Definition = NormalizeViewDefinition(view.Definition);
        }
        return views;
    }

    /// <summary>
    /// Drops a view if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="viewName">The view name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the view was dropped, otherwise false.</returns>
    public virtual async Task<bool> DropViewIfExistsAsync(
        IDbConnection db,
        string? schemaName,
        string viewName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        if (
            !await DoesViewExistAsync(db, schemaName, viewName, tx, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            return false;
        }

        var sql = SqlDropView(schemaName, viewName);

        await ExecuteAsync(db, sql, tx: tx, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Renames a view if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="viewName">The current view name.</param>
    /// <param name="newViewName">The new view name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the view was renamed, otherwise false.</returns>
    public virtual async Task<bool> RenameViewIfExistsAsync(
        IDbConnection db,
        string? schemaName,
        string viewName,
        string newViewName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var view = await GetViewAsync(db, schemaName, viewName, tx, cancellationToken)
            .ConfigureAwait(false);

        if (view == null || string.IsNullOrWhiteSpace(view.Definition))
        {
            return false;
        }

        await DropViewIfExistsAsync(db, schemaName, viewName, tx, cancellationToken)
            .ConfigureAwait(false);

        await CreateViewIfNotExistsAsync(
                db,
                schemaName,
                newViewName,
                view.Definition,
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);

        return true;
    }
}
