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
/// Smart type handler for NpgsqlPath with provider-specific optimization.
/// PostgreSQL: Native path type
/// Others: WKT (Well-Known Text) format - "LINESTRING(...)" for open paths, "POLYGON((...)))" for closed paths
/// </summary>
public class SmartNpgsqlPathTypeHandler : SqlMapper.ITypeHandler
{
    private static readonly string[] PointSeparator = new[] { "),(" };

    /// <summary>
    /// Sets the parameter value for a path.
    /// PostgreSQL: Passes path directly (Npgsql converts NpgsqlPath to native PostgreSQL path).
    /// Other providers: Serializes path to WKT format.
    /// </summary>
    /// <param name="parameter">The database parameter to set.</param>
    /// <param name="value">The NpgsqlPath value to store.</param>
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
            // PostgreSQL: Native path type handling
            parameter.Value = value;
        }
        else
        {
            // Other providers: WKT serialization
            var valueType = value.GetType();
            var pointsArray = valueType.GetProperty("Points")?.GetValue(value) as Array;
            var isOpen = (bool)(valueType.GetProperty("Open")?.GetValue(value) ?? true);

            if (pointsArray == null || pointsArray.Length == 0)
            {
                parameter.Value = DBNull.Value;
                return;
            }

            var sb = new StringBuilder();

            if (isOpen)
            {
                sb.Append("LINESTRING(");
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
                            sb.Append(", ");
                        }

                        sb.Append($"{x} {y}");
                    }
                }
                sb.Append(')');
            }
            else
            {
                // Closed path - use POLYGON format
                sb.Append("POLYGON((");
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
                // Close the polygon by repeating first point
                var firstPoint = pointsArray.GetValue(0);
                if (firstPoint != null)
                {
                    var pointType = firstPoint.GetType();
                    var x = pointType.GetProperty("X")?.GetValue(firstPoint);
                    var y = pointType.GetProperty("Y")?.GetValue(firstPoint);
                    sb.Append($",{x} {y}");
                }
                sb.Append("))");
            }

            parameter.Value = sb.ToString();
            parameter.DbType = DbType.String;
        }
    }

    /// <summary>
    /// Parses a database value back to NpgsqlPath.
    /// PostgreSQL: Value is already NpgsqlPath from Npgsql.
    /// Other providers: Deserializes from WKT format.
    /// </summary>
    /// <param name="destinationType">The target type (NpgsqlPath).</param>
    /// <param name="value">The database value to parse.</param>
    /// <returns>An NpgsqlPath instance.</returns>
    public object? Parse(Type destinationType, object value)
    {
        if (value == null || value is DBNull)
        {
            return null;
        }

        var valueType = value.GetType();

        // Check if value is already an NpgsqlPath (PostgreSQL native)
        if (valueType.Name == "NpgsqlPath")
        {
            return value;
        }

        // Parse from string format (other providers)
        var str = value.ToString() ?? string.Empty;

        bool isOpen;
        List<(double x, double y)> points = new();

        // Try PostgreSQL native format: "((x1,y1),(x2,y2),...)" or "[(x1,y1),(x2,y2),...]" (open path with brackets)
        if (
            (str.StartsWith('(') && str.EndsWith(')') && str.Length > 2 && str[1] == '(')
            || (str.StartsWith('[') && str.EndsWith(']'))
        )
        {
            // Open paths use [] brackets, closed paths use () parentheses
            isOpen = str.StartsWith('[');

            var content = str.Substring(1, str.Length - 2); // Remove outer brackets/parens
            var pointParts = content.Split(PointSeparator, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in pointParts)
            {
                var cleanPart = part.Trim('(', ')');
                var coords = cleanPart.Split(',', StringSplitOptions.RemoveEmptyEntries);

                if (coords.Length != 2)
                {
                    throw new FormatException(
                        $"Invalid PostgreSQL path format. Expected point coordinates, got: {part}"
                    );
                }

                var x = double.Parse(coords[0], CultureInfo.InvariantCulture);
                var y = double.Parse(coords[1], CultureInfo.InvariantCulture);
                points.Add((x, y));
            }
        }
        // Try WKT format: "LINESTRING(...)" or "POLYGON((...))
        else if (str.StartsWith("LINESTRING(", StringComparison.OrdinalIgnoreCase))
        {
            isOpen = true;
            var coordsStr = str.Substring(11, str.Length - 12); // Remove "LINESTRING(" and ")"
            var pointStrs = coordsStr.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (var pointStr in pointStrs)
            {
                var coords = pointStr.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (coords.Length != 2)
                {
                    throw new FormatException($"Invalid WKT path format. Expected point coordinates, got: {pointStr}");
                }

                var x = double.Parse(coords[0], CultureInfo.InvariantCulture);
                var y = double.Parse(coords[1], CultureInfo.InvariantCulture);
                points.Add((x, y));
            }
        }
        else if (str.StartsWith("POLYGON((", StringComparison.OrdinalIgnoreCase))
        {
            isOpen = false;
            var coordsStr = str.Substring(9, str.Length - 11); // Remove "POLYGON((" and "))"
            var pointStrs = coordsStr.Split(',', StringSplitOptions.RemoveEmptyEntries);

            // For closed paths, remove the last point if it's the same as the first (WKT convention)
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
                    throw new FormatException($"Invalid WKT path format. Expected point coordinates, got: {pointStr}");
                }

                var x = double.Parse(coords[0], CultureInfo.InvariantCulture);
                var y = double.Parse(coords[1], CultureInfo.InvariantCulture);
                points.Add((x, y));
            }
        }
        else
        {
            throw new FormatException($"Invalid path format. Expected '((x,y)...)' or WKT format, got: {str}");
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

        // Create NpgsqlPath using reflection
        var pathType = Type.GetType("NpgsqlTypes.NpgsqlPath, Npgsql");
        if (pathType == null)
        {
            throw new InvalidOperationException("NpgsqlPath type not found. Ensure Npgsql package is referenced.");
        }

        var arrayType = pointType.MakeArrayType();
        var pathCtor = pathType.GetConstructor(new[] { arrayType, typeof(bool) });
        if (pathCtor == null)
        {
            throw new InvalidOperationException("Could not find appropriate constructor for NpgsqlPath.");
        }

        return pathCtor.Invoke(new object[] { pointsArray, isOpen });
    }
}
