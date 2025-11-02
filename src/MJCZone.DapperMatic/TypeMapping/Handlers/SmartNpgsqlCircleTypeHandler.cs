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
    private static readonly string[] CircleSeparator = new[] { ")," };

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

        // Parse from string format (other providers)
        var str = value.ToString() ?? string.Empty;

        double x, y, radius;

        // Try PostgreSQL native format: "<(x,y),r>"
        if (str.StartsWith('<') && str.EndsWith('>'))
        {
            var content = str.Substring(1, str.Length - 2); // Remove "<" and ">"
            var parts = content.Split(CircleSeparator, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2)
            {
                throw new FormatException($"Invalid PostgreSQL circle format. Expected '<(x,y),r>', got: {str}");
            }

            // Parse center point "(x,y"
            var centerStr = parts[0].TrimStart('(');
            var centerCoords = centerStr.Split(',', StringSplitOptions.RemoveEmptyEntries);

            if (centerCoords.Length != 2)
            {
                throw new FormatException($"Invalid PostgreSQL circle format. Expected '<(x,y),r>', got: {str}");
            }

            x = double.Parse(centerCoords[0], CultureInfo.InvariantCulture);
            y = double.Parse(centerCoords[1], CultureInfo.InvariantCulture);
            radius = double.Parse(parts[1].Trim(), CultureInfo.InvariantCulture);
        }
        // Try extended WKT format: "CIRCLE(x y, radius)"
        else if (str.StartsWith("CIRCLE(", StringComparison.OrdinalIgnoreCase) && str.EndsWith(')'))
        {
            var content = str.Substring(7, str.Length - 8); // Remove "CIRCLE(" and ")"
            var parts = content.Split(',', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2)
            {
                throw new FormatException($"Invalid WKT circle format. Expected 'CIRCLE(x y, radius)', got: {str}");
            }

            var coords = parts[0].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (coords.Length != 2)
            {
                throw new FormatException($"Invalid center coordinates in circle WKT, got: {str}");
            }

            x = double.Parse(coords[0], CultureInfo.InvariantCulture);
            y = double.Parse(coords[1], CultureInfo.InvariantCulture);
            radius = double.Parse(parts[1].Trim(), CultureInfo.InvariantCulture);
        }
        else
        {
            throw new FormatException($"Invalid circle format. Expected '<(x,y),r>' or 'CIRCLE(x y, radius)', got: {str}");
        }

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
