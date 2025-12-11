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
    protected virtual async Task Can_perform_simple_crud_on_unique_constraints_Async(string? schemaName)
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, schemaName);

        var tableName = "testWithUc" + DateTime.Now.Ticks;
        var columnName = "testColumn";
        var columnName2 = "testColumn2";
        var uniqueConstraintName = "testUc";
        var uniqueConstraintName2 = "testUc2";

        await db.CreateTableIfNotExistsAsync(
            schemaName,
            tableName,
            [
                new DmColumn(schemaName, tableName, columnName, typeof(int), defaultExpression: "1", isNullable: false),
                new DmColumn(
                    schemaName,
                    tableName,
                    columnName2,
                    typeof(int),
                    defaultExpression: "1",
                    isNullable: false
                ),
            ],
            uniqueConstraints:
            [
                new DmUniqueConstraint(
                    schemaName,
                    tableName,
                    uniqueConstraintName2,
                    [new DmOrderedColumn(columnName2)]
                ),
            ]
        );

        Output.WriteLine("Unique Constraint Exists: {0}.{1}", tableName, uniqueConstraintName);
        var exists = await db.DoesUniqueConstraintExistAsync(schemaName, tableName, uniqueConstraintName);
        Assert.False(exists);

        Output.WriteLine("Unique Constraint2 Exists: {0}.{1}", tableName, uniqueConstraintName2);
        exists = await db.DoesUniqueConstraintExistAsync(schemaName, tableName, uniqueConstraintName2);
        Assert.True(exists);
        exists = await db.DoesUniqueConstraintExistOnColumnAsync(schemaName, tableName, columnName2);
        Assert.True(exists);

        Output.WriteLine("Creating unique constraint: {0}.{1}", tableName, uniqueConstraintName);
        await db.CreateUniqueConstraintIfNotExistsAsync(
            schemaName,
            tableName,
            uniqueConstraintName,
            [new DmOrderedColumn(columnName)]
        );

        // make sure the new constraint is there
        Output.WriteLine("Unique Constraint Exists: {0}.{1}", tableName, uniqueConstraintName);
        exists = await db.DoesUniqueConstraintExistAsync(schemaName, tableName, uniqueConstraintName);
        Assert.True(exists);
        exists = await db.DoesUniqueConstraintExistOnColumnAsync(schemaName, tableName, columnName);
        Assert.True(exists);

        // make sure the original constraint is still there
        Output.WriteLine("Unique Constraint Exists: {0}.{1}", tableName, uniqueConstraintName2);
        exists = await db.DoesUniqueConstraintExistAsync(schemaName, tableName, uniqueConstraintName2);
        Assert.True(exists);
        exists = await db.DoesUniqueConstraintExistOnColumnAsync(schemaName, tableName, columnName2);
        Assert.True(exists);

        Output.WriteLine("Get Unique Constraint Names: {0}", tableName);
        var uniqueConstraintNames = await db.GetUniqueConstraintNamesAsync(schemaName, tableName);
        Assert.Contains(uniqueConstraintName2, uniqueConstraintNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(uniqueConstraintName, uniqueConstraintNames, StringComparer.OrdinalIgnoreCase);

        var uniqueConstraints = await db.GetUniqueConstraintsAsync(schemaName, tableName);
        Assert.Contains(
            uniqueConstraints,
            uc => uc.ConstraintName.Equals(uniqueConstraintName2, StringComparison.OrdinalIgnoreCase)
        );
        Assert.Contains(
            uniqueConstraints,
            uc => uc.ConstraintName.Equals(uniqueConstraintName, StringComparison.OrdinalIgnoreCase)
        );

        Output.WriteLine("Dropping unique constraint: {0}.{1}", tableName, uniqueConstraintName);
        await db.DropUniqueConstraintIfExistsAsync(schemaName, tableName, uniqueConstraintName);

        Output.WriteLine("Unique Constraint Exists: {0}.{1}", tableName, uniqueConstraintName);
        exists = await db.DoesUniqueConstraintExistAsync(schemaName, tableName, uniqueConstraintName);
        Assert.False(exists);

        // test key ordering
        tableName = "testWithUc2";
        uniqueConstraintName = "uq_testWithUc2";
        await db.CreateTableIfNotExistsAsync(
            schemaName,
            tableName,
            [
                new DmColumn(schemaName, tableName, columnName, typeof(int), defaultExpression: "1", isNullable: false),
                new DmColumn(
                    schemaName,
                    tableName,
                    columnName2,
                    typeof(int),
                    defaultExpression: "1",
                    isNullable: false
                ),
            ],
            uniqueConstraints:
            [
                new DmUniqueConstraint(
                    schemaName,
                    tableName,
                    uniqueConstraintName,
                    [new DmOrderedColumn(columnName2), new DmOrderedColumn(columnName, DmColumnOrder.Descending)]
                ),
            ]
        );

        var uniqueConstraint = await db.GetUniqueConstraintAsync(schemaName, tableName, uniqueConstraintName);
        Assert.NotNull(uniqueConstraint);
        Assert.NotNull(uniqueConstraint.Columns);
        Assert.Equal(2, uniqueConstraint.Columns.Count);
        Assert.Equal(columnName2, uniqueConstraint.Columns[0].ColumnName, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(DmColumnOrder.Ascending, uniqueConstraint.Columns[0].Order);
        Assert.Equal(columnName, uniqueConstraint.Columns[1].ColumnName, StringComparer.OrdinalIgnoreCase);
        if (await db.SupportsOrderedKeysInConstraintsAsync())
        {
            Assert.Equal(DmColumnOrder.Descending, uniqueConstraint.Columns[1].Order);
        }
        await db.DropTableIfExistsAsync(schemaName, tableName);
    }

    /// <summary>
    /// Tests that a unique constraint on a nullable column preserves the column's nullability.
    /// This is a regression test to ensure that adding a unique constraint does not
    /// incorrectly force the column to become NOT NULL.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("my_app")]
    protected virtual async Task Unique_constraint_on_nullable_column_preserves_nullability_Async(string? schemaName)
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, schemaName);

        var tableName = "testNullableUc" + DateTime.Now.Ticks;
        var idColumnName = "id";
        var nullableColumnName = "phone_number";
        var uniqueConstraintName = "uq_phone";

        // Create table with a nullable column that has a unique constraint
        await db.CreateTableIfNotExistsAsync(
            schemaName,
            tableName,
            [
                new DmColumn(
                    schemaName,
                    tableName,
                    idColumnName,
                    typeof(int),
                    isPrimaryKey: true,
                    isAutoIncrement: true
                ),
                new DmColumn(schemaName, tableName, nullableColumnName, typeof(string), length: 20, isNullable: true),
            ],
            uniqueConstraints:
            [
                new DmUniqueConstraint(
                    schemaName,
                    tableName,
                    uniqueConstraintName,
                    [new DmOrderedColumn(nullableColumnName)]
                ),
            ]
        );

        // Verify the table was created
        var tableExists = await db.DoesTableExistAsync(schemaName, tableName);
        Assert.True(tableExists);

        // Verify the unique constraint exists
        var ucExists = await db.DoesUniqueConstraintExistAsync(schemaName, tableName, uniqueConstraintName);
        Assert.True(ucExists);

        // CRITICAL: Verify the column is still nullable after adding the unique constraint
        var table = await db.GetTableAsync(schemaName, tableName);
        Assert.NotNull(table);

        var nullableColumn = table.Columns.FirstOrDefault(c =>
            c.ColumnName.Equals(nullableColumnName, StringComparison.OrdinalIgnoreCase)
        );
        Assert.NotNull(nullableColumn);
        Assert.True(
            nullableColumn.IsNullable,
            $"Column '{nullableColumnName}' should remain nullable even with a unique constraint"
        );

        // Cleanup
        await db.DropTableIfExistsAsync(schemaName, tableName);
    }

    /// <summary>
    /// Tests that a unique index on a nullable column preserves the column's nullability.
    /// This is a regression test to ensure that adding a unique index does not
    /// incorrectly force the column to become NOT NULL.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("my_app")]
    protected virtual async Task Unique_index_on_nullable_column_preserves_nullability_Async(string? schemaName)
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, schemaName);

        var tableName = "testNullableIdx" + DateTime.Now.Ticks;
        var idColumnName = "id";
        var nullableColumnName = "phone_number";
        var uniqueIndexName = "ix_phone_unique";

        // Create table with a nullable column
        await db.CreateTableIfNotExistsAsync(
            schemaName,
            tableName,
            [
                new DmColumn(
                    schemaName,
                    tableName,
                    idColumnName,
                    typeof(int),
                    isPrimaryKey: true,
                    isAutoIncrement: true
                ),
                new DmColumn(schemaName, tableName, nullableColumnName, typeof(string), length: 20, isNullable: true),
            ]
        );

        // Add a unique index on the nullable column
        await db.CreateIndexIfNotExistsAsync(
            schemaName,
            tableName,
            uniqueIndexName,
            [new DmOrderedColumn(nullableColumnName)],
            isUnique: true
        );

        // Verify the table was created
        var tableExists = await db.DoesTableExistAsync(schemaName, tableName);
        Assert.True(tableExists);

        // Verify the unique index exists
        var indexExists = await db.DoesIndexExistAsync(schemaName, tableName, uniqueIndexName);
        Assert.True(indexExists);

        // CRITICAL: Verify the column is still nullable after adding the unique index
        var table = await db.GetTableAsync(schemaName, tableName);
        Assert.NotNull(table);

        var nullableColumn = table.Columns.FirstOrDefault(c =>
            c.ColumnName.Equals(nullableColumnName, StringComparison.OrdinalIgnoreCase)
        );
        Assert.NotNull(nullableColumn);
        Assert.True(
            nullableColumn.IsNullable,
            $"Column '{nullableColumnName}' should remain nullable even with a unique index"
        );

        // Cleanup
        await db.DropTableIfExistsAsync(schemaName, tableName);
    }

    /// <summary>
    /// Tests that a composite unique constraint with nullable columns preserves nullability.
    /// This tests the scenario from the User class with tenant_id + phone_number composite unique.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("my_app")]
    protected virtual async Task Composite_unique_constraint_with_nullable_column_preserves_nullability_Async(
        string? schemaName
    )
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, schemaName);

        var tableName = "testCompositeNullableUc" + DateTime.Now.Ticks;
        var idColumnName = "id";
        var tenantIdColumnName = "tenant_id";
        var phoneColumnName = "phone_number";
        var uniqueConstraintName = "uq_tenant_phone";

        // Create table with a composite unique constraint where one column is nullable
        await db.CreateTableIfNotExistsAsync(
            schemaName,
            tableName,
            [
                new DmColumn(
                    schemaName,
                    tableName,
                    idColumnName,
                    typeof(int),
                    isPrimaryKey: true,
                    isAutoIncrement: true
                ),
                new DmColumn(schemaName, tableName, tenantIdColumnName, typeof(Guid), isNullable: false),
                new DmColumn(schemaName, tableName, phoneColumnName, typeof(string), length: 20, isNullable: true),
            ],
            uniqueConstraints:
            [
                new DmUniqueConstraint(
                    schemaName,
                    tableName,
                    uniqueConstraintName,
                    [new DmOrderedColumn(tenantIdColumnName), new DmOrderedColumn(phoneColumnName)]
                ),
            ]
        );

        // Verify the table was created
        var tableExists = await db.DoesTableExistAsync(schemaName, tableName);
        Assert.True(tableExists);

        // Verify the unique constraint exists
        var ucExists = await db.DoesUniqueConstraintExistAsync(schemaName, tableName, uniqueConstraintName);
        Assert.True(ucExists);

        // Get the table definition
        var table = await db.GetTableAsync(schemaName, tableName);
        Assert.NotNull(table);

        // Verify tenant_id is NOT nullable (as specified)
        var tenantColumn = table.Columns.FirstOrDefault(c =>
            c.ColumnName.Equals(tenantIdColumnName, StringComparison.OrdinalIgnoreCase)
        );
        Assert.NotNull(tenantColumn);
        Assert.False(tenantColumn.IsNullable, $"Column '{tenantIdColumnName}' should be NOT NULL as specified");

        // CRITICAL: Verify phone_number is still nullable
        var phoneColumn = table.Columns.FirstOrDefault(c =>
            c.ColumnName.Equals(phoneColumnName, StringComparison.OrdinalIgnoreCase)
        );
        Assert.NotNull(phoneColumn);
        Assert.True(
            phoneColumn.IsNullable,
            $"Column '{phoneColumnName}' should remain nullable even in a composite unique constraint"
        );

        // Cleanup
        await db.DropTableIfExistsAsync(schemaName, tableName);
    }

    /// <summary>
    /// Tests that nullable columns in complex scenarios remain nullable even when they are part of:
    /// - Multiple unique indexes
    /// - Multiple non-unique indexes
    /// - Foreign key constraints
    /// This is a regression test based on the RoleMember class scenario.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("my_app")]
    protected virtual async Task Nullable_column_with_multiple_indexes_and_fk_preserves_nullability_Async(
        string? schemaName
    )
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, schemaName);

        // Create referenced table (simplified version of Role/Group/User/Tenant)
        var referencedTableName = "ref_table" + DateTime.Now.Ticks;
        await db.CreateTableIfNotExistsAsync(
            schemaName,
            referencedTableName,
            [
                new DmColumn(
                    schemaName,
                    referencedTableName,
                    "id",
                    typeof(Guid),
                    isPrimaryKey: true
                ),
            ]
        );

        // Create main table similar to RoleMember with nullable group_id
        var tableName = "test_role_member" + DateTime.Now.Ticks;
        var idColumnName = "id";
        var tenantIdColumnName = "tenant_id";
        var roleIdColumnName = "role_id";
        var userIdColumnName = "user_id";
        var groupIdColumnName = "group_id"; // This is the nullable column we're testing

        await db.CreateTableIfNotExistsAsync(
            schemaName,
            tableName,
            [
                new DmColumn(
                    schemaName,
                    tableName,
                    idColumnName,
                    typeof(Guid),
                    isPrimaryKey: true
                ),
                new DmColumn(schemaName, tableName, tenantIdColumnName, typeof(Guid), isNullable: false),
                new DmColumn(schemaName, tableName, roleIdColumnName, typeof(Guid), isNullable: false),
                new DmColumn(schemaName, tableName, userIdColumnName, typeof(Guid), isNullable: true),
                // CRITICAL: group_id is nullable
                new DmColumn(schemaName, tableName, groupIdColumnName, typeof(Guid), isNullable: true),
            ],
            indexes:
            [
                // Unique index with role_id + user_id
                new DmIndex(
                    schemaName,
                    tableName,
                    "UX_role_user",
                    [new DmOrderedColumn(roleIdColumnName), new DmOrderedColumn(userIdColumnName)],
                    isUnique: true
                ),
                // Unique index with role_id + group_id (nullable column in unique index)
                new DmIndex(
                    schemaName,
                    tableName,
                    "UX_role_group",
                    [new DmOrderedColumn(roleIdColumnName), new DmOrderedColumn(groupIdColumnName)],
                    isUnique: true
                ),
                // Non-unique index with tenant_id + role_id + user_id
                new DmIndex(
                    schemaName,
                    tableName,
                    "IX_tenant_role_user",
                    [
                        new DmOrderedColumn(tenantIdColumnName),
                        new DmOrderedColumn(roleIdColumnName),
                        new DmOrderedColumn(userIdColumnName)
                    ]
                ),
                // Non-unique index with tenant_id + role_id + group_id (nullable column)
                new DmIndex(
                    schemaName,
                    tableName,
                    "IX_tenant_role_group",
                    [
                        new DmOrderedColumn(tenantIdColumnName),
                        new DmOrderedColumn(roleIdColumnName),
                        new DmOrderedColumn(groupIdColumnName)
                    ]
                ),
            ],
            foreignKeyConstraints:
            [
                // Foreign key on group_id (nullable column with FK constraint)
                new DmForeignKeyConstraint(
                    "FK_group",
                    [new DmOrderedColumn(groupIdColumnName)],
                    referencedTableName,
                    [new DmOrderedColumn("id")],
                    DmForeignKeyAction.Cascade,
                    DmForeignKeyAction.Cascade
                ),
            ]
        );

        // Verify the table was created
        var tableExists = await db.DoesTableExistAsync(schemaName, tableName);
        Assert.True(tableExists);

        // Verify the indexes exist
        var uxRoleGroupExists = await db.DoesIndexExistAsync(schemaName, tableName, "UX_role_group");
        Assert.True(uxRoleGroupExists);
        var ixTenantRoleGroupExists = await db.DoesIndexExistAsync(schemaName, tableName, "IX_tenant_role_group");
        Assert.True(ixTenantRoleGroupExists);

        // Verify the foreign key exists
        var fkExists = await db.DoesForeignKeyConstraintExistAsync(schemaName, tableName, "FK_group");
        Assert.True(fkExists);

        // Get the table definition
        var table = await db.GetTableAsync(schemaName, tableName);
        Assert.NotNull(table);

        // Verify tenant_id is NOT nullable (as specified)
        var tenantColumn = table.Columns.FirstOrDefault(c =>
            c.ColumnName.Equals(tenantIdColumnName, StringComparison.OrdinalIgnoreCase)
        );
        Assert.NotNull(tenantColumn);
        Assert.False(tenantColumn.IsNullable, $"Column '{tenantIdColumnName}' should be NOT NULL as specified");

        // Verify role_id is NOT nullable (as specified)
        var roleColumn = table.Columns.FirstOrDefault(c =>
            c.ColumnName.Equals(roleIdColumnName, StringComparison.OrdinalIgnoreCase)
        );
        Assert.NotNull(roleColumn);
        Assert.False(roleColumn.IsNullable, $"Column '{roleIdColumnName}' should be NOT NULL as specified");

        // CRITICAL: Verify user_id is nullable (as specified)
        var userColumn = table.Columns.FirstOrDefault(c =>
            c.ColumnName.Equals(userIdColumnName, StringComparison.OrdinalIgnoreCase)
        );
        Assert.NotNull(userColumn);
        Assert.True(
            userColumn.IsNullable,
            $"Column '{userIdColumnName}' should remain nullable even when part of unique index"
        );

        // CRITICAL: Verify group_id is nullable even with:
        // - Unique index (UX_role_group)
        // - Non-unique index (IX_tenant_role_group)
        // - Foreign key constraint (FK_group)
        var groupColumn = table.Columns.FirstOrDefault(c =>
            c.ColumnName.Equals(groupIdColumnName, StringComparison.OrdinalIgnoreCase)
        );
        Assert.NotNull(groupColumn);
        Assert.True(
            groupColumn.IsNullable,
            $"Column '{groupIdColumnName}' should remain nullable even when part of multiple indexes and a foreign key constraint"
        );

        // Cleanup
        await db.DropTableIfExistsAsync(schemaName, tableName);
        await db.DropTableIfExistsAsync(schemaName, referencedTableName);
    }
}
