// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using Dapper;
using MJCZone.DapperMatic.Dml.Internal;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.Dml.Rest;

/// <summary>
/// Runs REST requests against tables the database describes: queries with their counts and expands, and writes of
/// rows, all through the query tree.
/// </summary>
internal sealed partial class RestEngine
{
    // Owners' keys per expand query, well within every provider's limit on parameters.
    private const int KeysPerQuery = 1_000;

    private readonly IDbConnection _connection;
    private readonly RestLimits _limits;
    private readonly Func<RestTable, string, CancellationToken, Task<RestRelation?>> _relation;
    private readonly DmlDialect _dialect;
    private readonly RestAccess? _access;
    private readonly HashSet<RestTable> _written = [];
    private IReadOnlyList<RestTable> _reads = [];
    private string? _versions;
    private List<DmChange>? _gathered;
    private bool? _windows;
    private int _expanded;

    /// <summary>
    /// Initializes a new instance of the <see cref="RestEngine"/> class.
    /// </summary>
    /// <param name="connection">The connection, open.</param>
    /// <param name="limits">How much a request may ask for.</param>
    /// <param name="relation">
    /// Finds a relation the endpoint declares for a table by name, with its related table described; null if there is
    /// none, which the request cannot then follow. It returns one description per table for every call.
    /// </param>
    /// <param name="access">
    /// What the policies allow on each table, or null to run without policies: every table, row and column.
    /// </param>
    public RestEngine(
        IDbConnection connection,
        RestLimits limits,
        Func<RestTable, string, CancellationToken, Task<RestRelation?>> relation,
        RestAccess? access = null
    )
    {
        _connection = connection;
        _limits = limits;
        _relation = relation;
        _access = access;
        _dialect = DmlDialect.For(connection);
    }

    /// <summary>
    /// Gets where the results of statements reading cached tables are kept, or null to keep none. Statements in a
    /// transaction are never cached: they may see the transaction's own writes.
    /// </summary>
    public RestCache? Cache { get; init; }

    /// <summary>
    /// Gets the tables the engine has written to, or tried to: those whose cached results a committed write makes stale.
    /// </summary>
    public IReadOnlyCollection<RestTable> Written => _written;

    /// <summary>
    /// Gets the tables and views the last query read: its own, and those of the relations it followed.
    /// </summary>
    public IReadOnlyList<RestTable> Reads => _reads;

    /// <summary>
    /// Gets what hears the changes the engine's writes make, in their transactions, or null for nothing: each change
    /// with the keys of the rows changed, read as the write runs.
    /// </summary>
    public IDmChangeSink? Changes { get; init; }

    /// <summary>
    /// Runs a query: its rows, each with its expands, and their count if asked for.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="table">The table or view.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The rows, as column name to value, with each expand under its relation's name; their count, or null; and the
    /// cursor of the next page, or null when this is the last or the query cannot be paged by keys.
    /// </returns>
    /// <exception cref="ArgumentException">The query names something the table or the endpoint does not have, or asks for too much.</exception>
    public async Task<(List<Dictionary<string, object?>> Rows, long? Count, string? NextCursor)> QueryAsync(
        RestQuery query,
        RestTable table,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    )
    {
        var page = await PageAsync(query, table, tx, cancellationToken).ConfigureAwait(false);
        return (page.Rows, page.Count, page.NextCursor);
    }

    /// <summary>
    /// Runs a query, as <see cref="QueryAsync"/> does, with what a page of a Relay connection needs besides: each row's
    /// cursor (<see cref="RestQuery.Cursors"/>) and whether rows follow the page (<see cref="RestQuery.Probe"/>).
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="table">The table or view.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page.</returns>
    /// <exception cref="ArgumentException">The query names something the table or the endpoint does not have, asks for too much, or asks for cursors of rows it cannot page by keys.</exception>
    public async Task<RestPage> PageAsync(RestQuery query, RestTable table, IDbTransaction? tx, CancellationToken cancellationToken)
    {
        RefuseValues(query);
        if (query.Apply is not null)
        {
            var (applied, appliedCount, _) = await ApplyAsync(query, table, tx, cancellationToken).ConfigureAwait(false);
            return new RestPage(applied, appliedCount, null, null, false);
        }

        // A page that names no top reads MaxTop rows at most (a probe tells whether more follow); an expand without one
        // is held, all together, to MaxExpandedRows.
        if (query.Top is null)
        {
            query = query with { Top = _limits.MaxTop };
        }

        // A search is a condition of the rows, ANDed: their count and cursor follow it.
        if (query.Search is { } search)
        {
            query = query with { Filter = And(query.Filter, new RestSearch(search)) };
        }

        var relations = await ResolveAsync(query, table, cancellationToken).ConfigureAwait(false);
        _reads = [table, .. relations.Values.Select(r => r.Other).Where(t => !ReferenceEquals(t, table)).Distinct()];

        // Every table the request reads must be readable, each read through its own policy filter, and only the
        // columns it may read selected, compared or ordered by.
        await PrepareReadFiltersAsync(relations.Values.Select(r => r.Other).Prepend(table), cancellationToken).ConfigureAwait(false);
        RequireReadable(query, table, relations);
        query = Effective(query, table, relations) with { Filter = And(query.Filter, ReadFilter(table)) };

        // Ordered by its own columns, then its key, a query is paged by keys: each page ends with a cursor, and $after
        // reads the rows after one.
        var keyset = Keyset(query, table);
        var counted = query;
        if (keyset is not null)
        {
            query = query with { OrderBy = [.. keyset.Select(k => new RestOrder(new RestPath([k.Column]), k.Descending))] };
        }

        if (query.After is { } after)
        {
            if (keyset is null)
            {
                throw new ArgumentException("$after pages a table that has a key, ordered by its own columns the caller may read.");
            }

            if (query.Skip is > 0)
            {
                throw new ArgumentException("$after and $skip page in two ways: give one.");
            }

            query = query with { Filter = And(query.Filter, new RestTrusted(RestCursor.After(keyset, RestCursor.Decode(after, keyset)))) };
        }

        if (query.Cursors && keyset is null)
        {
            throw new ArgumentException("A page's cursors are made of keys: the table needs a key, and its rows ordered by its own columns the caller may read.");
        }

        var translator = new RestTranslator(_dialect, relations, _limits, _access is null ? null : ReadFilter);
        var extras = ExpandKeys(query, table, relations).Concat(keyset?.Select(k => k.Column) ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var (select, _) = translator.Rows(query, table, extras);

        // A probe reads one row more than the page, past the limit on a page's rows, to tell whether more follow.
        var probed = query.Probe && select.Take is { } take ? take : (int?)null;
        if (probed is { } page)
        {
            select = select with { Take = page + 1 };
        }

        var rows = await ReadAsync(select, tx, cancellationToken).ConfigureAwait(false);
        var more = probed is { } size && rows.Count > size;
        if (more)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        foreach (var expand in query.Expand)
        {
            await ExpandAsync(rows, table, expand, translator, relations, tx, cancellationToken).ConfigureAwait(false);
        }

        // A full page may have more after it: its cursor is the last row's values in the keyset order.
        string? next = null;
        if (keyset is not null && rows.Count > 0 && (probed is null ? rows.Count == select.Take : more))
        {
            next = RestCursor.Encode(keyset, [.. keyset.Select(k => Find(rows[^1], k.Column))]);
        }

        List<string>? cursors = query.Cursors ? [.. rows.Select(r => RestCursor.Encode(keyset!, [.. keyset!.Select(k => Find(r, k.Column))]))] : null;
        Strip(rows, query, extras);

        long? count = null;
        if (query.Count)
        {
            var total = translator.Count(counted, table);
            var parameters = new DmlParameters();
            var sql = new SqlRenderer(_dialect, parameters).For(total).Render(total);
            count = await CachedAsync(
                    "count",
                    sql,
                    parameters,
                    tx,
                    () => Traced(table.Name, sql, () => _connection.ExecuteScalarAsync<long>(DmScope.Command(sql, parameters, tx, cancellationToken))),
                    RestCacheCodec.Encode,
                    RestCacheCodec.DecodeCount,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        return new RestPage(rows, count, next, cursors, more);
    }

    /// <summary>
    /// Inserts rows, all or none: each with the policies' presets, and refused if it breaks their check.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="rows">The rows, as column name to value.</param>
    /// <param name="tx">The transaction to use, or null for one of the method's own.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="dryRun">Whether to roll the insert back, reporting how many rows it would have inserted.</param>
    /// <returns>The number of rows inserted.</returns>
    /// <exception cref="ArgumentException">
    /// A row names a column the table does not have, or has a value not of its type; or there are more rows than
    /// <see cref="RestLimits.MaxInsertRows"/>.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">No policy lets the caller insert, or a row breaks a policy's check.</exception>
    public async Task<int> InsertAsync(
        RestTable table,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        IDbTransaction? tx,
        CancellationToken cancellationToken,
        bool dryRun = false
    )
    {
        RequireWritable(table);
        if (rows.Count > _limits.MaxInsertRows)
        {
            throw new ArgumentException($"An insert holds at most {_limits.MaxInsertRows} rows, not {rows.Count}.");
        }

        var rules = Require(table, DmAction.Create);
        var check = Trusted(await BindAsync(Condition(rules.Check), cancellationToken).ConfigureAwait(false));

        // Changes heard are told the keys of the rows inserted: each row is inserted returning its generated key.
        if (Changes?.Wants(table.SchemaName, table.Name) is not null)
        {
            var prepared = new List<Dictionary<string, object?>>();
            foreach (var row in rows)
            {
                prepared.Add(await PreparedAsync(table, rules, row, cancellationToken).ConfigureAwait(false));
            }

            return await WriteAsync(
                    tx,
                    dryRun,
                    maxAffected: null,
                    async transaction => (await InsertPreparedAsync(table, prepared, check, transaction!, cancellationToken).ConfigureAwait(false)).Count
                )
                .ConfigureAwait(false);
        }

        var statements = new List<(string Sql, DmlParameters Parameters)>();
        foreach (var row in rows)
        {
            var written = _access is null ? row : await WritableAsync(table, rules, row, cancellationToken).ConfigureAwait(false);
            statements.Add(InsertStatement(table, written, check));
        }

        return await WriteAsync(
                tx,
                dryRun,
                maxAffected: null,
                async transaction =>
                {
                    var inserted = 0;
                    foreach (var (sql, parameters) in statements)
                    {
                        var count = await Traced(table.Name, sql, () => _connection.ExecuteAsync(DmScope.Command(sql, parameters, transaction, cancellationToken))).ConfigureAwait(false);
                        if (check is not null && count == 0)
                        {
                            throw new UnauthorizedAccessException($"A row breaks the policies' check on {table.Name}; nothing was inserted.");
                        }

                        inserted += count;
                    }

                    return inserted;
                }
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Inserts rows as <see cref="InsertAsync"/> does, in the caller's transaction, returning each row as written: its
    /// values, the policies' values, and the key the database generated.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="rows">The rows, as column name to value.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rows written.</returns>
    /// <exception cref="ArgumentException">A row names a column the table does not have, or has a value not of its type.</exception>
    /// <exception cref="UnauthorizedAccessException">No policy lets the caller insert, or a row breaks a policy's check.</exception>
    public async Task<List<Dictionary<string, object?>>> InsertReturningAsync(
        RestTable table,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        IDbTransaction tx,
        CancellationToken cancellationToken
    )
    {
        RequireWritable(table);
        if (rows.Count > _limits.MaxInsertRows)
        {
            throw new ArgumentException($"An insert holds at most {_limits.MaxInsertRows} rows, not {rows.Count}.");
        }

        var rules = Require(table, DmAction.Create);
        var check = Trusted(await BindAsync(Condition(rules.Check), cancellationToken).ConfigureAwait(false));
        var prepared = new List<Dictionary<string, object?>>();
        foreach (var row in rows)
        {
            prepared.Add(await PreparedAsync(table, rules, row, cancellationToken).ConfigureAwait(false));
        }

        return await InsertPreparedAsync(table, prepared, check, tx, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes rows with the rows they relate to, through the relations declared for the endpoint: a related row a row
    /// refers to (a to-one relation) is saved first and its key written in the row; rows that refer to it (a
    /// to-many relation) after, with its key written in theirs. A related row whose key is given is updated if it
    /// exists, and inserted if not; one without a key is inserted. Rows left out are left as they are. Every row is
    /// held to its own table's policies.
    /// </summary>
    /// <param name="table">The table of the rows.</param>
    /// <param name="rows">The rows, as column or relation name to value: a related row as a dictionary, related rows as a list of them.</param>
    /// <param name="upsert">Whether the rows themselves are saved as related ones are (updated by their key), or inserted.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rows as written, with their related rows and the keys the database generated.</returns>
    /// <exception cref="ArgumentException">A row names a column or relation the table does not have, or there are too many rows.</exception>
    /// <exception cref="UnauthorizedAccessException">A write is not allowed, or a row exists that the caller may not change.</exception>
    public async Task<List<Dictionary<string, object?>>> SaveAsync(
        RestTable table,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        bool upsert,
        IDbTransaction tx,
        CancellationToken cancellationToken
    )
    {
        var count = 0;
        var written = new List<Dictionary<string, object?>>();

        // Rows are saved one by one: their changes are gathered and told once, a table's and action's together.
        var gathered = _gathered = Changes is null ? null : [];
        try
        {
            foreach (var row in rows)
            {
                written.Add(await SaveRowAsync(table, row, upsert, 0, () => ++count, tx, cancellationToken).ConfigureAwait(false));
            }
        }
        finally
        {
            _gathered = null;
        }

        foreach (var change in (gathered ?? []).GroupBy(c => (c.SchemaName, c.TableName, c.Action)).Select(g => g.First() with { Rows = [.. g.SelectMany(c => c.Rows)], Count = g.Sum(c => c.Count) }))
        {
            await Changes!.WrittenAsync(_connection, tx, change, cancellationToken).ConfigureAwait(false);
        }

        return await EchoAsync(table, written, rows, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Narrows rows as written to what the caller may see of them: the columns it gave, the table's key, and the
    /// columns it may read. The policies' presets and the values the database filled in others are not told, as a
    /// query would not tell them. Related rows likewise, by their own tables.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="written">The rows as written.</param>
    /// <param name="given">The rows as the request gave them, in the same order.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rows to answer with.</returns>
    public async Task<List<Dictionary<string, object?>>> EchoAsync(
        RestTable table,
        IReadOnlyList<Dictionary<string, object?>> written,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> given,
        CancellationToken cancellationToken
    )
    {
        if (_access is null)
        {
            return [.. written];
        }

        static object? Given(IReadOnlyDictionary<string, object?>? row, string name) =>
            row?.FirstOrDefault(p => string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase)).Value;

        var read = Rules(table, DmAction.Read);
        var echoed = new List<Dictionary<string, object?>>(written.Count);
        for (var i = 0; i < written.Count; i++)
        {
            var mine = i < given.Count ? given[i] : null;
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, value) in written[i])
            {
                if (table.Columns.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    if ((read.Allowed && (read.Columns is null || read.Columns.Contains(name)))
                        || table.Keys.Contains(name, StringComparer.OrdinalIgnoreCase)
                        || (mine?.Keys.Contains(name, StringComparer.OrdinalIgnoreCase) ?? false))
                    {
                        row[name] = value;
                    }

                    continue;
                }

                if (await _relation(table, name, cancellationToken).ConfigureAwait(false) is not { } relation)
                {
                    continue;
                }

                row[name] = value switch
                {
                    Dictionary<string, object?> one => (
                        await EchoAsync(relation.Other, [one], Given(mine, name) is IReadOnlyDictionary<string, object?> o ? [o] : [], cancellationToken).ConfigureAwait(false)
                    )[0],
                    List<Dictionary<string, object?>> many => await EchoAsync(
                            relation.Other,
                            many,
                            Given(mine, name) is IEnumerable<IReadOnlyDictionary<string, object?>> m ? [.. m] : [],
                            cancellationToken
                        )
                        .ConfigureAwait(false),
                    _ => value,
                };
            }

            echoed.Add(row);
        }

        return echoed;
    }

    /// <summary>
    /// Sets columns of the rows that match a condition, and the policies' filter: with the policies' presets, and
    /// refused, all of it, if a row would break their check.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="set">The columns and their values.</param>
    /// <param name="filter">The condition, over the table's own columns; <c>true</c> for every row.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="dryRun">Whether to roll the update back, reporting how many rows it would have changed.</param>
    /// <returns>The number of rows updated.</returns>
    /// <exception cref="ArgumentException">It would change more rows than <see cref="RestLimits.MaxAffectedRows"/>: nothing is changed.</exception>
    /// <exception cref="UnauthorizedAccessException">No policy lets the caller update, or a row would break a policy's check.</exception>
    /// <param name="keys">Where to add the keys of the rows changed (read as the update runs), or null.</param>
    public async Task<int> UpdateAsync(
        RestTable table,
        IReadOnlyDictionary<string, object?> set,
        RestNode filter,
        IDbTransaction? tx,
        CancellationToken cancellationToken,
        bool dryRun = false,
        List<IReadOnlyDictionary<string, object?>>? keys = null
    )
    {
        RequireWritable(table);
        RefuseValues(filter);
        var rules = Require(table, DmAction.Update);
        var written = _access is null ? set : await WritableAsync(table, rules, set, cancellationToken).ConfigureAwait(false);
        if (written.Count == 0)
        {
            return 0;
        }

        var policy = Trusted(await BindAsync(Condition(rules.Filter), cancellationToken).ConfigureAwait(false));
        var check = Trusted(await BindAsync(Condition(rules.Check), cancellationToken).ConfigureAwait(false));
        var (_, condition) = await WriteConditionAsync(table, filter, policy, cancellationToken).ConfigureAwait(false);
        var values = Values(table, written, "set");
        var byColumn = values.ToDictionary(v => v.Column.Name, v => (SqlNode)v.Value, StringComparer.OrdinalIgnoreCase);

        // The check reads the row as the update leaves it: the values set, and the other columns as they are.
        var checkSource = new SqlSource(table.QualifiedName, "t", table.ToTable(), null);
        var checkCondition = check is null ? null : new RestTranslator(_dialect, NoRelations(), _limits).Condition(check, table, checkSource);
        // The check is written by a renderer of its own: one that writes columns as values does not qualify them, and
        // the request's condition may correlate subqueries that need its columns qualified.
        var parameters = new DmlParameters();
        var renderer = new SqlRenderer(_dialect, parameters).For(values.ConvertAll(v => v.Value), condition);
        var sql = $"UPDATE {table.QualifiedName} SET {string.Join(", ", values.Select(v => $"{_dialect.Column(v.Column.Name)} = {renderer.Render(v.Value)}"))}"
            + $" WHERE {renderer.Render(condition)}";
        if (checkCondition is not null)
        {
            SqlRenderer checkRenderer = null!;
            checkRenderer = new SqlRenderer(_dialect, parameters)
            {
                ColumnWriter = c => byColumn.TryGetValue(c.ColumnName, out var value) ? checkRenderer.Render(value) : null,
            }.For(values.ConvertAll(v => v.Value), checkCondition);
            sql += $" AND {checkRenderer.Render(checkCondition)}";
        }

        string? countSql = null;
        var countParameters = new DmlParameters();
        if (checkCondition is not null)
        {
            countSql = $"SELECT COUNT(*) FROM {table.QualifiedName} WHERE {new SqlRenderer(_dialect, countParameters).For(condition).Render(condition)}";
        }

        return await WriteAsync(
                tx,
                dryRun,
                _limits.MaxAffectedRows,
                async transaction =>
                {
                    // The rows changed are read as the update runs, when their changes are heard: then their count is
                    // what the check is held to.
                    var changed = await ChangedRowsAsync(table, condition, sql, parameters, transaction!, cancellationToken, keys is not null).ConfigureAwait(false);
                    var matched = changed is { Before: true } ? changed.Value.Rows.Count
                        : countSql is null ? -1
                        : await Traced(table.Name, countSql, () => _connection.ExecuteScalarAsync<long>(DmScope.Command(countSql, countParameters, transaction, cancellationToken))).ConfigureAwait(false);
                    var updated = changed is { Before: false } after
                        ? after.Rows.Count
                        : await Traced(table.Name, sql, () => _connection.ExecuteAsync(DmScope.Command(sql, parameters, transaction, cancellationToken))).ConfigureAwait(false);
                    if (checkCondition is not null && matched >= 0 && updated < matched)
                    {
                        throw new UnauthorizedAccessException($"The update would leave rows of {table.Name} that break the policies' check; nothing was changed.");
                    }

                    if (changed is { } rows)
                    {
                        keys?.AddRange(rows.Rows.Select(r => KeyValues(table, r)));
                        await TellAsync(table, DmChangeAction.Update, rows.Rows, updated, transaction!, cancellationToken).ConfigureAwait(false);
                    }

                    return updated;
                }
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes the rows that match a condition, and the policies' filter.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="filter">The condition, over the table's own columns; <c>true</c> for every row.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="dryRun">Whether to roll the delete back, reporting how many rows it would have deleted.</param>
    /// <returns>The number of rows deleted.</returns>
    /// <exception cref="ArgumentException">It would delete more rows than <see cref="RestLimits.MaxAffectedRows"/>: nothing is deleted.</exception>
    /// <exception cref="UnauthorizedAccessException">No policy lets the caller delete.</exception>
    /// <param name="keys">Where to add the keys of the rows deleted (read as the delete runs), or null.</param>
    public async Task<int> DeleteAsync(
        RestTable table,
        RestNode filter,
        IDbTransaction? tx,
        CancellationToken cancellationToken,
        bool dryRun = false,
        List<IReadOnlyDictionary<string, object?>>? keys = null
    )
    {
        RequireWritable(table);
        RefuseValues(filter);
        var rules = Require(table, DmAction.Delete);
        var policy = Trusted(await BindAsync(Condition(rules.Filter), cancellationToken).ConfigureAwait(false));
        var (_, condition) = await WriteConditionAsync(table, filter, policy, cancellationToken).ConfigureAwait(false);
        var parameters = new DmlParameters();
        var sql = $"DELETE FROM {table.QualifiedName} WHERE {new SqlRenderer(_dialect, parameters).For(condition).Render(condition)}";
        return await WriteAsync(
                tx,
                dryRun,
                _limits.MaxAffectedRows,
                async transaction =>
                {
                    var changed = await ChangedRowsAsync(table, condition, sql, parameters, transaction!, cancellationToken, keys is not null).ConfigureAwait(false);
                    var deleted = changed is { Before: false } after
                        ? after.Rows.Count
                        : await Traced(table.Name, sql, () => _connection.ExecuteAsync(DmScope.Command(sql, parameters, transaction, cancellationToken))).ConfigureAwait(false);
                    if (changed is { } rows)
                    {
                        keys?.AddRange(rows.Rows.Select(r => KeyValues(table, r)));
                        await TellAsync(table, DmChangeAction.Delete, rows.Rows, deleted, transaction!, cancellationToken).ConfigureAwait(false);
                    }

                    return deleted;
                }
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes the rows that match a condition, and the policies' filter, after they are read: their keys are locked and
    /// read first, in the transaction; <paramref name="read"/> reads the rows by those keys (as the caller, with what it
    /// selects) before they go; then exactly those rows are deleted. No row is deleted unread, nor read and kept.
    /// </summary>
    /// <param name="table">The table, which has a key.</param>
    /// <param name="filter">The condition, over the table's own columns; <c>true</c> for every row.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="read">Reads the rows by their keys before they are deleted.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The keys of the rows deleted.</returns>
    /// <exception cref="ArgumentException">The table has no key, or the rows are more than <see cref="RestLimits.MaxAffectedRows"/>.</exception>
    /// <exception cref="UnauthorizedAccessException">No policy lets the caller delete.</exception>
    /// <exception cref="InvalidOperationException">Rows read were changed by another before they were deleted.</exception>
    public async Task<List<IReadOnlyDictionary<string, object?>>> DeleteReadingAsync(
        RestTable table,
        RestNode filter,
        IDbTransaction tx,
        Func<IReadOnlyList<IReadOnlyDictionary<string, object?>>, Task> read,
        CancellationToken cancellationToken
    )
    {
        RequireWritable(table);
        RefuseValues(filter);
        if (table.Keys.Count == 0)
        {
            throw new ArgumentException($"{table.Name} has no key: its rows cannot be read by key before they are deleted.");
        }

        var rules = Require(table, DmAction.Delete);
        var policy = Trusted(await BindAsync(Condition(rules.Filter), cancellationToken).ConfigureAwait(false));
        var (_, condition) = await WriteConditionAsync(table, filter, policy, cancellationToken).ConfigureAwait(false);

        // The keys, locked until the transaction ends: no one changes or deletes those rows meanwhile.
        var columns = ChangeColumns(table, Changes?.Wants(table.SchemaName, table.Name) ?? []);
        var parameters = new DmlParameters();
        var where = new SqlRenderer(_dialect, parameters).For(condition).Render(condition);
        var list = string.Join(", ", columns.Select(_dialect.Column));
        var select = _dialect.ProviderType switch
        {
            DbProviderType.SqlServer => $"SELECT {list} FROM {table.QualifiedName} WITH (UPDLOCK, HOLDLOCK) WHERE {where}",
            DbProviderType.MySql or DbProviderType.PostgreSql => $"SELECT {list} FROM {table.QualifiedName} WHERE {where} FOR UPDATE",
            _ => $"SELECT {list} FROM {table.QualifiedName} WHERE {where}",
        };
        var rows = (await Traced(table.Name, select, () => _connection.QueryAsync(DmScope.Command(select, parameters, tx, cancellationToken))).ConfigureAwait(false))
            .Select(r => (IReadOnlyDictionary<string, object?>)((IDictionary<string, object?>)r).ToDictionary(p => p.Key, p => p.Value is DBNull ? null : p.Value, StringComparer.OrdinalIgnoreCase))
            .ToList();
        Allowed(rows.Count, _limits.MaxAffectedRows);
        var keys = rows.ConvertAll(r => KeyValues(table, r));
        await read(keys).ConfigureAwait(false);

        // Exactly those rows, by their keys, in batches within every provider's limit on parameters.
        var deleted = 0;
        var perBatch = Math.Max(1, KeysPerQuery / table.Keys.Count);
        for (var i = 0; i < keys.Count; i += perBatch)
        {
            var batch = keys.GetRange(i, Math.Min(perBatch, keys.Count - i));
            var (_, byKeys) = await WriteConditionAsync(table, And(filter, new RestTrusted(KeyFilter(table, batch)))!, policy, cancellationToken).ConfigureAwait(false);
            var values = new DmlParameters();
            var sql = $"DELETE FROM {table.QualifiedName} WHERE {new SqlRenderer(_dialect, values).For(byKeys).Render(byKeys)}";
            deleted += await Traced(table.Name, sql, () => _connection.ExecuteAsync(DmScope.Command(sql, values, tx, cancellationToken))).ConfigureAwait(false);
        }

        if (deleted != keys.Count)
        {
            throw new InvalidOperationException($"Rows of {table.Name} changed while they were deleted: {keys.Count} were read, {deleted} deleted. Nothing was deleted.");
        }

        await TellAsync(table, DmChangeAction.Delete, rows, deleted, tx, cancellationToken).ConfigureAwait(false);
        return keys;
    }

    /// <summary>
    /// Makes the condition that matches rows by their keys: <c>key in (…)</c>, or for a key of several columns each
    /// row's columns equal, ORed.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="keys">The rows' keys, by column.</param>
    /// <returns>The condition.</returns>
    public static RestNode KeyFilter(RestTable table, IReadOnlyList<IReadOnlyDictionary<string, object?>> keys)
    {
        if (keys.Count == 0)
        {
            // No row: a condition no row meets.
            return new RestBinary("eq", new RestLiteral(1), new RestLiteral(0));
        }

        if (table.Keys.Count == 1)
        {
            var key = table.Keys[0];
            return new RestIn(new RestPath([key]), [.. keys.Select(k => new RestLiteral(Find(k, key)))]);
        }

        return keys
            .Select(k => table.Keys.Select(c => (RestNode)new RestBinary("eq", new RestPath([c]), new RestLiteral(Find(k, c)))).Aggregate((a, b) => new RestBinary("and", a, b)))
            .Aggregate((a, b) => new RestBinary("or", a, b));
    }

    // A row's key values, by column.
    private static IReadOnlyDictionary<string, object?> KeyValues(RestTable table, IReadOnlyDictionary<string, object?> row) =>
        table.Keys.ToDictionary(k => k, k => Find(row, k), StringComparer.OrdinalIgnoreCase);

    private static object? Find(IReadOnlyDictionary<string, object?> row, string column) =>
        row.TryGetValue(column, out var value) ? value : row.FirstOrDefault(p => string.Equals(p.Key, column, StringComparison.OrdinalIgnoreCase)).Value;

    // A row as it is inserted: the columns the caller may write, and the policies' presets.
    private async Task<Dictionary<string, object?>> PreparedAsync(RestTable table, DmAccessRules rules, IReadOnlyDictionary<string, object?> row, CancellationToken cancellationToken) =>
        _access is null
            ? new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase)
            : await WritableAsync(table, rules, row, cancellationToken).ConfigureAwait(false);

    // Inserts prepared rows one by one, each returning the key the database generated, and tells the change.
    private async Task<List<Dictionary<string, object?>>> InsertPreparedAsync(
        RestTable table,
        List<Dictionary<string, object?>> rows,
        RestNode? check,
        IDbTransaction tx,
        CancellationToken cancellationToken
    )
    {
        foreach (var values in rows)
        {
            // The check is read first, over the row's values: a key returned must be the row's own.
            if (check is not null)
            {
                var (checkSql, checkParameters) = CheckStatement(table, values, check);
                if (await Traced(table.Name, checkSql, () => _connection.ExecuteScalarAsync<int>(DmScope.Command(checkSql, checkParameters, tx, cancellationToken))).ConfigureAwait(false) != 1)
                {
                    throw new UnauthorizedAccessException($"A row breaks the policies' check on {table.Name}; nothing was inserted.");
                }
            }

            var (sql, parameters) = InsertStatement(table, values, null);
            if (table.Generated is { } generated && !values.ContainsKey(generated))
            {
                var returning = _dialect.ReturningGeneratedKey(sql, _dialect.Column(generated));
                var key = await Traced(table.Name, returning, () => _connection.ExecuteScalarAsync<object>(DmScope.Command(returning, parameters, tx, cancellationToken))).ConfigureAwait(false);
                values[generated] = RestTranslator.Value(key is DBNull ? null : key, table.Column(generated, "the key"));
            }
            else
            {
                await Traced(table.Name, sql, () => _connection.ExecuteAsync(DmScope.Command(sql, parameters, tx, cancellationToken))).ConfigureAwait(false);
            }
        }

        await TellAsync(table, DmChangeAction.Insert, rows, rows.Count, tx, cancellationToken).ConfigureAwait(false);
        return rows;
    }

    // The keys (and the columns asked for) of the rows an UPDATE or DELETE changes, when the table's changes are heard
    // or the caller captures them; else null. PostgreSQL runs the statement itself, RETURNING them (Before = false); the others read them first in
    // the write's transaction, locked so that the statement changes those rows and no others (Before = true).
    private async Task<(List<Dictionary<string, object?>> Rows, bool Before)?> ChangedRowsAsync(
        RestTable table,
        SqlNode condition,
        string sql,
        DmlParameters parameters,
        IDbTransaction transaction,
        CancellationToken cancellationToken,
        bool capture = false
    )
    {
        var wanted = Changes?.Wants(table.SchemaName, table.Name);
        if (wanted is null && !capture)
        {
            return null;
        }

        var columns = string.Join(", ", ChangeColumns(table, wanted ?? []).Select(_dialect.Column));
        if (columns.Length == 0)
        {
            // A table without a key: the rows are counted, not named.
            columns = "1 AS dm_row";
        }

        async Task<List<Dictionary<string, object?>>> Read(string text, DmlParameters values) =>
            [
                .. (await Traced(table.Name, text, () => _connection.QueryAsync(DmScope.Command(text, values, transaction, cancellationToken))).ConfigureAwait(false))
                    .Select(r => ((IDictionary<string, object?>)r).Where(p => p.Key != "dm_row").ToDictionary(p => p.Key, p => p.Value is DBNull ? null : p.Value, StringComparer.OrdinalIgnoreCase)),
            ];

        if (_dialect.ProviderType == DbProviderType.PostgreSql)
        {
            return (await Read($"{sql} RETURNING {columns}", parameters).ConfigureAwait(false), false);
        }

        var keys = new DmlParameters();
        var where = new SqlRenderer(_dialect, keys).For(condition).Render(condition);
        var select = _dialect.ProviderType switch
        {
            DbProviderType.SqlServer => $"SELECT {columns} FROM {table.QualifiedName} WITH (UPDLOCK, HOLDLOCK) WHERE {where}",
            DbProviderType.MySql => $"SELECT {columns} FROM {table.QualifiedName} WHERE {where} FOR UPDATE",
            _ => $"SELECT {columns} FROM {table.QualifiedName} WHERE {where}",
        };
        return (await Read(select, keys).ConfigureAwait(false), true);
    }

    // The columns a change tells of a table's rows: its key, and those the hearer asks for that the table has.
    private static List<string> ChangeColumns(RestTable table, IReadOnlyList<string> wanted) =>
        [
            .. table.Keys
                .Concat(wanted.Select(w => table.Columns.FirstOrDefault(c => string.Equals(c.Name, w, StringComparison.OrdinalIgnoreCase))?.Name).OfType<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase),
        ];

    // Tells a change, with the rows' keys and the columns asked for, in the write's transaction.
    private Task TellAsync(RestTable table, DmChangeAction action, IReadOnlyList<IReadOnlyDictionary<string, object?>> rows, int count, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        if (Changes?.Wants(table.SchemaName, table.Name) is not { } wanted)
        {
            return Task.CompletedTask;
        }

        var columns = ChangeColumns(table, wanted);
        var told = rows
            .Select(r => (IReadOnlyDictionary<string, object?>)columns.ToDictionary(c => c, c => r.FirstOrDefault(p => string.Equals(p.Key, c, StringComparison.OrdinalIgnoreCase)).Value, StringComparer.OrdinalIgnoreCase))
            .ToList();
        var change = new DmChange(table.SchemaName, table.Name, action, table.Keys, told, count);
        if (_gathered is not null)
        {
            _gathered.Add(change);
            return Task.CompletedTask;
        }

        return Changes.WrittenAsync(_connection, transaction, change, cancellationToken);
    }

    // A row's or a set's values, each checked against its column; names match columns ignoring case, so two names
    // of one column would write it twice.
    private static List<(RestColumn Column, SqlValue Value)> Values(RestTable table, IReadOnlyDictionary<string, object?> values, string what)
    {
        var columns = values.Select(p =>
        {
            var column = table.Column(p.Key, what);
            return (Column: column, Value: new SqlValue(column.Type, RestTranslator.Value(p.Value, column)));
        }).ToList();
        if (columns.GroupBy(c => c.Column.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1) is { } twice)
        {
            throw new ArgumentException($"{what} names the column {twice.Key} more than once.");
        }

        return columns;
    }

    private void RequireWritable(RestTable table)
    {
        if (!table.Writable)
        {
            throw new ArgumentException($"{table.Name} is a view, whose rows are read, not written.");
        }

        _written.Add(table);
    }

    // A key read from the database, so that an int and a long of one value match.
    private static object? Normalized(object? value) =>
        value switch
        {
            null or DBNull => null,
            byte or sbyte or short or ushort or int or uint or long or ulong or decimal => Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture),
            byte[] bytes => Convert.ToBase64String(bytes),
            _ => value,
        };

    private static string KeyOf(Dictionary<string, object?> row, IEnumerable<string> columns) =>
        string.Join('\u001f', columns.Select(c => Normalized(Find(row, c)) is { } v ? $"{v.GetType().Name}:{Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture)}" : "\u0000"));

    private static object? Find(Dictionary<string, object?> row, string column) =>
        row.TryGetValue(column, out var value) ? value : row.FirstOrDefault(p => string.Equals(p.Key, column, StringComparison.OrdinalIgnoreCase)).Value;

    // The keys expands need, read with the rows even when not selected.
    private static List<string> ExpandKeys(RestQuery query, RestTable table, IReadOnlyDictionary<(RestTable Table, string Name), RestRelation> relations) =>
        [.. query.Expand.SelectMany(e => relations[(table, e.Relation)].Keys.Select(k => k.This)).Distinct(StringComparer.OrdinalIgnoreCase)];

    // Removes the columns read only for expands or a cursor from rows whose query selected others.
    private static void Strip(List<Dictionary<string, object?>> rows, RestQuery query, IReadOnlyList<string> extras)
    {
        if (query.Select.Count == 0)
        {
            return;
        }

        var extra = extras.Where(k => !query.Select.Contains(k, StringComparer.OrdinalIgnoreCase)).ToList();
        foreach (var row in rows)
        {
            foreach (var key in extra)
            {
                row.Remove(row.Keys.First(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase)));
            }
        }
    }

    // A write's condition: the request's, whose related tables are read through their own policies, ANDed with the
    // policies' filter for the write. A request's condition that reads the table's columns reads its rows, so, as in
    // PostgreSQL, the caller must be allowed to read them: the columns it compares, and only the rows the read
    // policies show (how many rows a write changed would otherwise tell what reading would not).
    private async Task<(SqlSource Source, SqlNode Condition)> WriteConditionAsync(
        RestTable table,
        RestNode filter,
        RestNode? policy,
        CancellationToken cancellationToken
    )
    {
        var relations = await ResolveAsync(new RestQuery { Filter = filter }, table, cancellationToken).ConfigureAwait(false);
        await PrepareReadFiltersAsync(relations.Values.Select(r => r.Other), cancellationToken).ConfigureAwait(false);
        RestNode? read = null;
        if (_access is not null && ReadsColumns(filter))
        {
            await PrepareReadFiltersAsync([table], cancellationToken).ConfigureAwait(false);
            read = ReadFilter(table);
        }

        RequireReadable(new RestQuery { Filter = filter }, table, relations);
        var source = new SqlSource(table.QualifiedName, "t", table.ToTable(), null);
        var translator = new RestTranslator(_dialect, relations, _limits, _access is null ? null : ReadFilter);
        return (source, translator.Condition(And(And(filter, policy), read)!, table, source));
    }

    // The table's columns a policy's check reads (a check compares its own table's columns only).
    private static IEnumerable<string> CheckedColumns(RestNode node)
    {
        var pending = new Stack<RestNode>([node]);
        while (pending.TryPop(out var current))
        {
            switch (current)
            {
                case RestPath path:
                    yield return path.Segments[0];
                    break;
                case RestTrusted trusted:
                    pending.Push(trusted.Inner);
                    break;
                case RestBinary binary:
                    pending.Push(binary.Left);
                    pending.Push(binary.Right);
                    break;
                case RestNot not:
                    pending.Push(not.Operand);
                    break;
                case RestIn @in:
                    pending.Push(@in.Operand);
                    break;
                case RestCall call:
                    call.Arguments.ToList().ForEach(pending.Push);
                    break;
            }
        }
    }

    // Whether a condition compares any column, of the table or a related one.
    private static bool ReadsColumns(RestNode node)
    {
        var pending = new Stack<RestNode>([node]);
        while (pending.TryPop(out var current))
        {
            switch (current)
            {
                case RestPath or RestLambda:
                    return true;
                case RestBinary binary:
                    pending.Push(binary.Left);
                    pending.Push(binary.Right);
                    break;
                case RestNot not:
                    pending.Push(not.Operand);
                    break;
                case RestIn @in:
                    pending.Push(@in.Operand);
                    break;
                case RestCall call:
                    call.Arguments.ToList().ForEach(pending.Push);
                    break;
            }
        }

        return false;
    }

    private static Dictionary<(RestTable Table, string Name), RestRelation> NoRelations() => [];

    // A statement in a span of its own, for OpenTelemetry: its SQL with parameters by name, never their values.
    private Task<T> Traced<T>(string table, string sql, Func<Task<T>> run) =>
        DmTelemetry.TracedAsync(
            _dialect.ProviderType switch
            {
                DbProviderType.SqlServer => "mssql",
                DbProviderType.PostgreSql => "postgresql",
                DbProviderType.MySql => "mysql",
                _ => "sqlite",
            },
            table,
            sql,
            run
        );

    // A grouped query: the rows the policies let through and its filter steps pass, grouped; the columns grouped and
    // aggregated must be ones the caller may read, and the results are the groups.
    private async Task<(List<Dictionary<string, object?>> Rows, long? Count, string? NextCursor)> ApplyAsync(
        RestQuery query,
        RestTable table,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    )
    {
        var apply = query.Apply!;
        if (query.Select.Count > 0 || query.Expand.Count > 0 || query.After is not null)
        {
            throw new ArgumentException("With $apply the results are the groups: $select, $expand and $after do not apply.");
        }

        RefuseValues(apply.Filter);
        foreach (var aggregate in apply.Aggregates.Where(a => a.Value is not null))
        {
            RefuseValues(aggregate.Value);
        }

        // The rows grouped, as one query reads them: its condition, its keys and the values it aggregates.
        var read = new RestQuery
        {
            Filter = And(apply.Filter, query.Search is { } search ? new RestSearch(search) : null),
            OrderBy = [.. apply.GroupBy.Select(g => new RestOrder(g, false)), .. apply.Aggregates.Where(a => a.Value is not null).Select(a => new RestOrder(a.Value!, false))],
        };
        var relations = await ResolveAsync(read, table, cancellationToken).ConfigureAwait(false);
        _reads = [table, .. relations.Values.Select(r => r.Other).Where(t => !ReferenceEquals(t, table)).Distinct()];
        await PrepareReadFiltersAsync(relations.Values.Select(r => r.Other).Prepend(table), cancellationToken).ConfigureAwait(false);
        RequireReadable(read, table, relations);

        var translator = new RestTranslator(_dialect, relations, _limits, _access is null ? null : ReadFilter);
        var select = translator.Grouped(query, table, And(read.Filter, ReadFilter(table)));
        var rows = await ReadAsync(select, tx, cancellationToken).ConfigureAwait(false);
        long? count = null;
        if (query.Count)
        {
            var total = RestTranslator.CountGroups(select);
            var parameters = new DmlParameters();
            var sql = new SqlRenderer(_dialect, parameters).For(total).Render(total);
            count = await CachedAsync(
                    "count",
                    sql,
                    parameters,
                    tx,
                    () => Traced(table.Name, sql, () => _connection.ExecuteScalarAsync<long>(DmScope.Command(sql, parameters, tx, cancellationToken))),
                    RestCacheCodec.Encode,
                    RestCacheCodec.DecodeCount,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        return (rows, count, null);
    }

    // The keyset a query is paged by: its own ordering columns, then the table's key; null for a table without a key,
    // an ordering by related or computed values, or one the caller may not read every column of.
    private List<(string Column, bool Descending)>? Keyset(RestQuery query, RestTable table)
    {
        if (table.Keys.Count == 0)
        {
            return null;
        }

        var keyset = new List<(string Column, bool Descending)>();
        foreach (var order in query.OrderBy)
        {
            if (order.Key is not RestPath { Segments.Count: 1 } path)
            {
                return null;
            }

            var column = table.Column(path.Segments[0], "$orderby").Name;
            if (!keyset.Exists(k => string.Equals(k.Column, column, StringComparison.OrdinalIgnoreCase)))
            {
                keyset.Add((column, order.Descending));
            }
        }

        foreach (var key in table.Keys)
        {
            var column = table.Column(key, "the key").Name;
            if (!keyset.Exists(k => string.Equals(k.Column, column, StringComparison.OrdinalIgnoreCase)))
            {
                keyset.Add((column, false));
            }
        }

        return Readable(table) is { } readable && keyset.Exists(k => !readable.Contains(k.Column)) ? null : keyset;
    }

    private (string Sql, DmlParameters Parameters) InsertStatement(RestTable table, IReadOnlyDictionary<string, object?> row, RestNode? check)
    {
        if (row.Count == 0)
        {
            return check is null
                ? (_dialect.InsertDefaults(table.QualifiedName), new DmlParameters())
                : throw new ArgumentException($"A row of {table.Name} with no values cannot be checked against the policies: give its values.");
        }

        var values = Values(table, row, "the row");
        var parameters = new DmlParameters();
        var columns = string.Join(", ", values.Select(v => _dialect.Column(v.Column.Name)));
        if (check is null)
        {
            var plain = new SqlRenderer(_dialect, parameters).For(values.ConvertAll(v => v.Value));
            return ($"INSERT INTO {table.QualifiedName} ({columns}) VALUES ({string.Join(", ", values.Select(v => plain.Render(v.Value)))})", parameters);
        }

        // The row is inserted only if its values pass the check: read as the row's values, a column it does not give
        // as NULL. A column the database would fill is not NULL in the row inserted, so the row must give it.
        foreach (var name in CheckedColumns(check))
        {
            if (table.Column(name, "the policies' check") is { Filled: true } filled && !values.Exists(v => ReferenceEquals(v.Column, filled)))
            {
                throw new ArgumentException($"The policies' check on {table.Name} reads {filled.Name}, which the database fills when a row does not give it: give its value.");
            }
        }

        var source = new SqlSource(table.QualifiedName, "t", table.ToTable(), null);
        var condition = new RestTranslator(_dialect, NoRelations(), _limits).Condition(check, table, source);
        var byColumn = values.ToDictionary(v => v.Column.Name, v => (SqlNode)v.Value, StringComparer.OrdinalIgnoreCase);
        SqlRenderer renderer = null!;
        renderer = new SqlRenderer(_dialect, parameters)
        {
            ColumnWriter = c => !ReferenceEquals(c.Source, source) ? null
                : byColumn.TryGetValue(c.ColumnName, out var value) ? renderer.Render(value)
                : "NULL",
        }.For(values.ConvertAll(v => v.Value), condition);
        return (
            $"INSERT INTO {table.QualifiedName} ({columns}) SELECT {string.Join(", ", values.Select(v => renderer.Render(v.Value)))} "
                + $"FROM (SELECT 1 AS dm_one) dm WHERE {renderer.Render(condition)}",
            parameters
        );
    }

    // A row and the rows it relates to, as SaveAsync writes them.
    private async Task<Dictionary<string, object?>> SaveRowAsync(
        RestTable table,
        IReadOnlyDictionary<string, object?> row,
        bool upsert,
        int depth,
        Func<int> counted,
        IDbTransaction tx,
        CancellationToken cancellationToken
    )
    {
        if (counted() > _limits.MaxInsertRows)
        {
            throw new ArgumentException($"A write holds at most {_limits.MaxInsertRows} rows, related ones included.");
        }

        var own = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var related = new List<(RestRelation Relation, object? Value)>();
        foreach (var (name, value) in row)
        {
            if (table.Columns.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                // An object or array for a column is its value as JSON, as a JSON column holds it.
                own[name] = value is IReadOnlyDictionary<string, object?> or IEnumerable<IReadOnlyDictionary<string, object?>>
                    ? System.Text.Json.JsonSerializer.Serialize(value)
                    : value;
                continue;
            }

            if (depth >= _limits.MaxExpandDepth)
            {
                throw new ArgumentException($"Related rows are written at most {_limits.MaxExpandDepth} levels deep.");
            }

            var relation = await _relation(table, name, cancellationToken).ConfigureAwait(false)
                ?? throw new ArgumentException($"'{name}' is not a column of {table.Name}, nor a relation requests may write through.");
            related.Add((relation, value));
        }

        // The rows this one refers to come first: their keys are its columns.
        var saved = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (relation, value) in related.Where(r => !r.Relation.Many && r.Value is not null))
        {
            var other = value as IReadOnlyDictionary<string, object?> ?? throw new ArgumentException($"{relation.Name} is one row: give it as an object.");
            var written = await SaveRowAsync(relation.Other, other, upsert: true, depth + 1, counted, tx, cancellationToken).ConfigureAwait(false);
            foreach (var (mine, theirs) in relation.Keys)
            {
                own[mine] = Find(written, theirs);
            }

            saved[relation.Name] = written;
        }

        // A related row (depth > 0) is written only as the request's permissions allow writing its table on its own: the
        // request was authorized for its own table.
        var isRelated = depth > 0;
        var result = upsert
            ? await UpsertRowAsync(table, own, isRelated, tx, cancellationToken).ConfigureAwait(false)
            : (await InsertReturningAsync(table, [own], tx, cancellationToken).ConfigureAwait(false))[0];

        // The rows that refer to it come after: its key is their columns.
        foreach (var (relation, value) in related.Where(r => r.Relation.Many))
        {
            var children = value as IEnumerable<IReadOnlyDictionary<string, object?>> ?? throw new ArgumentException($"{relation.Name} holds many rows: give them as an array.");
            var written = new List<Dictionary<string, object?>>();
            foreach (var child in children)
            {
                var filled = new Dictionary<string, object?>(child, StringComparer.OrdinalIgnoreCase);
                foreach (var (mine, theirs) in relation.Keys)
                {
                    filled[theirs] = Find(result, mine);
                }

                written.Add(await SaveRowAsync(relation.Other, filled, upsert: true, depth + 1, counted, tx, cancellationToken).ConfigureAwait(false));
            }

            saved[relation.Name] = written;
        }

        foreach (var (name, value) in saved)
        {
            result[name] = value;
        }

        return result;
    }

    // A row with its whole key given is updated if it exists, and the caller may change it; else it is inserted.
    private async Task<Dictionary<string, object?>> UpsertRowAsync(
        RestTable table,
        Dictionary<string, object?> row,
        bool related,
        IDbTransaction tx,
        CancellationToken cancellationToken
    )
    {
        async Task<Dictionary<string, object?>> InsertAsync()
        {
            if (related && !await MayWriteAsync(table, DmAction.Create, cancellationToken).ConfigureAwait(false))
            {
                throw new UnauthorizedAccessException($"This request may not insert into {table.Name}.");
            }

            return (await InsertReturningAsync(table, [row], tx, cancellationToken).ConfigureAwait(false))[0];
        }

        if (table.Keys.Count == 0 || table.Keys.Any(k => Find(row, k) is null))
        {
            return await InsertAsync().ConfigureAwait(false);
        }

        var key = table.Keys
            .Select(k => (RestNode)new RestBinary("eq", new RestPath([table.Column(k, "the key").Name]), new RestLiteral(Find(row, k))))
            .Aggregate((a, b) => new RestBinary("and", a, b));
        var set = row.Where(p => !table.Keys.Contains(p.Key, StringComparer.OrdinalIgnoreCase)).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        if (
            set.Count > 0
            && Rules(table, DmAction.Update).Allowed
            && (!related || await MayWriteAsync(table, DmAction.Update, cancellationToken).ConfigureAwait(false))
            && await UpdateAsync(table, set, key, tx, cancellationToken).ConfigureAwait(false) == 1
        )
        {
            return row;
        }

        // A row with the key, whoever's it is, as the key's constraint sees it.
        var source = new SqlSource(table.QualifiedName, "t", table.ToTable(), null);
        var condition = new RestTranslator(_dialect, NoRelations(), _limits).Condition(key, table, source);
        var parameters = new DmlParameters();
        var exists = await _connection
            .ExecuteScalarAsync<object>(
                DmScope.Command($"SELECT 1 FROM {table.QualifiedName} WHERE {new SqlRenderer(_dialect, parameters).For(condition).Render(condition)}", parameters, tx, cancellationToken)
            )
            .ConfigureAwait(false) is not null and not DBNull;
        if (!exists)
        {
            return table.Generated is { } generated && table.Keys.Contains(generated, StringComparer.OrdinalIgnoreCase)
                ? throw new ArgumentException($"No row of {table.Name} has the key {Find(row, generated)}, and a key the database generates is not inserted as given: leave it out to insert the row.")
                : await InsertAsync().ConfigureAwait(false);
        }

        return set.Count == 0
            ? row
            : throw new UnauthorizedAccessException($"A row of {table.Name} with that key exists, and the policies do not let this caller change it.");
    }

    // SELECT CASE WHEN the check holds for a row's values THEN 1 ELSE 0 END, a column the row does not give as NULL.
    private (string Sql, DmlParameters Parameters) CheckStatement(RestTable table, IReadOnlyDictionary<string, object?> row, RestNode check)
    {
        var values = row.Count == 0 ? [] : Values(table, row, "the row");
        foreach (var name in CheckedColumns(check))
        {
            if (table.Column(name, "the policies' check") is { Filled: true } filled && !values.Exists(v => ReferenceEquals(v.Column, filled)))
            {
                throw new ArgumentException($"The policies' check on {table.Name} reads {filled.Name}, which the database fills when a row does not give it: give its value.");
            }
        }

        var parameters = new DmlParameters();
        var source = new SqlSource(table.QualifiedName, "t", table.ToTable(), null);
        var condition = new RestTranslator(_dialect, NoRelations(), _limits).Condition(check, table, source);
        var byColumn = values.ToDictionary(v => v.Column.Name, v => (SqlNode)v.Value, StringComparer.OrdinalIgnoreCase);
        SqlRenderer renderer = null!;
        renderer = new SqlRenderer(_dialect, parameters)
        {
            ColumnWriter = c => !ReferenceEquals(c.Source, source) ? null
                : byColumn.TryGetValue(c.ColumnName, out var value) ? renderer.Render(value)
                : "NULL",
        }.For(values.ConvertAll(v => v.Value), condition);
        return ($"SELECT CASE WHEN {renderer.Render(condition)} THEN 1 ELSE 0 END", parameters);
    }

    private async Task<List<Dictionary<string, object?>>> ReadAsync(SqlSelect select, IDbTransaction? tx, CancellationToken cancellationToken)
    {
        var parameters = new DmlParameters();
        var sql = new SqlRenderer(_dialect, parameters).For(select).Render(select);
        return await CachedAsync(
                "rows",
                sql,
                parameters,
                tx,
                async () =>
                {
                    var rows = await Traced(select.From.Table.TableName, sql, () => _connection.QueryAsync(DmScope.Command(sql, parameters, tx, cancellationToken))).ConfigureAwait(false);
                    return rows.Select(r => ((IDictionary<string, object?>)r).ToDictionary(p => p.Key, p => p.Value is DBNull ? null : p.Value, StringComparer.Ordinal)).ToList();
                },
                RestCacheCodec.Encode,
                RestCacheCodec.DecodeRows,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    // A statement's results from the cache, when every table the request reads is cached and the statement runs
    // outside a transaction; else run, and kept for next time when their values can be.
    private async Task<T> CachedAsync<T>(
        string kind,
        string sql,
        DmlParameters parameters,
        IDbTransaction? tx,
        Func<Task<T>> run,
        Func<T, byte[]?> encode,
        Func<byte[], T> decode,
        CancellationToken cancellationToken
    )
    {
        if (Cache is not { } cache || tx is not null || _reads.Count == 0 || cache.Duration(_reads) is not { } duration || duration <= TimeSpan.Zero)
        {
            return await run().ConfigureAwait(false);
        }

        // The versions are read once a request: its statements read the tables as of one moment.
        _versions ??= await cache.Versions(_reads, cancellationToken).ConfigureAwait(false);
        if (cache.Key(_versions, kind, sql, parameters.Named) is not { } key)
        {
            return await run().ConfigureAwait(false);
        }

        if (await cache.Get(key, cancellationToken).ConfigureAwait(false) is { } kept)
        {
            DmTelemetry.CacheRequests.Add(1, new KeyValuePair<string, object?>("dappermatic.cache.result", "hit"));
            return decode(kept);
        }

        DmTelemetry.CacheRequests.Add(1, new KeyValuePair<string, object?>("dappermatic.cache.result", "miss"));
        var result = await run().ConfigureAwait(false);
        if (encode(result) is { } bytes)
        {
            await cache.Set(key, bytes, duration, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    // Reads a relation's rows for owners already read, by batches of their keys, and sets them under its name.
    private async Task ExpandAsync(
        List<Dictionary<string, object?>> owners,
        RestTable table,
        RestExpand expand,
        RestTranslator translator,
        IReadOnlyDictionary<(RestTable Table, string Name), RestRelation> relations,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    )
    {
        var relation = relations[(table, expand.Relation)];
        var thisKeys = relation.Keys.Select(k => k.This).ToList();
        var otherKeys = relation.Keys.Select(k => k.Other).ToList();
        var byKey = owners.GroupBy(o => KeyOf(o, thisKeys)).ToDictionary(g => g.Key, g => g.ToList());
        var keys = owners
            .Select(o => thisKeys.Select(k => Find(o, k)).ToArray())
            .Where(k => k.All(v => v is not null))
            .DistinctBy(k => string.Join('\u001f', k.Select(v => Convert.ToString(Normalized(v), System.Globalization.CultureInfo.InvariantCulture))))
            .Select(k => k.Select((v, i) => RestTranslator.Value(v, relation.Other.Column(otherKeys[i], relation.Name))).ToArray())
            .ToList();

        var extra = otherKeys.Concat(ExpandKeys(expand.Query, relation.Other, relations)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var perOwnerTop = expand.Query.Top is not null && await SupportsWindowsAsync(tx, cancellationToken).ConfigureAwait(false);
        var related = new List<Dictionary<string, object?>>();
        var perQuery = Math.Max(1, KeysPerQuery / otherKeys.Count);
        for (var i = 0; i < keys.Count; i += perQuery)
        {
            var select = translator.Related(relation, expand.Query, keys.GetRange(i, Math.Min(perQuery, keys.Count - i)), extra, perOwnerTop);

            // Every expand of the request reads at most so many rows together: one more than are left tells too many.
            var left = _limits.MaxExpandedRows - _expanded;
            var batch = await ReadAsync(select with { Take = Math.Min(select.Take ?? int.MaxValue, left + 1) }, tx, cancellationToken)
                .ConfigureAwait(false);
            _expanded += batch.Count;
            if (_expanded > _limits.MaxExpandedRows)
            {
                throw new ArgumentException(
                    $"The request's expands read more than {_limits.MaxExpandedRows} related rows: give them a $top or a $filter."
                );
            }

            related.AddRange(batch);
        }

        foreach (var nested in expand.Query.Expand)
        {
            await ExpandAsync(related, relation.Other, nested, translator, relations, tx, cancellationToken).ConfigureAwait(false);
        }

        var groups = related.GroupBy(r => KeyOf(r, otherKeys)).ToDictionary(g => g.Key, g => g.ToList());
        Strip(related, expand.Query, ExpandKeys(expand.Query, relation.Other, relations));
        if (expand.Query.Select.Count > 0)
        {
            foreach (var row in related)
            {
                foreach (var key in otherKeys.Where(k => !expand.Query.Select.Contains(k, StringComparer.OrdinalIgnoreCase)))
                {
                    row.Remove(row.Keys.First(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase)));
                }
            }
        }

        foreach (var (key, same) in byKey)
        {
            var rows = groups.TryGetValue(key, out var found) ? found : [];
            if (!perOwnerTop && expand.Query.Top is { } top)
            {
                rows = [.. rows.Take(top)];
            }

            foreach (var owner in same)
            {
                owner[expand.As ?? relation.Name] = relation.Many ? rows : rows.FirstOrDefault();
            }
        }
    }

    // Follows every relation the query names from its table, through the endpoint's declarations.
    private async Task<Dictionary<(RestTable Table, string Name), RestRelation>> ResolveAsync(
        RestQuery query,
        RestTable table,
        CancellationToken cancellationToken
    )
    {
        // Before anything walks the request: Walk below, and the translator, recurse through it.
        _limits.Check(query);

        var found = new Dictionary<(RestTable Table, string Name), RestRelation>();
        async Task<RestRelation> Relation(RestTable from, string name)
        {
            if (found.TryGetValue((from, name), out var known))
            {
                return known;
            }

            var relation = await _relation(from, name, cancellationToken).ConfigureAwait(false)
                ?? throw new ArgumentException($"'{name}' is not a column or relation of {from.Name} that requests may use.");
            found[(from, name)] = relation;
            return relation;
        }

        async Task Walk(RestNode? node, RestTable it, Dictionary<string, RestTable> variables)
        {
            switch (node)
            {
                case null or RestLiteral:
                    return;
                case RestPath path:
                    {
                        var (current, start) = variables.TryGetValue(path.Segments[0], out var bound) ? (bound, 1) : (it, 0);
                        for (var i = start; i < path.Segments.Count - 1; i++)
                        {
                            current = (await Relation(current, path.Segments[i]).ConfigureAwait(false)).Other;
                        }

                        return;
                    }

                case RestLambda lambda:
                    {
                        var segments = lambda.Collection.Segments;
                        var (current, start) = variables.TryGetValue(segments[0], out var bound) ? (bound, 1) : (it, 0);
                        for (var i = start; i < segments.Count; i++)
                        {
                            current = (await Relation(current, segments[i]).ConfigureAwait(false)).Other;
                        }

                        if (lambda.Predicate is not null)
                        {
                            var inner = new Dictionary<string, RestTable>(variables, StringComparer.Ordinal);
                            if (lambda.Variable is { } variable)
                            {
                                inner[variable] = current;
                            }

                            await Walk(lambda.Predicate, it, inner).ConfigureAwait(false);
                        }

                        return;
                    }

                case RestBinary binary:
                    await Walk(binary.Left, it, variables).ConfigureAwait(false);
                    await Walk(binary.Right, it, variables).ConfigureAwait(false);
                    return;
                case RestNot not:
                    await Walk(not.Operand, it, variables).ConfigureAwait(false);
                    return;
                case RestIn @in:
                    await Walk(@in.Operand, it, variables).ConfigureAwait(false);
                    return;
                case RestCall call:
                    foreach (var argument in call.Arguments)
                    {
                        await Walk(argument, it, variables).ConfigureAwait(false);
                    }

                    return;
            }
        }

        async Task Query(RestQuery q, RestTable from, int depth)
        {
            if (depth > _limits.MaxExpandDepth)
            {
                throw new ArgumentException($"$expand is nested at most {_limits.MaxExpandDepth} deep.");
            }

            await Walk(q.Filter, from, []).ConfigureAwait(false);
            foreach (var order in q.OrderBy)
            {
                await Walk(order.Key, from, []).ConfigureAwait(false);
            }

            foreach (var expand in q.Expand)
            {
                var relation = await Relation(from, expand.Relation).ConfigureAwait(false);
                await Query(expand.Query, relation.Other, depth + 1).ConfigureAwait(false);
            }
        }

        await Query(query, table, 0).ConfigureAwait(false);
        return found;
    }

    private async Task<bool> SupportsWindowsAsync(IDbTransaction? tx, CancellationToken cancellationToken)
    {
        if (_windows is { } known)
        {
            return known;
        }

        if (_dialect.ProviderType != DbProviderType.MySql)
        {
            return (_windows = true).Value;
        }

        var version = await _connection.GetDatabaseVersionAsync(tx, cancellationToken).ConfigureAwait(false);
        return (_windows = version.Major >= 8).Value;
    }

    // Runs a write in the caller's transaction, or one of its own; then rolls it back when it changed more rows than
    // allowed (refusing it) or was a dry run (reporting what it would have changed).
    private async Task<int> WriteAsync(IDbTransaction? tx, bool dryRun, int? maxAffected, Func<IDbTransaction?, Task<int>> work)
    {
        if (tx is not null)
        {
            if (dryRun)
            {
                throw new ArgumentException("A dry run rolls back a transaction of its own, so it cannot run in the caller's.");
            }

            return Allowed(await work(tx).ConfigureAwait(false), maxAffected);
        }

        if (_connection.State != ConnectionState.Open)
        {
            _connection.Open();
        }

        using var own = _connection.BeginTransaction();
        var affected = Allowed(await work(own).ConfigureAwait(false), maxAffected);
        if (!dryRun)
        {
            own.Commit();
        }

        return affected;
    }

    // The rows changed, if no more than allowed; disposing the transaction then rolls the change back.
    private static int Allowed(int affected, int? maxAffected) =>
        maxAffected is { } max && affected > max
            ? throw new ArgumentException($"The request would change {affected} rows, more than the {max} allowed; nothing was changed.")
            : affected;
}
