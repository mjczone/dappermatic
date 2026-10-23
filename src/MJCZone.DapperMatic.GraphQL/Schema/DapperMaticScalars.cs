// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

#pragma warning disable SA1402 // The scalars belong together.
#pragma warning disable SA1649

using System.Globalization;
using System.Numerics;
using System.Text.Json;
using GraphQL.Types;
using GraphQLParser.AST;

namespace MJCZone.DapperMatic.GraphQL.Schema;

/// <summary>
/// The scalar types of one schema, made as its columns need them: a column's .NET type gives its scalar. Names follow
/// Hot Chocolate's, which Strawberry Shake maps to .NET types (<c>UUID</c>, <c>Long</c>, <c>Decimal</c>,
/// <c>DateTime</c> with an offset, <c>LocalDateTime</c>, <c>Date</c>, <c>LocalTime</c>, <c>TimeSpan</c>,
/// <c>ByteArray</c>, <c>Any</c>); Relay and Apollo Client read them as they come.
/// </summary>
internal sealed class DapperMaticScalars
{
    private readonly Dictionary<string, ScalarGraphType> _made = new(StringComparer.Ordinal);
    private readonly bool _bigNumbersAsStrings;

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticScalars"/> class.
    /// </summary>
    /// <param name="bigNumbersAsStrings">
    /// Whether 64-bit integers and decimals are written as strings (<c>BigInt</c>, <c>BigDecimal</c>), which JavaScript
    /// reads without losing digits; else as JSON numbers (<c>Long</c>, <c>Decimal</c>).
    /// </param>
    public DapperMaticScalars(bool bigNumbersAsStrings)
    {
        _bigNumbersAsStrings = bigNumbersAsStrings;
    }

    /// <summary>Gets the scalars made so far, each once.</summary>
    public IEnumerable<ScalarGraphType> Made => _made.Values;

    /// <summary>
    /// Gets the scalar of a column's .NET type.
    /// </summary>
    /// <param name="type">The type; a nullable value type counts as its underlying type.</param>
    /// <returns>The scalar.</returns>
    public ScalarGraphType For(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type switch
        {
            _ when type == typeof(bool) => Make("Boolean", () => new BooleanGraphType()),
            _ when type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort) || type == typeof(int) => Make("Int", () => new IntGraphType()),
            _ when type == typeof(uint) || type == typeof(long) => _bigNumbersAsStrings ? Make("BigInt", () => new BigIntGraphType()) : Make("Long", () => new LongGraphType()),
            _ when type == typeof(ulong) || type == typeof(Int128) || type == typeof(UInt128) || type == typeof(BigInteger) => Make("BigInt", () => new BigIntGraphType()),
            _ when type == typeof(float) || type == typeof(double) || type == typeof(Half) => Make("Float", () => new FloatGraphType()),
            _ when type == typeof(decimal) => _bigNumbersAsStrings ? Make("BigDecimal", () => new BigDecimalGraphType()) : Make("Decimal", () => new DecimalGraphType()),
            _ when type == typeof(string) || type == typeof(char) => Make("String", () => new StringGraphType()),
            _ when type == typeof(Guid) => Make("UUID", () => new UuidGraphType()),
            _ when type == typeof(DateTimeOffset) => Make("DateTime", () => new OffsetDateTimeGraphType()),
            _ when type == typeof(DateTime) => Make("LocalDateTime", () => new LocalDateTimeGraphType()),
            _ when type == typeof(DateOnly) => Make("Date", () => new DateGraphType()),
            _ when type == typeof(TimeOnly) => Make("LocalTime", () => new LocalTimeGraphType()),
            _ when type == typeof(TimeSpan) => Make("TimeSpan", () => new DurationGraphType()),
            _ when type == typeof(byte[]) => Make("ByteArray", () => new ByteArrayGraphType()),
            _ => Make("Any", () => new AnyGraphType()),
        };
    }

    /// <summary>Gets a scalar by name, made if it was not: <c>String</c>, <c>Int</c>, <c>Boolean</c>, <c>ID</c>.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The scalar.</returns>
    public ScalarGraphType Named(string name) =>
        name switch
        {
            "String" => Make(name, () => new StringGraphType()),
            "Int" => Make(name, () => new IntGraphType()),
            "Boolean" => Make(name, () => new BooleanGraphType()),
            "ID" => Make(name, () => new IdGraphType()),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Not a scalar made by name."),
        };

    private ScalarGraphType Make(string name, Func<ScalarGraphType> make)
    {
        if (!_made.TryGetValue(name, out var scalar))
        {
            scalar = make();
            _made[name] = scalar;
        }

        return scalar;
    }
}

/// <summary>A scalar written as a string, read from a string: the base of DapperMatic's own scalars.</summary>
internal abstract class TextScalarGraphType : ScalarGraphType
{
    /// <inheritdoc />
    public override object? ParseLiteral(GraphQLValue value) =>
        value switch
        {
            GraphQLNullValue => null,
            GraphQLStringValue text => Read((string)text.Value) ?? ThrowLiteralConversionError(value),
            _ => FromLiteral(value) ?? ThrowLiteralConversionError(value),
        };

    /// <inheritdoc />
    public override object? ParseValue(object? value) =>
        value switch
        {
            null => null,
            string text => Read(text) ?? ThrowValueConversionError(value),
            _ => FromValue(value) ?? ThrowValueConversionError(value),
        };

    /// <inheritdoc />
    public override object? Serialize(object? value) => value is null ? null : Write(value) ?? ThrowSerializationError(value);

    /// <summary>Reads a value from its text.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The value, or null if the text is not one.</returns>
    protected abstract object? Read(string text);

    /// <summary>Writes a value as text.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The text, or null if it is not a value of the scalar.</returns>
    protected abstract string? Write(object value);

    /// <summary>Reads a value from a literal other than a string, as a number.</summary>
    /// <param name="value">The literal.</param>
    /// <returns>The value, or null if the scalar takes none such.</returns>
    protected virtual object? FromLiteral(GraphQLValue value) => null;

    /// <summary>Reads a value given as something other than a string (a variable's number).</summary>
    /// <param name="value">The value.</param>
    /// <returns>The value, or null if the scalar takes none such.</returns>
    protected virtual object? FromValue(object value) => Write(value) is { } text ? Read(text) : null;
}

/// <summary><c>UUID</c>: a Guid, as <c>xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx</c>.</summary>
internal sealed class UuidGraphType : TextScalarGraphType
{
    /// <summary>Initializes a new instance of the <see cref="UuidGraphType"/> class.</summary>
    public UuidGraphType()
    {
        Name = "UUID";
        Description = "A UUID, as xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx.";
    }

    /// <inheritdoc />
    protected override object? Read(string text) => Guid.TryParse(text, out var guid) ? guid : null;

    /// <inheritdoc />
    protected override string? Write(object value) => value is Guid guid ? guid.ToString("D") : null;
}

/// <summary><c>DateTime</c>: a date and time with its offset from UTC (RFC 3339).</summary>
internal sealed class OffsetDateTimeGraphType : TextScalarGraphType
{
    // RFC 3339: the offset is part of the value, Z or ±hh:mm; text without one is no instant, and is refused rather
    // than read in the server's own time zone.
    private static readonly string[] Formats = ["yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'"];

    /// <summary>Initializes a new instance of the <see cref="OffsetDateTimeGraphType"/> class.</summary>
    public OffsetDateTimeGraphType()
    {
        Name = "DateTime";
        Description = "A date and time with its offset from UTC, as 2024-05-17T09:30:00.000+02:00 (RFC 3339).";
    }

    /// <inheritdoc />
    protected override object? Read(string text) =>
        DateTimeOffset.TryParseExact(text.ToUpperInvariant(), Formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value) ? value : null;

    /// <inheritdoc />
    protected override string? Write(object value) =>
        value switch
        {
            DateTimeOffset offset => offset.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFFzzz", CultureInfo.InvariantCulture),
            DateTime { Kind: DateTimeKind.Utc } utc => new DateTimeOffset(utc).ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFFzzz", CultureInfo.InvariantCulture),
            _ => null,
        };
}

/// <summary><c>LocalDateTime</c>: a date and time of no particular offset.</summary>
internal sealed class LocalDateTimeGraphType : TextScalarGraphType
{
    // A date and time as written, of no offset: text with one (a JavaScript Date's toISOString(), ending in Z) is
    // refused rather than moved into the server's own time zone. Send a DateTime's instant as one.
    private static readonly string[] Formats = ["yyyy-MM-dd'T'HH:mm:ss.FFFFFFF", "yyyy-MM-dd'T'HH:mm"];

    /// <summary>Initializes a new instance of the <see cref="LocalDateTimeGraphType"/> class.</summary>
    public LocalDateTimeGraphType()
    {
        Name = "LocalDateTime";
        Description = "A date and time of no particular offset, as 2024-05-17T09:30:00.";
    }

    /// <inheritdoc />
    protected override object? Read(string text) =>
        DateTime.TryParseExact(text.ToUpperInvariant(), Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? DateTime.SpecifyKind(value, DateTimeKind.Unspecified) : null;

    /// <inheritdoc />
    protected override string? Write(object value) =>
        value is DateTime time ? time.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture) : null;
}

/// <summary><c>Date</c>: a date, as <c>yyyy-MM-dd</c>.</summary>
internal sealed class DateGraphType : TextScalarGraphType
{
    /// <summary>Initializes a new instance of the <see cref="DateGraphType"/> class.</summary>
    public DateGraphType()
    {
        Name = "Date";
        Description = "A date, as 2024-05-17.";
    }

    /// <inheritdoc />
    protected override object? Read(string text) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value : null;

    /// <inheritdoc />
    protected override string? Write(object value) =>
        value is DateOnly date ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null;
}

/// <summary><c>LocalTime</c>: a time of day.</summary>
internal sealed class LocalTimeGraphType : TextScalarGraphType
{
    /// <summary>Initializes a new instance of the <see cref="LocalTimeGraphType"/> class.</summary>
    public LocalTimeGraphType()
    {
        Name = "LocalTime";
        Description = "A time of day, as 09:30:00.";
    }

    /// <inheritdoc />
    protected override object? Read(string text) =>
        TimeOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value : null;

    /// <inheritdoc />
    protected override string? Write(object value) =>
        value is TimeOnly time ? time.ToString("HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture) : null;
}

/// <summary><c>TimeSpan</c>: a duration, as ISO 8601 (<c>PT1H30M</c>).</summary>
internal sealed class DurationGraphType : TextScalarGraphType
{
    /// <summary>Initializes a new instance of the <see cref="DurationGraphType"/> class.</summary>
    public DurationGraphType()
    {
        Name = "TimeSpan";
        Description = "A duration, as ISO 8601: PT1H30M.";
    }

    /// <inheritdoc />
    protected override object? Read(string text)
    {
        try
        {
            return System.Xml.XmlConvert.ToTimeSpan(text);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    protected override string? Write(object value) => value is TimeSpan span ? System.Xml.XmlConvert.ToString(span) : null;
}

/// <summary><c>ByteArray</c>: bytes, as base64.</summary>
internal sealed class ByteArrayGraphType : TextScalarGraphType
{
    /// <summary>Initializes a new instance of the <see cref="ByteArrayGraphType"/> class.</summary>
    public ByteArrayGraphType()
    {
        Name = "ByteArray";
        Description = "Bytes, as base64.";
    }

    /// <inheritdoc />
    protected override object? Read(string text)
    {
        var bytes = new byte[(text.Length * 3 / 4) + 3];
        return Convert.TryFromBase64String(text, bytes, out var written) ? bytes[..written] : null;
    }

    /// <inheritdoc />
    protected override string? Write(object value) => value is byte[] bytes ? Convert.ToBase64String(bytes) : null;
}

/// <summary><c>BigInt</c>: an integer, as a string, so that JavaScript reads it without losing digits.</summary>
internal sealed class BigIntGraphType : TextScalarGraphType
{
    /// <summary>Initializes a new instance of the <see cref="BigIntGraphType"/> class.</summary>
    public BigIntGraphType()
    {
        Name = "BigInt";
        Description = "An integer, as a string: 9007199254740993.";
    }

    /// <inheritdoc />
    protected override object? Read(string text) =>
        BigInteger.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <inheritdoc />
    protected override string? Write(object value) =>
        value switch
        {
            sbyte or byte or short or ushort or int or uint or long or ulong or Int128 or UInt128 or BigInteger => Convert.ToString(value, CultureInfo.InvariantCulture),
            decimal d when d == decimal.Truncate(d) => d.ToString("0", CultureInfo.InvariantCulture),
            _ => null,
        };

    /// <inheritdoc />
    protected override object? FromLiteral(GraphQLValue value) => value is GraphQLIntValue number ? Read((string)number.Value) : null;
}

/// <summary><c>BigDecimal</c>: a decimal number, as a string, so that JavaScript reads it without losing digits.</summary>
internal sealed class BigDecimalGraphType : TextScalarGraphType
{
    /// <summary>Initializes a new instance of the <see cref="BigDecimalGraphType"/> class.</summary>
    public BigDecimalGraphType()
    {
        Name = "BigDecimal";
        Description = "A decimal number, as a string: 1234.50.";
    }

    /// <inheritdoc />
    protected override object? Read(string text) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <inheritdoc />
    protected override string? Write(object value) =>
        value switch
        {
            decimal d => d.ToString(CultureInfo.InvariantCulture),
            sbyte or byte or short or ushort or int or uint or long or ulong => Convert.ToString(value, CultureInfo.InvariantCulture),
            _ => null,
        };

    /// <inheritdoc />
    protected override object? FromLiteral(GraphQLValue value) =>
        value switch
        {
            GraphQLIntValue number => Read((string)number.Value),
            GraphQLFloatValue number => Read((string)number.Value),
            _ => null,
        };
}

/// <summary>
/// <c>Any</c>: a value of a column GraphQL has no type for (JSON, arrays, ranges, shapes), as JSON: what is JSON already
/// as it is, anything else as its text.
/// </summary>
internal sealed class AnyGraphType : ScalarGraphType
{
    /// <summary>Initializes a new instance of the <see cref="AnyGraphType"/> class.</summary>
    public AnyGraphType()
    {
        Name = "Any";
        Description = "Any value, as JSON.";
    }

    /// <inheritdoc />
    public override object? ParseLiteral(GraphQLValue value) =>
        value switch
        {
            GraphQLNullValue => null,
            GraphQLStringValue text => (string)text.Value,
            GraphQLIntValue number => long.TryParse((string)number.Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var l)
                ? l
                : decimal.Parse((string)number.Value, CultureInfo.InvariantCulture),
            GraphQLFloatValue number => double.Parse((string)number.Value, CultureInfo.InvariantCulture),
            GraphQLBooleanValue flag => flag is GraphQLTrueBooleanValue,
            GraphQLEnumValue name => (string)name.Name.Value,
            GraphQLListValue list => list.Values?.Select(ParseLiteral).ToList() ?? [],
            GraphQLObjectValue obj => obj.Fields?.ToDictionary(f => (string)f.Name.Value, f => ParseLiteral(f.Value)) ?? [],
            _ => ThrowLiteralConversionError(value),
        };

    /// <inheritdoc />
    public override bool CanParseLiteral(GraphQLValue value) => true;

    /// <inheritdoc />
    public override object? ParseValue(object? value) => value;

    /// <inheritdoc />
    public override bool CanParseValue(object? value) => true;

    /// <inheritdoc />
    public override object? Serialize(object? value) =>
        value switch
        {
            null => null,
            string or bool or sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal => value,
            JsonElement or JsonDocument or System.Text.Json.Nodes.JsonNode => value,
            System.Collections.IDictionary or System.Collections.IEnumerable => value,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture),
        };
}
