// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;

namespace MJCZone.DapperMatic.Providers.Base;

public abstract partial class DatabaseMethodsBase
{
    /// <summary>
    /// Checks if a schema exists in the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The name of the schema.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the schema exists, otherwise false.</returns>
    public virtual async Task<bool> DoesSchemaExistAsync(
        IDbConnection db,
        string schemaName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        if (!SupportsSchemas)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(schemaName))
        {
            throw new ArgumentException("Schema name is required.", nameof(schemaName));
        }

        return (
                await GetSchemaNamesAsync(db, schemaName, tx, cancellationToken)
                    .ConfigureAwait(false)
            ).Count > 0;
    }

    /// <summary>
    /// Creates a schema if it does not exist in the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The name of the schema.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the schema was created, otherwise false.</returns>
    public virtual async Task<bool> CreateSchemaIfNotExistsAsync(
        IDbConnection db,
        string schemaName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        if (!SupportsSchemas)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(schemaName))
        {
            throw new ArgumentException("Schema name is required.", nameof(schemaName));
        }

        if (await DoesSchemaExistAsync(db, schemaName, tx, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var sql = SqlCreateSchema(schemaName);

        await ExecuteAsync(db, sql, tx: tx, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Retrieves the list of schema names from the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaNameFilter">The schema name filter.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of schema names.</returns>
    public virtual async Task<List<string>> GetSchemaNamesAsync(
        IDbConnection db,
        string? schemaNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        if (!SupportsSchemas)
        {
            return [];
        }

        var (sql, parameters) = SqlGetSchemaNames(schemaNameFilter);

        return await QueryAsync<string>(
                db,
                sql,
                parameters,
                tx: tx,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Drops a schema if it exists in the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The name of the schema.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the schema was dropped, otherwise false.</returns>
    public virtual async Task<bool> DropSchemaIfExistsAsync(
        IDbConnection db,
        string schemaName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        if (!SupportsSchemas)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(schemaName))
        {
            throw new ArgumentException("Schema name is required.", nameof(schemaName));
        }

        if (
            !await DoesSchemaExistAsync(db, schemaName, tx, cancellationToken).ConfigureAwait(false)
        )
        {
            return false;
        }

        var sql = SqlDropSchema(schemaName);

        await ExecuteAsync(db, sql, tx: tx, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return true;
    }
}
