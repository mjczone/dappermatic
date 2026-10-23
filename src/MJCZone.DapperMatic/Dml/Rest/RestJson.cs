// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Text.Json;

namespace MJCZone.DapperMatic.Dml.Rest;

/// <summary>
/// Reads a condition written as JSON, the same tree <c>$filter</c> parses to, for a request body:
/// <c>{"and": [{"eq": [{"path": "status"}, {"value": "open"}]}, {"any": {"path": "lines", "as": "l", "where": {"gt": [{"path": "l/qty"}, {"value": 5}]}}}]}</c>.
/// </summary>
/// <remarks>
/// Every node is an object of one property: <c>path</c>, <c>value</c>; <c>eq</c>, <c>ne</c>, <c>gt</c>, <c>ge</c>,
/// <c>lt</c>, <c>le</c>, <c>add</c>, <c>sub</c>, <c>mul</c>, <c>div</c>, <c>mod</c> with two operands; <c>and</c> and
/// <c>or</c> with any number; <c>not</c> with one; <c>in</c> with an operand and an array of values; a function by
/// name with its arguments; and <c>any</c>, <c>all</c> and <c>count</c> with <c>path</c>, and <c>as</c> and
/// <c>where</c>. Paths and values are never guessed from a bare string.
/// </remarks>
internal static class RestJson
{
    private static readonly HashSet<string> Binary = ["eq", "ne", "gt", "ge", "lt", "le", "add", "sub", "mul", "div", "mod"];

    private static readonly HashSet<string> Functions =
    [
        "contains", "startswith", "endswith", "tolower", "toupper", "trim", "length", "year", "month", "day", "hour", "minute", "second",
    ];

    /// <summary>
    /// Reads a grouping and its aggregates: <c>{"filter": node, "groupBy": ["customer_id"], "aggregate":
    /// [{"path": "amount", "with": "sum", "as": "total"}, {"with": "count", "as": "orders"}]}</c>.
    /// </summary>
    /// <param name="json">The grouping.</param>
    /// <returns>The grouping, as <c>$apply</c> gives it.</returns>
    /// <exception cref="ArgumentException">It is malformed.</exception>
    public static RestApply Apply(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("A grouping is an object of filter, groupBy and aggregate.");
        }

        RestNode? filter = null;
        var groupBy = new List<RestPath>();
        var aggregates = new List<RestAggregate>();
        foreach (var property in json.EnumerateObject())
        {
            switch (property.Name.ToLowerInvariant())
            {
                case "filter":
                    filter = Node(property.Value);
                    break;
                case "groupby":
                    groupBy.AddRange(
                        property.Value.ValueKind == JsonValueKind.Array
                            ? property.Value.EnumerateArray().Select(e => e.GetString() is { Length: > 0 } path ? new RestPath(path.Split('/')) : throw new ArgumentException("groupBy lists columns, as [\"customer_id\"]."))
                            : throw new ArgumentException("groupBy lists columns, as [\"customer_id\"].")
                    );
                    break;
                case "aggregate":
                    if (property.Value.ValueKind != JsonValueKind.Array)
                    {
                        throw new ArgumentException("aggregate lists aggregates, as [{\"path\": \"amount\", \"with\": \"sum\", \"as\": \"total\"}].");
                    }

                    foreach (var item in property.Value.EnumerateArray())
                    {
                        string? Text(string name) => item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                        var method = Text("with")?.ToLowerInvariant() ?? throw new ArgumentException("An aggregate says how, with \"with\": sum, min, max, average, countdistinct or count.");
                        var alias = Text("as") ?? throw new ArgumentException("An aggregate is named, with \"as\".");
                        var path = Text("path");
                        aggregates.Add(
                            method == "count" ? new RestAggregate(null, "count", alias)
                            : path is { Length: > 0 } && method is "sum" or "min" or "max" or "average" or "countdistinct" ? new RestAggregate(new RestPath(path.Split('/')), method, alias)
                            : throw new ArgumentException($"An aggregate {method} of {path} is not one: give a path, and sum, min, max, average or countdistinct.")
                        );
                    }

                    break;
                default:
                    throw new ArgumentException($"A grouping has filter, groupBy and aggregate, not {property.Name}.");
            }
        }

        return new RestApply { Filter = filter, GroupBy = groupBy, Aggregates = aggregates };
    }

    /// <summary>
    /// Reads a condition.
    /// </summary>
    /// <param name="json">The condition.</param>
    /// <returns>The node.</returns>
    /// <exception cref="ArgumentException">It is not a node.</exception>
    public static RestNode Node(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Object || json.EnumerateObject().Count() != 1)
        {
            throw new ArgumentException($"A condition's node is an object of one property, as {{\"eq\": [...]}}, not {json.GetRawText()}.");
        }

        var property = json.EnumerateObject().First();
        var name = property.Name.ToLowerInvariant();
        var value = property.Value;
        switch (name)
        {
            case "path":
                return value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } path
                    ? new RestPath(path.Split('/'))
                    : throw new ArgumentException("path is a string, as \"customer/country\".");
            case "value":
                return new RestLiteral(Scalar(value));
            case "and" or "or":
                {
                    var operands = Operands(value, name);
                    return operands.Count == 0
                        ? throw new ArgumentException($"{name} takes at least one condition.")
                        : operands.Skip(1).Aggregate(operands[0], (left, right) => new RestBinary(name, left, right));
                }

            case "not":
                return new RestNot(Node(value));
            case "in":
                {
                    if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != 2 || value[1].ValueKind != JsonValueKind.Array)
                    {
                        throw new ArgumentException("in takes an operand and an array of values, as {\"in\": [{\"path\": \"status\"}, [\"open\", \"held\"]]}.");
                    }

                    return new RestIn(Node(value[0]), [.. value[1].EnumerateArray().Select(v => new RestLiteral(Scalar(v)))]);
                }

            case "any" or "all" or "count":
                {
                    if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty("path", out var collection) || collection.GetString() is not { Length: > 0 } relationPath)
                    {
                        throw new ArgumentException($"{name} takes an object with the relation's path, as {{\"{name}\": {{\"path\": \"lines\", \"as\": \"l\", \"where\": ...}}}}.");
                    }

                    var variable = value.TryGetProperty("as", out var alias) ? alias.GetString() : null;
                    var predicate = value.TryGetProperty("where", out var where) ? Node(where) : null;
                    if (name == "all" && predicate is null)
                    {
                        throw new ArgumentException("all takes a where.");
                    }

                    return new RestLambda(name == "count" ? "$count" : name, new RestPath(relationPath.Split('/')), variable, predicate);
                }

            case var _ when Binary.Contains(name):
                {
                    var operands = Operands(value, name);
                    return operands.Count == 2
                        ? new RestBinary(name, operands[0], operands[1])
                        : throw new ArgumentException($"{name} takes two operands.");
                }

            case var _ when Functions.Contains(name):
                return new RestCall(name, Operands(value, name));
            default:
                throw new ArgumentException($"'{property.Name}' is not a node of a condition.");
        }
    }

    /// <summary>
    /// Reads a value of a request body: a string, number, bool or null.
    /// </summary>
    /// <param name="json">The value.</param>
    /// <returns>The value: a string, a long or decimal, a bool, or null.</returns>
    /// <exception cref="ArgumentException">It is an object or an array.</exception>
    public static object? Scalar(JsonElement json) =>
        json.ValueKind switch
        {
            JsonValueKind.String => json.GetString(),
            JsonValueKind.Number => json.TryGetInt64(out var whole) ? whole
                : json.TryGetDecimal(out var number) ? number
                : throw new ArgumentException($"{json.GetRawText()} is too large a number."),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => throw new ArgumentException($"A value is a string, a number, true, false or null, not {json.GetRawText()}."),
        };

    private static List<RestNode> Operands(JsonElement json, string name) =>
        json.ValueKind == JsonValueKind.Array
            ? [.. json.EnumerateArray().Select(Node)]
            : throw new ArgumentException($"{name} takes an array of operands.");
}
