// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;

namespace MJCZone.DapperMatic.Interfaces;

/// <summary>
/// Factory interface for creating database methods.
/// </summary>
public interface IDatabaseMethodsFactory
{
    /// <summary>
    /// Determines whether the factory supports the specified database connection.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <returns><c>true</c> if the factory supports the specified database connection; otherwise, <c>false</c>.</returns>
    bool SupportsConnection(IDbConnection db);

    /// <summary>
    /// Gets the database methods for the specified database connection.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <returns>An instance of <see cref="IDatabaseMethods"/> for the specified database connection.</returns>
    IDatabaseMethods GetMethods(IDbConnection db);
}
