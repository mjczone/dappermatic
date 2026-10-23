// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Migrations;

/// <summary>
/// What a migration run did.
/// </summary>
public sealed class DmMigrationResult
{
    /// <summary>
    /// Gets a value indicating whether the run created the ledger table.
    /// </summary>
    public bool LedgerCreated { get; init; }

    /// <summary>
    /// Gets a value indicating whether the run took the lock. A run that finds every model unchanged returns
    /// without it.
    /// </summary>
    public bool LockTaken { get; init; }

    /// <summary>
    /// Gets what the run did with each registered table, in the order registered.
    /// </summary>
    public IReadOnlyList<DmTableMigrationResult> Tables { get; init; } = [];

    /// <summary>
    /// Gets the tables that were reconciled and recorded.
    /// </summary>
    public IReadOnlyList<DmTableMigrationResult> Reconciled =>
        [.. Tables.Where(t => t.Outcome == DmTableMigrationOutcome.Reconciled)];

    /// <summary>
    /// Gets the tables left with something blocked, which are reconciled again on the next run.
    /// </summary>
    public IReadOnlyList<DmTableMigrationResult> Pending =>
        [.. Tables.Where(t => t.Outcome == DmTableMigrationOutcome.Pending)];

    /// <summary>
    /// Gets the tables whose model a newer model superseded.
    /// </summary>
    public IReadOnlyList<DmTableMigrationResult> Superseded =>
        [.. Tables.Where(t => t.Outcome == DmTableMigrationOutcome.Superseded)];

    /// <summary>
    /// Gets a value indicating whether nothing was left pending.
    /// </summary>
    public bool IsComplete => Tables.All(t => t.Outcome != DmTableMigrationOutcome.Pending);
}
