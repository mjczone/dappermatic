// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Runtime.CompilerServices;
using System.Text;
using Dapper;
using MJCZone.DapperMatic.AspNetCore.Auditing;
using MJCZone.DapperMatic.AspNetCore.Factories;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Repositories;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.AspNetCore.Validation;

namespace MJCZone.DapperMatic.AspNetCore.Services;

/// <summary>
/// Implementation of IDapperMaticService for ASP.NET Core applications.
/// </summary>
public partial class DapperMaticService : IDapperMaticService
{
    private readonly IDapperMaticDatasourceRepository _datasourceRepository;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IDapperMaticPermissions _permissions;
    private readonly IDapperMaticAuditLogger _auditLogger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticService"/> class.
    /// </summary>
    /// <param name="datasourceRepository">The datasource repository.</param>
    /// <param name="connectionFactory">The connection factory.</param>
    /// <param name="permissions">The permissions manager.</param>
    /// <param name="auditLogger">The audit logger.</param>
    public DapperMaticService(
        IDapperMaticDatasourceRepository datasourceRepository,
        IDbConnectionFactory connectionFactory,
        IDapperMaticPermissions permissions,
        IDapperMaticAuditLogger auditLogger
    )
    {
        _datasourceRepository = datasourceRepository;
        _connectionFactory = connectionFactory;
        _permissions = permissions;
        _auditLogger = auditLogger;
    }

    #region Shared Protected Methods

    /// <summary>
    /// Asserts that the operation context has the necessary permissions.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <param name="methodName">The calling method name (automatically provided).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    protected virtual async Task AssertPermissionsAsync(
        IOperationContext context,
        [CallerMemberName] string methodName = ""
    )
    {
        // Only validate what's REQUIRED for authorization
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        // Always track the current method for rich context
        context.Properties ??= [];
        context.Properties["CurrentMethod"] = methodName;

        // Someone could enhance this with a cache of some kind if performance is a concern,
        // checking for example if we've already authorized this operation
        // Provided as an example if someone wants to override this method and implement caching,
        // see commented code below where cache is populated
        //
        // const string authCacheKey = "_AuthorizationCache";
        // if (context.Properties.TryGetValue(authCacheKey, out var cache))
        // {
        //     var authCache = (HashSet<string>)cache;
        //     if (authCache.Contains(context.Operation!))
        //     {
        //         return; // Already authorized for this operation
        //     }
        // }

        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            var msg = new StringBuilder();
            msg.Append($"Access denied for operation '{context.Operation}'");
            if (!string.IsNullOrWhiteSpace(context.DatasourceId))
            {
                msg.Append($" on datasource '{context.DatasourceId}'");
                if (!string.IsNullOrWhiteSpace(context.SchemaName))
                {
                    msg.Append($", '{context.SchemaName}'");
                }
                if (!string.IsNullOrWhiteSpace(context.TableName))
                {
                    msg.Append($", table '{context.TableName}'");
                }
                if (!string.IsNullOrWhiteSpace(context.ViewName))
                {
                    msg.Append($", view '{context.ViewName}'");
                }
                if (!string.IsNullOrWhiteSpace(context.ColumnName))
                {
                    msg.Append($", column '{context.ColumnName}'");
                }
            }
            msg.Append('.');
            throw new UnauthorizedAccessException(msg.ToString());
        }

        // Cache the result (provided as an example if someone wants to override this method and implement caching)
        //
        // if (!context.Properties.TryGetValue(authCacheKey, out object? value))
        // {
        //     value = new HashSet<string>();
        //     context.Properties[authCacheKey] = value;
        // }
        // ((HashSet<string>)value).Add(context.Operation!);
    }

    /// <summary>
    /// Creates and opens a database connection for the specified datasource.
    /// </summary>
    /// <param name="datasourceId">The ID of the datasource.</param>
    /// <returns>An open database connection.</returns>
    /// <exception cref="ArgumentException">Thrown when the datasource is not found.</exception>
    protected virtual async Task<IDbConnection> CreateConnectionForDatasource(string datasourceId)
    {
        var datasource =
            await _datasourceRepository.GetDatasourceAsync(datasourceId).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Datasource '{datasourceId}' not found.");

        if (string.IsNullOrWhiteSpace(datasource.Provider))
        {
            throw new InvalidOperationException(
                $"Datasource '{datasourceId}' is missing a provider."
            );
        }

        var connectionString = await _datasourceRepository
            .GetConnectionStringAsync(datasourceId)
            .ConfigureAwait(false);

        if (connectionString == null)
        {
            throw new InvalidOperationException(
                $"Connection string for datasource '{datasourceId}' is not available."
            );
        }

        return _connectionFactory.CreateConnection(datasource.Provider, connectionString);
    }

    /// <summary>
    /// Logs an audit event for the specified operation context.
    /// </summary>
    /// <param name="operation">The operation context.</param>
    /// <param name="success">Indicates if the operation was successful.</param>
    /// <param name="errorMessage">Optional error message if the operation failed.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected virtual async Task LogAuditEventAsync(
        IOperationContext operation,
        bool success,
        string? errorMessage = null
    )
    {
        var auditEvent = operation.ToAuditEvent(success, errorMessage);
        await _auditLogger.LogOperationAsync(auditEvent).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes a data query with common logic for filtering, sorting, and pagination.
    /// </summary>
    /// <param name="connection">The database connection.</param>
    /// <param name="schemaQualifiedTableOrViewName">The qualified table/view name.</param>
    /// <param name="request">The query request.</param>
    /// <param name="schemaName">The schema name (optional, for table column introspection).</param>
    /// <param name="tableName">The table name (optional, for table column introspection). If null, falls back to result-based column detection.</param>
    /// <returns>A query result containing data, field information, and pagination details.</returns>
    protected virtual async Task<QueryResultDto> ExecuteDataQueryAsync(
        IDbConnection connection,
        string schemaQualifiedTableOrViewName,
        QueryDto request,
        string? schemaName = null,
        string? tableName = null
    )
    {
        // Build SELECT clause
        var selectColumns = request.GetSelectColumns();
        var selectClause = selectColumns.Count > 0 ? string.Join(", ", selectColumns) : "*";

        var sql = new StringBuilder($"SELECT {selectClause} FROM {schemaQualifiedTableOrViewName}");
        var parameters = new DynamicParameters();
        var parameterIndex = 0;

        // Add WHERE clause for filters
        var filterConditions = request.GetFilterConditions();
        if (filterConditions.Count > 0)
        {
            sql.AppendLine(" WHERE");
            var whereConditions = new List<string>();

            foreach (var condition in filterConditions)
            {
                var paramName = $"param{parameterIndex++}";
                var sqlOperator = condition.ToSqlOperator();

                if (condition.Operator == "isnull" || condition.Operator == "notnull")
                {
                    whereConditions.Add($"{condition.Column} {sqlOperator}");
                }
                else if (condition.Operator == "in" || condition.Operator == "nin")
                {
                    if (!string.IsNullOrWhiteSpace(condition.Value))
                    {
                        var values = condition.Value.Split(
                            ',',
                            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                        );
                        var inParams = new List<string>();

                        foreach (var value in values)
                        {
                            var inParamName = $"param{parameterIndex++}";
                            inParams.Add($"@{inParamName}");
                            parameters.Add(inParamName, value);
                        }

                        whereConditions.Add(
                            $"{condition.Column} {sqlOperator} ({string.Join(",", inParams)})"
                        );
                    }
                }
                else if (condition.Operator == "like" || condition.Operator == "nlike")
                {
                    whereConditions.Add($"{condition.Column} {sqlOperator} @{paramName}");
                    parameters.Add(paramName, $"%{condition.Value}%");
                }
                else
                {
                    whereConditions.Add($"{condition.Column} {sqlOperator} @{paramName}");
                    parameters.Add(paramName, condition.Value);
                }
            }

            sql.Append(string.Join(" AND ", whereConditions));
        }

        // Add ORDER BY clause
        var orderByPairs = request.GetOrderByPairs();
        if (orderByPairs.Count > 0)
        {
            sql.AppendLine(" ORDER BY");
            var orderByParts = orderByPairs.Select(pair =>
                $"{pair.Column} {(pair.IsAscending ? "ASC" : "DESC")}"
            );
            sql.Append(string.Join(", ", orderByParts));
        }

        // Get total count if requested
        long? totalCount = null;
        if (request.IncludeTotal)
        {
            var countSql = $"SELECT COUNT(*) FROM {schemaQualifiedTableOrViewName}";
            if (filterConditions.Count > 0)
            {
                var sqlString = sql.ToString();
                var whereIndex = sqlString.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase);
                if (whereIndex > 0)
                {
                    var whereClause = sqlString[whereIndex..];
                    var orderByIndex = whereClause.IndexOf(
                        "ORDER BY",
                        StringComparison.OrdinalIgnoreCase
                    );
                    if (orderByIndex > 0)
                    {
                        whereClause = whereClause[..orderByIndex];
                    }
                    countSql += " " + whereClause;
                }
            }

            totalCount = await connection
                .QuerySingleAsync<long>(countSql, parameters)
                .ConfigureAwait(false);
        }

        // Add pagination - database specific syntax
        var providerType = connection.GetDbProviderType();
        switch (providerType)
        {
            case DbProviderType.SqlServer:
                // SQL Server uses OFFSET/FETCH NEXT and requires ORDER BY
                if (orderByPairs.Count == 0)
                {
                    // Default ORDER BY if none specified (required for OFFSET/FETCH NEXT)
                    sql.AppendLine(" ORDER BY (SELECT NULL)");
                }
                sql.AppendLine($" OFFSET {request.Skip} ROWS");
                sql.AppendLine($" FETCH NEXT {request.Take} ROWS ONLY");
                break;

            case DbProviderType.MySql:
            case DbProviderType.PostgreSql:
            case DbProviderType.Sqlite:
                // Standard LIMIT/OFFSET syntax
                sql.AppendLine($" LIMIT {request.Take} OFFSET {request.Skip}");
                break;

            default:
                // Fallback to LIMIT/OFFSET for unknown providers
                sql.AppendLine($" LIMIT {request.Take} OFFSET {request.Skip}");
                break;
        }

        // Execute the main query
        var results = await connection.QueryAsync(sql.ToString(), parameters).ConfigureAwait(false);

        // Get field information from table schema instead of query results
        var fields = new List<FieldDto>();
        if (!string.IsNullOrWhiteSpace(tableName))
        {
            // Get table columns from schema
            var tableColumns = await connection
                .GetColumnsAsync(schemaName, tableName)
                .ConfigureAwait(false);

            // Filter columns based on SELECT clause if specific columns are requested
            var targetColumns =
                selectColumns.Count > 0
                    ? tableColumns.Where(col =>
                        selectColumns.Contains(col.ColumnName, StringComparer.OrdinalIgnoreCase)
                    )
                    : tableColumns;

            foreach (var col in targetColumns)
            {
                fields.Add(
                    new FieldDto
                    {
                        Name = col.ColumnName,
                        FieldType = col.DotnetType?.GetFriendlyName() ?? "Object",
                        IsNullable = col.IsNullable,
                    }
                );
            }
        }
        else
        {
            // Fallback to old behavior for views or when table name is not provided
            var firstResult = results.FirstOrDefault();
            if (firstResult is IDictionary<string, object> dict)
            {
                foreach (var kvp in dict)
                {
                    fields.Add(
                        new FieldDto
                        {
                            Name = kvp.Key,
                            FieldType = kvp.Value?.GetType().GetFriendlyName() ?? "Object",
                            IsNullable = kvp.Value == null,
                        }
                    );
                }
            }
        }

        // Check if there are more records
        var hasMore =
            results.Count() == request.Take
            && (totalCount == null || request.Skip + request.Take < totalCount);

        return new QueryResultDto
        {
            Data = results,
            Fields = fields,
            Pagination = new PaginationDto
            {
                Take = request.Take,
                Skip = request.Skip,
                Total = totalCount,
                HasMore = hasMore,
            },
        };
    }

    #endregion // Shared Protected Methods

    #region Shared Private Methods

    private string? NormalizeSchemaName(string? schemaName)
    {
        if (string.IsNullOrWhiteSpace(schemaName) || schemaName == "_")
        {
            return null; // Normalize to null
        }
        return schemaName;
    }

    private static async Task AssertSchemaExistsIfSpecifiedAsync(
        string datasourceId,
        string? schemaName,
        IDbConnection connection,
        CancellationToken cancellationToken
    )
    {
        if (!string.IsNullOrWhiteSpace(schemaName))
        {
            var schemaExists = await connection
                .DoesSchemaExistAsync(schemaName, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (!schemaExists)
            {
                throw new KeyNotFoundException(
                    $"Schema '{schemaName}' not found in datasource '{datasourceId}'"
                );
            }
        }
    }

    private static async Task AssertSchemaDoesNotExistAsync(
        string datasourceId,
        string? schemaName,
        IDbConnection connection,
        CancellationToken cancellationToken
    )
    {
        if (!string.IsNullOrWhiteSpace(schemaName))
        {
            var schemaExists = await connection
                .DoesSchemaExistAsync(schemaName, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (schemaExists)
            {
                throw new DuplicateKeyException(
                    $"Schema '{schemaName}' already exists in datasource '{datasourceId}'"
                );
            }
        }
    }

    private static async Task AssertTableExistsAsync(
        string datasourceId,
        string tableName,
        string? schemaName,
        IDbConnection connection,
        CancellationToken cancellationToken
    )
    {
        if (
            !await connection
                .DoesTableExistAsync(schemaName, tableName, cancellationToken: cancellationToken)
                .ConfigureAwait(false)
        )
        {
            throw new KeyNotFoundException(
                !string.IsNullOrWhiteSpace(schemaName)
                    ? $"Table '{tableName}' not found in schema '{schemaName}' of datasource '{datasourceId}'"
                    : $"Table '{tableName}' not found in datasource '{datasourceId}'"
            );
        }
    }

    private static async Task AssertTableDoesNotExistAsync(
        string datasourceId,
        string tableName,
        string? schemaName,
        IDbConnection connection,
        CancellationToken cancellationToken
    )
    {
        if (
            await connection
                .DoesTableExistAsync(schemaName, tableName, cancellationToken: cancellationToken)
                .ConfigureAwait(false)
        )
        {
            throw new DuplicateKeyException(
                !string.IsNullOrWhiteSpace(schemaName)
                    ? $"Table '{tableName}' already exists in schema '{schemaName}' of datasource '{datasourceId}'"
                    : $"Table '{tableName}' already exists in datasource '{datasourceId}'"
            );
        }
    }

    private static async Task AssertViewExistsAsync(
        string datasourceId,
        string viewName,
        string? schemaName,
        IDbConnection connection,
        CancellationToken cancellationToken
    )
    {
        if (
            !await connection
                .DoesViewExistAsync(schemaName, viewName, cancellationToken: cancellationToken)
                .ConfigureAwait(false)
        )
        {
            throw new KeyNotFoundException(
                !string.IsNullOrWhiteSpace(schemaName)
                    ? $"View '{viewName}' not found in schema '{schemaName}' of datasource '{datasourceId}'"
                    : $"View '{viewName}' not found in datasource '{datasourceId}'"
            );
        }
    }

    private static async Task AssertViewDoesNotExistAsync(
        string datasourceId,
        string viewName,
        string? schemaName,
        IDbConnection connection,
        CancellationToken cancellationToken
    )
    {
        if (
            await connection
                .DoesViewExistAsync(schemaName, viewName, cancellationToken: cancellationToken)
                .ConfigureAwait(false)
        )
        {
            throw new DuplicateKeyException(
                !string.IsNullOrWhiteSpace(schemaName)
                    ? $"View '{viewName}' already exists in schema '{schemaName}' of datasource '{datasourceId}'"
                    : $"View '{viewName}' already exists in datasource '{datasourceId}'"
            );
        }
    }

    #endregion // Shared Private Methods
}
