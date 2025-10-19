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
}
