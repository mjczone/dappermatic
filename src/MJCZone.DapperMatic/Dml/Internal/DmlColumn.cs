// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Reflection;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Dml.Internal;

/// <summary>
/// A model property and the column it is mapped to.
/// </summary>
internal sealed class DmlColumn
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DmlColumn"/> class.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <param name="column">The column it is mapped to.</param>
    public DmlColumn(PropertyInfo property, DmColumn column)
    {
        Property = property;
        Column = column;
    }

    /// <summary>
    /// Gets the property.
    /// </summary>
    public PropertyInfo Property { get; }

    /// <summary>
    /// Gets the column.
    /// </summary>
    public DmColumn Column { get; }

    /// <summary>
    /// Gets the name of the parameter the property's value is passed as: the property's own name, which is what
    /// Dapper binds a model's properties by.
    /// </summary>
    public string ParameterName => Property.Name;
}
