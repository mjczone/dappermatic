// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using MJCZone.DapperMatic.Converters;
using MJCZone.DapperMatic.Providers.Base;

namespace MJCZone.DapperMatic.Providers.MySql
{
    /// <summary>
    /// Provides type mapping for MySQL database provider.
    /// </summary>
    /// <remarks>
    /// See:
    /// https://dev.mysql.com/doc/connector-net/en/
    /// https://stackoverflow.com/questions/67101765/c-sharp-mysql-dapper-mysqlgeometry
    /// ...
    /// </remarks>
    public sealed class MySqlProviderTypeMap : DbProviderTypeMapBase<MySqlProviderTypeMap>
    {
        /// <inheritdoc/>
        protected override IProviderTypeMapping GetProviderTypeMapping()
        {
            return new MySqlTypeMapping();
        }

        /// <inheritdoc/>
        protected override string GetProviderName()
        {
            return "mysql";
        }

        /// <inheritdoc/>
        protected override void RegisterNetTopologySuiteTypes()
        {
            // NetTopologySuite types map to specific MySQL geometry types
            var geometryType = Type.GetType("NetTopologySuite.Geometries.Geometry, NetTopologySuite");
            var pointType = Type.GetType("NetTopologySuite.Geometries.Point, NetTopologySuite");
            var lineStringType = Type.GetType("NetTopologySuite.Geometries.LineString, NetTopologySuite");
            var polygonType = Type.GetType("NetTopologySuite.Geometries.Polygon, NetTopologySuite");
            var multiPointType = Type.GetType("NetTopologySuite.Geometries.MultiPoint, NetTopologySuite");
            var multiLineStringType = Type.GetType("NetTopologySuite.Geometries.MultiLineString, NetTopologySuite");
            var multiPolygonType = Type.GetType("NetTopologySuite.Geometries.MultiPolygon, NetTopologySuite");
            var geometryCollectionType = Type.GetType(
                "NetTopologySuite.Geometries.GeometryCollection, NetTopologySuite"
            );

            if (geometryType != null)
            {
                RegisterConverter(
                    geometryType,
                    new DotnetTypeToSqlTypeConverter(d =>
                        TypeMappingHelpers.CreateGeometryType(MySqlTypes.sql_geometry)
                    )
                );
            }

            if (pointType != null)
            {
                RegisterConverter(
                    pointType,
                    new DotnetTypeToSqlTypeConverter(d => TypeMappingHelpers.CreateGeometryType(MySqlTypes.sql_point))
                );
            }

            if (lineStringType != null)
            {
                RegisterConverter(
                    lineStringType,
                    new DotnetTypeToSqlTypeConverter(d =>
                        TypeMappingHelpers.CreateGeometryType(MySqlTypes.sql_linestring)
                    )
                );
            }

            if (polygonType != null)
            {
                RegisterConverter(
                    polygonType,
                    new DotnetTypeToSqlTypeConverter(d => TypeMappingHelpers.CreateGeometryType(MySqlTypes.sql_polygon))
                );
            }

            if (multiPointType != null)
            {
                RegisterConverter(
                    multiPointType,
                    new DotnetTypeToSqlTypeConverter(d =>
                        TypeMappingHelpers.CreateGeometryType(MySqlTypes.sql_multipoint)
                    )
                );
            }

            if (multiLineStringType != null)
            {
                RegisterConverter(
                    multiLineStringType,
                    new DotnetTypeToSqlTypeConverter(d =>
                        TypeMappingHelpers.CreateGeometryType(MySqlTypes.sql_multilinestring)
                    )
                );
            }

            if (multiPolygonType != null)
            {
                RegisterConverter(
                    multiPolygonType,
                    new DotnetTypeToSqlTypeConverter(d =>
                        TypeMappingHelpers.CreateGeometryType(MySqlTypes.sql_multipolygon)
                    )
                );
            }

            if (geometryCollectionType != null)
            {
                RegisterConverter(
                    geometryCollectionType,
                    new DotnetTypeToSqlTypeConverter(d =>
                        TypeMappingHelpers.CreateGeometryType(MySqlTypes.sql_geometrycollection)
                    )
                );
            }
        }

        /// <inheritdoc/>
        protected override void RegisterSqlServerTypes()
        {
            // SQL Server geometry types map to MySQL GEOMETRY
            var sqlGeometryType = Type.GetType("Microsoft.SqlServer.Types.SqlGeometry, Microsoft.SqlServer.Types");
            var sqlGeographyType = Type.GetType("Microsoft.SqlServer.Types.SqlGeography, Microsoft.SqlServer.Types");
            var sqlHierarchyIdType = Type.GetType(
                "Microsoft.SqlServer.Types.SqlHierarchyId, Microsoft.SqlServer.Types"
            );

            if (sqlGeometryType != null)
            {
                RegisterConverter(
                    sqlGeometryType,
                    new DotnetTypeToSqlTypeConverter(d =>
                        TypeMappingHelpers.CreateGeometryType(MySqlTypes.sql_geometry)
                    )
                );
            }

            if (sqlGeographyType != null)
            {
                RegisterConverter(
                    sqlGeographyType,
                    new DotnetTypeToSqlTypeConverter(d =>
                        TypeMappingHelpers.CreateGeometryType(MySqlTypes.sql_geometry)
                    )
                );
            }

            if (sqlHierarchyIdType != null)
            {
                var mySqlHierarchyIdType = GetProviderTypeMapping()
                    .CreateTextType(new DotnetTypeDescriptor(typeof(string), length: -1, isUnicode: false));
                RegisterConverter(sqlHierarchyIdType, new DotnetTypeToSqlTypeConverter(d => mySqlHierarchyIdType));
            }
        }

        /// <inheritdoc/>
        protected override void RegisterMySqlTypes()
        {
            // MySQL native geometry types
            var mySqlGeometryConverter = new DotnetTypeToSqlTypeConverter(d =>
                TypeMappingHelpers.CreateGeometryType(MySqlTypes.sql_geometry)
            );

            RegisterConverterForTypes(mySqlGeometryConverter, TypeMappingHelpers.GetMySqlGeometryTypes());
        }

        /// <inheritdoc/>
        protected override void RegisterNpgsqlTypes()
        {
            // PostgreSQL geometric types map to specific MySQL geometry types
            var npgsqlPointType = Type.GetType("NpgsqlTypes.NpgsqlPoint, Npgsql");
            var npgsqlLSegType = Type.GetType("NpgsqlTypes.NpgsqlLSeg, Npgsql");
            var npgsqlPathType = Type.GetType("NpgsqlTypes.NpgsqlPath, Npgsql");
            var npgsqlPolygonType = Type.GetType("NpgsqlTypes.NpgsqlPolygon, Npgsql");
            var npgsqlLineType = Type.GetType("NpgsqlTypes.NpgsqlLine, Npgsql");
            var npgsqlCircleType = Type.GetType("NpgsqlTypes.NpgsqlCircle, Npgsql");
            var npgsqlBoxType = Type.GetType("NpgsqlTypes.NpgsqlBox, Npgsql");

            if (npgsqlPointType != null)
            {
                RegisterConverter(
                    npgsqlPointType,
                    new DotnetTypeToSqlTypeConverter(d => TypeMappingHelpers.CreateGeometryType(MySqlTypes.sql_point))
                );
            }

            if (npgsqlLSegType != null)
            {
                RegisterConverter(
                    npgsqlLSegType,
                    new DotnetTypeToSqlTypeConverter(d =>
                        TypeMappingHelpers.CreateGeometryType(MySqlTypes.sql_linestring)
                    )
                );
            }

            if (npgsqlPathType != null)
            {
                RegisterConverter(
                    npgsqlPathType,
                    new DotnetTypeToSqlTypeConverter(d =>
                        TypeMappingHelpers.CreateGeometryType(MySqlTypes.sql_geometry)
                    )
                );
            }

            if (npgsqlPolygonType != null)
            {
                RegisterConverter(
                    npgsqlPolygonType,
                    new DotnetTypeToSqlTypeConverter(d => TypeMappingHelpers.CreateGeometryType(MySqlTypes.sql_polygon))
                );
            }

            if (npgsqlLineType != null)
            {
                RegisterConverter(
                    npgsqlLineType,
                    new DotnetTypeToSqlTypeConverter(d =>
                        TypeMappingHelpers.CreateGeometryType(MySqlTypes.sql_linestring)
                    )
                );
            }

            if (npgsqlCircleType != null)
            {
                RegisterConverter(
                    npgsqlCircleType,
                    new DotnetTypeToSqlTypeConverter(d =>
                        TypeMappingHelpers.CreateGeometryType(MySqlTypes.sql_geometry)
                    )
                );
            }

            if (npgsqlBoxType != null)
            {
                RegisterConverter(
                    npgsqlBoxType,
                    new DotnetTypeToSqlTypeConverter(d => TypeMappingHelpers.CreateGeometryType(MySqlTypes.sql_polygon))
                );
            }

            // PostgreSQL network types map to VARCHAR
            var npgsqlInetType = Type.GetType("NpgsqlTypes.NpgsqlInet, Npgsql");
            var npgsqlCidrType = Type.GetType("NpgsqlTypes.NpgsqlCidr, Npgsql");

            if (npgsqlInetType != null)
            {
                RegisterConverter(
                    npgsqlInetType,
                    new DotnetTypeToSqlTypeConverter(d =>
                        TypeMappingHelpers.CreateStringType(MySqlTypes.sql_varchar, 45, isUnicode: false)
                    )
                );
            }

            if (npgsqlCidrType != null)
            {
                RegisterConverter(
                    npgsqlCidrType,
                    new DotnetTypeToSqlTypeConverter(d =>
                        TypeMappingHelpers.CreateStringType(MySqlTypes.sql_varchar, 43, isUnicode: false)
                    )
                );
            }

            // PostgreSQL range and special types map to JSON
            var npgsqlSpecialTypes = new[]
            {
                Type.GetType("NpgsqlTypes.NpgsqlInterval, Npgsql"),
                Type.GetType("NpgsqlTypes.NpgsqlTid, Npgsql"),
                Type.GetType("NpgsqlTypes.NpgsqlTsQuery, Npgsql"),
                Type.GetType("NpgsqlTypes.NpgsqlTsVector, Npgsql"),
            }
                .Where(t => t != null)
                .ToArray();

            var npgsqlSpecialConverter = new DotnetTypeToSqlTypeConverter(d =>
                TypeMappingHelpers.CreateJsonType(MySqlTypes.sql_json, isText: false)
            );
            RegisterConverterForTypes(npgsqlSpecialConverter, npgsqlSpecialTypes!);

            // PostgreSQL range arrays
            var rangeType = Type.GetType("NpgsqlTypes.NpgsqlRange`1, Npgsql");
            if (rangeType != null)
            {
                var rangeArrayTypes = new[]
                {
                    typeof(DateOnly),
                    typeof(int),
                    typeof(long),
                    typeof(decimal),
                    typeof(DateTime),
                    typeof(DateTimeOffset),
                }
                    .Select(t => rangeType.MakeGenericType(t).MakeArrayType())
                    .ToArray();

                var rangeArrayConverter = new DotnetTypeToSqlTypeConverter(d =>
                    TypeMappingHelpers.CreateJsonType(MySqlTypes.sql_json, isText: false)
                );
                RegisterConverterForTypes(rangeArrayConverter, rangeArrayTypes);
            }
        }

        /// <summary>
        /// Registers SQL types to .NET types converters.
        /// </summary>
        protected override void RegisterSqlTypeToDotnetTypeConverters()
        {
            var booleanConverter = GetBooleanToDotnetTypeConverter();
            var numericConverter = GetNumericToDotnetTypeConverter();
            var guidConverter = GetGuidToDotnetTypeConverter();
            var textConverter = GetTextToDotnetTypeConverter();
            var jsonConverter = GetJsonToDotnetTypeConverter();
            var dateTimeConverter = GetDateTimeToDotnetTypeConverter();
            var byteArrayConverter = GetByteArrayToDotnetTypeConverter();
            var geometricConverter = GetGeometricToDotnetTypeConverter();

            // Boolean affinity (in MySQL, bool and boolean are the same, they are synonyms of tinyint(1))
            RegisterConverterForTypes(booleanConverter, MySqlTypes.sql_bool, MySqlTypes.sql_boolean);

            // Numeric affinity
            RegisterConverterForTypes(
                numericConverter,
                MySqlTypes.sql_bit,
                MySqlTypes.sql_tinyint,
                MySqlTypes.sql_tinyint_unsigned,
                MySqlTypes.sql_smallint,
                MySqlTypes.sql_smallint_unsigned,
                MySqlTypes.sql_mediumint,
                MySqlTypes.sql_mediumint_unsigned,
                MySqlTypes.sql_int,
                MySqlTypes.sql_int_unsigned,
                MySqlTypes.sql_integer,
                MySqlTypes.sql_integer_unsigned,
                MySqlTypes.sql_bigint,
                MySqlTypes.sql_bigint_unsigned,
                MySqlTypes.sql_serial,
                MySqlTypes.sql_fixed,
                MySqlTypes.sql_real,
                MySqlTypes.sql_float,
                MySqlTypes.sql_dec,
                MySqlTypes.sql_decimal,
                MySqlTypes.sql_numeric,
                MySqlTypes.sql_double,
                MySqlTypes.sql_double_unsigned,
                MySqlTypes.sql_double_precision,
                MySqlTypes.sql_double_precision_unsigned
            );

            // DateTime affinity
            RegisterConverterForTypes(
                dateTimeConverter,
                MySqlTypes.sql_datetime,
                MySqlTypes.sql_timestamp,
                MySqlTypes.sql_time,
                MySqlTypes.sql_date,
                MySqlTypes.sql_year
            );

            // Guid affinity
            RegisterConverterForTypes(guidConverter, MySqlTypes.sql_char, MySqlTypes.sql_varchar);

            // Text affinity
            RegisterConverterForTypes(
                textConverter,
                MySqlTypes.sql_char,
                MySqlTypes.sql_varchar,
                MySqlTypes.sql_long_varchar,
                MySqlTypes.sql_tinytext,
                MySqlTypes.sql_mediumtext,
                MySqlTypes.sql_text,
                MySqlTypes.sql_longtext,
                MySqlTypes.sql_enum,
                MySqlTypes.sql_set
            );

            // Json affinity
            RegisterConverterForTypes(jsonConverter, MySqlTypes.sql_json);

            // Binary affinity
            RegisterConverterForTypes(
                byteArrayConverter,
                MySqlTypes.sql_binary,
                MySqlTypes.sql_varbinary,
                MySqlTypes.sql_long_varbinary,
                MySqlTypes.sql_tinyblob,
                MySqlTypes.sql_blob,
                MySqlTypes.sql_mediumblob,
                MySqlTypes.sql_longblob
            );

            // Geometry affinity
            RegisterConverterForTypes(
                geometricConverter,
                MySqlTypes.sql_geometry,
                MySqlTypes.sql_point,
                MySqlTypes.sql_linestring,
                MySqlTypes.sql_polygon,
                MySqlTypes.sql_multipoint,
                MySqlTypes.sql_multilinestring,
                MySqlTypes.sql_multipolygon,
                MySqlTypes.sql_geometrycollection,
                MySqlTypes.sql_geomcollection
            );
        }

        #region SqlTypeToDotnetTypeConverters

        /// <summary>
        /// Gets the boolean to .NET type converter.
        /// </summary>
        /// <returns>The boolean to .NET type converter.</returns>
        private static SqlTypeToDotnetTypeConverter GetBooleanToDotnetTypeConverter()
        {
            return new(d =>
            {
                if (d.BaseTypeName == MySqlTypes.sql_bool || d.BaseTypeName == MySqlTypes.sql_boolean)
                {
                    return new DotnetTypeDescriptor(typeof(bool));
                }

                return null;
            });
        }

        /// <summary>
        /// Gets the numeric to .NET type converter.
        /// </summary>
        /// <returns>The numeric to .NET type converter.</returns>
        private static SqlTypeToDotnetTypeConverter GetNumericToDotnetTypeConverter()
        {
            return new(d =>
            {
                switch (d.BaseTypeName)
                {
                    case MySqlTypes.sql_bit:
                        if (!d.Length.HasValue || d.Length == 1)
                        {
                            return new DotnetTypeDescriptor(typeof(bool));
                        }

                        if (d.Length == 8)
                        {
                            return new DotnetTypeDescriptor(typeof(byte));
                        }

                        if (d.Length == 16)
                        {
                            return new DotnetTypeDescriptor(typeof(short));
                        }

                        if (d.Length == 32)
                        {
                            return new DotnetTypeDescriptor(typeof(int));
                        }

                        if (d.Length == 64)
                        {
                            return new DotnetTypeDescriptor(typeof(long));
                        }

                        // make it a long if no recognizable length is specified
                        return new DotnetTypeDescriptor(typeof(long));
                    case MySqlTypes.sql_tinyint:
                        if (d.Precision == 1)
                        {
                            return new DotnetTypeDescriptor(typeof(bool));
                        }
                        return new DotnetTypeDescriptor(typeof(sbyte));
                    case MySqlTypes.sql_tinyint_unsigned:
                        if (d.Precision == 1)
                        {
                            return new DotnetTypeDescriptor(typeof(bool));
                        }
                        return new DotnetTypeDescriptor(typeof(byte));
                    case MySqlTypes.sql_smallint:
                        return new DotnetTypeDescriptor(typeof(short));
                    case MySqlTypes.sql_smallint_unsigned:
                        return new DotnetTypeDescriptor(typeof(ushort));
                    case MySqlTypes.sql_mediumint:
                    case MySqlTypes.sql_int:
                    case MySqlTypes.sql_integer:
                        return new DotnetTypeDescriptor(typeof(int));
                    case MySqlTypes.sql_serial:
                        return new DotnetTypeDescriptor(typeof(int), isAutoIncrementing: true);
                    case MySqlTypes.sql_mediumint_unsigned:
                    case MySqlTypes.sql_int_unsigned:
                    case MySqlTypes.sql_integer_unsigned:
                        return new DotnetTypeDescriptor(typeof(uint));
                    case MySqlTypes.sql_bigint:
                        return new DotnetTypeDescriptor(typeof(long));
                    case MySqlTypes.sql_bigint_unsigned:
                        return new DotnetTypeDescriptor(typeof(ulong));
                    case MySqlTypes.sql_decimal:
                    case MySqlTypes.sql_dec:
                    case MySqlTypes.sql_fixed:
                    case MySqlTypes.sql_numeric:
                        return new DotnetTypeDescriptor(typeof(decimal))
                        {
                            Precision = d.Precision ?? 16,
                            Scale = d.Scale ?? 4,
                        };
                    case MySqlTypes.sql_float:
                        return new DotnetTypeDescriptor(typeof(float));
                    case MySqlTypes.sql_real:
                    case MySqlTypes.sql_double_precision:
                    case MySqlTypes.sql_double:
                        return new DotnetTypeDescriptor(typeof(double));
                    case MySqlTypes.sql_double_precision_unsigned:
                    case MySqlTypes.sql_double_unsigned:
                        // there is no unsigned double in C#
                        return new DotnetTypeDescriptor(typeof(double));
                }

                return null;
            });
        }

        /// <summary>
        /// Gets the DateTime to .NET type converter.
        /// </summary>
        /// <returns>The DateTime to .NET type converter.</returns>
        private static SqlTypeToDotnetTypeConverter GetDateTimeToDotnetTypeConverter()
        {
            return new(d =>
            {
                switch (d.BaseTypeName)
                {
                    case MySqlTypes.sql_datetime:
                        return new DotnetTypeDescriptor(typeof(DateTime));
                    case MySqlTypes.sql_timestamp:
                        return new DotnetTypeDescriptor(typeof(DateTimeOffset));
                    case MySqlTypes.sql_time:
                        return new DotnetTypeDescriptor(typeof(TimeOnly));
                    case MySqlTypes.sql_date:
                        return new DotnetTypeDescriptor(typeof(DateOnly));
                    case MySqlTypes.sql_year:
                        return new DotnetTypeDescriptor(typeof(int));
                }

                return null;
            });
        }

        /// <summary>
        /// Gets the GUID to .NET type converter.
        /// </summary>
        /// <returns>The GUID to .NET type converter.</returns>
        private static SqlTypeToDotnetTypeConverter GetGuidToDotnetTypeConverter()
        {
            return new(d =>
            {
                if (d.Length == 36)
                {
                    return new DotnetTypeDescriptor(typeof(Guid));
                }

                // move on to the next type converter
                return null;
            });
        }

        /// <summary>
        /// Gets the text to .NET type converter.
        /// </summary>
        /// <returns>The text to .NET type converter.</returns>
        private static SqlTypeToDotnetTypeConverter GetTextToDotnetTypeConverter()
        {
            /*
            MySQL Type   Maximum Length/Count
            -------------------------------------------------------
            CHAR(M)      255 characters
            VARCHAR(M)   65,535 characters (or 16,383 if M > 16,383)
            LONGTEXT     (2^{32} - 1) bytes (approximately 4 GB)
            TINYTEXT     255 bytes
            MEDIUMTEXT   (2^{24} - 1) bytes (approximately 16 MB)
            TEXT         (2^{16} - 1) bytes (65,535 bytes)
            ENUM         65,535 enumeration values, each up to 255 characters
            SET          64 set members, each up to 255 characters
            */
            return new(d =>
            {
                if (
                    (d.BaseTypeName == MySqlTypes.sql_char || d.BaseTypeName == MySqlTypes.sql_varchar)
                    && d.Length == 36
                )
                {
                    return new DotnetTypeDescriptor(typeof(Guid));
                }

                switch (d.BaseTypeName)
                {
                    case MySqlTypes.sql_char:
                        return new DotnetTypeDescriptor(
                            typeof(string),
                            d.Length,
                            isUnicode: d.IsUnicode.GetValueOrDefault(true),
                            isFixedLength: true
                        );
                    case MySqlTypes.sql_varchar:
                    case MySqlTypes.sql_tinytext:
                    case MySqlTypes.sql_mediumtext:
                    case MySqlTypes.sql_text:
                    case MySqlTypes.sql_long_varchar:
                    case MySqlTypes.sql_longtext:
                    case MySqlTypes.sql_enum:
                    case MySqlTypes.sql_set:
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

        /// <summary>
        /// Gets the JSON to .NET type converter.
        /// </summary>
        /// <returns>The JSON to .NET type converter.</returns>
        private static SqlTypeToDotnetTypeConverter GetJsonToDotnetTypeConverter()
        {
            return new(d => new DotnetTypeDescriptor(typeof(JsonDocument)));
        }

        /// <summary>
        /// Gets the byte array to .NET type converter.
        /// </summary>
        /// <returns>The byte array to .NET type converter.</returns>
        private static SqlTypeToDotnetTypeConverter GetByteArrayToDotnetTypeConverter()
        {
            return new(d =>
            {
                if (d.BaseTypeName == MySqlTypes.sql_binary && d.Length.HasValue)
                {
                    return new DotnetTypeDescriptor(typeof(byte[]), d.Length, isFixedLength: true);
                }

                switch (d.BaseTypeName)
                {
                    case MySqlTypes.sql_binary:
                    case MySqlTypes.sql_varbinary:
                    case MySqlTypes.sql_long_varbinary:
                    case MySqlTypes.sql_tinyblob:
                    case MySqlTypes.sql_blob:
                    case MySqlTypes.sql_mediumblob:
                    case MySqlTypes.sql_longblob:
                        return new DotnetTypeDescriptor(
                            typeof(byte[]),
                            d.Length,
                            isUnicode: true,
                            isFixedLength: d.IsFixedLength.GetValueOrDefault(false)
                        );
                }

                return null;
            });
        }

        /// <summary>
        /// Gets the geometric to .NET type converter.
        /// </summary>
        /// <returns>The geometric to .NET type converter.</returns>
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
            var sqlNetTopologyMultiPolygonType = Type.GetType(
                "NetTopologySuite.Geometries.MultiPolygon, NetTopologySuite"
            );
            var sqlNetTopologyGeometryCollectionType = Type.GetType(
                "NetTopologySuite.Geometries.GeometryCollection, NetTopologySuite"
            );

            // Geometry affinity
            var sqlMySqlDataGeometryType = Type.GetType("MySql.Data.Types.MySqlGeometry, MySql.Data", false, false);
            var sqlMySqlConnectorGeometryType = Type.GetType(
                "MySqlConnector.MySqlGeometry, MySqlConnector",
                false,
                false
            );

            return new(d =>
            {
                switch (d.BaseTypeName)
                {
                    case MySqlTypes.sql_geometry:
                        if (sqlNetTopologyGeometryType != null)
                        {
                            return new DotnetTypeDescriptor(sqlNetTopologyGeometryType);
                        }

                        if (sqlMySqlDataGeometryType != null)
                        {
                            return new DotnetTypeDescriptor(sqlMySqlDataGeometryType);
                        }

                        if (sqlMySqlConnectorGeometryType != null)
                        {
                            return new DotnetTypeDescriptor(sqlMySqlConnectorGeometryType);
                        }

                        // Fallback: WKT (Well-Known Text) format as string
                        return new DotnetTypeDescriptor(typeof(string));
                    case MySqlTypes.sql_point:
                        if (sqlNetTopologyPointType != null)
                        {
                            return new DotnetTypeDescriptor(sqlNetTopologyPointType);
                        }

                        if (sqlMySqlDataGeometryType != null)
                        {
                            return new DotnetTypeDescriptor(sqlMySqlDataGeometryType);
                        }

                        if (sqlMySqlConnectorGeometryType != null)
                        {
                            return new DotnetTypeDescriptor(sqlMySqlConnectorGeometryType);
                        }

                        // Fallback: WKT format as string
                        return new DotnetTypeDescriptor(typeof(string));
                    case MySqlTypes.sql_linestring:
                        if (sqlNetTopologyLineStringType != null)
                        {
                            return new DotnetTypeDescriptor(sqlNetTopologyLineStringType);
                        }

                        if (sqlMySqlDataGeometryType != null)
                        {
                            return new DotnetTypeDescriptor(sqlMySqlDataGeometryType);
                        }

                        if (sqlMySqlConnectorGeometryType != null)
                        {
                            return new DotnetTypeDescriptor(sqlMySqlConnectorGeometryType);
                        }

                        // Fallback: WKT format as string
                        return new DotnetTypeDescriptor(typeof(string));
                    case MySqlTypes.sql_polygon:
                        if (sqlNetTopologyPolygonType != null)
                        {
                            return new DotnetTypeDescriptor(sqlNetTopologyPolygonType);
                        }

                        if (sqlMySqlDataGeometryType != null)
                        {
                            return new DotnetTypeDescriptor(sqlMySqlDataGeometryType);
                        }

                        if (sqlMySqlConnectorGeometryType != null)
                        {
                            return new DotnetTypeDescriptor(sqlMySqlConnectorGeometryType);
                        }

                        // Fallback: WKT format as string
                        return new DotnetTypeDescriptor(typeof(string));
                    case MySqlTypes.sql_multipoint:
                        if (sqlNetTopologyMultiPointType != null)
                        {
                            return new DotnetTypeDescriptor(sqlNetTopologyMultiPointType);
                        }

                        if (sqlMySqlDataGeometryType != null)
                        {
                            return new DotnetTypeDescriptor(sqlMySqlDataGeometryType);
                        }

                        if (sqlMySqlConnectorGeometryType != null)
                        {
                            return new DotnetTypeDescriptor(sqlMySqlConnectorGeometryType);
                        }

                        // Fallback: WKT format as string
                        return new DotnetTypeDescriptor(typeof(string));
                    case MySqlTypes.sql_multilinestring:
                        if (sqlNetTopologyMultLineStringType != null)
                        {
                            return new DotnetTypeDescriptor(sqlNetTopologyMultLineStringType);
                        }

                        if (sqlMySqlDataGeometryType != null)
                        {
                            return new DotnetTypeDescriptor(sqlMySqlDataGeometryType);
                        }

                        if (sqlMySqlConnectorGeometryType != null)
                        {
                            return new DotnetTypeDescriptor(sqlMySqlConnectorGeometryType);
                        }

                        // Fallback: WKT format as string
                        return new DotnetTypeDescriptor(typeof(string));
                    case MySqlTypes.sql_multipolygon:
                        if (sqlNetTopologyMultiPolygonType != null)
                        {
                            return new DotnetTypeDescriptor(sqlNetTopologyMultiPolygonType);
                        }

                        if (sqlMySqlDataGeometryType != null)
                        {
                            return new DotnetTypeDescriptor(sqlMySqlDataGeometryType);
                        }

                        if (sqlMySqlConnectorGeometryType != null)
                        {
                            return new DotnetTypeDescriptor(sqlMySqlConnectorGeometryType);
                        }

                        // Fallback: WKT format as string
                        return new DotnetTypeDescriptor(typeof(string));
                    case MySqlTypes.sql_geomcollection:
                    case MySqlTypes.sql_geometrycollection:
                        if (sqlNetTopologyGeometryCollectionType != null)
                        {
                            return new DotnetTypeDescriptor(sqlNetTopologyGeometryCollectionType);
                        }

                        if (sqlMySqlDataGeometryType != null)
                        {
                            return new DotnetTypeDescriptor(sqlMySqlDataGeometryType);
                        }

                        if (sqlMySqlConnectorGeometryType != null)
                        {
                            return new DotnetTypeDescriptor(sqlMySqlConnectorGeometryType);
                        }

                        // Fallback: WKT format as string
                        return new DotnetTypeDescriptor(typeof(string));
                }

                return null;
            });
        }

        #endregion // SqlTypeToDotnetTypeConverters
    }
}
