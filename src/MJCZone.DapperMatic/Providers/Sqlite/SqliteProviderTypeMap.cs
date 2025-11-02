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

namespace MJCZone.DapperMatic.Providers.Sqlite;

/// <summary>
/// Provides SQLite specific database type mapping.
/// </summary>
/// <remarks>
/// See:
/// https://www.sqlite.org/datatype3.html.
/// </remarks>
public sealed class SqliteProviderTypeMap : DbProviderTypeMapBase<SqliteProviderTypeMap>
{
    /// <inheritdoc/>
    protected override IProviderTypeMapping GetProviderTypeMapping()
    {
        return new SqliteTypeMapping();
    }

    /// <inheritdoc/>
    protected override string GetProviderName()
    {
        return "sqlite";
    }

    /// <inheritdoc/>
    protected override void RegisterNetTopologySuiteTypes()
    {
        // NetTopologySuite types map to PostgreSQL/PostGIS geometry types
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

        var lobTypeConverter = TypeMappingHelpers.CreateLobType(SqliteTypes.sql_text, isUnicode: false);

        // SQLite stores all geometry types as TEXT (WKT format)
        RegisterConverterForTypes(
            new DotnetTypeToSqlTypeConverter(d => lobTypeConverter),
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
        var sqlServerTypes = new[]
        {
            Type.GetType("Microsoft.SqlServer.Types.SqlGeometry, Microsoft.SqlServer.Types"),
            Type.GetType("Microsoft.SqlServer.Types.SqlGeography, Microsoft.SqlServer.Types"),
            Type.GetType("Microsoft.SqlServer.Types.SqlHierarchyId, Microsoft.SqlServer.Types"),
        }
            .Where(t => t != null)
            .ToArray();

        // SQLite stores all SQL Server types as TEXT
        var textConverter = new DotnetTypeToSqlTypeConverter(d =>
            TypeMappingHelpers.CreateLobType(SqliteTypes.sql_text, isUnicode: false)
        );

        RegisterConverterForTypes(textConverter, sqlServerTypes!);
    }

    /// <inheritdoc/>
    protected override void RegisterMySqlTypes()
    {
        var sqlMySqlDataGeometryType = Type.GetType("MySql.Data.Types.MySqlGeometry, MySql.Data");
        var sqlMySqlConnectorGeometryType = Type.GetType("MySqlConnector.MySqlGeometry, MySqlConnector");

        // SQLite stores all MySQL geometry types as TEXT
        var mySqlGeometryConverter = new DotnetTypeToSqlTypeConverter(d =>
            TypeMappingHelpers.CreateLobType(SqliteTypes.sql_text, isUnicode: false)
        );

        RegisterConverterForTypes(mySqlGeometryConverter, [sqlMySqlDataGeometryType, sqlMySqlConnectorGeometryType]);
    }

    /// <inheritdoc/>
    protected override void RegisterNpgsqlTypes()
    {
        // SQLite stores most Npgsql types as TEXT
        var textConverter = new DotnetTypeToSqlTypeConverter(d =>
            TypeMappingHelpers.CreateLobType(SqliteTypes.sql_text, isUnicode: false)
        );

        // PostgreSQL geometric and special types → TEXT
        var npgsqlTextTypes = new[]
        {
            Type.GetType("NpgsqlTypes.NpgsqlPoint, Npgsql"),
            Type.GetType("NpgsqlTypes.NpgsqlLSeg, Npgsql"),
            Type.GetType("NpgsqlTypes.NpgsqlPath, Npgsql"),
            Type.GetType("NpgsqlTypes.NpgsqlPolygon, Npgsql"),
            Type.GetType("NpgsqlTypes.NpgsqlLine, Npgsql"),
            Type.GetType("NpgsqlTypes.NpgsqlCircle, Npgsql"),
            Type.GetType("NpgsqlTypes.NpgsqlBox, Npgsql"),
            Type.GetType("NpgsqlTypes.NpgsqlInterval, Npgsql"),
            Type.GetType("NpgsqlTypes.NpgsqlTid, Npgsql"),
            Type.GetType("NpgsqlTypes.NpgsqlTsQuery, Npgsql"),
            Type.GetType("NpgsqlTypes.NpgsqlTsVector, Npgsql"),
        }
            .Where(t => t != null)
            .ToArray();

        RegisterConverterForTypes(textConverter, npgsqlTextTypes!);

        // PostgreSQL network types → VARCHAR with specific lengths
        var npgsqlInetType = Type.GetType("NpgsqlTypes.NpgsqlInet, Npgsql");
        var npgsqlCidrType = Type.GetType("NpgsqlTypes.NpgsqlCidr, Npgsql");

        if (npgsqlInetType != null)
        {
            RegisterConverter(
                npgsqlInetType,
                new DotnetTypeToSqlTypeConverter(d =>
                    TypeMappingHelpers.CreateStringType(SqliteTypes.sql_varchar, 45, isUnicode: false)
                )
            );
        }

        if (npgsqlCidrType != null)
        {
            RegisterConverter(
                npgsqlCidrType,
                new DotnetTypeToSqlTypeConverter(d =>
                    TypeMappingHelpers.CreateStringType(SqliteTypes.sql_varchar, 43, isUnicode: false)
                )
            );
        }

        // PostgreSQL range arrays also map to TEXT
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

            RegisterConverterForTypes(textConverter, rangeArrayTypes);
        }
    }

    /// <inheritdoc/>
    protected override void RegisterSqlTypeToDotnetTypeConverters()
    {
        var booleanConverter = GetBooleanToDotnetTypeConverter();
        var numericConverter = GetNumbericToDotnetTypeConverter();
        var guidConverter = GetGuidToDotnetTypeConverter();
        var textConverter = GetTextToDotnetTypeConverter();
        var dateTimeConverter = GetDateTimeToDotnetTypeConverter();
        var byteArrayConverter = GetByteArrayToDotnetTypeConverter();
        var objectConverter = GetObjectToDotnetTypeConverter();

        // Boolean affinity
        RegisterConverterForTypes(booleanConverter, SqliteTypes.sql_bool, SqliteTypes.sql_boolean);

        // Numeric affinity
        RegisterConverterForTypes(
            numericConverter,
            SqliteTypes.sql_tinyint,
            SqliteTypes.sql_smallint,
            SqliteTypes.sql_int,
            SqliteTypes.sql_integer,
            SqliteTypes.sql_mediumint,
            SqliteTypes.sql_unsigned_big_int,
            SqliteTypes.sql_bigint,
            SqliteTypes.sql_real,
            SqliteTypes.sql_float,
            SqliteTypes.sql_decimal,
            SqliteTypes.sql_numeric,
            SqliteTypes.sql_double,
            SqliteTypes.sql_double_precision,
            SqliteTypes.sql_int2,
            SqliteTypes.sql_int4,
            SqliteTypes.sql_int8
        );

        // Guid affinity
        RegisterConverterForTypes(guidConverter, SqliteTypes.sql_char, SqliteTypes.sql_varchar);

        // Text affinity
        RegisterConverterForTypes(
            textConverter,
            SqliteTypes.sql_nvarchar,
            SqliteTypes.sql_varchar,
            SqliteTypes.sql_varying_character,
            SqliteTypes.sql_native_character,
            SqliteTypes.sql_text,
            SqliteTypes.sql_nchar,
            SqliteTypes.sql_char,
            SqliteTypes.sql_character
        );

        // DateTime affinity
        RegisterConverterForTypes(
            dateTimeConverter,
            SqliteTypes.sql_datetime,
            SqliteTypes.sql_time,
            SqliteTypes.sql_date,
            SqliteTypes.sql_timestamp,
            SqliteTypes.sql_year
        );

        // Binary affinity
        RegisterConverterForTypes(byteArrayConverter, SqliteTypes.sql_blob);

        // Object affinity
        RegisterConverterForTypes(objectConverter, SqliteTypes.sql_clob);
    }

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
                case SqliteTypes.sql_tinyint:
                    return new DotnetTypeDescriptor(typeof(byte));
                case SqliteTypes.sql_smallint:
                case SqliteTypes.sql_int2:
                    return new DotnetTypeDescriptor(typeof(short));
                case SqliteTypes.sql_int:
                case SqliteTypes.sql_int4:
                case SqliteTypes.sql_integer:
                case SqliteTypes.sql_mediumint:
                    return new DotnetTypeDescriptor(typeof(int));
                case SqliteTypes.sql_unsigned_big_int:
                case SqliteTypes.sql_bigint:
                case SqliteTypes.sql_int8:
                    return new DotnetTypeDescriptor(typeof(long));
                case SqliteTypes.sql_real:
                    return new DotnetTypeDescriptor(typeof(double));
                case SqliteTypes.sql_float:
                case SqliteTypes.sql_double:
                case SqliteTypes.sql_double_precision:
                    return new DotnetTypeDescriptor(typeof(double));
                case SqliteTypes.sql_decimal:
                case SqliteTypes.sql_numeric:
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

    private static SqlTypeToDotnetTypeConverter GetDateTimeToDotnetTypeConverter()
    {
        return new(d =>
        {
            switch (d.BaseTypeName)
            {
                case SqliteTypes.sql_datetime:
                case SqliteTypes.sql_timestamp:
                    return new DotnetTypeDescriptor(typeof(DateTime));
                case SqliteTypes.sql_time:
                    return new DotnetTypeDescriptor(typeof(TimeOnly));
                case SqliteTypes.sql_date:
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

    #endregion // SqlTypeToDotnetTypeConverters
}
