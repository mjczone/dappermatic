// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.Extensions.Primitives;

namespace MJCZone.DapperMatic.AspNetCore.Models.Dtos;

/// <summary>
/// /// Request model for querying tables or views with filtering, sorting, and pagination.
/// </summary>
public class QueryDto
{
    /// <summary>
    /// Gets or sets the number of records to take. Default is 100, max is 1000.
    /// </summary>
    [Range(1, 1000)]
    public int Take { get; set; } = 100;

    /// <summary>
    /// Gets or sets the number of records to skip for pagination.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int Skip { get; set; } = 0;

    /// <summary>
    /// Gets or sets a value indicating whether to include the total count in the response.
    /// </summary>
    public bool IncludeTotal { get; set; }

    /// <summary>
    /// Gets or sets the order by clause. Format: "column1.asc,column2.desc"
    /// </summary>
    public string? OrderBy { get; set; }

    /// <summary>
    /// Gets or sets the filter conditions. Dictionary where key is "column.operator" and value is the filter value.
    /// Supported operators: eq, neq, gt, gte, lt, lte, like, nlike, in, nin, isnull, notnull
    /// </summary>
    public Dictionary<string, string> Filters { get; set; } = new();

    /// <summary>
    /// Gets or sets the columns to select. If null or empty, selects all columns (*).
    /// Format: "column1,column2,column3"
    /// </summary>
    public string? Select { get; set; }

    /// <summary>
    /// Gets or sets a condition as OData's <c>$filter</c> writes it, ANDed with <see cref="Filters"/>:
    /// <c>status eq 'open' and customer/country in ('FR', 'DE') or lines/any(l: l/qty gt 5)</c>. Relations must be
    /// declared in <c>DapperMaticOptions.Relations</c>.
    /// </summary>
    public string? Filter { get; set; }

    /// <summary>
    /// Gets or sets a condition as a JSON tree, the one <see cref="Filter"/> parses to, ANDed with the others:
    /// <c>{"and": [{"eq": [{"path": "status"}, {"value": "open"}]}, ...]}</c>.
    /// </summary>
    public JsonElement? Where { get; set; }

    /// <summary>
    /// Gets or sets the order as OData's <c>$orderby</c> writes it, in place of <see cref="OrderBy"/>:
    /// <c>placed_at desc, customer/name</c>.
    /// </summary>
    public string? Order { get; set; }

    /// <summary>
    /// Gets or sets the relations to read with each row, as OData's <c>$expand</c> writes them:
    /// <c>lines($filter=qty gt 0;$orderby=id;$top=5;$expand=product),customer</c>.
    /// </summary>
    public string? Expand { get; set; }

    /// <summary>
    /// Gets or sets the cursor of the page this one follows, as a page's <c>pagination.nextCursor</c> gives it
    /// (<c>$after</c>): the rows after it, in the same order. Not with <see cref="Skip"/>.
    /// </summary>
    public string? After { get; set; }

    /// <summary>
    /// Gets or sets the rows' grouping and aggregates, as OData's <c>$apply</c> writes them:
    /// <c>filter(status eq 'open')/groupby((customer_id),aggregate(amount with sum as total,$count as orders))</c>. With it,
    /// the results are the groups, and the filter, order and page apply to them.
    /// </summary>
    public string? Apply { get; set; }

    /// <summary>
    /// Gets or sets the words the rows' searchable columns must hold (<c>$search</c>): every word, in any of the
    /// columns the table is declared searchable by; a phrase in double quotes is one.
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Gets or sets the grouping as a JSON tree, as <see cref="Where"/> is to <see cref="Filter"/>:
    /// <c>{"filter": node, "groupBy": ["customer_id"], "aggregate": [{"path": "amount", "with": "sum", "as": "total"}]}</c>.
    /// </summary>
    public JsonElement? ApplyTree { get; set; }

    /// <summary>
    /// Gets or sets the schema name. This is set internally from the route and not by the user.
    /// </summary>
    public string? SchemaName { get; set; }

    /// <summary>
    /// Parses the OrderBy string into a list of column and direction pairs.
    /// </summary>
    /// <returns>A list of column and direction pairs.</returns>
    public List<(string Column, bool IsAscending)> GetOrderByPairs()
    {
        var pairs = new List<(string, bool)>();

        if (string.IsNullOrWhiteSpace(OrderBy))
        {
            return pairs;
        }

        var parts = OrderBy.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            var orderParts = part.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (orderParts.Length == 2)
            {
                var column = orderParts[0];
                var isAscending = !orderParts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);
                pairs.Add((column, isAscending));
            }
            else if (orderParts.Length == 1)
            {
                // Default to ascending if no direction specified
                pairs.Add((orderParts[0], true));
            }
        }

        return pairs;
    }

    /// <summary>
    /// Parses the filter conditions into a structured format.
    /// </summary>
    /// <returns>A list of filter conditions.</returns>
    public List<FilterConditionDto> GetFilterConditions()
    {
        var conditions = new List<FilterConditionDto>();

        foreach (var filter in Filters)
        {
            var parts = filter.Key.Split(
                '.',
                2,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            );
            if (parts.Length == 2)
            {
                conditions.Add(
                    new FilterConditionDto
                    {
                        Column = parts[0],
                        Operator = parts[1].ToLowerInvariant(),
                        Value = filter.Value,
                    }
                );
            }
        }

        return conditions;
    }

    /// <summary>
    /// Gets the columns to select as a list.
    /// </summary>
    /// <returns>A list of column names to select.</returns>
    public List<string> GetSelectColumns()
    {
        if (string.IsNullOrWhiteSpace(Select))
        {
            return [];
        }

        return Select.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    /// <summary>
    /// Creates a QueryRequest from query string parameters.
    /// </summary>
    /// <param name="query">The query collection from HTTP request.</param>
    /// <returns>A QueryRequest instance populated from query parameters.</returns>
    public static QueryDto FromQueryParameters(IReadOnlyDictionary<string, StringValues> query)
    {
        var request = new QueryDto();

        // OData-shaped options: $filter, $orderby, $expand, $select, $top, $skip, $count.
        if (query.TryGetValue("$filter", out var filterValue) && !string.IsNullOrWhiteSpace(filterValue))
        {
            request.Filter = filterValue;
        }

        if (query.TryGetValue("$orderby", out var orderValue) && !string.IsNullOrWhiteSpace(orderValue))
        {
            request.Order = orderValue;
        }

        if (query.TryGetValue("$expand", out var expandValue) && !string.IsNullOrWhiteSpace(expandValue))
        {
            request.Expand = expandValue;
        }

        if (query.TryGetValue("$select", out var odataSelect) && !string.IsNullOrWhiteSpace(odataSelect))
        {
            request.Select = odataSelect;
        }

        if (query.TryGetValue("$top", out var topValue))
        {
            request.Take = int.TryParse(topValue, out var top) ? top : throw new ArgumentException("$top is a whole number.");
        }

        if (query.TryGetValue("$skip", out var odataSkip))
        {
            request.Skip = int.TryParse(odataSkip, out var skipped) ? skipped : throw new ArgumentException("$skip is a whole number.");
        }

        if (query.TryGetValue("$search", out var search) && !string.IsNullOrWhiteSpace(search))
        {
            request.Search = search;
        }

        if (query.TryGetValue("$apply", out var apply) && !string.IsNullOrWhiteSpace(apply))
        {
            request.Apply = apply;
        }

        if (query.TryGetValue("$after", out var after) && !string.IsNullOrWhiteSpace(after))
        {
            request.After = after;
        }

        if (query.TryGetValue("$count", out var odataCount))
        {
            request.IncludeTotal = bool.TryParse(odataCount, out var counted) ? counted : throw new ArgumentException("$count is true or false.");
        }

        // Parse pagination parameters
        if (query.TryGetValue("take", out var takeValue) && int.TryParse(takeValue, out var take))
        {
            request.Take = Math.Clamp(take, 1, 1000);
        }

        if (query.TryGetValue("skip", out var skipValue) && int.TryParse(skipValue, out var skip))
        {
            request.Skip = Math.Max(skip, 0);
        }

        if (query.TryGetValue("count", out var countValue) && bool.TryParse(countValue, out var count))
        {
            request.IncludeTotal = count;
        }

        // Parse sort parameter
        if (query.TryGetValue("sort", out var sortValue) && !string.IsNullOrWhiteSpace(sortValue))
        {
            var sortParts = new List<string>();
            var fields = sortValue
                .ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var field in fields)
            {
                if (field.StartsWith('-'))
                {
                    sortParts.Add($"{field[1..]}.desc");
                }
                else
                {
                    sortParts.Add($"{field}.asc");
                }
            }

            request.OrderBy = string.Join(",", sortParts);
        }

        // Parse select parameter
        if (query.TryGetValue("select", out var selectValue) && !string.IsNullOrWhiteSpace(selectValue))
        {
            request.Select = selectValue;
        }

        // Parse filter parameters (those with dots and valid operators)
        var validOperators = new HashSet<string>
        {
            "eq",
            "neq",
            "gt",
            "gte",
            "lt",
            "lte",
            "like",
            "nlike",
            "in",
            "nin",
            "isnull",
            "notnull",
        };

        foreach (var param in query)
        {
            if (param.Key.Contains('.', StringComparison.OrdinalIgnoreCase))
            {
                var parts = param.Key.Split('.');
                if (parts.Length == 2 && validOperators.Contains(parts[1].ToLowerInvariant()))
                {
                    request.Filters[param.Key] = param.Value.ToString();
                }
            }
        }

        return request;
    }
}
