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

        // Parse from WKT format (other providers)
        var wkt = value.ToString() ?? string.Empty;

        bool isOpen;
        string coordsStr;

        if (wkt.StartsWith("LINESTRING(", StringComparison.OrdinalIgnoreCase))
        {
            isOpen = true;
            coordsStr = wkt.Substring(11, wkt.Length - 12); // Remove "LINESTRING(" and ")"
        }
        else if (wkt.StartsWith("POLYGON((", StringComparison.OrdinalIgnoreCase))
        {
            isOpen = false;
            coordsStr = wkt.Substring(9, wkt.Length - 11); // Remove "POLYGON((" and "))"
        }
        else
        {
            throw new FormatException(
                $"Invalid WKT format for path. Expected 'LINESTRING(...)' or 'POLYGON((...))', got: {wkt}"
            );
        }

        var pointStrs = coordsStr.Split(',', StringSplitOptions.RemoveEmptyEntries);

        // For closed paths, remove the last point if it's the same as the first (WKT convention)
        if (!isOpen && pointStrs.Length > 1)
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
                throw new FormatException($"Invalid coordinate format in path WKT at point {i}.");
            }

            var x = double.Parse(coords[0], CultureInfo.InvariantCulture);
            var y = double.Parse(coords[1], CultureInfo.InvariantCulture);
            var point = pointCtor.Invoke(new object[] { x, y });
            points.SetValue(point, i);
        }

        // Create NpgsqlPath using reflection
        var pathType = Type.GetType("NpgsqlTypes.NpgsqlPath, Npgsql");
        if (pathType == null)
        {
            throw new InvalidOperationException("NpgsqlPath type not found. Ensure Npgsql package is referenced.");
        }

        var pathCtor = pathType.GetConstructor(new[] { points.GetType(), typeof(bool) });
        if (pathCtor == null)
        {
            throw new InvalidOperationException("Could not find appropriate constructor for NpgsqlPath.");
        }

        return pathCtor.Invoke(new object[] { points, isOpen });
    }
}
