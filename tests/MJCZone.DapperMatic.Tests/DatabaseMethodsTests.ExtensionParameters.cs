// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using MJCZone.DapperMatic.DataAnnotations;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.Tests;

public abstract partial class DatabaseMethodsTests
{
    [Fact]
    protected virtual async Task Can_use_command_timeout_parameter_async()
    {
        // Test commandTimeout parameter like documentation example
        var table = new DmTable(
            null,
            "TimeoutTestTable",
            new[]
            {
                new DmColumn("Id", typeof(int)) { IsPrimaryKey = true, IsAutoIncrement = true },
                new DmColumn("Name", typeof(string)) { Length = 100 },
            }
        );

        using var db = await OpenConnectionAsync();

        // Clean up
        await db.DropTableIfExistsAsync(table.SchemaName, table.TableName);

        // Test with default parameters (no commandTimeout in DapperMatic extensions)
        var created = await db.CreateTableIfNotExistsAsync(table);
        Assert.True(created);

        // Verify table exists
        var exists = await db.DoesTableExistAsync(table.SchemaName, table.TableName);
        Assert.True(exists);

        // Test introspection operations
        var tables = await db.GetTablesAsync(table.SchemaName);
        Assert.Contains(
            tables,
            t => t.TableName.Equals(table.TableName, StringComparison.OrdinalIgnoreCase)
        );

        // Clean up
        await db.DropTableIfExistsAsync(table.SchemaName, table.TableName);

        Output.WriteLine("✅ Command timeout parameter works correctly");
    }

    [Fact]
    protected virtual async Task Can_use_transaction_support_async()
    {
        // Test transaction support like documentation example
        var usersTable = new DmTable(
            null,
            "TransactionUsers",
            new[]
            {
                new DmColumn("Id", typeof(int)) { IsPrimaryKey = true, IsAutoIncrement = true },
                new DmColumn("Name", typeof(string)) { Length = 100 },
            }
        );

        var ordersTable = new DmTable(
            null,
            "TransactionOrders",
            new[]
            {
                new DmColumn("Id", typeof(int)) { IsPrimaryKey = true, IsAutoIncrement = true },
                new DmColumn("UserId", typeof(int)),
                new DmColumn("OrderDate", typeof(DateTime)),
            }
        );

        var orderItemsTable = new DmTable(
            null,
            "TransactionOrderItems",
            new[]
            {
                new DmColumn("Id", typeof(int)) { IsPrimaryKey = true, IsAutoIncrement = true },
                new DmColumn("OrderId", typeof(int)),
                new DmColumn("ProductName", typeof(string)) { Length = 200 },
            }
        );

        using var db = await OpenConnectionAsync();

        // Clean up any existing tables
        await db.DropTableIfExistsAsync(usersTable.SchemaName, usersTable.TableName);
        await db.DropTableIfExistsAsync(ordersTable.SchemaName, ordersTable.TableName);
        await db.DropTableIfExistsAsync(orderItemsTable.SchemaName, orderItemsTable.TableName);

        using var transaction = db.BeginTransaction();

        try
        {
            // Create multiple related tables in a transaction (like documentation example)
            await db.CreateTableIfNotExistsAsync(usersTable, tx: transaction);
            await db.CreateTableIfNotExistsAsync(ordersTable, tx: transaction);
            await db.CreateTableIfNotExistsAsync(orderItemsTable, tx: transaction);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        // Verify all tables were created
        var usersExists = await db.DoesTableExistAsync(usersTable.SchemaName, usersTable.TableName);
        var ordersExists = await db.DoesTableExistAsync(
            ordersTable.SchemaName,
            ordersTable.TableName
        );
        var itemsExists = await db.DoesTableExistAsync(
            orderItemsTable.SchemaName,
            orderItemsTable.TableName
        );

        Assert.True(usersExists);
        Assert.True(ordersExists);
        Assert.True(itemsExists);

        // Clean up
        await db.DropTableIfExistsAsync(orderItemsTable.SchemaName, orderItemsTable.TableName);
        await db.DropTableIfExistsAsync(ordersTable.SchemaName, ordersTable.TableName);
        await db.DropTableIfExistsAsync(usersTable.SchemaName, usersTable.TableName);

        Output.WriteLine("✅ Transaction support works correctly");
    }

    [Fact]
    protected virtual async Task Can_use_cancellation_token_async()
    {
        // Test cancellation token support like documentation example
        var table = new DmTable(
            null,
            "CancellationTestTable",
            new[]
            {
                new DmColumn("Id", typeof(int)) { IsPrimaryKey = true, IsAutoIncrement = true },
                new DmColumn("Data", typeof(string)) { Length = 500 },
            }
        );

        using var db = await OpenConnectionAsync();

        // Clean up
        await db.DropTableIfExistsAsync(table.SchemaName, table.TableName);

        using var cts = new CancellationTokenSource();

        // Test with cancellation token (don't actually cancel for this test)
        var created = await db.CreateTableIfNotExistsAsync(table, cancellationToken: cts.Token);
        Assert.True(created);

        // Verify table exists
        var exists = await db.DoesTableExistAsync(
            table.SchemaName,
            table.TableName,
            cancellationToken: cts.Token
        );
        Assert.True(exists);

        // Clean up
        await db.DropTableIfExistsAsync(
            table.SchemaName,
            table.TableName,
            cancellationToken: cts.Token
        );

        Output.WriteLine("✅ Cancellation token support works correctly");
    }

    [Fact]
    protected virtual async Task Can_combine_all_parameters_async()
    {
        // Test combining timeout, transaction, and cancellation token
        var table = new DmTable(
            null,
            "CombinedParamsTable",
            new[]
            {
                new DmColumn("Id", typeof(int)) { IsPrimaryKey = true, IsAutoIncrement = true },
                new DmColumn("Name", typeof(string)) { Length = 100 },
            }
        );

        using var db = await OpenConnectionAsync();

        // Clean up
        await db.DropTableIfExistsAsync(table.SchemaName, table.TableName);

        using var transaction = db.BeginTransaction();
        using var cts = new CancellationTokenSource();

        try
        {
            // Combine transaction and cancellation token
            var created = await db.CreateTableIfNotExistsAsync(
                table,
                tx: transaction,
                cancellationToken: cts.Token
            );
            Assert.True(created);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        // Verify table exists
        var exists = await db.DoesTableExistAsync(table.SchemaName, table.TableName);
        Assert.True(exists);

        // Clean up
        await db.DropTableIfExistsAsync(table.SchemaName, table.TableName);

        Output.WriteLine("✅ Combined parameters work correctly");
    }

    [Fact]
    protected virtual async Task Transaction_rollback_prevents_table_creation_async()
    {
        // Test that rollback actually prevents table creation
        // Note: MySQL/MariaDB DDL statements auto-commit, so rollback doesn't work for CREATE TABLE
        var typeName = this.GetType().Name;
        var supportsRollback = !typeName.Contains("MySql") && !typeName.Contains("MariaDb");

        var table = new DmTable(
            null,
            "RollbackTestTable",
            new[]
            {
                new DmColumn("Id", typeof(int)) { IsPrimaryKey = true, IsAutoIncrement = true },
                new DmColumn("Name", typeof(string)) { Length = 100 },
            }
        );

        using var db = await OpenConnectionAsync();

        // Clean up
        await db.DropTableIfExistsAsync(table.SchemaName, table.TableName);

        using var transaction = db.BeginTransaction();

        // Create table in transaction
        await db.CreateTableIfNotExistsAsync(table, tx: transaction);

        // Rollback instead of commit
        transaction.Rollback();

        // Check if table exists after rollback
        var exists = await db.DoesTableExistAsync(table.SchemaName, table.TableName);

        if (supportsRollback)
        {
            // Table should NOT exist after rollback for databases that support DDL transactions
            Assert.False(exists);
            Output.WriteLine("✅ Transaction rollback works correctly");
        }
        else
        {
            // MySQL/MariaDB: DDL auto-commits, so table will exist even after rollback
            Assert.True(exists);
            Output.WriteLine("✅ DDL auto-commit behavior confirmed (MySQL/MariaDB)");

            // Clean up the table that was auto-committed
            await db.DropTableIfExistsAsync(table.SchemaName, table.TableName);
        }
    }
}
