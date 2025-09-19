// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for query operations.
/// </summary>
public class QueryResponse : ResponseBase<QueryResultDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueryResponse"/> class.
    /// </summary>
    public QueryResponse()
        : base() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryResponse"/> class.
    /// </summary>
    /// <param name="result">The query result.</param>
    /// <param name="success">Indicates whether the operation was successful.</param>
    /// <param name="message">An optional message providing additional information.</param>
    public QueryResponse(QueryResultDto? result, bool success = true, string? message = null)
        : base(result!, success, message) { }
}
