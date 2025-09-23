// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Extensions;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Validation;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.AspNetCore.Services;

/// <summary>
/// Partial class containing default constraint-related methods for DapperMaticService.
/// </summary>
public partial class DapperMaticService
{
    /// <summary>
    /// Gets all default constraints from the specified table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of default constraints.</returns>
    public async Task<IEnumerable<DefaultConstraintDto>> GetDefaultConstraintsAsync(
        IOperationContext context,
        string datasourceId,
        string tableName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    )
    {
        await AssertPermissionsAsync(context).ConfigureAwait(false);

        schemaName = NormalizeSchemaName(schemaName);

        Validate
            .Arguments()
            .NotNull(context, nameof(context))
            .NotNullOrWhiteSpace(datasourceId, nameof(datasourceId))
            .NotNullOrWhiteSpace(tableName, nameof(tableName))
            .Assert();

        IEnumerable<DmDefaultConstraint> defaultConstraints;

        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            // Check schema exists if specified
            await AssertSchemaExistsIfSpecifiedAsync(
                    datasourceId,
                    schemaName,
                    connection,
                    cancellationToken
                )
                .ConfigureAwait(false);

            // Check table exists
            await AssertTableExistsAsync(
                    datasourceId,
                    tableName,
                    schemaName,
                    connection,
                    cancellationToken
                )
                .ConfigureAwait(false);

            defaultConstraints = await connection
                .GetDefaultConstraintsAsync(schemaName, tableName, null, null, cancellationToken)
                .ConfigureAwait(false);
        }

        await LogAuditEventAsync(context, true, $"Retrieved default constraints for table '{tableName}'").ConfigureAwait(false);
        return defaultConstraints.ToDefaultConstraintDtos();
    }

    /// <summary>
    /// Gets a specific default constraint from the table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="constraintName">The name of the constraint.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The default constraint or null if not found.</returns>
    public async Task<DefaultConstraintDto> GetDefaultConstraintAsync(
        IOperationContext context,
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    )
    {
        await AssertPermissionsAsync(context).ConfigureAwait(false);

        schemaName = NormalizeSchemaName(schemaName);

        Validate
            .Arguments()
            .NotNull(context, nameof(context))
            .NotNullOrWhiteSpace(datasourceId, nameof(datasourceId))
            .NotNullOrWhiteSpace(tableName, nameof(tableName))
            .NotNullOrWhiteSpace(constraintName, nameof(constraintName))
            .Assert();

        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            // Check schema exists if specified
            await AssertSchemaExistsIfSpecifiedAsync(
                    datasourceId,
                    schemaName,
                    connection,
                    cancellationToken
                )
                .ConfigureAwait(false);

            // Check table exists
            await AssertTableExistsAsync(
                    datasourceId,
                    tableName,
                    schemaName,
                    connection,
                    cancellationToken
                )
                .ConfigureAwait(false);

            var defaultConstraint = await connection
                .GetDefaultConstraintAsync(
                    schemaName,
                    tableName,
                    constraintName,
                    null,
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (defaultConstraint == null)
            {
                throw new KeyNotFoundException(
                    $"Default constraint '{constraintName}' not found on table '{tableName}'"
                );
            }

            await LogAuditEventAsync(
                    context,
                    true,
                    $"Retrieved default constraint '{constraintName}' from table '{tableName}'"
                )
                .ConfigureAwait(false);
            return defaultConstraint.ToDefaultConstraintDto();
        }
    }

    /// <summary>
    /// Creates a new default constraint on a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="defaultConstraint">The default constraint details.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created default constraint.</returns>
    public async Task<DefaultConstraintDto> CreateDefaultConstraintAsync(
        IOperationContext context,
        string datasourceId,
        string tableName,
        DefaultConstraintDto defaultConstraint,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    )
    {
        await AssertPermissionsAsync(context).ConfigureAwait(false);

        // Convert DTO to domain model for validation
        schemaName = NormalizeSchemaName(schemaName);

        Validate
            .Arguments()
            .NotNull(context, nameof(context))
            .NotNullOrWhiteSpace(datasourceId, nameof(datasourceId))
            .NotNullOrWhiteSpace(tableName, nameof(tableName))
            .NotNull(defaultConstraint, nameof(defaultConstraint))
            .Object(
                defaultConstraint,
                nameof(defaultConstraint),
                builder =>
                    builder
                        .NotNullOrWhiteSpace(
                            r => r.ColumnName,
                            nameof(defaultConstraint.ColumnName)
                        )
                        .NotNullOrWhiteSpace(
                            r => r.DefaultExpression,
                            nameof(defaultConstraint.DefaultExpression)
                        )
            )
            .Assert();

        // Create connection to create default constraint
        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            // Check schema exists if specified
            await AssertSchemaExistsIfSpecifiedAsync(
                    datasourceId,
                    schemaName,
                    connection,
                    cancellationToken
                )
                .ConfigureAwait(false);

            // Check table exists
            await AssertTableExistsAsync(
                    datasourceId,
                    tableName,
                    schemaName,
                    connection,
                    cancellationToken
                )
                .ConfigureAwait(false);

            var dmDefaultConstraint = defaultConstraint.ToDmDefaultConstraint(
                schemaName,
                tableName
            );

            var created = await connection
                .CreateDefaultConstraintIfNotExistsAsync(
                    dmDefaultConstraint,
                    null,
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (!created)
            {
                throw new InvalidOperationException(
                    !string.IsNullOrWhiteSpace(dmDefaultConstraint.ConstraintName)
                        ? $"Failed to create default constraint '{dmDefaultConstraint.ConstraintName}' for an unknown reason."
                        : $"Failed to create default constraint on column '{dmDefaultConstraint.ColumnName}' for an unknown reason."
                );
            }

            var createdDefaultConstraint = !string.IsNullOrWhiteSpace(
                defaultConstraint.ConstraintName
            )
                ? await connection
                    .GetDefaultConstraintAsync(
                        schemaName,
                        tableName,
                        defaultConstraint.ConstraintName,
                        null,
                        cancellationToken
                    )
                    .ConfigureAwait(false)
                : null;

            if (createdDefaultConstraint == null)
            {
                // try to match up the default constraint based on column if no name was provided
                createdDefaultConstraint = await connection
                    .GetDefaultConstraintOnColumnAsync(
                        schemaName,
                        tableName,
                        defaultConstraint.ColumnName!,
                        null,
                        cancellationToken
                    )
                    .ConfigureAwait(false);

                if (createdDefaultConstraint == null)
                {
                    throw new InvalidOperationException(
                        $"Failed to retrieve the created default constraint."
                    );
                }
            }

            await LogAuditEventAsync(
                    context,
                    true,
                    $"Default constraint '{createdDefaultConstraint.ConstraintName}' created successfully."
                )
                .ConfigureAwait(false);

            return createdDefaultConstraint.ToDefaultConstraintDto();
        }
    }

    /// <summary>
    /// Drops a default constraint from a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="constraintName">The name of the constraint.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DropDefaultConstraintAsync(
        IOperationContext context,
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    )
    {
        await AssertPermissionsAsync(context).ConfigureAwait(false);

        schemaName = NormalizeSchemaName(schemaName);

        Validate
            .Arguments()
            .NotNull(context, nameof(context))
            .NotNullOrWhiteSpace(datasourceId, nameof(datasourceId))
            .NotNullOrWhiteSpace(tableName, nameof(tableName))
            .NotNullOrWhiteSpace(constraintName, nameof(constraintName))
            .Assert();

        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            // Check schema exists if specified
            await AssertSchemaExistsIfSpecifiedAsync(
                    datasourceId,
                    schemaName,
                    connection,
                    cancellationToken
                )
                .ConfigureAwait(false);

            // Check table exists
            await AssertTableExistsAsync(
                    datasourceId,
                    tableName,
                    schemaName,
                    connection,
                    cancellationToken
                )
                .ConfigureAwait(false);

            // Check default constraint exists
            var existingConstraint = await connection
                .DoesDefaultConstraintExistAsync(
                    schemaName,
                    tableName,
                    constraintName,
                    null,
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (!existingConstraint)
            {
                throw new KeyNotFoundException(
                    $"Default constraint '{constraintName}' not found on table '{tableName}'"
                );
            }

            var dropped = await connection
                .DropDefaultConstraintIfExistsAsync(
                    schemaName,
                    tableName,
                    constraintName,
                    null,
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (!dropped)
            {
                throw new InvalidOperationException(
                    $"Failed to drop default constraint '{constraintName}' on table '{tableName}' for an unknown reason."
                );
            }

            await LogAuditEventAsync(
                    context,
                    dropped,
                    $"Default constraint '{constraintName}' dropped successfully."
                )
                .ConfigureAwait(false);
        }
    }
}
