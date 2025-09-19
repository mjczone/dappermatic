// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.DataAnnotations;

/// <summary>
/// Attribute to define a primary key constraint on a table.
/// </summary>
/// <example>
/// [DmPrimaryKeyConstraint(new[] { "Id" }, "PK_MyTable")]
/// </example>
[AttributeUsage(
    AttributeTargets.Property | AttributeTargets.Class,
    AllowMultiple = false,
    Inherited = false
)]
public sealed class DmPrimaryKeyConstraintAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DmPrimaryKeyConstraintAttribute"/> class.
    /// </summary>
    /// <param name="columnNames">The column names that form the primary key constraint.</param>
    /// <param name="constraintName">The name of the constraint.</param>
    public DmPrimaryKeyConstraintAttribute(
        string[]? columnNames = null,
        string? constraintName = null
    )
    {
        // The column names are only required if the attribute is applied to a class.
        // If applied to a property, the column name is derived from the property name.

        Columns = columnNames;
        ConstraintName = constraintName;
    }

    /// <summary>
    /// Gets the name of the constraint.
    /// </summary>
    public string? ConstraintName { get; }

    /// <summary>
    /// Gets the columns that form the primary key constraint.
    /// </summary>
    public string[]? Columns { get; }
}
