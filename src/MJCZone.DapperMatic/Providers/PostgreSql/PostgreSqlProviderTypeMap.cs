// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Net;
using System.Net.NetworkInformation;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using MJCZone.DapperMatic.Converters;
using MJCZone.DapperMatic.Providers.Base;

namespace MJCZone.DapperMatic.Providers.PostgreSql;

/// <summary>
/// Provides a mapping of .NET types to PostgreSQL types.
/// </summary>
/// <remarks>
/// See:
/// https://www.npgsql.org/doc/types/basic.html#read-mappings
/// https://www.npgsql.org/doc/types/basic.html#write-mappings.
/// </remarks>
public sealed class PostgreSqlProviderTypeMap : DbProviderTypeMapBase<PostgreSqlProviderTypeMap>
{
    /// <inheritdoc />
    protected override IProviderTypeMapping GetProviderTypeMapping() => new PostgreSqlTypeMapping();

    /// <inheritdoc />
    protected override string GetProviderName() => "postgresql";

    /// <inheritdoc />
    protected override void RegisterDotnetTypeToSqlTypeConverters()
    {
        RegisterStandardDotnetTypeToSqlTypeConverters();
    }

    /// <inheritdoc />
    protected override void RegisterProviderSpecificConverters()
    {
        var rangeConverter = GetRangeToSqlTypeConverter();

        // Range types (PostgreSQL is jacked up with range types)
        var rangeType = Type.GetType("NpgsqlTypes.NpgsqlRange`1, Npgsql");
        if (rangeType != null)
        {
            RegisterConverterForTypes(
                rangeConverter,
                new[]
                {
                    typeof(DateOnly),
                    typeof(int),
                    typeof(long),
                    typeof(double),
                    typeof(float),
                    typeof(decimal),
                    typeof(DateTime),
                    typeof(DateTimeOffset),
                }
                    .SelectMany(t =>
                    {
                        var dotnetType = rangeType.MakeGenericType(t);
                        return new[] { dotnetType, dotnetType.MakeArrayType() };
                    })
                    .ToArray()
            );
        }
    }

    /// <inheritdoc />
    protected override DotnetTypeToSqlTypeConverter GetEnumerableToSqlTypeConverter()
    {
        return new(d =>
        {
            if (
                d.DotnetType == typeof(Dictionary<string, string>)
                || d.DotnetType == typeof(IDictionary<string, string>)
                || d.DotnetType == typeof(ImmutableDictionary<string, string>)
            )
            {
                return TypeMappingHelpers.CreateSimpleType(PostgreSqlTypes.sql_hstore);
            }

            return TypeMappingHelpers.CreateJsonType(PostgreSqlTypes.sql_jsonb, isText: false);
        });
    }

    /// <inheritdoc />
    protected override SqlTypeDescriptor? CreateGeometryTypeForShortName(string shortName)
    {
        return shortName switch
        {
            // NetTopologySuite types - PostgreSQL has specific geometry types
            "NetTopologySuite.Geometries.Geometry, NetTopologySuite" => TypeMappingHelpers.CreateGeometryType(
                PostgreSqlTypes.sql_geometry
            ),
            "NetTopologySuite.Geometries.Point, NetTopologySuite" => TypeMappingHelpers.CreateGeometryType(
                PostgreSqlTypes.sql_point
            ),
            "NetTopologySuite.Geometries.LineString, NetTopologySuite" => TypeMappingHelpers.CreateGeometryType(
                PostgreSqlTypes.sql_geometry
            ),
            "NetTopologySuite.Geometries.Polygon, NetTopologySuite" => TypeMappingHelpers.CreateGeometryType(
                PostgreSqlTypes.sql_polygon
            ),
            "NetTopologySuite.Geometries.MultiPoint, NetTopologySuite"
            or "NetTopologySuite.Geometries.MultiLineString, NetTopologySuite"
            or "NetTopologySuite.Geometries.MultiPolygon, NetTopologySuite"
            or "NetTopologySuite.Geometries.GeometryCollection, NetTopologySuite" =>
                TypeMappingHelpers.CreateGeometryType(PostgreSqlTypes.sql_geometry),
            "NpgsqlTypes.NpgsqlInet, Npgsql" => TypeMappingHelpers.CreateSimpleType(PostgreSqlTypes.sql_inet),
            "NpgsqlTypes.NpgsqlCidr, Npgsql" => TypeMappingHelpers.CreateSimpleType(PostgreSqlTypes.sql_cidr),
            "NpgsqlTypes.NpgsqlPoint, Npgsql" => TypeMappingHelpers.CreateGeometryType(PostgreSqlTypes.sql_point),
            "NpgsqlTypes.NpgsqlLSeg, Npgsql" => TypeMappingHelpers.CreateSimpleType(PostgreSqlTypes.sql_lseg),
            "NpgsqlTypes.NpgsqlPath, Npgsql" => TypeMappingHelpers.CreateSimpleType(PostgreSqlTypes.sql_path),
            "NpgsqlTypes.NpgsqlPolygon, Npgsql" => TypeMappingHelpers.CreateGeometryType(PostgreSqlTypes.sql_polygon),
            "NpgsqlTypes.NpgsqlLine, Npgsql" => TypeMappingHelpers.CreateSimpleType(PostgreSqlTypes.sql_line),
            "NpgsqlTypes.NpgsqlCircle, Npgsql" => TypeMappingHelpers.CreateSimpleType(PostgreSqlTypes.sql_circle),
            "NpgsqlTypes.NpgsqlBox, Npgsql" => TypeMappingHelpers.CreateSimpleType(PostgreSqlTypes.sql_box),
            "NpgsqlTypes.NpgsqlInterval, Npgsql" => TypeMappingHelpers.CreateSimpleType(PostgreSqlTypes.sql_interval),
            "NpgsqlTypes.NpgsqlTid, Npgsql" => TypeMappingHelpers.CreateSimpleType(PostgreSqlTypes.sql_tid),
            "NpgsqlTypes.NpgsqlTsQuery, Npgsql" => TypeMappingHelpers.CreateSimpleType(PostgreSqlTypes.sql_tsquery),
            "NpgsqlTypes.NpgsqlTsVector, Npgsql" => TypeMappingHelpers.CreateSimpleType(PostgreSqlTypes.sql_tsvector),
            _ => null,
        };
    }

    /// <inheritdoc />
    protected override void RegisterSqlTypeToDotnetTypeConverters()
    {
        var booleanConverter = GetBooleanToDotnetTypeConverter();
        var numericConverter = GetNumbericToDotnetTypeConverter();
        var guidConverter = GetGuidToDotnetTypeConverter();
        var textConverter = GetTextToDotnetTypeConverter();
        var xmlConverter = GetXmlToDotnetTypeConverter();
        var jsonConverter = GetJsonToDotnetTypeConverter();
        var dateTimeConverter = GetDateTimeToDotnetTypeConverter();
        var byteArrayConverter = GetByteArrayToDotnetTypeConverter();
        var geometricConverter = GetGeometricToDotnetTypeConverter();
        var rangeConverter = GetRangeToDotnetTypeConverter();
        var miscConverter = GetMiscellaneousToDotnetTypeConverter();
        var arrayConverter = TypeMappingHelpers.CreatePostgreSqlArrayTypeConverter();

        // Boolean affinity
        RegisterConverterForTypes(booleanConverter, PostgreSqlTypes.sql_bool, PostgreSqlTypes.sql_boolean);

        // Numeric affinity
        RegisterConverterForTypes(
            numericConverter,
            PostgreSqlTypes.sql_smallint,
            PostgreSqlTypes.sql_int2,
            PostgreSqlTypes.sql_smallserial,
            PostgreSqlTypes.sql_serial2,
            PostgreSqlTypes.sql_integer,
            PostgreSqlTypes.sql_int,
            PostgreSqlTypes.sql_int4,
            PostgreSqlTypes.sql_serial,
            PostgreSqlTypes.sql_serial4,
            PostgreSqlTypes.sql_bigint,
            PostgreSqlTypes.sql_int8,
            PostgreSqlTypes.sql_bigserial,
            PostgreSqlTypes.sql_serial8,
            PostgreSqlTypes.sql_float4,
            PostgreSqlTypes.sql_real,
            PostgreSqlTypes.sql_double_precision,
            PostgreSqlTypes.sql_float8,
            PostgreSqlTypes.sql_money,
            PostgreSqlTypes.sql_numeric,
            PostgreSqlTypes.sql_decimal
        );

        // DateTime affinity
        RegisterConverterForTypes(
            dateTimeConverter,
            PostgreSqlTypes.sql_date,
            PostgreSqlTypes.sql_interval,
            PostgreSqlTypes.sql_time_without_time_zone,
            PostgreSqlTypes.sql_time,
            PostgreSqlTypes.sql_time_with_time_zone,
            PostgreSqlTypes.sql_timetz,
            PostgreSqlTypes.sql_timestamp_without_time_zone,
            PostgreSqlTypes.sql_timestamp,
            PostgreSqlTypes.sql_timestamp_with_time_zone,
            PostgreSqlTypes.sql_timestamptz
        );

        // Guid affinity
        RegisterConverter(PostgreSqlTypes.sql_uuid, guidConverter);

        // Text affinity
        RegisterConverterForTypes(
            textConverter,
            PostgreSqlTypes.sql_bit,
            PostgreSqlTypes.sql_bit_varying,
            PostgreSqlTypes.sql_varbit,
            PostgreSqlTypes.sql_character_varying,
            PostgreSqlTypes.sql_varchar,
            PostgreSqlTypes.sql_character,
            PostgreSqlTypes.sql_char,
            PostgreSqlTypes.sql_bpchar,
            PostgreSqlTypes.sql_quoted_char,
            PostgreSqlTypes.sql_text,
            PostgreSqlTypes.sql_name
        );

        // Xml affinity
        RegisterConverter(PostgreSqlTypes.sql_xml, xmlConverter);

        // Json affinity (only for very latest versions of SQL Server)
        RegisterConverterForTypes(
            jsonConverter,
            PostgreSqlTypes.sql_json,
            PostgreSqlTypes.sql_jsonb,
            PostgreSqlTypes.sql_jsonpath
        );

        // Binary affinity
        RegisterConverter(PostgreSqlTypes.sql_bytea, byteArrayConverter);

        // Geometry affinity
        RegisterConverterForTypes(
            geometricConverter,
            // the NetopologySuite types are often mapped to the geometry type
            PostgreSqlTypes.sql_box,
            PostgreSqlTypes.sql_circle,
            PostgreSqlTypes.sql_geography,
            PostgreSqlTypes.sql_geometry,
            PostgreSqlTypes.sql_line,
            PostgreSqlTypes.sql_lseg,
            PostgreSqlTypes.sql_path,
            PostgreSqlTypes.sql_point,
            PostgreSqlTypes.sql_polygon
        );

        // Miscellaneous affinity
        RegisterConverterForTypes(
            miscConverter,
            PostgreSqlTypes.sql_cidr,
            PostgreSqlTypes.sql_citext,
            PostgreSqlTypes.sql_hstore,
            PostgreSqlTypes.sql_inet,
            PostgreSqlTypes.sql_int2vector,
            PostgreSqlTypes.sql_lquery,
            PostgreSqlTypes.sql_ltree,
            PostgreSqlTypes.sql_ltxtquery,
            PostgreSqlTypes.sql_macaddr,
            PostgreSqlTypes.sql_macaddr8,
            PostgreSqlTypes.sql_oid,
            PostgreSqlTypes.sql_oidvector,
            PostgreSqlTypes.sql_pg_lsn,
            PostgreSqlTypes.sql_pg_snapshot,
            PostgreSqlTypes.sql_refcursor,
            PostgreSqlTypes.sql_regclass,
            PostgreSqlTypes.sql_regcollation,
            PostgreSqlTypes.sql_regconfig,
            PostgreSqlTypes.sql_regdictionary,
            PostgreSqlTypes.sql_regnamespace,
            PostgreSqlTypes.sql_regoper,
            PostgreSqlTypes.sql_regoperator,
            PostgreSqlTypes.sql_regproc,
            PostgreSqlTypes.sql_regprocedure,
            PostgreSqlTypes.sql_regrole,
            PostgreSqlTypes.sql_regtype,
            PostgreSqlTypes.sql_tid,
            PostgreSqlTypes.sql_tsquery,
            PostgreSqlTypes.sql_tsvector,
            PostgreSqlTypes.sql_txid_snapshot,
            PostgreSqlTypes.sql_xid,
            PostgreSqlTypes.sql_xid8
        );

        // Range types (PostgreSQL is jacked up with range types)
        RegisterConverterForTypes(
            rangeConverter,
            PostgreSqlTypes.sql_datemultirange,
            PostgreSqlTypes.sql_daterange,
            PostgreSqlTypes.sql_int4multirange,
            PostgreSqlTypes.sql_int4range,
            PostgreSqlTypes.sql_int8multirange,
            PostgreSqlTypes.sql_int8range,
            PostgreSqlTypes.sql_nummultirange,
            PostgreSqlTypes.sql_numrange,
            PostgreSqlTypes.sql_tsrange,
            PostgreSqlTypes.sql_tsmultirange,
            PostgreSqlTypes.sql_tstzrange,
            PostgreSqlTypes.sql_tstzmultirange
        );

        // Native Array types (PostgreSQL specific)
        RegisterConverterForTypes(arrayConverter, TypeMappingHelpers.GetPostgreSqlStandardArrayTypes());
    }

    #region DotnetTypeToSqlTypeConverters

    private DotnetTypeToSqlTypeConverter GetRangeToSqlTypeConverter()
    {
        return new(d =>
        {
            var isArray = d.DotnetType != null && d.DotnetType.IsArray;

            var dotnetType = isArray ? d.DotnetType!.GetElementType() : d.DotnetType;
            if (dotnetType == null)
            {
                return null;
            }

            var shortName = TypeMappingHelpers.GetAssemblyQualifiedShortName(d.DotnetType);
            if (string.IsNullOrWhiteSpace(shortName))
            {
                return null;
            }

            switch (shortName)
            {
                case "NpgsqlTypes.NpgsqlRange`1, Npgsql":
                    var genericType = dotnetType.GetGenericArguments()[0];
                    if (genericType == typeof(DateOnly))
                    {
                        return isArray ? new(PostgreSqlTypes.sql_datemultirange) : new(PostgreSqlTypes.sql_daterange);
                    }
                    if (genericType == typeof(int))
                    {
                        return isArray ? new(PostgreSqlTypes.sql_int4multirange) : new(PostgreSqlTypes.sql_int4range);
                    }
                    if (genericType == typeof(long))
                    {
                        return isArray ? new(PostgreSqlTypes.sql_int8multirange) : new(PostgreSqlTypes.sql_int8range);
                    }
                    if (genericType == typeof(double))
                    {
                        return isArray ? new(PostgreSqlTypes.sql_nummultirange) : new(PostgreSqlTypes.sql_numrange);
                    }
                    if (genericType == typeof(float))
                    {
                        return isArray ? new(PostgreSqlTypes.sql_nummultirange) : new(PostgreSqlTypes.sql_numrange);
                    }
                    if (genericType == typeof(decimal))
                    {
                        return isArray ? new(PostgreSqlTypes.sql_nummultirange) : new(PostgreSqlTypes.sql_numrange);
                    }
                    if (genericType == typeof(DateTime))
                    {
                        return isArray ? new(PostgreSqlTypes.sql_tsmultirange) : new(PostgreSqlTypes.sql_tsrange);
                    }
                    if (genericType == typeof(DateTimeOffset))
                    {
                        return isArray ? new(PostgreSqlTypes.sql_tstzmultirange) : new(PostgreSqlTypes.sql_tstzrange);
                    }
                    break;
            }

            return null;
        });
    }

    #endregion // DotnetTypeToSqlTypeConverters

    #region SqlTypeToDotnetTypeConverters

#pragma warning disable SA1204 // Static elements should appear before instance elements
    private static SqlTypeToDotnetTypeConverter GetBooleanToDotnetTypeConverter()
#pragma warning restore SA1204 // Static elements should appear before instance elements
    {
        return new(d =>
        {
            return new DotnetTypeDescriptor(typeof(bool));
        });
    }

    private static SqlTypeToDotnetTypeConverter GetNumbericToDotnetTypeConverter()
    {
        return new(d =>
        {
            switch (d.BaseTypeName)
            {
                case PostgreSqlTypes.sql_smallint:
                case PostgreSqlTypes.sql_int2:
                    return new DotnetTypeDescriptor(typeof(short));
                case PostgreSqlTypes.sql_smallserial:
                case PostgreSqlTypes.sql_serial2:
                    return new DotnetTypeDescriptor(typeof(short), isAutoIncrementing: true);
                case PostgreSqlTypes.sql_int:
                case PostgreSqlTypes.sql_integer:
                case PostgreSqlTypes.sql_int4:
                    return new DotnetTypeDescriptor(typeof(int));
                case PostgreSqlTypes.sql_serial:
                case PostgreSqlTypes.sql_serial4:
                    return new DotnetTypeDescriptor(typeof(int), isAutoIncrementing: true);
                case PostgreSqlTypes.sql_bigint:
                case PostgreSqlTypes.sql_int8:
                    return new DotnetTypeDescriptor(typeof(long));
                case PostgreSqlTypes.sql_bigserial:
                case PostgreSqlTypes.sql_serial8:
                    return new DotnetTypeDescriptor(typeof(long), isAutoIncrementing: true);
                case PostgreSqlTypes.sql_real:
                case PostgreSqlTypes.sql_float4:
                    return new DotnetTypeDescriptor(typeof(float));
                case PostgreSqlTypes.sql_double_precision:
                case PostgreSqlTypes.sql_float8:
                    return new DotnetTypeDescriptor(typeof(double));
                case PostgreSqlTypes.sql_decimal:
                case PostgreSqlTypes.sql_money:
                case PostgreSqlTypes.sql_numeric:
                    return new DotnetTypeDescriptor(typeof(decimal))
                    {
                        Precision = d.Precision ?? 16,
                        Scale = d.Scale ?? 4,
                    };
                default:
                    return new DotnetTypeDescriptor(typeof(int));
            }
        });
    }

    private static SqlTypeToDotnetTypeConverter GetDateTimeToDotnetTypeConverter()
    {
        return new(d =>
        {
            switch (d.BaseTypeName)
            {
                case PostgreSqlTypes.sql_timestamp_without_time_zone:
                case PostgreSqlTypes.sql_timestamp:
                    return new DotnetTypeDescriptor(typeof(DateTime));
                case PostgreSqlTypes.sql_timestamp_with_time_zone:
                case PostgreSqlTypes.sql_timestamptz:
                    return new DotnetTypeDescriptor(typeof(DateTimeOffset));
                case PostgreSqlTypes.sql_interval:
                    return new DotnetTypeDescriptor(typeof(TimeSpan));
                case PostgreSqlTypes.sql_time:
                case PostgreSqlTypes.sql_time_without_time_zone:
                case PostgreSqlTypes.sql_timetz:
                case PostgreSqlTypes.sql_time_with_time_zone:
                    return new DotnetTypeDescriptor(typeof(TimeOnly));
                case PostgreSqlTypes.sql_date:
                    return new DotnetTypeDescriptor(typeof(DateOnly));
                default:
                    return new DotnetTypeDescriptor(typeof(DateTime));
            }
        });
    }

    private static SqlTypeToDotnetTypeConverter GetGuidToDotnetTypeConverter()
    {
        return new(d =>
        {
            return new DotnetTypeDescriptor(typeof(Guid));
        });
    }

    private static SqlTypeToDotnetTypeConverter GetTextToDotnetTypeConverter()
    {
        return new(d =>
        {
            switch (d.BaseTypeName)
            {
                case PostgreSqlTypes.sql_char:
                case PostgreSqlTypes.sql_character:
                case PostgreSqlTypes.sql_quoted_char:
                    return new DotnetTypeDescriptor(
                        typeof(string),
                        d.Length,
                        isUnicode: d.IsUnicode.GetValueOrDefault(true),
                        isFixedLength: true
                    );
                case PostgreSqlTypes.sql_bit:
                case PostgreSqlTypes.sql_bit_varying:
                case PostgreSqlTypes.sql_varbit:
                case PostgreSqlTypes.sql_varchar:
                case PostgreSqlTypes.sql_character_varying:
                case PostgreSqlTypes.sql_text:
                case PostgreSqlTypes.sql_bpchar:
                    return new DotnetTypeDescriptor(
                        typeof(string),
                        d.Length,
                        isUnicode: d.IsUnicode.GetValueOrDefault(true),
                        isFixedLength: false
                    );
            }

            return null;
        });
    }

    private static SqlTypeToDotnetTypeConverter GetXmlToDotnetTypeConverter()
    {
        return new(d => new DotnetTypeDescriptor(typeof(XDocument)));
    }

    private static SqlTypeToDotnetTypeConverter GetJsonToDotnetTypeConverter()
    {
        return new(d => new DotnetTypeDescriptor(typeof(JsonDocument)));
    }

    private static SqlTypeToDotnetTypeConverter GetByteArrayToDotnetTypeConverter()
    {
        return new(d =>
        {
            return new DotnetTypeDescriptor(
                typeof(byte[]),
                d.Length,
                isFixedLength: d.IsFixedLength.GetValueOrDefault(false)
            );
        });
    }

    private static SqlTypeToDotnetTypeConverter GetGeometricToDotnetTypeConverter()
    {
        // NetTopologySuite types
        var sqlNetTopologyGeometryType = Type.GetType("NetTopologySuite.Geometries.Geometry, NetTopologySuite");
        var sqlNetTopologyPointType = Type.GetType("NetTopologySuite.Geometries.Point, NetTopologySuite");
        var sqlNetTopologyLineStringType = Type.GetType("NetTopologySuite.Geometries.LineString, NetTopologySuite");
        var sqlNetTopologyPolygonType = Type.GetType("NetTopologySuite.Geometries.Polygon, NetTopologySuite");
        var sqlNetTopologyMultiPointType = Type.GetType("NetTopologySuite.Geometries.MultiPoint, NetTopologySuite");
        var sqlNetTopologyMultLineStringType = Type.GetType(
            "NetTopologySuite.Geometries.MultiLineString, NetTopologySuite"
        );
        var sqlNetTopologyMultiPolygonType = Type.GetType("NetTopologySuite.Geometries.MultiPolygon, NetTopologySuite");
        var sqlNetTopologyGeometryCollectionType = Type.GetType(
            "NetTopologySuite.Geometries.GeometryCollection, NetTopologySuite"
        );

        // Geometry affinity
        var sqlNpgsqlPoint = Type.GetType("NpgsqlTypes.NpgsqlPoint, Npgsql");
        var sqlNpgsqlLSeg = Type.GetType("NpgsqlTypes.NpgsqlLSeg, Npgsql");
        var sqlNpgsqlPath = Type.GetType("NpgsqlTypes.NpgsqlPath, Npgsql");
        var sqlNpgsqlPolygon = Type.GetType("NpgsqlTypes.NpgsqlPolygon, Npgsql");
        var sqlNpgsqlLine = Type.GetType("NpgsqlTypes.NpgsqlLine, Npgsql");
        var sqlNpgsqlCircle = Type.GetType("NpgsqlTypes.NpgsqlCircle, Npgsql");
        var sqlNpgsqlBox = Type.GetType("NpgsqlTypes.NpgsqlBox, Npgsql");
        var sqlGeometry = Type.GetType("NetTopologySuite.Geometries.Geometry, NetTopologySuite", false, false);

        return new(d =>
        {
            switch (d.BaseTypeName)
            {
                case PostgreSqlTypes.sql_box:
                    if (sqlNpgsqlBox != null)
                    {
                        return new DotnetTypeDescriptor(sqlNpgsqlBox);
                    }

                    if (sqlNetTopologyGeometryType != null)
                    {
                        return new DotnetTypeDescriptor(sqlNetTopologyGeometryType);
                    }

                    // Fallback: WKT format as string
                    return new DotnetTypeDescriptor(typeof(string));
                case PostgreSqlTypes.sql_circle:
                    if (sqlNpgsqlCircle != null)
                    {
                        return new DotnetTypeDescriptor(sqlNpgsqlCircle);
                    }

                    if (sqlNetTopologyGeometryType != null)
                    {
                        return new DotnetTypeDescriptor(sqlNetTopologyGeometryType);
                    }

                    // Fallback: WKT format as string
                    return new DotnetTypeDescriptor(typeof(string));
                case PostgreSqlTypes.sql_geography:
                    if (sqlNetTopologyGeometryType != null)
                    {
                        return new DotnetTypeDescriptor(sqlNetTopologyGeometryType);
                    }

                    if (sqlGeometry != null)
                    {
                        return new DotnetTypeDescriptor(sqlGeometry);
                    }

                    // Fallback: WKT (Well-Known Text) format as string
                    return new DotnetTypeDescriptor(typeof(string));
                case PostgreSqlTypes.sql_geometry:
                    if (sqlNetTopologyGeometryType != null)
                    {
                        return new DotnetTypeDescriptor(sqlNetTopologyGeometryType);
                    }

                    if (sqlGeometry != null)
                    {
                        return new DotnetTypeDescriptor(sqlGeometry);
                    }

                    // Fallback: WKT (Well-Known Text) format as string
                    return new DotnetTypeDescriptor(typeof(string));
                case PostgreSqlTypes.sql_line:
                    if (sqlNpgsqlLine != null)
                    {
                        return new DotnetTypeDescriptor(sqlNpgsqlLine);
                    }

                    if (sqlNetTopologyLineStringType != null)
                    {
                        return new DotnetTypeDescriptor(sqlNetTopologyLineStringType);
                    }

                    // Fallback: WKT format as string
                    return new DotnetTypeDescriptor(typeof(string));
                case PostgreSqlTypes.sql_lseg:
                    if (sqlNpgsqlLSeg != null)
                    {
                        return new DotnetTypeDescriptor(sqlNpgsqlLSeg);
                    }

                    // Fallback: WKT format as string
                    return new DotnetTypeDescriptor(typeof(string));
                case PostgreSqlTypes.sql_path:
                    if (sqlNpgsqlPath != null)
                    {
                        return new DotnetTypeDescriptor(sqlNpgsqlPath);
                    }

                    // Fallback: WKT format as string
                    return new DotnetTypeDescriptor(typeof(string));
                case PostgreSqlTypes.sql_point:
                    if (sqlNpgsqlPoint != null)
                    {
                        return new DotnetTypeDescriptor(sqlNpgsqlPoint);
                    }

                    if (sqlNetTopologyPointType != null)
                    {
                        return new DotnetTypeDescriptor(sqlNetTopologyPointType);
                    }

                    // Fallback: WKT format as string
                    return new DotnetTypeDescriptor(typeof(string));
                case PostgreSqlTypes.sql_polygon:
                    if (sqlNpgsqlPolygon != null)
                    {
                        return new DotnetTypeDescriptor(sqlNpgsqlPolygon);
                    }

                    if (sqlNetTopologyPolygonType != null)
                    {
                        return new DotnetTypeDescriptor(sqlNetTopologyPolygonType);
                    }

                    // Fallback: WKT format as string
                    return new DotnetTypeDescriptor(typeof(string));
            }

            return null;
        });
    }

    private static SqlTypeToDotnetTypeConverter GetMiscellaneousToDotnetTypeConverter()
    {
        var sqlNpgsqlInet = Type.GetType("NpgsqlTypes.NpgsqlInet, Npgsql");
        var sqlNpgsqlCidr = Type.GetType("NpgsqlTypes.NpgsqlCidr, Npgsql");
        var sqlPhysicalAddress = Type.GetType(
            "System.Net.NetworkInformation.PhysicalAddress, System.Net.NetworkInformation",
            false,
            false
        );
        var sqlNpgsqlInterval = Type.GetType("NpgsqlTypes.NpgsqlInterval, Npgsql");
        var sqlNpgsqlTid = Type.GetType("NpgsqlTypes.NpgsqlTid, Npgsql");
        var sqlNpgsqlTsQuery = Type.GetType("NpgsqlTypes.NpgsqlTsQuery, Npgsql");
        var sqlNpgsqlTsVector = Type.GetType("NpgsqlTypes.NpgsqlTsVector, Npgsql");

        return new(d =>
        {
            switch (d.BaseTypeName)
            {
                case PostgreSqlTypes.sql_cidr:
                    if (sqlNpgsqlCidr != null)
                    {
                        return new DotnetTypeDescriptor(sqlNpgsqlCidr);
                    }

                    // Fallback: CIDR notation as string (e.g., "192.168.0.0/24")
                    return new DotnetTypeDescriptor(typeof(string));
                case PostgreSqlTypes.sql_citext:
                    return new DotnetTypeDescriptor(typeof(string));
                case PostgreSqlTypes.sql_hstore:
                    return new DotnetTypeDescriptor(typeof(Dictionary<string, string>));
                case PostgreSqlTypes.sql_inet:
                    return new DotnetTypeDescriptor(typeof(IPAddress));
                case PostgreSqlTypes.sql_lquery:
                case PostgreSqlTypes.sql_ltree:
                case PostgreSqlTypes.sql_ltxtquery:
                    // ltree types store hierarchical data as text (e.g., "Top.Science.Astronomy")
                    return new DotnetTypeDescriptor(typeof(string));
                case PostgreSqlTypes.sql_macaddr:
                case PostgreSqlTypes.sql_macaddr8:
                    return new DotnetTypeDescriptor(typeof(PhysicalAddress));
                case PostgreSqlTypes.sql_interval:
                    if (sqlNpgsqlInterval != null)
                    {
                        return new DotnetTypeDescriptor(sqlNpgsqlInterval);
                    }

                    // Fallback: PostgreSQL interval format as string (e.g., "1 day 02:03:04")
                    return new DotnetTypeDescriptor(typeof(string));
                case PostgreSqlTypes.sql_int2vector:
                    return new DotnetTypeDescriptor(typeof(int[]));
                case PostgreSqlTypes.sql_oid:
                    return new DotnetTypeDescriptor(typeof(uint));
                case PostgreSqlTypes.sql_oidvector:
                    return new DotnetTypeDescriptor(typeof(uint[]));
                case PostgreSqlTypes.sql_pg_lsn:
                case PostgreSqlTypes.sql_pg_snapshot:
                case PostgreSqlTypes.sql_refcursor:
                    return new DotnetTypeDescriptor(typeof(object));
                case PostgreSqlTypes.sql_regclass:
                case PostgreSqlTypes.sql_regcollation:
                case PostgreSqlTypes.sql_regconfig:
                case PostgreSqlTypes.sql_regdictionary:
                case PostgreSqlTypes.sql_regnamespace:
                case PostgreSqlTypes.sql_regoper:
                case PostgreSqlTypes.sql_regoperator:
                case PostgreSqlTypes.sql_regproc:
                case PostgreSqlTypes.sql_regprocedure:
                case PostgreSqlTypes.sql_regrole:
                case PostgreSqlTypes.sql_regtype:
                    // OID (Object Identifier) types - internally they're unsigned integers
                    return new DotnetTypeDescriptor(typeof(uint));
                case PostgreSqlTypes.sql_tid:
                    if (sqlNpgsqlTid != null)
                    {
                        return new DotnetTypeDescriptor(sqlNpgsqlTid);
                    }

                    // Fallback: Tuple identifier as string (e.g., "(0,1)")
                    return new DotnetTypeDescriptor(typeof(string));
                case PostgreSqlTypes.sql_tsquery:
                    if (sqlNpgsqlTsQuery != null)
                    {
                        return new DotnetTypeDescriptor(sqlNpgsqlTsQuery);
                    }

                    // Fallback: Text search query as string
                    return new DotnetTypeDescriptor(typeof(string));
                case PostgreSqlTypes.sql_tsvector:
                    if (sqlNpgsqlTsVector != null)
                    {
                        return new DotnetTypeDescriptor(sqlNpgsqlTsVector);
                    }

                    // Fallback: Text search vector as string
                    return new DotnetTypeDescriptor(typeof(string));
                case PostgreSqlTypes.sql_txid_snapshot:
                case PostgreSqlTypes.sql_xid:
                case PostgreSqlTypes.sql_xid8:
                    return new DotnetTypeDescriptor(typeof(object));
            }

            return null;
        });
    }

    private static SqlTypeToDotnetTypeConverter GetRangeToDotnetTypeConverter()
    {
        var rangeType = Type.GetType("NpgsqlTypes.NpgsqlRange`1, Npgsql");
        if (rangeType == null)
        {
            return new(d => new DotnetTypeDescriptor(typeof(object)));
        }

        return new(d =>
        {
            switch (d.BaseTypeName)
            {
                case PostgreSqlTypes.sql_datemultirange:
                    return new DotnetTypeDescriptor(rangeType.MakeGenericType(typeof(DateOnly)).MakeArrayType());
                case PostgreSqlTypes.sql_daterange:
                    return new DotnetTypeDescriptor(rangeType.MakeGenericType(typeof(DateOnly)));
                case PostgreSqlTypes.sql_int4multirange:
                    return new DotnetTypeDescriptor(rangeType.MakeGenericType(typeof(int)).MakeArrayType());
                case PostgreSqlTypes.sql_int4range:
                    return new DotnetTypeDescriptor(rangeType.MakeGenericType(typeof(int)));
                case PostgreSqlTypes.sql_int8multirange:
                    return new DotnetTypeDescriptor(rangeType.MakeGenericType(typeof(long)).MakeArrayType());
                case PostgreSqlTypes.sql_int8range:
                    return new DotnetTypeDescriptor(rangeType.MakeGenericType(typeof(long)));
                case PostgreSqlTypes.sql_nummultirange:
                    return new DotnetTypeDescriptor(rangeType.MakeGenericType(typeof(double)).MakeArrayType());
                case PostgreSqlTypes.sql_numrange:
                    return new DotnetTypeDescriptor(rangeType.MakeGenericType(typeof(double)));
                case PostgreSqlTypes.sql_tsrange:
                    return new DotnetTypeDescriptor(rangeType.MakeGenericType(typeof(DateTime)));
                case PostgreSqlTypes.sql_tsmultirange:
                    return new DotnetTypeDescriptor(rangeType.MakeGenericType(typeof(DateTime)).MakeArrayType());
                case PostgreSqlTypes.sql_tstzrange:
                    return new DotnetTypeDescriptor(rangeType.MakeGenericType(typeof(DateTimeOffset)));
                case PostgreSqlTypes.sql_tstzmultirange:
                    return new DotnetTypeDescriptor(rangeType.MakeGenericType(typeof(DateTimeOffset)).MakeArrayType());
            }

            return null;
        });
    }

    #endregion // SqlTypeToDotnetTypeConverters
}
