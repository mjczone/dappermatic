// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Reflection;
using Dapper;
using MJCZone.DapperMatic.DataAnnotations;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.TypeMapping;

/// <summary>
/// Type mapper that automatically detects and maps column attributes at runtime.
/// Supports DmColumn, EF Core Column, and ServiceStack.OrmLite Alias attributes for column name mapping.
/// Supports DmIgnore, EF Core NotMapped, and ServiceStack.OrmLite Ignore attributes for excluding properties.
/// Supports modern C# patterns including records with parameterized constructors.
/// Fallback to default property name matching if no mapping attributes are found.
/// </summary>
public class DmColumnFallbackMapper : SqlMapper.ITypeMap
{
    private readonly Type _type;
    private readonly DmTable? _table;
    private readonly DapperMaticMappingOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DmColumnFallbackMapper"/> class.
    /// </summary>
    /// <param name="type">The type to map.</param>
    /// <param name="options">Configuration options for type mapping.</param>
    public DmColumnFallbackMapper(Type type, DapperMaticMappingOptions options)
    {
        _type = type;
        _options = options;

        // Try to get DmTable if type has DmTable attribute
        if (type.GetCustomAttribute<DmTableAttribute>() != null)
        {
            _table = DmTableFactory.GetTable(type);
        }
    }

    /// <inheritdoc/>
    public ConstructorInfo? FindConstructor(string[] names, Type[] types)
    {
        // If record support is disabled, use parameterless constructor only
        if (!_options.EnableRecordSupport)
        {
            return _type.GetConstructor(Type.EmptyTypes);
        }

        // Try to find parameterized constructor that matches column names (for records)
        var constructors = _type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        // Find constructor with parameters matching the column names
        foreach (var ctor in constructors)
        {
            var parameters = ctor.GetParameters();
            if (parameters.Length == 0)
            {
                continue;
            }

            // Check if all parameters can be matched to column names
            bool allMatch = true;
            foreach (var param in parameters)
            {
                if (!names.Any(n => n.Equals(param.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    allMatch = false;
                    break;
                }
            }

            if (allMatch)
            {
                return ctor; // Found matching parameterized constructor
            }
        }

        // Fall back to parameterless constructor if available
        return _type.GetConstructor(Type.EmptyTypes);
    }

    /// <inheritdoc/>
    public SqlMapper.IMemberMap? GetMember(string columnName)
    {
        // First, check all properties for column name mapping attributes
        var properties = _type.GetProperties();
        foreach (var prop in properties)
        {
            var propertyAttributes = prop.GetCustomAttributes().ToArray();

            // Skip properties with ignore attributes (DmIgnore, EF Core NotMapped, ServiceStack Ignore)
            var hasIgnoreAttribute = propertyAttributes.Any(pa =>
            {
                var paType = pa.GetType();
                return pa is DmIgnoreAttribute
                    // EF Core
                    || pa is System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute
                    // ServiceStack.OrmLite
                    || paType.Name == "IgnoreAttribute";
            });

            if (hasIgnoreAttribute)
            {
                continue;
            }

            // Check for column name mapping attributes (DmColumn, EF Core Column, ServiceStack Alias)
            var mappedColumnName = propertyAttributes
                .Select(pa =>
                {
                    var paType = pa.GetType();
                    if (pa is DmColumnAttribute dca && !string.IsNullOrWhiteSpace(dca.ColumnName))
                    {
                        return dca.ColumnName;
                    }
                    // EF Core
                    else if (
                        pa is System.ComponentModel.DataAnnotations.Schema.ColumnAttribute ca
                        && !string.IsNullOrWhiteSpace(ca.Name)
                    )
                    {
                        return ca.Name;
                    }
                    // ServiceStack.OrmLite
                    else if (
                        paType.Name == "AliasAttribute"
                        && pa.TryGetPropertyValue<string>("Name", out var name)
                        && !string.IsNullOrWhiteSpace(name)
                    )
                    {
                        return name;
                    }

                    return null;
                })
                .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));

            if (mappedColumnName != null && mappedColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase))
            {
                return new DmMemberMap(columnName, prop);
            }
        }

        // Fall back to default property name mapping (case-insensitive)
        var property = _type.GetProperty(
            columnName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase
        );

        if (property != null)
        {
            var propertyAttributes = property.GetCustomAttributes().ToArray();

            // Skip properties with ignore attributes
            var hasIgnoreAttribute = propertyAttributes.Any(pa =>
            {
                var paType = pa.GetType();
                return pa is DmIgnoreAttribute
                    // EF Core
                    || pa is System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute
                    // ServiceStack.OrmLite
                    || paType.Name == "IgnoreAttribute";
            });

            if (hasIgnoreAttribute)
            {
                return null;
            }

            return new DmMemberMap(columnName, property);
        }

        // Check for constructor parameter (for records)
        if (_options.EnableRecordSupport)
        {
            var constructors = _type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            foreach (var ctor in constructors)
            {
                var param = ctor.GetParameters()
                    .FirstOrDefault(p =>
                        p.Name != null && p.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase)
                    );

                if (param != null)
                {
                    return new DmMemberMap(columnName, null, param);
                }
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public ConstructorInfo? FindExplicitConstructor()
    {
        return null;
    }

    /// <inheritdoc/>
    public SqlMapper.IMemberMap? GetConstructorParameter(ConstructorInfo constructor, string columnName)
    {
        var param = constructor
            .GetParameters()
            .FirstOrDefault(p => p.Name != null && p.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));

        if (param != null)
        {
            return new DmMemberMap(columnName, null, param);
        }

        return null;
    }
}
