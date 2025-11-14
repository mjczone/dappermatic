// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Providers.MySql;

/// <summary>
/// MySQL specific type mapping configuration.
/// </summary>
public class MySqlTypeMapping : ProviderTypeMappingBase
{
    /// <inheritdoc />
    public override string BooleanType => MySqlTypes.sql_bool;

    /// <inheritdoc />
    public override Dictionary<Type, string> NumericTypeMap { get; } =
        new()
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
    public override SqlTypeDescriptor CreateObjectType(DotnetTypeDescriptor descriptor)
    {
        return TypeMappingHelpers.CreateJsonType(MySqlTypes.sql_json, isText: false);
    }

    /// <inheritdoc />
    public override SqlTypeDescriptor CreateDateTimeType(DotnetTypeDescriptor descriptor)
    {
        return descriptor.DotnetType switch
        {
            Type t when t == typeof(DateTime) => TypeMappingHelpers.CreateDateTimeType(
                MySqlTypes.sql_datetime,
                descriptor.Precision ?? 6
            ),
            Type t when t == typeof(DateTimeOffset) => TypeMappingHelpers.CreateDateTimeType(
                MySqlTypes.sql_timestamp,
                descriptor.Precision ?? 6
            ),
            Type t when t == typeof(TimeSpan) => TypeMappingHelpers.CreateSimpleType(MySqlTypes.sql_time),
            Type t when t == typeof(DateOnly) => TypeMappingHelpers.CreateSimpleType(MySqlTypes.sql_date),
            Type t when t == typeof(TimeOnly) => TypeMappingHelpers.CreateSimpleType(MySqlTypes.sql_time),
            _ => TypeMappingHelpers.CreateDateTimeType(MySqlTypes.sql_datetime, descriptor.Precision ?? 6),
        };
    }

    /// <inheritdoc />
    public override SqlTypeDescriptor CreateBinaryType(DotnetTypeDescriptor descriptor)
    {
        // Determine appropriate default length when not specified
        int? actualLength = descriptor.Length;
        if (actualLength == null)
        {
            // Stream types should default to MAX (LONGBLOB), others to 255
            actualLength =
                (descriptor.DotnetType == typeof(Stream) || descriptor.DotnetType == typeof(MemoryStream))
                    ? TypeMappingDefaults.MaxLength
                    : TypeMappingDefaults.DefaultBinaryLength;
        }

        // Support both -1 and int.MaxValue for backward compatibility (use LONGBLOB)
        if (actualLength == -1 || actualLength == int.MaxValue)
        {
            return TypeMappingHelpers.CreateLobType(MySqlTypes.sql_longblob, isUnicode: false);
        }

        var sqlType = descriptor.IsFixedLength == true ? MySqlTypes.sql_binary : MySqlTypes.sql_varbinary;
        return TypeMappingHelpers.CreateBinaryType(
            sqlType,
            actualLength,
            descriptor.IsFixedLength.GetValueOrDefault(false)
        );
    }

    /// <inheritdoc />
    public override SqlTypeDescriptor CreateXmlType(DotnetTypeDescriptor descriptor)
    {
        return TypeMappingHelpers.CreateLobType(MySqlTypes.sql_text, isUnicode: false);
    }

    /// <summary>
    /// Creates a SqlTypeDescriptor for string/text types with consistent length and unicode handling.
    /// </summary>
    /// <param name="length">The length, or null to use default.</param>
    /// <param name="isUnicode">Whether the type supports unicode characters.</param>
    /// <param name="isFixedLength">Whether the type is fixed-length.</param>
    /// <returns>A SqlTypeDescriptor with properly formatted SQL type name and metadata.</returns>
    protected override SqlTypeDescriptor CreateStringTypeInternal(
        int length,
        bool isUnicode = false,
        bool isFixedLength = false
    )
    {
        // Fix the length value
        if (length <= 0 && length != TypeMappingDefaults.MaxLength)
        {
            length = TypeMappingDefaults.DefaultStringLength;
        }

        string sqlType;
        if (length == TypeMappingDefaults.MaxLength)
        {
            sqlType = MySqlTypes.sql_text;
            return new SqlTypeDescriptor(sqlType)
            {
                Length = TypeMappingDefaults.MaxLength,
                IsUnicode = isUnicode,
                IsFixedLength = false,
            };
        }

        if (length > 65535)
        {
            sqlType = MySqlTypes.sql_longtext;
            return new SqlTypeDescriptor(sqlType)
            {
                Length = TypeMappingDefaults.MaxLength,
                IsUnicode = isUnicode,
                IsFixedLength = false,
            };
        }

        sqlType = isFixedLength ? $"{MySqlTypes.sql_char}({length})" : $"{MySqlTypes.sql_varchar}({length})";
        return new SqlTypeDescriptor(sqlType)
        {
            Length = length,
            // UNICODE is assigned in DDL as `CHARACTER SET to utf8mb4` rather than in the type name
            IsUnicode = isUnicode,
            IsFixedLength = isFixedLength,
        };
    }
}
