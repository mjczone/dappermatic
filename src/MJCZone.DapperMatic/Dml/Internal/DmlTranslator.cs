// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace MJCZone.DapperMatic.Dml.Internal;

/// <summary>
/// Translates C# expressions over a model into query tree nodes, which <see cref="SqlRenderer"/> writes as SQL with
/// every value a parameter.
/// </summary>
/// <remarks>
/// Comparisons keep C#'s meaning where a column can be NULL: <c>o.Note != "x"</c> is true for a row whose note is
/// NULL, as it is in C#, so it becomes <c>(note &lt;&gt; @p OR note IS NULL)</c>. Negations are pushed down to the
/// comparisons they apply to, so <c>!(o.Total &gt; 5)</c> also keeps the rows with no total.
/// </remarks>
internal sealed partial class DmlTranslator
{
    private const string Supported =
        "the DML methods translate comparisons, &&, ||, !, == null, bool properties, HasValue, list.Contains(o.X), "
        + "string Contains/StartsWith/EndsWith/Equals with a value (optionally ignoring case), string.IsNullOrEmpty, "
        + "arithmetic, ??, ?:, string + and string.Concat, ToUpper/ToLower/Trim/TrimStart/TrimEnd/Substring/Replace/"
        + "Length, and the Year/Month/Day/Hour/Minute/Second of a DateTime, DateOnly or TimeOnly. Compute other values "
        + "into a variable first, or query with Dapper directly.";

    // Escape characters for LIKE, tried in order for one the value does not contain. MySQL 5.7 with a UCA collation
    // such as utf8mb4_unicode_ci fails to match an escaped escape character after a %: '%~~%' ESCAPE '~' does not
    // match 'a~b'. An escape character absent from the value never has to escape itself.
    private static readonly char[] LikeEscapes = ['~', '!', '#', '^', '|', '$'];

    // Each lambda parameter is a row of one source: one for a query, (current, incoming) for an upsert, and the rows
    // of a subquery's lambda (o.Lines.Any(l => ...)) beside those of the query it is in.
    private readonly IReadOnlyDictionary<ParameterExpression, SqlSource> _sources;
    private readonly SqlScope _scope;
    private readonly DmlDialect _dialect;
    private readonly bool _relations;
    private readonly bool _aggregates;

    // A join's condition, and a relation's, is over rows that exist: a LEFT JOIN's row is missing only after it.
    private readonly bool _rowsExist;

    // Nodes already translated, by expression: a group's key, written once, so that its parameters are the same
    // wherever it is used.
    private readonly IReadOnlyDictionary<Expression, SqlNode>? _known;

    private DmlTranslator(SqlSource source, SqlScope scope, ParameterExpression row)
        : this(new Dictionary<ParameterExpression, SqlSource> { [row] = source }, scope) { }

    private DmlTranslator(
        IReadOnlyDictionary<ParameterExpression, SqlSource> sources,
        SqlScope scope,
        bool relations = true,
        bool aggregates = false,
        IReadOnlyDictionary<Expression, SqlNode>? known = null
    )
    {
        if (sources.Values.Any(s => s.Model is null))
        {
            throw new ArgumentException("C# expressions are translated over models.", nameof(sources));
        }

        _sources = sources;
        _scope = scope;
        _dialect = scope.Dialect;
        _relations = relations && scope.AllowsRelations;
        _rowsExist = !relations;
        _aggregates = aggregates;
        _known = known;
    }

    /// <summary>
    /// Translates a predicate into a condition, following relations as the scope allows: joined in a query, read by
    /// subqueries in an update or a delete.
    /// </summary>
    /// <param name="predicate">The predicate, over the model.</param>
    /// <param name="source">The source the model's rows are read from.</param>
    /// <param name="scope">The scope of the statement's <c>SELECT</c>, or of the statement that writes rows.</param>
    /// <returns>The condition.</returns>
    public static SqlNode Condition(LambdaExpression predicate, SqlSource source, SqlScope scope) =>
        new DmlTranslator(source, scope, predicate.Parameters[0]).Predicate(predicate.Body);

    /// <summary>
    /// Translates a predicate into a condition over the model's own columns.
    /// </summary>
    /// <param name="predicate">The predicate, over the model.</param>
    /// <param name="source">The source the model's rows are read from.</param>
    /// <param name="dialect">The dialect, for the values whose escaping differs by provider.</param>
    /// <returns>The condition.</returns>
    public static SqlNode Condition(LambdaExpression predicate, SqlSource source, DmlDialect dialect) =>
        Condition(predicate, source, SqlScope.Plain(dialect));

    /// <summary>
    /// Translates a predicate over several rows, such as an upsert's <c>(current, incoming)</c>, into a condition.
    /// </summary>
    /// <param name="predicate">The predicate.</param>
    /// <param name="sources">The source of each of its parameters, in order.</param>
    /// <param name="dialect">The dialect.</param>
    /// <returns>The condition.</returns>
    public static SqlNode Condition(LambdaExpression predicate, IReadOnlyList<SqlSource> sources, DmlDialect dialect) =>
        new DmlTranslator(Bind(predicate, sources), SqlScope.Plain(dialect)).Predicate(predicate.Body);

    /// <summary>
    /// Translates a predicate over the rows of several sources, such as a join's, into a condition.
    /// </summary>
    /// <param name="predicate">The predicate, a parameter per source.</param>
    /// <param name="sources">The source of each of its parameters, in order.</param>
    /// <param name="scope">The scope of the statement's <c>SELECT</c>.</param>
    /// <param name="aggregates">Whether the predicate is over groups, and may hold aggregates.</param>
    /// <param name="known">Nodes already translated, by expression, to reuse.</param>
    /// <param name="relations">Whether the predicate may follow relations.</param>
    /// <returns>The condition.</returns>
    public static SqlNode Condition(
        LambdaExpression predicate,
        IReadOnlyList<SqlSource> sources,
        SqlScope scope,
        bool aggregates = false,
        IReadOnlyDictionary<Expression, SqlNode>? known = null,
        bool relations = true
    ) => new DmlTranslator(Bind(predicate, sources), scope, relations, aggregates, known).Predicate(predicate.Body);

    /// <summary>
    /// Translates a value over the rows of several sources, such as a join's.
    /// </summary>
    /// <param name="value">The value, a parameter per source.</param>
    /// <param name="sources">The source of each of its parameters, in order.</param>
    /// <param name="scope">The scope of the statement's <c>SELECT</c>.</param>
    /// <param name="aggregates">Whether the value is over groups, and may hold aggregates.</param>
    /// <param name="known">Nodes already translated, by expression, to reuse.</param>
    /// <returns>The node, and whether it can be NULL.</returns>
    public static (SqlNode Node, bool Nullable) Value(
        LambdaExpression value,
        IReadOnlyList<SqlSource> sources,
        SqlScope scope,
        bool aggregates = false,
        IReadOnlyDictionary<Expression, SqlNode>? known = null
    )
    {
        var operand = new DmlTranslator(Bind(value, sources), scope, aggregates: aggregates, known: known).SqlOf(value.Body);
        return (operand.Node!, operand.Nullable);
    }

    /// <summary>
    /// Translates an object initializer over several rows, such as an upsert's
    /// <c>(current, incoming) =&gt; new T { Views = current.Views + incoming.Views }</c>, into assignments.
    /// </summary>
    /// <param name="set">The initializer.</param>
    /// <param name="target">The source the assignments write.</param>
    /// <param name="sources">The source of each of the initializer's parameters, in order.</param>
    /// <param name="dialect">The dialect.</param>
    /// <returns>The columns and their new values; empty if the initializer sets nothing.</returns>
    public static List<(SqlColumn Column, SqlNode Value)> Assignments(
        LambdaExpression set,
        SqlSource target,
        IReadOnlyList<SqlSource> sources,
        DmlDialect dialect
    ) => new DmlTranslator(Bind(set, sources), SqlScope.Plain(dialect)).AssignmentsOf(set.Body, target);

    /// <summary>
    /// Translates an object initializer such as <c>o =&gt; new Order { Status = s }</c> into the assignments of an
    /// <c>UPDATE</c>.
    /// </summary>
    /// <param name="set">The initializer, over the model.</param>
    /// <param name="source">The source the model's rows are written to.</param>
    /// <param name="scope">The scope of the statement.</param>
    /// <returns>The columns and their new values; empty if the initializer sets nothing.</returns>
    public static List<(SqlColumn Column, SqlNode Value)> Assignments(LambdaExpression set, SqlSource source, SqlScope scope) =>
        new DmlTranslator(source, scope, set.Parameters[0]).AssignmentsOf(set.Body, source);

    /// <summary>
    /// Translates an object initializer into the assignments of an <c>UPDATE</c>, over the model's own columns.
    /// </summary>
    /// <param name="set">The initializer, over the model.</param>
    /// <param name="source">The source the model's rows are written to.</param>
    /// <param name="dialect">The dialect.</param>
    /// <returns>The columns and their new values; empty if the initializer sets nothing.</returns>
    public static List<(SqlColumn Column, SqlNode Value)> Assignments(LambdaExpression set, SqlSource source, DmlDialect dialect) =>
        Assignments(set, source, SqlScope.Plain(dialect));

    private static Dictionary<ParameterExpression, SqlSource> Bind(LambdaExpression lambda, IReadOnlyList<SqlSource> sources) =>
        lambda.Parameters.Count == sources.Count
            ? lambda.Parameters.Zip(sources).ToDictionary(p => p.First, p => p.Second)
            : throw new ArgumentException($"The expression has {lambda.Parameters.Count} parameters, not {sources.Count}.", nameof(lambda));

    private List<(SqlColumn Column, SqlNode Value)> AssignmentsOf(Expression body, SqlSource target)
    {
        var translator = this;
        if (body is NewExpression { Arguments.Count: 0 })
        {
            return [];
        }

        if (body is not MemberInitExpression { NewExpression.Arguments.Count: 0 } init)
        {
            throw new DmTranslationException(
                body,
                "set the columns in an object initializer with no constructor arguments, such as "
                    + "o => new Order { Status = status, Note = null }."
            );
        }

        var assignments = new List<(SqlColumn Column, SqlNode Value)>();
        foreach (var binding in init.Bindings)
        {
            if (binding is not MemberAssignment assignment)
            {
                throw new DmTranslationException(body, $"{binding} is not an assignment.");
            }

            var column = ColumnFor(target.Model!, assignment.Member, assignment.Expression);
            SqlNode value = translator.ReferencesRow(assignment.Expression)
                ? translator.RowOperand(assignment.Expression).Node!
                : Parameter(column.Property.PropertyType, Evaluate(assignment.Expression));
            assignments.Add((new SqlColumn(target, column.Column.ColumnName), value));
        }

        return assignments;
    }

    private static ExpressionType Inverse(ExpressionType op) =>
        op switch
        {
            ExpressionType.Equal => ExpressionType.NotEqual,
            ExpressionType.NotEqual => ExpressionType.Equal,
            ExpressionType.LessThan => ExpressionType.GreaterThanOrEqual,
            ExpressionType.LessThanOrEqual => ExpressionType.GreaterThan,
            ExpressionType.GreaterThan => ExpressionType.LessThanOrEqual,
            ExpressionType.GreaterThanOrEqual => ExpressionType.LessThan,
            _ => op,
        };

    private static ExpressionType Mirror(ExpressionType op) =>
        op switch
        {
            ExpressionType.LessThan => ExpressionType.GreaterThan,
            ExpressionType.LessThanOrEqual => ExpressionType.GreaterThanOrEqual,
            ExpressionType.GreaterThan => ExpressionType.LessThan,
            ExpressionType.GreaterThanOrEqual => ExpressionType.LessThanOrEqual,
            _ => op,
        };

    private static string Operator(ExpressionType op) =>
        op switch
        {
            ExpressionType.Equal => "=",
            ExpressionType.NotEqual => "<>",
            ExpressionType.LessThan => "<",
            ExpressionType.LessThanOrEqual => "<=",
            ExpressionType.GreaterThan => ">",
            _ => ">=",
        };

    private static bool IsComparison(ExpressionType op) =>
        op
            is ExpressionType.Equal
                or ExpressionType.NotEqual
                or ExpressionType.LessThan
                or ExpressionType.LessThanOrEqual
                or ExpressionType.GreaterThan
                or ExpressionType.GreaterThanOrEqual;

    private static bool IsOrdered(ExpressionType op) => op != ExpressionType.Equal && op != ExpressionType.NotEqual;

    // (int)o.Price drops the fraction in C#; stripped like the conversions that keep the value, the column would be
    // compared with its fraction.
    private static void RequireValueKept(Expression e)
    {
        for (var part = e; part is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } convert; part = convert.Operand)
        {
            var from = Nullable.GetUnderlyingType(convert.Operand.Type) ?? convert.Operand.Type;
            if (IsIntegral(convert.Type) && (from == typeof(decimal) || from == typeof(double) || from == typeof(float)))
            {
                throw new DmTranslationException(
                    e,
                    "a cast to a whole number drops the fraction, which SQL would keep: compare the value as it is."
                );
            }
        }
    }

    private static Expression StripConversions(Expression e)
    {
        while (e is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } convert)
        {
            e = convert.Operand;
        }

        return e;
    }

    private static SqlNode Always(bool value) => SqlText.Always(value);

    private static SqlLogical Or(params SqlNode[] operands) => new("OR", operands);

    private static SqlLogical And(params SqlNode[] operands) => new("AND", operands);

    private SqlNode Predicate(Expression e)
    {
        if (!ReferencesRow(e))
        {
            return Always(Evaluate(e) is true);
        }

        switch (e)
        {
            case BinaryExpression { NodeType: ExpressionType.AndAlso } and:
                return And(Predicate(and.Left), Predicate(and.Right));
            case BinaryExpression { NodeType: ExpressionType.OrElse } or:
                return Or(Predicate(or.Left), Predicate(or.Right));
            case UnaryExpression { NodeType: ExpressionType.Not } not:
                return Negation(not.Operand);
            case BinaryExpression binary when IsComparison(binary.NodeType):
                return Comparison(binary.NodeType, binary.Left, binary.Right, negated: false);
            case MethodCallExpression call:
                return Call(call, negated: false);
            case MemberExpression { Member.Name: "HasValue" } hasValue when IsNullable(hasValue.Expression):
                return new SqlIsNull(RowOperand(hasValue.Expression!).Node!, Negated: true);
            default:
                return BoolColumn(e, true);
        }
    }

    private SqlNode Negation(Expression e)
    {
        if (!ReferencesRow(e))
        {
            return Always(Evaluate(e) is not true);
        }

        switch (e)
        {
            case BinaryExpression { NodeType: ExpressionType.AndAlso } and:
                return Or(Negation(and.Left), Negation(and.Right));
            case BinaryExpression { NodeType: ExpressionType.OrElse } or:
                return And(Negation(or.Left), Negation(or.Right));
            case UnaryExpression { NodeType: ExpressionType.Not } not:
                return Predicate(not.Operand);
            case BinaryExpression binary when IsComparison(binary.NodeType):
                return Comparison(binary.NodeType, binary.Left, binary.Right, negated: true);
            case MethodCallExpression call:
                return Call(call, negated: true);
            case MemberExpression { Member.Name: "HasValue" } hasValue when IsNullable(hasValue.Expression):
                return new SqlIsNull(RowOperand(hasValue.Expression!).Node!, Negated: false);
            default:
                return BoolColumn(e, false);
        }
    }

    private SqlNode BoolColumn(Expression e, bool value)
    {
        var operand = RowOperand(e);
        if ((Nullable.GetUnderlyingType(e.Type) ?? e.Type) != typeof(bool))
        {
            throw new DmTranslationException(e, Supported);
        }

        return new SqlCompare(operand.Node!, "=", Parameter(operand.Column?.Property.PropertyType ?? typeof(bool), value));
    }

    private SqlNode Comparison(ExpressionType op, Expression leftExpression, Expression rightExpression, bool negated)
    {
        if (negated)
        {
            op = Inverse(op);
        }

        if ((RowNullTest(op, leftExpression, rightExpression) ?? RelationNullTest(op, leftExpression, rightExpression)) is { } test)
        {
            return test;
        }

        var left = OperandOf(leftExpression);
        var right = OperandOf(rightExpression);
        if (left.IsValue && !right.IsValue)
        {
            (left, right) = (right, left);
            op = Mirror(op);
        }

        // In C#, comparing with a null: == is false, != is true, and an ordered comparison is false, so true once
        // negated. SQL makes all of them unknown, which drops the row.
        var nullMakesTrue = op == ExpressionType.NotEqual || (negated && IsOrdered(op));

        if (right.IsValue)
        {
            if (right.Value is null)
            {
                return op switch
                {
                    ExpressionType.Equal => new SqlIsNull(left.Node!, Negated: false),
                    ExpressionType.NotEqual => new SqlIsNull(left.Node!, Negated: true),
                    _ => Always(negated),
                };
            }

            var comparison = new SqlCompare(left.Node!, Operator(op), Parameter(left, right.Value));
            return left.Nullable && nullMakesTrue ? Or(comparison, new SqlIsNull(left.Node!, Negated: false)) : comparison;
        }

        var both = new SqlCompare(left.Node!, Operator(op), right.Node!);
        SqlNode IsNull(Operand o) => new SqlIsNull(o.Node!, Negated: false);
        SqlNode IsNotNull(Operand o) => new SqlIsNull(o.Node!, Negated: true);
        return op switch
        {
            ExpressionType.Equal when left.Nullable && right.Nullable => Or(both, And(IsNull(left), IsNull(right))),
            ExpressionType.NotEqual when left.Nullable && right.Nullable =>
                Or(both, And(IsNull(left), IsNotNull(right)), And(IsNotNull(left), IsNull(right))),
            _ when nullMakesTrue && (left.Nullable || right.Nullable) =>
                new SqlLogical(
                    "OR",
                    [both, .. left.Nullable ? [IsNull(left)] : Array.Empty<SqlNode>(), .. right.Nullable ? [IsNull(right)] : Array.Empty<SqlNode>()]
                ),
            _ => both,
        };
    }

    private SqlNode Call(MethodCallExpression call, bool negated)
    {
        var method = call.Method;
        if ((CollectionPredicate(call, negated) ?? ExistsMarker(call, negated)) is { } related)
        {
            return related;
        }

        if (method.Name == nameof(Enumerable.Contains))
        {
            // list.Contains(o.Id), Enumerable.Contains(list, o.Id), and from C# 14 an array's
            // MemoryExtensions.Contains(span, o.Id), whose span is an implicit conversion of the array. For an array of
            // a type that is not IEquatable<T>, such as an enum, C# 14 binds the overload taking a comparer, with null.
            if (call.Object is null
                && (call.Arguments.Count == 2 || (call.Arguments.Count == 3 && call.Arguments[2] is ConstantExpression { Value: null })))
            {
                if (method.DeclaringType == typeof(string))
                {
                    throw new DmTranslationException(call, Supported);
                }

                return In(Unspanned(call.Arguments[0]), call.Arguments[1], negated);
            }

            if (call.Object is not null && call.Object.Type != typeof(string) && call.Arguments.Count == 1
                && typeof(IEnumerable).IsAssignableFrom(call.Object.Type))
            {
                return In(call.Object, call.Arguments[0], negated);
            }
        }

        if (call.Object is not null && call.Object.Type == typeof(string)
            && method.Name is nameof(string.Contains) or nameof(string.StartsWith) or nameof(string.EndsWith))
        {
            return call.Arguments.Count switch
            {
                1 => Like(call.Object, call.Arguments[0], method.Name, negated),
                2 when IgnoresCase(call, call.Arguments[1]) => Like(Lowered(call.Object), Lowered(call.Arguments[0]), method.Name, negated),
                _ => throw new DmTranslationException(call, Supported),
            };
        }

        if (method.Name == nameof(string.Equals) && method.DeclaringType == typeof(string) && call.Arguments[^1].Type == typeof(StringComparison))
        {
            var (left, right) = call.Object is null ? (call.Arguments[0], call.Arguments[1]) : (call.Object, call.Arguments[0]);
            if (IgnoresCase(call, call.Arguments[^1]))
            {
                return Comparison(ExpressionType.Equal, Lowered(left), Lowered(right), negated);
            }
        }

        if (method.DeclaringType == typeof(string) && method.Name == nameof(string.IsNullOrEmpty))
        {
            var column = RowOperand(call.Arguments[0]).Node!;
            var empty = new SqlDialectFunction(DialectFunction.IsEmptyString, [column]);
            return negated
                ? And(new SqlIsNull(column, Negated: true), new SqlNot(empty))
                : Or(new SqlIsNull(column, Negated: false), empty);
        }

        if (method.Name == nameof(Equals) && method.ReturnType == typeof(bool))
        {
            if (call.Object is not null && call.Arguments.Count == 1)
            {
                return Comparison(ExpressionType.Equal, call.Object, call.Arguments[0], negated);
            }

            if (call.Object is null && call.Arguments.Count == 2)
            {
                return Comparison(ExpressionType.Equal, call.Arguments[0], call.Arguments[1], negated);
            }
        }

        throw new DmTranslationException(call, Supported);
    }

    private SqlNode In(Expression source, Expression item, bool negated)
    {
        if (ReferencesRow(source))
        {
            throw new DmTranslationException(
                source,
                "searching a column's own collection is not translated; Contains is translated for a list of values "
                    + "and a column, such as ids.Contains(o.Id)."
            );
        }

        var column = OperandOf(item);
        var values = Evaluate(source) as IEnumerable ?? throw new DmTranslationException(source, "the list is null.");
        var items = values.Cast<object?>().ToList();
        var hasNull = items.RemoveAll(v => v is null) > 0;
        List<SqlValue> list = [.. items.Select(v => Parameter(column, v!))];
        if (!negated)
        {
            SqlNode? match = list.Count > 0 ? new SqlIn(column.Node!, list, Negated: false) : null;
            return (match, hasNull) switch
            {
                (null, false) => Always(false),
                (null, true) => new SqlIsNull(column.Node!, Negated: false),
                (_, true) => Or(match, new SqlIsNull(column.Node!, Negated: false)),
                _ => match,
            };
        }

        // C# finds a NULL row's value in the list only if the list holds a null. NOT IN drops a NULL row, which is
        // right when it does and wrong when it does not.
        if (list.Count == 0)
        {
            return hasNull ? new SqlIsNull(column.Node!, Negated: true) : Always(true);
        }

        var notIn = new SqlIn(column.Node!, list, Negated: true);
        return column.Nullable && !hasNull ? Or(notIn, new SqlIsNull(column.Node!, Negated: false)) : notIn;
    }

    private SqlNode Like(Expression target, Expression argument, string method, bool negated)
    {
        var column = RowOperand(target);
        if (ReferencesRow(argument))
        {
            throw new DmTranslationException(argument, $"{method} is translated for a value, not another column.");
        }

        var value = Evaluate(argument) switch
        {
            string text => text,
            char c => c.ToString(),
            null => throw new DmTranslationException(argument, $"the value {method} looks for is null."),
            var other => other.ToString()!,
        };

        var escape = LikeEscapes.FirstOrDefault(c => !value.Contains(c), LikeEscapes[0]);
        var escaped = new StringBuilder(value.Length + 2);
        foreach (var c in value)
        {
            if (c == escape || c is '%' or '_' || (c == '[' && _dialect.ProviderType == DbProviderType.SqlServer))
            {
                escaped.Append(escape);
            }

            escaped.Append(c);
        }

        var pattern = method switch
        {
            nameof(string.StartsWith) => $"{escaped}%",
            nameof(string.EndsWith) => $"%{escaped}",
            _ => $"%{escaped}%",
        };

        return new SqlLike(column.Node!, Parameter(typeof(string), pattern), escape, negated);
    }

    private static Expression Lowered(Expression text)
    {
        if (text.Type == typeof(char))
        {
            text = Expression.Call(text, typeof(char).GetMethod(nameof(char.ToString), Type.EmptyTypes)!);
        }

        return Expression.Call(text, typeof(string).GetMethod(nameof(string.ToLowerInvariant), Type.EmptyTypes)!);
    }

    // Ignoring case is translated as LOWER on both sides. A comparison that keeps case is not: whether the database
    // does follows the column's collation, case-insensitive by default on SQL Server and MySQL.
    private bool IgnoresCase(MethodCallExpression call, Expression comparison)
    {
        if (ReferencesRow(comparison) || Evaluate(comparison) is not StringComparison value)
        {
            throw new DmTranslationException(call, "the StringComparison must be a value.");
        }

        return value is StringComparison.OrdinalIgnoreCase or StringComparison.InvariantCultureIgnoreCase or StringComparison.CurrentCultureIgnoreCase
            ? true
            : throw new DmTranslationException(
                call,
                $"{value} keeps case, and whether the database does follows the column's collation. Use the overload "
                    + "without a StringComparison, or one that ignores case."
            );
    }

    private Operand OperandOf(Expression e) =>
        ReferencesRow(e) ? RowOperand(e) : new Operand { IsValue = true, Value = Evaluate(e) };

    private Operand RowOperand(Expression e)
    {
        if (_known is not null && _known.TryGetValue(e, out var known))
        {
            return new Operand { Node = known, Nullable = true };
        }

        RequireValueKept(e);
        e = StripConversions(e);

        // o.Total.Value: the column, which C# would never compare as null.
        if (e is MemberExpression { Member.Name: "Value", Expression: { } inner } && IsNullable(inner))
        {
            return RowOperand(inner) with { Nullable = false };
        }

        if (e is MemberExpression { Expression: { } owner } member
            && StripConversions(owner) is ParameterExpression row
            && _sources.TryGetValue(row, out var source))
        {
            var column = ColumnFor(source.Model!, member.Member, e);
            var nullable = (source.Optional && !_rowsExist) || (column.Column.IsNullable && !column.Column.IsPrimaryKey);
            return new Operand { Node = new SqlColumn(source, column.Column.ColumnName), Column = column, Nullable = nullable };
        }

        if ((CollectionValue(e) ?? GroupAggregate(e)) is { } value)
        {
            return value;
        }

        if (e is MemberExpression { Expression: { } path } related && TryChain(path, out var root, out var hops) && hops.Count > 0)
        {
            return RelatedColumn(related, root, hops);
        }

        return Function(e) ?? throw new DmTranslationException(e, Supported);
    }

    private static DmlColumn ColumnFor(DmlModel model, MemberInfo member, Expression context) =>
        model.ColumnsByProperty.TryGetValue(member.Name, out var column)
            ? column
            : throw new DmTranslationException(
                context,
                $"{model.Type.Name}.{member.Name} is not mapped to a column of {model.Name}."
            );

    private static SqlValue Parameter(Operand column, object value)
    {
        // The value bound by the column's own type, so its type handler applies: an enum compared as
        // (int)o.Status == 2 is bound as the enum, and a char compared as its code is bound as the char.
        if (column.Column is { } mapped)
        {
            var declared = mapped.Property.PropertyType;
            var target = Nullable.GetUnderlyingType(declared) ?? declared;
            if (target.IsInstanceOfType(value))
            {
                return Parameter(declared, value);
            }

            if (target.IsEnum)
            {
                return Parameter(declared, value is string text ? Enum.Parse(target, text) : Enum.ToObject(target, value));
            }

            if (target == typeof(char) && value is IConvertible)
            {
                return Parameter(declared, Convert.ToChar(value, System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        // A value of another type, such as 2.5 compared with an integer column: the database compares the numbers.
        return Parameter(value.GetType(), value);
    }

    private static SqlValue Parameter(Type declaredType, object? value) => new(declaredType, value);

    private static bool IsNullable(Expression? e) => e is not null && Nullable.GetUnderlyingType(e.Type) is not null;

    private bool ReferencesRow(Expression e)
    {
        var finder = new RowFinder(_sources);
        finder.Visit(e);
        return finder.Found;
    }

    private static object? Evaluate(Expression e)
    {
        switch (e)
        {
            case ConstantExpression constant:
                return constant.Value;

            // Captured variables and their members, without compiling anything.
            case MemberExpression { Member: FieldInfo field } member:
                {
                    var owner = member.Expression is null ? null : Evaluate(member.Expression);
                    if (owner is not null || field.IsStatic)
                    {
                        return field.GetValue(owner);
                    }

                    break;
                }

            case MemberExpression { Member: PropertyInfo property } member when property.GetIndexParameters().Length == 0:
                {
                    var owner = member.Expression is null ? null : Evaluate(member.Expression);
                    if (owner is not null || property.GetMethod?.IsStatic == true)
                    {
                        return property.GetValue(owner);
                    }

                    break;
                }
        }

        return Expression.Lambda<Func<object?>>(Expression.Convert(e, typeof(object))).Compile(preferInterpretation: true)();
    }

    private static Expression Unspanned(Expression source) =>
        source switch
        {
            MethodCallExpression { Method.Name: "op_Implicit", Arguments.Count: 1 } implicitSpan => implicitSpan.Arguments[0],
            UnaryExpression { NodeType: ExpressionType.Convert } convert when convert.Type.Name.Contains("Span", StringComparison.Ordinal) =>
                convert.Operand,
            _ => source,
        };

    /// <summary>
    /// One side of a comparison: a column, or a value.
    /// </summary>
    private readonly struct Operand
    {
        /// <summary>
        /// Gets the node for a column or a computed value, or null for a value.
        /// </summary>
        public SqlNode? Node { get; init; }

        /// <summary>
        /// Gets the column, if the side is one.
        /// </summary>
        public DmlColumn? Column { get; init; }

        /// <summary>
        /// Gets a value indicating whether the side can be NULL in the database.
        /// </summary>
        public bool Nullable { get; init; }

        /// <summary>
        /// Gets a value indicating whether the side is a value, with no reference to the row.
        /// </summary>
        public bool IsValue { get; init; }

        /// <summary>
        /// Gets the value.
        /// </summary>
        public object? Value { get; init; }
    }

    private sealed class RowFinder(IReadOnlyDictionary<ParameterExpression, SqlSource> rows) : ExpressionVisitor
    {
        public bool Found { get; private set; }

        public override Expression? Visit(Expression? node) => Found ? node : base.Visit(node);

        protected override Expression VisitParameter(ParameterExpression node)
        {
            Found |= rows.ContainsKey(node);
            return node;
        }

        // An aggregate of a group is computed from its rows.
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            Found |= node.Method.DeclaringType == typeof(DmlAggregates);
            return Found ? node : base.VisitMethodCall(node);
        }
    }
}
