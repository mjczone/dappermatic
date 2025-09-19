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
    public void TestGetFriendlyName(Type input, string expected)
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
    public void TestToAlpha(string input, string expected, string additionalAllowedCharacters = "")
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
    public void TestDiscardLengthPrecisionAndScaleFromSqlTypeName(string input, string expected)
    {
        var actual = input.DiscardLengthPrecisionAndScaleFromSqlTypeName();
        Assert.Equal(expected, actual);
    }
}
