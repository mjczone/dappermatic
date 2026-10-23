// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Linq.Expressions;
using System.Reflection;

namespace MJCZone.DapperMatic.Dml.Internal;

/// <summary>
/// What a projection such as <c>o =&gt; new { o.Id, o.Total }</c> reads from a row.
/// </summary>
internal static class DmlProjection
{
    private static readonly MethodInfo ValueAtMethod = typeof(DmlProjection).GetMethod(nameof(ValueAt), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly HashSet<string> CollectionMethods =
    [
        nameof(Enumerable.Any),
        nameof(Enumerable.All),
        nameof(Enumerable.Count),
        nameof(Enumerable.LongCount),
        nameof(Enumerable.Sum),
        nameof(Enumerable.Min),
        nameof(Enumerable.Max),
        nameof(Enumerable.Average),
    ];

    /// <summary>
    /// Finds the parts of a selector the database computes, which a row read into the model does not hold: a related
    /// row's column (<c>o.Customer!.Name</c>), tests of related rows (<c>o.Lines!.Any()</c>, <c>o.Customer == null</c>),
    /// and values over them (<c>o.Lines!.Count()</c>, <c>.Sum(l =&gt; l.Qty)</c>).
    /// </summary>
    /// <param name="selector">The selector, over the model.</param>
    /// <param name="model">The model.</param>
    /// <returns>
    /// The selector with each part read from an array of values instead, as <c>(row, values) =&gt; ...</c>, and the
    /// parts, in the array's order; no parts if the selector has none.
    /// </returns>
    public static (LambdaExpression Selector, List<Expression> Parts) DatabaseParts(LambdaExpression selector, DmlModel model)
    {
        var row = selector.Parameters[0];
        var values = Expression.Parameter(typeof(object?[]), "values");
        var replacer = new PartReplacer([(row, model)], values, columns: false);
        var body = replacer.Visit(selector.Body)!;
        return (Expression.Lambda(body, row, values), replacer.Parts);
    }

    /// <summary>
    /// Finds the parts of a selector over joined rows the database computes: every column of the rows, and what
    /// <see cref="DatabaseParts"/> finds; a row the selector uses whole is read whole.
    /// </summary>
    /// <param name="selector">The selector, a parameter per joined model.</param>
    /// <param name="models">The joined models, in order.</param>
    /// <returns>
    /// The selector reading each part from an array of values and each row it uses whole from an array of rows, as
    /// <c>(values, rows) =&gt; ...</c>; the parts; and which rows it uses whole.
    /// </returns>
    public static (LambdaExpression Reads, List<Expression> Parts, bool[] Whole) JoinParts(LambdaExpression selector, IReadOnlyList<DmlModel> models)
    {
        var values = Expression.Parameter(typeof(object?[]), "values");
        var rows = Expression.Parameter(typeof(object?[]), "rows");
        var replacer = new PartReplacer([.. selector.Parameters.Zip(models)], values, columns: true);
        var body = replacer.Visit(selector.Body)!;

        var whole = selector.Parameters.Select(p => References(body, p)).ToArray();
        var map = selector.Parameters
            .Select((p, i) => (p, (Expression)Expression.Convert(Expression.ArrayIndex(rows, Expression.Constant(i)), p.Type)))
            .ToDictionary(x => x.p, x => x.Item2);
        return (Expression.Lambda(new ParameterReplacer(map).Visit(body)!, values, rows), replacer.Parts, whole);
    }

    /// <summary>
    /// Tells whether a part found by <see cref="DatabaseParts"/> is a test, which the database writes as a condition.
    /// </summary>
    /// <param name="part">The part.</param>
    /// <returns>True for <c>Any</c>, <c>All</c> and a comparison of a related row with null.</returns>
    public static bool IsTest(Expression part) =>
        part is BinaryExpression
        || part is MethodCallExpression { Method.Name: nameof(Enumerable.Any) or nameof(Enumerable.All) };

    /// <summary>
    /// Tells whether an expression reads a part found by <see cref="DatabaseParts"/>.
    /// </summary>
    /// <param name="e">The expression.</param>
    /// <returns>True if it does.</returns>
    public static bool IsPart(Expression e) => Unconverted(e) is MethodCallExpression { Method: var m } && m == ValueAtMethod;

    /// <summary>
    /// Finds the columns a selector uses.
    /// </summary>
    /// <param name="selector">The selector.</param>
    /// <param name="model">The model.</param>
    /// <returns>
    /// The columns, in the order the selector uses them; or null if it uses the row itself or a property that is not a
    /// column, such as one computed from others, so that every column has to be read.
    /// </returns>
    public static IReadOnlyList<DmlColumn>? Columns(LambdaExpression selector, DmlModel model)
    {
        var finder = new ColumnFinder(selector.Parameters[0], model);
        finder.Visit(selector.Body);
        return finder.WholeRow ? null : finder.Found;
    }

    /// <summary>
    /// Tells whether a selector is made only of columns, such as <c>o =&gt; o.CustomerId</c> or
    /// <c>o =&gt; new { o.CustomerId, o.Status }</c>, so that the database can compare what it returns.
    /// </summary>
    /// <param name="selector">The selector.</param>
    /// <returns>True if it is.</returns>
    public static bool IsColumnsOnly(LambdaExpression selector)
    {
        var row = selector.Parameters[0];
        bool IsColumn(Expression e) =>
            (Unconverted(e) is MemberExpression { Expression: { } owner } && Unconverted(owner) == row) || IsPart(e);

        return selector.Body switch
        {
            NewExpression create => create.Arguments.All(IsColumn),
            MemberInitExpression init => init.NewExpression.Arguments.All(IsColumn)
                && init.Bindings.All(b => b is MemberAssignment assignment && IsColumn(assignment.Expression)),
            var body => IsColumn(body),
        };
    }

    private static object? ValueAt(object?[] values, int index, Type type) => DmlValues.Convert(values[index], type);

    private static bool References(Expression e, ParameterExpression parameter)
    {
        var finder = new ParameterFinder(parameter);
        finder.Visit(e);
        return finder.Found;
    }

    // o.Customer.Region, o.Lines: the relations followed from a row, in order; null if the expression is not that.
    private static List<DmlRelation>? Chain(Expression e, IReadOnlyList<(ParameterExpression Row, DmlModel Model)> rows)
    {
        var properties = new Stack<PropertyInfo>();
        e = Unconverted(e);
        while (e is MemberExpression { Member: PropertyInfo property, Expression: { } owner })
        {
            properties.Push(property);
            e = Unconverted(owner);
        }

        var root = rows.FirstOrDefault(r => r.Row == e);
        return root.Row is null ? null : Hops(properties, root.Model);
    }

    private static List<DmlRelation>? Hops(Stack<PropertyInfo> properties, DmlModel model)
    {
        var hops = new List<DmlRelation>();
        foreach (var property in properties)
        {
            if ((hops.Count > 0 && hops[^1].IsCollection) || model.RelationOf(property) is not { } relation)
            {
                return null;
            }

            hops.Add(relation);
            model = relation.Other;
        }

        return hops;
    }

    private static Expression Unconverted(Expression e)
    {
        while (e is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } convert)
        {
            e = convert.Operand;
        }

        return e;
    }

    private sealed class ParameterFinder(ParameterExpression parameter) : ExpressionVisitor
    {
        public bool Found { get; private set; }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            Found |= node == parameter;
            return node;
        }
    }

    private sealed class ParameterReplacer(Dictionary<ParameterExpression, Expression> map) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => map.TryGetValue(node, out var e) ? e : node;
    }

    // Replaces each part the database computes with a read of its value: with columns, also each row's own columns.
    private sealed class PartReplacer(
        IReadOnlyList<(ParameterExpression Row, DmlModel Model)> rows,
        ParameterExpression values,
        bool columns
    ) : ExpressionVisitor
    {
        public List<Expression> Parts { get; } = [];

        public override Expression? Visit(Expression? node)
        {
            if (node is null || !IsDatabasePart(node))
            {
                return base.Visit(node);
            }

            var read = Expression.Call(ValueAtMethod, values, Expression.Constant(Parts.Count), Expression.Constant(node.Type, typeof(Type)));
            Parts.Add(node);
            return Expression.Convert(read, node.Type);
        }

        private bool IsDatabasePart(Expression node)
        {
            switch (node)
            {
                // (int?)o.Customer!.Id: read as the nullable, NULL where there is no related row.
                case UnaryExpression { NodeType: ExpressionType.Convert } convert when Nullable.GetUnderlyingType(convert.Type) is not null:
                    return IsDatabasePart(convert.Operand);

                // A row's own column, for a join, whose rows are not read into objects.
                case MemberExpression { Expression: ParameterExpression owner } member when columns && rows.Any(r => r.Row == owner):
                    return rows.First(r => r.Row == owner).Model.ColumnsByProperty.ContainsKey(member.Member.Name);

                // A column of a related row.
                case MemberExpression { Expression: { } owner } member
                    when Chain(owner, rows) is { } hops && hops.Count > 0 && !hops[^1].IsCollection:
                    return hops[^1].Other.ColumnsByProperty.ContainsKey(member.Member.Name);

                // The number of related rows.
                case MemberExpression { Member.Name: "Count" or "Length" } count when count.Expression is not null:
                    return IsCollection(count.Expression);

                // Tests of and values over related rows.
                case MethodCallExpression call when call.Method.DeclaringType == typeof(Enumerable) && CollectionMethods.Contains(call.Method.Name):
                    return IsCollection(call.Arguments[0]);

                // Whether there is a related row.
                case BinaryExpression { NodeType: ExpressionType.Equal or ExpressionType.NotEqual } compare:
                    return (IsRelatedRow(compare.Left) && IsNull(compare.Right)) || (IsRelatedRow(compare.Right) && IsNull(compare.Left));

                default:
                    return false;
            }
        }

        private bool IsCollection(Expression e)
        {
            e = Unconverted(e);
            while (e is MethodCallExpression { Method.Name: nameof(Enumerable.Where), Arguments.Count: 2 } where
                && where.Method.DeclaringType == typeof(Enumerable))
            {
                e = Unconverted(where.Arguments[0]);
            }

            return Chain(e, rows) is { } hops && hops.Count > 0 && hops[^1].IsCollection;
        }

        private bool IsRelatedRow(Expression e) => Chain(e, rows) is { } hops && hops.Count > 0 && !hops[^1].IsCollection;

        private static bool IsNull(Expression e) => Unconverted(e) is ConstantExpression { Value: null };
    }

    private sealed class ColumnFinder(ParameterExpression row, DmlModel model) : ExpressionVisitor
    {
        public List<DmlColumn> Found { get; } = [];

        public bool WholeRow { get; private set; }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression is not null && Unconverted(node.Expression) == row)
            {
                if (model.ColumnsByProperty.TryGetValue(node.Member.Name, out var column))
                {
                    if (!Found.Contains(column))
                    {
                        Found.Add(column);
                    }
                }
                else
                {
                    WholeRow = true;
                }

                return node;
            }

            return base.VisitMember(node);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            WholeRow |= node == row;
            return node;
        }
    }
}
