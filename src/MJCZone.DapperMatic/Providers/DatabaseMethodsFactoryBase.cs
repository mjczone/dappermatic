// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Data;
using MJCZone.DapperMatic.Interfaces;

namespace MJCZone.DapperMatic.Providers;

/// <summary>
/// Provides a base class for creating database methods factories.
/// </summary>
public abstract class DatabaseMethodsFactoryBase : IDatabaseMethodsFactory
{
    /// <summary>
    /// A thread-safe dictionary to cache database methods by connection type.
    /// </summary>
    private readonly ConcurrentDictionary<Type, IDatabaseMethods> _methodsCache = new();

    /// <summary>
    /// Gets the database methods for the specified database connection.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <returns>The database methods for the specified connection.</returns>
    /// <exception cref="NotSupportedException">Thrown when the connection type is not supported by this factory.</exception>
    public IDatabaseMethods GetMethods(IDbConnection db)
    {
        if (!SupportsConnection(db))
        {
            throw new NotSupportedException(
                $"Connection type {db.GetType().FullName} is not supported by this factory."
            );
        }

        return _methodsCache.GetOrAdd(db.GetType(), _ => CreateMethodsCore());
    }

    /// <summary>
    /// Determines whether the specified database connection is supported by this factory.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <returns><c>true</c> if the connection is supported; otherwise, <c>false</c>.</returns>
    public abstract bool SupportsConnection(IDbConnection db);

    /// <summary>
    /// Creates the core database methods.
    /// </summary>
    /// <returns>The created database methods.</returns>
    protected abstract IDatabaseMethods CreateMethodsCore();
}
