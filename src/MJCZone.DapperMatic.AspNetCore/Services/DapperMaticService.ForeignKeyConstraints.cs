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
/// Partial class containing foreign key constraint-related methods for DapperMaticService.
/// </summary>
public partial class DapperMaticService
{
    /// <summary>
    /// Gets all foreign key constraints from the specified table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of foreign key constraints.</returns>
    public async Task<IEnumerable<ForeignKeyConstraintDto>> GetForeignKeyConstraintsAsync(
        IOperationContext context,
        string datasourceId,
        string tableName,
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
            .NotNullOrWhiteSpace(tableName, nameof(tableName))
            .Assert();

        IEnumerable<DmForeignKeyConstraint> foreignKeys;

        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            // Check schema exists if specified
            await AssertSchemaExistsIfSpecifiedAsync(datasourceId, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            // Check table exists
            await AssertTableExistsAsync(datasourceId, tableName, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            foreignKeys = await connection
                .GetForeignKeyConstraintsAsync(schemaName, tableName, null, null, cancellationToken)
                .ConfigureAwait(false);
        }

        await LogAuditEventAsync(context, true, $"Retrieved foreign keys for table '{tableName}'")
            .ConfigureAwait(false);
        return foreignKeys.ToForeignKeyConstraintDtos();
    }

    /// <summary>
    /// Gets a specific foreign key constraint from the table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="constraintName">The name of the constraint.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The foreign key constraint or null if not found.</returns>
    public async Task<ForeignKeyConstraintDto> GetForeignKeyConstraintAsync(
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

        ValidationFactory
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

            var foreignKey = await connection
                .GetForeignKeyConstraintAsync(schemaName, tableName, constraintName, null, cancellationToken)
                .ConfigureAwait(false);

            if (foreignKey == null)
            {
                throw new KeyNotFoundException(
                    $"Foreign key constraint '{constraintName}' not found on table '{tableName}'"
                );
            }

            await LogAuditEventAsync(
                    context,
                    true,
                    $"Retrieved foreign key constraint '{constraintName}' from table '{tableName}'"
                )
                .ConfigureAwait(false);
            return foreignKey.ToForeignKeyConstraintDto();
        }
    }

    /// <summary>
    /// Creates a new foreign key constraint on a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="foreignKeyConstraint">The foreign key constraint details.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created foreign key constraint.</returns>
    public async Task<ForeignKeyConstraintDto> CreateForeignKeyConstraintAsync(
        IOperationContext context,
        string datasourceId,
        string tableName,
        ForeignKeyConstraintDto foreignKeyConstraint,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    )
    {
        await AssertPermissionsAsync(context).ConfigureAwait(false);

        // Convert DTO to domain model for validation
        schemaName = NormalizeSchemaName(schemaName);

        // auto-generate constraint name if not provided
        if (
            foreignKeyConstraint != null
            && string.IsNullOrWhiteSpace(foreignKeyConstraint.ConstraintName)
            && foreignKeyConstraint.ColumnNames != null
            && foreignKeyConstraint.ColumnNames.Count > 0
        )
        {
            foreignKeyConstraint.ConstraintName = string.IsNullOrWhiteSpace(schemaName)
                ? $"fk_{tableName}_{string.Join('_', foreignKeyConstraint.ColumnNames)}"
                : $"fk_{schemaName}_{tableName}_{string.Join('_', foreignKeyConstraint.ColumnNames)}";
        }

        ValidationFactory
            .Arguments()
            .NotNull(context, nameof(context))
            .NotNullOrWhiteSpace(datasourceId, nameof(datasourceId))
            .NotNullOrWhiteSpace(tableName, nameof(tableName))
            .NotNull(foreignKeyConstraint, nameof(foreignKeyConstraint))
            .Object(
                foreignKeyConstraint,
                nameof(foreignKeyConstraint),
                builder =>
                    builder
                        .NotNullOrWhiteSpace(
                            r => r.ReferencedTableName,
                            nameof(foreignKeyConstraint.ReferencedTableName)
                        )
                        .Custom(
                            r => r.ColumnNames != null && r.ColumnNames.Count > 0,
                            nameof(foreignKeyConstraint.ColumnNames),
                            "At least one column is required."
                        )
                        .Custom(
                            r => r.ReferencedColumnNames != null && r.ReferencedColumnNames.Count > 0,
                            nameof(foreignKeyConstraint.ReferencedColumnNames),
                            "At least one referenced column is required."
                        )
            )
            .Assert();

        // Create connection to create foreign key
        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            // Check schema exists if specified
            await AssertSchemaExistsIfSpecifiedAsync(datasourceId, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            // Check table exists
            await AssertTableExistsAsync(datasourceId, tableName, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            // Check referenced table exists
            await AssertTableExistsAsync(
                    datasourceId,
                    foreignKeyConstraint.ReferencedTableName!,
                    schemaName, // Foreign keys typically reference tables in the same schema
                    connection,
                    cancellationToken
                )
                .ConfigureAwait(false);

            // Check foreign key does not already exist
            if (
                !string.IsNullOrWhiteSpace(foreignKeyConstraint.ConstraintName)
                && await connection
                    .DoesForeignKeyConstraintExistAsync(
                        schemaName,
                        tableName,
                        foreignKeyConstraint.ConstraintName,
                        null,
                        cancellationToken
                    )
                    .ConfigureAwait(false)
            )
            {
                throw new DuplicateKeyException(
                    $"Foreign key constraint '{foreignKeyConstraint.ConstraintName}' already exists on table '{tableName}'"
                );
            }

            var dmForeignKey = foreignKeyConstraint.ToDmForeignKeyConstraint(schemaName, tableName);
            var dmColumnNames = dmForeignKey.SourceColumns.Select(c => c.ColumnName).ToList();

            var created = await connection
                .CreateForeignKeyConstraintIfNotExistsAsync(dmForeignKey, null, cancellationToken)
                .ConfigureAwait(false);

            if (!created)
            {
                throw new InvalidOperationException(
                    !string.IsNullOrWhiteSpace(dmForeignKey.ConstraintName)
                        ? $"Failed to create foreign key constraint '{dmForeignKey.ConstraintName}' for an unknown reason."
                        : $"Failed to create foreign key constraint on columns ({string.Join(", ", dmColumnNames)}) for an unknown reason."
                );
            }

            var createdForeignKey = !string.IsNullOrWhiteSpace(foreignKeyConstraint.ConstraintName)
                ? await connection
                    .GetForeignKeyConstraintAsync(
                        schemaName,
                        tableName,
                        foreignKeyConstraint.ConstraintName,
                        null,
                        cancellationToken
                    )
                    .ConfigureAwait(false)
                : null;

            if (createdForeignKey == null)
            {
                // try to match up the foreign key based on columns if no name was provided
                var foreignKeys = await connection
                    .GetForeignKeyConstraintsAsync(schemaName, tableName, null, null, cancellationToken)
                    .ConfigureAwait(false);

                createdForeignKey = foreignKeys.FirstOrDefault(fk =>
                    fk.SourceColumns.Count == dmForeignKey.SourceColumns.Count
                    && !fk.SourceColumns.All(c =>
                        !dmColumnNames.Contains(c.ColumnName, StringComparer.OrdinalIgnoreCase)
                    )
                );

                if (createdForeignKey == null)
                {
                    throw new InvalidOperationException($"Failed to retrieve the created foreign key constraint.");
                }
            }

            await LogAuditEventAsync(
                    context,
                    true,
                    $"Foreign key constraint '{createdForeignKey.ConstraintName}' created successfully."
                )
                .ConfigureAwait(false);

            return createdForeignKey.ToForeignKeyConstraintDto();
        }
    }

    /// <summary>
    /// Drops a foreign key constraint from a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="constraintName">The name of the constraint.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DropForeignKeyConstraintAsync(
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

        ValidationFactory
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

            // Check foreign key constraint exists
            var existingConstraint = await connection
                .DoesForeignKeyConstraintExistAsync(schemaName, tableName, constraintName, null, cancellationToken)
                .ConfigureAwait(false);

            if (!existingConstraint)
            {
                throw new KeyNotFoundException(
                    $"Foreign key constraint '{constraintName}' not found on table '{tableName}'"
                );
            }

            var dropped = await connection
                .DropForeignKeyConstraintIfExistsAsync(schemaName, tableName, constraintName, null, cancellationToken)
                .ConfigureAwait(false);

            if (!dropped)
            {
                throw new InvalidOperationException(
                    $"Failed to drop foreign key constraint '{constraintName}' on table '{tableName}' for an unknown reason."
                );
            }

            await LogAuditEventAsync(
                    context,
                    dropped,
                    $"Foreign key constraint '{constraintName}' dropped successfully."
                )
                .ConfigureAwait(false);
        }
    }
}
