// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using MJCZone.DapperMatic.Interfaces;

namespace MJCZone.DapperMatic.Providers.SqlServer;

/// <summary>
/// Factory class for creating SQL Server specific database methods.
/// </summary>
public class SqlServerMethodsFactory : DatabaseMethodsFactoryBase
{
    /// <summary>
    /// Determines whether the specified connection supports custom handling.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <returns><c>true</c> if the connection supports custom handling; otherwise, <c>false</c>.</returns>
    public virtual bool SupportsConnectionCustom(IDbConnection db) => false;

    /// <summary>
    /// Determines whether the specified connection is supported.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <returns><c>true</c> if the connection is supported; otherwise, <c>false</c>.</returns>
    public override bool SupportsConnection(IDbConnection db)
    {
        var typeName = db.GetType().FullName;
        return SupportsConnectionCustom(db)
            || (typeName == "System.Data.SqlClient.SqlConnection")
            || (typeName == "System.Data.SqlServerCe.SqlCeConnection")
            || (typeName == "Microsoft.Data.SqlClient.SqlConnection");
    }

    /// <summary>
    /// Creates the core database methods for SQL Server.
    /// </summary>
    /// <returns>The SQL Server specific database methods.</returns>
    protected override IDatabaseMethods CreateMethodsCore() => new SqlServerMethods();
}
