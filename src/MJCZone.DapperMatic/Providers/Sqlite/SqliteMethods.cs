// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using MJCZone.DapperMatic.Providers.Base;

namespace MJCZone.DapperMatic.Providers.Sqlite;

/// <summary>
/// Provides SQLite specific database methods.
/// </summary>
public partial class SqliteMethods : DatabaseMethodsBase<SqliteProviderTypeMap>, ISqliteMethods
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteMethods"/> class.
    /// </summary>
    internal SqliteMethods()
        : base(DbProviderType.Sqlite) { }

    /// <inheritdoc/>
    public override char[] QuoteChars => ['"'];

    /// <inheritdoc/>
    protected override string DefaultSchema => string.Empty;

    /// <inheritdoc/>
    public override async Task<Version> GetDatabaseVersionAsync(
        IDbConnection db,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        // sample output: 3.44.1
        const string sql = "SELECT sqlite_version()";
        var versionString =
            await ExecuteScalarAsync<string>(db, sql, tx: tx, cancellationToken: cancellationToken)
                .ConfigureAwait(false) ?? string.Empty;
        return DbProviderUtils.ExtractVersionFromVersionString(versionString);
    }

    /// <inheritdoc />
    protected override IProviderDataTypeRegistry GetDataTypeRegistry()
    {
        return new SqliteDataTypeRegistry();
    }
}
