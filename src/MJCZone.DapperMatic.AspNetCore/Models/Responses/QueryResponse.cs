// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for query operations.
/// </summary>
public class QueryResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueryResponse"/> class.
    /// </summary>
    public QueryResponse()
    {
        Result = [];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryResponse"/> class.
    /// </summary>
    /// <param name="result">The list of query results.</param>
    public QueryResponse(IEnumerable<object> result)
    {
        Result = result;
    }

    /// <summary>
    /// Gets or sets the list of query results.
    /// </summary>
    public IEnumerable<object> Result { get; set; }

    /// <summary>
    /// Gets or sets the pagination information.
    /// </summary>
    public PaginationDto Pagination { get; set; } = new();

    /// <summary>
    /// Gets or sets the field information for the results.
    /// </summary>
    public IEnumerable<FieldDto> Fields { get; set; } = [];
}
