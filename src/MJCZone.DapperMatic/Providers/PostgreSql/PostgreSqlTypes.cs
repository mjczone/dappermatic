// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace MJCZone.DapperMatic.Providers.PostgreSql;

/// <summary>
/// Provides constants for PostgreSQL data types.
/// </summary>
public static class PostgreSqlTypes
{
    // integers

    /// <summary>Represents the PostgreSQL smallint type.</summary>
    public const string sql_smallint = "smallint";

    /// <summary>Represents the PostgreSQL int2 type.</summary>
    public const string sql_int2 = "int2";

    /// <summary>Represents the PostgreSQL smallserial type.</summary>
    public const string sql_smallserial = "smallserial";

    /// <summary>Represents the PostgreSQL serial2 type.</summary>
    public const string sql_serial2 = "serial2";

    /// <summary>Represents the PostgreSQL integer type.</summary>
    public const string sql_integer = "integer";

    /// <summary>Represents the PostgreSQL int type.</summary>
    public const string sql_int = "int";

    /// <summary>Represents the PostgreSQL int4 type.</summary>
    public const string sql_int4 = "int4";

    /// <summary>Represents the PostgreSQL serial type.</summary>
    public const string sql_serial = "serial";

    /// <summary>Represents the PostgreSQL serial4 type.</summary>
    public const string sql_serial4 = "serial4";

    /// <summary>Represents the PostgreSQL bigint type.</summary>
    public const string sql_bigint = "bigint";

    /// <summary>Represents the PostgreSQL int8 type.</summary>
    public const string sql_int8 = "int8";

    /// <summary>Represents the PostgreSQL bigserial type.</summary>
    public const string sql_bigserial = "bigserial";

    /// <summary>Represents the PostgreSQL serial8 type.</summary>
    public const string sql_serial8 = "serial8";

    // real

    /// <summary>Represents the PostgreSQL float4 type.</summary>
    public const string sql_float4 = "float4";

    /// <summary>Represents the PostgreSQL real type.</summary>
    public const string sql_real = "real";

    /// <summary>Represents the PostgreSQL double precision type.</summary>
    public const string sql_double_precision = "double precision";

    /// <summary>Represents the PostgreSQL float8 type.</summary>
    public const string sql_float8 = "float8";

    /// <summary>Represents the PostgreSQL money type.</summary>
    public const string sql_money = "money";

    /// <summary>Represents the PostgreSQL numeric type.</summary>
    public const string sql_numeric = "numeric";

    /// <summary>Represents the PostgreSQL decimal type.</summary>
    public const string sql_decimal = "decimal";

    // bool

    /// <summary>Represents the PostgreSQL bool type.</summary>
    public const string sql_bool = "bool";

    /// <summary>Represents the PostgreSQL boolean type.</summary>
    public const string sql_boolean = "boolean";

    // datetime

    /// <summary>Represents the PostgreSQL date type.</summary>
    public const string sql_date = "date";

    /// <summary>Represents the PostgreSQL interval type.</summary>
    public const string sql_interval = "interval";

    /// <summary>Represents the PostgreSQL time without time zone type.</summary>
    public const string sql_time_without_time_zone = "time without time zone";

    /// <summary>Represents the PostgreSQL time type.</summary>
    public const string sql_time = "time";

    /// <summary>Represents the PostgreSQL time with time zone type.</summary>
    public const string sql_time_with_time_zone = "time with time zone";

    /// <summary>Represents the PostgreSQL timetz type.</summary>
    public const string sql_timetz = "timetz";

    /// <summary>Represents the PostgreSQL timestamp without time zone type.</summary>
    public const string sql_timestamp_without_time_zone = "timestamp without time zone";

    /// <summary>Represents the PostgreSQL timestamp type.</summary>
    public const string sql_timestamp = "timestamp";

    /// <summary>Represents the PostgreSQL timestamp with time zone type.</summary>
    public const string sql_timestamp_with_time_zone = "timestamp with time zone";

    /// <summary>Represents the PostgreSQL timestamptz type.</summary>
    public const string sql_timestamptz = "timestamptz";

    // text

    /// <summary>Represents the PostgreSQL bit type.</summary>
    public const string sql_bit = "bit";

    /// <summary>Represents the PostgreSQL bit varying type.</summary>
    public const string sql_bit_varying = "bit varying";

    /// <summary>Represents the PostgreSQL varbit type.</summary>
    public const string sql_varbit = "varbit";

    /// <summary>Represents the PostgreSQL character varying type.</summary>
    public const string sql_character_varying = "character varying";

    /// <summary>Represents the PostgreSQL varchar type.</summary>
    public const string sql_varchar = "varchar";

    /// <summary>Represents the PostgreSQL character type.</summary>
    public const string sql_character = "character";

    /// <summary>Represents the PostgreSQL char type.</summary>
    public const string sql_char = "char";

    /// <summary>Represents the PostgreSQL bpchar type.</summary>
    public const string sql_bpchar = "bpchar";

    /// <summary>Represents the PostgreSQL "char" type (internal single-byte character).</summary>
    public const string sql_quoted_char = "\"char\"";

    /// <summary>Represents the PostgreSQL text type.</summary>
    public const string sql_text = "text";

    /// <summary>Represents the PostgreSQL name type.</summary>
    public const string sql_name = "name";

    /// <summary>Represents the PostgreSQL uuid type.</summary>
    public const string sql_uuid = "uuid";

    /// <summary>Represents the PostgreSQL json type.</summary>
    public const string sql_json = "json";

    /// <summary>Represents the PostgreSQL jsonb type.</summary>
    public const string sql_jsonb = "jsonb";

    /// <summary>Represents the PostgreSQL jsonpath type.</summary>
    public const string sql_jsonpath = "jsonpath";

    /// <summary>Represents the PostgreSQL xml type.</summary>
    public const string sql_xml = "xml";

    // binary

    /// <summary>Represents the PostgreSQL bytea type.</summary>
    public const string sql_bytea = "bytea";

    // geometry

    /// <summary>Represents the PostgreSQL box type.</summary>
    public const string sql_box = "box";

    /// <summary>Represents the PostgreSQL circle type.</summary>
    public const string sql_circle = "circle";

    /// <summary>Represents the PostgreSQL geometry type.</summary>
    public const string sql_geometry = "geometry";

    /// <summary>Represents the PostgreSQL geometry point type.</summary>
    public const string sql_geometry_point = "geometry(Point)";

    /// <summary>Represents the PostgreSQL geometry linestring type.</summary>
    public const string sql_geometry_linestring = "geometry(LineString)";

    /// <summary>Represents the PostgreSQL geometry polygon type.</summary>
    public const string sql_geometry_polygon = "geometry(Polygon)";

    /// <summary>Represents the PostgreSQL geometry multipoint type.</summary>
    public const string sql_geometry_multipoint = "geometry(MultiPoint)";

    /// <summary>Represents the PostgreSQL geometry multilinestring type.</summary>
    public const string sql_geometry_multilinestring = "geometry(MultiLineString)";

    /// <summary>Represents the PostgreSQL geometry multipolygon type.</summary>
    public const string sql_geometry_multipolygon = "geometry(MultiPolygon)";

    /// <summary>Represents the PostgreSQL geometry collection type.</summary>
    public const string sql_geometry_collection = "geometry(GeometryCollection)";

    /// <summary>Represents the PostgreSQL geography type.</summary>
    public const string sql_geography = "geography";

    /// <summary>Represents the PostgreSQL geography point type.</summary>
    public const string sql_geography_point = "geography(Point)";

    /// <summary>Represents the PostgreSQL geography linestring type.</summary>
    public const string sql_geography_linestring = "geography(LineString)";

    /// <summary>Represents the PostgreSQL geography polygon type.</summary>
    public const string sql_geography_polygon = "geography(Polygon)";

    /// <summary>Represents the PostgreSQL geography multipoint type.</summary>
    public const string sql_geography_multipoint = "geography(MultiPoint)";

    /// <summary>Represents the PostgreSQL geography multilinestring type.</summary>
    public const string sql_geography_multilinestring = "geography(MultiLineString)";

    /// <summary>Represents the PostgreSQL geography multipolygon type.</summary>
    public const string sql_geography_multipolygon = "geography(MultiPolygon)";

    /// <summary>Represents the PostgreSQL geography collection type.</summary>
    public const string sql_geography_collection = "geography(GeometryCollection)";

    /// <summary>Represents the PostgreSQL line type.</summary>
    public const string sql_line = "line";

    /// <summary>Represents the PostgreSQL lseg type.</summary>
    public const string sql_lseg = "lseg";

    /// <summary>Represents the PostgreSQL path type.</summary>
    public const string sql_path = "path";

    /// <summary>Represents the PostgreSQL point type.</summary>
    public const string sql_point = "point";

    /// <summary>Represents the PostgreSQL polygon type.</summary>
    public const string sql_polygon = "polygon";

    // range types

    /// <summary>Represents the PostgreSQL datemultirange type.</summary>
    public const string sql_datemultirange = "datemultirange";

    /// <summary>Represents the PostgreSQL daterange type.</summary>
    public const string sql_daterange = "daterange";

    /// <summary>Represents the PostgreSQL int4multirange type.</summary>
    public const string sql_int4multirange = "int4multirange";

    /// <summary>Represents the PostgreSQL int4range type.</summary>
    public const string sql_int4range = "int4range";

    /// <summary>Represents the PostgreSQL int8multirange type.</summary>
    public const string sql_int8multirange = "int8multirange";

    /// <summary>Represents the PostgreSQL int8range type.</summary>
    public const string sql_int8range = "int8range";

    /// <summary>Represents the PostgreSQL nummultirange type.</summary>
    public const string sql_nummultirange = "nummultirange";

    /// <summary>Represents the PostgreSQL numrange type.</summary>
    public const string sql_numrange = "numrange";

    /// <summary>Represents the PostgreSQL tsmultirange type.</summary>
    public const string sql_tsmultirange = "tsmultirange";

    /// <summary>Represents the PostgreSQL tsrange type.</summary>
    public const string sql_tsrange = "tsrange";

    /// <summary>Represents the PostgreSQL tstzmultirange type.</summary>
    public const string sql_tstzmultirange = "tstzmultirange";

    /// <summary>Represents the PostgreSQL tstzrange type.</summary>
    public const string sql_tstzrange = "tstzrange";

    // other data types

    /// <summary>Represents the PostgreSQL cidr type.</summary>
    public const string sql_cidr = "cidr";

    /// <summary>Represents the PostgreSQL citext type.</summary>
    public const string sql_citext = "citext";

    /// <summary>Represents the PostgreSQL hstore type.</summary>
    public const string sql_hstore = "hstore";

    /// <summary>Represents the PostgreSQL inet type.</summary>
    public const string sql_inet = "inet";

    /// <summary>Represents the PostgreSQL int2vector type.</summary>
    public const string sql_int2vector = "int2vector";

    /// <summary>Represents the PostgreSQL lquery type.</summary>
    public const string sql_lquery = "lquery";

    /// <summary>Represents the PostgreSQL ltree type.</summary>
    public const string sql_ltree = "ltree";

    /// <summary>Represents the PostgreSQL ltxtquery type.</summary>
    public const string sql_ltxtquery = "ltxtquery";

    /// <summary>Represents the PostgreSQL macaddr type.</summary>
    public const string sql_macaddr = "macaddr";

    /// <summary>Represents the PostgreSQL macaddr8 type.</summary>
    public const string sql_macaddr8 = "macaddr8";

    /// <summary>Represents the PostgreSQL oid type.</summary>
    public const string sql_oid = "oid";

    /// <summary>Represents the PostgreSQL oidvector type.</summary>
    public const string sql_oidvector = "oidvector";

    /// <summary>Represents the PostgreSQL pg_lsn type.</summary>
    public const string sql_pg_lsn = "pg_lsn";

    /// <summary>Represents the PostgreSQL pg_snapshot type.</summary>
    public const string sql_pg_snapshot = "pg_snapshot";

    /// <summary>Represents the PostgreSQL refcursor type.</summary>
    public const string sql_refcursor = "refcursor";

    /// <summary>Represents the PostgreSQL regclass type.</summary>
    public const string sql_regclass = "regclass";

    /// <summary>Represents the PostgreSQL regcollation type.</summary>
    public const string sql_regcollation = "regcollation";

    /// <summary>Represents the PostgreSQL regconfig type.</summary>
    public const string sql_regconfig = "regconfig";

    /// <summary>Represents the PostgreSQL regdictionary type.</summary>
    public const string sql_regdictionary = "regdictionary";

    /// <summary>Represents the PostgreSQL regnamespace type.</summary>
    public const string sql_regnamespace = "regnamespace";

    /// <summary>Represents the PostgreSQL regoper type.</summary>
    public const string sql_regoper = "regoper";

    /// <summary>Represents the PostgreSQL regoperator type.</summary>
    public const string sql_regoperator = "regoperator";

    /// <summary>Represents the PostgreSQL regproc type.</summary>
    public const string sql_regproc = "regproc";

    /// <summary>Represents the PostgreSQL regprocedure type.</summary>
    public const string sql_regprocedure = "regprocedure";

    /// <summary>Represents the PostgreSQL regrole type.</summary>
    public const string sql_regrole = "regrole";

    /// <summary>Represents the PostgreSQL regtype type.</summary>
    public const string sql_regtype = "regtype";

    /// <summary>Represents the PostgreSQL tid type.</summary>
    public const string sql_tid = "tid";

    /// <summary>Represents the PostgreSQL tsquery type.</summary>
    public const string sql_tsquery = "tsquery";

    /// <summary>Represents the PostgreSQL tsvector type.</summary>
    public const string sql_tsvector = "tsvector";

    /// <summary>Represents the PostgreSQL txid_snapshot type.</summary>
    public const string sql_txid_snapshot = "txid_snapshot";

    /// <summary>Represents the PostgreSQL xid type.</summary>
    public const string sql_xid = "xid";

    /// <summary>Represents the PostgreSQL xid8 type.</summary>
    public const string sql_xid8 = "xid8";
}
