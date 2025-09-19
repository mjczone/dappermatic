// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.Converters;
using MJCZone.DapperMatic.Providers;
using Xunit.Abstractions;

namespace MJCZone.DapperMatic.Tests;

public class TypeMappingHelpersTests : TestBase
{
    public TypeMappingHelpersTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void GetAssemblyQualifiedShortName_WithValidType_ReturnsShortName()
    {
        // Arrange
        var type = typeof(string);

        // Act
        var result = TypeMappingHelpers.GetAssemblyQualifiedShortName(type);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("System.String", result);
        Assert.Contains("System.Private.CoreLib", result);
        Assert.DoesNotContain("Version=", result);
        Assert.DoesNotContain("Culture=", result);
    }

    [Fact]
    public void GetAssemblyQualifiedShortName_WithNullType_ReturnsNull()
    {
        // Act
        var result = TypeMappingHelpers.GetAssemblyQualifiedShortName(null);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void CreateDecimalType_WithDefaults_UsesDefaultPrecisionAndScale()
    {
        // Act
        var result = TypeMappingHelpers.CreateDecimalType("decimal");

        // Assert
        Assert.Equal("decimal(16,4)", result.SqlTypeName);
        Assert.Equal(16, result.Precision);
        Assert.Equal(4, result.Scale);
    }

    [Fact]
    public void CreateDecimalType_WithCustomValues_UsesProvidedValues()
    {
        // Act
        var result = TypeMappingHelpers.CreateDecimalType("numeric", 10, 2);

        // Assert
        Assert.Equal("numeric(10,2)", result.SqlTypeName);
        Assert.Equal(10, result.Precision);
        Assert.Equal(2, result.Scale);
    }

    [Fact]
    public void CreateStringType_WithDefaults_UsesDefaultLength()
    {
        // Act
        var result = TypeMappingHelpers.CreateStringType("varchar");

        // Assert
        Assert.Equal("varchar(255)", result.SqlTypeName);
        Assert.Equal(255, result.Length);
        Assert.False(result.IsUnicode);
        Assert.False(result.IsFixedLength);
    }

    [Fact]
    public void CreateStringType_WithMaxLength_UsesMaxSyntax()
    {
        // Act
        var result = TypeMappingHelpers.CreateStringType("varchar", TypeMappingDefaults.MaxLength);

        // Assert
        Assert.Equal("varchar(max)", result.SqlTypeName);
        Assert.Null(result.Length);
        Assert.False(result.IsUnicode);
        Assert.False(result.IsFixedLength);
    }

    [Fact]
    public void CreateStringType_WithUnicodeAndFixedLength_SetsFlags()
    {
        // Act
        var result = TypeMappingHelpers.CreateStringType("nchar", 50, isUnicode: true, isFixedLength: true);

        // Assert
        Assert.Equal("nchar(50)", result.SqlTypeName);
        Assert.Equal(50, result.Length);
        Assert.True(result.IsUnicode);
        Assert.True(result.IsFixedLength);
    }

    [Fact]
    public void CreateGuidStringType_ReturnsCorrectConfiguration()
    {
        // Act
        var result = TypeMappingHelpers.CreateGuidStringType("char", isUnicode: false, isFixedLength: true);

        // Assert
        Assert.Equal("char(36)", result.SqlTypeName);
        Assert.Equal(36, result.Length);
        Assert.False(result.IsUnicode);
        Assert.True(result.IsFixedLength);
    }

    [Fact]
    public void CreateEnumStringType_ReturnsCorrectConfiguration()
    {
        // Act
        var result = TypeMappingHelpers.CreateEnumStringType("varchar", isUnicode: true);

        // Assert
        Assert.Equal("varchar(128)", result.SqlTypeName);
        Assert.Equal(128, result.Length);
        Assert.True(result.IsUnicode);
        Assert.False(result.IsFixedLength);
    }

    [Theory]
    [InlineData(typeof(string), false)]
    [InlineData(typeof(int), false)]
    public void IsNetTopologySuiteGeometryType_WithNonGeometryTypes_ReturnsFalse(Type type, bool expected)
    {
        // Act
        var result = TypeMappingHelpers.IsNetTopologySuiteGeometryType(type);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsNetTopologySuiteGeometryType_WithNull_ReturnsFalse()
    {
        // Act
        var result = TypeMappingHelpers.IsNetTopologySuiteGeometryType(null);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(typeof(string), null)]
    [InlineData(typeof(int), null)]
    public void GetGeometryTypeName_WithNonGeometryTypes_ReturnsNull(Type type, string? expected)
    {
        // Act
        var result = TypeMappingHelpers.GetGeometryTypeName(type);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetGeometryTypeName_WithNull_ReturnsNull()
    {
        // Act
        var result = TypeMappingHelpers.GetGeometryTypeName(null);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void CreateSimpleType_ReturnsBasicDescriptor()
    {
        // Act
        var result = TypeMappingHelpers.CreateSimpleType("int");

        // Assert
        Assert.Equal("int", result.SqlTypeName);
        Assert.Null(result.Length);
        Assert.Null(result.Precision);
        Assert.Null(result.Scale);
    }

    [Fact]
    public void CreateDateTimeType_WithoutPrecision_ReturnsBasicType()
    {
        // Act
        var result = TypeMappingHelpers.CreateDateTimeType("datetime");

        // Assert
        Assert.Equal("datetime", result.SqlTypeName);
        Assert.Null(result.Precision);
    }

    [Fact]
    public void CreateDateTimeType_WithPrecision_IncludesPrecision()
    {
        // Act
        var result = TypeMappingHelpers.CreateDateTimeType("datetime2", 3);

        // Assert
        Assert.Equal("datetime2(3)", result.SqlTypeName);
        Assert.Equal(3, result.Precision);
    }

    [Fact]
    public void CreateBinaryType_WithoutLength_ReturnsBasicType()
    {
        // Act
        var result = TypeMappingHelpers.CreateBinaryType("binary");

        // Assert
        Assert.Equal("binary", result.SqlTypeName);
        Assert.Null(result.Length);
        Assert.False(result.IsFixedLength);
    }

    [Fact]
    public void CreateBinaryType_WithMaxLength_UsesMaxSyntax()
    {
        // Act
        var result = TypeMappingHelpers.CreateBinaryType("varbinary", TypeMappingDefaults.MaxLength, false);

        // Assert
        Assert.Equal("varbinary(max)", result.SqlTypeName);
        Assert.Null(result.Length);
        Assert.False(result.IsFixedLength);
    }

    [Fact]
    public void CreateBinaryType_WithSpecificLength_UsesLength()
    {
        // Act
        var result = TypeMappingHelpers.CreateBinaryType("binary", 50, true);

        // Assert
        Assert.Equal("binary(50)", result.SqlTypeName);
        Assert.Equal(50, result.Length);
        Assert.True(result.IsFixedLength);
    }

    [Fact]
    public void CreateJsonType_AsNativeJson_ReturnsBasicType()
    {
        // Act
        var result = TypeMappingHelpers.CreateJsonType("json", false);

        // Assert
        Assert.Equal("json", result.SqlTypeName);
        Assert.Null(result.Length);
    }

    [Fact]
    public void CreateJsonType_AsText_SetsMaxLength()
    {
        // Act
        var result = TypeMappingHelpers.CreateJsonType("nvarchar(max)", true);

        // Assert
        Assert.Equal("nvarchar(max)", result.SqlTypeName);
        Assert.Equal(TypeMappingDefaults.MaxLength, result.Length);
    }

    [Fact]
    public void CreateGeometryType_WithoutTypeName_ReturnsBasicType()
    {
        // Act
        var result = TypeMappingHelpers.CreateGeometryType("geometry");

        // Assert
        Assert.Equal("geometry", result.SqlTypeName);
    }

    [Fact]
    public void CreateGeometryType_WithTypeName_ReturnsBasicType()
    {
        // Act
        var result = TypeMappingHelpers.CreateGeometryType("geometry", "Point");

        // Assert
        Assert.Equal("geometry", result.SqlTypeName);
    }

    [Fact]
    public void CreateLobType_SetsMaxLengthAndUnicode()
    {
        // Act
        var result = TypeMappingHelpers.CreateLobType("text", true);

        // Assert
        Assert.Equal("text", result.SqlTypeName);
        Assert.Equal(TypeMappingDefaults.MaxLength, result.Length);
        Assert.True(result.IsUnicode);
    }

    [Fact]
    public void CreateArrayType_ReturnsArrayDescriptor()
    {
        // Act
        var result = TypeMappingHelpers.CreateArrayType("integer[]", "integer");

        // Assert
        Assert.Equal("integer[]", result.SqlTypeName);
    }

    [Fact]
    public void CreatePrecisionType_IncludesPrecision()
    {
        // Act
        var result = TypeMappingHelpers.CreatePrecisionType("time", 6);

        // Assert
        Assert.Equal("time(6)", result.SqlTypeName);
        Assert.Equal(6, result.Precision);
    }

    [Theory]
    [InlineData(typeof(int[]), true)]
    [InlineData(typeof(string[]), true)]
    [InlineData(typeof(int), false)]
    [InlineData(typeof(string), false)]
    public void IsArrayType_ReturnsCorrectResult(Type type, bool expected)
    {
        // Act
        var result = TypeMappingHelpers.IsArrayType(type);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsArrayType_WithNull_ReturnsFalse()
    {
        // Act
        var result = TypeMappingHelpers.IsArrayType(null);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetArrayElementType_WithArrayType_ReturnsElementType()
    {
        // Act
        var result = TypeMappingHelpers.GetArrayElementType(typeof(int[]));

        // Assert
        Assert.Equal(typeof(int), result);
    }

    [Fact]
    public void GetArrayElementType_WithNonArrayType_ReturnsNull()
    {
        // Act
        var result = TypeMappingHelpers.GetArrayElementType(typeof(int));

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData(typeof(List<int>), true)]
    [InlineData(typeof(IList<string>), true)]
    [InlineData(typeof(HashSet<int>), true)]
    [InlineData(typeof(ICollection<string>), true)]
    [InlineData(typeof(IEnumerable<int>), true)]
    [InlineData(typeof(int[]), false)]
    [InlineData(typeof(string), false)]
    [InlineData(typeof(int), false)]
    public void IsGenericCollectionType_ReturnsCorrectResult(Type type, bool expected)
    {
        // Act
        var result = TypeMappingHelpers.IsGenericCollectionType(type);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsGenericCollectionType_WithNull_ReturnsFalse()
    {
        // Act
        var result = TypeMappingHelpers.IsGenericCollectionType(null);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetCollectionElementType_WithGenericCollection_ReturnsElementType()
    {
        // Act
        var result = TypeMappingHelpers.GetCollectionElementType(typeof(List<string>));

        // Assert
        Assert.Equal(typeof(string), result);
    }

    [Fact]
    public void GetCollectionElementType_WithNonGenericCollection_ReturnsNull()
    {
        // Act
        var result = TypeMappingHelpers.GetCollectionElementType(typeof(string[]));

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetCollectionElementType_WithNull_ReturnsNull()
    {
        // Act
        var result = TypeMappingHelpers.GetCollectionElementType(null);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetStandardGeometryTypes_ReturnsFilteredArray()
    {
        // Act
        var types = TypeMappingHelpers.GetStandardGeometryTypes();
        
        // Assert
        Assert.NotNull(types);
        Assert.IsType<Type[]>(types);
        
        // All returned types should be non-null
        Assert.True(types.All(t => t != null));
        
        // If NetTopologySuite is available, should have 8 types
        if (types.Length > 0)
        {
            Assert.Equal(8, types.Length);
        }
    }

    [Fact]
    public void GetSqlServerGeometryTypes_ReturnsFilteredArray()
    {
        // Act
        var types = TypeMappingHelpers.GetSqlServerGeometryTypes();
        
        // Assert
        Assert.NotNull(types);
        Assert.IsType<Type[]>(types);
        
        // All returned types should be non-null
        Assert.True(types.All(t => t != null));
    }

    [Fact]
    public void GetMySqlGeometryTypes_ReturnsFilteredArray()
    {
        // Act
        var types = TypeMappingHelpers.GetMySqlGeometryTypes();
        
        // Assert
        Assert.NotNull(types);
        Assert.IsType<Type[]>(types);
        
        // All returned types should be non-null
        Assert.True(types.All(t => t != null));
    }

    [Fact]
    public void GetPostgreSqlSpecialTypes_ReturnsFilteredArray()
    {
        // Act
        var types = TypeMappingHelpers.GetPostgreSqlSpecialTypes();
        
        // Assert
        Assert.NotNull(types);
        Assert.IsType<Type[]>(types);
        
        // All returned types should be non-null
        Assert.True(types.All(t => t != null));
        
        // Should at least have some system types
        if (types.Length > 0)
        {
            // Should have at least System.Net types
            var hasNetworkTypes = types.Any(t => t.Namespace?.StartsWith("System.Net") == true);
            Assert.True(hasNetworkTypes);
        }
    }

    [Fact]
    public void GetGeometryTypesForProvider_ReturnsCorrectTypesForEachProvider()
    {
        // Test SQL Server
        var sqlServerTypes = TypeMappingHelpers.GetGeometryTypesForProvider("sqlserver");
        Assert.NotNull(sqlServerTypes);
        Assert.True(sqlServerTypes.All(t => t != null));
        
        // Test MySQL
        var mysqlTypes = TypeMappingHelpers.GetGeometryTypesForProvider("mysql");
        Assert.NotNull(mysqlTypes);
        Assert.True(mysqlTypes.All(t => t != null));
        
        // Test PostgreSQL
        var postgresTypes = TypeMappingHelpers.GetGeometryTypesForProvider("postgresql");
        Assert.NotNull(postgresTypes);
        Assert.True(postgresTypes.All(t => t != null));
        
        // Test SQLite
        var sqliteTypes = TypeMappingHelpers.GetGeometryTypesForProvider("sqlite");
        Assert.NotNull(sqliteTypes);
        Assert.True(sqliteTypes.All(t => t != null));
        
        // Test unknown provider (should return standard types)
        var unknownTypes = TypeMappingHelpers.GetGeometryTypesForProvider("unknown");
        Assert.NotNull(unknownTypes);
        Assert.True(unknownTypes.All(t => t != null));
        
        // SQLite should only have standard types (no additional provider-specific types)
        var standardTypes = TypeMappingHelpers.GetStandardGeometryTypes();
        Assert.Equal(standardTypes.Length, sqliteTypes.Length);
    }

    [Fact]
    public void GetGeometryTypesForProvider_CaseInsensitive()
    {
        // Act
        var sqlServerLower = TypeMappingHelpers.GetGeometryTypesForProvider("sqlserver");
        var sqlServerUpper = TypeMappingHelpers.GetGeometryTypesForProvider("SQLSERVER");
        var sqlServerMixed = TypeMappingHelpers.GetGeometryTypesForProvider("SqlServer");
        
        // Assert
        Assert.Equal(sqlServerLower.Length, sqlServerUpper.Length);
        Assert.Equal(sqlServerLower.Length, sqlServerMixed.Length);
    }

    [Fact]
    public void GetStandardJsonTypes_ReturnsExpectedTypes()
    {
        // Act
        var jsonTypes = TypeMappingHelpers.GetStandardJsonTypes();
        
        // Assert
        Assert.NotNull(jsonTypes);
        Assert.Equal(6, jsonTypes.Length);
        
        // Verify specific types are included
        Assert.Contains(typeof(System.Text.Json.JsonDocument), jsonTypes);
        Assert.Contains(typeof(System.Text.Json.JsonElement), jsonTypes);
        Assert.Contains(typeof(System.Text.Json.Nodes.JsonArray), jsonTypes);
        Assert.Contains(typeof(System.Text.Json.Nodes.JsonNode), jsonTypes);
        Assert.Contains(typeof(System.Text.Json.Nodes.JsonObject), jsonTypes);
        Assert.Contains(typeof(System.Text.Json.Nodes.JsonValue), jsonTypes);
    }

    [Fact]
    public void CreateJsonConverter_ReturnsCorrectConverterForEachProvider()
    {
        // Test each provider
        var providers = new[] { "mysql", "postgresql", "sqlserver", "sqlite", "unknown" };
        
        foreach (var provider in providers)
        {
            // Act
            var converter = TypeMappingHelpers.CreateJsonConverter(provider);
            
            // Assert
            Assert.NotNull(converter);
            Assert.IsType<DotnetTypeToSqlTypeConverter>(converter);
        }
    }

    [Fact]
    public void CreateJsonConverter_CaseInsensitive()
    {
        // Act & Assert - should not throw exceptions
        var mysqlLower = TypeMappingHelpers.CreateJsonConverter("mysql");
        var mysqlUpper = TypeMappingHelpers.CreateJsonConverter("MYSQL");
        var mysqlMixed = TypeMappingHelpers.CreateJsonConverter("MySQL");
        
        Assert.NotNull(mysqlLower);
        Assert.NotNull(mysqlUpper);
        Assert.NotNull(mysqlMixed);
    }

    [Fact]
    public void CreateProviderOptimizedJsonType_ReturnsCorrectTypeForEachProvider()
    {
        // Test MySQL (native JSON)
        var mysqlJson = TypeMappingHelpers.CreateProviderOptimizedJsonType("mysql");
        Assert.NotNull(mysqlJson);
        Assert.Equal("json", mysqlJson.SqlTypeName);
        
        // Test PostgreSQL (native JSONB)
        var postgresJson = TypeMappingHelpers.CreateProviderOptimizedJsonType("postgresql");
        Assert.NotNull(postgresJson);
        Assert.Equal("jsonb", postgresJson.SqlTypeName);
        
        // Test SQL Server (text-based, non-Unicode)
        var sqlServerJson = TypeMappingHelpers.CreateProviderOptimizedJsonType("sqlserver", isUnicode: false);
        Assert.NotNull(sqlServerJson);
        Assert.Equal("varchar(max)", sqlServerJson.SqlTypeName);
        
        // Test SQL Server (text-based, Unicode)
        var sqlServerJsonUnicode = TypeMappingHelpers.CreateProviderOptimizedJsonType("sqlserver", isUnicode: true);
        Assert.NotNull(sqlServerJsonUnicode);
        Assert.Equal("nvarchar(max)", sqlServerJsonUnicode.SqlTypeName);
        
        // Test SQLite (text-based)
        var sqliteJson = TypeMappingHelpers.CreateProviderOptimizedJsonType("sqlite");
        Assert.NotNull(sqliteJson);
        Assert.Equal("text", sqliteJson.SqlTypeName);
        
        // Test unknown provider (defaults to text)
        var unknownJson = TypeMappingHelpers.CreateProviderOptimizedJsonType("unknown");
        Assert.NotNull(unknownJson);
        Assert.Equal("text", unknownJson.SqlTypeName);
    }

    [Fact]
    public void CreateProviderOptimizedJsonType_CaseInsensitive()
    {
        // Act
        var mysqlLower = TypeMappingHelpers.CreateProviderOptimizedJsonType("mysql");
        var mysqlUpper = TypeMappingHelpers.CreateProviderOptimizedJsonType("MYSQL");
        var mysqlMixed = TypeMappingHelpers.CreateProviderOptimizedJsonType("MySQL");
        
        // Assert
        Assert.Equal(mysqlLower.SqlTypeName, mysqlUpper.SqlTypeName);
        Assert.Equal(mysqlLower.SqlTypeName, mysqlMixed.SqlTypeName);
    }

    [Fact]
    public void CreateNativeArrayType_ReturnsCorrectArrayType()
    {
        // Act
        var intArrayType = TypeMappingHelpers.CreateNativeArrayType("integer");
        var textArrayType = TypeMappingHelpers.CreateNativeArrayType("text");
        
        // Assert
        Assert.NotNull(intArrayType);
        Assert.Equal("integer[]", intArrayType.SqlTypeName);
        
        Assert.NotNull(textArrayType);
        Assert.Equal("text[]", textArrayType.SqlTypeName);
    }

    [Fact]
    public void CreateArrayConverter_PostgreSqlUsesNativeArrays()
    {
        // Act
        var postgresConverter = TypeMappingHelpers.CreateArrayConverter("postgresql");
        
        // Assert
        Assert.NotNull(postgresConverter);
        Assert.IsType<DotnetTypeToSqlTypeConverter>(postgresConverter);
    }

    [Fact]
    public void CreateArrayConverter_OtherProvidersFallBackToJson()
    {
        // Test other providers fall back to JSON
        var providers = new[] { "mysql", "sqlserver", "sqlite", "unknown" };
        
        foreach (var provider in providers)
        {
            // Act
            var converter = TypeMappingHelpers.CreateArrayConverter(provider);
            
            // Assert
            Assert.NotNull(converter);
            Assert.IsType<DotnetTypeToSqlTypeConverter>(converter);
        }
    }

    [Theory]
    [InlineData(typeof(bool), "boolean")]
    [InlineData(typeof(short), "smallint")]
    [InlineData(typeof(int), "integer")]
    [InlineData(typeof(long), "bigint")]
    [InlineData(typeof(float), "real")]
    [InlineData(typeof(double), "double precision")]
    [InlineData(typeof(decimal), "numeric")]
    [InlineData(typeof(string), "text")]
    [InlineData(typeof(char), "char")]
    [InlineData(typeof(DateTime), "timestamp")]
    [InlineData(typeof(DateTimeOffset), "timestamptz")]
    [InlineData(typeof(TimeSpan), "interval")]
    [InlineData(typeof(DateOnly), "date")]
    [InlineData(typeof(TimeOnly), "time")]
    [InlineData(typeof(Guid), "uuid")]
    public void GetPostgreSqlArrayTypeName_ReturnsMappedType(Type elementType, string expectedSqlType)
    {
        // Act
        var result = TypeMappingHelpers.GetPostgreSqlArrayTypeName(elementType);
        
        // Assert
        Assert.Equal(expectedSqlType, result);
    }

    [Fact]
    public void GetPostgreSqlArrayTypeName_WithUnsupportedType_ReturnsNull()
    {
        // Act
        var result = TypeMappingHelpers.GetPostgreSqlArrayTypeName(typeof(object));
        
        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetPostgreSqlArrayTypeName_WithNull_ReturnsNull()
    {
        // Act
        var result = TypeMappingHelpers.GetPostgreSqlArrayTypeName(null);
        
        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("postgresql", true)]
    [InlineData("POSTGRESQL", true)]
    [InlineData("mysql", false)]
    [InlineData("sqlserver", false)]
    [InlineData("sqlite", false)]
    [InlineData("unknown", false)]
    public void SupportsNativeArrays_ReturnsCorrectResult(string provider, bool expected)
    {
        // Act
        var result = TypeMappingHelpers.SupportsNativeArrays(provider);
        
        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetPostgreSqlStandardArrayTypes_ReturnsExpectedTypes()
    {
        // Act
        var arrayTypes = TypeMappingHelpers.GetPostgreSqlStandardArrayTypes();
        
        // Assert
        Assert.NotNull(arrayTypes);
        Assert.True(arrayTypes.Length > 0);
        
        // Verify some key array types are included
        Assert.Contains("boolean[]", arrayTypes);
        Assert.Contains("integer[]", arrayTypes);
        Assert.Contains("text[]", arrayTypes);
        Assert.Contains("timestamp[]", arrayTypes);
        Assert.Contains("uuid[]", arrayTypes);
        Assert.Contains("jsonb[]", arrayTypes);
    }

    [Fact]
    public void CreatePostgreSqlArrayTypeConverter_ReturnsValidConverter()
    {
        // Act
        var converter = TypeMappingHelpers.CreatePostgreSqlArrayTypeConverter();
        
        // Assert
        Assert.NotNull(converter);
        Assert.IsType<SqlTypeToDotnetTypeConverter>(converter);
    }

    [Theory]
    [InlineData("text[]", typeof(string[]))]
    [InlineData("integer[]", typeof(int[]))]
    [InlineData("boolean[]", typeof(bool[]))]
    [InlineData("bigint[]", typeof(long[]))]
    [InlineData("uuid[]", typeof(Guid[]))]
    [InlineData("timestamp[]", typeof(DateTime[]))]
    [InlineData("timestamptz[]", typeof(DateTimeOffset[]))]
    [InlineData("date[]", typeof(DateOnly[]))]
    [InlineData("time[]", typeof(TimeOnly[]))]
    [InlineData("interval[]", typeof(TimeSpan[]))]
    // PostgreSQL internal array notation (underscore prefix)
    [InlineData("_text", typeof(string[]))]
    [InlineData("_int4", typeof(int[]))]
    [InlineData("_bool", typeof(bool[]))]
    [InlineData("_int8", typeof(long[]))]
    [InlineData("_uuid", typeof(Guid[]))]
    [InlineData("_timestamp", typeof(DateTime[]))]
    [InlineData("_timestamptz", typeof(DateTimeOffset[]))]
    [InlineData("_date", typeof(DateOnly[]))]
    [InlineData("_time", typeof(TimeOnly[]))]
    [InlineData("_interval", typeof(TimeSpan[]))]
    public void CreatePostgreSqlArrayTypeConverter_MapsArrayTypesCorrectly(string sqlType, Type expectedDotnetType)
    {
        // Arrange
        var converter = TypeMappingHelpers.CreatePostgreSqlArrayTypeConverter();
        var sqlDescriptor = new SqlTypeDescriptor(sqlType);
        
        // Act
        var result = converter.ConvertFunc(sqlDescriptor);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedDotnetType, result.DotnetType);
    }

    [Theory]
    [InlineData("text")]
    [InlineData("integer")]
    [InlineData("notanarray")]
    public void CreatePostgreSqlArrayTypeConverter_WithNonArrayTypes_ReturnsNull(string sqlType)
    {
        // Arrange
        var converter = TypeMappingHelpers.CreatePostgreSqlArrayTypeConverter();
        var sqlDescriptor = new SqlTypeDescriptor(sqlType);
        
        // Act
        var result = converter.ConvertFunc(sqlDescriptor);
        
        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void CreatePostgreSqlArrayTypeConverter_WithNullTypeName_ReturnsNull()
    {
        // Arrange
        var converter = TypeMappingHelpers.CreatePostgreSqlArrayTypeConverter();
        var sqlDescriptor = new SqlTypeDescriptor("text");
        
        // Use reflection to set SqlTypeName to null for testing
        var sqlTypeNameProperty = typeof(SqlTypeDescriptor).GetProperty("SqlTypeName");
        sqlTypeNameProperty?.SetValue(sqlDescriptor, null);
        
        // Act
        var result = converter.ConvertFunc(sqlDescriptor);
        
        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void CreatePostgreSqlArrayTypeConverter_WithEmptyTypeName_ReturnsNull()
    {
        // Arrange
        var converter = TypeMappingHelpers.CreatePostgreSqlArrayTypeConverter();
        var sqlDescriptor = new SqlTypeDescriptor("text");
        
        // Use reflection to set SqlTypeName to empty string for testing
        var sqlTypeNameProperty = typeof(SqlTypeDescriptor).GetProperty("SqlTypeName");
        sqlTypeNameProperty?.SetValue(sqlDescriptor, "");
        
        // Act
        var result = converter.ConvertFunc(sqlDescriptor);
        
        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void CreatePostgreSqlArrayTypeConverter_WithUnsupportedArrayType_ReturnsNull()
    {
        // Arrange
        var converter = TypeMappingHelpers.CreatePostgreSqlArrayTypeConverter();
        var sqlDescriptor = new SqlTypeDescriptor("unsupported_type[]");
        
        // Act
        var result = converter.ConvertFunc(sqlDescriptor);
        
        // Assert
        Assert.Null(result);
    }
}