// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using System.Net.NetworkInformation;
using MJCZone.DapperMatic.Providers.Base;

namespace MJCZone.DapperMatic.Providers.PostgreSql;

/// <summary>
/// PostgreSQL specific type mapping configuration.
/// </summary>
public class PostgreSqlTypeMapping : ProviderTypeMappingBase
{
    /// <inheritdoc />
    public override string BooleanType => PostgreSqlTypes.sql_boolean;

    /// <inheritdoc />
    public override bool IsUnicodeProvider => true; // PostgreSQL uses Unicode by default

    /// <inheritdoc />
    public override Dictionary<Type, string> NumericTypeMap { get; } =
        new()
        {
            { typeof(byte), PostgreSqlTypes.sql_smallint },
            { typeof(sbyte), PostgreSqlTypes.sql_smallint },
            { typeof(short), PostgreSqlTypes.sql_smallint },
            { typeof(ushort), PostgreSqlTypes.sql_int },
            { typeof(int), PostgreSqlTypes.sql_int },
            { typeof(uint), PostgreSqlTypes.sql_bigint },
            { typeof(System.Numerics.BigInteger), PostgreSqlTypes.sql_bigint },
            { typeof(long), PostgreSqlTypes.sql_bigint },
            { typeof(ulong), PostgreSqlTypes.sql_bigint },
            { typeof(float), PostgreSqlTypes.sql_real },
            { typeof(double), PostgreSqlTypes.sql_double_precision },
            { typeof(decimal), PostgreSqlTypes.sql_decimal },
        };

    /// <inheritdoc />
    public override SqlTypeDescriptor CreateGuidType()
    {
        return TypeMappingHelpers.CreateSimpleType(PostgreSqlTypes.sql_uuid);
    }

    /// <inheritdoc />
    public override SqlTypeDescriptor CreateObjectType(DotnetTypeDescriptor descriptor)
    {
        return TypeMappingHelpers.CreateJsonType(PostgreSqlTypes.sql_jsonb, isText: false);
    }

    /// <inheritdoc />
    public override SqlTypeDescriptor CreateDateTimeType(DotnetTypeDescriptor descriptor)
    {
        return descriptor.DotnetType switch
        {
            Type t when t == typeof(DateTime) => TypeMappingHelpers.CreateSimpleType(PostgreSqlTypes.sql_timestamp),
            Type t when t == typeof(DateTimeOffset) => TypeMappingHelpers.CreateSimpleType(
                PostgreSqlTypes.sql_timestamptz
            ),
            Type t when t == typeof(TimeSpan) => TypeMappingHelpers.CreateSimpleType(PostgreSqlTypes.sql_interval),
            Type t when t == typeof(DateOnly) => TypeMappingHelpers.CreateSimpleType(PostgreSqlTypes.sql_date),
            Type t when t == typeof(TimeOnly) => TypeMappingHelpers.CreateSimpleType(PostgreSqlTypes.sql_time),
            _ => TypeMappingHelpers.CreateSimpleType(PostgreSqlTypes.sql_timestamp),
        };
    }

    /// <inheritdoc />
    public override SqlTypeDescriptor CreateBinaryType(DotnetTypeDescriptor descriptor)
    {
        // PostgreSQL uses bytea for all binary data regardless of length
        return TypeMappingHelpers.CreateSimpleType(PostgreSqlTypes.sql_bytea);
    }

    /// <inheritdoc />
    public override SqlTypeDescriptor CreateXmlType(DotnetTypeDescriptor descriptor)
    {
        return TypeMappingHelpers.CreateSimpleType(PostgreSqlTypes.sql_xml);
    }

    /// <inheritdoc />
    public override SqlTypeDescriptor CreateNetworkType(DotnetTypeDescriptor descriptor)
    {
        return descriptor.DotnetType switch
        {
            Type t when t == typeof(IPAddress) => TypeMappingHelpers.CreateSimpleType(PostgreSqlTypes.sql_inet),
            Type t when t == typeof(PhysicalAddress) => TypeMappingHelpers.CreateSimpleType(
                PostgreSqlTypes.sql_macaddr
            ),
            _ => CreateStringTypeInternal(
                length: descriptor.Length.GetValueOrDefault(50),
                isUnicode: descriptor.IsUnicode.GetValueOrDefault(false),
                isFixedLength: descriptor.IsFixedLength.GetValueOrDefault(false)
            ),
        };
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
        if (length == TypeMappingDefaults.MaxLength || length == int.MaxValue)
        {
            sqlType = PostgreSqlTypes.sql_text;
            return new SqlTypeDescriptor(sqlType)
            {
                Length = TypeMappingDefaults.MaxLength,
                IsUnicode = isUnicode,
                IsFixedLength = false,
            };
        }

        sqlType = isFixedLength ? $"{PostgreSqlTypes.sql_char}({length})" : $"{PostgreSqlTypes.sql_varchar}({length})";
        return new SqlTypeDescriptor(sqlType)
        {
            Length = length,
            IsUnicode = isUnicode,
            IsFixedLength = isFixedLength,
        };
    }
}
