// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Tests;

public abstract partial class DatabaseMethodsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("my_app")]
    protected virtual async Task Can_perform_simple_CRUD_on_ForeignKeyConstraints_Async(
        string? schemaName
    )
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, schemaName);

        const string tableName = "testWithFk";
        const string columnName = "testFkColumn";
        const string foreignKeyName = "testFk";
        const string refTableName = "testRefPk";
        const string refTableColumn = "id";

        await db.CreateTableIfNotExistsAsync(
            schemaName,
            tableName,
            [
                new DmColumn(
                    schemaName,
                    tableName,
                    columnName,
                    typeof(int),
                    defaultExpression: "1",
                    isNullable: false
                )
            ]
        );
        await db.CreateTableIfNotExistsAsync(
            schemaName,
            refTableName,
            [
                new DmColumn(
                    schemaName,
                    refTableName,
                    refTableColumn,
                    typeof(int),
                    defaultExpression: "1",
                    isPrimaryKey: true,
                    isNullable: false
                )
            ]
        );

        Output.WriteLine("Foreign Key Exists: {0}.{1}", tableName, foreignKeyName);
        var exists = await db.DoesForeignKeyConstraintExistAsync(
            schemaName,
            tableName,
            foreignKeyName
        );
        Assert.False(exists);

        Output.WriteLine("Creating foreign key: {0}.{1}", tableName, foreignKeyName);
        var created = await db.CreateForeignKeyConstraintIfNotExistsAsync(
            schemaName,
            tableName,
            foreignKeyName,
            [new DmOrderedColumn(columnName)],
            refTableName,
            [new DmOrderedColumn("id")],
            onDelete: DmForeignKeyAction.Cascade
        );
        Assert.True(created);

        Output.WriteLine("Foreign Key Exists: {0}.{1}", tableName, foreignKeyName);
        exists = await db.DoesForeignKeyConstraintExistAsync(schemaName, tableName, foreignKeyName);
        Assert.True(exists);
        exists = await db.DoesForeignKeyConstraintExistOnColumnAsync(
            schemaName,
            tableName,
            columnName
        );
        Assert.True(exists);

        Output.WriteLine("Get Foreign Key Names: {0}", tableName);
        var fkNames = await db.GetForeignKeyConstraintNamesAsync(schemaName, tableName);
        Assert.Contains(
            fkNames,
            fk => fk.Equals(foreignKeyName, StringComparison.OrdinalIgnoreCase)
        );

        Output.WriteLine("Get Foreign Keys: {0}", tableName);
        var fks = await db.GetForeignKeyConstraintsAsync(schemaName, tableName);
        Assert.Contains(
            fks,
            fk =>
                fk.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase)
                && fk.SourceColumns.Any(sc =>
                    sc.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase)
                )
                && fk.ConstraintName.Equals(foreignKeyName, StringComparison.OrdinalIgnoreCase)
                && fk.ReferencedTableName.Equals(refTableName, StringComparison.OrdinalIgnoreCase)
                && fk.ReferencedColumns.Any(sc =>
                    sc.ColumnName.Equals("id", StringComparison.OrdinalIgnoreCase)
                )
                && fk.OnDelete.Equals(DmForeignKeyAction.Cascade)
        );

        Output.WriteLine("Dropping foreign key: {0}", foreignKeyName);
        await db.DropForeignKeyConstraintIfExistsAsync(schemaName, tableName, foreignKeyName);

        Output.WriteLine("Foreign Key Exists: {0}", foreignKeyName);
        exists = await db.DoesForeignKeyConstraintExistAsync(schemaName, tableName, foreignKeyName);
        Assert.False(exists);
        exists = await db.DoesForeignKeyConstraintExistOnColumnAsync(
            schemaName,
            tableName,
            columnName
        );
        Assert.False(exists);

        await db.DropTableIfExistsAsync(schemaName, tableName);
        await db.DropTableIfExistsAsync(schemaName, refTableName);
    }
}
