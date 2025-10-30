// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using Dapper;

namespace MJCZone.DapperMatic.TypeMapping.Handlers;

/// <summary>
/// Smart type handler for NpgsqlPolygon with provider-specific optimization.
/// PostgreSQL: Native polygon type
/// Others: WKT (Well-Known Text) format - "POLYGON((x1 y1, x2 y2, ..., x1 y1))"
/// </summary>
public class SmartNpgsqlPolygonTypeHandler : SqlMapper.ITypeHandler
{
    /// <summary>
    /// Sets the parameter value for a polygon.
    /// PostgreSQL: Passes polygon directly (Npgsql converts NpgsqlPolygon to native PostgreSQL polygon).
    /// Other providers: Serializes polygon to WKT format.
    /// </summary>
    /// <param name="parameter">The database parameter to set.</param>
    /// <param name="value">The NpgsqlPolygon value to store.</param>
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
            // PostgreSQL: Native polygon type handling
            parameter.Value = value;
        }
        else
        {
            // Other providers: WKT POLYGON serialization
            var valueType = value.GetType();
            var pointsArray = valueType.GetProperty("Points")?.GetValue(value) as Array;

            if (pointsArray == null || pointsArray.Length == 0)
            {
                parameter.Value = DBNull.Value;
                return;
            }

            var sb = new StringBuilder("POLYGON((");

            for (var i = 0; i < pointsArray.Length; i++)
            {
                var point = pointsArray.GetValue(i);
                if (point != null)
                {
                    var pointType = point.GetType();
                    var x = pointType.GetProperty("X")?.GetValue(point);
                    var y = pointType.GetProperty("Y")?.GetValue(point);

                    if (i > 0)
                    {
                        sb.Append(',');
                    }

                    sb.Append($"{x} {y}");
                }
            }

            // Close the polygon by repeating first point (WKT convention)
            var firstPoint = pointsArray.GetValue(0);
            if (firstPoint != null)
            {
                var pointType = firstPoint.GetType();
                var x = pointType.GetProperty("X")?.GetValue(firstPoint);
                var y = pointType.GetProperty("Y")?.GetValue(firstPoint);
                sb.Append($",{x} {y}");
            }

            sb.Append("))");
            parameter.Value = sb.ToString();
            parameter.DbType = DbType.String;
        }
    }

    /// <summary>
    /// Parses a database value back to NpgsqlPolygon.
    /// PostgreSQL: Value is already NpgsqlPolygon from Npgsql.
    /// Other providers: Deserializes from WKT format.
    /// </summary>
    /// <param name="destinationType">The target type (NpgsqlPolygon).</param>
    /// <param name="value">The database value to parse.</param>
    /// <returns>An NpgsqlPolygon instance.</returns>
    public object? Parse(Type destinationType, object value)
    {
        if (value == null || value is DBNull)
        {
            return null;
        }

        var valueType = value.GetType();

        // Check if value is already an NpgsqlPolygon (PostgreSQL native)
        if (valueType.Name == "NpgsqlPolygon")
        {
            return value;
        }

        // Parse from WKT POLYGON format (other providers)
        var wkt = value.ToString() ?? string.Empty;

        // Expected format: "POLYGON((x1 y1, x2 y2, ..., x1 y1))"
        if (!wkt.StartsWith("POLYGON((", StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException($"Invalid WKT format for polygon. Expected 'POLYGON((...))', got: {wkt}");
        }

        var coordsStr = wkt.Substring(9, wkt.Length - 11); // Remove "POLYGON((" and "))"
        var pointStrs = coordsStr.Split(',', StringSplitOptions.RemoveEmptyEntries);

        // Remove the last point if it's the same as the first (WKT convention)
        if (pointStrs.Length > 1)
        {
            pointStrs = pointStrs.Take(pointStrs.Length - 1).ToArray();
        }

        // Create NpgsqlPoint array
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

        var points = Array.CreateInstance(pointType, pointStrs.Length);

        for (var i = 0; i < pointStrs.Length; i++)
        {
            var coords = pointStrs[i].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (coords.Length != 2)
            {
                throw new FormatException($"Invalid coordinate format in polygon WKT at point {i}.");
            }

            var x = double.Parse(coords[0], CultureInfo.InvariantCulture);
            var y = double.Parse(coords[1], CultureInfo.InvariantCulture);
            var point = pointCtor.Invoke(new object[] { x, y });
            points.SetValue(point, i);
        }

        // Create NpgsqlPolygon using reflection
        var polygonType = Type.GetType("NpgsqlTypes.NpgsqlPolygon, Npgsql");
        if (polygonType == null)
        {
            throw new InvalidOperationException("NpgsqlPolygon type not found. Ensure Npgsql package is referenced.");
        }

        var polygonCtor = polygonType.GetConstructor(new[] { points.GetType() });
        if (polygonCtor == null)
        {
            throw new InvalidOperationException("Could not find appropriate constructor for NpgsqlPolygon.");
        }

        return polygonCtor.Invoke(new object[] { points });
    }
}
