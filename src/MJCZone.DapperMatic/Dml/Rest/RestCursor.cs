// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Text;
using System.Text.Json;

namespace MJCZone.DapperMatic.Dml.Rest;

/// <summary>
/// The cursor of a page read in keyset order: the ordering it belongs to, and the last row's values in it, opaque to
/// clients. It needs no signature: it holds values of columns the caller may read, so one written by hand tells no
/// more than a <c>$filter</c> would.
/// </summary>
internal static class RestCursor
{
    /// <summary>
    /// Writes the cursor after a row.
    /// </summary>
    /// <param name="keyset">The ordering: each column and whether it descends.</param>
    /// <param name="values">The row's values of those columns.</param>
    /// <returns>The cursor.</returns>
    public static string Encode(IReadOnlyList<(string Column, bool Descending)> keyset, IReadOnlyList<object?> values)
    {
        var json = JsonSerializer.Serialize(new
        {
            k = keyset.Select(k => (k.Descending ? "-" : "+") + k.Column).ToArray(),
            v = values.Select(Text).ToArray(),
        });
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>
    /// Reads a cursor's values, for the ordering it must belong to.
    /// </summary>
    /// <param name="cursor">The cursor.</param>
    /// <param name="keyset">The ordering of the query it continues.</param>
    /// <returns>The values, as text or null, in the ordering's order.</returns>
    /// <exception cref="ArgumentException">The cursor is not one, or is of another ordering.</exception>
    public static IReadOnlyList<string?> Decode(string cursor, IReadOnlyList<(string Column, bool Descending)> keyset)
    {
        try
        {
            var base64 = cursor.Replace('-', '+').Replace('_', '/');
            base64 += new string('=', (4 - (base64.Length % 4)) % 4);
            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(base64)));
            var keys = document.RootElement.GetProperty("k").EnumerateArray().Select(e => e.GetString()).ToList();
            var values = document.RootElement.GetProperty("v").EnumerateArray().Select(e => e.ValueKind == JsonValueKind.Null ? null : e.GetString()).ToList();
            var expected = keyset.Select(k => (k.Descending ? "-" : "+") + k.Column).ToList();
            if (!keys.SequenceEqual(expected, StringComparer.OrdinalIgnoreCase) || values.Count != keyset.Count)
            {
                throw new ArgumentException("$after is the cursor of a query in another order: give the same $orderby as the page it came from.");
            }

            return values;
        }
        catch (Exception e) when (e is FormatException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new ArgumentException("$after is not a cursor this API gave: pass a page's nextCursor as it is.", e);
        }
    }

    /// <summary>
    /// Writes the condition of the rows after a cursor's: in the query language, so that values are converted to the
    /// columns' types and NULLs ordered as the query orders them (first ascending, last descending).
    /// </summary>
    /// <param name="keyset">The ordering.</param>
    /// <param name="values">The cursor's values.</param>
    /// <returns>The condition.</returns>
    public static RestNode After(IReadOnlyList<(string Column, bool Descending)> keyset, IReadOnlyList<string?> values)
    {
        // (a after va) or (a = va and b after vb) or (a = va and b = vb and c after vc) ...
        RestNode? any = null;
        for (var i = keyset.Count - 1; i >= 0; i--)
        {
            var (column, descending) = keyset[i];
            var path = new RestPath([column]);
            var value = values[i];
            RestNode after = value is null
                ? (descending ? new RestLiteral(false) : new RestBinary("ne", path, new RestLiteral(null)))
                : descending
                    ? new RestBinary("or", new RestBinary("lt", path, new RestLiteral(value)), new RestBinary("eq", path, new RestLiteral(null)))
                    : new RestBinary("gt", path, new RestLiteral(value));
            if (any is not null)
            {
                RestNode same = new RestBinary("eq", path, new RestLiteral(value));
                after = new RestBinary("or", after, new RestBinary("and", same, any));
            }

            any = after;
        }

        return any!;
    }

    // A value as text that converts back to its column's type exactly.
    private static string? Text(object? value) =>
        value switch
        {
            null or DBNull => null,
            DateTime time => time.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset time => time.ToString("O", CultureInfo.InvariantCulture),
            DateOnly date => date.ToString("O", CultureInfo.InvariantCulture),
            TimeOnly time => time.ToString("O", CultureInfo.InvariantCulture),
            byte[] bytes => Convert.ToBase64String(bytes),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString(),
        };
}
