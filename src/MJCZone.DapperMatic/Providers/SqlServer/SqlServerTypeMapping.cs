// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.Providers.Base;

namespace MJCZone.DapperMatic.Providers.SqlServer;

/// <summary>
/// SQL Server specific type mapping configuration.
/// </summary>
public class SqlServerTypeMapping : IProviderTypeMapping
{
    /// <inheritdoc />
    public string BooleanType => SqlServerTypes.sql_bit;

    /// <inheritdoc />
    public string EnumStringType => SqlServerTypes.sql_varchar;

    /// <inheritdoc />
    public bool IsUnicodeProvider => false; // SQL Server can use both Unicode and non-Unicode

    /// <inheritdoc />
    public Dictionary<Type, string> NumericTypeMap { get; } = new()
    {
        { typeof(byte), SqlServerTypes.sql_tinyint },
        { typeof(sbyte), SqlServerTypes.sql_tinyint },
        { typeof(short), SqlServerTypes.sql_smallint },
        { typeof(ushort), SqlServerTypes.sql_smallint },
        { typeof(int), SqlServerTypes.sql_int },
        { typeof(uint), SqlServerTypes.sql_int },
        { typeof(System.Numerics.BigInteger), SqlServerTypes.sql_bigint },
        { typeof(long), SqlServerTypes.sql_bigint },
        { typeof(ulong), SqlServerTypes.sql_bigint },
        { typeof(float), SqlServerTypes.sql_real },
        { typeof(double), SqlServerTypes.sql_float },
        { typeof(decimal), SqlServerTypes.sql_decimal },
    };

    /// <inheritdoc />
    public SqlTypeDescriptor CreateGuidType()
    {
        return TypeMappingHelpers.CreateSimpleType(SqlServerTypes.sql_uniqueidentifier);
    }

    /// <inheritdoc />
    public SqlTypeDescriptor CreateCharType(DotnetTypeDescriptor descriptor)
    {
        var sqlType = descriptor.IsUnicode == true ? SqlServerTypes.sql_nchar : SqlServerTypes.sql_char;
        return TypeMappingHelpers.CreateStringType(sqlType, length: 1, descriptor.IsUnicode.GetValueOrDefault(false), isFixedLength: true);
    }

    /// <inheritdoc />
    public SqlTypeDescriptor CreateObjectType(DotnetTypeDescriptor descriptor)
    {
        // Objects are stored as JSON strings - use VARCHAR(MAX) or NVARCHAR(MAX) based on unicode flag
        var sqlType = descriptor.IsUnicode == true ? $"{SqlServerTypes.sql_nvarchar}(max)" : $"{SqlServerTypes.sql_varchar}(max)";
        return TypeMappingHelpers.CreateLobType(sqlType, descriptor.IsUnicode.GetValueOrDefault(false));
    }

    /// <inheritdoc />
    public SqlTypeDescriptor CreateTextType(DotnetTypeDescriptor descriptor)
    {
        // Support both -1 and int.MaxValue for backward compatibility
        if (descriptor.Length == -1 || descriptor.Length == int.MaxValue)
        {
            var sqlType = descriptor.IsUnicode == true ? $"{SqlServerTypes.sql_nvarchar}(max)" : $"{SqlServerTypes.sql_varchar}(max)";
            return TypeMappingHelpers.CreateLobType(sqlType, descriptor.IsUnicode.GetValueOrDefault(false));
        }

        var baseType = descriptor.IsFixedLength == true
            ? (descriptor.IsUnicode == true ? SqlServerTypes.sql_nchar : SqlServerTypes.sql_char)
            : (descriptor.IsUnicode == true ? SqlServerTypes.sql_nvarchar : SqlServerTypes.sql_varchar);

        return TypeMappingHelpers.CreateStringType(
            baseType,
            descriptor.Length,
            descriptor.IsUnicode.GetValueOrDefault(false),
            descriptor.IsFixedLength.GetValueOrDefault(false));
    }

    /// <inheritdoc />
    public SqlTypeDescriptor CreateDateTimeType(DotnetTypeDescriptor descriptor)
    {
        return descriptor.DotnetType switch
        {
            Type t when t == typeof(DateTime) => TypeMappingHelpers.CreateDateTimeType(SqlServerTypes.sql_datetime2, descriptor.Precision),
            Type t when t == typeof(DateTimeOffset) => TypeMappingHelpers.CreateDateTimeType(SqlServerTypes.sql_datetimeoffset, descriptor.Precision),
            Type t when t == typeof(TimeSpan) => TypeMappingHelpers.CreateSimpleType(SqlServerTypes.sql_time),
            Type t when t == typeof(DateOnly) => TypeMappingHelpers.CreateSimpleType(SqlServerTypes.sql_date),
            Type t when t == typeof(TimeOnly) => TypeMappingHelpers.CreateSimpleType(SqlServerTypes.sql_time),
            _ => TypeMappingHelpers.CreateDateTimeType(SqlServerTypes.sql_datetime2, descriptor.Precision),
        };
    }

    /// <inheritdoc />
    public SqlTypeDescriptor CreateBinaryType(DotnetTypeDescriptor descriptor)
    {
        // Determine appropriate default length when not specified
        int? actualLength = descriptor.Length;
        if (actualLength == null)
        {
            // Stream types should default to MAX, others to 255
            actualLength = (descriptor.DotnetType == typeof(Stream) || descriptor.DotnetType == typeof(MemoryStream))
                ? TypeMappingDefaults.MaxLength
                : TypeMappingDefaults.DefaultBinaryLength;
        }

        // Support both -1 and int.MaxValue for backward compatibility
        if (actualLength == -1 || actualLength == int.MaxValue)
        {
            return TypeMappingHelpers.CreateBinaryType($"{SqlServerTypes.sql_varbinary}(max)", null, false);
        }

        var sqlType = descriptor.IsFixedLength == true ? SqlServerTypes.sql_binary : SqlServerTypes.sql_varbinary;
        return TypeMappingHelpers.CreateBinaryType(
            sqlType,
            actualLength,
            descriptor.IsFixedLength.GetValueOrDefault(false));
    }

    /// <inheritdoc />
    public SqlTypeDescriptor CreateXmlType()
    {
        return TypeMappingHelpers.CreateSimpleType(SqlServerTypes.sql_xml);
    }

    /// <inheritdoc />
    public Type[] GetSupportedGeometryTypes()
    {
        return TypeMappingHelpers.GetGeometryTypesForProvider("sqlserver");
    }
}