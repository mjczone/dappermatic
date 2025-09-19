// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for checking if a schema exists.
/// </summary>
public class SchemaExistsResponse : ResponseBase<bool>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaExistsResponse"/> class.
    /// </summary>
    public SchemaExistsResponse()
        : base(false) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaExistsResponse"/> class.
    /// </summary>
    /// <param name="exists">Whether the schema exists.</param>
    public SchemaExistsResponse(bool exists)
        : base(exists) { }
}