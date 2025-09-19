// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore.Models.Dtos;

/// <summary>
/// Represents a single filter condition.
/// </summary>
public class FilterConditionDto
{
    /// <summary>
    /// Gets or sets the column name to filter on.
    /// </summary>
    public string Column { get; set; } = default!;

    /// <summary>
    /// Gets or sets the filter operator (eq, neq, gt, gte, lt, lte, like, nlike, in, nin, isnull, notnull).
    /// </summary>
    public string Operator { get; set; } = default!;

    /// <summary>
    /// Gets or sets the filter value.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Converts the operator to SQL.
    /// </summary>
    /// <returns>The SQL operator string.</returns>
    public string ToSqlOperator()
    {
        return Operator switch
        {
            "eq" => "=",
            "neq" => "!=",
            "gt" => ">",
            "gte" => ">=",
            "lt" => "<",
            "lte" => "<=",
            "like" => "LIKE",
            "nlike" => "NOT LIKE",
            "in" => "IN",
            "nin" => "NOT IN",
            "isnull" => "IS NULL",
            "notnull" => "IS NOT NULL",
            _ => "="
        };
    }

    /// <summary>
    /// Determines if this operator requires a value.
    /// </summary>
    /// <returns>True if the operator requires a value; otherwise, false.</returns>
    public bool RequiresValue()
    {
        return Operator != "isnull" && Operator != "notnull";
    }
}