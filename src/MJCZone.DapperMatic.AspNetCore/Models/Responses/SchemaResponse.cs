// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for schema operations.
/// </summary>
public class SchemaResponse : ResponseBase<SchemaDto?>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaResponse"/> class.
    /// </summary>
    public SchemaResponse()
        : base(null) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaResponse"/> class.
    /// </summary>
    /// <param name="schema">The schema information.</param>
    public SchemaResponse(SchemaDto? schema)
        : base(schema) { }
}
