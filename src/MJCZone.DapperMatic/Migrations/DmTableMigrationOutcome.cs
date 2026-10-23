// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Migrations;

/// <summary>
/// What a migration run did with one table.
/// </summary>
public enum DmTableMigrationOutcome
{
    /// <summary>
    /// The model was unchanged, so the table was not reconciled.
    /// </summary>
    Unchanged,

    /// <summary>
    /// The model was superseded by a newer one, so the table was not reconciled. See
    /// <see cref="DmTableStatus.Superseded"/>.
    /// </summary>
    Superseded,

    /// <summary>
    /// The table was reconciled and its fingerprint recorded.
    /// </summary>
    Reconciled,

    /// <summary>
    /// The table was reconciled but something was blocked, so its fingerprint was not recorded and it is
    /// reconciled again on the next run.
    /// </summary>
    Pending,
}
