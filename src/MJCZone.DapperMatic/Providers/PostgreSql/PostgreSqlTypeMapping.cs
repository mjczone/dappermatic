// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.Providers.Base;

namespace MJCZone.DapperMatic.Providers.PostgreSql;

/// <summary>
/// PostgreSQL specific type mapping configuration.
/// </summary>
public class PostgreSqlTypeMapping : IProviderTypeMapping
{
    /// <inheritdoc />
    public string BooleanType => PostgreSqlTypes.sql_boolean;

    /// <inheritdoc />
    public string EnumStringType => PostgreSqlTypes.sql_varchar;

    /// <inheritdoc />
    public bool IsUnicodeProvider => true; // PostgreSQL uses Unicode by default

    /// <inheritdoc />
    public Dictionary<Type, string> NumericTypeMap { get; } =
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
    public SqlTypeDescriptor CreateGuidType()
    {
        return TypeMappingHelpers.CreateSimpleType(PostgreSqlTypes.sql_uuid);
    }

    /// <inheritdoc />
    public SqlTypeDescriptor CreateCharType(DotnetTypeDescriptor descriptor)
    {
        return TypeMappingHelpers.CreateStringType(
            PostgreSqlTypes.sql_char,
            length: 1,
            isUnicode: false,
            isFixedLength: true
        );
    }

    /// <inheritdoc />
    public SqlTypeDescriptor CreateObjectType(DotnetTypeDescriptor descriptor)
    {
        return TypeMappingHelpers.CreateJsonType(PostgreSqlTypes.sql_jsonb, isText: false);
    }

    /// <inheritdoc />
    public SqlTypeDescriptor CreateTextType(DotnetTypeDescriptor descriptor)
    {
        // Support both -1 and int.MaxValue for backward compatibility
        if (descriptor.Length == -1 || descriptor.Length == int.MaxValue)
        {
            return TypeMappingHelpers.CreateLobType(PostgreSqlTypes.sql_text, isUnicode: false);
        }

        var sqlType = descriptor.IsFixedLength == true ? PostgreSqlTypes.sql_char : PostgreSqlTypes.sql_varchar;
        return TypeMappingHelpers.CreateStringType(
            sqlType,
            descriptor.Length,
            isUnicode: false,
            descriptor.IsFixedLength.GetValueOrDefault(false)
        );
    }

    /// <inheritdoc />
    public SqlTypeDescriptor CreateDateTimeType(DotnetTypeDescriptor descriptor)
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
    public SqlTypeDescriptor CreateBinaryType(DotnetTypeDescriptor descriptor)
    {
        // PostgreSQL uses bytea for all binary data regardless of length
        return TypeMappingHelpers.CreateSimpleType(PostgreSqlTypes.sql_bytea);
    }

    /// <inheritdoc />
    public SqlTypeDescriptor CreateXmlType()
    {
        return TypeMappingHelpers.CreateSimpleType(PostgreSqlTypes.sql_xml);
    }

    /// <inheritdoc />
    public Type[] GetSupportedGeometryTypes()
    {
        return TypeMappingHelpers.GetGeometryTypesForProvider("postgresql");
    }
}
