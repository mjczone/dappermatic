// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.Models;

using Xunit.Abstractions;

namespace MJCZone.DapperMatic.Tests;

public class OrderedColumnParseTests : TestBase
{
    public OrderedColumnParseTests(ITestOutputHelper output)
        : base(output) { }

    [Theory]
    [InlineData("date", "date", DmColumnOrder.Ascending)]
    [InlineData("date DESC", "date", DmColumnOrder.Descending)]
    [InlineData("date ASC", "date", DmColumnOrder.Ascending)]
    [InlineData("date desc", "date", DmColumnOrder.Descending)]
    [InlineData("date Asc", "date", DmColumnOrder.Ascending)]
    [InlineData("my_column", "my_column", DmColumnOrder.Ascending)]
    [InlineData("my_column DESC", "my_column", DmColumnOrder.Descending)]
    public void Parse_returns_correct_column_name_and_order(
        string input,
        string expectedColumnName,
        DmColumnOrder expectedOrder
    )
    {
        var result = DmOrderedColumn.Parse(input);

        Assert.Equal(expectedColumnName, result.ColumnName);
        Assert.Equal(expectedOrder, result.Order);
    }

    [Theory]
    [InlineData("  date   DESC  ", "date", DmColumnOrder.Descending)]
    [InlineData("  col_a  ", "col_a", DmColumnOrder.Ascending)]
    public void Parse_handles_extra_whitespace(string input, string expectedColumnName, DmColumnOrder expectedOrder)
    {
        var result = DmOrderedColumn.Parse(input);

        Assert.Equal(expectedColumnName, result.ColumnName);
        Assert.Equal(expectedOrder, result.Order);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Parse_throws_on_null_or_empty(string? input)
    {
        Assert.Throws<ArgumentException>(() => DmOrderedColumn.Parse(input!));
    }
}
