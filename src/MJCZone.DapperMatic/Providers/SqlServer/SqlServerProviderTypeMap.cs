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
    protected override void RegisterDotnetTypeToSqlTypeConverters()
    {
        // Use the standardized registration from base class
        RegisterStandardDotnetTypeToSqlTypeConverters();
    }

    /// <inheritdoc/>
    protected override SqlTypeDescriptor? CreateGeometryTypeForShortName(string shortName)
    {
        return shortName switch
        {
            // NetTopologySuite types
            "NetTopologySuite.Geometries.Geometry" => TypeMappingHelpers.CreateGeometryType(
                SqlServerTypes.sql_geometry
            ),
            "NetTopologySuite.Geometries.Point"
            or "NetTopologySuite.Geometries.LineString"
            or "NetTopologySuite.Geometries.Polygon"
            or "NetTopologySuite.Geometries.MultiPoint"
            or "NetTopologySuite.Geometries.MultiLineString"
            or "NetTopologySuite.Geometries.MultiPolygon"
            or "NetTopologySuite.Geometries.GeometryCollection" => TypeMappingHelpers.CreateLobType(
                "nvarchar(max)",
                isUnicode: true
            ),
            // SQL Server types
            "Microsoft.SqlServer.Types.SqlGeometry" => TypeMappingHelpers.CreateGeometryType(
                SqlServerTypes.sql_geometry
            ),
            "Microsoft.SqlServer.Types.SqlGeography" => TypeMappingHelpers.CreateGeometryType(
                SqlServerTypes.sql_geography
            ),
            "Microsoft.SqlServer.Types.SqlHierarchyId" => TypeMappingHelpers.CreateSimpleType(
                SqlServerTypes.sql_hierarchyid
            ),
            _ => null, // Unsupported geometry type
        };
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
                case SqlServerTypes.sql_tinyint:
                    return new DotnetTypeDescriptor(typeof(byte));
                case SqlServerTypes.sql_smallint:
                    return new DotnetTypeDescriptor(typeof(short));
                case SqlServerTypes.sql_int:
                    return new DotnetTypeDescriptor(typeof(int));
                case SqlServerTypes.sql_bigint:
                    return new DotnetTypeDescriptor(typeof(long));
                case SqlServerTypes.sql_real:
                    return new DotnetTypeDescriptor(typeof(float));
                case SqlServerTypes.sql_float:
                    return new DotnetTypeDescriptor(typeof(double));
                case SqlServerTypes.sql_decimal:
                    return new DotnetTypeDescriptor(typeof(decimal))
                    {
                        Precision = d.Precision ?? 16,
                        Scale = d.Scale ?? 4,
                    };
                case SqlServerTypes.sql_numeric:
                    return new DotnetTypeDescriptor(typeof(decimal))
                    {
                        Precision = d.Precision ?? 16,
                        Scale = d.Scale ?? 4,
                    };
                case SqlServerTypes.sql_money:
                    return new DotnetTypeDescriptor(typeof(decimal))
                    {
                        Precision = d.Precision ?? 19,
                        Scale = d.Scale ?? 4,
                    };
                case SqlServerTypes.sql_smallmoney:
                    return new DotnetTypeDescriptor(typeof(decimal))
                    {
                        Precision = d.Precision ?? 10,
                        Scale = d.Scale ?? 4,
                    };
                default:
                    return new DotnetTypeDescriptor(typeof(int));
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
            return new DotnetTypeDescriptor(
                typeof(string),
                d.Length ?? 255,
                isUnicode: d.IsUnicode.GetValueOrDefault(true),
                isFixedLength: d.IsFixedLength.GetValueOrDefault(false)
            );
        });
    }

    private static SqlTypeToDotnetTypeConverter GetXmlToDotnetTypeConverter()
    {
        return new(d =>
        {
            return new DotnetTypeDescriptor(typeof(XDocument));
        });
    }

    private static SqlTypeToDotnetTypeConverter GetJsonToDotnetTypeConverter()
    {
        return new(d =>
        {
            return new DotnetTypeDescriptor(typeof(JsonDocument));
        });
    }

    private static SqlTypeToDotnetTypeConverter GetDateTimeToDotnetTypeConverter()
    {
        return new(d =>
        {
            switch (d.BaseTypeName)
            {
                case SqlServerTypes.sql_smalldatetime:
                case SqlServerTypes.sql_datetime:
                case SqlServerTypes.sql_datetime2:
                case SqlServerTypes.sql_timestamp:
                    return new DotnetTypeDescriptor(typeof(DateTime));
                case SqlServerTypes.sql_datetimeoffset:
                    return new DotnetTypeDescriptor(typeof(DateTimeOffset));
                case SqlServerTypes.sql_time:
                    return new DotnetTypeDescriptor(typeof(TimeOnly));
                case SqlServerTypes.sql_date:
                    return new DotnetTypeDescriptor(typeof(DateOnly));
                default:
                    return new DotnetTypeDescriptor(typeof(DateTime));
            }
        });
    }

    private static SqlTypeToDotnetTypeConverter GetByteArrayToDotnetTypeConverter()
    {
        return new(d =>
        {
            return new DotnetTypeDescriptor(
                typeof(byte[]),
                d.Length ?? int.MaxValue,
                isFixedLength: d.IsFixedLength.GetValueOrDefault(false)
            );
        });
    }

    private static SqlTypeToDotnetTypeConverter GetObjectToDotnetTypeConverter()
    {
        return new(d =>
        {
            return new DotnetTypeDescriptor(typeof(object));
        });
    }

    private static SqlTypeToDotnetTypeConverter GetGeometricToDotnetTypeConverter()
    {
        // NetTopologySuite types
        var sqlNetTopologyGeometryType = Type.GetType("NetTopologySuite.Geometries.Geometry, NetTopologySuite");

        // var sqlNetTopologyPointType = Type.GetType(
        //     "NetTopologySuite.Geometries.Point, NetTopologySuite"
        // );
        // var sqlNetTopologyLineStringType = Type.GetType(
        //     "NetTopologySuite.Geometries.LineString, NetTopologySuite"
        // );
        // var sqlNetTopologyPolygonType = Type.GetType(
        //     "NetTopologySuite.Geometries.Polygon, NetTopologySuite"
        // );
        // var sqlNetTopologyMultiPointType = Type.GetType(
        //     "NetTopologySuite.Geometries.MultiPoint, NetTopologySuite"
        // );
        // var sqlNetTopologyMultLineStringType = Type.GetType(
        //     "NetTopologySuite.Geometries.MultiLineString, NetTopologySuite"
        // );
        // var sqlNetTopologyMultiPolygonType = Type.GetType(
        //     "NetTopologySuite.Geometries.MultiPolygon, NetTopologySuite"
        // );
        // var sqlNetTopologyGeometryCollectionType = Type.GetType(
        //     "NetTopologySuite.Geometries.GeometryCollection, NetTopologySuite"
        // );

        // Geometry affinity
        var sqlGeometryType = Type.GetType(
            "Microsoft.SqlServer.Types.SqlGeometry, Microsoft.SqlServer.Types",
            false,
            false
        );
        var sqlGeographyType = Type.GetType(
            "Microsoft.SqlServer.Types.SqlGeography, Microsoft.SqlServer.Types",
            false,
            false
        );
        var sqlHierarchyIdType = Type.GetType(
            "Microsoft.SqlServer.Types.SqlHierarchyId, Microsoft.SqlServer.Types",
            false,
            false
        );

        return new(d =>
        {
            switch (d.BaseTypeName)
            {
                case SqlServerTypes.sql_geometry:
                    if (sqlNetTopologyGeometryType != null)
                    {
                        return new DotnetTypeDescriptor(sqlNetTopologyGeometryType);
                    }

                    if (sqlGeometryType != null)
                    {
                        return new DotnetTypeDescriptor(sqlGeometryType);
                    }

                    // Fallback: WKB (Well-Known Binary) format as byte[]
                    return new DotnetTypeDescriptor(typeof(byte[]));
                case SqlServerTypes.sql_geography:
                    if (sqlGeographyType != null)
                    {
                        return new DotnetTypeDescriptor(sqlGeographyType);
                    }

                    // Fallback: WKB (Well-Known Binary) format as byte[]
                    return new DotnetTypeDescriptor(typeof(byte[]));
                case SqlServerTypes.sql_hierarchyid:
                    if (sqlHierarchyIdType != null)
                    {
                        return new DotnetTypeDescriptor(sqlHierarchyIdType);
                    }

                    // Fallback: Hierarchical path as string (e.g., "/1/2/3/")
                    return new DotnetTypeDescriptor(typeof(string));
            }

            return null;
        });
    }

    #endregion // SqlTypeToDotnetTypeConverters
}
