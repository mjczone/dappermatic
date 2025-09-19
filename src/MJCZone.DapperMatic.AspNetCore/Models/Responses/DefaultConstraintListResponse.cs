// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for default constraint list operations.
/// </summary>
public class DefaultConstraintListResponse : ResponseBase<IEnumerable<DefaultConstraintDto>>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultConstraintListResponse"/> class.
    /// </summary>
    public DefaultConstraintListResponse()
        : base([]) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultConstraintListResponse"/> class.
    /// </summary>
    /// <param name="defaultConstraints">The list of default constraints.</param>
    public DefaultConstraintListResponse(IEnumerable<DefaultConstraintDto> defaultConstraints)
        : base(defaultConstraints) { }
}
