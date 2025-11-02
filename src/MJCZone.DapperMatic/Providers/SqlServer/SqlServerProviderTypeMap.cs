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

namespace MJCZone.DapperMatic.Providers.SqlServer;

// See:
// https://learn.microsoft.com/en-us/dotnet/framework/data/adonet/sql-server-data-type-mappings
// https://learn.microsoft.com/en-us/dotnet/framework/data/adonet/sql/linq/media/sql-clr-type-mapping.png

/// <summary>
/// Provides a type map for SQL Server, mapping .NET types to SQL Server types and vice versa.
/// </summary>
public sealed class SqlServerProviderTypeMap : DbProviderTypeMapBase<SqlServerProviderTypeMap>
{
    /// <inheritdoc/>
    protected override IProviderTypeMapping GetProviderTypeMapping()
    {
        return new SqlServerTypeMapping();
    }

    /// <inheritdoc/>
    protected override string GetProviderName()
    {
        return "sqlserver";
    }

    /// <inheritdoc/>
    protected override void RegisterNetTopologySuiteTypes()
    {
        // NetTopologySuite types map to VARCHAR(MAX) on SQL Server (WKT text format)
        // SQL Server's native GEOMETRY type requires Microsoft.SqlServer.Types which has been removed
        // for cross-platform compatibility. NTS geometries are stored as WKT text.
        var ntsGeometryType = Type.GetType("NetTopologySuite.Geometries.Geometry, NetTopologySuite");
        var ntsPointType = Type.GetType("NetTopologySuite.Geometries.Point, NetTopologySuite");
        var ntsLineStringType = Type.GetType("NetTopologySuite.Geometries.LineString, NetTopologySuite");
        var ntsPolygonType = Type.GetType("NetTopologySuite.Geometries.Polygon, NetTopologySuite");
        var ntsMultiPointType = Type.GetType("NetTopologySuite.Geometries.MultiPoint, NetTopologySuite");
        var ntsMultiLineStringType = Type.GetType("NetTopologySuite.Geometries.MultiLineString, NetTopologySuite");
        var ntsMultiPolygonType = Type.GetType("NetTopologySuite.Geometries.MultiPolygon, NetTopologySuite");
        var ntsGeometryCollectionType = Type.GetType(
            "NetTopologySuite.Geometries.GeometryCollection, NetTopologySuite"
        );

        var geometryTypeConverter = TypeMappingHelpers.CreateStringType(
            SqlServerTypes.sql_varchar,
            isUnicode: false,
            length: TypeMappingDefaults.MaxLength
        );

        RegisterConverterForTypes(
            new DotnetTypeToSqlTypeConverter(d => geometryTypeConverter),
            [
                ntsGeometryType,
                ntsPointType,
                ntsLineStringType,
                ntsPolygonType,
                ntsMultiPointType,
                ntsMultiLineStringType,
                ntsMultiPolygonType,
                ntsGeometryCollectionType,
            ]
        );
    }

    /// <inheritdoc/>
    protected override void RegisterSqlServerTypes()
    {
        // SQL Server-specific spatial types (SqlGeometry, SqlGeography, SqlHierarchyId) are no longer supported
        // to maintain cross-platform compatibility. They require Microsoft.SqlServer.Types with Windows-specific
        // native assemblies.
        //
        // For spatial data on SQL Server, use NetTopologySuite types instead (cross-platform).
        // NTS geometries are stored as WKT (Well-Known Text) in VARCHAR(MAX) columns.
        // The geometry.ToString() method produces WKT format which is parsed back by the handler.
    }

    /// <inheritdoc/>
    protected override void RegisterMySqlTypes()
    {
        var sqlMySqlDataGeometryType = Type.GetType("MySql.Data.Types.MySqlGeometry, MySql.Data");
        var sqlMySqlConnectorGeometryType = Type.GetType("MySqlConnector.MySqlGeometry, MySqlConnector");

        // MySQL geometry types → VARCHAR (WKB format)
        // Note: Without SqlGeometry support, we store these as WKB hex strings instead of native geometry columns
        var mySqlGeometryTypeInSqlServer = TypeMappingHelpers.CreateStringType(
            SqlServerTypes.sql_varchar,
            isUnicode: false,
            length: TypeMappingDefaults.MaxLength
        );
        var mySqlGeometryConverter = new DotnetTypeToSqlTypeConverter(d => mySqlGeometryTypeInSqlServer);

        RegisterConverterForTypes(mySqlGeometryConverter, [sqlMySqlDataGeometryType, sqlMySqlConnectorGeometryType]);
    }

    /// <inheritdoc/>
    protected override void RegisterNpgsqlTypes()
    {
        // PostgreSQL geometric types → VARCHAR (WKT format)
        // Note: Without SqlGeometry support, we store these as WKT text instead of native geometry columns
        var npgsqlGeometryTypes = new[]
        {
            Type.GetType("NpgsqlTypes.NpgsqlPoint, Npgsql"),
            Type.GetType("NpgsqlTypes.NpgsqlLSeg, Npgsql"),
            Type.GetType("NpgsqlTypes.NpgsqlPath, Npgsql"),
            Type.GetType("NpgsqlTypes.NpgsqlPolygon, Npgsql"),
            Type.GetType("NpgsqlTypes.NpgsqlLine, Npgsql"),
            Type.GetType("NpgsqlTypes.NpgsqlCircle, Npgsql"),
            Type.GetType("NpgsqlTypes.NpgsqlBox, Npgsql"),
        }
            .Where(t => t != null)
            .ToArray();

        var npgsqlGeometryConverter = new DotnetTypeToSqlTypeConverter(d =>
            TypeMappingHelpers.CreateStringType(
                SqlServerTypes.sql_varchar,
                isUnicode: false,
                length: TypeMappingDefaults.MaxLength
            )
        );
        RegisterConverterForTypes(npgsqlGeometryConverter, npgsqlGeometryTypes!);

        // PostgreSQL network types map to VARCHAR
        var npgsqlInetType = Type.GetType("NpgsqlTypes.NpgsqlInet, Npgsql");
        var npgsqlCidrType = Type.GetType("NpgsqlTypes.NpgsqlCidr, Npgsql");

        if (npgsqlInetType != null)
        {
            RegisterConverter(
                npgsqlInetType,
                new DotnetTypeToSqlTypeConverter(d =>
                    TypeMappingHelpers.CreateStringType(SqlServerTypes.sql_varchar, 45, isUnicode: false)
                )
            );
        }

        if (npgsqlCidrType != null)
        {
            RegisterConverter(
                npgsqlCidrType,
                new DotnetTypeToSqlTypeConverter(d =>
                    TypeMappingHelpers.CreateStringType(SqlServerTypes.sql_varchar, 43, isUnicode: false)
                )
            );
        }

        // PostgreSQL range and special types map to VARCHAR(MAX) for JSON serialization
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
            TypeMappingHelpers.CreateLobType($"{SqlServerTypes.sql_varchar}(max)", isUnicode: false)
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
                TypeMappingHelpers.CreateLobType($"{SqlServerTypes.sql_varchar}(max)", isUnicode: false)
            );
            RegisterConverterForTypes(rangeArrayConverter, rangeArrayTypes);
        }
    }

    /// <inheritdoc/>
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
        var objectConverter = GetObjectToDotnetTypeConverter();
        var geometricConverter = GetGeometricToDotnetTypeConverter();

        // Boolean affinity (in SQL Server, the bit type is used for boolean values, it consists of 0 or 1)
        RegisterConverter(SqlServerTypes.sql_bit, booleanConverter);

        // Numeric affinity
        RegisterConverterForTypes(
            numericConverter,
            SqlServerTypes.sql_tinyint,
            SqlServerTypes.sql_smallint,
            SqlServerTypes.sql_int,
            SqlServerTypes.sql_bigint,
            SqlServerTypes.sql_real,
            SqlServerTypes.sql_float,
            SqlServerTypes.sql_decimal,
            SqlServerTypes.sql_numeric,
            SqlServerTypes.sql_money,
            SqlServerTypes.sql_smallmoney
        );

        // Guid affinity
        RegisterConverter(SqlServerTypes.sql_uniqueidentifier, guidConverter);

        // Text affinity
        RegisterConverterForTypes(
            textConverter,
            SqlServerTypes.sql_nvarchar,
            SqlServerTypes.sql_varchar,
            SqlServerTypes.sql_ntext,
            SqlServerTypes.sql_text,
            SqlServerTypes.sql_nchar,
            SqlServerTypes.sql_char
        );

        // Xml affinity
        RegisterConverter(SqlServerTypes.sql_xml, xmlConverter);

        // Json affinity (only for very latest versions of SQL Server)
        // See: https://learn.microsoft.com/en-us/sql/t-sql/data-types/json-data-type?view=sql-server-ver17
        // Introduced in Azure SQL Database and Azure SQL Managed Instance, and is also available
        // in SQL Server 2025 (17.x) Preview.
        // RegisterConverter(SqlServerTypes.sql_json, jsonConverter);

        // DateTime affinity
        RegisterConverterForTypes(
            dateTimeConverter,
            SqlServerTypes.sql_smalldatetime,
            SqlServerTypes.sql_datetime,
            SqlServerTypes.sql_datetime2,
            SqlServerTypes.sql_datetimeoffset,
            SqlServerTypes.sql_time,
            SqlServerTypes.sql_date,
            SqlServerTypes.sql_timestamp,
            SqlServerTypes.sql_rowversion
        );

        // Binary affinity
        RegisterConverterForTypes(
            byteArrayConverter,
            SqlServerTypes.sql_varbinary,
            SqlServerTypes.sql_binary,
            SqlServerTypes.sql_image
        );

        // Object affinity
        RegisterConverter(SqlServerTypes.sql_variant, objectConverter);

        // Geometry affinity
        RegisterConverterForTypes(
            geometricConverter,
            SqlServerTypes.sql_geometry,
            SqlServerTypes.sql_geography,
            SqlServerTypes.sql_hierarchyid
        );
    }

    #region Custom Provider-Specific Converters (Override base if needed)

    /// <summary>
    /// Gets the DateTime to SQL type converter with SQL Server specific defaults.
    /// SQL Server uses datetime instead of datetime2 for DateTime by default in the original implementation.
    /// This overrides the base implementation to maintain backward compatibility.
    /// </summary>
    /// <returns>DateTime to SQL type converter with SQL Server specific behavior.</returns>
    protected override DotnetTypeToSqlTypeConverter GetDateTimeToSqlTypeConverter()
    {
        return new DotnetTypeToSqlTypeConverter(d =>
        {
            return d.DotnetType switch
            {
                Type t when t == typeof(DateTime) => TypeMappingHelpers.CreateSimpleType(SqlServerTypes.sql_datetime),
                Type t when t == typeof(DateTimeOffset) => TypeMappingHelpers.CreateSimpleType(
                    SqlServerTypes.sql_datetimeoffset
                ),
                Type t when t == typeof(TimeSpan) => TypeMappingHelpers.CreateSimpleType(SqlServerTypes.sql_time),
                Type t when t == typeof(DateOnly) => TypeMappingHelpers.CreateSimpleType(SqlServerTypes.sql_date),
                Type t when t == typeof(TimeOnly) => TypeMappingHelpers.CreateSimpleType(SqlServerTypes.sql_time),
                _ => TypeMappingHelpers.CreateSimpleType(SqlServerTypes.sql_datetime),
            };
        });
    }

    #endregion // Custom Provider-Specific Converters

    #region SqlTypeToDotnetTypeConverters

    private static SqlTypeToDotnetTypeConverter GetBooleanToDotnetTypeConverter()
    {
        return new(d => new DotnetTypeDescriptor(typeof(bool)));
    }

    private static SqlTypeToDotnetTypeConverter GetNumbericToDotnetTypeConverter()
    {
        return new(d =>
        {
            return d.BaseTypeName switch
            {
                SqlServerTypes.sql_tinyint => new DotnetTypeDescriptor(typeof(byte)),
                SqlServerTypes.sql_smallint => new DotnetTypeDescriptor(typeof(short)),
                SqlServerTypes.sql_int => new DotnetTypeDescriptor(typeof(int)),
                SqlServerTypes.sql_bigint => new DotnetTypeDescriptor(typeof(long)),
                SqlServerTypes.sql_real => new DotnetTypeDescriptor(typeof(float)),
                SqlServerTypes.sql_float => new DotnetTypeDescriptor(typeof(double)),
                SqlServerTypes.sql_decimal => new DotnetTypeDescriptor(typeof(decimal))
                {
                    Precision = d.Precision ?? 16,
                    Scale = d.Scale ?? 4,
                },
                SqlServerTypes.sql_numeric => new DotnetTypeDescriptor(typeof(decimal))
                {
                    Precision = d.Precision ?? 16,
                    Scale = d.Scale ?? 4,
                },
                SqlServerTypes.sql_money => new DotnetTypeDescriptor(typeof(decimal))
                {
                    Precision = d.Precision ?? 19,
                    Scale = d.Scale ?? 4,
                },
                SqlServerTypes.sql_smallmoney => new DotnetTypeDescriptor(typeof(decimal))
                {
                    Precision = d.Precision ?? 10,
                    Scale = d.Scale ?? 4,
                },
                _ => new DotnetTypeDescriptor(typeof(int)),
            };
        });
    }

    private static SqlTypeToDotnetTypeConverter GetGuidToDotnetTypeConverter()
    {
        return new(d => new DotnetTypeDescriptor(typeof(Guid)));
    }

    private static SqlTypeToDotnetTypeConverter GetTextToDotnetTypeConverter()
    {
        return new(d => new DotnetTypeDescriptor(
            typeof(string),
            d.Length ?? 255,
            isUnicode: d.IsUnicode.GetValueOrDefault(true),
            isFixedLength: d.IsFixedLength.GetValueOrDefault(false)
        ));
    }

    private static SqlTypeToDotnetTypeConverter GetXmlToDotnetTypeConverter()
    {
        return new(d => new DotnetTypeDescriptor(typeof(XDocument)));
    }

    private static SqlTypeToDotnetTypeConverter GetJsonToDotnetTypeConverter()
    {
        return new(d => new DotnetTypeDescriptor(typeof(JsonDocument)));
    }

    private static SqlTypeToDotnetTypeConverter GetDateTimeToDotnetTypeConverter()
    {
        return new(d =>
            d.BaseTypeName switch
            {
                SqlServerTypes.sql_smalldatetime
                or SqlServerTypes.sql_datetime
                or SqlServerTypes.sql_datetime2
                or SqlServerTypes.sql_timestamp => new DotnetTypeDescriptor(typeof(DateTime)),
                SqlServerTypes.sql_datetimeoffset => new DotnetTypeDescriptor(typeof(DateTimeOffset)),
                SqlServerTypes.sql_time => new DotnetTypeDescriptor(typeof(TimeOnly)),
                SqlServerTypes.sql_date => new DotnetTypeDescriptor(typeof(DateOnly)),
                _ => new DotnetTypeDescriptor(typeof(DateTime)),
            }
        );
    }

    private static SqlTypeToDotnetTypeConverter GetByteArrayToDotnetTypeConverter()
    {
        return new(d => new DotnetTypeDescriptor(
            typeof(byte[]),
            d.Length ?? int.MaxValue,
            isFixedLength: d.IsFixedLength.GetValueOrDefault(false)
        ));
    }

    private static SqlTypeToDotnetTypeConverter GetObjectToDotnetTypeConverter()
    {
        return new(d => new DotnetTypeDescriptor(typeof(object)));
    }

    private static SqlTypeToDotnetTypeConverter GetGeometricToDotnetTypeConverter()
    {
        // NetTopologySuite types map to specific SqlServer geometry types
        var ntsGeometryType = Type.GetType("NetTopologySuite.Geometries.Geometry, NetTopologySuite");
        var ntsPointType = Type.GetType("NetTopologySuite.Geometries.Point, NetTopologySuite");
        var ntsLineStringType = Type.GetType("NetTopologySuite.Geometries.LineString, NetTopologySuite");
        var ntsPolygonType = Type.GetType("NetTopologySuite.Geometries.Polygon, NetTopologySuite");
        var ntsMultiPointType = Type.GetType("NetTopologySuite.Geometries.MultiPoint, NetTopologySuite");
        var ntsMultiLineStringType = Type.GetType("NetTopologySuite.Geometries.MultiLineString, NetTopologySuite");
        var ntsMultiPolygonType = Type.GetType("NetTopologySuite.Geometries.MultiPolygon, NetTopologySuite");
        var ntsGeometryCollectionType = Type.GetType(
            "NetTopologySuite.Geometries.GeometryCollection, NetTopologySuite"
        );

        // SQL Server-specific spatial types (SqlGeometry, SqlGeography, SqlHierarchyId) are no longer supported
        // to maintain cross-platform compatibility. For spatial data, use NetTopologySuite types instead.

        return new(d =>
        {
            switch (d.BaseTypeName)
            {
                case SqlServerTypes.sql_geometry:
                    if (ntsGeometryType != null)
                    {
                        return new DotnetTypeDescriptor(ntsGeometryType);
                    }

                    // Fallback: WKB (Well-Known Binary) format as byte[]
                    return new DotnetTypeDescriptor(typeof(byte[]));
                case SqlServerTypes.sql_geography:
                    // WKB (Well-Known Binary) format as byte[]
                    return new DotnetTypeDescriptor(typeof(byte[]));
                case SqlServerTypes.sql_hierarchyid:
                    // Hierarchical path as string (e.g., "/1/2/3/")
                    return new DotnetTypeDescriptor(typeof(string));
            }

            return null;
        });
    }

    #endregion // SqlTypeToDotnetTypeConverters
}
