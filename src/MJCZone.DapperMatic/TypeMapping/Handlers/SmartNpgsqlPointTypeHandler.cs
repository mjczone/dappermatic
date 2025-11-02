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
/// Smart type handler for NpgsqlPoint with provider-specific optimization.
/// PostgreSQL: Native point type
/// Others: WKT (Well-Known Text) format - "POINT(x y)"
/// </summary>
public class SmartNpgsqlPointTypeHandler : SqlMapper.ITypeHandler
{
    /// <summary>
    /// Sets the parameter value for a point.
    /// PostgreSQL: Passes point directly (Npgsql converts NpgsqlPoint to native PostgreSQL point).
    /// Other providers: Serializes point to WKT format.
    /// </summary>
    /// <param name="parameter">The database parameter to set.</param>
    /// <param name="value">The NpgsqlPoint value to store.</param>
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
            // PostgreSQL: Native point type handling
            parameter.Value = value;
        }
        else
        {
            // Other providers: WKT serialization
            var valueType = value.GetType();
            var x = valueType.GetProperty("X")?.GetValue(value);
            var y = valueType.GetProperty("Y")?.GetValue(value);

            parameter.Value = $"POINT({x} {y})";
            parameter.DbType = DbType.String;
        }
    }

    /// <summary>
    /// Parses a database value back to NpgsqlPoint.
    /// PostgreSQL: Value is already NpgsqlPoint from Npgsql.
    /// Other providers: Deserializes from WKT format.
    /// </summary>
    /// <param name="destinationType">The target type (NpgsqlPoint).</param>
    /// <param name="value">The database value to parse.</param>
    /// <returns>An NpgsqlPoint instance.</returns>
    public object? Parse(Type destinationType, object value)
    {
        if (value == null || value is DBNull)
        {
            return null;
        }

        var valueType = value.GetType();

        // Check if value is already an NpgsqlPoint (PostgreSQL native)
        if (valueType.Name == "NpgsqlPoint")
        {
            return value;
        }

        // Parse from string format (other providers)
        var str = value.ToString() ?? string.Empty;

        double x,
            y;

        // Try PostgreSQL native format: "(x,y)"
        if (str.StartsWith('(') && str.EndsWith(')'))
        {
            var coords = str.Substring(1, str.Length - 2).Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (coords.Length != 2)
            {
                throw new FormatException($"Invalid PostgreSQL point format. Expected '(x,y)', got: {str}");
            }
            x = double.Parse(coords[0], CultureInfo.InvariantCulture);
            y = double.Parse(coords[1], CultureInfo.InvariantCulture);
        }
        // Try WKT format: "POINT(x y)"
        else if (str.StartsWith("POINT(", StringComparison.OrdinalIgnoreCase) && str.EndsWith(')'))
        {
            var coords = str.Substring(6, str.Length - 7).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (coords.Length != 2)
            {
                throw new FormatException($"Invalid WKT point format. Expected 'POINT(x y)', got: {str}");
            }
            x = double.Parse(coords[0], CultureInfo.InvariantCulture);
            y = double.Parse(coords[1], CultureInfo.InvariantCulture);
        }
        else
        {
            throw new FormatException($"Invalid point format. Expected '(x,y)' or 'POINT(x y)', got: {str}");
        }

        // Create NpgsqlPoint using reflection (avoids direct Npgsql dependency)
        var pointType = Type.GetType("NpgsqlTypes.NpgsqlPoint, Npgsql");
        if (pointType == null)
        {
            throw new InvalidOperationException("NpgsqlPoint type not found. Ensure Npgsql package is referenced.");
        }

        var ctor = pointType.GetConstructor(new[] { typeof(double), typeof(double) });
        if (ctor == null)
        {
            throw new InvalidOperationException("Could not find appropriate constructor for NpgsqlPoint.");
        }

        return ctor.Invoke(new object[] { x, y });
    }
}
