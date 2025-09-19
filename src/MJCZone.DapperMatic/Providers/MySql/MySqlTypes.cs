// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace MJCZone.DapperMatic.Providers.MySql;

/// <summary>
/// Provides constants for MySQL data types.
/// </summary>
[SuppressMessage(
    "ReSharper",
    "InconsistentNaming",
    Justification = "Constants are named to match MySQL documentation."
)]
public static class MySqlTypes
{
    /// <summary>Represents the MySQL boolean type.</summary>
    public const string sql_bool = "bool";

    /// <summary>Represents the MySQL boolean type.</summary>
    public const string sql_boolean = "boolean";

    // integers

    /// <summary>Represents the MySQL bit type.</summary>
    public const string sql_bit = "bit";

    /// <summary>Represents the MySQL tinyint type.</summary>
    public const string sql_tinyint = "tinyint";

    /// <summary>Represents the MySQL unsigned tinyint type.</summary>
    public const string sql_tinyint_unsigned = "tinyint unsigned";

    /// <summary>Represents the MySQL smallint type.</summary>
    public const string sql_smallint = "smallint";

    /// <summary>Represents the MySQL unsigned smallint type.</summary>
    public const string sql_smallint_unsigned = "smallint unsigned";

    /// <summary>Represents the MySQL mediumint type.</summary>
    public const string sql_mediumint = "mediumint";

    /// <summary>Represents the MySQL unsigned mediumint type.</summary>
    public const string sql_mediumint_unsigned = "mediumint unsigned";

    /// <summary>Represents the MySQL integer type.</summary>
    public const string sql_integer = "integer";

    /// <summary>Represents the MySQL unsigned integer type.</summary>
    public const string sql_integer_unsigned = "integer unsigned";

    /// <summary>Represents the MySQL int type.</summary>
    public const string sql_int = "int";

    /// <summary>Represents the MySQL unsigned int type.</summary>
    public const string sql_int_unsigned = "int unsigned";

    /// <summary>Represents the MySQL bigint type.</summary>
    public const string sql_bigint = "bigint";

    /// <summary>Represents the MySQL unsigned bigint type.</summary>
    public const string sql_bigint_unsigned = "bigint unsigned";

    /// <summary>Represents the MySQL serial type.</summary>
    public const string sql_serial = "serial";

    // real

    /// <summary>Represents the MySQL decimal type.</summary>
    public const string sql_decimal = "decimal";

    /// <summary>Represents the MySQL decimal type.</summary>
    public const string sql_dec = "dec";

    /// <summary>Represents the MySQL fixed type.</summary>
    public const string sql_fixed = "fixed";

    /// <summary>Represents the MySQL numeric type.</summary>
    public const string sql_numeric = "numeric";

    /// <summary>Represents the MySQL float type.</summary>
    public const string sql_float = "float";

    /// <summary>Represents the MySQL real type.</summary>
    public const string sql_real = "real";

    /// <summary>Represents the MySQL double precision type.</summary>
    public const string sql_double_precision = "double precision";

    /// <summary>Represents the MySQL unsigned double precision type.</summary>
    public const string sql_double_precision_unsigned = "double precision unsigned";

    /// <summary>Represents the MySQL double type.</summary>
    public const string sql_double = "double";

    /// <summary>Represents the MySQL unsigned double type.</summary>
    public const string sql_double_unsigned = "double unsigned";

    // datetime

    /// <summary>Represents the MySQL datetime type.</summary>
    public const string sql_datetime = "datetime";

    /// <summary>Represents the MySQL timestamp type.</summary>
    public const string sql_timestamp = "timestamp";

    /// <summary>Represents the MySQL time type.</summary>
    public const string sql_time = "time";

    /// <summary>Represents the MySQL date type.</summary>
    public const string sql_date = "date";

    /// <summary>Represents the MySQL year type.</summary>
    public const string sql_year = "year";

    // text

    /// <summary>Represents the MySQL char type.</summary>
    public const string sql_char = "char";

    /// <summary>Represents the MySQL varchar type.</summary>
    public const string sql_varchar = "varchar";

    /// <summary>Represents the MySQL long varchar type.</summary>
    public const string sql_long_varchar = "long varchar";

    /// <summary>Represents the MySQL tinytext type.</summary>
    public const string sql_tinytext = "tinytext";

    /// <summary>Represents the MySQL mediumtext type.</summary>
    public const string sql_mediumtext = "mediumtext";

    /// <summary>Represents the MySQL text type.</summary>
    public const string sql_text = "text";

    /// <summary>Represents the MySQL longtext type.</summary>
    public const string sql_longtext = "longtext";

    /// <summary>Represents the MySQL enum type.</summary>
    public const string sql_enum = "enum";

    /// <summary>Represents the MySQL set type.</summary>
    public const string sql_set = "set"; // csv of strings 'a,b,c'

    /// <summary>Represents the MySQL json type.</summary>
    public const string sql_json = "json";

    // binary

    /// <summary>Represents the MySQL binary type.</summary>
    public const string sql_binary = "binary";

    /// <summary>Represents the MySQL varbinary type.</summary>
    public const string sql_varbinary = "varbinary";

    /// <summary>Represents the MySQL long varbinary type.</summary>
    public const string sql_long_varbinary = "long varbinary";

    /// <summary>Represents the MySQL tinyblob type.</summary>
    public const string sql_tinyblob = "tinyblob";

    /// <summary>Represents the MySQL blob type.</summary>
    public const string sql_blob = "blob";

    /// <summary>Represents the MySQL mediumblob type.</summary>
    public const string sql_mediumblob = "mediumblob";

    /// <summary>Represents the MySQL longblob type.</summary>
    public const string sql_longblob = "longblob";

    // geometry

    /// <summary>Represents the MySQL geometry type.</summary>
    public const string sql_geometry = "geometry";

    /// <summary>Represents the MySQL point type.</summary>
    public const string sql_point = "point";

    /// <summary>Represents the MySQL linestring type.</summary>
    public const string sql_linestring = "linestring";

    /// <summary>Represents the MySQL polygon type.</summary>
    public const string sql_polygon = "polygon";

    /// <summary>Represents the MySQL multipoint type.</summary>
    public const string sql_multipoint = "multipoint";

    /// <summary>Represents the MySQL multilinestring type.</summary>
    public const string sql_multilinestring = "multilinestring";

    /// <summary>Represents the MySQL multipolygon type.</summary>
    public const string sql_multipolygon = "multipolygon";

    /// <summary>Represents the MySQL geomcollection type.</summary>
    public const string sql_geomcollection = "geomcollection";

    /// <summary>Represents the MySQL geometrycollection type.</summary>
    public const string sql_geometrycollection = "geometrycollection";
}
