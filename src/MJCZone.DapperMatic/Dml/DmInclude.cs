// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using MJCZone.DapperMatic.Dml.Internal;

namespace MJCZone.DapperMatic.Dml;

/// <summary>
/// Which of a relation's rows to load, and what to load with them: <c>Where</c>, <c>OrderBy</c> and <c>Take</c> for a
/// collection, and <c>Include</c> for the rows' own relations, to any depth.
/// </summary>
/// <remarks>
/// <c>Take</c> applies to each owner's rows: <c>c =&gt; c.Orders, o =&gt; o.OrderByDescending(x =&gt; x.PlacedAt).Take(5)</c>
/// loads each customer's five latest orders.
/// </remarks>
/// <typeparam name="T">The related model.</typeparam>
public sealed class DmInclude<T>
    where T : class
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DmInclude{T}"/> class: every row, with nothing more.
    /// </summary>
    internal DmInclude()
        : this([], [], null, []) { }

    private DmInclude(
        IReadOnlyList<LambdaExpression> filters,
        IReadOnlyList<(LambdaExpression Key, bool Descending)> order,
        int? take,
        IReadOnlyList<DmlInclude> includes
    )
    {
        Filters = filters;
        Order = order;
        TakeCount = take;
        Includes = includes;
    }

    /// <summary>Gets the conditions the rows must meet, ANDed.</summary>
    internal IReadOnlyList<LambdaExpression> Filters { get; }

    /// <summary>Gets the order of each owner's rows.</summary>
    internal IReadOnlyList<(LambdaExpression Key, bool Descending)> Order { get; }

    /// <summary>Gets the most rows to load for each owner, or null.</summary>
    internal int? TakeCount { get; }

    /// <summary>Gets the rows' own relations to load.</summary>
    internal IReadOnlyList<DmlInclude> Includes { get; }

    /// <summary>
    /// Loads only the rows that meet a condition, as well as any earlier ones.
    /// </summary>
    /// <param name="predicate">The condition, which can follow the rows' own relations.</param>
    /// <returns>The include.</returns>
    public DmInclude<T> Where(Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return new([.. Filters, predicate], Order, TakeCount, Includes);
    }

    /// <summary>
    /// Orders each owner's rows by a key, ascending, in place of any earlier ordering.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="key">The key.</param>
    /// <returns>The include.</returns>
    public DmInclude<T> OrderBy<TKey>(Expression<Func<T, TKey>> key) => Ordered(key, descending: false, then: false);

    /// <summary>
    /// Orders each owner's rows by a key, descending, in place of any earlier ordering.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="key">The key.</param>
    /// <returns>The include.</returns>
    public DmInclude<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> key) => Ordered(key, descending: true, then: false);

    /// <summary>
    /// Orders rows with the same earlier keys by another key, ascending.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="key">The key.</param>
    /// <returns>The include.</returns>
    public DmInclude<T> ThenBy<TKey>(Expression<Func<T, TKey>> key) => Ordered(key, descending: false, then: true);

    /// <summary>
    /// Orders rows with the same earlier keys by another key, descending.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="key">The key.</param>
    /// <returns>The include.</returns>
    public DmInclude<T> ThenByDescending<TKey>(Expression<Func<T, TKey>> key) => Ordered(key, descending: true, then: true);

    /// <summary>
    /// Loads at most a number of rows for each owner, in the include's order (or key order if it has none).
    /// </summary>
    /// <param name="count">The most rows per owner.</param>
    /// <returns>The include.</returns>
    public DmInclude<T> Take(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return new(Filters, Order, count, Includes);
    }

    /// <summary>
    /// Loads a relation of these rows that holds one row.
    /// </summary>
    /// <typeparam name="TRelated">The related model.</typeparam>
    /// <param name="relation">The relation property: <c>l =&gt; l.Product</c>.</param>
    /// <param name="configure">What to load with the related rows, or null.</param>
    /// <returns>The include.</returns>
    public DmInclude<T> Include<TRelated>(
        Expression<Func<T, TRelated?>> relation,
        Func<DmInclude<TRelated>, DmInclude<TRelated>>? configure = null
    )
        where TRelated : class => With(DmlInclude.Of(typeof(T), relation, configure));

    /// <summary>
    /// Loads a relation of these rows that holds many rows.
    /// </summary>
    /// <typeparam name="TRelated">The related model.</typeparam>
    /// <param name="relation">The relation property: <c>p =&gt; p.Tags</c>.</param>
    /// <param name="configure">Which related rows to load, in what order, and what with them; or null for all.</param>
    /// <returns>The include.</returns>
    [OverloadResolutionPriority(1)] // A collection is also a TRelated: this overload is the one that sees its rows.
    public DmInclude<T> Include<TRelated>(
        Expression<Func<T, IEnumerable<TRelated>?>> relation,
        Func<DmInclude<TRelated>, DmInclude<TRelated>>? configure = null
    )
        where TRelated : class => With(DmlInclude.Of(typeof(T), relation, configure));

    private DmInclude<T> With(DmlInclude include) => new(Filters, Order, TakeCount, DmlInclude.Add(Includes, include));

    private DmInclude<T> Ordered(LambdaExpression key, bool descending, bool then)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (then && Order.Count == 0)
        {
            throw new InvalidOperationException("ThenBy and ThenByDescending follow OrderBy or OrderByDescending.");
        }

        return new(Filters, then ? [.. Order, (key, descending)] : [(key, descending)], TakeCount, Includes);
    }
}
