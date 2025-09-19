// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.Providers.Base;

namespace MJCZone.DapperMatic.Providers.MySql;

/// <summary>
/// MySQL specific type mapping configuration.
/// </summary>
public class MySqlTypeMapping : IProviderTypeMapping
{
    /// <inheritdoc />
    public string BooleanType => MySqlTypes.sql_bool;

    /// <inheritdoc />
    public string EnumStringType => MySqlTypes.sql_varchar;

    /// <inheritdoc />
    public bool IsUnicodeProvider => false; // MySQL does not use Unicode by default

    /// <inheritdoc />
    public Dictionary<Type, string> NumericTypeMap { get; } = new()
    {
        { typeof(byte), MySqlTypes.sql_tinyint },
        { typeof(sbyte), MySqlTypes.sql_tinyint },
        { typeof(short), MySqlTypes.sql_smallint },
        { typeof(ushort), MySqlTypes.sql_smallint },
        { typeof(int), MySqlTypes.sql_int },
        { typeof(uint), MySqlTypes.sql_int },
        { typeof(System.Numerics.BigInteger), MySqlTypes.sql_bigint },
        { typeof(long), MySqlTypes.sql_bigint },
        { typeof(ulong), MySqlTypes.sql_bigint },
        { typeof(float), MySqlTypes.sql_real },
        { typeof(double), MySqlTypes.sql_float },
        { typeof(decimal), MySqlTypes.sql_decimal },
    };

    /// <inheritdoc />
    public SqlTypeDescriptor CreateGuidType()
    {
        return TypeMappingHelpers.CreateGuidStringType(MySqlTypes.sql_char, isUnicode: false, isFixedLength: true);
    }

    /// <inheritdoc />
    public SqlTypeDescriptor CreateObjectType()
    {
        return TypeMappingHelpers.CreateJsonType(MySqlTypes.sql_json, isText: false);
    }

    /// <inheritdoc />
    public SqlTypeDescriptor CreateTextType(DotnetTypeDescriptor descriptor)
    {
        if (descriptor.Length == TypeMappingDefaults.MaxLength)
        {
            return TypeMappingHelpers.CreateLobType(MySqlTypes.sql_text, isUnicode: false);
        }

        var sqlType = descriptor.IsFixedLength == true ? MySqlTypes.sql_char : MySqlTypes.sql_varchar;
        return TypeMappingHelpers.CreateStringType(
            sqlType,
            descriptor.Length,
            isUnicode: false,
            descriptor.IsFixedLength.GetValueOrDefault(false));
    }

    /// <inheritdoc />
    public SqlTypeDescriptor CreateDateTimeType(DotnetTypeDescriptor descriptor)
    {
        return descriptor.DotnetType switch
        {
            Type t when t == typeof(DateTime) => TypeMappingHelpers.CreateDateTimeType(MySqlTypes.sql_datetime, descriptor.Precision ?? 6),
            Type t when t == typeof(DateTimeOffset) => TypeMappingHelpers.CreateDateTimeType(MySqlTypes.sql_timestamp, descriptor.Precision ?? 6),
            Type t when t == typeof(TimeSpan) => TypeMappingHelpers.CreateSimpleType(MySqlTypes.sql_time),
            Type t when t == typeof(DateOnly) => TypeMappingHelpers.CreateSimpleType(MySqlTypes.sql_date),
            Type t when t == typeof(TimeOnly) => TypeMappingHelpers.CreateSimpleType(MySqlTypes.sql_time),
            _ => TypeMappingHelpers.CreateDateTimeType(MySqlTypes.sql_datetime, descriptor.Precision ?? 6),
        };
    }

    /// <inheritdoc />
    public SqlTypeDescriptor CreateBinaryType(DotnetTypeDescriptor descriptor)
    {
        if (descriptor.Length == TypeMappingDefaults.MaxLength)
        {
            return TypeMappingHelpers.CreateLobType(MySqlTypes.sql_blob, isUnicode: false);
        }

        var sqlType = descriptor.IsFixedLength == true ? MySqlTypes.sql_binary : MySqlTypes.sql_varbinary;
        var actualLength = descriptor.Length ?? TypeMappingDefaults.DefaultBinaryLength;
        return TypeMappingHelpers.CreateBinaryType(
            sqlType,
            actualLength,
            descriptor.IsFixedLength.GetValueOrDefault(false));
    }

    /// <inheritdoc />
    public SqlTypeDescriptor CreateXmlType()
    {
        return TypeMappingHelpers.CreateLobType(MySqlTypes.sql_text, isUnicode: false);
    }

    /// <inheritdoc />
    public Type[] GetSupportedGeometryTypes()
    {
        return TypeMappingHelpers.GetGeometryTypesForProvider("mysql");
    }
}