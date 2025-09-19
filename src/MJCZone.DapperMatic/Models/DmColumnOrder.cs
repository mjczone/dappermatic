// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Models;

/// <summary>
/// Specifies the order of a column in an index or constraint.
/// </summary>
[Serializable]
public enum DmColumnOrder
{
    /// <summary>
    /// Specifies that the column is sorted in ascending order.
    /// </summary>
    Ascending,

    /// <summary>
    /// Specifies that the column is sorted in descending order.
    /// </summary>
    Descending,
}
