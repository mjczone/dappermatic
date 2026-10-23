// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using MJCZone.DapperMatic.Dml.Internal;

namespace MJCZone.DapperMatic.Dml;

/// <summary>
/// Fills relation properties from rows already in memory, without a query: by a <c>[DmRelation]</c>'s keys, fixed
/// values and condition, or by key selectors given explicitly.
/// </summary>
/// <remarks>
/// Keys are compared as they are typed, never as strings and never by the rows' order. After a merge the relation
/// counts as loaded: a collection is never null (empty if no row matches), and a relation of one row is the matching
/// row or null.
/// </remarks>
/// <example>
/// <code>
/// DmMerge.Into(orders, o =&gt; o.Customer, customers);                 // by the relation
/// DmMerge.Into(orders, o =&gt; o.Lines, lines);
/// DmMerge.Into(orders, o =&gt; o.Lines, lines, o =&gt; o.Id, l =&gt; l.OrderId);   // by explicit keys
/// DmMerge.Into(products, p =&gt; p.Tags, tags, productTags);           // many-to-many, through the link rows
/// </code>
/// </example>
public static class DmMerge
{
    /// <summary>
    /// Sets each owner's relation of one row to the row its keys match, or null.
    /// </summary>
    /// <typeparam name="TOwner">The owners' model.</typeparam>
    /// <typeparam name="TOther">The related model.</typeparam>
    /// <param name="owners">The owners.</param>
    /// <param name="relation">The relation property, marked <c>[DmRelation]</c>: <c>o =&gt; o.Customer</c>.</param>
    /// <param name="others">The related rows.</param>
    /// <exception cref="InvalidOperationException">Several rows match one owner.</exception>
    public static void Into<TOwner, TOther>(IEnumerable<TOwner> owners, Expression<Func<TOwner, TOther?>> relation, IEnumerable<TOther> others)
        where TOwner : class
        where TOther : class => ByRelation(owners, relation, others, links: null);

    /// <summary>
    /// Sets each owner's collection to the rows its keys match: empty if none do.
    /// </summary>
    /// <typeparam name="TOwner">The owners' model.</typeparam>
    /// <typeparam name="TOther">The related model.</typeparam>
    /// <param name="owners">The owners.</param>
    /// <param name="relation">The relation property, marked <c>[DmRelation]</c>: <c>o =&gt; o.Lines</c>.</param>
    /// <param name="others">The related rows.</param>
    [OverloadResolutionPriority(1)] // A collection is also a TOther: this overload is the one that sees its rows.
    public static void Into<TOwner, TOther>(
        IEnumerable<TOwner> owners,
        Expression<Func<TOwner, IEnumerable<TOther>?>> relation,
        IEnumerable<TOther> others
    )
        where TOwner : class
        where TOther : class => ByRelation(owners, relation, others, links: null);

    /// <summary>
    /// Sets each owner's many-to-many collection to the rows the link rows join it to: empty if none do.
    /// </summary>
    /// <typeparam name="TOwner">The owners' model.</typeparam>
    /// <typeparam name="TOther">The related model.</typeparam>
    /// <typeparam name="TLink">The link model the relation goes through.</typeparam>
    /// <param name="owners">The owners.</param>
    /// <param name="relation">The relation property, marked <c>[DmRelation(through: ...)]</c>.</param>
    /// <param name="others">The related rows.</param>
    /// <param name="links">The link rows.</param>
    public static void Into<TOwner, TOther, TLink>(
        IEnumerable<TOwner> owners,
        Expression<Func<TOwner, IEnumerable<TOther>?>> relation,
        IEnumerable<TOther> others,
        IEnumerable<TLink> links
    )
        where TOwner : class
        where TOther : class
        where TLink : class => ByRelation(owners, relation, others, links);

    /// <summary>
    /// Sets each owner's property to the row whose key equals the owner's, or null.
    /// </summary>
    /// <typeparam name="TOwner">The owners' model.</typeparam>
    /// <typeparam name="TOther">The related model.</typeparam>
    /// <typeparam name="TKey">The key; a tuple for a key of several values, as <c>o =&gt; (o.A, o.B)</c>.</typeparam>
    /// <param name="owners">The owners.</param>
    /// <param name="property">The property to set, a relation or not.</param>
    /// <param name="others">The related rows.</param>
    /// <param name="ownerKey">An owner's key.</param>
    /// <param name="otherKey">A related row's key.</param>
    /// <exception cref="InvalidOperationException">Several rows match one owner.</exception>
    public static void Into<TOwner, TOther, TKey>(
        IEnumerable<TOwner> owners,
        Expression<Func<TOwner, TOther?>> property,
        IEnumerable<TOther> others,
        Func<TOwner, TKey> ownerKey,
        Func<TOther, TKey> otherKey
    )
        where TOwner : class
        where TOther : class => ByKeys(owners, property, others, ownerKey, otherKey, collection: false);

    /// <summary>
    /// Sets each owner's collection to the rows whose key equals the owner's: empty if none do.
    /// </summary>
    /// <typeparam name="TOwner">The owners' model.</typeparam>
    /// <typeparam name="TOther">The related model.</typeparam>
    /// <typeparam name="TKey">The key; a tuple for a key of several values.</typeparam>
    /// <param name="owners">The owners.</param>
    /// <param name="property">The collection property to set, a relation or not.</param>
    /// <param name="others">The related rows.</param>
    /// <param name="ownerKey">An owner's key.</param>
    /// <param name="otherKey">A related row's key.</param>
    [OverloadResolutionPriority(1)] // A collection is also a TOther: this overload is the one that sees its rows.
    public static void Into<TOwner, TOther, TKey>(
        IEnumerable<TOwner> owners,
        Expression<Func<TOwner, IEnumerable<TOther>?>> property,
        IEnumerable<TOther> others,
        Func<TOwner, TKey> ownerKey,
        Func<TOther, TKey> otherKey
    )
        where TOwner : class
        where TOther : class => ByKeys(owners, property, others, ownerKey, otherKey, collection: true);

    private static PropertyInfo PropertyOf(LambdaExpression property)
    {
        ArgumentNullException.ThrowIfNull(property);
        var body = property.Body is UnaryExpression { NodeType: ExpressionType.Convert } convert ? convert.Operand : property.Body;
        return body is MemberExpression { Member: PropertyInfo info, Expression: ParameterExpression } && info.CanWrite
            ? info
            : throw new ArgumentException($"Merge sets a settable property of the owners, as o => o.Lines, and {property} is not one.", nameof(property));
    }

    private static void ByKeys<TOwner, TOther, TKey>(
        IEnumerable<TOwner> owners,
        LambdaExpression property,
        IEnumerable<TOther> others,
        Func<TOwner, TKey> ownerKey,
        Func<TOther, TKey> otherKey,
        bool collection
    )
        where TOwner : class
        where TOther : class
    {
        ArgumentNullException.ThrowIfNull(owners);
        ArgumentNullException.ThrowIfNull(others);
        ArgumentNullException.ThrowIfNull(ownerKey);
        ArgumentNullException.ThrowIfNull(otherKey);
        var info = PropertyOf(property);

        // Boxed keys, whose Equals is the key type's own: a nullable key and its value box alike.
        var byKey = new Dictionary<object, List<TOther>>();
        foreach (var other in others)
        {
            object? key = otherKey(other);
            if (key is null)
            {
                continue;
            }

            if (!byKey.TryGetValue(key, out var same))
            {
                byKey[key] = same = [];
            }

            same.Add(other);
        }

        foreach (var owner in owners)
        {
            object? key = ownerKey(owner);
            var matches = key is not null && byKey.TryGetValue(key, out var found) ? found : [];
            Set(info, owner, matches, collection, typeof(TOther));
        }
    }

    private static void ByRelation<TOwner, TOther>(
        IEnumerable<TOwner> owners,
        LambdaExpression property,
        IEnumerable<TOther> others,
        System.Collections.IEnumerable? links
    )
        where TOwner : class
        where TOther : class
    {
        ArgumentNullException.ThrowIfNull(owners);
        ArgumentNullException.ThrowIfNull(others);
        var info = PropertyOf(property);
        var relation = DmlModel.For(typeof(TOwner)).RelationOf(info)
            ?? throw new ArgumentException(
                $"{typeof(TOwner).Name}.{info.Name} is not a relation: mark it [DmRelation], or give the keys to merge on.",
                nameof(property)
            );
        if ((relation.Link is null) != (links is null))
        {
            throw new ArgumentException(
                relation.Link is null
                    ? $"{typeof(TOwner).Name}.{info.Name} does not go through a link model, so it takes no link rows."
                    : $"{typeof(TOwner).Name}.{info.Name} goes through {relation.Link.Type.Name}: pass its rows too.",
                nameof(links)
            );
        }

        var single = relation.Condition is { Parameters.Count: 1 } one ? (Func<TOther, bool>)one.Compile() : null;
        var paired = relation.Condition is { Parameters.Count: 2 } two ? (Func<TOwner, TOther, bool>)two.Compile() : null;
        bool Fits(TOther other) =>
            relation.OtherValues.All(v => Equals(v.Column.Property.GetValue(other), v.Value)) && (single?.Invoke(other) ?? true);

        // The related rows by the keys owners have: their own, or through the link rows the owners' keys.
        var byOtherKey = Group(others.Where(Fits), relation.Keys.Select(k => k.Other).ToList());
        Dictionary<DmlKey, List<TOther>> byOwnerKey;
        List<DmlColumn> ownerKeys;
        if (relation.Link is null)
        {
            byOwnerKey = byOtherKey;
            ownerKeys = [.. relation.Keys.Select(k => k.This)];
        }
        else
        {
            byOwnerKey = [];
            var linkToOther = relation.Keys.Select(k => k.This).ToList();
            foreach (var (key, linked) in Group(links!.Cast<object>(), relation.LinkKeys.Select(k => k.Link).ToList()))
            {
                byOwnerKey[key] = [.. linked.SelectMany(l => byOtherKey.TryGetValue(DmlKey.Of(l, linkToOther), out var found) ? found : [])];
            }

            ownerKeys = [.. relation.LinkKeys.Select(k => k.This)];
        }

        foreach (var owner in owners)
        {
            var key = DmlKey.Of(owner, ownerKeys);
            var applies = !key.HasNull && relation.ThisValues.All(v => Equals(v.Column.Property.GetValue(owner), v.Value));
            List<TOther> matches = applies && byOwnerKey.TryGetValue(key, out var found)
                ? paired is null ? found : [.. found.Where(o => paired(owner, o))]
                : [];
            Set(info, owner, matches, relation.IsCollection, typeof(TOther));
        }
    }

    private static Dictionary<DmlKey, List<TRow>> Group<TRow>(IEnumerable<TRow> rows, List<DmlColumn> columns)
        where TRow : class
    {
        var groups = new Dictionary<DmlKey, List<TRow>>();
        foreach (var row in rows)
        {
            var key = DmlKey.Of(row, columns);
            if (key.HasNull)
            {
                continue;
            }

            if (!groups.TryGetValue(key, out var same))
            {
                groups[key] = same = [];
            }

            same.Add(row);
        }

        return groups;
    }

    private static void Set<TOther>(PropertyInfo property, object owner, List<TOther> matches, bool collection, Type otherType)
    {
        if (collection)
        {
            property.SetValue(owner, DmlLoader.Collection(property, otherType, [.. matches.Cast<object>()]));
            return;
        }

        property.SetValue(
            owner,
            matches.Count <= 1
                ? matches.FirstOrDefault()
                : throw new InvalidOperationException(
                    $"{property.DeclaringType!.Name}.{property.Name} is one row, and {matches.Count} {otherType.Name} rows match one owner."
                )
        );
    }
}
