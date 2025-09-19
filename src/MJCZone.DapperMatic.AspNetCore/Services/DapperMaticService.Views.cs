// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Security.Claims;

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Models.Requests;
using MJCZone.DapperMatic.AspNetCore.Models.Responses;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.AspNetCore.Services;

/// <summary>
/// Partial class containing view-related methods for DapperMaticService.
/// </summary>
public sealed partial class DapperMaticService
{
    /// <summary>
    /// Gets all views from the specified datasource.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="schemaName">Optional schema name filter.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of views.</returns>
    public async Task<IEnumerable<DmView>> GetViewsAsync(
        string datasourceId,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        if (schemaName == "_")
        {
            schemaName = null; // Normalize "_" to null for non-schema providers
        }

        // Validate inputs
        if (string.IsNullOrWhiteSpace(datasourceId))
        {
            throw new ArgumentException("Datasource ID is required.", nameof(datasourceId));
        }

        // Create operation context
        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.ListViews,
            DatasourceId = datasourceId,
            SchemaName = schemaName,
        };

        // Check authorization
        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to list views for datasource '{datasourceId}'."
            );
        }

        try
        {
            var connection = await CreateConnectionForDatasource(datasourceId)
                .ConfigureAwait(false);

            using (connection)
            {
                // Get views using extension method
                var views = await connection
                    .GetViewsAsync(schemaName, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                await LogAuditEventAsync(context, true).ConfigureAwait(false);
                return views;
            }
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Gets a specific view from the datasource.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="viewName">The view name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The view if found, otherwise null.</returns>
    public async Task<DmView?> GetViewAsync(
        string datasourceId,
        string viewName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        if (schemaName == "_")
        {
            schemaName = null; // Normalize "_" to null for non-schema providers
        }

        // Validate inputs
        if (string.IsNullOrWhiteSpace(datasourceId))
        {
            throw new ArgumentException("Datasource ID is required.", nameof(datasourceId));
        }
        if (string.IsNullOrWhiteSpace(viewName))
        {
            throw new ArgumentException("View name is required.", nameof(viewName));
        }

        // Create operation context
        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.GetView,
            DatasourceId = datasourceId,
            SchemaName = schemaName,
            ViewName = viewName,
        };

        // Check authorization
        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to get view '{viewName}' from datasource '{datasourceId}'."
            );
        }

        try
        {
            var connection = await CreateConnectionForDatasource(datasourceId)
                .ConfigureAwait(false);

            using (connection)
            {
                // Get view using extension method
                var view = await connection
                    .GetViewAsync(schemaName, viewName, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                await LogAuditEventAsync(context, true).ConfigureAwait(false);
                return view;
            }
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Creates a new view in the datasource.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="view">The view to create.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created view if successful, otherwise null.</returns>
    public async Task<DmView?> CreateViewAsync(
        string datasourceId,
        DmView view,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        if (view.SchemaName == "_")
        {
            view.SchemaName = null; // Normalize "_" to null for non-schema providers
        }

        // Validate inputs
        if (string.IsNullOrWhiteSpace(datasourceId))
        {
            throw new ArgumentException("Datasource ID is required.", nameof(datasourceId));
        }
        ArgumentNullException.ThrowIfNull(view);
        if (string.IsNullOrWhiteSpace(view.ViewName))
        {
            throw new ArgumentException("View name is required.", nameof(view));
        }
        if (string.IsNullOrWhiteSpace(view.Definition))
        {
            throw new ArgumentException("View definition is required.", nameof(view));
        }

        // Create operation context
        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.CreateView,
            DatasourceId = datasourceId,
            SchemaName = view.SchemaName,
            ViewName = view.ViewName,
            RequestBody = view,
        };

        // Check authorization
        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to create view '{view.ViewName}' in datasource '{datasourceId}'."
            );
        }

        try
        {
            var connection = await CreateConnectionForDatasource(datasourceId)
                .ConfigureAwait(false);

            using (connection)
            {
                // Create view using extension method
                var success = await connection
                    .CreateViewIfNotExistsAsync(view, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                if (success)
                {
                    await LogAuditEventAsync(context, true).ConfigureAwait(false);
                    return view; // Return original view object
                }

                await LogAuditEventAsync(context, false, "View already exists")
                    .ConfigureAwait(false);
                return null;
            }
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing view in the datasource.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="viewName">The name of the view to update.</param>
    /// <param name="newViewName">The new name for the view (optional).</param>
    /// <param name="newViewDefinition">The new definition for the view (optional).</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated view if successful, otherwise null.</returns>
    public async Task<DmView?> UpdateViewAsync(
        string datasourceId,
        string viewName,
        string? newViewName,
        string? newViewDefinition,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        if (schemaName == "_")
        {
            schemaName = null; // Normalize "_" to null for non-schema providers
        }

        // Validate inputs
        if (string.IsNullOrWhiteSpace(datasourceId))
        {
            throw new ArgumentException("Datasource ID is required.", nameof(datasourceId));
        }
        if (string.IsNullOrWhiteSpace(viewName))
        {
            throw new ArgumentException("View name is required.", nameof(viewName));
        }

        // Create operation context
        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.UpdateView,
            DatasourceId = datasourceId,
            SchemaName = schemaName,
            ViewName = viewName,
        };

        // Check authorization
        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to update view '{viewName}' in datasource '{datasourceId}'."
            );
        }

        try
        {
            var connection = await CreateConnectionForDatasource(datasourceId)
                .ConfigureAwait(false);

            DmView? updatedView = null;
            using (connection)
            {
                if (!string.IsNullOrWhiteSpace(newViewDefinition))
                {
                    // Update the view
                    var updated = await connection
                        .UpdateViewIfExistsAsync(
                            schemaName,
                            viewName,
                            newViewDefinition,
                            cancellationToken: cancellationToken
                        )
                        .ConfigureAwait(false);

                    if (updated)
                    {
                        updatedView = await connection
                            .GetViewAsync(
                                schemaName,
                                viewName,
                                cancellationToken: cancellationToken
                            )
                            .ConfigureAwait(false);
                        await LogAuditEventAsync(context, true).ConfigureAwait(false);
                    }
                }

                if (!string.IsNullOrWhiteSpace(newViewName))
                {
                    // Rename the view if a new name is provided
                    var updated = await connection
                        .RenameViewIfExistsAsync(
                            schemaName,
                            viewName,
                            newViewName!,
                            cancellationToken: cancellationToken
                        )
                        .ConfigureAwait(false);

                    if (updated)
                    {
                        updatedView = await connection
                            .GetViewAsync(
                                schemaName,
                                viewName,
                                cancellationToken: cancellationToken
                            )
                            .ConfigureAwait(false);
                        await LogAuditEventAsync(context, true).ConfigureAwait(false);
                    }
                }
            }

            if (updatedView != null)
            {
                return updatedView;
            }
            else
            {
                await LogAuditEventAsync(context, false, "View not found or no changes made")
                    .ConfigureAwait(false);
                return null;
            }
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Drops a view from the datasource.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="viewName">The name of the view to drop.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the view was dropped, false if it didn't exist.</returns>
    public async Task<bool> DropViewAsync(
        string datasourceId,
        string viewName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        if (schemaName == "_")
        {
            schemaName = null; // Normalize "_" to null for non-schema providers
        }

        // Validate inputs
        if (string.IsNullOrWhiteSpace(datasourceId))
        {
            throw new ArgumentException("Datasource ID is required.", nameof(datasourceId));
        }
        if (string.IsNullOrWhiteSpace(viewName))
        {
            throw new ArgumentException("View name is required.", nameof(viewName));
        }

        // Create operation context
        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.DropView,
            DatasourceId = datasourceId,
            SchemaName = schemaName,
            ViewName = viewName,
        };

        // Check authorization
        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to drop view '{viewName}' from datasource '{datasourceId}'."
            );
        }

        try
        {
            var connection = await CreateConnectionForDatasource(datasourceId)
                .ConfigureAwait(false);

            using (connection)
            {
                // Drop view using extension method
                var success = await connection
                    .DropViewIfExistsAsync(
                        schemaName,
                        viewName,
                        cancellationToken: cancellationToken
                    )
                    .ConfigureAwait(false);

                await LogAuditEventAsync(context, success).ConfigureAwait(false);
                return success;
            }
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Checks if a view exists in the datasource.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="viewName">The view name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the view exists, otherwise false.</returns>
    public async Task<bool> ViewExistsAsync(
        string datasourceId,
        string viewName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        if (schemaName == "_")
        {
            schemaName = null; // Normalize "_" to null for non-schema providers
        }

        // Validate inputs
        if (string.IsNullOrWhiteSpace(datasourceId))
        {
            throw new ArgumentException("Datasource ID is required.", nameof(datasourceId));
        }
        if (string.IsNullOrWhiteSpace(viewName))
        {
            throw new ArgumentException("View name is required.", nameof(viewName));
        }

        // Create operation context
        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.ViewExists,
            DatasourceId = datasourceId,
            SchemaName = schemaName,
            ViewName = viewName,
        };

        // Check authorization
        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to check if view '{viewName}' exists in datasource '{datasourceId}'."
            );
        }

        try
        {
            var connection = await CreateConnectionForDatasource(datasourceId)
                .ConfigureAwait(false);

            using (connection)
            {
                // Check if view exists using extension method
                var exists = await connection
                    .DoesViewExistAsync(schemaName, viewName, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                await LogAuditEventAsync(context, true).ConfigureAwait(false);
                return exists;
            }
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Queries a view with filtering, sorting, and pagination.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="viewName">The view name to query.</param>
    /// <param name="request">The query parameters.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The query results with pagination information.</returns>
    public async Task<QueryResultDto> QueryViewAsync(
        string datasourceId,
        string viewName,
        QueryRequest request,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        if (schemaName == "_")
        {
            schemaName = null; // Normalize "_" to null for non-schema providers
        }

        // Validate inputs
        if (string.IsNullOrWhiteSpace(datasourceId))
        {
            throw new ArgumentException("Datasource ID is required.", nameof(datasourceId));
        }
        if (string.IsNullOrWhiteSpace(viewName))
        {
            throw new ArgumentException("View name is required.", nameof(viewName));
        }
        ArgumentNullException.ThrowIfNull(request);

        // Create operation context
        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.QueryView,
            DatasourceId = datasourceId,
            SchemaName = schemaName,
            ViewName = viewName,
            RequestBody = request,
        };

        // Check authorization
        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to query view '{viewName}' in datasource '{datasourceId}'."
            );
        }

        try
        {
            var connection = await CreateConnectionForDatasource(datasourceId)
                .ConfigureAwait(false);

            using (connection)
            {
                // First verify the view exists
                var viewExists = await connection
                    .DoesViewExistAsync(schemaName, viewName, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                if (!viewExists)
                {
                    throw new ArgumentException($"View '{viewName}' does not exist.");
                }

                // Build the query using provider-specific identifier naming
                var qualifiedFromName = connection.GetSchemaQualifiedTableName(schemaName, viewName);

                var result = await ExecuteDataQueryAsync(
                        connection,
                        qualifiedFromName,
                        request,
                        schemaName,
                        null // tableName = null for views to use fallback behavior
                    )
                    .ConfigureAwait(false);

                await LogAuditEventAsync(context, true).ConfigureAwait(false);
                return result;
            }
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
            throw;
        }
    }
}
