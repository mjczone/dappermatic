// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Migrations;

/// <summary>
/// How a table's model compares with what the ledger recorded for it.
/// </summary>
public enum DmTableStatus
{
    /// <summary>
    /// The model's fingerprint is the one recorded: the table is not reconciled.
    /// </summary>
    Unchanged,

    /// <summary>
    /// Nothing is recorded for the table yet: it is reconciled, which also adopts an existing table.
    /// </summary>
    NotRecorded,

    /// <summary>
    /// The model's fingerprint differs from the one recorded: the table is reconciled.
    /// </summary>
    ModelChanged,

    /// <summary>
    /// The model is one that a newer model renamed things away from, typically an older version of the
    /// application still running during a deploy. The table is not reconciled, because reconciling it would
    /// create again what was renamed.
    /// </summary>
    Superseded,

    /// <summary>
    /// The run was forced: the table is reconciled whatever was recorded.
    /// </summary>
    Forced,
}
