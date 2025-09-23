// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for listing multiple data types.
/// </summary>
public class ProviderDataTypeListResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderDataTypeListResponse"/> class.
    /// </summary>
    public ProviderDataTypeListResponse()
    {
        Result = [];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderDataTypeListResponse"/> class.
    /// </summary>
    /// <param name="providerName">The database provider name (e.g., "SqlServer", "MySQL", "PostgreSQL", "Sqlite").</param>
    /// <param name="result">The list of data types.</param>
    public ProviderDataTypeListResponse(string providerName, IEnumerable<DataTypeDto> result)
    {
        ProviderName = providerName;
        Result = result;
    }

    /// <summary>
    /// Gets or sets the database provider name (e.g., "SqlServer", "MySQL", "PostgreSQL", "Sqlite").
    /// </summary>
    public string ProviderName { get; set; } = default!;

    /// <summary>
    /// Gets or sets the list of data types.
    /// </summary>
    public IEnumerable<DataTypeDto> Result { get; set; }
}
