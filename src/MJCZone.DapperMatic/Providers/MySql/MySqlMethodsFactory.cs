// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using MJCZone.DapperMatic.Interfaces;

namespace MJCZone.DapperMatic.Providers.MySql;

/// <summary>
/// Factory class for creating MySQL specific database methods.
/// </summary>
public class MySqlMethodsFactory : DatabaseMethodsFactoryBase
{
    /// <summary>
    /// Determines whether the specified database connection supports custom connection settings.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <returns><c>true</c> if the connection supports custom settings; otherwise, <c>false</c>.</returns>
    public virtual bool SupportsConnectionCustom(IDbConnection db) => false;

    /// <summary>
    /// Determines whether the specified database connection is supported.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <returns><c>true</c> if the connection is supported; otherwise, <c>false</c>.</returns>
    public override bool SupportsConnection(IDbConnection db) =>
        SupportsConnectionCustom(db)
        || (db.GetType().FullName ?? string.Empty).Contains(
            "mysql",
            StringComparison.OrdinalIgnoreCase
        );

    /// <summary>
    /// Creates the core database methods for MySQL.
    /// </summary>
    /// <returns>An instance of <see cref="IDatabaseMethods"/> for MySQL.</returns>
    protected override IDatabaseMethods CreateMethodsCore() => new MySqlMethods();
}
