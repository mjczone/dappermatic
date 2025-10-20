// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic
{
    /// <summary>
    /// Specifies the type of database provider.
    /// </summary>
    public enum DbProviderType
    {
        /// <summary>
        /// SQLite database provider.
        /// </summary>
        Sqlite,

        /// <summary>
        /// SQL Server database provider.
        /// </summary>
        SqlServer,

        /// <summary>
        /// MySQL database provider.
        /// </summary>
        MySql,

        /// <summary>
        /// PostgreSQL database provider.
        /// </summary>
        PostgreSql,

        /// <summary>
        /// Other database provider.
        /// </summary>
        Other,
    }
}
