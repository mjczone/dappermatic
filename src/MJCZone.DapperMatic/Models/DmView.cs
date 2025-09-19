// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace MJCZone.DapperMatic.Models;

/// <summary>
/// Represents a view in a database.
/// </summary>
[Serializable]
public class DmView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DmView"/> class.
    /// Used for deserialization.
    /// </summary>
    public DmView() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DmView"/> class.
    /// </summary>
    /// <param name="schemaName">The schema name of the view.</param>
    /// <param name="viewName">The name of the view.</param>
    /// <param name="definition">The definition of the view.</param>
    [SetsRequiredMembers]
    public DmView(string? schemaName, string viewName, string definition)
    {
        SchemaName = schemaName;
        ViewName = viewName;
        Definition = definition;
    }

    /// <summary>
    /// Gets or sets the schema name of the view.
    /// </summary>
    public string? SchemaName { get; set; }

    /// <summary>
    /// Gets or sets the name of the view.
    /// </summary>
    public required string ViewName { get; set; }

    /// <summary>
    /// Gets or sets the definition of the view.
    /// </summary>
    public required string Definition { get; set; }
}
