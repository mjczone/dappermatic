// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Diagnostics.CodeAnalysis;
using MJCZone.DapperMatic.Interfaces;
using MJCZone.DapperMatic.Providers;

namespace MJCZone.DapperMatic;

/// <summary>
///   Extension methods for <see cref="IDbConnection" />.
/// </summary>
[SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "Reviewed")]
[SuppressMessage("ReSharper", "UnusedMethodReturnValue.Global", Justification = "Reviewed")]
public static partial class DbConnectionExtensions
{
    #region IDatabaseMethods

    /// <summary>
    /// Gets the <see cref="DbProviderType" /> of the database connection.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <returns>The <see cref="DbProviderType"/> of the database connection.</returns>
    public static DbProviderType GetDbProviderType(this IDbConnection db)
    {
        return Database(db).ProviderType;
    }

    /// <summary>
    /// Gets the version of the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="tx">The transaction to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The version of the database.</returns>
    public static async Task<Version> GetDatabaseVersionAsync(
        this IDbConnection db,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .GetDatabaseVersionAsync(db, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the <see cref="IDbProviderTypeMap"/> of the database connection.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <returns>The <see cref="IDbProviderTypeMap"/> of the database connection.</returns>
    public static IDbProviderTypeMap GetProviderTypeMap(this IDbConnection db)
    {
        return Database(db).ProviderTypeMap;
    }

    /// <summary>
    /// Gets the .NET type descriptor from the SQL type.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="sqlType">The SQL type.</param>
    /// <returns>The <see cref="DotnetTypeDescriptor"/> corresponding to the SQL type.</returns>
    public static DotnetTypeDescriptor GetDotnetTypeFromSqlType(
        this IDbConnection db,
        string sqlType
    )
    {
        return Database(db).GetDotnetTypeFromSqlType(sqlType);
    }

    /// <summary>
    /// Gets the SQL type from the .NET type descriptor.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="descriptor">The .NET type descriptor.</param>
    /// <returns>The SQL type corresponding to the .NET type descriptor.</returns>
    public static string GetSqlTypeFromDotnetType(
        this IDbConnection db,
        DotnetTypeDescriptor descriptor
    )
    {
        return Database(db).GetSqlTypeFromDotnetType(descriptor);
    }

    #endregion // IDatabaseMethods

    #region Private static methods
    private static IDatabaseMethods Database(this IDbConnection db)
    {
        return DatabaseMethodsProvider.GetMethods(db);
    }
    #endregion // Private static methods
}
