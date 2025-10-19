// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

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
    [Theory]
    // Primitive & Common Types
    [InlineData(typeof(byte), "TINYINT", "TINYINT UNSIGNED", "SMALLINT", "INTEGER")]
    [InlineData(typeof(short), "SMALLINT", "SMALLINT", "SMALLINT", "INTEGER")]
    [InlineData(typeof(int), "INT(10)", "INT(10)", "int4", "INT")] // int4 is PostgreSQL name for 4-byte integer
    [InlineData(typeof(long), "BIGINT", "BIGINT", "BIGINT", "INTEGER")]
    [InlineData(typeof(float), "REAL", "FLOAT", "REAL", "REAL")]
    [InlineData(typeof(double), "FLOAT", "DOUBLE", "DOUBLE PRECISION", "REAL")]
    [InlineData(typeof(decimal), "DECIMAL(16,4)", "DECIMAL(16,4)", "NUMERIC(16,4)", "NUMERIC(16,4)")]
    [InlineData(typeof(bool), "BIT", "BOOLEAN", "BOOLEAN", "INTEGER")]
    [InlineData(typeof(char), "NCHAR(1)", "CHAR(1)", "CHAR(1)", "TEXT")]
    [InlineData(typeof(string), "NVARCHAR(255)", "VARCHAR(255)", "TEXT", "TEXT")]
    [InlineData(typeof(string), "VARCHAR(234)", "VARCHAR(234)", "VARCHAR(234)", "VARCHAR(234)", false, 234)]
    [InlineData(typeof(string), "NVARCHAR(234)", "VARCHAR(234)", "VARCHAR(234)", "NVARCHAR(234)", true, 234)]
    [InlineData(typeof(Guid), "UNIQUEIDENTIFIER", "CHAR(36)", "UUID", "TEXT")]
    // Date & Time Types
    [InlineData(typeof(DateTime), "DATETIME2", "DATETIME(6)", "TIMESTAMP", "TEXT")]
    [InlineData(typeof(DateTimeOffset), "DATETIMEOFFSET", "DATETIME(6)", "TIMESTAMPTZ", "TEXT")]
    [InlineData(typeof(TimeSpan), "TIME", "TIME(6)", "TIME", "TEXT")]
    [InlineData(typeof(DateOnly), "DATE", "DATE", "DATE", "TEXT")]
    [InlineData(typeof(TimeOnly), "TIME", "TIME(6)", "TIME", "TEXT")]
    // Binary Types
    [InlineData(typeof(byte[]), "VARBINARY(255)", "VARBINARY(255)", "BYTEA", "BLOB")]
    [InlineData(typeof(Memory<byte>), "VARBINARY(255)", "VARBINARY(255)", "BYTEA", "BLOB")]
    [InlineData(typeof(ReadOnlyMemory<byte>), "VARBINARY(255)", "VARBINARY(255)", "BYTEA", "BLOB")]
    [InlineData(typeof(Stream), "VARBINARY(MAX)", "LONGBLOB", "BYTEA", "BLOB")]
    // JSON & Complex Types
    [InlineData(typeof(System.Text.Json.JsonDocument), "NVARCHAR(MAX)", "JSON", "JSONB", "TEXT")]
    [InlineData(typeof(System.Text.Json.JsonElement), "NVARCHAR(MAX)", "JSON", "JSONB", "TEXT")]
    [InlineData(typeof(object), "NVARCHAR(MAX)", "TEXT", "TEXT", "TEXT")]
    [InlineData(typeof(DayOfWeek), "VARCHAR(128)", "VARCHAR(128)", "TEXT", "TEXT")] // Enum example
    // Array Types (PostgreSQL native, others JSON/TEXT)
    [InlineData(typeof(string[]), "NVARCHAR(MAX)", "TEXT", "text[]", "TEXT")]
    [InlineData(typeof(int[]), "NVARCHAR(MAX)", "TEXT", "integer[]", "TEXT")]
    [InlineData(typeof(long[]), "NVARCHAR(MAX)", "TEXT", "bigint[]", "TEXT")]
    [InlineData(typeof(Guid[]), "NVARCHAR(MAX)", "TEXT", "uuid[]", "TEXT")]
    [InlineData(typeof(char[]), "NVARCHAR(MAX)", "TEXT", "text", "TEXT")]
    // Collection Types (all serialized as JSON)
    [InlineData(typeof(List<string>), "NVARCHAR(MAX)", "JSON", "JSONB", "TEXT")]
    [InlineData(typeof(IList<string>), "NVARCHAR(MAX)", "JSON", "JSONB", "TEXT")]
    [InlineData(typeof(ICollection<string>), "NVARCHAR(MAX)", "JSON", "JSONB", "TEXT")]
    [InlineData(typeof(IEnumerable<string>), "NVARCHAR(MAX)", "JSON", "JSONB", "TEXT")]
    [InlineData(typeof(Dictionary<string, string>), "NVARCHAR(MAX)", "JSON", "JSONB", "TEXT")]
    [InlineData(typeof(IDictionary<string, string>), "NVARCHAR(MAX)", "JSON", "JSONB", "TEXT")]
    protected virtual async Task Should_map_common_column_types_exactly_as_expected(
        Type type,
        string sqlServerTypeName,
        string mySqlTypeName,
        string postgreSqlTypeName,
        string sqliteTypeName,
        bool isUnicode = false,
        int? length = null,
        int? precision = null,
        int? scale = null
    )
    {
        using var db = await OpenConnectionAsync();
        var dbType = db.GetDbProviderType();

        var databaseMethods = DatabaseMethodsProvider.GetMethods(db);
        var providerDataTypes = databaseMethods
            .GetAvailableDataTypes(includeAdvanced: true)
            .ToList();

        string expectedTypeName = dbType switch
        {
            DbProviderType.SqlServer => sqlServerTypeName,
            DbProviderType.MySql => mySqlTypeName,
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
                new DmColumn(
                    null,
                    tableName,
                    "id",
                    typeof(int),
                    isPrimaryKey: true,
                    isAutoIncrement: true
                ),
                new DmColumn(
                    null,
                    tableName,
                    columnName,
                    type,
                    isUnicode: isUnicode,
                    length: length,
                    precision: precision,
                    scale: scale
                ),
            ]
        );

        // Fetch the column
        var column = await db.GetColumnAsync(null, tableName, columnName);
        // Clean up before we validate the column
        await db.DropTableIfExistsAsync(null, tableName);
        Assert.NotNull(column);

        // STRANGELY, MySQL RETURN TYPES WITH the LENGTH: varchar(234)
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
        if (column!.Length.HasValue && column.Length.Value > 0)
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
}
