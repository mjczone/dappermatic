// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Models.Responses;

/// <summary>
/// Response model for primary key constraint operations.
/// </summary>
public class PrimaryKeyResponse : ResponseBase<PrimaryKeyConstraintDto?>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PrimaryKeyResponse"/> class.
    /// </summary>
    public PrimaryKeyResponse()
        : base(null) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrimaryKeyResponse"/> class.
    /// </summary>
    /// <param name="primaryKeyConstraint">The primary key constraint information.</param>
    public PrimaryKeyResponse(PrimaryKeyConstraintDto? primaryKeyConstraint)
        : base(primaryKeyConstraint) { }
}
