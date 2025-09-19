// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.DataAnnotations;

/// <summary>
/// Attribute to define a check constraint.
/// </summary>
/// <example>
/// [DmCheckConstraint("Age > 18")]
/// public int Age { get; set; }
/// </example>
[AttributeUsage(
    AttributeTargets.Property | AttributeTargets.Class,
    AllowMultiple = true,
    Inherited = false
)]
public sealed class DmCheckConstraintAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DmCheckConstraintAttribute"/> class.
    /// </summary>
    /// <param name="expression">The check constraint expression.</param>
    /// <param name="constraintName">The name of the check constraint.</param>
    public DmCheckConstraintAttribute(string expression, string? constraintName = null)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new ArgumentException("Expression is required", nameof(expression));
        }

        Expression = expression;
        ConstraintName = constraintName;
    }

    /// <summary>
    /// Gets the name of the check constraint.
    /// </summary>
    public string? ConstraintName { get; }

    /// <summary>
    /// Gets the check constraint expression.
    /// </summary>
    public string Expression { get; }
}
