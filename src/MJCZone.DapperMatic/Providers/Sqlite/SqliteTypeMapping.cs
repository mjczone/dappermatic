// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Providers.Sqlite;

/// <summary>
/// SQLite specific type mapping configuration.
/// </summary>
public class SqliteTypeMapping : ProviderTypeMappingBase
{
    /// <inheritdoc />
    public override string BooleanType => SqliteTypes.sql_boolean;

    /// <inheritdoc />
    public override bool IsUnicodeProvider => false; // SQLite can handle both, but we default to non-Unicode

    /// <inheritdoc />
    public override Dictionary<Type, string> NumericTypeMap { get; } =
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
    public override SqlTypeDescriptor CreateObjectType(DotnetTypeDescriptor descriptor)
    {
        return TypeMappingHelpers.CreateLobType(SqliteTypes.sql_text, isUnicode: false);
    }

    /// <inheritdoc />
    public override SqlTypeDescriptor CreateDateTimeType(DotnetTypeDescriptor descriptor)
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
    public override SqlTypeDescriptor CreateBinaryType(DotnetTypeDescriptor descriptor)
    {
        // SQLite uses blob for all binary data
        return TypeMappingHelpers.CreateLobType(SqliteTypes.sql_blob, isUnicode: false);
    }

    /// <inheritdoc />
    public override SqlTypeDescriptor CreateXmlType(DotnetTypeDescriptor descriptor)
    {
        // SQLite stores XML as text
        return TypeMappingHelpers.CreateLobType(SqliteTypes.sql_text, isUnicode: false);
    }

    /// <summary>
    /// Creates a SqlTypeDescriptor for string/text types with consistent length and unicode handling.
    /// </summary>
    /// <param name="length">The length.</param>
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
            sqlType = isUnicode
                ? $"{SqliteTypes.sql_nvarchar}({TypeMappingDefaults.MaxLength})"
                : $"{SqliteTypes.sql_varchar}({TypeMappingDefaults.MaxLength})";
            return new SqlTypeDescriptor(sqlType)
            {
                Length = TypeMappingDefaults.MaxLength,
                IsUnicode = isUnicode,
                IsFixedLength = false,
            };
        }

        if (isFixedLength)
        {
            sqlType = isUnicode ? $"{SqliteTypes.sql_nchar}({length})" : $"{SqliteTypes.sql_char}({length})";
            return new SqlTypeDescriptor(sqlType)
            {
                Length = length,
                IsUnicode = isUnicode,
                IsFixedLength = isFixedLength,
            };
        }
        else
        {
            sqlType = isUnicode ? $"{SqliteTypes.sql_nvarchar}({length})" : $"{SqliteTypes.sql_varchar}({length})";
            return new SqlTypeDescriptor(sqlType)
            {
                Length = length,
                IsUnicode = isUnicode,
                IsFixedLength = isFixedLength,
            };
        }
    }
}
