// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for datasource operations.
/// </summary>
public class DatasourceResponse : ResponseBase<DatasourceDto?>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DatasourceResponse"/> class.
    /// </summary>
    public DatasourceResponse()
        : base(null) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DatasourceResponse"/> class.
    /// </summary>
    /// <param name="datasource">The datasource information.</param>
    public DatasourceResponse(DatasourceDto? datasource)
        : base(datasource) { }
}
