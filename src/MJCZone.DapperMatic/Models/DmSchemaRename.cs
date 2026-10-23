// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Models;

/// <summary>
/// An element that exists in the database under an earlier name, and is renamed rather than created again.
/// </summary>
public sealed class DmSchemaRename
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DmSchemaRename"/> class.
    /// </summary>
    /// <param name="elementType">The kind of element.</param>
    /// <param name="oldName">The name it has in the database.</param>
    /// <param name="newName">The name the model gives it.</param>
    public DmSchemaRename(DmSchemaElementType elementType, string oldName, string newName)
    {
        ElementType = elementType;
        OldName = oldName;
        NewName = newName;
    }

    /// <summary>
    /// Gets the kind of element.
    /// </summary>
    public DmSchemaElementType ElementType { get; }

    /// <summary>
    /// Gets the name the element has in the database.
    /// </summary>
    public string OldName { get; }

    /// <summary>
    /// Gets the name the model gives the element.
    /// </summary>
    public string NewName { get; }

    /// <summary>
    /// Returns a description of the rename.
    /// </summary>
    /// <returns>A description of the rename.</returns>
    public override string ToString()
    {
        return $"{ElementType} '{OldName}' to '{NewName}'";
    }
}
