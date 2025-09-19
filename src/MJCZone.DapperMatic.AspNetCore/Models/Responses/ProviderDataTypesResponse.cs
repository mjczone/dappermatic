// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for listing data types.
/// </summary>
public class DataTypesResponse : ResponseBase<List<DataTypeDto>>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DataTypesResponse"/> class.
    /// </summary>
    public DataTypesResponse()
        : base(new List<DataTypeDto>()) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DataTypesResponse"/> class with the specified provider name and data types.
    /// </summary>
    /// <param name="providerName">The database provider name (e.g., "SqlServer", "MySQL", "PostgreSQL", "Sqlite").</param>
    /// <param name="dataTypes">The list of data types.</param>
    public DataTypesResponse(string providerName, List<DataTypeDto> dataTypes)
        : base(dataTypes)
    {
        ProviderName = providerName;
    }

    /// <summary>
    /// Gets or sets the database provider name (e.g., "SqlServer", "MySQL", "PostgreSQL", "Sqlite").
    /// </summary>
    public string ProviderName { get; set; } = default!;
}
