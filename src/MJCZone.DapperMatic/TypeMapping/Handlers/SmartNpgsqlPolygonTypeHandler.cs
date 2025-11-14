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
    private static readonly string[] PointSeparator = new[] { "),(" };

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

        // Parse from string format (other providers)
        var str = value.ToString() ?? string.Empty;

        List<(double x, double y)> points = new();

        // Try PostgreSQL native format: "((x1,y1),(x2,y2),...)"
        if (str.StartsWith('(') && str.EndsWith(')') && str.Length > 2 && str[1] == '(')
        {
            var content = str.Substring(1, str.Length - 2); // Remove outer parentheses
            var pointParts = content.Split(PointSeparator, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in pointParts)
            {
                var cleanPart = part.Trim('(', ')');
                var coords = cleanPart.Split(',', StringSplitOptions.RemoveEmptyEntries);

                if (coords.Length != 2)
                {
                    throw new FormatException(
                        $"Invalid PostgreSQL polygon format. Expected point coordinates, got: {part}"
                    );
                }

                var x = double.Parse(coords[0], CultureInfo.InvariantCulture);
                var y = double.Parse(coords[1], CultureInfo.InvariantCulture);
                points.Add((x, y));
            }
        }
        // Try WKT POLYGON format: "POLYGON((x1 y1, x2 y2, ..., x1 y1))"
        else if (str.StartsWith("POLYGON((", StringComparison.OrdinalIgnoreCase))
        {
            var coordsStr = str.Substring(9, str.Length - 11); // Remove "POLYGON((" and "))"
            var pointStrs = coordsStr.Split(',', StringSplitOptions.RemoveEmptyEntries);

            // Remove the last point if it's the same as the first (WKT convention)
            var pointsToProcess = pointStrs;
            if (pointStrs.Length > 1)
            {
                pointsToProcess = pointStrs.Take(pointStrs.Length - 1).ToArray();
            }

            foreach (var pointStr in pointsToProcess)
            {
                var coords = pointStr.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (coords.Length != 2)
                {
                    throw new FormatException(
                        $"Invalid WKT polygon format. Expected point coordinates, got: {pointStr}"
                    );
                }

                var x = double.Parse(coords[0], CultureInfo.InvariantCulture);
                var y = double.Parse(coords[1], CultureInfo.InvariantCulture);
                points.Add((x, y));
            }
        }
        else
        {
            throw new FormatException($"Invalid polygon format. Expected '((x,y)...)' or 'POLYGON((...))', got: {str}");
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

        var pointsArray = Array.CreateInstance(pointType, points.Count);

        for (var i = 0; i < points.Count; i++)
        {
            var (x, y) = points[i];
            var point = pointCtor.Invoke(new object[] { x, y });
            pointsArray.SetValue(point, i);
        }

        // Create NpgsqlPolygon using reflection
        var polygonType = Type.GetType("NpgsqlTypes.NpgsqlPolygon, Npgsql");
        if (polygonType == null)
        {
            throw new InvalidOperationException("NpgsqlPolygon type not found. Ensure Npgsql package is referenced.");
        }

        var arrayType = pointType.MakeArrayType();
        var polygonCtor = polygonType.GetConstructor(new[] { arrayType });
        if (polygonCtor == null)
        {
            throw new InvalidOperationException("Could not find appropriate constructor for NpgsqlPolygon.");
        }

        return polygonCtor.Invoke(new object[] { pointsArray });
    }
}
