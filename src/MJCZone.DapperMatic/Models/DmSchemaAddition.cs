// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Models;

/// <summary>
/// Describes a single schema element that reconciliation would create, or did create.
/// </summary>
public sealed class DmSchemaAddition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DmSchemaAddition"/> class.
    /// </summary>
    /// <param name="elementType">The kind of element.</param>
    /// <param name="name">The name of the element.</param>
    /// <param name="note">An optional explanation, used when the element could not be created.</param>
    public DmSchemaAddition(DmSchemaElementType elementType, string name, string? note = null)
    {
        ElementType = elementType;
        Name = name;
        Note = note;
    }

    /// <summary>
    /// Gets the kind of element.
    /// </summary>
    public DmSchemaElementType ElementType { get; }

    /// <summary>
    /// Gets the name of the element.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets an optional explanation, set when the element could not be created.
    /// </summary>
    public string? Note { get; }

    /// <summary>
    /// Gets a value indicating whether the element is blocked because the database server cannot hold it at all,
    /// as MySQL before 8.0.16 cannot hold a check constraint: no run will create it, so it does not keep a
    /// migration from recording the table.
    /// </summary>
    public bool Unsupported { get; init; }

    /// <summary>
    /// Returns a human-readable description of the addition.
    /// </summary>
    /// <returns>A string naming the element kind and the element.</returns>
    public override string ToString()
    {
        return Note is null ? $"{ElementType} '{Name}'" : $"{ElementType} '{Name}' ({Note})";
    }
}
