// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;

namespace MJCZone.DapperMatic;

public static partial class DbConnectionExtensions
{
    #region IDatabaseSchemaMethods

    /// <summary>
    /// Determines whether the database supports schemas.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <returns>True if the database supports schemas; otherwise, false.</returns>
    public static bool SupportsSchemas(this IDbConnection db)
    {
        return Database(db).SupportsSchemas;
    }

    /// <summary>
    /// Gets the schema-qualified table name.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <returns>The schema-qualified table name.</returns>
    public static string GetSchemaQualifiedTableName(this IDbConnection db, string? schemaName, string tableName)
    {
        return Database(db).GetSchemaQualifiedIdentifierName(schemaName, tableName);
    }

    /// <summary>
    /// Determines whether the database supports check constraints asynchronously.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the database supports check constraints; otherwise, false.</returns>
    public static async Task<bool> SupportsCheckConstraintsAsync(
        this IDbConnection db,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db).SupportsCheckConstraintsAsync(db, tx, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Determines whether the database supports ordered keys in constraints asynchronously.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the database supports ordered keys in constraints; otherwise, false.</returns>
    public static async Task<bool> SupportsOrderedKeysInConstraintsAsync(
        this IDbConnection db,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .SupportsOrderedKeysInConstraintsAsync(db, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Creates the schema if it does not exist asynchronously.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the schema was created; otherwise, false.</returns>
    public static async Task<bool> CreateSchemaIfNotExistsAsync(
        this IDbConnection db,
        string schemaName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .CreateSchemaIfNotExistsAsync(db, schemaName, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Determines whether the schema exists asynchronously.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the schema exists; otherwise, false.</returns>
    public static async Task<bool> DoesSchemaExistAsync(
        this IDbConnection db,
        string schemaName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db).DoesSchemaExistAsync(db, schemaName, tx, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the schema names asynchronously.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaNameFilter">The schema name filter.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of schema names.</returns>
    public static async Task<List<string>> GetSchemaNamesAsync(
        this IDbConnection db,
        string? schemaNameFilter = null,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db)
            .GetSchemaNamesAsync(db, schemaNameFilter, tx, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Drops the schema if it exists asynchronously.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="tx">The database transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the schema was dropped; otherwise, false.</returns>
    public static async Task<bool> DropSchemaIfExistsAsync(
        this IDbConnection db,
        string schemaName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Database(db).DropSchemaIfExistsAsync(db, schemaName, tx, cancellationToken).ConfigureAwait(false);
    }
    #endregion // IDatabaseSchemaMethods
}
