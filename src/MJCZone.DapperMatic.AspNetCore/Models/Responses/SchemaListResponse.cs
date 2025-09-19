// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for listing multiple schemas.
/// </summary>
public class SchemaListResponse : ResponseBase<IEnumerable<SchemaDto>>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaListResponse"/> class.
    /// </summary>
    public SchemaListResponse()
        : base([]) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaListResponse"/> class.
    /// </summary>
    /// <param name="schemas">The list of schemas.</param>
    public SchemaListResponse(IEnumerable<SchemaDto> schemas)
        : base(schemas) { }
}
