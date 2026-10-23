// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Linq.Expressions;

namespace MJCZone.DapperMatic.Dml.Internal;

/// <summary>
/// The aggregates of grouped rows, as <see cref="DmlGrouping"/> rewrites them: <c>COUNT(*)</c>, <c>SUM(x)</c>, ...,
/// with a group's <c>Where</c> as <c>CASE WHEN</c> inside the aggregate.
/// </summary>
internal sealed partial class DmlTranslator
{
    private Operand? GroupAggregate(Expression e)
    {
        if (!DmlGrouping.IsAggregate(e))
        {
            return null;
        }

        if (!_aggregates)
        {
            throw new DmTranslationException(
                e,
                "aggregates are read from groups, as GroupBy(o => o.CustomerId).Select(g => new { g.Key, Count = g.Count() })."
            );
        }

        var call = (MethodCallExpression)e;
        var function = (string)((ConstantExpression)call.Arguments[0]).Value!;
        RequireValueKept(call.Arguments[1]);
        var argument = StripConversions(call.Arguments[1]);

        // The rows' values, without aggregates of their own.
        var rows = new DmlTranslator(_sources, _scope, _relations);
        var value = argument is ConstantExpression { Value: null } ? null : rows.SqlOf(argument).Node;
        var filter = call.Arguments[2] is ConstantExpression { Value: true } ? null : rows.Predicate(call.Arguments[2]);
        if (SqlNode.HasSubquery(value) || SqlNode.HasSubquery(filter))
        {
            // SQL Server refuses an aggregate of a subquery; refused everywhere, so a query runs on every provider.
            throw new DmTranslationException(
                e,
                $"{function} is over a value or condition that reads related rows' aggregates or tests, which SQL Server "
                    + "refuses: aggregate the rows' columns, or those of a row they relate to."
            );
        }

        if (filter is not null)
        {
            value = new SqlCase(filter, value ?? new SqlText("1"), new SqlText("NULL"));
        }

        SqlNode aggregate = new SqlAggregate(function, value, WithFraction: function == "AVG" && IsIntegral(argument.Type));
        return function switch
        {
            // C#'s Sum of no values is 0, and SQL's NULL.
            "SUM" => Computed(new SqlFunction("COALESCE", [aggregate, new SqlText("0")]), nullable: false),
            "COUNT" => Computed(aggregate, nullable: false),
            _ => Computed(aggregate, nullable: true),
        };
    }
}
