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
/// Partial class containing column-related methods for DapperMaticService.
/// </summary>
public partial class DapperMaticService
{
    /// <summary>
    /// Gets all columns from the specified table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of columns.</returns>
    public async Task<IEnumerable<ColumnDto>> GetColumnsAsync(
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

        IEnumerable<DmColumn> columns;

        // Create connection to get columns
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

            columns = await connection
                .GetColumnsAsync(schemaName, tableName, null, null, cancellationToken)
                .ConfigureAwait(false);
        }

        await LogAuditEventAsync(context, true, $"Retrieved columns from table '{tableName}'")
            .ConfigureAwait(false);
        return columns.ToColumnDtos();
    }

    /// <summary>
    /// Gets a specific column from the table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The column if found, otherwise null.</returns>
    public async Task<ColumnDto> GetColumnAsync(
        IOperationContext context,
        string datasourceId,
        string tableName,
        string columnName,
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
            .NotNullOrWhiteSpace(columnName, nameof(columnName))
            .Assert();

        // Create connection to get column
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

            var column = await connection
                .GetColumnAsync(
                    schemaName,
                    tableName,
                    columnName,
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);

            if (column == null)
            {
                throw new KeyNotFoundException(
                    $"Column '{columnName}' does not exist in table '{tableName}'."
                );
            }

            await LogAuditEventAsync(
                    context,
                    true,
                    $"Retrieved column '{columnName}' from table '{tableName}'"
                )
                .ConfigureAwait(false);
            return column.ToColumnDto();
        }
    }

    /// <summary>
    /// Adds a new column to an existing table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="column">The add column request.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The added column information if successful, otherwise null.</returns>
    public async Task<ColumnDto> AddColumnAsync(
        IOperationContext context,
        string datasourceId,
        string tableName,
        ColumnDto column,
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
            .NotNull(column, nameof(column))
            .Object(
                column,
                nameof(column),
                builder =>
                    builder
                        .NotNullOrWhiteSpace(r => r.ColumnName, nameof(column.ColumnName))
                        .Custom(
                            r => !string.IsNullOrWhiteSpace(r.ProviderDataType),
                            nameof(column.ProviderDataType),
                            $"{nameof(column.ProviderDataType)} is required."
                        )
            )
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

            var dmColumn = new DmColumn(
                schemaName,
                tableName,
                column.ColumnName,
                null,
                providerDataTypes: new Dictionary<DbProviderType, string>
                {
                    { connection.GetDbProviderType(), column.ProviderDataType },
                },
                isPrimaryKey: column.IsPrimaryKey,
                isAutoIncrement: column.IsAutoIncrement,
                isUnicode: column.IsUnicode == true,
                isUnique: column.IsUnique,
                isIndexed: column.IsIndexed,
                isNullable: column.IsNullable,
                defaultExpression: column.DefaultExpression,
                checkExpression: column.CheckExpression
            );
            var created = await connection
                .CreateColumnIfNotExistsAsync(dmColumn, null, cancellationToken)
                .ConfigureAwait(false);

            if (!created)
            {
                throw new InvalidOperationException(
                    $"Failed to create column '{dmColumn.ColumnName}' for an unknown reason."
                );
            }

            var createdColumn = await connection
                .GetColumnAsync(
                    schemaName,
                    tableName,
                    column.ColumnName,
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);

            if (createdColumn == null)
            {
                throw new InvalidOperationException(
                    $"Failed to retrieve the created column '{dmColumn.ColumnName}'."
                );
            }

            await LogAuditEventAsync(
                    context,
                    true,
                    $"Column '{dmColumn.ColumnName}' added to table '{tableName}' successfully."
                )
                .ConfigureAwait(false);
            return createdColumn.ToColumnDto();
        }
    }

    /// <summary>
    /// Updates an existing column in a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name to update.</param>
    /// <param name="newColumnName">The new column name.</param>
    /// /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated column information if successful, otherwise null.</returns>
    public async Task<ColumnDto> RenameColumnAsync(
        IOperationContext context,
        string datasourceId,
        string tableName,
        string columnName,
        string newColumnName,
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
            .NotNullOrWhiteSpace(columnName, nameof(columnName))
            .NotNullOrWhiteSpace(newColumnName, nameof(newColumnName))
            .IsTrue(
                string.IsNullOrWhiteSpace(newColumnName) || newColumnName.Length <= 128,
                nameof(newColumnName),
                "New column name must be 128 characters or fewer."
            )
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

            var renamed = await connection
                .RenameColumnIfExistsAsync(
                    schemaName,
                    tableName,
                    columnName,
                    newColumnName,
                    null,
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (!renamed)
            {
                throw new InvalidOperationException(
                    $"Failed to rename column '{columnName}' to '{newColumnName}'."
                );
            }

            // If renamed, fetch the full column details
            var column = await connection
                .GetColumnAsync(
                    schemaName,
                    tableName,
                    newColumnName,
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);

            if (column == null)
            {
                throw new InvalidOperationException(
                    $"Failed to retrieve the renamed column '{newColumnName}'."
                );
            }

            await LogAuditEventAsync(
                    context,
                    true,
                    $"Column '{columnName}' renamed to '{newColumnName}' in table '{tableName}' successfully."
                )
                .ConfigureAwait(false);
            return column.ToColumnDto();
        }
    }

    /// <summary>
    /// Drops a column from an existing table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name to drop.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the column was dropped, false if it didn't exist.</returns>
    public async Task DropColumnAsync(
        IOperationContext context,
        string datasourceId,
        string tableName,
        string columnName,
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
            .NotNullOrWhiteSpace(columnName, nameof(columnName))
            .Assert();

        // Create connection to drop column
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

            if (
                !await connection
                    .DoesColumnExistAsync(
                        schemaName,
                        tableName,
                        columnName,
                        cancellationToken: cancellationToken
                    )
                    .ConfigureAwait(false)
            )
            {
                throw new KeyNotFoundException($"Column '{columnName}' does not exist");
            }

            var dropped = await connection
                .DropColumnIfExistsAsync(schemaName, tableName, columnName, null, cancellationToken)
                .ConfigureAwait(false);

            if (!dropped)
            {
                throw new InvalidOperationException(
                    $"Failed to drop column '{columnName}' for an unknown reason."
                );
            }

            await LogAuditEventAsync(
                    context,
                    dropped,
                    $"Column '{columnName}' dropped from table '{tableName}' successfully."
                )
                .ConfigureAwait(false);
        }
    }
}
