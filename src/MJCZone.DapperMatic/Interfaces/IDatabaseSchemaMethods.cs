// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;

namespace MJCZone.DapperMatic.Interfaces;

/// <summary>
/// Provides database methods for database operations.
/// </summary>
public interface IDatabaseSchemaMethods
{
    /// <summary>
    /// Gets the schema-qualified identifier name.
    /// </summary>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <returns>The schema-qualified identifier name.</returns>
    string GetSchemaQualifiedIdentifierName(string? schemaName, string tableName);

    /// <summary>
    /// Creates the schema if it does not exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating success.</returns>
    Task<bool> CreateSchemaIfNotExistsAsync(
        IDbConnection db,
        string schemaName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Checks if the schema exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating existence.</returns>
    Task<bool> DoesSchemaExistAsync(
        IDbConnection db,
        string schemaName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets the schema names.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaNameFilter">The schema name filter.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of schema names.</returns>
    Task<List<string>> GetSchemaNamesAsync(
        IDbConnection db,
        string? schemaNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Drops the schema if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tx">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating success.</returns>
    Task<bool> DropSchemaIfExistsAsync(
        IDbConnection db,
        string schemaName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    );
}
