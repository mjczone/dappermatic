// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore.Models.Dtos;

/// <summary>
/// Represents the result of a query (table or view) with pagination information.
/// </summary>
public class QueryResultDto
{
    /// <summary>
    /// Gets or sets the query results as a list of dynamic objects.
    /// </summary>
    public IEnumerable<object> Data { get; set; } = [];

    /// <summary>
    /// Gets or sets the pagination information.
    /// </summary>
    public PaginationDto Pagination { get; set; } = new();

    /// <summary>
    /// Gets or sets the field information for the results.
    /// </summary>
    public IEnumerable<FieldDto> Fields { get; set; } = [];
}
