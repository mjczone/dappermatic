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
/// Partial class containing primary key constraint-related methods for DapperMaticService.
/// </summary>
public partial class DapperMaticService
{
    /// <summary>
    /// Gets the primary key constraint from the specified table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The primary key constraint or null if not found.</returns>
    public async Task<PrimaryKeyConstraintDto> GetPrimaryKeyAsync(
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

            var primaryKey = await connection
                .GetPrimaryKeyConstraintAsync(schemaName, tableName, null, cancellationToken)
                .ConfigureAwait(false);

            if (primaryKey == null)
            {
                throw new KeyNotFoundException(
                    $"Primary key constraint not found on table '{tableName}'"
                );
            }

            await LogAuditEventAsync(
                    context,
                    true,
                    $"Retrieved primary key constraint for table '{tableName}'"
                )
                .ConfigureAwait(false);
            return primaryKey.ToPrimaryKeyConstraintDto();
        }
    }

    /// <summary>
    /// Creates a new primary key constraint on a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="primaryKeyConstraint">The primary key constraint details.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created primary key constraint.</returns>
    public async Task<PrimaryKeyConstraintDto> CreatePrimaryKeyAsync(
        IOperationContext context,
        string datasourceId,
        string tableName,
        PrimaryKeyConstraintDto primaryKeyConstraint,
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
            .NotNull(primaryKeyConstraint, nameof(primaryKeyConstraint))
            .Object(
                primaryKeyConstraint,
                nameof(primaryKeyConstraint),
                builder =>
                    builder.Custom(
                        r => r.ColumnNames != null && r.ColumnNames.Count > 0,
                        nameof(primaryKeyConstraint.ColumnNames),
                        "At least one column is required."
                    )
            )
            .Assert();

        // Create connection to create primary key
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

            var dmPrimaryKey = primaryKeyConstraint.ToDmPrimaryKeyConstraint(schemaName, tableName);
            var dmColumnNames = dmPrimaryKey.Columns.Select(c => c.ColumnName).ToList();

            var created = await connection
                .CreatePrimaryKeyConstraintIfNotExistsAsync(dmPrimaryKey, null, cancellationToken)
                .ConfigureAwait(false);

            if (!created)
            {
                throw new InvalidOperationException(
                    !string.IsNullOrWhiteSpace(dmPrimaryKey.ConstraintName)
                        ? $"Failed to create primary key constraint '{dmPrimaryKey.ConstraintName}' for an unknown reason."
                        : $"Failed to create primary key constraint on columns ({string.Join(", ", dmColumnNames)}) for an unknown reason."
                );
            }

            // Primary key constraints are unique per table, so we can just retrieve it directly
            var createdPrimaryKey = await connection
                .GetPrimaryKeyConstraintAsync(schemaName, tableName, null, cancellationToken)
                .ConfigureAwait(false);

            if (createdPrimaryKey == null)
            {
                throw new InvalidOperationException(
                    $"Failed to retrieve the created primary key constraint."
                );
            }

            await LogAuditEventAsync(
                    context,
                    true,
                    $"Primary key constraint '{createdPrimaryKey.ConstraintName}' created successfully."
                )
                .ConfigureAwait(false);

            return createdPrimaryKey.ToPrimaryKeyConstraintDto();
        }
    }

    /// <summary>
    /// Drops the primary key constraint from a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DropPrimaryKeyAsync(
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

            // Check if primary key exists
            var existingPrimaryKey = await connection
                .GetPrimaryKeyConstraintAsync(schemaName, tableName, null, cancellationToken)
                .ConfigureAwait(false);

            if (existingPrimaryKey == null)
            {
                throw new KeyNotFoundException(
                    $"Primary key constraint not found on table '{tableName}'"
                );
            }

            var dropped = await connection
                .DropPrimaryKeyConstraintIfExistsAsync(
                    schemaName,
                    tableName,
                    null,
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (!dropped)
            {
                throw new InvalidOperationException(
                    $"Failed to drop primary key constraint on table '{tableName}' for an unknown reason."
                );
            }

            await LogAuditEventAsync(
                    context,
                    dropped,
                    $"Primary key constraint dropped successfully."
                )
                .ConfigureAwait(false);
        }
    }
}
