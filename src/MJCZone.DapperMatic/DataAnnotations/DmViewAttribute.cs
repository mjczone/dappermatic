// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.DataAnnotations;

/// <summary>
/// Attribute to define a database view.
/// </summary>
/// <example>
/// [DmView("CREATE VIEW {0}.MyView AS SELECT ...", schemaName: "dbo", viewName: "MyView")]
/// public class MyView { }
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DmViewAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DmViewAttribute"/> class.
    /// </summary>
    /// <param name="definition">The SQL definition for the view. Use '{0}' to represent the schema name.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="viewName">The view name.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="definition"/> is null or whitespace.</exception>
    public DmViewAttribute(string definition, string? schemaName = null, string? viewName = null)
    {
        if (string.IsNullOrWhiteSpace(definition))
        {
            throw new ArgumentException("Definition is required", nameof(definition));
        }

        Definition = definition;
        SchemaName = schemaName;
        ViewName = viewName;
    }

    /// <summary>
    /// Gets the schema name.
    /// </summary>
    public string? SchemaName { get; }

    /// <summary>
    /// Gets the view name.
    /// </summary>
    public string? ViewName { get; }

    /// <summary>
    /// Gets the SQL definition for the view.
    /// </summary>
    public string Definition { get; }
}
