// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Providers.Sqlite;

public partial class SqliteMethods
{
    /// <summary>
    /// Checks SQLite-specific metadata for auto-increment indicators.
    /// </summary>
    /// <param name="metadata">Provider-specific metadata object.</param>
    /// <returns>True if the metadata indicates auto-increment, false otherwise.</returns>
    protected override bool CheckProviderSpecificAutoIncrement(object metadata)
    {
        // SQLite's parser already sets IsAutoIncrement on the column during parsing
        // This is mainly here for consistency
        return metadata switch
        {
            bool isAutoIncrement => isAutoIncrement,
            int isAutoIncrement => isAutoIncrement == 1,
            _ => false,
        };
    }
}
