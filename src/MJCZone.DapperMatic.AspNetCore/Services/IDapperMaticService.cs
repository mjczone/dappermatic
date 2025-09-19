// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Security.Claims;

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Models.Requests;
using MJCZone.DapperMatic.AspNetCore.Models.Responses;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.AspNetCore.Services;

/// <summary>
/// Service interface for DapperMatic operations in ASP.NET Core applications.
/// </summary>
public interface IDapperMaticService
{
    /// <summary>
    /// Gets all registered datasources.
    /// </summary>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of datasource information.</returns>
    Task<IEnumerable<DatasourceDto>> GetDatasourcesAsync(
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets datasource information by name.
    /// </summary>
    /// <param name="datasourceId">The id of the datasource.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The datasource information if found, otherwise null.</returns>
    Task<DatasourceDto?> GetDatasourceAsync(
        string datasourceId,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Adds a new datasource.
    /// </summary>
    /// <param name="datasource">The datasource to add.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The added datasource if successful, otherwise null if a datasource with the same id already exists.</returns>
    Task<DatasourceDto?> AddDatasourceAsync(
        DatasourceDto datasource,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Updates an existing datasource.
    /// </summary>
    /// <param name="datasource">The updated datasource information.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated datasource if successful, otherwise null if not found.</returns>
    Task<DatasourceDto?> UpdateDatasourceAsync(
        DatasourceDto datasource,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Removes a datasource.
    /// </summary>
    /// <param name="datasourceId">The id of the datasource to remove.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the datasource was removed, false if not found.</returns>
    Task<bool> RemoveDatasourceAsync(
        string datasourceId,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Checks if a datasource exists.
    /// </summary>
    /// <param name="datasourceId">The id of the datasource to check.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the datasource exists, false otherwise.</returns>
    Task<bool> DatasourceExistsAsync(
        string datasourceId,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Tests the connection to a datasource.
    /// </summary>
    /// <param name="datasourceId">The id of the datasource to test.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Test result containing connection status and details.</returns>
    Task<Models.Responses.DatasourceTestResult> TestDatasourceAsync(
        string datasourceId,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets all schemas from the specified datasource.
    /// </summary>
    /// <param name="datasourceId">The id of the datasource.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of schemas.</returns>
    Task<IEnumerable<SchemaDto>> GetSchemasAsync(
        string datasourceId,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets a specific schema from the specified datasource.
    /// </summary>
    /// <param name="datasourceId">The id of the datasource.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The schema if found, otherwise null.</returns>
    Task<SchemaDto?> GetSchemaAsync(
        string datasourceId,
        string schemaName,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Creates a new schema in the specified datasource.
    /// </summary>
    /// <param name="datasourceId">The id of the datasource.</param>
    /// <param name="schema">The schema to create.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created schema if successful, otherwise null if it already existed.</returns>
    Task<SchemaDto?> CreateSchemaAsync(
        string datasourceId,
        SchemaDto schema,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Drops a schema from the specified datasource.
    /// </summary>
    /// <param name="datasourceId">The id of the datasource.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the schema was dropped, false if it didn't exist.</returns>
    Task<bool> DropSchemaAsync(
        string datasourceId,
        string schemaName,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Checks if a schema exists in the specified datasource.
    /// </summary>
    /// <param name="datasourceId">The id of the datasource.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the schema exists, false otherwise.</returns>
    Task<bool> SchemaExistsAsync(
        string datasourceId,
        string schemaName,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets all views from the specified datasource.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="schemaName">Optional schema name filter.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of views.</returns>
    Task<IEnumerable<DmView>> GetViewsAsync(
        string datasourceId,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets a specific view from the datasource.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="viewName">The view name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The view if found, otherwise null.</returns>
    Task<DmView?> GetViewAsync(
        string datasourceId,
        string viewName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Creates a new view in the datasource.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="view">The view to create.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created view if successful, otherwise null.</returns>
    Task<DmView?> CreateViewAsync(
        string datasourceId,
        DmView view,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Updates an existing view in the datasource.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="viewName">The name of the view to update.</param>
    /// <param name="newViewName">The new name for the view (optional).</param>
    /// <param name="newViewDefinition">The new view definition (optional).</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated view if successful, otherwise null.</returns>
    Task<DmView?> UpdateViewAsync(
        string datasourceId,
        string viewName,
        string? newViewName,
        string? newViewDefinition,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Drops a view from the datasource.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="viewName">The name of the view to drop.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the view was dropped, false if it didn't exist.</returns>
    Task<bool> DropViewAsync(
        string datasourceId,
        string viewName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Checks if a view exists in the datasource.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="viewName">The view name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the view exists, otherwise false.</returns>
    Task<bool> ViewExistsAsync(
        string datasourceId,
        string viewName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Queries a view with filtering, sorting, and pagination.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="viewName">The view name to query.</param>
    /// <param name="request">The query parameters.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The query results with pagination information.</returns>
    Task<QueryResultDto> QueryViewAsync(
        string datasourceId,
        string viewName,
        QueryRequest request,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets all available data types for a specific datasource.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="includeCustomTypes">If true, discovers custom types from the database in addition to static types.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of data types available in the datasource, including provider-specific types, extensions, and custom types.</returns>
    Task<(string providerName, List<DataTypeInfo> dataTypes)> GetDatasourceDataTypesAsync(
        string datasourceId,
        ClaimsPrincipal? user = null,
        bool includeCustomTypes = false,
        CancellationToken cancellationToken = default
    );

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
    Task<IEnumerable<TableDto>> GetTablesAsync(
        string datasourceId,
        string? schemaName = null,
        bool includeColumns = false,
        bool includeIndexes = false,
        bool includeConstraints = false,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

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
    Task<TableDto?> GetTableAsync(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        bool includeColumns = true,
        bool includeIndexes = true,
        bool includeConstraints = true,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Creates a new table in the datasource.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="request">The table creation request.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created table if successful, otherwise null.</returns>
    Task<TableDto?> CreateTableAsync(
        string datasourceId,
        CreateTableRequest request,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Drops a table from the datasource.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The name of the table to drop.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the table was dropped, false if it didn't exist.</returns>
    Task<bool> DropTableAsync(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Checks if a table exists in the datasource.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the table exists, otherwise false.</returns>
    Task<bool> TableExistsAsync(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

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
    Task<QueryResultDto> QueryTableAsync(
        string datasourceId,
        string tableName,
        QueryRequest request,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

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
    Task<bool> RenameTableAsync(
        string datasourceId,
        string tableName,
        string newTableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    // Column management methods

    /// <summary>
    /// Gets all columns for a specific table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>List of columns for the table.</returns>
    Task<IEnumerable<ColumnDto>> GetColumnsAsync(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets a specific column from a table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The column if found, otherwise null.</returns>
    Task<ColumnDto?> GetColumnAsync(
        string datasourceId,
        string tableName,
        string columnName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Adds a new column to an existing table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="request">The add column request.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The added column if successful, otherwise null.</returns>
    Task<ColumnDto?> AddColumnAsync(
        string datasourceId,
        string tableName,
        CreateTableColumnRequest request,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

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
    /// <returns>The updated column if successful, otherwise null.</returns>
    Task<ColumnDto?> RenameColumnAsync(
        string datasourceId,
        string tableName,
        string columnName,
        string newColumnName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Drops a column from a table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name to drop.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the column was dropped successfully, false if it didn't exist.</returns>
    Task<bool> DropColumnAsync(
        string datasourceId,
        string tableName,
        string columnName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    // Index management methods

    /// <summary>
    /// Gets all indexes for a specific table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>List of indexes for the table.</returns>
    Task<IEnumerable<IndexDto>> GetIndexesAsync(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets a specific index from a table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="indexName">The index name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The index if found, otherwise null.</returns>
    Task<IndexDto?> GetIndexAsync(
        string datasourceId,
        string tableName,
        string indexName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Creates a new index on a table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="request">The create index request.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created index if successful, otherwise null.</returns>
    Task<IndexDto?> CreateIndexAsync(
        string datasourceId,
        string tableName,
        CreateIndexRequest request,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Drops an index from a table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="indexName">The index name to drop.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the index was dropped successfully, false if it didn't exist.</returns>
    Task<bool> DropIndexAsync(
        string datasourceId,
        string tableName,
        string indexName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    // Constraint management methods

    /// <summary>
    /// Gets the primary key constraint for a table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The primary key constraint if found, otherwise null.</returns>
    Task<PrimaryKeyConstraintDto?> GetPrimaryKeyAsync(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Creates a primary key constraint on a table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="request">The create primary key request.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created primary key constraint if successful, otherwise null.</returns>
    Task<PrimaryKeyConstraintDto?> CreatePrimaryKeyAsync(
        string datasourceId,
        string tableName,
        CreatePrimaryKeyRequest request,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Drops the primary key constraint from a table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the primary key was dropped successfully, false if it didn't exist.</returns>
    Task<bool> DropPrimaryKeyAsync(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets all foreign key constraints for a table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>List of foreign key constraints for the table.</returns>
    Task<IEnumerable<ForeignKeyConstraintDto>> GetForeignKeysAsync(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets a specific foreign key constraint from a table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The foreign key constraint if found, otherwise null.</returns>
    Task<ForeignKeyConstraintDto?> GetForeignKeyAsync(
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Creates a foreign key constraint on a table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="request">The create foreign key request.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created foreign key constraint if successful, otherwise null.</returns>
    Task<ForeignKeyConstraintDto?> CreateForeignKeyAsync(
        string datasourceId,
        string tableName,
        CreateForeignKeyRequest request,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Drops a foreign key constraint from a table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name to drop.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the foreign key was dropped successfully, false if it didn't exist.</returns>
    Task<bool> DropForeignKeyAsync(
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets all check constraints for a table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>List of check constraints for the table.</returns>
    Task<IEnumerable<CheckConstraintDto>> GetCheckConstraintsAsync(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets a specific check constraint from a table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The check constraint if found, otherwise null.</returns>
    Task<CheckConstraintDto?> GetCheckConstraintAsync(
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Creates a check constraint on a table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="request">The create check constraint request.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created check constraint if successful, otherwise null.</returns>
    Task<CheckConstraintDto?> CreateCheckConstraintAsync(
        string datasourceId,
        string tableName,
        CreateCheckConstraintRequest request,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Drops a check constraint from a table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name to drop.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the check constraint was dropped successfully, false if it didn't exist.</returns>
    Task<bool> DropCheckConstraintAsync(
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets all unique constraints for a table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>List of unique constraints for the table.</returns>
    Task<IEnumerable<UniqueConstraintDto>> GetUniqueConstraintsAsync(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets a specific unique constraint from a table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The unique constraint if found, otherwise null.</returns>
    Task<UniqueConstraintDto?> GetUniqueConstraintAsync(
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Creates a unique constraint on a table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="request">The create unique constraint request.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created unique constraint if successful, otherwise null.</returns>
    Task<UniqueConstraintDto?> CreateUniqueConstraintAsync(
        string datasourceId,
        string tableName,
        CreateUniqueConstraintRequest request,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Drops a unique constraint from a table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name to drop.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the unique constraint was dropped successfully, false if it didn't exist.</returns>
    Task<bool> DropUniqueConstraintAsync(
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets all default constraints for a table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>List of default constraints for the table.</returns>
    Task<IEnumerable<DefaultConstraintDto>> GetDefaultConstraintsAsync(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets a specific default constraint from a table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The default constraint if found, otherwise null.</returns>
    Task<DefaultConstraintDto?> GetDefaultConstraintAsync(
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Creates a default constraint on a table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="request">The create default constraint request.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created default constraint if successful, otherwise null.</returns>
    Task<DefaultConstraintDto?> CreateDefaultConstraintAsync(
        string datasourceId,
        string tableName,
        CreateDefaultConstraintRequest request,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Drops a default constraint from a table.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name to drop.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">The user making the request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the default constraint was dropped successfully, false if it didn't exist.</returns>
    Task<bool> DropDefaultConstraintAsync(
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    );
}
