// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Factories;
using MJCZone.DapperMatic.AspNetCore.Repositories;
using MJCZone.DapperMatic.Dml;

namespace MJCZone.DapperMatic.AspNetCore.Accounts;

/// <summary>
/// The database the account tables are in: a datasource named for them, a database named for them, or where
/// DapperMatic keeps access (its database access repository). With none, the accounts cannot start.
/// </summary>
internal sealed class DapperMaticAccountDb
{
    private readonly DapperMaticAccountOptions _options;
    private readonly IServiceProvider _services;
    private (string Provider, string ConnectionString, string? Schema)? _resolved;

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticAccountDb"/> class.
    /// </summary>
    /// <param name="options">The DapperMatic options.</param>
    /// <param name="services">The application's services.</param>
    public DapperMaticAccountDb(IOptions<DapperMaticOptions> options, IServiceProvider services)
    {
        _options = options.Value.Accounts;
        _services = services;
    }

    /// <summary>Gets the schema of the account tables, or null for the database's default.</summary>
    public string? Schema => _resolved?.Schema ?? _options.Schema;

    /// <summary>
    /// Opens a connection to the account tables' database.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The connection, open.</returns>
    /// <exception cref="InvalidOperationException">No database is named for the accounts.</exception>
    public async Task<IDbConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var (provider, connectionString, _) = _resolved ??= await ResolveAsync().ConfigureAwait(false);
        var db = _services.GetRequiredService<IDbConnectionFactory>().CreateConnection(provider, connectionString);
        if (db is System.Data.Common.DbConnection connection)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            db.Open();
        }

        return db;
    }

    /// <summary>
    /// Opens a connection synchronously: for Data Protection, whose key repository is synchronous.
    /// </summary>
    /// <returns>The connection, open.</returns>
    public IDbConnection Open() => OpenAsync(CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>
    /// Makes the scope DML of the account tables runs in: their schema.
    /// </summary>
    /// <param name="db">The connection.</param>
    /// <returns>The scope.</returns>
    public DmScope Scope(IDbConnection db) => Schema is { Length: > 0 } schema ? new DmScope(db).InSchema(schema) : new DmScope(db);

    private async Task<(string, string, string?)> ResolveAsync()
    {
        if (_options.Datasource is { Length: > 0 } id)
        {
            var repository = _services.GetRequiredService<IDapperMaticDatasourceRepository>();
            var datasource = await repository.GetDatasourceAsync(id).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"The built-in accounts' datasource '{id}' does not exist.");
            var connectionString = await repository.GetConnectionStringAsync(id).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"The built-in accounts' datasource '{id}' has no connection string.");
            return (datasource.Provider!, connectionString, _options.Schema);
        }

        if (_options.Provider is { Length: > 0 } provider && _options.ConnectionString is { Length: > 0 } connection)
        {
            return (provider, connection, _options.Schema);
        }

        if (_services.GetService<IDapperMaticAccessRepository>() is DatabaseDapperMaticAccessRepository access)
        {
            return (access.Provider, access.ConnectionString, _options.Schema ?? access.SchemaName);
        }

        throw new InvalidOperationException(
            "DapperMatic's built-in accounts need a database: a.UseDatasource(\"…\"), a.UseDatabase(provider, connectionString), or keep access in a database (dm.UseDatabaseAccessRepository(…))."
        );
    }
}
