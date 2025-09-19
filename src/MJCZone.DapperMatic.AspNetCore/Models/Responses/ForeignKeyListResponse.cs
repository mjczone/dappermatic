// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for foreign key list operations.
/// </summary>
public class ForeignKeyListResponse : ResponseBase<IEnumerable<ForeignKeyConstraintDto>>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ForeignKeyListResponse"/> class.
    /// </summary>
    public ForeignKeyListResponse()
        : base([]) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ForeignKeyListResponse"/> class.
    /// </summary>
    /// <param name="foreignKeysConstraints">The list of foreign key constraints.</param>
    public ForeignKeyListResponse(IEnumerable<ForeignKeyConstraintDto> foreignKeysConstraints)
        : base(foreignKeysConstraints) { }
}
