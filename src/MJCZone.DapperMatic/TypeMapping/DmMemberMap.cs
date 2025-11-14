// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Reflection;
using Dapper;

namespace MJCZone.DapperMatic.TypeMapping;

/// <summary>
/// Maps a database column to a C# property or constructor parameter.
/// Supports modern C# patterns including records with parameterized constructors.
/// </summary>
public class DmMemberMap : SqlMapper.IMemberMap
{
    private readonly string _columnName;
    private readonly PropertyInfo? _property;
    private readonly ParameterInfo? _parameter;

    /// <summary>
    /// Initializes a new instance of the <see cref="DmMemberMap"/> class.
    /// </summary>
    /// <param name="columnName">The database column name.</param>
    /// <param name="property">The property to map to (for regular properties and settable members).</param>
    /// <param name="parameter">The constructor parameter to map to (for records and immutable objects).</param>
    public DmMemberMap(string columnName, PropertyInfo? property, ParameterInfo? parameter = null)
    {
        _columnName = columnName;
        _property = property;
        _parameter = parameter;
    }

    /// <inheritdoc/>
    public string ColumnName => _columnName;

    /// <inheritdoc/>
    public Type MemberType => _property?.PropertyType ?? _parameter?.ParameterType ?? typeof(object);

    /// <inheritdoc/>
    public PropertyInfo? Property => _property;

    /// <inheritdoc/>
    public FieldInfo? Field => null;

    /// <inheritdoc/>
    public ParameterInfo? Parameter => _parameter;
}
