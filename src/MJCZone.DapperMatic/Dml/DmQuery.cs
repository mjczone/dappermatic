// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Globalization;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Dapper;
using MJCZone.DapperMatic.Dml.Internal;
using MJCZone.DapperMatic.Dml.Rest;

namespace MJCZone.DapperMatic.Dml;

/// <summary>
/// A query of a model's rows: one <c>SELECT</c>, built up by <c>Where</c>, <c>OrderBy</c>, <c>Skip</c>, <c>Take</c>,
/// <c>Distinct</c> and <c>Select</c>, and run by one of the <c>...Async</c> methods.
/// </summary>
/// <remarks>
/// A query is immutable: each call returns a new one, so a base query can be shared and refined. The calls compose
/// into one statement whatever their order: <c>Where</c> conditions are ANDed, and <c>Distinct</c> applies before
/// <c>Skip</c> and <c>Take</c>.
/// </remarks>
/// <typeparam name="T">The model type.</typeparam>
public sealed class DmQuery<T>
    where T : class
{
    private readonly DmScope _scope;
    private readonly IReadOnlyList<Expression<Func<T, bool>>> _where;
    private readonly IReadOnlyList<(LambdaExpression Key, bool Descending)> _order;
    private readonly int? _skip;
    private readonly int? _take;
    private readonly bool _distinct;
    private readonly IReadOnlyList<DmlInclude> _includes;
    private readonly string? _after;
    private readonly IReadOnlyList<Func<SqlSource, SqlNode>> _nodes;

    /// <summary>
    /// Initializes a new instance of the <see cref="DmQuery{T}"/> class: a query of every row.
    /// </summary>
    /// <param name="scope">The scope the query runs in.</param>
    internal DmQuery(DmScope scope)
        : this(scope, [], [], null, null, false, []) { }

    private DmQuery(
        DmScope scope,
        IReadOnlyList<Expression<Func<T, bool>>> where,
        IReadOnlyList<(LambdaExpression Key, bool Descending)> order,
        int? skip,
        int? take,
        bool distinct,
        IReadOnlyList<DmlInclude> includes,
        string? after = null,
        IReadOnlyList<Func<SqlSource, SqlNode>>? nodes = null
    )
    {
        _scope = scope;
        _where = where;
        _order = order;
        _skip = skip;
        _take = take;
        _distinct = distinct;
        _includes = includes;
        _after = after;
        _nodes = nodes ?? [];
    }

    /// <summary>
    /// Gets a value indicating whether the query returns distinct rows.
    /// </summary>
    internal bool IsDistinct => _distinct;

    /// <summary>
    /// Gets the most rows the query returns, or null.
    /// </summary>
    internal int? TakeCount => _take;

    /// <summary>
    /// Gets a value indicating whether the query loads relations with its rows.
    /// </summary>
    internal bool HasIncludes => _includes.Count > 0;

    /// <summary>
    /// Gets the scope the query runs in.
    /// </summary>
    internal DmScope Scope => _scope;

    private bool NeedsSubquery => _distinct || _skip is not null || _take is not null;

    /// <summary>
    /// Keeps the rows that match a condition, as well as any earlier ones.
    /// </summary>
    /// <param name="predicate">The condition.</param>
    /// <returns>The query.</returns>
    public DmQuery<T> Where(Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return new(_scope, [.. _where, predicate], _order, _skip, _take, _distinct, _includes, _after, _nodes);
    }

    /// <summary>
    /// Keeps the rows for which a row of another model matches a condition, as well as any earlier ones: a correlated
    /// <c>EXISTS</c>, for models with no relation property between them.
    /// </summary>
    /// <typeparam name="TOther">The other model.</typeparam>
    /// <param name="predicate">
    /// The condition, over this query's row and the other model's: <c>(o, n) =&gt; n.OrderId == o.Id &amp;&amp; n.Flagged</c>.
    /// </param>
    /// <returns>The query.</returns>
    public DmQuery<T> WhereExists<TOther>(Expression<Func<T, TOther, bool>> predicate)
        where TOther : class => Where(Exists(predicate, negated: false));

    /// <summary>
    /// Keeps the rows for which no row of another model matches a condition, as well as any earlier ones.
    /// </summary>
    /// <typeparam name="TOther">The other model.</typeparam>
    /// <param name="predicate">The condition, over this query's row and the other model's.</param>
    /// <returns>The query.</returns>
    public DmQuery<T> WhereNotExists<TOther>(Expression<Func<T, TOther, bool>> predicate)
        where TOther : class => Where(Exists(predicate, negated: true));

    /// <summary>
    /// Keeps the rows for which rows of two other models match a condition together, as well as any earlier ones.
    /// </summary>
    /// <typeparam name="TOther1">The first other model.</typeparam>
    /// <typeparam name="TOther2">The second other model.</typeparam>
    /// <param name="predicate">
    /// The condition, over this query's row and the other models': <c>(o, l, p) =&gt; l.OrderId == o.Id &amp;&amp;
    /// p.Id == l.ProductId &amp;&amp; p.Origin == "Spain"</c>.
    /// </param>
    /// <returns>The query.</returns>
    public DmQuery<T> WhereExists<TOther1, TOther2>(Expression<Func<T, TOther1, TOther2, bool>> predicate)
        where TOther1 : class
        where TOther2 : class => Where(Exists(predicate, negated: false));

    /// <summary>
    /// Keeps the rows for which no rows of two other models match a condition together, as well as any earlier ones.
    /// </summary>
    /// <typeparam name="TOther1">The first other model.</typeparam>
    /// <typeparam name="TOther2">The second other model.</typeparam>
    /// <param name="predicate">The condition, over this query's row and the other models'.</param>
    /// <returns>The query.</returns>
    public DmQuery<T> WhereNotExists<TOther1, TOther2>(Expression<Func<T, TOther1, TOther2, bool>> predicate)
        where TOther1 : class
        where TOther2 : class => Where(Exists(predicate, negated: true));

    /// <summary>
    /// Orders the rows by a key, ascending, in place of any earlier ordering. NULLs come first, as in LINQ.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="key">The key.</param>
    /// <returns>The query.</returns>
    public DmQuery<T> OrderBy<TKey>(Expression<Func<T, TKey>> key) => Ordered(key, descending: false, then: false);

    /// <summary>
    /// Orders the rows by a key, descending, in place of any earlier ordering. NULLs come last, as in LINQ.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="key">The key.</param>
    /// <returns>The query.</returns>
    public DmQuery<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> key) => Ordered(key, descending: true, then: false);

    /// <summary>
    /// Orders rows with the same earlier keys by another key, ascending.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="key">The key.</param>
    /// <returns>The query.</returns>
    /// <exception cref="InvalidOperationException">The query is not ordered yet.</exception>
    public DmQuery<T> ThenBy<TKey>(Expression<Func<T, TKey>> key) => Ordered(key, descending: false, then: true);

    /// <summary>
    /// Orders rows with the same earlier keys by another key, descending.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="key">The key.</param>
    /// <returns>The query.</returns>
    /// <exception cref="InvalidOperationException">The query is not ordered yet.</exception>
    public DmQuery<T> ThenByDescending<TKey>(Expression<Func<T, TKey>> key) => Ordered(key, descending: true, then: true);

    /// <summary>
    /// Skips rows.
    /// </summary>
    /// <param name="count">The number of rows to skip.</param>
    /// <returns>The query.</returns>
    public DmQuery<T> Skip(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return new(_scope, _where, _order, count, _take, _distinct, _includes, _after, _nodes);
    }

    /// <summary>
    /// Returns at most a number of rows.
    /// </summary>
    /// <param name="count">The most rows to return.</param>
    /// <returns>The query.</returns>
    public DmQuery<T> Take(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return new(_scope, _where, _order, _skip, count, _distinct, _includes, _after, _nodes);
    }

    /// <summary>
    /// Returns each distinct row once.
    /// </summary>
    /// <returns>The query.</returns>
    public DmQuery<T> Distinct() => new(_scope, _where, _order, _skip, _take, true, _includes, _after, _nodes);

    /// <summary>
    /// Reads deleted rows of models with a <c>[DmSoftDelete]</c> too: the query's, its relations' and its includes'.
    /// </summary>
    /// <returns>The query.</returns>
    public DmQuery<T> IncludeDeleted() => new(_scope.IncludeDeleted(), _where, _order, _skip, _take, _distinct, _includes, _after, _nodes);

    /// <summary>
    /// Reads only the query's deleted rows, with its relations' and includes' rows that are not deleted.
    /// </summary>
    /// <returns>The query.</returns>
    public DmQuery<T> OnlyDeleted() => new(_scope.OnlyDeleted(), _where, _order, _skip, _take, _distinct, _includes, _after, _nodes);

    /// <summary>
    /// Loads a relation that holds one row with each row: joined into the query, and null where there is no related
    /// row.
    /// </summary>
    /// <typeparam name="TRelated">The related model.</typeparam>
    /// <param name="relation">The relation property: <c>o =&gt; o.Customer</c>.</param>
    /// <param name="configure">What to load with the related rows, as <c>c =&gt; c.Include(x =&gt; x.Region)</c>; or null.</param>
    /// <returns>The query.</returns>
    public DmQuery<T> Include<TRelated>(
        Expression<Func<T, TRelated?>> relation,
        Func<DmInclude<TRelated>, DmInclude<TRelated>>? configure = null
    )
        where TRelated : class => With(DmlInclude.Of(typeof(T), relation, configure));

    /// <summary>
    /// Loads a relation that holds many rows with each row, by one query per batch of the rows' keys: never null once
    /// loaded, empty if there are none.
    /// </summary>
    /// <typeparam name="TRelated">The related model.</typeparam>
    /// <param name="relation">The relation property: <c>o =&gt; o.Lines</c>.</param>
    /// <param name="configure">
    /// Which related rows to load, in what order, and what with them, as
    /// <c>l =&gt; l.Where(x =&gt; x.Qty &gt; 0).OrderBy(x =&gt; x.Id).Take(5).Include(x =&gt; x.Product)</c>; or null for all.
    /// </param>
    /// <returns>The query.</returns>
    [OverloadResolutionPriority(1)] // A collection is also a TRelated: this overload is the one that sees its rows.
    public DmQuery<T> Include<TRelated>(
        Expression<Func<T, IEnumerable<TRelated>?>> relation,
        Func<DmInclude<TRelated>, DmInclude<TRelated>>? configure = null
    )
        where TRelated : class => With(DmlInclude.Of(typeof(T), relation, configure));

    /// <summary>
    /// Reads each row as something else, such as some of its columns: <c>o =&gt; new { o.Id, o.Total }</c>.
    /// </summary>
    /// <typeparam name="TResult">What each row is read as.</typeparam>
    /// <param name="selector">
    /// How. Only the columns it uses are read; the rest of it runs in .NET on each row, so it can call any method.
    /// </param>
    /// <returns>The query.</returns>
    public DmQuery<T, TResult> Select<TResult>(Expression<Func<T, TResult>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return new(this, selector, distinct: false);
    }

    /// <summary>
    /// Joins another model's rows that match a condition written out: <c>INNER JOIN</c>, for models with no relation
    /// property between them, or a join a relation does not describe.
    /// </summary>
    /// <typeparam name="T2">The model joined.</typeparam>
    /// <param name="on">The condition, of the two rows' own columns: <c>(o, n) =&gt; n.OrderId == o.Id</c>.</param>
    /// <returns>The joined query, with this query's conditions.</returns>
    /// <exception cref="InvalidOperationException">The query is ordered, paged, distinct or includes relations.</exception>
    public DmJoinQuery<T, T2> Join<T2>(Expression<Func<T, T2, bool>> on)
        where T2 : class => new(Joined(typeof(T2), "INNER JOIN", on));

    /// <summary>
    /// Joins another model's rows that match a condition written out, or none: <c>LEFT JOIN</c>, the row read as null
    /// where none matched.
    /// </summary>
    /// <typeparam name="T2">The model joined.</typeparam>
    /// <param name="on">The condition, of the two rows' own columns.</param>
    /// <returns>The joined query, with this query's conditions.</returns>
    /// <exception cref="InvalidOperationException">The query is ordered, paged, distinct or includes relations.</exception>
    public DmJoinQuery<T, T2> LeftJoin<T2>(Expression<Func<T, T2, bool>> on)
        where T2 : class => new(Joined(typeof(T2), "LEFT JOIN", on));

    /// <summary>
    /// Groups the rows by a key, for <c>Having</c>, ordering and paging of the groups, and a <c>Select</c> of each
    /// group's key and aggregates.
    /// </summary>
    /// <typeparam name="TKey">The key: a value, or several as <c>o =&gt; new { o.CustomerId, o.Status }</c>.</typeparam>
    /// <param name="key">The key, which can follow relations: <c>o =&gt; o.Customer!.Country</c>.</param>
    /// <returns>The groups.</returns>
    /// <exception cref="InvalidOperationException">The query is ordered, paged, distinct or includes relations.</exception>
    public DmGroupQuery<TKey, T> GroupBy<TKey>(Expression<Func<T, TKey>> key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return new(Rows(), key, null);
    }

    /// <summary>
    /// Groups the rows by a key, each row being something else to its group: <c>o =&gt; new { o.Total, o.Customer!.Discount }</c>.
    /// </summary>
    /// <typeparam name="TKey">The key's type.</typeparam>
    /// <typeparam name="TElement">The type of what each row is to its group, for the aggregates.</typeparam>
    /// <param name="key">The key, over the row.</param>
    /// <param name="element">What each row is to its group, over the row.</param>
    /// <returns>The groups.</returns>
    /// <exception cref="InvalidOperationException">The query is ordered, paged, distinct or includes relations.</exception>
    public DmGroupQuery<TKey, TElement> GroupBy<TKey, TElement>(Expression<Func<T, TKey>> key, Expression<Func<T, TElement>> element)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(element);
        return new(Rows(), key, element);
    }

    /// <summary>
    /// Continues from a page: <see cref="ToPageAsync"/> then reads the rows after the cursor, in the same order.
    /// </summary>
    /// <param name="cursor">A page's <see cref="DmPage{T}.NextCursor"/>, or one the web API gave; null for the first page.</param>
    /// <returns>The query.</returns>
    public DmQuery<T> After(string? cursor) => new(_scope, _where, _order, _skip, _take, _distinct, _includes, cursor, _nodes);

    /// <summary>
    /// Reads a page of rows in keyset order: the query's own ordering columns, then the model's key, so that pages
    /// never skip or repeat a row however deep they go; from the cursor <see cref="After"/> gives, or the start.
    /// </summary>
    /// <param name="size">The most rows the page holds.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rows, and the cursor of the next page: null when this one is not full.</returns>
    /// <exception cref="InvalidOperationException">
    /// The query is ordered by something other than the model's own columns, has <c>Skip</c>, <c>Take</c> or
    /// <c>Distinct</c>, or the model has no key.
    /// </exception>
    /// <exception cref="ArgumentException">The cursor is not one, or is of another ordering.</exception>
    public async Task<DmPage<T>> ToPageAsync(int size, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);
        if (_skip is not null || _take is not null || _distinct)
        {
            throw new InvalidOperationException("ToPageAsync reads a page by its size and cursor: leave out Skip, Take and Distinct.");
        }

        var (model, _, _) = Target();
        if (model.Keys.Count == 0)
        {
            throw new InvalidOperationException($"ToPageAsync pages by {model.Type.Name}'s key, and it has none.");
        }

        var keyset = new List<(DmlColumn Column, bool Descending)>();
        foreach (var (key, descending) in _order)
        {
            var body = key.Body is UnaryExpression { NodeType: ExpressionType.Convert } convert ? convert.Operand : key.Body;
            var column = body is MemberExpression { Expression: ParameterExpression } member && model.ColumnsByProperty.TryGetValue(member.Member.Name, out var found)
                ? found
                : throw new InvalidOperationException($"ToPageAsync pages by {model.Type.Name}'s own columns: order by them, as OrderBy(o => o.Id).");
            if (!keyset.Exists(k => k.Column == column))
            {
                keyset.Add((column, descending));
            }
        }

        foreach (var key in model.Keys.Where(k => !keyset.Exists(c => c.Column == k)))
        {
            keyset.Add((key, false));
        }

        var names = keyset.ConvertAll(k => (k.Column.Column.ColumnName, k.Descending));
        var parameter = Expression.Parameter(typeof(T), "row");
        List<(LambdaExpression Key, bool Descending)> order =
        [
            .. keyset.Select(k => ((LambdaExpression)Expression.Lambda(Expression.Property(parameter, k.Column.Property), parameter), k.Descending)),
        ];
        var nodes = _nodes;
        if (_after is { } cursor)
        {
            var values = RestCursor.Decode(cursor, names);
            nodes = [.. _nodes, source => KeysetAfter(source, keyset, values)];
        }

        var page = new DmQuery<T>(_scope, _where, order, null, size, false, _includes, null, nodes);
        var rows = await page.ToListAsync(tx, cancellationToken).ConfigureAwait(false);
        var next = rows.Count == size ? RestCursor.Encode(names, [.. keyset.Select(k => k.Column.Property.GetValue(rows[^1]))]) : null;
        return new DmPage<T>(rows, next);
    }

    /// <summary>
    /// Reads the rows.
    /// </summary>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rows.</returns>
    /// <exception cref="DmTranslationException">An expression has a part that cannot be written as SQL.</exception>
    public Task<List<T>> ToListAsync(IDbTransaction? tx = null, CancellationToken cancellationToken = default) =>
        ReadAsync(columns: null, _distinct, take: _take, keyOrder: false, tx, cancellationToken);

    /// <summary>
    /// Reads the first row: in the query's order, or in key order if it has none.
    /// </summary>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The row, or null if there is none.</returns>
    public async Task<T?> FirstAsync(IDbTransaction? tx = null, CancellationToken cancellationToken = default) =>
        (await ReadAsync(null, _distinct, Math.Min(_take ?? 1, 1), keyOrder: true, tx, cancellationToken).ConfigureAwait(false))
            .FirstOrDefault();

    /// <summary>
    /// Reads the one row the query returns.
    /// </summary>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The row, or null if there is none.</returns>
    /// <exception cref="InvalidOperationException">The query returns more than one row.</exception>
    public async Task<T?> SingleAsync(IDbTransaction? tx = null, CancellationToken cancellationToken = default) =>
        Single(await ReadAsync(null, _distinct, Math.Min(_take ?? 2, 2), keyOrder: false, tx, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Counts the rows the query returns.
    /// </summary>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows.</returns>
    public async Task<long> CountAsync(IDbTransaction? tx = null, CancellationToken cancellationToken = default)
    {
        if (_take == 0)
        {
            return 0;
        }

        var (_, dialect, source) = Target();
        var parameters = new DmlParameters();
        var count = new SqlSelectItem(new SqlAggregate("COUNT", null), null);
        var select = NeedsSubquery
            ? new SqlSelect { From = source, FromSubquery = Build(source, dialect, null, subquery: true, _distinct, _take, false), Items = [count] }
            : Filtered(source, dialect) with { Items = [count] };
        var sql = new SqlRenderer(dialect, parameters).For(select).Render(select);
        return await _scope
            .Connection.ExecuteScalarAsync<long>(DmScope.Command(sql, parameters, tx, cancellationToken))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Tells whether the query returns any rows.
    /// </summary>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if it does.</returns>
    public async Task<bool> ExistsAsync(IDbTransaction? tx = null, CancellationToken cancellationToken = default)
    {
        if (_take == 0)
        {
            return false;
        }

        var (_, dialect, source) = Target();
        var parameters = new DmlParameters();
        var inner = NeedsSubquery
            ? Build(source, dialect, null, subquery: true, _distinct, _take, false)
            : Filtered(source, dialect);
        var exists = new SqlExists(inner);
        var sql = $"SELECT CASE WHEN {new SqlRenderer(dialect, parameters).For(exists).Render(exists)} THEN 1 ELSE 0 END";
        return await _scope
                .Connection.ExecuteScalarAsync<long>(DmScope.Command(sql, parameters, tx, cancellationToken))
                .ConfigureAwait(false)
            != 0;
    }

    /// <summary>
    /// Adds up a value over the rows the query returns, skipping NULLs.
    /// </summary>
    /// <typeparam name="TValue">The value's type.</typeparam>
    /// <param name="selector">The value, such as <c>o =&gt; o.Total</c> or <c>o =&gt; o.Price * o.Qty</c>.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The sum: 0 if there are no rows, as in LINQ.</returns>
    public async Task<TValue> SumAsync<TValue>(
        Expression<Func<T, TValue>> selector,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => Converted<TValue>(await AggregateAsync("SUM", selector, tx, cancellationToken).ConfigureAwait(false) ?? 0);

    /// <summary>
    /// Finds the least value over the rows the query returns.
    /// </summary>
    /// <typeparam name="TValue">The value's type.</typeparam>
    /// <param name="selector">The value.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The least value; null if there are no rows and the value can be null.</returns>
    /// <exception cref="InvalidOperationException">There are no rows and the value cannot be null, as in LINQ.</exception>
    public async Task<TValue> MinAsync<TValue>(
        Expression<Func<T, TValue>> selector,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => OrNoElements<TValue>(await AggregateAsync("MIN", selector, tx, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Finds the greatest value over the rows the query returns.
    /// </summary>
    /// <typeparam name="TValue">The value's type.</typeparam>
    /// <param name="selector">The value.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The greatest value; null if there are no rows and the value can be null.</returns>
    /// <exception cref="InvalidOperationException">There are no rows and the value cannot be null, as in LINQ.</exception>
    public async Task<TValue> MaxAsync<TValue>(
        Expression<Func<T, TValue>> selector,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) => OrNoElements<TValue>(await AggregateAsync("MAX", selector, tx, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Averages a number over the rows the query returns, skipping NULLs, with a fraction even for integers.
    /// </summary>
    /// <typeparam name="TValue">The number's type.</typeparam>
    /// <param name="selector">The number.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The average, or null if there are no values.</returns>
    public async Task<double?> AverageAsync<TValue>(
        Expression<Func<T, TValue>> selector,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) =>
        await AggregateAsync("AVG", selector, tx, cancellationToken, withFraction: true).ConfigureAwait(false) is { } average
            ? Converted<double>(average)
            : null;

    /// <summary>
    /// Averages a decimal over the rows the query returns, as a decimal.
    /// </summary>
    /// <param name="selector">The number.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The average, or null if there are no rows.</returns>
    public async Task<decimal?> AverageAsync(
        Expression<Func<T, decimal>> selector,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) =>
        await AggregateAsync("AVG", selector, tx, cancellationToken).ConfigureAwait(false) is { } average
            ? Converted<decimal>(average)
            : null;

    /// <summary>
    /// Averages a decimal over the rows the query returns, as a decimal, skipping NULLs.
    /// </summary>
    /// <param name="selector">The number.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The average, or null if there are no values.</returns>
    public async Task<decimal?> AverageAsync(
        Expression<Func<T, decimal?>> selector,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    ) =>
        await AggregateAsync("AVG", selector, tx, cancellationToken).ConfigureAwait(false) is { } average
            ? Converted<decimal>(average)
            : null;

    /// <summary>
    /// Writes the SQL that <see cref="ToListAsync"/> runs, without running it.
    /// </summary>
    /// <returns>The SQL and its parameters.</returns>
    public DmStatement ToSql()
    {
        var (sql, parameters) = Statement(null, _distinct, _take, keyOrder: false);
        return new DmStatement(sql, parameters.Named);
    }

    /// <summary>
    /// Reads the rows into the model, or some of their columns.
    /// </summary>
    /// <param name="columns">The columns to read, or null for all.</param>
    /// <param name="distinct">Whether to read distinct rows.</param>
    /// <param name="take">The most rows to read, or null.</param>
    /// <param name="keyOrder">Whether to order by the key if the query has no order.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rows.</returns>
    internal async Task<List<T>> ReadAsync(
        IReadOnlyList<DmlColumn>? columns,
        bool distinct,
        int? take,
        bool keyOrder,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    )
    {
        if (take == 0)
        {
            return [];
        }

        var (sql, parameters, segments) = Planned(columns, distinct, take, keyOrder);
        if (segments is not null)
        {
            return await new DmlLoader(_scope, tx, cancellationToken).ReadAsync<T>(sql, parameters, segments).ConfigureAwait(false);
        }

        var rows = await _scope
            .Connection.QueryAsync<T>(DmScope.Command(sql, parameters, tx, cancellationToken))
            .ConfigureAwait(false);
        return rows.AsList();
    }

    /// <summary>
    /// Writes the <c>SELECT</c> that <see cref="ReadAsync"/> runs.
    /// </summary>
    /// <param name="columns">The columns to read, or null for all.</param>
    /// <param name="distinct">Whether to read distinct rows.</param>
    /// <param name="take">The most rows to read, or null.</param>
    /// <param name="keyOrder">Whether to order by the key if the query has no order.</param>
    /// <returns>The SQL and its parameters.</returns>
    internal (string Sql, DmlParameters Parameters) Statement(IReadOnlyList<DmlColumn>? columns, bool distinct, int? take, bool keyOrder)
    {
        var (sql, parameters, _) = Planned(columns, distinct, take, keyOrder);
        return (sql, parameters);
    }

    /// <summary>
    /// Builds the <c>SELECT</c> that <see cref="ReadAsync"/> runs, with the included relations of one row joined.
    /// </summary>
    /// <param name="columns">The columns to read, null for all, or none for a select list of the caller's own.</param>
    /// <param name="distinct">Whether to read distinct rows.</param>
    /// <param name="take">The most rows to read, or null.</param>
    /// <param name="keyOrder">Whether to order by the key if the query has no order.</param>
    /// <returns>The query, its scope and source, the dialect, and the segments of its rows.</returns>
    internal (SqlSelect Select, SqlScope Scope, SqlSource Source, DmlDialect Dialect, List<DmlSegment> Segments) Tree(
        IReadOnlyList<DmlColumn>? columns,
        bool distinct,
        int? take,
        bool keyOrder
    )
    {
        var (_, dialect, source) = Target();
        var select = Build(source, dialect, columns, subquery: false, distinct, take, keyOrder, out var scope);
        var (planned, segments) = DmlLoader.Plan(select, scope, _includes);
        return (planned, scope, source, dialect, segments);
    }

    /// <summary>
    /// Takes the one row of at most two, as LINQ's SingleOrDefault does.
    /// </summary>
    /// <typeparam name="TResult">The row type.</typeparam>
    /// <param name="rows">The rows.</param>
    /// <returns>The row, or the default.</returns>
    /// <exception cref="InvalidOperationException">There are two rows.</exception>
    internal static TResult? Single<TResult>(List<TResult> rows) =>
        rows.Count > 1
            ? throw new InvalidOperationException("The query returns more than one row.")
            : rows.FirstOrDefault();

    // The SELECT, joining the included relations of one row; and, with includes, the segments its rows are read in.
    private (string Sql, DmlParameters Parameters, List<DmlSegment>? Segments) Planned(
        IReadOnlyList<DmlColumn>? columns,
        bool distinct,
        int? take,
        bool keyOrder
    )
    {
        var (select, _, _, dialect, segments) = Tree(columns, distinct, take, keyOrder);
        var parameters = new DmlParameters();
        return (new SqlRenderer(dialect, parameters).For(select).Render(select), parameters, _includes.Count > 0 ? segments : null);
    }

    private static TValue Converted<TValue>(object value) => (TValue)DmlValues.Convert(value, typeof(TValue))!;

    // (o, x) => condition becomes o => DmlExists.Any<X>(x => condition), which the translator writes as EXISTS.
    private static Expression<Func<T, bool>> Exists(LambdaExpression predicate, bool negated)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var others = predicate.Parameters.Skip(1).ToArray();
        var marker = typeof(DmlExists)
            .GetMethods()
            .Single(m => m.Name == nameof(DmlExists.Any) && m.GetGenericArguments().Length == others.Length)
            .MakeGenericMethod([.. others.Select(o => o.Type)]);
        Expression exists = Expression.Call(marker, Expression.Quote(Expression.Lambda(predicate.Body, others)));
        return Expression.Lambda<Func<T, bool>>(negated ? Expression.Not(exists) : exists, predicate.Parameters[0]);
    }

    private static TValue OrNoElements<TValue>(object? value) =>
        value is not null ? Converted<TValue>(value)
        : default(TValue) is null ? default!
        : throw new InvalidOperationException("The query returns no rows, and the value cannot be null.");

    // A join of this query's rows, with its conditions: they follow the join's, as they would be written in SQL.
    private DmlJoin Joined(Type type, string kind, LambdaExpression on)
    {
        if (_order.Count > 0 || _skip is not null || _take is not null || _distinct || _includes.Count > 0)
        {
            throw new InvalidOperationException(
                "Join follows From and Where: order, page and make distinct the joined rows after it; Include does not "
                    + "apply to them."
            );
        }

        return new DmlJoin { Scope = _scope, Models = [typeof(T)], Joins = [], Where = [.. _where] }.With(type, kind, on);
    }

    // The rows to group: the query's conditions, which the groups' own clauses follow.
    private DmlRows Rows()
    {
        if (_order.Count > 0 || _skip is not null || _take is not null || _distinct || _includes.Count > 0)
        {
            throw new InvalidOperationException(
                "GroupBy follows Where: order and page the groups after it; Distinct and Include do not apply to groups."
            );
        }

        return new DmlRows
        {
            Scope = _scope,
            Open = () =>
            {
                var (_, dialect, source) = Target();
                var scope = ScopeOf(dialect, source);
                var where = Conditions(source, scope);
                return (new SqlSelect { From = source, Joins = scope.Joins, Where = where }, scope, [source]);
            },
        };
    }

    private DmQuery<T> With(DmlInclude include) =>
        new(_scope, _where, _order, _skip, _take, _distinct, DmlInclude.Add(_includes, include), _after, _nodes);

    private DmQuery<T> Ordered(LambdaExpression key, bool descending, bool then)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (then && _order.Count == 0)
        {
            throw new InvalidOperationException("ThenBy and ThenByDescending follow OrderBy or OrderByDescending.");
        }

        return new(_scope, _where, then ? [.. _order, (key, descending)] : [(key, descending)], _skip, _take, _distinct, _includes, _after, _nodes);
    }

    private async Task<object?> AggregateAsync(
        string function,
        LambdaExpression selector,
        IDbTransaction? tx,
        CancellationToken cancellationToken,
        bool withFraction = false
    )
    {
        ArgumentNullException.ThrowIfNull(selector);
        if (_take == 0)
        {
            return null;
        }

        var (_, dialect, source) = Target();
        var parameters = new DmlParameters();

        // The rows first, then the value over them: column names in the subquery are not aliased, so the value's
        // columns name them as they do the table's, and the rows it relates to are joined to the subquery's.
        SqlSelect select;
        if (NeedsSubquery)
        {
            var scope = ScopeOf(dialect, source);
            var value = DmlTranslator.Value(selector, source, scope).Node;
            select = new SqlSelect
            {
                From = source,
                FromSubquery = Build(source, dialect, null, subquery: true, _distinct, _take, false),
                Items = [new SqlSelectItem(new SqlAggregate(function, value, withFraction), null)],
                Joins = scope.Joins,
            };
        }
        else
        {
            var filtered = Filtered(source, dialect, out var scope);
            var value = DmlTranslator.Value(selector, source, scope).Node;
            select = filtered with { Items = [new SqlSelectItem(new SqlAggregate(function, value, withFraction), null)] };
        }

        var sql = new SqlRenderer(dialect, parameters).For(select).Render(select);

        var result = await _scope
            .Connection.ExecuteScalarAsync<object>(DmScope.Command(sql, parameters, tx, cancellationToken))
            .ConfigureAwait(false);
        return result is DBNull ? null : result;
    }

    // The rows after a cursor's, in a keyset order: NULLs first ascending and last descending, as the query orders them.
    private static SqlNode KeysetAfter(SqlSource source, IReadOnlyList<(DmlColumn Column, bool Descending)> keyset, IReadOnlyList<string?> values)
    {
        SqlNode? any = null;
        for (var i = keyset.Count - 1; i >= 0; i--)
        {
            var (column, descending) = keyset[i];
            var sql = new SqlColumn(source, column.Column.ColumnName);
            var type = column.Property.PropertyType;
            var value = values[i] is { } text ? RestTranslator.Value(text, new RestColumn(column.Column.ColumnName, type, column.Column.IsNullable)) : null;
            SqlNode after = value is null
                ? (descending ? SqlText.Always(false) : new SqlIsNull(sql, Negated: true))
                : descending
                    ? new SqlLogical("OR", [new SqlCompare(sql, "<", new SqlValue(type, value)), new SqlIsNull(sql, Negated: false)])
                    : new SqlCompare(sql, ">", new SqlValue(type, value));
            if (any is not null)
            {
                SqlNode same = value is null ? new SqlIsNull(sql, Negated: false) : new SqlCompare(sql, "=", new SqlValue(type, value));
                after = new SqlLogical("OR", [after, new SqlLogical("AND", [same, any])]);
            }

            any = after;
        }

        return any!;
    }

    private (DmlModel Model, DmlDialect Dialect, SqlSource Source) Target()
    {
        var (model, dialect, table) = _scope.Target<T>();
        return (model, dialect, _scope.SourceOf(model, table));
    }

    private SqlScope ScopeOf(DmlDialect dialect, SqlSource source) => SqlScope.Select(dialect, _scope.TableOf, source, _scope.FiltersFor, _scope.HiddenFor);

    private List<SqlNode> Conditions(SqlSource source, SqlScope scope) =>
        [.. scope.Filters(source), .. _where.Select(w => DmlTranslator.Condition(w, source, scope)), .. _nodes.Select(n => n(source))];

    private SqlSelect Filtered(SqlSource source, DmlDialect dialect) => Filtered(source, dialect, out _);

    // The rows that match the conditions, unordered, with the related rows the conditions read joined.
    private SqlSelect Filtered(SqlSource source, DmlDialect dialect, out SqlScope scope)
    {
        scope = ScopeOf(dialect, source);
        return new SqlSelect { From = source, Where = Conditions(source, scope), Joins = scope.Joins };
    }

    // A subquery's columns are not aliased, so the outer query names them as the table does; and it is ordered only
    // when paged, since SQL Server refuses an ORDER BY in a subquery otherwise.
    private SqlSelect Build(
        SqlSource source,
        DmlDialect dialect,
        IReadOnlyList<DmlColumn>? columns,
        bool subquery,
        bool distinct,
        int? take,
        bool keyOrder
    ) => Build(source, dialect, columns, subquery, distinct, take, keyOrder, out _);

    private SqlSelect Build(
        SqlSource source,
        DmlDialect dialect,
        IReadOnlyList<DmlColumn>? columns,
        bool subquery,
        bool distinct,
        int? take,
        bool keyOrder,
        out SqlScope scope
    )
    {
        var model = source.Model!;
        if (distinct)
        {
            RequireOrderInSelectList(source, dialect, columns ?? model.Columns);
        }

        var paged = _skip is not null || take is not null;
        var built = ScopeOf(dialect, source);
        scope = built;
        var where = Conditions(source, built);
        List<SqlOrder> orderBy = [];
        string? orderBySql = null;
        if (!subquery || paged)
        {
            if (_order.Count > 0)
            {
                orderBy =
                [
                    .. _order.Select(o =>
                    {
                        var (node, nullable, _) = DmlTranslator.Value(o.Key, source, built);
                        return new SqlOrder(node, o.Descending, nullable);
                    }),
                ];
            }
            else if (keyOrder && model.Keys.Count > 0)
            {
                orderBy = [.. model.Keys.Select(k => new SqlOrder(new SqlColumn(source, k.Column.ColumnName), Descending: false, Nullable: false))];
            }
            else if (distinct && paged && dialect.ProviderType == DbProviderType.SqlServer)
            {
                // Paging needs an ORDER BY on SQL Server, and with DISTINCT it must be in the select list.
                orderBySql = "1";
            }
        }

        return new SqlSelect
        {
            From = source,
            Joins = built.Joins,
            Items = DmScope.Items(source, columns, aliased: !subquery),
            Distinct = distinct,
            Where = where,
            OrderBy = orderBy,
            OrderBySql = orderBySql,
            Skip = _skip,
            Take = take,
        };
    }

    // PostgreSQL and SQL Server refuse an ORDER BY of anything but the selected columns with DISTINCT; MySQL and
    // SQLite allow it. Refused here everywhere, so a query does not run on one provider and fail on another.
    private void RequireOrderInSelectList(SqlSource source, DmlDialect dialect, IReadOnlyList<DmlColumn> columns)
    {
        foreach (var (key, _) in _order)
        {
            var (_, _, column) = DmlTranslator.Value(key, source, ScopeOf(dialect, source));
            if (column is null || !columns.Contains(column))
            {
                throw new InvalidOperationException(
                    $"A distinct query is ordered only by the columns it reads, and {key} is not one of them."
                );
            }
        }
    }
}

/// <summary>
/// A query whose rows are read as something else, such as some of their columns.
/// </summary>
/// <remarks>
/// Only the columns the selector uses are read, and the selector runs in .NET on each row. A selector that uses the
/// row itself, or a property that is not a column, reads every column.
/// </remarks>
/// <typeparam name="T">The model type.</typeparam>
/// <typeparam name="TResult">What each row is read as.</typeparam>
public sealed class DmQuery<T, TResult>
    where T : class
{
    private readonly DmQuery<T> _query;
    private readonly Expression<Func<T, TResult>> _selector;
    private readonly bool _distinct;

    // The selector reading the parts the database computes from an array, as (row, values) => ..., and the parts.
    private readonly LambdaExpression _reads;
    private readonly List<Expression> _parts;

    /// <summary>
    /// Initializes a new instance of the <see cref="DmQuery{T, TResult}"/> class.
    /// </summary>
    /// <param name="query">The query of the rows.</param>
    /// <param name="selector">What each row is read as.</param>
    /// <param name="distinct">Whether the results are distinct.</param>
    internal DmQuery(DmQuery<T> query, Expression<Func<T, TResult>> selector, bool distinct)
    {
        _query = query;
        _selector = selector;
        _distinct = distinct;
        (_reads, _parts) = DmlProjection.DatabaseParts(selector, DmlModel.For(typeof(T)));
    }

    /// <summary>
    /// Returns each distinct result once. The selector must be of columns only, such as
    /// <c>o =&gt; new { o.CustomerId, o.Status }</c>, since the database compares them.
    /// </summary>
    /// <returns>The query.</returns>
    public DmQuery<T, TResult> Distinct() => new(_query, _selector, distinct: true);

    /// <summary>
    /// Reads the results.
    /// </summary>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The results.</returns>
    public Task<List<TResult>> ToListAsync(IDbTransaction? tx = null, CancellationToken cancellationToken = default) =>
        ReadAsync(_query.TakeCount, keyOrder: false, tx, cancellationToken);

    /// <summary>
    /// Reads the first result: in the query's order, or in key order if it has none.
    /// </summary>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result, or the default (null for a class) if there is none.</returns>
    public async Task<TResult?> FirstAsync(IDbTransaction? tx = null, CancellationToken cancellationToken = default) =>
        (await ReadAsync(Math.Min(_query.TakeCount ?? 1, 1), keyOrder: true, tx, cancellationToken).ConfigureAwait(false))
            .FirstOrDefault();

    /// <summary>
    /// Reads the one result the query returns.
    /// </summary>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result, or the default (null for a class) if there is none.</returns>
    /// <exception cref="InvalidOperationException">The query returns more than one row.</exception>
    public async Task<TResult?> SingleAsync(IDbTransaction? tx = null, CancellationToken cancellationToken = default) =>
        DmQuery<T>.Single(
            await ReadAsync(Math.Min(_query.TakeCount ?? 2, 2), keyOrder: false, tx, cancellationToken).ConfigureAwait(false)
        );

    /// <summary>
    /// Writes the SQL that <see cref="ToListAsync"/> runs, without running it.
    /// </summary>
    /// <returns>The SQL and its parameters.</returns>
    public DmStatement ToSql()
    {
        var (columns, distinct) = Plan();
        if (_parts.Count == 0)
        {
            var (sql, parameters) = _query.Statement(columns, distinct, _query.TakeCount, keyOrder: false);
            return new DmStatement(sql, parameters.Named);
        }

        var (partsSql, partsParameters, _, _) = WithParts(columns, distinct, _query.TakeCount, keyOrder: false);
        return new DmStatement(partsSql, partsParameters.Named);
    }

    private async Task<List<TResult>> ReadAsync(int? take, bool keyOrder, IDbTransaction? tx, CancellationToken cancellationToken)
    {
        var (columns, distinct) = Plan();
        if (_parts.Count == 0)
        {
            var rows = await _query.ReadAsync(columns, distinct, take, keyOrder, tx, cancellationToken).ConfigureAwait(false);
            var project = _selector.Compile();
            return rows.ConvertAll(row => project(row));
        }

        var (sql, parameters, segments, partsStart) = WithParts(columns, distinct, take, keyOrder);
        var read = await new DmlLoader(_query.Scope, tx, cancellationToken)
            .ReadWithValuesAsync(sql, parameters, segments, partsStart, _parts.Count)
            .ConfigureAwait(false);
        var reads = (Func<T, object?[], TResult>)_reads.Compile();
        return read.ConvertAll(r => reads((T)r.Row!, r.Values));
    }

    // The SELECT with the parts the database computes after the rows' columns, each one item.
    private (string Sql, DmlParameters Parameters, List<DmlSegment> Segments, int PartsStart) WithParts(
        IReadOnlyList<DmlColumn>? columns,
        bool distinct,
        int? take,
        bool keyOrder
    )
    {
        var (select, scope, source, dialect, segments) = _query.Tree(columns, distinct, take, keyOrder);
        var row = _reads.Parameters[0];
        List<SqlSelectItem> parts =
        [
            .. _parts.Select((part, i) =>
            {
                var lambda = Expression.Lambda(part, row);
                SqlNode node = DmlProjection.IsTest(part)
                    ? new SqlCase(DmlTranslator.Condition(lambda, source, scope), new SqlText("1"), new SqlText("0"))
                    : DmlTranslator.Value(lambda, source, scope).Node;
                return new SqlSelectItem(node, $"dm_p{i}");
            }),
        ];

        var start = select.Items.Count;
        select = select with { Items = [.. select.Items, .. parts] };
        var parameters = new DmlParameters();
        return (new SqlRenderer(dialect, parameters).For(select).Render(select), parameters, segments, start);
    }

    // The columns to read, null for all, and whether the database makes them distinct.
    private (IReadOnlyList<DmlColumn>? Columns, bool Distinct) Plan()
    {
        // Relations are loaded onto whole rows, which the selector then reads in .NET.
        if (_query.HasIncludes)
        {
            return _distinct
                ? throw new InvalidOperationException(
                    "Distinct after Select compares the selected columns in the database, and Include loads relations onto "
                        + "whole rows: drop one of them."
                )
                : (null, _query.IsDistinct);
        }

        var model = DmlModel.For(typeof(T));
        var used = DmlProjection.Columns(_reads, model);

        // Rows are read into the model, so a partial read needs a constructor that does not take every column.
        var partial = used is not null && typeof(T).GetConstructor(Type.EmptyTypes) is not null;

        if (_distinct)
        {
            if (used is null || !DmlProjection.IsColumnsOnly(_reads))
            {
                throw new InvalidOperationException(
                    "Distinct after Select needs a selector of columns only, such as o => new { o.CustomerId, o.Status }: "
                        + $"the database compares the columns, and {_selector} computes more than them."
                );
            }

            if (!partial && used.Count != model.Columns.Count)
            {
                throw new NotSupportedException(
                    $"Distinct after Select reads only the selected columns, and {typeof(T).Name} has no parameterless "
                        + "constructor to read them into."
                );
            }

            return (used, true);
        }

        // Distinct rows, then each projected, as LINQ does: every column is read.
        if (_query.IsDistinct)
        {
            return (null, true);
        }

        // A selector of database parts alone reads no column; one of no column reads the first, for a select list.
        return partial ? (used!.Count == 0 && _parts.Count == 0 ? [model.Columns[0]] : used, false) : (null, false);
    }
}
