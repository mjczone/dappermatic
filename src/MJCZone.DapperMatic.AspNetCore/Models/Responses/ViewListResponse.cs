// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for listing multiple views.
/// </summary>
public class ViewListResponse : ResponseBase<IEnumerable<ViewDto>>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ViewListResponse"/> class.
    /// </summary>
    public ViewListResponse()
        : base([]) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ViewListResponse"/> class.
    /// </summary>
    /// <param name="views">The list of views.</param>
    public ViewListResponse(IEnumerable<ViewDto> views)
        : base(views) { }
}