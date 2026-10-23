// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Migrations;

/// <summary>
/// Configures a <see cref="DmMigrator"/>.
/// </summary>
public sealed class DmMigrationOptions
{
    /// <summary>
    /// Gets or sets the schema the ledger table lives in. Null means the provider's default schema, resolved when
    /// the migrator runs. Ignored by providers without schemas.
    /// </summary>
    public string? SchemaName { get; set; }

    /// <summary>
    /// Gets or sets the name of the ledger table that records each table's model fingerprint.
    /// </summary>
    public string TableStateTableName { get; set; } = "dm_table_state";

    /// <summary>
    /// Gets or sets a value indicating whether the ledger table is created when it does not exist. When false, a
    /// missing ledger is an error.
    /// </summary>
    public bool CreateLedgerIfMissing { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether instances running the migrator at the same time take turns. Leave it
    /// on unless only ever one instance migrates.
    /// </summary>
    public bool UseLock { get; set; } = true;

    /// <summary>
    /// Gets or sets the name the lock is derived from. Null derives it from the database and the ledger table, so
    /// applications with different ledgers never wait for each other.
    /// </summary>
    public string? LockName { get; set; }

    /// <summary>
    /// Gets or sets how long to wait for another instance to finish migrating before giving up with a
    /// <see cref="TimeoutException"/>.
    /// </summary>
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets how many times a run the database chose as the victim of a deadlock is run again, after a short
    /// pause. Zero throws the first deadlock.
    /// </summary>
    /// <remarks>
    /// Runs in different schemas hold no lock in common, so on SQL Server two of them changing tables at the same
    /// time, as <see cref="DmMigrator.MigrateSchemasAsync"/> does, can deadlock on the catalog. The database rolls
    /// the victim's transaction back, so nothing of that run is kept, and running it again is what the error asks
    /// for. On MySQL, which commits each change as it is made, the next run starts from what the first one did.
    /// Each retry is reported as <see cref="DmMigrationEventKind.Retrying"/>.
    /// </remarks>
    public int DeadlockRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets a value indicating whether existing columns are altered in the ways that cannot lose data:
    /// a longer length, more precision, allowing nulls. See <see cref="Models.DmColumnAlteration"/>.
    /// </summary>
    public bool ApplySafeAlterations { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether a run that leaves any table blocked throws a
    /// <see cref="DmMigrationException"/> once its work is saved. When false, blocked tables are reported in the
    /// result and tried again on the next run.
    /// </summary>
    public bool ThrowOnBlocked { get; set; }

    /// <summary>
    /// Gets or sets who is recorded as having reconciled each table, such as a machine or user name.
    /// </summary>
    public string? AppliedBy { get; set; }

    /// <summary>
    /// Gets or sets a callback that receives what the migrator does as it does it.
    /// </summary>
    /// <example>
    /// <code>
    /// Log = e => logger.Log(e.Level switch
    /// {
    ///     DmMigrationEventLevel.Debug => LogLevel.Debug,
    ///     DmMigrationEventLevel.Warning => LogLevel.Warning,
    ///     _ => LogLevel.Information,
    /// }, "{Message}", e.Message),
    /// </code>
    /// </example>
    public Action<DmMigrationEvent>? Log { get; set; }

    /// <summary>
    /// Copies the options for another schema's ledger.
    /// </summary>
    /// <param name="schemaName">The schema the copy's ledger lives in.</param>
    /// <returns>The copy.</returns>
    internal DmMigrationOptions ForSchema(string? schemaName)
    {
        var copy = (DmMigrationOptions)MemberwiseClone();
        copy.SchemaName = schemaName;

        // Each schema's run has its own lock, as it has its own ledger; a lock name given for all of them is made
        // the schema's own, so that they can run at once.
        copy.LockName = LockName is null ? null : $"{LockName}\n{schemaName}";
        return copy;
    }
}
