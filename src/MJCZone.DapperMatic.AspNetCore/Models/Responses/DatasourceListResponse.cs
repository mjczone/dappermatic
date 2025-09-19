// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for listing multiple datasources.
/// </summary>
public class DatasourceListResponse : ResponseBase<IEnumerable<DatasourceDto>>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DatasourceListResponse"/> class.
    /// </summary>
    public DatasourceListResponse()
        : base([]) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DatasourceListResponse"/> class.
    /// </summary>
    /// <param name="datasources">The list of datasources.</param>
    public DatasourceListResponse(IEnumerable<DatasourceDto> datasources)
        : base(datasources) { }
}
