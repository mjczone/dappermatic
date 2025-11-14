// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Text.Json;
using Dapper;

namespace MJCZone.DapperMatic.TypeMapping.Handlers;

/// <summary>
/// Type handler for List&lt;T&gt; to support list data across all database providers.
/// Serializes List to JSON array string for storage and deserializes JSON back to List.
/// </summary>
/// <typeparam name="T">The list element type.</typeparam>
public class ListTypeHandler<T> : SqlMapper.TypeHandler<List<T>>
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    /// <summary>
    /// Sets the parameter value for a List.
    /// Converts List to JSON array string for database storage.
    /// </summary>
    /// <param name="parameter">The database parameter to set.</param>
    /// <param name="value">The List value to serialize.</param>
    public override void SetValue(IDbDataParameter parameter, List<T>? value)
    {
        if (value == null)
        {
            parameter.Value = DBNull.Value;
        }
        else
        {
            var jsonString = JsonSerializer.Serialize(value, SerializerOptions);

            // PostgreSQL requires explicit JSONB type for jsonb columns
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
    /// Parses the database value to a List.
    /// Converts JSON array string from database back to List.
    /// </summary>
    /// <param name="value">The database value to parse.</param>
    /// <returns>The parsed List, or null if the value is null or DBNull.</returns>
    public override List<T>? Parse(object value)
    {
        if (value == null || value is DBNull)
        {
            return null;
        }

        // If already a List (shouldn't happen, but handle it)
        if (value is List<T> list)
        {
            return list;
        }

        // Parse string to List
        var jsonString = value.ToString();
        if (string.IsNullOrWhiteSpace(jsonString))
        {
            return null;
        }

        return JsonSerializer.Deserialize<List<T>>(jsonString);
    }
}
