// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using DbQueryLogging;
using MJCZone.DapperMatic.Models;
using MJCZone.DapperMatic.Providers;
using Npgsql;

namespace MJCZone.DapperMatic.Tests;

public abstract partial class DatabaseMethodsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("my_app")]
    protected virtual async Task Can_set_common_default_expressions_on_Columns_Async(
        string? schemaName
    )
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, schemaName);

        const string tableName = "testTableWithExpressions";
        const string columnName1 = "testColumnWithDefaultDate";
        const string columnName2 = "testColumnWithDefaultDateSetAfterCreate";
        const string columnName3 = "testColumnWithDefaultUuid";
        const string columnName4 = "testColumnWithDefaultShort";
        const string columnName5 = "testColumnWithDefaultBool";

        string? defaultDateTimeSql = null;
        string? defaultGuidSql = null;
        var dbType = db.GetDbProviderType();

        var version = await db.GetDatabaseVersionAsync();

        switch (dbType)
        {
            case DbProviderType.SqlServer:
                defaultDateTimeSql = "GETUTCDATE()";
                defaultGuidSql = "NEWID()";
                break;
            case DbProviderType.Sqlite:
                defaultDateTimeSql = "CURRENT_TIMESTAMP";
                //this could be supported IF the sqlite UUID extension was loaded and enabled
                //defaultGuidSql = "uuid_blob(uuid())";
                defaultGuidSql = null;
                break;
            case DbProviderType.PostgreSql:
                defaultDateTimeSql = "CURRENT_TIMESTAMP";
                defaultGuidSql = "uuid_generate_v4()";
                break;
            case DbProviderType.MySql:
                defaultDateTimeSql = version > new Version(5, 6, 5) ? "CURRENT_TIMESTAMP(6)" : null;
                // only supported after 8.0.13
                // LEADS TO THIS ERROR:
                // Statement is unsafe because it uses a system function that may return a different value on the replication slave.
                // MySQL isn't a good database for auto-generating GUIDs. Don't do it!
                // defaultGuidSql =
                //     version > new Version(8, 0, 13) && version < new Version(10, 0, 0)
                //         ? "(UUID())"
                //         : null;
                defaultGuidSql = null;
                break;
        }

        // Create table with a column with an expression
        var tableCreated = await db.CreateTableIfNotExistsAsync(
            schemaName,
            tableName,
            [
                new DmColumn(
                    schemaName,
                    tableName,
                    "id",
                    typeof(int),
                    isPrimaryKey: true,
                    isAutoIncrement: true
                ),
                new DmColumn(
                    schemaName,
                    tableName,
                    columnName1,
                    typeof(DateTime),
                    defaultExpression: defaultDateTimeSql
                ),
            ]
        );
        Assert.True(tableCreated);

        // Add a column with a default expression after the table is created
        var columnCreated = await db.CreateColumnIfNotExistsAsync(
            new DmColumn(
                schemaName,
                tableName,
                columnName2,
                typeof(DateTime),
                defaultExpression: defaultDateTimeSql
            )
        );
        Assert.True(columnCreated);

        if (defaultGuidSql != null)
        {
            // Add a column with a default expression after the table is created
            columnCreated = await db.CreateColumnIfNotExistsAsync(
                new DmColumn(
                    schemaName,
                    tableName,
                    columnName3,
                    typeof(Guid),
                    defaultExpression: defaultGuidSql
                )
            );
            Assert.True(columnCreated);
        }

        // Add a column with a default expression after the table is created
        columnCreated = await db.CreateColumnIfNotExistsAsync(
            new DmColumn(schemaName, tableName, columnName4, typeof(short), defaultExpression: "4")
        );
        Assert.True(columnCreated);

        if (
            db is NpgsqlConnection
            || db is LoggedDbConnection loggedDbConnection
                && loggedDbConnection.Inner is NpgsqlConnection
        )
        {
            columnCreated = await db.CreateColumnIfNotExistsAsync(
                new DmColumn(
                    schemaName,
                    tableName,
                    columnName5,
                    typeof(bool),
                    defaultExpression: "true"
                )
            );
            Assert.True(columnCreated);
        }
        else
        {
            // other databases take an integer
            columnCreated = await db.CreateColumnIfNotExistsAsync(
                new DmColumn(
                    schemaName,
                    tableName,
                    columnName5,
                    typeof(bool),
                    defaultExpression: "1"
                )
            );
            Assert.True(columnCreated);
        }

        // Now check to make sure the default expressions are set
        var table = await db.GetTableAsync(schemaName, tableName);
        var columns = await db.GetColumnsAsync(schemaName, tableName);
        var column1 = columns.SingleOrDefault(c =>
            c.ColumnName.Equals(columnName1, StringComparison.OrdinalIgnoreCase)
        );
        var column2 = columns.SingleOrDefault(c =>
            c.ColumnName.Equals(columnName2, StringComparison.OrdinalIgnoreCase)
        );
        var column4 = columns.SingleOrDefault(c =>
            c.ColumnName.Equals(columnName4, StringComparison.OrdinalIgnoreCase)
        );
        var column5 = columns.SingleOrDefault(c =>
            c.ColumnName.Equals(columnName5, StringComparison.OrdinalIgnoreCase)
        );

        Assert.NotNull(column1);
        Assert.NotNull(column1.DefaultExpression);
        Assert.NotEmpty(column1.DefaultExpression);

        Assert.NotNull(column2);
        Assert.NotNull(column2.DefaultExpression);
        Assert.NotEmpty(column2.DefaultExpression);

        Assert.NotNull(column4);
        Assert.NotNull(column4.DefaultExpression);
        Assert.NotEmpty(column4.DefaultExpression);

        Assert.NotNull(column5);
        Assert.NotNull(column5.DefaultExpression);
        Assert.NotEmpty(column5.DefaultExpression);

        if (defaultGuidSql != null)
        {
            var column3 = columns.SingleOrDefault(c =>
                c.ColumnName.Equals(columnName3, StringComparison.OrdinalIgnoreCase)
            );
            Assert.NotNull(column3);
            Assert.NotNull(column3.DefaultExpression);
            Assert.NotEmpty(column3.DefaultExpression);
        }

        // Now try to remove the default expressions (first using the column name, then using the constraint name)
        Assert.True(
            await db.DropDefaultConstraintOnColumnIfExistsAsync(schemaName, tableName, columnName1)
        );
        var constraintName = table
            ?.DefaultConstraints.First(dc =>
                dc.ColumnName.Equals(column2.ColumnName, StringComparison.OrdinalIgnoreCase)
            )
            .ConstraintName;
        Assert.NotNull(constraintName);
        Assert.NotEmpty(constraintName);
        Assert.True(
            await db.DropDefaultConstraintIfExistsAsync(schemaName, tableName, constraintName)
        );

        // TODO: timestamp columns can't have default values dropped in MariaDB, WEIRD!
        // might have to change syntax to use ALTER TABLE table_name MODIFY COLUMN column_name TIMESTAMP NULL;
        if (
            db.GetDbProviderType() != DbProviderType.MySql
            || (version.Major != 10 && version.Major != 11 && version.Major != 12)
        )
        {
            var table2 = await db.GetTableAsync(schemaName, tableName);
            columns = await db.GetColumnsAsync(schemaName, tableName);
            column1 = columns.SingleOrDefault(c =>
                c.ColumnName.Equals(columnName1, StringComparison.OrdinalIgnoreCase)
            );
            column2 = columns.SingleOrDefault(c =>
                c.ColumnName.Equals(columnName2, StringComparison.OrdinalIgnoreCase)
            );

            Assert.Equal(table!.DefaultConstraints.Count - 2, table2!.DefaultConstraints.Count);

            Assert.NotNull(column1);
            Assert.Null(column1.DefaultExpression);

            Assert.NotNull(column2);
            Assert.Null(column2.DefaultExpression);
        }

        await db.DropTableIfExistsAsync(schemaName, tableName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("my_app")]
    protected virtual async Task Can_perform_simple_CRUD_on_Columns_Async(string? schemaName)
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, schemaName);

        var dbType = db.GetDbProviderType();
        var dbTypeMap = db.GetProviderTypeMap();

        const string tableName = "testWithTypedColumns";
        const string columnName = "testColumn";

        await db.CreateTableIfNotExistsAsync(
            schemaName,
            tableName,
            [
                new DmColumn(
                    schemaName,
                    tableName,
                    "id",
                    typeof(int),
                    isPrimaryKey: true,
                    isAutoIncrement: true
                ),
            ]
        );

        // try adding a columnName of all the supported types
        var i = 0;
        foreach (var type in GetSupportedTypes(dbTypeMap))
        {
            // create a column with the supported type
            var uniqueColumnName = $"{columnName}_{type.Name.ToAlpha()}_{i++}";
            var column = new DmColumn(
                schemaName,
                tableName,
                uniqueColumnName,
                type,
                isNullable: true
            );
            var columnCreated = await db.CreateColumnIfNotExistsAsync(column);

            if (!columnCreated)
            {
                columnCreated = await db.CreateColumnIfNotExistsAsync(column);
                Assert.True(columnCreated);
            }
        }

        // Test column rename functionality (similar to table rename testing)
        // Use the first created column for rename testing
        var firstType = GetSupportedTypes(dbTypeMap).First();
        var originalColumnName = $"{columnName}_{firstType.Name.ToAlpha()}_0";
        var newColumnName = "renamedTestColumn";

        // Rename the column
        var renamed = await db.RenameColumnIfExistsAsync(schemaName, tableName, originalColumnName, newColumnName);
        Assert.True(renamed);

        // Verify old name doesn't exist and new name exists
        var columns = await db.GetColumnsAsync(schemaName, tableName);
        Assert.DoesNotContain(columns, c => c.ColumnName.Equals(originalColumnName, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(columns, c => c.ColumnName.Equals(newColumnName, StringComparison.OrdinalIgnoreCase));

        await db.DropTableIfExistsAsync(schemaName, tableName);
    }
}
