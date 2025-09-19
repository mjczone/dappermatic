// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Factories;

/// <summary>
/// Factory for generating datasource IDs using GUIDs.
/// </summary>
public class GuidDatasourceIdFactory : IDatasourceIdFactory
{
    /// <inheritdoc />
    public string GenerateId(DatasourceDto datasource)
    {
        ArgumentNullException.ThrowIfNull(datasource);

        if (
            !string.IsNullOrWhiteSpace(datasource.Id)
            && Guid.TryParse(datasource.Id, out var uid)
            && uid != Guid.Empty
        )
        {
            return uid.ToString();
        }

        return Guid.NewGuid().ToString();
    }
}
