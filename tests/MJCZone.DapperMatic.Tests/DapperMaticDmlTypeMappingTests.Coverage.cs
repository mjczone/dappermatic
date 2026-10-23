// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections.Immutable;
using System.Data;
using System.Net;
using System.Net.NetworkInformation;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Dapper;
using MJCZone.DapperMatic.DataAnnotations;
using MJCZone.DapperMatic.Models;
using MJCZone.DapperMatic.TypeMapping;

namespace MJCZone.DapperMatic.Tests;

/// <summary>
/// Every property type a data access object commonly has, written through Dapper and read back with no
/// registration beyond <see cref="DapperMaticTypeMapping.Initialize"/>.
/// </summary>
public abstract partial class DapperMaticDmlTypeMappingTests
{
    private static readonly JsonSerializerOptions CoverageJson = new() { WriteIndented = false };

    /// <summary>
    /// A table is created from a model with a property of each type, as an application creates its tables. A model
    /// holding a sample value is written as Dapper parameters, then read back as a model and as a scalar, and so is
    /// a null where the type allows one. The first step that fails is reported for each type.
    /// </summary>
    [Fact]
    protected virtual async Task Common_property_types_round_trip_without_registration_Async()
    {
        using var db = await OpenConnectionAsync();
        await InitFreshSchemaAsync(db, null);

        DapperMaticTypeMapping.Initialize(
            new DapperMaticMappingOptions { HandlerPrecedence = TypeHandlerPrecedence.OverrideExisting }
        );

        var roundTrip = typeof(DapperMaticDmlTypeMappingTests).GetMethod(
            nameof(CoverageRoundTripAsync),
            BindingFlags.NonPublic | BindingFlags.Static
        )!;

        var failures = new List<string>();
        var index = 0;
        var isPostgreSql = db.GetDbProviderType() == DbProviderType.PostgreSql;
        foreach (var (name, type, sample, postgreSqlOnly) in CoverageCases())
        {
            if (postgreSqlOnly && !isPostgreSql)
            {
                continue;
            }

            var table = $"dml_cov_{index++}";
            var error = await (Task<string?>)roundTrip.MakeGenericMethod(type).Invoke(null, [db, table, sample])!;
            string sqlType;
            try
            {
                sqlType = db.GetSqlTypeFromDotnetType(new DotnetTypeDescriptor(type));
            }
            catch (Exception ex)
            {
                sqlType = "? " + ex.GetType().Name;
            }

            if (error is null)
            {
                Output.WriteLine($"ok    {name} [{sqlType}]");
            }
            else
            {
                Output.WriteLine($"FAIL  {name} [{sqlType}]: {error}");
                failures.Add($"{name}: {error}");
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} types do not round-trip:\n" + string.Join("\n", failures));
    }

    private static IEnumerable<(string Name, Type Type, object? Sample, bool PostgreSqlOnly)> CoverageCases()
    {
        static (string, Type, object?, bool) Case<T>(T sample, string? note = null, bool postgreSqlOnly = false) =>
            (note is null ? Name(typeof(T)) : $"{Name(typeof(T))} ({note})", typeof(T), sample, postgreSqlOnly);

        // Collections of simple values: JSON on every provider with StoreSimpleCollectionsAsJson; otherwise only
        // arrays are stored, natively on PostgreSQL, and the rest keep Dapper's own behaviour, expanding for IN lists.
        static (string, Type, object?, bool) NativeArray<T>(T sample) =>
            SimpleCollectionsAreJson ? Case(sample, "JSON") : Case(sample, "native", postgreSqlOnly: true);

        var guid = Guid.Parse("6f9619ff-8b86-d011-b42d-00cf4fc964ff");
        var dateTime = new DateTime(2025, 1, 15, 10, 30, 45, DateTimeKind.Unspecified);
        var dateTimeOffset = new DateTimeOffset(2025, 1, 15, 10, 30, 45, TimeSpan.Zero);
        var date = new DateOnly(2025, 1, 15);
        var time = new TimeOnly(10, 30, 45);
        var span = new TimeSpan(10, 30, 45);
        var address = new CoverageAddress { Street = "1 Main St", City = "Springfield", Zip = 12345 };

        // Numbers and other scalars
        yield return Case(true);
        yield return Case((byte)200);
        yield return Case((sbyte)-5);
        yield return Case((short)-1234);
        yield return Case((ushort)60000);
        yield return Case(123456);
        yield return Case(4000000000u);
        yield return Case(9000000000L);
        yield return Case(12345678901234UL);
        yield return Case(1.5f);
        yield return Case(2.25d);
        yield return Case(1d / 3, "beyond single precision");
        yield return Case(1234.56m);
        yield return Case((Half)1.5);
        yield return Case(BigInteger.Parse("123456789012345678901234567890", System.Globalization.CultureInfo.InvariantCulture));
        yield return Case((Int128)long.MaxValue * 4);
        yield return Case((UInt128)ulong.MaxValue * 4);
        yield return Case('Z');
        yield return Case("hello wörld");
        yield return Case(guid);

        // Dates and times
        yield return Case(dateTime);
        yield return Case(dateTimeOffset);
        yield return Case(date);
        yield return Case(time);
        yield return Case(span);

        // Nullable value types
        yield return Case<int?>(42);
        yield return Case<long?>(42L);
        yield return Case<bool?>(true);
        yield return Case<decimal?>(9.99m);
        yield return Case<double?>(0.5d);
        yield return Case<char?>('q');
        yield return Case<Guid?>(guid);
        yield return Case<DateTime?>(dateTime);
        yield return Case<DateTimeOffset?>(dateTimeOffset);
        yield return Case<DateOnly?>(date);
        yield return Case<TimeOnly?>(time);
        yield return Case<TimeSpan?>(span);
        yield return Case<CoverageColor?>(CoverageColor.Blue);

        // Enums
        yield return Case(CoverageColor.Green);
        yield return Case(CoverageByteEnum.B);
        yield return Case(CoverageLongEnum.Big);
        yield return Case(CoverageFlags.A | CoverageFlags.C);

        // Binary, text-like and network
        yield return Case(new byte[] { 1, 2, 3, 255 });
        yield return Case(new Uri("https://example.com/a?b=1"));
        yield return Case(new Version(1, 2, 3, 4));
        yield return Case(IPAddress.Parse("192.168.1.10"));
        yield return Case(IPNetwork.Parse("10.0.0.0/8"));
        yield return Case(PhysicalAddress.Parse("00-11-22-33-44-55"));
        yield return Case(new IPEndPoint(IPAddress.Loopback, 8080));

        // XML and JSON
        yield return Case(XDocument.Parse("<a><b>1</b></a>"));
        yield return Case(XElement.Parse("<a b=\"1\"><c/></a>"));
        yield return Case(JsonDocument.Parse("{\"a\":1,\"b\":[true,null]}"));
        yield return Case(JsonDocument.Parse("{\"a\":1}").RootElement);
        yield return Case(JsonNode.Parse("{\"a\":1}"));
        yield return Case((JsonObject)JsonNode.Parse("{\"a\":{\"b\":2}}")!);
        yield return Case((JsonArray)JsonNode.Parse("[1,\"two\",3]")!);

        // Arrays of simple values
        yield return NativeArray(new[] { "a", "b" });
        yield return NativeArray(new[] { 1, 2, 3 });
        yield return NativeArray(new[] { 1L, 2L });
        yield return NativeArray(new short[] { 1, 2 });
        yield return NativeArray(new[] { true, false });
        yield return NativeArray(new[] { 1.5d, 2.5d });
        yield return NativeArray(new[] { 1.5f, 2.5f });
        yield return NativeArray(new[] { 1.25m, 2.5m });
        yield return NativeArray(new[] { guid, Guid.Empty });
        yield return NativeArray(new[] { dateTime });
        yield return NativeArray(new[] { dateTimeOffset });
        yield return NativeArray(new[] { date });
        yield return NativeArray(new[] { time });
        yield return NativeArray(new[] { span });

        if (SimpleCollectionsAreJson)
        {
            yield return Case(new List<string> { "a", "b" });
            yield return Case(new List<string> { "日本語", "Ελληνικά" }, "non-Latin text");
            yield return Case(new List<int> { 1, 2, 3 });
            yield return Case(new List<long> { 1L, 2L });
            yield return Case(new List<Guid> { guid });
            yield return Case(new List<decimal> { 1.25m });
            yield return Case(new List<double> { 1.5d });
            yield return Case(new List<bool> { true });
            yield return Case(new List<DateTime> { dateTime });
            yield return Case(new List<DateOnly> { date });
            yield return Case<IList<string>>(new List<string> { "a" });
            yield return Case<ICollection<string>>(new List<string> { "a" });
            yield return Case<IEnumerable<int>>(new List<int> { 1 });
            yield return Case<IReadOnlyList<string>>(new List<string> { "a" });
            yield return Case<IReadOnlyCollection<int>>(new List<int> { 1 });
            yield return Case(new HashSet<string> { "a", "b" });
            yield return Case<ISet<string>>(new HashSet<string> { "a" });
            yield return Case(ImmutableArray.Create(1, 2));
            yield return Case(ImmutableList.Create("a", "b"));
        }

        // Collections of objects: JSON on every provider
        yield return Case(new[] { address });
        yield return Case(new List<CoverageAddress> { address });
        yield return Case<IList<CoverageAddress>>(new List<CoverageAddress> { address });
        yield return Case<IReadOnlyList<CoverageAddress>>(new List<CoverageAddress> { address });
        yield return Case<IEnumerable<CoverageAddress>>(new List<CoverageAddress> { address });
        yield return Case(new HashSet<CoverageRecord> { new("widget", 3) });
        yield return Case(ImmutableList.Create(address));
        yield return Case(new List<List<string>> { new() { "a", "b" }, new() { "日本語" } });
        yield return Case(new List<object> { "a", 1 });
        yield return Case(new object[] { "a", 1 });
        yield return Case(new List<Dictionary<string, object>> { new() { ["a"] = "x" } });

        // Dictionaries
        yield return Case(new Dictionary<string, string> { ["a"] = "x" });
        yield return Case(new Dictionary<string, string> { ["名前"] = "日本語", ["όνομα"] = "Ελληνικά" }, "non-Latin text");
        yield return Case(new Dictionary<string, object> { ["a"] = "x", ["b"] = 1 });
        yield return Case(new Dictionary<string, int> { ["a"] = 1 });
        yield return Case(new Dictionary<string, decimal> { ["a"] = 1.5m });
        yield return Case(new Dictionary<string, bool> { ["a"] = true });
        yield return Case(new Dictionary<int, string> { [1] = "x" });
        yield return Case(new Dictionary<Guid, string> { [guid] = "x" });
        yield return Case(new Dictionary<string, List<string>> { ["a"] = ["x", "y"] });
        yield return Case(new Dictionary<string, CoverageAddress> { ["home"] = address });
        yield return Case<IDictionary<string, string>>(new Dictionary<string, string> { ["a"] = "x" });
        yield return Case<IReadOnlyDictionary<string, string>>(new Dictionary<string, string> { ["a"] = "x" });
        yield return Case<IDictionary<string, object>>(new Dictionary<string, object> { ["a"] = "x" });

        // Objects of the application's own types
        yield return Case(address);
        yield return Case(new CoverageRecord("widget", 3));
        yield return Case(new CoveragePoint(1, 2));
        yield return Case(
            new CoverageNested
            {
                Name = "n",
                Tags = ["a", "b"],
                Address = address,
                Counts = new() { ["x"] = 1 },
            }
        );
    }

    private static async Task<string?> CoverageRoundTripAsync<T>(IDbConnection db, string table, object? sample)
    {
        var value = (T)sample!;

        try
        {
            // The model's own columns, under a table name of this case's.
            var columns = DmTableFactory
                .GetTable(typeof(CoverageRow<T>))
                .Columns.Select(c =>
                {
                    var column = c.Copy();
                    column.TableName = table;
                    return column;
                })
                .ToArray();

            await db.DropTableIfExistsAsync(null, table);
            await db.CreateTableIfNotExistsAsync(new DmTable(null, table, columns));
        }
        catch (Exception ex)
        {
            return "create table: " + Describe(ex);
        }

        try
        {
            var insert = $"INSERT INTO {table} (id, payload) VALUES (@Id, @Payload)";
            var select = $"SELECT id, payload FROM {table} WHERE id = @id";

            try
            {
                await db.ExecuteAsync(insert, new CoverageRow<T> { Id = 1, Payload = value });
            }
            catch (Exception ex)
            {
                return "write: " + Describe(ex);
            }

            CoverageRow<T> row;
            try
            {
                row = (await db.QueryAsync<CoverageRow<T>>(select, new { id = 1 })).Single();
            }
            catch (Exception ex)
            {
                return "read: " + Describe(ex);
            }

            if (!Same(value, row.Payload))
            {
                return $"read back {Show(row.Payload)}, wrote {Show(value)}";
            }

            try
            {
                var scalar = await db.ExecuteScalarAsync<T>($"SELECT payload FROM {table} WHERE id = 1");
                if (!Same(value, scalar))
                {
                    return $"scalar read back {Show(scalar)}, wrote {Show(value)}";
                }
            }
            catch (Exception ex)
            {
                return "scalar read: " + Describe(ex);
            }

            if (default(T) is null)
            {
                try
                {
                    await db.ExecuteAsync(insert, new CoverageRow<T> { Id = 2, Payload = default });
                }
                catch (Exception ex)
                {
                    return "write null: " + Describe(ex);
                }

                try
                {
                    var nullRow = (await db.QueryAsync<CoverageRow<T>>(select, new { id = 2 })).Single();
                    if (nullRow.Payload is not null)
                    {
                        return $"null read back as {Show(nullRow.Payload)}";
                    }
                }
                catch (Exception ex)
                {
                    return "read null: " + Describe(ex);
                }
            }

            return null;
        }
        finally
        {
            try
            {
                await db.DropTableIfExistsAsync(null, table);
            }
            catch (Exception)
            {
                // Best effort
            }
        }
    }

    private static bool Same(object? expected, object? actual)
    {
        if (expected is null || actual is null)
        {
            return expected is null && actual is null;
        }

        return expected switch
        {
            // The same instant; PostgreSQL keeps timestamptz in UTC.
            DateTimeOffset e when actual is DateTimeOffset a => e.UtcDateTime == a.UtcDateTime,
            DateTime e when actual is DateTime a => e.Ticks == a.Ticks,
            XNode e when actual is XNode a => XNode.DeepEquals(e, a),
            JsonDocument e when actual is JsonDocument a => JsonElement.DeepEquals(e.RootElement, a.RootElement),
            JsonElement e when actual is JsonElement a => JsonElement.DeepEquals(e, a),
            JsonNode e when actual is JsonNode a => JsonNode.DeepEquals(e, a),
            IPEndPoint or Uri or Version or IPAddress or PhysicalAddress => expected.Equals(actual),
            _ => expected.Equals(actual) || Json(expected) == Json(actual),
        };
    }

    private static string Json(object value)
    {
        try
        {
            return JsonSerializer.Serialize(value, CoverageJson);
        }
        catch (Exception)
        {
            return value.ToString() ?? string.Empty;
        }
    }

    private static string Show(object? value) =>
        value is null ? "null" : $"{Name(value.GetType())} {Truncate(Json(value), 80)}";

    private static string Describe(Exception ex)
    {
        while (ex is TargetInvocationException { InnerException: { } inner })
        {
            ex = inner;
        }

        var message = ex.Message.Split('\n')[0].Trim();
        return $"{ex.GetType().Name}: {Truncate(message, 200)}";
    }

    private static string Truncate(string text, int length) =>
        text.Length <= length ? text : text[..length] + "...";

    private static string Name(Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            return Name(underlying) + "?";
        }

        if (type.IsArray)
        {
            return Name(type.GetElementType()!) + "[]";
        }

        if (!type.IsGenericType)
        {
            return type.Name;
        }

        var name = type.Name[..type.Name.IndexOf('`', StringComparison.Ordinal)];
        return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(Name))}>";
    }

    [DmTable(tableName: "dml_cov")]
    public class CoverageRow<T>
    {
        [DmColumn("id", isPrimaryKey: true)]
        public int Id { get; set; }

        [DmColumn("payload", isNullable: true)]
        public T? Payload { get; set; }
    }

    public class CoverageAddress
    {
        public string Street { get; set; } = string.Empty;

        public string City { get; set; } = string.Empty;

        public int? Zip { get; set; }
    }

    public class CoverageNested
    {
        public string Name { get; set; } = string.Empty;

        public List<string> Tags { get; set; } = [];

        public CoverageAddress? Address { get; set; }

        public Dictionary<string, int> Counts { get; set; } = [];
    }

    public record CoverageRecord(string Name, int Count);

    public record struct CoveragePoint(int X, int Y);

    public enum CoverageColor
    {
        Red,
        Green,
        Blue,
    }

    public enum CoverageByteEnum : byte
    {
        A = 1,
        B = 2,
    }

    public enum CoverageLongEnum : long
    {
        Small = 1,
        Big = 5_000_000_000,
    }

    [Flags]
    public enum CoverageFlags
    {
        None = 0,
        A = 1,
        B = 2,
        C = 4,
    }
}
