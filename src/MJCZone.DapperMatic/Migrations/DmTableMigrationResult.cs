// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Migrations;

/// <summary>
/// What a migration run did with one table.
/// </summary>
public sealed class DmTableMigrationResult
{
    /// <summary>
    /// Gets the table's state as the run found it.
    /// </summary>
    public required DmTableState State { get; init; }

    /// <summary>
    /// Gets what the run did with the table.
    /// </summary>
    public required DmTableMigrationOutcome Outcome { get; init; }

    /// <summary>
    /// Gets what reconciling the table did, or null when it was not reconciled.
    /// </summary>
    public DmTableReconciliationResult? Reconciliation { get; init; }

    /// <summary>
    /// Returns the table key and outcome.
    /// </summary>
    /// <returns>The table key and outcome.</returns>
    public override string ToString() => $"{State.TableKey}: {Outcome}";
}
