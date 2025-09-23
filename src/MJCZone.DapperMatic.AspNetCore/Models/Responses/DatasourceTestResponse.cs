// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for datasource connectivity test operations.
/// </summary>
public class DatasourceTestResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DatasourceTestResponse"/> class.
    /// </summary>
    public DatasourceTestResponse() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DatasourceTestResponse"/> class.
    /// </summary>
    /// <param name="result">The datasource connectivity test information.</param>
    public DatasourceTestResponse(DatasourceConnectivityTestDto result)
    {
        Result = result;
    }

    /// <summary>
    /// Gets or sets the datasource connectivity tests data.
    /// </summary>
    public DatasourceConnectivityTestDto? Result { get; set; }
}
