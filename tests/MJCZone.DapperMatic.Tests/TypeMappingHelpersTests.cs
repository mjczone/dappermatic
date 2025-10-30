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
    public TypeMappingHelpersTests(ITestOutputHelper output)
        : base(output) { }

    [Theory]
    // Simple types - should include namespace but no assembly info
    [InlineData(typeof(int), "System.Int32")]
    [InlineData(typeof(string), "System.String")]
    [InlineData(typeof(bool), "System.Boolean")]
    [InlineData(typeof(decimal), "System.Decimal")]
    [InlineData(typeof(DateTime), "System.DateTime")]
    [InlineData(typeof(DateTimeOffset), "System.DateTimeOffset")]
    [InlineData(typeof(TimeSpan), "System.TimeSpan")]
    [InlineData(typeof(Guid), "System.Guid")]
    [InlineData(typeof(byte), "System.Byte")]
    [InlineData(typeof(short), "System.Int16")]
    [InlineData(typeof(long), "System.Int64")]
    [InlineData(typeof(float), "System.Single")]
    [InlineData(typeof(double), "System.Double")]
    // Arrays - should show element type with [] notation
    [InlineData(typeof(int[]), "System.Int32[]")]
    [InlineData(typeof(string[]), "System.String[]")]
    [InlineData(typeof(byte[]), "System.Byte[]")]
    // Multi-dimensional arrays
    [InlineData(typeof(int[,]), "System.Int32[,]")]
    [InlineData(typeof(string[,,]), "System.String[,,]")]
    // Nullable types - generic with one parameter
    [InlineData(typeof(int?), "System.Nullable<System.Int32>")]
    [InlineData(typeof(bool?), "System.Nullable<System.Boolean>")]
    [InlineData(typeof(DateTime?), "System.Nullable<System.DateTime>")]
    [InlineData(typeof(Guid?), "System.Nullable<System.Guid>")]
    // Generic collections - single type parameter
    [InlineData(typeof(List<int>), "System.Collections.Generic.List<System.Int32>")]
    [InlineData(typeof(List<string>), "System.Collections.Generic.List<System.String>")]
    [InlineData(typeof(HashSet<int>), "System.Collections.Generic.HashSet<System.Int32>")]
    [InlineData(typeof(IEnumerable<string>), "System.Collections.Generic.IEnumerable<System.String>")]
    [InlineData(typeof(IList<int>), "System.Collections.Generic.IList<System.Int32>")]
    [InlineData(typeof(ICollection<string>), "System.Collections.Generic.ICollection<System.String>")]
    // Generic collections - two type parameters
    [InlineData(typeof(Dictionary<string, int>), "System.Collections.Generic.Dictionary<System.String, System.Int32>")]
    [InlineData(typeof(Dictionary<int, string>), "System.Collections.Generic.Dictionary<System.Int32, System.String>")]
    [InlineData(
        typeof(KeyValuePair<string, int>),
        "System.Collections.Generic.KeyValuePair<System.String, System.Int32>"
    )]
    // Nested generics
    [InlineData(
        typeof(List<List<int>>),
        "System.Collections.Generic.List<System.Collections.Generic.List<System.Int32>>"
    )]
    [InlineData(
        typeof(Dictionary<string, List<int>>),
        "System.Collections.Generic.Dictionary<System.String, System.Collections.Generic.List<System.Int32>>"
    )]
    [InlineData(typeof(List<int?>), "System.Collections.Generic.List<System.Nullable<System.Int32>>")]
    // Arrays of generics
    [InlineData(typeof(List<int>[]), "System.Collections.Generic.List<System.Int32>[]")]
    [InlineData(
        typeof(Dictionary<string, int>[]),
        "System.Collections.Generic.Dictionary<System.String, System.Int32>[]"
    )]
    public void Should_expect_get_full_type_name_returns_type_name_without_assembly_info(
        Type type,
        string expectedFullName
    )
    {
        // Act
        var result = type.GetFullTypeName();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedFullName, result);

        // Verify no assembly information is included
        // Check for actual assembly info markers (Version=, Culture=, PublicKeyToken=)
        // Don't check for assembly names with commas as they could be part of generic type parameters
        Assert.DoesNotContain("Version=", result);
        Assert.DoesNotContain("Culture=", result);
        Assert.DoesNotContain("PublicKeyToken=", result);
    }

    [Fact]
    public void Should_expect_get_full_type_name_with_null_type_returns_null()
    {
        // Act
        Type? nullType = null;
        var result = nullType.GetFullTypeName();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Should_create_decimal_type_with_defaults_uses_default_precision_and_scale()
    {
        // Act
        var result = TypeMappingHelpers.CreateDecimalType("decimal");

        // Assert
        Assert.Equal("decimal(16,4)", result.SqlTypeName);
        Assert.Equal(16, result.Precision);
        Assert.Equal(4, result.Scale);
    }

    [Fact]
    public void Should_create_decimal_type_with_custom_values_uses_provided_values()
    {
        // Act
        var result = TypeMappingHelpers.CreateDecimalType("numeric", 10, 2);

        // Assert
        Assert.Equal("numeric(10,2)", result.SqlTypeName);
        Assert.Equal(10, result.Precision);
        Assert.Equal(2, result.Scale);
    }

    [Fact]
    public void Should_create_string_type_with_defaults_uses_default_length()
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
    public void Should_create_string_type_with_max_length_uses_max_syntax()
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
    public void Should_create_string_type_with_unicode_and_fixed_length_sets_flags()
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
    public void Should_create_guid_string_type_returns_correct_configuration()
    {
        // Act
        var result = TypeMappingHelpers.CreateGuidStringType("char", isUnicode: false, isFixedLength: true);

        // Assert
        Assert.Equal("char(36)", result.SqlTypeName);
        Assert.Equal(36, result.Length);
        Assert.False(result.IsUnicode);
        Assert.True(result.IsFixedLength);
    }

    [Theory]
    [InlineData(typeof(string), false)]
    [InlineData(typeof(int), false)]
    public void Should_expect_is_net_topology_suite_geometry_type_with_non_geometry_types_returns_false(
        Type type,
        bool expected
    )
    {
        // Act
        var result = TypeMappingHelpers.IsNetTopologySuiteGeometryType(type);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Should_expect_is_net_topology_suite_geometry_type_with_null_returns_false()
    {
        // Act
        var result = TypeMappingHelpers.IsNetTopologySuiteGeometryType(null);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(typeof(string), null)]
    [InlineData(typeof(int), null)]
    public void Should_expect_get_geometry_type_name_with_non_geometry_types_returns_null(Type type, string? expected)
    {
        // Act
        var result = TypeMappingHelpers.GetGeometryTypeName(type);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Should_expect_get_geometry_type_name_with_null_returns_null()
    {
        // Act
        var result = TypeMappingHelpers.GetGeometryTypeName(null);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Should_create_simple_type_returns_basic_descriptor()
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
    public void Should_create_date_time_type_without_precision_returns_basic_type()
    {
        // Act
        var result = TypeMappingHelpers.CreateDateTimeType("datetime");

        // Assert
        Assert.Equal("datetime", result.SqlTypeName);
        Assert.Null(result.Precision);
    }

    [Fact]
    public void Should_create_date_time_type_with_precision_includes_precision()
    {
        // Act
        var result = TypeMappingHelpers.CreateDateTimeType("datetime2", 3);

        // Assert
        Assert.Equal("datetime2(3)", result.SqlTypeName);
        Assert.Equal(3, result.Precision);
    }

    [Fact]
    public void Should_create_binary_type_without_length_returns_basic_type()
    {
        // Act
        var result = TypeMappingHelpers.CreateBinaryType("binary");

        // Assert
        Assert.Equal("binary", result.SqlTypeName);
        Assert.Null(result.Length);
        Assert.False(result.IsFixedLength);
    }

    [Fact]
    public void Should_create_binary_type_with_max_length_uses_max_syntax()
    {
        // Act
        var result = TypeMappingHelpers.CreateBinaryType("varbinary", TypeMappingDefaults.MaxLength, false);

        // Assert
        Assert.Equal("varbinary(max)", result.SqlTypeName);
        Assert.Null(result.Length);
        Assert.False(result.IsFixedLength);
    }

    [Fact]
    public void Should_create_binary_type_with_specific_length_uses_length()
    {
        // Act
        var result = TypeMappingHelpers.CreateBinaryType("binary", 50, true);

        // Assert
        Assert.Equal("binary(50)", result.SqlTypeName);
        Assert.Equal(50, result.Length);
        Assert.True(result.IsFixedLength);
    }

    [Fact]
    public void Should_create_json_type_as_native_json_returns_basic_type()
    {
        // Act
        var result = TypeMappingHelpers.CreateJsonType("json", false);

        // Assert
        Assert.Equal("json", result.SqlTypeName);
        Assert.Null(result.Length);
    }

    [Fact]
    public void Should_create_json_type_as_text_sets_max_length()
    {
        // Act
        var result = TypeMappingHelpers.CreateJsonType("nvarchar(max)", true);

        // Assert
        Assert.Equal("nvarchar(max)", result.SqlTypeName);
        Assert.Equal(TypeMappingDefaults.MaxLength, result.Length);
    }

    [Fact]
    public void Should_create_geometry_type_without_type_name_returns_basic_type()
    {
        // Act
        var result = TypeMappingHelpers.CreateGeometryType("geometry");

        // Assert
        Assert.Equal("geometry", result.SqlTypeName);
    }

    [Fact]
    public void Should_create_geometry_type_with_type_name_returns_basic_type()
    {
        // Act
        var result = TypeMappingHelpers.CreateGeometryType("geometry", "Point");

        // Assert
        Assert.Equal("geometry", result.SqlTypeName);
    }

    [Fact]
    public void Should_create_lob_type_sets_max_length_and_unicode()
    {
        // Act
        var result = TypeMappingHelpers.CreateLobType("text", true);

        // Assert
        Assert.Equal("text", result.SqlTypeName);
        Assert.Equal(TypeMappingDefaults.MaxLength, result.Length);
        Assert.True(result.IsUnicode);
    }

    [Fact]
    public void Should_create_array_type_returns_array_descriptor()
    {
        // Act
        var result = TypeMappingHelpers.CreateArrayType("integer[]", "integer");

        // Assert
        Assert.Equal("integer[]", result.SqlTypeName);
    }

    [Fact]
    public void Should_create_precision_type_includes_precision()
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
    public void Should_expect_is_array_type_returns_correct_result(Type type, bool expected)
    {
        // Act
        var result = TypeMappingHelpers.IsArrayType(type);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Should_expect_is_array_type_with_null_returns_false()
    {
        // Act
        var result = TypeMappingHelpers.IsArrayType(null);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Should_expect_get_array_element_type_with_array_type_returns_element_type()
    {
        // Act
        var result = TypeMappingHelpers.GetArrayElementType(typeof(int[]));

        // Assert
        Assert.Equal(typeof(int), result);
    }

    [Fact]
    public void Should_expect_get_array_element_type_with_non_array_type_returns_null()
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
    public void Should_expect_is_generic_collection_type_returns_correct_result(Type type, bool expected)
    {
        // Act
        var result = TypeMappingHelpers.IsGenericCollectionType(type);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Should_expect_is_generic_collection_type_with_null_returns_false()
    {
        // Act
        var result = TypeMappingHelpers.IsGenericCollectionType(null);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Should_expect_get_collection_element_type_with_generic_collection_returns_element_type()
    {
        // Act
        var result = TypeMappingHelpers.GetCollectionElementType(typeof(List<string>));

        // Assert
        Assert.Equal(typeof(string), result);
    }

    [Fact]
    public void Should_expect_get_collection_element_type_with_non_generic_collection_returns_null()
    {
        // Act
        var result = TypeMappingHelpers.GetCollectionElementType(typeof(string[]));

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Should_expect_get_collection_element_type_with_null_returns_null()
    {
        // Act
        var result = TypeMappingHelpers.GetCollectionElementType(null);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Should_expect_get_standard_geometry_types_returns_filtered_array()
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
    public void Should_expect_get_sqlserver_geometry_types_returns_filtered_array()
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
    public void Should_expect_get_mysql_geometry_types_returns_filtered_array()
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
    public void Should_expect_get_postgresql_special_types_returns_filtered_array()
    {
        // Act
        var types = TypeMappingHelpers.GetPostgreSqlSpecialTypes();

        // Assert
        Assert.NotNull(types);
        Assert.IsType<Type[]>(types);

        // All returned types should be non-null
        Assert.True(types.All(t => t != null));
    }

    [Fact]
    public void Should_expect_get_geometry_types_for_provider_returns_correct_types_for_each_provider()
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
    public void Should_expect_get_geometry_types_for_provider_case_insensitive()
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
    public void Should_expect_get_standard_json_types_returns_expected_types()
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
    public void Should_create_json_converter_returns_correct_converter_for_each_provider()
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
    public void Should_create_json_converter_case_insensitive()
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
    public void Should_create_provider_optimized_json_type_returns_correct_type_for_each_provider()
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
    public void Should_create_provider_optimized_json_type_case_insensitive()
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
    public void Should_create_native_array_type_returns_correct_array_type()
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
    public void Should_create_array_converter_postgresql_uses_native_arrays()
    {
        // Act
        var postgresConverter = TypeMappingHelpers.CreateArrayConverter("postgresql");

        // Assert
        Assert.NotNull(postgresConverter);
        Assert.IsType<DotnetTypeToSqlTypeConverter>(postgresConverter);
    }

    [Fact]
    public void Should_create_array_converter_other_providers_fall_back_to_json()
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
    public void Should_expect_get_postgresql_array_type_name_returns_mapped_type(
        Type elementType,
        string expectedSqlType
    )
    {
        // Act
        var result = TypeMappingHelpers.GetPostgreSqlArrayTypeName(elementType);

        // Assert
        Assert.Equal(expectedSqlType, result);
    }

    [Fact]
    public void Should_expect_get_postgresql_array_type_name_with_unsupported_type_returns_null()
    {
        // Act
        var result = TypeMappingHelpers.GetPostgreSqlArrayTypeName(typeof(object));

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Should_expect_get_postgresql_array_type_name_with_null_returns_null()
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
    public void Should_require_native_arrays_returns_correct_result(string provider, bool expected)
    {
        // Act
        var result = TypeMappingHelpers.SupportsNativeArrays(provider);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Should_expect_get_postgresql_standard_array_types_returns_expected_types()
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
    public void Should_create_postgresql_array_type_converter_returns_valid_converter()
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
    public void Should_expect_postgresql_array_type_converter_maps_array_types_correctly(
        string sqlType,
        Type expectedDotnetType
    )
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
    public void Should_create_postgresql_array_type_converter_with_non_array_types_returns_null(string sqlType)
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
    public void Should_create_postgresql_array_type_converter_with_null_type_name_returns_null()
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
    public void Should_create_postgresql_array_type_converter_with_empty_type_name_returns_null()
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
    public void Should_create_postgresql_array_type_converter_with_unsupported_array_type_returns_null()
    {
        // Arrange
        var converter = TypeMappingHelpers.CreatePostgreSqlArrayTypeConverter();
        var sqlDescriptor = new SqlTypeDescriptor("unsupported_type[]");

        // Act
        var result = converter.ConvertFunc(sqlDescriptor);

        // Assert
        Assert.Null(result);
    }

    #region ParseProviderDataTypes Tests

    [Fact]
    public void Should_parse_provider_data_types_with_simple_types()
    {
        // Act
        var result = TypeMappingHelpers.ParseProviderDataTypes("{mysql:int,sqlserver:int}");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("int", result[DbProviderType.MySql]);
        Assert.Equal("int", result[DbProviderType.SqlServer]);
    }

    [Fact]
    public void Should_parse_provider_data_types_with_parameterized_types()
    {
        // Act
        var result = TypeMappingHelpers.ParseProviderDataTypes("{mysql:decimal(10,2),sqlserver:decimal(12,4)}");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("decimal(10,2)", result[DbProviderType.MySql]);
        Assert.Equal("decimal(12,4)", result[DbProviderType.SqlServer]);
    }

    [Fact]
    public void Should_parse_provider_data_types_with_mixed_parameterized_and_simple_types()
    {
        // This is the documented example from docs/guide/providers.md
        // Act
        var result = TypeMappingHelpers.ParseProviderDataTypes(
            "{sqlserver:money,mysql:decimal(19,4),postgresql:money,sqlite:real}"
        );

        // Assert
        Assert.Equal(4, result.Count);
        Assert.Equal("money", result[DbProviderType.SqlServer]);
        Assert.Equal("decimal(19,4)", result[DbProviderType.MySql]);
        Assert.Equal("money", result[DbProviderType.PostgreSql]);
        Assert.Equal("real", result[DbProviderType.Sqlite]);
    }

    [Fact]
    public void Should_parse_provider_data_types_with_array_types()
    {
        // Act
        var result = TypeMappingHelpers.ParseProviderDataTypes("{postgresql:integer[],mysql:json}");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("integer[]", result[DbProviderType.PostgreSql]);
        Assert.Equal("json", result[DbProviderType.MySql]);
    }

    [Fact]
    public void Should_parse_provider_data_types_with_varchar_types()
    {
        // Act
        var result = TypeMappingHelpers.ParseProviderDataTypes("{mysql:varchar(255),sqlserver:nvarchar(255)}");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("varchar(255)", result[DbProviderType.MySql]);
        Assert.Equal("nvarchar(255)", result[DbProviderType.SqlServer]);
    }

    [Fact]
    public void Should_parse_provider_data_types_with_semicolon_delimiter()
    {
        // Act
        var result = TypeMappingHelpers.ParseProviderDataTypes("{mysql:int;sqlserver:int}");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("int", result[DbProviderType.MySql]);
        Assert.Equal("int", result[DbProviderType.SqlServer]);
    }

    [Fact]
    public void Should_parse_provider_data_types_without_braces()
    {
        // Act
        var result = TypeMappingHelpers.ParseProviderDataTypes("mysql:int,sqlserver:int");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("int", result[DbProviderType.MySql]);
        Assert.Equal("int", result[DbProviderType.SqlServer]);
    }

    [Fact]
    public void Should_parse_provider_data_types_with_extra_whitespace()
    {
        // Act
        var result = TypeMappingHelpers.ParseProviderDataTypes("{ mysql : decimal(10,2) , sqlserver : decimal(12,4) }");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("decimal(10,2)", result[DbProviderType.MySql]);
        Assert.Equal("decimal(12,4)", result[DbProviderType.SqlServer]);
    }

    [Fact]
    public void Should_parse_provider_data_types_with_null_input_returns_empty_dictionary()
    {
        // Act
        var result = TypeMappingHelpers.ParseProviderDataTypes(null);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Should_parse_provider_data_types_with_empty_input_returns_empty_dictionary()
    {
        // Act
        var result = TypeMappingHelpers.ParseProviderDataTypes("");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Should_parse_provider_data_types_with_whitespace_input_returns_empty_dictionary()
    {
        // Act
        var result = TypeMappingHelpers.ParseProviderDataTypes("   ");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Should_parse_provider_data_types_skips_invalid_entries_without_colon()
    {
        // Act
        var result = TypeMappingHelpers.ParseProviderDataTypes("{mysql:int,invalid,sqlserver:int}");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("int", result[DbProviderType.MySql]);
        Assert.Equal("int", result[DbProviderType.SqlServer]);
    }

    [Fact]
    public void Should_parse_provider_data_types_skips_invalid_entries_with_empty_provider_name()
    {
        // Act
        var result = TypeMappingHelpers.ParseProviderDataTypes("{mysql:int,:emptyProvider,sqlserver:int}");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("int", result[DbProviderType.MySql]);
        Assert.Equal("int", result[DbProviderType.SqlServer]);
    }

    [Fact]
    public void Should_parse_provider_data_types_skips_invalid_entries_with_empty_type_name()
    {
        // Act
        var result = TypeMappingHelpers.ParseProviderDataTypes("{mysql:int,postgresql:,sqlserver:int}");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("int", result[DbProviderType.MySql]);
        Assert.Equal("int", result[DbProviderType.SqlServer]);
    }

    [Theory]
    [InlineData("mysql")]
    [InlineData("MySQL")]
    [InlineData("MYSQL")]
    [InlineData("maria")]
    [InlineData("mariadb")]
    public void Should_parse_provider_data_types_recognizes_mysql_variants(string providerName)
    {
        // Act
        var result = TypeMappingHelpers.ParseProviderDataTypes($"{{{providerName}:int}}");

        // Assert
        Assert.Single(result);
        Assert.Equal("int", result[DbProviderType.MySql]);
    }

    [Theory]
    [InlineData("postgres")]
    [InlineData("postgresql")]
    [InlineData("PostgreSQL")]
    [InlineData("pg")]
    [InlineData("PG")]
    public void Should_parse_provider_data_types_recognizes_postgresql_variants(string providerName)
    {
        // Act
        var result = TypeMappingHelpers.ParseProviderDataTypes($"{{{providerName}:int}}");

        // Assert
        Assert.Single(result);
        Assert.Equal("int", result[DbProviderType.PostgreSql]);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("SQLite")]
    [InlineData("SQLITE")]
    public void Should_parse_provider_data_types_recognizes_sqlite_variants(string providerName)
    {
        // Act
        var result = TypeMappingHelpers.ParseProviderDataTypes($"{{{providerName}:int}}");

        // Assert
        Assert.Single(result);
        Assert.Equal("int", result[DbProviderType.Sqlite]);
    }

    [Theory]
    [InlineData("sqlserver")]
    [InlineData("SQLServer")]
    [InlineData("SQLSERVER")]
    [InlineData("mssql")]
    [InlineData("MSSQL")]
    public void Should_parse_provider_data_types_recognizes_sqlserver_variants(string providerName)
    {
        // Act
        var result = TypeMappingHelpers.ParseProviderDataTypes($"{{{providerName}:int}}");

        // Assert
        Assert.Single(result);
        Assert.Equal("int", result[DbProviderType.SqlServer]);
    }

    [Fact]
    public void Should_parse_provider_data_types_with_numeric_precision_and_scale()
    {
        // Act
        var result = TypeMappingHelpers.ParseProviderDataTypes("{mysql:numeric(18,6),postgresql:numeric(18,6)}");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("numeric(18,6)", result[DbProviderType.MySql]);
        Assert.Equal("numeric(18,6)", result[DbProviderType.PostgreSql]);
    }

    [Fact]
    public void Should_parse_provider_data_types_from_existing_test_cases()
    {
        // This is from the actual test case in DatabaseMethodsTests.TableFactory.cs
        // Act
        var result = TypeMappingHelpers.ParseProviderDataTypes(
            "{sqlserver:nvarchar(max),mysql:longtext,postgresql:text,sqlite:text}"
        );

        // Assert
        Assert.Equal(4, result.Count);
        Assert.Equal("nvarchar(max)", result[DbProviderType.SqlServer]);
        Assert.Equal("longtext", result[DbProviderType.MySql]);
        Assert.Equal("text", result[DbProviderType.PostgreSql]);
        Assert.Equal("text", result[DbProviderType.Sqlite]);
    }

    #endregion

    #region SplitRespectingParentheses Tests

    [Fact]
    public void Should_split_respecting_parentheses_with_simple_string()
    {
        // Act
        var result = TypeMappingHelpers.SplitRespectingParentheses("mysql:int,sqlserver:int", ',');

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("mysql:int", result[0]);
        Assert.Equal("sqlserver:int", result[1]);
    }

    [Fact]
    public void Should_split_respecting_parentheses_preserves_commas_inside_parentheses()
    {
        // Act
        var result = TypeMappingHelpers.SplitRespectingParentheses("mysql:decimal(10,2),sqlserver:decimal(12,4)", ',');

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("mysql:decimal(10,2)", result[0]);
        Assert.Equal("sqlserver:decimal(12,4)", result[1]);
    }

    [Fact]
    public void Should_split_respecting_parentheses_preserves_brackets()
    {
        // Act
        var result = TypeMappingHelpers.SplitRespectingParentheses("postgresql:integer[],mysql:json", ',');

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("postgresql:integer[]", result[0]);
        Assert.Equal("mysql:json", result[1]);
    }

    [Fact]
    public void Should_split_respecting_parentheses_with_nested_parentheses()
    {
        // Act - hypothetical case with nested structure
        var result = TypeMappingHelpers.SplitRespectingParentheses("type1(a,b(c,d)),type2(x,y)", ',');

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("type1(a,b(c,d))", result[0]);
        Assert.Equal("type2(x,y)", result[1]);
    }

    [Fact]
    public void Should_split_respecting_parentheses_with_multiple_delimiters()
    {
        // Act
        var result = TypeMappingHelpers.SplitRespectingParentheses("mysql:int,sqlserver:int;postgresql:int", ',', ';');

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("mysql:int", result[0]);
        Assert.Equal("sqlserver:int", result[1]);
        Assert.Equal("postgresql:int", result[2]);
    }

    [Fact]
    public void Should_split_respecting_parentheses_trims_whitespace()
    {
        // Act
        var result = TypeMappingHelpers.SplitRespectingParentheses("mysql:int , sqlserver:int", ',');

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("mysql:int", result[0]);
        Assert.Equal("sqlserver:int", result[1]);
    }

    [Fact]
    public void Should_split_respecting_parentheses_with_empty_input_returns_empty_list()
    {
        // Act
        var result = TypeMappingHelpers.SplitRespectingParentheses("", ',');

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Should_split_respecting_parentheses_with_no_delimiters_returns_single_item()
    {
        // Act
        var result = TypeMappingHelpers.SplitRespectingParentheses("mysql:int", ',');

        // Assert
        Assert.Single(result);
        Assert.Equal("mysql:int", result[0]);
    }

    [Fact]
    public void Should_split_respecting_parentheses_handles_multiple_consecutive_delimiters()
    {
        // Act
        var result = TypeMappingHelpers.SplitRespectingParentheses("mysql:int,,sqlserver:int", ',');

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("mysql:int", result[0]);
        Assert.Equal("sqlserver:int", result[1]);
    }

    [Fact]
    public void Should_split_respecting_parentheses_with_mixed_brackets_and_parentheses()
    {
        // Act
        var result = TypeMappingHelpers.SplitRespectingParentheses("type1(a,b),type2[x,y],type3", ',');

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("type1(a,b)", result[0]);
        Assert.Equal("type2[x,y]", result[1]);
        Assert.Equal("type3", result[2]);
    }

    #endregion
}
