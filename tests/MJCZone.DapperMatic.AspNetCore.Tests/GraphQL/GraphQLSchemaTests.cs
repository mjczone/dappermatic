// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Text.Json;
using FluentAssertions;
using GraphQL;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Services;
using MJCZone.DapperMatic.Dml.Rest;
using MJCZone.DapperMatic.GraphQL;
using MJCZone.DapperMatic.GraphQL.Schema;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Tests.GraphQL;

/// <summary>
/// The schema a caller model gives, and the rules it follows: names, global ids, scalars. The schema's text is kept in
/// <c>tests/graphql-clients/schema.graphql</c>, which the client compatibility job generates Relay, Apollo (codegen) and
/// Strawberry Shake clients from; a change to it is a change clients see.
/// </summary>
public class GraphQLSchemaTests
{
    // Set DM_UPDATE_GOLDEN=1 to write the schema's text after a deliberate change.
    private static readonly string Golden = Path.Combine(Root(), "tests", "graphql-clients", "schema.graphql");

    [Fact]
    public void The_schema_of_a_model_is_the_one_clients_are_generated_from()
    {
        var sdl = GraphQLSchemaBuilder.Build(Model(), new DapperMaticGraphQLOptions(), t => t.Name == "orders").Schema.Print().ReplaceLineEndings("\n");
        if (Environment.GetEnvironmentVariable("DM_UPDATE_GOLDEN") == "1")
        {
            File.WriteAllText(Golden, sdl);
        }

        sdl.Should().Be(File.ReadAllText(Golden).ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Names_are_the_databases_made_GraphQL_names_and_never_clash()
    {
        GraphQLNames.Of("order lines").Should().Be("order_lines");
        GraphQLNames.Of("2024_sales").Should().Be("_2024_sales");
        GraphQLNames.Of("__secret").Should().Be("x__secret");
        GraphQLNames.Of("prix-€").Should().Be("prix__");

        // A table named as another's connection keeps its name; the connection's type is renamed.
        var model = Model(extra: Table("orders_connection", ("id", typeof(int), false)));
        var built = GraphQLSchemaBuilder.Build(model, new DapperMaticGraphQLOptions(), _ => false);
        built.Schema.Initialize();
        built.Schema.AllTypes["orders_connection"]!.Should().BeOfType<global::GraphQL.Types.ObjectGraphType>()
            .Which.GetMetadata<GraphQLTable>(GraphQLBinding.Table)!.Table.Name.Should().Be("orders_connection");
        built.Renamed.Should().Contain(("orders_connection", "orders_connection_2"));
    }

    // Databases whose names differ only in case (PostgreSQL's "Orders" and orders) are two tables, and two types.
    [Fact]
    public void Tables_whose_names_differ_in_case_are_two_tables()
    {
        var built = GraphQLSchemaBuilder.Build(Model(extra: Table("Orders", ("id", typeof(int), false))), new DapperMaticGraphQLOptions(), _ => false);
        built.Tables.Keys.Should().Contain(["orders", "Orders"]);
        built.Tables["Orders"].Table.Name.Should().Be("Orders");
        built.Tables["orders"].Table.Name.Should().Be("orders");
    }

    [Fact]
    public void Global_ids_name_a_table_and_a_key()
    {
        var id = GraphQLNodeIds.Encode("orders", [17, "x"]);
        var (table, keys) = GraphQLNodeIds.Decode(id)!.Value;
        table.Should().Be("orders");
        keys.Select(k => k.ToString()).Should().Equal("17", "x");
        GraphQLNodeIds.Decode("not an id").Should().BeNull();
        GraphQLNodeIds.Decode(Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new[] { 1 }))).Should().BeNull();
    }

    [Fact]
    public void Scalars_write_and_read_their_values()
    {
        var scalars = new DapperMaticScalars(bigNumbersAsStrings: false);
        scalars.For(typeof(DateTime)).Serialize(new DateTime(2024, 5, 17, 9, 30, 0)).Should().Be("2024-05-17T09:30:00");
        scalars.For(typeof(DateTimeOffset)).Serialize(new DateTimeOffset(2024, 5, 17, 9, 30, 0, TimeSpan.FromHours(2))).Should().Be("2024-05-17T09:30:00+02:00");
        scalars.For(typeof(DateOnly)).ParseValue("2024-05-17").Should().Be(new DateOnly(2024, 5, 17));
        scalars.For(typeof(TimeOnly)).Serialize(new TimeOnly(9, 30)).Should().Be("09:30:00");
        scalars.For(typeof(TimeSpan)).Serialize(TimeSpan.FromMinutes(90)).Should().Be("PT1H30M");
        scalars.For(typeof(TimeSpan)).ParseValue("PT1H30M").Should().Be(TimeSpan.FromMinutes(90));
        scalars.For(typeof(byte[])).ParseValue("AQID").Should().BeEquivalentTo(new byte[] { 1, 2, 3 });
        scalars.For(typeof(Guid)).Name.Should().Be("UUID");
        scalars.For(typeof(long)).Name.Should().Be("Long");
        // An instant has its offset; a date and time of no offset has none, and is never moved into the server's zone.
        scalars.For(typeof(DateTimeOffset)).ParseValue("2024-05-17T09:30:00Z").Should().Be(new DateTimeOffset(2024, 5, 17, 9, 30, 0, TimeSpan.Zero));
        scalars.For(typeof(DateTimeOffset)).ParseValue("2024-05-17T09:30:00.125+02:00").Should().Be(new DateTimeOffset(2024, 5, 17, 9, 30, 0, 125, TimeSpan.FromHours(2)));
        ((Action)(() => scalars.For(typeof(DateTimeOffset)).ParseValue("2024-05-17T09:30:00"))).Should().Throw<Exception>();
        scalars.For(typeof(DateTime)).ParseValue("2024-05-17T09:30:00").Should().Be(new DateTime(2024, 5, 17, 9, 30, 0));
        scalars.For(typeof(DateTime)).ParseValue("2024-05-17T09:30:00.125").Should().Be(new DateTime(2024, 5, 17, 9, 30, 0, 125));
        ((Action)(() => scalars.For(typeof(DateTime)).ParseValue("2024-05-17T09:30:00.000Z"))).Should().Throw<Exception>();
        var strings = new DapperMaticScalars(bigNumbersAsStrings: true);
        strings.For(typeof(long)).Serialize(9007199254740993L).Should().Be("9007199254740993");
        strings.For(typeof(decimal)).Serialize(1234.50m).Should().Be("1234.50");
    }

    // Customers, their orders, a view of totals, two functions: every kind of column, relation and root field.
    internal static DapperMaticCallerModel Model(DapperMaticCallerTable? extra = null)
    {
        var customers = Table(
            "customers",
            ("id", typeof(int), false),
            ("name", typeof(string), false),
            ("country", typeof(string), true),
            ("created", typeof(DateTimeOffset), false),
            ("ref", typeof(Guid), true),
            ("photo", typeof(byte[]), true),
            ("prefs", typeof(JsonDocument), true),
            ("points", typeof(long), false)
        );
        var orders = Table(
            "orders",
            ("id", typeof(int), false),
            ("customer_id", typeof(int), true),
            ("total", typeof(decimal), false),
            ("status", typeof(string), false),
            ("placed", typeof(DateTime), true),
            ("ship_date", typeof(DateOnly), true),
            ("ship_time", typeof(TimeOnly), true),
            ("duration", typeof(TimeSpan), true),
            ("rush", typeof(bool), false)
        );
        var totals = new DapperMaticCallerTable(
            new RestTable { SchemaName = null, Name = "order_totals", QualifiedName = "order_totals", Columns = [new RestColumn("customer_id", typeof(int), true), new RestColumn("total", typeof(decimal), true)] },
            View: true,
            Rules(DmAction.Read)
        );
        orders.Relations.Add(new DapperMaticCallerRelation("customer", customers, [("customer_id", "id")], Many: false));
        customers.Relations.Add(new DapperMaticCallerRelation("orders", orders, [("id", "customer_id")], Many: true));
        var top = new DapperMaticFunction { Name = "top_customers", Description = "The customers who spent the most.", Sql = "SELECT 1" };
        top.Parameters.Add(new DapperMaticFunctionParameter { Name = "min", Type = typeof(decimal) });
        var recalc = new DapperMaticFunction { Name = "recalc_totals", Sql = "SELECT 1", Writes = ["orders"] };
        return new DapperMaticCallerModel("main", null, null, DbProviderType.Sqlite, [customers, orders, totals, .. extra is null ? [] : new[] { extra }], [top, recalc]);
    }

    private static DapperMaticCallerTable Table(string name, params (string Name, Type Type, bool Nullable)[] columns) =>
        new(
            new RestTable
            {
                SchemaName = null,
                Name = name,
                QualifiedName = name,
                Columns = [.. columns.Select(c => new RestColumn(c.Name, c.Type, c.Nullable, Filled: c.Name == "id"))],
                Keys = ["id"],
                Generated = "id",
                Writable = true,
                Searchable = name == "customers" ? new RestSearchable(["name"]) : null,
            },
            View: false,
            Rules(DmAction.Read, DmAction.Create, DmAction.Update, DmAction.Delete)
        );

    private static IReadOnlyDictionary<DmAction, DmAccessRules> Rules(params DmAction[] actions) => actions.ToDictionary(a => a, _ => new DmAccessRules { Allowed = true });

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MJCZone.DapperMatic.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository's root was not found.");
    }
}
