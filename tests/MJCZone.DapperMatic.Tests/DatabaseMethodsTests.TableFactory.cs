// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.SQLite;
using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using MJCZone.DapperMatic.DataAnnotations;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Tests;

public abstract partial class DatabaseMethodsTests
{
    [Theory]
    [InlineData(typeof(TestDao1))]
    [InlineData(typeof(TestDao2))]
    [InlineData(typeof(TestDao3))]
    [InlineData(typeof(TestTable4))]
    [InlineData(typeof(TestTable5))]
    [InlineData(typeof(TestTable6))]
    protected virtual async Task Can_create_tables_from_model_classes_Async(Type type)
    {
        var tableDef = DmTableFactory.GetTable(type);

        using var db = await OpenConnectionAsync();

        if (!string.IsNullOrWhiteSpace(tableDef.SchemaName))
        {
            await db.CreateSchemaIfNotExistsAsync(tableDef.SchemaName);
        }

        await db.CreateTableIfNotExistsAsync(tableDef);

        var tableExists = await db.DoesTableExistAsync(tableDef.SchemaName, tableDef.TableName);
        Assert.True(tableExists);

        // Retrieve the table definition from the database to verify it matches the model
        var dbTableDef = await db.GetTableAsync(tableDef.SchemaName, tableDef.TableName);
        Assert.NotNull(dbTableDef);

        if (
            db is SqliteConnection
            || db is SQLiteConnection
            || (
                db is Logging.DbLoggingConnection ldb
                && (ldb.Inner is SqliteConnection || ldb.Inner is SQLiteConnection)
            )
        )
        {
            // For SQLite, we can retrieve the table definition using PRAGMA
            // Note: This will not return column data types, only names and nullability
            var tableDefAsSql = await db.QueryAsync($"PRAGMA table_info('{dbTableDef.TableName}');");
            Assert.NotNull(tableDefAsSql);
            Output.WriteLine($"Table definition for {tableDef.TableName}:\n{JsonSerializer.Serialize(tableDefAsSql)}");
        }

        // Verify the table definition matches the model
        Assert.Equal(tableDef.TableName, dbTableDef.TableName, ignoreCase: true);
        if (db.SupportsSchemas())
        {
            Assert.NotNull(dbTableDef.SchemaName);
            if (!string.IsNullOrWhiteSpace(tableDef.SchemaName))
            {
                Assert.Equal(tableDef.SchemaName, dbTableDef.SchemaName);
            }
        }
        else
        {
            Assert.Null(dbTableDef.SchemaName);
        }
        Assert.Equal(tableDef.Columns.Count, dbTableDef.Columns.Count);

        // All columns that start with "Nullable" should be nullable in the database
        foreach (var column in tableDef.Columns)
        {
            var dbColumn = dbTableDef.Columns.FirstOrDefault(c =>
                c.ColumnName.Equals(column.ColumnName, StringComparison.OrdinalIgnoreCase)
            );
            Assert.NotNull(dbColumn);
            if (column.ColumnName.StartsWith("Nullable", StringComparison.OrdinalIgnoreCase))
            {
                Assert.True(dbColumn.IsNullable, $"Column {column.ColumnName} should be nullable.");
            }
            else
            {
                Assert.False(dbColumn.IsNullable, $"Column {column.ColumnName} should not be nullable.");
            }
        }

        var dropped = await db.DropTableIfExistsAsync(tableDef.SchemaName, tableDef.TableName);
        Assert.True(dropped);
    }

    /// <summary>
    /// Tests that DmTableFactory.GetTable() correctly preserves nullable columns
    /// even when they are part of a unique index. This is a regression test for
    /// a reported issue where nullable columns in unique indexes were being
    /// created as NOT NULL.
    /// </summary>
    [Fact]
    protected virtual async Task DmTableFactory_preserves_nullable_columns_in_unique_indexes_Async()
    {
        // Step 1: Verify DmTableFactory.GetTable() correctly interprets the model
        var tableDef = DmTableFactory.GetTable(typeof(TestTableWithNullableUniqueIndex));

        // Verify the table definition has the expected columns
        Assert.Equal("test_users_with_nullable_unique", tableDef.TableName, ignoreCase: true);
        Assert.Equal(5, tableDef.Columns.Count);

        // CRITICAL: Verify phone_number is nullable in the DmTable definition
        var phoneColumn = tableDef.Columns.FirstOrDefault(c =>
            c.ColumnName.Equals("phone_number", StringComparison.OrdinalIgnoreCase)
        );
        Assert.NotNull(phoneColumn);
        Assert.True(
            phoneColumn.IsNullable,
            "phone_number column should be nullable in DmTableFactory.GetTable() result"
        );

        // Verify display_name is also nullable
        var displayNameColumn = tableDef.Columns.FirstOrDefault(c =>
            c.ColumnName.Equals("display_name", StringComparison.OrdinalIgnoreCase)
        );
        Assert.NotNull(displayNameColumn);
        Assert.True(
            displayNameColumn.IsNullable,
            "display_name column should be nullable in DmTableFactory.GetTable() result"
        );

        // Verify the unique indexes exist
        Assert.Single(tableDef.UniqueConstraints);
        var phoneUniqueConstraint = tableDef.UniqueConstraints.FirstOrDefault(i =>
            i.ConstraintName.Equals("UX_test_users_tenant_phone", StringComparison.OrdinalIgnoreCase)
        );
        Assert.NotNull(phoneUniqueConstraint);
        Assert.Equal(2, phoneUniqueConstraint.Columns.Count);

        // Step 2: Create the table in the database and verify the column is still nullable
        using var db = await OpenConnectionAsync();

        await db.DropTableIfExistsAsync(tableDef.SchemaName, tableDef.TableName);
        await db.CreateTableIfNotExistsAsync(tableDef);

        var tableExists = await db.DoesTableExistAsync(tableDef.SchemaName, tableDef.TableName);
        Assert.True(tableExists);

        // Retrieve the table definition from the database
        var dbTableDef = await db.GetTableAsync(tableDef.SchemaName, tableDef.TableName);
        Assert.NotNull(dbTableDef);

        // CRITICAL: Verify phone_number is still nullable in the actual database table
        var dbPhoneColumn = dbTableDef.Columns.FirstOrDefault(c =>
            c.ColumnName.Equals("phone_number", StringComparison.OrdinalIgnoreCase)
        );
        Assert.NotNull(dbPhoneColumn);
        Assert.True(
            dbPhoneColumn.IsNullable,
            "phone_number column should remain nullable in the database even with a unique constraint"
        );

        // Verify display_name is also nullable in the database
        var dbDisplayNameColumn = dbTableDef.Columns.FirstOrDefault(c =>
            c.ColumnName.Equals("display_name", StringComparison.OrdinalIgnoreCase)
        );
        Assert.NotNull(dbDisplayNameColumn);
        Assert.True(dbDisplayNameColumn.IsNullable, "display_name column should remain nullable in the database");

        // Cleanup
        await db.DropTableIfExistsAsync(tableDef.SchemaName, tableDef.TableName);
    }

    /// <summary>
    /// Tests the exact RoleMember scenario with nullable Guid columns in unique indexes with foreign keys.
    /// This validates that nullable Guid? columns remain nullable even when part of complex constraints.
    /// </summary>
    [Fact]
    protected virtual async Task DmTableFactory_preserves_nullable_guid_columns_in_indexes_and_fks_Async()
    {
        // Clear cache to ensure fresh table generation
        // DmTableFactory.ClearCacheForTesting();

        using var db = await OpenConnectionAsync();

        await db.CreateTablesIfNotExistsAsync([
            typeof(TestTenant),
            typeof(TestRole),
            typeof(TestUser),
            typeof(TestGroup),
            typeof(TestRoleMember),
        ]);

        // Step 1: Create the referenced tables first
        var tenantTableDef = DmTableFactory.GetTable(typeof(TestTenant));
        var roleTableDef = DmTableFactory.GetTable(typeof(TestRole));
        var userTableDef = DmTableFactory.GetTable(typeof(TestUser));
        var groupTableDef = DmTableFactory.GetTable(typeof(TestGroup));

        // Step 2: Get the RoleMember table definition
        var tableDef = DmTableFactory.GetTable(typeof(TestRoleMember));

        // await db.CreateTableIfNotExistsAsync(tenantTableDef);
        // await db.CreateTableIfNotExistsAsync(roleTableDef);
        // await db.CreateTableIfNotExistsAsync(userTableDef);
        // await db.CreateTableIfNotExistsAsync(groupTableDef);

        // Verify the table definition has the expected columns
        Assert.Equal("test_role_members", tableDef.TableName, ignoreCase: true);

        // CRITICAL: Verify user_id is nullable in the DmTable definition
        var userIdColumn = tableDef.Columns.FirstOrDefault(c =>
            c.ColumnName.Equals("user_id", StringComparison.OrdinalIgnoreCase)
        );
        Assert.NotNull(userIdColumn);
        Assert.True(userIdColumn.IsNullable, "user_id column should be nullable in DmTableFactory.GetTable() result");

        // CRITICAL: Verify group_id is nullable in the DmTable definition
        var groupIdColumn = tableDef.Columns.FirstOrDefault(c =>
            c.ColumnName.Equals("group_id", StringComparison.OrdinalIgnoreCase)
        );
        Assert.NotNull(groupIdColumn);
        Assert.True(groupIdColumn.IsNullable, "group_id column should be nullable in DmTableFactory.GetTable() result");

        // Step 3: Create the table in the database
        await db.DropTableIfExistsAsync(tableDef.SchemaName, tableDef.TableName);
        await db.CreateTableIfNotExistsAsync(tableDef);

        var tableExists = await db.DoesTableExistAsync(tableDef.SchemaName, tableDef.TableName);
        Assert.True(tableExists);

        // Retrieve the table definition from the database
        var dbTableDef = await db.GetTableAsync(tableDef.SchemaName, tableDef.TableName);
        Assert.NotNull(dbTableDef);

        // CRITICAL: Verify user_id is still nullable in the actual database table
        var dbUserIdColumn = dbTableDef.Columns.FirstOrDefault(c =>
            c.ColumnName.Equals("user_id", StringComparison.OrdinalIgnoreCase)
        );
        Assert.NotNull(dbUserIdColumn);
        Assert.True(
            dbUserIdColumn.IsNullable,
            "user_id column should remain nullable in the database even with unique index and FK"
        );

        // CRITICAL: Verify group_id is still nullable in the actual database table
        var dbGroupIdColumn = dbTableDef.Columns.FirstOrDefault(c =>
            c.ColumnName.Equals("group_id", StringComparison.OrdinalIgnoreCase)
        );
        Assert.NotNull(dbGroupIdColumn);
        Assert.True(
            dbGroupIdColumn.IsNullable,
            "group_id column should remain nullable in the database even with unique index and FK"
        );

        // Cleanup
        await db.DropTableIfExistsAsync(tableDef.SchemaName, tableDef.TableName);
        await db.DropTableIfExistsAsync(groupTableDef.SchemaName, groupTableDef.TableName);
        await db.DropTableIfExistsAsync(userTableDef.SchemaName, userTableDef.TableName);
        await db.DropTableIfExistsAsync(roleTableDef.SchemaName, roleTableDef.TableName);
        await db.DropTableIfExistsAsync(tenantTableDef.SchemaName, tenantTableDef.TableName);

        DmTableFactory.ClearCacheForTesting();
    }

    /// <summary>
    /// Tests that DmIndexAttribute can be applied to properties without specifying column names.
    /// The column name should be automatically inferred from the property name.
    /// </summary>
    [Fact]
    protected virtual async Task Should_create_indexes_from_property_level_attributes()
    {
        // Arrange
        var table = DmTableFactory.GetTable(typeof(TestPropertyLevelIndexes));

        // Assert - Should have 3 indexes total
        Assert.Equal(3, table.Indexes.Count);

        // Verify UserName index
        var userNameIndex = table.Indexes.FirstOrDefault(i => i.Columns.Any(c => c.ColumnName == "UserName"));
        Assert.NotNull(userNameIndex);
        Assert.False(userNameIndex.IsUnique);
        Assert.Single(userNameIndex.Columns);

        // Verify Email unique index
        var emailIndex = table.Indexes.FirstOrDefault(i => i.Columns.Any(c => c.ColumnName == "Email"));
        Assert.NotNull(emailIndex);
        Assert.True(emailIndex.IsUnique);
        Assert.Single(emailIndex.Columns);

        // Verify Status index with custom name
        var statusIndex = table.Indexes.FirstOrDefault(i => i.IndexName == "IX_custom_status");
        Assert.NotNull(statusIndex);
        Assert.False(statusIndex.IsUnique);
        Assert.Single(statusIndex.Columns);
        Assert.Equal("Status", statusIndex.Columns[0].ColumnName);

        // Also verify that the columns are marked as indexed
        var userNameColumn = table.Columns.FirstOrDefault(c => c.ColumnName == "UserName");
        Assert.NotNull(userNameColumn);
        Assert.True(userNameColumn.IsIndexed);

        var emailColumn = table.Columns.FirstOrDefault(c => c.ColumnName == "Email");
        Assert.NotNull(emailColumn);
        Assert.True(emailColumn.IsIndexed);
        Assert.True(emailColumn.IsUnique);

        var statusColumn = table.Columns.FirstOrDefault(c => c.ColumnName == "Status");
        Assert.NotNull(statusColumn);
        Assert.True(statusColumn.IsIndexed);

        // Now test that we can actually create the table with these indexes in the database
        using var db = await OpenConnectionAsync();

        await db.DropTableIfExistsAsync(table.SchemaName, table.TableName);
        await db.CreateTableIfNotExistsAsync(table);

        var tableExists = await db.DoesTableExistAsync(table.SchemaName, table.TableName);
        Assert.True(tableExists);

        // Verify indexes exist in the database
        var dbIndexes = await db.GetIndexesAsync(table.SchemaName, table.TableName);
        Assert.NotEmpty(dbIndexes);

        // Cleanup
        await db.DropTableIfExistsAsync(table.SchemaName, table.TableName);
    }

    /// <summary>
    /// Regression test: a SINGLE-column unique constraint must not force its column to NOT NULL.
    /// The pre-existing nullable-unique test only covered a two-column constraint, and
    /// DmTableFactory only sets DmColumn.IsUnique for single-column constraints, so the
    /// "unique implies NOT NULL" defect slipped through that gap.
    /// </summary>
    [Fact]
    protected virtual async Task Single_column_unique_constraint_keeps_column_nullable_Async()
    {
        var tableDef = DmTableFactory.GetTable(typeof(TestTableWithSingleColumnNullableUnique));

        var emailColumn = tableDef.Columns.Single(c =>
            c.ColumnName.Equals("email", StringComparison.OrdinalIgnoreCase)
        );
        Assert.True(emailColumn.IsNullable, "email should be nullable in the model");
        Assert.True(emailColumn.IsUnique, "a single-column unique constraint should flag the column as unique");

        using var db = await OpenConnectionAsync();
        await db.DropTableIfExistsAsync(tableDef.SchemaName, tableDef.TableName);
        await db.CreateTableIfNotExistsAsync(tableDef);

        var dbTableDef = await db.GetTableAsync(tableDef.SchemaName, tableDef.TableName);
        Assert.NotNull(dbTableDef);

        var dbEmail = dbTableDef.Columns.Single(c =>
            c.ColumnName.Equals("email", StringComparison.OrdinalIgnoreCase)
        );
        Assert.True(
            dbEmail.IsNullable,
            "a nullable column in a single-column UNIQUE constraint must remain nullable in the database"
        );

        // control column: nullable and not unique
        var dbNickname = dbTableDef.Columns.Single(c =>
            c.ColumnName.Equals("nickname", StringComparison.OrdinalIgnoreCase)
        );
        Assert.True(dbNickname.IsNullable, "nickname should remain nullable");

        await db.DropTableIfExistsAsync(tableDef.SchemaName, tableDef.TableName);
    }

    /// <summary>
    /// Regression test: an explicitly configured primary key constraint name must survive into the
    /// database. Single-column primary keys previously had their name regenerated as pk_{table}_{column}.
    /// </summary>
    [Theory]
    [InlineData(typeof(TestTableSingleColumnNamedPk), "pk_single_column_named")]
    [InlineData(typeof(TestTableClassLevelNamedPk), "pk_class_level_named")]
    [InlineData(typeof(TestTableCompositeNamedPk), "pk_composite_named")]
    protected virtual async Task Primary_key_constraint_name_is_preserved_Async(Type type, string expectedName)
    {
        var tableDef = DmTableFactory.GetTable(type);

        Assert.NotNull(tableDef.PrimaryKeyConstraint);
        Assert.Equal(expectedName, tableDef.PrimaryKeyConstraint!.ConstraintName, ignoreCase: true);

        using var db = await OpenConnectionAsync();
        await db.DropTableIfExistsAsync(tableDef.SchemaName, tableDef.TableName);
        await db.CreateTableIfNotExistsAsync(tableDef);

        var dbTableDef = await db.GetTableAsync(tableDef.SchemaName, tableDef.TableName);
        Assert.NotNull(dbTableDef);
        Assert.NotNull(dbTableDef!.PrimaryKeyConstraint);

        // MySQL and MariaDB do not support named primary key constraints: the server always calls the
        // primary key "PRIMARY" and discards any supplied name, so DapperMatic synthesizes one when
        // reading the table back. Only the model-level assertion above is meaningful there.
        if (db.GetDbProviderType() != DbProviderType.MySql)
        {
            Assert.Equal(
                expectedName,
                dbTableDef.PrimaryKeyConstraint!.ConstraintName,
                ignoreCase: true
            );
        }

        await db.DropTableIfExistsAsync(tableDef.SchemaName, tableDef.TableName);
    }

    /// <summary>
    /// Regression test: a provider data type given WITHOUT a {provider:...} prefix applies to any
    /// provider that recognises the type name, instead of being silently discarded. Providers that
    /// do not recognise it fall back to inferring a type from the .NET type, so cross-provider
    /// models keep working.
    /// </summary>
    [Fact]
    protected virtual async Task Bare_provider_data_type_applies_to_recognising_providers_Async()
    {
        var tableDef = DmTableFactory.GetTable(typeof(TestTableWithBareProviderDataType));

        // The bare type is recorded against "any provider", not dropped on the floor.
        var textColumn = tableDef.Columns.Single(c =>
            c.ColumnName.Equals("bare_text", StringComparison.OrdinalIgnoreCase)
        );
        Assert.Equal("text", textColumn.GetProviderDataType(DbProviderType.Other), ignoreCase: true);

        // A prefixed type still targets exactly one provider.
        var prefixedColumn = tableDef.Columns.Single(c =>
            c.ColumnName.Equals("prefixed_text", StringComparison.OrdinalIgnoreCase)
        );
        Assert.Equal("text", prefixedColumn.GetProviderDataType(DbProviderType.PostgreSql), ignoreCase: true);
        Assert.Null(prefixedColumn.GetProviderDataType(DbProviderType.Other));

        using var db = await OpenConnectionAsync();

        // A bare type name that this provider does NOT recognise must not break table creation.
        await db.DropTableIfExistsAsync(tableDef.SchemaName, tableDef.TableName);
        await db.CreateTableIfNotExistsAsync(tableDef);

        var dbTableDef = await db.GetTableAsync(tableDef.SchemaName, tableDef.TableName);
        Assert.NotNull(dbTableDef);
        Assert.Equal(tableDef.Columns.Count, dbTableDef!.Columns.Count);

        await db.DropTableIfExistsAsync(tableDef.SchemaName, tableDef.TableName);
    }

    /// <summary>
    /// Regression test for the shared mutable state issue: DmTableFactory caches one DmTable per .NET
    /// type, so every DmColumn is shared process-wide. Rendering DDL used to write the resolved SQL type
    /// back onto the column via SetProviderDataType, which both polluted the caller's model and raced when
    /// two providers rendered the same cached table concurrently ("Operations that change non-concurrent
    /// collections must have exclusive access"). Rendering must not mutate its input.
    /// </summary>
    [Fact]
    protected virtual async Task Creating_a_table_does_not_mutate_the_cached_model_Async()
    {
        var tableDef = DmTableFactory.GetTable(typeof(TestTableForCacheMutation));

        // The factory hands out one shared instance per type.
        Assert.Same(tableDef, DmTableFactory.GetTable(typeof(TestTableForCacheMutation)));

        var before = tableDef.Columns.ToDictionary(
            c => c.ColumnName,
            c => string.Join(
                ",",
                c.ProviderDataTypes.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}")
            ),
            StringComparer.OrdinalIgnoreCase
        );

        using var db = await OpenConnectionAsync();
        await db.DropTableIfExistsAsync(tableDef.SchemaName, tableDef.TableName);
        await db.CreateTableIfNotExistsAsync(tableDef);

        var after = DmTableFactory.GetTable(typeof(TestTableForCacheMutation));
        foreach (var column in after.Columns)
        {
            var actual = string.Join(
                ",",
                column.ProviderDataTypes.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}")
            );
            Assert.Equal(before[column.ColumnName], actual);
        }

        await db.DropTableIfExistsAsync(tableDef.SchemaName, tableDef.TableName);
    }

    /// <summary>
    /// <c>configureColumn</c> adjusts the column being created, not the model it was taken from:
    /// <see cref="DmTableFactory"/> shares that model process-wide, so a change made to it would carry into
    /// every later use of the type, on every connection and thread.
    /// </summary>
    [Fact]
    protected virtual async Task Configuring_a_column_taken_from_a_model_leaves_the_model_unchanged_Async()
    {
        var model = DmTableFactory.GetTable(typeof(TestTableForColumnConfiguration));
        var before = DmTableFingerprint.GetCanonicalForm(model);

        using var db = await OpenConnectionAsync();
        await db.DropTableIfExistsAsync(null, model.TableName);
        await db.CreateTableIfNotExistsAsync(
            null,
            model.TableName,
            [new DmColumn(null, model.TableName, "id", typeof(int), isPrimaryKey: true)]
        );

        // Another provider's type, so that setting it leaves what this provider creates alone.
        var otherProvider =
            db.GetDbProviderType() == DbProviderType.Sqlite ? DbProviderType.PostgreSql : DbProviderType.Sqlite;

        var created = await db.CreateColumnIfNotExistsAsync<TestTableForColumnConfiguration>(
            t => t.Code,
            c =>
            {
                c.IsNullable = true;
                c.PreviousNames.Add("old_code");
                c.SetProviderDataType(otherProvider, "text");
            }
        );

        Assert.True(created);
        var column = await db.GetColumnAsync(null, model.TableName, "code");
        Assert.NotNull(column);
        Assert.True(column.IsNullable);
        Assert.Equal(before, DmTableFingerprint.GetCanonicalForm(model));

        await db.DropTableIfExistsAsync(null, model.TableName);
    }
}

[Table("TestTable1")]
public class TestDao1
{
    [Key]
    public Guid Id { get; set; }
}

[Table("TestTable2", Schema = "my_app")]
public class TestDao2
{
    [Key]
    public Guid Id { get; set; }
}

[DmTable(tableName: "TestTable3")]
public class TestDao3
{
    [DmPrimaryKeyConstraint]
    public Guid Id { get; set; }
}

[DmPrimaryKeyConstraint([nameof(TestTable4.Id)])]
public class TestTable4
{
    public Guid Id { get; set; }

    // create column of all supported types
    [DmColumn("StringColumn", providerDataType: "nvarchar", length: 100, isNullable: false)]
    public string StringColumn { get; set; } = null!;

    [DmColumn("int_column", providerDataType: "int", isNullable: false)]
    public int IntColumn { get; set; }

    [DmColumn(isUnique: false, isNullable: false)]
    public long LongColumn { get; set; }
    public short ShortColumn { get; set; }
    public byte ByteColumn { get; set; }
    public decimal DecimalColumn { get; set; }
    public double DoubleColumn { get; set; }
    public float FloatColumn { get; set; }
    public bool BoolColumn { get; set; }
    public DateTime DateTimeColumn { get; set; }
    public DateTimeOffset DateTimeOffsetColumn { get; set; }
    public TimeSpan TimeSpanColumn { get; set; }

    [DmColumn(isNullable: false)]
    public byte[] ByteArrayColumn { get; set; } = null!;
    public Guid GuidColumn { get; set; }
    public char CharColumn { get; set; }

    [DmColumn(isNullable: false)]
    public char[] CharArrayColumn { get; set; } = null!;

    [DmColumn(isNullable: false)]
    public object ObjectColumn { get; set; } = null!;

    // create column of all supported nullable types

    [DmColumn(isNullable: true)]
    public string? NullableStringColumn { get; set; }
    public int? NullableIntColumn { get; set; }
    public long? NullableLongColumn { get; set; }
    public short? NullableShortColumn { get; set; }
    public byte? NullableByteColumn { get; set; }
    public decimal? NullableDecimalColumn { get; set; }
    public double? NullableDoubleColumn { get; set; }
    public float? NullableFloatColumn { get; set; }
    public bool? NullableBoolColumn { get; set; }
    public DateTime? NullableDateTimeColumn { get; set; }
    public DateTimeOffset? NullableDateTimeOffsetColumn { get; set; }
    public TimeSpan? NullableTimeSpanColumn { get; set; }
    public byte[]? NullableByteArrayColumn { get; set; }
    public Guid? NullableGuidColumn { get; set; }
    public char? NullableCharColumn { get; set; }
    public char[]? NullableCharArrayColumn { get; set; }
    public object? NullableObjectColumn { get; set; }

    // create columns of all enumerable types
    [DmColumn(isNullable: false)]
    public IDictionary<string, string> IDictionaryColumn { get; set; } = null!;
    public IDictionary<string, string>? NullableIDictionaryColumn { get; set; }

    [DmColumn(isNullable: false)]
    public Dictionary<string, string> DictionaryColumn { get; set; } = null!;
    public Dictionary<string, string>? NullableDictionaryColumn { get; set; }

    [DmColumn(isNullable: false)]
    public IDictionary<string, object> IObjectDictionaryColumn { get; set; } = null!;
    public IDictionary<string, object>? NullableIObjectDictionaryColumn { get; set; }

    [DmColumn(isNullable: false)]
    public Dictionary<string, object> ObjectDictionaryColumn { get; set; } = null!;
    public Dictionary<string, object>? NullableObjectDictionaryColumn { get; set; }

    [DmColumn(isNullable: false)]
    public IList<string> IListColumn { get; set; } = null!;
    public IList<string>? NullableIListColumn { get; set; }

    [DmColumn(isNullable: false)]
    public List<string> ListColumn { get; set; } = null!;
    public List<string>? NullableListColumn { get; set; }

    [DmColumn(isNullable: false)]
    public ICollection<string> ICollectionColumn { get; set; } = null!;
    public ICollection<string>? NullableICollectionColumn { get; set; }

    [DmColumn(isNullable: false)]
    public Collection<string> CollectionColumn { get; set; } = null!;
    public Collection<string>? NullableCollectionColumn { get; set; }

    [DmColumn(isNullable: false)]
    public IEnumerable<string> IEnumerableColumn { get; set; } = null!;
    public IEnumerable<string>? NullableIEnumerableColumn { get; set; }

    // create columns of arrays
    [DmColumn(isNullable: false)]
    public string[] StringArrayColumn { get; set; } = null!;
    public string[]? NullableStringArrayColumn { get; set; }

    [DmColumn(isNullable: false)]
    public int[] IntArrayColumn { get; set; } = null!;
    public int[]? NullableIntArrayColumn { get; set; }

    [DmColumn(isNullable: false)]
    public long[] LongArrayColumn { get; set; } = null!;
    public long[]? NullableLongArrayColumn { get; set; }

    [DmColumn(isNullable: false)]
    public Guid[] GuidArrayColumn { get; set; } = null!;
    public Guid[]? NullableGuidArrayColumn { get; set; }

    // create columns of enums, structs and classes
    public TestEnum EnumColumn { get; set; }
    public TestEnum? NullableEnumColumn { get; set; }
    public TestStruct StructColumn { get; set; }
    public TestStruct? NullableStructColumn { get; set; }

    [DmColumn(isNullable: false)]
    public TestClass ClassColumn { get; set; } = null!;
    public TestClass? NullableClassColumn { get; set; }

    [DmColumn(isNullable: false)]
    public TestInterface InterfaceColumn { get; set; } = null!;
    public TestInterface? NullableInterfaceColumn { get; set; }

    [DmColumn(isNullable: false)]
    public TestAbstractClass AbstractClassColumn { get; set; } = null!;
    public TestAbstractClass? NullableAbstractClassColumn { get; set; }

    [DmColumn(isNullable: false)]
    public TestConcreteClass ConcreteClass { get; set; } = null!;
    public TestConcreteClass? NullableConcreteClass { get; set; }

    // Modern .NET types (automatic mapping)
    [DmColumn(isNullable: false)]
    public JsonDocument JsonDocumentColumn { get; set; } = null!;
    public JsonDocument? NullableJsonDocumentColumn { get; set; }

    public JsonElement JsonElementColumn { get; set; }
    public JsonElement? NullableJsonElementColumn { get; set; }

    public DateOnly DateOnlyColumn { get; set; }
    public DateOnly? NullableDateOnlyColumn { get; set; }

    public TimeOnly TimeOnlyColumn { get; set; }
    public TimeOnly? NullableTimeOnlyColumn { get; set; }

    [DmColumn(isNullable: false)]
    public ReadOnlyMemory<byte> ReadOnlyMemoryByteColumn { get; set; }
    public ReadOnlyMemory<byte>? NullableReadOnlyMemoryByteColumn { get; set; }

    [DmColumn(isNullable: false)]
    public Memory<byte> MemoryByteColumn { get; set; }
    public Memory<byte>? NullableMemoryByteColumn { get; set; }

    [DmColumn(isNullable: false)]
    public Stream StreamColumn { get; set; } = null!;
    public Stream? NullableStreamColumn { get; set; }
}

public enum TestEnum
{
    Value1,
    Value2,
    Value3,
}

public struct TestStruct
{
    public int Value { get; set; }
}

public class TestClass
{
    public int Value { get; set; }
}

public interface TestInterface
{
    int Value { get; set; }
}

public abstract class TestAbstractClass
{
    public int Value { get; set; }
}

public class TestConcreteClass : TestAbstractClass
{
    public int Value2 { get; set; }
}

/// <summary>
/// Test class demonstrating explicit providerDataType designation.
/// This tests the advanced use case where users need precise control over database column types.
/// </summary>
[DmPrimaryKeyConstraint([nameof(Id)])]
public class TestTable5
{
    public Guid Id { get; set; }

    // String type variations with explicit types
    [DmColumn("FixedLengthString", providerDataType: "nvarchar", length: 50, isNullable: false)]
    public string FixedLengthString { get; set; } = null!;

    [DmColumn("MaxLengthString", providerDataType: "nvarchar(max)", isNullable: false)]
    public string MaxLengthString { get; set; } = null!;

    [DmColumn("TextString", providerDataType: "text", isNullable: false)]
    public string TextString { get; set; } = null!;

    [DmColumn("NonUnicodeString", providerDataType: "varchar", length: 100, isNullable: false)]
    public string? NonUnicodeString { get; set; }

    // Decimal type variations with different precision/scale
    [DmColumn("CurrencyDecimal", providerDataType: "decimal", precision: 18, scale: 2, isNullable: false)]
    public decimal CurrencyDecimal { get; set; }

    [DmColumn("HighPrecisionDecimal", providerDataType: "decimal", precision: 10, scale: 4, isNullable: false)]
    public decimal HighPrecisionDecimal { get; set; }

    [DmColumn("MoneyType", providerDataType: "money", isNullable: false)]
    public decimal? MoneyType { get; set; }

    // Provider-specific type mapping for cross-database compatibility
    [DmColumn(
        "LargeText",
        providerDataType: "{sqlserver:nvarchar(max),mysql:longtext,postgresql:text,sqlite:text}",
        isNullable: false
    )]
    public string LargeText { get; set; } = null!;

    [DmColumn(
        "JsonData",
        providerDataType: "{postgresql:jsonb,mysql:json,sqlserver:nvarchar(max),sqlite:text}",
        isNullable: false
    )]
    public string? JsonData { get; set; }

    // Binary type variations
    [DmColumn("LargeBinary", providerDataType: "varbinary(max)", isNullable: false)]
    public byte[]? LargeBinary { get; set; }

    [DmColumn("FixedBinary", providerDataType: "varbinary", length: 256, isNullable: false)]
    public byte[] FixedBinary { get; set; } = null!;

    [DmColumn("ImageBinary", providerDataType: "image", isNullable: false)]
    public byte[]? ImageBinary { get; set; }

    // DateTime type variations
    [DmColumn("Date", providerDataType: "date", isNullable: false)]
    public DateTime Date { get; set; }

    [DmColumn("Time", providerDataType: "time", isNullable: false)]
    public TimeSpan Time { get; set; }

    [DmColumn("DateTime2", providerDataType: "datetime2", isNullable: false)]
    public DateTime DateTime2 { get; set; }

    [DmColumn("SmallDateTime", providerDataType: "smalldatetime", isNullable: false)]
    public DateTime? SmallDateTime { get; set; }

    // Integer type variations
    [DmColumn("TinyIntColumn", providerDataType: "tinyint", isNullable: false)]
    public byte TinyIntColumn { get; set; }

    [DmColumn("SmallIntColumn", providerDataType: "smallint", isNullable: false)]
    public short SmallIntColumn { get; set; }

    [DmColumn("BigIntColumn", providerDataType: "bigint", isNullable: false)]
    public long BigIntColumn { get; set; }

    // Floating point variations
    [DmColumn("RealColumn", providerDataType: "real", isNullable: false)]
    public float RealColumn { get; set; }

    [DmColumn("FloatColumn", providerDataType: "float", isNullable: false)]
    public double FloatColumn { get; set; }

    // Boolean variations
    [DmColumn("BitColumn", providerDataType: "bit", isNullable: false)]
    public bool BitColumn { get; set; }

    // GUID variations
    [DmColumn("UniqueIdentifier", providerDataType: "uniqueidentifier", isNullable: false)]
    public Guid UniqueIdentifier { get; set; }

    // XML type (SQL Server specific)
    [DmColumn("XmlData", providerDataType: "xml", isNullable: false)]
    public string? XmlData { get; set; }
}

/// <summary>
/// Test table specifically for testing parameterized types in multi-provider format.
/// Tests the bug fix for parsing types like decimal(19,4) that contain commas.
/// </summary>
[Table("TestTable6")]
public class TestTable6
{
    [DmColumn("Id", isAutoIncrement: true, isPrimaryKey: true)]
    public int Id { get; set; }

    // Test case from documentation: {sqlserver:money,mysql:decimal(19,4),postgresql:money,sqlite:real}
    [DmColumn(
        "Price",
        providerDataType: "{sqlserver:money,mysql:decimal(19,4),postgresql:money,sqlite:real}",
        isNullable: false
    )]
    public decimal Price { get; set; }

    // Test parameterized decimal types across providers
    [DmColumn(
        "Amount",
        providerDataType: "{mysql:decimal(10,2),sqlserver:decimal(12,4),postgresql:numeric(10,2),sqlite:real}",
        isNullable: false
    )]
    public decimal Amount { get; set; }

    // Test parameterized numeric types with higher precision
    [DmColumn(
        "Quantity",
        providerDataType: "{mysql:numeric(18,6),postgresql:numeric(18,6),sqlserver:decimal(18,6),sqlite:real}",
        isNullable: false
    )]
    public decimal Quantity { get; set; }

    // Test varchar with length parameter (no comma inside parentheses)
    [DmColumn(
        "Description",
        providerDataType: "{mysql:varchar(255),sqlserver:nvarchar(255),postgresql:varchar(255),sqlite:text}",
        isNullable: false
    )]
    public string Description { get; set; } = null!;

    // Test array types for PostgreSQL
    [DmColumn(
        "NullableTags",
        providerDataType: "{postgresql:integer[],mysql:json,sqlserver:nvarchar(max),sqlite:text}",
        isNullable: true
    )]
    public string? Tags { get; set; }

    // Test mixed simple and parameterized types
    [DmColumn(
        "Status",
        providerDataType: "{mysql:varchar(50),sqlserver:nvarchar(50),postgresql:varchar(50),sqlite:text}",
        isNullable: false
    )]
    public string Status { get; set; } = null!;
}

/// <summary>
/// Test table for verifying that nullable columns in unique indexes remain nullable.
/// This mirrors a real-world scenario where a user has a unique index on a nullable column.
/// </summary>
[DmTable(tableName: "test_users_with_nullable_unique")]
[DmIndex(columnNames: new[] { "tenant_id", "email" }, isUnique: true, indexName: "IX_test_users_tenant_email")]
[DmUniqueConstraint(columnNames: new[] { "tenant_id", "phone_number" }, constraintName: "UX_test_users_tenant_phone")]
public class TestTableWithNullableUniqueIndex
{
    [DmColumn("id", isPrimaryKey: true)]
    public Guid Id { get; set; }

    [DmColumn("tenant_id")]
    public Guid TenantId { get; set; }

    [DmColumn("email", length: 320)]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// This nullable column is part of a unique index.
    /// It should remain nullable when the table is created.
    /// </summary>
    [DmColumn("phone_number", length: 20, isNullable: true)]
    public string? PhoneNumber { get; set; }

    [DmColumn("display_name", length: 200, isNullable: true)]
    public string? DisplayName { get; set; }
}

/// <summary>
/// Test models that mirror the RoleMember class structure with Guid? columns in unique indexes and foreign keys.
/// </summary>
[DmTable(tableName: "test_tenants")]
public class TestTenant
{
    [DmColumn("id", isPrimaryKey: true)]
    public Guid Id { get; set; }
}

[DmTable(tableName: "test_roles")]
public class TestRole
{
    [DmColumn("id", isPrimaryKey: true)]
    public Guid Id { get; set; }
}

[DmTable(tableName: "test_users")]
public class TestUser
{
    [DmColumn("id", isPrimaryKey: true)]
    public Guid Id { get; set; }
}

[DmTable(tableName: "test_groups")]
public class TestGroup
{
    [DmColumn("id", isPrimaryKey: true)]
    public Guid Id { get; set; }
}

/// <summary>
/// Test table that mirrors the RoleMember class with:
/// - Nullable Guid? columns (user_id, group_id)
/// - Multiple unique indexes containing nullable columns
/// - Foreign key constraints on nullable columns
/// </summary>
[DmTable(tableName: "test_role_members")]
[DmIndex(columnNames: ["role_id", "user_id"], isUnique: true, indexName: "UX_test_role_members_role_id_user_id")]
[DmIndex(columnNames: ["role_id", "group_id"], isUnique: true, indexName: "UX_test_role_members_role_id_group_id")]
[DmIndex(columnNames: ["tenant_id", "role_id", "user_id"], indexName: "IX_test_role_members_tenant_id_role_id_user_id")]
[DmIndex(
    columnNames: ["tenant_id", "role_id", "group_id"],
    indexName: "IX_test_role_members_tenant_id_role_id_group_id"
)]
[DmIndex(columnNames: ["tenant_id", "is_tenant_group"], indexName: "IX_test_role_members_tenant_id_is_tenant_group")]
[DmForeignKeyConstraint(
    sourceColumnNames: ["tenant_id"],
    referencedType: typeof(TestTenant),
    referencedColumnNames: ["id"],
    constraintName: "FK_test_role_members_tenant_id",
    onDelete: DmForeignKeyAction.Cascade
)]
[DmForeignKeyConstraint(
    sourceColumnNames: ["user_id"],
    referencedType: typeof(TestUser),
    referencedColumnNames: ["id"],
    constraintName: "FK_test_role_members_user_id",
    onDelete: DmForeignKeyAction.Cascade
)]
[DmForeignKeyConstraint(
    sourceColumnNames: ["group_id"],
    referencedType: typeof(TestGroup),
    referencedColumnNames: ["id"],
    constraintName: "FK_test_role_members_group_id",
    onDelete: DmForeignKeyAction.Cascade
)]
[DmForeignKeyConstraint(
    sourceColumnNames: ["role_id"],
    referencedType: typeof(TestRole),
    referencedColumnNames: ["id"],
    constraintName: "FK_test_role_members_role_id",
    onDelete: DmForeignKeyAction.Cascade
)]
public class TestRoleMember
{
    [DmColumn("id", isPrimaryKey: true)]
    public Guid Id { get; set; }

    [DmColumn("tenant_id")]
    public Guid TenantId { get; set; }

    [DmColumn("role_id")]
    public Guid RoleId { get; set; }

    /// <summary>
    /// This nullable Guid? column is part of a unique index and has a foreign key.
    /// It should remain nullable when the table is created.
    /// </summary>
    [DmColumn("user_id", isNullable: true)]
    public Guid? UserId { get; set; }

    /// <summary>
    /// This nullable Guid? column is part of a unique index and has a foreign key.
    /// It should remain nullable when the table is created.
    /// </summary>
    [DmColumn("group_id", isNullable: true)]
    public Guid? GroupId { get; set; }

    [DmColumn("is_tenant_group", isNullable: true)]
    public bool? IsTenantGroup { get; set; }
}

[DmTable(tableName: "test_property_indexes")]
public class TestPropertyLevelIndexes
{
    [DmColumn(isPrimaryKey: true, isAutoIncrement: true)]
    public int Id { get; set; }

    // Property-level index without specifying column names
    [DmColumn(length: 255)]
    [DmIndex]
    public string? UserName { get; set; }

    // Property-level unique index
    [DmColumn(length: 255)]
    [DmIndex(isUnique: true)]
    public string? Email { get; set; }

    // Property-level index with custom name
    [DmColumn(length: 255)]
    [DmIndex(indexName: "IX_custom_status")]
    public string? Status { get; set; }

    // Regular property without index
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Test table for a SINGLE-column unique constraint on a nullable column.
/// </summary>
[DmTable(tableName: "test_single_col_nullable_unique")]
[DmUniqueConstraint(columnNames: new[] { "email" }, constraintName: "UX_test_single_col_email")]
public class TestTableWithSingleColumnNullableUnique
{
    [DmColumn("id", isPrimaryKey: true)]
    public Guid Id { get; set; }

    [DmColumn("email", length: 320, isNullable: true)]
    public string? Email { get; set; }

    [DmColumn("nickname", length: 200, isNullable: true)]
    public string? Nickname { get; set; }
}

/// <summary>
/// Single-column primary key named via a property-level attribute.
/// </summary>
[DmTable(tableName: "test_single_column_named_pk")]
public class TestTableSingleColumnNamedPk
{
    [DmPrimaryKeyConstraint(constraintName: "pk_single_column_named")]
    [DmColumn("id")]
    public int Id { get; set; }
}

/// <summary>
/// Single-column primary key named via a class-level attribute that supplies only the name.
/// </summary>
[DmTable(tableName: "test_class_level_named_pk")]
[DmPrimaryKeyConstraint(constraintName: "pk_class_level_named")]
public class TestTableClassLevelNamedPk
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }
}

/// <summary>
/// Composite primary key with an explicit name.
/// </summary>
[DmTable(tableName: "test_composite_named_pk")]
[DmPrimaryKeyConstraint(new[] { "part_a", "part_b" }, "pk_composite_named")]
public class TestTableCompositeNamedPk
{
    [DmColumn("part_a")]
    public int PartA { get; set; }

    [DmColumn("part_b")]
    public int PartB { get; set; }
}

/// <summary>
/// Test table exercising provider data types with and without a {provider:...} prefix.
/// </summary>
[DmTable(tableName: "test_bare_provider_data_type")]
public class TestTableWithBareProviderDataType
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }

    // No provider prefix: applies to any provider that recognises "text".
    [DmColumn("bare_text", providerDataType: "text", isNullable: true)]
    public string? BareText { get; set; }

    // Prefixed: targets PostgreSQL only.
    [DmColumn("prefixed_text", providerDataType: "{postgresql:text}", isNullable: true)]
    public string? PrefixedText { get; set; }

    // A bare type most providers will not recognise; they must fall back to .NET type inference.
    [DmColumn("unknown_bare", providerDataType: "datetime2", isNullable: true)]
    public DateTime? UnknownBare { get; set; }
}

/// <summary>
/// Test table used to verify that rendering DDL does not mutate the cached model.
/// Includes an autoincrement key, since SQLite has its own type-resolution override for those.
/// </summary>
[DmTable(tableName: "test_cache_mutation")]
public class TestTableForCacheMutation
{
    [DmColumn("id", isPrimaryKey: true, isAutoIncrement: true)]
    public int Id { get; set; }

    [DmColumn("name", length: 100)]
    public string Name { get; set; } = string.Empty;

    [DmColumn("notes", isNullable: true)]
    public string? Notes { get; set; }
}

/// <summary>
/// Test table whose column is created with a <c>configureColumn</c> callback.
/// </summary>
[DmTable(tableName: "test_column_configuration")]
public class TestTableForColumnConfiguration
{
    [DmColumn("id", isPrimaryKey: true)]
    public int Id { get; set; }

    [DmColumn("code", length: 20)]
    public string Code { get; set; } = string.Empty;
}
