// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using System.Net.NetworkInformation;

using MJCZone.DapperMatic.Providers.Base;

namespace MJCZone.DapperMatic.Providers.Sqlite;

/// <summary>
/// SQLite specific type mapping configuration.
/// </summary>
public class SqliteTypeMapping : IProviderTypeMapping
{
    /// <inheritdoc />
    public string BooleanType => SqliteTypes.sql_boolean;

    /// <inheritdoc />
    public bool IsUnicodeProvider => false; // SQLite can handle both, but we default to non-Unicode

    /// <inheritdoc />
    public Dictionary<Type, string> NumericTypeMap { get; } =
        new()
        {
            { typeof(byte), SqliteTypes.sql_tinyint },
            { typeof(sbyte), SqliteTypes.sql_tinyint },
            { typeof(short), SqliteTypes.sql_smallint },
            { typeof(ushort), SqliteTypes.sql_smallint },
            { typeof(int), SqliteTypes.sql_int },
            { typeof(uint), SqliteTypes.sql_int },
            { typeof(System.Numerics.BigInteger), SqliteTypes.sql_bigint },
            { typeof(long), SqliteTypes.sql_bigint },
            { typeof(ulong), SqliteTypes.sql_bigint },
            { typeof(float), SqliteTypes.sql_real },
            { typeof(double), SqliteTypes.sql_double },
            { typeof(decimal), SqliteTypes.sql_numeric },
        };

    /// <inheritdoc />
    public SqlTypeDescriptor CreateGuidType()
    {
        return TypeMappingHelpers.CreateGuidStringType(SqliteTypes.sql_varchar, isUnicode: false, isFixedLength: false);
    }

    /// <inheritdoc />
    public SqlTypeDescriptor CreateCharType(DotnetTypeDescriptor descriptor)
    {
        // SQLite doesn't enforce fixed-length types but preserves type names in schema
        // Using CHAR(1)/NCHAR(1) for consistency with other providers and to enable proper round-tripping
        var sqlType = descriptor.IsUnicode == true ? SqliteTypes.sql_nchar : SqliteTypes.sql_char;
        return TypeMappingHelpers.CreateStringType(
            sqlType,
            length: 1,
            descriptor.IsUnicode.GetValueOrDefault(false),
            isFixedLength: true
        );
    }

    /// <inheritdoc />
    public SqlTypeDescriptor CreateObjectType(DotnetTypeDescriptor descriptor)
    {
        return TypeMappingHelpers.CreateLobType(SqliteTypes.sql_text, isUnicode: false);
    }

    /// <inheritdoc />
    public SqlTypeDescriptor CreateTextType(DotnetTypeDescriptor descriptor)
    {
        // Support both -1 and int.MaxValue for backward compatibility
        if (descriptor.Length == -1 || descriptor.Length == int.MaxValue)
        {
            // max is NOT supported by SQLite, instead, we'll use the text type; however,
            // using nvarchar and varchar gives DapperMatic a better chance of mapping the
            // correct type when reading the schema
            return TypeMappingHelpers.CreateLobType(
                descriptor.IsUnicode == true ? SqliteTypes.sql_nvarchar : SqliteTypes.sql_varchar,
                descriptor.IsUnicode.GetValueOrDefault(false)
            );
        }

        var sqlType =
            descriptor.IsFixedLength == true
                ? (descriptor.IsUnicode == true ? SqliteTypes.sql_nchar : SqliteTypes.sql_char)
                : (descriptor.IsUnicode == true ? SqliteTypes.sql_nvarchar : SqliteTypes.sql_varchar);

        return TypeMappingHelpers.CreateStringType(
            sqlType,
            descriptor.Length,
            descriptor.IsUnicode.GetValueOrDefault(false),
            descriptor.IsFixedLength.GetValueOrDefault(false)
        );
    }

    /// <inheritdoc />
    public SqlTypeDescriptor CreateDateTimeType(DotnetTypeDescriptor descriptor)
    {
        return descriptor.DotnetType switch
        {
            Type t when t == typeof(DateTime) => TypeMappingHelpers.CreateSimpleType(SqliteTypes.sql_datetime),
            Type t when t == typeof(DateTimeOffset) => TypeMappingHelpers.CreateSimpleType(SqliteTypes.sql_datetime), // SQLite stores both as datetime
            Type t when t == typeof(TimeSpan) => TypeMappingHelpers.CreateSimpleType(SqliteTypes.sql_time),
            Type t when t == typeof(DateOnly) => TypeMappingHelpers.CreateSimpleType(SqliteTypes.sql_date),
            Type t when t == typeof(TimeOnly) => TypeMappingHelpers.CreateSimpleType(SqliteTypes.sql_time),
            _ => TypeMappingHelpers.CreateSimpleType(SqliteTypes.sql_datetime),
        };
    }

    /// <inheritdoc />
    public SqlTypeDescriptor CreateBinaryType(DotnetTypeDescriptor descriptor)
    {
        // SQLite uses blob for all binary data
        return TypeMappingHelpers.CreateLobType(SqliteTypes.sql_blob, isUnicode: false);
    }

    /// <inheritdoc />
    public SqlTypeDescriptor CreateXmlType(DotnetTypeDescriptor descriptor)
    {
        // SQLite stores XML as text
        return TypeMappingHelpers.CreateLobType(SqliteTypes.sql_text, isUnicode: false);
    }

    /// <inheritdoc />
    public SqlTypeDescriptor CreateNetworkType(DotnetTypeDescriptor descriptor)
    {
        return descriptor.DotnetType switch
        {
            Type t when t == typeof(IPAddress) => TypeMappingHelpers.CreateStringType(
                descriptor.IsFixedLength == true ? SqliteTypes.sql_char : SqliteTypes.sql_varchar,
                descriptor.Length.GetValueOrDefault(45),
                isUnicode: false,
                descriptor.IsFixedLength.GetValueOrDefault(false)
            ),
            Type t when t == typeof(PhysicalAddress) => TypeMappingHelpers.CreateStringType(
                descriptor.IsFixedLength == true ? SqliteTypes.sql_char : SqliteTypes.sql_varchar,
                descriptor.Length.GetValueOrDefault(17),
                isUnicode: false,
                descriptor.IsFixedLength.GetValueOrDefault(false)
            ),
            _ => TypeMappingHelpers.CreateStringType(
                descriptor.IsFixedLength == true ? SqliteTypes.sql_char : SqliteTypes.sql_varchar,
                descriptor.Length.GetValueOrDefault(50),
                isUnicode: false,
                descriptor.IsFixedLength.GetValueOrDefault(false)
            ),
        };
    }

    /// <inheritdoc />
    public Type[] GetSupportedGeometryTypes()
    {
        return TypeMappingHelpers.GetGeometryTypesForProvider("sqlite");
    }
}
