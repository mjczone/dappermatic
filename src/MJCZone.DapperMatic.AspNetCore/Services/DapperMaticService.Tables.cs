// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Security.Claims;

using MJCZone.DapperMatic.AspNetCore.Extensions;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Models.Requests;
using MJCZone.DapperMatic.AspNetCore.Models.Responses;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.AspNetCore.Services;

/// <summary>
/// Partial class containing table-related methods for DapperMaticService.
/// </summary>
public sealed partial class DapperMaticService
{
    /// <summary>
    /// Gets all tables from the specified datasource.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="schemaName">Optional schema name filter.</param>
    /// <param name="includeColumns">Whether to include column information.</param>
    /// <param name="includeIndexes">Whether to include index information.</param>
    /// <param name="includeConstraints">Whether to include constraint information.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of tables.</returns>
    public async Task<IEnumerable<TableDto>> GetTablesAsync(
        string datasourceId,
        string? schemaName = null,
        bool includeColumns = false,
        bool includeIndexes = false,
        bool includeConstraints = false,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(datasourceId))
        {
            throw new ArgumentException("Datasource ID is required.", nameof(datasourceId));
        }

        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.ListTables,
            DatasourceId = datasourceId,
            SchemaName = schemaName,
        };

        // Check permissions
        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException($"Access denied to datasource '{datasourceId}'");
        }

        try
        {
            // Create connection to get tables
            var connection = await CreateConnectionForDatasource(datasourceId)
                .ConfigureAwait(false);
            using (connection)
            {
                IEnumerable<DmTable> tables;

                if (includeColumns || includeIndexes || includeConstraints)
                {
                    // Get detailed table information
                    tables = await connection
                        .GetTablesAsync(schemaName, null, null, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    // Get just table names and basic info
                    var tableNames = await connection
                        .GetTableNamesAsync(schemaName, null, null, cancellationToken)
                        .ConfigureAwait(false);

                    tables = tableNames.Select(name => new DmTable(schemaName, name));
                }

                await LogAuditEventAsync(context, true).ConfigureAwait(false);

                return tables.ToTableDtos(includeColumns, includeIndexes, includeConstraints);
            }
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException and not ArgumentException)
        {
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Gets a specific table from the datasource.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="includeColumns">Whether to include column information.</param>
    /// <param name="includeIndexes">Whether to include index information.</param>
    /// <param name="includeConstraints">Whether to include constraint information.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The table if found, otherwise null.</returns>
    public async Task<TableDto?> GetTableAsync(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        bool includeColumns = true,
        bool includeIndexes = true,
        bool includeConstraints = true,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(datasourceId))
        {
            throw new ArgumentException("Datasource ID is required.", nameof(datasourceId));
        }

        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.GetTable,
            DatasourceId = datasourceId,
            SchemaName = schemaName,
            TableName = tableName,
        };

        // Check permissions
        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to table '{tableName}' in datasource '{datasourceId}'"
            );
        }

        try
        {
            // Create connection to get table
            var connection = await CreateConnectionForDatasource(datasourceId)
                .ConfigureAwait(false);
            using (connection)
            {
                var table = await connection
                    .GetTableAsync(schemaName, tableName, null, cancellationToken)
                    .ConfigureAwait(false);

                if (table == null)
                {
                    await LogAuditEventAsync(context, true, "Table not found")
                        .ConfigureAwait(false);
                    return null;
                }

                await LogAuditEventAsync(context, true).ConfigureAwait(false);

                return table.ToTableDto(includeColumns, includeIndexes, includeConstraints);
            }
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException and not ArgumentException)
        {
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Creates a new table in the datasource.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="request">The table creation request.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created table if successful, otherwise null.</returns>
    public async Task<TableDto?> CreateTableAsync(
        string datasourceId,
        CreateTableRequest request,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(datasourceId))
        {
            throw new ArgumentException("Datasource ID is required.", nameof(datasourceId));
        }

        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.TableName))
        {
            throw new ArgumentException("Table name is required.", nameof(request));
        }

        if (request.Columns == null || request.Columns.Count == 0)
        {
            throw new ArgumentException("At least one column is required.", nameof(request));
        }

        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.CreateTable,
            DatasourceId = datasourceId,
            SchemaName = request.SchemaName,
            TableName = request.TableName,
            RequestBody = request,
        };

        // Check permissions
        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to create table '{request.TableName}' in datasource '{datasourceId}'"
            );
        }

        try
        {
            // Create connection to drop table
            var connection = await CreateConnectionForDatasource(datasourceId)
                .ConfigureAwait(false);
            using (connection)
            {
                DmColumn[] columns =
                [
                    .. request.Columns.Select(c =>
                    {
                        var column = new DmColumn(
                            c.ColumnName,
                            null,
                            providerDataTypes: new Dictionary<DbProviderType, string>
                            {
                                { connection.GetDbProviderType(), c.ProviderDataType },
                            },
                            isPrimaryKey: c.IsPrimaryKey,
                            isAutoIncrement: c.IsAutoIncrement,
                            isUnicode: c.IsUnique == true,
                            isUnique: c.IsUnique,
                            isIndexed: c.IsIndexed,
                            isNullable: c.IsNullable,
                            defaultExpression: c.DefaultExpression,
                            checkExpression: c.CheckExpression
                        );
                        return column;
                    }),
                ];
                DmIndex[] indexes =
                [
                    .. request
                        .Indexes?.Select(i =>
                        {
                            var index = new DmIndex(
                                i.IndexName,
                                [.. i.Columns.Select(ci => DmOrderedColumn.Parse(ci))],
                                isUnique: i.IsUnique
                            );
                            return index;
                        })
                        .ToList() ?? [],
                ];
                DmPrimaryKeyConstraint? primaryKeyConstraint =
                    request.PrimaryKey == null
                        ? null
                        : new DmPrimaryKeyConstraint(
                            request.PrimaryKey.ConstraintName ?? string.Empty,
                            [.. request.PrimaryKey.Columns.Select(c => DmOrderedColumn.Parse(c))]
                        );
                DmCheckConstraint[]? checkConstraints =
                    request.CheckConstraints == null
                        ? null
                        :
                        [
                            .. request.CheckConstraints.Select(c => new DmCheckConstraint(
                                string.IsNullOrWhiteSpace(c.ColumnName) ? null : c.ColumnName,
                                c.ConstraintName ?? string.Empty,
                                c.CheckExpression
                            )),
                        ];
                DmDefaultConstraint[]? defaultConstraints =
                    request.DefaultConstraints == null
                        ? null
                        :
                        [
                            .. request.DefaultConstraints.Select(c => new DmDefaultConstraint(
                                c.ColumnName,
                                c.ConstraintName ?? string.Empty,
                                c.DefaultExpression
                            )),
                        ];
                DmUniqueConstraint[]? uniqueConstraints =
                    request.UniqueConstraints == null
                        ? null
                        :
                        [
                            .. request.UniqueConstraints.Select(c => new DmUniqueConstraint(
                                c.ConstraintName ?? string.Empty,
                                [.. c.ColumnNames.Select(col => DmOrderedColumn.Parse(col))]
                            )),
                        ];
                DmForeignKeyConstraint[]? foreignKeyConstraints =
                    request.ForeignKeys == null
                        ? null
                        :
                        [
                            .. request.ForeignKeys.Select(c => new DmForeignKeyConstraint(
                                c.ConstraintName ?? string.Empty,
                                [.. c.Columns.Select(col => DmOrderedColumn.Parse(col))],
                                c.ReferencedTableName,
                                [.. c.ReferencedColumns.Select(col => DmOrderedColumn.Parse(col))],
                                onUpdate: c.OnUpdate?.ToForeignKeyAction()
                                    ?? DmForeignKeyAction.NoAction,
                                onDelete: c.OnDelete?.ToForeignKeyAction()
                                    ?? DmForeignKeyAction.NoAction
                            )),
                        ];

                var table = new DmTable(
                    request.SchemaName,
                    request.TableName,
                    columns,
                    primaryKeyConstraint,
                    checkConstraints,
                    defaultConstraints,
                    uniqueConstraints,
                    foreignKeyConstraints,
                    indexes
                );

                var created = await connection
                    .CreateTableIfNotExistsAsync(table, null, cancellationToken)
                    .ConfigureAwait(false);

                var message = created ? "Table created successfully" : "Table already exists";
                await LogAuditEventAsync(context, true, message).ConfigureAwait(false);

                if (created)
                {
                    // If created, fetch the full table details
                    table =
                        await connection
                            .GetTableAsync(
                                request.SchemaName,
                                request.TableName,
                                null,
                                cancellationToken
                            )
                            .ConfigureAwait(false) ?? table;

                    return table.ToTableDto(true, true, true);
                }
            }
            return null;
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException and not ArgumentException)
        {
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Drops a table from the datasource.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The name of the table to drop.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the table was dropped, false if it didn't exist.</returns>
    public async Task<bool> DropTableAsync(
        string datasourceId,
        string tableName,
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

        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.DropTable,
            DatasourceId = datasourceId,
            SchemaName = schemaName,
            TableName = tableName,
        };

        // Check permissions
        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to drop table '{tableName}' in datasource '{datasourceId}'"
            );
        }

        try
        {
            // Create connection to drop table
            var connection = await CreateConnectionForDatasource(datasourceId)
                .ConfigureAwait(false);
            using (connection)
            {
                var dropped = await connection
                    .DropTableIfExistsAsync(schemaName, tableName, null, cancellationToken)
                    .ConfigureAwait(false);

                var message = dropped ? "Table dropped successfully" : "Table not found";
                await LogAuditEventAsync(context, true, message).ConfigureAwait(false);

                return dropped;
            }
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException and not ArgumentException)
        {
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Checks if a table exists in the datasource.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the table exists, otherwise false.</returns>
    public async Task<bool> TableExistsAsync(
        string datasourceId,
        string tableName,
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

        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.TableExists,
            DatasourceId = datasourceId,
            SchemaName = schemaName,
            TableName = tableName,
        };

        // Check permissions
        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to check table '{tableName}' in datasource '{datasourceId}'"
            );
        }

        try
        {
            // Create connection to check table existence
            var connection = await CreateConnectionForDatasource(datasourceId)
                .ConfigureAwait(false);
            using (connection)
            {
                var exists = await connection
                    .DoesTableExistAsync(schemaName, tableName, null, cancellationToken)
                    .ConfigureAwait(false);

                var message = exists ? "Table exists" : "Table does not exist";
                await LogAuditEventAsync(context, true, message).ConfigureAwait(false);

                return exists;
            }
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException and not ArgumentException)
        {
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Queries a table with filtering, sorting, and pagination.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name to query.</param>
    /// <param name="request">The query parameters.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The query results with pagination information.</returns>
    public async Task<QueryResultDto> QueryTableAsync(
        string datasourceId,
        string tableName,
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

        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        ArgumentNullException.ThrowIfNull(request);

        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.QueryTable,
            DatasourceId = datasourceId,
            SchemaName = schemaName,
            TableName = tableName,
            RequestBody = request,
        };

        // Check permissions
        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to query table '{tableName}' in datasource '{datasourceId}'"
            );
        }

        try
        {
            // Create connection to check table existence
            var connection = await CreateConnectionForDatasource(datasourceId)
                .ConfigureAwait(false);
            using (connection)
            {
                // First verify the view exists
                var tableExists = await connection
                    .DoesTableExistAsync(schemaName, tableName, null, cancellationToken)
                    .ConfigureAwait(false);

                if (!tableExists)
                {
                    throw new ArgumentException($"Table '{tableName}' does not exist.");
                }

                // Build the query using provider-specific identifier naming
                var qualifiedFromName = connection.GetSchemaQualifiedTableName(
                    schemaName,
                    tableName
                );

                var result = await ExecuteDataQueryAsync(
                        connection,
                        qualifiedFromName,
                        request,
                        schemaName,
                        tableName
                    )
                    .ConfigureAwait(false);

                await LogAuditEventAsync(context, true).ConfigureAwait(false);
                return result;
            }
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException and not ArgumentException)
        {
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Renames an existing table in the datasource.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The current name of the table.</param>
    /// <param name="newTableName">The new name for the table.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the table was renamed successfully, false if it didn't exist.</returns>
    public async Task<bool> RenameTableAsync(
        string datasourceId,
        string tableName,
        string newTableName,
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

        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        if (string.IsNullOrWhiteSpace(newTableName))
        {
            throw new ArgumentException("New table name is required.", nameof(newTableName));
        }

        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.RenameTable,
            DatasourceId = datasourceId,
            SchemaName = schemaName,
            TableName = tableName,
            RequestBody = new { NewTableName = newTableName },
        };

        // Check permissions
        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to rename table '{tableName}' in datasource '{datasourceId}'"
            );
        }

        try
        {
            // Create connection to rename table
            var connection = await CreateConnectionForDatasource(datasourceId)
                .ConfigureAwait(false);
            using (connection)
            {
                var renamed = await connection
                    .RenameTableIfExistsAsync(
                        schemaName,
                        tableName,
                        newTableName,
                        null,
                        cancellationToken
                    )
                    .ConfigureAwait(false);

                var message = renamed
                    ? $"Table renamed from '{tableName}' to '{newTableName}' successfully"
                    : "Table not found";
                await LogAuditEventAsync(context, true, message).ConfigureAwait(false);

                return renamed;
            }
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException and not ArgumentException)
        {
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    #region Column Management

    /// <summary>
    /// Gets all columns from the specified table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of columns.</returns>
    public async Task<IEnumerable<ColumnDto>> GetColumnsAsync(
        string datasourceId,
        string tableName,
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

        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.ListColumns,
            DatasourceId = datasourceId,
            SchemaName = schemaName,
            TableName = tableName,
        };

        // Check permissions
        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to columns in table '{tableName}' in datasource '{datasourceId}'"
            );
        }

        try
        {
            // Create connection to get columns
            var connection = await CreateConnectionForDatasource(datasourceId)
                .ConfigureAwait(false);
            using (connection)
            {
                var table = await connection
                    .GetTableAsync(schemaName, tableName, null, cancellationToken)
                    .ConfigureAwait(false);

                if (table == null)
                {
                    throw new ArgumentException($"Table '{tableName}' does not exist.");
                }

                await LogAuditEventAsync(context, true).ConfigureAwait(false);

                return table.Columns.ToColumnDtos();
            }
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException and not ArgumentException)
        {
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Gets a specific column from the table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The column if found, otherwise null.</returns>
    public async Task<ColumnDto?> GetColumnAsync(
        string datasourceId,
        string tableName,
        string columnName,
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

        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        if (string.IsNullOrWhiteSpace(columnName))
        {
            throw new ArgumentException("Column name is required.", nameof(columnName));
        }

        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.GetColumn,
            DatasourceId = datasourceId,
            SchemaName = schemaName,
            TableName = tableName,
            ColumnName = columnName,
        };

        // Check permissions
        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to column '{columnName}' in table '{tableName}' in datasource '{datasourceId}'"
            );
        }

        try
        {
            // Create connection to get column
            var connection = await CreateConnectionForDatasource(datasourceId)
                .ConfigureAwait(false);
            using (connection)
            {
                var table = await connection
                    .GetTableAsync(schemaName, tableName, null, cancellationToken)
                    .ConfigureAwait(false);

                if (table == null)
                {
                    throw new ArgumentException($"Table '{tableName}' does not exist.");
                }

                var column = table.Columns.FirstOrDefault(c =>
                    string.Equals(c.ColumnName, columnName, StringComparison.OrdinalIgnoreCase)
                );

                if (column == null)
                {
                    await LogAuditEventAsync(context, true, "Column not found")
                        .ConfigureAwait(false);
                    return null;
                }

                await LogAuditEventAsync(context, true).ConfigureAwait(false);

                return column.ToColumnDto();
            }
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException and not ArgumentException)
        {
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Adds a new column to an existing table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="request">The add column request.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The added column information if successful, otherwise null.</returns>
    public async Task<ColumnDto?> AddColumnAsync(
        string datasourceId,
        string tableName,
        CreateTableColumnRequest request,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(datasourceId))
        {
            throw new ArgumentException("Datasource ID is required.", nameof(datasourceId));
        }

        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.ColumnName))
        {
            throw new ArgumentException("Column name is required.", nameof(request));
        }

        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.AddColumn,
            DatasourceId = datasourceId,
            SchemaName = schemaName,
            TableName = tableName,
            ColumnName = request.ColumnName,
            RequestBody = request,
        };

        // Check permissions
        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to create column '{tableName}.{request.ColumnName}' in datasource '{datasourceId}'"
            );
        }

        try
        {
            var connection = await CreateConnectionForDatasource(datasourceId)
                .ConfigureAwait(false);
            using (connection)
            {
                var column = new DmColumn(
                    schemaName,
                    tableName,
                    request.ColumnName,
                    null,
                    providerDataTypes: new Dictionary<DbProviderType, string>
                    {
                        { connection.GetDbProviderType(), request.ProviderDataType },
                    },
                    isPrimaryKey: request.IsPrimaryKey,
                    isAutoIncrement: request.IsAutoIncrement,
                    isUnicode: request.IsUnique == true,
                    isUnique: request.IsUnique,
                    isIndexed: request.IsIndexed,
                    isNullable: request.IsNullable,
                    defaultExpression: request.DefaultExpression,
                    checkExpression: request.CheckExpression
                );
                var created = await connection
                    .CreateColumnIfNotExistsAsync(column, null, cancellationToken)
                    .ConfigureAwait(false);

                var message = created ? "Column added successfully" : "Column already exists";
                await LogAuditEventAsync(context, true, message).ConfigureAwait(false);

                if (created)
                {
                    // If created, fetch the full column details
                    column =
                        await connection
                            .GetColumnAsync(
                                schemaName,
                                tableName,
                                request.ColumnName,
                                cancellationToken: cancellationToken
                            )
                            .ConfigureAwait(false) ?? column;

                    return column.ToColumnDto();
                }
            }
            return null;
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException and not ArgumentException)
        {
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing column in a table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name to update.</param>
    /// <param name="newColumnName">The new column name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated column information if successful, otherwise null.</returns>
    public async Task<ColumnDto?> RenameColumnAsync(
        string datasourceId,
        string tableName,
        string columnName,
        string newColumnName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(datasourceId))
        {
            throw new ArgumentException("Datasource ID is required.", nameof(datasourceId));
        }

        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        if (string.IsNullOrWhiteSpace(columnName))
        {
            throw new ArgumentException("Column name is required.", nameof(columnName));
        }

        if (string.IsNullOrWhiteSpace(newColumnName))
        {
            throw new ArgumentException("New column name is required.", nameof(newColumnName));
        }

        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.UpdateColumn,
            DatasourceId = datasourceId,
            SchemaName = schemaName,
            TableName = tableName,
            ColumnName = columnName,
            RequestBody = new { NewColumnName = newColumnName },
        };

        // Check permissions
        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to update column '{tableName}.{columnName}' in datasource '{datasourceId}'"
            );
        }

        try
        {
            var connection = await CreateConnectionForDatasource(datasourceId)
                .ConfigureAwait(false);
            using (connection)
            {
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

                var message = renamed ? "Column renamed successfully" : "Column already exists";
                await LogAuditEventAsync(context, true, message).ConfigureAwait(false);

                if (renamed)
                {
                    // If renamed, fetch the full column details
                    var column = await connection
                        .GetColumnAsync(
                            schemaName,
                            tableName,
                            newColumnName,
                            cancellationToken: cancellationToken
                        )
                        .ConfigureAwait(false);

                    return column?.ToColumnDto();
                }
            }
            return null;
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException and not ArgumentException)
        {
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Drops a column from an existing table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name to drop.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the column was dropped, false if it didn't exist.</returns>
    public async Task<bool> DropColumnAsync(
        string datasourceId,
        string tableName,
        string columnName,
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

        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        if (string.IsNullOrWhiteSpace(columnName))
        {
            throw new ArgumentException("Column name is required.", nameof(columnName));
        }

        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.DropColumn,
            DatasourceId = datasourceId,
            SchemaName = schemaName,
            TableName = tableName,
            ColumnName = columnName,
        };

        // Check permissions
        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to drop column '{columnName}' from table '{tableName}' in datasource '{datasourceId}'"
            );
        }

        try
        {
            // Create connection to drop column
            var connection = await CreateConnectionForDatasource(datasourceId)
                .ConfigureAwait(false);
            using (connection)
            {
                var dropped = await connection
                    .DropColumnIfExistsAsync(
                        schemaName,
                        tableName,
                        columnName,
                        null,
                        cancellationToken
                    )
                    .ConfigureAwait(false);

                var message = dropped ? "Column dropped successfully" : "Column not found";
                await LogAuditEventAsync(context, true, message).ConfigureAwait(false);

                return dropped;
            }
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException and not ArgumentException)
        {
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    #endregion

    #region Index Management

    /// <summary>
    /// Gets all indexes from the specified table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of indexes.</returns>
    public async Task<IEnumerable<IndexDto>> GetIndexesAsync(
        string datasourceId,
        string tableName,
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

        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.ListIndexes,
            DatasourceId = datasourceId,
            SchemaName = schemaName,
            TableName = tableName,
        };

        // Check permissions
        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to indexes in table '{tableName}' in datasource '{datasourceId}'"
            );
        }

        try
        {
            // Create connection to get indexes
            var connection = await CreateConnectionForDatasource(datasourceId)
                .ConfigureAwait(false);
            using (connection)
            {
                var table = await connection
                    .GetTableAsync(schemaName, tableName, null, cancellationToken)
                    .ConfigureAwait(false);

                if (table == null)
                {
                    throw new ArgumentException($"Table '{tableName}' does not exist.");
                }

                await LogAuditEventAsync(context, true).ConfigureAwait(false);

                return table.Indexes.ToIndexDtos();
            }
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException and not ArgumentException)
        {
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Gets a specific index from the table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="indexName">The index name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The index if found, otherwise null.</returns>
    public async Task<IndexDto?> GetIndexAsync(
        string datasourceId,
        string tableName,
        string indexName,
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

        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        if (string.IsNullOrWhiteSpace(indexName))
        {
            throw new ArgumentException("Index name is required.", nameof(indexName));
        }

        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.GetIndex,
            DatasourceId = datasourceId,
            SchemaName = schemaName,
            TableName = tableName,
            IndexName = indexName,
        };

        // Check permissions
        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to index '{indexName}' in table '{tableName}' in datasource '{datasourceId}'"
            );
        }

        try
        {
            // Create connection to get index
            var connection = await CreateConnectionForDatasource(datasourceId)
                .ConfigureAwait(false);
            using (connection)
            {
                var table = await connection
                    .GetTableAsync(schemaName, tableName, null, cancellationToken)
                    .ConfigureAwait(false);

                if (table == null)
                {
                    throw new ArgumentException($"Table '{tableName}' does not exist.");
                }

                var index = table.Indexes.FirstOrDefault(i =>
                    string.Equals(i.IndexName, indexName, StringComparison.OrdinalIgnoreCase)
                );

                if (index == null)
                {
                    await LogAuditEventAsync(context, true, "Index not found")
                        .ConfigureAwait(false);
                    return null;
                }

                await LogAuditEventAsync(context, true).ConfigureAwait(false);

                return index.ToIndexDto();
            }
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException and not ArgumentException)
        {
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Creates a new index on a table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="request">The index creation request.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created index if successful, otherwise null.</returns>
    public async Task<IndexDto?> CreateIndexAsync(
        string datasourceId,
        string tableName,
        CreateIndexRequest request,
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

        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.IndexName))
        {
            throw new ArgumentException("Index name is required.", nameof(request));
        }

        if (request.Columns == null || request.Columns.Count == 0)
        {
            throw new ArgumentException("At least one column is required.", nameof(request));
        }

        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.CreateIndex,
            DatasourceId = datasourceId,
            SchemaName = schemaName,
            TableName = tableName,
            IndexName = request.IndexName,
            RequestBody = request,
        };

        // Check permissions
        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to create index '{request.IndexName}' on table '{tableName}' in datasource '{datasourceId}'"
            );
        }

        try
        {
            // Create connection to create index
            var connection = await CreateConnectionForDatasource(datasourceId)
                .ConfigureAwait(false);
            using (connection)
            {
                // Check if table exists
                var tableExists = await connection
                    .DoesTableExistAsync(schemaName, tableName, null, cancellationToken)
                    .ConfigureAwait(false);

                if (!tableExists)
                {
                    throw new ArgumentException($"Table '{tableName}' does not exist.");
                }

                var index = new DmIndex(
                    schemaName,
                    tableName,
                    request.IndexName,
                    [.. request.Columns.Select(c => DmOrderedColumn.Parse(c))],
                    isUnique: request.IsUnique
                );

                var created = await connection
                    .CreateIndexIfNotExistsAsync(index, null, cancellationToken)
                    .ConfigureAwait(false);

                var message = created ? "Index created successfully" : "Index already exists";
                await LogAuditEventAsync(context, true, message).ConfigureAwait(false);

                if (created)
                {
                    // If created, fetch the index details
                    var table = await connection
                        .GetTableAsync(schemaName, tableName, null, cancellationToken)
                        .ConfigureAwait(false);

                    var createdIndex = table?.Indexes.FirstOrDefault(i =>
                        string.Equals(
                            i.IndexName,
                            request.IndexName,
                            StringComparison.OrdinalIgnoreCase
                        )
                    );

                    return createdIndex?.ToIndexDto();
                }
            }
            return null;
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException and not ArgumentException)
        {
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Drops an index from a table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="indexName">The index name to drop.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the index was dropped, false if it didn't exist.</returns>
    public async Task<bool> DropIndexAsync(
        string datasourceId,
        string tableName,
        string indexName,
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

        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        if (string.IsNullOrWhiteSpace(indexName))
        {
            throw new ArgumentException("Index name is required.", nameof(indexName));
        }

        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.DropIndex,
            DatasourceId = datasourceId,
            SchemaName = schemaName,
            TableName = tableName,
            IndexName = indexName,
        };

        // Check permissions
        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to drop index '{indexName}' from table '{tableName}' in datasource '{datasourceId}'"
            );
        }

        try
        {
            // Create connection to drop index
            var connection = await CreateConnectionForDatasource(datasourceId)
                .ConfigureAwait(false);
            using (connection)
            {
                var dropped = await connection
                    .DropIndexIfExistsAsync(
                        schemaName,
                        tableName,
                        indexName,
                        null,
                        cancellationToken
                    )
                    .ConfigureAwait(false);

                var message = dropped ? "Index dropped successfully" : "Index not found";
                await LogAuditEventAsync(context, true, message).ConfigureAwait(false);

                return dropped;
            }
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException and not ArgumentException)
        {
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    #endregion
}
