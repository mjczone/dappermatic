// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.DataAnnotations;
using MJCZone.DapperMatic.Models;
using Xunit.Abstractions;

namespace MJCZone.DapperMatic.Tests;

[DmTable(tableName: "test_ordered_idx")]
[DmIndex(columnNames: ["col_a", "col_b DESC", "col_c ASC"], indexName: "ix_test_ordered")]
public class TestOrderedIndex
{
    [DmColumn("col_a", isPrimaryKey: true)]
    public int ColA { get; set; }

    [DmColumn("col_b")]
    public int ColB { get; set; }

    [DmColumn("col_c")]
    public int ColC { get; set; }
}

[DmTable(tableName: "test_ordered_pk")]
[DmPrimaryKeyConstraint(["id DESC"], "pk_test_ordered")]
public class TestOrderedPrimaryKey
{
    [DmColumn("id")]
    public int Id { get; set; }

    [DmColumn("name", length: 100)]
    public string Name { get; set; } = string.Empty;
}

[DmTable(tableName: "test_ordered_uc")]
[DmUniqueConstraint(columnNames: ["tenant_id", "email DESC"], constraintName: "ux_test_ordered")]
public class TestOrderedUniqueConstraint
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }

    [DmColumn("tenant_id")]
    public Guid TenantId { get; set; }

    [DmColumn("email", length: 320)]
    public string Email { get; set; } = string.Empty;
}

public class TableFactoryOrderedColumnTests : TestBase
{
    public TableFactoryOrderedColumnTests(ITestOutputHelper output)
        : base(output) { }

    [Fact]
    public void Should_parse_DmIndex_attribute_column_sort_direction()
    {
        var table = DmTableFactory.GetTable(typeof(TestOrderedIndex));

        var index = table.Indexes.SingleOrDefault(i =>
            i.IndexName.Equals("ix_test_ordered", StringComparison.OrdinalIgnoreCase)
        );
        Assert.NotNull(index);
        Assert.Equal(3, index.Columns.Count);

        Assert.Equal("col_a", index.Columns[0].ColumnName);
        Assert.Equal(DmColumnOrder.Ascending, index.Columns[0].Order);

        Assert.Equal("col_b", index.Columns[1].ColumnName);
        Assert.Equal(DmColumnOrder.Descending, index.Columns[1].Order);

        Assert.Equal("col_c", index.Columns[2].ColumnName);
        Assert.Equal(DmColumnOrder.Ascending, index.Columns[2].Order);
    }

    [Fact]
    public void Should_parse_DmPrimaryKeyConstraint_attribute_column_sort_direction()
    {
        var table = DmTableFactory.GetTable(typeof(TestOrderedPrimaryKey));

        Assert.NotNull(table.PrimaryKeyConstraint);
        Assert.Single(table.PrimaryKeyConstraint.Columns);

        Assert.Equal("id", table.PrimaryKeyConstraint.Columns[0].ColumnName);
        Assert.Equal(DmColumnOrder.Descending, table.PrimaryKeyConstraint.Columns[0].Order);
    }

    [Fact]
    public void Should_parse_DmUniqueConstraint_attribute_column_sort_direction()
    {
        var table = DmTableFactory.GetTable(typeof(TestOrderedUniqueConstraint));

        var uc = table.UniqueConstraints.SingleOrDefault(c =>
            c.ConstraintName.Equals("ux_test_ordered", StringComparison.OrdinalIgnoreCase)
        );
        Assert.NotNull(uc);
        Assert.Equal(2, uc.Columns.Count);

        Assert.Equal("tenant_id", uc.Columns[0].ColumnName);
        Assert.Equal(DmColumnOrder.Ascending, uc.Columns[0].Order);

        Assert.Equal("email", uc.Columns[1].ColumnName);
        Assert.Equal(DmColumnOrder.Descending, uc.Columns[1].Order);
    }
}
