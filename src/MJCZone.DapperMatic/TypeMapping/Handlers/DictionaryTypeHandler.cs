// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Text.Json;
using Dapper;

namespace MJCZone.DapperMatic.TypeMapping.Handlers;

/// <summary>
/// Type handler for Dictionary&lt;TKey, TValue&gt; to support dictionary data across all database providers.
/// Serializes Dictionary to JSON string for storage and deserializes JSON back to Dictionary.
/// </summary>
/// <typeparam name="TKey">The dictionary key type.</typeparam>
/// <typeparam name="TValue">The dictionary value type.</typeparam>
public class DictionaryTypeHandler<TKey, TValue> : SqlMapper.TypeHandler<Dictionary<TKey, TValue>>
    where TKey : notnull
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    /// <summary>
    /// Sets the parameter value for a Dictionary.
    /// Converts Dictionary to JSON string for database storage.
    /// </summary>
    /// <param name="parameter">The database parameter to set.</param>
    /// <param name="value">The Dictionary value to serialize.</param>
    public override void SetValue(IDbDataParameter parameter, Dictionary<TKey, TValue>? value)
    {
        if (value == null)
        {
            parameter.Value = DBNull.Value;
        }
        else
        {
            var jsonString = JsonSerializer.Serialize(value, SerializerOptions);

            // PostgreSQL requires explicit Jsonb type for jsonb columns
            // Note: We use Jsonb instead of Hstore because JSON serialization format is incompatible with hstore
            // Use reflection to set NpgsqlDbType.Jsonb if available (avoid hard dependency on Npgsql)
            if (parameter.GetType().FullName?.Contains("Npgsql", StringComparison.Ordinal) == true)
            {
                var npgsqlDbTypeProperty = parameter.GetType().GetProperty("NpgsqlDbType");
                if (npgsqlDbTypeProperty != null)
                {
                    // Get the NpgsqlDbType enum and set it to Jsonb
                    var npgsqlDbTypeEnum = npgsqlDbTypeProperty.PropertyType;
                    var jsonbValue = Enum.Parse(npgsqlDbTypeEnum, "Jsonb");
                    npgsqlDbTypeProperty.SetValue(parameter, jsonbValue);
                }
            }

            parameter.Value = jsonString;
        }
    }

    /// <summary>
    /// Parses the database value to a Dictionary.
    /// Converts JSON string from database back to Dictionary.
    /// </summary>
    /// <param name="value">The database value to parse.</param>
    /// <returns>The parsed Dictionary, or null if the value is null or DBNull.</returns>
    public override Dictionary<TKey, TValue>? Parse(object value)
    {
        if (value == null || value is DBNull)
        {
            return null;
        }

        // If already a Dictionary (shouldn't happen, but handle it)
        if (value is Dictionary<TKey, TValue> dict)
        {
            return dict;
        }

        // Parse string to Dictionary
        var jsonString = value.ToString();
        if (string.IsNullOrWhiteSpace(jsonString))
        {
            return null;
        }

        return JsonSerializer.Deserialize<Dictionary<TKey, TValue>>(jsonString);
    }
}
