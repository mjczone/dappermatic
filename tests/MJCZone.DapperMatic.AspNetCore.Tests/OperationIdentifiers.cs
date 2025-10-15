using System.Security.Claims;
using Microsoft.Extensions.Primitives;
using MJCZone.DapperMatic.AspNetCore;

namespace MJCZone.DapperMatic.AspNetCore.Tests;

/// <summary>
/// Helper class for creating IOperationContext instances for testing service methods.
/// </summary>
public static class OperationIdentifiers
{
    #region Base Context Creation

    /// <summary>
    /// Creates a basic operation context with default values.
    /// </summary>
    /// <param name="operation">The operation identifier (e.g., "datasources/get").</param>
    /// <param name="user">Optional claims principal for the user.</param>
    /// <param name="requestId">Optional request ID for correlation.</param>
    /// <returns>A configured operation context.</returns>
    public static IOperationContext CreateContext(
        string? operation = null,
        ClaimsPrincipal? user = null,
        string? requestId = null
    )
    {
        return new OperationContext
        {
            Operation = operation,
            User = user ?? CreateDefaultUser(),
            RequestId = requestId ?? Guid.NewGuid().ToString(),
            HttpMethod = "GET", // Default to GET, override in specific methods
            EndpointPath = "/api/dappermatic",
            IpAddress = "127.0.0.1",
            Properties = [],
            QueryParameters = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase),
            RouteValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            HeaderValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        };
    }

    /// <summary>
    /// Creates a default test user with basic claims.
    /// </summary>
    /// <param name="userName">Optional username (defaults to "testuser").</param>
    /// <param name="userId">Optional user ID (defaults to "test-user-id").</param>
    /// <param name="additionalClaims">Additional claims to add.</param>
    /// <returns>A ClaimsPrincipal for testing.</returns>
    public static ClaimsPrincipal CreateDefaultUser(
        string userName = "testuser",
        string userId = "test-user-id",
        params Claim[] additionalClaims
    )
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, userName),
            new(ClaimTypes.NameIdentifier, userId),
            new("sub", userId),
        };

        if (additionalClaims.Length > 0)
        {
            claims.AddRange(additionalClaims);
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    /// <summary>
    /// Creates an anonymous user context (no authentication).
    /// </summary>
    /// <returns>A ClaimsPrincipal with no authenticated identity.</returns>
    public static ClaimsPrincipal CreateAnonymousUser()
    {
        return new ClaimsPrincipal(new ClaimsIdentity());
    }

    #endregion

    #region Datasource Operations

    /// <summary>
    /// Creates context for datasource list operations.
    /// </summary>
    public static IOperationContext ForDatasourceList(ClaimsPrincipal? user = null)
    {
        var context = CreateContext("datasources/list", user);
        context.HttpMethod = "GET";
        return context;
    }

    /// <summary>
    /// Creates context for datasource get operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForDatasourceGet(string datasourceId, ClaimsPrincipal? user = null)
    {
        var context = CreateContext("datasources/get", user);
        context.DatasourceId = datasourceId;
        context.HttpMethod = "GET";
        return context;
    }

    /// <summary>
    /// Creates context for datasource add operations.
    /// </summary>
    /// <param name="requestBody">The datasource to add.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForDatasourceAdd(object requestBody, ClaimsPrincipal? user = null)
    {
        var context = CreateContext("datasources/add", user);
        context.RequestBody = requestBody;
        context.HttpMethod = "POST";
        return context;
    }

    /// <summary>
    /// Creates context for datasource update operations.
    /// </summary>
    /// <param name="requestBody">The datasource update data.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForDatasourceUpdate(object requestBody, ClaimsPrincipal? user = null)
    {
        var context = CreateContext("datasources/update", user);
        context.RequestBody = requestBody;
        context.HttpMethod = "PUT";
        return context;
    }

    /// <summary>
    /// Creates context for datasource remove operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForDatasourceRemove(string datasourceId, ClaimsPrincipal? user = null)
    {
        var context = CreateContext("datasources/remove", user);
        context.DatasourceId = datasourceId;
        context.HttpMethod = "DELETE";
        return context;
    }

    /// <summary>
    /// Creates context for datasource exists operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForDatasourceExists(string datasourceId, ClaimsPrincipal? user = null)
    {
        var context = CreateContext("datasources/exists", user);
        context.DatasourceId = datasourceId;
        context.HttpMethod = "GET";
        return context;
    }

    /// <summary>
    /// Creates context for datasource test operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForDatasourceTest(string datasourceId, ClaimsPrincipal? user = null)
    {
        var context = CreateContext("datasources/test", user);
        context.DatasourceId = datasourceId;
        context.HttpMethod = "GET";
        return context;
    }

    #endregion

    #region Schema Operations

    /// <summary>
    /// Creates context for schema list operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForSchemaList(string datasourceId, ClaimsPrincipal? user = null)
    {
        var context = CreateContext("schemas/list", user);
        context.DatasourceId = datasourceId;
        context.HttpMethod = "GET";
        return context;
    }

    /// <summary>
    /// Creates context for schema get operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForSchemaGet(
        string datasourceId,
        string schemaName,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("schemas/get", user);
        context.DatasourceId = datasourceId;
        context.SchemaName = schemaName;
        context.HttpMethod = "GET";
        return context;
    }

    /// <summary>
    /// Creates context for schema create operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="requestBody">The schema to create.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForSchemaCreate(
        string datasourceId,
        object requestBody,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("schemas/create", user);
        context.DatasourceId = datasourceId;
        context.RequestBody = requestBody;
        context.HttpMethod = "POST";
        return context;
    }

    /// <summary>
    /// Creates context for schema drop operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForSchemaDrop(
        string datasourceId,
        string schemaName,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("schemas/drop", user);
        context.DatasourceId = datasourceId;
        context.SchemaName = schemaName;
        context.HttpMethod = "DELETE";
        return context;
    }

    /// <summary>
    /// Creates context for schema exists operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForSchemaExists(
        string datasourceId,
        string schemaName,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("schemas/exists", user);
        context.DatasourceId = datasourceId;
        context.SchemaName = schemaName;
        context.HttpMethod = "GET";
        return context;
    }

    #endregion

    #region Table Operations

    /// <summary>
    /// Creates context for table list operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="schemaName">Optional schema name filter.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForTableList(
        string datasourceId,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("tables/list", user);
        context.DatasourceId = datasourceId;
        context.SchemaName = schemaName;
        context.HttpMethod = "GET";
        return context;
    }

    /// <summary>
    /// Creates context for table get operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForTableGet(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("tables/get", user);
        context.DatasourceId = datasourceId;
        context.TableName = tableName;
        context.SchemaName = schemaName;
        context.HttpMethod = "GET";
        return context;
    }

    /// <summary>
    /// Creates context for table create operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="requestBody">The table to create.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForTableCreate(
        string datasourceId,
        object requestBody,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("tables/create", user);
        context.DatasourceId = datasourceId;
        context.RequestBody = requestBody;
        context.HttpMethod = "POST";
        return context;
    }

    /// <summary>
    /// Creates context for table drop operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForTableDrop(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("tables/drop", user);
        context.DatasourceId = datasourceId;
        context.TableName = tableName;
        context.SchemaName = schemaName;
        context.HttpMethod = "DELETE";
        return context;
    }

    /// <summary>
    /// Creates context for table exists operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForTableExists(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("tables/exists", user);
        context.DatasourceId = datasourceId;
        context.TableName = tableName;
        context.SchemaName = schemaName;
        context.HttpMethod = "GET";
        return context;
    }

    /// <summary>
    /// Creates context for table query operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="requestBody">The query request.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForTableQuery(
        string datasourceId,
        string tableName,
        object requestBody,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("tables/query", user);
        context.DatasourceId = datasourceId;
        context.TableName = tableName;
        context.SchemaName = schemaName;
        context.RequestBody = requestBody;
        context.HttpMethod = "POST";
        return context;
    }

    /// <summary>
    /// Creates context for table update operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="requestBody">The table update data.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForTableUpdate(
        string datasourceId,
        string tableName,
        object requestBody,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("tables/update", user);
        context.DatasourceId = datasourceId;
        context.TableName = tableName;
        context.RequestBody = requestBody;
        context.HttpMethod = "PUT";
        return context;
    }

    /// <summary>
    /// Creates context for table rename operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="currentTableName">The current table name.</param>
    /// <param name="newTableName">The new table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForTableRename(
        string datasourceId,
        string currentTableName,
        string newTableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("tables/rename", user);
        context.DatasourceId = datasourceId;
        context.TableName = currentTableName;
        context.SchemaName = schemaName;
        context.Properties ??= [];
        context.Properties["NewTableName"] = newTableName;
        context.HttpMethod = "PATCH";
        return context;
    }

    #endregion

    #region View Operations

    /// <summary>
    /// Creates context for view list operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="schemaName">Optional schema name filter.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForViewList(
        string datasourceId,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("views/list", user);
        context.DatasourceId = datasourceId;
        context.SchemaName = schemaName;
        context.HttpMethod = "GET";
        return context;
    }

    /// <summary>
    /// Creates context for view get operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="viewName">The view name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForViewGet(
        string datasourceId,
        string viewName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("views/get", user);
        context.DatasourceId = datasourceId;
        context.ViewName = viewName;
        context.SchemaName = schemaName;
        context.HttpMethod = "GET";
        return context;
    }

    /// <summary>
    /// Creates context for view create operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="requestBody">The view to create.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForViewCreate(
        string datasourceId,
        object requestBody,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("views/create", user);
        context.DatasourceId = datasourceId;
        context.RequestBody = requestBody;
        context.HttpMethod = "POST";
        return context;
    }

    /// <summary>
    /// Creates context for view update operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="viewName">The view name.</param>
    /// <param name="requestBody">The view update data.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForViewUpdate(
        string datasourceId,
        string viewName,
        object requestBody,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("views/update", user);
        context.DatasourceId = datasourceId;
        context.ViewName = viewName;
        context.RequestBody = requestBody;
        context.HttpMethod = "PUT";
        return context;
    }

    /// <summary>
    /// Creates context for view rename operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="currentViewName">The current view name.</param>
    /// <param name="newViewName">The new view name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForViewRename(
        string datasourceId,
        string currentViewName,
        string newViewName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("views/rename", user);
        context.DatasourceId = datasourceId;
        context.ViewName = currentViewName;
        context.SchemaName = schemaName;
        context.Properties ??= [];
        context.Properties["NewViewName"] = newViewName;
        context.HttpMethod = "PATCH";
        return context;
    }

    /// <summary>
    /// Creates context for view drop operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="viewName">The view name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForViewDrop(
        string datasourceId,
        string viewName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("views/drop", user);
        context.DatasourceId = datasourceId;
        context.ViewName = viewName;
        context.SchemaName = schemaName;
        context.HttpMethod = "DELETE";
        return context;
    }

    /// <summary>
    /// Creates context for view exists operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="viewName">The view name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForViewExists(
        string datasourceId,
        string viewName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("views/exists", user);
        context.DatasourceId = datasourceId;
        context.ViewName = viewName;
        context.SchemaName = schemaName;
        context.HttpMethod = "GET";
        return context;
    }

    /// <summary>
    /// Creates context for view query operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="viewName">The view name.</param>
    /// <param name="requestBody">The query request.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForViewQuery(
        string datasourceId,
        string viewName,
        object requestBody,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("views/query", user);
        context.DatasourceId = datasourceId;
        context.ViewName = viewName;
        context.SchemaName = schemaName;
        context.RequestBody = requestBody;
        context.HttpMethod = "POST";
        return context;
    }

    #endregion

    #region Column Operations

    /// <summary>
    /// Creates context for column list operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForColumnList(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("columns/list", user);
        context.DatasourceId = datasourceId;
        context.TableName = tableName;
        context.SchemaName = schemaName;
        context.HttpMethod = "GET";
        return context;
    }

    /// <summary>
    /// Creates context for column get operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForColumnGet(
        string datasourceId,
        string tableName,
        string columnName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("columns/get", user);
        context.DatasourceId = datasourceId;
        context.TableName = tableName;
        context.ColumnName = columnName;
        context.SchemaName = schemaName;
        context.HttpMethod = "GET";
        return context;
    }

    /// <summary>
    /// Creates context for column add operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="requestBody">The column to add.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForColumnAdd(
        string datasourceId,
        string tableName,
        object requestBody,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("columns/add", user);
        context.DatasourceId = datasourceId;
        context.TableName = tableName;
        context.SchemaName = schemaName;
        context.RequestBody = requestBody;
        context.HttpMethod = "POST";
        return context;
    }

    /// <summary>
    /// Creates context for column rename operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The current column name.</param>
    /// <param name="newColumnName">The new column name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForColumnRename(
        string datasourceId,
        string tableName,
        string columnName,
        string newColumnName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("columns/rename", user);
        context.DatasourceId = datasourceId;
        context.TableName = tableName;
        context.ColumnName = columnName;
        context.SchemaName = schemaName;
        context.Properties ??= [];
        context.Properties["NewColumnName"] = newColumnName;
        context.HttpMethod = "PUT";
        return context;
    }

    /// <summary>
    /// Creates context for column drop operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForColumnDrop(
        string datasourceId,
        string tableName,
        string columnName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("columns/drop", user);
        context.DatasourceId = datasourceId;
        context.TableName = tableName;
        context.ColumnName = columnName;
        context.SchemaName = schemaName;
        context.HttpMethod = "DELETE";
        return context;
    }

    #endregion

    #region Index Operations

    /// <summary>
    /// Creates context for index list operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForIndexList(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("indexes/list", user);
        context.DatasourceId = datasourceId;
        context.TableName = tableName;
        context.SchemaName = schemaName;
        context.HttpMethod = "GET";
        return context;
    }

    /// <summary>
    /// Creates context for index get operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="indexName">The index name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForIndexGet(
        string datasourceId,
        string tableName,
        string indexName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("indexes/get", user);
        context.DatasourceId = datasourceId;
        context.TableName = tableName;
        context.IndexName = indexName;
        context.SchemaName = schemaName;
        context.HttpMethod = "GET";
        return context;
    }

    /// <summary>
    /// Creates context for index create operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="requestBody">The index to create.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForIndexCreate(
        string datasourceId,
        string tableName,
        object requestBody,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("indexes/create", user);
        context.DatasourceId = datasourceId;
        context.TableName = tableName;
        context.SchemaName = schemaName;
        context.RequestBody = requestBody;
        context.HttpMethod = "POST";
        return context;
    }

    /// <summary>
    /// Creates context for index drop operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="indexName">The index name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForIndexDrop(
        string datasourceId,
        string tableName,
        string indexName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("indexes/drop", user);
        context.DatasourceId = datasourceId;
        context.TableName = tableName;
        context.IndexName = indexName;
        context.SchemaName = schemaName;
        context.HttpMethod = "DELETE";
        return context;
    }

    #endregion

    #region Constraint Operations

    /// <summary>
    /// Creates context for primary key get operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForPrimaryKeyGet(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("primary-key-constraints/get", user);
        context.DatasourceId = datasourceId;
        context.TableName = tableName;
        context.SchemaName = schemaName;
        context.HttpMethod = "GET";
        return context;
    }

    /// <summary>
    /// Creates context for primary key create operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="requestBody">The primary key constraint to create.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForPrimaryKeyCreate(
        string datasourceId,
        string tableName,
        object requestBody,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("primary-key-constraints/create", user);
        context.DatasourceId = datasourceId;
        context.TableName = tableName;
        context.SchemaName = schemaName;
        context.RequestBody = requestBody;
        context.HttpMethod = "POST";
        return context;
    }

    /// <summary>
    /// Creates context for primary key drop operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForPrimaryKeyDrop(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("primary-key-constraints/drop", user);
        context.DatasourceId = datasourceId;
        context.TableName = tableName;
        context.SchemaName = schemaName;
        context.HttpMethod = "DELETE";
        return context;
    }

    /// <summary>
    /// Creates context for foreign key list operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForForeignKeyList(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("foreign-key-constraints/list", user);
        context.DatasourceId = datasourceId;
        context.TableName = tableName;
        context.SchemaName = schemaName;
        context.HttpMethod = "GET";
        return context;
    }

    /// <summary>
    /// Creates context for foreign key get operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForForeignKeyGet(
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("foreign-key-constraints/get", user);
        context.DatasourceId = datasourceId;
        context.TableName = tableName;
        context.ConstraintName = constraintName;
        context.SchemaName = schemaName;
        context.HttpMethod = "GET";
        return context;
    }

    /// <summary>
    /// Creates context for foreign key create operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="requestBody">The foreign key constraint to create.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForForeignKeyCreate(
        string datasourceId,
        string tableName,
        object requestBody,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("foreign-key-constraints/create", user);
        context.DatasourceId = datasourceId;
        context.TableName = tableName;
        context.SchemaName = schemaName;
        context.RequestBody = requestBody;
        context.HttpMethod = "POST";
        return context;
    }

    /// <summary>
    /// Creates context for foreign key drop operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForForeignKeyDrop(
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("foreign-key-constraints/drop", user);
        context.DatasourceId = datasourceId;
        context.TableName = tableName;
        context.ConstraintName = constraintName;
        context.SchemaName = schemaName;
        context.HttpMethod = "DELETE";
        return context;
    }

    /// <summary>
    /// Creates context for check constraint list operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForCheckConstraintList(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("check-constraints/list", user);
        context.DatasourceId = datasourceId;
        context.TableName = tableName;
        context.SchemaName = schemaName;
        context.HttpMethod = "GET";
        return context;
    }

    /// <summary>
    /// Creates context for check constraint get operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForCheckConstraintGet(
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("check-constraints/get", user);
        context.DatasourceId = datasourceId;
        context.TableName = tableName;
        context.ConstraintName = constraintName;
        context.SchemaName = schemaName;
        context.HttpMethod = "GET";
        return context;
    }

    /// <summary>
    /// Creates context for check constraint create operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="requestBody">The check constraint to create.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForCheckConstraintCreate(
        string datasourceId,
        string tableName,
        object requestBody,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("check-constraints/create", user);
        context.DatasourceId = datasourceId;
        context.TableName = tableName;
        context.SchemaName = schemaName;
        context.RequestBody = requestBody;
        context.HttpMethod = "POST";
        return context;
    }

    /// <summary>
    /// Creates context for check constraint drop operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForCheckConstraintDrop(
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("check-constraints/drop", user);
        context.DatasourceId = datasourceId;
        context.TableName = tableName;
        context.ConstraintName = constraintName;
        context.SchemaName = schemaName;
        context.HttpMethod = "DELETE";
        return context;
    }

    /// <summary>
    /// Creates context for unique constraint list operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForUniqueConstraintList(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("uniqueconstraints/list", user);
        context.DatasourceId = datasourceId;
        context.TableName = tableName;
        context.SchemaName = schemaName;
        context.HttpMethod = "GET";
        return context;
    }

    /// <summary>
    /// Creates context for unique constraint get operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForUniqueConstraintGet(
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("uniqueconstraints/get", user);
        context.DatasourceId = datasourceId;
        context.TableName = tableName;
        context.ConstraintName = constraintName;
        context.SchemaName = schemaName;
        context.HttpMethod = "GET";
        return context;
    }

    /// <summary>
    /// Creates context for unique constraint create operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="requestBody">The unique constraint to create.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForUniqueConstraintCreate(
        string datasourceId,
        string tableName,
        object requestBody,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("uniqueconstraints/create", user);
        context.DatasourceId = datasourceId;
        context.TableName = tableName;
        context.SchemaName = schemaName;
        context.RequestBody = requestBody;
        context.HttpMethod = "POST";
        return context;
    }

    /// <summary>
    /// Creates context for unique constraint drop operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForUniqueConstraintDrop(
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("uniqueconstraints/drop", user);
        context.DatasourceId = datasourceId;
        context.TableName = tableName;
        context.ConstraintName = constraintName;
        context.SchemaName = schemaName;
        context.HttpMethod = "DELETE";
        return context;
    }

    /// <summary>
    /// Creates context for default constraint list operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForDefaultConstraintList(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("default-constraints/list", user);
        context.DatasourceId = datasourceId;
        context.TableName = tableName;
        context.SchemaName = schemaName;
        context.HttpMethod = "GET";
        return context;
    }

    /// <summary>
    /// Creates context for default constraint get operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForDefaultConstraintGet(
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("default-constraints/get", user);
        context.DatasourceId = datasourceId;
        context.TableName = tableName;
        context.ConstraintName = constraintName;
        context.SchemaName = schemaName;
        context.HttpMethod = "GET";
        return context;
    }

    /// <summary>
    /// Creates context for default constraint get operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForDefaultConstraintOnColumnGet(
        string datasourceId,
        string tableName,
        string columnName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("default-constraints/get", user);
        context.DatasourceId = datasourceId;
        context.TableName = tableName;
        context.ColumnName = columnName;
        context.SchemaName = schemaName;
        context.HttpMethod = "GET";
        return context;
    }

    /// <summary>
    /// Creates context for default constraint create operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="requestBody">The default constraint to create.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForDefaultConstraintCreate(
        string datasourceId,
        string tableName,
        object requestBody,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("default-constraints/create", user);
        context.DatasourceId = datasourceId;
        context.TableName = tableName;
        context.SchemaName = schemaName;
        context.RequestBody = requestBody;
        context.HttpMethod = "POST";
        return context;
    }

    /// <summary>
    /// Creates context for default constraint drop operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForDefaultConstraintDrop(
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("default-constraints/drop", user);
        context.DatasourceId = datasourceId;
        context.TableName = tableName;
        context.ConstraintName = constraintName;
        context.SchemaName = schemaName;
        context.HttpMethod = "DELETE";
        return context;
    }

    /// <summary>
    /// Creates context for default constraint drop operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="constraintName">The constraint name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForDefaultConstraintOnColumnDrop(
        string datasourceId,
        string tableName,
        string columnName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("default-constraints/drop", user);
        context.DatasourceId = datasourceId;
        context.TableName = tableName;
        context.ColumnName = columnName;
        context.SchemaName = schemaName;
        context.HttpMethod = "DELETE";
        return context;
    }

    #endregion

    #region DataType Operations

    /// <summary>
    /// Creates context for data type get operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="includeCustomTypes">Whether to include custom types.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForDataTypeGet(
        string datasourceId,
        bool includeCustomTypes = false,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("datatypes/get", user);
        context.DatasourceId = datasourceId;
        context.Properties ??= [];
        context.Properties["IncludeCustomTypes"] = includeCustomTypes;
        context.HttpMethod = "GET";
        return context;
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Creates context with custom properties for advanced testing scenarios.
    /// </summary>
    /// <param name="operation">The operation identifier.</param>
    /// <param name="properties">Custom properties.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForCustomOperation(
        string operation,
        Dictionary<string, object>? properties = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext(operation, user);
        if (properties != null)
        {
            context.Properties ??= [];
            foreach (var (key, value) in properties)
            {
                context.Properties[key] = value;
            }
        }
        context.HttpMethod = "POST"; // Default for custom operations
        return context;
    }

    /// <summary>
    /// Creates context with specific HTTP details for testing HTTP-related functionality.
    /// </summary>
    /// <param name="operation">The operation identifier.</param>
    /// <param name="httpMethod">The HTTP method.</param>
    /// <param name="endpointPath">The endpoint path.</param>
    /// <param name="queryParams">Query parameters.</param>
    /// <param name="headers">HTTP headers.</param>
    /// <param name="user">Optional user context.</param>
    public static IOperationContext ForHttpOperation(
        string operation,
        string httpMethod = "POST",
        string endpointPath = "/api/dappermatic",
        Dictionary<string, string>? queryParams = null,
        Dictionary<string, string>? headers = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext(operation, user);
        context.HttpMethod = httpMethod;
        context.EndpointPath = endpointPath;

        if (queryParams != null)
        {
            context.QueryParameters ??= new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in queryParams)
            {
                context.QueryParameters[key] = value;
            }
        }

        if (headers != null)
        {
            context.HeaderValues ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in headers)
            {
                context.HeaderValues[key] = value;
            }
        }

        return context;
    }

    #endregion
}
