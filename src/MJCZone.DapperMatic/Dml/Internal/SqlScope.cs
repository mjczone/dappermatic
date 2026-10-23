// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Dml.Internal;

/// <summary>
/// The sources one <c>SELECT</c> of a statement reads, while it is translated: the source it reads from, the sources
/// relations join to it, and its subqueries' scopes, which share the statement's aliases.
/// </summary>
/// <remarks>
/// A statement that writes rows, an <c>UPDATE</c> or a <c>DELETE</c>, has a scope that joins nothing: a related row's
/// column is read there by a subquery instead.
/// </remarks>
internal sealed class SqlScope
{
    private readonly SqlScope? _parent;
    private readonly Func<Type, string>? _tableOf;
    private readonly Func<SqlSource, bool, IEnumerable<SqlNode>>? _filters;
    private readonly Func<DmlModel, IReadOnlySet<string>?>? _hidden;
    private readonly SqlSource? _root;
    private readonly int[] _nextAlias;
    private readonly HashSet<SqlSource> _owned = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<(SqlSource Owner, DmlRelation Relation), SqlSource> _joined = [];
    private readonly List<SqlJoin> _joins = [];

    private SqlScope(
        DmlDialect dialect,
        Func<Type, string>? tableOf,
        bool allowsJoins,
        SqlScope? parent,
        int[] nextAlias,
        Func<SqlSource, bool, IEnumerable<SqlNode>>? filters,
        Func<DmlModel, IReadOnlySet<string>?>? hidden,
        SqlSource? root
    )
    {
        Dialect = dialect;
        _tableOf = tableOf;
        AllowsJoins = allowsJoins;
        _parent = parent;
        _nextAlias = nextAlias;
        _filters = filters;
        _hidden = hidden;
        _root = root;
    }

    /// <summary>Gets the provider's dialect.</summary>
    public DmlDialect Dialect { get; }

    /// <summary>Gets a value indicating whether conditions may follow relations, which needs the related tables.</summary>
    public bool AllowsRelations => _tableOf is not null;

    /// <summary>Gets a value indicating whether relations may join sources to this scope's.</summary>
    public bool AllowsJoins { get; }

    /// <summary>Gets the sources joined, in order.</summary>
    public IReadOnlyList<SqlJoin> Joins => _joins;

    /// <summary>
    /// Makes the scope of a <c>SELECT</c>, which reads from a source and joins the rows its relations lead to.
    /// </summary>
    /// <param name="dialect">The dialect.</param>
    /// <param name="tableOf">The quoted, qualified table of a model type, as the statement's scope names it.</param>
    /// <param name="from">The source read from, aliased <c>t0</c>.</param>
    /// <param name="filters">
    /// The conditions keeping a source's rows to those the statement sees (its tenant's, not deleted), given whether it
    /// is the source the statement is about; or null for none.
    /// </param>
    /// <param name="hidden">The columns of a model the statement's caller may not read, or null for none.</param>
    /// <returns>The scope.</returns>
    public static SqlScope Select(
        DmlDialect dialect,
        Func<Type, string> tableOf,
        SqlSource from,
        Func<SqlSource, bool, IEnumerable<SqlNode>>? filters = null,
        Func<DmlModel, IReadOnlySet<string>?>? hidden = null
    ) => Rooted(dialect, tableOf, allowsJoins: true, from, filters, hidden);

    /// <summary>
    /// Makes the scope of an <c>UPDATE</c> or a <c>DELETE</c>, which joins nothing.
    /// </summary>
    /// <param name="dialect">The dialect.</param>
    /// <param name="tableOf">The quoted, qualified table of a model type, as the statement's scope names it.</param>
    /// <param name="target">The source written.</param>
    /// <param name="filters">The conditions keeping a source's rows to those the statement sees, or null for none.</param>
    /// <param name="hidden">The columns of a model the statement's caller may not read, or null for none.</param>
    /// <returns>The scope.</returns>
    public static SqlScope Write(
        DmlDialect dialect,
        Func<Type, string> tableOf,
        SqlSource target,
        Func<SqlSource, bool, IEnumerable<SqlNode>>? filters = null,
        Func<DmlModel, IReadOnlySet<string>?>? hidden = null
    ) => Rooted(dialect, tableOf, allowsJoins: false, target, filters, hidden);

    /// <summary>
    /// Makes the scope of a statement whose conditions may not follow relations, such as an upsert's.
    /// </summary>
    /// <param name="dialect">The dialect.</param>
    /// <returns>The scope.</returns>
    public static SqlScope Plain(DmlDialect dialect) => new(dialect, null, allowsJoins: false, null, [1], null, null, null);

    /// <summary>
    /// Makes the scope of a subquery, which joins what its own conditions need.
    /// </summary>
    /// <returns>The scope.</returns>
    public SqlScope Nested() => new(Dialect, _tableOf, allowsJoins: true, this, _nextAlias, _filters, _hidden, _root);

    /// <summary>
    /// Writes the conditions that keep a source's rows to those the statement sees: its tenant's, and not deleted.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The conditions; none for a model without the columns.</returns>
    public IEnumerable<SqlNode> Filters(SqlSource source) =>
        _filters?.Invoke(source, ReferenceEquals(source, _root)) ?? [];

    /// <summary>
    /// Makes a source of a model's table, with the statement's next alias, read by this scope.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="optional">Whether the source's row may be missing, as a <c>LEFT JOIN</c>'s may.</param>
    /// <returns>The source.</returns>
    public SqlSource Source(DmlModel model, bool optional = false)
    {
        var tableOf = _tableOf ?? throw new InvalidOperationException("This statement reads only its own table.");
        var source = new SqlSource(tableOf(model.Type), $"t{_nextAlias[0]++}", model.Table, model) { Optional = optional, Hidden = _hidden?.Invoke(model) };
        _owned.Add(source);
        return source;
    }

    /// <summary>
    /// Joins a source to this scope.
    /// </summary>
    /// <param name="source">A source made by <see cref="Source"/>.</param>
    /// <param name="kind"><c>LEFT JOIN</c>, <c>INNER JOIN</c> or <c>CROSS JOIN</c>.</param>
    /// <param name="on">The join condition, or null for a cross join.</param>
    public void Join(SqlSource source, string kind, SqlNode? on) => _joins.Add(new SqlJoin(source, kind, on));

    /// <summary>
    /// Finds or makes the join of a to-one relation, in the scope that reads its owner: the rows of one source's
    /// relation are joined once, however often a statement uses them.
    /// </summary>
    /// <param name="owner">The source whose relation it is.</param>
    /// <param name="relation">The relation.</param>
    /// <param name="on">Makes the join condition, given the related source.</param>
    /// <returns>The related source, or null if the scope that reads the owner joins nothing.</returns>
    public SqlSource? JoinOnce(SqlSource owner, DmlRelation relation, Func<SqlScope, SqlSource, SqlNode> on)
    {
        var scope = OwnerOf(owner) ?? this;
        if (!scope.AllowsJoins)
        {
            return null;
        }

        if (scope._joined.TryGetValue((owner, relation), out var joined))
        {
            return joined;
        }

        joined = scope.Source(relation.Other, optional: true);
        scope.Join(joined, "LEFT JOIN", on(scope, joined));
        scope._joined[(owner, relation)] = joined;
        return joined;
    }

    private static SqlScope Rooted(
        DmlDialect dialect,
        Func<Type, string> tableOf,
        bool allowsJoins,
        SqlSource root,
        Func<SqlSource, bool, IEnumerable<SqlNode>>? filters,
        Func<DmlModel, IReadOnlySet<string>?>? hidden
    )
    {
        var scope = new SqlScope(dialect, tableOf, allowsJoins, null, [1], filters, hidden, root);
        scope._owned.Add(root);
        return scope;
    }

    private SqlScope? OwnerOf(SqlSource source)
    {
        for (var scope = this; scope is not null; scope = scope._parent)
        {
            if (scope._owned.Contains(source))
            {
                return scope;
            }
        }

        return null;
    }
}
