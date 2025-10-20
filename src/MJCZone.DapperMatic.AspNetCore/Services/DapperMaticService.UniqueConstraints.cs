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
/// Partial class containing unique constraint-related methods for DapperMaticService.
/// </summary>
public partial class DapperMaticService
{
    /// <summary>
    /// Gets all unique constraints from the specified table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of unique constraints.</returns>
    public async Task<IEnumerable<UniqueConstraintDto>> GetUniqueConstraintsAsync(
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

        IEnumerable<DmUniqueConstraint> uniqueConstraints;

        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            // Check schema exists if specified
            await AssertSchemaExistsIfSpecifiedAsync(datasourceId, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            // Check table exists
            await AssertTableExistsAsync(datasourceId, tableName, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            uniqueConstraints = await connection
                .GetUniqueConstraintsAsync(schemaName, tableName, null, null, cancellationToken)
                .ConfigureAwait(false);
        }

        await LogAuditEventAsync(
                context,
                true,
                $"Retrieved unique constraints for table '{tableName}' in datasource '{datasourceId}'"
            )
            .ConfigureAwait(false);
        return uniqueConstraints.ToUniqueConstraintDtos();
    }

    /// <summary>
    /// Gets a specific unique constraint from the table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="constraintName">The name of the constraint.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The unique constraint or null if not found.</returns>
    public async Task<UniqueConstraintDto> GetUniqueConstraintAsync(
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
            await AssertSchemaExistsIfSpecifiedAsync(datasourceId, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            // Check table exists
            await AssertTableExistsAsync(datasourceId, tableName, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            var uniqueConstraint = await connection
                .GetUniqueConstraintAsync(schemaName, tableName, constraintName, null, cancellationToken)
                .ConfigureAwait(false);

            if (uniqueConstraint == null)
            {
                throw new KeyNotFoundException(
                    $"Unique constraint '{constraintName}' not found on table '{tableName}'"
                );
            }

            await LogAuditEventAsync(
                    context,
                    true,
                    $"Retrieved unique constraint '{constraintName}' from table '{tableName}'"
                )
                .ConfigureAwait(false);
            return uniqueConstraint.ToUniqueConstraintDto();
        }
    }

    /// <summary>
    /// Creates a new unique constraint on a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="uniqueConstraint">The request containing unique constraint details.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created unique constraint or null if creation failed.</returns>
    public async Task<UniqueConstraintDto> CreateUniqueConstraintAsync(
        IOperationContext context,
        string datasourceId,
        string tableName,
        UniqueConstraintDto uniqueConstraint,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    )
    {
        await AssertPermissionsAsync(context).ConfigureAwait(false);

        // Convert DTO to domain model for validation
        schemaName = NormalizeSchemaName(schemaName);

        // auto-generate constraint name if not provided
        if (
            uniqueConstraint != null
            && string.IsNullOrWhiteSpace(uniqueConstraint.ConstraintName)
            && uniqueConstraint.ColumnNames != null
            && uniqueConstraint.ColumnNames.Count > 0
        )
        {
            uniqueConstraint.ConstraintName = string.IsNullOrWhiteSpace(schemaName)
                ? $"uq_{tableName}_{string.Join('_', uniqueConstraint.ColumnNames)}"
                : $"uq_{schemaName}_{tableName}_{string.Join('_', uniqueConstraint.ColumnNames)}";
        }

        Validate
            .Arguments()
            .NotNull(context, nameof(context))
            .NotNullOrWhiteSpace(datasourceId, nameof(datasourceId))
            .NotNullOrWhiteSpace(tableName, nameof(tableName))
            .NotNull(uniqueConstraint, nameof(uniqueConstraint))
            .Object(
                uniqueConstraint,
                nameof(uniqueConstraint),
                builder =>
                    builder.Custom(
                        r => r.ColumnNames != null && r.ColumnNames.Count > 0,
                        nameof(uniqueConstraint.ColumnNames),
                        "At least one column is required."
                    )
            )
            .Assert();

        // Create connection to create index
        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            // Check schema exists if specified
            await AssertSchemaExistsIfSpecifiedAsync(datasourceId, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            // Check table exists
            await AssertTableExistsAsync(datasourceId, tableName, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            if (
                !string.IsNullOrWhiteSpace(uniqueConstraint.ConstraintName)
                && await connection
                    .DoesUniqueConstraintExistAsync(
                        schemaName,
                        tableName,
                        uniqueConstraint.ConstraintName,
                        null,
                        cancellationToken
                    )
                    .ConfigureAwait(false)
            )
            {
                throw new DuplicateKeyException(
                    $"A unique constraint with the name '{uniqueConstraint.ConstraintName}' already exists on table '{tableName}'."
                );
            }

            var dmUniqueConstraint = uniqueConstraint.ToDmUniqueConstraint(schemaName, tableName);
            var dmColumnNames = dmUniqueConstraint.Columns.Select(c => c.ColumnName).ToList();

            var created = await connection
                .CreateUniqueConstraintIfNotExistsAsync(dmUniqueConstraint, null, cancellationToken)
                .ConfigureAwait(false);

            if (!created)
            {
                throw new InvalidOperationException(
                    !string.IsNullOrWhiteSpace(dmUniqueConstraint.ConstraintName)
                        ? $"Failed to create unique constraint '{dmUniqueConstraint.ConstraintName}' for an unknown reason."
                        : $"Failed to create unique constraint on columns ({string.Join(", ", dmColumnNames)}) for an unknown reason."
                );
            }

            var createdUniqueConstraint = !string.IsNullOrWhiteSpace(uniqueConstraint.ConstraintName)
                ? await connection
                    .GetUniqueConstraintAsync(
                        schemaName,
                        tableName,
                        uniqueConstraint.ConstraintName,
                        null,
                        cancellationToken
                    )
                    .ConfigureAwait(false)
                : null;

            if (createdUniqueConstraint == null)
            {
                // try to match up the unique constraint based on columns if no name was provided
                var uniqueConstraints = await connection
                    .GetUniqueConstraintsAsync(schemaName, tableName, null, null, cancellationToken)
                    .ConfigureAwait(false);

                createdUniqueConstraint = uniqueConstraints.FirstOrDefault(uc =>
                    uc.Columns.Count == dmUniqueConstraint.Columns.Count
                    && !uc.Columns.All(c => !dmColumnNames.Contains(c.ColumnName, StringComparer.OrdinalIgnoreCase))
                );

                if (createdUniqueConstraint == null)
                {
                    throw new InvalidOperationException($"Failed to retrieve the created unique constraint.");
                }
            }

            await LogAuditEventAsync(
                    context,
                    true,
                    $"Unique constraint '{createdUniqueConstraint.ConstraintName}' created successfully."
                )
                .ConfigureAwait(false);

            return createdUniqueConstraint.ToUniqueConstraintDto();
        }
    }

    /// <summary>
    /// Drops a unique constraint from a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="constraintName">The name of the constraint.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the unique constraint was dropped successfully, false otherwise.</returns>
    public async Task DropUniqueConstraintAsync(
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
            await AssertSchemaExistsIfSpecifiedAsync(datasourceId, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            // Check table exists
            await AssertTableExistsAsync(datasourceId, tableName, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            // Check unique constraint exists
            var existingConstraint = await connection
                .DoesUniqueConstraintExistAsync(schemaName, tableName, constraintName, null, cancellationToken)
                .ConfigureAwait(false);

            if (!existingConstraint)
            {
                throw new KeyNotFoundException(
                    $"Unique constraint '{constraintName}' not found on table '{tableName}'"
                );
            }

            var dropped = await connection
                .DropUniqueConstraintIfExistsAsync(schemaName, tableName, constraintName, null, cancellationToken)
                .ConfigureAwait(false);

            if (!dropped)
            {
                throw new InvalidOperationException(
                    $"Failed to drop unique constraint '{constraintName}' on table '{tableName}' for an unknown reason."
                );
            }

            await LogAuditEventAsync(context, dropped, $"Unique constraint '{constraintName}' dropped successfully.")
                .ConfigureAwait(false);
        }
    }
}
