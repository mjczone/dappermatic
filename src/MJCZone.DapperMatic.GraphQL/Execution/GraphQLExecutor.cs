// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Text.Json;
using GraphQL;
using GraphQL.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore;
using MJCZone.DapperMatic.AspNetCore.Changes;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.AspNetCore.Services;
using MJCZone.DapperMatic.Dml.Rest;
using MJCZone.DapperMatic.GraphQL.Schema;

namespace MJCZone.DapperMatic.GraphQL.Execution;

/// <summary>
/// Runs root fields through DapperMatic's engine, as the caller: each field one query or write, authorized as the
/// REST endpoint for it is.
/// </summary>
internal static class GraphQLExecutor
{
    /// <summary>Reads a table's rows.</summary>
    /// <param name="context">The field's context.</param>
    /// <param name="table">The table.</param>
    /// <returns>The rows.</returns>
    public static ValueTask<object?> ListAsync(IResolveFieldContext context, GraphQLTable table) =>
        RunAsync(context, async (request, service, operation) =>
        {
            var query = GraphQLQueries.Rows(context, request.Model!, table);
            var (page, _) = await service.PageRowsAsync(operation, request.DatasourceId, table.Table.Name, table.Model.View, query, context.CancellationToken).ConfigureAwait(false);
            return (object?)page.Rows.ConvertAll(r => new GraphQLRow(table, r));
        });

    /// <summary>Reads a page of a table's rows, as a Relay connection.</summary>
    /// <param name="context">The field's context.</param>
    /// <param name="table">The table.</param>
    /// <returns>The page.</returns>
    public static ValueTask<object?> ConnectionAsync(IResolveFieldContext context, GraphQLTable table) =>
        RunAsync(context, async (request, service, operation) =>
        {
            var query = GraphQLQueries.Connection(context, request.Model!, table);
            var (page, _) = await service.PageRowsAsync(operation, request.DatasourceId, table.Table.Name, view: false, query, context.CancellationToken).ConfigureAwait(false);
            return (object?)new GraphQLPage
            {
                Edges = [.. page.Rows.Select((r, i) => new GraphQLEdge(new GraphQLRow(table, r), page.Cursors![i]))],
                HasNextPage = page.HasMore,
                TotalCount = page.Count,
            };
        });

    /// <summary>Reads a row by its key.</summary>
    /// <param name="context">The field's context.</param>
    /// <param name="table">The table.</param>
    /// <returns>The row, or null.</returns>
    public static ValueTask<object?> ByKeyAsync(IResolveFieldContext context, GraphQLTable table) =>
        RunAsync(context, async (request, service, operation) =>
        {
            var query = GraphQLQueries.Rows(context, request.Model!, table) with { Filter = GraphQLQueries.ByKey(context.Arguments, table), Top = 1 };
            var (page, _) = await service.PageRowsAsync(operation, request.DatasourceId, table.Table.Name, view: false, query, context.CancellationToken).ConfigureAwait(false);
            return page.Rows.Count == 0 ? null : (object?)new GraphQLRow(table, page.Rows[0]);
        });

    /// <summary>
    /// Reads a row by its global id: null for an id of a table outside the caller's schema, or of a row they may not
    /// read.
    /// </summary>
    /// <param name="context">The field's context.</param>
    /// <returns>The row, or null.</returns>
    public static ValueTask<object?> NodeAsync(IResolveFieldContext context) =>
        RunAsync(context, async (request, service, operation) =>
        {
            if (GraphQLQueries.Value(context.Arguments, "id") is not string id
                || GraphQLNodeIds.Decode(id) is not { } decoded
                || !request.Model!.Tables.TryGetValue(decoded.Table, out var table)
                || !table.IsNode
                || decoded.Keys.Length != table.Table.Keys.Count)
            {
                return null;
            }

            var keys = table.Table.Keys.Select((k, i) => (RestNode)new RestBinary("eq", new RestPath([k]), new RestLiteral(RestJson.Scalar(decoded.Keys[i])))).Aggregate((a, b) => new RestBinary("and", a, b));
            var query = GraphQLQueries.Node(context, request.Model, table) with { Filter = keys, Top = 1 };
            var (page, _) = await service.PageRowsAsync(operation, request.DatasourceId, table.Table.Name, view: false, query, context.CancellationToken).ConfigureAwait(false);
            return page.Rows.Count == 0 ? null : (object?)new GraphQLRow(table, page.Rows[0]);
        });

    /// <summary>
    /// Writes rows, in the request's transaction: what the field asks of its payload is read back as the caller, after
    /// an insert or update, before a delete.
    /// </summary>
    /// <param name="context">The field's context.</param>
    /// <param name="table">The table.</param>
    /// <param name="kind">The write.</param>
    /// <returns>The payload.</returns>
    public static ValueTask<object?> MutateAsync(IResolveFieldContext context, GraphQLTable table, GraphQLFieldKind kind) =>
        RunAsync(context, async (request, service, operation) =>
        {
            var session = request.Writes ?? throw new InvalidOperationException("A mutation's fields write in its transaction, which was not begun.");
            var cancellationToken = context.CancellationToken;
            var write = WriteKind(kind);
            var (engine, described) = await session.OpenAsync(table.Table.Name, write, cancellationToken).ConfigureAwait(false);

            // A failure is audited as the write the table was opened for.
            operation.State.Received ??= session.OperationOf(table.Table.Name, write);
            var (query, read, _) = GraphQLQueries.Payload(context, request.Model!, table);
            read &= table.Type is not null && described.Keys.Count > 0;
            var limit = context.RequestServices!.GetRequiredService<IOptions<DapperMaticOptions>>().Value.QueryLimits.MaxExpandedRows;
            var keys = new List<IReadOnlyDictionary<string, object?>>();
            List<(Dictionary<string, object?> Row, string? Cursor)> rows = [];
            int affected;
            switch (kind)
            {
                case GraphQLFieldKind.Insert or GraphQLFieldKind.Save:
                    var type = context.FieldDefinition.Arguments!.Find("rows")!.ResolvedType!;
                    var given = ((IEnumerable<object?>)GraphQLQueries.Value(context.Arguments, "rows")!).Select(r => (IReadOnlyDictionary<string, object?>)GraphQLQueries.Input(r, type)).ToList();
                    var written = await engine.SaveAsync(described, given, kind == GraphQLFieldKind.Save, session.Transaction, cancellationToken).ConfigureAwait(false);
                    affected = written.Count;
                    keys.AddRange(written.Select(w => (IReadOnlyDictionary<string, object?>)described.Keys.ToDictionary(k => k, k => w.GetValueOrDefault(k), StringComparer.OrdinalIgnoreCase)));
                    break;
                case GraphQLFieldKind.Update or GraphQLFieldKind.UpdateByKey:
                    var set = GraphQLQueries.Input(GraphQLQueries.Value(context.Arguments, "set"), context.FieldDefinition.Arguments!.Find("set")!.ResolvedType!);
                    affected = await engine.UpdateAsync(described, set, Condition(context, table, kind), session.Transaction, cancellationToken, keys: read ? keys : null).ConfigureAwait(false);
                    break;
                default:
                    if (read)
                    {
                        // The rows as they were: locked, read as the caller, then deleted by their keys.
                        var deleted = await engine.DeleteReadingAsync(
                                described,
                                Condition(context, table, kind),
                                session.Transaction,
                                async locked => rows = await session.ReadAsync(table.Table.Name, locked, query, limit, cancellationToken).ConfigureAwait(false),
                                cancellationToken
                            )
                            .ConfigureAwait(false);
                        affected = deleted.Count;
                    }
                    else
                    {
                        affected = await engine.DeleteAsync(described, Condition(context, table, kind), session.Transaction, cancellationToken).ConfigureAwait(false);
                    }

                    break;
            }

            var deleting = kind is GraphQLFieldKind.Delete or GraphQLFieldKind.DeleteByKey;
            if (read && !deleting && keys.Count > 0)
            {
                rows = await session.ReadAsync(table.Table.Name, keys, query, limit, cancellationToken).ConfigureAwait(false);
            }

            session.Written(table.Table.Name, write, $"{Verb(kind)} {affected} rows of table '{table.Table.Name}' for datasource '{session.DatasourceId}' (GraphQL)");
            return (object?)new GraphQLPage
            {
                Affected = affected,
                Edges = [.. rows.Select(r => new GraphQLEdge(new GraphQLRow(table, r.Row), r.Cursor ?? string.Empty))],
            };
        });

    /// <summary>Runs a declared function.</summary>
    /// <param name="context">The field's context.</param>
    /// <param name="function">The function.</param>
    /// <returns>Its rows, each a JSON object.</returns>
    public static ValueTask<object?> FunctionAsync(IResolveFieldContext context, DapperMaticFunction function) =>
        RunAsync(context, async (request, service, operation) =>
        {
            var arguments = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var parameter in function.Parameters.Where(p => p.Value is null))
            {
                if (context.Arguments?.TryGetValue(GraphQLNames.Of(parameter.Name), out var value) == true && value.Source != ArgumentSource.FieldDefault)
                {
                    arguments[parameter.Name] = value.Value;
                }
            }

            var asFunction = operation with
            {
                Operation = "functions/post",
                RouteValues = new Dictionary<string, string>(operation.RouteValues, StringComparer.OrdinalIgnoreCase) { ["functionName"] = function.Name },
            };
            // In a mutation, in its transaction: all or none, with the other fields.
            var given = JsonSerializer.SerializeToElement(arguments);
            var result = request.Writes is { } writes
                ? await service.RunFunctionAsync(asFunction, request.DatasourceId, function.Name, given, writes, context.CancellationToken).ConfigureAwait(false)
                : await service.ExecuteFunctionAsync(asFunction, request.DatasourceId, function.Name, given, context.CancellationToken).ConfigureAwait(false);
            return (object?)result.Result;
        });

    /// <summary>
    /// Runs a live query: its results now, and again, as the caller, each time a table it reads changes; sent only when
    /// they differ.
    /// </summary>
    /// <param name="context">The field's context.</param>
    /// <param name="table">The table.</param>
    /// <returns>The stream of results.</returns>
    public static async ValueTask<IObservable<object?>> LiveAsync(IResolveFieldContext context, GraphQLTable table)
    {
        var request = Request(context);
        var services = context.RequestServices!;
        var hub = services.GetRequiredService<DapperMaticRealtimeHub>();
        var options = services.GetRequiredService<IOptions<DapperMaticOptions>>().Value.Realtime;
        var query = GraphQLQueries.Rows(context, request.Model!, table);

        // The first run, as a query: a refusal ends the subscription before it starts.
        async Task<(List<Dictionary<string, object?>> Rows, List<(string? Schema, string Table)> Tables)> RunAsync(IServiceProvider scope, CancellationToken cancellationToken)
        {
            var service = Service(scope);
            var operation = request.Operation with { State = new() };
            var (page, _) = await service.PageRowsAsync(operation, request.DatasourceId, table.Table.Name, table.Model.View, query, cancellationToken).ConfigureAwait(false);
            return (page.Rows, service.SubscribedTables(operation, request.DatasourceId));
        }

        List<Dictionary<string, object?>> rows;
        List<(string? Schema, string Table)> tables;
        var since = hub.Notified;
        try
        {
            (rows, tables) = await RunAsync(services, context.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw GraphQLErrors.Of(ex, services);
        }

        // The subscribe operation's scope ends once the subscription is set up: each run makes a scope of its own.
        var scopes = services.GetRequiredService<IServiceScopeFactory>();
        return new LiveQuery(
            () => hub.Subscribe(request.DatasourceId, tables, since),
            rows,
            options.Debounce,
            async cancellationToken =>
            {
                using var scope = scopes.CreateScope();
                try
                {
                    // Each run is the caller's, checked again: their session may have ended, or their tenant changed.
                    request.Use(await request.Session.CheckAsync(cancellationToken).ConfigureAwait(false));
                    DapperMaticCallerSession.Current = request.Session;
                    return (await RunAsync(scope.ServiceProvider, cancellationToken).ConfigureAwait(false)).Rows;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    throw GraphQLErrors.Of(ex, scope.ServiceProvider);
                }
            },
            found => found.ConvertAll(r => new GraphQLRow(table, r)),
            ex => ex is ExecutionError error ? error : new ExecutionError("An unexpected error occurred.") { Code = "INTERNAL_SERVER_ERROR" }
        );
    }

    private static string WriteKind(GraphQLFieldKind kind) =>
        kind switch
        {
            GraphQLFieldKind.Insert => "insert-rows",
            GraphQLFieldKind.Save => "save-rows",
            GraphQLFieldKind.Update or GraphQLFieldKind.UpdateByKey => "update-rows",
            _ => "delete-rows",
        };

    private static string Verb(GraphQLFieldKind kind) =>
        kind switch
        {
            GraphQLFieldKind.Insert => "Inserted",
            GraphQLFieldKind.Save => "Saved",
            GraphQLFieldKind.Update or GraphQLFieldKind.UpdateByKey => "Updated",
            _ => "Deleted",
        };

    // The rows a write changes: by key, or by its where and filter, at least one of them given.
    private static RestNode Condition(IResolveFieldContext context, GraphQLTable table, GraphQLFieldKind kind) =>
        kind is GraphQLFieldKind.UpdateByKey or GraphQLFieldKind.DeleteByKey
            ? GraphQLQueries.ByKey(context.Arguments, table)
            : GraphQLQueries.Condition(context.Arguments, table)
                ?? throw new ArgumentException("Say which rows: give where, filter, or both (filter: \"true\" for every row).");

    private static DapperMaticGraphQLContext Request(IResolveFieldContext context) =>
        context.UserContext as DapperMaticGraphQLContext ?? throw new InvalidOperationException("DapperMatic's GraphQL fields run within its endpoint.");

    private static DapperMaticService Service(IServiceProvider services) =>
        services.GetRequiredService<IDapperMaticService>() as DapperMaticService
            ?? throw new InvalidOperationException("DapperMatic's GraphQL endpoint needs DapperMatic's own service.");

    // Runs a field as the caller, an operation of its own; what goes wrong becomes an error with a code.
    private static async ValueTask<object?> RunAsync(
        IResolveFieldContext context,
        Func<DapperMaticGraphQLContext, DapperMaticService, DapperMaticOperation, Task<object?>> run
    )
    {
        var request = Request(context);
        var operation = request.Operation with { State = new() };
        try
        {
            return await run(request, Service(context.RequestServices!), operation).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not ExecutionError)
        {
            // Audited as an endpoint's failure is: the field's error is GraphQL's answer, not an exception's.
            await GraphQLErrors.AuditAsync(context.RequestServices!, operation, ex).ConfigureAwait(false);
            throw GraphQLErrors.Of(ex, context.RequestServices!);
        }
    }
}
