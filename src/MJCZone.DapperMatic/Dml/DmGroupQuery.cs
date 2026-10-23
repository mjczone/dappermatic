// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

#pragma warning disable SA1402 // A group query and its projection belong together.

using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using Dapper;
using MJCZone.DapperMatic.Dml.Internal;

namespace MJCZone.DapperMatic.Dml;

/// <summary>
/// Groups of a query's rows, one <c>GROUP BY</c>: filtered by <c>Having</c>, ordered and paged, and read by
/// <c>Select</c> over each group's <c>Key</c> and aggregates (<c>Count</c>, <c>Sum</c>, <c>Min</c>, <c>Max</c>,
/// <c>Average</c>, <c>Any</c>, <c>All</c>, each after any <c>Where</c>).
/// </summary>
/// <example>
/// <code>
/// var totals = await db.From&lt;Order&gt;()
///     .Where(o =&gt; o.PlacedAt &gt;= since)
///     .GroupBy(o =&gt; o.Customer!.Country)
///     .Having(g =&gt; g.Count() &gt; 10)
///     .OrderByDescending(g =&gt; g.Sum(o =&gt; o.Total))
///     .Select(g =&gt; new { Country = g.Key, Orders = g.Count(), Total = g.Sum(o =&gt; o.Total) })
///     .ToListAsync();
/// </code>
/// </example>
/// <typeparam name="TKey">The grouping key.</typeparam>
/// <typeparam name="TElement">What each row is to its group.</typeparam>
public sealed class DmGroupQuery<TKey, TElement>
{
    private readonly DmlRows _rows;
    private readonly LambdaExpression _key;
    private readonly LambdaExpression? _element;
    private readonly IReadOnlyList<LambdaExpression> _having;
    private readonly IReadOnlyList<(LambdaExpression Key, bool Descending)> _order;
    private readonly int? _skip;
    private readonly int? _take;

    /// <summary>
    /// Initializes a new instance of the <see cref="DmGroupQuery{TKey, TElement}"/> class.
    /// </summary>
    /// <param name="rows">The rows grouped.</param>
    /// <param name="key">The key, over the rows.</param>
    /// <param name="element">What each row is to its group, over the rows; null for the row itself.</param>
    internal DmGroupQuery(DmlRows rows, LambdaExpression key, LambdaExpression? element)
        : this(rows, key, element, [], [], null, null) { }

    private DmGroupQuery(
        DmlRows rows,
        LambdaExpression key,
        LambdaExpression? element,
        IReadOnlyList<LambdaExpression> having,
        IReadOnlyList<(LambdaExpression Key, bool Descending)> order,
        int? skip,
        int? take
    )
    {
        _rows = rows;
        _key = key;
        _element = element;
        _having = having;
        _order = order;
        _skip = skip;
        _take = take;
    }

    /// <summary>
    /// Gets the most groups the query returns, or null.
    /// </summary>
    internal int? TakeCount => _take;

    /// <summary>
    /// Gets the scope the query runs in.
    /// </summary>
    internal DmScope Scope => _rows.Scope;

    /// <summary>
    /// Keeps the groups that meet a condition, as well as any earlier ones: <c>HAVING</c>.
    /// </summary>
    /// <param name="predicate">The condition, over the group's key and aggregates.</param>
    /// <returns>The query.</returns>
    public DmGroupQuery<TKey, TElement> Having(Expression<Func<IGrouping<TKey, TElement>, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return new(_rows, _key, _element, [.. _having, predicate], _order, _skip, _take);
    }

    /// <summary>
    /// Orders the groups by their key or an aggregate, ascending, in place of any earlier ordering.
    /// </summary>
    /// <typeparam name="TOrder">The ordering key's type.</typeparam>
    /// <param name="key">The ordering key: <c>g =&gt; g.Key</c>, <c>g =&gt; g.Sum(o =&gt; o.Total)</c>.</param>
    /// <returns>The query.</returns>
    public DmGroupQuery<TKey, TElement> OrderBy<TOrder>(Expression<Func<IGrouping<TKey, TElement>, TOrder>> key) =>
        Ordered(key, descending: false, then: false);

    /// <summary>
    /// Orders the groups by their key or an aggregate, descending, in place of any earlier ordering.
    /// </summary>
    /// <typeparam name="TOrder">The ordering key's type.</typeparam>
    /// <param name="key">The ordering key.</param>
    /// <returns>The query.</returns>
    public DmGroupQuery<TKey, TElement> OrderByDescending<TOrder>(Expression<Func<IGrouping<TKey, TElement>, TOrder>> key) =>
        Ordered(key, descending: true, then: false);

    /// <summary>
    /// Orders groups with the same earlier keys by another, ascending.
    /// </summary>
    /// <typeparam name="TOrder">The ordering key's type.</typeparam>
    /// <param name="key">The ordering key.</param>
    /// <returns>The query.</returns>
    public DmGroupQuery<TKey, TElement> ThenBy<TOrder>(Expression<Func<IGrouping<TKey, TElement>, TOrder>> key) =>
        Ordered(key, descending: false, then: true);

    /// <summary>
    /// Orders groups with the same earlier keys by another, descending.
    /// </summary>
    /// <typeparam name="TOrder">The ordering key's type.</typeparam>
    /// <param name="key">The ordering key.</param>
    /// <returns>The query.</returns>
    public DmGroupQuery<TKey, TElement> ThenByDescending<TOrder>(Expression<Func<IGrouping<TKey, TElement>, TOrder>> key) =>
        Ordered(key, descending: true, then: true);

    /// <summary>
    /// Skips groups.
    /// </summary>
    /// <param name="count">The number of groups to skip.</param>
    /// <returns>The query.</returns>
    public DmGroupQuery<TKey, TElement> Skip(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return new(_rows, _key, _element, _having, _order, count, _take);
    }

    /// <summary>
    /// Returns at most a number of groups.
    /// </summary>
    /// <param name="count">The most groups to return.</param>
    /// <returns>The query.</returns>
    public DmGroupQuery<TKey, TElement> Take(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return new(_rows, _key, _element, _having, _order, _skip, count);
    }

    /// <summary>
    /// Reads each group as something made of its key and aggregates, which the database computes; the rest of the
    /// selector runs in .NET.
    /// </summary>
    /// <typeparam name="TResult">What each group is read as.</typeparam>
    /// <param name="selector">How: <c>g =&gt; new { g.Key, Count = g.Count(), Total = g.Sum(o =&gt; o.Total) }</c>.</param>
    /// <returns>The query.</returns>
    public DmGroupQuery<TKey, TElement, TResult> Select<TResult>(Expression<Func<IGrouping<TKey, TElement>, TResult>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return new(this, selector);
    }

    /// <summary>
    /// Counts the groups.
    /// </summary>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of groups.</returns>
    public async Task<long> CountAsync(IDbTransaction? tx = null, CancellationToken cancellationToken = default)
    {
        var (select, dialect, _) = Build(selector: null, _take);
        var count = new SqlSelect
        {
            From = select.From,
            FromSubquery = select with { Items = [new SqlSelectItem(new SqlText("1"), "one")], OrderBy = [] },
            Items = [new SqlSelectItem(new SqlAggregate("COUNT", null), null)],
        };
        var parameters = new DmlParameters();
        var sql = new SqlRenderer(dialect, parameters).For(count).Render(count);
        return await _rows.Scope.Connection
            .ExecuteScalarAsync<long>(DmScope.Command(sql, parameters, tx, cancellationToken))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the <c>SELECT</c> of the groups.
    /// </summary>
    /// <param name="selector">What each group is read as, or null for no select list.</param>
    /// <param name="take">The most groups to read, or null.</param>
    /// <returns>The query, the dialect, and the selector reading its values as <c>values =&gt; ...</c>, if any.</returns>
    internal (SqlSelect Select, DmlDialect Dialect, LambdaExpression? Reads) Build(LambdaExpression? selector, int? take)
    {
        var (select, scope, sources) = _rows.Open();
        var rows = _key.Parameters;

        // The key's values once, reused wherever the key is read, so that their parameters are the same everywhere.
        var known = new Dictionary<Expression, SqlNode>(ReferenceEqualityComparer.Instance);
        var keyValues = DmlGrouping.KeyValues(_key);
        foreach (var value in keyValues)
        {
            known[value] = Node(value, rows, sources, scope, null);
        }

        // A key value that reads no row, as GroupBy(o => 1) has, is the same for every row: grouping by it groups by
        // nothing. SQL Server refuses a parameter in GROUP BY, and MySQL would read GROUP BY 1 as the first item.
        List<SqlNode> groupBy = [.. keyValues.Where(v => DmlGrouping.ReadsRows(v, rows)).Select(v => known[v])];

        var having = _having.Select(h => DmlTranslator.Condition(DmlGrouping.Rewrite(h, _key, _element), sources, scope, aggregates: true, known));
        List<SqlOrder> order =
        [
            .. _order.Select(o =>
            {
                var (node, nullable) = DmlTranslator.Value(DmlGrouping.Rewrite(o.Key, _key, _element), sources, scope, aggregates: true, known);
                return new SqlOrder(node, o.Descending, nullable);
            }),
        ];

        LambdaExpression? reads = null;
        List<SqlSelectItem> items = [];
        if (selector is not null)
        {
            (reads, var parts) = DmlGrouping.Parts(DmlGrouping.Rewrite(selector, _key, _element));
            items = [.. parts.Select((part, i) => new SqlSelectItem(Node(part, rows, sources, scope, known), $"dm_p{i}"))];
        }

        return (
            select with
            {
                Items = items,
                GroupBy = groupBy,

                // Without GROUP BY, SQL makes one group even of no rows; LINQ makes none.
                Having = groupBy.Count > 0 ? [.. having] : [.. having, new SqlCompare(new SqlAggregate("COUNT", null), ">", new SqlText("0"))],
                OrderBy = order,
                Skip = _skip,
                Take = take,
            },
            scope.Dialect,
            reads
        );
    }

    // A value of the grouped rows, or a condition on them read as 1 or 0.
    private static SqlNode Node(
        Expression part,
        IReadOnlyList<ParameterExpression> rows,
        IReadOnlyList<SqlSource> sources,
        SqlScope scope,
        IReadOnlyDictionary<Expression, SqlNode>? known
    )
    {
        if (known is not null && known.TryGetValue(part, out var node))
        {
            return node;
        }

        var lambda = Expression.Lambda(part, rows);
        return part.Type == typeof(bool) && part is not MemberExpression && !DmlGrouping.IsAggregate(part)
            ? new SqlCase(DmlTranslator.Condition(lambda, sources, scope, aggregates: true, known), new SqlText("1"), new SqlText("0"))
            : DmlTranslator.Value(lambda, sources, scope, aggregates: true, known).Node;
    }

    private DmGroupQuery<TKey, TElement> Ordered(LambdaExpression key, bool descending, bool then)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (then && _order.Count == 0)
        {
            throw new InvalidOperationException("ThenBy and ThenByDescending follow OrderBy or OrderByDescending.");
        }

        return new(_rows, _key, _element, _having, then ? [.. _order, (key, descending)] : [(key, descending)], _skip, _take);
    }
}

/// <summary>
/// Groups read as something made of their keys and aggregates.
/// </summary>
/// <typeparam name="TKey">The grouping key.</typeparam>
/// <typeparam name="TElement">What each row is to its group.</typeparam>
/// <typeparam name="TResult">What each group is read as.</typeparam>
public sealed class DmGroupQuery<TKey, TElement, TResult>
{
    private readonly DmGroupQuery<TKey, TElement> _groups;
    private readonly Expression<Func<IGrouping<TKey, TElement>, TResult>> _selector;

    /// <summary>
    /// Initializes a new instance of the <see cref="DmGroupQuery{TKey, TElement, TResult}"/> class.
    /// </summary>
    /// <param name="groups">The groups.</param>
    /// <param name="selector">What each group is read as.</param>
    internal DmGroupQuery(DmGroupQuery<TKey, TElement> groups, Expression<Func<IGrouping<TKey, TElement>, TResult>> selector)
    {
        _groups = groups;
        _selector = selector;
    }

    /// <summary>
    /// Reads the groups.
    /// </summary>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The results.</returns>
    /// <exception cref="DmTranslationException">An expression has a part that cannot be written as SQL.</exception>
    public Task<List<TResult>> ToListAsync(IDbTransaction? tx = null, CancellationToken cancellationToken = default) =>
        ReadAsync(_groups.TakeCount, tx, cancellationToken);

    /// <summary>
    /// Reads the first group, in the query's order.
    /// </summary>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result, or the default if there are no groups.</returns>
    public async Task<TResult?> FirstAsync(IDbTransaction? tx = null, CancellationToken cancellationToken = default) =>
        (await ReadAsync(Math.Min(_groups.TakeCount ?? 1, 1), tx, cancellationToken).ConfigureAwait(false)).FirstOrDefault();

    /// <summary>
    /// Reads the one group the query returns.
    /// </summary>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result, or the default if there are no groups.</returns>
    /// <exception cref="InvalidOperationException">There is more than one group.</exception>
    public async Task<TResult?> SingleAsync(IDbTransaction? tx = null, CancellationToken cancellationToken = default) =>
        DmQuery<object>.Single(await ReadAsync(Math.Min(_groups.TakeCount ?? 2, 2), tx, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Writes the SQL that <see cref="ToListAsync"/> runs, without running it.
    /// </summary>
    /// <returns>The SQL and its parameters.</returns>
    public DmStatement ToSql()
    {
        var (sql, parameters, _) = Statement(_groups.TakeCount);
        return new DmStatement(sql, parameters.Named);
    }

    private (string Sql, DmlParameters Parameters, Func<object?[], TResult> Reads) Statement(int? take)
    {
        var (select, dialect, reads) = _groups.Build(_selector, take);
        var parameters = new DmlParameters();
        var sql = new SqlRenderer(dialect, parameters).For(select).Render(select);
        return (sql, parameters, (Func<object?[], TResult>)reads!.Compile());
    }

    private async Task<List<TResult>> ReadAsync(int? take, IDbTransaction? tx, CancellationToken cancellationToken)
    {
        if (take == 0)
        {
            return [];
        }

        var (sql, parameters, reads) = Statement(take);
        var command = DmScope.Command(sql, parameters, tx, cancellationToken);
        using var reader = await _groups.Scope.Connection.ExecuteReaderAsync(command).ConfigureAwait(false);
        var results = new List<TResult>();
        while (reader is DbDataReader db ? await db.ReadAsync(cancellationToken).ConfigureAwait(false) : reader.Read())
        {
            var values = new object?[reader.FieldCount];
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }

            results.Add(reads(values));
        }

        return results;
    }
}
