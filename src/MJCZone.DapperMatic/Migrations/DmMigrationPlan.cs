// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Migrations;

/// <summary>
/// What a <see cref="DmMigrator"/> would do, worked out from the ledger and the models alone.
/// </summary>
public sealed class DmMigrationPlan
{
    /// <summary>
    /// Gets a value indicating whether the ledger table exists. Without it every table is
    /// <see cref="DmTableStatus.NotRecorded"/>.
    /// </summary>
    public bool LedgerExists { get; init; }

    /// <summary>
    /// Gets every registered table, in the order registered.
    /// </summary>
    public IReadOnlyList<DmTableState> Tables { get; init; } = [];

    /// <summary>
    /// Gets the tables that would be reconciled.
    /// </summary>
    public IReadOnlyList<DmTableState> ChangedTables => [.. Tables.Where(t => t.NeedsReconcile)];

    /// <summary>
    /// Gets a value indicating whether running the migrator would do anything.
    /// </summary>
    public bool HasChanges => !LedgerExists || Tables.Any(t => t.NeedsReconcile);
}
