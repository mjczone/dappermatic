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
/// Partial class containing index-related methods for DapperMaticService.
/// </summary>
public partial class DapperMaticService
{
    /// <summary>
    /// Gets all indexes from the specified table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of indexes.</returns>
    public async Task<IEnumerable<IndexDto>> GetIndexesAsync(
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

        IEnumerable<DmIndex> indexes;

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

            indexes = await connection
                .GetIndexesAsync(schemaName, tableName, null, null, cancellationToken)
                .ConfigureAwait(false);
        }

        await LogAuditEventAsync(context, true, $"Retrieved indexes for table '{tableName}'")
            .ConfigureAwait(false);
        return indexes.ToIndexDtos();
    }

    /// <summary>
    /// Gets a specific index from the table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="indexName">The index name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The index if found, otherwise null.</returns>
    public async Task<IndexDto> GetIndexAsync(
        IOperationContext context,
        string datasourceId,
        string tableName,
        string indexName,
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
            .NotNullOrWhiteSpace(indexName, nameof(indexName))
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

            var index = await connection
                .GetIndexAsync(schemaName, tableName, indexName, null, cancellationToken)
                .ConfigureAwait(false);

            if (index == null)
            {
                throw new KeyNotFoundException(
                    $"Index '{indexName}' not found on table '{tableName}'"
                );
            }

            await LogAuditEventAsync(
                    context,
                    true,
                    $"Retrieved index '{indexName}' from table '{tableName}'"
                )
                .ConfigureAwait(false);
            return index.ToIndexDto();
        }
    }

    /// <summary>
    /// Creates a new index on a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="index">The index details.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created index.</returns>
    public async Task<IndexDto> CreateIndexAsync(
        IOperationContext context,
        string datasourceId,
        string tableName,
        IndexDto index,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    )
    {
        await AssertPermissionsAsync(context).ConfigureAwait(false);

        // Convert DTO to domain model for validation
        schemaName = NormalizeSchemaName(schemaName);

        // auto-generate index name if not provided
        if (
            index != null
            && string.IsNullOrWhiteSpace(index.IndexName)
            && index.ColumnNames != null
            && index.ColumnNames.Count > 0
        )
        {
            index.IndexName = string.IsNullOrWhiteSpace(schemaName)
                ? $"ix_{tableName}_{string.Join('_', index.ColumnNames)}"
                : $"ix_{schemaName}_{tableName}_{string.Join('_', index.ColumnNames)}";
        }

        Validate
            .Arguments()
            .NotNull(context, nameof(context))
            .NotNullOrWhiteSpace(datasourceId, nameof(datasourceId))
            .NotNullOrWhiteSpace(tableName, nameof(tableName))
            .NotNull(index, nameof(index))
            .Object(
                index,
                nameof(index),
                builder =>
                    builder.Custom(
                        r => r.ColumnNames != null && r.ColumnNames.Count > 0,
                        nameof(index.ColumnNames),
                        "At least one column is required."
                    )
            )
            .Assert();

        // Create connection to create index
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

            // Check if index with the same name already exists
            if (
                !string.IsNullOrWhiteSpace(index.IndexName)
                && await connection
                    .DoesIndexExistAsync(
                        schemaName,
                        tableName,
                        index.IndexName,
                        null,
                        cancellationToken
                    )
                    .ConfigureAwait(false)
            )
            {
                throw new DuplicateKeyException(
                    $"An index with the name '{index.IndexName}' already exists on table '{tableName}'."
                );
            }

            var dmIndex = index.ToDmIndex(schemaName, tableName);
            var dmColumnNames = dmIndex.Columns.Select(c => c.ColumnName).ToList();

            var created = await connection
                .CreateIndexIfNotExistsAsync(dmIndex, null, cancellationToken)
                .ConfigureAwait(false);

            if (!created)
            {
                throw new InvalidOperationException(
                    !string.IsNullOrWhiteSpace(dmIndex.IndexName)
                        ? $"Failed to create index '{dmIndex.IndexName}' for an unknown reason."
                        : $"Failed to create index on columns ({string.Join(", ", dmColumnNames)}) for an unknown reason."
                );
            }

            var createdIndex = !string.IsNullOrWhiteSpace(index.IndexName)
                ? await connection
                    .GetIndexAsync(schemaName, tableName, index.IndexName, null, cancellationToken)
                    .ConfigureAwait(false)
                : null;

            if (createdIndex == null)
            {
                // try to match up the index based on columns if no name was provided
                var indexes = await connection
                    .GetIndexesAsync(schemaName, tableName, null, null, cancellationToken)
                    .ConfigureAwait(false);

                createdIndex = indexes.FirstOrDefault(idx =>
                    idx.Columns.Count == dmIndex.Columns.Count
                    && !idx.Columns.All(c =>
                        !dmColumnNames.Contains(c.ColumnName, StringComparer.OrdinalIgnoreCase)
                    )
                );

                if (createdIndex == null)
                {
                    throw new InvalidOperationException($"Failed to retrieve the created index.");
                }
            }

            await LogAuditEventAsync(
                    context,
                    true,
                    $"Index '{createdIndex.IndexName}' created successfully."
                )
                .ConfigureAwait(false);

            return createdIndex.ToIndexDto();
        }
    }

    /// <summary>
    /// Drops an index from a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="indexName">The index name to drop.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DropIndexAsync(
        IOperationContext context,
        string datasourceId,
        string tableName,
        string indexName,
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
            .NotNullOrWhiteSpace(indexName, nameof(indexName))
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

            // Check index exists
            var existingIndex = await connection
                .DoesIndexExistAsync(schemaName, tableName, indexName, null, cancellationToken)
                .ConfigureAwait(false);

            if (!existingIndex)
            {
                throw new KeyNotFoundException(
                    $"Index '{indexName}' not found on table '{tableName}'"
                );
            }

            var dropped = await connection
                .DropIndexIfExistsAsync(schemaName, tableName, indexName, null, cancellationToken)
                .ConfigureAwait(false);

            if (!dropped)
            {
                throw new InvalidOperationException(
                    $"Failed to drop index '{indexName}' on table '{tableName}' for an unknown reason."
                );
            }

            await LogAuditEventAsync(context, dropped, $"Index '{indexName}' dropped successfully.")
                .ConfigureAwait(false);
        }
    }
}
