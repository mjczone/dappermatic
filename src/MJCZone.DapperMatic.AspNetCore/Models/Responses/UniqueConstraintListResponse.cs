// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for listing multiple unique constraints.
/// </summary>
public class UniqueConstraintListResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UniqueConstraintListResponse"/> class.
    /// </summary>
    public UniqueConstraintListResponse()
    {
        Result = [];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UniqueConstraintListResponse"/> class.
    /// </summary>
    /// <param name="result">The list of unique constraints.</param>
    public UniqueConstraintListResponse(IEnumerable<UniqueConstraintDto> result)
    {
        Result = result;
    }

    /// <summary>
    /// Gets or sets the list of unique constraints.
    /// </summary>
    public IEnumerable<UniqueConstraintDto> Result { get; set; }
}
