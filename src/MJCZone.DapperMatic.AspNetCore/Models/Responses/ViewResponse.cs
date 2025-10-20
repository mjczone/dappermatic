// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for view operations.
/// </summary>
public class ViewResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ViewResponse"/> class.
    /// </summary>
    public ViewResponse() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ViewResponse"/> class.
    /// </summary>
    /// <param name="result">The view information.</param>
    public ViewResponse(ViewDto result)
    {
        Result = result;
    }

    /// <summary>
    /// Gets or sets the view data.
    /// </summary>
    public ViewDto? Result { get; set; }
}
