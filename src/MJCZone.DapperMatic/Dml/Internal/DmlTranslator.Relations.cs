// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Linq.Expressions;
using System.Reflection;

namespace MJCZone.DapperMatic.Dml.Internal;

/// <summary>
/// Relations in expressions. A related row's column (<c>o.Customer!.Country</c>) is joined in a query and read by a
/// subquery in an update or a delete, and reads as NULL when there is no related row, as <c>o.Customer?.Country</c>
/// would. Tests of related rows (<c>o.Lines!.Any(l =&gt; ...)</c>, <c>All</c>, <c>o.Customer == null</c>) are
/// <c>EXISTS</c> subqueries, and values over them (<c>Count</c>, <c>Sum</c>, <c>Min</c>, <c>Max</c>, <c>Average</c>)
/// subqueries of one value.
/// </summary>
internal sealed partial class DmlTranslator
{
    private static readonly HashSet<string> CollectionValues =
    [
        nameof(Enumerable.Count),
        nameof(Enumerable.LongCount),
        nameof(Enumerable.Sum),
        nameof(Enumerable.Min),
        nameof(Enumerable.Max),
        nameof(Enumerable.Average),
    ];

    /// <summary>
    /// Writes a relation's fixed values as conditions on a source's columns.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="values">The columns and their values.</param>
    /// <returns>A condition per value: <c>= @p</c>, or <c>IS NULL</c> for a null.</returns>
    internal static IEnumerable<SqlNode> FixedValues(SqlSource source, IEnumerable<(DmlColumn Column, object? Value)> values) =>
        values.Select(v =>
        {
            var column = new SqlColumn(source, v.Column.Column.ColumnName);
            return v.Value is null
                ? (SqlNode)new SqlIsNull(column, Negated: false)
                : new SqlCompare(column, "=", Parameter(v.Column.Property.PropertyType, v.Value));
        });

    /// <summary>
    /// Joins the row of a to-one relation where its owner is read, once however often it is asked for.
    /// </summary>
    /// <param name="scope">The scope.</param>
    /// <param name="owner">The owner's source.</param>
    /// <param name="relation">The relation.</param>
    /// <returns>The related row's source, or null if the scope that reads the owner joins nothing.</returns>
    internal static SqlSource? Join(SqlScope scope, SqlSource owner, DmlRelation relation) => Joined(scope, owner, [relation]);

    /// <summary>
    /// Translates a relation's condition over the owner's row and the related row.
    /// </summary>
    /// <param name="scope">The scope the condition is in.</param>
    /// <param name="condition">The condition, over the related row, or the owner's row and the related row.</param>
    /// <param name="owner">The owner's source.</param>
    /// <param name="other">The related row's source.</param>
    /// <returns>The condition.</returns>
    internal static SqlNode ConditionOver(SqlScope scope, LambdaExpression condition, SqlSource owner, SqlSource other)
    {
        var sources = condition.Parameters.Count == 1
            ? new Dictionary<ParameterExpression, SqlSource> { [condition.Parameters[0]] = other }
            : new Dictionary<ParameterExpression, SqlSource> { [condition.Parameters[0]] = owner, [condition.Parameters[1]] = other };
        return new DmlTranslator(sources, scope, relations: false).Predicate(condition.Body);
    }

    private static Expression StripQuotes(Expression e) =>
        e is UnaryExpression { NodeType: ExpressionType.Quote } quote ? quote.Operand : e;

    private static LambdaExpression Lambda(Expression argument, Expression context) =>
        StripQuotes(argument) as LambdaExpression
        ?? throw new DmTranslationException(
            context,
            "the condition or value must be written in place, as l => l.Qty > 2: a delegate cannot be translated."
        );

    private static SqlNode AllOf(List<SqlNode> parts) =>
        parts.Count switch
        {
            0 => SqlText.True,
            1 => parts[0],
            _ => new SqlLogical("AND", parts),
        };

    private static SqlCompare Equal(SqlSource a, DmlColumn aColumn, SqlSource b, DmlColumn bColumn) =>
        new(new SqlColumn(a, aColumn.Column.ColumnName), "=", new SqlColumn(b, bColumn.Column.ColumnName));

    // o, o.Customer, o.Customer.Region, o.Customer.Orders: a row parameter and the relations followed from it, in
    // order; false if the expression is not one.
    private bool TryChain(Expression e, out ParameterExpression root, out List<DmlRelation> hops)
    {
        var properties = new Stack<PropertyInfo>();
        e = StripConversions(e);
        while (e is MemberExpression { Member: PropertyInfo property, Expression: { } owner })
        {
            properties.Push(property);
            e = StripConversions(owner);
        }

        root = null!;
        hops = [];
        if (e is not ParameterExpression parameter || !_sources.TryGetValue(parameter, out var source))
        {
            return false;
        }

        root = parameter;
        var model = source.Model!;
        foreach (var property in properties)
        {
            if ((hops.Count > 0 && hops[^1].IsCollection) || model.RelationOf(property) is not { } relation)
            {
                return false;
            }

            hops.Add(relation);
            model = relation.Other;
        }

        return true;
    }

    private void RequireRelations(Expression e)
    {
        if (!_relations)
        {
            throw new DmTranslationException(
                e,
                _scope.AllowsRelations
                    ? "a relation's or a join's condition is over the rows' own columns, and does not follow relations."
                    : "relations are followed in queries, updates and deletes; an upsert's expressions are over the "
                        + "rows' own columns."
            );
        }
    }

    // A column of a related row: joined where the root row is read, or read by a subquery in an UPDATE or a DELETE,
    // which joins nothing. Either reads NULL when there is no related row.
    private Operand RelatedColumn(MemberExpression member, ParameterExpression root, List<DmlRelation> hops)
    {
        RequireRelations(member);
        if (hops[^1].IsCollection)
        {
            throw new DmTranslationException(
                member,
                $"{hops[^1].Property.Name} holds many rows: test them with Any or All, or count them, as "
                    + $"o.{hops[^1].Property.Name}!.Any(x => ...)."
            );
        }

        var column = ColumnFor(hops[^1].Other, member.Member, member);
        if (Joined(_scope, _sources[root], hops) is { } joined)
        {
            return new Operand { Node = new SqlColumn(joined, column.Column.ColumnName), Column = column, Nullable = true };
        }

        var (scope, from, rows, where) = Rows(root, hops);
        var select = new SqlSelect
        {
            From = from,
            Joins = scope.Joins,
            Items = [new SqlSelectItem(new SqlColumn(rows, column.Column.ColumnName), null)],
            Where = where,
        };
        return new Operand { Node = new SqlScalar(select), Column = column, Nullable = true };
    }

    // The source of the row a chain of to-one relations leads to, joined where the owner is read; null if that scope
    // joins nothing.
    private static SqlSource? Joined(SqlScope scope, SqlSource owner, IReadOnlyList<DmlRelation> hops)
    {
        SqlSource? current = owner;
        foreach (var hop in hops)
        {
            var from = current;
            current = scope.JoinOnce(from, hop, (joinScope, other) => AllOf(RelationCondition(joinScope, from, hop, other)));
            if (current is null)
            {
                return null;
            }
        }

        return current;
    }

    // What joins a row to its related rows: the key pairs, both sides' fixed values, and the relation's condition.
    private static List<SqlNode> RelationCondition(SqlScope scope, SqlSource owner, DmlRelation relation, SqlSource other)
    {
        List<SqlNode> parts = [.. relation.Keys.Select(k => Equal(other, k.Other, owner, k.This))];
        parts.AddRange(FixedValues(other, relation.OtherValues));
        parts.AddRange(scope.Filters(other));
        parts.AddRange(FixedValues(owner, relation.ThisValues));
        if (relation.Condition is { } condition)
        {
            parts.Add(ConditionOver(scope, condition, owner, other));
        }

        return parts;
    }

    // The rows a chain of relations leads to, as a subquery: to-one relations before the last are joined where the
    // root row is read, if that scope joins, and the rest are the subquery's own FROM and joins.
    private (SqlScope Scope, SqlSource From, SqlSource Rows, List<SqlNode> Where) Rows(ParameterExpression root, List<DmlRelation> hops)
    {
        var owner = _sources[root];
        var i = 0;
        for (; i < hops.Count - 1; i++)
        {
            if (Joined(_scope, owner, [hops[i]]) is not { } joined)
            {
                break;
            }

            owner = joined;
        }

        var scope = _scope.Nested();
        var (from, rows, where) = Open(scope, owner, hops[i]);
        for (i++; i < hops.Count; i++)
        {
            rows = hops[i].IsCollection ? JoinMany(scope, rows, hops[i]) : Joined(scope, rows, [hops[i]])!;
        }

        return (scope, from, rows, where);
    }

    // A relation's rows as a subquery's FROM, correlated to the owner in its WHERE. Through a link model, the link is
    // the FROM and the related rows are joined to it.
    private (SqlSource From, SqlSource Rows, List<SqlNode> Where) Open(SqlScope scope, SqlSource owner, DmlRelation relation)
    {
        if (relation.Link is not { } link)
        {
            var from = scope.Source(relation.Other);
            return (from, from, RelationCondition(scope, owner, relation, from));
        }

        var linkRows = scope.Source(link);
        var rows = scope.Source(relation.Other);
        scope.Join(rows, "INNER JOIN", AllOf([.. relation.Keys.Select(k => Equal(rows, k.Other, linkRows, k.This))]));

        List<SqlNode> where = [.. relation.LinkKeys.Select(k => Equal(linkRows, k.Link, owner, k.This))];
        where.AddRange(FixedValues(rows, relation.OtherValues));
        where.AddRange(scope.Filters(linkRows));
        where.AddRange(scope.Filters(rows));
        where.AddRange(FixedValues(owner, relation.ThisValues));
        if (relation.Condition is { } condition)
        {
            where.Add(ConditionOver(scope, condition, owner, rows));
        }

        return (linkRows, rows, where);
    }

    // A relation's rows joined into a subquery, after a to-one relation the subquery reads.
    private SqlSource JoinMany(SqlScope scope, SqlSource owner, DmlRelation relation)
    {
        if (relation.Link is not { } link)
        {
            var rows = scope.Source(relation.Other);
            scope.Join(rows, "INNER JOIN", AllOf(RelationCondition(scope, owner, relation, rows)));
            return rows;
        }

        var linkRows = scope.Source(link);
        scope.Join(
            linkRows,
            "INNER JOIN",
            AllOf(
                [
                    .. relation.LinkKeys.Select(k => Equal(linkRows, k.Link, owner, k.This)),
                    .. FixedValues(owner, relation.ThisValues),
                    .. scope.Filters(linkRows),
                ]
            )
        );

        var other = scope.Source(relation.Other);
        List<SqlNode> on =
        [
            .. relation.Keys.Select(k => Equal(other, k.Other, linkRows, k.This)),
            .. FixedValues(other, relation.OtherValues),
            .. scope.Filters(other),
        ];
        if (relation.Condition is { } condition)
        {
            on.Add(ConditionOver(scope, condition, owner, other));
        }

        scope.Join(other, "INNER JOIN", AllOf(on));
        return other;
    }

    // o.Lines, or o.Lines.Where(l => ...): the relations to the rows, and the filters on them.
    private (ParameterExpression Root, List<DmlRelation> Hops, List<LambdaExpression> Filters)? CollectionOf(Expression e)
    {
        var filters = new List<LambdaExpression>();
        e = StripConversions(e);
        while (e is MethodCallExpression { Method.Name: nameof(Enumerable.Where), Arguments.Count: 2 } where
            && where.Method.DeclaringType == typeof(Enumerable)
            && StripQuotes(where.Arguments[1]) is LambdaExpression { Parameters.Count: 1 } filter)
        {
            filters.Insert(0, filter);
            e = StripConversions(where.Arguments[0]);
        }

        return TryChain(e, out var root, out var hops) && hops.Count > 0 && hops[^1].IsCollection ? (root, hops, filters) : null;
    }

    // A collection's rows as a subquery, its filters translated over them.
    private (SqlScope Scope, SqlSource From, SqlSource Rows, List<SqlNode> Where) Subquery(
        (ParameterExpression Root, List<DmlRelation> Hops, List<LambdaExpression> Filters) collection
    )
    {
        var (scope, from, rows, where) = Rows(collection.Root, collection.Hops);
        foreach (var filter in collection.Filters)
        {
            where.Add(Over(scope, filter.Parameters[0], rows).Predicate(filter.Body));
        }

        return (scope, from, rows, where);
    }

    // A translator of a subquery's lambda: its parameter is the subquery's rows, and the query's rows stay in reach.
    private DmlTranslator Over(SqlScope scope, ParameterExpression parameter, SqlSource rows) =>
        new(new Dictionary<ParameterExpression, SqlSource>(_sources) { [parameter] = rows }, scope, _relations);

    // o.Lines.Any(), o.Lines.Any(l => ...), o.Lines.All(l => ...).
    private SqlNode? CollectionPredicate(MethodCallExpression call, bool negated)
    {
        if (call.Method.DeclaringType != typeof(Enumerable)
            || (call.Method.Name != nameof(Enumerable.Any) && call.Method.Name != nameof(Enumerable.All))
            || CollectionOf(call.Arguments[0]) is not { } collection)
        {
            return null;
        }

        RequireRelations(call);
        var all = call.Method.Name == nameof(Enumerable.All);
        var predicate = call.Arguments.Count == 2 ? Lambda(call.Arguments[1], call) : null;
        var (scope, from, rows, where) = Subquery(collection);
        if (predicate is not null)
        {
            // All holds when no row fails the predicate, and a row whose column is NULL fails a comparison in C#: the
            // failing rows are the predicate's negation, which keeps them.
            var over = Over(scope, predicate.Parameters[0], rows);
            where.Add(all ? over.Negation(predicate.Body) : over.Predicate(predicate.Body));
        }

        var exists = new SqlExists(new SqlSelect { From = from, Joins = scope.Joins, Where = where });
        return all != negated ? new SqlNot(exists) : exists;
    }

    // o.Lines.Count(), o.Lines!.Count, o.Lines.Count(l => ...), o.Lines.Sum(l => l.Qty), Min, Max, Average.
    private Operand? CollectionValue(Expression e)
    {
        string function;
        (ParameterExpression Root, List<DmlRelation> Hops, List<LambdaExpression> Filters) collection;
        LambdaExpression? selector = null;
        switch (e)
        {
            case MemberExpression { Member.Name: "Count" or "Length", Expression: { } items } when CollectionOf(items) is { } counted:
                function = nameof(Enumerable.Count);
                collection = counted;
                break;

            case MethodCallExpression call
                when call.Method.DeclaringType == typeof(Enumerable)
                    && CollectionValues.Contains(call.Method.Name)
                    && CollectionOf(call.Arguments[0]) is { } aggregated:
                function = call.Method.Name;
                collection = aggregated;
                if (call.Arguments.Count == 2)
                {
                    var lambda = Lambda(call.Arguments[1], call);
                    if (function is nameof(Enumerable.Count) or nameof(Enumerable.LongCount))
                    {
                        collection.Filters.Add(lambda);
                    }
                    else
                    {
                        selector = lambda;
                    }
                }
                else if (function != nameof(Enumerable.Count) && function != nameof(Enumerable.LongCount))
                {
                    throw new DmTranslationException(call, $"{function} of related rows names the value, as o.Lines!.{function}(l => l.Qty).");
                }

                break;

            default:
                return null;
        }

        RequireRelations(e);
        var (scope, from, rows, where) = Subquery(collection);
        SqlNode item;
        bool nullable;
        if (selector is null)
        {
            item = new SqlAggregate("COUNT", null);
            nullable = false;
        }
        else
        {
            var value = Over(scope, selector.Parameters[0], rows).RowOperand(selector.Body);
            if (SqlNode.HasSubquery(value.Node))
            {
                // SQL Server refuses an aggregate of a subquery; the others allow it. Refused everywhere, so that a query
                // does not run on one provider and fail on another.
                throw new DmTranslationException(
                    selector,
                    $"{function} is over a value that reads other related rows, which SQL Server refuses: aggregate a "
                        + "column of the rows, or of a row they relate to."
                );
            }

            var aggregate = function switch
            {
                nameof(Enumerable.Sum) => "SUM",
                nameof(Enumerable.Min) => "MIN",
                nameof(Enumerable.Max) => "MAX",
                _ => "AVG",
            };
            item = new SqlAggregate(aggregate, value.Node, WithFraction: function == nameof(Enumerable.Average) && IsIntegral(StripConversions(selector.Body).Type));

            // C#'s Sum of no rows is 0, and SQL's NULL; Min, Max and Average of no values are NULL in both.
            nullable = function != nameof(Enumerable.Sum);
            if (!nullable)
            {
                item = new SqlFunction("COALESCE", [item, new SqlText("0")]);
            }
        }

        var select = new SqlSelect { From = from, Joins = scope.Joins, Items = [new SqlSelectItem(item, null)], Where = where };
        return Computed(new SqlScalar(select), nullable);
    }

    // o.Customer == null, o.Customer != null: whether there is a related row, as NOT EXISTS or EXISTS.
    private SqlNode? RelationNullTest(ExpressionType op, Expression left, Expression right)
    {
        var (row, other) = IsRelatedRow(left) ? (left, right) : IsRelatedRow(right) ? (right, left) : (null, null);
        if (row is null || other is null)
        {
            return null;
        }

        RequireRelations(row);
        TryChain(row, out var root, out var hops);
        if (hops[^1].IsCollection)
        {
            throw new DmTranslationException(
                row,
                $"{hops[^1].Property.Name} holds many rows, which a query never reads as null: test "
                    + $"o.{hops[^1].Property.Name}!.Any()."
            );
        }

        if ((op != ExpressionType.Equal && op != ExpressionType.NotEqual) || ReferencesRow(other) || Evaluate(other) is not null)
        {
            throw new DmTranslationException(
                row,
                "a related row is compared only with null, as o.Customer == null: compare its columns instead, as "
                    + "o.Customer!.Id == id."
            );
        }

        var (scope, from, _, where) = Rows(root, hops);
        var exists = new SqlExists(new SqlSelect { From = from, Joins = scope.Joins, Where = where });
        return op == ExpressionType.Equal ? new SqlNot(exists) : exists;
    }

    // c == null for a row of a LEFT JOIN: whether a row matched, by a column that is never NULL in a row that did.
    private SqlNode? RowNullTest(ExpressionType op, Expression left, Expression right)
    {
        bool IsRow(Expression e) => StripConversions(e) is ParameterExpression p && _sources.ContainsKey(p);
        var (row, other) = IsRow(left) ? (left, right) : IsRow(right) ? (right, left) : (null, null);
        if (row is null || other is null)
        {
            return null;
        }

        if ((op != ExpressionType.Equal && op != ExpressionType.NotEqual) || ReferencesRow(other) || Evaluate(other) is not null)
        {
            throw new DmTranslationException(row, "a row is compared only with null, as c == null for a row of a LeftJoin.");
        }

        var source = _sources[(ParameterExpression)StripConversions(row)];
        if (!source.Optional)
        {
            return Always(op == ExpressionType.NotEqual);
        }

        var model = source.Model!;
        var column = model.Keys.Count > 0 ? model.Keys[0] : model.Columns.FirstOrDefault(c => !c.Column.IsNullable) ?? model.Columns[0];
        return new SqlIsNull(new SqlColumn(source, column.Column.ColumnName), Negated: op == ExpressionType.NotEqual);
    }

    private bool IsRelatedRow(Expression e) => TryChain(e, out _, out var hops) && hops.Count > 0;

    // WhereExists and WhereNotExists: rows of other models, correlated by the condition.
    private SqlNode? ExistsMarker(MethodCallExpression call, bool negated)
    {
        if (call.Method.DeclaringType != typeof(DmlExists))
        {
            return null;
        }

        RequireRelations(call);
        var lambda = Lambda(call.Arguments[0], call);
        var scope = _scope.Nested();
        var sources = new Dictionary<ParameterExpression, SqlSource>(_sources);
        SqlSource? from = null;
        foreach (var parameter in lambda.Parameters)
        {
            var source = scope.Source(DmlModel.For(parameter.Type));
            if (from is null)
            {
                from = source;
            }
            else
            {
                scope.Join(source, "CROSS JOIN", null);
            }

            sources[parameter] = source;
        }

        var condition = new DmlTranslator(sources, scope, _relations).Predicate(lambda.Body);
        List<SqlNode> where = [.. lambda.Parameters.SelectMany(p => scope.Filters(sources[p])), condition];
        var exists = new SqlExists(new SqlSelect { From = from!, Joins = scope.Joins, Where = where });
        return negated ? new SqlNot(exists) : exists;
    }
}

/// <summary>
/// Methods that stand for <c>EXISTS</c> subqueries in the conditions <c>WhereExists</c> builds; never called.
/// </summary>
internal static class DmlExists
{
    /// <summary>
    /// Stands for whether a row of a model matches a condition.
    /// </summary>
    /// <typeparam name="T">The model.</typeparam>
    /// <param name="predicate">The condition, over the model and the query's rows.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always: the method is translated, not called.</exception>
    public static bool Any<T>(Expression<Func<T, bool>> predicate) =>
        throw new NotSupportedException("DmlExists.Any is translated to SQL, not called.");

    /// <summary>
    /// Stands for whether rows of two models match a condition.
    /// </summary>
    /// <typeparam name="T1">The first model.</typeparam>
    /// <typeparam name="T2">The second model.</typeparam>
    /// <param name="predicate">The condition, over the models and the query's rows.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always: the method is translated, not called.</exception>
    public static bool Any<T1, T2>(Expression<Func<T1, T2, bool>> predicate) =>
        throw new NotSupportedException("DmlExists.Any is translated to SQL, not called.");
}
