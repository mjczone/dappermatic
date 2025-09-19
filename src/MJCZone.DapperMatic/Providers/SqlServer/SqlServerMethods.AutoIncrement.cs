// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Providers.SqlServer;

public partial class SqlServerMethods
{
    /// <summary>
    /// Checks SQL Server-specific metadata for auto-increment indicators.
    /// </summary>
    /// <param name="metadata">Provider-specific metadata object.</param>
    /// <returns>True if the metadata indicates auto-increment, false otherwise.</returns>
    protected override bool CheckProviderSpecificAutoIncrement(object metadata)
    {
        // SQL Server uses is_identity flag
        return metadata switch
        {
            bool isIdentity => isIdentity,
            int isIdentity => isIdentity == 1,
            _ => false
        };
    }
}