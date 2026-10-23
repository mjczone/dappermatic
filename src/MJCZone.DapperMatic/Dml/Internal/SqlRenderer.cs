// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Text;
using System.Text.Json;

namespace MJCZone.DapperMatic.Dml.Internal;

/// <summary>
/// Writes a query tree as SQL for one provider, adding its values to the parameters.
/// </summary>
/// <remarks>
/// Parameters are named <c>p0</c>, <c>p1</c> ... in the order they are written, continuing from any the collection
/// already holds, and one value node is one parameter however often it is written.
/// </remarks>
internal sealed class SqlRenderer
{
    private readonly DmlDialect _dialect;
    private readonly DmlParameters _parameters;
    private readonly Dictionary<SqlValue, string> _names = new(ReferenceEqualityComparer.Instance);
    private readonly Stack<Dictionary<SqlSource, string>> _frames = new();
    private bool _jsonLists;
    private bool _qualify;
    private int _trusted;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlRenderer"/> class.
    /// </summary>
    /// <param name="dialect">The provider's dialect.</param>
    /// <param name="parameters">The parameters, which values are added to.</param>
    public SqlRenderer(DmlDialect dialect, DmlParameters parameters)
    {
        _dialect = dialect;
        _parameters = parameters;
    }

    /// <summary>
    /// Gets how to write a column instead of by its name, or null to write it by name: for a column qualified by an
    /// alias, or one of an upsert's incoming row (<c>excluded."views"</c>, <c>VALUES(`views`)</c>).
    /// </summary>
    public Func<SqlColumn, string?>? ColumnWriter { get; init; }

    /// <summary>
    /// Writes a statement's parts, deciding first, from all of them, whether its lists fit the provider's limit on
    /// parameters.
    /// </summary>
    /// <param name="nodes">Every node of the statement, nulls ignored.</param>
    /// <returns>This renderer.</returns>
    /// <exception cref="NotSupportedException">The statement needs more parameters than the provider allows.</exception>
    public SqlRenderer For(params object?[] nodes)
    {
        var counter = new ValueCounter();
        foreach (var node in nodes)
        {
            counter.Count(node);
        }

        // A statement of one table names its columns alone; one that joins or correlates qualifies every column. An
        // upsert's two rows are named by its column writer.
        _qualify = ColumnWriter is null && counter.Sources.Count > 1;

        if (_parameters.Count + counter.Values <= _dialect.MaxParameters)
        {
            return this;
        }

        // SQL Server allows 2,100 parameters: a long list is one JSON array, which SQL Server converts to the column's
        // type value by value, as it would a parameter.
        if (_dialect.ProviderType == DbProviderType.SqlServer
            && _parameters.Count + counter.Values - counter.ListValues + counter.Lists <= _dialect.MaxParameters)
        {
            _jsonLists = true;
            return this;
        }

        throw new NotSupportedException(
            $"The statement needs {_parameters.Count + counter.Values} parameters, more than {_dialect.ProviderType} "
                + "allows in one: read in batches, or join to a table of the values."
        );
    }

    /// <summary>
    /// Writes a node.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The SQL.</returns>
    public string Render(SqlNode node) =>
        node switch
        {
            SqlColumn column when _trusted == 0 && column.Source.Hidden?.Contains(column.ColumnName) == true => throw Hidden(column),
            SqlTrusted trusted => Trusted(trusted.Inner),
            SqlRaw raw => string.Format(System.Globalization.CultureInfo.InvariantCulture, raw.Format, [.. raw.Arguments.Select(Render)]),
            SqlColumn column => ColumnWriter?.Invoke(column) ?? Column(column),
            SqlValue value => Parameter(value),
            SqlText text => text.Sql,
            SqlLogical logical => $"({string.Join($" {logical.Operator} ", logical.Operands.Select(Render))})",
            SqlNot not => $"NOT ({Render(not.Operand)})",
            SqlCompare compare => $"{Render(compare.Left)} {compare.Operator} {Render(compare.Right)}",
            SqlIsNull isNull => $"{Render(isNull.Operand)} {(isNull.Negated ? "IS NOT NULL" : "IS NULL")}",
            SqlIn @in => In(@in),
            SqlLike like =>
                $"{Render(like.Operand)} {(like.Negated ? "NOT LIKE" : "LIKE")} {Render(like.Pattern)} ESCAPE '{like.Escape}'",
            SqlArithmetic arithmetic => $"({Render(arithmetic.Left)} {arithmetic.Operator} {Render(arithmetic.Right)})",
            SqlNegate negate => $"(-{Render(negate.Operand)})",
            SqlFunction function => $"{function.Name}({string.Join(", ", function.Arguments.Select(Render))})",
            SqlDialectFunction function => DialectFunction(function),
            SqlCase @case => $"CASE WHEN {Render(@case.When)} THEN {Render(@case.Then)} ELSE {Render(@case.Else)} END",
            SqlAggregate aggregate => Aggregate(aggregate),
            SqlExists exists => $"EXISTS ({Render(exists.Select)})",
            SqlScalar scalar => $"({Render(scalar.Select)})",
            SqlRowNumber rowNumber =>
                $"ROW_NUMBER() OVER (PARTITION BY {string.Join(", ", rowNumber.PartitionBy.Select(Render))} ORDER BY "
                    + $"{string.Join(", ", rowNumber.OrderBy.Select(o => _dialect.OrderKey(Render(o.Key), o.Descending, o.Nullable)))})",
            _ => throw new NotSupportedException($"{node.GetType().Name} is not a node the renderer writes."),
        };

    /// <summary>
    /// Writes a <c>SELECT</c>, ordered and paged.
    /// </summary>
    /// <param name="select">The select.</param>
    /// <returns>The SQL.</returns>
    public string Render(SqlSelect select)
    {
        var frame = new Dictionary<SqlSource, string>(ReferenceEqualityComparer.Instance)
        {
            [select.From] = select.FromSubquery is null ? select.From.Alias : "q",
        };
        foreach (var join in select.Joins)
        {
            frame[join.Source] = join.Source.Alias;
        }

        _frames.Push(frame);
        try
        {
            return RenderInFrame(select);
        }
        finally
        {
            _frames.Pop();
        }
    }

    private static object? JsonValue(object? value) =>
        value switch
        {
            Enum e => Convert.ChangeType(e, Enum.GetUnderlyingType(e.GetType()), CultureInfo.InvariantCulture),
            char c => c.ToString(),
            _ => value,
        };

    private string RenderInFrame(SqlSelect select)
    {
        var items = select.Items.Count == 0
            ? "1"
            : string.Join(
                ", ",
                select.Items.Select(i => i.Alias is null ? Item(i.Node) : $"{Item(i.Node)} AS {_dialect.Alias(i.Alias)}")
            );
        var from = select.FromSubquery is not null ? $"({Render(select.FromSubquery)}) q"
            : _qualify ? $"{select.From.QualifiedName} {select.From.Alias}"
            : select.From.QualifiedName;

        var sql = new StringBuilder("SELECT ");
        if (select.Distinct)
        {
            sql.Append("DISTINCT ");
        }

        sql.Append(items).Append(" FROM ").Append(from);
        foreach (var join in select.Joins)
        {
            sql.Append(' ').Append(join.Kind).Append(' ').Append(join.Source.QualifiedName).Append(' ').Append(join.Source.Alias);
            if (join.On is not null)
            {
                sql.Append(" ON ").Append(Render(join.On));
            }
        }

        if (select.Where.Count > 0)
        {
            sql.Append(" WHERE ").Append(string.Join(" AND ", select.Where.Select(Render)));
        }

        if (select.GroupBy.Count > 0)
        {
            sql.Append(" GROUP BY ").Append(string.Join(", ", select.GroupBy.Select(Render)));
        }

        if (select.Having.Count > 0)
        {
            sql.Append(" HAVING ").Append(string.Join(" AND ", select.Having.Select(Render)));
        }

        var orderBy = select.OrderBySql
            ?? (select.OrderBy.Count == 0
                ? null
                : string.Join(", ", select.OrderBy.Select(o => _dialect.OrderKey(Render(o.Key), o.Descending, o.Nullable))));
        return _dialect.Page(sql.ToString(), orderBy, select.Skip, select.Take);
    }

    // A policy's condition is written as it is, columns the caller may not read included.
    private string Trusted(SqlNode node)
    {
        _trusted++;
        try
        {
            return Render(node);
        }
        finally
        {
            _trusted--;
        }
    }

    // A column the caller may not read is read as empty, and never compared, ordered, grouped or computed with.
    private static UnauthorizedAccessException Hidden(SqlColumn column) =>
        new($"The column {column.ColumnName} of {column.Source.Model?.Name ?? column.Source.QualifiedName} is not one this caller may read.");

    private string Item(SqlNode node) =>
        node is SqlColumn column && column.Source.Hidden?.Contains(column.ColumnName) == true ? "NULL" : Render(node);

    // A column alone, or qualified by the alias of the innermost SELECT reading its source; a column of the table an
    // UPDATE or DELETE writes, read in a subquery, is qualified by the table's name.
    private string Column(SqlColumn column)
    {
        var name = _dialect.Column(column.ColumnName);
        if (!_qualify)
        {
            return name;
        }

        foreach (var frame in _frames)
        {
            if (frame.TryGetValue(column.Source, out var alias))
            {
                return $"{alias}.{name}";
            }
        }

        return $"{column.Source.QualifiedName}.{name}";
    }

    private string Parameter(SqlValue value)
    {
        if (!_names.TryGetValue(value, out var name))
        {
            name = $"p{_parameters.Count}";
            _parameters.Add(name, value.DeclaredType, value.Value);
            _names[value] = name;
        }

        return _dialect.ParameterReference(name, value.DeclaredType);
    }

    private string In(SqlIn @in)
    {
        var list = _jsonLists && @in.Values.Count > 1
            ? $"SELECT value FROM OPENJSON({Parameter(new SqlValue(typeof(string), JsonSerializer.Serialize(@in.Values.Select(v => JsonValue(v.Value)))))})"
            : string.Join(", ", @in.Values.Select(Render));
        return $"{Render(@in.Operand)} {(@in.Negated ? "NOT IN" : "IN")} ({list})";
    }

    private string DialectFunction(SqlDialectFunction function)
    {
        var arguments = function.Arguments.Select(Render).ToList();
        return function.Function switch
        {
            Internal.DialectFunction.Length => _dialect.Length(arguments[0]),
            Internal.DialectFunction.Trim => _dialect.Trim(
                arguments[0],
                start: function.Option != "end",
                end: function.Option != "start"
            ),
            Internal.DialectFunction.Substring => _dialect.Substring(
                arguments[0],
                arguments[1],
                arguments.Count > 2 ? arguments[2] : null
            ),
            Internal.DialectFunction.DatePart => _dialect.DatePart(function.Option!, arguments[0]),
            Internal.DialectFunction.Concat => _dialect.Concat(arguments),
            Internal.DialectFunction.IsEmptyString => _dialect.IsEmptyString(arguments[0]),
            Internal.DialectFunction.IntegerDivide => _dialect.IntegerDivide(arguments[0], arguments[1]),
            Internal.DialectFunction.FractionRemainder => _dialect.FractionRemainder(arguments[0], arguments[1]),
            _ => $"({arguments[0]} * 1.0 / {arguments[1]})",
        };
    }

    private string Aggregate(SqlAggregate aggregate)
    {
        if (aggregate.Argument is null)
        {
            return $"{aggregate.Function}(*)";
        }

        var argument = Render(aggregate.Argument);
        return aggregate.WithFraction ? _dialect.AverageWithFraction(argument)
            : aggregate.Distinct ? $"{aggregate.Function}(DISTINCT {argument})"
            : $"{aggregate.Function}({argument})";
    }

    // Counts the parameters a statement will need, and how many of them are in lists.
    private sealed class ValueCounter
    {
        private readonly HashSet<SqlValue> _seen = new(ReferenceEqualityComparer.Instance);

        public HashSet<SqlSource> Sources { get; } = new(ReferenceEqualityComparer.Instance);

        public int Values { get; private set; }

        public int ListValues { get; private set; }

        public int Lists { get; private set; }

        public void Count(object? node)
        {
            switch (node)
            {
                case null:
                    return;
                case SqlValue value:
                    Values += _seen.Add(value) ? 1 : 0;
                    return;
                case SqlColumn column:
                    Sources.Add(column.Source);
                    return;
                case SqlIn @in:
                    Count(@in.Operand);
                    if (@in.Values.Count > 1)
                    {
                        Lists++;
                        ListValues += @in.Values.Count;
                    }

                    foreach (var value in @in.Values)
                    {
                        Count(value);
                    }

                    return;
                case SqlSelect select:
                    Sources.Add(select.From);
                    Count(select.FromSubquery);
                    foreach (var join in select.Joins)
                    {
                        Sources.Add(join.Source);
                        Count(join.On);
                    }

                    foreach (var item in select.Items)
                    {
                        Count(item.Node);
                    }

                    Count(select.Where);
                    Count(select.GroupBy);
                    Count(select.Having);
                    foreach (var order in select.OrderBy)
                    {
                        Count(order.Key);
                    }

                    return;
                case IEnumerable<SqlNode> nodes:
                    foreach (var child in nodes)
                    {
                        Count(child);
                    }

                    return;
                case SqlLogical logical:
                    Count(logical.Operands);
                    return;
                case SqlNot not:
                    Count(not.Operand);
                    return;
                case SqlTrusted trusted:
                    Count(trusted.Inner);
                    return;
                case SqlRaw raw:
                    Count(raw.Arguments);
                    return;
                case SqlCompare compare:
                    Count(compare.Left);
                    Count(compare.Right);
                    return;
                case SqlIsNull isNull:
                    Count(isNull.Operand);
                    return;
                case SqlLike like:
                    Count(like.Operand);
                    Count(like.Pattern);
                    return;
                case SqlArithmetic arithmetic:
                    Count(arithmetic.Left);
                    Count(arithmetic.Right);
                    return;
                case SqlNegate negate:
                    Count(negate.Operand);
                    return;
                case SqlFunction function:
                    Count(function.Arguments);
                    return;
                case SqlDialectFunction function:
                    Count(function.Arguments);
                    return;
                case SqlCase @case:
                    Count(@case.When);
                    Count(@case.Then);
                    Count(@case.Else);
                    return;
                case SqlAggregate aggregate:
                    Count(aggregate.Argument);
                    return;
                case SqlExists exists:
                    Count(exists.Select);
                    return;
                case SqlScalar scalar:
                    Count(scalar.Select);
                    return;
                case SqlRowNumber rowNumber:
                    Count(rowNumber.PartitionBy);
                    foreach (var order in rowNumber.OrderBy)
                    {
                        Count(order.Key);
                    }

                    return;
            }
        }
    }
}
