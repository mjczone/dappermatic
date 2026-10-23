// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Linq.Expressions;

namespace MJCZone.DapperMatic.Dml.Internal;

/// <summary>
/// The values a condition, an ordering or an aggregate can compute from a row: arithmetic, string functions, date
/// parts, <c>??</c> and <c>?:</c>.
/// </summary>
internal sealed partial class DmlTranslator
{
    private static readonly HashSet<string> DateTimeParts = ["Year", "Month", "Day", "Hour", "Minute", "Second"];
    private static readonly HashSet<string> DateParts = ["Year", "Month", "Day"];
    private static readonly HashSet<string> TimeParts = ["Hour", "Minute", "Second"];
    private static readonly SqlText EmptyString = new("''");

    /// <summary>
    /// Translates a value computed from a row, such as an ordering key or an aggregate's argument.
    /// </summary>
    /// <param name="value">The value, over the model.</param>
    /// <param name="source">The source the model's rows are read from.</param>
    /// <param name="scope">The scope of the statement's <c>SELECT</c>, which joins the related rows the value reads.</param>
    /// <returns>The node, whether it can be NULL, and the column if the value is one of the model's.</returns>
    public static (SqlNode Node, bool Nullable, DmlColumn? Column) Value(LambdaExpression value, SqlSource source, SqlScope scope)
    {
        var operand = new DmlTranslator(source, scope, value.Parameters[0]).RowOperand(value.Body);
        var own = operand.Node is SqlColumn { } column && ReferenceEquals(column.Source, source) ? operand.Column : null;
        return (operand.Node!, operand.Nullable, own);
    }

    /// <summary>
    /// Translates a value computed from a row's own columns.
    /// </summary>
    /// <param name="value">The value, over the model.</param>
    /// <param name="source">The source the model's rows are read from.</param>
    /// <param name="dialect">The dialect.</param>
    /// <returns>The node, whether it can be NULL, and the column if the value is one.</returns>
    public static (SqlNode Node, bool Nullable, DmlColumn? Column) Value(LambdaExpression value, SqlSource source, DmlDialect dialect) =>
        Value(value, source, SqlScope.Plain(dialect));

    private static bool IsIntegral(Type type) =>
        (Nullable.GetUnderlyingType(type) ?? type) is var t
        && (t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte)
            || t == typeof(sbyte) || t == typeof(ushort) || t == typeof(uint) || t == typeof(ulong));

    private static bool IsArithmetic(BinaryExpression b) =>
        b.NodeType
            is ExpressionType.Add
                or ExpressionType.AddChecked
                or ExpressionType.Subtract
                or ExpressionType.SubtractChecked
                or ExpressionType.Multiply
                or ExpressionType.MultiplyChecked
                or ExpressionType.Divide
                or ExpressionType.Modulo
        && (b.Method is null || b.Method.DeclaringType == typeof(decimal));

    private static string ArithmeticOperator(ExpressionType op) =>
        op switch
        {
            ExpressionType.Add or ExpressionType.AddChecked => "+",
            ExpressionType.Subtract or ExpressionType.SubtractChecked => "-",
            ExpressionType.Multiply or ExpressionType.MultiplyChecked => "*",
            ExpressionType.Modulo => "%",
            _ => "/",
        };

    private static Operand Computed(SqlNode node, bool nullable) => new() { Node = node, Nullable = nullable };

    private Operand? Function(Expression e)
    {
        switch (e)
        {
            case MemberExpression { Member.Name: nameof(string.Length), Expression: { } text } when text.Type == typeof(string):
                {
                    var operand = SqlOf(text);
                    return Computed(new SqlDialectFunction(DialectFunction.Length, [operand.Node!]), operand.Nullable);
                }

            case MemberExpression { Expression: { } date } part when DateTimeParts.Contains(part.Member.Name):
                return DatePart(part, date);

            case UnaryExpression { NodeType: ExpressionType.Negate or ExpressionType.NegateChecked } negate:
                {
                    var operand = SqlOf(negate.Operand);
                    return Computed(new SqlNegate(operand.Node!), operand.Nullable);
                }

            case BinaryExpression { NodeType: ExpressionType.Coalesce } coalesce:
                {
                    var left = SqlOf(coalesce.Left);
                    var right = SqlOf(coalesce.Right);
                    return Computed(new SqlFunction("COALESCE", [left.Node!, right.Node!]), right.Nullable);
                }

            case BinaryExpression { NodeType: ExpressionType.Add or ExpressionType.AddChecked } add when add.Type == typeof(string):
                return Concat(add);

            case BinaryExpression arithmetic when IsArithmetic(arithmetic):
                return Arithmetic(arithmetic);

            case ConditionalExpression conditional:
                {
                    var test = Predicate(conditional.Test);
                    var whenTrue = SqlOf(conditional.IfTrue);
                    var whenFalse = SqlOf(conditional.IfFalse);
                    return Computed(new SqlCase(test, whenTrue.Node!, whenFalse.Node!), whenTrue.Nullable || whenFalse.Nullable);
                }

            case MethodCallExpression { Method.Name: nameof(string.Concat) } concat when concat.Method.DeclaringType == typeof(string):
                return Concat(concat);

            case MethodCallExpression { Object: { } text } call when text.Type == typeof(string):
                return StringFunction(call, text);

            default:
                return null;
        }
    }

    private Operand DatePart(MemberExpression part, Expression date)
    {
        var type = Nullable.GetUnderlyingType(date.Type) ?? date.Type;
        var supported =
            type == typeof(DateTime) ? DateTimeParts
            : type == typeof(DateOnly) ? DateParts
            : type == typeof(TimeOnly) ? TimeParts
            : null;

        if (type == typeof(DateTimeOffset))
        {
            throw new DmTranslationException(
                part,
                "the parts of a DateTimeOffset depend on its offset, which providers keep differently: compare it "
                    + "with DateTimeOffset values instead."
            );
        }

        if (supported is null || !supported.Contains(part.Member.Name))
        {
            throw new DmTranslationException(part, Supported);
        }

        var operand = SqlOf(date);
        return Computed(new SqlDialectFunction(DialectFunction.DatePart, [operand.Node!], part.Member.Name), operand.Nullable);
    }

    private Operand Arithmetic(BinaryExpression arithmetic)
    {
        var left = SqlOf(arithmetic.Left);
        var right = SqlOf(arithmetic.Right);
        var nullable = left.Nullable || right.Nullable;

        var type = Nullable.GetUnderlyingType(arithmetic.Type) ?? arithmetic.Type;
        if (arithmetic.NodeType == ExpressionType.Modulo && !IsIntegral(type))
        {
            if (type != typeof(decimal) && _dialect.ProviderType is DbProviderType.SqlServer or DbProviderType.PostgreSql)
            {
                throw new DmTranslationException(
                    arithmetic,
                    "SQL Server and PostgreSQL have no % for floating-point numbers: compute it over decimal values."
                );
            }

            return Computed(new SqlDialectFunction(DialectFunction.FractionRemainder, [left.Node!, right.Node!]), nullable);
        }

        if (arithmetic.NodeType != ExpressionType.Divide)
        {
            return Computed(new SqlArithmetic(left.Node!, ArithmeticOperator(arithmetic.NodeType), right.Node!), nullable);
        }

        if (IsIntegral(type))
        {
            // A ulong is a decimal(20, 0) on SQL Server and a numeric(20, 0) on PostgreSQL, which divide with a
            // fraction; it is never negative, so its floor is C#'s quotient.
            return Computed(
                type == typeof(ulong) && _dialect.ProviderType is DbProviderType.SqlServer or DbProviderType.PostgreSql
                    ? new SqlFunction("FLOOR", [new SqlArithmetic(left.Node!, "/", right.Node!)])
                    : new SqlDialectFunction(DialectFunction.IntegerDivide, [left.Node!, right.Node!]),
                nullable
            );
        }

        // (double)o.Qty / o.Count divides as numbers with fractions in C#; the columns themselves would divide as
        // integers in SQL Server, PostgreSQL and SQLite. SQLite also keeps a whole decimal as an integer, so that
        // 10.00 / 4 is 2 there, whatever the declared types.
        var integers = IsIntegral(StripConversions(arithmetic.Left).Type) && IsIntegral(StripConversions(arithmetic.Right).Type);
        return Computed(
            integers || _dialect.ProviderType == DbProviderType.Sqlite
                ? new SqlDialectFunction(DialectFunction.FractionDivide, [left.Node!, right.Node!])
                : new SqlArithmetic(left.Node!, "/", right.Node!),
            nullable
        );
    }

    private Operand Concat(Expression concat)
    {
        // a + b + c is nested; string.Concat(a, b, c) is flat.
        var parts = new List<Expression>();
        void Flatten(Expression part)
        {
            switch (part)
            {
                case BinaryExpression { NodeType: ExpressionType.Add or ExpressionType.AddChecked } add when add.Type == typeof(string):
                    Flatten(add.Left);
                    Flatten(add.Right);
                    break;
                case MethodCallExpression { Method.Name: nameof(string.Concat), Object: null } call
                    when call.Method.DeclaringType == typeof(string) && call.Arguments.All(a => a.Type != typeof(string[])):
                    call.Arguments.ToList().ForEach(Flatten);
                    break;
                default:
                    parts.Add(part);
                    break;
            }
        }

        Flatten(concat);

        var sql = new List<SqlNode>();
        foreach (var part in parts)
        {
            var unconverted = StripConversions(part);
            if (unconverted.Type != typeof(string))
            {
                throw new DmTranslationException(
                    part,
                    "only strings are joined: the providers write other types as text differently."
                );
            }

            // In C#, joining a null adds nothing; in SQL it makes the whole result NULL.
            if (!ReferencesRow(unconverted))
            {
                if (Evaluate(unconverted) is string value)
                {
                    sql.Add(Parameter(typeof(string), value));
                }

                continue;
            }

            var operand = RowOperand(unconverted);
            sql.Add(operand.Nullable ? new SqlFunction("COALESCE", [operand.Node!, EmptyString]) : operand.Node!);
        }

        return Computed(sql.Count == 0 ? EmptyString : new SqlDialectFunction(DialectFunction.Concat, sql), nullable: false);
    }

    private Operand? StringFunction(MethodCallExpression call, Expression text)
    {
        var name = call.Method.Name;
        var arguments = call.Arguments;

        string? function = (name, arguments.Count) switch
        {
            (nameof(string.ToUpper) or nameof(string.ToUpperInvariant), 0) => "UPPER",
            (nameof(string.ToLower) or nameof(string.ToLowerInvariant), 0) => "LOWER",
            _ => null,
        };

        if (function is not null)
        {
            var operand = SqlOf(text);
            return Computed(new SqlFunction(function, [operand.Node!]), operand.Nullable);
        }

        if (name is nameof(string.Trim) or nameof(string.TrimStart) or nameof(string.TrimEnd) && arguments.Count == 0)
        {
            var operand = SqlOf(text);
            var ends = name switch
            {
                nameof(string.TrimStart) => "start",
                nameof(string.TrimEnd) => "end",
                _ => "both",
            };
            return Computed(new SqlDialectFunction(DialectFunction.Trim, [operand.Node!], ends), operand.Nullable);
        }

        if (name == nameof(string.Substring) && arguments.Count is 1 or 2)
        {
            var operand = SqlOf(text);
            var start = ValueArgument<int>(call, arguments[0]) + 1;
            int? length = arguments.Count == 2 ? ValueArgument<int>(call, arguments[1]) : null;
            return Computed(
                new SqlDialectFunction(
                    DialectFunction.Substring,
                    length is null
                        ? [operand.Node!, Parameter(typeof(int), start)]
                        : [operand.Node!, Parameter(typeof(int), start), Parameter(typeof(int), length)]
                ),
                operand.Nullable
            );
        }

        if (name == nameof(string.Replace) && arguments.Count == 2 && arguments[0].Type == typeof(string))
        {
            var operand = SqlOf(text);
            var oldValue = ValueArgument<string>(call, arguments[0]);
            var newValue = ValueArgument<string?>(call, arguments[1]) ?? string.Empty;
            return Computed(
                new SqlFunction("REPLACE", [operand.Node!, Parameter(typeof(string), oldValue), Parameter(typeof(string), newValue)]),
                operand.Nullable
            );
        }

        return null;
    }

    private TValue ValueArgument<TValue>(MethodCallExpression call, Expression argument)
    {
        if (ReferencesRow(argument))
        {
            throw new DmTranslationException(call, $"{call.Method.Name} is translated with values, not columns, as its arguments.");
        }

        return Evaluate(argument) is TValue value
            ? value
            : default(TValue) is null
                ? default!
                : throw new DmTranslationException(call, $"an argument of {call.Method.Name} is null.");
    }

    // A side of a function: SQL for a row expression, or a parameter for a value.
    private Operand SqlOf(Expression e)
    {
        if (ReferencesRow(e))
        {
            return RowOperand(e);
        }

        var value = Evaluate(e);
        return Computed(Parameter(Nullable.GetUnderlyingType(e.Type) ?? e.Type, value), value is null);
    }
}
