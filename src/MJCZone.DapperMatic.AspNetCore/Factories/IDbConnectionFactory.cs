// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;

namespace MJCZone.DapperMatic.AspNetCore.Factories;

/// <summary>
/// Factory interface for creating database connections from datasource configurations.
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>
    /// Creates a database connection for the specified provider and connection string.
    /// </summary>
    /// <param name="provider">The database provider.</param>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>A configured database connection.</returns>
    IDbConnection CreateConnection(string provider, string connectionString);
}
