// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using MJCZone.DapperMatic.AspNetCore.Extensions;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Models.Requests;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.AspNetCore.Services;

/// <summary>
/// Partial class containing constraint-related methods for DapperMaticService.
/// Note: These methods are stub implementations and require proper implementation.
/// </summary>
public sealed partial class DapperMaticService
{
    #region Primary Key Constraints

    /// <summary>
    /// Gets the primary key constraint from the specified table.
    /// </summary>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="user">The claims principal for authorization (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The primary key constraint or null if not found.</returns>
    public async Task<PrimaryKeyConstraintDto?> GetPrimaryKeyAsync(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        if (schemaName == "_")
        {
            schemaName = null;
        }

        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            var table = await connection
                .GetTableAsync(schemaName, tableName, null, cancellationToken)
                .ConfigureAwait(false);
            return table?.PrimaryKeyConstraint?.ToPrimaryKeyConstraintDto();
        }
    }

    /// <summary>
    /// Creates a new primary key constraint on a table.
    /// </summary>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="request">The request containing primary key details.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="user">The claims principal for authorization (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created primary key constraint or null if creation failed.</returns>
    public async Task<PrimaryKeyConstraintDto?> CreatePrimaryKeyAsync(
        string datasourceId,
        string tableName,
        CreatePrimaryKeyRequest request,
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

        if (string.IsNullOrWhiteSpace(request.ConstraintName))
        {
            throw new ArgumentException("Constraint name is required.", nameof(request));
        }

        if (request.Columns == null || request.Columns.Count == 0)
        {
            throw new ArgumentException("At least one column is required.", nameof(request));
        }

        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.CreatePrimaryKey,
            DatasourceId = datasourceId,
            SchemaName = schemaName,
            TableName = tableName,
            ConstraintName = request.ConstraintName,
            RequestBody = request,
        };

        // Check permissions
        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to create primary key '{request.ConstraintName}' on table '{tableName}' in datasource '{datasourceId}'"
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

                var pk = new DmPrimaryKeyConstraint(
                    schemaName,
                    tableName,
                    request.ConstraintName,
                    [.. request.Columns.Select(c => DmOrderedColumn.Parse(c))]
                );

                var created = await connection
                    .CreatePrimaryKeyConstraintIfNotExistsAsync(pk, null, cancellationToken)
                    .ConfigureAwait(false);

                var message = created
                    ? "Primary key created successfully"
                    : "Primary key already exists";
                await LogAuditEventAsync(context, true, message).ConfigureAwait(false);

                if (created)
                {
                    var createdPrimaryKey = await connection
                        .GetPrimaryKeyConstraintAsync(
                            schemaName,
                            tableName,
                            null,
                            cancellationToken
                        )
                        .ConfigureAwait(false);

                    return createdPrimaryKey?.ToPrimaryKeyConstraintDto();
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
    /// Drops the primary key constraint from a table.
    /// </summary>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="user">The claims principal for authorization (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the primary key was dropped successfully, false otherwise.</returns>
    public async Task<bool> DropPrimaryKeyAsync(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        if (schemaName == "_")
        {
            schemaName = null;
        }

        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            return await connection
                .DropPrimaryKeyConstraintIfExistsAsync(
                    schemaName,
                    tableName,
                    null,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
    }

    #endregion

    #region Foreign Key Constraints

    /// <summary>
    /// Gets all foreign key constraints from the specified table.
    /// </summary>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="user">The claims principal for authorization (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of foreign key constraints.</returns>
    public async Task<IEnumerable<ForeignKeyConstraintDto>> GetForeignKeysAsync(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        if (schemaName == "_")
        {
            schemaName = null;
        }

        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            var table = await connection
                .GetTableAsync(schemaName, tableName, null, cancellationToken)
                .ConfigureAwait(false);
            return table?.ForeignKeyConstraints.ToForeignKeyConstraintDtos()
                ?? Enumerable.Empty<ForeignKeyConstraintDto>();
        }
    }

    /// <summary>
    /// Gets a specific foreign key constraint from the table.
    /// </summary>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="constraintName">The name of the constraint.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="user">The claims principal for authorization (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The foreign key constraint or null if not found.</returns>
    public async Task<ForeignKeyConstraintDto?> GetForeignKeyAsync(
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        if (schemaName == "_")
        {
            schemaName = null;
        }

        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            var table = await connection
                .GetTableAsync(schemaName, tableName, null, cancellationToken)
                .ConfigureAwait(false);
            var foreignKey = table?.ForeignKeyConstraints.FirstOrDefault(fk =>
                string.Equals(fk.ConstraintName, constraintName, StringComparison.OrdinalIgnoreCase)
            );
            return foreignKey?.ToForeignKeyConstraintDto();
        }
    }

    /// <summary>
    /// Creates a new foreign key constraint on a table.
    /// </summary>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="request">The request containing foreign key details.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="user">The claims principal for authorization (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// /// <returns>The created foreign key constraint or null if creation failed.</returns>
    public async Task<ForeignKeyConstraintDto?> CreateForeignKeyAsync(
        string datasourceId,
        string tableName,
        CreateForeignKeyRequest request,
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

        if (string.IsNullOrWhiteSpace(request.ConstraintName))
        {
            throw new ArgumentException("Constraint name is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ReferencedTableName))
        {
            throw new ArgumentException("Referenced table is required.", nameof(request));
        }

        if (request.Columns == null || request.Columns.Count == 0)
        {
            throw new ArgumentException("At least one column is required.", nameof(request));
        }

        if (request.ReferencedColumns == null || request.ReferencedColumns.Count == 0)
        {
            throw new ArgumentException(
                "At least one referenced column is required.",
                nameof(request)
            );
        }

        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.CreateForeignKey,
            DatasourceId = datasourceId,
            SchemaName = schemaName,
            TableName = tableName,
            ConstraintName = request.ConstraintName,
            RequestBody = request,
        };

        // Check permissions
        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to create foreign key '{request.ConstraintName}' on table '{tableName}' in datasource '{datasourceId}'"
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

                var fk = new DmForeignKeyConstraint(
                    schemaName,
                    tableName,
                    request.ConstraintName,
                    [.. request.Columns.Select(c => DmOrderedColumn.Parse(c))],
                    request.ReferencedTableName,
                    [.. request.ReferencedColumns.Select(c => DmOrderedColumn.Parse(c))],
                    onUpdate: request.OnUpdate?.ToForeignKeyAction() ?? DmForeignKeyAction.NoAction,
                    onDelete: request.OnDelete?.ToForeignKeyAction() ?? DmForeignKeyAction.NoAction
                );

                var created = await connection
                    .CreateForeignKeyConstraintIfNotExistsAsync(fk, null, cancellationToken)
                    .ConfigureAwait(false);

                var message = created
                    ? "Foreign key created successfully"
                    : "Foreign key already exists";
                await LogAuditEventAsync(context, true, message).ConfigureAwait(false);

                if (created)
                {
                    var createdForeignKey = await connection
                        .GetForeignKeyConstraintAsync(
                            schemaName,
                            tableName,
                            request.ConstraintName,
                            null,
                            cancellationToken
                        )
                        .ConfigureAwait(false);

                    return createdForeignKey?.ToForeignKeyConstraintDto();
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
    /// Drops a foreign key constraint from a table.
    /// </summary>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="constraintName">The name of the constraint.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="user">The claims principal for authorization (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the foreign key was dropped successfully, false otherwise.</returns>
    public async Task<bool> DropForeignKeyAsync(
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        if (schemaName == "_")
        {
            schemaName = null;
        }

        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            return await connection
                .DropForeignKeyConstraintIfExistsAsync(
                    schemaName,
                    tableName,
                    constraintName,
                    null,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
    }

    #endregion

    #region Check Constraints

    /// <summary>
    /// Gets all check constraints from the specified table.
    /// </summary>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="user">The claims principal for authorization (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of check constraints.</returns>
    public async Task<IEnumerable<CheckConstraintDto>> GetCheckConstraintsAsync(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        if (schemaName == "_")
        {
            schemaName = null;
        }

        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            var table = await connection
                .GetTableAsync(schemaName, tableName, null, cancellationToken)
                .ConfigureAwait(false);
            return table?.CheckConstraints.ToCheckConstraintDtos()
                ?? Enumerable.Empty<CheckConstraintDto>();
        }
    }

    /// <summary>
    /// Gets a specific check constraint from the table.
    /// </summary>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="constraintName">The name of the constraint.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="user">The claims principal for authorization (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The check constraint or null if not found.</returns>
    public async Task<CheckConstraintDto?> GetCheckConstraintAsync(
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        if (schemaName == "_")
        {
            schemaName = null;
        }

        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            var table = await connection
                .GetTableAsync(schemaName, tableName, null, cancellationToken)
                .ConfigureAwait(false);
            var checkConstraint = table?.CheckConstraints.FirstOrDefault(cc =>
                string.Equals(cc.ConstraintName, constraintName, StringComparison.OrdinalIgnoreCase)
            );
            return checkConstraint?.ToCheckConstraintDto();
        }
    }

    /// <summary>
    /// Creates a new check constraint on a table.
    /// </summary>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="request">The request containing check constraint details.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="user">The claims principal for authorization (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created check constraint or null if creation failed.</returns>
    public async Task<CheckConstraintDto?> CreateCheckConstraintAsync(
        string datasourceId,
        string tableName,
        CreateCheckConstraintRequest request,
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

        if (string.IsNullOrWhiteSpace(request.ConstraintName))
        {
            throw new ArgumentException("Constraint name is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.CheckExpression))
        {
            throw new ArgumentException("Check expression is required.", nameof(request));
        }

        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.CreateCheckConstraint,
            DatasourceId = datasourceId,
            SchemaName = schemaName,
            TableName = tableName,
            ConstraintName = request.ConstraintName,
            RequestBody = request,
        };

        // Check permissions
        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to create check constraint '{request.ConstraintName}' on table '{tableName}' in datasource '{datasourceId}'"
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

                var ck = new DmCheckConstraint(
                    schemaName,
                    tableName,
                    request.ColumnName,
                    request.ConstraintName,
                    request.CheckExpression
                );

                var created = await connection
                    .CreateCheckConstraintIfNotExistsAsync(ck, null, cancellationToken)
                    .ConfigureAwait(false);

                var message = created
                    ? "Check constraint created successfully"
                    : "Check constraint already exists";
                await LogAuditEventAsync(context, true, message).ConfigureAwait(false);

                if (created)
                {
                    var createdCheckConstraint = await connection
                        .GetCheckConstraintAsync(
                            schemaName,
                            tableName,
                            request.ConstraintName,
                            null,
                            cancellationToken
                        )
                        .ConfigureAwait(false);

                    return createdCheckConstraint?.ToCheckConstraintDto();
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
    /// Drops a check constraint from a table.
    /// </summary>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="constraintName">The name of the constraint.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="user">The claims principal for authorization (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the check constraint was dropped successfully, false otherwise.</returns>
    public async Task<bool> DropCheckConstraintAsync(
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        if (schemaName == "_")
        {
            schemaName = null;
        }

        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            return await connection
                .DropCheckConstraintIfExistsAsync(
                    schemaName,
                    tableName,
                    constraintName,
                    null,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
    }

    #endregion

    #region Unique Constraints

    /// <summary>
    /// Gets all unique constraints from the specified table.
    /// </summary>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="user">The claims principal for authorization (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of unique constraints.</returns>
    public async Task<IEnumerable<UniqueConstraintDto>> GetUniqueConstraintsAsync(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        if (schemaName == "_")
        {
            schemaName = null;
        }

        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            var table = await connection
                .GetTableAsync(schemaName, tableName, null, cancellationToken)
                .ConfigureAwait(false);
            return table?.UniqueConstraints.ToUniqueConstraintDtos()
                ?? Enumerable.Empty<UniqueConstraintDto>();
        }
    }

    /// <summary>
    /// Gets a specific unique constraint from the table.
    /// </summary>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="constraintName">The name of the constraint.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="user">The claims principal for authorization (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The unique constraint or null if not found.</returns>
    public async Task<UniqueConstraintDto?> GetUniqueConstraintAsync(
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        if (schemaName == "_")
        {
            schemaName = null;
        }

        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            var table = await connection
                .GetTableAsync(schemaName, tableName, null, cancellationToken)
                .ConfigureAwait(false);
            var uniqueConstraint = table?.UniqueConstraints.FirstOrDefault(uc =>
                string.Equals(uc.ConstraintName, constraintName, StringComparison.OrdinalIgnoreCase)
            );
            return uniqueConstraint?.ToUniqueConstraintDto();
        }
    }

    /// <summary>
    /// Creates a new unique constraint on a table.
    /// </summary>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="request">The request containing unique constraint details.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="user">The claims principal for authorization (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created unique constraint or null if creation failed.</returns>
    public async Task<UniqueConstraintDto?> CreateUniqueConstraintAsync(
        string datasourceId,
        string tableName,
        CreateUniqueConstraintRequest request,
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

        if (string.IsNullOrWhiteSpace(request.ConstraintName))
        {
            throw new ArgumentException("Constraint name is required.", nameof(request));
        }

        if (request.Columns == null || request.Columns.Count == 0)
        {
            throw new ArgumentException("At least one column is required.", nameof(request));
        }

        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.CreateUniqueConstraint,
            DatasourceId = datasourceId,
            SchemaName = schemaName,
            TableName = tableName,
            ConstraintName = request.ConstraintName,
            RequestBody = request,
        };

        // Check permissions
        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to create unique constraint '{request.ConstraintName}' on table '{tableName}' in datasource '{datasourceId}'"
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

                var uc = new DmUniqueConstraint(
                    schemaName,
                    tableName,
                    request.ConstraintName,
                    [.. request.Columns.Select(c => DmOrderedColumn.Parse(c))]
                );

                var created = await connection
                    .CreateUniqueConstraintIfNotExistsAsync(uc, null, cancellationToken)
                    .ConfigureAwait(false);

                var message = created
                    ? "Unique constraint created successfully"
                    : "Unique constraint already exists";
                await LogAuditEventAsync(context, true, message).ConfigureAwait(false);

                if (created)
                {
                    var createdUniqueConstraint = await connection
                        .GetUniqueConstraintAsync(
                            schemaName,
                            tableName,
                            request.ConstraintName,
                            null,
                            cancellationToken
                        )
                        .ConfigureAwait(false);

                    return createdUniqueConstraint?.ToUniqueConstraintDto();
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
    /// Drops a unique constraint from a table.
    /// </summary>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="constraintName">The name of the constraint.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="user">The claims principal for authorization (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the unique constraint was dropped successfully, false otherwise.</returns>
    public async Task<bool> DropUniqueConstraintAsync(
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        if (schemaName == "_")
        {
            schemaName = null;
        }

        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            return await connection
                .DropUniqueConstraintIfExistsAsync(
                    schemaName,
                    tableName,
                    constraintName,
                    null,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
    }

    #endregion

    #region Default Constraints

    /// <summary>
    /// Gets all default constraints from the specified table.
    /// </summary>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="user">The claims principal for authorization (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of default constraints.</returns>
    public async Task<IEnumerable<DefaultConstraintDto>> GetDefaultConstraintsAsync(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        if (schemaName == "_")
        {
            schemaName = null;
        }

        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            var table = await connection
                .GetTableAsync(schemaName, tableName, null, cancellationToken)
                .ConfigureAwait(false);
            return table?.DefaultConstraints.ToDefaultConstraintDtos()
                ?? Enumerable.Empty<DefaultConstraintDto>();
        }
    }

    /// <summary>
    /// Gets a specific default constraint from the table.
    /// </summary>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="constraintName">The name of the constraint.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="user">The claims principal for authorization (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The default constraint or null if not found.</returns>
    public async Task<DefaultConstraintDto?> GetDefaultConstraintAsync(
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        if (schemaName == "_")
        {
            schemaName = null;
        }

        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            var table = await connection
                .GetTableAsync(schemaName, tableName, null, cancellationToken)
                .ConfigureAwait(false);
            var defaultConstraint = table?.DefaultConstraints.FirstOrDefault(dc =>
                string.Equals(dc.ConstraintName, constraintName, StringComparison.OrdinalIgnoreCase)
            );
            return defaultConstraint?.ToDefaultConstraintDto();
        }
    }

    /// <summary>
    /// Creates a new default constraint on a table.
    /// </summary>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="request">The request containing default constraint details.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="user">The claims principal for authorization (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created default constraint or null if creation failed.</returns>
    public async Task<DefaultConstraintDto?> CreateDefaultConstraintAsync(
        string datasourceId,
        string tableName,
        CreateDefaultConstraintRequest request,
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

        if (string.IsNullOrWhiteSpace(request.ConstraintName))
        {
            throw new ArgumentException("Constraint name is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.DefaultExpression))
        {
            throw new ArgumentException("Default expression is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ColumnName))
        {
            throw new ArgumentException("Column name is required.", nameof(request));
        }

        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.CreateDefaultConstraint,
            DatasourceId = datasourceId,
            SchemaName = schemaName,
            TableName = tableName,
            ConstraintName = request.ConstraintName,
            RequestBody = request,
        };

        // Check permissions
        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to create default constraint '{request.ConstraintName}' on table '{tableName}' in datasource '{datasourceId}'"
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

                var dfc = new DmDefaultConstraint(
                    schemaName,
                    tableName,
                    request.ColumnName,
                    request.ConstraintName,
                    request.DefaultExpression
                );

                var created = await connection
                    .CreateDefaultConstraintIfNotExistsAsync(dfc, null, cancellationToken)
                    .ConfigureAwait(false);

                var message = created
                    ? "Default constraint created successfully"
                    : "Default constraint already exists";
                await LogAuditEventAsync(context, true, message).ConfigureAwait(false);

                if (created)
                {
                    var createdDefaultConstraint = await connection
                        .GetDefaultConstraintAsync(
                            schemaName,
                            tableName,
                            request.ConstraintName,
                            null,
                            cancellationToken
                        )
                        .ConfigureAwait(false);

                    return createdDefaultConstraint?.ToDefaultConstraintDto();
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
    /// Drops a default constraint from a table.
    /// </summary>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="constraintName">The name of the constraint.</param>
    /// <param name="schemaName">The schema name (optional).</param>
    /// <param name="user">The claims principal for authorization (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the default constraint was dropped successfully, false otherwise.</returns>
    public async Task<bool> DropDefaultConstraintAsync(
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        if (schemaName == "_")
        {
            schemaName = null;
        }

        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            return await connection
                .DropDefaultConstraintIfExistsAsync(
                    schemaName,
                    tableName,
                    constraintName,
                    null,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
    }

    #endregion
}
