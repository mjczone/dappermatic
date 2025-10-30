// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Data.Common;

namespace MJCZone.DapperMatic.Providers.SqlServer;

public partial class SqlServerMethods
{
    /// <summary>
    /// Drops the specified schema if it exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The name of the schema to drop.</param>
    /// <param name="tx">The transaction to use, or null to create a new transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether the schema was dropped.</returns>
    public override async Task<bool> DropSchemaIfExistsAsync(
        IDbConnection db,
        string schemaName,
        IDbTransaction? tx = null,
        CancellationToken cancellationToken = default
    )
    {
        if (!await DoesSchemaExistAsync(db, schemaName, tx, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        schemaName = NormalizeSchemaName(schemaName)!;

        // Ensure connection is open before beginning transaction
        if (db.State != ConnectionState.Open)
        {
            if (db is DbConnection dbc)
            {
                await dbc.OpenAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                db.Open();
            }
        }

        // Only create a new transaction if one wasn't provided
        IDbTransaction? innerTx = null;
        if (tx != null)
        {
            // Use the existing transaction
            innerTx = (DbTransaction)tx;
        }
        else if (db is DbConnection dbcc)
        {
            // Create a new transaction only if none exists
            innerTx = await dbcc.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            innerTx = db.BeginTransaction();
        }

        try
        {
            // drop all objects in the schemaName (except tables, which will be handled separately)
            var dropAllRelatedTypesSqlStatement = await QueryAsync<string>(
                    db,
                    $"""

                                    SELECT CASE
                                        WHEN type in ('C', 'D', 'F', 'UQ', 'PK') THEN
                                            CONCAT('ALTER TABLE ', QUOTENAME(SCHEMA_NAME(o.schema_id))+'.'+QUOTENAME(OBJECT_NAME(o.parent_object_id)), ' DROP CONSTRAINT ', QUOTENAME(o.[name]))
                                        WHEN type in ('SN') THEN
                                            CONCAT('DROP SYNONYM ', QUOTENAME(SCHEMA_NAME(o.schema_id))+'.'+QUOTENAME(o.[name]))
                                        WHEN type in ('SO') THEN
                                            CONCAT('DROP SEQUENCE ', QUOTENAME(SCHEMA_NAME(o.schema_id))+'.'+QUOTENAME(o.[name]))
                                        WHEN type in ('U') THEN
                                            CONCAT('DROP TABLE ', QUOTENAME(SCHEMA_NAME(o.schema_id))+'.'+QUOTENAME(o.[name]))
                                        WHEN type in ('V') THEN
                                            CONCAT('DROP VIEW ', QUOTENAME(SCHEMA_NAME(o.schema_id))+'.'+QUOTENAME(o.[name]))
                                        WHEN type in ('TR') THEN
                                            CONCAT('DROP TRIGGER ', QUOTENAME(SCHEMA_NAME(o.schema_id))+'.'+QUOTENAME(o.[name]))
                                        WHEN type in ('IF', 'TF', 'FN', 'FS', 'FT') THEN
                                            CONCAT('DROP FUNCTION ', QUOTENAME(SCHEMA_NAME(o.schema_id))+'.'+QUOTENAME(o.[name]))
                                        WHEN type in ('P', 'PC') THEN
                                            CONCAT('DROP PROCEDURE ', QUOTENAME(SCHEMA_NAME(o.schema_id))+'.'+QUOTENAME(o.[name]))
                                        END AS DropSqlStatement
                                    FROM sys.objects o
                                    WHERE o.schema_id = SCHEMA_ID('{schemaName}')
                                    AND
                                        type IN(
                                            --constraints (check, default, foreign key, unique)
                                            'C', 'D', 'F', 'UQ',
                                            --primary keys
                                            'PK',
                                            --synonyms
                                            'SN',
                                            --sequences
                                            'SO',
                                            --user defined tables
                                            'U',
                                            --views
                                            'V',
                                            --triggers
                                            'TR',
                                            --functions (inline, tableName-valued, scalar, CLR scalar, CLR tableName-valued)
                                            'IF', 'TF', 'FN', 'FS', 'FT',
                                            --procedures (stored procedure, CLR stored procedure)
                                            'P', 'PC'
                                        )
                                    ORDER BY CASE
                                        WHEN type in ('C', 'D', 'UQ') THEN 2
                                        WHEN type in ('F') THEN 1
                                        WHEN type in ('PK') THEN 19
                                        WHEN type in ('SN') THEN 3
                                        WHEN type in ('SO') THEN 4
                                        WHEN type in ('U') THEN 20
                                        WHEN type in ('V') THEN 18
                                        WHEN type in ('TR') THEN 10
                                        WHEN type in ('IF', 'TF', 'FN', 'FS', 'FT') THEN 9
                                        WHEN type in ('P', 'PC') THEN 8
                                        END

                    """,
                    tx: innerTx,
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);
            foreach (var dropSql in dropAllRelatedTypesSqlStatement)
            {
                await ExecuteAsync(db, dropSql, tx: innerTx, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }

            // drop xml schemaName collection
            var dropXmlSchemaCollectionSqlStatements = await QueryAsync<string>(
                    db,
                    $"""
                    SELECT 'DROP XML SCHEMA COLLECTION ' + QUOTENAME(SCHEMA_NAME(schema_id)) + '.' + QUOTENAME(name)
                                        FROM sys.xml_schema_collections
                                        WHERE schema_id = SCHEMA_ID('{schemaName}')
                    """,
                    tx: innerTx,
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);
            foreach (var dropSql in dropXmlSchemaCollectionSqlStatements)
            {
                await ExecuteAsync(db, dropSql, tx: innerTx, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }

            // drop all custom types
            var dropCustomTypesSqlStatements = await QueryAsync<string>(
                    db,
                    $"""
                    SELECT 'DROP TYPE ' +QUOTENAME(SCHEMA_NAME(schema_id))+'.'+QUOTENAME(name)
                                        FROM sys.types
                                        WHERE schema_id = SCHEMA_ID('{schemaName}')
                    """,
                    tx: innerTx,
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);
            foreach (var dropSql in dropCustomTypesSqlStatements)
            {
                await ExecuteAsync(db, dropSql, tx: innerTx, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }

            // drop the schemaName itself
            await ExecuteAsync(db, $"DROP SCHEMA [{schemaName}]", tx: innerTx, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (tx == null)
            {
                if (innerTx is DbTransaction dbTransaction)
                {
                    await dbTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    innerTx.Commit();
                }
            }
        }
        catch
        {
            if (tx == null)
            {
                if (innerTx is DbTransaction dbTransaction)
                {
                    await dbTransaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    innerTx.Rollback();
                }
            }

            throw;
        }
        finally
        {
            if (tx == null)
            {
                if (innerTx is DbTransaction dbTransaction)
                {
                    await dbTransaction.DisposeAsync().ConfigureAwait(false);
                }
                else
                {
                    innerTx.Dispose();
                }
            }
        }

        return true;
    }
}
