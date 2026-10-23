// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Net;
using MJCZone.DapperMatic.Dml.Rest;

namespace MJCZone.DapperMatic.Tests;

/// <summary>
/// Kept results read back with their values' types as the driver gave them: expands and cursors compare them.
/// </summary>
public class RestCacheCodecTests
{
    [Fact]
    public void Rows_are_read_back_with_their_types()
    {
        var local = new DateTime(2026, 10, 22, 13, 45, 7, 123, DateTimeKind.Local);
        var row = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["null"] = null,
            ["bool"] = true,
            ["byte"] = (byte)200,
            ["sbyte"] = (sbyte)-5,
            ["short"] = (short)-300,
            ["ushort"] = (ushort)60000,
            ["int"] = -70000,
            ["uint"] = 4000000000u,
            ["long"] = long.MinValue,
            ["ulong"] = ulong.MaxValue,
            ["float"] = 1.5f,
            ["double"] = Math.PI,
            ["decimal"] = 12345678901234567890.123456789m,
            ["char"] = 'é',
            ["string"] = "naïve ☃",
            ["guid"] = Guid.NewGuid(),
            ["utc"] = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            ["local"] = local,
            ["unspecified"] = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Unspecified),
            ["offset"] = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(-7)),
            ["date"] = new DateOnly(2026, 10, 22),
            ["time"] = new TimeOnly(23, 59, 58, 999),
            ["span"] = TimeSpan.FromMilliseconds(-1234567),
            ["bytes"] = new byte[] { 0, 1, 255 },
        };

        var rows = RestCacheCodec.DecodeRows(RestCacheCodec.Encode([row, new Dictionary<string, object?>(StringComparer.Ordinal)])!);

        Assert.Equal(2, rows.Count);
        Assert.Empty(rows[1]);
        Assert.Equal(row.Keys, rows[0].Keys);
        foreach (var (name, value) in row)
        {
            var read = rows[0][name];
            Assert.Equal(value?.GetType(), read?.GetType());
            Assert.Equal(value, read);
        }

        Assert.Equal(DateTimeKind.Local, ((DateTime)rows[0]["local"]!).Kind);
        Assert.Equal(TimeSpan.FromHours(-7), ((DateTimeOffset)rows[0]["offset"]!).Offset);
    }

    [Fact]
    public void Values_of_other_types_are_not_kept()
    {
        Assert.Null(RestCacheCodec.Encode([new Dictionary<string, object?> { ["ip"] = IPAddress.Loopback }]));
        Assert.Null(RestCacheCodec.Encode([new Dictionary<string, object?> { ["array"] = new[] { 1, 2 } }]));

        var cache = new RestCache { Duration = _ => null, Versions = (_, _) => Task.FromResult(string.Empty), Get = (_, _) => Task.FromResult<byte[]?>(null), Set = (_, _, _, _) => Task.CompletedTask, Scope = "ds" };
        Assert.Null(cache.Key("v", "rows", "SELECT 1", [new("p0", IPAddress.Loopback)]));

        // Every part of the key counts: the scope, the versions, the kind, the SQL, each parameter's name, type and value.
        var key = cache.Key("v", "rows", "SELECT @p0", [new("p0", 1)]);
        Assert.StartsWith("dm:r:", key);
        Assert.Equal(key, cache.Key("v", "rows", "SELECT @p0", [new("p0", 1)]));
        Assert.NotEqual(key, cache.Key("w", "rows", "SELECT @p0", [new("p0", 1)]));
        Assert.NotEqual(key, cache.Key("v", "count", "SELECT @p0", [new("p0", 1)]));
        Assert.NotEqual(key, cache.Key("v", "rows", "SELECT  @p0", [new("p0", 1)]));
        Assert.NotEqual(key, cache.Key("v", "rows", "SELECT @p0", [new("p1", 1)]));
        Assert.NotEqual(key, cache.Key("v", "rows", "SELECT @p0", [new("p0", 1L)]));
        Assert.NotEqual(key, cache.Key("v", "rows", "SELECT @p0", [new("p0", 2)]));
        Assert.NotEqual(key, new RestCache { Duration = cache.Duration, Versions = cache.Versions, Get = cache.Get, Set = cache.Set, Scope = "other" }.Key("v", "rows", "SELECT @p0", [new("p0", 1)]));
        Assert.Equal(42L, RestCacheCodec.DecodeCount(RestCacheCodec.Encode(42L)));
    }
}
