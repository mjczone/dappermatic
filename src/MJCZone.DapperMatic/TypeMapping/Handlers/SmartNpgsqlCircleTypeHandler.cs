// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System;
using System.Data;
using System.Globalization;
using Dapper;

namespace MJCZone.DapperMatic.TypeMapping.Handlers;

/// <summary>
/// Smart type handler for NpgsqlCircle with provider-specific optimization.
/// PostgreSQL: Native circle type
/// Others: Extended WKT format - "CIRCLE(x y, radius)"
/// </summary>
public class SmartNpgsqlCircleTypeHandler : SqlMapper.ITypeHandler
{
    /// <summary>
    /// Sets the parameter value for a circle.
    /// PostgreSQL: Passes circle directly (Npgsql converts NpgsqlCircle to native PostgreSQL circle).
    /// Other providers: Serializes circle to extended WKT format.
    /// </summary>
    /// <param name="parameter">The database parameter to set.</param>
    /// <param name="value">The NpgsqlCircle value to store.</param>
    public void SetValue(IDbDataParameter parameter, object? value)
    {
        if (value == null)
        {
            parameter.Value = DBNull.Value;
            return;
        }

        var paramType = parameter.GetType().FullName ?? string.Empty;

        if (paramType.Contains("Npgsql", StringComparison.Ordinal))
        {
            // PostgreSQL: Native circle type handling
            parameter.Value = value;
        }
        else
        {
            // Other providers: Extended WKT serialization
            var valueType = value.GetType();
            var center = valueType.GetProperty("Center")?.GetValue(value);
            var radius = valueType.GetProperty("Radius")?.GetValue(value);

            if (center == null)
            {
                parameter.Value = DBNull.Value;
                return;
            }

            var centerType = center.GetType();
            var x = centerType.GetProperty("X")?.GetValue(center);
            var y = centerType.GetProperty("Y")?.GetValue(center);

            parameter.Value = $"CIRCLE({x} {y}, {radius})";
            parameter.DbType = DbType.String;
        }
    }

    /// <summary>
    /// Parses a database value back to NpgsqlCircle.
    /// PostgreSQL: Value is already NpgsqlCircle from Npgsql.
    /// Other providers: Deserializes from extended WKT format.
    /// </summary>
    /// <param name="destinationType">The target type (NpgsqlCircle).</param>
    /// <param name="value">The database value to parse.</param>
    /// <returns>An NpgsqlCircle instance.</returns>
    public object? Parse(Type destinationType, object value)
    {
        if (value == null || value is DBNull)
        {
            return null;
        }

        var valueType = value.GetType();

        // Check if value is already an NpgsqlCircle (PostgreSQL native)
        if (valueType.Name == "NpgsqlCircle")
        {
            return value;
        }

        // Parse from extended WKT format (other providers)
        var wkt = value.ToString() ?? string.Empty;

        // Expected format: "CIRCLE(x y, radius)"
#pragma warning disable CA1865 // Use char overload (no StringComparison overload available for char)
        if (
            !wkt.StartsWith("CIRCLE(", StringComparison.OrdinalIgnoreCase)
            || !wkt.EndsWith(")", StringComparison.Ordinal)
        )
#pragma warning restore CA1865
        {
            throw new FormatException($"Invalid WKT format for circle. Expected 'CIRCLE(x y, radius)', got: {wkt}");
        }

        var content = wkt.Substring(7, wkt.Length - 8); // Remove "CIRCLE(" and ")"
        var parts = content.Split(',', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 2)
        {
            throw new FormatException($"Invalid WKT format for circle. Expected 'x y, radius', got: {content}");
        }

        var coords = parts[0].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (coords.Length != 2)
        {
            throw new FormatException($"Invalid center coordinates in circle WKT.");
        }

        var x = double.Parse(coords[0], CultureInfo.InvariantCulture);
        var y = double.Parse(coords[1], CultureInfo.InvariantCulture);
        var radius = double.Parse(parts[1].Trim(), CultureInfo.InvariantCulture);

        // Create NpgsqlPoint for center
        var pointType = Type.GetType("NpgsqlTypes.NpgsqlPoint, Npgsql");
        if (pointType == null)
        {
            throw new InvalidOperationException("NpgsqlPoint type not found. Ensure Npgsql package is referenced.");
        }

        var pointCtor = pointType.GetConstructor(new[] { typeof(double), typeof(double) });
        if (pointCtor == null)
        {
            throw new InvalidOperationException("Could not find appropriate constructor for NpgsqlPoint.");
        }

        var center = pointCtor.Invoke(new object[] { x, y });

        // Create NpgsqlCircle using reflection
        var circleType = Type.GetType("NpgsqlTypes.NpgsqlCircle, Npgsql");
        if (circleType == null)
        {
            throw new InvalidOperationException("NpgsqlCircle type not found. Ensure Npgsql package is referenced.");
        }

        var circleCtor = circleType.GetConstructor(new[] { pointType, typeof(double) });
        if (circleCtor == null)
        {
            throw new InvalidOperationException("Could not find appropriate constructor for NpgsqlCircle.");
        }

        return circleCtor.Invoke(new[] { center, radius });
    }
}
