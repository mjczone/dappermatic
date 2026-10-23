// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Dapper;
using MJCZone.DapperMatic.DataAnnotations;
using MJCZone.DapperMatic.TypeMapping;

namespace MJCZone.DapperMatic.Tests;

/// <summary>
/// DapperMatic's member mapping replaces Dapper's for every type once initialized, so whatever the attributes do not
/// decide must still map the way Dapper would.
/// </summary>
public class DmColumnFallbackMapperTests
{
    [Fact]
    public void Snake_case_columns_map_when_dapper_is_set_to_match_underscores()
    {
        var previous = DefaultTypeMap.MatchNamesWithUnderscores;
        try
        {
            DefaultTypeMap.MatchNamesWithUnderscores = true;
            var map = new DmColumnFallbackMapper(typeof(SnakeCaseModel), new DapperMaticMappingOptions());

            Assert.Equal(nameof(SnakeCaseModel.FirstName), map.GetMember("first_name")?.Property?.Name);
            Assert.Equal(nameof(SnakeCaseModel.LastName), map.GetMember("last_name")?.Property?.Name);

            // An ignored property stays ignored however its column is spelled.
            Assert.Null(map.GetMember("internal_note"));
        }
        finally
        {
            DefaultTypeMap.MatchNamesWithUnderscores = previous;
        }
    }

    [Fact]
    public void Public_fields_map_as_they_do_in_dapper()
    {
        var map = new DmColumnFallbackMapper(typeof(FieldModel), new DapperMaticMappingOptions());

        Assert.Equal(nameof(FieldModel.Code), map.GetMember("code")?.Field?.Name);
    }

    public class SnakeCaseModel
    {
        public string FirstName { get; set; } = string.Empty;

        [DmColumn("last_name")]
        public string LastName { get; set; } = string.Empty;

        [DmIgnore]
        public string InternalNote { get; set; } = string.Empty;
    }

    public class FieldModel
    {
#pragma warning disable SA1401 // A public field is what this tests
        public string Code = string.Empty;
#pragma warning restore SA1401
    }
}

/// <summary>
/// Which collections keep Dapper's own behaviour, expanding for IN lists, instead of being stored as JSON.
/// </summary>
public class SimpleValueCollectionTests
{
    [Theory]
    [InlineData(typeof(int[]))]
    [InlineData(typeof(int?[]))]
    [InlineData(typeof(string[]))]
    [InlineData(typeof(List<int>))]
    [InlineData(typeof(IEnumerable<long>))]
    [InlineData(typeof(IReadOnlyList<Guid>))]
    [InlineData(typeof(HashSet<string>))]
    [InlineData(typeof(ISet<DateOnly>))]
    [InlineData(typeof(System.Collections.Immutable.ImmutableArray<decimal>))]
    [InlineData(typeof(List<DayOfWeek>))]
    public void Collections_of_simple_values_are_left_to_dapper(Type type)
    {
        Assert.True(DapperMaticTypeMapping.IsSimpleValueCollection(type));
    }

    [Theory]
    [InlineData(typeof(byte[]))]
    [InlineData(typeof(char[]))]
    [InlineData(typeof(string))]
    [InlineData(typeof(List<object>))]
    [InlineData(typeof(List<List<string>>))]
    [InlineData(typeof(List<Uri>))]
    [InlineData(typeof(Dictionary<string, string>))]
    [InlineData(typeof(List<SimpleValueCollectionTests>))]
    public void Other_collections_are_stored_as_json(Type type)
    {
        Assert.False(DapperMaticTypeMapping.IsSimpleValueCollection(type));
    }
}
