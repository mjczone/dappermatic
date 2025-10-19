// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.Converters;
using MJCZone.DapperMatic.Providers;
using MJCZone.DapperMatic.Providers.MySql;
using MJCZone.DapperMatic.Providers.PostgreSql;
using MJCZone.DapperMatic.Providers.Sqlite;
using MJCZone.DapperMatic.Providers.SqlServer;
using Xunit.Abstractions;

namespace MJCZone.DapperMatic.Tests;

/// <summary>
/// Tests to verify type mapping consistency across all database providers after the consolidation work.
/// These tests ensure that the StandardTypeMapBase merge maintained consistent behavior.
/// </summary>
public class TypeMappingConsistencyTests : TestBase
{
    public TypeMappingConsistencyTests(ITestOutputHelper output)
        : base(output) { }

    [Fact]
    public void Should_require_decimal_types_map_consistently_across_providers()
    {
        // Arrange
        var providers = new IDbProviderTypeMap[]
        {
            new SqlServerProviderTypeMap(),
            new MySqlProviderTypeMap(),
            new PostgreSqlProviderTypeMap(),
            new SqliteProviderTypeMap(),
        };

        foreach (var provider in providers)
        {
            var providerName = provider.GetType().Name;

            // Act - Test default decimal mapping
            var success = provider.TryGetProviderSqlTypeMatchingDotnetType(
                typeof(decimal),
                out var decimalType
            );

            // Assert
            Assert.True(success, $"{providerName} should handle decimal types");
            Assert.NotNull(decimalType);

            var typeLower = decimalType.BaseTypeName.ToLowerInvariant();
            Assert.True(
                typeLower.Contains("decimal") || typeLower.Contains("numeric"),
                $"{providerName} should use decimal/numeric type: {decimalType.SqlTypeName}"
            );
        }

        // Test precise decimal mapping using DotnetTypeDescriptor
        var preciseDecimalDescriptor = new DotnetTypeDescriptor(typeof(decimal))
        {
            Precision = 10,
            Scale = 2,
        };

        var sqlServerProvider = new SqlServerProviderTypeMap();
        var mysqlProvider = new MySqlProviderTypeMap();

        var sqlServerPrecise = sqlServerProvider.TryGetProviderSqlTypeMatchingDotnetType(
            preciseDecimalDescriptor,
            out var sqlServerPreciseType
        );
        var mysqlPrecise = mysqlProvider.TryGetProviderSqlTypeMatchingDotnetType(
            preciseDecimalDescriptor,
            out var mysqlPreciseType
        );

        Assert.True(sqlServerPrecise);
        Assert.True(mysqlPrecise);
        Assert.NotNull(sqlServerPreciseType);
        Assert.NotNull(mysqlPreciseType);

        // Both should include precision and scale in the type definition
        Assert.Contains("(10,2)", sqlServerPreciseType.SqlTypeName);
        Assert.Contains("(10,2)", mysqlPreciseType.SqlTypeName);
    }

    [Fact]
    public void Should_require_string_types_map_to_appropriate_text_types()
    {
        // Arrange
        var providers = new IDbProviderTypeMap[]
        {
            new SqlServerProviderTypeMap(),
            new MySqlProviderTypeMap(),
            new PostgreSqlProviderTypeMap(),
            new SqliteProviderTypeMap(),
        };

        foreach (var provider in providers)
        {
            var providerName = provider.GetType().Name;

            // Act
            var success = provider.TryGetProviderSqlTypeMatchingDotnetType(
                typeof(string),
                out var stringType
            );

            // Assert
            Assert.True(success, $"{providerName} should handle string types");
            Assert.NotNull(stringType);

            var typeLower = stringType.BaseTypeName.ToLowerInvariant();
            Assert.True(
                typeLower.Contains("varchar")
                    || typeLower.Contains("text")
                    || typeLower.Contains("char")
                    || typeLower.Contains("nvarchar"),
                $"{providerName} should use string-based type: {stringType.SqlTypeName}"
            );
        }
    }

    [Fact]
    public void Should_require_boolean_types_map_to_provider_specific_boolean_types()
    {
        // Arrange
        var providers = new Dictionary<string, IDbProviderTypeMap>
        {
            ["SqlServer"] = new SqlServerProviderTypeMap(),
            ["MySQL"] = new MySqlProviderTypeMap(),
            ["PostgreSQL"] = new PostgreSqlProviderTypeMap(),
            ["SQLite"] = new SqliteProviderTypeMap(),
        };

        foreach (var (providerName, provider) in providers)
        {
            // Act
            var success = provider.TryGetProviderSqlTypeMatchingDotnetType(
                typeof(bool),
                out var booleanType
            );

            // Assert
            Assert.True(success, $"{providerName} should handle boolean types");
            Assert.NotNull(booleanType);

            var typeLower = booleanType.BaseTypeName.ToLowerInvariant();

            // Each provider should use its appropriate boolean type
            switch (providerName)
            {
                case "SqlServer":
                    Assert.Equal("bit", typeLower);
                    break;
                case "MySQL":
                    Assert.True(typeLower.Contains("bool") || typeLower.Contains("tinyint"));
                    break;
                case "PostgreSQL":
                    Assert.Equal("boolean", typeLower);
                    break;
                case "SQLite":
                    Assert.True(typeLower.Contains("bool") || typeLower.Contains("integer"));
                    break;
            }
        }
    }

    [Fact]
    public void Should_require_guid_types_map_consistently_based_on_provider_capabilities()
    {
        // Arrange
        var providers = new Dictionary<string, IDbProviderTypeMap>
        {
            ["SqlServer"] = new SqlServerProviderTypeMap(),
            ["MySQL"] = new MySqlProviderTypeMap(),
            ["PostgreSQL"] = new PostgreSqlProviderTypeMap(),
            ["SQLite"] = new SqliteProviderTypeMap(),
        };

        foreach (var (providerName, provider) in providers)
        {
            // Act
            var success = provider.TryGetProviderSqlTypeMatchingDotnetType(
                typeof(Guid),
                out var guidType
            );

            // Assert
            Assert.True(success, $"{providerName} should handle GUID types");
            Assert.NotNull(guidType);

            var typeLower = guidType.BaseTypeName.ToLowerInvariant();

            // Verify provider-specific GUID handling
            switch (providerName)
            {
                case "SqlServer":
                    Assert.Equal("uniqueidentifier", typeLower);
                    break;
                case "MySQL":
                    // MySQL should use CHAR(36) for GUIDs
                    Assert.True(typeLower.Contains("char") && guidType.SqlTypeName.Contains("36"));
                    break;
                case "PostgreSQL":
                    Assert.Equal("uuid", typeLower);
                    break;
                case "SQLite":
                    // SQLite should use TEXT for GUIDs
                    Assert.True(typeLower.Contains("text") || typeLower.Contains("char"));
                    break;
            }
        }
    }

    [Fact]
    public void Should_require_date_time_types_map_to_provider_specific_date_time_types()
    {
        // Arrange
        var providers = new IDbProviderTypeMap[]
        {
            new SqlServerProviderTypeMap(),
            new MySqlProviderTypeMap(),
            new PostgreSqlProviderTypeMap(),
            new SqliteProviderTypeMap(),
        };

        foreach (var provider in providers)
        {
            var providerName = provider.GetType().Name;

            // Act
            var success = provider.TryGetProviderSqlTypeMatchingDotnetType(
                typeof(DateTime),
                out var dateTimeType
            );

            // Assert
            Assert.True(success, $"{providerName} should handle DateTime types");
            Assert.NotNull(dateTimeType);

            var typeLower = dateTimeType.BaseTypeName.ToLowerInvariant();

            // Each provider should use appropriate DateTime types
            Assert.True(
                typeLower.Contains("datetime")
                    || typeLower.Contains("timestamp")
                    || typeLower.Contains("date")
                    || typeLower.Contains("text"), // SQLite might use TEXT
                $"{providerName} DateTime type should be appropriate: {dateTimeType.SqlTypeName}"
            );
        }
    }

    [Fact]
    public void Should_require_numeric_types_map_to_appropriate_provider_types()
    {
        // Arrange
        var providers = new IDbProviderTypeMap[]
        {
            new SqlServerProviderTypeMap(),
            new MySqlProviderTypeMap(),
            new PostgreSqlProviderTypeMap(),
            new SqliteProviderTypeMap(),
        };

        var numericTypes = new[]
        {
            typeof(int),
            typeof(long),
            typeof(short),
            typeof(byte),
            typeof(double),
            typeof(float),
        };

        foreach (var provider in providers)
        {
            var providerName = provider.GetType().Name;

            foreach (var numericType in numericTypes)
            {
                // Act
                var success = provider.TryGetProviderSqlTypeMatchingDotnetType(
                    numericType,
                    out var sqlType
                );

                // Assert
                Assert.True(success, $"{providerName} should handle {numericType.Name}");
                Assert.NotNull(sqlType);

                var typeLower = sqlType.BaseTypeName.ToLowerInvariant();

                // Should map to some kind of numeric type
                Assert.True(
                    typeLower.Contains("int")
                        || typeLower.Contains("bigint")
                        || typeLower.Contains("smallint")
                        || typeLower.Contains("tinyint")
                        || typeLower.Contains("float")
                        || typeLower.Contains("real")
                        || typeLower.Contains("double")
                        || typeLower.Contains("numeric")
                        || typeLower.Contains("decimal"),
                    $"{providerName} should map {numericType.Name} to numeric type: {sqlType.SqlTypeName}"
                );
            }
        }
    }

    [Fact]
    public void Should_require_array_types_map_consistently_based_on_provider_capabilities()
    {
        // Arrange
        var providers = new Dictionary<string, IDbProviderTypeMap>
        {
            ["SqlServer"] = new SqlServerProviderTypeMap(),
            ["MySQL"] = new MySqlProviderTypeMap(),
            ["PostgreSQL"] = new PostgreSqlProviderTypeMap(),
            ["SQLite"] = new SqliteProviderTypeMap(),
        };

        foreach (var (providerName, provider) in providers)
        {
            // Act
            var success = provider.TryGetProviderSqlTypeMatchingDotnetType(
                typeof(int[]),
                out var arrayType
            );

            // Assert
            Assert.True(success, $"{providerName} should handle array types");
            Assert.NotNull(arrayType);

            var typeLower = arrayType.BaseTypeName.ToLowerInvariant();

            // PostgreSQL should use native arrays, others should use JSON/TEXT
            if (providerName == "PostgreSQL")
            {
                // PostgreSQL should support native arrays or JSON
                Assert.True(
                    typeLower.Contains("integer[]")
                        || typeLower.Contains("jsonb")
                        || typeLower.Contains("json"),
                    $"PostgreSQL should use native arrays or JSON: {arrayType.SqlTypeName}"
                );
            }
            else
            {
                // Other providers should serialize to JSON or TEXT
                Assert.True(
                    typeLower.Contains("json")
                        || typeLower.Contains("text")
                        || typeLower.Contains("varchar")
                        || typeLower.Contains("clob"),
                    $"{providerName} should serialize arrays to JSON/TEXT: {arrayType.SqlTypeName}"
                );
            }
        }
    }

    [Fact]
    public void Should_require_collection_types_map_to_json_or_text_consistently()
    {
        // Arrange
        var providers = new IDbProviderTypeMap[]
        {
            new SqlServerProviderTypeMap(),
            new MySqlProviderTypeMap(),
            new PostgreSqlProviderTypeMap(),
            new SqliteProviderTypeMap(),
        };

        foreach (var provider in providers)
        {
            var providerName = provider.GetType().Name;

            // Act
            var success = provider.TryGetProviderSqlTypeMatchingDotnetType(
                typeof(List<string>),
                out var collectionType
            );

            // Assert
            Assert.True(success, $"{providerName} should handle collection types");
            Assert.NotNull(collectionType);

            var typeLower = collectionType.BaseTypeName.ToLowerInvariant();

            // Collections should serialize to JSON or TEXT
            Assert.True(
                typeLower.Contains("json")
                    || typeLower.Contains("text")
                    || typeLower.Contains("varchar")
                    || typeLower.Contains("clob"),
                $"{providerName} should serialize collections to JSON/TEXT: {collectionType.SqlTypeName}"
            );
        }
    }

    [Fact]
    public void Should_require_enum_types_map_to_string_types_consistently()
    {
        // Arrange
        var providers = new IDbProviderTypeMap[]
        {
            new SqlServerProviderTypeMap(),
            new MySqlProviderTypeMap(),
            new PostgreSqlProviderTypeMap(),
            new SqliteProviderTypeMap(),
        };

        foreach (var provider in providers)
        {
            var providerName = provider.GetType().Name;

            // Act
            var success = provider.TryGetProviderSqlTypeMatchingDotnetType(
                typeof(DayOfWeek),
                out var enumType
            );

            // Assert
            Assert.True(success, $"{providerName} should handle enum types");
            Assert.NotNull(enumType);

            // Enums should map to string types with reasonable length
            var typeLower = enumType.BaseTypeName.ToLowerInvariant();
            Assert.True(
                typeLower.Contains("varchar")
                    || typeLower.Contains("text")
                    || typeLower.Contains("char"),
                $"{providerName} enum type should be string-based: {enumType.SqlTypeName}"
            );
        }
    }

    [Fact]
    public void Should_require_type_mapping_helpers_produce_consistent_results()
    {
        // Test that the consolidation hasn't broken the helper methods

        // Test decimal type creation
        var decimalType = TypeMappingHelpers.CreateDecimalType("decimal", 10, 2);
        Assert.Equal("decimal(10,2)", decimalType.SqlTypeName);
        Assert.Equal(10, decimalType.Precision);
        Assert.Equal(2, decimalType.Scale);

        // Test string type creation
        var stringType = TypeMappingHelpers.CreateStringType("varchar", 255);
        Assert.Equal("varchar(255)", stringType.SqlTypeName);
        Assert.Equal(255, stringType.Length);

        // Test simple type creation
        var simpleType = TypeMappingHelpers.CreateSimpleType("int");
        Assert.Equal("int", simpleType.SqlTypeName);

        // Test GUID string type creation
        var guidType = TypeMappingHelpers.CreateGuidStringType("char", false, true);
        Assert.Equal("char(36)", guidType.SqlTypeName);
        Assert.Equal(36, guidType.Length);
        Assert.False(guidType.IsUnicode);
        Assert.True(guidType.IsFixedLength);
    }

    [Fact]
    public void Should_require_all_providers_use_standard_converters_from_base_class()
    {
        // This test verifies that all providers are now using the standard converters
        // from the merged DbProviderTypeMapBase class

        var providers = new IDbProviderTypeMap[]
        {
            new SqlServerProviderTypeMap(),
            new MySqlProviderTypeMap(),
            new PostgreSqlProviderTypeMap(),
            new SqliteProviderTypeMap(),
        };

        // Test common types that should be handled by standard converters
        var commonTypes = new[]
        {
            typeof(bool),
            typeof(int),
            typeof(string),
            typeof(DateTime),
            typeof(Guid),
            typeof(byte[]),
            typeof(decimal),
        };

        foreach (var provider in providers)
        {
            var providerName = provider.GetType().Name;

            foreach (var type in commonTypes)
            {
                // Act
                var success = provider.TryGetProviderSqlTypeMatchingDotnetType(
                    type,
                    out var sqlType
                );

                // Assert
                Assert.True(
                    success,
                    $"{providerName} should handle {type.Name} using standard converters"
                );
                Assert.NotNull(sqlType);
                Assert.NotEmpty(sqlType.SqlTypeName);
            }
        }
    }
}
