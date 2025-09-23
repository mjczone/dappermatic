// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for listing multiple check constraints.
/// </summary>
public class CheckConstraintListResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CheckConstraintListResponse"/> class.
    /// </summary>
    public CheckConstraintListResponse()
    {
        Result = [];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckConstraintListResponse"/> class.
    /// </summary>
    /// <param name="result">The list of views.</param>
    public CheckConstraintListResponse(IEnumerable<CheckConstraintDto> result)
    {
        Result = result;
    }

    /// <summary>
    /// Gets or sets the list of views.
    /// </summary>
    public IEnumerable<CheckConstraintDto> Result { get; set; }
}
