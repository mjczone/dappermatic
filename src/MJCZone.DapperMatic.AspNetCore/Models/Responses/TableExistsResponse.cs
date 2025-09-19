// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for table existence checks.
/// </summary>
public class TableExistsResponse : ResponseBase<bool>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TableExistsResponse"/> class.
    /// </summary>
    public TableExistsResponse()
        : base(false) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="TableExistsResponse"/> class.
    /// </summary>
    /// <param name="exists">Whether the table exists.</param>
    public TableExistsResponse(bool exists)
        : base(exists) { }
}