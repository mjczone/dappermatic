// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for index operations.
/// </summary>
public class IndexResponse : ResponseBase<IndexDto?>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IndexResponse"/> class.
    /// </summary>
    public IndexResponse()
        : base(null) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexResponse"/> class.
    /// </summary>
    /// <param name="index">The index information.</param>
    public IndexResponse(IndexDto? index)
        : base(index) { }
}
