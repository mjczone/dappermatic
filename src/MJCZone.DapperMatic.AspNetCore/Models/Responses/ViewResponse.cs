// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for view operations.
/// </summary>
public class ViewResponse : ResponseBase<ViewDto?>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ViewResponse"/> class.
    /// </summary>
    public ViewResponse()
        : base(null) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ViewResponse"/> class.
    /// </summary>
    /// <param name="view">The view information.</param>
    public ViewResponse(ViewDto? view)
        : base(view) { }
}