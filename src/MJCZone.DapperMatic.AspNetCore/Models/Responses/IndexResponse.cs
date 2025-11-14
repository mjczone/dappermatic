// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for index operations.
/// </summary>
public class IndexResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IndexResponse"/> class.
    /// </summary>
    public IndexResponse() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexResponse"/> class.
    /// </summary>
    /// <param name="result">The index information.</param>
    public IndexResponse(IndexDto result)
    {
        Result = result;
    }

    /// <summary>
    /// Gets or sets the index data.
    /// </summary>
    public IndexDto? Result { get; set; }
}
