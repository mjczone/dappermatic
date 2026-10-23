// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Models;

/// <summary>
/// Describes a schema element that exists in both the model and the database but does not match.
/// </summary>
/// <remarks>
/// Drift is reported, never acted upon. Reconciliation is additive only, so resolving drift -- by
/// altering a column, replacing a constraint, or dropping and recreating an index -- is left to the
/// caller, who is in a position to know whether the change is safe against the data already stored.
/// </remarks>
public sealed class DmSchemaDrift
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DmSchemaDrift"/> class.
    /// </summary>
    /// <param name="elementType">The kind of element that differs.</param>
    /// <param name="name">The name of the element that differs.</param>
    /// <param name="expected">The value described by the model.</param>
    /// <param name="actual">The value found in the database.</param>
    /// <param name="description">A human-readable summary of the difference.</param>
    public DmSchemaDrift(
        DmSchemaElementType elementType,
        string name,
        string expected,
        string actual,
        string description
    )
    {
        ElementType = elementType;
        Name = name;
        Expected = expected;
        Actual = actual;
        Description = description;
    }

    /// <summary>
    /// Gets the kind of element that differs.
    /// </summary>
    public DmSchemaElementType ElementType { get; }

    /// <summary>
    /// Gets the name of the element that differs.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the value described by the model.
    /// </summary>
    public string Expected { get; }

    /// <summary>
    /// Gets the value found in the database.
    /// </summary>
    public string Actual { get; }

    /// <summary>
    /// Gets a human-readable summary of the difference.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Returns a human-readable description of the drift.
    /// </summary>
    /// <returns>A string describing the element, the expected value and the actual value.</returns>
    public override string ToString()
    {
        return $"{ElementType} '{Name}': expected {Expected}, found {Actual}";
    }
}
