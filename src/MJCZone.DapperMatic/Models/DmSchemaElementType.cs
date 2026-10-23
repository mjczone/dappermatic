// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Models;

/// <summary>
/// Identifies the kind of schema element a reconciliation addition or drift entry refers to.
/// </summary>
public enum DmSchemaElementType
{
    /// <summary>
    /// The table itself.
    /// </summary>
    Table,

    /// <summary>
    /// A column.
    /// </summary>
    Column,

    /// <summary>
    /// A primary key constraint.
    /// </summary>
    PrimaryKeyConstraint,

    /// <summary>
    /// A unique constraint.
    /// </summary>
    UniqueConstraint,

    /// <summary>
    /// A check constraint.
    /// </summary>
    CheckConstraint,

    /// <summary>
    /// A default constraint.
    /// </summary>
    DefaultConstraint,

    /// <summary>
    /// An index.
    /// </summary>
    Index,

    /// <summary>
    /// A foreign key constraint.
    /// </summary>
    ForeignKeyConstraint,
}
