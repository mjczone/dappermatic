// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for column operations.
/// </summary>
public class ColumnResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ColumnResponse"/> class.
    /// </summary>
    public ColumnResponse() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ColumnResponse"/> class.
    /// </summary>
    /// <param name="result">The column information.</param>
    public ColumnResponse(ColumnDto result)
    {
        Result = result;
    }

    /// <summary>
    /// Gets or sets the column data.
    /// </summary>
    public ColumnDto? Result { get; set; }
}
