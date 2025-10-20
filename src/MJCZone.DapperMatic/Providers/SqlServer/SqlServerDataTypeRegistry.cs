// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Providers.SqlServer;

/// <summary>
/// Registry of SQL Server data types.
/// </summary>
public class SqlServerDataTypeRegistry : ProviderDataTypeRegistryBase
{
    /// <inheritdoc />
    protected override void RegisterDataTypes()
    {
        // Integer types
        RegisterDataType(CreateIntegerType("bit", "Boolean value (0 or 1)", isCommon: true));
        RegisterDataType(CreateIntegerType("tinyint", "0 to 255", isCommon: true));
        RegisterDataType(CreateIntegerType("smallint", "-32,768 to 32,767", isCommon: false));
        RegisterDataType(CreateIntegerType("int", "-2,147,483,648 to 2,147,483,647", isCommon: true, "integer"));
        RegisterDataType(
            CreateIntegerType("bigint", "-9,223,372,036,854,775,808 to 9,223,372,036,854,775,807", isCommon: true)
        );

        // Decimal types
        RegisterDataType(
            CreateDecimalType(
                "decimal",
                38,
                38,
                18,
                2,
                isCommon: true,
                "Fixed precision and scale numeric value",
                "numeric"
            )
        );
        RegisterDataType(CreateDecimalType("numeric", 38, 38, 18, 2, isCommon: false, "Synonym for decimal"));
        RegisterDataType(
            CreateSimpleType(
                "float",
                DataTypeCategory.Decimal,
                isCommon: false,
                "Approximate numeric with floating precision"
            )
        );
        RegisterDataType(
            CreateSimpleType("real", DataTypeCategory.Decimal, isCommon: false, "Single precision floating point")
        );

        // Money types
        RegisterDataType(
            CreateSimpleType(
                "money",
                DataTypeCategory.Money,
                isCommon: false,
                "-922,337,203,685,477.5808 to 922,337,203,685,477.5807"
            )
        );
        RegisterDataType(
            CreateSimpleType("smallmoney", DataTypeCategory.Money, isCommon: false, "-214,748.3648 to 214,748.3647")
        );

        // String types
        RegisterDataType(CreateStringType("char", 8000, 1, isCommon: false, "Fixed-length non-Unicode string"));
        RegisterDataType(CreateStringType("varchar", 8000, 255, isCommon: true, "Variable-length non-Unicode string"));
        RegisterDataType(
            CreateSimpleType(
                "varchar(max)",
                DataTypeCategory.Text,
                isCommon: true,
                "Variable-length non-Unicode string up to 2GB"
            )
        );
        RegisterDataType(CreateStringType("nchar", 4000, 1, isCommon: false, "Fixed-length Unicode string"));
        RegisterDataType(CreateStringType("nvarchar", 4000, 255, isCommon: true, "Variable-length Unicode string"));
        RegisterDataType(
            CreateSimpleType(
                "nvarchar(max)",
                DataTypeCategory.Text,
                isCommon: true,
                "Variable-length Unicode string up to 2GB"
            )
        );
        RegisterDataType(
            CreateSimpleType(
                "text",
                DataTypeCategory.Text,
                isCommon: false,
                "Deprecated: Variable-length non-Unicode data"
            )
        );
        RegisterDataType(
            CreateSimpleType(
                "ntext",
                DataTypeCategory.Text,
                isCommon: false,
                "Deprecated: Variable-length Unicode data"
            )
        );

        // Date/Time types
        RegisterDataType(
            CreateSimpleType("date", DataTypeCategory.DateTime, isCommon: true, "Date only (0001-01-01 to 9999-12-31)")
        );
        RegisterDataType(
            CreateDateTimeType("time", true, 7, 7, isCommon: false, "Time only (00:00:00.0000000 to 23:59:59.9999999)")
        );
        RegisterDataType(
            CreateSimpleType(
                "datetime",
                DataTypeCategory.DateTime,
                isCommon: true,
                "Date and time (1753-01-01 to 9999-12-31)"
            )
        );
        RegisterDataType(
            CreateDateTimeType(
                "datetime2",
                true,
                7,
                7,
                isCommon: true,
                "Date and time with higher precision (0001-01-01 to 9999-12-31)"
            )
        );
        RegisterDataType(
            CreateDateTimeType("datetimeoffset", true, 7, 7, isCommon: false, "Date and time with time zone awareness")
        );
        RegisterDataType(
            CreateSimpleType(
                "smalldatetime",
                DataTypeCategory.DateTime,
                isCommon: false,
                "Date and time (1900-01-01 to 2079-06-06)"
            )
        );

        // Binary types
        RegisterDataType(CreateBinaryType("binary", 8000, 1, isCommon: false, "Fixed-length binary data"));
        RegisterDataType(CreateBinaryType("varbinary", 8000, 1, isCommon: true, "Variable-length binary data"));
        RegisterDataType(
            CreateSimpleType(
                "varbinary(max)",
                DataTypeCategory.Binary,
                isCommon: true,
                "Variable-length binary data up to 2GB"
            )
        );
        RegisterDataType(
            CreateSimpleType(
                "image",
                DataTypeCategory.Binary,
                isCommon: false,
                "Deprecated: Variable-length binary data"
            )
        );

        // Special types
        RegisterDataType(
            CreateSimpleType(
                "uniqueidentifier",
                DataTypeCategory.Identifier,
                isCommon: true,
                "GUID (globally unique identifier)"
            )
        );
        RegisterDataType(CreateSimpleType("xml", DataTypeCategory.Xml, isCommon: false, "XML data"));
        RegisterDataType(
            CreateSimpleType(
                "sql_variant",
                DataTypeCategory.Other,
                isCommon: false,
                "Stores values of various data types"
            )
        );
        RegisterDataType(
            CreateSimpleType(
                "hierarchyid",
                DataTypeCategory.Other,
                isCommon: false,
                "Represents position in a hierarchy"
            )
        );
        RegisterDataType(
            CreateSimpleType(
                "geography",
                DataTypeCategory.Spatial,
                isCommon: false,
                "Spatial data type for geographical data"
            )
        );
        RegisterDataType(
            CreateSimpleType("geometry", DataTypeCategory.Spatial, isCommon: false, "Spatial data type for planar data")
        );
        RegisterDataType(
            CreateSimpleType(
                "timestamp",
                DataTypeCategory.Other,
                isCommon: false,
                "Deprecated: Automatically generated binary number",
                "rowversion"
            )
        );
        RegisterDataType(
            CreateSimpleType(
                "rowversion",
                DataTypeCategory.Other,
                isCommon: false,
                "Automatically generated unique binary number"
            )
        );
    }
}
