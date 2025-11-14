// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using MJCZone.DapperMatic.Providers.Base;

namespace MJCZone.DapperMatic.Providers.SqlServer;

/// <summary>
/// Provides SQL Server specific database methods.
/// </summary>
public partial class SqlServerMethods : DatabaseMethodsBase<SqlServerProviderTypeMap>, ISqlServerMethods
{
    private static string _defaultSchema = "dbo";

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlServerMethods"/> class.
    /// </summary>
    internal SqlServerMethods()
        : base(DbProviderType.SqlServer) { }

    /// <summary>
    /// Gets the characters used for quoting identifiers.
    /// </summary>
    public override char[] QuoteChars => ['[', ']'];

    /// <summary>
    /// Gets the default schema.
    /// </summary>
    protected override string DefaultSchema => _defaultSchema;

    /// <summary>
    /// Sets the default schema.
    /// </summary>
    /// <param name="schema">The schema name.</param>
    public static void SetDefaultSchema(string schema)
    {
        _defaultSchema = schema;
    }

    /// <summary>
    /// Gets the database version asynchronously.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The database version.</returns>
    public override async Task<Version> GetDatabaseVersionAsync(
        IDbConnection db,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        /*
            SELECT
            SERVERPROPERTY('Productversion') As [SQL Server Version]        --> 15.0.2000.5, 15.0.4390.2
            SERVERPROPERTY('Productlevel') As [SQL Server Build Level],     --> RTM
            SERVERPROPERTY('edition') As [SQL Server Edition]               --> Express Edition (64-bit), Developer Edition (64-bit), etc.
         */

        const string sql = "SELECT SERVERPROPERTY('Productversion')";
        var versionString =
            await ExecuteScalarAsync<string>(db, sql, tx: tx, cancellationToken: cancellationToken)
                .ConfigureAwait(false) ?? string.Empty;
        return DbProviderUtils.ExtractVersionFromVersionString(versionString);
    }

    /// <inheritdoc />
    protected override IProviderDataTypeRegistry GetDataTypeRegistry()
    {
        return new SqlServerDataTypeRegistry();
    }
}
