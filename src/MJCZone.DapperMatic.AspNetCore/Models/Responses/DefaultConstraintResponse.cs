// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for default constraint operations.
/// </summary>
public class DefaultConstraintResponse : ResponseBase<DefaultConstraintDto?>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultConstraintResponse"/> class.
    /// </summary>
    public DefaultConstraintResponse()
        : base(null) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultConstraintResponse"/> class.
    /// </summary>
    /// <param name="defaultConstraint">The default constraint information.</param>
    public DefaultConstraintResponse(DefaultConstraintDto? defaultConstraint)
        : base(defaultConstraint) { }
}
