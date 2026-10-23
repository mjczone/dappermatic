// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Providers.Sqlite;

public partial class SqliteMethods
{
    /// <inheritdoc/>
    /// <remarks>
    /// SQLite cannot rename an index, so it is dropped and created again with the same definition, inside one
    /// transaction so the table is never left without it.
    /// </remarks>
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
        var index =
            (await GetIndexesAsync(db, schemaName, tableName, indexName, tx, cancellationToken).ConfigureAwait(false))
                .FirstOrDefault(i => i.IndexName.Equals(indexName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Index '{indexName}' on '{tableName}' could not be read.");

        if (db.State != ConnectionState.Open)
        {
            db.Open();
        }

        var ownTx = tx is null ? db.BeginTransaction() : null;
        try
        {
            var activeTx = tx ?? ownTx;

            await DropIndexIfExistsAsync(db, schemaName, tableName, indexName, activeTx, cancellationToken)
                .ConfigureAwait(false);

            index.IndexName = newIndexName;
            await CreateIndexIfNotExistsAsync(db, index, activeTx, cancellationToken).ConfigureAwait(false);

            ownTx?.Commit();
        }
        catch
        {
            ownTx?.Rollback();
            throw;
        }
        finally
        {
            ownTx?.Dispose();
        }
    }

    /// <inheritdoc/>
    /// <remarks>SQLite keeps constraints in the table definition, so the table is rebuilt with the new name.</remarks>
    protected override Task RenameUniqueConstraintAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string constraintName,
        string newConstraintName,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    ) =>
        RenameConstraintInTableAsync(
            db,
            schemaName,
            tableName,
            table => table.UniqueConstraints,
            constraintName,
            newConstraintName,
            tx,
            cancellationToken
        );

    /// <inheritdoc/>
    /// <remarks>SQLite keeps constraints in the table definition, so the table is rebuilt with the new name.</remarks>
    protected override Task RenameCheckConstraintAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string constraintName,
        string newConstraintName,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    ) =>
        RenameConstraintInTableAsync(
            db,
            schemaName,
            tableName,
            table => table.CheckConstraints,
            constraintName,
            newConstraintName,
            tx,
            cancellationToken
        );

    /// <inheritdoc/>
    /// <remarks>SQLite keeps constraints in the table definition, so the table is rebuilt with the new name.</remarks>
    protected override Task RenameForeignKeyConstraintAsync(
        IDbConnection db,
        string? schemaName,
        string tableName,
        string constraintName,
        string newConstraintName,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    ) =>
        RenameConstraintInTableAsync(
            db,
            schemaName,
            tableName,
            table => table.ForeignKeyConstraints,
            constraintName,
            newConstraintName,
            tx,
            cancellationToken
        );

    private async Task RenameConstraintInTableAsync<TConstraint>(
        IDbConnection db,
        string? schemaName,
        string tableName,
        Func<DmTable, List<TConstraint>> constraintsOf,
        string constraintName,
        string newConstraintName,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    )
        where TConstraint : DmConstraint
    {
        await AlterTableUsingRecreateTableStrategyAsync(
                db,
                schemaName,
                tableName,
                table =>
                    constraintsOf(table)
                        .Any(c => c.ConstraintName.Equals(constraintName, StringComparison.OrdinalIgnoreCase)),
                table =>
                {
                    // The table was read from the database for this call, so its constraints are its own.
                    constraintsOf(table)
                        .First(c => c.ConstraintName.Equals(constraintName, StringComparison.OrdinalIgnoreCase))
                        .ConstraintName = newConstraintName;
                    return table;
                },
                tx,
                cancellationToken
            )
            .ConfigureAwait(false);
    }
}
