// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Reflection;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Migrations;

/// <summary>
/// Migrating several schemas: a schema per tenant, with the shared models in one schema.
/// </summary>
public sealed partial class DmMigrator
{
    private static readonly MethodInfo ShallowCopy = typeof(object).GetMethod(nameof(MemberwiseClone), BindingFlags.Instance | BindingFlags.NonPublic)!;

    /// <summary>
    /// Migrates the registered models into several schemas: the models <see cref="DmSchemaLayout"/> declares shared
    /// once, into the shared schema, and every other model into each schema listed.
    /// </summary>
    /// <param name="connectionFactory">Makes a connection for a schema's run, which the run opens and disposes.</param>
    /// <param name="schemas">
    /// The schemas, such as every tenant's from a registry; the shared schema may be one of them, for the tenant that
    /// shares it. A schema that does not exist is created.
    /// </param>
    /// <param name="maxParallelism">The most schemas migrated at once, after the shared schema.</param>
    /// <param name="force">True to reconcile every table whatever was recorded, as for <see cref="MigrateAsync"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Each schema's outcome. A schema that fails does not stop the others; nothing of its run is recorded.</returns>
    /// <exception cref="NotSupportedException">The provider has no schemas.</exception>
    /// <exception cref="InvalidOperationException">A model migrated into every schema has a foreign key to a shared one.</exception>
    /// <remarks>
    /// Each schema has its own ledger, lock and transaction: the shared schema's ledger records the shared models and,
    /// when it is listed, its tenant's. The shared schema is migrated first, then the others in parallel.
    /// </remarks>
    public async Task<DmSchemaMigrationResult> MigrateSchemasAsync(
        Func<IDbConnection> connectionFactory,
        IEnumerable<string> schemas,
        int maxParallelism = 4,
        bool force = false,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(schemas);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxParallelism, 1);
        List<string> listed = [.. schemas.Distinct(StringComparer.OrdinalIgnoreCase)];
        if (listed.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("A schema's name is empty.", nameof(schemas));
        }

        using (var probe = connectionFactory())
        {
            if (!probe.SupportsSchemas())
            {
                throw new NotSupportedException($"{probe.GetDbProviderType()} has no schemas to migrate: use MigrateAsync.");
            }
        }

        var sharedTables = new HashSet<DmTable>(DmSchemaLayout.SharedModels.Select(DmTableFactory.GetTable), ReferenceEqualityComparer.Instance);
        var shared = _tables.Where(t => sharedTables.Contains(t) || sharedTables.Any(s => Same(s.TableName, t.TableName))).ToList();
        var perSchema = _tables.Except(shared).ToList();
        RefuseForeignKeysToShared(perSchema, shared);

        var sharedSchema = DmSchemaLayout.SharedSchemaName;
        var sharedListed = sharedSchema is not null && listed.Contains(sharedSchema, StringComparer.OrdinalIgnoreCase);
        var outcomes = new List<DmSchemaMigrationOutcome>();
        if (shared.Count > 0 || sharedListed)
        {
            List<DmTable> tables =
            [
                .. shared.Select(t => sharedSchema is null ? t : InSchema(t, sharedSchema)),
                .. sharedListed ? perSchema.Select(t => InSchema(t, sharedSchema!)) : [],
            ];
            outcomes.Add(await RunAsync(sharedSchema ?? Options.SchemaName, tables, isShared: true, cancellationToken).ConfigureAwait(false));
        }

        var others = listed.Where(s => !sharedListed || !Same(s, sharedSchema!)).ToList();
        var results = new DmSchemaMigrationOutcome[others.Count];
        await Parallel
            .ForEachAsync(
                Enumerable.Range(0, others.Count),
                new ParallelOptions { MaxDegreeOfParallelism = maxParallelism, CancellationToken = cancellationToken },
                async (i, token) =>
                    results[i] = await RunAsync(others[i], [.. perSchema.Select(t => InSchema(t, others[i]))], isShared: false, token)
                        .ConfigureAwait(false)
            )
            .ConfigureAwait(false);
        outcomes.AddRange(results);
        return new DmSchemaMigrationResult { Schemas = outcomes };

        async Task<DmSchemaMigrationOutcome> RunAsync(string? schema, List<DmTable> tables, bool isShared, CancellationToken token)
        {
            try
            {
                using var db = connectionFactory();
                var migrator = new DmMigrator(Options.ForSchema(schema));
                foreach (var table in tables)
                {
                    migrator.AddTable(table);
                }

                var result = await migrator.MigrateAsync(db, force, token).ConfigureAwait(false);
                return new DmSchemaMigrationOutcome { SchemaName = schema, IsShared = isShared, Result = result };
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return new DmSchemaMigrationOutcome { SchemaName = schema, IsShared = isShared, Exception = exception };
            }
        }
    }

    /// <summary>
    /// Copies a model into another schema, its parts too: a model <c>DmTableFactory</c> caches is left as it is.
    /// </summary>
    /// <param name="table">The model.</param>
    /// <param name="schemaName">The schema.</param>
    /// <returns>The copy.</returns>
    internal static DmTable CopyToSchema(DmTable table, string schemaName) => InSchema(table, schemaName);

    private static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    // A foreign key names its table without a schema, so in a tenant's schema one to a shared table would point at a
    // table of the tenant's own, which does not exist.
    private static void RefuseForeignKeysToShared(List<DmTable> perSchema, List<DmTable> shared)
    {
        foreach (var table in perSchema)
        {
            var referenced = table.ForeignKeyConstraints.Select(fk => fk.ReferencedTableName)
                .Concat(table.Columns.Where(c => c.IsForeignKey).Select(c => c.ReferencedTableName));
            if (referenced.FirstOrDefault(name => shared.Any(s => Same(s.TableName, name))) is { } name)
            {
                throw new InvalidOperationException(
                    $"{table.TableName} is migrated into every schema and has a foreign key to {name}, which is shared: a "
                        + "foreign key across schemas is not supported. Drop the foreign key (keep the column), or share the table."
                );
            }
        }
    }

    // A copy of a model in another schema, its parts too; the model is cached by DmTableFactory and left as it is.
    private static DmTable InSchema(DmTable table, string schemaName)
    {
        T Copy<T>(T item)
            where T : class => (T)ShallowCopy.Invoke(item, null)!;

        T InIt<T>(T item, Action<T> set)
            where T : class
        {
            var copy = Copy(item);
            set(copy);
            return copy;
        }

        var copy = Copy(table);
        copy.SchemaName = schemaName;
        copy.Columns = [.. table.Columns.Select(c => InIt(c.Copy(), x => x.SchemaName = schemaName))];
        copy.PrimaryKeyConstraint = table.PrimaryKeyConstraint is { } key ? InIt(key, x => x.SchemaName = schemaName) : null;
        copy.CheckConstraints = [.. table.CheckConstraints.Select(c => InIt(c, x => x.SchemaName = schemaName))];
        copy.DefaultConstraints = [.. table.DefaultConstraints.Select(c => InIt(c, x => x.SchemaName = schemaName))];
        copy.UniqueConstraints = [.. table.UniqueConstraints.Select(c => InIt(c, x => x.SchemaName = schemaName))];
        copy.ForeignKeyConstraints = [.. table.ForeignKeyConstraints.Select(c => InIt(c, x => x.SchemaName = schemaName))];
        copy.Indexes = [.. table.Indexes.Select(c => InIt(c, x => x.SchemaName = schemaName))];
        return copy;
    }
}
