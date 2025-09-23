// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for checking if a table exists.
/// </summary>
public class TableExistsResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TableExistsResponse"/> class.
    /// </summary>
    public TableExistsResponse()
    {
        Result = false;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TableExistsResponse"/> class.
    /// </summary>
    /// <param name="exists">Whether the table exists.</param>
    public TableExistsResponse(bool exists)
    {
        Result = exists;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the table exists.
    /// </summary>
    public bool Result { get; set; }
}
