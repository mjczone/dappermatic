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
    protected virtual async Task Can_perform_simple_crud_on_primary_key_constraints_Async(
        string? schemaName
    )
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, schemaName);

        const string tableName = "testWithPk";
        const string columnName = "testColumn";
        const string primaryKeyName = "testPk";

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
                ),
            ]
        );
        Output.WriteLine("Primary Key Exists: {0}.{1}", tableName, primaryKeyName);
        var exists = await db.DoesPrimaryKeyConstraintExistAsync(schemaName, tableName);
        Assert.False(exists);
        Output.WriteLine("Creating primary key: {0}.{1}", tableName, primaryKeyName);
        await db.CreatePrimaryKeyConstraintIfNotExistsAsync(
            schemaName,
            tableName,
            primaryKeyName,
            [new DmOrderedColumn(columnName)]
        );
        Output.WriteLine("Primary Key Exists: {0}.{1}", tableName, primaryKeyName);
        exists = await db.DoesPrimaryKeyConstraintExistAsync(schemaName, tableName);
        Assert.True(exists);
        Output.WriteLine("Dropping primary key: {0}.{1}", tableName, primaryKeyName);
        await db.DropPrimaryKeyConstraintIfExistsAsync(schemaName, tableName);
        Output.WriteLine("Primary Key Exists: {0}.{1}", tableName, primaryKeyName);
        exists = await db.DoesPrimaryKeyConstraintExistAsync(schemaName, tableName);
        Assert.False(exists);
        await db.DropTableIfExistsAsync(schemaName, tableName);
    }
}
