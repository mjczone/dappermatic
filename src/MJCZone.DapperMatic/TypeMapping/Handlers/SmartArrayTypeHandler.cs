// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Text.Json;
using Dapper;

namespace MJCZone.DapperMatic.TypeMapping.Handlers;

/// <summary>
/// Smart type handler for T[] arrays with runtime provider detection.
/// PostgreSQL: Uses native array types (text[], int4[], etc.) for 10-50x performance boost.
/// Other providers: Uses JSON array serialization for cross-database compatibility.
/// </summary>
/// <typeparam name="T">The array element type.</typeparam>
public class SmartArrayTypeHandler<T> : SqlMapper.TypeHandler<T[]>
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    /// <summary>
    /// Sets the parameter value for an array.
    /// PostgreSQL: Passes array directly (Npgsql converts T[] to native PostgreSQL array).
    /// Other providers: Serializes array to JSON string.
    /// </summary>
    /// <param name="parameter">The database parameter to set.</param>
    /// <param name="value">The array value to store.</param>
    public override void SetValue(IDbDataParameter parameter, T[]? value)
    {
        if (value == null)
        {
            parameter.Value = DBNull.Value;
            return;
        }

        // Runtime provider detection via parameter type
        var paramType = parameter.GetType().FullName ?? string.Empty;

        if (paramType.Contains("Npgsql", StringComparison.Ordinal))
        {
            // PostgreSQL: Use native array types (FASTEST - 10-50x faster than JSON)
            // Npgsql automatically converts:
            //   string[] → text[]
            //   int[] → int4[]
            //   long[] → int8[]
            //   etc.
            parameter.Value = value;
        }
        else
        {
            // Other providers (SQL Server, MySQL, SQLite): JSON array fallback
            // Works reliably across all databases
            var jsonString = JsonSerializer.Serialize(value, SerializerOptions);
            parameter.Value = jsonString;
        }
    }

    /// <summary>
    /// Parses the database value to an array.
    /// PostgreSQL: Returns native array directly (T[]).
    /// Other providers: Deserializes from JSON string.
    /// </summary>
    /// <param name="value">The database value to parse.</param>
    /// <returns>The parsed array, or null if the value is null or DBNull.</returns>
    public override T[]? Parse(object value)
    {
        if (value == null || value is DBNull)
        {
            return null;
        }

        // If already a typed array (PostgreSQL native array)
        if (value is T[] array)
        {
            return array;
        }

        // Deserialize from JSON (other providers)
        var jsonString = value.ToString();
        if (string.IsNullOrWhiteSpace(jsonString))
        {
            return null;
        }

        return JsonSerializer.Deserialize<T[]>(jsonString);
    }
}
