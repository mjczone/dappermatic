// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text;

namespace MJCZone.DapperMatic.Dml.Rest;

/// <summary>
/// Where a REST engine keeps the results of statements reading tables that are cached: each kept under a key made of
/// the statement's SQL, its parameters' values, what else the results depend on, and the versions of the tables it
/// reads, which a write to one of them changes.
/// </summary>
internal sealed class RestCache
{
    /// <summary>
    /// Gets how long the results of a statement reading the tables may be kept, or null when one of them is not cached.
    /// </summary>
    public required Func<IReadOnlyList<RestTable>, TimeSpan?> Duration { get; init; }

    /// <summary>Gets the tables' current versions, as one text: a write to one of them changes it.</summary>
    public required Func<IReadOnlyList<RestTable>, CancellationToken, Task<string>> Versions { get; init; }

    /// <summary>Gets a function reading what is kept under a key, or null.</summary>
    public required Func<string, CancellationToken, Task<byte[]?>> Get { get; init; }

    /// <summary>Gets a function keeping a value under a key, for so long.</summary>
    public required Func<string, byte[], TimeSpan, CancellationToken, Task> Set { get; init; }

    /// <summary>
    /// Gets what else the results depend on, which the SQL does not show: the datasource, and the session values the
    /// database's own row-level security reads.
    /// </summary>
    public required string Scope { get; init; }

    /// <summary>
    /// Makes the key of a statement's results, or null when a parameter's value is not one the key can hold.
    /// </summary>
    /// <param name="versions">The versions of the tables it reads.</param>
    /// <param name="kind">What the statement returns: rows, or a count.</param>
    /// <param name="sql">The SQL.</param>
    /// <param name="parameters">The parameters' names and values.</param>
    /// <returns>The key, or null.</returns>
    public string? Key(string versions, string kind, string sql, IEnumerable<KeyValuePair<string, object?>> parameters)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Scope);
            writer.Write(versions);
            writer.Write(kind);
            writer.Write(sql);
            foreach (var parameter in parameters)
            {
                writer.Write(parameter.Key);
                if (!RestCacheCodec.TryWrite(writer, parameter.Value))
                {
                    return null;
                }
            }
        }

        return "dm:r:" + Convert.ToHexStringLower(SHA256.HashData(stream.GetBuffer().AsSpan(0, (int)stream.Length)));
    }
}

/// <summary>
/// Writes the results a <see cref="RestCache"/> keeps, and reads them back with their values' types as the database
/// driver gave them: a statement whose rows hold a value of another type is not kept.
/// </summary>
internal static class RestCacheCodec
{
    private enum Tag : byte
    {
        Null,
        Boolean,
        Byte,
        SByte,
        Int16,
        UInt16,
        Int32,
        UInt32,
        Int64,
        UInt64,
        Single,
        Double,
        Decimal,
        Char,
        String,
        Guid,
        DateTime,
        DateTimeOffset,
        DateOnly,
        TimeOnly,
        TimeSpan,
        Bytes,
    }

    /// <summary>
    /// Writes rows, or returns null when a value is not of a type that can be read back as it was.
    /// </summary>
    /// <param name="rows">The rows.</param>
    /// <returns>The bytes, or null.</returns>
    public static byte[]? Encode(List<Dictionary<string, object?>> rows)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(rows.Count);
            foreach (var row in rows)
            {
                writer.Write(row.Count);
                foreach (var (name, value) in row)
                {
                    writer.Write(name);
                    if (!TryWrite(writer, value))
                    {
                        return null;
                    }
                }
            }
        }

        return stream.ToArray();
    }

    /// <summary>
    /// Reads rows written by <see cref="Encode(List{Dictionary{string, object?}})"/>.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The rows, new ones.</returns>
    public static List<Dictionary<string, object?>> DecodeRows(byte[] bytes)
    {
        using var reader = new BinaryReader(new MemoryStream(bytes), Encoding.UTF8);
        var count = reader.ReadInt32();
        var rows = new List<Dictionary<string, object?>>(count);
        for (var i = 0; i < count; i++)
        {
            var columns = reader.ReadInt32();
            var row = new Dictionary<string, object?>(columns, StringComparer.Ordinal);
            for (var c = 0; c < columns; c++)
            {
                var name = reader.ReadString();
                row[name] = Read(reader);
            }

            rows.Add(row);
        }

        return rows;
    }

    /// <summary>Writes a count.</summary>
    /// <param name="count">The count.</param>
    /// <returns>The bytes.</returns>
    public static byte[] Encode(long count) => BitConverter.GetBytes(count);

    /// <summary>Reads a count.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The count.</returns>
    public static long DecodeCount(byte[] bytes) => BitConverter.ToInt64(bytes);

    /// <summary>
    /// Writes a value with its type, or returns false when it is not of a type that can be read back as it was.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The value.</param>
    /// <returns>Whether it was written.</returns>
    public static bool TryWrite(BinaryWriter writer, object? value)
    {
        switch (value)
        {
            case null or DBNull:
                writer.Write((byte)Tag.Null);
                break;
            case bool v:
                writer.Write((byte)Tag.Boolean);
                writer.Write(v);
                break;
            case byte v:
                writer.Write((byte)Tag.Byte);
                writer.Write(v);
                break;
            case sbyte v:
                writer.Write((byte)Tag.SByte);
                writer.Write(v);
                break;
            case short v:
                writer.Write((byte)Tag.Int16);
                writer.Write(v);
                break;
            case ushort v:
                writer.Write((byte)Tag.UInt16);
                writer.Write(v);
                break;
            case int v:
                writer.Write((byte)Tag.Int32);
                writer.Write(v);
                break;
            case uint v:
                writer.Write((byte)Tag.UInt32);
                writer.Write(v);
                break;
            case long v:
                writer.Write((byte)Tag.Int64);
                writer.Write(v);
                break;
            case ulong v:
                writer.Write((byte)Tag.UInt64);
                writer.Write(v);
                break;
            case float v:
                writer.Write((byte)Tag.Single);
                writer.Write(v);
                break;
            case double v:
                writer.Write((byte)Tag.Double);
                writer.Write(v);
                break;
            case decimal v:
                writer.Write((byte)Tag.Decimal);
                writer.Write(v);
                break;
            case char v:
                writer.Write((byte)Tag.Char);
                writer.Write((ushort)v);
                break;
            case string v:
                writer.Write((byte)Tag.String);
                writer.Write(v);
                break;
            case Guid v:
                writer.Write((byte)Tag.Guid);
                writer.Write(v.ToByteArray());
                break;
            case DateTime v:
                writer.Write((byte)Tag.DateTime);
                writer.Write(v.ToBinary());
                break;
            case DateTimeOffset v:
                writer.Write((byte)Tag.DateTimeOffset);
                writer.Write(v.Ticks);
                writer.Write(v.Offset.Ticks);
                break;
            case DateOnly v:
                writer.Write((byte)Tag.DateOnly);
                writer.Write(v.DayNumber);
                break;
            case TimeOnly v:
                writer.Write((byte)Tag.TimeOnly);
                writer.Write(v.Ticks);
                break;
            case TimeSpan v:
                writer.Write((byte)Tag.TimeSpan);
                writer.Write(v.Ticks);
                break;
            case byte[] v:
                writer.Write((byte)Tag.Bytes);
                writer.Write(v.Length);
                writer.Write(v);
                break;
            default:
                return false;
        }

        return true;
    }

    private static object? Read(BinaryReader reader) =>
        (Tag)reader.ReadByte() switch
        {
            Tag.Null => null,
            Tag.Boolean => reader.ReadBoolean(),
            Tag.Byte => reader.ReadByte(),
            Tag.SByte => reader.ReadSByte(),
            Tag.Int16 => reader.ReadInt16(),
            Tag.UInt16 => reader.ReadUInt16(),
            Tag.Int32 => reader.ReadInt32(),
            Tag.UInt32 => reader.ReadUInt32(),
            Tag.Int64 => reader.ReadInt64(),
            Tag.UInt64 => reader.ReadUInt64(),
            Tag.Single => reader.ReadSingle(),
            Tag.Double => reader.ReadDouble(),
            Tag.Decimal => reader.ReadDecimal(),
            Tag.Char => (char)reader.ReadUInt16(),
            Tag.String => reader.ReadString(),
            Tag.Guid => new Guid(reader.ReadBytes(16)),
            Tag.DateTime => DateTime.FromBinary(reader.ReadInt64()),
            Tag.DateTimeOffset => new DateTimeOffset(reader.ReadInt64(), new TimeSpan(reader.ReadInt64())),
            Tag.DateOnly => DateOnly.FromDayNumber(reader.ReadInt32()),
            Tag.TimeOnly => new TimeOnly(reader.ReadInt64()),
            Tag.TimeSpan => new TimeSpan(reader.ReadInt64()),
            Tag.Bytes => reader.ReadBytes(reader.ReadInt32()),
            var tag => throw new InvalidDataException($"A cached result holds an unknown value tag, {tag}."),
        };
}
