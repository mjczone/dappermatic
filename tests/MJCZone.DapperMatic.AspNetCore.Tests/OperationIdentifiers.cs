using System.Security.Claims;
using Microsoft.Extensions.Primitives;
using MJCZone.DapperMatic.AspNetCore;

namespace MJCZone.DapperMatic.AspNetCore.Tests;

/// <summary>
/// Helper class for creating DapperMaticOperation instances for testing service methods.
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
    public static DapperMaticOperation CreateContext(
        string? operation = null,
        ClaimsPrincipal? user = null,
        string? requestId = null
    )
    {
        return new DapperMaticOperation
        {
            Operation = operation,
            User = user ?? CreateDefaultUser(),
            RequestId = requestId ?? Guid.NewGuid().ToString(),
            HttpMethod = "GET", // Default to GET, override in specific methods
            EndpointPath = "/api/dappermatic",
            IpAddress = "127.0.0.1",
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
    public static DapperMaticOperation ForDatasourceList(ClaimsPrincipal? user = null)
    {
        var context = CreateContext("datasources/list", user);
        context = context with { HttpMethod = "GET" };
        return context;
    }

    /// <summary>
    /// Creates context for datasource get operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="user">Optional user context.</param>
    public static DapperMaticOperation ForDatasourceGet(string datasourceId, ClaimsPrincipal? user = null)
    {
        var context = CreateContext("datasources/get", user);
        context = context with { DatasourceId = datasourceId, HttpMethod = "GET" };
        return context;
    }

    /// <summary>
    /// Creates context for datasource add operations.
    /// </summary>
    /// <param name="requestBody">The datasource to add.</param>
    /// <param name="user">Optional user context.</param>
    public static DapperMaticOperation ForDatasourceAdd(object requestBody, ClaimsPrincipal? user = null)
    {
        var context = CreateContext("datasources/post", user);
        context = context with { RequestBody = requestBody, HttpMethod = "POST" };
        return context;
    }

    /// <summary>
    /// Creates context for datasource update operations.
    /// </summary>
    /// <param name="requestBody">The datasource update data.</param>
    /// <param name="user">Optional user context.</param>
    public static DapperMaticOperation ForDatasourceUpdate(object requestBody, ClaimsPrincipal? user = null)
    {
        var context = CreateContext("datasources/put", user);
        context = context with { RequestBody = requestBody, HttpMethod = "PUT" };
        return context;
    }

    /// <summary>
    /// Creates context for datasource remove operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="user">Optional user context.</param>
    public static DapperMaticOperation ForDatasourceRemove(string datasourceId, ClaimsPrincipal? user = null)
    {
        var context = CreateContext("datasources/delete", user);
        context = context with { DatasourceId = datasourceId, HttpMethod = "DELETE" };
        return context;
    }

    /// <summary>
    /// Creates context for datasource exists operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="user">Optional user context.</param>
    public static DapperMaticOperation ForDatasourceExists(string datasourceId, ClaimsPrincipal? user = null)
    {
        var context = CreateContext("datasources/exists", user);
        context = context with { DatasourceId = datasourceId, HttpMethod = "GET" };
        return context;
    }

    /// <summary>
    /// Creates context for datasource test operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="user">Optional user context.</param>
    public static DapperMaticOperation ForDatasourceTest(string datasourceId, ClaimsPrincipal? user = null)
    {
        var context = CreateContext("datasources/test", user);
        context = context with { DatasourceId = datasourceId, HttpMethod = "GET" };
        return context;
    }

    #endregion

    #region Schema Operations

    /// <summary>
    /// Creates context for schema list operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="user">Optional user context.</param>
    public static DapperMaticOperation ForSchemaList(string datasourceId, ClaimsPrincipal? user = null)
    {
        var context = CreateContext("schemas/list", user);
        context = context with { DatasourceId = datasourceId, HttpMethod = "GET" };
        return context;
    }

    /// <summary>
    /// Creates context for schema get operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static DapperMaticOperation ForSchemaGet(string datasourceId, string schemaName, ClaimsPrincipal? user = null)
    {
        var context = CreateContext("schemas/get", user);
        context = context with { DatasourceId = datasourceId, SchemaName = schemaName, HttpMethod = "GET" };
        return context;
    }

    /// <summary>
    /// Creates context for schema create operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="requestBody">The schema to create.</param>
    /// <param name="user">Optional user context.</param>
    public static DapperMaticOperation ForSchemaCreate(
        string datasourceId,
        object requestBody,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("schemas/post", user);
        context = context with { DatasourceId = datasourceId, RequestBody = requestBody, HttpMethod = "POST" };
        return context;
    }

    /// <summary>
    /// Creates context for schema drop operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static DapperMaticOperation ForSchemaDrop(string datasourceId, string schemaName, ClaimsPrincipal? user = null)
    {
        var context = CreateContext("schemas/delete", user);
        context = context with { DatasourceId = datasourceId, SchemaName = schemaName, HttpMethod = "DELETE" };
        return context;
    }

    /// <summary>
    /// Creates context for schema exists operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static DapperMaticOperation ForSchemaExists(
        string datasourceId,
        string schemaName,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("schemas/exists", user);
        context = context with { DatasourceId = datasourceId, SchemaName = schemaName, HttpMethod = "GET" };
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
    public static DapperMaticOperation ForTableList(
        string datasourceId,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("tables/list", user);
        context = context with { DatasourceId = datasourceId, SchemaName = schemaName, HttpMethod = "GET" };
        return context;
    }

    /// <summary>
    /// Creates context for table get operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static DapperMaticOperation ForTableGet(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("tables/get", user);
        context = context with
        {
            DatasourceId = datasourceId,
            TableName = tableName,
            SchemaName = schemaName,
            HttpMethod = "GET",
        };
        return context;
    }

    /// <summary>
    /// Creates context for table create operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="requestBody">The table to create.</param>
    /// <param name="user">Optional user context.</param>
    public static DapperMaticOperation ForTableCreate(
        string datasourceId,
        object requestBody,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("tables/post", user);
        context = context with { DatasourceId = datasourceId, RequestBody = requestBody, HttpMethod = "POST" };
        return context;
    }

    /// <summary>
    /// Creates context for table drop operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static DapperMaticOperation ForTableDrop(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("tables/delete", user);
        context = context with
        {
            DatasourceId = datasourceId,
            TableName = tableName,
            SchemaName = schemaName,
            HttpMethod = "DELETE",
        };
        return context;
    }

    /// <summary>
    /// Creates context for table exists operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static DapperMaticOperation ForTableExists(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("tables/exists", user);
        context = context with
        {
            DatasourceId = datasourceId,
            TableName = tableName,
            SchemaName = schemaName,
            HttpMethod = "GET",
        };
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
    public static DapperMaticOperation ForTableQuery(
        string datasourceId,
        string tableName,
        object requestBody,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("tables/query", user);
        context = context with
        {
            DatasourceId = datasourceId,
            TableName = tableName,
            SchemaName = schemaName,
            RequestBody = requestBody,
            HttpMethod = "POST",
        };
        return context;
    }

    /// <summary>
    /// Creates context for table update operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="requestBody">The table update data.</param>
    /// <param name="user">Optional user context.</param>
    public static DapperMaticOperation ForTableUpdate(
        string datasourceId,
        string tableName,
        object requestBody,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("tables/put", user);
        context = context with
        {
            DatasourceId = datasourceId,
            TableName = tableName,
            RequestBody = requestBody,
            HttpMethod = "PUT",
        };
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
    public static DapperMaticOperation ForTableRename(
        string datasourceId,
        string currentTableName,
        string newTableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("tables/put", user);
        context = context with { DatasourceId = datasourceId, TableName = currentTableName, SchemaName = schemaName };
        context = context.WithProperty("NewTableName", newTableName);
        context = context with { HttpMethod = "PATCH" };
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
    public static DapperMaticOperation ForViewList(
        string datasourceId,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("views/list", user);
        context = context with { DatasourceId = datasourceId, SchemaName = schemaName, HttpMethod = "GET" };
        return context;
    }

    /// <summary>
    /// Creates context for view get operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="viewName">The view name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static DapperMaticOperation ForViewGet(
        string datasourceId,
        string viewName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("views/get", user);
        context = context with
        {
            DatasourceId = datasourceId,
            ViewName = viewName,
            SchemaName = schemaName,
            HttpMethod = "GET",
        };
        return context;
    }

    /// <summary>
    /// Creates context for view create operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="requestBody">The view to create.</param>
    /// <param name="user">Optional user context.</param>
    public static DapperMaticOperation ForViewCreate(string datasourceId, object requestBody, ClaimsPrincipal? user = null)
    {
        var context = CreateContext("views/post", user);
        context = context with { DatasourceId = datasourceId, RequestBody = requestBody, HttpMethod = "POST" };
        return context;
    }

    /// <summary>
    /// Creates context for view update operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="viewName">The view name.</param>
    /// <param name="requestBody">The view update data.</param>
    /// <param name="user">Optional user context.</param>
    public static DapperMaticOperation ForViewUpdate(
        string datasourceId,
        string viewName,
        object requestBody,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("views/put", user);
        context = context with
        {
            DatasourceId = datasourceId,
            ViewName = viewName,
            RequestBody = requestBody,
            HttpMethod = "PUT",
        };
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
    public static DapperMaticOperation ForViewRename(
        string datasourceId,
        string currentViewName,
        string newViewName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("views/put", user);
        context = context with { DatasourceId = datasourceId, ViewName = currentViewName, SchemaName = schemaName };
        context = context.WithProperty("NewViewName", newViewName);
        context = context with { HttpMethod = "PATCH" };
        return context;
    }

    /// <summary>
    /// Creates context for view drop operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="viewName">The view name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static DapperMaticOperation ForViewDrop(
        string datasourceId,
        string viewName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("views/delete", user);
        context = context with
        {
            DatasourceId = datasourceId,
            ViewName = viewName,
            SchemaName = schemaName,
            HttpMethod = "DELETE",
        };
        return context;
    }

    /// <summary>
    /// Creates context for view exists operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="viewName">The view name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static DapperMaticOperation ForViewExists(
        string datasourceId,
        string viewName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("views/exists", user);
        context = context with
        {
            DatasourceId = datasourceId,
            ViewName = viewName,
            SchemaName = schemaName,
            HttpMethod = "GET",
        };
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
    public static DapperMaticOperation ForViewQuery(
        string datasourceId,
        string viewName,
        object requestBody,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("views/query", user);
        context = context with
        {
            DatasourceId = datasourceId,
            ViewName = viewName,
            SchemaName = schemaName,
            RequestBody = requestBody,
            HttpMethod = "POST",
        };
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
    public static DapperMaticOperation ForColumnList(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("columns/list", user);
        context = context with
        {
            DatasourceId = datasourceId,
            TableName = tableName,
            SchemaName = schemaName,
            HttpMethod = "GET",
        };
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
    public static DapperMaticOperation ForColumnGet(
        string datasourceId,
        string tableName,
        string columnName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("columns/get", user);
        context = context with
        {
            DatasourceId = datasourceId,
            TableName = tableName,
            ColumnNames = [columnName],
            SchemaName = schemaName,
            HttpMethod = "GET",
        };
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
    public static DapperMaticOperation ForColumnAdd(
        string datasourceId,
        string tableName,
        object requestBody,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("columns/post", user);
        context = context with
        {
            DatasourceId = datasourceId,
            TableName = tableName,
            SchemaName = schemaName,
            RequestBody = requestBody,
            HttpMethod = "POST",
        };
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
    public static DapperMaticOperation ForColumnRename(
        string datasourceId,
        string tableName,
        string columnName,
        string newColumnName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("columns/put", user);
        context = context with
        {
            DatasourceId = datasourceId,
            TableName = tableName,
            ColumnNames = [columnName],
            SchemaName = schemaName,
        };
        context = context.WithProperty("NewColumnName", newColumnName);
        context = context with { HttpMethod = "PUT" };
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
    public static DapperMaticOperation ForColumnDrop(
        string datasourceId,
        string tableName,
        string columnName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("columns/delete", user);
        context = context with
        {
            DatasourceId = datasourceId,
            TableName = tableName,
            ColumnNames = [columnName],
            SchemaName = schemaName,
            HttpMethod = "DELETE",
        };
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
    public static DapperMaticOperation ForIndexList(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("indexes/list", user);
        context = context with
        {
            DatasourceId = datasourceId,
            TableName = tableName,
            SchemaName = schemaName,
            HttpMethod = "GET",
        };
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
    public static DapperMaticOperation ForIndexGet(
        string datasourceId,
        string tableName,
        string indexName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("indexes/get", user);
        context = context with
        {
            DatasourceId = datasourceId,
            TableName = tableName,
            IndexName = indexName,
            SchemaName = schemaName,
            HttpMethod = "GET",
        };
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
    public static DapperMaticOperation ForIndexCreate(
        string datasourceId,
        string tableName,
        object requestBody,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("indexes/post", user);
        context = context with
        {
            DatasourceId = datasourceId,
            TableName = tableName,
            SchemaName = schemaName,
            RequestBody = requestBody,
            HttpMethod = "POST",
        };
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
    public static DapperMaticOperation ForIndexDrop(
        string datasourceId,
        string tableName,
        string indexName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("indexes/delete", user);
        context = context with
        {
            DatasourceId = datasourceId,
            TableName = tableName,
            IndexName = indexName,
            SchemaName = schemaName,
            HttpMethod = "DELETE",
        };
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
    public static DapperMaticOperation ForPrimaryKeyGet(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("primary-key-constraints/get", user);
        context = context with
        {
            DatasourceId = datasourceId,
            TableName = tableName,
            SchemaName = schemaName,
            HttpMethod = "GET",
        };
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
    public static DapperMaticOperation ForPrimaryKeyCreate(
        string datasourceId,
        string tableName,
        object requestBody,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("primary-key-constraints/post", user);
        context = context with
        {
            DatasourceId = datasourceId,
            TableName = tableName,
            SchemaName = schemaName,
            RequestBody = requestBody,
            HttpMethod = "POST",
        };
        return context;
    }

    /// <summary>
    /// Creates context for primary key drop operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static DapperMaticOperation ForPrimaryKeyDrop(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("primary-key-constraints/delete", user);
        context = context with
        {
            DatasourceId = datasourceId,
            TableName = tableName,
            SchemaName = schemaName,
            HttpMethod = "DELETE",
        };
        return context;
    }

    /// <summary>
    /// Creates context for foreign key list operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static DapperMaticOperation ForForeignKeyList(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("foreign-key-constraints/list", user);
        context = context with
        {
            DatasourceId = datasourceId,
            TableName = tableName,
            SchemaName = schemaName,
            HttpMethod = "GET",
        };
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
    public static DapperMaticOperation ForForeignKeyGet(
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("foreign-key-constraints/get", user);
        context = context with
        {
            DatasourceId = datasourceId,
            TableName = tableName,
            ConstraintName = constraintName,
            SchemaName = schemaName,
            HttpMethod = "GET",
        };
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
    public static DapperMaticOperation ForForeignKeyCreate(
        string datasourceId,
        string tableName,
        object requestBody,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("foreign-key-constraints/post", user);
        context = context with
        {
            DatasourceId = datasourceId,
            TableName = tableName,
            SchemaName = schemaName,
            RequestBody = requestBody,
            HttpMethod = "POST",
        };
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
    public static DapperMaticOperation ForForeignKeyDrop(
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("foreign-key-constraints/delete", user);
        context = context with
        {
            DatasourceId = datasourceId,
            TableName = tableName,
            ConstraintName = constraintName,
            SchemaName = schemaName,
            HttpMethod = "DELETE",
        };
        return context;
    }

    /// <summary>
    /// Creates context for check constraint list operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static DapperMaticOperation ForCheckConstraintList(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("check-constraints/list", user);
        context = context with
        {
            DatasourceId = datasourceId,
            TableName = tableName,
            SchemaName = schemaName,
            HttpMethod = "GET",
        };
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
    public static DapperMaticOperation ForCheckConstraintGet(
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("check-constraints/get", user);
        context = context with
        {
            DatasourceId = datasourceId,
            TableName = tableName,
            ConstraintName = constraintName,
            SchemaName = schemaName,
            HttpMethod = "GET",
        };
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
    public static DapperMaticOperation ForCheckConstraintCreate(
        string datasourceId,
        string tableName,
        object requestBody,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("check-constraints/post", user);
        context = context with
        {
            DatasourceId = datasourceId,
            TableName = tableName,
            SchemaName = schemaName,
            RequestBody = requestBody,
            HttpMethod = "POST",
        };
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
    public static DapperMaticOperation ForCheckConstraintDrop(
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("check-constraints/delete", user);
        context = context with
        {
            DatasourceId = datasourceId,
            TableName = tableName,
            ConstraintName = constraintName,
            SchemaName = schemaName,
            HttpMethod = "DELETE",
        };
        return context;
    }

    /// <summary>
    /// Creates context for unique constraint list operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static DapperMaticOperation ForUniqueConstraintList(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("unique-constraints/list", user);
        context = context with
        {
            DatasourceId = datasourceId,
            TableName = tableName,
            SchemaName = schemaName,
            HttpMethod = "GET",
        };
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
    public static DapperMaticOperation ForUniqueConstraintGet(
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("unique-constraints/get", user);
        context = context with
        {
            DatasourceId = datasourceId,
            TableName = tableName,
            ConstraintName = constraintName,
            SchemaName = schemaName,
            HttpMethod = "GET",
        };
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
    public static DapperMaticOperation ForUniqueConstraintCreate(
        string datasourceId,
        string tableName,
        object requestBody,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("unique-constraints/post", user);
        context = context with
        {
            DatasourceId = datasourceId,
            TableName = tableName,
            SchemaName = schemaName,
            RequestBody = requestBody,
            HttpMethod = "POST",
        };
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
    public static DapperMaticOperation ForUniqueConstraintDrop(
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("unique-constraints/delete", user);
        context = context with
        {
            DatasourceId = datasourceId,
            TableName = tableName,
            ConstraintName = constraintName,
            SchemaName = schemaName,
            HttpMethod = "DELETE",
        };
        return context;
    }

    /// <summary>
    /// Creates context for default constraint list operations.
    /// </summary>
    /// <param name="datasourceId">The datasource identifier.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">Optional schema name.</param>
    /// <param name="user">Optional user context.</param>
    public static DapperMaticOperation ForDefaultConstraintList(
        string datasourceId,
        string tableName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("default-constraints/list", user);
        context = context with
        {
            DatasourceId = datasourceId,
            TableName = tableName,
            SchemaName = schemaName,
            HttpMethod = "GET",
        };
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
    public static DapperMaticOperation ForDefaultConstraintGet(
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("default-constraints/get", user);
        context = context with
        {
            DatasourceId = datasourceId,
            TableName = tableName,
            ConstraintName = constraintName,
            SchemaName = schemaName,
            HttpMethod = "GET",
        };
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
    public static DapperMaticOperation ForDefaultConstraintOnColumnGet(
        string datasourceId,
        string tableName,
        string columnName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("default-constraints/get", user);
        context = context with
        {
            DatasourceId = datasourceId,
            TableName = tableName,
            ColumnNames = [columnName],
            SchemaName = schemaName,
            HttpMethod = "GET",
        };
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
    public static DapperMaticOperation ForDefaultConstraintCreate(
        string datasourceId,
        string tableName,
        object requestBody,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("default-constraints/post", user);
        context = context with
        {
            DatasourceId = datasourceId,
            TableName = tableName,
            SchemaName = schemaName,
            RequestBody = requestBody,
            HttpMethod = "POST",
        };
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
    public static DapperMaticOperation ForDefaultConstraintDrop(
        string datasourceId,
        string tableName,
        string constraintName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("default-constraints/delete", user);
        context = context with
        {
            DatasourceId = datasourceId,
            TableName = tableName,
            ConstraintName = constraintName,
            SchemaName = schemaName,
            HttpMethod = "DELETE",
        };
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
    public static DapperMaticOperation ForDefaultConstraintOnColumnDrop(
        string datasourceId,
        string tableName,
        string columnName,
        string? schemaName = null,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("default-constraints/delete", user);
        context = context with
        {
            DatasourceId = datasourceId,
            TableName = tableName,
            ColumnNames = [columnName],
            SchemaName = schemaName,
            HttpMethod = "DELETE",
        };
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
    public static DapperMaticOperation ForDataTypeGet(
        string datasourceId,
        bool includeCustomTypes = false,
        ClaimsPrincipal? user = null
    )
    {
        var context = CreateContext("datatypes/get", user);
        context = context with { DatasourceId = datasourceId };
        context = context.WithProperty("IncludeCustomTypes", includeCustomTypes);
        context = context with { HttpMethod = "GET" };
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
    public static DapperMaticOperation ForCustomOperation(
        string operation,
        Dictionary<string, object>? properties = null,
        ClaimsPrincipal? user = null
    )
    {
        return CreateContext(operation, user) with
        {
            HttpMethod = "POST", // Default for custom operations
            Properties = new Dictionary<string, object>(properties ?? [], StringComparer.OrdinalIgnoreCase),
        };
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
    public static DapperMaticOperation ForHttpOperation(
        string operation,
        string httpMethod = "POST",
        string endpointPath = "/api/dappermatic",
        Dictionary<string, string>? queryParams = null,
        Dictionary<string, string>? headers = null,
        ClaimsPrincipal? user = null
    )
    {
        return CreateContext(operation, user) with
        {
            HttpMethod = httpMethod,
            EndpointPath = endpointPath,
            QueryParameters = (queryParams ?? []).ToDictionary(p => p.Key, p => new StringValues(p.Value), StringComparer.OrdinalIgnoreCase),
            HeaderValues = new Dictionary<string, string>(headers ?? [], StringComparer.OrdinalIgnoreCase),
        };
    }

    #endregion
}
