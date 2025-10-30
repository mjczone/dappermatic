// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Text.Json;
using Dapper;

namespace MJCZone.DapperMatic.TypeMapping.Handlers;

/// <summary>
/// Type handler for JsonDocument to support JSON data across all database providers.
/// Serializes JsonDocument to string for storage and deserializes string back to JsonDocument.
/// </summary>
public class JsonDocumentTypeHandler : SqlMapper.TypeHandler<JsonDocument>
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    /// <summary>
    /// Sets the parameter value for a JsonDocument.
    /// Converts JsonDocument to JSON string for database storage.
    /// </summary>
    /// <param name="parameter">The database parameter to set.</param>
    /// <param name="value">The JsonDocument value to serialize.</param>
    public override void SetValue(IDbDataParameter parameter, JsonDocument? value)
    {
        if (value == null)
        {
            parameter.Value = DBNull.Value;
        }
        else
        {
            var jsonString = JsonSerializer.Serialize(value.RootElement, SerializerOptions);

            // PostgreSQL requires explicit JSON/JSONB type for json/jsonb columns
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
    /// Parses the database value to a JsonDocument.
    /// Converts JSON string from database back to JsonDocument.
    /// </summary>
    /// <param name="value">The database value to parse.</param>
    /// <returns>The parsed JsonDocument, or null if the value is null or DBNull.</returns>
    public override JsonDocument? Parse(object value)
    {
        if (value == null || value is DBNull)
        {
            return null;
        }

        // If already a JsonDocument (shouldn't happen, but handle it)
        if (value is JsonDocument jsonDoc)
        {
            return jsonDoc;
        }

        // Parse string to JsonDocument
        var jsonString = value.ToString();
        if (string.IsNullOrWhiteSpace(jsonString))
        {
            return null;
        }

        return JsonDocument.Parse(jsonString);
    }
}
