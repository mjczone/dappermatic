// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Models;

/// <summary>
/// Selects which schema elements a reconciliation considers.
/// </summary>
/// <remarks>
/// Reconciliation is additive: elements present in the model but missing from the database are created,
/// and nothing is ever dropped. These flags narrow which kinds of element are eligible to be created; they
/// never enable a destructive operation. Whether existing columns may be altered is a separate choice, made with
/// the <c>applySafeAlterations</c> parameter of reconciliation.
/// </remarks>
[Flags]
public enum DmReconcileElements
{
    /// <summary>
    /// No elements. A plan computed with this value reports drift only.
    /// </summary>
    None = 0,

    /// <summary>
    /// Missing columns.
    /// </summary>
    Columns = 1 << 0,

    /// <summary>
    /// A missing primary key constraint. Only applied when the table has no primary key at all;
    /// an existing primary key is never replaced.
    /// </summary>
    PrimaryKey = 1 << 1,

    /// <summary>
    /// Missing unique constraints.
    /// </summary>
    UniqueConstraints = 1 << 2,

    /// <summary>
    /// Missing check constraints.
    /// </summary>
    CheckConstraints = 1 << 3,

    /// <summary>
    /// Missing default constraints.
    /// </summary>
    DefaultConstraints = 1 << 4,

    /// <summary>
    /// Missing indexes.
    /// </summary>
    Indexes = 1 << 5,

    /// <summary>
    /// Missing foreign key constraints. Applied last, after every other element, so that columns
    /// added in the same call are available to reference.
    /// </summary>
    ForeignKeyConstraints = 1 << 6,

    /// <summary>
    /// Every element kind.
    /// </summary>
    All = Columns
        | PrimaryKey
        | UniqueConstraints
        | CheckConstraints
        | DefaultConstraints
        | Indexes
        | ForeignKeyConstraints,
}
