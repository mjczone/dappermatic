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
/// Partial class containing check constraint-related methods for DapperMaticService.
/// </summary>
public partial class DapperMaticService
{
    /// <summary>
    /// Gets all check constraints from the specified table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of check constraints.</returns>
    public async Task<IEnumerable<CheckConstraintDto>> GetCheckConstraintsAsync(
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

        IEnumerable<DmCheckConstraint> checkConstraints;

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

            checkConstraints = await connection
                .GetCheckConstraintsAsync(schemaName, tableName, null, null, cancellationToken)
                .ConfigureAwait(false);
        }

        await LogAuditEventAsync(
                context,
                true,
                $"Retrieved check constraints from table '{tableName}'"
            )
            .ConfigureAwait(false);
        return checkConstraints.ToCheckConstraintDtos();
    }

    /// <summary>
    /// Gets a specific check constraint from the table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="constraintName">The name of the constraint.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The check constraint or null if not found.</returns>
    public async Task<CheckConstraintDto> GetCheckConstraintAsync(
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

            var checkConstraint = await connection
                .GetCheckConstraintAsync(
                    schemaName,
                    tableName,
                    constraintName,
                    null,
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (checkConstraint == null)
            {
                throw new KeyNotFoundException(
                    $"Check constraint '{constraintName}' not found on table '{tableName}'"
                );
            }

            await LogAuditEventAsync(
                    context,
                    true,
                    $"Retrieved check constraint '{constraintName}' from table '{tableName}'"
                )
                .ConfigureAwait(false);
            return checkConstraint.ToCheckConstraintDto();
        }
    }

    /// <summary>
    /// Creates a new check constraint on a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="checkConstraint">The check constraint details.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created check constraint.</returns>
    public async Task<CheckConstraintDto> CreateCheckConstraintAsync(
        IOperationContext context,
        string datasourceId,
        string tableName,
        CheckConstraintDto checkConstraint,
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
            .NotNull(checkConstraint, nameof(checkConstraint))
            .Object(
                checkConstraint,
                nameof(checkConstraint),
                builder =>
                    builder.NotNullOrWhiteSpace(
                        r => r.CheckExpression,
                        nameof(checkConstraint.CheckExpression)
                    )
            )
            .Assert();

        // Create connection to create check constraint
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

            var dmCheckConstraint = checkConstraint.ToDmCheckConstraint(schemaName, tableName);
            var dmColumnName = dmCheckConstraint.ColumnName;

            var created = await connection
                .CreateCheckConstraintIfNotExistsAsync(dmCheckConstraint, null, cancellationToken)
                .ConfigureAwait(false);

            if (!created)
            {
                throw new InvalidOperationException(
                    !string.IsNullOrWhiteSpace(dmCheckConstraint.ConstraintName)
                        ? $"Failed to create check constraint '{dmCheckConstraint.ConstraintName}' for an unknown reason."
                        : $"Failed to create check constraint for an unknown reason."
                );
            }

            var createdCheckConstraint = !string.IsNullOrWhiteSpace(checkConstraint.ConstraintName)
                ? await connection
                    .GetCheckConstraintAsync(
                        schemaName,
                        tableName,
                        checkConstraint.ConstraintName,
                        null,
                        cancellationToken
                    )
                    .ConfigureAwait(false)
                : null;

            if (createdCheckConstraint == null)
            {
                if (!string.IsNullOrWhiteSpace(dmColumnName))
                {
                    // try to match up the check constraint based on column if no name was provided
                    createdCheckConstraint = await connection
                        .GetCheckConstraintOnColumnAsync(
                            schemaName,
                            tableName,
                            dmColumnName,
                            null,
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                }
                else
                {
                    // if there's only one check constraint on the table, return that
                    var allConstraints = await connection
                        .GetCheckConstraintsAsync(
                            schemaName,
                            tableName,
                            null,
                            null,
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                    if (allConstraints.Count == 1)
                    {
                        createdCheckConstraint = allConstraints.First();
                    }
                }

                if (createdCheckConstraint == null)
                {
                    throw new InvalidOperationException(
                        $"Failed to retrieve the created check constraint."
                    );
                }
            }

            await LogAuditEventAsync(
                    context,
                    true,
                    $"Check constraint '{createdCheckConstraint.ConstraintName}' created successfully."
                )
                .ConfigureAwait(false);

            return createdCheckConstraint.ToCheckConstraintDto();
        }
    }

    /// <summary>
    /// Drops a check constraint from a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="constraintName">The name of the constraint.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DropCheckConstraintAsync(
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

            // Check check constraint exists
            var existingConstraint = await connection
                .DoesCheckConstraintExistAsync(
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
                    $"Check constraint '{constraintName}' not found on table '{tableName}'"
                );
            }

            var dropped = await connection
                .DropCheckConstraintIfExistsAsync(
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
                    $"Failed to drop check constraint '{constraintName}' on table '{tableName}' for an unknown reason."
                );
            }

            await LogAuditEventAsync(
                    context,
                    dropped,
                    $"Check constraint '{constraintName}' dropped successfully."
                )
                .ConfigureAwait(false);
        }
    }
}
