// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Globalization;
using System.Text.Json;
using Dapper;

namespace MJCZone.DapperMatic.TypeMapping.Handlers;

/// <summary>
/// Stores a value as JSON: a collection, a dictionary, a JSON document node, or an object of the application's
/// own type. PostgreSQL receives the text as <c>jsonb</c>; other providers store it in the JSON or text column DDL
/// creates for the type.
/// </summary>
/// <remarks>
/// A value read back as the type itself is returned as it is, such as a dictionary Npgsql reads from an
/// <c>hstore</c> column.
/// </remarks>
/// <typeparam name="T">The type stored.</typeparam>
public class JsonTypeHandler<T> : SqlMapper.TypeHandler<T>
{
    /// <inheritdoc />
    public override void SetValue(IDbDataParameter parameter, T? value)
    {
        if (value is null)
        {
            parameter.Value = DBNull.Value;
            return;
        }

        if (ProviderParameter.IsNpgsql(parameter))
        {
            ProviderParameter.SetNpgsqlDbType(parameter, "Jsonb");
        }

        parameter.Value = JsonSerializer.Serialize(value, DapperMaticTypeMapping.JsonOptions);
    }

    /// <inheritdoc />
    public override T? Parse(object value)
    {
        var options = DapperMaticTypeMapping.JsonOptions;
        return value switch
        {
            null or DBNull => default,
            T typed => typed,
            string text => string.IsNullOrWhiteSpace(text) ? default : JsonSerializer.Deserialize<T>(text, options),
            byte[] utf8 => utf8.Length == 0 ? default : JsonSerializer.Deserialize<T>(utf8, options),
            JsonDocument document => document.RootElement.Deserialize<T>(options),
            JsonElement element => element.Deserialize<T>(options),
            _ => JsonSerializer.Deserialize<T>(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null", options),
        };
    }
}
