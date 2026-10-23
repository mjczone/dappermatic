// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;

namespace MJCZone.DapperMatic.Providers.PostgreSql;

public partial class PostgreSqlMethods
{
    /// <inheritdoc/>
    protected override async Task RenameIndexAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string indexName,
        string newIndexName,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    )
    {
        // An index lives in its table's schema, so it is qualified with that schema.
        await ExecuteAsync(
                db,
                $"ALTER INDEX {GetSchemaQualifiedIdentifierName(schemaName, indexName)} RENAME TO {QuoteName(newIndexName)}",
                tx: tx,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <remarks>Renaming a unique constraint also renames the index behind it.</remarks>
    protected override Task RenameUniqueConstraintAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string constraintName,
        string newConstraintName,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    ) => RenameConstraintAsync(db, schemaName, tableName, constraintName, newConstraintName, tx, cancellationToken);

    /// <inheritdoc/>
    protected override Task RenameCheckConstraintAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string constraintName,
        string newConstraintName,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    ) => RenameConstraintAsync(db, schemaName, tableName, constraintName, newConstraintName, tx, cancellationToken);

    /// <inheritdoc/>
    protected override Task RenameForeignKeyConstraintAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string constraintName,
        string newConstraintName,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    ) => RenameConstraintAsync(db, schemaName, tableName, constraintName, newConstraintName, tx, cancellationToken);

    private async Task RenameConstraintAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string constraintName,
        string newConstraintName,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    )
    {
        await ExecuteAsync(
                db,
                $"ALTER TABLE {GetSchemaQualifiedIdentifierName(schemaName, tableName)} RENAME CONSTRAINT {QuoteName(constraintName)} TO {QuoteName(newConstraintName)}",
                tx: tx,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
    }
}
