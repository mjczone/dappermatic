// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Models;

/// <summary>
/// Represents a constraint on a table.
/// </summary>
public abstract class DmConstraint
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DmConstraint"/> class.
    /// </summary>
    /// <param name="constraintName">The name of the constraint.</param>
    protected DmConstraint(string constraintName)
    {
        ConstraintName = constraintName;
    }

    /// <summary>
    /// Gets the type of the constraint.
    /// </summary>
    public abstract DmConstraintType ConstraintType { get; }

    /// <summary>
    /// Gets or sets the name of the constraint.
    /// </summary>
    public string ConstraintName { get; set; }
}
