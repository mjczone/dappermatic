// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Dapper;

using MJCZone.DapperMatic.Models;
using MJCZone.DapperMatic.Providers;

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
    // CONTAIN WORDS LIKE 'CHAR' OR 'INT' ARE ACCEPTED AS VALID TYPES AND MAPPED AUTOMATICALLY
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
    [InlineData(typeof(byte), "TINYINT(3)", "TINYINT(3)", "INT2", "TINYINT", false, null, null, null, false, "TINYINT(4)", "TINYINT(4)")]
    [InlineData(typeof(sbyte), "TINYINT(3)", "TINYINT(3)", "INT2", "TINYINT", false, null, null, null, false, "TINYINT(4)", "TINYINT(4)")]
    [InlineData(typeof(short), "SMALLINT(5)", "SMALLINT(5)", "INT2", "SMALLINT", false, null, null, null, false, "SMALLINT(6)", "SMALLINT(6)")]
    [InlineData(typeof(int), "INT(10)", "INT(10)", "INT4", "INT", false, null, null, null, false, "INT(11)", "INT(11)")]// int4 is PostgreSQL name for 4-byte integer
    [InlineData(typeof(long), "BIGINT(19)", "BIGINT(19)", "INT8", "BIGINT", false, null, null, null, false, "BIGINT(20)", "BIGINT(20)")]
    [InlineData(typeof(float), "REAL(24)", "DOUBLE(22)", "FLOAT4", "REAL")]
    [InlineData(typeof(double), "FLOAT(53)", "FLOAT(12)", "FLOAT8", "DOUBLE")]
    [InlineData(typeof(decimal), "DECIMAL(16,4)", "DECIMAL(16,4)", "NUMERIC(16,4)", "NUMERIC(16,4)")]
    [InlineData(typeof(decimal), "DECIMAL(12,8)", "DECIMAL(12,8)", "NUMERIC(12,8)", "NUMERIC(12,8)", false, null, 12, 8)]
    [InlineData(typeof(decimal), "DECIMAL(12)", "DECIMAL(12)", "NUMERIC(12)", "NUMERIC(12)", false, null, 12, 0)]
    [InlineData(typeof(bool), "BIT", "TINYINT(1)", "BOOL", "BOOLEAN")]
    [InlineData(typeof(char), "CHAR(1)", "CHAR(1)", "BPCHAR(1)", "CHAR(1)")]
    [InlineData(typeof(char), "NCHAR(1)", "CHAR(1)", "BPCHAR(1)", "NCHAR(1)", true)]
    [InlineData(typeof(string), "CHAR(234)", "CHAR(234)", "BPCHAR(234)", "CHAR(234)", false, 234, null, null, true)]
    [InlineData(typeof(string), "NCHAR(234)", "CHAR(234)", "BPCHAR(234)", "NCHAR(234)", true, 234, null, null, true)]
    [InlineData(typeof(string), "VARCHAR(255)", "VARCHAR(255)", "VARCHAR(255)", "VARCHAR(255)")]
    [InlineData(typeof(string), "NVARCHAR(255)", "VARCHAR(255)", "VARCHAR(255)", "NVARCHAR(255)", true)]
    [InlineData(typeof(string), "VARCHAR(234)", "VARCHAR(234)", "VARCHAR(234)", "VARCHAR(234)", false, 234)]
    [InlineData(typeof(string), "NVARCHAR(234)", "VARCHAR(234)", "VARCHAR(234)", "NVARCHAR(234)", true, 234)]
    [InlineData(typeof(string), "NVARCHAR(MAX)", "TEXT(65535)", "TEXT", "NVARCHAR", true, -1)]
    [InlineData(typeof(string), "NVARCHAR(MAX)", "TEXT(65535)", "TEXT", "NVARCHAR", true, int.MaxValue)]
    [InlineData(typeof(Guid), "UNIQUEIDENTIFIER", "CHAR(36)", "UUID", "VARCHAR(36)")]
    // Date & Time Types
    [InlineData(typeof(DateTime), "DATETIME", "DATETIME", "TIMESTAMP", "DATETIME")]
    [InlineData(typeof(DateTimeOffset), "DATETIMEOFFSET", "TIMESTAMP", "TIMESTAMPTZ", "DATETIME")]
    [InlineData(typeof(TimeSpan), "TIME", "TIME", "INTERVAL", "TIME")]
    [InlineData(typeof(DateOnly), "DATE", "DATE", "DATE", "DATE")]
    [InlineData(typeof(TimeOnly), "TIME", "TIME", "TIME", "TIME")]
    // Binary Types
    [InlineData(typeof(byte[]), "VARBINARY(255)", "VARBINARY(255)", "BYTEA", "BLOB")]
    [InlineData(typeof(Memory<byte>), "VARBINARY(255)", "VARBINARY(255)", "BYTEA", "BLOB")]
    [InlineData(typeof(ReadOnlyMemory<byte>), "VARBINARY(255)", "VARBINARY(255)", "BYTEA", "BLOB")]
    [InlineData(typeof(Stream), "VARBINARY(MAX)", "LONGBLOB", "BYTEA", "BLOB")]
    [InlineData(typeof(MemoryStream), "VARBINARY(MAX)", "LONGBLOB", "BYTEA", "BLOB")]
    // JSON & Complex Types (MariaDB 10.x maps JSON to LONGTEXT)
    [InlineData(typeof(System.Text.Json.JsonDocument), "VARCHAR(MAX)", "JSON", "JSONB", "TEXT")]
    [InlineData(typeof(System.Text.Json.JsonElement), "VARCHAR(MAX)", "JSON", "JSONB", "TEXT")]
    [InlineData(typeof(System.Text.Json.JsonDocument), "NVARCHAR(MAX)", "JSON", "JSONB", "TEXT", true)]
    [InlineData(typeof(System.Text.Json.JsonElement), "NVARCHAR(MAX)", "JSON", "JSONB", "TEXT", true)]
    [InlineData(typeof(System.Text.Json.Nodes.JsonArray), "NVARCHAR(MAX)", "JSON", "JSONB", "TEXT", true)]
    [InlineData(typeof(System.Text.Json.Nodes.JsonObject), "NVARCHAR(MAX)", "JSON", "JSONB", "TEXT", true)]
    [InlineData(typeof(System.Text.Json.Nodes.JsonValue), "NVARCHAR(MAX)", "JSON", "JSONB", "TEXT", true)]
    [InlineData(typeof(object), "VARCHAR(MAX)", "JSON", "JSONB", "TEXT")]
    [InlineData(typeof(object), "NVARCHAR(MAX)", "JSON", "JSONB", "TEXT", true)]
    [InlineData(typeof(DayOfWeek), "VARCHAR(128)", "VARCHAR(128)", "VARCHAR(128)", "VARCHAR(128)")] // Enum example
    // Array Types (PostgreSQL native, others JSON/TEXT, MariaDB 10.x maps JSON to LONGTEXT)
    [InlineData(typeof(string[]), "VARCHAR(MAX)", "JSON", "_TEXT", "TEXT")]
    [InlineData(typeof(int[]), "VARCHAR(MAX)", "JSON", "_INT4", "TEXT")]
    [InlineData(typeof(long[]), "VARCHAR(MAX)", "JSON", "_INT8", "TEXT")]
    [InlineData(typeof(Guid[]), "VARCHAR(MAX)", "JSON", "_UUID", "TEXT")]
    [InlineData(typeof(char[]), "VARCHAR(255)", "VARCHAR(255)", "VARCHAR(255)", "VARCHAR(255)")]
    [InlineData(typeof(char[]), "NVARCHAR(255)", "VARCHAR(255)", "VARCHAR(255)", "NVARCHAR(255)", true)]
    [InlineData(typeof(char[]), "VARCHAR(MAX)", "TEXT(65535)", "TEXT", "VARCHAR", false, -1)]
    [InlineData(typeof(char[]), "NVARCHAR(MAX)", "TEXT(65535)", "TEXT", "NVARCHAR", true, -1)]
    // Collection Types (all serialized as JSON, MariaDB 10.x maps JSON to LONGTEXT)
    [InlineData(typeof(List<string>), "VARCHAR(MAX)", "JSON", "JSONB", "TEXT")]
    [InlineData(typeof(List<string>), "NVARCHAR(MAX)", "JSON", "JSONB", "TEXT", true)]
    [InlineData(typeof(IList<string>), "VARCHAR(MAX)", "JSON", "JSONB", "TEXT")]
    [InlineData(typeof(ICollection<string>), "VARCHAR(MAX)", "JSON", "JSONB", "TEXT")]
    [InlineData(typeof(IEnumerable<string>), "VARCHAR(MAX)", "JSON", "JSONB", "TEXT")]
    [InlineData(typeof(Dictionary<string, string>), "VARCHAR(MAX)", "JSON", "HSTORE", "TEXT")]
    [InlineData(typeof(IDictionary<string, string>), "VARCHAR(MAX)", "JSON", "HSTORE", "TEXT")]
    [InlineData(typeof(Dictionary<string, string>), "NVARCHAR(MAX)", "JSON", "HSTORE", "TEXT", true)]
    [InlineData(typeof(IDictionary<string, string>), "NVARCHAR(MAX)", "JSON", "HSTORE", "TEXT", true)]
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

        var databaseMethods = DatabaseMethodsProvider.GetMethods(db);
        var providerDataTypes = databaseMethods.GetAvailableDataTypes(includeAdvanced: true).ToList();

        Func<DbProviderType, Task<string>> getExpectedMySqlTypeName = async dbType =>
        {
            var dbVersion = await databaseMethods.GetDatabaseVersionAsync(db);
            if (dbVersion.Major == 5)
            {
                return mySql5TypeName ?? mySqlTypeName;
            }
            else if (dbVersion.Major >= 10)
            {
                if (mySqlTypeName.Equals("JSON", StringComparison.OrdinalIgnoreCase))
                {
                    // MySQL 5.x and MariaDB 10.x do not have a native JSON type, so we map to LONGTEXT
                    return "LONGTEXT";
                }
                return mariaDb10TypeName ?? mySqlTypeName;
            }
            return mySqlTypeName;
        };

        string expectedTypeName = dbType switch
        {
            DbProviderType.SqlServer => sqlServerTypeName,
            DbProviderType.MySql => await getExpectedMySqlTypeName(dbType),
            DbProviderType.PostgreSql => postgreSqlTypeName,
            DbProviderType.Sqlite => sqliteTypeName,
            _ => throw new NotSupportedException($"Database type {dbType} is not supported."),
        };

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

        var providerTypeName = column?.ProviderDataTypes.FirstOrDefault().Value;
        Assert.NotNull(providerTypeName);
        Assert.NotEmpty(providerTypeName);

        var providerDataType = providerDataTypes.FirstOrDefault(dt =>
            string.Equals(dt.DataType, providerTypeName, StringComparison.OrdinalIgnoreCase)
        );
        if (providerDataType == null)
        {
            // The type could be an alias
            providerDataType = providerDataTypes.FirstOrDefault(dt =>
                (dt.Aliases ?? []).Any(alias =>
                    string.Equals(alias, providerTypeName, StringComparison.OrdinalIgnoreCase)
                )
            );
        }
        Assert.NotNull(providerDataType);
        var providerDataTypeAliases = providerDataType.Aliases ?? [];
        Output.WriteLine(
            "Column '{0}' provider type '{1}' has {2} aliases: {3}",
            columnName,
            providerTypeName,
            providerDataTypeAliases.Count,
            string.Join(", ", providerDataTypeAliases)
        );

        // The provider type name DOES NOT include the parentheses for length/precision/scale, these
        // are rather properties of the column metadata
        Assert.False(
            providerTypeName!.Contains('('),
            $"When fetching a column, the provider type name '{providerTypeName}' would not contain '{expectedTypeName.Split('(')[0]}'"
        );

        // The data type will does not include length/precision/scale in the name, so we add these if they are returned by the provider
        // Handle -1 as MAX/unlimited length
        if (column!.Length.HasValue && column.Length.Value == -1)
        {
            // For SQL Server, render as (MAX)
            if (dbType == DbProviderType.SqlServer)
            {
                providerTypeName += "(MAX)";
            }
            // For other providers (MySQL TEXT, PostgreSQL TEXT, SQLite TEXT), -1 means unlimited, don't append anything
        }
        else if (column!.Length.HasValue && column.Length.Value > 0)
        {
            providerTypeName += $"({column.Length.Value})";
        }
        else if (
            column.Precision.HasValue
            && column.Precision.Value > 0
            && column.Scale.HasValue
            && column.Scale.Value > 0
        )
        {
            providerTypeName += $"({column.Precision.Value},{column.Scale.Value})";
        }
        else if (column.Precision.HasValue && column.Precision.Value > 0)
        {
            providerTypeName += $"({column.Precision.Value})";
        }

        base.Output.WriteLine(
            "Column '{0}' mapped to provider type '{1}' (expected '{2}')",
            columnName,
            providerTypeName,
            expectedTypeName
        );
        Assert.Equal(expectedTypeName, providerTypeName, StringComparer.OrdinalIgnoreCase);
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

        var file = @"/mnt/c/Temp/provider_datatypes_" + dbType.ToString() + "_" + dbVersion.ToString() + ".csv";
        using var writer = new StreamWriter(file, append: false);
        writer.WriteLine("Provider,Data Type,Category,Supports Length,Supports Precision,Supports Scale,Aliases,DotNetType,DotNetTypeUnicode");

        foreach (var providerDataType in providerDataTypes)
        {
            // Disregard postgis types if postgis extension is not installed
            if (dbType == DbProviderType.PostgreSql && postgisTypes.Contains(providerDataType.DataType, StringComparer.OrdinalIgnoreCase) && !hasPostgisExtension)
            {
                continue;
            }

            // Print out all provider data types for debugging, along with aliases
            var aliases = providerDataType.Aliases != null
                ? string.Join(", ", providerDataType.Aliases)
                : "None";

            Output.WriteLine(
                "Provider: {0} Data Type: {1}, Category: {2}, SupportsLength: {3}, SupportsPrecision: {4}, SupportsScale: {5}, Aliases: {6}",
                $"{dbType} ({dbVersion})",
                providerDataType.DataType,
                providerDataType.Category,
                providerDataType.SupportsLength,
                providerDataType.SupportsPrecision,
                providerDataType.SupportsScale,
                aliases
            );

            // Create a table with a column of that type to verify it can be created
            const string tableName = "testTableWithAllProviderTypes";
            const string columnName = "testColumnWithProviderType";
            const string tableNameUnicode = "testTableWithAllProviderTypesUnicode";
            const string columnNameUnicode = "testColumnWithProviderTypeUnicode";
            await db.DropTableIfExistsAsync(null, tableName);

            var actualProviderTypeName = providerDataType.DataType;
            if (dbType == DbProviderType.MySql && (providerDataType.DataType.Equals("enum", StringComparison.OrdinalIgnoreCase) || providerDataType.DataType.Equals("set", StringComparison.OrdinalIgnoreCase)))
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
            var tableCreatedUnicode = await db.CreateTableIfNotExistsAsync(
                null,
                tableNameUnicode,
                [
                    new DmColumn("id", typeof(int), isPrimaryKey: true, isAutoIncrement: true),
                    new DmColumn(
                        columnName: columnNameUnicode,
                        providerDataTypes: new Dictionary<DbProviderType, string>
                        {
                            { dbType, actualProviderTypeName }
                        },
                        length: providerDataType.SupportsLength ? (providerDataType.DefaultLength ?? 50) : null,
                        precision: providerDataType.SupportsPrecision ? (providerDataType.DefaultPrecision ?? 10) : null,
                        scale: providerDataType.SupportsScale ? (providerDataType.DefaultScale ?? 2) : null,
                        isUnicode: true
                    ),
                ]
            );
            Assert.True(tableCreated, "Failed to create table with provider data type " + providerDataType.DataType);

            var column = await db.GetColumnAsync(null, tableName, columnName);
            Assert.NotNull(column);
            var columnUnicode = await db.GetColumnAsync(null, tableNameUnicode, columnNameUnicode);
            Assert.NotNull(columnUnicode);

            // Clean up table
            await db.DropTableIfExistsAsync(null, tableName);
            await db.DropTableIfExistsAsync(null, tableNameUnicode);

            // Write the data to the CSV file
            writer.WriteLine(
                "{0},{1},{2},{3},{4},{5},{6},{7},{8}",
                $"{dbType} ({dbVersion})",
                providerDataType.DataType,
                providerDataType.Category,
                providerDataType.SupportsLength,
                providerDataType.SupportsPrecision,
                providerDataType.SupportsScale,
                aliases,
                column!.DotnetType?.FullName ?? "Unknown",
                columnUnicode!.DotnetType?.FullName ?? "Unknown"
            );
        }

        writer.Flush();
        writer.Close();
    }
}
