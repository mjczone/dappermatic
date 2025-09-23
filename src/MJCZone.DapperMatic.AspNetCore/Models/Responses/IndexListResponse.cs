// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for listing multiple indexes.
/// </summary>
public class IndexListResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IndexListResponse"/> class.
    /// </summary>
    public IndexListResponse()
    {
        Result = [];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexListResponse"/> class.
    /// </summary>
    /// <param name="result">The list of indexes.</param>
    public IndexListResponse(IEnumerable<IndexDto> result)
    {
        Result = result;
    }

    /// <summary>
    /// Gets or sets the list of indexes.
    /// </summary>
    public IEnumerable<IndexDto> Result { get; set; }
}
