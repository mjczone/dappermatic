// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections;

namespace MJCZone.DapperMatic.Dml.Rest;

/// <summary>
/// The request's values in policies' conditions: which a condition reads, and the condition with them in place. Shared
/// by the REST engine and typed code run as a caller.
/// </summary>
internal static class RestValues
{
    /// <summary>
    /// Lists the paths of the request's values a condition reads, such as <c>tenant.id</c>.
    /// </summary>
    /// <param name="node">The condition.</param>
    /// <returns>The paths, in no order, repeated when read more than once.</returns>
    public static IEnumerable<string> Parameters(RestNode node)
    {
        var pending = new Stack<RestNode>([node]);
        while (pending.TryPop(out var current))
        {
            switch (current)
            {
                case RestParameter parameter:
                    yield return parameter.Path;
                    break;
                case RestIn @in:
                    if (@in.ValuesFrom is { } from)
                    {
                        yield return from.Path;
                    }

                    pending.Push(@in.Operand);
                    break;
                case RestBinary binary:
                    pending.Push(binary.Left);
                    pending.Push(binary.Right);
                    break;
                case RestNot not:
                    pending.Push(not.Operand);
                    break;
                case RestCall call:
                    call.Arguments.ToList().ForEach(pending.Push);
                    break;
                case RestLambda lambda when lambda.Predicate is not null:
                    pending.Push(lambda.Predicate);
                    break;
            }
        }
    }

    /// <summary>
    /// Puts the request's values in place. A value the request does not have is read as PostgreSQL reads a NULL in a
    /// policy: a test of it is unknown, which no row satisfies whether the test is negated or not, so that
    /// <c>tenant_id eq @tenant.id</c> holds for no row when there is no tenant (never for the rows whose tenant_id is
    /// NULL), and so does its negation. <c>@tenant.id eq null</c> tests whether there is one.
    /// </summary>
    /// <param name="node">The condition.</param>
    /// <param name="values">The request's values, by path; one missing or null is a value the request does not have.</param>
    /// <returns>The condition, with values in place.</returns>
    public static RestNode Substitute(RestNode node, IReadOnlyDictionary<string, object?> values) => Substitute(node, values, negated: false);

    private static RestNode Substitute(RestNode node, IReadOnlyDictionary<string, object?> values, bool negated) =>
        node switch
        {
            RestBinary { Operator: "and" or "or" } logical => logical with
            {
                Left = Substitute(logical.Left, values, negated),
                Right = Substitute(logical.Right, values, negated),
            },
            RestNot not => not with { Operand = Substitute(not.Operand, values, !negated) },
            RestBinary { Operator: "eq" or "ne", Left: RestParameter parameter, Right: RestLiteral { Value: null } } test =>
                new RestLiteral(Missing(values, parameter.Path) == (test.Operator == "eq")),
            RestBinary { Operator: "eq" or "ne", Left: RestLiteral { Value: null }, Right: RestParameter parameter } test =>
                new RestLiteral(Missing(values, parameter.Path) == (test.Operator == "eq")),

            // Under a negation the test is put as true, which the negation makes false.
            _ when Parameters(node).Any(p => Missing(values, p)) => new RestLiteral(negated),
            _ => Bind(node, values),
        };

    private static bool Missing(IReadOnlyDictionary<string, object?> values, string path) => values.GetValueOrDefault(path) is null or DBNull;

    private static RestNode Bind(RestNode node, IReadOnlyDictionary<string, object?> values) =>
        node switch
        {
            RestParameter parameter => values.GetValueOrDefault(parameter.Path) is IEnumerable and not string
                ? throw new ArgumentException($"@{parameter.Path} is a list: test it with in, as tenant_id in @{parameter.Path}.")
                : new RestLiteral(values.GetValueOrDefault(parameter.Path)),
            RestIn { ValuesFrom: { } from } @in => new RestIn(Bind(@in.Operand, values), Literals(values.GetValueOrDefault(from.Path))),
            RestIn @in => @in with { Operand = Bind(@in.Operand, values) },
            RestBinary binary => binary with { Left = Bind(binary.Left, values), Right = Bind(binary.Right, values) },
            RestNot not => not with { Operand = Bind(not.Operand, values) },
            RestCall call => call with { Arguments = [.. call.Arguments.Select(a => Bind(a, values))] },
            RestLambda { Predicate: { } predicate } lambda => lambda with { Predicate = Bind(predicate, values) },
            _ => node,
        };

    // A list value as in's values: none for null, one for a single value.
    private static List<RestLiteral> Literals(object? value) =>
        value switch
        {
            null => [],
            string text => [new RestLiteral(text)],
            IEnumerable list => [.. list.Cast<object?>().Select(v => new RestLiteral(v))],
            _ => [new RestLiteral(value)],
        };
}
