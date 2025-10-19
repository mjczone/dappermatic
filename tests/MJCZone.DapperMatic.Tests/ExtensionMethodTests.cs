// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Tests;

public class ExtensionMethodTests
{
    // tes the GetFriendlyName method
    [Theory]
    [InlineData(typeof(bool), "Boolean")]
    [InlineData(typeof(List<string>), "List<String>")]
    [InlineData(
        typeof(IEnumerable<IDictionary<string, List<int>>>),
        "IEnumerable<IDictionary<String, List<Int32>>>"
    )]
    public void Should_test_get_friendly_name(Type input, string expected)
    {
        var actual = input.GetFriendlyName();
        Assert.Equal(expected, actual);
    }

    // test the ToAlpha method
    [Theory]
    [InlineData("abc", "abc")]
    [InlineData("abc123", "abc")]
    [InlineData("abc123def", "abcdef")]
    [InlineData("abc123def456", "abcdef")]
    [InlineData("abc (&__-1234)123def456ghi", "abcdefghi")]
    [InlineData("abc (&__-1234)123def456ghi", "abc(&__)defghi", "_&()")]
    public void Should_test_to_alpha(
        string input,
        string expected,
        string additionalAllowedCharacters = ""
    )
    {
        var actual = input.ToAlpha(additionalAllowedCharacters);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("char", "char")]
    [InlineData("decimal(10,2)", "decimal")]
    [InlineData("abc(12,2) dd aa", "abc dd aa")]
    [InlineData("abc (   12 ,2   ) dd aa", "abc dd aa")]
    [InlineData("  nvarchar (    255 ) ", "nvarchar")]
    public void Should_test_discard_length_precision_and_scale_from_sql_type_name(
        string input,
        string expected
    )
    {
        var actual = input.DiscardLengthPrecisionAndScaleFromSqlTypeName();
        Assert.Equal(expected, actual);
    }
}
