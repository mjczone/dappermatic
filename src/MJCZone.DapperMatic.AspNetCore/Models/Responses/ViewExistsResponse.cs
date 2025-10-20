// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for checking if a view exists.
/// </summary>
public class ViewExistsResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ViewExistsResponse"/> class.
    /// </summary>
    public ViewExistsResponse()
    {
        Result = false;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ViewExistsResponse"/> class.
    /// </summary>
    /// <param name="exists">Whether the view exists.</param>
    public ViewExistsResponse(bool exists)
    {
        Result = exists;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the view exists.
    /// </summary>
    public bool Result { get; set; }
}
