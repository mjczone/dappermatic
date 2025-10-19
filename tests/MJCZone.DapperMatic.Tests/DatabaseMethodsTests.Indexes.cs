// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Text.Json;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Tests;

public abstract partial class DatabaseMethodsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("my_app")]
    protected virtual async Task Can_perform_simple_crud_on_indexes_Async(string? schemaName)
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, schemaName);

        var version = await db.GetDatabaseVersionAsync();
        Assert.True(version.Major > 0);

        var supportsDescendingColumnSorts = true;
        var dbType = db.GetDbProviderType();
        if (dbType.HasFlag(DbProviderType.MySql))
        {
            if (version.Major == 5)
            {
                supportsDescendingColumnSorts = false;
            }
        }

        const string tableName = "testWithIndex";
        const string columnName = "testColumn";
        const string indexName = "testIndex";

        var columns = new List<DmColumn>
        {
            new(
                schemaName,
                tableName,
                columnName,
                typeof(int),
                defaultExpression: "1",
                isNullable: false
            ),
        };
        for (var i = 0; i < 10; i++)
        {
            columns.Add(
                new DmColumn(
                    schemaName,
                    tableName,
                    columnName + "_" + i,
                    typeof(int),
                    defaultExpression: i.ToString(),
                    isNullable: false
                )
            );
        }

        await db.DropTableIfExistsAsync(schemaName, tableName);
        await db.CreateTableIfNotExistsAsync(schemaName, tableName, columns: [.. columns]);

        Output.WriteLine("Index Exists: {0}.{1}", tableName, indexName);
        var exists = await db.DoesIndexExistAsync(schemaName, tableName, indexName);
        Assert.False(exists);

        Output.WriteLine("Creating unique index: {0}.{1}", tableName, indexName);
        await db.CreateIndexIfNotExistsAsync(
            schemaName,
            tableName,
            indexName,
            [new DmOrderedColumn(columnName)],
            isUnique: true
        );

        Output.WriteLine(
            "Creating multiple column unique index: {0}.{1}_multi",
            tableName,
            indexName + "_multi"
        );
        await db.CreateIndexIfNotExistsAsync(
            schemaName,
            tableName,
            indexName + "_multi",
            [
                new DmOrderedColumn(columnName + "_1", DmColumnOrder.Descending),
                new DmOrderedColumn(columnName + "_2"),
            ],
            isUnique: true
        );

        Output.WriteLine(
            "Creating multiple column non unique index: {0}.{1}_multi2",
            tableName,
            indexName
        );
        await db.CreateIndexIfNotExistsAsync(
            schemaName,
            tableName,
            indexName + "_multi2",
            [
                new DmOrderedColumn(columnName + "_3"),
                new DmOrderedColumn(columnName + "_4", DmColumnOrder.Descending),
            ]
        );

        Output.WriteLine("Index Exists: {0}.{1}", tableName, indexName);
        exists = await db.DoesIndexExistAsync(schemaName, tableName, indexName);
        Assert.True(exists);
        exists = await db.DoesIndexExistAsync(schemaName, tableName, indexName + "_multi");
        Assert.True(exists);
        exists = await db.DoesIndexExistAsync(schemaName, tableName, indexName + "_multi2");
        Assert.True(exists);

        var indexNames = await db.GetIndexNamesAsync(schemaName, tableName);
        Assert.Contains(indexNames, i => i.Equals(indexName, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            indexNames,
            i => i.Equals(indexName + "_multi", StringComparison.OrdinalIgnoreCase)
        );
        Assert.Contains(
            indexNames,
            i => i.Equals(indexName + "_multi2", StringComparison.OrdinalIgnoreCase)
        );

        var indexes = await db.GetIndexesAsync(schemaName, tableName);
        Assert.True(indexes.Count >= 3);
        var idxMulti1 = indexes.SingleOrDefault(i =>
            i.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase)
            && i.IndexName.Equals(indexName + "_multi", StringComparison.OrdinalIgnoreCase)
        );
        var idxMulti2 = indexes.SingleOrDefault(i =>
            i.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase)
            && i.IndexName.Equals(indexName + "_multi2", StringComparison.OrdinalIgnoreCase)
        );
        Assert.NotNull(idxMulti1);
        Assert.NotNull(idxMulti2);
        Assert.NotNull(idxMulti1);
        Assert.NotNull(idxMulti2);
        Assert.True(idxMulti1.IsUnique);
        Assert.Equal(2, idxMulti1.Columns.Count);
        if (supportsDescendingColumnSorts)
        {
            Assert.Equal(DmColumnOrder.Descending, idxMulti1.Columns[0].Order);
            Assert.Equal(DmColumnOrder.Ascending, idxMulti1.Columns[1].Order);
        }
        Assert.False(idxMulti2.IsUnique);
        Assert.True(idxMulti2.Columns.Count == 2);
        Assert.Equal(DmColumnOrder.Ascending, idxMulti2.Columns[0].Order);
        if (supportsDescendingColumnSorts)
        {
            Assert.Equal(DmColumnOrder.Descending, idxMulti2.Columns[1].Order);
        }

        var indexesOnColumn = await db.GetIndexesOnColumnAsync(schemaName, tableName, columnName);
        Assert.NotEmpty(indexesOnColumn);

        Output.WriteLine("Dropping indexName: {0}.{1}", tableName, indexName);
        await db.DropIndexIfExistsAsync(schemaName, tableName, indexName);

        Output.WriteLine("Index Exists: {0}.{1}", tableName, indexName);
        exists = await db.DoesIndexExistAsync(schemaName, tableName, indexName);
        Assert.False(exists);

        await db.DropTableIfExistsAsync(schemaName, tableName);
    }
}
