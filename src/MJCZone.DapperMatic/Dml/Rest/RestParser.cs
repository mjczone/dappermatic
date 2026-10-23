// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Text;

namespace MJCZone.DapperMatic.Dml.Rest;

/// <summary>
/// Parses OData-shaped query options: <c>$filter=status eq 'open' and lines/any(l: l/qty gt 5)</c>,
/// <c>$orderby=placed_at desc</c>, <c>$select=id,total</c>, <c>$top</c>, <c>$skip</c>, <c>$count=true</c>, and
/// <c>$expand=lines($filter=qty gt 0;$top=5;$expand=product),customer</c>.
/// </summary>
/// <remarks>
/// Values are written as OData writes them, strings in single quotes with a quote doubled, and are converted to the
/// type of the column they are compared with: a date, a time or a GUID is written as a quoted string.
/// </remarks>
internal sealed class RestParser
{
    // Each parenthesis, not, minus sign, lambda and $expand nests a level of recursion, and a stack overflow cannot be
    // caught: a request nesting deeper than any real condition needs is refused.
    private const int MaxDepth = 64;

    private static readonly HashSet<string> Functions =
    [
        "contains", "startswith", "endswith", "tolower", "toupper", "trim", "length", "year", "month", "day", "hour", "minute", "second",
    ];

    private static readonly HashSet<string> Comparisons = ["eq", "ne", "gt", "ge", "lt", "le"];

    private static readonly HashSet<string> ExpandOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "$filter",
        "$select",
        "$orderby",
        "$top",
        "$skip",
        "$expand",
    };

    private readonly List<Token> _tokens;
    private int _position;
    private int _depth;

    private RestParser(string text)
    {
        _tokens = Tokenize(text);
    }

    private enum Kind
    {
        Parameter,
        Name,
        String,
        Number,
        Symbol,
        End,
    }

    private Token Peek => _tokens[_position];

    /// <summary>
    /// Parses a query's options, as a query string gives them.
    /// </summary>
    /// <param name="options">The options, by name: <c>$filter</c>, <c>$select</c> and the rest; others are ignored.</param>
    /// <returns>The query.</returns>
    /// <exception cref="ArgumentException">An option is malformed.</exception>
    public static RestQuery Parse(IReadOnlyDictionary<string, string> options) => Parse(options, 0);

    /// <summary>
    /// Parses a grouping and its aggregates, as OData's <c>$apply</c>:
    /// <c>filter(status eq 'open')/groupby((customer_id),aggregate(amount with sum as total,$count as orders))</c>.
    /// </summary>
    /// <param name="text">The transformations.</param>
    /// <returns>The grouping.</returns>
    /// <exception cref="ArgumentException">It is malformed, or not one this API supports.</exception>
    public static RestApply ParseApply(string text)
    {
        var parser = new RestParser(text);
        RestNode? filter = null;
        List<RestPath>? groupBy = null;
        List<RestAggregate>? aggregates = null;
        do
        {
            if (groupBy is not null || aggregates is not null)
            {
                throw new ArgumentException("$apply groups once, last: filter(...) steps, then groupby(...) or aggregate(...).");
            }

            var step = parser.Expect(Kind.Name, "filter, groupby or aggregate").Text.ToLowerInvariant();
            parser.Expect("(");
            switch (step)
            {
                case "filter":
                    var condition = parser.Or();
                    filter = filter is null ? condition : new RestBinary("and", filter, condition);
                    break;
                case "groupby":
                    parser.Expect("(");
                    groupBy = [];
                    do
                    {
                        groupBy.Add(parser.Add() as RestPath ?? throw new ArgumentException("groupby groups by columns, as groupby((customer_id))."));
                    }
                    while (parser.Accept(","));

                    parser.Expect(")");
                    aggregates = [];
                    if (parser.Accept(","))
                    {
                        if (!parser.AcceptKeyword("aggregate"))
                        {
                            throw new ArgumentException("groupby's second part is aggregate(...).");
                        }

                        parser.Expect("(");
                        aggregates = parser.Aggregates();
                        parser.Expect(")");
                    }

                    break;
                case "aggregate":
                    groupBy = [];
                    aggregates = parser.Aggregates();
                    break;
                default:
                    throw new ArgumentException($"$apply has filter, groupby and aggregate, not {step}.");
            }

            parser.Expect(")");
        }
        while (parser.Accept("/"));

        parser.Expect(Kind.End, "the end of $apply");
        if (groupBy is null)
        {
            throw new ArgumentException("$apply ends with groupby(...) or aggregate(...).");
        }

        var names = groupBy.Select(g => g.Segments[^1]).Concat(aggregates!.Select(a => a.Alias)).ToList();
        if (names.GroupBy(n => n, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1) is { } twice)
        {
            throw new ArgumentException($"$apply names {twice.Key} twice in its results.");
        }

        return new RestApply { Filter = filter, GroupBy = groupBy, Aggregates = aggregates! };
    }

    /// <summary>
    /// Parses a condition.
    /// </summary>
    /// <param name="text">The condition, as <c>$filter</c> takes it.</param>
    /// <returns>The condition.</returns>
    /// <exception cref="ArgumentException">It is malformed.</exception>
    public static RestNode ParseFilter(string text)
    {
        var parser = new RestParser(text);
        var node = parser.Or();
        parser.Expect(Kind.End, "the end of the condition");
        return node;
    }

    /// <summary>
    /// Parses ordering keys: <c>placed_at desc, id</c>.
    /// </summary>
    /// <param name="text">The keys, as <c>$orderby</c> takes them.</param>
    /// <returns>The keys.</returns>
    /// <exception cref="ArgumentException">They are malformed.</exception>
    public static List<RestOrder> ParseOrderBy(string text)
    {
        var parser = new RestParser(text);
        var keys = new List<RestOrder>();
        do
        {
            var key = parser.Add();
            var descending = false;
            if (parser.Peek is { Kind: Kind.Name } direction && direction.Text is "asc" or "desc")
            {
                descending = direction.Text == "desc";
                parser._position++;
            }

            keys.Add(new RestOrder(key, descending));
        }
        while (parser.Accept(","));

        parser.Expect(Kind.End, "the end of $orderby");
        return keys;
    }

    /// <summary>
    /// Parses relations to read with each row, and their options.
    /// </summary>
    /// <param name="text">The relations, as <c>$expand</c> takes them.</param>
    /// <returns>The relations.</returns>
    /// <exception cref="ArgumentException">They are malformed.</exception>
    public static List<RestExpand> ParseExpand(string text) => ParseExpand(text, 0);

    private static RestQuery Parse(IReadOnlyDictionary<string, string> options, int depth)
    {
        ArgumentNullException.ThrowIfNull(options);
        string? Option(string name) =>
            options.FirstOrDefault(o => string.Equals(o.Key, name, StringComparison.OrdinalIgnoreCase)).Value is { Length: > 0 } value ? value : null;

        return new RestQuery
        {
            Filter = Option("$filter") is { } filter ? ParseFilter(filter) : null,
            Select = Option("$select") is { } select ? Names(select, "$select") : [],
            OrderBy = Option("$orderby") is { } orderBy ? ParseOrderBy(orderBy) : [],
            Top = Option("$top") is { } top ? Count(top, "$top") : null,
            Skip = Option("$skip") is { } skip ? Count(skip, "$skip") : null,
            Count = Option("$count") is { } count && bool.TryParse(count, out var counted) ? counted
                : Option("$count") is not null ? throw new ArgumentException("$count is true or false.") : false,
            Expand = Option("$expand") is { } expand ? ParseExpand(expand, depth) : [],
            After = depth == 0 ? Option("$after") : null,
            Apply = depth == 0 && Option("$apply") is { } apply ? ParseApply(apply) : null,
            Search = depth == 0 ? Option("$search") : null,
        };
    }

    private static List<RestExpand> ParseExpand(string text, int depth)
    {
        if (depth >= MaxDepth)
        {
            throw new ArgumentException($"$expand nests more than {MaxDepth} levels deep.");
        }

        var expands = new List<RestExpand>();
        foreach (var item in Split(text, ','))
        {
            var open = item.IndexOf('(', StringComparison.Ordinal);
            var name = (open < 0 ? item : item[..open]).Trim();
            if (!IsName(name))
            {
                throw new ArgumentException($"$expand names relations, and '{name}' is not one.");
            }

            var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (open >= 0)
            {
                if (!item.EndsWith(')'))
                {
                    throw new ArgumentException($"$expand's options for {name} are not closed with ')'.");
                }

                foreach (var option in Split(item[(open + 1)..^1], ';'))
                {
                    var equals = option.IndexOf('=', StringComparison.Ordinal);
                    if (equals <= 0)
                    {
                        throw new ArgumentException($"$expand's option '{option}' for {name} is not name=value.");
                    }

                    options[option[..equals].Trim()] = option[(equals + 1)..].Trim();
                }
            }

            if (options.Keys.FirstOrDefault(k => !ExpandOptions.Contains(k)) is { } unknown)
            {
                throw new ArgumentException($"$expand's options are $filter, $select, $orderby, $top, $skip and $expand, not {unknown}.");
            }

            expands.Add(new RestExpand(name, Parse(options, depth + 1)));
        }

        return expands;
    }

    private static List<string> Names(string text, string option) =>
        [
            .. text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(n => IsName(n) ? n : throw new ArgumentException($"{option} names columns, and '{n}' is not one.")),
        ];

    private static int Count(string text, string option) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var count)
            ? count
            : throw new ArgumentException($"{option} is a whole number, not '{text}'.");

    private static bool IsName(string text) =>
        text.Length > 0 && (char.IsLetter(text[0]) || text[0] == '_') && text.All(c => char.IsLetterOrDigit(c) || c == '_');

    // Splits on a separator outside parentheses and quotes.
    private static List<string> Split(string text, char separator)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        var depth = 0;
        var quoted = false;
        foreach (var c in text)
        {
            if (c == '\'')
            {
                quoted = !quoted;
            }
            else if (!quoted && c == '(')
            {
                depth++;
            }
            else if (!quoted && c == ')')
            {
                depth--;
            }
            else if (!quoted && depth == 0 && c == separator)
            {
                parts.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0)
        {
            parts.Add(current.ToString().Trim());
        }

        return [.. parts.Where(p => p.Length > 0)];
    }

    private static List<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
            }
            else if (c == '\'')
            {
                var value = new StringBuilder();
                var start = i++;
                while (true)
                {
                    if (i >= text.Length)
                    {
                        throw new ArgumentException($"The string starting at {start} is not closed with a quote.");
                    }

                    if (text[i] == '\'')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '\'')
                        {
                            value.Append('\'');
                            i += 2;
                            continue;
                        }

                        i++;
                        break;
                    }

                    value.Append(text[i++]);
                }

                tokens.Add(new Token(Kind.String, value.ToString(), start));
            }
            else if (char.IsDigit(c))
            {
                var start = i;
                while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.'))
                {
                    i++;
                }

                if (i < text.Length && text[i] is 'e' or 'E')
                {
                    i++;
                    if (i < text.Length && text[i] is '+' or '-')
                    {
                        i++;
                    }

                    while (i < text.Length && char.IsDigit(text[i]))
                    {
                        i++;
                    }
                }

                tokens.Add(new Token(Kind.Number, text[start..i], start));
            }
            else if (char.IsLetter(c) || c is '_' or '$')
            {
                var start = i++;
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_'))
                {
                    i++;
                }

                tokens.Add(new Token(Kind.Name, text[start..i], start));
            }
            else if (c == '@')
            {
                // A value of the request's context, as policies read them: @tenant.id, @tenant.below, @claims.region.
                var start = i++;
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] is '_' or '.'))
                {
                    i++;
                }

                if (i == start + 1)
                {
                    throw new ArgumentException($"'@' at {start} is not followed by a value's name, as @tenant.id.");
                }

                tokens.Add(new Token(Kind.Parameter, text[(start + 1)..i], start));
            }
            else if (c is '(' or ')' or ',' or '/' or ':' or '-')
            {
                tokens.Add(new Token(Kind.Symbol, c.ToString(), i++));
            }
            else
            {
                throw new ArgumentException($"'{c}' at {i} is not part of a condition.");
            }
        }

        tokens.Add(new Token(Kind.End, string.Empty, text.Length));
        return tokens;
    }

    private Token Next() => _tokens[_position++];

    // amount with sum as total, $count as orders, ...
    private List<RestAggregate> Aggregates()
    {
        var aggregates = new List<RestAggregate>();
        do
        {
            if (Peek is { Kind: Kind.Name } count && string.Equals(count.Text, "$count", StringComparison.OrdinalIgnoreCase))
            {
                _position++;
                aggregates.Add(new RestAggregate(null, "count", Alias()));
                continue;
            }

            var value = Add();
            if (!AcceptKeyword("with"))
            {
                throw new ArgumentException($"An aggregate says how, as amount with sum as total; expected 'with' at {Peek.Position}.");
            }

            var method = Expect(Kind.Name, "sum, min, max, average or countdistinct").Text.ToLowerInvariant();
            if (method != "sum" && method != "min" && method != "max" && method != "average" && method != "countdistinct")
            {
                throw new ArgumentException($"An aggregate is sum, min, max, average or countdistinct, not {method}.");
            }

            aggregates.Add(new RestAggregate(value, method, Alias()));
        }
        while (Accept(","));

        return aggregates;
    }

    private string Alias()
    {
        if (!AcceptKeyword("as"))
        {
            throw new ArgumentException($"An aggregate is named, as amount with sum as total; expected 'as' at {Peek.Position}.");
        }

        var alias = Expect(Kind.Name, "the aggregate's name").Text;
        return IsName(alias) ? alias : throw new ArgumentException($"'{alias}' is not a name for an aggregate.");
    }

    private bool Accept(string symbol)
    {
        if (Peek.Text == symbol && Peek.Kind is Kind.Symbol or Kind.Name)
        {
            _position++;
            return true;
        }

        return false;
    }

    private Token Expect(Kind kind, string what)
    {
        var token = Next();
        return token.Kind == kind ? token : throw new ArgumentException($"Expected {what} at {token.Position}, not '{token.Text}'.");
    }

    private void Expect(string symbol)
    {
        var token = Next();
        if (token.Text != symbol)
        {
            throw new ArgumentException($"Expected '{symbol}' at {token.Position}, not '{token.Text}'.");
        }
    }

    private bool AcceptKeyword(string keyword)
    {
        if (Peek is { Kind: Kind.Name } token && string.Equals(token.Text, keyword, StringComparison.OrdinalIgnoreCase))
        {
            _position++;
            return true;
        }

        return false;
    }

    // A level of nesting, refused past MaxDepth.
    private RestNode Deeper(Func<RestNode> parse)
    {
        if (++_depth > MaxDepth)
        {
            throw new ArgumentException($"The condition nests more than {MaxDepth} levels deep.");
        }

        try
        {
            return parse();
        }
        finally
        {
            _depth--;
        }
    }

    private RestNode Or() => Deeper(OrTerms);

    private RestNode OrTerms()
    {
        var left = And();
        while (AcceptKeyword("or"))
        {
            left = new RestBinary("or", left, And());
        }

        return left;
    }

    private RestNode And()
    {
        var left = Not();
        while (AcceptKeyword("and"))
        {
            left = new RestBinary("and", left, Not());
        }

        return left;
    }

    private RestNode Not() => AcceptKeyword("not") ? new RestNot(Deeper(Not)) : Comparison();

    private RestNode Comparison()
    {
        var left = Add();
        if (Peek is { Kind: Kind.Name } op && Comparisons.Contains(op.Text.ToLowerInvariant()))
        {
            _position++;
            return new RestBinary(op.Text.ToLowerInvariant(), left, Add());
        }

        if (AcceptKeyword("in"))
        {
            // in @tenant.below: a list the context gives.
            if (Peek.Kind == Kind.Parameter)
            {
                return new RestIn(left, [], new RestParameter(Next().Text));
            }

            Expect("(");
            var values = new List<RestLiteral>();
            do
            {
                values.Add(Primary() as RestLiteral ?? throw new ArgumentException("in takes a list of values, as status in ('open', 'held')."));
            }
            while (Accept(","));

            Expect(")");
            return new RestIn(left, values);
        }

        return left;
    }

    private RestNode Add()
    {
        var left = Multiply();
        while (Peek is { Kind: Kind.Name } op && op.Text is "add" or "sub")
        {
            _position++;
            left = new RestBinary(op.Text, left, Multiply());
        }

        return left;
    }

    private RestNode Multiply()
    {
        var left = Unary();
        while (Peek is { Kind: Kind.Name } op && op.Text is "mul" or "div" or "mod")
        {
            _position++;
            left = new RestBinary(op.Text, left, Unary());
        }

        return left;
    }

    private RestNode Unary()
    {
        if (!Accept("-"))
        {
            return Primary();
        }

        var operand = Deeper(Unary);
        return operand is RestLiteral { Value: decimal d } ? new RestLiteral(-d)
            : operand is RestLiteral { Value: long l } ? new RestLiteral(-l)
            : new RestBinary("sub", new RestLiteral(0L), operand);
    }

    private RestNode Primary()
    {
        var token = Next();
        switch (token.Kind)
        {
            case Kind.String:
                return new RestLiteral(token.Text);
            case Kind.Parameter:
                return new RestParameter(token.Text);
            case Kind.Number:
                return !token.Text.Contains('.') && !token.Text.Contains('e') && !token.Text.Contains('E')
                    && long.TryParse(token.Text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var whole)
                        ? new RestLiteral(whole)
                    : decimal.TryParse(token.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                        ? new RestLiteral(number)
                    : throw new ArgumentException($"{token.Text} at {token.Position} is too large a number.");
            case Kind.Symbol when token.Text == "(":
                var inner = Or();
                Expect(")");
                return inner;
            case Kind.Name:
                switch (token.Text.ToLowerInvariant())
                {
                    case "true":
                        return new RestLiteral(true);
                    case "false":
                        return new RestLiteral(false);
                    case "null":
                        return new RestLiteral(null);
                }

                if (Functions.Contains(token.Text.ToLowerInvariant()) && Peek.Text == "(")
                {
                    _position++;
                    var arguments = new List<RestNode>();
                    if (!Accept(")"))
                    {
                        do
                        {
                            arguments.Add(Or());
                        }
                        while (Accept(","));

                        Expect(")");
                    }

                    return new RestCall(token.Text.ToLowerInvariant(), arguments);
                }

                return Path(token);
            default:
                throw new ArgumentException($"Expected a value, a column or '(' at {token.Position}, not '{token.Text}'.");
        }
    }

    // customer/country, lines/any(l: l/qty gt 5), lines/all(l: ...), lines/$count.
    private RestNode Path(Token first)
    {
        if (!IsName(first.Text))
        {
            throw new ArgumentException($"'{first.Text}' at {first.Position} is not a column.");
        }

        var segments = new List<string> { first.Text };
        while (Accept("/"))
        {
            var segment = Expect(Kind.Name, "a name after '/'");
            var kind = segment.Text.ToLowerInvariant();
            if (kind is "any" or "all" && Peek.Text == "(")
            {
                _position++;
                if (Accept(")"))
                {
                    return kind == "any"
                        ? new RestLambda("any", new RestPath(segments), null, null)
                        : throw new ArgumentException("all takes a condition, as lines/all(l: l/qty gt 0).");
                }

                var variable = Expect(Kind.Name, "the lambda's variable").Text;
                Expect(":");
                var predicate = Or();
                Expect(")");
                return new RestLambda(kind, new RestPath(segments), variable, predicate);
            }

            if (kind == "$count")
            {
                return new RestLambda("$count", new RestPath(segments), null, null);
            }

            if (!IsName(segment.Text))
            {
                throw new ArgumentException($"'{segment.Text}' at {segment.Position} is not a name.");
            }

            segments.Add(segment.Text);
        }

        return new RestPath(segments);
    }

    private readonly struct Token(Kind kind, string text, int position)
    {
        public Kind Kind { get; } = kind;

        public string Text { get; } = text;

        public int Position { get; } = position;
    }
}
