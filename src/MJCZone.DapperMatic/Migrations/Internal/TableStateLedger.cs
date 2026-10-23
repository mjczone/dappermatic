// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Reflection;
using System.Text.RegularExpressions;
using Dapper;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Migrations.Internal;

/// <summary>
/// The ledger table that records, for each table, the fingerprint of the model it was last reconciled to.
/// </summary>
internal sealed partial class TableStateLedger
{
    /// <summary>
    /// The library version recorded with each row, without build metadata so that it fits its column.
    /// </summary>
    private static readonly string LibraryVersion = GetLibraryVersion();

    /// <summary>
    /// Initializes a new instance of the <see cref="TableStateLedger"/> class.
    /// </summary>
    /// <param name="schemaName">The ledger's schema, or null for the provider's default.</param>
    /// <param name="tableName">The ledger's table name.</param>
    public TableStateLedger(string? schemaName, string tableName)
    {
        // The names end up in SQL the migrator writes itself, so anything but a plain identifier is refused.
        if (schemaName is not null && !PlainIdentifier().IsMatch(schemaName))
        {
            throw new ArgumentException(
                $"The ledger schema '{schemaName}' must be a plain identifier: letters, digits and underscores.",
                nameof(schemaName)
            );
        }

        if (string.IsNullOrEmpty(tableName) || !PlainIdentifier().IsMatch(tableName))
        {
            throw new ArgumentException(
                $"The ledger table name '{tableName}' must be a plain identifier: letters, digits and underscores.",
                nameof(tableName)
            );
        }

        SchemaName = schemaName;
        TableName = tableName;
    }

    /// <summary>
    /// Gets the ledger's schema, or null for the provider's default.
    /// </summary>
    public string? SchemaName { get; }

    /// <summary>
    /// Gets the ledger's table name.
    /// </summary>
    public string TableName { get; }

    /// <summary>
    /// Returns the key a table is recorded under: its schema-qualified name as the provider writes it, with the
    /// schema resolved and the quotes removed, in lowercase.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="schemaName">The table's schema, or null for the provider's default.</param>
    /// <param name="tableName">The table's name.</param>
    /// <returns>The key.</returns>
    /// <remarks>
    /// Resolving the schema matters: the provider's default schema can be changed for the whole process, so a key
    /// that stored "no schema" would mean different tables in different processes.
    /// </remarks>
    public static string GetTableKey(IDbConnection db, string? schemaName, string tableName)
    {
        var qualified = db.GetSchemaQualifiedTableName(schemaName, tableName);
        return string.Concat(qualified.Where(c => c is not '[' and not ']' and not '"' and not '`')).ToLowerInvariant();
    }

    /// <summary>
    /// Returns the ledger table's own model.
    /// </summary>
    /// <returns>The model.</returns>
    public DmTable GetModel()
    {
        DmColumn Column(string name, Type type, int? length = null, bool nullable = false, bool primaryKey = false) =>
            new(SchemaName, TableName, name, type, length: length, isNullable: nullable, isPrimaryKey: primaryKey);

        return new DmTable(
            SchemaName,
            TableName,
            [
                Column("table_key", typeof(string), 300, primaryKey: true),
                Column("schema_name", typeof(string), 128, nullable: true),
                Column("table_name", typeof(string), 128),
                Column("model_hash", typeof(string), 64),
                Column("rename_history", typeof(string), Providers.TypeMappingDefaults.MaxLength, nullable: true),
                Column("reconciled_at_utc", typeof(DateTime)),
                Column("applied_by", typeof(string), 200, nullable: true),
                Column("library_version", typeof(string), 40),
            ]
        );
    }

    /// <summary>
    /// Returns whether the ledger table exists.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="tx">The transaction, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if it exists.</returns>
    public Task<bool> ExistsAsync(IDbConnection db, IDbTransaction? tx, CancellationToken cancellationToken) =>
        db.DoesTableExistAsync(SchemaName, TableName, tx, cancellationToken);

    /// <summary>
    /// Creates the ledger table, and its schema, when they do not exist.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="tx">The transaction, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the table was created.</returns>
    public async Task<bool> CreateIfMissingAsync(IDbConnection db, IDbTransaction? tx, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(SchemaName) && db.SupportsSchemas())
        {
            await db.CreateSchemaIfNotExistsAsync(SchemaName, tx, cancellationToken).ConfigureAwait(false);
        }

        return await db.CreateTableIfNotExistsAsync(GetModel(), tx, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads every row, keyed by table key.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="tx">The transaction, or null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rows.</returns>
    public async Task<Dictionary<string, LedgerRow>> ReadAsync(
        IDbConnection db,
        IDbTransaction? tx,
        CancellationToken cancellationToken
    )
    {
        var rows = await db.QueryAsync<(string TableKey, string ModelHash, string? RenameHistory)>(
                new CommandDefinition(
                    $"SELECT table_key, model_hash, rename_history FROM {QualifiedName(db)}",
                    transaction: tx,
                    cancellationToken: cancellationToken
                )
            )
            .ConfigureAwait(false);

        var byKey = new Dictionary<string, LedgerRow>(StringComparer.OrdinalIgnoreCase);
        foreach (var (tableKey, modelHash, renameHistory) in rows)
        {
            byKey[tableKey] = new LedgerRow(tableKey, modelHash.Trim(), renameHistory);
        }

        return byKey;
    }

    /// <summary>
    /// Records a table's fingerprint, replacing what was recorded for it.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="tx">The transaction, or null.</param>
    /// <param name="tableName">The table's name, recorded for whoever reads the ledger.</param>
    /// <param name="tableKey">The table's key.</param>
    /// <param name="modelHash">The fingerprint to record.</param>
    /// <param name="renameHistory">The rename history to store, or null.</param>
    /// <param name="exists">Whether a row for the table exists already.</param>
    /// <param name="appliedBy">Who is recorded as having reconciled the table.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the row is written.</returns>
    public async Task WriteAsync(
        IDbConnection db,
        IDbTransaction? tx,
        string tableName,
        string tableKey,
        string modelHash,
        string? renameHistory,
        bool exists,
        string? appliedBy,
        CancellationToken cancellationToken
    )
    {
        var dot = tableKey.IndexOf('.', StringComparison.Ordinal);
        var parameters = new
        {
            TableKey = tableKey,
            SchemaName = dot < 0 ? null : tableKey[..dot],
            TableName = tableName,
            ModelHash = modelHash,
            RenameHistory = renameHistory,

            // Unspecified, not Utc: the column is a timestamp without a time zone, which Npgsql will not write a
            // UTC DateTime to.
            ReconciledAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            AppliedBy = appliedBy is { Length: > 200 } ? appliedBy[..200] : appliedBy,
            LibraryVersion,
        };

        var sql = exists
            ? $"""
                UPDATE {QualifiedName(db)}
                SET schema_name = @SchemaName, table_name = @TableName, model_hash = @ModelHash,
                    rename_history = @RenameHistory, reconciled_at_utc = @ReconciledAtUtc,
                    applied_by = @AppliedBy, library_version = @LibraryVersion
                WHERE table_key = @TableKey
                """
            : $"""
                INSERT INTO {QualifiedName(db)}
                    (table_key, schema_name, table_name, model_hash, rename_history, reconciled_at_utc, applied_by, library_version)
                VALUES
                    (@TableKey, @SchemaName, @TableName, @ModelHash, @RenameHistory, @ReconciledAtUtc, @AppliedBy, @LibraryVersion)
                """;

        await db.ExecuteAsync(
                new CommandDefinition(sql, parameters, transaction: tx, cancellationToken: cancellationToken)
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the ledger table's schema-qualified, quoted name.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <returns>The name.</returns>
    public string QualifiedName(IDbConnection db) => db.GetSchemaQualifiedTableName(SchemaName, TableName);

    private static string GetLibraryVersion()
    {
        var version =
            typeof(DmTable).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(DmTable).Assembly.GetName().Version?.ToString()
            ?? "unknown";

        var plus = version.IndexOf('+', StringComparison.Ordinal);
        version = plus < 0 ? version : version[..plus];
        return version.Length > 40 ? version[..40] : version;
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]{0,127}$")]
    private static partial Regex PlainIdentifier();
}
