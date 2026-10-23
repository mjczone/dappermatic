// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.Dml.Internal;

namespace MJCZone.DapperMatic.Dml.Rest;

/// <summary>
/// Translates a REST query into the query tree, over tables the database describes: every name checked against a
/// table's columns or the relations declared for it, every value a parameter of the column's type.
/// </summary>
/// <remarks>
/// Comparisons keep the meaning the typed DML methods give them where a column can be NULL: <c>note ne 'x'</c> is
/// true for a row whose note is NULL, and <c>not (total gt 5)</c> keeps the rows with no total. A name without a
/// lambda variable is a column of the row the query is about, wherever it is written.
/// </remarks>
internal sealed class RestTranslator
{
    private static readonly char[] LikeEscapes = ['~', '!', '#', '^', '|', '$'];

    private readonly DmlDialect _dialect;
    private readonly IReadOnlyDictionary<(RestTable Table, string Name), RestRelation> _relations;
    private readonly RestLimits _limits;
    private readonly Func<RestTable, RestNode?>? _readFilter;
    private int _nextAlias;
    private long _nodes;
    private int _lambdaDepth;
    private int _trusted;

    /// <summary>
    /// Initializes a new instance of the <see cref="RestTranslator"/> class.
    /// </summary>
    /// <param name="dialect">The dialect.</param>
    /// <param name="relations">The relations the request may follow, by table and name, resolved before it is translated.</param>
    /// <param name="limits">How much the request may ask for.</param>
    /// <param name="readFilter">
    /// The condition the policies put on each related table the request reads, over its own columns; null for none.
    /// </param>
    public RestTranslator(
        DmlDialect dialect,
        IReadOnlyDictionary<(RestTable Table, string Name), RestRelation> relations,
        RestLimits limits,
        Func<RestTable, RestNode?>? readFilter = null
    )
    {
        _dialect = dialect;
        _relations = relations;
        _limits = limits;
        _readFilter = readFilter;
    }

    /// <summary>
    /// Writes the <c>SELECT</c> of a query's rows.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="table">The table or view.</param>
    /// <param name="extraColumns">Columns to read beyond those selected, such as the keys an expand needs.</param>
    /// <returns>The query, and its source.</returns>
    /// <exception cref="ArgumentException">The query names a column or relation the table does not have, or asks for too much.</exception>
    public (SqlSelect Select, SqlSource Source) Rows(RestQuery query, RestTable table, IReadOnlyList<string> extraColumns)
    {
        _nodes = 0;
        var frame = Open(table);
        var where = query.Filter is null ? [] : new List<SqlNode> { Predicate(query.Filter, frame, negated: false) };
        var order = Order(query, frame);
        var top = Top(query);
        var select = new SqlSelect
        {
            From = frame.From,
            Joins = frame.Joins,
            Items = Items(query, table, frame.From, extraColumns),
            Where = where,
            OrderBy = order,
            Skip = query.Skip,
            Take = top,
        };
        return (select, frame.From);
    }

    /// <summary>
    /// Writes the <c>SELECT</c> of a query's groups (<c>$apply</c>): the rows that pass the condition, grouped by the
    /// keys, each group with its keys and aggregates; the query's own condition, order and page apply to the groups.
    /// </summary>
    /// <param name="query">The query, with its grouping.</param>
    /// <param name="table">The table or view.</param>
    /// <param name="rows">The condition on the rows grouped: the grouping's filter and the policies', or null.</param>
    /// <returns>The query.</returns>
    /// <exception cref="ArgumentException">The grouping reads something the table does not have, or that cannot be aggregated.</exception>
    public SqlSelect Grouped(RestQuery query, RestTable table, RestNode? rows)
    {
        _nodes = 0;
        var apply = query.Apply ?? throw new ArgumentException("The query has no $apply.");
        var frame = Open(table);
        var where = rows is null ? [] : new List<SqlNode> { Predicate(rows, frame, negated: false) };
        var outputs = new Dictionary<string, Side>(StringComparer.OrdinalIgnoreCase);
        var items = new List<SqlSelectItem>();
        var groupBy = new List<SqlNode>();
        foreach (var key in apply.GroupBy)
        {
            if (key.Segments.Count != 1)
            {
                throw new ArgumentException($"groupby groups by the table's own columns, and {key} is a related row's.");
            }

            var side = Operand(key, frame);
            groupBy.Add(side.Node!);
            items.Add(new SqlSelectItem(side.Node!, side.Column!.Name));
            outputs[side.Column.Name] = side;
        }

        foreach (var aggregate in apply.Aggregates)
        {
            var value = aggregate.Value is null ? default : Operand(aggregate.Value, frame);
            if (aggregate.Value is not null && value.IsValue)
            {
                throw new ArgumentException($"{aggregate.Alias} aggregates a value: aggregate a column.");
            }

            var type = value.Column?.Type is { } declared ? Nullable.GetUnderlyingType(declared) ?? declared : typeof(long);
            if (aggregate.Method is "sum" or "average" && !IsNumeric(type))
            {
                throw new ArgumentException($"{aggregate.Alias} is a {aggregate.Method} of {type.Name} values, which are not numbers.");
            }

            var (node, result) = aggregate.Method switch
            {
                "count" => ((SqlNode)new SqlAggregate("COUNT", null), typeof(long)),
                "countdistinct" => (new SqlAggregate("COUNT", value.Node, Distinct: true), typeof(long)),
                "sum" => (new SqlAggregate("SUM", value.Node), type),
                "min" => (new SqlAggregate("MIN", value.Node), type),
                "max" => (new SqlAggregate("MAX", value.Node), type),
                _ => (new SqlAggregate("AVG", value.Node, WithFraction: IsIntegral(type)), IsIntegral(type) ? typeof(decimal) : type),
            };
            items.Add(new SqlSelectItem(node, aggregate.Alias));
            var counts = aggregate.Method == "count" || aggregate.Method == "countdistinct";
            outputs[aggregate.Alias] = new Side { Node = node, Column = new RestColumn(aggregate.Alias, result, Nullable: !counts), Nullable = !counts };
        }

        // The groups' own condition and order read their keys and aggregates.
        var groups = new Frame { From = frame.From, Table = table, Outputs = outputs };
        var having = query.Filter is null ? [] : new List<SqlNode> { Predicate(query.Filter, groups, negated: false) };
        var order = query.OrderBy.Count > 0
            ? Order(query, groups)
            : [.. apply.GroupBy.Select(k => outputs[table.Column(k.Segments[0], "groupby").Name]).Select(k => new SqlOrder(k.Node!, Descending: false, k.Nullable))];
        return new SqlSelect
        {
            From = frame.From,
            Joins = frame.Joins,
            Items = items,
            Where = where,
            GroupBy = groupBy,
            Having = having,
            OrderBy = order,
            Skip = query.Skip,
            Take = Top(query),
        };
    }

    /// <summary>
    /// Writes the count of a grouped query's groups, without its order, top and skip.
    /// </summary>
    /// <param name="grouped">The grouped query.</param>
    /// <returns>The <c>SELECT COUNT(*)</c>.</returns>
    public static SqlSelect CountGroups(SqlSelect grouped) =>
        new()
        {
            From = grouped.From with { Alias = "q" },
            FromSubquery = grouped with { OrderBy = [], Skip = null, Take = null },
            Items = [new SqlSelectItem(new SqlAggregate("COUNT", null), null)],
        };

    /// <summary>
    /// Writes the count of a query's rows, without its top and skip.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="table">The table or view.</param>
    /// <returns>The <c>SELECT COUNT(*)</c>.</returns>
    public SqlSelect Count(RestQuery query, RestTable table)
    {
        _nodes = 0;
        var frame = Open(table);
        var where = query.Filter is null ? [] : new List<SqlNode> { Predicate(query.Filter, frame, negated: false) };
        return new SqlSelect
        {
            From = frame.From,
            Joins = frame.Joins,
            Items = [new SqlSelectItem(new SqlAggregate("COUNT", null), null)],
            Where = where,
        };
    }

    /// <summary>
    /// Writes the <c>SELECT</c> of a relation's rows for a batch of owners' keys: the expand's own filter, order and
    /// columns, and its top per owner.
    /// </summary>
    /// <param name="relation">The relation.</param>
    /// <param name="query">The expand's query.</param>
    /// <param name="keys">The owners' keys, in the order of the relation's key pairs.</param>
    /// <param name="extraColumns">Columns to read beyond those selected: the relation's keys and the expand's own expands'.</param>
    /// <param name="perOwnerTop">Whether the database numbers each owner's rows for the top; false to trim them as read.</param>
    /// <returns>The query.</returns>
    public SqlSelect Related(RestRelation relation, RestQuery query, IReadOnlyList<object?[]> keys, IReadOnlyList<string> extraColumns, bool perOwnerTop)
    {
        _nodes = 0;
        var other = relation.Other;
        var frame = Open(other);
        var columns = relation.Keys.Select(k => other.Column(k.Other, relation.Name)).ToList();
        List<SqlNode> where = [KeyIn(frame.From, columns, keys)];
        if (query.Filter is not null)
        {
            where.Add(Predicate(query.Filter, frame, negated: false));
        }

        if (_readFilter?.Invoke(other) is { } policy)
        {
            where.Add(Predicate(policy, frame, negated: false));
        }

        var order = Order(query, frame);
        var select = new SqlSelect
        {
            From = frame.From,
            Joins = frame.Joins,
            Items = Items(query, other, frame.From, extraColumns),
            Where = where,
            OrderBy = order,
        };

        if (query.Top is not { } top || !perOwnerTop)
        {
            return select;
        }

        // The top is per owner: each owner's rows numbered in the expand's order, or key order.
        if (order.Count == 0)
        {
            order = [.. (other.Keys.Count > 0 ? other.Keys : [other.Columns[0].Name]).Select(k => new SqlOrder(new SqlColumn(frame.From, other.Column(k, relation.Name).Name), false, false))];
        }

        return DmlLoader.PerOwner(select with { OrderBy = order }, frame.From, [.. columns.Select(c => c.Name)], order, Math.Min(top, _limits.MaxTop));
    }

    /// <summary>
    /// Translates a condition of a write, over the table written.
    /// </summary>
    /// <param name="filter">The request's condition.</param>
    /// <param name="target">The table.</param>
    /// <param name="source">The source the statement writes.</param>
    /// <returns>The condition, as the query tree writes it.</returns>
    public SqlNode Condition(RestNode filter, RestTable target, SqlSource source)
    {
        _nodes = 0;
        var frame = new Frame { From = source, Table = target, Writes = true };
        frame.Owned[source] = target;
        return Predicate(filter, frame, negated: false);
    }

    /// <summary>
    /// Converts a value from a request to a column's type.
    /// </summary>
    /// <param name="value">The value: a string, number, bool or null from the request.</param>
    /// <param name="column">The column.</param>
    /// <returns>The value, of the column's type.</returns>
    /// <exception cref="ArgumentException">The value is not one of the column's type.</exception>
    public static object? Value(object? value, RestColumn column)
    {
        if (value is null)
        {
            return null;
        }

        var type = Nullable.GetUnderlyingType(column.Type) ?? column.Type;
        try
        {
            if (type.IsInstanceOfType(value))
            {
                return value;
            }

            var text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!;
            var invariant = System.Globalization.CultureInfo.InvariantCulture;
            return type switch
            {
                _ when type == typeof(string) => text,
                _ when type.IsEnum => Enum.Parse(type, text, ignoreCase: true),
                _ when type == typeof(Guid) => Guid.Parse(text),
                _ when type == typeof(bool) => value is bool b ? b : bool.Parse(text),
                _ when type == typeof(DateTime) => DateTime.Parse(text, invariant, System.Globalization.DateTimeStyles.RoundtripKind),
                _ when type == typeof(DateTimeOffset) => DateTimeOffset.Parse(text, invariant),
                _ when type == typeof(DateOnly) => DateOnly.Parse(text, invariant),
                _ when type == typeof(TimeOnly) => TimeOnly.Parse(text, invariant),
                _ when type == typeof(TimeSpan) => TimeSpan.Parse(text, invariant),
                _ when type == typeof(byte[]) => Convert.FromBase64String(text),
                _ => Convert.ChangeType(value is string ? text : value, type, invariant),
            };
        }
        catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException or ArgumentException)
        {
            throw new ArgumentException($"'{value}' is not a value of {column.Name}, a {type.Name}.", e);
        }
    }

    private static SqlLogical And(params SqlNode[] operands) => new("AND", operands);

    private static SqlLogical Or(params SqlNode[] operands) => new("OR", operands);

    private static string Inverse(string op) =>
        op switch { "eq" => "ne", "ne" => "eq", "lt" => "ge", "le" => "gt", "gt" => "le", _ => "lt" };

    private static string Mirror(string op) =>
        op switch { "lt" => "gt", "le" => "ge", "gt" => "lt", "ge" => "le", _ => op };

    private static string Sql(string op) =>
        op switch { "eq" => "=", "ne" => "<>", "lt" => "<", "le" => "<=", "gt" => ">", _ => ">=" };

    private static bool IsComparison(string op) => op is "eq" or "ne" or "lt" or "le" or "gt" or "ge";

    // A search: the database's full-text index where the table has one, or every word contained, ignoring case, in
    // any of the searchable columns.
    private SqlNode Search(RestSearch search, Frame frame)
    {
        var table = frame.Table;
        var searchable = table.Searchable ?? throw new ArgumentException($"$search searches tables declared searchable, and {table.Name} is not.");
        var words = Words(search.Text);
        if (words.Count == 0)
        {
            return SqlText.Always(true);
        }

        // The request's words count toward its size as its conditions do: one per word and column matched by contains,
        // one per word given to an index.
        Count(searchable.Native is null ? (long)words.Count * searchable.Columns.Count : words.Count);
        if (searchable.Native is { } native)
        {
            var key = _dialect.ProviderType == DbProviderType.Sqlite ? "rowid" : table.Keys.Single();
            var text = searchable.Query switch
            {
                RestSearchQuery.MySqlBoolean => string.Join(
                    ' ',
                    words
                        .Select(w => new string([.. w.Where(c => char.IsLetterOrDigit(c) || c is '_' or ' ')]).Trim())
                        .Where(w => w.Length > 0)
                        .Select(w => w.Contains(' ', StringComparison.Ordinal) ? $"+\"{w}\"" : $"+{w}*")
                ),
                RestSearchQuery.Fts5 => string.Join(" AND ", words.Select(w => "\"" + w.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"*")),
                _ => search.Text,
            };
            return new SqlRaw(native, [new SqlColumn(frame.From, key), new SqlValue(typeof(string), text)]);
        }

        // Every word, in any column: contains(tolower(column), word), as the query language writes it.
        RestNode condition = words
            .Select(word => searchable.Columns
                .Select(c => (RestNode)new RestCall("contains", [new RestCall("tolower", [new RestPath([c])]), new RestLiteral(word.ToLowerInvariant())]))
                .Aggregate((a, b) => new RestBinary("or", a, b)))
            .Aggregate((a, b) => new RestBinary("and", a, b));
        _trusted++;
        try
        {
            return Predicate(condition, frame, negated: false);
        }
        finally
        {
            _trusted--;
        }
    }

    // The words of a search: a phrase in double quotes is one.
    private static List<string> Words(string text)
    {
        var words = new List<string>();
        var parts = text.Split('"');
        for (var i = 0; i < parts.Length; i++)
        {
            if (i % 2 == 1)
            {
                if (parts[i].Trim() is { Length: > 0 } phrase)
                {
                    words.Add(phrase);
                }
            }
            else
            {
                words.AddRange(parts[i].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
        }

        return words;
    }

    private static bool IsNumeric(Type type) =>
        IsIntegral(type) || type == typeof(decimal) || type == typeof(double) || type == typeof(float);

    private static bool IsIntegral(Type type) =>
        (Nullable.GetUnderlyingType(type) ?? type) is var t
        && (t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte));

    private static SqlNode KeyIn(SqlSource source, IReadOnlyList<RestColumn> columns, IReadOnlyList<object?[]> keys)
    {
        if (columns.Count == 1)
        {
            return new SqlIn(new SqlColumn(source, columns[0].Name), [.. keys.Select(k => new SqlValue(columns[0].Type, k[0]))], Negated: false);
        }

        return new SqlLogical(
            "OR",
            [
                .. keys.Select(k => (SqlNode)new SqlLogical(
                    "AND",
                    [.. columns.Select((c, i) => (SqlNode)new SqlCompare(new SqlColumn(source, c.Name), "=", new SqlValue(c.Type, k[i])))]
                )),
            ]
        );
    }

    private Frame Open(RestTable table)
    {
        var frame = new Frame { From = Source(table), Table = table };
        frame.Owned[frame.From] = table;
        return frame;
    }

    private SqlSource Source(RestTable table) => new(table.QualifiedName, $"t{_nextAlias++}", table.ToTable(), null);

    private int? Top(RestQuery query) =>
        query.Top is { } top && (top > _limits.MaxTop || top < 0)
            ? throw new ArgumentException($"$top is 0 to {_limits.MaxTop}.")
            : query.Skip is < 0
                ? throw new ArgumentException("$skip is 0 or more.")
                : query.Top;

    private List<SqlSelectItem> Items(RestQuery query, RestTable table, SqlSource source, IReadOnlyList<string> extraColumns)
    {
        var names = query.Select.Count == 0
            ? table.Columns.Select(c => c.Name).ToList()
            : [.. query.Select.Select(n => table.Column(n, "$select").Name)];
        foreach (var extra in extraColumns.Select(e => table.Column(e, "a relation's keys").Name))
        {
            if (!names.Contains(extra, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(extra);
            }
        }

        return [.. names.Select(n => new SqlSelectItem(new SqlColumn(source, n), n))];
    }

    private List<SqlOrder> Order(RestQuery query, Frame frame) =>
        [
            .. query.OrderBy.Select(o =>
            {
                var operand = Operand(o.Key, frame);
                return operand.IsValue
                    ? throw new ArgumentException("$orderby orders by columns, not values.")
                    : new SqlOrder(operand.Node!, o.Descending, operand.Nullable);
            }),
        ];

    private void Count(RestNode node) => Count(1);

    private void Count(long parts)
    {
        if (_trusted > 0)
        {
            return;
        }

        if ((_nodes += parts) > _limits.MaxNodes)
        {
            throw new ArgumentException($"The request's conditions, orderings and values have more than {_limits.MaxNodes} parts.");
        }
    }

    private SqlNode Predicate(RestNode node, Frame frame, bool negated)
    {
        Count(node);
        switch (node)
        {
            case RestTrusted trusted:
                _trusted++;
                try
                {
                    return Predicate(trusted.Inner, frame, negated);
                }
                finally
                {
                    _trusted--;
                }

            case RestSearch search:
                {
                    var found = Search(search, frame);
                    return negated ? new SqlNot(found) : found;
                }

            case RestBinary { Operator: "and" } and:
                return negated
                    ? Or(Predicate(and.Left, frame, true), Predicate(and.Right, frame, true))
                    : And(Predicate(and.Left, frame, false), Predicate(and.Right, frame, false));
            case RestBinary { Operator: "or" } or:
                return negated
                    ? And(Predicate(or.Left, frame, true), Predicate(or.Right, frame, true))
                    : Or(Predicate(or.Left, frame, false), Predicate(or.Right, frame, false));
            case RestNot not:
                return Predicate(not.Operand, frame, !negated);
            case RestBinary binary when IsComparison(binary.Operator):
                return Compare(binary.Operator, binary.Left, binary.Right, frame, negated);
            case RestIn @in:
                return In(@in, frame, negated);
            case RestCall { Function: "contains" or "startswith" or "endswith" } call:
                return Like(call, frame, negated);
            case RestLambda { Kind: "any" or "all" } lambda:
                return Lambda(lambda, frame, negated);
            case RestLiteral literal when literal.Value is bool value:
                return SqlText.Always(value != negated);
            case RestPath path:
                {
                    var operand = Operand(path, frame);
                    var type = Nullable.GetUnderlyingType(operand.Column!.Type) ?? operand.Column.Type;
                    return type == typeof(bool)
                        ? new SqlCompare(operand.Node!, "=", new SqlValue(operand.Column.Type, !negated))
                        : throw new ArgumentException($"{path} is a {type.Name}, not a condition.");
                }

            default:
                throw new ArgumentException($"'{node}' is a value, not a condition.");
        }
    }

    private SqlNode Compare(string op, RestNode leftNode, RestNode rightNode, Frame frame, bool negated)
    {
        if (negated)
        {
            op = Inverse(op);
        }

        var left = Operand(leftNode, frame);
        var right = Operand(rightNode, frame);
        if (left.IsValue && !right.IsValue)
        {
            (left, right) = (right, left);
            op = Mirror(op);
        }

        if (left.IsValue)
        {
            throw new ArgumentException("A comparison compares a column with a value or another column, not two values.");
        }

        // As the typed methods do: in C#, comparing with a null is false for ==, true for !=, and false for an
        // ordered comparison, which is true once negated; SQL makes all of them unknown.
        var nullMakesTrue = op == "ne" || (negated && op != "eq" && op != "ne");
        if (right.IsValue)
        {
            if (right.Value is null)
            {
                return op switch
                {
                    "eq" => new SqlIsNull(left.Node!, Negated: false),
                    "ne" => new SqlIsNull(left.Node!, Negated: true),
                    _ => SqlText.Always(negated),
                };
            }

            var value = left.Column is { } column ? new SqlValue(column.Type, Value(right.Value, column)) : new SqlValue(right.Value.GetType(), right.Value);
            var comparison = new SqlCompare(left.Node!, Sql(op), value);
            return left.Nullable && nullMakesTrue ? Or(comparison, new SqlIsNull(left.Node!, Negated: false)) : comparison;
        }

        var both = new SqlCompare(left.Node!, Sql(op), right.Node!);
        SqlNode IsNull(Side o) => new SqlIsNull(o.Node!, Negated: false);
        SqlNode IsNotNull(Side o) => new SqlIsNull(o.Node!, Negated: true);
        return op switch
        {
            "eq" when left.Nullable && right.Nullable => Or(both, And(IsNull(left), IsNull(right))),
            "ne" when left.Nullable && right.Nullable => Or(both, And(IsNull(left), IsNotNull(right)), And(IsNotNull(left), IsNull(right))),
            _ when nullMakesTrue && (left.Nullable || right.Nullable) =>
                new SqlLogical("OR", [both, .. left.Nullable ? [IsNull(left)] : Array.Empty<SqlNode>(), .. right.Nullable ? [IsNull(right)] : Array.Empty<SqlNode>()]),
            _ => both,
        };
    }

    private SqlNode In(RestIn @in, Frame frame, bool negated)
    {
        var operand = Operand(@in.Operand, frame);
        if (operand.IsValue || operand.Column is not { } column)
        {
            throw new ArgumentException("in tests a column, as status in ('open', 'held').");
        }

        // A policy's list, however long, is not the request's to pay for.
        if (_trusted == 0)
        {
            _nodes += @in.Values.Count - 1;
        }

        Count(@in);
        var hasNull = @in.Values.Any(v => v.Value is null);
        List<SqlValue> values = [.. @in.Values.Where(v => v.Value is not null).Select(v => new SqlValue(column.Type, Value(v.Value, column)))];
        if (!negated)
        {
            SqlNode? match = values.Count > 0 ? new SqlIn(operand.Node!, values, Negated: false) : null;
            return (match, hasNull) switch
            {
                (null, false) => SqlText.False,
                (null, true) => new SqlIsNull(operand.Node!, Negated: false),
                (_, true) => Or(match, new SqlIsNull(operand.Node!, Negated: false)),
                _ => match,
            };
        }

        if (values.Count == 0)
        {
            return hasNull ? new SqlIsNull(operand.Node!, Negated: true) : SqlText.True;
        }

        var notIn = new SqlIn(operand.Node!, values, Negated: true);
        return operand.Nullable && !hasNull ? Or(notIn, new SqlIsNull(operand.Node!, Negated: false)) : notIn;
    }

    private SqlNode Like(RestCall call, Frame frame, bool negated)
    {
        if (call.Arguments.Count != 2 || call.Arguments[1] is not RestLiteral { Value: { } found })
        {
            throw new ArgumentException($"{call.Function} takes a column and a value, as {call.Function}(name, 'x').");
        }

        var operand = Operand(call.Arguments[0], frame);
        var value = Convert.ToString(found, System.Globalization.CultureInfo.InvariantCulture)!;
        var escape = LikeEscapes.FirstOrDefault(c => !value.Contains(c), LikeEscapes[0]);
        var escaped = new System.Text.StringBuilder(value.Length + 2);
        foreach (var c in value)
        {
            if (c == escape || c is '%' or '_' || (c == '[' && _dialect.ProviderType == DbProviderType.SqlServer))
            {
                escaped.Append(escape);
            }

            escaped.Append(c);
        }

        var pattern = call.Function switch
        {
            "startswith" => $"{escaped}%",
            "endswith" => $"%{escaped}",
            _ => $"%{escaped}%",
        };
        return new SqlLike(operand.Node!, new SqlValue(typeof(string), pattern), escape, negated);
    }

    // lines/any(l: ...), lines/all(l: ...), and lines/$count: subqueries of the related rows.
    private (SqlSelect Select, Frame Frame) Related(RestLambda lambda, Frame frame)
    {
        if (frame.Outputs is not null)
        {
            throw new ArgumentException("With $apply, $filter reads the groups' keys and aggregates, not related rows.");
        }

        var (owner, ownerTable, start) = Root(lambda.Collection.Segments, frame);
        var segments = lambda.Collection.Segments;
        for (var i = start; i < segments.Count - 1; i++)
        {
            (owner, ownerTable) = JoinOne(owner, ownerTable, segments[i], frame);
        }

        var relation = RelationOf(ownerTable, segments[^1]);
        if (!relation.Many)
        {
            throw new ArgumentException($"{relation.Name} is one row: compare its columns, as {relation.Name}/name eq 'x'.");
        }

        if (++_lambdaDepth > _limits.MaxLambdaDepth)
        {
            throw new ArgumentException($"any, all and $count are nested at most {_limits.MaxLambdaDepth} deep.");
        }

        var inner = new Frame { From = Source(relation.Other), Table = relation.Other, Parent = frame };
        inner.Owned[inner.From] = relation.Other;
        if (lambda.Variable is { } variable)
        {
            inner.Variables[variable] = (inner.From, relation.Other);
        }

        List<SqlNode> where =
        [
            .. relation.Keys.Select(k =>
                (SqlNode)new SqlCompare(
                    new SqlColumn(inner.From, relation.Other.Column(k.Other, relation.Name).Name),
                    "=",
                    new SqlColumn(owner, ownerTable.Column(k.This, relation.Name).Name)
                )
            ),
        ];
        // The related table's policy reads the related row's columns: its frame has no parent, so that a bare column
        // name never reaches the owner's row.
        if (_readFilter?.Invoke(relation.Other) is { } policy)
        {
            where.Add(Predicate(policy, new Frame { From = inner.From, Table = relation.Other }, negated: false));
        }

        return (new SqlSelect { From = inner.From, Joins = inner.Joins, Where = where }, inner);
    }

    private SqlNode Lambda(RestLambda lambda, Frame frame, bool negated)
    {
        var (select, inner) = Related(lambda, frame);
        var all = lambda.Kind == "all";
        if (lambda.Predicate is { } predicate)
        {
            // All holds when no row fails the predicate, which keeps a NULL as failing.
            select = select with { Where = [.. select.Where, Predicate(predicate, inner, negated: all)] };
        }

        _lambdaDepth--;
        var exists = new SqlExists(select);
        return all != negated ? new SqlNot(exists) : exists;
    }

    private Side Operand(RestNode node, Frame frame)
    {
        Count(node);
        switch (node)
        {
            case RestLiteral literal:
                return new Side { IsValue = true, Value = literal.Value };

            case RestPath path when frame.Outputs is { } outputs:
                return path.Segments.Count == 1 && outputs.TryGetValue(path.Segments[0], out var output)
                    ? output
                    : throw new ArgumentException($"With $apply, $filter and $orderby read the groups' keys and aggregates ({string.Join(", ", outputs.Keys)}), not {path}.");

            case RestPath path:
                {
                    var (source, table, start) = Root(path.Segments, frame);
                    var through = false;
                    for (var i = start; i < path.Segments.Count - 1; i++)
                    {
                        (source, table) = JoinOne(source, table, path.Segments[i], frame);
                        through = true;
                    }

                    if (start == path.Segments.Count)
                    {
                        throw new ArgumentException($"{path} is a row, not a column: compare its columns, as {path}/id eq 1.");
                    }

                    var column = table.Column(path.Segments[^1], path.ToString());
                    return new Side { Node = new SqlColumn(source, column.Name), Column = column, Nullable = column.Nullable || through };
                }

            case RestLambda { Kind: "$count" } count:
                {
                    var (select, inner) = Related(count, frame);
                    if (count.Predicate is { } predicate)
                    {
                        // The JSON tree's count of the related rows that match: {"count": {"path": ..., "where": ...}}.
                        select = select with { Where = [.. select.Where, Predicate(predicate, inner, negated: false)] };
                    }

                    _lambdaDepth--;
                    return new Side { Node = new SqlScalar(select with { Items = [new SqlSelectItem(new SqlAggregate("COUNT", null), null)] }) };
                }

            case RestCall { Function: "tolower" or "toupper" or "trim" or "length" } call when call.Arguments.Count == 1:
                {
                    var argument = Operand(call.Arguments[0], frame);
                    SqlNode function = call.Function switch
                    {
                        "tolower" => new SqlFunction("LOWER", [argument.Node!]),
                        "toupper" => new SqlFunction("UPPER", [argument.Node!]),
                        "trim" => new SqlDialectFunction(DialectFunction.Trim, [argument.Node!], "both"),
                        _ => new SqlDialectFunction(DialectFunction.Length, [argument.Node!]),
                    };
                    var type = call.Function == "length" ? typeof(int) : typeof(string);
                    return new Side { Node = function, Nullable = argument.Nullable, Column = new RestColumn(call.Function, type, argument.Nullable) };
                }

            case RestCall { Function: "year" or "month" or "day" or "hour" or "minute" or "second" } part when part.Arguments.Count == 1:
                {
                    var argument = Operand(part.Arguments[0], frame);
                    var name = char.ToUpperInvariant(part.Function[0]) + part.Function[1..];
                    return new Side
                    {
                        Node = new SqlDialectFunction(DialectFunction.DatePart, [argument.Node!], name),
                        Nullable = argument.Nullable,
                        Column = new RestColumn(part.Function, typeof(int), argument.Nullable),
                    };
                }

            case RestBinary { Operator: "add" or "sub" or "mul" or "div" or "mod" } arithmetic:
                {
                    var left = Operand(arithmetic.Left, frame);
                    var right = Operand(arithmetic.Right, frame);
                    var type = left.Column?.Type ?? right.Column?.Type ?? typeof(decimal);
                    static SqlNode Of(Side o) => o.IsValue ? new SqlValue(o.Value?.GetType() ?? typeof(decimal), o.Value) : o.Node!;
                    SqlNode value = arithmetic.Operator == "div" && IsIntegral(left.Column?.Type ?? typeof(decimal)) && IsIntegral(right.Column?.Type ?? right.Value?.GetType() ?? typeof(decimal))
                        ? new SqlDialectFunction(DialectFunction.IntegerDivide, [Of(left), Of(right)])
                        : new SqlArithmetic(Of(left), arithmetic.Operator switch { "add" => "+", "sub" => "-", "mul" => "*", "div" => "/", _ => "%" }, Of(right));
                    var nullable = left.Nullable || right.Nullable;
                    return new Side { Node = value, Nullable = nullable, Column = new RestColumn(arithmetic.Operator, type, nullable) };
                }

            default:
                throw new ArgumentException($"'{node}' is a condition or a function that is not supported here, not a value.");
        }
    }

    // The row a path starts from: its lambda variable's, or the query's own row.
    private (SqlSource Source, RestTable Table, int Start) Root(IReadOnlyList<string> segments, Frame frame)
    {
        for (var f = frame; f is not null; f = f.Parent)
        {
            if (f.Variables.TryGetValue(segments[0], out var bound))
            {
                return (bound.Source, bound.Table, 1);
            }
        }

        var root = frame;
        while (root.Parent is not null)
        {
            root = root.Parent;
        }

        return (root.From, root.Table, 0);
    }

    private RestRelation RelationOf(RestTable table, string name) =>
        _relations.TryGetValue((table, name), out var relation)
            ? relation
            : throw new ArgumentException($"'{name}' is not a column or relation of {table.Name} that requests may use.");

    // A to-one relation's row, joined once where its owner is read; a statement that writes joins nothing, so there it
    // is a subquery's row instead.
    private (SqlSource Source, RestTable Table) JoinOne(SqlSource owner, RestTable ownerTable, string name, Frame frame)
    {
        var relation = RelationOf(ownerTable, name);
        if (relation.Many)
        {
            throw new ArgumentException($"{name} holds many rows: test them with {name}/any(x: ...), {name}/all(x: ...) or {name}/$count.");
        }

        var scope = frame;
        while (scope is not null && !scope.Owned.ContainsKey(owner))
        {
            scope = scope.Parent;
        }

        scope ??= frame;
        if (scope.Writes)
        {
            throw new ArgumentException($"A write's condition compares its table's own columns; {name}'s are another table's.");
        }

        if (scope.Joined.TryGetValue((owner, name), out var joined))
        {
            return joined;
        }

        var other = Source(relation.Other);
        List<SqlNode> on =
        [
            .. relation.Keys.Select(k =>
                (SqlNode)new SqlCompare(new SqlColumn(other, relation.Other.Column(k.Other, name).Name), "=", new SqlColumn(owner, ownerTable.Column(k.This, name).Name))
            ),
        ];
        // A related row the policies hide reads as missing: its condition, over the related row's own columns, is part
        // of the join.
        if (_readFilter?.Invoke(relation.Other) is { } policy)
        {
            on.Add(Predicate(policy, new Frame { From = other, Table = relation.Other }, negated: false));
        }

        scope.Joins.Add(new SqlJoin(other, "LEFT JOIN", on.Count == 1 ? on[0] : new SqlLogical("AND", on)));
        scope.Owned[other] = relation.Other;
        scope.Joined[(owner, name)] = (other, relation.Other);
        return (other, relation.Other);
    }

    // One side of a comparison: a column or a computed value, or a value.
    private readonly struct Side
    {
        public SqlNode? Node { get; init; }

        public RestColumn? Column { get; init; }

        public bool Nullable { get; init; }

        public bool IsValue { get; init; }

        public object? Value { get; init; }
    }

    // One SELECT's sources while it is translated.
    private sealed class Frame
    {
        public required SqlSource From { get; init; }

        public required RestTable Table { get; init; }

        public Frame? Parent { get; init; }

        public bool Writes { get; init; }

        public List<SqlJoin> Joins { get; } = [];

        public Dictionary<SqlSource, RestTable> Owned { get; } = new(ReferenceEqualityComparer.Instance);

        public Dictionary<(SqlSource Owner, string Name), (SqlSource Source, RestTable Table)> Joined { get; } = [];

        public Dictionary<string, (SqlSource Source, RestTable Table)> Variables { get; } = new(StringComparer.Ordinal);

        // The groups' keys and aggregates, by name, which a grouped query's own condition and order read.
        public IReadOnlyDictionary<string, Side>? Outputs { get; init; }
    }
}
