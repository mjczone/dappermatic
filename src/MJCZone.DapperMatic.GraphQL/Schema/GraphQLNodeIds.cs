// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Buffers.Text;
using System.Text.Json;

namespace MJCZone.DapperMatic.GraphQL.Schema;

/// <summary>
/// Rows' global ids (<c>nodeId</c>): the table's database name and the row's key values, as base64url of a JSON array,
/// <c>["orders", 17]</c>. Opaque to clients; the datasource and schema are the endpoint's.
/// </summary>
internal static class GraphQLNodeIds
{
    /// <summary>
    /// Makes a row's global id.
    /// </summary>
    /// <param name="table">The table's database name.</param>
    /// <param name="keys">The row's key values, in the key's order.</param>
    /// <returns>The id.</returns>
    public static string Encode(string table, IEnumerable<object?> keys) =>
        Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(new object?[] { table }.Concat(keys).ToArray()));

    /// <summary>
    /// Reads a global id.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <returns>The table's database name and the key values, as JSON; null if the text is not an id.</returns>
    public static (string Table, JsonElement[] Keys)? Decode(string id)
    {
        try
        {
            using var json = JsonDocument.Parse(Base64Url.DecodeFromChars(id));
            var parts = json.RootElement;
            if (parts.ValueKind != JsonValueKind.Array || parts.GetArrayLength() < 2 || parts[0].ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return (parts[0].GetString()!, [.. parts.EnumerateArray().Skip(1).Select(k => k.Clone())]);
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }
    }
}
