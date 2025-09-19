// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for check constraint operations.
/// </summary>
public class CheckConstraintResponse : ResponseBase<CheckConstraintDto?>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CheckConstraintResponse"/> class.
    /// </summary>
    public CheckConstraintResponse()
        : base(null) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckConstraintResponse"/> class.
    /// </summary>
    /// <param name="checkConstraint">The check constraint information.</param>
    public CheckConstraintResponse(CheckConstraintDto? checkConstraint)
        : base(checkConstraint) { }
}
