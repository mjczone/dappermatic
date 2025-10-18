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
using DbQueryLogging;
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
    protected virtual async Task Can_create_tables_from_model_classes_async(Type type)
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
                db is LoggedDbConnection ldb
                && (ldb.Inner is SqliteConnection || ldb.Inner is SQLiteConnection)
            )
        )
        {
            // For SQLite, we can retrieve the table definition using PRAGMA
            // Note: This will not return column data types, only names and nullability
            var tableDefAsSql = await db.QueryAsync(
                $"PRAGMA table_info('{dbTableDef.TableName}');"
            );
            Assert.NotNull(tableDefAsSql);
            Output.WriteLine(
                $"Table definition for {tableDef.TableName}:\n{JsonSerializer.Serialize(tableDefAsSql)}"
            );
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
                Assert.False(
                    dbColumn.IsNullable,
                    $"Column {column.ColumnName} should not be nullable."
                );
            }
        }

        var dropped = await db.DropTableIfExistsAsync(tableDef.SchemaName, tableDef.TableName);
        Assert.True(dropped);
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
    [DmColumn(
        "CurrencyDecimal",
        providerDataType: "decimal",
        precision: 18,
        scale: 2,
        isNullable: false
    )]
    public decimal CurrencyDecimal { get; set; }

    [DmColumn(
        "HighPrecisionDecimal",
        providerDataType: "decimal",
        precision: 10,
        scale: 4,
        isNullable: false
    )]
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
