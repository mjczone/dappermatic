// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Factories;

/// <summary>
/// Factory for generating datasource IDs based on request data.
/// </summary>
public interface IDatasourceIdFactory
{
    /// <summary>
    /// Generates a datasource ID based on the provided request.
    /// </summary>
    /// <param name="datasource">The request containing datasource details.</param>
    /// <returns>A unique datasource ID.</returns>
    string GenerateId(DatasourceDto datasource);
}
