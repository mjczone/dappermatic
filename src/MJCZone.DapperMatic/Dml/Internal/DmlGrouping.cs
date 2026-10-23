// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

#pragma warning disable SA1402 // The grouping's parts belong together.
#pragma warning disable SA1649

using System.Linq.Expressions;
using System.Reflection;

namespace MJCZone.DapperMatic.Dml.Internal;

/// <summary>
/// Expressions over groups (<c>g =&gt; new { g.Key, Total = g.Sum(o =&gt; o.Total) }</c>) rewritten over the grouped rows,
/// with each aggregate a <see cref="DmlAggregates.Of{TResult}"/> call the translator writes as SQL.
/// </summary>
internal static class DmlGrouping
{
    private static readonly MethodInfo OfMethod = typeof(DmlAggregates).GetMethod(nameof(DmlAggregates.Of))!;

    private static readonly MethodInfo ValueAtMethod =
        typeof(DmlGrouping).GetMethod(nameof(ValueAt), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    /// Rewrites an expression over a group as one over the grouped rows: <c>g.Key</c> is the key, and
    /// <c>g.Count()</c>, <c>g.Sum(x =&gt; ...)</c> and the like are aggregates of the elements.
    /// </summary>
    /// <param name="overGroup">The expression, over an <see cref="IGrouping{TKey, TElement}"/>.</param>
    /// <param name="key">The grouping key, over the rows.</param>
    /// <param name="element">What each row is to the group, over the same rows; null for the row itself.</param>
    /// <returns>The expression, over the key's parameters.</returns>
    /// <exception cref="DmTranslationException">The expression reads the group in a way that is not translated.</exception>
    public static LambdaExpression Rewrite(LambdaExpression overGroup, LambdaExpression key, LambdaExpression? element)
    {
        var elementBody = element is null ? key.Parameters[0] : Rebound(element, key.Parameters);
        var body = new GroupRewriter(overGroup.Parameters[0], key.Body, elementBody).Visit(overGroup.Body)!;
        return Expression.Lambda(body, key.Parameters);
    }

    /// <summary>
    /// Lists the values a key groups by: its members for <c>new { o.A, o.B }</c>, or the key itself.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The values, each an expression of the key's body.</returns>
    public static List<Expression> KeyValues(LambdaExpression key)
    {
        var values = new List<Expression>();
        void Add(Expression e)
        {
            switch (e)
            {
                case NewExpression create when create.Arguments.Count > 0:
                    create.Arguments.ToList().ForEach(Add);
                    break;
                case MemberInitExpression init:
                    init.NewExpression.Arguments.ToList().ForEach(Add);
                    init.Bindings.OfType<MemberAssignment>().Select(b => b.Expression).ToList().ForEach(Add);
                    break;
                default:
                    values.Add(e);
                    break;
            }
        }

        Add(key.Body);
        return values;
    }

    /// <summary>
    /// Tells whether an expression reads any of the rows.
    /// </summary>
    /// <param name="e">The expression.</param>
    /// <param name="rows">The rows' parameters.</param>
    /// <returns>True if it does; false for a value that is the same for every row.</returns>
    public static bool ReadsRows(Expression e, IReadOnlyList<ParameterExpression> rows)
    {
        var finder = new RowReader([.. rows]);
        finder.Visit(e);
        return finder.Found;
    }

    /// <summary>
    /// Finds the parts of an expression over the grouped rows the database computes: its aggregates, and the values of
    /// the rows outside them, which are the key's.
    /// </summary>
    /// <param name="rewritten">The expression, from <see cref="Rewrite"/>.</param>
    /// <returns>The expression reading each part from an array of values, as <c>values =&gt; ...</c>, and the parts.</returns>
    public static (LambdaExpression Reads, List<Expression> Parts) Parts(LambdaExpression rewritten)
    {
        var values = Expression.Parameter(typeof(object?[]), "values");
        var finder = new PartFinder([.. rewritten.Parameters], values);
        var body = finder.Visit(rewritten.Body)!;
        return (Expression.Lambda(body, values), finder.Parts);
    }

    /// <summary>
    /// Tells whether an expression is an aggregate the rewrite made.
    /// </summary>
    /// <param name="e">The expression.</param>
    /// <returns>True if it is.</returns>
    public static bool IsAggregate(Expression e) => e is MethodCallExpression { Method.IsGenericMethod: true } call && call.Method.GetGenericMethodDefinition() == OfMethod;

    private static object? ValueAt(object?[] values, int index, Type type) => DmlValues.Convert(values[index], type);

    private static Expression Rebound(LambdaExpression lambda, IReadOnlyList<ParameterExpression> parameters)
    {
        if (lambda.Parameters.Count != parameters.Count)
        {
            throw new ArgumentException($"{lambda} has {lambda.Parameters.Count} parameters, not {parameters.Count}.", nameof(lambda));
        }

        var map = lambda.Parameters.Zip(parameters).ToDictionary(p => p.First, p => (Expression)p.Second);
        return new Replacer(map).Visit(lambda.Body)!;
    }

    private static Expression Unconverted(Expression e)
    {
        while (e is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } convert)
        {
            e = convert.Operand;
        }

        return e;
    }

    // new { A = x, B = y }.A is x; a tuple's Item1 is its first value; an initializer's member is its assignment.
    private static Expression? Reduced(Expression owner, MemberInfo member)
    {
        switch (owner)
        {
            case NewExpression { Members: { } members } create:
                var index = members.ToList().FindIndex(m => m.Name == member.Name);
                return index >= 0 ? create.Arguments[index] : null;
            case NewExpression create when create.Type.FullName?.StartsWith("System.ValueTuple`", StringComparison.Ordinal) == true
                && member.Name.StartsWith("Item", StringComparison.Ordinal)
                && int.TryParse(member.Name.AsSpan(4), out var item)
                && item <= create.Arguments.Count:
                return create.Arguments[item - 1];
            case MemberInitExpression init:
                return init.Bindings.OfType<MemberAssignment>().FirstOrDefault(b => b.Member.Name == member.Name)?.Expression;
            default:
                return null;
        }
    }

    private sealed class Replacer(Dictionary<ParameterExpression, Expression> map) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => map.TryGetValue(node, out var e) ? e : node;

        protected override Expression VisitMember(MemberExpression node)
        {
            var owner = node.Expression is null ? null : Visit(node.Expression);
            return owner is not null && Reduced(owner, node.Member) is { } reduced ? reduced : node.Update(owner);
        }
    }

    private sealed class GroupRewriter(ParameterExpression group, Expression key, Expression element) : ExpressionVisitor
    {
        private const string Read =
            "a group is read through Key, Count, LongCount, Sum, Min, Max, Average, Any and All, and Where before them";

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression is { } owner && Unconverted(owner) == group)
            {
                return node.Member.Name == nameof(IGrouping<object, object>.Key)
                    ? key
                    : throw new DmTranslationException(node, Read);
            }

            var visited = node.Expression is null ? null : Visit(node.Expression);
            return visited is not null && Reduced(visited, node.Member) is { } reduced ? reduced : node.Update(visited);
        }

        protected override Expression VisitParameter(ParameterExpression node) =>
            node == group ? throw new DmTranslationException(node, Read) : node;

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType != typeof(Enumerable) || node.Arguments.Count == 0)
            {
                return base.VisitMethodCall(node);
            }

            // g, or g.Where(x => ...) any number of times.
            var filters = new List<Expression>();
            var source = Unconverted(node.Arguments[0]);
            while (source is MethodCallExpression { Method.Name: nameof(Enumerable.Where), Arguments.Count: 2 } where
                && where.Method.DeclaringType == typeof(Enumerable)
                && Lambda(where.Arguments[1]) is { Parameters.Count: 1 } filter)
            {
                filters.Insert(0, Inlined(filter));
                source = Unconverted(where.Arguments[0]);
            }

            if (source != group)
            {
                return base.VisitMethodCall(node);
            }

            var argument = node.Arguments.Count == 2 ? Lambda(node.Arguments[1]) : null;
            switch (node.Method.Name)
            {
                case nameof(Enumerable.Count) or nameof(Enumerable.LongCount):
                    if (argument is not null)
                    {
                        filters.Add(Inlined(argument));
                    }

                    return Of(node.Type, "COUNT", null, filters);

                case nameof(Enumerable.Sum) or nameof(Enumerable.Min) or nameof(Enumerable.Max) or nameof(Enumerable.Average):
                    var function = node.Method.Name switch
                    {
                        nameof(Enumerable.Sum) => "SUM",
                        nameof(Enumerable.Min) => "MIN",
                        nameof(Enumerable.Max) => "MAX",
                        _ => "AVG",
                    };
                    return Of(node.Type, function, argument is null ? element : Inlined(argument), filters);

                case nameof(Enumerable.Any):
                    if (argument is not null)
                    {
                        filters.Add(Inlined(argument));
                    }

                    return Expression.GreaterThan(Of(typeof(int), "COUNT", null, filters), Expression.Constant(0));

                case nameof(Enumerable.All) when argument is not null:
                    filters.Add(Expression.Not(Inlined(argument)));
                    return Expression.Equal(Of(typeof(int), "COUNT", null, filters), Expression.Constant(0));

                default:
                    throw new DmTranslationException(node, Read);
            }
        }

        private static LambdaExpression? Lambda(Expression e) =>
            (e is UnaryExpression { NodeType: ExpressionType.Quote } quote ? quote.Operand : e) as LambdaExpression;

        private static Expression Of(Type type, string function, Expression? value, List<Expression> filters) =>
            Expression.Call(
                OfMethod.MakeGenericMethod(type),
                Expression.Constant(function),
                value is null ? Expression.Constant(null, typeof(object)) : Expression.Convert(value, typeof(object)),
                filters.Count == 0 ? Expression.Constant(true) : filters.Aggregate(Expression.AndAlso)
            );

        // x => x.Total * 2 over the element: the element's expression in place of x.
        private Expression Inlined(LambdaExpression lambda) =>
            new Replacer(new Dictionary<ParameterExpression, Expression> { [lambda.Parameters[0]] = element }).Visit(lambda.Body)!;
    }

    // The aggregates, and the rows' values outside them, each read from the array of values.
    private sealed class PartFinder(HashSet<ParameterExpression> rows, ParameterExpression values) : ExpressionVisitor
    {
        public List<Expression> Parts { get; } = [];

        public override Expression? Visit(Expression? node)
        {
            if (node is null)
            {
                return null;
            }

            var constructs = node is NewExpression || node is MemberInitExpression || node is NewArrayExpression || node is ListInitExpression;
            var isPart = IsAggregate(node) || (!constructs && References(node, rows) && !References(node, null));
            if (!isPart)
            {
                return base.Visit(node);
            }

            var read = Expression.Call(ValueAtMethod, values, Expression.Constant(Parts.Count), Expression.Constant(node.Type, typeof(Type)));
            Parts.Add(node);
            return Expression.Convert(read, node.Type);
        }

        // Whether an expression reads one of the rows (rows), or holds an aggregate (null).
        private static bool References(Expression e, HashSet<ParameterExpression>? rows)
        {
            var finder = new Finder(rows);
            finder.Visit(e);
            return finder.Found;
        }

        private sealed class Finder(HashSet<ParameterExpression>? rows) : ExpressionVisitor
        {
            public bool Found { get; private set; }

            public override Expression? Visit(Expression? node)
            {
                if (Found || node is null)
                {
                    return node;
                }

                if (rows is null ? IsAggregate(node) : node is ParameterExpression p && rows.Contains(p))
                {
                    Found = true;
                    return node;
                }

                return base.Visit(node);
            }
        }
    }
}

/// <summary>
/// Finds whether an expression reads one of a set of parameters.
/// </summary>
/// <param name="rows">The parameters.</param>
internal sealed class RowReader(HashSet<ParameterExpression> rows) : ExpressionVisitor
{
    /// <summary>Gets a value indicating whether one was read.</summary>
    public bool Found { get; private set; }

    /// <inheritdoc />
    public override Expression? Visit(Expression? node)
    {
        if (Found || node is null)
        {
            return node;
        }

        if (node is ParameterExpression p && rows.Contains(p))
        {
            Found = true;
            return node;
        }

        return base.Visit(node);
    }
}

/// <summary>
/// Stands for an aggregate of a group's rows in a rewritten expression; never called.
/// </summary>
internal static class DmlAggregates
{
    /// <summary>
    /// Stands for an aggregate.
    /// </summary>
    /// <typeparam name="TResult">The aggregate's type.</typeparam>
    /// <param name="function"><c>COUNT</c>, <c>SUM</c>, <c>MIN</c>, <c>MAX</c> or <c>AVG</c>.</param>
    /// <param name="value">The value aggregated, or null for <c>COUNT</c> of rows.</param>
    /// <param name="filter">Which rows count.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always: the method is translated, not called.</exception>
    public static TResult Of<TResult>(string function, object? value, bool filter) =>
        throw new NotSupportedException("DmlAggregates.Of is translated to SQL, not called.");
}

/// <summary>
/// The rows a grouping groups: a query's, or a join's, with the sources its key's parameters read.
/// </summary>
internal sealed class DmlRows
{
    /// <summary>Gets the scope the rows are read in.</summary>
    public required DmScope Scope { get; init; }

    /// <summary>
    /// Gets a function building the rows' <c>SELECT</c> (sources, joins and conditions, no select list), its scope, and
    /// the source of each of the key's parameters, in order.
    /// </summary>
    public required Func<(SqlSelect Select, SqlScope Scope, IReadOnlyList<SqlSource> Sources)> Open { get; init; }
}
