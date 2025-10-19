// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Providers.Sqlite;

/// <summary>
/// Registry of SQLite data types.
/// </summary>
public class SqliteDataTypeRegistry : ProviderDataTypeRegistryBase
{
    /// <inheritdoc />
    protected override void RegisterDataTypes()
    {
        // SQLite has only 5 fundamental data types, but supports many type names for compatibility

        // INTEGER type and aliases
        RegisterDataType(
            CreateIntegerType(
                "integer",
                "Signed integer value",
                isCommon: true,
                "int",
                "tinyint",
                "smallint",
                "mediumint",
                "bigint",
                "unsigned big int",
                "int2",
                "int8"
            )
        );

        // REAL type and aliases
        RegisterDataType(
            CreateSimpleType(
                "real",
                DataTypeCategory.Decimal,
                isCommon: true,
                "Floating point value",
                "double",
                "double precision",
                "float"
            )
        );

        // TEXT type and aliases
        RegisterDataType(
            CreateSimpleType(
                "text",
                DataTypeCategory.Text,
                isCommon: true,
                "Text string (UTF-8 or UTF-16)",
                "character",
                "varchar",
                "varying character",
                "nchar",
                "native character",
                "nvarchar",
                "clob"
            )
        );

        // BLOB type
        RegisterDataType(
            CreateSimpleType(
                "blob",
                DataTypeCategory.Binary,
                isCommon: true,
                "Binary large object - stores data exactly as input"
            )
        );

        // NUMERIC type and aliases
        RegisterDataType(
            CreateSimpleType(
                "numeric",
                DataTypeCategory.Decimal,
                isCommon: false,
                "Can store INTEGER, REAL, or TEXT",
                "decimal",
                "boolean",
                "date",
                "datetime"
            )
        );

        // Additional commonly used type names (all map to the 5 fundamental types above)
        RegisterDataType(
            CreateStringType(
                "varchar",
                int.MaxValue,
                255,
                isCommon: true,
                "Variable-length character string (maps to TEXT)"
            )
        );

        RegisterDataType(CreateStringType("char", int.MaxValue, 1, isCommon: false, "Character string (maps to TEXT)"));

        RegisterDataType(
            CreateSimpleType(
                "boolean",
                DataTypeCategory.Boolean,
                isCommon: true,
                "Boolean value (stored as INTEGER 0 or 1)"
            )
        );

        RegisterDataType(
            CreateSimpleType(
                "date",
                DataTypeCategory.DateTime,
                isCommon: true,
                "Date value (stored as TEXT, REAL, or INTEGER)"
            )
        );

        RegisterDataType(
            CreateSimpleType(
                "datetime",
                DataTypeCategory.DateTime,
                isCommon: true,
                "Date and time value (stored as TEXT, REAL, or INTEGER)"
            )
        );

        RegisterDataType(
            CreateSimpleType(
                "time",
                DataTypeCategory.DateTime,
                isCommon: true,
                "Time value (stored as TEXT, REAL, or INTEGER)"
            )
        );

        RegisterDataType(
            CreateSimpleType(
                "timestamp",
                DataTypeCategory.DateTime,
                isCommon: false,
                "Timestamp value (stored as TEXT, REAL, or INTEGER)"
            )
        );

        RegisterDataType(
            CreateDecimalType("decimal", 1000, 1000, 18, 2, isCommon: false, "Decimal number (stored as TEXT or REAL)")
        );

        // SQLite specific features
        RegisterDataType(
            CreateSimpleType(
                "json",
                DataTypeCategory.Json,
                isCommon: false,
                "JSON data (stored as TEXT with JSON functions available)"
            )
        );

        // Note: SQLite doesn't have true constraints on these types - they're mainly for compatibility
        // The actual storage class is determined by the affinity rules
    }
}
