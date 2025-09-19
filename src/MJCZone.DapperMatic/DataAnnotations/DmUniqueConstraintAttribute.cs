// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.DataAnnotations;

/// <summary>
/// Attribute to define a unique constraint on a table.
/// </summary>
/// <example>
/// [DmUniqueConstraint("UQ_MyTable_Col1_Col2", "Col1", "Col2")]
/// </example>
[AttributeUsage(
    AttributeTargets.Property | AttributeTargets.Class,
    AllowMultiple = true,
    Inherited = false
)]
public sealed class DmUniqueConstraintAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DmUniqueConstraintAttribute"/> class.
    /// </summary>
    /// <param name="columnNames">The column names that form the unique constraint.</param>
    /// <param name="constraintName">The name of the constraint.</param>
    public DmUniqueConstraintAttribute(string[]? columnNames = null, string? constraintName = null)
    {
        Columns = columnNames;
        ConstraintName = constraintName;
    }

    /// <summary>
    /// Gets the name of the constraint.
    /// </summary>
    public string? ConstraintName { get; }

    /// <summary>
    /// Gets the columns that form the unique constraint.
    /// </summary>
    public string[]? Columns { get; }
}
