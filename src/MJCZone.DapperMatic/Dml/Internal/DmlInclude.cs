// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Linq.Expressions;
using System.Reflection;

namespace MJCZone.DapperMatic.Dml.Internal;

/// <summary>
/// A relation to load, with which of its rows and what to load with them: what <c>Include</c> builds.
/// </summary>
internal sealed class DmlInclude
{
    /// <summary>Gets the relation.</summary>
    public required DmlRelation Relation { get; init; }

    /// <summary>Gets the conditions the related rows must meet.</summary>
    public IReadOnlyList<LambdaExpression> Filters { get; init; } = [];

    /// <summary>Gets the order of each owner's rows.</summary>
    public IReadOnlyList<(LambdaExpression Key, bool Descending)> Order { get; init; } = [];

    /// <summary>Gets the most rows to load for each owner, or null.</summary>
    public int? Take { get; init; }

    /// <summary>Gets the related rows' own relations to load.</summary>
    public IReadOnlyList<DmlInclude> Includes { get; init; } = [];

    /// <summary>
    /// Resolves an include of a relation property.
    /// </summary>
    /// <typeparam name="TRelated">The related model.</typeparam>
    /// <param name="ownerType">The model the property belongs to.</param>
    /// <param name="relation">The relation property, as <c>o =&gt; o.Lines</c>.</param>
    /// <param name="configure">Which related rows to load, and what with them; or null.</param>
    /// <returns>The include.</returns>
    /// <exception cref="ArgumentException">The expression is not a relation property, or the rows are configured for a relation of one row.</exception>
    public static DmlInclude Of<TRelated>(
        Type ownerType,
        LambdaExpression relation,
        Func<DmInclude<TRelated>, DmInclude<TRelated>>? configure
    )
        where TRelated : class
    {
        ArgumentNullException.ThrowIfNull(relation);
        var body = relation.Body is UnaryExpression { NodeType: ExpressionType.Convert } convert ? convert.Operand : relation.Body;
        if (body is not MemberExpression { Member: PropertyInfo property, Expression: ParameterExpression })
        {
            throw new ArgumentException(
                $"Include names a relation property of {ownerType.Name}, as o => o.Lines, and {relation} is not one.",
                nameof(relation)
            );
        }

        var resolved = DmlModel.For(ownerType).RelationOf(property)
            ?? throw new ArgumentException($"{ownerType.Name}.{property.Name} is not a relation: mark it [DmRelation].", nameof(relation));

        var configured = configure?.Invoke(new DmInclude<TRelated>());
        if (configured is not null && !resolved.IsCollection
            && (configured.Filters.Count > 0 || configured.Order.Count > 0 || configured.TakeCount is not null))
        {
            throw new ArgumentException(
                $"{ownerType.Name}.{property.Name} holds one row: Where, OrderBy and Take choose among a collection's rows.",
                nameof(configure)
            );
        }

        return new DmlInclude
        {
            Relation = resolved,
            Filters = configured?.Filters ?? [],
            Order = configured?.Order ?? [],
            Take = configured?.TakeCount,
            Includes = configured?.Includes ?? [],
        };
    }

    /// <summary>
    /// Adds an include to a list, refusing a relation already in it.
    /// </summary>
    /// <param name="includes">The list.</param>
    /// <param name="include">The include.</param>
    /// <returns>The new list.</returns>
    /// <exception cref="InvalidOperationException">The relation is already included.</exception>
    public static IReadOnlyList<DmlInclude> Add(IReadOnlyList<DmlInclude> includes, DmlInclude include) =>
        includes.Any(i => i.Relation == include.Relation)
            ? throw new InvalidOperationException(
                $"{include.Relation.Owner.Type.Name}.{include.Relation.Property.Name} is included twice: include it once, "
                    + "with everything to load with it."
            )
            : [.. includes, include];
}
