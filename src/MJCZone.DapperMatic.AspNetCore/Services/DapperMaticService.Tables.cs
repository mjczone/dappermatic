// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using MJCZone.DapperMatic.AspNetCore.Extensions;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Validation;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.AspNetCore.Services;

/// <summary>
/// Partial class containing table-related methods for DapperMaticService.
/// </summary>
public partial class DapperMaticService
{
    /// <summary>
    /// Gets all tables from the specified datasource.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="schemaName">Optional schema name filter.</param>
    /// <param name="includeColumns">Whether to include column information.</param>
    /// <param name="includeIndexes">Whether to include index information.</param>
    /// <param name="includeConstraints">Whether to include constraint information.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of tables.</returns>
    public async Task<IEnumerable<TableDto>> GetTablesAsync(
        IOperationContext context,
        string datasourceId,
        string? schemaName = null,
        bool includeColumns = false,
        bool includeIndexes = false,
        bool includeConstraints = false,
        CancellationToken cancellationToken = default
    )
    {
        await AssertPermissionsAsync(context).ConfigureAwait(false);

        schemaName = NormalizeSchemaName(schemaName);

        Validate
            .Arguments()
            .NotNull(context, nameof(context))
            .NotNullOrWhiteSpace(datasourceId, nameof(datasourceId))
            .Assert();

        // Create connection to get tables
        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            // Check schema exists if specified
            await AssertSchemaExistsIfSpecifiedAsync(datasourceId, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            IEnumerable<DmTable> tables;

            if (includeColumns || includeIndexes || includeConstraints)
            {
                // Get detailed table information
                tables = await connection
                    .GetTablesAsync(schemaName, null, null, cancellationToken)
                    .ConfigureAwait(false);

                // Remove column details if not requested
                foreach (var table in tables)
                {
                    if (!includeColumns)
                    {
                        table.Columns = [];
                    }

                    if (!includeIndexes)
                    {
                        table.Indexes = [];
                    }

                    if (!includeConstraints)
                    {
                        table.PrimaryKeyConstraint = null;
                        table.ForeignKeyConstraints = [];
                        table.UniqueConstraints = [];
                        table.CheckConstraints = [];
                        table.DefaultConstraints = [];
                    }
                }
            }
            else
            {
                // Get just table names and basic info
                var tableNames = await connection
                    .GetTableNamesAsync(schemaName, null, null, cancellationToken)
                    .ConfigureAwait(false);

                tables = tableNames.Select(name => new DmTable(schemaName, name));
            }

            await LogAuditEventAsync(context, true, $"Retrieved tables for datasource '{datasourceId}'")
                .ConfigureAwait(false);
            return tables.ToTableDtos(includeColumns, includeIndexes, includeConstraints);
        }
    }

    /// <summary>
    /// Gets a specific table from the datasource.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="includeColumns">Whether to include column information.</param>
    /// <param name="includeIndexes">Whether to include index information.</param>
    /// <param name="includeConstraints">Whether to include constraint information.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The table.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the table does not exist.</exception>
    public async Task<TableDto> GetTableAsync(
        IOperationContext context,
        string datasourceId,
        string tableName,
        string? schemaName = null,
        bool includeColumns = true,
        bool includeIndexes = true,
        bool includeConstraints = true,
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

        // Create connection to get table
        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            // Check schema exists if specified
            await AssertSchemaExistsIfSpecifiedAsync(datasourceId, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            var table = await connection
                .GetTableAsync(schemaName, tableName, null, cancellationToken)
                .ConfigureAwait(false);

            if (table == null)
            {
                throw new KeyNotFoundException(
                    !string.IsNullOrWhiteSpace(schemaName)
                        ? $"Table '{tableName}' not found in schema '{schemaName}'"
                        : $"Table '{tableName}' not found"
                );
            }

            if (!includeColumns)
            {
                table.Columns = [];
            }

            if (!includeIndexes)
            {
                table.Indexes = [];
            }

            if (!includeConstraints)
            {
                table.PrimaryKeyConstraint = null;
                table.ForeignKeyConstraints = [];
                table.UniqueConstraints = [];
                table.CheckConstraints = [];
                table.DefaultConstraints = [];
            }

            await LogAuditEventAsync(context, true, $"Retrieved table '{tableName}' for datasource '{datasourceId}'")
                .ConfigureAwait(false);
            return table.ToTableDto(includeColumns, includeIndexes, includeConstraints);
        }
    }

    /// <summary>
    /// Creates a new table in the datasource.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="table">The table information.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created table.</returns>
    public async Task<TableDto> CreateTableAsync(
        IOperationContext context,
        string datasourceId,
        TableDto table,
        CancellationToken cancellationToken = default
    )
    {
        await AssertPermissionsAsync(context).ConfigureAwait(false);

        // Convert DTO to domain model for validation
        var dmTable = table.ToDmTable();
        var schemaName = NormalizeSchemaName(dmTable.SchemaName);

        Validate
            .Arguments()
            .NotNull(context, nameof(context))
            .NotNullOrWhiteSpace(datasourceId, nameof(datasourceId))
            .NotNull(table, nameof(table))
            .NotNullOrWhiteSpace(table.TableName, "TableName is required")
            .Assert();

        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            // Check schema exists if specified
            await AssertSchemaExistsIfSpecifiedAsync(datasourceId, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            // Check view does not already exist
            await AssertTableDoesNotExistAsync(
                    datasourceId,
                    dmTable.TableName,
                    schemaName,
                    connection,
                    cancellationToken
                )
                .ConfigureAwait(false);

            // Create table using extension method
            var created = await connection
                .CreateTableIfNotExistsAsync(dmTable, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (!created)
            {
                throw new InvalidOperationException(
                    $"Failed to create table '{dmTable.TableName}' for an unknown reason."
                );
            }

            await LogAuditEventAsync(context, true, $"Table '{dmTable.TableName}' created successfully.")
                .ConfigureAwait(false);

            var createdTable = await connection
                .GetTableAsync(schemaName, dmTable.TableName, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return (
                createdTable
                ?? throw new InvalidOperationException(
                    $"Table '{dmTable.TableName}' was created but could not be retrieved."
                )
            ).ToTableDto();
        }
    }

    /// <summary>
    /// Updates an existing table in the datasource.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The name of the table to update.</param>
    /// <param name="updates">The table updates.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated table.</returns>
    public async Task<TableDto> UpdateTableAsync(
        IOperationContext context,
        string datasourceId,
        string tableName,
        TableDto updates,
        CancellationToken cancellationToken = default
    )
    {
        await AssertPermissionsAsync(context).ConfigureAwait(false);

        var schemaName = NormalizeSchemaName(updates.SchemaName);

        Validate
            .Arguments()
            .NotNull(context, nameof(context))
            .NotNullOrWhiteSpace(datasourceId, nameof(datasourceId))
            .NotNullOrWhiteSpace(tableName, nameof(tableName))
            .NotNull(updates, nameof(updates))
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

            var changesMade = false;
            var transaction = connection.BeginTransaction();
            try
            {
                // Update table if changes are provided
                if (updates.Columns != null && updates.Columns.Count > 0)
                {
                    foreach (var columnDto in updates.Columns)
                    {
                        var column = columnDto.ToDmColumn(schemaName, tableName);
                        column.SchemaName = schemaName;
                        column.TableName = tableName;

                        // Add new column
                        var added = await connection
                            .CreateColumnIfNotExistsAsync(column, transaction, cancellationToken)
                            .ConfigureAwait(false);

                        if (added)
                        {
                            changesMade = true;
                            await LogAuditEventAsync(
                                    context,
                                    true,
                                    $"Column '{column.ColumnName}' added to table '{tableName}'."
                                )
                                .ConfigureAwait(false);
                        }
                    }

                    foreach (var indexDto in updates.Indexes ?? [])
                    {
                        var index = indexDto.ToDmIndex(schemaName, tableName);
                        index.SchemaName = schemaName;
                        index.TableName = tableName;

                        // Add new index
                        var added = await connection
                            .CreateIndexIfNotExistsAsync(index, transaction, cancellationToken)
                            .ConfigureAwait(false);

                        if (added)
                        {
                            changesMade = true;
                            await LogAuditEventAsync(
                                    context,
                                    true,
                                    $"Index '{index.IndexName}' added to table '{tableName}'."
                                )
                                .ConfigureAwait(false);
                        }
                    }

                    foreach (var constraintDto in updates.ForeignKeyConstraints ?? [])
                    {
                        var constraint = constraintDto.ToDmForeignKeyConstraint(schemaName, tableName);
                        constraint.SchemaName = schemaName;
                        constraint.TableName = tableName;

                        // Add new foreign key constraint
                        var added = await connection
                            .CreateForeignKeyConstraintIfNotExistsAsync(constraint, transaction, cancellationToken)
                            .ConfigureAwait(false);

                        if (added)
                        {
                            changesMade = true;
                            await LogAuditEventAsync(
                                    context,
                                    true,
                                    $"Foreign key constraint '{constraint.ConstraintName}' added to table '{tableName}'."
                                )
                                .ConfigureAwait(false);
                        }
                    }

                    foreach (var constraintDto in updates.UniqueConstraints ?? [])
                    {
                        var constraint = constraintDto.ToDmUniqueConstraint(schemaName, tableName);

                        // Add new unique constraint
                        var added = await connection
                            .CreateUniqueConstraintIfNotExistsAsync(constraint, transaction, cancellationToken)
                            .ConfigureAwait(false);

                        if (added)
                        {
                            changesMade = true;
                            await LogAuditEventAsync(
                                    context,
                                    true,
                                    $"Unique constraint '{constraint.ConstraintName}' added to table '{tableName}'."
                                )
                                .ConfigureAwait(false);
                        }
                    }

                    foreach (var constraintDto in updates.CheckConstraints ?? [])
                    {
                        var constraint = constraintDto.ToDmCheckConstraint(schemaName, tableName);
                        constraint.SchemaName = schemaName;
                        constraint.TableName = tableName;

                        // Add new check constraint
                        var added = await connection
                            .CreateCheckConstraintIfNotExistsAsync(constraint, transaction, cancellationToken)
                            .ConfigureAwait(false);

                        if (added)
                        {
                            changesMade = true;
                            await LogAuditEventAsync(
                                    context,
                                    true,
                                    $"Check constraint '{constraint.ConstraintName}' added to table '{tableName}'."
                                )
                                .ConfigureAwait(false);
                        }
                    }

                    foreach (var constraintDto in updates.DefaultConstraints ?? [])
                    {
                        var constraint = constraintDto.ToDmDefaultConstraint(schemaName, tableName);
                        constraint.SchemaName = schemaName;
                        constraint.TableName = tableName;

                        // Add new default constraint
                        var added = await connection
                            .CreateDefaultConstraintIfNotExistsAsync(constraint, transaction, cancellationToken)
                            .ConfigureAwait(false);

                        if (added)
                        {
                            changesMade = true;
                            await LogAuditEventAsync(
                                    context,
                                    true,
                                    $"Default constraint '{constraint.ConstraintName}' added to table '{tableName}'."
                                )
                                .ConfigureAwait(false);
                        }
                    }
                }

                // Update table if needed in future (e.g., table-level properties)
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
            finally
            {
                transaction.Dispose();
            }

            if (!changesMade)
            {
                await LogAuditEventAsync(context, false, "No changes made - no valid definition provided")
                    .ConfigureAwait(false);
                throw new InvalidOperationException("No changes made - no valid definition provided");
            }

            // Get the updated table
            var updatedTable = await connection
                .GetTableAsync(schemaName, tableName, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return (
                updatedTable
                ?? throw new InvalidOperationException($"Table '{tableName}' was updated but could not be retrieved.")
            ).ToTableDto();
        }
    }

    /// <summary>
    /// Renames an existing table in the datasource.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="currentTableName">The current name of the table.</param>
    /// <param name="newTableName">The new name for the table.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The renamed table.</returns>
    public async Task<TableDto> RenameTableAsync(
        IOperationContext context,
        string datasourceId,
        string currentTableName,
        string newTableName,
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
            .NotNullOrWhiteSpace(currentTableName, nameof(currentTableName))
            .IsTrue(
                string.IsNullOrWhiteSpace(newTableName) || newTableName.Length <= 128,
                nameof(newTableName),
                "New table name must be 128 characters or fewer."
            )
            .Assert();

        // Create connection to rename table
        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            // Check schema exists if specified
            await AssertSchemaExistsIfSpecifiedAsync(datasourceId, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            // Check table exists
            await AssertTableExistsAsync(datasourceId, currentTableName, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            // Check new table name does not already exist
            await AssertTableDoesNotExistAsync(datasourceId, newTableName, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            // Rename the table
            var renamed = await connection
                .RenameTableIfExistsAsync(schemaName, currentTableName, newTableName, null, cancellationToken)
                .ConfigureAwait(false);

            if (!renamed)
            {
                throw new InvalidOperationException(
                    $"Failed to rename table '{currentTableName}' to '{newTableName}'."
                );
            }

            await LogAuditEventAsync(
                    context,
                    true,
                    $"Table '{currentTableName}' renamed to '{newTableName}' successfully."
                )
                .ConfigureAwait(false);

            // Get the renamed table
            var renamedTable = await connection
                .GetTableAsync(schemaName, newTableName, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return (
                renamedTable
                ?? throw new InvalidOperationException(
                    $"Table '{newTableName}' was renamed but could not be retrieved."
                )
            ).ToTableDto();
        }
    }

    /// <summary>
    /// Drops a table from the datasource.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The name of the table to drop.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the table does not exist.</exception>
    public async Task DropTableAsync(
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

        // Create connection to drop table
        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            // Check schema exists if specified
            await AssertSchemaExistsIfSpecifiedAsync(datasourceId, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            // Check table exists
            await AssertTableExistsAsync(datasourceId, tableName, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            var dropped = await connection
                .DropTableIfExistsAsync(schemaName, tableName, null, cancellationToken)
                .ConfigureAwait(false);

            if (!dropped)
            {
                throw new InvalidOperationException($"Failed to drop table '{tableName}' for an unknown reason.");
            }

            await LogAuditEventAsync(context, dropped, $"Table '{tableName}' dropped successfully.")
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Checks if a table exists in the datasource.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the table exists, otherwise false.</returns>
    public async Task<bool> TableExistsAsync(
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

        // Create connection to check table existence
        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            // Check schema exists if specified
            await AssertSchemaExistsIfSpecifiedAsync(datasourceId, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            // Check if the table exists
            var exists = await connection
                .DoesTableExistAsync(schemaName, tableName, null, cancellationToken)
                .ConfigureAwait(false);

            await LogAuditEventAsync(
                    context,
                    true,
                    exists == true ? $"Table '{tableName}' exists." : $"Table '{tableName}' does not exist."
                )
                .ConfigureAwait(false);
            return exists;
        }
    }

    /// <summary>
    /// Queries a table with filtering, sorting, and pagination.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name to query.</param>
    /// <param name="request">The query parameters.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The query results with pagination information.</returns>
    public async Task<QueryResultDto> QueryTableAsync(
        IOperationContext context,
        string datasourceId,
        string tableName,
        QueryDto request,
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

        // Create connection to check table existence
        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            // Check schema exists if specified
            await AssertSchemaExistsIfSpecifiedAsync(datasourceId, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            // Check table exists
            await AssertTableExistsAsync(datasourceId, tableName, schemaName, connection, cancellationToken)
                .ConfigureAwait(false);

            // Build the query using provider-specific identifier naming
            var qualifiedFromName = connection.GetSchemaQualifiedTableName(schemaName, tableName);

            var result = await ExecuteDataQueryAsync(connection, qualifiedFromName, request, schemaName, tableName)
                .ConfigureAwait(false);

            await LogAuditEventAsync(context, true, $"Queried table '{tableName}' for datasource '{datasourceId}'")
                .ConfigureAwait(false);
            return result;
        }
    }
}
