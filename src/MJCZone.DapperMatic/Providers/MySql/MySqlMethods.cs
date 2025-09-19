// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using MJCZone.DapperMatic.Providers.Base;

namespace MJCZone.DapperMatic.Providers.MySql;

/// <summary>
/// Provides MySQL specific database methods.
/// </summary>
public partial class MySqlMethods : DatabaseMethodsBase<MySqlProviderTypeMap>, IMySqlMethods
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MySqlMethods"/> class.
    /// </summary>
    internal MySqlMethods()
        : base(DbProviderType.MySql) { }

    /// <summary>
    /// Gets the characters used for quoting identifiers.
    /// </summary>
    public override char[] QuoteChars => ['`'];

    /// <summary>
    /// Gets the default schema.
    /// </summary>
    protected override string DefaultSchema => string.Empty;

    /// <summary>
    /// Checks if the database supports check constraints.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating support for check constraints.</returns>
    public override async Task<bool> SupportsCheckConstraintsAsync(
        IDbConnection db,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        var versionStr =
            await ExecuteScalarAsync<string>(
                    db,
                    "SELECT VERSION()",
                    tx: tx,
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false) ?? string.Empty;
        var version = DbProviderUtils.ExtractVersionFromVersionString(versionStr);
        return (
                versionStr.Contains("MariaDB", StringComparison.OrdinalIgnoreCase)
                && version > new Version(10, 2, 1)
            )
            || version >= new Version(8, 0, 16);
    }

    /// <summary>
    /// Checks if the database supports ordered keys in constraints.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating support for ordered keys in constraints.</returns>
    public override Task<bool> SupportsOrderedKeysInConstraintsAsync(
        IDbConnection db,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets the database version.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="tx">The transaction to use, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the database version.</returns>
    public override async Task<Version> GetDatabaseVersionAsync(
        IDbConnection db,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        // sample output: 8.0.27, 8.4.2
        var sql = @"SELECT VERSION()";
        var versionString =
            await ExecuteScalarAsync<string>(db, sql, tx: tx, cancellationToken: cancellationToken)
                .ConfigureAwait(false) ?? string.Empty;
        return DbProviderUtils.ExtractVersionFromVersionString(versionString);
    }

    /// <inheritdoc />
    protected override IProviderDataTypeRegistry GetDataTypeRegistry()
    {
        return new MySqlDataTypeRegistry();
    }
}
