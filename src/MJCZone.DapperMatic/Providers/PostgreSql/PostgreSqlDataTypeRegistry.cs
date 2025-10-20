// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Providers.PostgreSql;

/// <summary>
/// Registry of PostgreSQL data types.
/// </summary>
public class PostgreSqlDataTypeRegistry : ProviderDataTypeRegistryBase
{
    /// <inheritdoc />
    protected override void RegisterDataTypes()
    {
        // Integer types
        RegisterDataType(CreateIntegerType("smallint", "2-byte signed integer", isCommon: false, "int2"));
        RegisterDataType(CreateIntegerType("integer", "4-byte signed integer", isCommon: true, "int", "int4"));
        RegisterDataType(CreateIntegerType("bigint", "8-byte signed integer", isCommon: true, "int8"));
        RegisterDataType(
            CreateIntegerType("smallserial", "2-byte autoincrementing integer", isCommon: false, "serial2")
        );
        RegisterDataType(CreateIntegerType("serial", "4-byte autoincrementing integer", isCommon: true, "serial4"));
        RegisterDataType(CreateIntegerType("bigserial", "8-byte autoincrementing integer", isCommon: true, "serial8"));

        // Decimal types
        RegisterDataType(
            CreateDecimalType(
                "numeric",
                1000,
                1000,
                18,
                2,
                isCommon: true,
                "User-specified precision, exact",
                "decimal"
            )
        );
        RegisterDataType(
            CreateSimpleType(
                "real",
                DataTypeCategory.Decimal,
                isCommon: false,
                "Single precision floating-point number",
                "float4"
            )
        );
        RegisterDataType(
            CreateSimpleType(
                "double precision",
                DataTypeCategory.Decimal,
                isCommon: false,
                "Double precision floating-point number",
                "float8"
            )
        );

        // Money type
        RegisterDataType(CreateSimpleType("money", DataTypeCategory.Money, isCommon: false, "Currency amount"));

        // String types
        RegisterDataType(
            CreateStringType(
                "character",
                10485760,
                1,
                isCommon: false,
                "Fixed-length character string",
                "char",
                "bpchar"
            )
        );
        RegisterDataType(
            CreateStringType(
                "character varying",
                10485760,
                255,
                isCommon: true,
                "Variable-length character string",
                "varchar"
            )
        );
        RegisterDataType(
            CreateSimpleType(
                "\"char\"",
                DataTypeCategory.Text,
                isCommon: false,
                "Internal single-byte character type (fixed 1 byte, no modifiers)"
            )
        );
        RegisterDataType(
            CreateSimpleType(
                "text",
                DataTypeCategory.Text,
                isCommon: true,
                "Variable unlimited length character string"
            )
        );

        // Boolean type
        RegisterDataType(
            CreateSimpleType("boolean", DataTypeCategory.Boolean, isCommon: true, "True or false", "bool")
        );

        // Date/Time types
        RegisterDataType(
            CreateSimpleType("date", DataTypeCategory.DateTime, isCommon: true, "Calendar date (year, month, day)")
        );
        RegisterDataType(CreateDateTimeType("time", true, 6, 6, isCommon: false, "Time of day (no time zone)"));
        RegisterDataType(
            CreateDateTimeType(
                "time with time zone",
                true,
                6,
                6,
                isCommon: false,
                "Time of day, including time zone",
                "timetz"
            )
        );
        RegisterDataType(CreateDateTimeType("timestamp", true, 6, 6, isCommon: true, "Date and time (no time zone)"));
        RegisterDataType(
            CreateDateTimeType(
                "timestamp with time zone",
                true,
                6,
                6,
                isCommon: true,
                "Date and time, including time zone",
                "timestamptz"
            )
        );
        RegisterDataType(CreateSimpleType("interval", DataTypeCategory.DateTime, isCommon: false, "Time span"));

        // Binary type
        RegisterDataType(
            CreateSimpleType("bytea", DataTypeCategory.Binary, isCommon: true, "Binary data (byte array)")
        );

        // Network types
        RegisterDataType(
            CreateSimpleType("cidr", DataTypeCategory.Network, isCommon: false, "IPv4 or IPv6 network address")
        );
        RegisterDataType(
            CreateSimpleType("inet", DataTypeCategory.Network, isCommon: false, "IPv4 or IPv6 host address")
        );
        RegisterDataType(
            CreateSimpleType("macaddr", DataTypeCategory.Network, isCommon: false, "MAC (Media Access Control) address")
        );
        RegisterDataType(
            CreateSimpleType(
                "macaddr8",
                DataTypeCategory.Network,
                isCommon: false,
                "MAC (Media Access Control) address (EUI-64 format)"
            )
        );

        // Geometric types
        RegisterDataType(CreateSimpleType("point", DataTypeCategory.Spatial, isCommon: false, "Point on a plane"));
        RegisterDataType(CreateSimpleType("line", DataTypeCategory.Spatial, isCommon: false, "Infinite line"));
        RegisterDataType(CreateSimpleType("lseg", DataTypeCategory.Spatial, isCommon: false, "Line segment"));
        RegisterDataType(CreateSimpleType("box", DataTypeCategory.Spatial, isCommon: false, "Rectangular box"));
        RegisterDataType(CreateSimpleType("path", DataTypeCategory.Spatial, isCommon: false, "Geometric path"));
        RegisterDataType(
            CreateSimpleType("polygon", DataTypeCategory.Spatial, isCommon: false, "Closed geometric path")
        );
        RegisterDataType(CreateSimpleType("circle", DataTypeCategory.Spatial, isCommon: false, "Circle"));
        RegisterDataType(
            CreateSimpleType("geometry", DataTypeCategory.Spatial, isCommon: false, "PostGIS geometry type")
        );
        RegisterDataType(
            CreateSimpleType("geography", DataTypeCategory.Spatial, isCommon: false, "PostGIS geography type")
        );

        // JSON types
        RegisterDataType(CreateSimpleType("json", DataTypeCategory.Json, isCommon: true, "JSON data"));
        RegisterDataType(
            CreateSimpleType("jsonb", DataTypeCategory.Json, isCommon: true, "Binary JSON data (recommended)")
        );

        // XML type
        RegisterDataType(CreateSimpleType("xml", DataTypeCategory.Xml, isCommon: false, "XML data"));

        // UUID type
        RegisterDataType(
            CreateSimpleType("uuid", DataTypeCategory.Identifier, isCommon: true, "Universally unique identifier")
        );

        // Bit string types
        RegisterDataType(CreateStringType("bit", 83886080, 1, isCommon: false, "Fixed-length bit string"));
        RegisterDataType(
            CreateStringType("bit varying", 83886080, 1, isCommon: false, "Variable-length bit string", "varbit")
        );

        // Text search types
        RegisterDataType(CreateSimpleType("tsvector", DataTypeCategory.Other, isCommon: false, "Text search vector"));
        RegisterDataType(CreateSimpleType("tsquery", DataTypeCategory.Other, isCommon: false, "Text search query"));

        // Range types (PostgreSQL specific)
        RegisterDataType(CreateSimpleType("int4range", DataTypeCategory.Range, isCommon: false, "Range of integer"));
        RegisterDataType(CreateSimpleType("int8range", DataTypeCategory.Range, isCommon: false, "Range of bigint"));
        RegisterDataType(CreateSimpleType("numrange", DataTypeCategory.Range, isCommon: false, "Range of numeric"));
        RegisterDataType(
            CreateSimpleType("tsrange", DataTypeCategory.Range, isCommon: false, "Range of timestamp without time zone")
        );
        RegisterDataType(
            CreateSimpleType("tstzrange", DataTypeCategory.Range, isCommon: false, "Range of timestamp with time zone")
        );
        RegisterDataType(CreateSimpleType("daterange", DataTypeCategory.Range, isCommon: false, "Range of date"));

        // Array types - standard notation (suffix [])
        RegisterDataType(CreateSimpleType("boolean[]", DataTypeCategory.Array, isCommon: false, "Array of boolean"));
        RegisterDataType(CreateSimpleType("smallint[]", DataTypeCategory.Array, isCommon: false, "Array of smallint"));
        RegisterDataType(CreateSimpleType("integer[]", DataTypeCategory.Array, isCommon: false, "Array of integers"));
        RegisterDataType(CreateSimpleType("bigint[]", DataTypeCategory.Array, isCommon: false, "Array of bigint"));
        RegisterDataType(CreateSimpleType("real[]", DataTypeCategory.Array, isCommon: false, "Array of real"));
        RegisterDataType(
            CreateSimpleType("double precision[]", DataTypeCategory.Array, isCommon: false, "Array of double precision")
        );
        RegisterDataType(CreateSimpleType("numeric[]", DataTypeCategory.Array, isCommon: false, "Array of numeric"));
        RegisterDataType(CreateSimpleType("text[]", DataTypeCategory.Array, isCommon: false, "Array of text"));
        RegisterDataType(
            CreateSimpleType("character[]", DataTypeCategory.Array, isCommon: false, "Array of character")
        );
        RegisterDataType(
            CreateSimpleType(
                "character varying[]",
                DataTypeCategory.Array,
                isCommon: false,
                "Array of character varying"
            )
        );
        RegisterDataType(CreateSimpleType("bytea[]", DataTypeCategory.Array, isCommon: false, "Array of bytea"));
        RegisterDataType(
            CreateSimpleType("timestamp[]", DataTypeCategory.Array, isCommon: false, "Array of timestamp")
        );
        RegisterDataType(
            CreateSimpleType(
                "timestamp without time zone[]",
                DataTypeCategory.Array,
                isCommon: false,
                "Array of timestamp without time zone"
            )
        );
        RegisterDataType(
            CreateSimpleType(
                "timestamp with time zone[]",
                DataTypeCategory.Array,
                isCommon: false,
                "Array of timestamp with time zone"
            )
        );
        RegisterDataType(CreateSimpleType("date[]", DataTypeCategory.Array, isCommon: false, "Array of date"));
        RegisterDataType(CreateSimpleType("time[]", DataTypeCategory.Array, isCommon: false, "Array of time"));
        RegisterDataType(
            CreateSimpleType(
                "time without time zone[]",
                DataTypeCategory.Array,
                isCommon: false,
                "Array of time without time zone"
            )
        );
        RegisterDataType(
            CreateSimpleType(
                "time with time zone[]",
                DataTypeCategory.Array,
                isCommon: false,
                "Array of time with time zone"
            )
        );
        RegisterDataType(CreateSimpleType("interval[]", DataTypeCategory.Array, isCommon: false, "Array of interval"));
        RegisterDataType(CreateSimpleType("uuid[]", DataTypeCategory.Array, isCommon: false, "Array of uuid"));
        RegisterDataType(CreateSimpleType("json[]", DataTypeCategory.Array, isCommon: false, "Array of json"));
        RegisterDataType(CreateSimpleType("jsonb[]", DataTypeCategory.Array, isCommon: false, "Array of jsonb"));

        // Array types - PostgreSQL internal notation (prefix _)
        RegisterDataType(
            CreateSimpleType("_bool", DataTypeCategory.Array, isCommon: false, "Array of boolean (internal notation)")
        );
        RegisterDataType(
            CreateSimpleType("_int2", DataTypeCategory.Array, isCommon: false, "Array of smallint (internal notation)")
        );
        RegisterDataType(
            CreateSimpleType("_int4", DataTypeCategory.Array, isCommon: false, "Array of integer (internal notation)")
        );
        RegisterDataType(
            CreateSimpleType("_int8", DataTypeCategory.Array, isCommon: false, "Array of bigint (internal notation)")
        );
        RegisterDataType(
            CreateSimpleType("_float4", DataTypeCategory.Array, isCommon: false, "Array of real (internal notation)")
        );
        RegisterDataType(
            CreateSimpleType(
                "_float8",
                DataTypeCategory.Array,
                isCommon: false,
                "Array of double precision (internal notation)"
            )
        );
        RegisterDataType(
            CreateSimpleType(
                "_numeric",
                DataTypeCategory.Array,
                isCommon: false,
                "Array of numeric (internal notation)"
            )
        );
        RegisterDataType(
            CreateSimpleType("_text", DataTypeCategory.Array, isCommon: false, "Array of text (internal notation)")
        );
        RegisterDataType(
            CreateSimpleType("_char", DataTypeCategory.Array, isCommon: false, "Array of char (internal notation)")
        );
        RegisterDataType(
            CreateSimpleType(
                "_varchar",
                DataTypeCategory.Array,
                isCommon: false,
                "Array of varchar (internal notation)"
            )
        );
        RegisterDataType(
            CreateSimpleType(
                "_bpchar",
                DataTypeCategory.Array,
                isCommon: false,
                "Array of character (internal notation)"
            )
        );
        RegisterDataType(
            CreateSimpleType("_bytea", DataTypeCategory.Array, isCommon: false, "Array of bytea (internal notation)")
        );
        RegisterDataType(
            CreateSimpleType(
                "_timestamp",
                DataTypeCategory.Array,
                isCommon: false,
                "Array of timestamp (internal notation)"
            )
        );
        RegisterDataType(
            CreateSimpleType(
                "_timestamptz",
                DataTypeCategory.Array,
                isCommon: false,
                "Array of timestamp with time zone (internal notation)"
            )
        );
        RegisterDataType(
            CreateSimpleType("_date", DataTypeCategory.Array, isCommon: false, "Array of date (internal notation)")
        );
        RegisterDataType(
            CreateSimpleType("_time", DataTypeCategory.Array, isCommon: false, "Array of time (internal notation)")
        );
        RegisterDataType(
            CreateSimpleType(
                "_timetz",
                DataTypeCategory.Array,
                isCommon: false,
                "Array of time with time zone (internal notation)"
            )
        );
        RegisterDataType(
            CreateSimpleType(
                "_interval",
                DataTypeCategory.Array,
                isCommon: false,
                "Array of interval (internal notation)"
            )
        );
        RegisterDataType(
            CreateSimpleType("_uuid", DataTypeCategory.Array, isCommon: false, "Array of uuid (internal notation)")
        );
        RegisterDataType(
            CreateSimpleType("_json", DataTypeCategory.Array, isCommon: false, "Array of json (internal notation)")
        );
        RegisterDataType(
            CreateSimpleType("_jsonb", DataTypeCategory.Array, isCommon: false, "Array of jsonb (internal notation)")
        );

        // PostgreSQL specific types
        RegisterDataType(CreateSimpleType("hstore", DataTypeCategory.Other, isCommon: false, "Key-value pairs"));
        RegisterDataType(
            CreateSimpleType(
                "ltree",
                DataTypeCategory.Other,
                isCommon: false,
                "Labels of data stored in a hierarchical tree-like structure"
            )
        );

        // OID types (Object Identifiers)
        RegisterDataType(CreateSimpleType("oid", DataTypeCategory.Other, isCommon: false, "Object identifier"));
        RegisterDataType(CreateSimpleType("regproc", DataTypeCategory.Other, isCommon: false, "Function name"));
        RegisterDataType(
            CreateSimpleType("regprocedure", DataTypeCategory.Other, isCommon: false, "Function with argument types")
        );
        RegisterDataType(CreateSimpleType("regoper", DataTypeCategory.Other, isCommon: false, "Operator name"));
        RegisterDataType(
            CreateSimpleType("regoperator", DataTypeCategory.Other, isCommon: false, "Operator with argument types")
        );
        RegisterDataType(CreateSimpleType("regclass", DataTypeCategory.Other, isCommon: false, "Relation name"));
        RegisterDataType(CreateSimpleType("regtype", DataTypeCategory.Other, isCommon: false, "Data type name"));
        RegisterDataType(
            CreateSimpleType("regconfig", DataTypeCategory.Other, isCommon: false, "Text search configuration")
        );
        RegisterDataType(
            CreateSimpleType("regdictionary", DataTypeCategory.Other, isCommon: false, "Text search dictionary")
        );
    }
}
