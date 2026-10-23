// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Models.Responses;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.AspNetCore.Services;
using MJCZone.DapperMatic.AspNetCore.Validation;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace MJCZone.DapperMatic.AspNetCore.Mcp;

/// <summary>
/// DapperMatic as tools for AI assistants, over the Model Context Protocol: each call runs as the request's signed-in
/// caller, held to their tenant and to the policies, exactly as the web API's endpoints are.
/// </summary>
[McpServerToolType]
public sealed class DapperMaticMcpTools
{
    /// <summary>What the server tells an assistant about the tools.</summary>
    internal const string Instructions =
        "DapperMatic serves a database's tables as the signed-in user may use them. Start with list_datasources, then "
        + "describe_datasource for the tables, columns, relations, allowed actions and functions. Filters use OData syntax: "
        + "total gt 100 and status eq 'open', customer/country eq 'FR', lines/any(l: l/qty gt 5), contains(name,'acme'). "
        + "Every call is held to the user's permissions; a refusal says so.";

    private readonly IHttpContextAccessor _http;

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticMcpTools"/> class.
    /// </summary>
    /// <param name="http">The current request, whose caller the tools run as.</param>
    public DapperMaticMcpTools(IHttpContextAccessor http)
    {
        _http = http;
    }

    /// <summary>Lists the datasources the caller may see.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Their ids and descriptions.</returns>
    [McpServerTool(Name = "list_datasources", ReadOnly = true, Idempotent = true)]
    [Description("Lists the datasources (databases) the user may see: their ids, used by every other tool.")]
    public Task<object> ListDatasourcesAsync(CancellationToken cancellationToken = default) =>
        RunAsync(
            "datasources/list",
            null,
            null,
            null,
            async (service, context) =>
                (object)(await service.GetDatasourcesAsync(context, cancellationToken).ConfigureAwait(false))
                    .Select(d => new { id = d.Id, displayName = d.DisplayName, description = d.Description, provider = d.Provider })
                    .ToList(),
            cancellationToken
        );

    /// <summary>Describes what the caller may do in a datasource.</summary>
    /// <param name="datasourceId">The datasource.</param>
    /// <param name="schema">The schema, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The caller's OpenAPI document of the datasource.</returns>
    [McpServerTool(Name = "describe_datasource", ReadOnly = true, Idempotent = true)]
    [Description(
        "Describes what the user may do in a datasource, as an OpenAPI 3.1 document: each table and view it may use, its columns "
            + "and their types, the relations it may expand, the row actions allowed (query, insert, update, delete), and the "
            + "functions it may run with their arguments."
    )]
    public Task<object> DescribeDatasourceAsync(
        [Description("The datasource id, from list_datasources.")] string datasourceId,
        [Description("A schema, or leave out for the default one.")] string? schema = null,
        CancellationToken cancellationToken = default
    ) =>
        RunAsync(
            "openapi/list",
            datasourceId,
            schema,
            null,
            async (service, context) =>
            {
                var http = _http.HttpContext!;
                var options = http.RequestServices.GetService<Microsoft.Extensions.Options.IOptions<DapperMaticOptions>>()?.Value;
                var server = $"{http.Request.PathBase}/{(options?.BasePath ?? "/api/dm").Trim('/')}/d/{datasourceId}{(schema is null ? string.Empty : "/s/" + schema)}";
                return (object)await service.GetOpenApiDocumentAsync(context, datasourceId, schema, server, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken
        );

    /// <summary>Reads rows of a table or view.</summary>
    /// <param name="datasourceId">The datasource.</param>
    /// <param name="table">The table or view.</param>
    /// <param name="filter">An OData condition, or null.</param>
    /// <param name="orderby">An OData ordering, or null.</param>
    /// <param name="select">Columns, comma-separated, or null.</param>
    /// <param name="expand">Relations to read with each row, or null.</param>
    /// <param name="top">The most rows.</param>
    /// <param name="skip">The rows to skip.</param>
    /// <param name="count">Whether to count every matching row.</param>
    /// <param name="after">A cursor of the page before, or null.</param>
    /// <param name="search">Words to search for, or null.</param>
    /// <param name="apply">An OData $apply grouping, or null.</param>
    /// <param name="view">Whether it is a view.</param>
    /// <param name="schema">The schema, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rows, and the page.</returns>
    [McpServerTool(Name = "query_rows", ReadOnly = true, Idempotent = true)]
    [Description("Reads rows of a table or view, with OData-style options. Returns the rows, and pagination with nextCursor for the next page.")]
    public Task<object> QueryRowsAsync(
        [Description("The datasource id.")] string datasourceId,
        [Description("The table or view.")] string table,
        [Description("An OData condition: total gt 100 and customer/country eq 'FR'.")] string? filter = null,
        [Description("An OData ordering: placed desc, id.")] string? orderby = null,
        [Description("Columns to read, comma-separated; leave out for all.")] string? select = null,
        [Description("Relations to read with each row, comma-separated: customer,lines($top=5).")] string? expand = null,
        [Description("The most rows to read; 100 when left out.")] int? top = null,
        [Description("The rows to skip.")] int? skip = null,
        [Description("Whether to count every row the condition matches.")] bool count = false,
        [Description("The nextCursor of the page before, to read the next page.")] string? after = null,
        [Description("Words every row must contain, in the table's searchable columns.")] string? search = null,
        [Description("An OData $apply: groupby((status),aggregate(total with sum as sum)).")] string? apply = null,
        [Description("True to read a view rather than a table.")] bool view = false,
        [Description("A schema, or leave out for the default one.")] string? schema = null,
        CancellationToken cancellationToken = default
    ) =>
        RunAsync(
            view ? "views/query" : "tables/query",
            datasourceId,
            schema,
            table,
            async (service, context) =>
            {
                if (view)
                {
                    context = context with { ViewName = table, TableName = null };
                }

                var request = new QueryDto
                {
                    Filter = filter,
                    Order = orderby,
                    Select = select,
                    Expand = expand,
                    Take = top ?? 100,
                    Skip = skip ?? 0,
                    IncludeTotal = count,
                    After = after,
                    Search = search,
                    Apply = apply,
                    SchemaName = schema,
                };
                context = context with { RequestBody = request };
                var result = view
                    ? await service.QueryViewAsync(context, datasourceId, table, request, schema, cancellationToken).ConfigureAwait(false)
                    : await service.QueryTableAsync(context, datasourceId, table, request, schema, cancellationToken).ConfigureAwait(false);
                return (object)new QueryResponse(result.Data) { Pagination = result.Pagination, Fields = result.Fields };
            },
            cancellationToken
        );

    /// <summary>Inserts rows.</summary>
    /// <param name="datasourceId">The datasource.</param>
    /// <param name="table">The table.</param>
    /// <param name="rows">The rows, an array of objects.</param>
    /// <param name="schema">The schema, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rows written, with their keys.</returns>
    [McpServerTool(Name = "insert_rows", Destructive = false)]
    [Description("Inserts rows into a table, all or none: an array of objects of column values. A related row (object) or rows (array) under a relation's name are written with it. Returns the rows as written, with their keys.")]
    public Task<object> InsertRowsAsync(
        [Description("The datasource id.")] string datasourceId,
        [Description("The table.")] string table,
        [Description("The rows: [{\"name\":\"Acme\",\"country\":\"FR\"}].")] JsonElement rows,
        [Description("A schema, or leave out for the default one.")] string? schema = null,
        CancellationToken cancellationToken = default
    ) =>
        RunAsync(
            "tables/insert-rows",
            datasourceId,
            schema,
            table,
            async (service, context) =>
            {
                if (rows.ValueKind != JsonValueKind.Array)
                {
                    throw new ArgumentException("The rows are an array of objects.");
                }

                var request = new RowsInsertDto { Rows = [.. rows.EnumerateArray().Select(r => r.Deserialize<Dictionary<string, JsonElement>>()!)] };
                context = context with { RequestBody = request };
                return (object)await service.SaveTableRowsAsync(context, datasourceId, table, request, upsert: false, schema, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken
        );

    /// <summary>Updates rows.</summary>
    /// <param name="datasourceId">The datasource.</param>
    /// <param name="table">The table.</param>
    /// <param name="set">The columns to set.</param>
    /// <param name="filter">The rows to change.</param>
    /// <param name="schema">The schema, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many rows changed.</returns>
    [McpServerTool(Name = "update_rows", Destructive = true)]
    [Description("Sets columns of the rows of a table that match an OData condition. Returns how many rows changed.")]
    public Task<object> UpdateRowsAsync(
        [Description("The datasource id.")] string datasourceId,
        [Description("The table.")] string table,
        [Description("The columns and their new values: {\"status\":\"shipped\"}.")] JsonElement set,
        [Description("The OData condition of the rows to change: id eq 17. Use true for every row.")] string filter,
        [Description("A schema, or leave out for the default one.")] string? schema = null,
        CancellationToken cancellationToken = default
    ) =>
        RunAsync(
            "tables/update-rows",
            datasourceId,
            schema,
            table,
            async (service, context) =>
            {
                var request = new RowsUpdateDto { Set = set.Deserialize<Dictionary<string, JsonElement>>() ?? [], Filter = filter };
                context = context with { RequestBody = request };
                return (object)new RowsResponse(await service.UpdateTableRowsAsync(context, datasourceId, table, request, schema, cancellationToken).ConfigureAwait(false));
            },
            cancellationToken
        );

    /// <summary>Deletes rows.</summary>
    /// <param name="datasourceId">The datasource.</param>
    /// <param name="table">The table.</param>
    /// <param name="filter">The rows to delete.</param>
    /// <param name="schema">The schema, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many rows were deleted.</returns>
    [McpServerTool(Name = "delete_rows", Destructive = true)]
    [Description("Deletes the rows of a table that match an OData condition. Returns how many rows were deleted.")]
    public Task<object> DeleteRowsAsync(
        [Description("The datasource id.")] string datasourceId,
        [Description("The table.")] string table,
        [Description("The OData condition of the rows to delete: id eq 17.")] string filter,
        [Description("A schema, or leave out for the default one.")] string? schema = null,
        CancellationToken cancellationToken = default
    ) =>
        RunAsync(
            "tables/delete-rows",
            datasourceId,
            schema,
            table,
            async (service, context) =>
            {
                var request = new RowsDeleteDto { Filter = filter };
                context = context with { RequestBody = request };
                return (object)new RowsResponse(await service.DeleteTableRowsAsync(context, datasourceId, table, request, schema, cancellationToken).ConfigureAwait(false));
            },
            cancellationToken
        );

    /// <summary>Runs a declared function.</summary>
    /// <param name="datasourceId">The datasource.</param>
    /// <param name="function">The function.</param>
    /// <param name="arguments">Its arguments, an object.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rows it returned.</returns>
    [McpServerTool(Name = "run_function")]
    [Description("Runs a function the application declares, with arguments by name (describe_datasource lists them). Returns its rows.")]
    public Task<object> RunFunctionAsync(
        [Description("The datasource id.")] string datasourceId,
        [Description("The function's name.")] string function,
        [Description("The arguments: {\"min\": 1000}.")] JsonElement? arguments = null,
        CancellationToken cancellationToken = default
    ) =>
        RunAsync(
            "functions/post",
            datasourceId,
            null,
            null,
            async (service, context) =>
            {
                context = context with
                {
                    RouteValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["datasourceId"] = datasourceId, ["functionName"] = function },
                    RequestBody = arguments,
                };
                return (object)await service.ExecuteFunctionAsync(context, datasourceId, function, arguments, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken
        );

    /// <summary>
    /// Adds the tools to an MCP server's, with the server's name and instructions where the application gave none.
    /// </summary>
    /// <param name="options">The MCP server's options.</param>
    /// <param name="services">The application's services.</param>
    internal static void AddTo(McpServerOptions options, IServiceProvider services)
    {
        options.ServerInfo ??= new ModelContextProtocol.Protocol.Implementation
        {
            Name = "DapperMatic",
            Version = typeof(DapperMaticMcpTools).Assembly.GetName().Version?.ToString() ?? "0",
        };
        options.ServerInstructions ??= Instructions;
        options.ToolCollection ??= [];
        foreach (var method in typeof(DapperMaticMcpTools).GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
        {
            if (System.Reflection.CustomAttributeExtensions.GetCustomAttribute<McpServerToolAttribute>(method) is not null)
            {
                options.ToolCollection.Add(
                    McpServerTool.Create(
                        method,
                        request => ActivatorUtilities.CreateInstance<DapperMaticMcpTools>(request.Services ?? services),
                        new McpServerToolCreateOptions { Services = services }
                    )
                );
            }
        }
    }

    // Runs a tool as the request's caller: the operation named as its endpoint names it, the caller held to their
    // tenant; a refusal or a request the API would answer 4xx is told as the tool's error.
    private async Task<object> RunAsync(
        string operation,
        string? datasourceId,
        string? schema,
        string? table,
        Func<IDapperMaticService, DapperMaticOperation, Task<object>> run,
        CancellationToken cancellationToken
    )
    {
        var http = _http.HttpContext ?? throw new McpException("DapperMatic's tools run within an HTTP request.");
        var services = http.RequestServices;
        var context = DapperMaticOperation.Of(http) with
        {
            Operation = operation,
            DatasourceId = datasourceId,
            SchemaName = schema,
            TableName = table,
            ViewName = null,
            HttpMethod = "POST",
            Properties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["McpTool"] = operation },
            State = new(),
        };
        try
        {
            // The request's session, built before the tool ran; the tool's operation held to its tenant.
            if (services.GetService<DapperMaticAccess>() is { } access)
            {
                context = access.Hold(await access.GetSessionAsync(http, cancellationToken).ConfigureAwait(false), context);
            }

            return await run(services.GetRequiredService<IDapperMaticService>(), context).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or ArgumentException or KeyNotFoundException or DuplicateKeyException or NotSupportedException or InvalidOperationException)
        {
            throw new McpException(
                ex switch
                {
                    UnauthorizedAccessException => "Forbidden: " + ex.Message,
                    KeyNotFoundException => "Not found: " + ex.Message,
                    _ => ex.Message,
                },
                ex
            );
        }
    }
}
