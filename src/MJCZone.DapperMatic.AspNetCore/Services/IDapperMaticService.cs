// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Validation;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.AspNetCore.Services;

/// <summary>
/// Service interface for DapperMatic operations in ASP.NET Core applications.
/// </summary>
/// <remarks>
/// This interface defines the contract for DapperMatic services, including methods for managing
/// datasources and executing queries. DapperMatic services ONLY interact with DTOs and do not
/// expose any internal models or entities. This ensures a clear separation of concerns and
/// maintains the integrity of the service layer.
/// </remarks>
public interface IDapperMaticService
{
    #region Datasource Methods

    /// <summary>
    /// Gets all registered datasources.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of datasource information.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<IEnumerable<DatasourceDto>> GetDatasourcesAsync(
        DapperMaticOperation context,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets datasource information by name.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The id of the datasource.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The datasource information.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the datasource is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<DatasourceDto> GetDatasourceAsync(
        DapperMaticOperation context,
        string datasourceId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Adds a new datasource.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasource">The datasource to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The added datasource if successful.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<DatasourceDto> AddDatasourceAsync(
        DapperMaticOperation context,
        DatasourceDto datasource,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Updates an existing datasource.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasource">The updated datasource information.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated datasource.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the datasource is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<DatasourceDto> UpdateDatasourceAsync(
        DapperMaticOperation context,
        DatasourceDto datasource,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Removes a datasource.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The id of the datasource to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the datasource is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task RemoveDatasourceAsync(
        DapperMaticOperation context,
        string datasourceId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Checks if a datasource exists.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The id of the datasource to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the datasource exists, false otherwise.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<bool> DatasourceExistsAsync(
        DapperMaticOperation context,
        string datasourceId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Tests the connection to a datasource.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The id of the datasource to test.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Test result containing connection status and details.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the datasource is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<DatasourceConnectivityTestDto> TestDatasourceAsync(
        DapperMaticOperation context,
        string datasourceId,
        CancellationToken cancellationToken = default
    );

    #endregion // Datasource Methods

    #region Schema Methods

    /// <summary>
    /// Gets all schemas from the specified datasource.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The id of the datasource.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of schemas.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<IEnumerable<SchemaDto>> GetSchemasAsync(
        DapperMaticOperation context,
        string datasourceId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets a specific schema from the specified datasource.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The id of the datasource.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The schema if found.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the schema is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<SchemaDto> GetSchemaAsync(
        DapperMaticOperation context,
        string datasourceId,
        string schemaName,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Creates a new schema in the specified datasource.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The id of the datasource.</param>
    /// <param name="schema">The schema to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created schema.</returns>
    /// <exception cref="DuplicateKeyException">Thrown when the schema already exists.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<SchemaDto> CreateSchemaAsync(
        DapperMaticOperation context,
        string datasourceId,
        SchemaDto schema,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Drops a schema from the specified datasource.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The id of the datasource.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the schema is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task DropSchemaAsync(
        DapperMaticOperation context,
        string datasourceId,
        string schemaName,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Checks if a schema exists in the specified datasource.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The id of the datasource.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the schema exists, false otherwise.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<bool> SchemaExistsAsync(
        DapperMaticOperation context,
        string datasourceId,
        string schemaName,
        CancellationToken cancellationToken = default
    );

    #endregion // Schema Methods

    #region View Methods

    /// <summary>
    /// Gets all views from the specified datasource.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="schemaName">Optional schema name filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of views.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<IEnumerable<ViewDto>> GetViewsAsync(
        DapperMaticOperation context,
        string datasourceId,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets a specific view from the datasource.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="viewName">The view name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The view.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the view is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<ViewDto> GetViewAsync(
        DapperMaticOperation context,
        string datasourceId,
        string viewName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Creates a new view in the datasource.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="view">The view data transfer object containing the view information.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created view.</returns>
    /// <exception cref="DuplicateKeyException">Thrown when the view already exists.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<ViewDto> CreateViewAsync(
        DapperMaticOperation context,
        string datasourceId,
        ViewDto view,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Updates an existing view's properties in the datasource.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="viewName">The name of the view to update.</param>
    /// <param name="updates">The view updates (only non-null properties will be updated).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated view.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the view is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<ViewDto> UpdateViewAsync(
        DapperMaticOperation context,
        string datasourceId,
        string viewName,
        ViewDto updates,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Renames an existing view in the datasource.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="currentViewName">The current name of the view.</param>
    /// <param name="newViewName">The new name for the view.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The renamed view.</returns>
    /// <exception cref="DuplicateKeyException">Thrown when the new view name already exists.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the view is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<ViewDto> RenameViewAsync(
        DapperMaticOperation context,
        string datasourceId,
        string currentViewName,
        string newViewName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Drops a view from the datasource.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="viewName">The name of the view to drop.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the view is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task DropViewAsync(
        DapperMaticOperation context,
        string datasourceId,
        string viewName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Checks if a view exists in the datasource.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="viewName">The view name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the view exists, otherwise false.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<bool> ViewExistsAsync(
        DapperMaticOperation context,
        string datasourceId,
        string viewName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Queries a view with filtering, sorting, and pagination.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="viewName">The view name to query.</param>
    /// <param name="request">The query parameters.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The query results with pagination information.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the view is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<QueryResultDto> QueryViewAsync(
        DapperMaticOperation context,
        string datasourceId,
        string viewName,
        QueryDto request,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    #endregion // View Methods

    #region DataType Methods

    /// <summary>
    /// Gets all available data types for a specific datasource.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="includeCustomTypes">If true, discovers custom types from the database in addition to static types.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of data types available in the datasource, including provider-specific types, extensions, and custom types.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<(string providerName, List<DataTypeInfo> dataTypes)> GetDatasourceDataTypesAsync(
        DapperMaticOperation context,
        string datasourceId,
        bool includeCustomTypes = false,
        CancellationToken cancellationToken = default
    );

    #endregion // DataType Methods

    #region Table Methods

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
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<IEnumerable<TableDto>> GetTablesAsync(
        DapperMaticOperation context,
        string datasourceId,
        string? schemaName = null,
        bool includeColumns = false,
        bool includeIndexes = false,
        bool includeConstraints = false,
        CancellationToken cancellationToken = default
    );

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
    /// <exception cref="KeyNotFoundException">Thrown when the table is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<TableDto> GetTableAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        string? schemaName = null,
        bool includeColumns = true,
        bool includeIndexes = true,
        bool includeConstraints = true,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Creates a new table in the datasource.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="table">The table information.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created table.</returns>
    /// <exception cref="DuplicateKeyException">Thrown when the table already exists.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<TableDto> CreateTableAsync(
        DapperMaticOperation context,
        string datasourceId,
        TableDto table,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Drops a table from the datasource.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The name of the table to drop.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the table is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task DropTableAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Checks if a table exists in the datasource.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the table exists, otherwise false.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<bool> TableExistsAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Inserts rows into a table, all or none, each value converted to its column's type.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table.</param>
    /// <param name="request">The rows.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows inserted.</returns>
    /// <exception cref="ArgumentException">A row names a column the table does not have, or a value not of its type.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<int> InsertTableRowsAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        RowsInsertDto request,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    ) => throw new NotSupportedException($"{GetType().Name} does not write rows.");

    /// <summary>
    /// Sets columns of the rows of a table that match a condition.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table.</param>
    /// <param name="request">The columns to set and the condition.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows updated.</returns>
    /// <exception cref="ArgumentException">The request names a column the table does not have, or gives no condition.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<int> UpdateTableRowsAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        RowsUpdateDto request,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    ) => throw new NotSupportedException($"{GetType().Name} does not write rows.");

    /// <summary>
    /// Deletes the rows of a table that match a condition.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table.</param>
    /// <param name="request">The condition.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows deleted.</returns>
    /// <exception cref="ArgumentException">The request gives no condition, or names a column the table does not have.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<int> DeleteTableRowsAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        RowsDeleteDto request,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    ) => throw new NotSupportedException($"{GetType().Name} does not write rows.");

    /// <summary>
    /// Writes rows of a table with the rows they relate to, through declared relations, all or none: a related row
    /// whose key is given is updated if it exists and inserted if not; one without a key is inserted; and, with
    /// <paramref name="upsert"/>, the rows themselves too. Every row is held to its table's policies.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource.</param>
    /// <param name="tableName">The table.</param>
    /// <param name="request">The rows, related rows nested under their relations' names.</param>
    /// <param name="upsert">Whether the rows themselves are updated by their key when they exist, or inserted.</param>
    /// <param name="schemaName">The schema, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows written, and the rows as written.</returns>
    Task<Models.Responses.RowsResponse> SaveTableRowsAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        RowsInsertDto request,
        bool upsert,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    ) => throw new NotSupportedException($"{GetType().Name} does not write rows.");

    /// <summary>
    /// Runs writes of rows of a datasource's tables in order, in one transaction, all or none: each authorized and
    /// held to its table's policies as the row endpoints are, a later one able to use values an earlier one wrote.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource.</param>
    /// <param name="request">The operations.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Each operation's result.</returns>
    /// <exception cref="ArgumentException">An operation is malformed, or refers to a value no earlier one wrote.</exception>
    /// <exception cref="UnauthorizedAccessException">An operation is not allowed: nothing is written.</exception>
    Task<Models.Responses.BatchResponse> BatchAsync(
        DapperMaticOperation context,
        string datasourceId,
        BatchDto request,
        CancellationToken cancellationToken = default
    ) => throw new NotSupportedException($"{GetType().Name} does not write rows.");

    /// <summary>
    /// Describes a datasource's rows as the caller may use them, in an OpenAPI 3.1 document: the tables and views it
    /// may read or write, only the actions its policies allow on each, and only the columns it may read or write.
    /// </summary>
    /// <param name="context">The operation context, whose caller the document is for.</param>
    /// <param name="datasourceId">The datasource.</param>
    /// <param name="schemaName">The schema, or null for the default or the tenant's.</param>
    /// <param name="serverUrl">The URL the document's paths are relative to.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The document.</returns>
    Task<System.Text.Json.Nodes.JsonObject> GetOpenApiDocumentAsync(
        DapperMaticOperation context,
        string datasourceId,
        string? schemaName,
        string serverUrl,
        CancellationToken cancellationToken = default
    ) => throw new NotSupportedException($"{GetType().Name} does not describe rows.");

    /// <summary>
    /// Runs a function the application declares in <see cref="DapperMaticOptions.Functions"/>, as policies granting
    /// <see cref="MJCZone.DapperMatic.Security.DmAction.Execute"/> allow: its arguments read from a JSON object by parameter name, those
    /// it takes from the request's context set whatever the request says.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource.</param>
    /// <param name="functionName">The function.</param>
    /// <param name="arguments">The arguments, a JSON object; or null for none.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rows it returned.</returns>
    /// <exception cref="KeyNotFoundException">No such function is declared for the datasource.</exception>
    /// <exception cref="ArgumentException">An argument is unknown, missing, or not of its parameter's type.</exception>
    Task<Models.Responses.FunctionResponse> ExecuteFunctionAsync(
        DapperMaticOperation context,
        string datasourceId,
        string functionName,
        System.Text.Json.JsonElement? arguments,
        CancellationToken cancellationToken = default
    ) => throw new NotSupportedException($"{GetType().Name} does not run functions.");

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
    /// <exception cref="KeyNotFoundException">Thrown when the table is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<QueryResultDto> QueryTableAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        QueryDto request,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Updates an existing table in the datasource.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The name of the table to update.</param>
    /// <param name="updates">The table updates.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated table.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the table is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<TableDto> UpdateTableAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        TableDto updates,
        CancellationToken cancellationToken = default
    );

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
    /// <exception cref="DuplicateKeyException">Thrown when the new table name already exists.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the table is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<TableDto> RenameTableAsync(
        DapperMaticOperation context,
        string datasourceId,
        string currentTableName,
        string newTableName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    #endregion // Table Methods

    #region Column Methods

    // Column management methods

    /// <summary>
    /// Gets all columns for a specific table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>List of columns for the table.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the table is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<IEnumerable<ColumnDto>> GetColumnsAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets a specific column from a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The column if found.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the table or column is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<ColumnDto> GetColumnAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        string columnName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Adds a new column to an existing table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="column">The add column request.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The added column.</returns>
    /// <exception cref="DuplicateKeyException">Thrown when the column already exists.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the table is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<ColumnDto> AddColumnAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        ColumnDto column,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Updates an existing column in a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name to update.</param>
    /// <param name="newColumnName">The new column name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated column.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the table or column is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<ColumnDto> RenameColumnAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        string columnName,
        string newColumnName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Drops a column from a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name to drop.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the table or column is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task DropColumnAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        string columnName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    #endregion // Column Methods

    #region Index Methods

    // Index management methods

    /// <summary>
    /// Gets all indexes for a specific table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>List of indexes for the table.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the table is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<IEnumerable<IndexDto>> GetIndexesAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets a specific index from a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="indexName">The index name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The index if found.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the table or index is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<IndexDto> GetIndexAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        string indexName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Creates a new index on a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="index">The create index request.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created index.</returns>
    /// <exception cref="DuplicateKeyException">Thrown when the index already exists.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the table is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<IndexDto> CreateIndexAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        IndexDto index,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Drops an index from a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="indexName">The index name to drop.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the table or index is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task DropIndexAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        string indexName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    #endregion // Index Methods

    #region PrimaryKeyConstraint Methods

    /// <summary>
    /// Gets the primary key constraint for a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The primary key constraint or null if the table does not have a primary key.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the table is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<PrimaryKeyConstraintDto?> GetPrimaryKeyConstraintAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Creates a primary key constraint on a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="primaryKeyConstraint">The create primary key request.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created primary key constraint.</returns>
    /// <exception cref="DuplicateKeyException">Thrown when a primary key already exists.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the table is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<PrimaryKeyConstraintDto> CreatePrimaryKeyConstraintAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        PrimaryKeyConstraintDto primaryKeyConstraint,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Drops the primary key constraint from a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the table is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task DropPrimaryKeyConstraintAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    #endregion // PrimaryKeyConstraint Methods

    #region ForeignKeyConstraint Methods

    /// <summary>
    /// Gets all foreign key constraints for a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>List of foreign key constraints for the table.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the table is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<IEnumerable<ForeignKeyConstraintDto>> GetForeignKeyConstraintsAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets a specific foreign key constraint from a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The foreign key constraint if found.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the table or foreign key is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<ForeignKeyConstraintDto> GetForeignKeyConstraintAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Creates a foreign key constraint on a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="foreignKeyConstraint">The create foreign key request.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created foreign key constraint.</returns>
    /// <exception cref="DuplicateKeyException">Thrown when the foreign key already exists.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the table is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<ForeignKeyConstraintDto> CreateForeignKeyConstraintAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        ForeignKeyConstraintDto foreignKeyConstraint,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Drops a foreign key constraint from a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name to drop.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the table or foreign key is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task DropForeignKeyConstraintAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    #endregion // ForeignKeyConstraint Methods

    #region CheckConstraint Methods

    /// <summary>
    /// Gets all check constraints for a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>List of check constraints for the table.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the table is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<IEnumerable<CheckConstraintDto>> GetCheckConstraintsAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets a specific check constraint from a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The check constraint if found.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the table or constraint is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<CheckConstraintDto> GetCheckConstraintAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Creates a check constraint on a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="checkConstraint">The create check constraint request.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created check constraint.</returns>
    /// <exception cref="DuplicateKeyException">Thrown when the check constraint already exists.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the table is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<CheckConstraintDto> CreateCheckConstraintAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        CheckConstraintDto checkConstraint,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Drops a check constraint from a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name to drop.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the table or constraint is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task DropCheckConstraintAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    #endregion // CheckConstraint Methods

    #region UniqueConstraint Methods

    /// <summary>
    /// Gets all unique constraints for a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>List of unique constraints for the table.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the table is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<IEnumerable<UniqueConstraintDto>> GetUniqueConstraintsAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets a specific unique constraint from a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The unique constraint if found.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the table or constraint is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<UniqueConstraintDto> GetUniqueConstraintAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Creates a unique constraint on a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="uniqueConstraint">The create unique constraint request.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created unique constraint.</returns>
    /// <exception cref="DuplicateKeyException">Thrown when the unique constraint already exists.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the table is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<UniqueConstraintDto> CreateUniqueConstraintAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        UniqueConstraintDto uniqueConstraint,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Drops a unique constraint from a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name to drop.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the table or constraint is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task DropUniqueConstraintAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    #endregion // UniqueConstraint Methods

    #region DefaultConstraint Methods

    /// <summary>
    /// Gets all default constraints for a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>List of default constraints for the table.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the table is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<IEnumerable<DefaultConstraintDto>> GetDefaultConstraintsAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets a specific default constraint from a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The default constraint if found, otherwise null.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the table or constraint is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<DefaultConstraintDto> GetDefaultConstraintAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets a specific default constraint from a table for a column.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The default constraint if found, otherwise null.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the table or constraint is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<DefaultConstraintDto> GetDefaultConstraintOnColumnAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        string columnName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Creates a default constraint on a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="defaultConstraint">The create default constraint request.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created default constraint if successful, otherwise null.</returns>
    /// <exception cref="DuplicateKeyException">Thrown when the default constraint already exists.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the table is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task<DefaultConstraintDto> CreateDefaultConstraintAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        DefaultConstraintDto defaultConstraint,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Drops a default constraint from a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name to drop.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the table or constraint is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task DropDefaultConstraintAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Drops a column default constraint from a table.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name to drop the constraint from.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the table or constraint is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    Task DropDefaultConstraintOnColumnAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        string columnName,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    );

    #endregion // DefaultConstraint Methods
}
