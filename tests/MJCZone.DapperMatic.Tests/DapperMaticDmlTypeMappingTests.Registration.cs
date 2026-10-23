// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections.Immutable;
using System.Reflection;
using Dapper;
using MJCZone.DapperMatic.Models;
using MJCZone.DapperMatic.TypeMapping;

namespace MJCZone.DapperMatic.Tests;

/// <summary>
/// What registering handlers for collections and for the application's own types must not break, and how a model
/// gets its handlers without being registered.
/// </summary>
public abstract partial class DapperMaticDmlTypeMappingTests
{
    /// <summary>
    /// Dapper expands a collection of simple values for <c>IN @ids</c>, or on PostgreSQL passes it as an array for
    /// <c>= ANY(@ids)</c>. A handler for the collection's type would stop that, so none is registered for one.
    /// </summary>
    [Fact]
    protected virtual async Task Collections_of_simple_values_still_expand_for_in_lists_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        DapperMaticTypeMapping.Initialize(
            new DapperMaticMappingOptions { HandlerPrecedence = TypeHandlerPrecedence.OverrideExisting }
        );

        if (SimpleCollectionsAreJson)
        {
            Output.WriteLine("Not run here: with StoreSimpleCollectionsAsJson on, collections of simple values are JSON values, which do not expand.");
            return;
        }

        const string table = "dml_in_list";
        await db.DropTableIfExistsAsync(null, table);
        await db.CreateTableIfNotExistsAsync(
            new DmTable(
                null,
                table,
                [
                    new DmColumn(null, table, "id", typeof(int), isPrimaryKey: true),
                    new DmColumn(null, table, "code", typeof(string), length: 10),
                ]
            )
        );
        await db.ExecuteAsync($"INSERT INTO {table} (id, code) VALUES (1, 'a'), (2, 'b'), (3, 'c')");

        if (db.GetDbProviderType() == DbProviderType.PostgreSql)
        {
            // Dapper passes a list to PostgreSQL as one array parameter, which takes = ANY rather than IN.
            var anyId = $"SELECT COUNT(*) FROM {table} WHERE id = ANY(@ids)";
            Assert.Equal(2, await db.ExecuteScalarAsync<int>(anyId, new { ids = new[] { 1, 2 } }));
            Assert.Equal(2, await db.ExecuteScalarAsync<int>(anyId, new { ids = new List<int> { 1, 2 } }));
            var anyCode = $"SELECT COUNT(*) FROM {table} WHERE code = ANY(@codes)";
            Assert.Equal(2, await db.ExecuteScalarAsync<int>(anyCode, new { codes = new[] { "a", "c" } }));
            Assert.Equal(2, await db.ExecuteScalarAsync<int>(anyCode, new { codes = new List<string> { "a", "c" } }));
        }
        else
        {
            var byId = $"SELECT COUNT(*) FROM {table} WHERE id IN @ids";
            Assert.Equal(2, await db.ExecuteScalarAsync<int>(byId, new { ids = new[] { 1, 2 } }));
            Assert.Equal(2, await db.ExecuteScalarAsync<int>(byId, new { ids = new List<int> { 1, 2 } }));
            Assert.Equal(2, await db.ExecuteScalarAsync<int>(byId, new { ids = (IEnumerable<int>)[1, 2] }));
            Assert.Equal(2, await db.ExecuteScalarAsync<int>(byId, new { ids = (IReadOnlyList<int>)[1, 2] }));
            Assert.Equal(2, await db.ExecuteScalarAsync<int>(byId, new { ids = new HashSet<int> { 1, 2 } }));
            Assert.Equal(2, await db.ExecuteScalarAsync<int>(byId, new { ids = ImmutableArray.Create(1, 2) }));
            Assert.Equal(2, await db.ExecuteScalarAsync<int>(byId, new { ids = new long[] { 1, 2 } }));

            var byCode = $"SELECT COUNT(*) FROM {table} WHERE code IN @codes";
            Assert.Equal(2, await db.ExecuteScalarAsync<int>(byCode, new { codes = new[] { "a", "c" } }));
            Assert.Equal(2, await db.ExecuteScalarAsync<int>(byCode, new { codes = new List<string> { "a", "c" } }));
            Assert.Equal(2, await db.ExecuteScalarAsync<int>(byCode, new { codes = (ISet<string>)new HashSet<string> { "a", "c" } }));
        }

        await db.DropTableIfExistsAsync(null, table);
    }

    /// <summary>
    /// A collection of simple values is stored as JSON when asked to be, at the cost of IN lists of that type.
    /// </summary>
    [Fact]
    protected virtual async Task Collections_of_simple_values_registered_as_json_round_trip_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        DapperMaticTypeMapping.Initialize(
            new DapperMaticMappingOptions { HandlerPrecedence = TypeHandlerPrecedence.OverrideExisting }
        );

        // Enum collections, which no other test uses for an IN list.
        DapperMaticTypeMapping.RegisterJsonTypes(
            typeof(List<CoverageColor>),
            typeof(HashSet<CoverageColor>),
            typeof(CoverageColor[])
        );

        var roundTrip = typeof(DapperMaticDmlTypeMappingTests).GetMethod(
            nameof(CoverageRoundTripAsync),
            BindingFlags.NonPublic | BindingFlags.Static
        )!;

        foreach (var sample in new object[]
        {
            new List<CoverageColor> { CoverageColor.Red, CoverageColor.Blue },
            new HashSet<CoverageColor> { CoverageColor.Green },
            new[] { CoverageColor.Blue },
        })
        {
            var error = await (Task<string?>)roundTrip.MakeGenericMethod(sample.GetType()).Invoke(null, [db, "dml_json_opt_in", sample])!;
            Assert.True(error is null, $"{sample.GetType().Name}: {error}");
        }
    }

    /// <summary>
    /// A model whose property is a collection of the application's own type gets its handler the first time Dapper
    /// reads it, with nothing registered beforehand, and can be written from then on.
    /// </summary>
    [Fact]
    protected virtual async Task A_model_read_before_it_is_written_gets_its_handlers_Async()
    {
        // A type of this class's own, so that no other test class has registered it already.
        var run = typeof(DapperMaticDmlTypeMappingTests)
            .GetMethod(nameof(ReadBeforeWriteAsync), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(GetType());

        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        DapperMaticTypeMapping.Initialize(
            new DapperMaticMappingOptions { HandlerPrecedence = TypeHandlerPrecedence.OverrideExisting }
        );

        await (Task)run.Invoke(null, [db])!;
    }

    /// <summary>
    /// An object property is stored as JSON only where it is clearly a column. A navigation property filled by
    /// multi-mapping is not, and its type must still be readable as rows.
    /// </summary>
    [Fact]
    protected virtual async Task Objects_read_as_rows_are_not_stored_as_json_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);
        DapperMaticTypeMapping.Initialize(
            new DapperMaticMappingOptions { HandlerPrecedence = TypeHandlerPrecedence.OverrideExisting }
        );

        await db.DropTableIfExistsAsync(null, "dml_mm_orders");
        await db.DropTableIfExistsAsync(null, "dml_mm_customers");
        await db.CreateTableIfNotExistsAsync(
            new DmTable(
                null,
                "dml_mm_customers",
                [
                    new DmColumn(null, "dml_mm_customers", "id", typeof(int), isPrimaryKey: true),
                    new DmColumn(null, "dml_mm_customers", "name", typeof(string), length: 50),
                ]
            )
        );
        await db.CreateTableIfNotExistsAsync(
            new DmTable(
                null,
                "dml_mm_orders",
                [
                    new DmColumn(null, "dml_mm_orders", "id", typeof(int), isPrimaryKey: true),
                    new DmColumn(null, "dml_mm_orders", "customer_id", typeof(int)),
                ]
            )
        );
        await db.ExecuteAsync("INSERT INTO dml_mm_customers (id, name) VALUES (7, 'Ada')");
        await db.ExecuteAsync("INSERT INTO dml_mm_orders (id, customer_id) VALUES (1, 7)");

        // Seen as a model, the way scanning or a first read would see it.
        DapperMaticTypeMapping.RegisterModels(typeof(MultiMapOrder));
        Assert.False(SqlMapper.HasTypeHandler(typeof(MultiMapCustomer)));

        var orders = await db.QueryAsync<MultiMapOrder, MultiMapCustomer, MultiMapOrder>(
            "SELECT o.id AS Id, o.customer_id AS CustomerId, c.id AS Id, c.name AS Name "
                + "FROM dml_mm_orders o JOIN dml_mm_customers c ON c.id = o.customer_id",
            (order, customer) =>
            {
                order.Customer = customer;
                return order;
            },
            splitOn: "Id"
        );

        var order = Assert.Single(orders);
        Assert.Equal("Ada", order.Customer?.Name);
        Assert.Equal("Ada", (await db.QuerySingleAsync<MultiMapCustomer>("SELECT id, name FROM dml_mm_customers")).Name);

        await db.DropTableIfExistsAsync(null, "dml_mm_orders");
        await db.DropTableIfExistsAsync(null, "dml_mm_customers");
    }

    /// <summary>
    /// Gets a value indicating whether collections of simple values are stored as JSON in this test run. They are
    /// only with StoreSimpleCollectionsAsJson, which the MJCZone.DapperMatic.Tests.JsonCollections project turns on.
    /// </summary>
    protected static bool SimpleCollectionsAreJson => DapperMaticTypeMapping.Options.StoreSimpleCollectionsAsJson;

    /// <summary>
    /// For a test of collections of simple values stored through Dapper: whether they are stored here, natively on
    /// PostgreSQL or as JSON with StoreSimpleCollectionsAsJson. Says so when they are not.
    /// </summary>
    /// <param name="db">The connection.</param>
    /// <param name="nativeOnPostgreSql">Whether PostgreSQL stores the test's collections natively.</param>
    /// <returns>True if the test applies here.</returns>
    protected bool StoresSimpleCollectionsHere(System.Data.IDbConnection db, bool nativeOnPostgreSql)
    {
        if (SimpleCollectionsAreJson || (nativeOnPostgreSql && db.GetDbProviderType() == DbProviderType.PostgreSql))
        {
            return true;
        }

        Output.WriteLine(
            "Not run here: collections of simple values keep Dapper's own behaviour unless StoreSimpleCollectionsAsJson "
                + "is on. The MJCZone.DapperMatic.Tests.JsonCollections project runs this test with it on."
        );
        return false;
    }

    private static async Task ReadBeforeWriteAsync<TTag>(System.Data.IDbConnection db)
    {
        const string table = "dml_read_first";
        await db.DropTableIfExistsAsync(null, table);
        await db.CreateTableIfNotExistsAsync(
            new DmTable(
                null,
                table,
                [
                    new DmColumn(null, table, "id", typeof(int), isPrimaryKey: true),
                    new DmColumn(null, table, "order_lines", typeof(List<ReadFirstLine<TTag>>), isNullable: true),
                ]
            )
        );

        Assert.False(SqlMapper.HasTypeHandler(typeof(List<ReadFirstLine<TTag>>)));

        // Written as JSON text by something other than Dapper, then read.
        await db.ExecuteAsync($"INSERT INTO {table} (id, order_lines) VALUES (1, '[{{\"Sku\":\"x\",\"Qty\":2}}]')");
        var read = await db.QuerySingleAsync<ReadFirstOrder<TTag>>($"SELECT id, order_lines AS OrderLines FROM {table} WHERE id = 1");
        Assert.Equal(2, Assert.Single(read.OrderLines!).Qty);

        // And now written through Dapper.
        await db.ExecuteAsync(
            $"INSERT INTO {table} (id, order_lines) VALUES (@Id, @OrderLines)",
            new ReadFirstOrder<TTag> { Id = 2, OrderLines = [new ReadFirstLine<TTag> { Sku = "y", Qty = 3 }] }
        );
        var written = await db.QuerySingleAsync<ReadFirstOrder<TTag>>($"SELECT id, order_lines AS OrderLines FROM {table} WHERE id = 2");
        Assert.Equal("y", Assert.Single(written.OrderLines!).Sku);

        await db.DropTableIfExistsAsync(null, table);
    }

    public class ReadFirstOrder<TTag>
    {
        public int Id { get; set; }

        public List<ReadFirstLine<TTag>>? OrderLines { get; set; }
    }

    public class ReadFirstLine<TTag>
    {
        public string Sku { get; set; } = string.Empty;

        public int Qty { get; set; }
    }

    public class MultiMapOrder
    {
        public int Id { get; set; }

        public int CustomerId { get; set; }

        public MultiMapCustomer? Customer { get; set; }
    }

    public class MultiMapCustomer
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
