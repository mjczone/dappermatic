// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Extensions;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Validation;

namespace MJCZone.DapperMatic.AspNetCore.Services;

/// <summary>
/// Partial class containing view-related methods for DapperMaticService.
/// </summary>
public partial class DapperMaticService
{
    /// <summary>
    /// Gets all views from the specified datasource.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="schemaName">Optional schema name filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of views.</returns>
    public async Task<IEnumerable<ViewDto>> GetViewsAsync(
        IOperationContext context,
        string datasourceId,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    )
    {
        await AssertPermissionsAsync(context).ConfigureAwait(false);

        schemaName = NormalizeSchemaName(schemaName);

        ValidationFactory
            .Arguments()
            .NotNull(context, nameof(context))
            .NotNullOrWhiteSpace(datasourceId, nameof(datasourceId))
            .Assert();

        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);

        using (connection)
        {
            // Check schema exists if specified
            await AssertSchemaExistsIfSpecifiedAsync(datasourceId, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            // Get views using extension method
            var views = await connection
                .GetViewsAsync(schemaName, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            await LogAuditEventAsync(context, true, $"Retrieved views for datasource '{datasourceId}'")
                .ConfigureAwait(false);
            return views.ToViewDtos();
        }
    }

    /// <summary>
    /// Gets a specific view from the datasource.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="viewName">The view name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The view.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the schema or view is not found.</exception>
    public async Task<ViewDto> GetViewAsync(
        IOperationContext context,
        string datasourceId,
        string viewName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    )
    {
        await AssertPermissionsAsync(context).ConfigureAwait(false);

        schemaName = NormalizeSchemaName(schemaName);

        // Validate inputs
        ValidationFactory
            .Arguments()
            .NotNull(context, nameof(context))
            .NotNullOrWhiteSpace(datasourceId, nameof(datasourceId))
            .NotNullOrWhiteSpace(viewName, nameof(viewName))
            .Assert();

        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            // Check schema exists if specified
            await AssertSchemaExistsIfSpecifiedAsync(datasourceId, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            // Get view using extension method
            var view = await connection
                .GetViewAsync(schemaName, viewName, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (view == null)
            {
                throw new KeyNotFoundException(
                    !string.IsNullOrWhiteSpace(schemaName)
                        ? $"View '{viewName}' not found in schema '{schemaName}' of datasource '{datasourceId}'"
                        : $"View '{viewName}' not found in datasource '{datasourceId}'"
                );
            }

            await LogAuditEventAsync(context, true, $"Retrieved view '{viewName}' for datasource '{datasourceId}'")
                .ConfigureAwait(false);
            return view.ToViewDto();
        }
    }

    /// <summary>
    /// Creates a new view in the datasource.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="view">The view data transfer object containing the view information.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created view.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the schema is not found.</exception>
    /// <exception cref="DuplicateKeyException">Thrown when the view already exists.</exception>
    public async Task<ViewDto> CreateViewAsync(
        IOperationContext context,
        string datasourceId,
        ViewDto view,
        CancellationToken cancellationToken = default
    )
    {
        await AssertPermissionsAsync(context).ConfigureAwait(false);

        // Convert DTO to domain model for validation
        var dmView = view.ToDmView();
        var schemaName = NormalizeSchemaName(dmView.SchemaName);

        ValidationFactory
            .Arguments()
            .NotNull(context, nameof(context))
            .NotNullOrWhiteSpace(datasourceId, nameof(datasourceId))
            .NotNull(view, nameof(view))
            .Assert();

        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            // Check schema exists if specified
            await AssertSchemaExistsIfSpecifiedAsync(datasourceId, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            // Check view does not already exist
            await AssertViewDoesNotExistAsync(datasourceId, dmView.ViewName, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            // Create view using extension method
            var created = await connection
                .CreateViewIfNotExistsAsync(
                    schemaName,
                    dmView.ViewName,
                    dmView.Definition,
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);

            if (!created)
            {
                throw new InvalidOperationException(
                    $"Failed to create view '{dmView.ViewName}' for an unknown reason."
                );
            }

            await LogAuditEventAsync(context, true, $"View '{dmView.ViewName}' created successfully.")
                .ConfigureAwait(false);

            var createdView = await connection
                .GetViewAsync(schemaName, dmView.ViewName, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return (
                createdView
                ?? throw new InvalidOperationException(
                    $"View '{dmView.ViewName}' was created but could not be retrieved."
                )
            ).ToViewDto();
        }
    }

    /// <summary>
    /// Updates an existing view's properties in the datasource.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="viewName">The name of the view to update.</param>
    /// <param name="updates">The view updates (only non-null properties will be updated).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated view.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the schema or view is not found.</exception>
    public async Task<ViewDto> UpdateViewAsync(
        IOperationContext context,
        string datasourceId,
        string viewName,
        ViewDto updates,
        CancellationToken cancellationToken = default
    )
    {
        await AssertPermissionsAsync(context).ConfigureAwait(false);

        var schemaName = NormalizeSchemaName(updates.SchemaName);

        ValidationFactory
            .Arguments()
            .NotNull(context, nameof(context))
            .NotNullOrWhiteSpace(datasourceId, nameof(datasourceId))
            .NotNullOrWhiteSpace(viewName, nameof(viewName))
            .NotNull(updates, nameof(updates))
            .Assert();

        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            // Check schema exists if specified
            await AssertSchemaExistsIfSpecifiedAsync(datasourceId, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            // Check view exists
            await AssertViewExistsAsync(datasourceId, viewName, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            var changesMade = false;

            // Update definition if provided
            if (!string.IsNullOrWhiteSpace(updates.Definition))
            {
                var updated = await connection
                    .UpdateViewIfExistsAsync(
                        schemaName,
                        viewName,
                        updates.Definition,
                        cancellationToken: cancellationToken
                    )
                    .ConfigureAwait(false);

                if (updated)
                {
                    changesMade = true;
                    await LogAuditEventAsync(context, true, $"View '{viewName}' definition updated successfully.")
                        .ConfigureAwait(false);
                }
                else
                {
                    throw new InvalidOperationException($"Failed to update definition for view '{viewName}'.");
                }
            }

            if (!changesMade)
            {
                await LogAuditEventAsync(context, false, "No changes made - no valid definition provided")
                    .ConfigureAwait(false);
                throw new InvalidOperationException("No changes made - no valid definition provided");
            }

            // Get the updated view
            var updatedView = await connection
                .GetViewAsync(schemaName, viewName, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return (
                updatedView
                ?? throw new InvalidOperationException($"View '{viewName}' was updated but could not be retrieved.")
            ).ToViewDto();
        }
    }

    /// <summary>
    /// Renames an existing view in the datasource.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="currentViewName">The current name of the view.</param>
    /// <param name="newViewName">The new name for the view.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The renamed view.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the schema or view is not found.</exception>
    public async Task<ViewDto> RenameViewAsync(
        IOperationContext context,
        string datasourceId,
        string currentViewName,
        string newViewName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    )
    {
        await AssertPermissionsAsync(context).ConfigureAwait(false);

        schemaName = NormalizeSchemaName(schemaName);

        ValidationFactory
            .Arguments()
            .NotNull(context, nameof(context))
            .NotNullOrWhiteSpace(datasourceId, nameof(datasourceId))
            .NotNullOrWhiteSpace(currentViewName, nameof(currentViewName))
            .NotNullOrWhiteSpace(newViewName, nameof(newViewName))
            .Assert();

        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            // Check schema exists if specified
            await AssertSchemaExistsIfSpecifiedAsync(datasourceId, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            // Check view exists
            await AssertViewExistsAsync(datasourceId, currentViewName, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            // Check view does not already exist
            await AssertViewDoesNotExistAsync(datasourceId, newViewName, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            // Rename the view
            var updated = await connection
                .RenameViewIfExistsAsync(schemaName, currentViewName, newViewName, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (!updated)
            {
                throw new InvalidOperationException($"Failed to rename view '{currentViewName}' to '{newViewName}'.");
            }

            await LogAuditEventAsync(
                    context,
                    true,
                    $"View '{currentViewName}' renamed to '{newViewName}' successfully."
                )
                .ConfigureAwait(false);

            // Get the renamed view
            var renamedView = await connection
                .GetViewAsync(schemaName, newViewName, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return (
                renamedView
                ?? throw new InvalidOperationException($"View '{newViewName}' was renamed but could not be retrieved.")
            ).ToViewDto();
        }
    }

    /// <summary>
    /// Drops a view from the datasource.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="viewName">The name of the view to drop.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the view does not exist.</exception>
    public async Task DropViewAsync(
        IOperationContext context,
        string datasourceId,
        string viewName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    )
    {
        await AssertPermissionsAsync(context).ConfigureAwait(false);

        schemaName = NormalizeSchemaName(schemaName);

        ValidationFactory
            .Arguments()
            .NotNull(context, nameof(context))
            .NotNullOrWhiteSpace(datasourceId, nameof(datasourceId))
            .NotNullOrWhiteSpace(viewName, nameof(viewName))
            .Assert();

        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            // Check schema exists if specified
            await AssertSchemaExistsIfSpecifiedAsync(datasourceId, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            // Check view exists
            await AssertViewExistsAsync(datasourceId, viewName, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            // Drop view using extension method
            var dropped = await connection
                .DropViewIfExistsAsync(schemaName, viewName, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (!dropped)
            {
                throw new InvalidOperationException($"Failed to drop view '{viewName}' for an unknown reason.");
            }

            await LogAuditEventAsync(context, dropped, $"View '{viewName}' dropped successfully.")
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Checks if a view exists in the datasource.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="viewName">The view name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the view exists, otherwise false.</returns>
    public async Task<bool> ViewExistsAsync(
        IOperationContext context,
        string datasourceId,
        string viewName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    )
    {
        await AssertPermissionsAsync(context).ConfigureAwait(false);

        schemaName = NormalizeSchemaName(schemaName);

        ValidationFactory
            .Arguments()
            .NotNull(context, nameof(context))
            .NotNullOrWhiteSpace(datasourceId, nameof(datasourceId))
            .NotNullOrWhiteSpace(viewName, nameof(viewName))
            .Assert();

        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            // Check schema exists if specified
            await AssertSchemaExistsIfSpecifiedAsync(datasourceId, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            // Check if view exists using extension method
            var exists = await connection
                .DoesViewExistAsync(schemaName, viewName, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            await LogAuditEventAsync(
                    context,
                    true,
                    exists == true ? $"View '{viewName}' exists." : $"View '{viewName}' does not exist."
                )
                .ConfigureAwait(false);
            return exists;
        }
    }

    /// <summary>
    /// Queries a view with filtering, sorting, and pagination.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="viewName">The view name to query.</param>
    /// <param name="request">The query parameters.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The query results with pagination information.</returns>
    public async Task<QueryResultDto> QueryViewAsync(
        IOperationContext context,
        string datasourceId,
        string viewName,
        QueryDto request,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    )
    {
        await AssertPermissionsAsync(context).ConfigureAwait(false);

        schemaName = NormalizeSchemaName(schemaName);

        ValidationFactory
            .Arguments()
            .NotNull(context, nameof(context))
            .NotNullOrWhiteSpace(datasourceId, nameof(datasourceId))
            .NotNullOrWhiteSpace(viewName, nameof(viewName))
            .NotNull(request, nameof(request))
            .Object(
                request,
                nameof(request),
                builder =>
                    builder
                        .Custom(
                            r => r.Take > 0 && r.Take <= 1000,
                            nameof(request.Take),
                            "Take must be greater than 0 and less than or equal to 1000."
                        )
                        .Custom(r => r.Skip >= 0, nameof(request.Skip), "Skip must be greater than or equal to 0.")
            )
            .Assert();

        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            // Check schema exists if specified
            await AssertSchemaExistsIfSpecifiedAsync(datasourceId, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            // Check view exists
            await AssertViewExistsAsync(datasourceId, viewName, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

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

            await LogAuditEventAsync(context, true, $"Queried view '{viewName}' for datasource '{datasourceId}'")
                .ConfigureAwait(false);
            return result;
        }
    }
}
