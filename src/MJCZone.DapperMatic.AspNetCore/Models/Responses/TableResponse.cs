// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for table operations.
/// </summary>
public class TableResponse : ResponseBase<TableDto?>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TableResponse"/> class.
    /// </summary>
    public TableResponse()
        : base(null) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="TableResponse"/> class.
    /// </summary>
    /// <param name="table">The table information.</param>
    public TableResponse(TableDto? table)
        : base(table) { }
}