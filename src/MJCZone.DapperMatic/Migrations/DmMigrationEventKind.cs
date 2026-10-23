// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Migrations;

/// <summary>
/// What a <see cref="DmMigrationEvent"/> reports.
/// </summary>
public enum DmMigrationEventKind
{
    /// <summary>
    /// The ledger table did not exist and was created.
    /// </summary>
    LedgerCreated,

    /// <summary>
    /// The migrator is waiting for another instance to finish.
    /// </summary>
    WaitingForLock,

    /// <summary>
    /// The migrator holds the lock.
    /// </summary>
    LockAcquired,

    /// <summary>
    /// A table's model matches the fingerprint recorded for it, so it was not reconciled.
    /// </summary>
    TableUnchanged,

    /// <summary>
    /// A table's model is one a newer model renamed things away from, so it was not reconciled.
    /// </summary>
    TableSuperseded,

    /// <summary>
    /// A table was reconciled and its fingerprint recorded.
    /// </summary>
    TableReconciled,

    /// <summary>
    /// A table was reconciled but something was blocked, so its fingerprint was not recorded and it will be
    /// reconciled again on the next run.
    /// </summary>
    TablePending,

    /// <summary>
    /// The database chose the run as the victim of a deadlock and rolled it back; it is run again, as
    /// <see cref="DmMigrationOptions.DeadlockRetries"/> allows.
    /// </summary>
    Retrying,

    /// <summary>
    /// The run finished.
    /// </summary>
    Completed,
}
