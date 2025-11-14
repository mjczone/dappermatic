// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for listing multiple foreign key constraints.
/// </summary>
public class ForeignKeyListResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ForeignKeyListResponse"/> class.
    /// </summary>
    public ForeignKeyListResponse()
    {
        Result = [];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ForeignKeyListResponse"/> class.
    /// </summary>
    /// <param name="result">The list of foreign key constraints.</param>
    public ForeignKeyListResponse(IEnumerable<ForeignKeyConstraintDto> result)
    {
        Result = result;
    }

    /// <summary>
    /// Gets or sets the list of foreign key constraints.
    /// </summary>
    public IEnumerable<ForeignKeyConstraintDto> Result { get; set; }
}
