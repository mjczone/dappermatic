// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for foreign key constraint operations.
/// </summary>
public class ForeignKeyResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ForeignKeyResponse"/> class.
    /// </summary>
    public ForeignKeyResponse() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ForeignKeyResponse"/> class.
    /// </summary>
    /// <param name="result">The foreign key constraint information.</param>
    public ForeignKeyResponse(ForeignKeyConstraintDto result)
    {
        Result = result;
    }

    /// <summary>
    /// Gets or sets the foreign key constraint data.
    /// </summary>
    public ForeignKeyConstraintDto? Result { get; set; }
}
