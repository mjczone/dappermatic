// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Providers.SqlServer;

/// <summary>
/// SQL Server specific type mapping configuration.
/// </summary>
public class SqlServerTypeMapping : ProviderTypeMappingBase
{
    /// <inheritdoc />
    public override string BooleanType => SqlServerTypes.sql_bit;

    /// <inheritdoc />
    public override Dictionary<Type, string> NumericTypeMap { get; } =
        new()
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
    public override SqlTypeDescriptor CreateGuidType()
    {
        return TypeMappingHelpers.CreateSimpleType(SqlServerTypes.sql_uniqueidentifier);
    }

    /// <inheritdoc />
    public override SqlTypeDescriptor CreateObjectType(DotnetTypeDescriptor descriptor)
    {
        // Objects are stored as JSON strings - use VARCHAR(MAX) or NVARCHAR(MAX) based on unicode flag
        var sqlType =
            descriptor.IsUnicode == true ? $"{SqlServerTypes.sql_nvarchar}(max)" : $"{SqlServerTypes.sql_varchar}(max)";
        return TypeMappingHelpers.CreateLobType(sqlType, descriptor.IsUnicode.GetValueOrDefault(false));
    }

    /// <inheritdoc />
    public override SqlTypeDescriptor CreateDateTimeType(DotnetTypeDescriptor descriptor)
    {
        return descriptor.DotnetType switch
        {
            Type t when t == typeof(DateTime) => TypeMappingHelpers.CreateDateTimeType(
                SqlServerTypes.sql_datetime2,
                descriptor.Precision
            ),
            Type t when t == typeof(DateTimeOffset) => TypeMappingHelpers.CreateDateTimeType(
                SqlServerTypes.sql_datetimeoffset,
                descriptor.Precision
            ),
            Type t when t == typeof(TimeSpan) => TypeMappingHelpers.CreateSimpleType(SqlServerTypes.sql_time),
            Type t when t == typeof(DateOnly) => TypeMappingHelpers.CreateSimpleType(SqlServerTypes.sql_date),
            Type t when t == typeof(TimeOnly) => TypeMappingHelpers.CreateSimpleType(SqlServerTypes.sql_time),
            _ => TypeMappingHelpers.CreateDateTimeType(SqlServerTypes.sql_datetime2, descriptor.Precision),
        };
    }

    /// <inheritdoc />
    public override SqlTypeDescriptor CreateBinaryType(DotnetTypeDescriptor descriptor)
    {
        // Determine appropriate default length when not specified
        int? actualLength = descriptor.Length;
        if (actualLength == null)
        {
            // Stream types should default to MAX, others to 255
            actualLength =
                (descriptor.DotnetType == typeof(Stream) || descriptor.DotnetType == typeof(MemoryStream))
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
            descriptor.IsFixedLength.GetValueOrDefault(false)
        );
    }

    /// <inheritdoc />
    public override SqlTypeDescriptor CreateXmlType(DotnetTypeDescriptor descriptor)
    {
        return TypeMappingHelpers.CreateSimpleType(SqlServerTypes.sql_xml);
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
            sqlType = isUnicode ? $"{SqlServerTypes.sql_nvarchar}(max)" : $"{SqlServerTypes.sql_varchar}(max)";
            return new SqlTypeDescriptor(sqlType)
            {
                Length = TypeMappingDefaults.MaxLength,
                IsUnicode = isUnicode,
                IsFixedLength = false,
            };
        }

        if (isFixedLength)
        {
            sqlType = isUnicode ? $"{SqlServerTypes.sql_nchar}({length})" : $"{SqlServerTypes.sql_char}({length})";
            return new SqlTypeDescriptor(sqlType)
            {
                Length = length,
                IsUnicode = isUnicode,
                IsFixedLength = isFixedLength,
            };
        }
        else
        {
            sqlType = isUnicode
                ? $"{SqlServerTypes.sql_nvarchar}({length})"
                : $"{SqlServerTypes.sql_varchar}({length})";
            return new SqlTypeDescriptor(sqlType)
            {
                Length = length,
                IsUnicode = isUnicode,
                IsFixedLength = isFixedLength,
            };
        }
    }
}
