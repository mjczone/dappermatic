// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Security.Claims;
using System.Text;

using Dapper;

using MJCZone.DapperMatic.AspNetCore.Auditing;
using MJCZone.DapperMatic.AspNetCore.Factories;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Models.Requests;
using MJCZone.DapperMatic.AspNetCore.Models.Responses;
using MJCZone.DapperMatic.AspNetCore.Repositories;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.Providers;

namespace MJCZone.DapperMatic.AspNetCore.Services;

/// <summary>
/// Implementation of IDapperMaticService for ASP.NET Core applications.
/// </summary>
public sealed partial class DapperMaticService : IDapperMaticService
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

    /// <inheritdoc />
    public async Task<IEnumerable<DatasourceDto>> GetDatasourcesAsync(
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.ListDatasources,
        };

        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException("Access denied to list datasources.");
        }

        try
        {
            var result = await _datasourceRepository.GetDatasourcesAsync().ConfigureAwait(false);
            await LogAuditEventAsync(context, true).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex)
        {
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<DatasourceDto?> GetDatasourceAsync(
        string datasourceId,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.GetDatasource,
            DatasourceId = datasourceId,
        };

        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to get datasource '{datasourceId}'."
            );
        }

        try
        {
            var result = await _datasourceRepository
                .GetDatasourceAsync(datasourceId)
                .ConfigureAwait(false);
            await LogAuditEventAsync(context, true).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex)
        {
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<DatasourceDto?> AddDatasourceAsync(
        DatasourceDto datasource,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.AddDatasource,
            DatasourceId = datasource.Id,
            RequestBody = datasource,
        };

        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to add datasource '{datasource.Id}'."
            );
        }

        try
        {
            var result = await _datasourceRepository
                .AddDatasourceAsync(datasource)
                .ConfigureAwait(false);

            if (!result)
            {
                await LogAuditEventAsync(context, false, "Datasource already exists")
                    .ConfigureAwait(false);
                return null; // Already exists
            }

            await LogAuditEventAsync(context, true).ConfigureAwait(false);
            return await _datasourceRepository
                .GetDatasourceAsync(datasource.Id!)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<DatasourceDto?> UpdateDatasourceAsync(
        DatasourceDto datasource,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(datasource);

        var datasourceId = datasource?.Id;
        if (string.IsNullOrWhiteSpace(datasourceId))
        {
            throw new ArgumentException("Datasource ID is required.", nameof(datasource));
        }

        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.UpdateDatasource,
            DatasourceId = datasourceId,
            RequestBody = datasource,
        };

        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to update datasource '{datasourceId}'."
            );
        }

        try
        {
            var result = await _datasourceRepository
                .UpdateDatasourceAsync(datasource!)
                .ConfigureAwait(false);

            if (!result)
            {
                await LogAuditEventAsync(context, false, "Datasource not found")
                    .ConfigureAwait(false);
                return null; // Not found
            }

            await LogAuditEventAsync(context, true).ConfigureAwait(false);

            return await _datasourceRepository
                .GetDatasourceAsync(datasourceId)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> RemoveDatasourceAsync(
        string datasourceId,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.RemoveDatasource,
            DatasourceId = datasourceId,
        };

        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to remove datasource '{datasourceId}'."
            );
        }

        try
        {
            var result = await _datasourceRepository
                .RemoveDatasourceAsync(datasourceId)
                .ConfigureAwait(false);
            await LogAuditEventAsync(context, true).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex)
        {
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DatasourceExistsAsync(
        string datasourceId,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.GetDatasource,
            DatasourceId = datasourceId,
        };

        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to check if datasource '{datasourceId}' exists."
            );
        }

        try
        {
            var result = await _datasourceRepository
                .DatasourceExistsAsync(datasourceId)
                .ConfigureAwait(false);
            await LogAuditEventAsync(context, true).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex)
        {
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<DatasourceTestResult> TestDatasourceAsync(
        string datasourceId,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default
    )
    {
        var context = new OperationContext
        {
            User = user,
            Operation = OperationIdentifiers.TestDatasource,
            DatasourceId = datasourceId,
        };

        if (!await _permissions.IsAuthorizedAsync(context).ConfigureAwait(false))
        {
            await LogAuditEventAsync(context, false, "Access denied").ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"Access denied to test datasource '{datasourceId}'."
            );
        }

        var result = new DatasourceTestResult { DatasourceId = datasourceId };

        var startTime = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using var connection = await CreateConnectionForDatasource(datasourceId)
                .ConfigureAwait(false);
            connection.Open();

            result.IsConnected = true;
            result.DatabaseName = connection.Database;
            result.Provider = connection.GetDbProviderType().ToString();
            result.ServerVersion =
                $"{await connection.GetDatabaseVersionAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false)}";
            result.ResponseTimeMs = startTime.ElapsedMilliseconds;
            await LogAuditEventAsync(context, true).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            result.IsConnected = false;
            result.ErrorMessage = ex.Message;
            result.ResponseTimeMs = startTime.ElapsedMilliseconds;
            await LogAuditEventAsync(context, false, ex.Message).ConfigureAwait(false);
        }

        return result;
    }

    private async Task<IDbConnection> CreateConnectionForDatasource(string datasourceId)
    {
        var datasource =
            await _datasourceRepository.GetDatasourceAsync(datasourceId).ConfigureAwait(false)
            ?? throw new ArgumentException(
                $"Datasource '{datasourceId}' not found.",
                nameof(datasourceId)
            );

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

    private async Task LogAuditEventAsync(
        OperationContext operation,
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
    /// <param name="qualifiedFromName">The qualified table/view name.</param>
    /// <param name="request">The query request.</param>
    /// <param name="schemaName">The schema name (optional, for table column introspection).</param>
    /// <param name="tableName">The table name (optional, for table column introspection). If null, falls back to result-based column detection.</param>
    /// <returns>A query result containing data, field information, and pagination details.</returns>
    private async Task<QueryResultDto> ExecuteDataQueryAsync(
        IDbConnection connection,
        string qualifiedFromName,
        QueryRequest request,
        string? schemaName = null,
        string? tableName = null
    )
    {
        // Build SELECT clause
        var selectColumns = request.GetSelectColumns();
        var selectClause = selectColumns.Count > 0 ? string.Join(", ", selectColumns) : "*";

        var sql = new StringBuilder($"SELECT {selectClause} FROM {qualifiedFromName}");
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
            var countSql = $"SELECT COUNT(*) FROM {qualifiedFromName}";
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
            var tableColumns = await connection.GetColumnsAsync(schemaName, tableName).ConfigureAwait(false);

            // Filter columns based on SELECT clause if specific columns are requested
            var targetColumns = selectColumns.Count > 0
                ? tableColumns.Where(col => selectColumns.Contains(col.ColumnName, StringComparer.OrdinalIgnoreCase))
                : tableColumns;

            foreach (var col in targetColumns)
            {
                fields.Add(new FieldDto
                {
                    Name = col.ColumnName,
                    FieldType = col.DotnetType?.GetFriendlyName() ?? "Object",
                    IsNullable = col.IsNullable,
                });
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
}
