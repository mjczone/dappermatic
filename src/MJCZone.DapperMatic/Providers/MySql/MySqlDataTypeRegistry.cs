// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Providers.MySql;

/// <summary>
/// Registry of MySQL data types.
/// </summary>
public class MySqlDataTypeRegistry : ProviderDataTypeRegistryBase
{
    /// <inheritdoc />
    protected override void RegisterDataTypes()
    {
        // Integer types
        RegisterDataType(
            CreateIntegerType("tinyint", "1-byte signed integer (-128 to 127)", isCommon: false)
        );
        RegisterDataType(
            CreateIntegerType(
                "smallint",
                "2-byte signed integer (-32768 to 32767)",
                isCommon: false
            )
        );
        RegisterDataType(
            CreateIntegerType(
                "mediumint",
                "3-byte signed integer (-8388608 to 8388607)",
                isCommon: false
            )
        );
        RegisterDataType(
            CreateIntegerType("int", "4-byte signed integer", isCommon: true, "integer")
        );
        RegisterDataType(CreateIntegerType("bigint", "8-byte signed integer", isCommon: true));

        // Boolean type
        RegisterDataType(
            CreateSimpleType(
                "boolean",
                DataTypeCategory.Boolean,
                isCommon: true,
                "Synonym for TINYINT(1)",
                "bool"
            )
        );

        // Bit type
        RegisterDataType(CreateStringType("bit", 64, 1, isCommon: false, "Bit-value type"));

        // Decimal types
        RegisterDataType(
            CreateDecimalType(
                "decimal",
                65,
                30,
                10,
                2,
                isCommon: true,
                "Fixed-point number",
                "dec",
                "numeric",
                "fixed"
            )
        );
        RegisterDataType(
            CreateSimpleType(
                "float",
                DataTypeCategory.Decimal,
                isCommon: false,
                "Single-precision floating-point number"
            )
        );
        RegisterDataType(
            CreateSimpleType(
                "double",
                DataTypeCategory.Decimal,
                isCommon: false,
                "Double-precision floating-point number",
                "real"
            )
        );

        // String types
        RegisterDataType(
            CreateStringType("char", 255, 1, isCommon: false, "Fixed-length string", "character")
        );
        RegisterDataType(
            CreateStringType("varchar", 65535, 255, isCommon: true, "Variable-length string")
        );
        RegisterDataType(
            CreateStringType("binary", 255, 1, isCommon: false, "Fixed-length binary string")
        );
        RegisterDataType(
            CreateStringType(
                "varbinary",
                65535,
                255,
                isCommon: false,
                "Variable-length binary string"
            )
        );

        // Text types
        RegisterDataType(
            CreateSimpleType(
                "tinytext",
                DataTypeCategory.Text,
                isCommon: false,
                "Very small text string (up to 255 characters)"
            )
        );
        RegisterDataType(
            CreateSimpleType(
                "text",
                DataTypeCategory.Text,
                isCommon: true,
                "Small text string (up to 65,535 characters)"
            )
        );
        RegisterDataType(
            CreateSimpleType(
                "mediumtext",
                DataTypeCategory.Text,
                isCommon: false,
                "Medium text string (up to 16,777,215 characters)"
            )
        );
        RegisterDataType(
            CreateSimpleType(
                "longtext",
                DataTypeCategory.Text,
                isCommon: true,
                "Large text string (up to 4,294,967,295 characters)"
            )
        );

        // Binary types
        RegisterDataType(
            CreateSimpleType(
                "tinyblob",
                DataTypeCategory.Binary,
                isCommon: false,
                "Very small binary object (up to 255 bytes)"
            )
        );
        RegisterDataType(
            CreateSimpleType(
                "blob",
                DataTypeCategory.Binary,
                isCommon: true,
                "Small binary object (up to 65,535 bytes)"
            )
        );
        RegisterDataType(
            CreateSimpleType(
                "mediumblob",
                DataTypeCategory.Binary,
                isCommon: false,
                "Medium binary object (up to 16,777,215 bytes)"
            )
        );
        RegisterDataType(
            CreateSimpleType(
                "longblob",
                DataTypeCategory.Binary,
                isCommon: true,
                "Large binary object (up to 4,294,967,295 bytes)"
            )
        );

        // Date/Time types
        RegisterDataType(
            CreateSimpleType(
                "date",
                DataTypeCategory.DateTime,
                isCommon: true,
                "Date value (YYYY-MM-DD)"
            )
        );
        RegisterDataType(
            CreateDateTimeType("time", true, 6, 0, isCommon: false, "Time value (HH:MM:SS)")
        );
        RegisterDataType(
            CreateDateTimeType("datetime", true, 6, 0, isCommon: true, "Date and time value")
        );
        RegisterDataType(
            CreateDateTimeType(
                "timestamp",
                true,
                6,
                0,
                isCommon: true,
                "Timestamp value with automatic initialization and updating"
            )
        );
        RegisterDataType(
            CreateSimpleType(
                "year",
                DataTypeCategory.DateTime,
                isCommon: false,
                "Year in 4-digit format"
            )
        );

        // JSON type (MySQL 5.7+)
        RegisterDataType(
            CreateSimpleType("json", DataTypeCategory.Json, isCommon: true, "Native JSON data type")
        );

        // Spatial types (MySQL with spatial extensions)
        RegisterDataType(
            CreateSimpleType(
                "geometry",
                DataTypeCategory.Spatial,
                isCommon: false,
                "Spatial geometry data"
            )
        );
        RegisterDataType(
            CreateSimpleType(
                "point",
                DataTypeCategory.Spatial,
                isCommon: false,
                "Point in 2D space"
            )
        );
        RegisterDataType(
            CreateSimpleType(
                "linestring",
                DataTypeCategory.Spatial,
                isCommon: false,
                "Curve with linear interpolation between points"
            )
        );
        RegisterDataType(
            CreateSimpleType(
                "polygon",
                DataTypeCategory.Spatial,
                isCommon: false,
                "Polygon (closed surface)"
            )
        );
        RegisterDataType(
            CreateSimpleType(
                "multipoint",
                DataTypeCategory.Spatial,
                isCommon: false,
                "Collection of Point values"
            )
        );
        RegisterDataType(
            CreateSimpleType(
                "multilinestring",
                DataTypeCategory.Spatial,
                isCommon: false,
                "Collection of LineString values"
            )
        );
        RegisterDataType(
            CreateSimpleType(
                "multipolygon",
                DataTypeCategory.Spatial,
                isCommon: false,
                "Collection of Polygon values"
            )
        );
        RegisterDataType(
            CreateSimpleType(
                "geometrycollection",
                DataTypeCategory.Spatial,
                isCommon: false,
                "Collection of geometry values"
            )
        );

        // Enum and Set types
        RegisterDataType(
            CreateSimpleType(
                "enum",
                DataTypeCategory.Other,
                isCommon: true,
                "Enumeration of string values"
            )
        );
        RegisterDataType(
            CreateSimpleType("set", DataTypeCategory.Other, isCommon: false, "Set of string values")
        );
    }
}
