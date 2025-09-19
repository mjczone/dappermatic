// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for unique constraint list operations.
/// </summary>
public class UniqueConstraintListResponse : ResponseBase<IEnumerable<UniqueConstraintDto>>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UniqueConstraintListResponse"/> class.
    /// </summary>
    public UniqueConstraintListResponse()
        : base([]) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="UniqueConstraintListResponse"/> class.
    /// </summary>
    /// <param name="uniqueConstraints">The list of unique constraints.</param>
    public UniqueConstraintListResponse(IEnumerable<UniqueConstraintDto> uniqueConstraints)
        : base(uniqueConstraints) { }
}
