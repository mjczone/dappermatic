// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using MJCZone.DapperMatic.Converters;
using MJCZone.DapperMatic.Providers.Base;

namespace MJCZone.DapperMatic.Providers;

// Add RegisterSqlTypeToDotnetTypeDescriptorConverter method to allow for custom type mappings
// Add RegisterDotnetTypeDescriptorToSqlTypeConverter method to allow for custom type mappings

/// <summary>
/// Manages mappings between .NET types and database types.
/// </summary>
/// <remarks>
/// It's important that this class remaing a generic class so that the static members are not shared between
/// different implementations of the class. This is because the static members are used to store mappings
/// between types and their corresponding SQL types. If the static members were shared between different
/// implementations, then the mappings would be shared between different implementations, which would cause
/// unexpected behavior.
///
/// Database type mappings are tricky because different databases have different types, and .NET types can
/// be mapped to different database types depending on the desired length, precision, and scale of the type,
/// whether the type is nullable, fixed length, auto-incrementing, etc. This class is designed
/// to provide a way to map .NET types to database types in a way that is flexible and extensible.
/// </remarks>
/// <typeparam name="TImpl">The type of the derived class.</typeparam>
public abstract partial class DbProviderTypeMapBase<TImpl> : IDbProviderTypeMap
    where TImpl : IDbProviderTypeMap
{
    /// <summary>
    /// The list of converters that convert .NET types to SQL types.
    /// </summary>
    /// <remarks>
    /// The key is the .NET type, and the value is a list of converters that convert the .NET type to a SQL type.
    /// </remarks>
    protected static readonly ConcurrentDictionary<
        Type,
        List<DotnetTypeToSqlTypeConverter>
    > DotnetTypeToSqlTypeConverters = new();

    /// <summary>
    /// The list of converters that convert SQL types to .NET types.
    /// </summary>
    /// <remarks>
    /// The key is the base type name of the SQL type, and the value is a list of converters that convert the SQL type to a .NET type.
    /// </remarks>
    protected static readonly ConcurrentDictionary<
        string,
        List<SqlTypeToDotnetTypeConverter>
    > SqlTypeToDotnetTypeConverters = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DbProviderTypeMapBase{TImpl}"/> class.
    /// </summary>
    protected DbProviderTypeMapBase()
    {
        if (DotnetTypeToSqlTypeConverters.IsEmpty)
        {
            RegisterDotnetTypeToSqlTypeConverters();
        }

        if (SqlTypeToDotnetTypeConverters.IsEmpty)
        {
            RegisterSqlTypeToDotnetTypeConverters();
        }
    }

    /// <summary>
    /// Tries to get the .NET type descriptor that matches the specified full SQL type name.
    /// </summary>
    /// <param name="sqlTypeName">The full SQL type name.</param>
    /// <param name="dotnetTypeDescriptor">The .NET type descriptor, if found.</param>
    /// <returns>True if a matching .NET type descriptor is found; otherwise, false.</returns>
    public bool TryGetDotnetTypeDescriptorMatchingFullSqlTypeName(
        string sqlTypeName,
        out DotnetTypeDescriptor? dotnetTypeDescriptor
    )
    {
        var sqlTypeDescriptor = GetSqlTypeDescriptor(sqlTypeName);
        return TryGetDotnetTypeDescriptorMatchingFullSqlTypeName(sqlTypeDescriptor, out dotnetTypeDescriptor);
    }

    /// <summary>
    /// Tries to get the .NET type descriptor that matches the specified SQL type descriptor.
    /// </summary>
    /// <param name="sqlTypeDescriptor">The SQL type descriptor.</param>
    /// <param name="dotnetTypeDescriptor">The .NET type descriptor, if found.</param>
    /// <returns>True if a matching .NET type descriptor is found; otherwise, false.</returns>
    public bool TryGetDotnetTypeDescriptorMatchingFullSqlTypeName(
        SqlTypeDescriptor sqlTypeDescriptor,
        out DotnetTypeDescriptor? dotnetTypeDescriptor
    )
    {
        if (
            !SqlTypeToDotnetTypeConverters.TryGetValue(sqlTypeDescriptor.BaseTypeName, out var converters)
            || converters == null
        )
        {
            dotnetTypeDescriptor = null;
            return false;
        }

        foreach (var converter in converters)
        {
            if (converter.TryConvert(sqlTypeDescriptor, out var rdt))
            {
                if (rdt != null)
                {
                    dotnetTypeDescriptor = rdt;
                    return true;
                }
            }
        }

        dotnetTypeDescriptor = null;
        return false;
    }

    /// <summary>
    /// Tries to get the SQL type descriptor that matches the specified .NET type.
    /// </summary>
    /// <param name="type">The .NET type.</param>
    /// <param name="sqlTypeDescriptor">The SQL type descriptor, if found.</param>
    /// <returns>True if a matching SQL type descriptor is found; otherwise, false.</returns>
    public bool TryGetProviderSqlTypeMatchingDotnetType(Type type, out SqlTypeDescriptor? sqlTypeDescriptor)
    {
        var dotnetTypeDescriptor = GetDotnetTypeDescriptor(type);
        return TryGetProviderSqlTypeMatchingDotnetType(dotnetTypeDescriptor, out sqlTypeDescriptor);
    }

    /// <summary>
    /// Tries to get the SQL type descriptor that matches the specified .NET type descriptor.
    /// </summary>
    /// <param name="dotnetTypeDescriptor">The .NET type descriptor.</param>
    /// <param name="sqlTypeDescriptor">The SQL type descriptor, if found.</param>
    /// <returns>True if a matching SQL type descriptor is found; otherwise, false.</returns>
    public bool TryGetProviderSqlTypeMatchingDotnetType(
        DotnetTypeDescriptor dotnetTypeDescriptor,
        out SqlTypeDescriptor? sqlTypeDescriptor
    )
    {
        if (
            !DotnetTypeToSqlTypeConverters.TryGetValue(dotnetTypeDescriptor.DotnetType, out var converters)
            || converters == null
        )
        {
            // if the type is a generic type, try to find a converter for the generic type definition
            if (dotnetTypeDescriptor.DotnetType.IsGenericType)
            {
                var genericType = dotnetTypeDescriptor.DotnetType.GetGenericTypeDefinition();
                DotnetTypeToSqlTypeConverters.TryGetValue(genericType, out converters);
            }

            // if the type is an enum type, try to find the Enum placeholder type
            if (converters == null && dotnetTypeDescriptor.DotnetType.IsEnum)
            {
                DotnetTypeToSqlTypeConverters.TryGetValue(typeof(InternalEnumTypePlaceholder), out converters);
            }

            // if the type is an array type, try to find the Array placeholder type
            if (converters == null && dotnetTypeDescriptor.DotnetType.IsArray)
            {
                DotnetTypeToSqlTypeConverters.TryGetValue(typeof(InternalArrayTypePlaceholder), out converters);
            }

            // if the type is an poco type, try first to see if there's a registration
            // for a type that can be cast to from this type, otherwise
            // look for the Poco placeholder type
            if (
                converters == null
                && (
                    dotnetTypeDescriptor.DotnetType.IsClass
                    || dotnetTypeDescriptor.DotnetType.IsInterface
                    || dotnetTypeDescriptor.DotnetType.IsStruct()
                )
            )
            {
                foreach (var registeredType in DotnetTypeToSqlTypeConverters.Keys)
                {
                    if (
                        registeredType == typeof(object)
                        || typeof(IEnumerable).IsAssignableFrom(registeredType)
                        || (!registeredType.IsClass && !registeredType.IsInterface)
                    )
                    {
                        continue;
                    }

                    if (registeredType.IsAssignableFrom(dotnetTypeDescriptor.DotnetType))
                    {
                        DotnetTypeToSqlTypeConverters.TryGetValue(registeredType, out converters);
                    }
                }

                if (converters == null)
                {
                    DotnetTypeToSqlTypeConverters.TryGetValue(typeof(InternalPocoTypePlaceholder), out converters);
                }
            }
        }

        if (converters == null || converters.Count == 0)
        {
            sqlTypeDescriptor = null;
            return false;
        }

        foreach (var converter in converters)
        {
            if (converter.TryConvert(dotnetTypeDescriptor, out var std))
            {
                if (std != null)
                {
                    sqlTypeDescriptor = std;
                    return true;
                }
            }
        }

        sqlTypeDescriptor = null;
        return false;
    }

    /// <summary>
    /// Gets the provider-specific type mapping configuration.
    /// </summary>
    /// <returns>Provider-specific type mapping configuration.</returns>
    protected abstract IProviderTypeMapping GetProviderTypeMapping();

    /// <summary>
    /// Gets the provider name for use with helper methods.
    /// </summary>
    /// <returns>The provider name (e.g., "sqlserver", "postgresql").</returns>
    protected abstract string GetProviderName();

    /// <summary>
    /// Registers the converters that convert .NET types to SQL types.
    /// </summary>
    /// <remarks>
    /// This method should be called in the constructor of the derived class to register the converters.
    /// </remarks>
    protected virtual void RegisterDotnetTypeToSqlTypeConverters()
    {
        RegisterStandardDotnetTypeToSqlTypeConverters();
    }

    /// <summary>
    /// Registers the converters that convert SQL types to .NET types.
    /// </summary>
    /// <remarks>
    /// This method should be called in the constructor of the derived class to register the converters.
    /// </remarks>
    protected abstract void RegisterSqlTypeToDotnetTypeConverters();

    /// <summary>
    /// Gets the SQL type descriptor for the specified full SQL type name.
    /// </summary>
    /// <param name="fullSqlType">The full SQL type name.</param>
    /// <returns>The SQL type descriptor.</returns>
    protected SqlTypeDescriptor GetSqlTypeDescriptor(string fullSqlType)
    {
        return new SqlTypeDescriptor(fullSqlType);
    }

    /// <summary>
    /// Gets the .NET type descriptor for the specified type.
    /// </summary>
    /// <param name="type">The .NET type.</param>
    /// <returns>The .NET type descriptor.</returns>
    protected DotnetTypeDescriptor GetDotnetTypeDescriptor(Type type)
    {
        return new DotnetTypeDescriptor(type);
    }

    #region Standard Converter Implementations

    /// <summary>
    /// Gets the boolean to SQL type converter using provider-specific boolean type.
    /// </summary>
    /// <returns>Boolean to SQL type converter.</returns>
    protected virtual DotnetTypeToSqlTypeConverter GetBooleanToSqlTypeConverter()
    {
        var mapping = GetProviderTypeMapping();
        return new DotnetTypeToSqlTypeConverter(d => TypeMappingHelpers.CreateSimpleType(mapping.BooleanType));
    }

    /// <summary>
    /// Gets the GUID to SQL type converter using provider-specific GUID handling.
    /// </summary>
    /// <returns>GUID to SQL type converter.</returns>
    protected virtual DotnetTypeToSqlTypeConverter GetGuidToSqlTypeConverter()
    {
        var mapping = GetProviderTypeMapping();
        return new DotnetTypeToSqlTypeConverter(d => mapping.CreateGuidType());
    }

    /// <summary>
    /// Gets the enum to SQL type converter using provider-specific integer type.
    /// Maps enums to their underlying integer type (byte, short, int, long, etc.)
    /// to align with Dapper's default enum handling behavior.
    /// </summary>
    /// <returns>Enum to SQL type converter.</returns>
    protected virtual DotnetTypeToSqlTypeConverter GetEnumToSqlTypeConverter()
    {
        var mapping = GetProviderTypeMapping();
        return new DotnetTypeToSqlTypeConverter(d =>
        {
            // Get the underlying type of the enum (byte, short, int, long, etc.)
            var underlyingType = Enum.GetUnderlyingType(d.DotnetType);

            // Look up the SQL type for the underlying numeric type
            if (mapping.NumericTypeMap.TryGetValue(underlyingType, out var sqlType))
            {
                return TypeMappingHelpers.CreateSimpleType(sqlType);
            }

            // Fallback to int if not found (default enum underlying type)
            return TypeMappingHelpers.CreateSimpleType(mapping.NumericTypeMap[typeof(int)]);
        });
    }

    /// <summary>
    /// Gets the numeric to SQL type converter using provider-specific numeric type mappings.
    /// </summary>
    /// <returns>Numeric to SQL type converter.</returns>
    protected virtual DotnetTypeToSqlTypeConverter GetNumericToSqlTypeConverter()
    {
        var mapping = GetProviderTypeMapping();
        return new DotnetTypeToSqlTypeConverter(d =>
        {
            if (mapping.NumericTypeMap.TryGetValue(d.DotnetType, out var sqlType))
            {
                if (d.DotnetType == typeof(decimal))
                {
                    return TypeMappingHelpers.CreateDecimalType(sqlType, d.Precision, d.Scale);
                }
                return TypeMappingHelpers.CreateSimpleType(sqlType);
            }
            // Default fallback to int type
            return TypeMappingHelpers.CreateSimpleType(mapping.NumericTypeMap[typeof(int)]);
        });
    }

    /// <summary>
    /// Gets the JSON to SQL type converter using standardized JSON handling.
    /// </summary>
    /// <returns>JSON to SQL type converter.</returns>
    protected virtual DotnetTypeToSqlTypeConverter GetJsonToSqlTypeConverter()
    {
        return TypeMappingHelpers.CreateJsonConverter(GetProviderName());
    }

    /// <summary>
    /// Gets the array to SQL type converter with native array support where available.
    /// </summary>
    /// <returns>Array to SQL type converter.</returns>
    protected virtual DotnetTypeToSqlTypeConverter GetArrayToSqlTypeConverter()
    {
        return TypeMappingHelpers.CreateArrayConverter(GetProviderName());
    }

    /// <summary>
    /// Gets the enumerable to SQL type converter, typically using JSON serialization.
    /// </summary>
    /// <returns>Enumerable to SQL type converter.</returns>
    protected virtual DotnetTypeToSqlTypeConverter GetEnumerableToSqlTypeConverter()
    {
        return GetJsonToSqlTypeConverter();
    }

    /// <summary>
    /// Gets the POCO to SQL type converter, typically using JSON serialization.
    /// </summary>
    /// <returns>POCO to SQL type converter.</returns>
    protected virtual DotnetTypeToSqlTypeConverter GetPocoToSqlTypeConverter()
    {
        return GetJsonToSqlTypeConverter();
    }

    /// <summary>
    /// Gets the object to SQL type converter using provider-specific object handling.
    /// </summary>
    /// <returns>Object to SQL type converter.</returns>
    protected virtual DotnetTypeToSqlTypeConverter GetObjectToSqlTypeConverter()
    {
        var mapping = GetProviderTypeMapping();
        return new DotnetTypeToSqlTypeConverter(d => mapping.CreateObjectType(d));
    }

    /// <summary>
    /// Gets the char to SQL type converter using provider-specific char handling.
    /// </summary>
    /// <returns>Char to SQL type converter.</returns>
    protected virtual DotnetTypeToSqlTypeConverter GetCharToSqlTypeConverter()
    {
        var mapping = GetProviderTypeMapping();
        return new DotnetTypeToSqlTypeConverter(d => mapping.CreateCharType(d));
    }

    /// <summary>
    /// Gets the text to SQL type converter using provider-specific text handling.
    /// </summary>
    /// <returns>Text to SQL type converter.</returns>
    protected virtual DotnetTypeToSqlTypeConverter GetTextToSqlTypeConverter()
    {
        var mapping = GetProviderTypeMapping();
        return new DotnetTypeToSqlTypeConverter(d => mapping.CreateTextType(d));
    }

    /// <summary>
    /// Gets the DateTime to SQL type converter using provider-specific DateTime handling.
    /// </summary>
    /// <returns>DateTime to SQL type converter.</returns>
    protected virtual DotnetTypeToSqlTypeConverter GetDateTimeToSqlTypeConverter()
    {
        var mapping = GetProviderTypeMapping();
        return new DotnetTypeToSqlTypeConverter(d => mapping.CreateDateTimeType(d));
    }

    /// <summary>
    /// Gets the byte array to SQL type converter using provider-specific binary handling.
    /// </summary>
    /// <returns>Byte array to SQL type converter.</returns>
    protected virtual DotnetTypeToSqlTypeConverter GetByteArrayToSqlTypeConverter()
    {
        var mapping = GetProviderTypeMapping();
        return new DotnetTypeToSqlTypeConverter(d => mapping.CreateBinaryType(d));
    }

    /// <summary>
    /// Gets the XML to SQL type converter using provider-specific XML handling.
    /// </summary>
    /// <returns>XML to SQL type converter.</returns>
    protected virtual DotnetTypeToSqlTypeConverter GetXmlToSqlTypeConverter()
    {
        var mapping = GetProviderTypeMapping();
        return new DotnetTypeToSqlTypeConverter(d => mapping.CreateXmlType(d));
    }

    /// <summary>
    /// Gets the network type to SQL type converter using provider-specific network type handling.
    /// </summary>
    /// <returns>Network type to SQL type converter.</returns>
    protected virtual DotnetTypeToSqlTypeConverter GetNetworkTypeToSqlTypeConverter()
    {
        var mapping = GetProviderTypeMapping();
        return new DotnetTypeToSqlTypeConverter(d => mapping.CreateNetworkType(d));
    }

    /// <summary>
    /// Gets the geometric to SQL type converter using standardized geometry handling.
    /// </summary>
    /// <returns>Geometric to SQL type converter.</returns>
    protected virtual DotnetTypeToSqlTypeConverter GetGeometricToSqlTypeConverter()
    {
        return new DotnetTypeToSqlTypeConverter(d =>
        {
            var shortName = TypeMappingHelpers.GetAssemblyQualifiedShortName(d.DotnetType);
            if (string.IsNullOrWhiteSpace(shortName))
            {
                return null;
            }

            return CreateGeometryTypeForShortName(shortName);
        });
    }

    /// <summary>
    /// Creates a geometry type descriptor for the given assembly-qualified short name.
    /// This method should be overridden by providers that support geometry types.
    /// </summary>
    /// <param name="shortName">The assembly-qualified short name of the geometry type.</param>
    /// <returns>SQL type descriptor for geometry storage, or null if not supported.</returns>
    protected virtual SqlTypeDescriptor? CreateGeometryTypeForShortName(string shortName)
    {
        // Default implementation returns null (no geometry support)
        return null;
    }

    #endregion

    #region Standard Registration Implementation

    /// <summary>
    /// Registers standard .NET types to SQL type converters using common patterns.
    /// This method provides the shared registration logic that all providers can inherit.
    /// </summary>
    protected virtual void RegisterStandardDotnetTypeToSqlTypeConverters()
    {
        var booleanConverter = GetBooleanToSqlTypeConverter();
        var numericConverter = GetNumericToSqlTypeConverter();
        var guidConverter = GetGuidToSqlTypeConverter();
        var charConverter = GetCharToSqlTypeConverter();
        var textConverter = GetTextToSqlTypeConverter();
        var xmlConverter = GetXmlToSqlTypeConverter();
        var jsonConverter = GetJsonToSqlTypeConverter();
        var dateTimeConverter = GetDateTimeToSqlTypeConverter();
        var byteArrayConverter = GetByteArrayToSqlTypeConverter();
        var objectConverter = GetObjectToSqlTypeConverter();
        var enumerableConverter = GetEnumerableToSqlTypeConverter();
        var enumConverter = GetEnumToSqlTypeConverter();
        var arrayConverter = GetArrayToSqlTypeConverter();
        var pocoConverter = GetPocoToSqlTypeConverter();
        var networkTypeConverter = GetNetworkTypeToSqlTypeConverter();
        var geometricConverter = GetGeometricToSqlTypeConverter();

        // Boolean affinity
        RegisterConverter<bool>(booleanConverter);

        // Numeric affinity
        RegisterConverterForTypes(numericConverter, TypeMappingHelpers.GetStandardNumericTypes());

        // Guid affinity
        RegisterConverter<Guid>(guidConverter);

        // Char affinity
        RegisterConverter<char>(charConverter);

        // Text affinity
        RegisterConverterForTypes(textConverter, TypeMappingHelpers.GetStandardTextTypes());

        // Xml affinity
        RegisterConverterForTypes(xmlConverter, typeof(System.Xml.Linq.XDocument), typeof(System.Xml.Linq.XElement));

        // Json affinity
        RegisterConverterForTypes(jsonConverter, TypeMappingHelpers.GetStandardJsonTypes());

        // DateTime affinity
        RegisterConverterForTypes(dateTimeConverter, TypeMappingHelpers.GetStandardDateTimeTypes());

        // Binary affinity
        RegisterConverterForTypes(byteArrayConverter, TypeMappingHelpers.GetStandardBinaryTypes());

        // Object affinity
        RegisterConverter<object>(objectConverter);

        // Enumerable affinity
        RegisterConverterForTypes(enumerableConverter, TypeMappingHelpers.GetStandardEnumerableTypes());

        // Enums (uses a placeholder to easily locate it)
        RegisterConverter<InternalEnumTypePlaceholder>(enumConverter);

        // Arrays (uses a placeholder to easily locate it)
        RegisterConverter<InternalArrayTypePlaceholder>(arrayConverter);

        // Poco (uses a placeholder to easily locate it)
        RegisterConverter<InternalPocoTypePlaceholder>(pocoConverter);

        // Network type affinity
        RegisterConverterForTypes(networkTypeConverter, TypeMappingHelpers.GetStandardNetworkTypes());

        // Geometry types (support provider-specific geometry types)
        var geometryTypes = GetProviderTypeMapping().GetSupportedGeometryTypes();
        if (geometryTypes.Length > 0)
        {
            RegisterConverterForTypes(geometricConverter, geometryTypes);
        }

        // Allow providers to register additional converters
        RegisterProviderSpecificConverters();
    }

    /// <summary>
    /// Allows providers to register additional provider-specific converters.
    /// Override this method to add converters that don't fit the standard patterns.
    /// </summary>
    protected virtual void RegisterProviderSpecificConverters()
    {
        // Default implementation does nothing
    }

    #endregion
}

public abstract partial class DbProviderTypeMapBase<TImpl> : IDbProviderTypeMap
    where TImpl : IDbProviderTypeMap
{
    /// <summary>
    /// Registers a converter for a given .NET type to a SQL type.
    /// </summary>
    /// <param name="type">The .NET type to convert to a SQL type.</param>
    /// <param name="converter">The converter to register.</param>
    /// <param name="prepend">Whether to prepend the converter to the list of converters.</param>
    public static void RegisterConverter(Type type, DotnetTypeToSqlTypeConverter converter, bool prepend = false)
    {
        if (converter == null)
        {
            return;
        }

        if (!DotnetTypeToSqlTypeConverters.TryGetValue(type, out var converters))
        {
            converters = [];
            DotnetTypeToSqlTypeConverters[type] = converters;
        }

        if (prepend)
        {
            converters.Insert(0, converter);
        }
        else
        {
            converters.Add(converter);
        }
    }

    /// <summary>
    /// Registers a converter for a given .NET type to a SQL type.
    /// </summary>
    /// <typeparam name="T">The .NET type to convert to a SQL type.</typeparam>
    /// <param name="converter">The converter to register.</param>
    public static void RegisterConverter<T>(DotnetTypeToSqlTypeConverter converter)
    {
        RegisterConverter(typeof(T), converter);
    }

    /// <summary>
    /// Registers a converter for a given SQL type to a .NET type.
    /// </summary>
    /// <param name="baseTypeName">The base type name to convert to a .NET type.</param>
    /// <param name="converter">The converter to register.</param>
    /// <param name="prepend">Whether to prepend the converter to the list of converters.</param>
    public static void RegisterConverter(
        string baseTypeName,
        SqlTypeToDotnetTypeConverter converter,
        bool prepend = false
    )
    {
        if (converter == null)
        {
            return;
        }

        if (!SqlTypeToDotnetTypeConverters.TryGetValue(baseTypeName, out var converters))
        {
            converters = [];
            SqlTypeToDotnetTypeConverters[baseTypeName] = converters;
        }

        if (prepend)
        {
            converters.Insert(0, converter);
        }
        else
        {
            converters.Add(converter);
        }
    }

    /// <summary>
    /// Registers a converter for multiple .NET types to a SQL type.
    /// </summary>
    /// <param name="converter">The converter to register.</param>
    /// <param name="types">The .NET types to convert to a SQL type.</param>
    protected static void RegisterConverterForTypes(DotnetTypeToSqlTypeConverter converter, params Type?[] types)
    {
        foreach (var type in types)
        {
            if (type != null)
            {
                RegisterConverter(type, converter);
            }
        }
    }

    /// <summary>
    /// Registers a converter for multiple .NET types to a SQL type using type names.
    /// </summary>
    /// <param name="converter">The converter to register.</param>
    /// <param name="clrTypeNames">The .NET type names to convert to a SQL type.</param>
    protected static void RegisterConverterForTypes(
        DotnetTypeToSqlTypeConverter converter,
        params string[] clrTypeNames
    )
    {
        foreach (var typeName in clrTypeNames)
        {
            if (Type.GetType(typeName, false, true) is Type type)
            {
                RegisterConverter(type, converter);
            }
        }
    }

    /// <summary>
    /// Registers a converter for multiple SQL types to a .NET type.
    /// </summary>
    /// <param name="converter">The converter to register.</param>
    /// <param name="baseTypeNames">The base type names to convert to a .NET type.</param>
    protected static void RegisterConverterForTypes(
        SqlTypeToDotnetTypeConverter converter,
        params string[] baseTypeNames
    )
    {
        foreach (var baseTypeName in baseTypeNames)
        {
            RegisterConverter(baseTypeName, converter);
        }
    }
}

/// <summary>
/// An internal placeholder type for enum types.
/// </summary>
internal class InternalEnumTypePlaceholder { }

/// <summary>
/// An internal placeholder type for array types.
/// </summary>
internal class InternalArrayTypePlaceholder { }

/// <summary>
/// An internal placeholder type for POCO types.
/// </summary>
internal class InternalPocoTypePlaceholder { }
