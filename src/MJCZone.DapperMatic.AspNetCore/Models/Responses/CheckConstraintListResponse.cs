// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for check constraint list operations.
/// </summary>
public class CheckConstraintListResponse : ResponseBase<IEnumerable<CheckConstraintDto>>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CheckConstraintListResponse"/> class.
    /// </summary>
    public CheckConstraintListResponse()
        : base([]) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckConstraintListResponse"/> class.
    /// </summary>
    /// <param name="checkConstraints">The list of check constraints.</param>
    public CheckConstraintListResponse(IEnumerable<CheckConstraintDto> checkConstraints)
        : base(checkConstraints) { }
}
