// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

#pragma warning disable SA1402 // The join queries of 2, 3 and 4 models belong together.
#pragma warning disable SA1649

using System.Data;
using System.Linq.Expressions;
using MJCZone.DapperMatic.Dml.Internal;

namespace MJCZone.DapperMatic.Dml;

/// <summary>
/// Rows of 2 models joined by conditions written out: one <c>SELECT</c>, built up by <c>Where</c>, <c>OrderBy</c>,
/// <c>Skip</c>, <c>Take</c> and <c>Distinct</c> over all the joined rows, read as tuples or by <c>Select</c>.
/// </summary>
/// <remarks>A model joined by <c>LeftJoin</c> reads as null where no row matched.</remarks>
/// <typeparam name="T1">The first model.</typeparam>
/// <typeparam name="T2">The second model.</typeparam>
public sealed class DmJoinQuery<T1, T2>
    where T1 : class
    where T2 : class
{
    private readonly DmlJoin _join;

    /// <summary>
    /// Initializes a new instance of the <see cref="DmJoinQuery{T1, T2}"/> class.
    /// </summary>
    /// <param name="join">The join.</param>
    internal DmJoinQuery(DmlJoin join) => _join = join;

    /// <summary>
    /// Joins another model's rows that match a condition: <c>INNER JOIN</c>.
    /// </summary>
    /// <typeparam name="T3">The model joined.</typeparam>
    /// <param name="on">The condition, over the rows joined so far and the new one, of their own columns.</param>
    /// <returns>The query.</returns>
    public DmJoinQuery<T1, T2, T3> Join<T3>(Expression<Func<T1, T2, T3, bool>> on)
        where T3 : class => new(_join.With(typeof(T3), "INNER JOIN", on));

    /// <summary>
    /// Joins another model's rows that match a condition, or none: <c>LEFT JOIN</c>, the row read as null where none
    /// matched.
    /// </summary>
    /// <typeparam name="T3">The model joined.</typeparam>
    /// <param name="on">The condition, over the rows joined so far and the new one, of their own columns.</param>
    /// <returns>The query.</returns>
    public DmJoinQuery<T1, T2, T3> LeftJoin<T3>(Expression<Func<T1, T2, T3, bool>> on)
        where T3 : class => new(_join.With(typeof(T3), "LEFT JOIN", on));

    /// <summary>
    /// Keeps the joined rows that match a condition, as well as any earlier ones.
    /// </summary>
    /// <param name="predicate">The condition, over all the joined rows, which can follow their relations.</param>
    /// <returns>The query.</returns>
    public DmJoinQuery<T1, T2> Where(Expression<Func<T1, T2, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return new(new DmlJoin { Scope = _join.Scope, Models = _join.Models, Joins = _join.Joins, Where = [.. _join.Where, predicate], Order = _join.Order, Skip = _join.Skip, Take = _join.Take, Distinct = _join.Distinct });
    }

    /// <summary>
    /// Orders the rows by a key, ascending, in place of any earlier ordering. NULLs come first, as in LINQ.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="key">The key.</param>
    /// <returns>The query.</returns>
    public DmJoinQuery<T1, T2> OrderBy<TKey>(Expression<Func<T1, T2, TKey>> key) => Ordered(key, descending: false, then: false);

    /// <summary>
    /// Orders the rows by a key, descending, in place of any earlier ordering. NULLs come last, as in LINQ.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="key">The key.</param>
    /// <returns>The query.</returns>
    public DmJoinQuery<T1, T2> OrderByDescending<TKey>(Expression<Func<T1, T2, TKey>> key) => Ordered(key, descending: true, then: false);

    /// <summary>
    /// Orders rows with the same earlier keys by another key, ascending.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="key">The key.</param>
    /// <returns>The query.</returns>
    public DmJoinQuery<T1, T2> ThenBy<TKey>(Expression<Func<T1, T2, TKey>> key) => Ordered(key, descending: false, then: true);

    /// <summary>
    /// Orders rows with the same earlier keys by another key, descending.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="key">The key.</param>
    /// <returns>The query.</returns>
    public DmJoinQuery<T1, T2> ThenByDescending<TKey>(Expression<Func<T1, T2, TKey>> key) => Ordered(key, descending: true, then: true);

    /// <summary>
    /// Skips rows.
    /// </summary>
    /// <param name="count">The number of rows to skip.</param>
    /// <returns>The query.</returns>
    public DmJoinQuery<T1, T2> Skip(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return new(new DmlJoin { Scope = _join.Scope, Models = _join.Models, Joins = _join.Joins, Where = _join.Where, Order = _join.Order, Skip = count, Take = _join.Take, Distinct = _join.Distinct });
    }

    /// <summary>
    /// Returns at most a number of rows.
    /// </summary>
    /// <param name="count">The most rows to return.</param>
    /// <returns>The query.</returns>
    public DmJoinQuery<T1, T2> Take(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return new(new DmlJoin { Scope = _join.Scope, Models = _join.Models, Joins = _join.Joins, Where = _join.Where, Order = _join.Order, Skip = _join.Skip, Take = count, Distinct = _join.Distinct });
    }

    /// <summary>
    /// Returns each distinct combination of rows once.
    /// </summary>
    /// <returns>The query.</returns>
    public DmJoinQuery<T1, T2> Distinct() =>
        new(new DmlJoin { Scope = _join.Scope, Models = _join.Models, Joins = _join.Joins, Where = _join.Where, Order = _join.Order, Skip = _join.Skip, Take = _join.Take, Distinct = true });

    /// <summary>
    /// Reads the joined rows as something else. The rows' columns, their related rows' columns and values over related
    /// rows are read by the database; the rest of the selector runs in .NET.
    /// </summary>
    /// <typeparam name="TResult">What the joined rows are read as.</typeparam>
    /// <param name="selector">How: <c>(o, c) =&gt; new { o.Id, Customer = c.Name }</c>.</param>
    /// <returns>The query.</returns>
    public DmJoinSelect<TResult> Select<TResult>(Expression<Func<T1, T2, TResult>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return new(_join, selector, distinct: false);
    }

    /// <summary>
    /// Groups the joined rows by a key, each being something to its group, for <c>Having</c>, ordering and paging of
    /// the groups, and a <c>Select</c> of each group's key and aggregates.
    /// </summary>
    /// <typeparam name="TKey">The key's type.</typeparam>
    /// <typeparam name="TElement">The type of what the joined rows are to their group.</typeparam>
    /// <param name="key">The key, over the joined rows.</param>
    /// <param name="element">What the joined rows are to their group, as <c>(o, c) =&gt; new { o.Total, c.Discount }</c>.</param>
    /// <returns>The groups.</returns>
    public DmGroupQuery<TKey, TElement> GroupBy<TKey, TElement>(Expression<Func<T1, T2, TKey>> key, Expression<Func<T1, T2, TElement>> element)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(element);
        return new(_join.Grouped(), key, element);
    }

    /// <summary>
    /// Reads the joined rows.
    /// </summary>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rows, a tuple per combination.</returns>
    public async Task<List<(T1, T2?)>> ToListAsync(IDbTransaction? tx = null, CancellationToken cancellationToken = default) =>
        (await ReadAsync(_join.Take, keyOrder: false, tx, cancellationToken).ConfigureAwait(false)).ConvertAll(r => ((T1)r[0]!, (T2?)r[1]));

    /// <summary>
    /// Reads the first joined rows: in the query's order, or the first model's key order if it has none.
    /// </summary>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rows, or null if there are none.</returns>
    public async Task<(T1, T2?)?> FirstAsync(IDbTransaction? tx = null, CancellationToken cancellationToken = default)
    {
        var rows = await ReadAsync(Math.Min(_join.Take ?? 1, 1), keyOrder: true, tx, cancellationToken).ConfigureAwait(false);
        return rows.Count == 0 ? null : rows.ConvertAll(r => ((T1)r[0]!, (T2?)r[1]))[0];
    }

    /// <summary>
    /// Counts the joined rows.
    /// </summary>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows.</returns>
    public Task<long> CountAsync(IDbTransaction? tx = null, CancellationToken cancellationToken = default) =>
        _join.CountAsync(exists: false, tx, cancellationToken);

    /// <summary>
    /// Tells whether there are any joined rows.
    /// </summary>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if there are.</returns>
    public async Task<bool> ExistsAsync(IDbTransaction? tx = null, CancellationToken cancellationToken = default) =>
        await _join.CountAsync(exists: true, tx, cancellationToken).ConfigureAwait(false) != 0;

    /// <summary>
    /// Writes the SQL that <see cref="ToListAsync"/> runs, without running it.
    /// </summary>
    /// <returns>The SQL and its parameters.</returns>
    public DmStatement ToSql()
    {
        var (select, scope, _, _) = _join.Rows([.. _join.Models.Select(_ => true)], _join.Take, keyOrder: false);
        var parameters = new DmlParameters();
        return new DmStatement(new SqlRenderer(scope.Dialect, parameters).For(select).Render(select), parameters.Named);
    }

    private async Task<List<object?[]>> ReadAsync(int? take, bool keyOrder, IDbTransaction? tx, CancellationToken cancellationToken)
    {
        if (take == 0)
        {
            return [];
        }

        var (select, scope, _, segments) = _join.Rows([.. _join.Models.Select(_ => true)], take, keyOrder);
        return await _join.ReadAsync(select, scope.Dialect, segments, 0, tx, cancellationToken).ConfigureAwait(false);
    }

    private DmJoinQuery<T1, T2> Ordered(LambdaExpression key, bool descending, bool then)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (then && _join.Order.Count == 0)
        {
            throw new InvalidOperationException("ThenBy and ThenByDescending follow OrderBy or OrderByDescending.");
        }

        return new(new DmlJoin { Scope = _join.Scope, Models = _join.Models, Joins = _join.Joins, Where = _join.Where, Order = then ? [.. _join.Order, (key, descending)] : [(key, descending)], Skip = _join.Skip, Take = _join.Take, Distinct = _join.Distinct });
    }
}

/// <summary>
/// Rows of 3 models joined by conditions written out: one <c>SELECT</c>, built up by <c>Where</c>, <c>OrderBy</c>,
/// <c>Skip</c>, <c>Take</c> and <c>Distinct</c> over all the joined rows, read as tuples or by <c>Select</c>.
/// </summary>
/// <remarks>A model joined by <c>LeftJoin</c> reads as null where no row matched.</remarks>
/// <typeparam name="T1">The first model.</typeparam>
/// <typeparam name="T2">The second model.</typeparam>
/// <typeparam name="T3">The third model.</typeparam>
public sealed class DmJoinQuery<T1, T2, T3>
    where T1 : class
    where T2 : class
    where T3 : class
{
    private readonly DmlJoin _join;

    /// <summary>
    /// Initializes a new instance of the <see cref="DmJoinQuery{T1, T2, T3}"/> class.
    /// </summary>
    /// <param name="join">The join.</param>
    internal DmJoinQuery(DmlJoin join) => _join = join;

    /// <summary>
    /// Joins another model's rows that match a condition: <c>INNER JOIN</c>.
    /// </summary>
    /// <typeparam name="T4">The model joined.</typeparam>
    /// <param name="on">The condition, over the rows joined so far and the new one, of their own columns.</param>
    /// <returns>The query.</returns>
    public DmJoinQuery<T1, T2, T3, T4> Join<T4>(Expression<Func<T1, T2, T3, T4, bool>> on)
        where T4 : class => new(_join.With(typeof(T4), "INNER JOIN", on));

    /// <summary>
    /// Joins another model's rows that match a condition, or none: <c>LEFT JOIN</c>, the row read as null where none
    /// matched.
    /// </summary>
    /// <typeparam name="T4">The model joined.</typeparam>
    /// <param name="on">The condition, over the rows joined so far and the new one, of their own columns.</param>
    /// <returns>The query.</returns>
    public DmJoinQuery<T1, T2, T3, T4> LeftJoin<T4>(Expression<Func<T1, T2, T3, T4, bool>> on)
        where T4 : class => new(_join.With(typeof(T4), "LEFT JOIN", on));

    /// <summary>
    /// Keeps the joined rows that match a condition, as well as any earlier ones.
    /// </summary>
    /// <param name="predicate">The condition, over all the joined rows, which can follow their relations.</param>
    /// <returns>The query.</returns>
    public DmJoinQuery<T1, T2, T3> Where(Expression<Func<T1, T2, T3, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return new(new DmlJoin { Scope = _join.Scope, Models = _join.Models, Joins = _join.Joins, Where = [.. _join.Where, predicate], Order = _join.Order, Skip = _join.Skip, Take = _join.Take, Distinct = _join.Distinct });
    }

    /// <summary>
    /// Orders the rows by a key, ascending, in place of any earlier ordering. NULLs come first, as in LINQ.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="key">The key.</param>
    /// <returns>The query.</returns>
    public DmJoinQuery<T1, T2, T3> OrderBy<TKey>(Expression<Func<T1, T2, T3, TKey>> key) => Ordered(key, descending: false, then: false);

    /// <summary>
    /// Orders the rows by a key, descending, in place of any earlier ordering. NULLs come last, as in LINQ.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="key">The key.</param>
    /// <returns>The query.</returns>
    public DmJoinQuery<T1, T2, T3> OrderByDescending<TKey>(Expression<Func<T1, T2, T3, TKey>> key) => Ordered(key, descending: true, then: false);

    /// <summary>
    /// Orders rows with the same earlier keys by another key, ascending.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="key">The key.</param>
    /// <returns>The query.</returns>
    public DmJoinQuery<T1, T2, T3> ThenBy<TKey>(Expression<Func<T1, T2, T3, TKey>> key) => Ordered(key, descending: false, then: true);

    /// <summary>
    /// Orders rows with the same earlier keys by another key, descending.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="key">The key.</param>
    /// <returns>The query.</returns>
    public DmJoinQuery<T1, T2, T3> ThenByDescending<TKey>(Expression<Func<T1, T2, T3, TKey>> key) => Ordered(key, descending: true, then: true);

    /// <summary>
    /// Skips rows.
    /// </summary>
    /// <param name="count">The number of rows to skip.</param>
    /// <returns>The query.</returns>
    public DmJoinQuery<T1, T2, T3> Skip(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return new(new DmlJoin { Scope = _join.Scope, Models = _join.Models, Joins = _join.Joins, Where = _join.Where, Order = _join.Order, Skip = count, Take = _join.Take, Distinct = _join.Distinct });
    }

    /// <summary>
    /// Returns at most a number of rows.
    /// </summary>
    /// <param name="count">The most rows to return.</param>
    /// <returns>The query.</returns>
    public DmJoinQuery<T1, T2, T3> Take(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return new(new DmlJoin { Scope = _join.Scope, Models = _join.Models, Joins = _join.Joins, Where = _join.Where, Order = _join.Order, Skip = _join.Skip, Take = count, Distinct = _join.Distinct });
    }

    /// <summary>
    /// Returns each distinct combination of rows once.
    /// </summary>
    /// <returns>The query.</returns>
    public DmJoinQuery<T1, T2, T3> Distinct() =>
        new(new DmlJoin { Scope = _join.Scope, Models = _join.Models, Joins = _join.Joins, Where = _join.Where, Order = _join.Order, Skip = _join.Skip, Take = _join.Take, Distinct = true });

    /// <summary>
    /// Reads the joined rows as something else. The rows' columns, their related rows' columns and values over related
    /// rows are read by the database; the rest of the selector runs in .NET.
    /// </summary>
    /// <typeparam name="TResult">What the joined rows are read as.</typeparam>
    /// <param name="selector">How: <c>(o, c) =&gt; new { o.Id, Customer = c.Name }</c>.</param>
    /// <returns>The query.</returns>
    public DmJoinSelect<TResult> Select<TResult>(Expression<Func<T1, T2, T3, TResult>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return new(_join, selector, distinct: false);
    }

    /// <summary>
    /// Groups the joined rows by a key, each being something to its group, for <c>Having</c>, ordering and paging of
    /// the groups, and a <c>Select</c> of each group's key and aggregates.
    /// </summary>
    /// <typeparam name="TKey">The key's type.</typeparam>
    /// <typeparam name="TElement">The type of what the joined rows are to their group.</typeparam>
    /// <param name="key">The key, over the joined rows.</param>
    /// <param name="element">What the joined rows are to their group, as <c>(o, c) =&gt; new { o.Total, c.Discount }</c>.</param>
    /// <returns>The groups.</returns>
    public DmGroupQuery<TKey, TElement> GroupBy<TKey, TElement>(Expression<Func<T1, T2, T3, TKey>> key, Expression<Func<T1, T2, T3, TElement>> element)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(element);
        return new(_join.Grouped(), key, element);
    }

    /// <summary>
    /// Reads the joined rows.
    /// </summary>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rows, a tuple per combination.</returns>
    public async Task<List<(T1, T2?, T3?)>> ToListAsync(IDbTransaction? tx = null, CancellationToken cancellationToken = default) =>
        (await ReadAsync(_join.Take, keyOrder: false, tx, cancellationToken).ConfigureAwait(false)).ConvertAll(r => ((T1)r[0]!, (T2?)r[1], (T3?)r[2]));

    /// <summary>
    /// Reads the first joined rows: in the query's order, or the first model's key order if it has none.
    /// </summary>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rows, or null if there are none.</returns>
    public async Task<(T1, T2?, T3?)?> FirstAsync(IDbTransaction? tx = null, CancellationToken cancellationToken = default)
    {
        var rows = await ReadAsync(Math.Min(_join.Take ?? 1, 1), keyOrder: true, tx, cancellationToken).ConfigureAwait(false);
        return rows.Count == 0 ? null : rows.ConvertAll(r => ((T1)r[0]!, (T2?)r[1], (T3?)r[2]))[0];
    }

    /// <summary>
    /// Counts the joined rows.
    /// </summary>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows.</returns>
    public Task<long> CountAsync(IDbTransaction? tx = null, CancellationToken cancellationToken = default) =>
        _join.CountAsync(exists: false, tx, cancellationToken);

    /// <summary>
    /// Tells whether there are any joined rows.
    /// </summary>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if there are.</returns>
    public async Task<bool> ExistsAsync(IDbTransaction? tx = null, CancellationToken cancellationToken = default) =>
        await _join.CountAsync(exists: true, tx, cancellationToken).ConfigureAwait(false) != 0;

    /// <summary>
    /// Writes the SQL that <see cref="ToListAsync"/> runs, without running it.
    /// </summary>
    /// <returns>The SQL and its parameters.</returns>
    public DmStatement ToSql()
    {
        var (select, scope, _, _) = _join.Rows([.. _join.Models.Select(_ => true)], _join.Take, keyOrder: false);
        var parameters = new DmlParameters();
        return new DmStatement(new SqlRenderer(scope.Dialect, parameters).For(select).Render(select), parameters.Named);
    }

    private async Task<List<object?[]>> ReadAsync(int? take, bool keyOrder, IDbTransaction? tx, CancellationToken cancellationToken)
    {
        if (take == 0)
        {
            return [];
        }

        var (select, scope, _, segments) = _join.Rows([.. _join.Models.Select(_ => true)], take, keyOrder);
        return await _join.ReadAsync(select, scope.Dialect, segments, 0, tx, cancellationToken).ConfigureAwait(false);
    }

    private DmJoinQuery<T1, T2, T3> Ordered(LambdaExpression key, bool descending, bool then)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (then && _join.Order.Count == 0)
        {
            throw new InvalidOperationException("ThenBy and ThenByDescending follow OrderBy or OrderByDescending.");
        }

        return new(new DmlJoin { Scope = _join.Scope, Models = _join.Models, Joins = _join.Joins, Where = _join.Where, Order = then ? [.. _join.Order, (key, descending)] : [(key, descending)], Skip = _join.Skip, Take = _join.Take, Distinct = _join.Distinct });
    }
}

/// <summary>
/// Rows of 4 models joined by conditions written out: one <c>SELECT</c>, built up by <c>Where</c>, <c>OrderBy</c>,
/// <c>Skip</c>, <c>Take</c> and <c>Distinct</c> over all the joined rows, read as tuples or by <c>Select</c>.
/// </summary>
/// <remarks>A model joined by <c>LeftJoin</c> reads as null where no row matched.</remarks>
/// <typeparam name="T1">The first model.</typeparam>
/// <typeparam name="T2">The second model.</typeparam>
/// <typeparam name="T3">The third model.</typeparam>
/// <typeparam name="T4">The fourth model.</typeparam>
public sealed class DmJoinQuery<T1, T2, T3, T4>
    where T1 : class
    where T2 : class
    where T3 : class
    where T4 : class
{
    private readonly DmlJoin _join;

    /// <summary>
    /// Initializes a new instance of the <see cref="DmJoinQuery{T1, T2, T3, T4}"/> class.
    /// </summary>
    /// <param name="join">The join.</param>
    internal DmJoinQuery(DmlJoin join) => _join = join;

    /// <summary>
    /// Keeps the joined rows that match a condition, as well as any earlier ones.
    /// </summary>
    /// <param name="predicate">The condition, over all the joined rows, which can follow their relations.</param>
    /// <returns>The query.</returns>
    public DmJoinQuery<T1, T2, T3, T4> Where(Expression<Func<T1, T2, T3, T4, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return new(new DmlJoin { Scope = _join.Scope, Models = _join.Models, Joins = _join.Joins, Where = [.. _join.Where, predicate], Order = _join.Order, Skip = _join.Skip, Take = _join.Take, Distinct = _join.Distinct });
    }

    /// <summary>
    /// Orders the rows by a key, ascending, in place of any earlier ordering. NULLs come first, as in LINQ.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="key">The key.</param>
    /// <returns>The query.</returns>
    public DmJoinQuery<T1, T2, T3, T4> OrderBy<TKey>(Expression<Func<T1, T2, T3, T4, TKey>> key) => Ordered(key, descending: false, then: false);

    /// <summary>
    /// Orders the rows by a key, descending, in place of any earlier ordering. NULLs come last, as in LINQ.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="key">The key.</param>
    /// <returns>The query.</returns>
    public DmJoinQuery<T1, T2, T3, T4> OrderByDescending<TKey>(Expression<Func<T1, T2, T3, T4, TKey>> key) => Ordered(key, descending: true, then: false);

    /// <summary>
    /// Orders rows with the same earlier keys by another key, ascending.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="key">The key.</param>
    /// <returns>The query.</returns>
    public DmJoinQuery<T1, T2, T3, T4> ThenBy<TKey>(Expression<Func<T1, T2, T3, T4, TKey>> key) => Ordered(key, descending: false, then: true);

    /// <summary>
    /// Orders rows with the same earlier keys by another key, descending.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="key">The key.</param>
    /// <returns>The query.</returns>
    public DmJoinQuery<T1, T2, T3, T4> ThenByDescending<TKey>(Expression<Func<T1, T2, T3, T4, TKey>> key) => Ordered(key, descending: true, then: true);

    /// <summary>
    /// Skips rows.
    /// </summary>
    /// <param name="count">The number of rows to skip.</param>
    /// <returns>The query.</returns>
    public DmJoinQuery<T1, T2, T3, T4> Skip(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return new(new DmlJoin { Scope = _join.Scope, Models = _join.Models, Joins = _join.Joins, Where = _join.Where, Order = _join.Order, Skip = count, Take = _join.Take, Distinct = _join.Distinct });
    }

    /// <summary>
    /// Returns at most a number of rows.
    /// </summary>
    /// <param name="count">The most rows to return.</param>
    /// <returns>The query.</returns>
    public DmJoinQuery<T1, T2, T3, T4> Take(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return new(new DmlJoin { Scope = _join.Scope, Models = _join.Models, Joins = _join.Joins, Where = _join.Where, Order = _join.Order, Skip = _join.Skip, Take = count, Distinct = _join.Distinct });
    }

    /// <summary>
    /// Returns each distinct combination of rows once.
    /// </summary>
    /// <returns>The query.</returns>
    public DmJoinQuery<T1, T2, T3, T4> Distinct() =>
        new(new DmlJoin { Scope = _join.Scope, Models = _join.Models, Joins = _join.Joins, Where = _join.Where, Order = _join.Order, Skip = _join.Skip, Take = _join.Take, Distinct = true });

    /// <summary>
    /// Reads the joined rows as something else. The rows' columns, their related rows' columns and values over related
    /// rows are read by the database; the rest of the selector runs in .NET.
    /// </summary>
    /// <typeparam name="TResult">What the joined rows are read as.</typeparam>
    /// <param name="selector">How: <c>(o, c) =&gt; new { o.Id, Customer = c.Name }</c>.</param>
    /// <returns>The query.</returns>
    public DmJoinSelect<TResult> Select<TResult>(Expression<Func<T1, T2, T3, T4, TResult>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return new(_join, selector, distinct: false);
    }

    /// <summary>
    /// Groups the joined rows by a key, each being something to its group, for <c>Having</c>, ordering and paging of
    /// the groups, and a <c>Select</c> of each group's key and aggregates.
    /// </summary>
    /// <typeparam name="TKey">The key's type.</typeparam>
    /// <typeparam name="TElement">The type of what the joined rows are to their group.</typeparam>
    /// <param name="key">The key, over the joined rows.</param>
    /// <param name="element">What the joined rows are to their group, as <c>(o, c) =&gt; new { o.Total, c.Discount }</c>.</param>
    /// <returns>The groups.</returns>
    public DmGroupQuery<TKey, TElement> GroupBy<TKey, TElement>(Expression<Func<T1, T2, T3, T4, TKey>> key, Expression<Func<T1, T2, T3, T4, TElement>> element)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(element);
        return new(_join.Grouped(), key, element);
    }

    /// <summary>
    /// Reads the joined rows.
    /// </summary>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rows, a tuple per combination.</returns>
    public async Task<List<(T1, T2?, T3?, T4?)>> ToListAsync(IDbTransaction? tx = null, CancellationToken cancellationToken = default) =>
        (await ReadAsync(_join.Take, keyOrder: false, tx, cancellationToken).ConfigureAwait(false)).ConvertAll(r => ((T1)r[0]!, (T2?)r[1], (T3?)r[2], (T4?)r[3]));

    /// <summary>
    /// Reads the first joined rows: in the query's order, or the first model's key order if it has none.
    /// </summary>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rows, or null if there are none.</returns>
    public async Task<(T1, T2?, T3?, T4?)?> FirstAsync(IDbTransaction? tx = null, CancellationToken cancellationToken = default)
    {
        var rows = await ReadAsync(Math.Min(_join.Take ?? 1, 1), keyOrder: true, tx, cancellationToken).ConfigureAwait(false);
        return rows.Count == 0 ? null : rows.ConvertAll(r => ((T1)r[0]!, (T2?)r[1], (T3?)r[2], (T4?)r[3]))[0];
    }

    /// <summary>
    /// Counts the joined rows.
    /// </summary>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows.</returns>
    public Task<long> CountAsync(IDbTransaction? tx = null, CancellationToken cancellationToken = default) =>
        _join.CountAsync(exists: false, tx, cancellationToken);

    /// <summary>
    /// Tells whether there are any joined rows.
    /// </summary>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if there are.</returns>
    public async Task<bool> ExistsAsync(IDbTransaction? tx = null, CancellationToken cancellationToken = default) =>
        await _join.CountAsync(exists: true, tx, cancellationToken).ConfigureAwait(false) != 0;

    /// <summary>
    /// Writes the SQL that <see cref="ToListAsync"/> runs, without running it.
    /// </summary>
    /// <returns>The SQL and its parameters.</returns>
    public DmStatement ToSql()
    {
        var (select, scope, _, _) = _join.Rows([.. _join.Models.Select(_ => true)], _join.Take, keyOrder: false);
        var parameters = new DmlParameters();
        return new DmStatement(new SqlRenderer(scope.Dialect, parameters).For(select).Render(select), parameters.Named);
    }

    private async Task<List<object?[]>> ReadAsync(int? take, bool keyOrder, IDbTransaction? tx, CancellationToken cancellationToken)
    {
        if (take == 0)
        {
            return [];
        }

        var (select, scope, _, segments) = _join.Rows([.. _join.Models.Select(_ => true)], take, keyOrder);
        return await _join.ReadAsync(select, scope.Dialect, segments, 0, tx, cancellationToken).ConfigureAwait(false);
    }

    private DmJoinQuery<T1, T2, T3, T4> Ordered(LambdaExpression key, bool descending, bool then)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (then && _join.Order.Count == 0)
        {
            throw new InvalidOperationException("ThenBy and ThenByDescending follow OrderBy or OrderByDescending.");
        }

        return new(new DmlJoin { Scope = _join.Scope, Models = _join.Models, Joins = _join.Joins, Where = _join.Where, Order = then ? [.. _join.Order, (key, descending)] : [(key, descending)], Skip = _join.Skip, Take = _join.Take, Distinct = _join.Distinct });
    }
}

/// <summary>
/// Joined rows read as something else: the rows' columns, their related rows' columns and values over related rows
/// read by the database, and the rest of the selector run in .NET.
/// </summary>
/// <typeparam name="TResult">What the joined rows are read as.</typeparam>
public sealed class DmJoinSelect<TResult>
{
    private readonly DmlJoin _join;
    private readonly LambdaExpression _selector;
    private readonly bool _distinct;

    /// <summary>
    /// Initializes a new instance of the <see cref="DmJoinSelect{TResult}"/> class.
    /// </summary>
    /// <param name="join">The join.</param>
    /// <param name="selector">What the joined rows are read as.</param>
    /// <param name="distinct">Whether the results are distinct.</param>
    internal DmJoinSelect(DmlJoin join, LambdaExpression selector, bool distinct)
    {
        _join = join;
        _selector = selector;
        _distinct = distinct;
    }

    /// <summary>
    /// Returns each distinct result once, as the database compares what the selector reads.
    /// </summary>
    /// <returns>The query.</returns>
    public DmJoinSelect<TResult> Distinct() => new(_join, _selector, distinct: true);

    /// <summary>
    /// Reads the results.
    /// </summary>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The results.</returns>
    public Task<List<TResult>> ToListAsync(IDbTransaction? tx = null, CancellationToken cancellationToken = default) =>
        ReadAsync(_join.Take, keyOrder: false, tx, cancellationToken);

    /// <summary>
    /// Reads the first result: in the query's order, or the first model's key order if it has none.
    /// </summary>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result, or the default if there is none.</returns>
    public async Task<TResult?> FirstAsync(IDbTransaction? tx = null, CancellationToken cancellationToken = default) =>
        (await ReadAsync(Math.Min(_join.Take ?? 1, 1), keyOrder: true, tx, cancellationToken).ConfigureAwait(false)).FirstOrDefault();

    /// <summary>
    /// Reads the one result the query returns.
    /// </summary>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result, or the default if there is none.</returns>
    /// <exception cref="InvalidOperationException">The query returns more than one.</exception>
    public async Task<TResult?> SingleAsync(IDbTransaction? tx = null, CancellationToken cancellationToken = default) =>
        DmQuery<object>.Single(await ReadAsync(Math.Min(_join.Take ?? 2, 2), keyOrder: false, tx, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Writes the SQL that <see cref="ToListAsync"/> runs, without running it.
    /// </summary>
    /// <returns>The SQL and its parameters.</returns>
    public DmStatement ToSql()
    {
        var (select, dialect, _, _, _) = Build(_join.Take, keyOrder: false);
        var parameters = new DmlParameters();
        return new DmStatement(new SqlRenderer(dialect, parameters).For(select).Render(select), parameters.Named);
    }

    private (SqlSelect Select, DmlDialect Dialect, List<DmlSegment> Segments, int Values, Func<object?[], object?[], TResult> Reads) Build(
        int? take,
        bool keyOrder
    )
    {
        var models = _join.Models.Select(DmlModel.For).ToList();
        var (reads, parts, whole) = DmlProjection.JoinParts(_selector, models);
        var (select, scope, sources, segments) = _join.Rows(whole, take, keyOrder);
        List<SqlSelectItem> items =
        [
            .. parts.Select((part, i) =>
            {
                var lambda = Expression.Lambda(part, _selector.Parameters);
                var used = sources.Take(_selector.Parameters.Count).ToList();
                SqlNode node = DmlProjection.IsTest(part)
                    ? new SqlCase(DmlTranslator.Condition(lambda, used, scope), new SqlText("1"), new SqlText("0"))
                    : DmlTranslator.Value(lambda, used, scope).Node;
                return new SqlSelectItem(node, $"dm_p{i}");
            }),
        ];

        select = select with { Items = [.. select.Items, .. items], Distinct = select.Distinct || _distinct };
        if (_distinct && select.OrderBy.Count == 0 && (select.Skip is not null || select.Take is not null)
            && scope.Dialect.ProviderType == DbProviderType.SqlServer)
        {
            // Paging needs an ORDER BY on SQL Server, and with DISTINCT it must be in the select list.
            select = select with { OrderBySql = "1" };
        }

        return (select, scope.Dialect, segments, items.Count, (Func<object?[], object?[], TResult>)reads.Compile());
    }

    private async Task<List<TResult>> ReadAsync(int? take, bool keyOrder, IDbTransaction? tx, CancellationToken cancellationToken)
    {
        if (take == 0)
        {
            return [];
        }

        var (select, dialect, segments, values, reads) = Build(take, keyOrder);
        var rows = await _join.ReadAsync(select, dialect, segments, values, tx, cancellationToken).ConfigureAwait(false);
        return rows.ConvertAll(r => reads(r[segments.Count..], r[..segments.Count]));
    }
}
