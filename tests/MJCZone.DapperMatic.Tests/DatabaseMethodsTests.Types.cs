// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Dapper;
using Docker.DotNet.Models;
using MJCZone.DapperMatic.Models;
using MJCZone.DapperMatic.Providers;
using NpgsqlTypes;

namespace MJCZone.DapperMatic.Tests;

public abstract partial class DatabaseMethodsTests
{
    private static Type[] GetSupportedTypes(IDbProviderTypeMap dbTypeMap)
    {
        Type[] typesToSupport =
        [
            typeof(bool),
            typeof(byte),
            typeof(sbyte),
            typeof(short),
            typeof(int),
            typeof(long),
            typeof(float),
            typeof(double),
            typeof(decimal),
            typeof(char),
            typeof(string),
            typeof(char[]),
            typeof(ReadOnlyMemory<byte>[]),
            typeof(Stream),
            typeof(Guid),
            typeof(DateTime),
            typeof(DateTimeOffset),
            typeof(TimeSpan),
            typeof(DateOnly),
            typeof(TimeOnly),
            typeof(byte[]),
            typeof(object),
            // generic definitions
            typeof(IDictionary<,>),
            typeof(Dictionary<,>),
            typeof(IEnumerable<>),
            typeof(ICollection<>),
            typeof(List<>),
            typeof(object[]),
            // generics
            typeof(IDictionary<string, string>),
            typeof(Dictionary<string, string>),
            typeof(Dictionary<string, object>),
            typeof(Dictionary<string, object>),
            typeof(Dictionary<int, string>),
            typeof(Dictionary<Guid, object>),
            typeof(Dictionary<long, DateTime>),
            typeof(IEnumerable<string>),
            typeof(IEnumerable<Guid>),
            typeof(ICollection<string>),
            typeof(ICollection<Guid>),
            typeof(List<string>),
            typeof(List<decimal>),
            typeof(List<DateTimeOffset>),
            typeof(string[]),
            typeof(Guid[]),
            typeof(int[]),
            typeof(long[]),
            typeof(double[]),
            typeof(decimal[]),
            typeof(TimeSpan[]),
            // custom classes
            typeof(TestClassDao),
        ];

        return typesToSupport;
    }

    public class TestClassDao
    {
        public Guid Id { get; set; }
    }

    [Fact]
    protected virtual async Task Should_expect_provider_type_map_supports_all_desired_dotnet_types_Async()
    {
        using var db = await OpenConnectionAsync();
        var dbTypeMap = db.GetProviderTypeMap();
        foreach (var desiredType in GetSupportedTypes(dbTypeMap))
        {
            var exists = dbTypeMap.TryGetProviderSqlTypeMatchingDotnetType(
                new DotnetTypeDescriptor(desiredType.OrUnderlyingTypeIfNullable()),
                out var sqlType
            );

            Assert.True(exists, "Could not find a SQL type for " + desiredType.FullName);
            Assert.NotNull(sqlType);
        }
    }

    [Fact]
    protected virtual async Task Should_expect_provider_returns_datatypes_Async()
    {
        using var db = await OpenConnectionAsync();
        var databaseMethods = DatabaseMethodsProvider.GetMethods(db);

        var allTypes = databaseMethods.GetAvailableDataTypes(includeAdvanced: true).ToList();
        var commonTypes = databaseMethods.GetAvailableDataTypes(includeAdvanced: false).ToList();

        Assert.NotEmpty(allTypes);
        Assert.NotEmpty(commonTypes);
        Assert.All(commonTypes, type => Assert.True(type.IsCommon));
        Assert.True(commonTypes.Count <= allTypes.Count);

        // Verify basic categories are present
        var categories = allTypes.Select(t => t.Category).Distinct().ToList();
        Assert.Contains(DataTypeCategory.Integer, categories);
        Assert.Contains(DataTypeCategory.Text, categories);
    }

    [Fact]
    protected virtual async Task Should_expect_provider_returns_datatypes_with_metadata_Async()
    {
        using var db = await OpenConnectionAsync();
        var databaseMethods = DatabaseMethodsProvider.GetMethods(db);

        var dataTypes = databaseMethods.GetAvailableDataTypes(includeAdvanced: true).ToList();
        Assert.NotEmpty(dataTypes);

        // Find types with specific characteristics to test metadata
        var stringType = dataTypes.FirstOrDefault(t => t.SupportsLength);
        if (stringType != null)
        {
            Assert.True(stringType.MaxLength > 0);
            Assert.NotNull(stringType.Description);
            Assert.NotEmpty(stringType.Description);
        }

        var decimalType = dataTypes.FirstOrDefault(t => t.SupportsPrecision && t.SupportsScale);
        if (decimalType != null)
        {
            Assert.True(decimalType.MaxPrecision > 0);
            Assert.True(decimalType.MaxScale >= 0);
            Assert.True(decimalType.MaxScale <= decimalType.MaxPrecision);
        }
    }

    [Fact]
    protected virtual async Task Should_expect_provider_returns_custom_datatypes_Async()
    {
        using var db = await OpenConnectionAsync();
        var databaseMethods = DatabaseMethodsProvider.GetMethods(db);

        // This should not throw, even if no custom types are found
        var customTypes = await databaseMethods.DiscoverCustomDataTypesAsync(db);
        Assert.NotNull(customTypes);

        // For most providers, this will return empty unless there are actual custom types
        // PostgreSQL might return user-defined types if any exist
    }

    // EXPLICIT TYPE MAPPING TESTS SO THERE IS NO AMBIGUITY IN EXPECTED TYPES

    // WHEN ADDING COLUMNS in SQLITE, WE ARE ABLE TO PRESERVE THE EXACT TYPE NAMES OFTEN,
    // BECAUSE SQLITE IS DYNAMICALLY TYPED AND DOES NOT ENFORCE TYPES STRICTLY, AND TYPES THAT
    // CONTAIN WORDS LIKE 'CHAR' OR 'INT' ARE ACCEPTED AS VALID TYPES AND MAPPED AUTOMATICALLYTypestext(65535)
    // TO 1 OF THE 5 STORAGE CLASSES (NULL, INTEGER, REAL, TEXT, BLOB). THIS IS A CONVENIENCE
    // WE TAKE ADVANTAGE OF SO THAT WE CAN REVERSE MAP THE TYPES TO PROPER .NET TYPES LATER
    // IF NEEDED.

    // NOTE, the ACTUAL provider type name returned when fetching the column DOES NOT INCLUDE
    // PARENTHESES FOR LENGTH/PRECISION/SCALE, THESE ARE RATHER PROPERTIES OF THE COLUMN METADATA,
    // WE ADD THESE BACK IN THE TESTS BELOW TO COMPARE TO THE EXPECTED TYPE NAMES, and THIS
    // ALSO ALLOWS TO VALIDATE LENGTHS AND PRECISION/SCALE ARE RETURNED PROPERLY, EVEN FOR THINGS
    // LIKE INT WHICH DO NOT TYPICALLY REVEAL LENGTH/PRECISION/SCALE.

    // csharpier-ignore-start
    [Theory]
    // Primitive & Common Types
    [InlineData(typeof(byte), "tinyint", "tinyint", "smallint", "tinyint", false, null, null, null, false, "tinyint(4)", "tinyint(4)")]
    [InlineData(typeof(sbyte), "tinyint", "tinyint", "smallint", "tinyint", false, null, null, null, false, "tinyint(4)", "tinyint(4)")]
    [InlineData(typeof(short), "smallint", "smallint", "smallint", "smallint", false, null, null, null, false, "smallint(6)", "smallint(6)")]
    [InlineData(typeof(int), "int", "int", "integer", "int", false, null, null, null, false, "int(11)", "int(11)")]
    [InlineData(typeof(long), "bigint", "bigint", "bigint", "bigint", false, null, null, null, false, "bigint(20)", "bigint(20)")]
    [InlineData(typeof(float), "real", "double", "real", "real")]
    [InlineData(typeof(double), "float(53)", "float", "double precision", "double")]
    [InlineData(typeof(decimal), "decimal(16,4)", "decimal(16,4)", "numeric(16,4)", "numeric(16,4)")]
    [InlineData(typeof(decimal), "decimal(12,8)", "decimal(12,8)", "numeric(12,8)", "numeric(12,8)", false, null, 12, 8)]
    [InlineData(typeof(decimal), "decimal(12,0)", "decimal(12,0)", "numeric(12,0)", "numeric(12,0)", false, null, 12, 0)]
    [InlineData(typeof(bool), "bit", "tinyint(1)", "boolean", "boolean")]
    [InlineData(typeof(char), "char(1)", "char(1)", "character(1)", "char(1)")]
    [InlineData(typeof(char), "nchar(1)", "char(1)", "character(1)", "nchar(1)", true)]
    [InlineData(typeof(string), "char(234)", "char(234)", "character(234)", "char(234)", false, 234, null, null, true)]
    [InlineData(typeof(string), "nchar(234)", "char(234)", "character(234)", "nchar(234)", true, 234, null, null, true)]
    [InlineData(typeof(string), "varchar(max)", "text", "text", "varchar(-1)")]
    [InlineData(typeof(string), "nvarchar(max)", "text", "text", "nvarchar(-1)", true)]
    [InlineData(typeof(string), "varchar(234)", "varchar(234)", "character varying(234)", "varchar(234)", false, 234)]
    [InlineData(typeof(string), "nvarchar(234)", "varchar(234)", "character varying(234)", "nvarchar(234)", true, 234)]
    [InlineData(typeof(string), "nvarchar(max)", "text", "text", "nvarchar(-1)", true, -1)]
    [InlineData(typeof(string), "nvarchar(max)", "longtext", "text", "nvarchar(-1)", true, int.MaxValue)]
    [InlineData(typeof(Guid), "uniqueidentifier", "char(36)", "uuid", "char(36)")]
    // Date & Time Types
    [InlineData(typeof(DateTime), "datetime", "datetime(6)", "timestamp without time zone", "datetime")]
    [InlineData(typeof(DateTimeOffset), "datetimeoffset", "timestamp(6)", "timestamp with time zone", "datetime")]
    [InlineData(typeof(TimeSpan), "time", "time", "interval", "time")]
    [InlineData(typeof(DateOnly), "date", "date", "date", "date")]
    [InlineData(typeof(TimeOnly), "time", "time", "time without time zone", "time")]
    // Binary Types
    [InlineData(typeof(byte[]), "varbinary(max)", "longblob", "bytea", "blob")]
    [InlineData(typeof(Memory<byte>), "varbinary(max)", "longblob", "bytea", "blob")]
    [InlineData(typeof(ReadOnlyMemory<byte>), "varbinary(max)", "longblob", "bytea", "blob")]
    [InlineData(typeof(Stream), "varbinary(max)", "longblob", "bytea", "blob")]
    [InlineData(typeof(MemoryStream), "varbinary(max)", "longblob", "bytea", "blob")]
    // JSON & Complex Types (MariaDB 10.x maps JSON to LONGTEXT)
    [InlineData(typeof(JsonDocument), "varchar(max)", "json", "jsonb", "text", false, null, null, null, false, null, "longtext")]
    [InlineData(typeof(JsonElement), "varchar(max)", "json", "jsonb", "text", false, null, null, null, false, null, "longtext")]
    [InlineData(typeof(JsonDocument), "nvarchar(max)", "json", "jsonb", "text", true, null, null, null, false, null, "longtext")]
    [InlineData(typeof(JsonElement), "nvarchar(max)", "json", "jsonb", "text", true, null, null, null, false, null, "longtext")]
    [InlineData(typeof(System.Text.Json.Nodes.JsonArray), "nvarchar(max)", "json", "jsonb", "text", true, null, null, null, false, null, "longtext")]
    [InlineData(typeof(System.Text.Json.Nodes.JsonObject), "nvarchar(max)", "json", "jsonb", "text", true, null, null, null, false, null, "longtext")]
    [InlineData(typeof(System.Text.Json.Nodes.JsonValue), "nvarchar(max)", "json", "jsonb", "text", true, null, null, null, false, null, "longtext")]
    [InlineData(typeof(object), "varchar(max)", "json", "jsonb", "text", false, null, null, null, false, null, "longtext")]
    [InlineData(typeof(object), "nvarchar(max)", "json", "jsonb", "text", true, null, null, null, false, null, "longtext")]
    [InlineData(typeof(DayOfWeek), "int", "int", "integer", "int", false, null, null, null, false, "int(11)", "int(11)")] // Enum example - stored as underlying integer type (int32)
    // Array Types (PostgreSQL native, others JSON/TEXT, MariaDB 10.x maps JSON to LONGTEXT)
    [InlineData(typeof(string[]), "varchar(max)", "json", "text[]", "text", false, null, null, null, false, null, "longtext")]
    [InlineData(typeof(int[]), "varchar(max)", "json", "integer[]", "text", false, null, null, null, false, null, "longtext")]
    [InlineData(typeof(long[]), "varchar(max)", "json", "bigint[]", "text", false, null, null, null, false, null, "longtext")]
    [InlineData(typeof(Guid[]), "varchar(max)", "json", "uuid[]", "text", false, null, null, null, false, null, "longtext")]
    [InlineData(typeof(char[]), "varchar(max)", "text", "text", "varchar(-1)")]
    [InlineData(typeof(char[]), "nvarchar(max)", "text", "text", "nvarchar(-1)", true)]
    [InlineData(typeof(char[]), "varchar(max)", "text", "text", "varchar(-1)", false, -1)]
    [InlineData(typeof(char[]), "nvarchar(max)", "text", "text", "nvarchar(-1)", true, -1)]
    // Collection Types (all serialized as JSON, MariaDB 10.x maps JSON to LONGTEXT)
    [InlineData(typeof(List<string>), "varchar(max)", "json", "jsonb", "text", false, null, null, null, false, null, "longtext")]
    [InlineData(typeof(List<string>), "nvarchar(max)", "json", "jsonb", "text", true, null, null, null, false, null, "longtext")]
    [InlineData(typeof(IList<string>), "varchar(max)", "json", "jsonb", "text", false, null, null, null, false, null, "longtext")]
    [InlineData(typeof(ICollection<string>), "varchar(max)", "json", "jsonb", "text", false, null, null, null, false, null, "longtext")]
    [InlineData(typeof(IEnumerable<string>), "varchar(max)", "json", "jsonb", "text", false, null, null, null, false, null, "longtext")]
    [InlineData(typeof(Dictionary<string, string>), "varchar(max)", "json", "hstore", "text", false, null, null, null, false, null, "longtext")]
    [InlineData(typeof(IDictionary<string, string>), "varchar(max)", "json", "hstore", "text", false, null, null, null, false, null, "longtext")]
    [InlineData(typeof(Dictionary<string, string>), "nvarchar(max)", "json", "hstore", "text", true, null, null, null, false, null, "longtext")]
    [InlineData(typeof(IDictionary<string, string>), "nvarchar(max)", "json", "hstore", "text", true, null, null, null, false, null, "longtext")]
    // Npgsql Types
    // The specific WKT/WKB conversion logic needs to be handled in the provider-specific code
    // SQL Server, MySQL, and SQLite use text-based storage for Npgsql geometric types
    [InlineData(typeof(NpgsqlPoint), "varchar(max)", "text", "point", "varchar(-1)", false, null, null, null, false, null, "text")]
    [InlineData(typeof(NpgsqlLSeg), "varchar(max)", "text", "lseg", "varchar(-1)", false, null, null, null, false, null, "text")]
    [InlineData(typeof(NpgsqlPath), "varchar(max)", "text", "path", "varchar(-1)", false, null, null, null, false, null, "text")]
    [InlineData(typeof(NpgsqlPolygon), "varchar(max)", "text", "polygon", "varchar(-1)", false, null, null, null, false, null, "text")]
    [InlineData(typeof(NpgsqlLine), "varchar(max)", "text", "line", "varchar(-1)", false, null, null, null, false, null, "text")]
    [InlineData(typeof(NpgsqlCircle), "varchar(max)", "text", "circle", "varchar(-1)", false, null, null, null, false, null, "text")]
    [InlineData(typeof(NpgsqlBox), "varchar(max)", "text", "box", "varchar(-1)", false, null, null, null, false, null, "text")]
    [InlineData(typeof(NpgsqlInet), "varchar(45)", "varchar(45)", "inet", "varchar(45)", false, 45, null, null, false, null, "varchar(45)")] // Use standard VARCHAR for IPs
    [InlineData(typeof(NpgsqlCidr), "varchar(43)", "varchar(43)", "cidr", "varchar(43)", false, 43, null, null, false, null, "varchar(43)")] // Use standard VARCHAR for CIDRs
    [InlineData(typeof(NpgsqlRange<DateOnly>), "varchar(max)", "text", "daterange", "varchar(-1)")]
    [InlineData(typeof(NpgsqlRange<int>), "varchar(max)", "text", "int4range", "varchar(-1)")]
    [InlineData(typeof(NpgsqlRange<long>), "varchar(max)", "text", "int8range", "varchar(-1)")]
    [InlineData(typeof(NpgsqlRange<decimal>), "varchar(max)", "text", "numrange", "varchar(-1)")]
    [InlineData(typeof(NpgsqlRange<DateTime>), "varchar(max)", "text", "tsrange", "varchar(-1)")]
    [InlineData(typeof(NpgsqlRange<DateTimeOffset>), "varchar(max)", "text", "tstzrange", "varchar(-1)")]
    [InlineData(typeof(NpgsqlRange<DateOnly>[]), "varchar(max)", "text", "datemultirange", "varchar(-1)")]
    [InlineData(typeof(NpgsqlRange<int>[]), "varchar(max)", "text", "int4multirange", "varchar(-1)")]
    [InlineData(typeof(NpgsqlRange<long>[]), "varchar(max)", "text", "int8multirange", "varchar(-1)")]
    [InlineData(typeof(NpgsqlRange<decimal>[]), "varchar(max)", "text", "nummultirange", "varchar(-1)")]
    [InlineData(typeof(NpgsqlRange<DateTime>[]), "varchar(max)", "text", "tsmultirange", "varchar(-1)")]
    [InlineData(typeof(NpgsqlRange<DateTimeOffset>[]), "varchar(max)", "text", "tstzmultirange", "varchar(-1)")]
    [InlineData(typeof(NpgsqlInterval), "varchar(max)", "text", "interval", "varchar(-1)")]
    [InlineData(typeof(NpgsqlTid), "varchar(max)", "text", "tid", "varchar(-1)")]
    [InlineData(typeof(NpgsqlTsQuery), "varchar(max)", "text", "tsquery", "varchar(-1)")]
    [InlineData(typeof(NpgsqlTsVector), "varchar(max)", "text", "tsvector", "varchar(-1)")]
    // NetTopologySuite Geometry Types
    // SQL Server uses VARCHAR(MAX) for NTS types (WKT text format, no native SqlGeometry support)
    [InlineData(typeof(NetTopologySuite.Geometries.Geometry), "varchar(max)", "geometry", "geometry", "varchar(-1)")]
    [InlineData(typeof(NetTopologySuite.Geometries.GeometryCollection), "varchar(max)", "geomcollection", "geometry(GeometryCollection)", "varchar(-1)", false, null, null, null, false, "geometrycollection", "geometrycollection")]
    [InlineData(typeof(NetTopologySuite.Geometries.Point), "varchar(max)", "point", "geometry(Point)", "varchar(-1)")]
    [InlineData(typeof(NetTopologySuite.Geometries.LineString), "varchar(max)", "linestring", "geometry(LineString)", "varchar(-1)")]
    [InlineData(typeof(NetTopologySuite.Geometries.Polygon), "varchar(max)", "polygon", "geometry(Polygon)", "varchar(-1)")]
    [InlineData(typeof(NetTopologySuite.Geometries.MultiPoint), "varchar(max)", "multipoint", "geometry(MultiPoint)", "varchar(-1)")]
    [InlineData(typeof(NetTopologySuite.Geometries.MultiLineString), "varchar(max)", "multilinestring", "geometry(MultiLineString)", "varchar(-1)")]
    [InlineData(typeof(NetTopologySuite.Geometries.MultiPolygon), "varchar(max)", "multipolygon", "geometry(MultiPolygon)", "varchar(-1)")]
    // Network Types
    [InlineData(typeof(IPAddress), "varchar(17)", "varchar(17)", "inet", "varchar(17)", false, 17)]
    [InlineData(typeof(PhysicalAddress), "varchar(43)", "varchar(43)", "macaddr", "varchar(43)", false, 43)]
    // MySqlConnector Spatial Types
    // SQL Server uses VARCHAR(MAX) for MySQL geometric types (no native SqlGeometry support)
    [InlineData(typeof(MySql.Data.Types.MySqlGeometry), "varchar(max)", "geometry", "text", "varchar(-1)", false, null, null, null, false, null, "geometry")]
    [InlineData(typeof(MySqlConnector.MySqlGeometry), "varchar(max)", "geometry", "text", "varchar(-1)", false, null, null, null, false, null, "geometry")]
    // csharpier-ignore-end
    protected virtual async Task Should_map_dotnet_types_to_expected_provider_data_types(
        Type type,
        string sqlServerTypeName,
        string mySqlTypeName,
        string postgreSqlTypeName,
        string sqliteTypeName,
        bool isUnicode = false,
        int? length = null,
        int? precision = null,
        int? scale = null,
        bool isFixedLength = false,
        string? mySql5TypeName = null,
        string? mariaDb10TypeName = null
    )
    {
        using var db = await OpenConnectionAsync();
        var dbType = db.GetDbProviderType();

        if (await PostGisRequiredAndNotInstalled(type, dbType, db)) return;

        // Create table with a column of that type
        const string tableName = "testTableWithSpecificType";
        const string columnName = "testColumnWithSpecificType";
        await db.DropTableIfExistsAsync(null, tableName);
        var tableCreated = await db.CreateTableIfNotExistsAsync(
            null,
            tableName,
            [
                new DmColumn(null, tableName, "id", typeof(int), isPrimaryKey: true, isAutoIncrement: true),
                new DmColumn(
                    null,
                    tableName,
                    columnName,
                    type,
                    isUnicode: isUnicode,
                    length: length,
                    precision: precision,
                    scale: scale
                )
                {
                    IsFixedLength = isFixedLength,
                },
            ]
        );

        // Fetch the column
        var column = await db.GetColumnAsync(null, tableName, columnName);
        // Clean up before we validate the column
        await db.DropTableIfExistsAsync(null, tableName);
        Assert.NotNull(column);

        var providerTypeName = column?.GetProviderDataType(dbType);
        Assert.NotNull(providerTypeName);
        Assert.NotEmpty(providerTypeName);

        var databaseMethods = DatabaseMethodsProvider.GetMethods(db);
        var databaseProviderDataTypes = databaseMethods.GetAvailableDataTypes(includeAdvanced: true).ToList();

        var dbVersion = await databaseMethods.GetDatabaseVersionAsync(db);
        var isMySql5 = dbType == DbProviderType.MySql && dbVersion.Major == 5;
        var isMariaDb = dbType == DbProviderType.MySql && dbVersion.Major >= 10;
        string expectedTypeName = dbType switch
        {
            DbProviderType.SqlServer => sqlServerTypeName,
            DbProviderType.MySql => isMySql5
                ? (mySql5TypeName ?? mySqlTypeName)
                : isMariaDb
                    ? (mariaDb10TypeName ?? mySqlTypeName)
                    : mySqlTypeName,
            DbProviderType.PostgreSql => postgreSqlTypeName,
            DbProviderType.Sqlite => sqliteTypeName,
            _ => throw new NotSupportedException($"Database type {dbType} is not supported."),
        };

        // var providerTypeNameWithDetails = column?.GetProviderDataType(dbType, true);
        // Assert.NotNull(providerTypeNameWithDetails);
        // Assert.NotEmpty(providerTypeNameWithDetails);

        // In MariaDB it's GEOMETRYCOLLECTIOn, in MySQL it's GEOMCOLLECTION except MySQL 5.7, so we want to accept both the alias
        // and the main name
        // List<string> possibleExpectedTypeNames =  [providerTypeNameWithDetails];
        // var alternativeProviderTypeName = providerTypeName;

        // // Extract base type name by removing parentheses and everything after them
        // // e.g., "VARCHAR(255)" -> "VARCHAR", "DECIMAL(10,2)" -> "DECIMAL"
        // var baseTypeName = providerTypeName.Contains('(')
        //     ? providerTypeName.Substring(0, providerTypeName.IndexOf('('))
        //     : providerTypeName;

        // var providerDataType = databaseProviderDataTypes.FirstOrDefault(dt =>
        //     string.Equals(dt.DataType, baseTypeName, StringComparison.OrdinalIgnoreCase)
        // );
        // if (providerDataType == null)
        // {
        //     // The type could be an alias
        //     providerDataType = databaseProviderDataTypes.FirstOrDefault(dt =>
        //         (dt.Aliases ?? []).Any(alias =>
        //             string.Equals(alias, baseTypeName, StringComparison.OrdinalIgnoreCase)
        //         )
        //     );
        // }
        // Assert.NotNull(providerDataType);
        // var providerDataTypeAliases = providerDataType.Aliases ?? [];
        // Output.WriteLine(
        //     "Column '{0}' provider type '{1}' has {2} aliases: {3}",
        //     columnName,
        //     providerTypeName,
        //     providerDataTypeAliases.Count,
        //     string.Join(", ", providerDataTypeAliases)
        // );

        // possibleExpectedTypeNames.AddIfNotExists(providerTypeNameWithDetails.Replace(baseTypeName, providerDataType.DataType));

        base.Output.WriteLine(
            "Column '{0}' mapped to provider type '{1}' (expected '{2}')",
            columnName,
            providerTypeName,
            expectedTypeName
        );

        Assert.Equivalent(expectedTypeName, providerTypeName);

// #pragma warning disable CA1869 // Cache and reuse 'JsonSerializerOptions' instances
//         var inputJson = JsonSerializer.Serialize(new
//         {
//             sqlServerTypeName,
//             mySqlTypeName,
//             postgreSqlTypeName,
//             sqliteTypeName,
//             isUnicode,
//             length,
//             precision,
//             scale,
//             isFixedLength
//         }, new JsonSerializerOptions { WriteIndented = true });
// #pragma warning restore CA1869 // Cache and reuse 'JsonSerializerOptions' instances
//         Assert.True(
//             possibleExpectedTypeNames.Any(name => string.Equals(name, expectedTypeName, StringComparison.OrdinalIgnoreCase)),
//             $"Expected type '{expectedTypeName}' not found in possible types: {string.Join(", ", possibleExpectedTypeNames)}. Column '{columnName}' mapped to provider type '{providerTypeName}'" +
//             // Add function parameters for more detail
//             $"\n({inputJson})"
//         );
    }

    private async Task<bool> PostGisRequiredAndNotInstalled(Type type, DbProviderType dbType, System.Data.IDbConnection db)
    {
        var requiresPostGis = type.Namespace!.StartsWith("NetTopologySuite.Geometries", StringComparison.OrdinalIgnoreCase);
        bool isPostGisInstalled = false;
        if (dbType == DbProviderType.PostgreSql)
        {
            await db.ExecuteAsync("CREATE EXTENSION IF NOT EXISTS hstore;");
            await db.ExecuteAsync("CREATE EXTENSION IF NOT EXISTS ltree;");
            await db.ExecuteAsync("CREATE EXTENSION IF NOT EXISTS \"uuid-ossp\";");

            isPostGisInstalled = await db.QueryFirstOrDefaultAsync<bool>(
                "SELECT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'postgis');"
            );
        }
        if (dbType == DbProviderType.PostgreSql && requiresPostGis && !isPostGisInstalled)
        {
            // Skip the test if PostGIS is required but not installed
            return true;
        }
        return false;
    }

    [Fact]
    protected virtual async Task Should_map_provider_data_types_to_expected_dotnet_types()
    {
        using var db = await OpenConnectionAsync();
        var dbType = db.GetDbProviderType();
        var dbVersion = await db.GetDatabaseVersionAsync();

        string[] postgisTypes = [
            "geometry",
            "geography",
            "box2d",
            "box3d",
            "circle",
            "line",
            "lseg",
            "path",
            "point",
            "polygon",
        ];
        var hasPostgisExtension = false;
        if (dbType == DbProviderType.PostgreSql)
        {
            // Add required extension for HSTORE support
            await db.ExecuteAsync("CREATE EXTENSION IF NOT EXISTS hstore;");

            // Add required extension for LTREE support
            await db.ExecuteAsync("CREATE EXTENSION IF NOT EXISTS ltree;");

            // Add required extension for UUID support
            await db.ExecuteAsync("CREATE EXTENSION IF NOT EXISTS \"uuid-ossp\";");

            // Add required extension for postgis support
            hasPostgisExtension = await db.QueryFirstOrDefaultAsync<bool>(
                "SELECT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'postgis');"
            );
        }

        var databaseMethods = DatabaseMethodsProvider.GetMethods(db);
        var providerDataTypes = databaseMethods.GetAvailableDataTypes(includeAdvanced: true).ToList();

        // We do this so that we can review the results easily, and so that everything is explicitly documented

        foreach (var providerDataType in providerDataTypes)
        {
            // Print out all provider data types for debugging, along with aliases
            string[] aliases = providerDataType.Aliases != null
                ? [.. providerDataType.Aliases]
                : [];

            string[] providerDataTypeNames = [.. aliases, providerDataType.DataType];

            foreach (var providerDataTypeName in providerDataTypeNames)
            {
                if (IgnoreSqlType(providerDataTypeName)) continue;

                // Create a table with this type to verify it can be created, and return the column definition as DapperMatic sees it
                DmColumn? column = await CreateTableWithProviderDataTypeAndGetColumnAsync(db, dbType, providerDataTypeName, providerDataType, hasPostgisExtension, postgisTypes);
                // Some types can't be created directly, like POSTGIS geometry types without the postgis extension installed, so we skip those
                if (column == null) continue;

                void AssertValues(string pType, DataTypeCategory pTypeCategory, Type dotnetType, int? length = null, int? precision = null, int? scale = null, bool unicode = false, bool fixedLength = false, bool autoIncrement = false)
                {
                    // Print out the parameters for debugging
                    base.Output.WriteLine(
                        "Asserting provider type '{0}' maps to .NET type '{1}' (Category: {2}, Length: {3}, Precision: {4}, Scale: {5}, IsUnicode: {6}, IsFixedLength: {7}, IsAutoIncrement: {8})",
                        pType,
                        dotnetType.FullName,
                        pTypeCategory,
                        length.HasValue ? length.Value.ToString() : "null",
                        precision.HasValue ? precision.Value.ToString() : "null",
                        scale.HasValue ? scale.Value.ToString() : "null",
                        unicode,
                        fixedLength,
                        autoIncrement
                    );
                    try
                    {
                        Assert.NotEmpty(pType);
                        Assert.Equal(dotnetType, column.DotnetType);
                        if (length != null) Assert.Equal(length, column.Length);
                        if (precision != null) Assert.Equal(precision, column.Precision);
                        if (scale != null) Assert.Equal(scale, column.Scale);
                        Assert.Equal(unicode, column.IsUnicode);
                        Assert.Equal(fixedLength, column.IsFixedLength);
                        Assert.Equal(autoIncrement, column.IsAutoIncrement);
                        base.Output.WriteLine("Assertion passed.");
                    }
                    catch (Exception ex)
                    {
                        base.Output.WriteLine("Exception during assertion: " + ex.ToString());
                        throw;
                    }
                }

                if (dbType == DbProviderType.MySql)
                {
                    switch (providerDataTypeName.ToLowerInvariant())
                    {
                        case "bigint": AssertValues(providerDataTypeName, DataTypeCategory.Integer, typeof(long)); break;
                        case "binary": AssertValues(providerDataTypeName, DataTypeCategory.Binary, typeof(byte[]), 1); break;
                        case "bit": AssertValues(providerDataTypeName, DataTypeCategory.Text, typeof(bool)); break;
                        case "blob": AssertValues(providerDataTypeName, DataTypeCategory.Binary, typeof(byte[]), unicode: true); break;
                        case "boolean": AssertValues(providerDataTypeName, DataTypeCategory.Boolean, typeof(bool)); break;
                        case "char": AssertValues(providerDataTypeName, DataTypeCategory.Text, typeof(string), 1, unicode: true); break;
                        case "date": AssertValues(providerDataTypeName, DataTypeCategory.DateTime, typeof(DateOnly)); break;
                        case "datetime": AssertValues(providerDataTypeName, DataTypeCategory.DateTime, typeof(DateTime)); break;
                        case "decimal": AssertValues(providerDataTypeName, DataTypeCategory.Decimal, typeof(decimal), null, 10, 2); break;
                        case "double": AssertValues(providerDataTypeName, DataTypeCategory.Decimal, typeof(double)); break;
                        case "enum": AssertValues(providerDataTypeName, DataTypeCategory.Other, typeof(string), unicode: true); break;
                        case "float": AssertValues(providerDataTypeName, DataTypeCategory.Decimal, typeof(float)); break;
                        case "geometry": AssertValues(providerDataTypeName, DataTypeCategory.Spatial, typeof(NetTopologySuite.Geometries.Geometry)); break;
                        case "geometrycollection": AssertValues(providerDataTypeName, DataTypeCategory.Spatial, typeof(NetTopologySuite.Geometries.GeometryCollection)); break;
                        case "int": AssertValues(providerDataTypeName, DataTypeCategory.Integer, typeof(int)); break;
                        case "json":
                            if (dbVersion.Major < 10)
                                // MySQL
                                AssertValues(providerDataTypeName, DataTypeCategory.Json, typeof(JsonDocument));
                            else
                                // MariaDB
                                AssertValues(providerDataTypeName, DataTypeCategory.Json, typeof(string), unicode: true);
                            break;
                        case "linestring": AssertValues(providerDataTypeName, DataTypeCategory.Spatial, typeof(NetTopologySuite.Geometries.LineString)); break;
                        case "longblob": AssertValues(providerDataTypeName, DataTypeCategory.Binary, typeof(byte[]), unicode: true); break;
                        case "longtext": AssertValues(providerDataTypeName, DataTypeCategory.Text, typeof(string), unicode: true); break;
                        case "mediumblob": AssertValues(providerDataTypeName, DataTypeCategory.Binary, typeof(byte[]), unicode: true); break;
                        case "mediumint": AssertValues(providerDataTypeName, DataTypeCategory.Integer, typeof(int)); break;
                        case "mediumtext": AssertValues(providerDataTypeName, DataTypeCategory.Text, typeof(string), unicode: true); break;
                        case "multilinestring": AssertValues(providerDataTypeName, DataTypeCategory.Spatial, typeof(NetTopologySuite.Geometries.MultiLineString)); break;
                        case "multipoint": AssertValues(providerDataTypeName, DataTypeCategory.Spatial, typeof(NetTopologySuite.Geometries.MultiPoint)); break;
                        case "multipolygon": AssertValues(providerDataTypeName, DataTypeCategory.Spatial, typeof(NetTopologySuite.Geometries.MultiPolygon)); break;
                        case "point": AssertValues(providerDataTypeName, DataTypeCategory.Spatial, typeof(NetTopologySuite.Geometries.Point)); break;
                        case "polygon": AssertValues(providerDataTypeName, DataTypeCategory.Spatial, typeof(NetTopologySuite.Geometries.Polygon)); break;
                        case "set": AssertValues(providerDataTypeName, DataTypeCategory.Other, typeof(string), unicode: true); break;
                        case "smallint": AssertValues(providerDataTypeName, DataTypeCategory.Integer, typeof(short)); break;
                        case "text": AssertValues(providerDataTypeName, DataTypeCategory.Text, typeof(string), unicode: true); break;
                        case "time": AssertValues(providerDataTypeName, DataTypeCategory.DateTime, typeof(TimeOnly)); break;
                        case "timestamp": AssertValues(providerDataTypeName, DataTypeCategory.DateTime, typeof(DateTimeOffset)); break;
                        case "tinyblob": AssertValues(providerDataTypeName, DataTypeCategory.Binary, typeof(byte[]), unicode: true); break;
                        case "tinyint": AssertValues(providerDataTypeName, DataTypeCategory.Integer, typeof(sbyte)); break;
                        case "tinytext": AssertValues(providerDataTypeName, DataTypeCategory.Text, typeof(string), unicode: true); break;
                        case "varbinary": AssertValues(providerDataTypeName, DataTypeCategory.Binary, typeof(byte[]), 255, unicode: true); break;
                        case "varchar": AssertValues(providerDataTypeName, DataTypeCategory.Text, typeof(string), 255, unicode: true); break;
                        case "year": AssertValues(providerDataTypeName, DataTypeCategory.DateTime, typeof(int)); break;
                    }
                }
                else if (dbType == DbProviderType.PostgreSql)
                {
                    switch (providerDataTypeName.ToLowerInvariant())
                    {
                        case "_bool": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(bool[])); break;
                        case "_bpchar": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(char[])); break;
                        case "_bytea": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(byte[][])); break;
                        case "_char": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(string[])); break;
                        case "_date": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(DateOnly[])); break;
                        case "_float4": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(float[])); break;
                        case "_float8": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(double[])); break;
                        case "_int2": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(short[])); break;
                        case "_int4": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(int[])); break;
                        case "_int8": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(long[])); break;
                        case "_interval": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(TimeSpan[])); break;
                        case "_json": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(JsonDocument[])); break;
                        case "_jsonb": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(JsonDocument[])); break;
                        case "_numeric": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(decimal[])); break;
                        case "_text": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(string[])); break;
                        case "_time": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(TimeOnly[])); break;
                        case "_timestamp": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(DateTime[])); break;
                        case "_timestamptz": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(DateTimeOffset[])); break;
                        case "_timetz": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(TimeOnly[])); break;
                        case "_uuid": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(Guid[])); break;
                        case "_varchar": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(string[])); break;
                        case "bigint": AssertValues(providerDataTypeName, DataTypeCategory.Integer, typeof(long)); break;
                        case "bigint[]": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(long[])); break;
                        case "bigserial": AssertValues(providerDataTypeName, DataTypeCategory.Integer, typeof(long)); break;
                        case "bit varying": AssertValues(providerDataTypeName, DataTypeCategory.Text, typeof(string), unicode: true); break;
                        case "bit": AssertValues(providerDataTypeName, DataTypeCategory.Text, typeof(string), unicode: true); break;
                        case "boolean": AssertValues(providerDataTypeName, DataTypeCategory.Boolean, typeof(bool)); break;
                        case "boolean[]": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(bool[])); break;
                        // will return NetTopologySuite.Geometries.Polygon when PostGIS is installed
                        case "box": AssertValues(providerDataTypeName, DataTypeCategory.Spatial, typeof(NetTopologySuite.Geometries.Polygon)); break;
                        // case "box": AssertValues(providerDataTypeName, DataTypeCategory.Spatial, typeof(NpgsqlBox)); break;
                        case "bytea": AssertValues(providerDataTypeName, DataTypeCategory.Binary, typeof(byte[])); break;
                        case "bytea[]": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(byte[][])); break;
                        case "char": AssertValues(providerDataTypeName, DataTypeCategory.Text, typeof(string), unicode: true); break;
                        case "character varying": AssertValues(providerDataTypeName, DataTypeCategory.Text, typeof(string), 255, unicode: true); break;
                        case "character varying[]": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(string[])); break;
                        case "character": AssertValues(providerDataTypeName, DataTypeCategory.Text, typeof(string), 1, unicode: true); break;
                        case "character[]": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(char[])); break;
                        case "cidr": AssertValues(providerDataTypeName, DataTypeCategory.Network, typeof(NpgsqlCidr)); break;
                        case "circle": AssertValues(providerDataTypeName, DataTypeCategory.Spatial, typeof(NetTopologySuite.Geometries.Geometry)); break;
                        // case "circle": AssertValues(providerDataTypeName, DataTypeCategory.Spatial, typeof(NpgsqlCircle)); break;
                        case "date": AssertValues(providerDataTypeName, DataTypeCategory.DateTime, typeof(DateOnly)); break;
                        case "date[]": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(DateOnly[])); break;
                        case "daterange": AssertValues(providerDataTypeName, DataTypeCategory.Range, typeof(NpgsqlRange<DateOnly>)); break;
                        case "double precision": AssertValues(providerDataTypeName, DataTypeCategory.Decimal, typeof(double)); break;
                        case "double precision[]": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(double[])); break;
                        case "float4": AssertValues(providerDataTypeName, DataTypeCategory.Decimal, typeof(float)); break;
                        case "float8": AssertValues(providerDataTypeName, DataTypeCategory.Decimal, typeof(double)); break;
                        case "float4[]": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(float[])); break;
                        case "float8[]": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(double[])); break;
                        case "geography": AssertValues(providerDataTypeName, DataTypeCategory.Spatial, typeof(NetTopologySuite.Geometries.Geometry)); break;
                        case "geometry": AssertValues(providerDataTypeName, DataTypeCategory.Spatial, typeof(NetTopologySuite.Geometries.Geometry)); break;
                        case "hstore": AssertValues(providerDataTypeName, DataTypeCategory.Other, typeof(Dictionary<string, string>)); break;
                        case "inet": AssertValues(providerDataTypeName, DataTypeCategory.Network, typeof(IPAddress)); break;
                        case "int4range": AssertValues(providerDataTypeName, DataTypeCategory.Range, typeof(NpgsqlRange<int>)); break;
                        case "int8range": AssertValues(providerDataTypeName, DataTypeCategory.Range, typeof(NpgsqlRange<long>)); break;
                        case "integer": AssertValues(providerDataTypeName, DataTypeCategory.Integer, typeof(int)); break;
                        case "integer[]": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(int[])); break;
                        case "interval": AssertValues(providerDataTypeName, DataTypeCategory.DateTime, typeof(TimeSpan)); break;
                        case "interval[]": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(TimeSpan[])); break;
                        case "json": AssertValues(providerDataTypeName, DataTypeCategory.Json, typeof(JsonDocument)); break;
                        case "json[]": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(JsonDocument[])); break;
                        case "jsonb": AssertValues(providerDataTypeName, DataTypeCategory.Json, typeof(JsonDocument)); break;
                        case "jsonb[]": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(JsonDocument[])); break;
                        case "line": AssertValues(providerDataTypeName, DataTypeCategory.Spatial, typeof(NetTopologySuite.Geometries.LineString)); break;
                        // case "line": AssertValues(providerDataTypeName, DataTypeCategory.Spatial, typeof(NpgsqlLine)); break;
                        case "lseg": AssertValues(providerDataTypeName, DataTypeCategory.Spatial, typeof(NetTopologySuite.Geometries.LineString)); break;
                        // case "lseg": AssertValues(providerDataTypeName, DataTypeCategory.Spatial, typeof(NpgsqlLSeg)); break;
                        case "ltree": AssertValues(providerDataTypeName, DataTypeCategory.Other, typeof(string)); break;
                        case "macaddr": AssertValues(providerDataTypeName, DataTypeCategory.Network, typeof(PhysicalAddress)); break;
                        case "macaddr8": AssertValues(providerDataTypeName, DataTypeCategory.Network, typeof(PhysicalAddress)); break;
                        case "money": AssertValues(providerDataTypeName, DataTypeCategory.Money, typeof(decimal)); break;
                        case "numeric": AssertValues(providerDataTypeName, DataTypeCategory.Decimal, typeof(decimal), null, 18, 2); break;
                        case "numeric[]": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(decimal[])); break;
                        case "numrange": AssertValues(providerDataTypeName, DataTypeCategory.Range, typeof(NpgsqlRange<decimal>)); break;
                        case "oid": AssertValues(providerDataTypeName, DataTypeCategory.Other, typeof(uint)); break;
                        case "path": AssertValues(providerDataTypeName, DataTypeCategory.Spatial, typeof(NetTopologySuite.Geometries.LineString)); break;
                        // case "path": AssertValues(providerDataTypeName, DataTypeCategory.Spatial, typeof(NpgsqlPath)); break;
                        case "point": AssertValues(providerDataTypeName, DataTypeCategory.Spatial, typeof(NetTopologySuite.Geometries.Point)); break;
                        // case "point": AssertValues(providerDataTypeName, DataTypeCategory.Spatial, typeof(NpgsqlPoint)); break;
                        case "polygon": AssertValues(providerDataTypeName, DataTypeCategory.Spatial, typeof(NetTopologySuite.Geometries.Polygon)); break;
                        // case "polygon": AssertValues(providerDataTypeName, DataTypeCategory.Spatial, typeof(NpgsqlPolygon)); break;
                        case "real": AssertValues(providerDataTypeName, DataTypeCategory.Decimal, typeof(float)); break;
                        case "real[]": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(float[])); break;
                        case "regclass": AssertValues(providerDataTypeName, DataTypeCategory.Other, typeof(uint)); break;
                        case "regconfig": AssertValues(providerDataTypeName, DataTypeCategory.Other, typeof(uint)); break;
                        case "regdictionary": AssertValues(providerDataTypeName, DataTypeCategory.Other, typeof(uint)); break;
                        case "regoper": AssertValues(providerDataTypeName, DataTypeCategory.Other, typeof(uint)); break;
                        case "regoperator": AssertValues(providerDataTypeName, DataTypeCategory.Other, typeof(uint)); break;
                        case "regproc": AssertValues(providerDataTypeName, DataTypeCategory.Other, typeof(uint)); break;
                        case "regprocedure": AssertValues(providerDataTypeName, DataTypeCategory.Other, typeof(uint)); break;
                        case "regtype": AssertValues(providerDataTypeName, DataTypeCategory.Other, typeof(uint)); break;
                        case "serial": AssertValues(providerDataTypeName, DataTypeCategory.Integer, typeof(int)); break;
                        case "smallint": AssertValues(providerDataTypeName, DataTypeCategory.Integer, typeof(short)); break;
                        case "smallint[]": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(short[])); break;
                        case "smallserial": AssertValues(providerDataTypeName, DataTypeCategory.Integer, typeof(short)); break;
                        case "text": AssertValues(providerDataTypeName, DataTypeCategory.Text, typeof(string), unicode: true); break;
                        case "text[]": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(string[])); break;
                        case "time with time zone": AssertValues(providerDataTypeName, DataTypeCategory.DateTime, typeof(TimeOnly)); break;
                        case "time with time zone[]": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(TimeOnly[])); break;
                        case "time without time zone[]": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(TimeOnly[])); break;
                        case "time": AssertValues(providerDataTypeName, DataTypeCategory.DateTime, typeof(TimeOnly)); break;
                        case "time[]": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(TimeOnly[])); break;
                        case "timestamp with time zone": AssertValues(providerDataTypeName, DataTypeCategory.DateTime, typeof(DateTimeOffset)); break;
                        case "timestamp with time zone[]": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(DateTimeOffset[])); break;
                        case "timestamp without time zone[]": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(DateTime[])); break;
                        case "timestamptz": AssertValues(providerDataTypeName, DataTypeCategory.DateTime, typeof(DateTimeOffset)); break;
                        case "timestamptz[]": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(DateTimeOffset[])); break;
                        case "timestamp": AssertValues(providerDataTypeName, DataTypeCategory.DateTime, typeof(DateTime)); break;
                        case "timestamp[]": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(DateTime[])); break;
                        case "tsquery": AssertValues(providerDataTypeName, DataTypeCategory.Other, typeof(NpgsqlTsQuery)); break;
                        case "tsrange": AssertValues(providerDataTypeName, DataTypeCategory.Range, typeof(NpgsqlRange<DateTime>)); break;
                        case "tstzrange": AssertValues(providerDataTypeName, DataTypeCategory.Range, typeof(NpgsqlRange<DateTimeOffset>)); break;
                        case "tsvector": AssertValues(providerDataTypeName, DataTypeCategory.Other, typeof(NpgsqlTsVector)); break;
                        case "uuid": AssertValues(providerDataTypeName, DataTypeCategory.Identifier, typeof(Guid)); break;
                        case "uuid[]": AssertValues(providerDataTypeName, DataTypeCategory.Array, typeof(Guid[])); break;
                        case "xml": AssertValues(providerDataTypeName, DataTypeCategory.Xml, typeof(XDocument)); break;
                    }
                }
                else if (dbType == DbProviderType.Sqlite)
                {
                    switch (providerDataTypeName.ToLowerInvariant())
                    {
                        case "blob": AssertValues(providerDataTypeName, DataTypeCategory.Binary, typeof(byte[])); break;
                        case "boolean": AssertValues(providerDataTypeName, DataTypeCategory.Boolean, typeof(bool)); break;
                        case "char": AssertValues(providerDataTypeName, DataTypeCategory.Text, typeof(string), 1); break;
                        case "date": AssertValues(providerDataTypeName, DataTypeCategory.DateTime, typeof(DateOnly)); break;
                        case "datetime": AssertValues(providerDataTypeName, DataTypeCategory.DateTime, typeof(DateTime)); break;
                        case "decimal": AssertValues(providerDataTypeName, DataTypeCategory.Decimal, typeof(decimal), null, 18, 2); break;
                        case "integer": AssertValues(providerDataTypeName, DataTypeCategory.Integer, typeof(int)); break;
                        case "numeric": AssertValues(providerDataTypeName, DataTypeCategory.Decimal, typeof(decimal)); break;
                        case "real": AssertValues(providerDataTypeName, DataTypeCategory.Decimal, typeof(double)); break;
                        case "text": AssertValues(providerDataTypeName, DataTypeCategory.Text, typeof(string)); break;
                        case "time": AssertValues(providerDataTypeName, DataTypeCategory.DateTime, typeof(TimeOnly)); break;
                        case "timestamp": AssertValues(providerDataTypeName, DataTypeCategory.DateTime, typeof(DateTime)); break;
                        case "varchar": AssertValues(providerDataTypeName, DataTypeCategory.Text, typeof(string), 255); break;
                    }
                }
                else if (dbType == DbProviderType.SqlServer)
                {
                    switch (providerDataTypeName.ToLowerInvariant())
                    {
                        case "bigint": AssertValues(providerDataTypeName, DataTypeCategory.Integer, typeof(long)); break;
                        case "binary": AssertValues(providerDataTypeName, DataTypeCategory.Binary, typeof(byte[]), 1); break;
                        case "bit": AssertValues(providerDataTypeName, DataTypeCategory.Integer, typeof(bool)); break;
                        case "char": AssertValues(providerDataTypeName, DataTypeCategory.Text, typeof(string), 1, unicode: true); break;
                        case "date": AssertValues(providerDataTypeName, DataTypeCategory.DateTime, typeof(DateOnly)); break;
                        case "datetime": AssertValues(providerDataTypeName, DataTypeCategory.DateTime, typeof(DateTime)); break;
                        case "datetime2": AssertValues(providerDataTypeName, DataTypeCategory.DateTime, typeof(DateTime)); break;
                        case "datetimeoffset": AssertValues(providerDataTypeName, DataTypeCategory.DateTime, typeof(DateTimeOffset)); break;
                        case "decimal": AssertValues(providerDataTypeName, DataTypeCategory.Decimal, typeof(decimal), null, 18, 2); break;
                        case "float": AssertValues(providerDataTypeName, DataTypeCategory.Decimal, typeof(double)); break;
                        case "geography": AssertValues(providerDataTypeName, DataTypeCategory.Spatial, typeof(byte[])); break;
                        case "geometry": AssertValues(providerDataTypeName, DataTypeCategory.Spatial, typeof(NetTopologySuite.Geometries.Geometry)); break;
                        case "hierarchyid": AssertValues(providerDataTypeName, DataTypeCategory.Other, typeof(string)); break;
                        case "image": AssertValues(providerDataTypeName, DataTypeCategory.Binary, typeof(byte[])); break;
                        case "int": AssertValues(providerDataTypeName, DataTypeCategory.Integer, typeof(int)); break;
                        case "money": AssertValues(providerDataTypeName, DataTypeCategory.Money, typeof(decimal)); break;
                        case "nchar": AssertValues(providerDataTypeName, DataTypeCategory.Text, typeof(string), 1, unicode: true); break;
                        case "ntext": AssertValues(providerDataTypeName, DataTypeCategory.Text, typeof(string), unicode: true); break;
                        case "numeric": AssertValues(providerDataTypeName, DataTypeCategory.Decimal, typeof(decimal), null, 18, 2); break;
                        case "nvarchar": AssertValues(providerDataTypeName, DataTypeCategory.Text, typeof(string), 255, unicode: true); break;
                        case "nvarchar(max)": AssertValues(providerDataTypeName, DataTypeCategory.Text, typeof(string), -1, unicode: true); break;
                        case "real": AssertValues(providerDataTypeName, DataTypeCategory.Decimal, typeof(float)); break;
                        case "rowversion": AssertValues(providerDataTypeName, DataTypeCategory.Other, typeof(DateTime)); break;
                        case "smalldatetime": AssertValues(providerDataTypeName, DataTypeCategory.DateTime, typeof(DateTime)); break;
                        case "smallint": AssertValues(providerDataTypeName, DataTypeCategory.Integer, typeof(short)); break;
                        case "smallmoney": AssertValues(providerDataTypeName, DataTypeCategory.Money, typeof(decimal)); break;
                        case "sql_variant": AssertValues(providerDataTypeName, DataTypeCategory.Other, typeof(object)); break;
                        case "text": AssertValues(providerDataTypeName, DataTypeCategory.Text, typeof(string), unicode: true); break;
                        case "time": AssertValues(providerDataTypeName, DataTypeCategory.DateTime, typeof(TimeOnly)); break;
                        case "timestamp": AssertValues(providerDataTypeName, DataTypeCategory.Other, typeof(DateTime)); break;
                        case "tinyint": AssertValues(providerDataTypeName, DataTypeCategory.Integer, typeof(byte)); break;
                        case "uniqueidentifier": AssertValues(providerDataTypeName, DataTypeCategory.Identifier, typeof(Guid)); break;
                        case "varbinary": AssertValues(providerDataTypeName, DataTypeCategory.Binary, typeof(byte[]), 1); break;
                        case "varbinary(max)": AssertValues(providerDataTypeName, DataTypeCategory.Binary, typeof(byte[]), -1); break;
                        case "varchar": AssertValues(providerDataTypeName, DataTypeCategory.Text, typeof(string), 255, unicode: true); break;
                        case "varchar(max)": AssertValues(providerDataTypeName, DataTypeCategory.Text, typeof(string), -1, unicode: true); break;
                        case "xml": AssertValues(providerDataTypeName, DataTypeCategory.Xml, typeof(XDocument)); break;
                    }
                }
            }
        }
    }

    private async Task<DmColumn?> CreateTableWithProviderDataTypeAndGetColumnAsync(System.Data.IDbConnection db, DbProviderType dbType, string providerDataTypeName, DataTypeInfo providerDataType, bool hasPostgisExtension, string[] postgisTypes)
    {
        // Disregard postgis types if postgis extension is not installed
        if (dbType == DbProviderType.PostgreSql && postgisTypes.Contains(providerDataTypeName, StringComparer.OrdinalIgnoreCase) && !hasPostgisExtension)
        {
            return null;
        }
        // Create a table with a column of that type to verify it can be created
        const string tableName = "testTableWithAllProviderTypes";
        const string columnName = "testColumnWithProviderType";
        await db.DropTableIfExistsAsync(null, tableName);

        var actualProviderTypeName = providerDataTypeName;

        if (dbType == DbProviderType.MySql && (actualProviderTypeName.Equals("enum", StringComparison.OrdinalIgnoreCase) || actualProviderTypeName.Equals("set", StringComparison.OrdinalIgnoreCase)))
        {
            // Enums need a length
            actualProviderTypeName += "( 'Value1', 'Value2' )";
        }

        var tableCreated = await db.CreateTableIfNotExistsAsync(
            null,
            tableName,
            [
                new DmColumn("id", typeof(int), isPrimaryKey: true, isAutoIncrement: true),
                    new DmColumn(
                        columnName: columnName,
                        providerDataTypes: new Dictionary<DbProviderType, string>
                        {
                            { dbType, actualProviderTypeName }
                        },
                        length: providerDataType.SupportsLength ? (providerDataType.DefaultLength ?? 50) : null,
                        precision: providerDataType.SupportsPrecision ? (providerDataType.DefaultPrecision ?? 10) : null,
                        scale: providerDataType.SupportsScale ? (providerDataType.DefaultScale ?? 2) : null
                    ),
            ]
        );
        Assert.True(tableCreated, "Failed to create table with provider data type " + actualProviderTypeName);

        var column = await db.GetColumnAsync(null, tableName, columnName);
        Assert.NotNull(column);

        // Clean up table
        await db.DropTableIfExistsAsync(null, tableName);

        return column;
    }
}
