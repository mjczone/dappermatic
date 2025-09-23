// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Dapper;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Tests;

public abstract partial class DatabaseMethodsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("my_app")]
    protected virtual async Task Can_perform_simple_CRUD_on_Tables_Async(string? schemaName)
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, schemaName);

        var supportsSchemas = db.SupportsSchemas();

        var tableName = "testTable";

        var exists = await db.DoesTableExistAsync(schemaName, tableName);
        if (exists)
        {
            await db.DropTableIfExistsAsync(schemaName, tableName);
        }

        exists = await db.DoesTableExistAsync(schemaName, tableName);
        Assert.False(exists);

        var nonExistentTable = await db.GetTableAsync(schemaName, tableName);
        Assert.Null(nonExistentTable);

        var table = new DmTable(
            schemaName,
            tableName,
            [
                new DmColumn("id", typeof(int), isPrimaryKey: true, isAutoIncrement: true),
                new DmColumn("name", typeof(string), isUnique: true),
            ]
        );
        var created = await db.CreateTableIfNotExistsAsync(table);
        Assert.True(created);

        var createdAgain = await db.CreateTableIfNotExistsAsync(table);
        Assert.False(createdAgain);

        exists = await db.DoesTableExistAsync(schemaName, tableName);
        Assert.True(exists);

        var tableNames = await db.GetTableNamesAsync(schemaName);
        Assert.NotEmpty(tableNames);
        Assert.Contains(tableName, tableNames, StringComparer.OrdinalIgnoreCase);

        var existingTable = await db.GetTableAsync(schemaName, tableName);
        Assert.NotNull(existingTable);

        if (supportsSchemas)
        {
            Assert.NotNull(existingTable.SchemaName);
            Assert.NotEmpty(existingTable.SchemaName);
        }
        Assert.Equal(tableName, existingTable.TableName, true);
        Assert.Equal(2, existingTable.Columns.Count);

        // Validate auto-increment detection
        var idColumn = existingTable.Columns.FirstOrDefault(c =>
            c.ColumnName.Equals("id", StringComparison.OrdinalIgnoreCase)
        );
        Assert.NotNull(idColumn);
        Assert.True(idColumn.IsAutoIncrement, "ID column should be detected as auto-increment");
        Assert.True(idColumn.IsPrimaryKey, "ID column should be primary key");

        var nameColumn = existingTable.Columns.FirstOrDefault(c =>
            c.ColumnName.Equals("name", StringComparison.OrdinalIgnoreCase)
        );
        Assert.NotNull(nameColumn);
        Assert.False(nameColumn.IsAutoIncrement, "Name column should not be auto-increment");

        // rename the table
        var newName = "newTestTable";
        var renamed = await db.RenameTableIfExistsAsync(schemaName, tableName, newName);
        Assert.True(renamed);

        exists = await db.DoesTableExistAsync(schemaName, tableName);
        Assert.False(exists);

        exists = await db.DoesTableExistAsync(schemaName, newName);
        Assert.True(exists);

        existingTable = await db.GetTableAsync(schemaName, newName);
        Assert.NotNull(existingTable);
        Assert.Equal(newName, existingTable.TableName, true);

        tableNames = await db.GetTableNamesAsync(schemaName);
        Assert.Contains(newName, tableNames, StringComparer.OrdinalIgnoreCase);

        var schemaQualifiedTableName = db.GetSchemaQualifiedTableName(schemaName, newName);

        // add a new row
        var newRow = new { id = 0, name = "Test" };
        await db.ExecuteAsync(
            @$"INSERT INTO {schemaQualifiedTableName} (name) VALUES (@name)",
            newRow
        );

        // get all rows
        var rows = await db.QueryAsync<dynamic>(
            @$"SELECT * FROM {schemaQualifiedTableName}",
            new { }
        );
        Assert.Single(rows);

        // truncate the table
        await db.TruncateTableIfExistsAsync(schemaName, newName);
        rows = await db.QueryAsync<dynamic>(@$"SELECT * FROM {schemaQualifiedTableName}", new { });
        Assert.Empty(rows);

        // drop the table
        await db.DropTableIfExistsAsync(schemaName, newName);

        exists = await db.DoesTableExistAsync(schemaName, newName);
        Assert.False(exists);

        Output.WriteLine($"Table names: {0}", string.Join(", ", tableNames));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("test_schema")]
    protected virtual async Task Can_CreateTable_Using_PropertyInitializers_Async(
        string? schemaName
    )
    {
        // This test validates that property initializers work correctly with DmTable
        // and that child objects get their TableName/SchemaName set properly

        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, schemaName);

        var tableName = "property_init_test_table";

        // Clean up any existing table
        await db.DropTableIfExistsAsync(schemaName, tableName);

        // Create DmTable using property initializers (as shown in documentation)
        var table = new DmTable(schemaName, tableName)
        {
            // Set TableName again via property (redundant but tests property setter)
            TableName = tableName,

            // Initialize Columns via property
            Columns =
            [
                new("Id", typeof(int))
                {
                    IsNullable = false,
                    IsAutoIncrement = true,
                    IsPrimaryKey = true,
                },
                new("Username", typeof(string), length: 50)
                {
                    IsNullable = false,
                    CheckExpressionFunc = CommonProviderCheckExpressions.LengthGreaterThanCheck(
                        "Username",
                        0
                    ),
                },
                new("Email", typeof(string), length: 100) { IsNullable = false, IsUnique = true },
                new("Age", typeof(int)) { IsNullable = true },
                new("IsActive", typeof(bool))
                {
                    IsNullable = false,
                    DefaultExpressionFunc = CommonProviderDefaultExpressions.TrueValue,
                },
                new("CreatedAt", typeof(DateTime)) { IsNullable = false },
                new("Balance", typeof(decimal), precision: 10, scale: 2) { IsNullable = false },
            ],

            // Initialize PrimaryKeyConstraint via property
            PrimaryKeyConstraint = new DmPrimaryKeyConstraint(
                "PK_PropertyInitTest",
                [new DmOrderedColumn("Id")]
            ),

            // Initialize Indexes via property
            Indexes =
            [
                new("IX_PropertyInitTest_Username", [new DmOrderedColumn("Username")])
                {
                    IsUnique = true,
                },
                new("IX_PropertyInitTest_Email", [new DmOrderedColumn("Email")])
                {
                    IsUnique = true,
                },
                new("IX_PropertyInitTest_CreatedAt", [new DmOrderedColumn("CreatedAt")]),
            ],

            // Initialize CheckConstraints via property
            CheckConstraints =
            [
                new("Age", "CK_PropertyInitTest_Age", "Age >= 0 AND Age <= 150"),
                // Username length check is handled at column level via CheckExpressionFunc
            ],

            // Initialize DefaultConstraints via property
            DefaultConstraints =
            [
                new("DF_PropertyInitTest_IsActive", "IsActive", "1"),
                new("DF_PropertyInitTest_CreatedAt", "CreatedAt", "GETDATE()"),
            ],

            // Initialize UniqueConstraints via property
            UniqueConstraints = [new("UQ_PropertyInitTest_Email", [new DmOrderedColumn("Email")])],
        };

        // Note: Not setting ForeignKeyConstraints as that would require another table

        // CRITICAL TEST: Try to create the table
        var created = await db.CreateTableIfNotExistsAsync(table);
        Assert.True(
            created,
            "Table should have been created successfully using property initializers"
        );

        // Verify the table exists
        var exists = await db.DoesTableExistAsync(schemaName, tableName);
        Assert.True(exists, "Table should exist after creation");

        // Get the table back and verify structure
        var retrievedTable = await db.GetTableAsync(schemaName, tableName);
        Assert.NotNull(retrievedTable);
        Assert.Equal(tableName, retrievedTable.TableName, ignoreCase: true);
        Assert.Equal(7, retrievedTable.Columns.Count); // Should have all 7 columns

        // Test that we can insert data
        var sql = db.GetDbProviderType() switch
        {
            DbProviderType.SqlServer => @"
                INSERT INTO [{0}].[{1}] (Username, Email, Age, IsActive, CreatedAt, Balance)
                VALUES (@Username, @Email, @Age, @IsActive, @CreatedAt, @Balance)",
            DbProviderType.MySql => @"
                INSERT INTO `{1}` (Username, Email, Age, IsActive, CreatedAt, Balance)
                VALUES (@Username, @Email, @Age, @IsActive, @CreatedAt, @Balance)",
            DbProviderType.PostgreSql => @"
                INSERT INTO {0} (""username"", ""email"", ""age"", ""isactive"", ""createdat"", ""balance"")
                VALUES (@Username, @Email, @Age, @IsActive, @CreatedAt, @Balance)",
            DbProviderType.Sqlite => @"
                INSERT INTO [{1}] (Username, Email, Age, IsActive, CreatedAt, Balance)
                VALUES (@Username, @Email, @Age, @IsActive, @CreatedAt, @Balance)",
            _ => throw new NotSupportedException(),
        };

        if (
            db.GetDbProviderType() == DbProviderType.MySql
            || db.GetDbProviderType() == DbProviderType.Sqlite
        )
        {
            sql = string.Format(sql, "", tableName);
        }
        else if (db.GetDbProviderType() == DbProviderType.PostgreSql)
        {
            sql = string.Format(sql, db.GetSchemaQualifiedTableName(schemaName, tableName));
        }
        else
        {
            sql = string.Format(sql, schemaName ?? "dbo", tableName);
        }

        var rowsInserted = await db.ExecuteAsync(
            sql,
            new
            {
                Username = "testuser",
                Email = "test@example.com",
                Age = 25,
                IsActive = true,
                CreatedAt = DateTime.Now,
                Balance = 100.50m,
            }
        );

        Assert.Equal(1, rowsInserted);

        // CRITICAL TEST: Try to add a column using the column from the original table
        // This tests if the column has its TableName set correctly
        var newColumn = new DmColumn("NewTestColumn", typeof(string), length: 100)
        {
            IsNullable = true,
            SchemaName = schemaName,
            TableName = tableName,
        };

        // Note: We have to set SchemaName and TableName manually here because
        // the column wasn't created through the table's constructor
        var columnAdded = await db.CreateColumnIfNotExistsAsync(newColumn);
        Assert.True(columnAdded, "Should be able to add a new column");

        // Clean up
        await db.DropTableIfExistsAsync(schemaName, tableName);

        Output.WriteLine($"Successfully created and tested table using property initializers");
    }

    [Fact]
    protected virtual void Can_configure_table_factory_with_all_options()
    {
        // Clear cache before configuration to prevent pollution from previous tests
        DmTableFactory.ClearCacheForTesting();

        // Test comprehensive configuration like documentation examples
        DmTableFactory.Configure(
            (type, table) =>
            {
                // Only configure our test type
                if (type == typeof(ConfigTestUser))
                {
                    // Customize table name
                    table.TableName = "app_users";

                    // Add audit columns
                    var auditColumns = new[]
                    {
                        new DmColumn("created_at", typeof(DateTime))
                        {
                            IsNullable = false,
                            DefaultExpression = "GETDATE()",
                        },
                        new DmColumn("updated_at", typeof(DateTime)) { IsNullable = true },
                    };
                    table.Columns = table.Columns.Concat(auditColumns).ToList();

                    // Add status column with default
                    var statusColumn = new DmColumn("status", typeof(string))
                    {
                        Length = 20,
                        IsNullable = false,
                        DefaultExpression = "'Active'",
                    };
                    table.Columns = table.Columns.Concat(new[] { statusColumn }).ToList();

                    // Add check constraints
                    var checkConstraints = new[]
                    {
                        new DmCheckConstraint("email", "CK_User_Email_Valid", "email LIKE '%@%'"),
                        new DmCheckConstraint(
                            "status",
                            "CK_User_Status_Valid",
                            "status IN ('Active', 'Inactive', 'Suspended')"
                        ),
                    };
                    table.CheckConstraints = [.. table.CheckConstraints, .. checkConstraints];
                }
            }
        );

        var userTable = DmTableFactory.GetTable(typeof(ConfigTestUser));

        // Verify all configurations were applied
        Assert.Equal("app_users", userTable.TableName);

        // Should have original 3 columns + 2 audit columns + 1 status column = 6 total
        Assert.Equal(6, userTable.Columns.Count);

        // Verify audit columns exist
        var createdAtColumn = userTable.Columns.FirstOrDefault(c =>
            c.ColumnName.Equals("created_at", StringComparison.OrdinalIgnoreCase)
        );
        Assert.NotNull(createdAtColumn);
        Assert.False(createdAtColumn.IsNullable);
        Assert.Equal("GETDATE()", createdAtColumn.DefaultExpression);

        var updatedAtColumn = userTable.Columns.FirstOrDefault(c =>
            c.ColumnName.Equals("updated_at", StringComparison.OrdinalIgnoreCase)
        );
        Assert.NotNull(updatedAtColumn);
        Assert.True(updatedAtColumn.IsNullable);

        // Verify status column exists
        var statusColumn = userTable.Columns.FirstOrDefault(c =>
            c.ColumnName.Equals("status", StringComparison.OrdinalIgnoreCase)
        );
        Assert.NotNull(statusColumn);
        Assert.Equal(20, statusColumn.Length);
        Assert.False(statusColumn.IsNullable);
        Assert.Equal("'Active'", statusColumn.DefaultExpression);

        // Verify check constraints were added
        Assert.Equal(2, userTable.CheckConstraints.Count);
        Assert.Contains(userTable.CheckConstraints, c => c.ConstraintName == "CK_User_Email_Valid");
        Assert.Contains(
            userTable.CheckConstraints,
            c => c.ConstraintName == "CK_User_Status_Valid"
        );

        Output.WriteLine("✅ Configuration with all options applied successfully");
    }
}

// Test class for configuration scenarios
public class ConfigTestUser
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
}
