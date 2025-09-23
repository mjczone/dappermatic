// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for table list operations.
/// </summary>
public class TableListResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TableListResponse"/> class.
    /// </summary>
    public TableListResponse()
    {
        Result = [];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TableListResponse"/> class.
    /// </summary>
    /// <param name="tables">The collection of tables.</param>
    public TableListResponse(IEnumerable<TableDto> tables)
    {
        Result = tables;
    }

    /// <summary>
    /// Gets or sets the list of tables.
    /// </summary>
    public IEnumerable<TableDto> Result { get; set; }
}