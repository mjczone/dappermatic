// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;

namespace MJCZone.DapperMatic.Providers.SqlServer;

public partial class SqlServerMethods
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
        // sp_rename names an index by its table: 'schema.table.index'.
        var objectName =
            $"{GetSchemaQualifiedIdentifierName(schemaName, tableName)}.{GetQuotedIdentifier(NormalizeName(indexName))}";
        await SpRenameAsync(db, objectName, newIndexName, "INDEX", tx, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override Task RenameUniqueConstraintAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string constraintName,
        string newConstraintName,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    ) => RenameConstraintAsync(db, schemaName, constraintName, newConstraintName, tx, cancellationToken);

    /// <inheritdoc/>
    protected override Task RenameCheckConstraintAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string constraintName,
        string newConstraintName,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    ) => RenameConstraintAsync(db, schemaName, constraintName, newConstraintName, tx, cancellationToken);

    /// <inheritdoc/>
    protected override Task RenameForeignKeyConstraintAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string constraintName,
        string newConstraintName,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    ) => RenameConstraintAsync(db, schemaName, constraintName, newConstraintName, tx, cancellationToken);

    /// <summary>
    /// Renames a constraint, which SQL Server names as a schema-scoped object: 'schema.constraint'.
    /// </summary>
    private Task RenameConstraintAsync(
        IDbConnection db,
        string? schemaName,
        string constraintName,
        string newConstraintName,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    ) =>
        SpRenameAsync(
            db,
            GetSchemaQualifiedIdentifierName(schemaName, constraintName),
            newConstraintName,
            "OBJECT",
            tx,
            cancellationToken
        );

    private async Task SpRenameAsync(
        IDbConnection db,
        string objectName,
        string newName,
        string objectType,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    )
    {
        await ExecuteAsync(
                db,
                "sp_rename @objname, @newname, @objtype",
                new
                {
                    objname = objectName,
                    newname = NormalizeName(newName),
                    objtype = objectType,
                },
                tx: tx,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
    }
}
