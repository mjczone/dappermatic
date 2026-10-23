// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Migrations;

/// <summary>
/// How much a <see cref="DmMigrationEvent"/> matters.
/// </summary>
public enum DmMigrationEventLevel
{
    /// <summary>
    /// Routine detail, such as a table that needed nothing.
    /// </summary>
    Debug,

    /// <summary>
    /// Work that was done.
    /// </summary>
    Information,

    /// <summary>
    /// Something left undone that needs a person to look at it.
    /// </summary>
    Warning,
}
