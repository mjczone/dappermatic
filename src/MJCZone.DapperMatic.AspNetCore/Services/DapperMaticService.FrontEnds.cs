// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Data.Common;
using MJCZone.DapperMatic.Dml.Rest;

namespace MJCZone.DapperMatic.AspNetCore.Services;

/// <summary>
/// What front ends built on DapperMatic (GraphQL) run their queries and writes through: the same authorization,
/// tenancy, descriptions, policies, caches, change capture and audit as the REST endpoints, over the query tree.
/// </summary>
public partial class DapperMaticService
{
    /// <summary>
    /// Reads a page of a table's or view's rows, as the operation's caller: authorized as the query endpoint for it is,
    /// each related table it reads as well, the policies' filters and columns applied. The tables read are recorded in
    /// the operation's state.
    /// </summary>
    /// <param name="operation">The operation, held to the caller's tenant, in the schema it acts in.</param>
    /// <param name="datasourceId">The datasource.</param>
    /// <param name="name">The table's or view's name.</param>
    /// <param name="view">Whether it is a view.</param>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page, and the table or view as described.</returns>
    internal async Task<(RestPage Page, RestTable Source)> PageRowsAsync(
        DapperMaticOperation operation,
        string datasourceId,
        string name,
        bool view,
        RestQuery query,
        CancellationToken cancellationToken
    )
    {
        var asQuery = operation with
        {
            Operation = view ? "views/query" : "tables/query",
            DatasourceId = datasourceId,
            TableName = view ? null : name,
            ViewName = view ? name : null,
            ColumnNames = null,
        };
        await AssertPermissionsAsync(asQuery, view ? nameof(QueryViewAsync) : nameof(QueryTableAsync)).ConfigureAwait(false);
        var schemaName = NormalizeSchemaName(operation.SchemaName);
        var connection = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        using (connection)
        {
            RestTable source;
            if (view)
            {
                source = await DescribeViewAsync(datasourceId, connection, connection.GetSchemaQualifiedTableName(schemaName, name), schemaName, name).ConfigureAwait(false);
            }
            else
            {
                if (!IsDescribed(datasourceId, schemaName, name, view: false))
                {
                    await AssertSchemaExistsIfSpecifiedAsync(datasourceId, schemaName, connection, cancellationToken).ConfigureAwait(false);
                    await AssertTableExistsAsync(datasourceId, name, schemaName, connection, cancellationToken).ConfigureAwait(false);
                }

                source = await DescribeTableAsync(datasourceId, connection, schemaName, name, cancellationToken).ConfigureAwait(false);
            }

            var engine = Engine(asQuery, connection, datasourceId, source);
            var page = await engine.PageAsync(query, source, null, cancellationToken).ConfigureAwait(false);
            operation.State.Reads = [.. engine.Reads.Select(t => (SchemaOf(connection, t.SchemaName ?? schemaName), t.Name, View: !t.Writable))];
            await LogAuditEventAsync(asQuery, true, $"Queried {(view ? "view" : "table")} '{name}' for datasource '{datasourceId}'").ConfigureAwait(false);
            return (page, source);
        }
    }

    /// <summary>
    /// Starts a front end's writes to a datasource, in one transaction: every table is opened (authorized, described,
    /// the change tables made) before the transaction begins, which MySQL would otherwise commit on DDL.
    /// </summary>
    /// <param name="operation">The operation, held to the caller's tenant, in the schema it acts in.</param>
    /// <param name="datasourceId">The datasource.</param>
    /// <returns>The session.</returns>
    internal async Task<WriteSession> BeginWritesAsync(DapperMaticOperation operation, string datasourceId)
    {
        // Tables are described on a connection of their own: SQL Server refuses a command outside a connection's open
        // transaction.
        var reader = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        var writer = await CreateConnectionForDatasource(datasourceId).ConfigureAwait(false);
        return new WriteSession(this, operation, datasourceId, reader, writer);
    }

    // An engine reading a table on the writer, in its transaction, as the caller; tables described on the reader.
    private async Task<(RestEngine Engine, RestTable Table)> ReaderOfAsync(
        DapperMaticOperation operation,
        IDbConnection reader,
        IDbConnection writer,
        string datasourceId,
        string table,
        CancellationToken cancellationToken
    )
    {
        var schemaName = NormalizeSchemaName(operation.SchemaName);
        var asQuery = operation with
        {
            Operation = "tables/query",
            DatasourceId = datasourceId,
            SchemaName = schemaName,
            TableName = table,
            ViewName = null,
            ColumnNames = null,
        };
        await AssertPermissionsAsync(asQuery, nameof(QueryTableAsync)).ConfigureAwait(false);
        var described = await DescribeTableAsync(datasourceId, reader, schemaName, table, cancellationToken).ConfigureAwait(false);
        // In the transaction, the engine keeps no results: they may see its own writes.
        return (Engine(asQuery, writer, datasourceId, described, reader), described);
    }

    /// <summary>
    /// A front end's writes in one transaction: all or none, the caches and subscriptions told and the writes audited
    /// once they are committed.
    /// </summary>
    internal sealed class WriteSession : IDisposable
    {
        private readonly DapperMaticService _service;
        private readonly DapperMaticOperation _operation;
        private readonly IDbConnection _reader;
        private readonly IDbConnection _writer;
        private readonly Dictionary<(string Kind, string Table), (RestEngine Engine, RestTable Table, DapperMaticOperation Operation)> _opened = [];
        private readonly List<(DapperMaticOperation Operation, string Message)> _audit = [];
        private readonly List<Func<CancellationToken, Task>> _afterCommit = [];
        private IDbTransaction? _transaction;

        /// <summary>
        /// Initializes a new instance of the <see cref="WriteSession"/> class.
        /// </summary>
        /// <param name="service">The service.</param>
        /// <param name="operation">The operation.</param>
        /// <param name="datasourceId">The datasource.</param>
        /// <param name="reader">The connection tables are described on.</param>
        /// <param name="writer">The connection written on.</param>
        public WriteSession(DapperMaticService service, DapperMaticOperation operation, string datasourceId, IDbConnection reader, IDbConnection writer)
        {
            _service = service;
            _operation = operation;
            DatasourceId = datasourceId;
            _reader = reader;
            _writer = writer;
        }

        /// <summary>Gets the datasource.</summary>
        public string DatasourceId { get; }

        /// <summary>Gets the connection the writes are made on.</summary>
        public IDbConnection Writer => _writer;

        /// <summary>Gets the transaction, once begun.</summary>
        public IDbTransaction Transaction => _transaction ?? throw new InvalidOperationException("The writes have not begun.");

        /// <summary>
        /// Opens a table for a kind of write, before the transaction begins: authorized as the row endpoint for it is,
        /// described, its engine made.
        /// </summary>
        /// <param name="table">The table.</param>
        /// <param name="kind">The write: <c>insert-rows</c>, <c>save-rows</c>, <c>update-rows</c> or <c>delete-rows</c>.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The table's engine and description.</returns>
        public async Task<(RestEngine Engine, RestTable Table)> OpenAsync(string table, string kind, CancellationToken cancellationToken)
        {
            if (_opened.TryGetValue((kind, table.ToLowerInvariant()), out var opened))
            {
                return (opened.Engine, opened.Table);
            }

            if (_transaction is not null)
            {
                throw new InvalidOperationException("Tables are opened before the writes begin.");
            }

            var schemaName = _service.NormalizeSchemaName(_operation.SchemaName);
            var asWrite = _operation with
            {
                Operation = "tables/" + kind,
                DatasourceId = DatasourceId,
                SchemaName = schemaName,
                TableName = table,
                ViewName = null,
                ColumnNames = null,
            };
            await _service.AssertPermissionsAsync(asWrite, kind switch
            {
                "insert-rows" => nameof(InsertTableRowsAsync),
                "update-rows" => nameof(UpdateTableRowsAsync),
                "delete-rows" => nameof(DeleteTableRowsAsync),
                _ => nameof(SaveTableRowsAsync),
            }).ConfigureAwait(false);
            var (_, described) = await _service.OpenRowsAsync(asWrite, _reader, DatasourceId, schemaName, table, cancellationToken).ConfigureAwait(false);
            var engine = _service.Engine(asWrite, _writer, DatasourceId, described, _reader);
            _opened[(kind, table.ToLowerInvariant())] = (engine, described, asWrite);
            return (engine, described);
        }

        /// <summary>
        /// Readies a function to run among the writes, before the transaction begins.
        /// </summary>
        /// <param name="functionName">The function.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task.</returns>
        public Task OpenFunctionAsync(string functionName, CancellationToken cancellationToken) =>
            _transaction is not null
                ? throw new InvalidOperationException("Functions are readied before the writes begin.")
                : _service.PrepareFunctionAsync(DatasourceId, functionName, _writer, cancellationToken);

        /// <summary>Notes what to do once the writes are committed (a function's caches and subscriptions).</summary>
        /// <param name="action">What to do.</param>
        public void AfterCommit(Func<CancellationToken, Task> action) => _afterCommit.Add(action);

        /// <summary>Notes a write for the audit, made once the writes are committed.</summary>
        /// <param name="operation">The operation it was made as.</param>
        /// <param name="message">What was written.</param>
        public void Audit(DapperMaticOperation operation, string message) => _audit.Add((operation, message));

        /// <summary>Gets the operation a table was opened for a kind of write as, or null if it was not.</summary>
        /// <param name="table">The table.</param>
        /// <param name="kind">The write.</param>
        /// <returns>The operation, authorized as the row endpoint for it is.</returns>
        public DapperMaticOperation? OperationOf(string table, string kind) =>
            _opened.TryGetValue((kind, table.ToLowerInvariant()), out var opened) ? opened.Operation : null;

        /// <summary>Begins the transaction, every table opened.</summary>
        public void Begin()
        {
            if (_writer.State != ConnectionState.Open)
            {
                _writer.Open();
            }

            _transaction = _writer.BeginTransaction();
        }

        /// <summary>
        /// Notes a write for the audit, made once the writes are committed.
        /// </summary>
        /// <param name="table">The table.</param>
        /// <param name="kind">The write.</param>
        /// <param name="message">What was written.</param>
        public void Written(string table, string kind, string message)
        {
            var operation = _opened.TryGetValue((kind, table.ToLowerInvariant()), out var opened) ? opened.Operation : _operation;
            _audit.Add((operation, message));
        }

        /// <summary>
        /// Reads rows written, by their keys, in the transaction and as the caller: the query's columns and expands, the
        /// policies' filters applied (a row the caller may write but not read is not returned). Rows come in the order
        /// of their keys.
        /// </summary>
        /// <param name="table">The table.</param>
        /// <param name="keys">The rows' keys.</param>
        /// <param name="query">The columns and expands to read; with <see cref="RestQuery.Cursors"/>, each row's cursor too.</param>
        /// <param name="limit">The most rows to read together.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The rows, each with its cursor when asked for.</returns>
        /// <exception cref="ArgumentException">The rows are more than <paramref name="limit"/>.</exception>
        public async Task<List<(Dictionary<string, object?> Row, string? Cursor)>> ReadAsync(
            string table,
            IReadOnlyList<IReadOnlyDictionary<string, object?>> keys,
            RestQuery query,
            int limit,
            CancellationToken cancellationToken
        )
        {
            if (keys.Count > limit)
            {
                throw new ArgumentException($"The write's {keys.Count} rows are more than the {limit} it may read back: write fewer rows, or read them with a query.");
            }

            var (engine, described) = await _service.ReaderOfAsync(_operation, _reader, _writer, DatasourceId, table, cancellationToken).ConfigureAwait(false);
            var read = new List<(Dictionary<string, object?> Row, string? Cursor)>();
            var perBatch = Math.Max(1, Math.Min(_service._options.QueryLimits.MaxTop, 1_000) / Math.Max(1, described.Keys.Count));
            for (var i = 0; i < keys.Count; i += perBatch)
            {
                var batch = keys.Skip(i).Take(perBatch).ToList();
                var page = await engine.PageAsync(
                        query with { Filter = And(query.Filter, new RestTrusted(RestEngine.KeyFilter(described, batch))), Top = batch.Count, Skip = null, After = null, Count = false, OrderBy = [] },
                        described,
                        Transaction,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                read.AddRange(page.Rows.Select((r, j) => (r, page.Cursors?[j])));
            }

            // In the order of the keys: an insert's rows as they were given.
            var order = keys.Select((k, i) => (Key: KeyText(described, k), Index: i)).GroupBy(k => k.Key).ToDictionary(g => g.Key, g => g.First().Index);
            return [.. read.OrderBy(r => order.GetValueOrDefault(KeyText(described, r.Row), int.MaxValue))];
        }

        /// <summary>
        /// Commits the writes. What follows a commit, <see cref="CommittedAsync"/>, is apart: a failure there does not
        /// undo them.
        /// </summary>
        public void Commit()
        {
            var transaction = Transaction;
            transaction.Commit();
            transaction.Dispose();
            _transaction = null;
        }

        /// <summary>
        /// Once the writes are committed: the caches kept for the tables written are stale, this server's subscriptions
        /// to them are told, and each write is audited. Each is done though one before it failed.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task.</returns>
        /// <exception cref="AggregateException">One of them failed; the writes stay committed.</exception>
        public async Task CommittedAsync(CancellationToken cancellationToken)
        {
            var failures = new List<Exception>();
            try
            {
                await _service.WrittenAsync(DatasourceId, _writer, _opened.Values.Select(o => o.Engine), dryRun: false, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures.Add(ex);
            }

            foreach (var action in _afterCommit)
            {
                try
                {
                    await action(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failures.Add(ex);
                }
            }

            foreach (var (operation, message) in _audit)
            {
                try
                {
                    await _service.LogAuditEventAsync(operation, true, message).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failures.Add(ex);
                }
            }

            if (failures.Count > 0)
            {
                throw new AggregateException("The writes were committed, and what follows them failed.", failures);
            }
        }

        /// <summary>Rolls the writes back: none, if a commit that failed ended the transaction already.</summary>
        public void Rollback()
        {
            try
            {
                _transaction?.Rollback();
            }
            catch (Exception ex) when (ex is InvalidOperationException or DbException)
            {
                // The transaction had ended: the commit's own failure is what the caller is told.
            }
            finally
            {
                _transaction?.Dispose();
                _transaction = null;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _transaction?.Dispose();
            _reader.Dispose();
            _writer.Dispose();
        }

        private static RestNode? And(RestNode? left, RestNode? right) =>
            left is null ? right : right is null ? left : new RestBinary("and", left, right);

        // A row's key as text, so that keys read back match those written whatever their .NET types.
        private static string KeyText(RestTable table, IReadOnlyDictionary<string, object?> row) =>
            string.Join(
                '\u001f',
                table.Keys.Select(k =>
                {
                    var value = row.TryGetValue(k, out var found) ? found : row.FirstOrDefault(p => string.Equals(p.Key, k, StringComparison.OrdinalIgnoreCase)).Value;
                    return value switch
                    {
                        null or DBNull => "\u0000",
                        byte[] bytes => Convert.ToBase64String(bytes),
                        byte or sbyte or short or ushort or int or uint or long or ulong or decimal => Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture).ToString(System.Globalization.CultureInfo.InvariantCulture),
                        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
                    };
                })
            );
    }
}
