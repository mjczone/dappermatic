// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for index list operations.
/// </summary>
public class IndexListResponse : ResponseBase<IEnumerable<IndexDto>>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IndexListResponse"/> class.
    /// </summary>
    public IndexListResponse()
        : base([]) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexListResponse"/> class.
    /// </summary>
    /// <param name="indexes">The list of indexes.</param>
    public IndexListResponse(IEnumerable<IndexDto> indexes)
        : base(indexes) { }
}
