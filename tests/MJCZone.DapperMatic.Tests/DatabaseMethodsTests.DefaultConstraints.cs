// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.Models;
using MJCZone.DapperMatic.Providers;

namespace MJCZone.DapperMatic.Tests;

public abstract partial class DatabaseMethodsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("my_app")]
    protected virtual async Task Can_perform_simple_crud_on_default_constraints_Async(string? schemaName)
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, schemaName);

        var testTableName = "testTableDefaultConstraints";
        var testColumnName = "testColumn";
        await db.CreateTableIfNotExistsAsync(
            schemaName,
            testTableName,
            [new DmColumn(schemaName, testTableName, testColumnName, typeof(int))]
        );

        // in MySQL, default constraints are not named, so this MUST use the ProviderUtils method which is what MJCZone.DapperMatic uses internally
        var constraintName = DbProviderUtils.GenerateDefaultConstraintName(testTableName, testColumnName);
        var exists = await db.DoesDefaultConstraintExistAsync(schemaName, testTableName, constraintName);
        if (exists)
        {
            await db.DropDefaultConstraintIfExistsAsync(schemaName, testTableName, constraintName);
        }

        await db.CreateDefaultConstraintIfNotExistsAsync(
            schemaName,
            testTableName,
            testColumnName,
            constraintName,
            "0"
        );
        var existingConstraint = await db.GetDefaultConstraintAsync(schemaName, testTableName, constraintName);
        Assert.Equal(constraintName, existingConstraint?.ConstraintName, true);

        var defaultConstraintNames = await db.GetDefaultConstraintNamesAsync(schemaName, testTableName);
        Assert.Contains(constraintName, defaultConstraintNames, StringComparer.OrdinalIgnoreCase);

        await db.DropDefaultConstraintIfExistsAsync(schemaName, testTableName, constraintName);
        exists = await db.DoesDefaultConstraintExistAsync(schemaName, testTableName, constraintName);
        Assert.False(exists);

        await db.DropTableIfExistsAsync(schemaName, testTableName);

        await db.CreateTableIfNotExistsAsync(
            schemaName,
            testTableName,
            [
                new DmColumn(schemaName, testTableName, testColumnName, typeof(int)),
                new DmColumn(schemaName, testTableName, "testColumn2", typeof(int), defaultExpression: "0"),
            ]
        );
        var defaultConstraint = await db.GetDefaultConstraintOnColumnAsync(schemaName, testTableName, "testColumn2");
        Assert.NotNull(defaultConstraint);

        var tableDeleted = await db.DropTableIfExistsAsync(schemaName, testTableName);
        Assert.True(tableDeleted);

        await InitFreshSchemaAsync(db, schemaName);
    }
}
