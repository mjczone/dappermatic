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
    protected virtual async Task Can_perform_simple_CRUD_on_CheckConstraints_Async(
        string? schemaName
    )
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, schemaName);

        var supportsCheckConstraints = await db.SupportsCheckConstraintsAsync();

        var testTableName = "testTableCheckConstraints";
        await db.CreateTableIfNotExistsAsync(
            schemaName,
            testTableName,
            [new DmColumn(schemaName, testTableName, "testColumn", typeof(int))]
        );

        var constraintName = "ck_testTable";
        var exists = await db.DoesCheckConstraintExistAsync(
            schemaName,
            testTableName,
            constraintName
        );

        if (exists)
        {
            await db.DropCheckConstraintIfExistsAsync(schemaName, testTableName, constraintName);
        }

        await db.CreateCheckConstraintIfNotExistsAsync(
            schemaName,
            testTableName,
            null,
            constraintName,
            "testColumn > 0"
        );

        exists = await db.DoesCheckConstraintExistAsync(schemaName, testTableName, constraintName);
        Assert.True(supportsCheckConstraints ? exists : !exists);

        var existingConstraint = await db.GetCheckConstraintAsync(
            schemaName,
            testTableName,
            constraintName
        );
        if (!supportsCheckConstraints)
        {
            Assert.Null(existingConstraint);
        }
        else
        {
            Assert.Equal(
                constraintName,
                existingConstraint?.ConstraintName,
                StringComparer.OrdinalIgnoreCase
            );
        }

        var checkConstraintNames = await db.GetCheckConstraintNamesAsync(schemaName, testTableName);
        if (!supportsCheckConstraints)
        {
            Assert.Empty(checkConstraintNames);
        }
        else
        {
            Assert.Contains(constraintName, checkConstraintNames, StringComparer.OrdinalIgnoreCase);
        }

        var dropped = await db.DropCheckConstraintIfExistsAsync(
            schemaName,
            testTableName,
            constraintName
        );
        if (!supportsCheckConstraints)
        {
            Assert.False(dropped);
        }
        else
        {
            Assert.True(dropped);
            exists = await db.DoesCheckConstraintExistAsync(
                schemaName,
                testTableName,
                constraintName
            );
        }

        exists = await db.DoesCheckConstraintExistAsync(schemaName, testTableName, constraintName);
        Assert.False(exists);

        await db.DropTableIfExistsAsync(schemaName, testTableName);

        await db.CreateTableIfNotExistsAsync(
            schemaName,
            testTableName,
            [
                new DmColumn(schemaName, testTableName, "testColumn", typeof(int)),
                new DmColumn(
                    schemaName,
                    testTableName,
                    "testColumn2",
                    typeof(int),
                    checkExpression: "testColumn2 > 0"
                )
            ]
        );

        var checkConstraint = await db.GetCheckConstraintOnColumnAsync(
            schemaName,
            testTableName,
            "testColumn2"
        );
        if (!supportsCheckConstraints)
        {
            Assert.Null(checkConstraint);
        }
        else
        {
            Assert.NotNull(checkConstraint);
        }

        await db.DropTableIfExistsAsync(schemaName, testTableName);
    }
}
