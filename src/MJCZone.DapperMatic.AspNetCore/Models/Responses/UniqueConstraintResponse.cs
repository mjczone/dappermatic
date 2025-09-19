// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for unique constraint operations.
/// </summary>
public class UniqueConstraintResponse : ResponseBase<UniqueConstraintDto?>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UniqueConstraintResponse"/> class.
    /// </summary>
    public UniqueConstraintResponse()
        : base(null) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="UniqueConstraintResponse"/> class.
    /// </summary>
    /// <param name="uniqueConstraint">The unique constraint information.</param>
    public UniqueConstraintResponse(UniqueConstraintDto? uniqueConstraint)
        : base(uniqueConstraint) { }
}
