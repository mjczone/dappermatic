// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Data.Common;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using MJCZone.DapperMatic.AspNetCore.Models.Dtos;
using MJCZone.DapperMatic.AspNetCore.Models.Responses;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.Dml.Rest;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Services;

/// <summary>
/// Querying and writing rows of tables and views, through DapperMatic's query tree: OData-shaped conditions,
/// relations declared in <see cref="DapperMaticOptions.Relations"/>, and every name checked against the table.
/// </summary>
public partial class DapperMaticService
{
    /// <inheritdoc />
    public virtual async Task<int> InsertTableRowsAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        RowsInsertDto request,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        await AssertPermissionsAsync(context).ConfigureAwait(false);
        schemaName = NormalizeSchemaName(schemaName);
        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            var (engine, table) = await OpenRowsAsync(context, connection, datasourceId, schemaName, tableName, cancellationToken).ConfigureAwait(false);
            var rows = (request.Rows ?? throw new ArgumentException("The request has no rows.")).Select(r => (IReadOnlyDictionary<string, object?>)r.ToDictionary(p => p.Key, p => RestJson.Scalar(p.Value))).ToList();
            var inserted = await engine.InsertAsync(table, rows, null, cancellationToken, request.DryRun).ConfigureAwait(false);
            await WrittenAsync(datasourceId, connection, [engine], request.DryRun, cancellationToken).ConfigureAwait(false);
            await LogAuditEventAsync(context, true, (request.DryRun ? "Dry run: " : string.Empty) + $"Inserted {inserted} rows into table '{tableName}' for datasource '{datasourceId}'")
                .ConfigureAwait(false);
            return inserted;
        }
    }

    /// <inheritdoc />
    public virtual async Task<int> UpdateTableRowsAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        RowsUpdateDto request,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        await AssertPermissionsAsync(context).ConfigureAwait(false);
        schemaName = NormalizeSchemaName(schemaName);
        var filter = Condition(request.Filter, request.Where);
        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            var (engine, table) = await OpenRowsAsync(context, connection, datasourceId, schemaName, tableName, cancellationToken).ConfigureAwait(false);
            var set = (request.Set ?? throw new ArgumentException("The request has nothing to set.")).ToDictionary(p => p.Key, p => RestJson.Scalar(p.Value));
            var updated = await engine.UpdateAsync(table, set, filter, null, cancellationToken, request.DryRun).ConfigureAwait(false);
            await WrittenAsync(datasourceId, connection, [engine], request.DryRun, cancellationToken).ConfigureAwait(false);
            await LogAuditEventAsync(context, true, (request.DryRun ? "Dry run: " : string.Empty) + $"Updated {updated} rows of table '{tableName}' for datasource '{datasourceId}'")
                .ConfigureAwait(false);
            return updated;
        }
    }

    /// <inheritdoc />
    public virtual async Task<int> DeleteTableRowsAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        RowsDeleteDto request,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        await AssertPermissionsAsync(context).ConfigureAwait(false);
        schemaName = NormalizeSchemaName(schemaName);
        var filter = Condition(request.Filter, request.Where);
        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            var (engine, table) = await OpenRowsAsync(context, connection, datasourceId, schemaName, tableName, cancellationToken).ConfigureAwait(false);
            var deleted = await engine.DeleteAsync(table, filter, null, cancellationToken, request.DryRun).ConfigureAwait(false);
            await WrittenAsync(datasourceId, connection, [engine], request.DryRun, cancellationToken).ConfigureAwait(false);
            await LogAuditEventAsync(context, true, (request.DryRun ? "Dry run: " : string.Empty) + $"Deleted {deleted} rows of table '{tableName}' for datasource '{datasourceId}'")
                .ConfigureAwait(false);
            return deleted;
        }
    }

    /// <inheritdoc />
    public virtual async Task<RowsResponse> SaveTableRowsAsync(
        DapperMaticOperation context,
        string datasourceId,
        string tableName,
        RowsInsertDto request,
        bool upsert,
        string? schemaName = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        await AssertPermissionsAsync(context).ConfigureAwait(false);
        schemaName = NormalizeSchemaName(schemaName);
        var rows = (request.Rows ?? throw new ArgumentException("The request has no rows.")).Select(Nested).ToList();

        // Tables are described on a connection of their own, outside the write's transaction.
        var reader = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        var writer = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (reader)
        using (writer)
        {
            var (_, table) = await OpenRowsAsync(context, reader, datasourceId, schemaName, tableName, cancellationToken).ConfigureAwait(false);
            var engine = Engine(context, writer, datasourceId, table, reader);
            if (writer.State != ConnectionState.Open)
            {
                writer.Open();
            }

            using var transaction = writer.BeginTransaction();
            var written = await engine.SaveAsync(table, rows, upsert, transaction, cancellationToken).ConfigureAwait(false);
            if (request.DryRun)
            {
                transaction.Rollback();
            }
            else
            {
                transaction.Commit();
            }

            await WrittenAsync(datasourceId, writer, [engine], request.DryRun, cancellationToken).ConfigureAwait(false);
            await LogAuditEventAsync(
                    context,
                    true,
                    (request.DryRun ? "Dry run: " : string.Empty) + $"{(upsert ? "Saved" : "Inserted")} {written.Count} rows of table '{tableName}' for datasource '{datasourceId}'"
                )
                .ConfigureAwait(false);
            return new RowsResponse(written.Count, written);
        }
    }

    /// <inheritdoc />
    public virtual async Task<BatchResponse> BatchAsync(
        DapperMaticOperation context,
        string datasourceId,
        BatchDto request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        var operations = request.Operations ?? [];
        if (operations.Count == 0)
        {
            throw new ArgumentException("The batch has no operations.");
        }

        if (operations.Count > _options.QueryLimits.MaxBatchOperations)
        {
            throw new ArgumentException($"A batch holds at most {_options.QueryLimits.MaxBatchOperations} operations, not {operations.Count}.");
        }

        // Each operation is authorized as the row endpoint for it is, before anything is written.
        var planned = new List<(BatchOperationDto Operation, string Kind, string? Schema)>();
        foreach (var operation in operations)
        {
            var kind = operation.Op?.ToLowerInvariant() switch
            {
                "insert" => "insert-rows",
                "update" => "update-rows",
                "delete" => "delete-rows",
                _ => throw new ArgumentException($"A batch's operations insert, update or delete, not '{operation.Op}'."),
            };
            if (string.IsNullOrWhiteSpace(operation.Table))
            {
                throw new ArgumentException("Each operation of a batch names its table.");
            }

            DapperMaticAccess.Hold(AccessOf(context), datasourceId, operation.Schema);
            var schemaName = NormalizeSchemaName(operation.Schema);
            await AssertAsAsync(context, "tables/" + kind, schemaName, operation.Table, null, nameof(BatchAsync)).ConfigureAwait(false);
            planned.Add((operation, kind, schemaName));
        }

        // Tables are described on a connection of their own: SQL Server refuses a command outside a connection's
        // open transaction.
        var reader = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        var writer = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (reader)
        using (writer)
        {
            var engines = new List<(RestEngine Engine, RestTable Table)>();
            foreach (var (operation, _, schemaName) in planned)
            {
                var (_, table) = await OpenRowsAsync(context, reader, datasourceId, schemaName, operation.Table!, cancellationToken).ConfigureAwait(false);
                engines.Add((Engine(context, writer, datasourceId, table, reader), table));
            }

            if (writer.State != ConnectionState.Open)
            {
                writer.Open();
            }

            using var transaction = writer.BeginTransaction();
            var results = new List<BatchOperationResult>();
            for (var i = 0; i < planned.Count; i++)
            {
                var (operation, kind, _) = planned[i];
                var (engine, table) = engines[i];
                var current = i;
                object? Value(JsonElement json) => Reference(json, results, current);
                switch (kind)
                {
                    case "insert-rows":
                        var rows = (operation.Rows ?? throw new ArgumentException($"Operation {i + 1} inserts no rows."))
                            .Select(r => (IReadOnlyDictionary<string, object?>)r.ToDictionary(p => p.Key, p => Value(p.Value)))
                            .ToList();
                        var written = await engine.InsertReturningAsync(table, rows, transaction, cancellationToken).ConfigureAwait(false);
                        results.Add(new BatchOperationResult { Affected = written.Count, Rows = written });
                        break;
                    case "update-rows":
                        var set = (operation.Set ?? throw new ArgumentException($"Operation {i + 1} sets nothing.")).ToDictionary(p => p.Key, p => Value(p.Value));
                        var updated = await engine.UpdateAsync(table, set, Condition(operation.Filter, operation.Where), transaction, cancellationToken).ConfigureAwait(false);
                        results.Add(new BatchOperationResult { Affected = updated });
                        break;
                    default:
                        var deleted = await engine.DeleteAsync(table, Condition(operation.Filter, operation.Where), transaction, cancellationToken).ConfigureAwait(false);
                        results.Add(new BatchOperationResult { Affected = deleted });
                        break;
                }
            }

            if (request.DryRun)
            {
                transaction.Rollback();
            }
            else
            {
                transaction.Commit();
            }

            await WrittenAsync(datasourceId, writer, engines.Select(e => e.Engine), request.DryRun, cancellationToken).ConfigureAwait(false);
            await LogAuditEventAsync(
                    context,
                    true,
                    (request.DryRun ? "Dry run: " : string.Empty) + $"Ran a batch of {planned.Count} operations on datasource '{datasourceId}'"
                )
                .ConfigureAwait(false);
            return new BatchResponse(results, request.DryRun);
        }
    }

    /// <summary>
    /// Runs a query of a table's or a view's rows through the query tree: the request's conditions (its simple
    /// filters, its OData-shaped <c>$filter</c> and its JSON tree, ANDed), order, columns, page, count and expands.
    /// </summary>
    /// <param name="context">The operation context, which each related table the request reads is authorized with.</param>
    /// <param name="connection">The database connection.</param>
    /// <param name="schemaQualifiedTableOrViewName">The qualified table or view name.</param>
    /// <param name="request">The query request.</param>
    /// <param name="schemaName">The schema name, or null.</param>
    /// <param name="tableName">The table's name; null for a view, whose columns are read from a query of it.</param>
    /// <param name="datasourceId">The datasource, for the relations declared for it; null for those of every datasource.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A query result containing data, field information, and pagination details.</returns>
    /// <exception cref="ArgumentException">The request names a column or relation the source does not have, or asks for too much.</exception>
    protected virtual async Task<QueryResultDto> ExecuteDataQueryAsync(
        DapperMaticOperation context,
        IDbConnection connection,
        string schemaQualifiedTableOrViewName,
        QueryDto request,
        string? schemaName = null,
        string? tableName = null,
        string? datasourceId = null,
        CancellationToken cancellationToken = default
    )
    {
        var source = tableName is null
            ? await DescribeViewAsync(datasourceId, connection, schemaQualifiedTableOrViewName, schemaName, context.ViewName).ConfigureAwait(false)
            : await DescribeTableAsync(datasourceId, connection, schemaName, tableName, cancellationToken).ConfigureAwait(false);
        var engine = Engine(context, connection, datasourceId, source);
        var query = ToRestQuery(request);
        var (rows, total, next) = await engine.QueryAsync(query, source, null, cancellationToken).ConfigureAwait(false);

        // What the query read, for a subscription to it: each table by its schema, as resolved, and whether it is a view.
        context.State.Reads = engine.Reads.Select(t => (SchemaOf(connection, t.SchemaName ?? schemaName), t.Name, View: !t.Writable)).ToList();

        var selected = request.GetSelectColumns();
        var fields = query.Apply is { } apply
            ? [
                .. apply.GroupBy.Select(k => source.Column(k.Segments[0], "groupby")).Select(c => new FieldDto { Name = c.Name, FieldType = c.Type.GetFriendlyName(), IsNullable = c.Nullable }),
                .. apply.Aggregates.Select(a => new FieldDto
                {
                    Name = a.Alias,
                    FieldType = (rows.Select(r => r.GetValueOrDefault(a.Alias)?.GetType()).FirstOrDefault(t => t is not null) ?? typeof(object)).GetFriendlyName(),
                    IsNullable = a.Method != "count" && a.Method != "countdistinct",
                }),
            ]
            : source.Columns
                .Where(c => selected.Count == 0 || selected.Contains(c.Name, StringComparer.OrdinalIgnoreCase))
                .Select(c => new FieldDto { Name = c.Name, FieldType = c.Type.GetFriendlyName(), IsNullable = c.Nullable })
                .ToList();

        return new QueryResultDto
        {
            Data = rows,
            Fields = fields,
            Pagination = new PaginationDto
            {
                Take = request.Take,
                Skip = request.Skip,
                Total = total,
                HasMore = rows.Count == request.Take && (total is null || request.Skip + request.Take < total),
                NextCursor = next,
            },
        };
    }

    // The request's query: its simple filters, $filter and JSON tree ANDed; its OData order, or its simple one.
    private static RestQuery ToRestQuery(QueryDto request)
    {
        var conditions = request.GetFilterConditions().Select(Simple).ToList();
        if (!string.IsNullOrWhiteSpace(request.Filter))
        {
            conditions.Add(RestParser.ParseFilter(request.Filter));
        }

        if (request.Where is { } where && where.ValueKind != JsonValueKind.Undefined && where.ValueKind != JsonValueKind.Null)
        {
            conditions.Add(RestJson.Node(where));
        }

        return new RestQuery
        {
            Filter = conditions.Count == 0 ? null : conditions.Skip(1).Aggregate(conditions[0], (a, b) => new RestBinary("and", a, b)),
            Select = request.GetSelectColumns(),
            OrderBy = !string.IsNullOrWhiteSpace(request.Order)
                ? RestParser.ParseOrderBy(request.Order)
                : [.. request.GetOrderByPairs().Select(p => new RestOrder(new RestPath([p.Column]), !p.IsAscending))],
            Top = request.Take,
            Skip = request.Skip,
            After = request.After,
            Search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search,
            Apply = request.ApplyTree is { ValueKind: JsonValueKind.Object } tree ? RestJson.Apply(tree)
                : !string.IsNullOrWhiteSpace(request.Apply) ? RestParser.ParseApply(request.Apply)
                : null,
            Count = request.IncludeTotal,
            Expand = string.IsNullOrWhiteSpace(request.Expand) ? [] : RestParser.ParseExpand(request.Expand),
        };
    }

    // A simple filter, column.operator=value, as the same tree.
    private static RestNode Simple(FilterConditionDto condition)
    {
        var column = new RestPath([condition.Column]);
        var value = new RestLiteral(condition.Value);
        RestNode In() => new RestIn(column, [.. (condition.Value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(v => new RestLiteral(v))]);
        return condition.Operator switch
        {
            "eq" => new RestBinary("eq", column, value),
            "neq" => new RestBinary("ne", column, value),
            "gt" => new RestBinary("gt", column, value),
            "gte" => new RestBinary("ge", column, value),
            "lt" => new RestBinary("lt", column, value),
            "lte" => new RestBinary("le", column, value),
            "like" => new RestCall("contains", [column, value]),
            "nlike" => new RestNot(new RestCall("contains", [column, value])),
            "in" => In(),
            "nin" => new RestNot(In()),
            "isnull" => new RestBinary("eq", column, new RestLiteral(null)),
            "notnull" => new RestBinary("ne", column, new RestLiteral(null)),
            _ => throw new ArgumentException($"'{condition.Operator}' is not a filter operator."),
        };
    }

    // A write's condition, which must be given: "true" for every row.
    private static RestNode Condition(string? filter, JsonElement? where)
    {
        var conditions = new List<RestNode>();
        if (!string.IsNullOrWhiteSpace(filter))
        {
            conditions.Add(RestParser.ParseFilter(filter));
        }

        if (where is { } tree && tree.ValueKind != JsonValueKind.Undefined && tree.ValueKind != JsonValueKind.Null)
        {
            conditions.Add(RestJson.Node(tree));
        }

        return conditions.Count == 0
            ? throw new ArgumentException("Say which rows: a filter, or \"true\" for every row.")
            : conditions.Skip(1).Aggregate(conditions[0], (a, b) => new RestBinary("and", a, b));
    }

    // A view's columns, as a query of it reads them.
    // Whether a table's or a view's description is cached, so that it is known to exist without asking the database.
    private bool IsDescribed(string datasourceId, string? schemaName, string name, bool view) =>
        _schemaCache.Get(datasourceId, schemaName, name, view) is not null;

    private async Task<RestTable> DescribeViewAsync(string? datasourceId, IDbConnection connection, string qualifiedName, string? schemaName, string? viewName)
    {
        if (_schemaCache.Get(datasourceId, schemaName, qualifiedName, view: true) is { } cached)
        {
            return cached;
        }

        var described = await ReadViewAsync(connection, qualifiedName, schemaName, viewName ?? qualifiedName).ConfigureAwait(false);
        _schemaCache.Set(datasourceId, schemaName, qualifiedName, view: true, described);
        return described;
    }

    private async Task<RestTable> DescribeTableAsync(
        string? datasourceId,
        IDbConnection connection,
        string? schemaName,
        string tableName,
        CancellationToken cancellationToken
    )
    {
        if (_schemaCache.Get(datasourceId, schemaName, tableName, view: false) is { } cached)
        {
            return cached;
        }

        var table = await connection.GetTableAsync(schemaName, tableName, cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Table '{tableName}' does not exist.");
        var described = RestTable.Of(table, connection.GetSchemaQualifiedTableName(schemaName, tableName));
        if (_options.Searchable.FirstOrDefault(s =>
                (s.DatasourceId is null || string.Equals(s.DatasourceId, datasourceId, StringComparison.OrdinalIgnoreCase))
                && (s.SchemaName is null || string.Equals(s.SchemaName, described.SchemaName, StringComparison.OrdinalIgnoreCase))
                && string.Equals(s.TableName, described.Name, StringComparison.OrdinalIgnoreCase)) is { } searchable)
        {
            described = described.WithSearchable(await SearchableAsync(connection, described, searchable.Columns, cancellationToken).ConfigureAwait(false));
        }

        _schemaCache.Set(datasourceId, schemaName, tableName, view: false, described);
        return described;
    }

    // How a table declared searchable is searched: the database's full-text index of its columns where there is one,
    // read from the database's own definitions; else a contains match.
    private static async Task<RestSearchable> SearchableAsync(IDbConnection connection, RestTable table, string[] columns, CancellationToken cancellationToken)
    {
        string Quote(string name) => connection.GetQuotedIdentifier(name);
        var plain = new RestSearchable(columns);
        try
        {
            switch (connection.GetDbProviderType())
            {
                case DbProviderType.PostgreSql when table.Keys.Count == 1:
                    {
                        var definitions = await connection
                            .QueryAsync<string>(
                                new CommandDefinition(
                                    "SELECT indexdef FROM pg_indexes WHERE schemaname = @schema AND tablename = @table AND indexdef ILIKE '%to_tsvector(%'",
                                    new { schema = table.SchemaName ?? "public", table = table.Name },
                                    cancellationToken: cancellationToken
                                )
                            )
                            .ConfigureAwait(false);
                        foreach (var definition in definitions)
                        {
                            var start = definition.IndexOf("to_tsvector(", StringComparison.OrdinalIgnoreCase);
                            var depth = 0;
                            var end = start;
                            for (var i = start + "to_tsvector".Length; i < definition.Length; i++)
                            {
                                depth += definition[i] == '(' ? 1 : definition[i] == ')' ? -1 : 0;
                                if (depth == 0)
                                {
                                    end = i;
                                    break;
                                }
                            }

                            var expression = definition.Substring(start, end - start + 1);
                            if (end <= start || !columns.All(c => Regex.IsMatch(expression, $"\\b{Regex.Escape(c)}\\b", RegexOptions.IgnoreCase)))
                            {
                                continue;
                            }

                            var config = Regex.Match(expression, @"to_tsvector\('([^']+)'::regconfig", RegexOptions.IgnoreCase) is { Success: true } match
                                ? $"'{match.Groups[1].Value}'::regconfig, "
                                : string.Empty;
                            var native = $"{{0}} IN (SELECT {Quote(table.Keys[0])} FROM {table.QualifiedName} WHERE {expression} @@ websearch_to_tsquery({config}{{1}}))";
                            return new RestSearchable(columns, native.Replace("{0}", "\u0001", StringComparison.Ordinal).Replace("{1}", "\u0002", StringComparison.Ordinal)
                                .Replace("{", "{{", StringComparison.Ordinal).Replace("}", "}}", StringComparison.Ordinal)
                                .Replace("\u0001", "{0}", StringComparison.Ordinal).Replace("\u0002", "{1}", StringComparison.Ordinal));
                        }

                        return plain;
                    }

                case DbProviderType.MySql when table.Keys.Count == 1:
                    {
                        var indexed = await connection
                            .QueryAsync<(string Index, string Column)>(
                                new CommandDefinition(
                                    "SELECT INDEX_NAME, COLUMN_NAME FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table AND INDEX_TYPE = 'FULLTEXT' ORDER BY INDEX_NAME, SEQ_IN_INDEX",
                                    new { table = table.Name },
                                    cancellationToken: cancellationToken
                                )
                            )
                            .ConfigureAwait(false);
                        foreach (var index in indexed.GroupBy(i => i.Index))
                        {
                            var names = index.Select(i => i.Column).ToList();
                            if (names.Count == columns.Length && names.All(n => columns.Contains(n, StringComparer.OrdinalIgnoreCase)))
                            {
                                var native = $"{{0}} IN (SELECT {Quote(table.Keys[0])} FROM {table.QualifiedName} WHERE MATCH({string.Join(", ", names.Select(Quote))}) AGAINST ({{1}} IN BOOLEAN MODE))";
                                return new RestSearchable(columns, native, RestSearchQuery.MySqlBoolean);
                            }
                        }

                        return plain;
                    }

                case DbProviderType.Sqlite:
                    {
                        var fts = table.Name + "_fts";
                        var sql = await connection
                            .ExecuteScalarAsync<string?>(new CommandDefinition("SELECT sql FROM sqlite_master WHERE type = 'table' AND name = @fts", new { fts }, cancellationToken: cancellationToken))
                            .ConfigureAwait(false);
                        return sql is not null && sql.Contains("fts5", StringComparison.OrdinalIgnoreCase)
                            ? new RestSearchable(columns, $"{{0}} IN (SELECT rowid FROM {Quote(fts)} WHERE {Quote(fts)} MATCH {{1}})", RestSearchQuery.Fts5)
                            : plain;
                    }

                default:
                    return plain;
            }
        }
        catch (DbException)
        {
            // A catalog the caller cannot read: searched by contains, as a table without an index is.
            return plain;
        }
    }

    private static async Task<RestTable> ReadViewAsync(IDbConnection connection, string qualifiedName, string? schemaName, string name)
    {
        // SQLite types values, not columns: a view's computed column has no declared type, and without a row a driver
        // guesses one (Microsoft.Data.Sqlite says byte[]). One row tells each value's type; a column it cannot tell
        // takes values of any type.
        var sqlite = connection.GetDbProviderType() == DbProviderType.Sqlite;
        using var reader = await connection
            .ExecuteReaderAsync(sqlite ? $"SELECT * FROM {qualifiedName} LIMIT 1" : $"SELECT * FROM {qualifiedName} WHERE 1 = 0")
            .ConfigureAwait(false);
        var row = sqlite && reader.Read();
        var columns = new List<RestColumn>();
        for (var i = 0; i < reader.FieldCount; i++)
        {
            var type = !sqlite ? reader.GetFieldType(i)
                : row && !reader.IsDBNull(i) ? reader.GetFieldType(i)
                : typeof(object);
            columns.Add(new RestColumn(reader.GetName(i), type, Nullable: true));
        }

        return new RestTable
        {
            SchemaName = schemaName,
            Name = name,
            QualifiedName = qualifiedName,
            Columns = columns,
            Writable = false,
        };
    }

    private async Task<(RestEngine Engine, RestTable Table)> OpenRowsAsync(
        DapperMaticOperation context,
        IDbConnection connection,
        string datasourceId,
        string? schemaName,
        string tableName,
        CancellationToken cancellationToken
    )
    {
        if (!IsDescribed(datasourceId, schemaName, tableName, view: false))
        {
            await AssertSchemaExistsIfSpecifiedAsync(datasourceId, schemaName, connection, cancellationToken).ConfigureAwait(false);
            await AssertTableExistsAsync(datasourceId, tableName, schemaName, connection, cancellationToken).ConfigureAwait(false);
        }

        var table = await DescribeTableAsync(datasourceId, connection, schemaName, tableName, cancellationToken).ConfigureAwait(false);

        // The change tables are made before any write's transaction: MySQL commits one on DDL.
        if (_changeLog?.HearsAny(datasourceId) == true)
        {
            await _changeLog.EnsureAsync(datasourceId, connection, cancellationToken).ConfigureAwait(false);
        }

        return (Engine(context, connection, datasourceId, table), table);
    }

    // An engine following the relations declared for the datasource, authorizing and describing each related table
    // once.
    // A row of a request, related rows nested: an object is a related row, an array of objects related rows.
    private static IReadOnlyDictionary<string, object?> Nested(Dictionary<string, JsonElement> row) =>
        row.ToDictionary(
            p => p.Key,
            p => p.Value.ValueKind switch
            {
                JsonValueKind.Object => (object?)Nested(p.Value.EnumerateObject().ToDictionary(o => o.Name, o => o.Value)),
                JsonValueKind.Array => p.Value.EnumerateArray()
                    .Select(e => e.ValueKind == JsonValueKind.Object ? Nested(e.EnumerateObject().ToDictionary(o => o.Name, o => o.Value)) : throw new ArgumentException($"{p.Key} holds rows: give them as objects."))
                    .ToList(),
                _ => RestJson.Scalar(p.Value),
            },
            StringComparer.OrdinalIgnoreCase
        );

    // A value of a batch's operation: "$n.column" or "$n[i].column", a value an earlier operation wrote; "$$..." a
    // string starting with one "$"; anything else as the row endpoints read it.
    private static object? Reference(JsonElement json, IReadOnlyList<BatchOperationResult> results, int current)
    {
        if (json.ValueKind != JsonValueKind.String || json.GetString() is not { Length: > 1 } text || text[0] != '$')
        {
            return RestJson.Scalar(json);
        }

        if (text[1] == '$')
        {
            return text[1..];
        }

        var match = BatchReference().Match(text);
        if (!match.Success)
        {
            throw new ArgumentException($"{text} is not a value of an earlier operation, as $1.id; a string starting with $ is written $$.");
        }

        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        var number = int.Parse(match.Groups[1].Value, invariant);
        var index = match.Groups[2].Success ? int.Parse(match.Groups[2].Value, invariant) : 0;
        if (number < 1 || number > current)
        {
            throw new ArgumentException($"{text} refers to operation {number}, which does not run before this one.");
        }

        var rows = results[number - 1].Rows ?? throw new ArgumentException($"{text}: operation {number} inserts no rows.");
        if (index >= rows.Count)
        {
            throw new ArgumentException($"{text}: operation {number} inserted {rows.Count} rows.");
        }

        var column = match.Groups[3].Value;
        var row = rows[index];
        var key = row.Keys.FirstOrDefault(k => string.Equals(k, column, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"{text}: the rows operation {number} inserted have no {column}.");
        return row[key];
    }

    // Writes committed: the results kept that read their tables are stale, and this server's subscriptions to them are
    // told. A dry run's were rolled back.
    private Task WrittenAsync(string datasourceId, IDbConnection connection, IEnumerable<RestEngine> engines, bool dryRun, CancellationToken cancellationToken) =>
        dryRun ? Task.CompletedTask : CommittedAsync(datasourceId, connection.GetDbProviderType(), [.. engines.SelectMany(e => e.Written).Select(t => (t.SchemaName, t.Name))], cancellationToken);

    // Writes to tables committed: their cached results are stale, and this server's subscriptions to them are told.
    private async Task CommittedAsync(string datasourceId, DbProviderType provider, List<(string? Schema, string Table)> tables, CancellationToken cancellationToken)
    {
        if (_resultCache is not null)
        {
            await _resultCache.WrittenAsync(datasourceId, provider, tables, cancellationToken).ConfigureAwait(false);
        }

        _hub?.Notify(datasourceId, tables.Select(t => (t.Schema is { Length: > 0 } and not "_" ? t.Schema : DapperMaticProviders.DefaultSchemaName(provider), t.Table)));
    }

    [GeneratedRegex(@"^\$(\d+)(?:\[(\d+)\])?\.([A-Za-z_][A-Za-z0-9_]*)$")]
    private static partial Regex BatchReference();

    private RestEngine Engine(DapperMaticOperation context, IDbConnection connection, string? datasourceId, RestTable source, IDbConnection? describe = null)
    {
        static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        var described = new Dictionary<(string?, string), RestTable>() { [(source.SchemaName?.ToLowerInvariant(), source.Name.ToLowerInvariant())] = source };
        var limits = _options.QueryLimits;

        async Task<RestRelation?> Resolve(RestTable from, string name, CancellationToken cancellationToken)
        {
            var declared = _options.Relations.FirstOrDefault(r =>
                (r.DatasourceId is null || Same(r.DatasourceId, datasourceId))
                && (r.SchemaName is null || Same(r.SchemaName, from.SchemaName))
                && Same(r.TableName, from.Name)
                && Same(r.Name, name)
            );
            if (declared is null)
            {
                return null;
            }

            if (declared.Columns.Length == 0 || declared.Columns.Length != declared.OtherColumns.Length)
            {
                throw new InvalidOperationException($"The relation {declared.TableName}.{declared.Name} pairs {declared.Columns.Length} columns with {declared.OtherColumns.Length}.");
            }

            var schema = declared.OtherSchemaName ?? from.SchemaName;
            var key = (schema?.ToLowerInvariant(), declared.OtherTableName.ToLowerInvariant());
            if (!described.TryGetValue(key, out var other))
            {
                await AssertAsAsync(context, "tables/query", schema, declared.OtherTableName, null, "QueryRelatedTable").ConfigureAwait(false);
                other = await DescribeTableAsync(datasourceId, describe ?? connection, schema, declared.OtherTableName, cancellationToken).ConfigureAwait(false);
                described[key] = other;
            }

            return new RestRelation(declared.Name, other, [.. declared.Columns.Zip(declared.OtherColumns)], declared.Many);
        }

        // Each table the request reads or writes is held to the policies for it, with its columns known.
        var caller = AccessOf(context);
        var access = new RestAccess
        {
            Rules = (table, action) =>
                RulesFor(context, new DmResource(datasourceId, SchemaOf(connection, table.SchemaName), table.Name, [.. table.Columns.Select(c => c.Name)]), action),
            Value = (path, cancellationToken) => caller.GetValueAsync(path, cancellationToken),
        };

        return new RestEngine(
            connection,
            new RestLimits
            {
                MaxTop = limits.MaxTop,
                MaxExpandDepth = limits.MaxExpandDepth,
                MaxLambdaDepth = limits.MaxLambdaDepth,
                MaxNodes = limits.MaxNodes,
                MaxExpandedRows = limits.MaxExpandedRows,
                MaxInsertRows = limits.MaxInsertRows,
                MaxAffectedRows = limits.MaxAffectedRows,
            },
            Resolve,
            access
        )
        {
            // Results are kept per datasource and session values; with session steps, which set what they like, none is.
            Cache = datasourceId is null || _session is null ? null : _resultCache?.For(datasourceId, connection.GetDbProviderType(), _session),
            Changes = datasourceId is null ? null : _changeLog?.SinkFor(datasourceId, connection.GetDbProviderType()),
        };
    }
}
